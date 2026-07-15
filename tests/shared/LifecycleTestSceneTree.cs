using System;
using Godot;

public abstract partial class LifecycleTestSceneTree : SceneTree
{
    private protected void RunAfterProcessStartup(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        void RunOnce()
        {
            ProcessFrame -= RunOnce;
            callback();
        }

        ProcessFrame += RunOnce;
    }

    private protected void RequestTestExit(TestResult result)
    {
        TestResourceOwnership.Close();
        TestExitCoordinator.Complete(this, result);
    }
}
