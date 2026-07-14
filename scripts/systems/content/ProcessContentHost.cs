using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;

internal sealed record ContentSnapshotBuildArtifact(ContentSnapshot Snapshot);

/// <summary>
/// Pure publication state used by the process host. Keeping projection commit
/// separate from Resource loading makes failed-build rollback independently
/// verifiable without creating a second raw host in the same Godot process.
/// </summary>
internal sealed class ContentSnapshotPublication
{
    private ContentSnapshot _snapshot;

    internal long Epoch { get; private set; }
    internal bool IsSealed { get; private set; }
    internal bool HasSnapshot => _snapshot != null;

    internal ContentSnapshot BuildAndSeal(
        long candidateEpoch,
        Func<ContentSnapshotBuildArtifact> project,
        Action rollBackAttempt,
        Action<long> publishEpoch,
        Action<long> onPublished
    )
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rollBackAttempt);
        ArgumentNullException.ThrowIfNull(publishEpoch);
        ArgumentNullException.ThrowIfNull(onPublished);
        if (_snapshot != null)
            return _snapshot;
        if (IsSealed)
            throw new InvalidOperationException("A sealed content host has no published snapshot.");

        try
        {
            ContentSnapshotBuildArtifact artifact = project()
                ?? throw new InvalidOperationException("Content snapshot builder returned no artifact.");
            ContentSnapshot snapshot = artifact.Snapshot
                ?? throw new InvalidOperationException("Content snapshot builder returned no snapshot.");
            if (snapshot.Epoch != candidateEpoch)
            {
                throw new InvalidOperationException(
                    $"Content snapshot epoch mismatch. expected={candidateEpoch}, actual={snapshot.Epoch}"
                );
            }

            publishEpoch(candidateEpoch);
            Epoch = candidateEpoch;
            _snapshot = snapshot;
            IsSealed = true;
            onPublished(candidateEpoch);
            return snapshot;
        }
        catch
        {
            rollBackAttempt();
            Epoch = 0;
            _snapshot = null;
            IsSealed = false;
            throw;
        }
    }

    internal ContentSnapshot GetSnapshot() =>
        _snapshot
        ?? throw new InvalidOperationException("No process content snapshot is active.");

    internal void Release()
    {
        _snapshot = null;
    }
}

/// <summary>
/// The only managed process-level anchor for path-backed authored content.
/// Godot retains native RefCounted ownership; this host keeps one uncached canonical
/// managed root wrapper reachable until the shutdown content-release phase.
/// </summary>
internal sealed class ProcessContentHost : IContentResourceLoader, IDisposable
{
    private static readonly object ProcessHostSync = new();
    private static bool _processHostCreated;
    private static long _lastPublishedEpoch;

    private readonly Dictionary<string, Resource> _roots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WeakReference<object>> _snapshotBorrowers =
        new(StringComparer.Ordinal);
    private readonly Func<IContentResourceLoader, long, ContentSnapshotBuildArtifact> _build;
    private readonly ContentSnapshotPublication _publication = new();
    private bool _acceptingLoads = true;
    private bool _disposed;

    internal ProcessContentHost(
        Func<IContentResourceLoader, long, ContentSnapshotBuildArtifact> build = null
    )
    {
        lock (ProcessHostSync)
        {
            if (_processHostCreated)
            {
                throw new InvalidOperationException(
                    "Only one raw ProcessContentHost may be created in a Godot process. "
                        + "Use a pure managed synthetic ContentSnapshot for isolated tests."
                );
            }
            _processHostCreated = true;
        }

        _build = build ?? BuildDefaultSnapshot;
        EngineAssets = new EngineAssetResolver();
    }

