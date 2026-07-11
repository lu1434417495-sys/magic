using System;
using System.Threading.Tasks;
using Godot;

internal static class LifecycleMeasurementBarrier
{
    internal static async Task RunAsync(SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        for (int wave = 0; wave < GodotObjectLifecycle.FinalizerDrainWaveLimit; wave++)
        {
            GC.Collect();
            long pendingFinalizers = GC.GetGCMemoryInfo().FinalizationPendingCount;
            GC.WaitForPendingFinalizers();
            if (pendingFinalizers == 0)
                break;
        }
        GC.Collect();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
