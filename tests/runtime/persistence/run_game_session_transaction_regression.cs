using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_session_transaction_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSettersStageRuntimeWithoutDiskWrite();
        TestCommitRuntimeStatePersistsCompleteSnapshot();
        TestCommitFailureKeepsDirtyAndLastError();
        TestUnloadCommitsPendingRuntimeState();

        RequestTestExit(_test.Finish("GameSession transaction regression"));
    }

    private void TestSettersStageRuntimeWithoutDiskWrite()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "事务 setter 回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
            {
                return;
            }

            Dictionary<string, object> originalPayload = ReadActiveSavePayload(gameSession);
            Vector2I originalCoord = PayloadPlayerCoord(originalPayload);
            Vector2I stagedCoord = originalCoord + Vector2I.Right;

            Error setError = (Error)gameSession.SetPlayerCoord(stagedCoord);
            _test.Eq(setError, Error.Ok, "set_player_coord 应只更新运行时并标记 dirty。");
            _test.Eq(gameSession.GetPlayerCoord(), stagedCoord, "set_player_coord 后内存坐标应立即更新。");
            _test.True(gameSession.HasPendingSave(), "setter 更新后应存在 pending save。");

            Dictionary<string, object> diskPayload = ReadActiveSavePayload(gameSession);
            _test.Eq(PayloadPlayerCoord(diskPayload), originalCoord, "未 commit 前 setter 不应把玩家坐标写入磁盘。");
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestCommitRuntimeStatePersistsCompleteSnapshot()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "事务 commit 回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
            {
                return;
            }

            Dictionary<string, object> originalPayload = ReadActiveSavePayload(gameSession);
            Vector2I originalCoord = PayloadPlayerCoord(originalPayload);
            int originalWorldStep = PayloadWorldStep(originalPayload);
            Vector2I stagedCoord = originalCoord + Vector2I.Right;
            GDictionary stagedWorldData = (GDictionary)gameSession.GetWorldData().Duplicate(true);
            stagedWorldData["world_step"] = originalWorldStep + 7;

            _test.Eq((Error)gameSession.SetPlayerCoord(stagedCoord), Error.Ok, "事务 commit 回归前置：坐标 staging 应成功。");
            _test.Eq((Error)gameSession.SetWorldData(stagedWorldData), Error.Ok, "事务 commit 回归前置：world_data staging 应成功。");

            Error commitError = (Error)gameSession.CommitRuntimeState(new StringName("test.full_snapshot"));
            _test.Eq(commitError, Error.Ok, "commit_runtime_state 应一次性持久化完整运行时快照。");
            _test.False(gameSession.HasPendingSave(), "commit 成功后 pending save 应被清空。");

            Dictionary<string, object> committedPayload = ReadActiveSavePayload(gameSession);
            _test.Eq(PayloadPlayerCoord(committedPayload), stagedCoord, "commit 后磁盘应保存 staged 玩家坐标。");
            _test.Eq(PayloadWorldStep(committedPayload), originalWorldStep + 7, "commit 后磁盘应保存 staged world_data。");
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestCommitFailureKeepsDirtyAndLastError()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "事务失败回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
            {
                return;
            }

            Dictionary<string, object> originalPayload = ReadActiveSavePayload(gameSession);
            Vector2I originalCoord = PayloadPlayerCoord(originalPayload);
            Vector2I stagedCoord = originalCoord + Vector2I.Right;
            _test.Eq((Error)gameSession.SetPlayerCoord(stagedCoord), Error.Ok, "事务失败回归前置：坐标 staging 应成功。");

            gameSession.fail_payload_write = true;
            Error commitError = (Error)gameSession.CommitRuntimeState(new StringName("test.fail_payload_write"));
            _test.Eq(commitError, Error.CantCreate, "payload 写入失败时 commit_runtime_state 应返回底层错误。");
            _test.True(gameSession.HasPendingSave(), "commit 失败后 pending save 不能被清空。");

            GDictionary status = gameSession.GetSaveStatus();
            _test.Eq(DictError(status, "last_error", Error.Ok), Error.CantCreate, "commit 失败后 save_status 应记录最近错误。");
            _test.True(
                ArrayHasStringName(status.ContainsKey("dirty_scopes") ? status["dirty_scopes"] : Variant.From(new GArray()), new StringName("player_coord")),
                "commit 失败后 dirty_scopes 应保留玩家坐标变更。"
            );

            Dictionary<string, object> diskPayload = ReadActiveSavePayload(gameSession);
            _test.Eq(PayloadPlayerCoord(diskPayload), originalCoord, "commit 失败后磁盘坐标应保持旧快照。");
        }
        finally
        {
            gameSession.fail_payload_write = false;
            CleanupTestSession(gameSession);
        }
    }

    private void TestUnloadCommitsPendingRuntimeState()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "卸载提交回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
            {
                return;
            }

            string saveId = gameSession.GetActiveSaveId();
            Dictionary<string, object> originalPayload = ReadActiveSavePayload(gameSession);
            Vector2I stagedCoord = PayloadPlayerCoord(originalPayload) + Vector2I.Right;
            _test.Eq((Error)gameSession.SetPlayerCoord(stagedCoord), Error.Ok, "卸载提交回归前置：坐标 staging 应成功。");
            _test.True(gameSession.HasPendingSave(), "卸载前应存在 pending save。");

            try
            {
                gameSession.UnloadActiveWorld();
            }
            catch (Exception ex)
            {
                _test.Fail($"卸载 active world 不应抛异常。| error={ex.Message}");
                return;
            }

            _test.False(gameSession.HasActiveWorld(), "卸载 active world 应清理当前内存态。");
            Error loadError = (Error)gameSession.LoadSave(saveId);
            _test.Eq(loadError, Error.Ok, "卸载前的 pending save 应先提交，之后仍能重新载入。");
            if (loadError == Error.Ok)
            {
                _test.Eq(gameSession.GetPlayerCoord(), stagedCoord, "卸载触发的提交应保存 staged 玩家坐标。");
            }
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private static Dictionary<string, object> ReadActiveSavePayload(GameSession gameSession)
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
        IReadOnlyDictionary<string, object> worldState = PlainDictionary(
            payload,
            "world_state"
        );
        return worldState.TryGetValue("player_coord", out object value)
            && value is Vector2I coord
            ? coord
            : Vector2I.Zero;
    }

    private static int PayloadWorldStep(IReadOnlyDictionary<string, object> payload)
    {
        IReadOnlyDictionary<string, object> worldState = PlainDictionary(
            payload,
            "world_state"
        );
        IReadOnlyDictionary<string, object> worldData = PlainDictionary(
            worldState,
            "world_data"
        );
        return worldData.TryGetValue("world_step", out object value)
            ? Convert.ToInt32(value)
            : 0;
    }

    private static IReadOnlyDictionary<string, object> PlainDictionary(
        IReadOnlyDictionary<string, object> values,
        string key
    ) =>
        values != null
        && values.TryGetValue(key, out object value)
        && value is IReadOnlyDictionary<string, object> dictionary
            ? dictionary
            : new Dictionary<string, object>(StringComparer.Ordinal);

    private static Variant DictionaryGet(GDictionary values, string key)
    {
        if (values == null)
        {
            return default;
        }
        if (values.ContainsKey(key))
        {
            return values[key];
        }

        StringName stringNameKey = new(key);
        return values.ContainsKey(stringNameKey) ? values[stringNameKey] : default;
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        Variant value = DictionaryGet(dictionary, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static Vector2I DictVector2I(GDictionary dictionary, string key, Vector2I fallback)
    {
        Variant value = DictionaryGet(dictionary, key);
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        Variant value = DictionaryGet(dictionary, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static Error DictError(GDictionary dictionary, string key, Error fallback)
    {
        return (Error)DictInt(dictionary, key, (int)fallback);
    }

    private static bool ArrayHasStringName(Variant values, StringName expected)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return false;
        }

        foreach (Variant value in values.AsGodotArray())
        {
            if (new StringName(value.AsString()) == expected)
            {
                return true;
            }
        }
        return false;
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }
        gameSession.ClearPersistedGame();
        gameSession.Free();
    }
}
