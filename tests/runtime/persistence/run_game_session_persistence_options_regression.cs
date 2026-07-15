using System;
using System.Diagnostics;
using Godot;

public partial class run_game_session_persistence_options_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestProductionDefaultsWithoutTouchingProductionStorage();
        TestLifecycleSoakRootValidation();
        TestLifecycleSoakRootsAreIsolated();

        RequestTestExit(_test.Finish("GameSession persistence options regression"));
    }

    private void TestProductionDefaultsWithoutTouchingProductionStorage()
    {
        GameSessionPersistenceOptions production =
            GameSessionPersistenceOptions.Production;

        _test.Eq(
            production.SaveDirectory,
            "user://saves",
            "生产 GameSession 的 save directory 必须保持 user://saves。"
        );
        _test.Eq(
            production.SaveIndexPath,
            "user://saves/index.dat",
            "生产 GameSession 的 save index 必须保持 user://saves/index.dat。"
        );

        GameSession productionSession = null;
        try
        {
            productionSession = new GameSession();
            _test.Eq(
                productionSession.BuildSaveFilePath("production-contract"),
                "user://saves/production-contract.dat",
                "GameSession 无参构造必须委托 production options。"
            );
        }
        finally
        {
            productionSession?.Dispose();
        }
    }

    private void TestLifecycleSoakRootValidation()
    {
        const string validRunId = "Run-42";
        GameSessionPersistenceOptions options =
            GameSessionPersistenceOptions.ForLifecycleSoak(validRunId);

        _test.Eq(
            options.SaveDirectory,
            "user://lifecycle_soak/Run-42",
            "lifecycle soak save directory 应位于对应 run root。"
        );
        _test.Eq(
            options.SaveIndexPath,
            "user://lifecycle_soak/Run-42/index.dat",
            "lifecycle soak index 应位于同一个 run root。"
        );
        _test.True(
            options.SaveIndexPath.StartsWith(
                $"{options.SaveDirectory}/",
                StringComparison.Ordinal
            ),
            "lifecycle soak index 不得逃出其 save directory。"
        );

        AssertInvalidRunId(null, "null");
        AssertInvalidRunId("", "empty");
        AssertInvalidRunId("   ", "whitespace");
        AssertInvalidRunId("..", "dot traversal");
        AssertInvalidRunId("../escape", "forward-slash traversal");
        AssertInvalidRunId("..\\escape", "backslash traversal");
        AssertInvalidRunId("run/id", "forward slash");
        AssertInvalidRunId("run\\id", "backslash");
        AssertInvalidRunId("run_id", "underscore");
        AssertInvalidRunId("run.id", "period");
        AssertInvalidRunId("run:id", "colon");
    }

    private void TestLifecycleSoakRootsAreIsolated()
    {
        string suffix =
            $"{Process.GetCurrentProcess().Id}-{Guid.NewGuid():N}"[..16];
        GameSessionPersistenceOptions optionsA =
            GameSessionPersistenceOptions.ForLifecycleSoak($"persistence-a-{suffix}");
        GameSessionPersistenceOptions optionsB =
            GameSessionPersistenceOptions.ForLifecycleSoak($"persistence-b-{suffix}");
        GameSession sessionA = null;
        GameSession sessionB = null;

        try
        {
            sessionA = new GameSession(optionsA);
            sessionB = new GameSession(optionsB);

            _test.Eq(
                (Error)sessionA.ClearPersistedGame(),
                Error.Ok,
                "A 隔离根前置清理应成功。"
            );
            _test.Eq(
                (Error)sessionB.ClearPersistedGame(),
                Error.Ok,
                "B 隔离根前置清理应成功。"
            );
            _test.Eq(
                sessionA.BuildSaveFilePath("slot-a"),
                $"{optionsA.SaveDirectory}/slot-a.dat",
                "A save path 应使用注入的 save directory。"
            );
            _test.Eq(
                sessionB.BuildSaveFilePath("slot-b"),
                $"{optionsB.SaveDirectory}/slot-b.dat",
                "B save path 应使用注入的 save directory。"
            );

            _test.Eq(
                sessionA.ListSaveSlotsPlain().Count,
                0,
                "A fresh 隔离根不应包含存档槽。"
            );
            _test.Eq(
                sessionB.ListSaveSlotsPlain().Count,
                0,
                "B fresh 隔离根不应包含存档槽。"
            );
            _test.True(
                FileAccess.FileExists(optionsA.SaveIndexPath),
                "A index 应写入 A 的注入路径。"
            );
            _test.True(
                FileAccess.FileExists(optionsB.SaveIndexPath),
                "B index 应写入 B 的注入路径。"
            );

            _test.Eq(
                (Error)sessionA.ClearPersistedGame(),
                Error.Ok,
                "清理 A 隔离根应成功。"
            );
            _test.False(
                DirAccess.DirExistsAbsolute(
                    ProjectSettings.GlobalizePath(optionsA.SaveDirectory)
                ),
                "清理 A 后 A 根应被移除。"
            );
            _test.True(
                FileAccess.FileExists(optionsB.SaveIndexPath),
                "清理 A 不得删除或重写 B 的 index。"
            );
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"lifecycle soak 持久化隔离验证不应抛异常。| error={exception}"
            );
        }
        finally
        {
            if (sessionA != null)
            {
                _ = sessionA.ClearPersistedGame();
                sessionA.Dispose();
            }
            if (sessionB != null)
            {
                _ = sessionB.ClearPersistedGame();
                sessionB.Dispose();
            }
        }
    }

    private void AssertInvalidRunId(string runId, string label)
    {
        try
        {
            _ = GameSessionPersistenceOptions.ForLifecycleSoak(runId);
            _test.Fail($"非法 lifecycle soak run ID 应被拒绝。| case={label}");
        }
        catch (ArgumentException exception)
        {
            _test.Eq(
                exception.ParamName,
                "runId",
                $"非法 run ID 应报告 runId 参数。| case={label}"
            );
        }
    }
}
