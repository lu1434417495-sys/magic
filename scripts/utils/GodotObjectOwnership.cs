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
    TestQuarantine,
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
        if (!GodotTypedResourceGraphWalker.IsGodotWrapper(wrapper))
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

    internal static void SuppressKnown(object wrapper)
    {
        if (wrapper == null)
            return;
        if (!IsKnown(wrapper))
        {
            LifecycleViolation.Report(
                $"Cannot suppress unknown Godot wrapper. type={wrapper.GetType().Name}"
            );
            return;
        }
        SuppressWrapper(wrapper);
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

    internal static void SuppressWrapper(object wrapper)
    {
        if (wrapper == null)
            return;
        try
        {
            GC.SuppressFinalize(wrapper);
            GC.KeepAlive(wrapper);
        }
        catch (Exception)
        {
        }
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
    private static readonly object StaticSync = new();
    private static readonly object StaticContentOwner = new();
    private static readonly HashSet<object> StaticStrongWrappers = new(
        GodotWrapperReferenceComparer.Instance
    );
    private static readonly HashSet<string> StaticStrongKeys = new(StringComparer.Ordinal);

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

        RegisterStaticContent(
            root,
            GodotWrapperOwnershipKind.BorrowedStaticContent,
            reason,
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

        RegisterStaticContent(
            root,
            GodotWrapperOwnershipKind.DerivedStaticContent,
            derivedKey,
            reason
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

        RegisterStaticContentGraph(
            root,
            GodotWrapperOwnershipKind.DerivedStaticContent,
            derivedKey,
            reason
        );
    }

    internal static bool IsBorrowedContent(Resource resource) =>
        GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(resource);

    internal static bool IsStaticContent(object wrapper) =>
        GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper);

    internal static void RetainStaticContentForFinalizerDrain()
    {
        GC.KeepAlive(StaticStrongWrappers);
    }

    internal static void SuppressBorrowedContentForProcessExit()
    {
        List<object> staticWrappers;
        lock (StaticSync)
        {
            staticWrappers = new List<object>(StaticStrongWrappers);
        }

        TraceStaticFinalizerDrain(staticWrappers.Count);
        foreach (object wrapper in staticWrappers)
            GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);

        GC.KeepAlive(StaticStrongWrappers);
    }

    private static void RegisterStaticContent(
        Resource root,
        GodotWrapperOwnershipKind kind,
        string key,
        string reason
    )
    {
        RegisterStaticContentGraph(root, kind, key, reason);
    }

    private static void RegisterStaticContentGraph(
        object root,
        GodotWrapperOwnershipKind kind,
        string key,
        string reason
    )
    {
        string label = string.IsNullOrEmpty(reason) ? key : $"{reason}:{key}";
        string strongKey = $"{kind}:{key}";
        lock (StaticSync)
        {
            StaticStrongKeys.Add(strongKey);
        }
        GodotTypedResourceGraphWalker.VisitValueGraph(root, wrapper =>
        {
            if (wrapper is Node)
            {
                LifecycleViolation.Report(
                    $"Node cannot be part of static content graph. type={wrapper.GetType().Name}, reason={label}"
                );
                return;
            }
            if (GodotWrapperOwnershipRegistry.IsOwnedTransient(wrapper))
            {
                LifecycleViolation.Report(
                    $"Static content graph contains owned transient wrapper. type={wrapper.GetType().Name}, reason={label}"
                );
                return;
            }

            bool registered = true;
            if (!GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper))
            {
                registered = GodotWrapperOwnershipRegistry.Register(wrapper, kind, StaticContentOwner, label);
            }
            if (!registered)
                return;

            lock (StaticSync)
            {
                StaticStrongWrappers.Add(wrapper);
            }
        });
    }

    private static void TraceStaticFinalizerDrain(int count)
    {
        if (System.Environment.GetEnvironmentVariable("MAGIC_LIFECYCLE_TRACE") != "1")
            return;
        GD.Print($"[lifecycle] suppressing static content wrappers: {count}");
    }
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

        GodotTypedResourceGraphWalker.VisitWrappers(root, wrapper =>
        {
            if (wrapper is Node)
            {
                LifecycleViolation.Report(
                    $"Node cannot be part of owned transient resource graph. type={wrapper.GetType().Name}, reason={reason}"
                );
                return;
            }

            if (GodotContentOwnership.IsStaticContent(wrapper))
            {
                LifecycleViolation.Report(
                    $"Owned transient graph contains static content wrapper. root={root.GetType().Name}, child={wrapper.GetType().Name}, reason={reason}"
                );
                return;
            }

            if (wrapper is Resource resource && !string.IsNullOrEmpty(resource.ResourcePath))
            {
                LifecycleViolation.Report(
                    $"Owned transient resource has ResourcePath. type={resource.GetType().Name}, path={resource.ResourcePath}, reason={reason}"
                );
                return;
            }

            if (!GodotWrapperOwnershipRegistry.Register(
                wrapper,
                GodotWrapperOwnershipKind.OwnedTransientRuntime,
                owner,
                reason
            ))
            {
                return;
            }
            owner.RetainWrapper(wrapper);
            GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);
        });
        return root;
    }

    internal static void SuppressOwnedTransientGraph(Resource root)
    {
        if (root == null)
            return;
        GodotWrapperOwnershipRegistry.AssertOwnedTransient(root, "SuppressOwnedTransientGraph");
        GodotTypedResourceGraphWalker.VisitWrappers(root, wrapper =>
        {
            if (GodotWrapperOwnershipRegistry.IsOwnedTransient(wrapper))
                GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);
        });
    }
}

