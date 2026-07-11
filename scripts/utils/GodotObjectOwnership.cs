using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

internal enum GodotWrapperOwnershipKind
{
    Unknown = 0,
    BorrowedStaticContent,
    DerivedStaticContent,
    OwnedTransientRuntime,
    RuntimeState,
    SceneTreeOwned,
}

internal static class GodotWrapperTypeClassifier
{
    internal static bool IsSupportedDirectWrapper(object value) =>
        value is GodotObject
        || value is Godot.Collections.Array
        || value is Godot.Collections.Dictionary;
}

internal static class GodotWrapperOwnershipRegistry
{
    private sealed class Entry
    {
        public WeakReference<object> Wrapper;
        public GodotWrapperOwnershipKind Kind;
        public WeakReference Owner;
        public string Reason = "";
        public string TypeName = "";
    }

    private static readonly object Sync = new();
    private static readonly ConditionalWeakTable<object, Entry> Entries = new();
    private static readonly List<WeakReference<object>> EntryRefs = new();

    internal static bool Register(
        object wrapper,
        GodotWrapperOwnershipKind kind,
        object owner,
        string reason
    )
    {
        if (wrapper == null)
            return false;
        if (!GodotWrapperTypeClassifier.IsSupportedDirectWrapper(wrapper))
            return false;

        if (wrapper is Node && kind != GodotWrapperOwnershipKind.SceneTreeOwned)
        {
            LifecycleViolation.Report(
                $"Node cannot be registered as {kind}. type={wrapper.GetType().Name}, reason={reason}"
            );
            return false;
        }

        lock (Sync)
        {
            if (Entries.TryGetValue(wrapper, out Entry existing))
            {
                if (existing.Kind == kind && OwnerMatches(existing, owner))
                    return true;
                if (IsStaticContentKind(existing.Kind) && IsStaticContentKind(kind))
                    return true;

                LifecycleViolation.Report(
                    $"Godot wrapper ownership conflict. type={wrapper.GetType().Name}, old={existing.Kind}, new={kind}, old_reason={existing.Reason}, reason={reason}"
                );
                return false;
            }

            var entry = new Entry
            {
                Wrapper = new WeakReference<object>(wrapper),
                Kind = kind,
                Owner = owner != null ? new WeakReference(owner) : null,
                Reason = reason ?? "",
                TypeName = wrapper.GetType().Name,
            };
            Entries.Add(wrapper, entry);
            EntryRefs.Add(entry.Wrapper);
            return true;
        }
    }

    internal static bool TryClaimLeaseOwnership(
        object wrapper,
        object owner,
        string reason,
        out string failure
    )
    {
        failure = string.Empty;
        if (wrapper == null || owner == null)
        {
            failure = "Lease ownership requires a wrapper and owner.";
            return false;
        }
        if (wrapper is Node)
        {
            failure = "A lease cannot own a SceneTree Node.";
            return false;
        }

        lock (Sync)
        {
            if (Entries.TryGetValue(wrapper, out Entry existing))
            {
                failure =
                    $"Godot wrapper already has an owner. type={wrapper.GetType().Name}, old={existing.Kind}, old_reason={existing.Reason}, reason={reason}";
                return false;
            }

            var entry = new Entry
            {
                Wrapper = new WeakReference<object>(wrapper),
                Kind = GodotWrapperOwnershipKind.OwnedTransientRuntime,
                Owner = new WeakReference(owner),
                Reason = reason ?? string.Empty,
                TypeName = wrapper.GetType().Name,
            };
            Entries.Add(wrapper, entry);
            EntryRefs.Add(entry.Wrapper);
            return true;
        }
    }

