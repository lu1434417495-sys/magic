using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_save_transaction_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPlainWorldMoveStagesWithoutDiskWrite();

        if (_failures.Count == 0)
        {
            GD.Print("World map save transaction regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map save transaction regression: FAIL ({_failures.Count})");
        Quit(1);
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
                GDictionary result = context.Facade.command_world_move(direction, 1);
                bool movedWithoutBoundary =
                    DictBool(result, "ok", false)
                    && context.Facade.get_player_coord() != originalCoord
                    && context.Facade.get_active_modal_id() == ""
                    && !context.Facade.is_battle_active();
                if (!movedWithoutBoundary)
                {
                    continue;
                }

                AssertTrue(context.GameSession.has_pending_save(), "普通大地图移动后应只标记 pending save。");
                GDictionary diskPayload = ReadActiveSavePayload(context.GameSession);
                AssertEq(
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

        _failures.Add("测试地图应至少存在一个不会打开窗口或战斗的相邻可移动格。");
    }

    private RuntimeContext CreateRuntimeContext()
    {
        GameSession gameSession = new();
        int createError = gameSession.create_new_save(TestWorldConfig);
        AssertEq(createError, (int)Error.Ok, "大地图保存事务回归前置：应能创建测试存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }

        GameRuntimeFacade facade = new();
        facade.setup(gameSession);
        return new RuntimeContext(gameSession, facade);
    }

    private static GDictionary ReadActiveSavePayload(GameSession gameSession)
    {
        string savePath = gameSession.get_active_save_path();
        if (string.IsNullOrEmpty(savePath))
        {
            return new GDictionary();
        }

        GDictionary readResult = gameSession._read_save_payload(savePath, false);
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

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static void CleanupGameSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.clear_persisted_game();
        gameSession.Free();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
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
            Facade?.dispose();
            CleanupGameSession(GameSession);
        }
    }
}
