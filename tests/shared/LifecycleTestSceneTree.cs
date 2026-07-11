using Godot;

public abstract partial class LifecycleTestSceneTree : SceneTree
{
    private protected void RequestTestExit(TestResult result)
    {
        TestResourceOwnership.Close();
        TestExitCoordinator.Complete(this, result);
    }
}