    internal static bool TryTransferLeaseOwnership(
        object wrapper,
        object sourceOwner,
        object targetOwner,
        string reason,
        out string failure
    )
    {
        failure = string.Empty;
        if (wrapper == null || sourceOwner == null || targetOwner == null)
        {
            failure = "Lease transfer requires a wrapper, source owner, and target owner.";
            return false;
        }

        lock (Sync)
        {
            if (
                !Entries.TryGetValue(wrapper, out Entry entry)
                || entry.Kind != GodotWrapperOwnershipKind.OwnedTransientRuntime
                || !OwnerMatches(entry, sourceOwner)
            )
            {
                failure =
                    $"Lease transfer source does not own the wrapper. type={wrapper.GetType().Name}, reason={reason}";
                return false;
            }

            entry.Owner = new WeakReference(targetOwner);
            entry.Reason = reason ?? string.Empty;
            return true;
        }
    }

    internal static bool TryReleaseLeaseOwnership(
        object wrapper,
        object owner,
        out string failure
    )
    {
        failure = string.Empty;
        if (wrapper == null || owner == null)
        {
            failure = "Lease release requires a wrapper and owner.";
            return false;
        }

        lock (Sync)
        {
            if (
                !Entries.TryGetValue(wrapper, out Entry entry)
                || entry.Kind != GodotWrapperOwnershipKind.OwnedTransientRuntime
                || !OwnerMatches(entry, owner)
            )
            {
                failure =
                    $"Lease release owner does not match. type={wrapper.GetType().Name}";
                return false;
            }

            Entries.Remove(wrapper);
            return true;
        }
    }

    internal static bool TryGetKind(object wrapper, out GodotWrapperOwnershipKind kind)
    {
        kind = GodotWrapperOwnershipKind.Unknown;
        if (wrapper == null)
            return false;

        lock (Sync)
        {
            if (!Entries.TryGetValue(wrapper, out Entry entry))
                return false;
            kind = entry.Kind;
            return true;
        }
    }

    internal static bool IsKnown(object wrapper) => TryGetKind(wrapper, out _);

    internal static bool IsBorrowedStaticContent(object wrapper) =>
        TryGetKind(wrapper, out GodotWrapperOwnershipKind kind)
        && kind == GodotWrapperOwnershipKind.BorrowedStaticContent;

    internal static bool IsDerivedStaticContent(object wrapper) =>
        TryGetKind(wrapper, out GodotWrapperOwnershipKind kind)
        && kind == GodotWrapperOwnershipKind.DerivedStaticContent;

    internal static bool IsBorrowedOrDerivedStaticContent(object wrapper) =>
        TryGetKind(wrapper, out GodotWrapperOwnershipKind kind) && IsStaticContentKind(kind);

    internal static bool IsOwnedTransient(object wrapper) =>
        TryGetKind(wrapper, out GodotWrapperOwnershipKind kind)
        && kind == GodotWrapperOwnershipKind.OwnedTransientRuntime;

    internal static bool IsOwnedTransientByOwner(object wrapper, object owner)
    {
        if (wrapper == null || owner == null)
            return false;

        lock (Sync)
        {
            return Entries.TryGetValue(wrapper, out Entry entry)
                && entry.Kind == GodotWrapperOwnershipKind.OwnedTransientRuntime
                && OwnerMatches(entry, owner);
        }
    }

    internal static bool IsRuntimeState(object wrapper) =>
        TryGetKind(wrapper, out GodotWrapperOwnershipKind kind)
        && kind == GodotWrapperOwnershipKind.RuntimeState;

    internal static List<object> SnapshotWrappers(GodotWrapperOwnershipKind kind)
    {
        var result = new List<object>();
        lock (Sync)
        {
            for (int index = EntryRefs.Count - 1; index >= 0; index--)
            {
                WeakReference<object> weakRef = EntryRefs[index];
                if (!weakRef.TryGetTarget(out object wrapper) || wrapper == null)
                {
                    EntryRefs.RemoveAt(index);
                    continue;
                }
                if (!Entries.TryGetValue(wrapper, out Entry entry) || entry.Kind != kind)
                    continue;
                if (wrapper is GodotObject godotObject && !IsInstanceValid(godotObject))
                    continue;
                result.Add(wrapper);
            }
        }
        return result;
    }

