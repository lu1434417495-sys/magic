using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;

internal sealed record ContentSnapshotBuildArtifact(ContentSnapshot Snapshot);

/// <summary>
/// Pure publication state used by the process host. Keeping projection commit
/// separate from JSON projection makes failed-build rollback independently
/// verifiable without creating a second process host in the same Godot process.
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
/// Process-level owner for immutable content snapshot publication and the typed
/// engine-asset catalog. Gameplay authoring is loaded directly from JSON by registries.
/// </summary>
internal sealed class ProcessContentHost : IDisposable
{
    private static readonly object ProcessHostSync = new();
    private static bool _processHostCreated;
    private static long _lastPublishedEpoch;

    private readonly Dictionary<string, WeakReference<object>> _snapshotBorrowers =
        new(StringComparer.Ordinal);
    private readonly Func<long, ContentSnapshotBuildArtifact> _build;
    private readonly ContentSnapshotPublication _publication = new();
    private readonly bool _ownsEngineAssets;
    private readonly bool _publishesProcessEpoch;
    private bool _acceptingBuilds = true;
    private bool _disposed;

    internal ProcessContentHost(
        Func<long, ContentSnapshotBuildArtifact> build = null
    )
        : this(
            build,
            engineAssets: null,
            claimProcessHost: true,
            ownsEngineAssets: true,
            publishesProcessEpoch: true
        )
    {
    }

    private ProcessContentHost(
        Func<long, ContentSnapshotBuildArtifact> build,
        EngineAssetResolver engineAssets,
        bool claimProcessHost,
        bool ownsEngineAssets,
        bool publishesProcessEpoch
    )
    {
        if (claimProcessHost)
        {
            lock (ProcessHostSync)
            {
                if (_processHostCreated)
                {
                    throw new InvalidOperationException(
                        "Only one ProcessContentHost may be created in a Godot process. "
                            + "Use a pure managed synthetic ContentSnapshot for isolated tests."
                    );
                }
                _processHostCreated = true;
            }
        }

        _build = build ?? BuildDefaultSnapshot;
        EngineAssets = engineAssets ?? new EngineAssetResolver();
        _ownsEngineAssets = ownsEngineAssets;
        _publishesProcessEpoch = publishesProcessEpoch;
    }

    internal static ProcessContentHost CreateSyntheticPublicationProbeForTest(
        Func<long, ContentSnapshotBuildArtifact> build,
        EngineAssetResolver borrowedEngineAssets
    )
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(borrowedEngineAssets);
        return new ProcessContentHost(
            build,
            borrowedEngineAssets,
            claimProcessHost: false,
            ownsEngineAssets: false,
            publishesProcessEpoch: false
        );
    }

    internal long Epoch => _publication.Epoch;
    internal bool IsSealed => _publication.IsSealed;
    internal bool HasSnapshot => _publication.HasSnapshot;
    internal int RollbackAttemptCountForTest { get; private set; }
    internal EngineAssetResolver EngineAssets { get; }

    internal ContentSnapshot BuildAndSeal()
    {
        ThrowIfDisposed();
        if (_publication.HasSnapshot)
            return _publication.GetSnapshot();
        if (IsSealed)
            throw new InvalidOperationException("A sealed content host has no published snapshot.");
        if (!_acceptingBuilds)
            throw new InvalidOperationException("Process content cannot build after quiescing begins.");

        EngineAssetCatalogBootstrap.LoadAndPublish(EngineAssets);
        long candidateEpoch = Interlocked.Read(ref _lastPublishedEpoch) + 1;
        return _publication.BuildAndSeal(
            candidateEpoch,
            () => ValidateIconAssetsForPublication(
                _build(candidateEpoch),
                EngineAssets
            ),
            () => RollbackAttemptCountForTest++,
            PublishProcessEpochIfOwned,
            SetActiveProcessEpochIfOwned
        );
    }

    internal static ContentSnapshotBuildArtifact ValidateIconAssetsForPublication(
        ContentSnapshotBuildArtifact artifact,
        EngineAssetResolver engineAssets
    )
    {
        if (artifact?.Snapshot == null)
            return artifact;
        ContentIconAssetCatalogValidator.ThrowIfInvalid(
            artifact.Snapshot.Skills,
            artifact.Snapshot.Items,
            engineAssets
        );
        EnemySpriteAssetCatalogValidator.ThrowIfInvalid(
            artifact.Snapshot.EnemyTemplates,
            engineAssets
        );
        return artifact;
    }

    internal ContentSnapshot GetSnapshot()
    {
        ThrowIfDisposed();
        return _publication.GetSnapshot();
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
        _acceptingBuilds = false;
        if (_ownsEngineAssets)
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
        if (_publishesProcessEpoch)
            LifecycleAuditRegistry.Shared.ClearActiveContentSnapshotEpoch();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ReleaseSnapshot();
        _disposed = true;
        _acceptingBuilds = false;
        if (_ownsEngineAssets)
            EngineAssets.Dispose();
    }

    internal IReadOnlyList<string> GetSnapshotBorrowerDiagnostics()
    {
        ThrowIfDisposed();
        return _snapshotBorrowers.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static ContentSnapshotBuildArtifact BuildDefaultSnapshot(long epoch)
    {
        var builder = new ContentSnapshotBuilder();
        ContentSnapshot snapshot = builder.Build(epoch);
        return new ContentSnapshotBuildArtifact(snapshot);
    }

    private void PublishProcessEpochIfOwned(long epoch)
    {
        if (_publishesProcessEpoch)
            Interlocked.Exchange(ref _lastPublishedEpoch, epoch);
    }

    private void SetActiveProcessEpochIfOwned(long epoch)
    {
        if (_publishesProcessEpoch)
            LifecycleAuditRegistry.Shared.SetActiveContentSnapshotEpoch(epoch);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
