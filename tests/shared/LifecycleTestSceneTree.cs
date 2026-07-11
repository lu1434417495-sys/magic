using Godot;

public abstract partial class LifecycleTestSceneTree : SceneTree
{
    private protected void RequestTestExit(TestResult result) =>
        TestExitCoordinator.Complete(this, result);
}