    internal static List<GodotObject> SnapshotObjects(GodotWrapperOwnershipKind kind)
    {
        var result = new List<GodotObject>();
        foreach (object wrapper in SnapshotWrappers(kind))
        {
            if (wrapper is GodotObject godotObject)
                result.Add(godotObject);
        }
        return result;
    }

    internal static void RegisterRuntimeState(GodotObject obj, string reason)
    {
        Register(obj, GodotWrapperOwnershipKind.RuntimeState, owner: null, reason: reason);
    }

    internal static void AssertOwnedTransient(GodotObject obj, string reason)
    {
        if (obj == null || IsOwnedTransient(obj))
            return;
        LifecycleViolation.Report(
            $"Expected owned transient GodotObject. type={obj.GetType().Name}, reason={reason}"
        );
    }

    internal static void AssertBorrowedOrOwnedKnown(Resource resource, string reason)
    {
        if (resource == null)
            return;
        if (
            IsBorrowedOrDerivedStaticContent(resource)
            || IsOwnedTransient(resource)
            || IsRuntimeState(resource)
        )
        {
            return;
        }

        LifecycleViolation.Report(
            $"Unknown Godot Resource ownership. type={resource.GetType().Name}, path={resource.ResourcePath}, reason={reason}"
        );
    }

    private static bool OwnerMatches(Entry entry, object owner)
    {
        if (entry.Owner == null)
            return owner == null;
        if (owner == null)
            return false;
        object existingOwner = entry.Owner.Target;
        return existingOwner != null && ReferenceEquals(existingOwner, owner);
    }

    private static bool IsStaticContentKind(GodotWrapperOwnershipKind kind) =>
        kind
            is GodotWrapperOwnershipKind.BorrowedStaticContent
                or GodotWrapperOwnershipKind.DerivedStaticContent;

