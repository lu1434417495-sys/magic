using Godot;

internal static class TestExitCoordinator
{
    internal static async void Complete(SceneTree tree, TestResult result)
    {
        var coordinator = tree.Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        await coordinator.RequestShutdownAsync(
            new ShutdownRequest(
                result.ExitCode,
                ShutdownReason.TestComplete,
                new ShutdownCallerResult(result.Label, result.Passed)
            )
        );
    }
}
