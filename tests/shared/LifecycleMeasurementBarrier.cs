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
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
