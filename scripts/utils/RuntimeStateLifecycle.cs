using System;
using System.Collections.Generic;
using Godot;

internal static class RuntimeStateLifecycle
{
    internal static T MarkFinalizerless<T>(T state, string reason)
        where T : GodotObject
    {
        if (state == null)
            return null;
        RegisterRuntimeStateGraph(state, reason);
        return state;
    }

    internal static void MarkValueGraphFinalizerless(object value, string reason)
    {
        RegisterRuntimeStateValueGraph(value, reason);
    }

    internal static void SuppressRuntimeStateGraphsForFinalizerDrain()
    {
        List<GodotObject> states = GodotWrapperOwnershipRegistry.SnapshotObjects(
            GodotWrapperOwnershipKind.RuntimeState
        );
        foreach (GodotObject state in states)
            RegisterRuntimeStateGraph(state, "finalizer-drain-refresh");

        List<object> wrappers = GodotWrapperOwnershipRegistry.SnapshotWrappers(
            GodotWrapperOwnershipKind.RuntimeState
        );
        TraceRuntimeStateFinalizerDrain(wrappers.Count);
        TraceRuntimeStateWrapperTypes(wrappers);
        foreach (object wrapper in wrappers)
            GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);
        GC.KeepAlive(wrappers);
        GC.KeepAlive(states);
    }

    private static void RegisterRuntimeStateGraph(GodotObject state, string reason)
    {
        RegisterRuntimeStateValueGraph(state, reason);
    }

    private static void RegisterRuntimeStateValueGraph(object value, string reason)
    {
        GodotTypedResourceGraphWalker.VisitValueGraph(value, wrapper =>
        {
            if (wrapper is Node)
            {
                LifecycleViolation.Report(
                    $"Node cannot be part of runtime state graph. type={wrapper.GetType().Name}, reason={reason}"
                );
                return;
            }

            if (GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper))
                return;

            if (GodotWrapperOwnershipRegistry.IsOwnedTransient(wrapper))
            {
                GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);
                return;
            }

            GodotWrapperOwnershipRegistry.Register(
                wrapper,
                GodotWrapperOwnershipKind.RuntimeState,
                owner: null,
                reason: reason
            );
            GodotWrapperOwnershipRegistry.SuppressWrapper(wrapper);
        });
    }

    private static void TraceRuntimeStateFinalizerDrain(int count)
    {
        if (System.Environment.GetEnvironmentVariable("MAGIC_LIFECYCLE_TRACE") != "1")
            return;
        GD.Print($"[lifecycle] suppressing runtime state wrappers: {count}");
    }

    private static void TraceRuntimeStateWrapperTypes(List<object> wrappers)
    {
        if (System.Environment.GetEnvironmentVariable("MAGIC_LIFECYCLE_TRACE") != "1")
            return;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (object wrapper in wrappers)
        {
            string typeName = wrapper?.GetType().Name ?? "<null>";
            counts[typeName] = counts.TryGetValue(typeName, out int count) ? count + 1 : 1;
        }
        var top = new List<KeyValuePair<string, int>>(counts);
        top.Sort((left, right) => right.Value.CompareTo(left.Value));
        int limit = Math.Min(top.Count, 12);
        for (int index = 0; index < limit; index++)
        {
            KeyValuePair<string, int> entry = top[index];
            GD.Print($"[lifecycle] runtime state type {entry.Key}: {entry.Value}");
        }
    }
}
