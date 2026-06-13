using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_save_transaction_regression : SceneTree
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

        Quit(_test.Finish("World map save transaction regression"));
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
                GDictionary originalPayload = ReadActiveSavePayload(context.GameSession);
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
                GDictionary diskPayload = ReadActiveSavePayload(context.GameSession);
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

    private static GDictionary ReadActiveSavePayload(GameSession gameSession)
    {
        string savePath = gameSession.GetActiveSavePath();
        if (string.IsNullOrEmpty(savePath))
        {
            return new GDictionary();
        }

        GDictionary readResult = gameSession.ReadSavePayload(savePath, false);
        if (!readResult.ContainsKey("payload"))
        {
            return new GDictionary();
        }
        return readResult["payload"].AsGodotDictionary();
    }

    private static Vector2I PayloadPlayerCoord(GDictionary payload)
    {
        if (payload == null || !payload.ContainsKey("world_state"))
        {
            return Vector2I.Zero;
        }
        GDictionary worldState = payload["world_state"].AsGodotDictionary();
        return worldState.ContainsKey("player_coord")
            ? worldState["player_coord"].AsVector2I()
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