    private static bool IsInstanceValid(GodotObject obj)
    {
        try
        {
            return obj != null && GodotObject.IsInstanceValid(obj);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

internal static class GodotObjectOwnershipRegistry
{
    internal static bool Register(
        GodotObject obj,
        GodotWrapperOwnershipKind kind,
        object owner,
        string reason
    )
    {
        return GodotWrapperOwnershipRegistry.Register(obj, kind, owner, reason);
    }

    internal static void RegisterRuntimeState(GodotObject obj, string reason)
    {
        GodotWrapperOwnershipRegistry.RegisterRuntimeState(obj, reason);
    }

    internal static bool IsBorrowedContent(GodotObject obj) =>
        GodotWrapperOwnershipRegistry.IsBorrowedStaticContent(obj);

    internal static bool IsOwnedTransient(GodotObject obj) =>
        GodotWrapperOwnershipRegistry.IsOwnedTransient(obj);

    internal static bool IsRuntimeState(GodotObject obj) =>
        GodotWrapperOwnershipRegistry.IsRuntimeState(obj);

    internal static bool IsBorrowedOrDerivedStaticContent(GodotObject obj) =>
        GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(obj);

    internal static List<GodotObject> SnapshotObjects(GodotWrapperOwnershipKind kind) =>
        GodotWrapperOwnershipRegistry.SnapshotObjects(kind);

    internal static void AssertOwnedTransient(GodotObject obj, string reason) =>
        GodotWrapperOwnershipRegistry.AssertOwnedTransient(obj, reason);

    internal static void AssertBorrowedOrOwnedKnown(Resource resource, string reason) =>
        GodotWrapperOwnershipRegistry.AssertBorrowedOrOwnedKnown(resource, reason);
}

internal static class GodotContentOwnership
{
    private static readonly object StaticContentOwner = new();

    internal static void RegisterBorrowedContent(Resource root, string reason)
    {
        if (root == null)
            return;
        if (string.IsNullOrEmpty(reason))
        {
            LifecycleViolation.Report(
                $"Borrowed static content root must be registered with a non-empty source. type={root.GetType().Name}"
            );
            return;
        }

        RegisterStaticWrapper(
            root,
            GodotWrapperOwnershipKind.BorrowedStaticContent,
            reason
        );
    }

    internal static void RegisterDerivedContent(Resource root, string derivedKey, string reason)
    {
        if (root == null)
            return;
        if (string.IsNullOrEmpty(derivedKey))
        {
            LifecycleViolation.Report(
                $"Derived static content root must have a derived key. type={root.GetType().Name}, reason={reason}"
            );
            return;
        }

        RegisterStaticWrapper(
            root,
            GodotWrapperOwnershipKind.DerivedStaticContent,
            BuildLabel(derivedKey, reason)
        );
    }

    internal static void RegisterDerivedWrapper(object root, string derivedKey, string reason)
    {
        if (root == null)
            return;
        if (string.IsNullOrEmpty(derivedKey))
        {
            LifecycleViolation.Report(
                $"Derived static wrapper root must have a derived key. type={root.GetType().Name}, reason={reason}"
            );
            return;
        }

        RegisterStaticWrapper(
            root,
            GodotWrapperOwnershipKind.DerivedStaticContent,
            BuildLabel(derivedKey, reason)
        );
    }

    internal static bool IsBorrowedContent(Resource resource) =>
        GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(resource);

    internal static bool IsStaticContent(object wrapper) =>
        GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper);

    private static void RegisterStaticWrapper(
        object wrapper,
        GodotWrapperOwnershipKind kind,
        string reason
    )
    {
        if (!GodotWrapperTypeClassifier.IsSupportedDirectWrapper(wrapper))
        {
            LifecycleViolation.Report(
                $"Static content registration requires a direct GodotObject, Array, or Dictionary wrapper. type={wrapper.GetType().Name}, reason={reason}"
            );
            return;
        }
        if (wrapper is Node)
        {
            LifecycleViolation.Report(
                $"Node cannot be registered as static content. type={wrapper.GetType().Name}, reason={reason}"
            );
            return;
        }
        if (GodotWrapperOwnershipRegistry.IsOwnedTransient(wrapper))
        {
            LifecycleViolation.Report(
                $"Owned transient wrapper cannot be registered as static content. type={wrapper.GetType().Name}, reason={reason}"
            );
            return;
        }

        if (!GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper))
        {
            GodotWrapperOwnershipRegistry.Register(wrapper, kind, StaticContentOwner, reason);
        }
    }

    private static string BuildLabel(string key, string reason) =>
        string.IsNullOrEmpty(reason) ? key : $"{reason}:{key}";
}

internal sealed class GodotWrapperReferenceComparer : IEqualityComparer<object>
{
    internal static readonly GodotWrapperReferenceComparer Instance = new();

    private GodotWrapperReferenceComparer() { }

    public new bool Equals(object x, object y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}

internal static class GodotRuntimeResourceOwnership
{
    internal static T MarkOwnedTransient<T>(
        T root,
        GodotTransientResourceScope owner,
        string reason
    )
        where T : Resource
    {
        if (root == null)
            return null;
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (GodotContentOwnership.IsStaticContent(root))
        {
            LifecycleViolation.Report(
                $"Cannot mark static content as owned transient. type={root.GetType().Name}, path={root.ResourcePath}, reason={reason}"
            );
            return root;
        }
        owner.RetainDirectWrapper(root, reason);
        return root;
    }
}

internal sealed class GodotTransientResourceScope : IDisposable
{
    private readonly List<object> _ownedWrappers = new();
    private readonly HashSet<object> _ownedWrapperSet = new(
        GodotWrapperReferenceComparer.Instance
    );
    private bool _closed;

    internal GodotTransientResourceScope(string name)
    {
        Name = string.IsNullOrEmpty(name) ? "unnamed" : name;
    }

    internal string Name { get; }

