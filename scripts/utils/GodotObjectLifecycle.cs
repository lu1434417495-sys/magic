using System;
using Godot;

internal static class GodotObjectLifecycle
{
    static GodotObjectLifecycle()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => PrepareForFinalizerDrain();
    }

    internal static void CollectPendingFinalizers()
    {
        PrepareForFinalizerDrain();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    internal static void PrepareForFinalizerDrain()
    {
        GodotContentOwnership.SuppressBorrowedContentForFinalizerDrain();
        RuntimeStateLifecycle.SuppressRuntimeStateGraphsForFinalizerDrain();
        SuppressGameSessionContentForFinalizerDrain();
    }

    internal static void DisposeGodotObject(FileAccess owned) => DisposeNativeIoWrapper(owned);

    internal static void DisposeGodotObject(DirAccess owned) => DisposeNativeIoWrapper(owned);

    private static void DisposeNativeIoWrapper(GodotObject owned)
    {
        if (owned == null)
            return;
        GC.SuppressFinalize(owned);
        try
        {
            if (GodotObject.IsInstanceValid(owned))
                owned.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal static void SuppressFinalizerGraph(GodotObject root)
    {
        SuppressFinalizersInValue(root);
    }

    internal static void SuppressTransientResourceFinalizerGraph(Resource resource)
    {
        if (resource == null)
            return;
        GodotRuntimeResourceOwnership.SuppressOwnedTransientGraph(resource);
    }

    internal static void SuppressFinalizersInValue(object value)
    {
        GodotTypedResourceGraphWalker.VisitValueGraph(
            value,
            GodotWrapperOwnershipRegistry.SuppressWrapper
        );
    }

    private static void SuppressGameSessionContentForFinalizerDrain()
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
                return;
            Node root = tree.Root;
            if (root == null)
                return;
            foreach (Node child in root.GetChildren())
            {
                if (child is GameSession session)
                    session.SuppressContentFinalizersForFinalizerDrain();
            }
        }
        catch (Exception)
        {
        }
    }
}
