using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_save_transaction_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPlainWorldMoveStagesWithoutDiskWrite();

        RequestTestExit(_test.Finish("World map save transaction regression"));
    }

    private void TestPlainWorldMoveStagesWithoutDiskWrite()
    {
        Vector2I[] directions =
        {
            Vector2I.Right,
            Vector2I.Left,
            Vector2I.Down,
            Vector2I.Up,
        };

        foreach (Vector2I direction in directions)
        {
            RuntimeContext context = CreateRuntimeContext();
            if (context == null)
            {
                return;
            }

            try
            {
                Dictionary<string, object> originalPayload =
                    ReadActiveSavePayload(context.GameSession);
                Vector2I originalCoord = PayloadPlayerCoord(originalPayload);
                GameRuntimeFacade.RuntimeCommandResult result =
                    context.Facade.CommandWorldMoveTyped(direction, 1);
                bool movedWithoutBoundary =
                    result.Ok
                    && context.Facade.GetPlayerCoord() != originalCoord
                    && context.Facade.GetActiveModalId() == ""
                    && !context.Facade.IsBattleActive();
                if (!movedWithoutBoundary)
                {
                    continue;
                }

                _test.True(context.GameSession.HasPendingSave(), "普通大地图移动后应只标记 pending save。");
                Dictionary<string, object> diskPayload =
                    ReadActiveSavePayload(context.GameSession);
                _test.Eq(
                    PayloadPlayerCoord(diskPayload),
                    originalCoord,
                    "普通大地图移动不应逐步写入磁盘坐标。"
                );
                return;
            }
            finally
            {
                context.Dispose();
            }
        }

        _test.Fail("测试地图应至少存在一个不会打开窗口或战斗的相邻可移动格。");
    }

    private RuntimeContext CreateRuntimeContext()
    {
        GameSession gameSession = new();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, (int)Error.Ok, "大地图保存事务回归前置：应能创建测试存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }

        GameRuntimeFacade facade = new();
        facade.Setup(gameSession);
        return new RuntimeContext(gameSession, facade);
    }

    private static Dictionary<string, object> ReadActiveSavePayload(
        GameSession gameSession
    )
    {
        string savePath = gameSession.GetActiveSavePath();
        if (string.IsNullOrEmpty(savePath))
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        int readError = gameSession.ReadSavePayload(
            savePath,
            out Dictionary<string, object> payload,
            false
        );
        return readError == (int)Error.Ok
            ? payload
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static Vector2I PayloadPlayerCoord(
        IReadOnlyDictionary<string, object> payload
    )
    {
        if (
            payload == null
            || !payload.TryGetValue("world_state", out object worldStateValue)
            || worldStateValue is not IReadOnlyDictionary<string, object> worldState
        )
        {
            return Vector2I.Zero;
        }
        return worldState.TryGetValue("player_coord", out object coordValue)
            && coordValue is Vector2I coord
            ? coord
            : Vector2I.Zero;
    }

    private static void CleanupGameSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.ClearPersistedGame();
        gameSession.Free();
    }

    private sealed class RuntimeContext
    {
        public RuntimeContext(GameSession gameSession, GameRuntimeFacade facade)
        {
            GameSession = gameSession;
            Facade = facade;
        }

        public GameSession GameSession { get; }

        public GameRuntimeFacade Facade { get; }

        public void Dispose()
        {
            Facade?.Dispose();
            CleanupGameSession(GameSession);
        }
    }
}