    internal T Own<T>(T resource, string reason)
        where T : Resource
    {
        ThrowIfClosed();
        if (resource == null)
            return null;
        GodotRuntimeResourceOwnership.MarkOwnedTransient(resource, this, $"{Name}:{reason}");
        return resource;
    }

    internal T OwnWrapper<T>(T wrapper, string reason)
        where T : class
    {
        ThrowIfClosed();
        if (wrapper == null)
            return null;

        RetainDirectWrapper(wrapper, $"{Name}:{reason}");
        return wrapper;
    }

    internal void RetainDirectWrapper(object wrapper, string reason)
    {
        ThrowIfClosed();
        if (wrapper == null || !GodotWrapperTypeClassifier.IsSupportedDirectWrapper(wrapper))
            return;
        if (wrapper is Node)
        {
            LifecycleViolation.Report(
                $"Node cannot be owned by a transient resource scope. type={wrapper.GetType().Name}, reason={reason}"
            );
            return;
        }
        if (wrapper is Resource resource && !string.IsNullOrEmpty(resource.ResourcePath))
        {
            LifecycleViolation.Report(
                $"Transient resource scope cannot own a path-backed Resource. type={resource.GetType().Name}, path={resource.ResourcePath}, reason={reason}"
            );
            return;
        }
        if (wrapper is GodotObject godotObject && !IsValid(godotObject))
        {
            LifecycleViolation.Report(
                $"Transient resource scope cannot own an invalid GodotObject. type={wrapper.GetType().Name}, reason={reason}"
            );
            return;
        }
        if (GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper))
        {
            LifecycleViolation.Report(
                $"Transient resource scope cannot own borrowed content. type={wrapper.GetType().Name}, reason={reason}"
            );
            return;
        }
        if (!_ownedWrapperSet.Add(wrapper))
            return;

        bool registered;
        try
        {
            registered = GodotWrapperOwnershipRegistry.Register(
                wrapper,
                GodotWrapperOwnershipKind.OwnedTransientRuntime,
                this,
                reason
            );
        }
        catch
        {
            _ownedWrapperSet.Remove(wrapper);
            throw;
        }
        if (!registered)
        {
            _ownedWrapperSet.Remove(wrapper);
            return;
        }

        _ownedWrappers.Add(wrapper);
    }

    internal Godot.Collections.Dictionary NewDictionary(string reason)
    {
        return OwnWrapper(new Godot.Collections.Dictionary(), reason);
    }

    internal Godot.Collections.Array NewArray(string reason)
    {
        return OwnWrapper(new Godot.Collections.Array(), reason);
    }

    internal void Close() => Dispose();

    public void Dispose()
    {
        if (_closed)
            return;
        _closed = true;

        var failures = new List<Exception>();
        for (int index = _ownedWrappers.Count - 1; index >= 0; index--)
        {
            object wrapper = _ownedWrappers[index];
            try
            {
                ((IDisposable)wrapper).Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (!GodotWrapperOwnershipRegistry.TryReleaseLeaseOwnership(
                wrapper,
                this,
                out string releaseFailure
            ))
            {
                failures.Add(new InvalidOperationException(releaseFailure));
            }
        }

        _ownedWrappers.Clear();
        _ownedWrapperSet.Clear();

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Transient resource scope disposal failed. owner={Name}",
                failures
            );
        }
    }

    private void ThrowIfClosed()
    {
        if (_closed)
            throw new ObjectDisposedException(nameof(GodotTransientResourceScope), Name);
    }

    private static bool IsValid(GodotObject wrapper)
    {
        try
        {
            return wrapper != null && GodotObject.IsInstanceValid(wrapper);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

internal static class LifecycleViolation
{
    internal static void Report(string message)
    {
        if (ShouldThrow())
            throw new InvalidOperationException(message);
        GD.PushError(message);
    }

    private static bool ShouldThrow()
    {
        return System.Environment.GetEnvironmentVariable("MAGIC_LIFECYCLE_STRICT") == "1";
    }
}
