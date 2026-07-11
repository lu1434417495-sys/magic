using System;
using System.Threading.Tasks;
using Godot;

internal static class TestExitCoordinator
{
    internal static async void Complete(SceneTree tree, TestResult result)
    {
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await SubmitAsync(tree, result);
    }

    internal static ValueTask<ShutdownReport> SubmitAsync(SceneTree tree, TestResult result)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(result);

        foreach (string failure in result.Failures)
            GD.PushError(FormatFailureDiagnostic(result.Label, failure));

        var coordinator = tree.Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        return coordinator.RequestShutdownAsync(
            new ShutdownRequest(
                result.ExitCode,
                ShutdownReason.TestComplete,
                new ShutdownCallerResult(result.Label, result.Passed)
            )
        );
    }

    internal static string FormatFailureDiagnostic(string label, string failure) =>
        $"[test] {label}: {failure}";
}
