using System;
using Godot;

internal static class GodotObjectLifecycle
{
    internal const int FinalizerDrainWaveLimit = 16;

    static GodotObjectLifecycle()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            ApplicationLifetimeDiagnostics.RecordProcessExit(
                ApplicationLifetimeDiagnostics.CurrentPhase
            );
    }

    internal static void CollectPendingFinalizers()
    {
        // Authored C# Resources can retain child Resource GC handles until the
        // parent finalizer releases its native RefCounted reference. A later
        // collection is therefore required to discover the next child layer.
        for (int wave = 0; wave < FinalizerDrainWaveLimit; wave++)
        {
            GC.Collect();
            long pendingFinalizers = GC.GetGCMemoryInfo().FinalizationPendingCount;
            GC.WaitForPendingFinalizers();
            if (pendingFinalizers == 0)
                break;
        }
        GC.Collect();
    }

    internal static void DisposeGodotObject(FileAccess owned) => DisposeNativeIoWrapper(owned);

    internal static void DisposeGodotObject(DirAccess owned) => DisposeNativeIoWrapper(owned);

    private static void DisposeNativeIoWrapper(GodotObject owned)
    {
        if (owned == null)
            return;
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