    internal long Epoch => _publication.Epoch;
    internal bool IsSealed => _publication.IsSealed;
    internal int CanonicalRootCount => _roots.Count;
    internal EngineAssetResolver EngineAssets { get; }
    public T LoadCanonical<T>(string resourcePath)
        where T : Resource
    {
        ThrowIfDisposed();
        if (!_acceptingLoads || IsSealed)
        {
            throw new InvalidOperationException(
                "Authored content cannot be loaded after the process content host is sealed or quiescing."
            );
        }

        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        if (_roots.TryGetValue(canonicalPath, out Resource existing))
        {
            return existing is T typed
                ? typed
                : throw new InvalidOperationException(
                    $"Canonical content root {canonicalPath} was loaded as "
                        + $"{existing.GetType().Name}, not {typeof(T).Name}."
                );
        }

        // The host root map is the managed process anchor. Ignore the engine's
        // deep cache so authored C# Resource graphs can drain before GDMono teardown.
        T loaded = ResourceLoader.Load<T>(
            canonicalPath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        if (loaded == null)
        {
            throw new InvalidOperationException(
                $"Failed to load canonical content root {canonicalPath} as {typeof(T).Name}."
            );
        }

        _roots.Add(canonicalPath, loaded);
        GodotWrapperOwnershipRegistry.Register(
            loaded,
            GodotWrapperOwnershipKind.BorrowedStaticContent,
            this,
            canonicalPath
        );
        LifecycleAuditRegistry.Shared.RegisterProcessContentRoot(
            canonicalPath,
            loaded.GetType(),
            loaded
        );
        return loaded;
    }

    internal ContentSnapshot BuildAndSeal()
    {
        ThrowIfDisposed();
        if (_publication.HasSnapshot)
            return _publication.GetSnapshot();
        if (IsSealed)
            throw new InvalidOperationException("A sealed content host has no published snapshot.");
        if (!_acceptingLoads)
            throw new InvalidOperationException("Process content cannot build after quiescing begins.");

        var baselinePaths = new HashSet<string>(_roots.Keys, StringComparer.Ordinal);
        long candidateEpoch = Interlocked.Read(ref _lastPublishedEpoch) + 1;
        return _publication.BuildAndSeal(
            candidateEpoch,
            () => _build(this, candidateEpoch),
            () => RollBackAttemptRoots(baselinePaths),
            epoch => Interlocked.Exchange(ref _lastPublishedEpoch, epoch),
            epoch => LifecycleAuditRegistry.Shared.SetActiveContentSnapshotEpoch(epoch)
        );
    }

    internal ContentSnapshot GetSnapshot()
    {
        ThrowIfDisposed();
        return _publication.GetSnapshot();
    }

    internal IReadOnlyList<ContentRootDiagnostic> GetCanonicalRootDiagnostics()
    {
        ThrowIfDisposed();
        return _roots
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry =>
                new ContentRootDiagnostic(
                    entry.Key,
                    entry.Value.GetType().FullName ?? entry.Value.GetType().Name,
                    ReferenceRole.Borrowed
                )
            )
            .ToArray();
    }

    internal void RegisterSnapshotBorrower(string borrowerId, object borrower)
    {
        ThrowIfDisposed();
        _ = GetSnapshot();
        if (string.IsNullOrWhiteSpace(borrowerId))
            throw new ArgumentException("Snapshot borrower ID is required.", nameof(borrowerId));
        ArgumentNullException.ThrowIfNull(borrower);
        if (_snapshotBorrowers.ContainsKey(borrowerId))
            throw new InvalidOperationException($"Snapshot borrower is already active. id={borrowerId}");

        _snapshotBorrowers.Add(borrowerId, new WeakReference<object>(borrower));
        LifecycleAuditRegistry.Shared.RegisterActive(
            LifecycleAuditActiveKind.ContentBorrower,
            borrowerId,
            LifetimeDomain.Session.ToString(),
            borrower
        );
    }

    internal void UnregisterSnapshotBorrower(string borrowerId)
    {
        if (string.IsNullOrWhiteSpace(borrowerId) || !_snapshotBorrowers.Remove(borrowerId))
            return;
        LifecycleAuditRegistry.Shared.UnregisterActive(
            LifecycleAuditActiveKind.ContentBorrower,
            borrowerId,
            LifetimeDomain.Session.ToString()
        );
    }

    internal void Quiesce()
    {
        if (_disposed)
            return;
        _acceptingLoads = false;
        EngineAssets.Quiesce();
    }

    internal void ReleaseSnapshot()
    {
        if (_disposed || !_publication.HasSnapshot)
            return;
        if (_snapshotBorrowers.Count != 0)
        {
            string message =
                "Process content snapshot cannot be released while borrowers remain active: "
                + string.Join(",", GetSnapshotBorrowerDiagnostics());
            LifecycleViolation.Report(message);
            throw new InvalidOperationException(message);
        }

        _publication.Release();
        LifecycleAuditRegistry.Shared.ClearActiveContentSnapshotEpoch();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseSnapshot();
        _disposed = true;
        _acceptingLoads = false;
        EngineAssets.Dispose();
        foreach (string canonicalPath in _roots.Keys)
            LifecycleAuditRegistry.Shared.ReleaseProcessContentRoot(canonicalPath);
        _roots.Clear();
    }

    internal IReadOnlyList<string> GetSnapshotBorrowerDiagnostics()
    {
        ThrowIfDisposed();
        return _snapshotBorrowers.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static ContentSnapshotBuildArtifact BuildDefaultSnapshot(
        IContentResourceLoader loader,
        long epoch
    )
    {
        var builder = new ContentSnapshotBuilder(loader);
        ContentSnapshot snapshot = builder.Build(epoch);
        return new ContentSnapshotBuildArtifact(snapshot);
    }

    private void RollBackAttemptRoots(HashSet<string> baselinePaths)
    {
        string[] createdPaths = _roots.Keys
            .Where(path => !baselinePaths.Contains(path))
            .ToArray();
        foreach (string canonicalPath in createdPaths)
        {
            _roots.Remove(canonicalPath);
            LifecycleAuditRegistry.Shared.ReleaseProcessContentRoot(canonicalPath);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
