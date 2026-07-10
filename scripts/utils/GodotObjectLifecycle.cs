using System;
using Godot;

internal static class GodotObjectLifecycle
{
    static GodotObjectLifecycle()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            ApplicationLifetimeDiagnostics.RecordProcessExit(
                ApplicationLifetimeDiagnostics.CurrentPhase
            );
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
        RuntimeStateLifecycle.SuppressRuntimeStateGraphsForFinalizerDrain();
        GodotContentOwnership.RetainStaticContentForFinalizerDrain();
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

}
