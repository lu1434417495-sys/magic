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
        // Finalizing a RefCounted authored root can release nested C# Resource
        // handles one layer at a time. Drain a bounded number of waves while
        // Godot is still alive, then finish with a collection-only pass.
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