internal static class GodotTestRuntimeQuarantine
{
    private static readonly object Sync = new();
    private static readonly HashSet<object> StrongWrappers = new(GodotWrapperReferenceComparer.Instance);

    internal static void Retain(IEnumerable<object> wrappers, string reason)
    {
        if (wrappers == null)
            return;

        lock (Sync)
        {
            foreach (object wrapper in wrappers)
            {
                if (wrapper == null || !GodotTypedResourceGraphWalker.IsGodotWrapper(wrapper))
                    continue;
                StrongWrappers.Add(wrapper);
                GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);
            }
        }

        if (System.Environment.GetEnvironmentVariable("MAGIC_LIFECYCLE_TRACE") == "1")
        {
            GD.Print(
                $"[lifecycle] quarantined runtime wrappers: {StrongWrappers.Count}, reason={reason}"
            );
        }
    }
}

internal sealed class GodotTransientResourceScope : IDisposable
{
    private readonly List<object> _strongWrappers = new();
    private readonly HashSet<object> _strongWrapperSet = new(GodotWrapperReferenceComparer.Instance);
    private readonly List<object> _rootGraphs = new();
    private readonly HashSet<object> _rootGraphSet = new(GodotWrapperReferenceComparer.Instance);
    private bool _disposed;

    internal GodotTransientResourceScope(string name, bool quarantineOnDrain = false)
    {
        Name = string.IsNullOrEmpty(name) ? "unnamed" : name;
        QuarantineOnDrain = quarantineOnDrain;
    }

    internal string Name { get; }
    internal bool QuarantineOnDrain { get; }

    internal T Own<T>(T resource, string reason)
        where T : Resource
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GodotTransientResourceScope));
        if (resource == null)
            return null;
        RetainRootGraph(resource);
        GodotRuntimeResourceOwnership.MarkOwnedTransient(resource, this, $"{Name}:{reason}");
        return resource;
    }

    internal T OwnWrapper<T>(T wrapper, string reason)
        where T : class
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GodotTransientResourceScope));
        if (wrapper == null)
            return null;

        string label = $"{Name}:{reason}";
        RetainRootGraph(wrapper);
        RegisterOwnedValueGraph(wrapper, label);
        return wrapper;
    }

    internal T OwnValueGraph<T>(T value, string reason)
        where T : class
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GodotTransientResourceScope));
        if (value == null)
            return null;

        string label = $"{Name}:{reason}";
        RetainRootGraph(value);
        RegisterOwnedValueGraph(value, label);
        return value;
    }

    private void RegisterOwnedValueGraph(object value, string label)
    {
        GodotTypedResourceGraphWalker.VisitValueGraph(value, ownedWrapper =>
        {
            if (ownedWrapper is Node)
            {
                LifecycleViolation.Report(
                    $"Node cannot be part of owned transient wrapper graph. type={ownedWrapper.GetType().Name}, reason={label}"
                );
                return;
            }

            if (GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(ownedWrapper))
            {
                return;
            }

            if (GodotWrapperOwnershipRegistry.IsRuntimeState(ownedWrapper))
            {
                RetainWrapper(ownedWrapper);
                GodotWrapperOwnershipRegistry.SuppressWrapper(ownedWrapper);
                return;
            }

            if (GodotWrapperOwnershipRegistry.IsOwnedTransient(ownedWrapper))
            {
                if (GodotWrapperOwnershipRegistry.IsOwnedTransientByOwner(ownedWrapper, this))
                {
                    RetainWrapper(ownedWrapper);
                    GodotWrapperOwnershipRegistry.SuppressWrapper(ownedWrapper);
                    return;
                }

                LifecycleViolation.Report(
                    $"Owned transient wrapper graph contains wrapper owned by another scope. type={ownedWrapper.GetType().Name}, reason={label}"
                );
                return;
            }

            if (
                ownedWrapper is Resource resource
                && !string.IsNullOrEmpty(resource.ResourcePath)
            )
            {
                LifecycleViolation.Report(
                    $"Owned transient wrapper graph contains path-backed resource. type={resource.GetType().Name}, path={resource.ResourcePath}, reason={label}"
                );
                return;
            }

            if (!GodotWrapperOwnershipRegistry.Register(
                ownedWrapper,
                GodotWrapperOwnershipKind.OwnedTransientRuntime,
                this,
                label
            ))
            {
                return;
            }
            RetainWrapper(ownedWrapper);
            GodotWrapperOwnershipRegistry.SuppressWrapper(ownedWrapper);
        });
    }

    internal Godot.Collections.Dictionary NewDictionary(string reason)
    {
        return OwnWrapper(new Godot.Collections.Dictionary(), reason);
    }

    internal Godot.Collections.Array NewArray(string reason)
    {
        return OwnWrapper(new Godot.Collections.Array(), reason);
    }

    internal void RetainWrapper(object wrapper)
    {
        if (wrapper == null || !_strongWrapperSet.Add(wrapper))
            return;
        _strongWrappers.Add(wrapper);
    }

    private void RetainRootGraph(object root)
    {
        if (root == null || !_rootGraphSet.Add(root))
            return;
        _rootGraphs.Add(root);
    }

    internal void Drain()
    {
        foreach (object wrapper in _strongWrappers)
            GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);

        if (QuarantineOnDrain)
            GodotTestRuntimeQuarantine.Retain(_strongWrappers, Name);

        _strongWrappers.Clear();
        _strongWrapperSet.Clear();
        _rootGraphs.Clear();
        _rootGraphSet.Clear();
    }

    internal void Close() => Drain();

    public void Dispose()
    {
        if (_disposed)
            return;
        Drain();
        _disposed = true;
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
