using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_invalid_save_graceful_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private const string InvalidGenerationConfigPath = "user://invalid_generation_config_resource.tres";
    private const FileAccess.CompressionMode SaveCompressionMode = FileAccess.CompressionMode.Zstd;

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestCreateNewSaveRejectsInvalidGenerationConfigWithoutQuit();
        TestLoadSaveRejectsBadWorldDataWithoutQuit();
        TestWorldCoordinatesRequireNativeVector2I();
        TestLoadSaveReturnsDoesNotExistWhenCachedPayloadDisappears();
        TestCreateNewSaveRejectsBadCreationIdentityWithoutCreatingSlot();
        TestCreateNewSaveAcceptsValidCreationIdentityPayload();
        TestSaveIndexVersionRequiresExactInt();
        RequestTestExit(_test.Finish("Invalid save graceful regression"));
    }

    private void TestCreateNewSaveRejectsInvalidGenerationConfigWithoutQuit()
    {
        RemoveUserFileIfExists(InvalidGenerationConfigPath);
        Error saveResourceError = ResourceSaver.Save(new Resource(), InvalidGenerationConfigPath);
        _test.Eq(saveResourceError, Error.Ok, "坏 generation config 回归前置：应能写入可加载但类型错误的资源。");
        if (saveResourceError != Error.Ok)
            return;

        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        Error createError = (Error)gameSession.CreateNewSave(InvalidGenerationConfigPath);
        _test.Eq(createError, Error.CantOpen, "类型错误的 generation config 应通过 create_new_save() 返回错误，不应中止进程。");
        _test.False(gameSession.HasActiveWorld(), "类型错误的 generation config 不应留下 active world。");
        CleanupTestSession(gameSession);
        RemoveUserFileIfExists(InvalidGenerationConfigPath);
    }

    private void TestLoadSaveRejectsBadWorldDataWithoutQuit()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "坏 world_data 回归前置：应能创建测试存档。");
        if (createError != Error.Ok)
        {
            CleanupTestSession(gameSession);
            return;
        }

        Dictionary<string, object> payload;
        using (GodotProjectionLease<GDictionary> payloadLease = BuildPayloadForSession(gameSession))
        {
            payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                "invalid-save-world-data.payload"
            );
        }
        Dictionary<string, object> worldState = PlainDictionary(payload, "world_state");
        Dictionary<string, object> worldData = PlainDictionary(worldState, "world_data");
        worldData.Remove("next_equipment_instance_serial");

        using GodotProjectionLease<GDictionary> corruptPayloadLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                payload,
                "invalid-save-corrupt-payload",
                LifetimeDomain.Request,
                "run_invalid_save_graceful_regression.bad_world_data",
                minimizeStrings: true
            );
        Error writeError = OverwriteActiveSavePayload(gameSession, corruptPayloadLease);
        _test.Eq(writeError, Error.Ok, "坏 world_data 回归前置：应能写入损坏存档 payload。");
        if (writeError == Error.Ok)
        {
            Error loadError = (Error)gameSession.LoadSave(gameSession.GetActiveSaveId());
            _test.Eq(loadError, Error.InvalidData, "坏 world_data 应通过 load_save() 返回 ERR_INVALID_DATA，不应中止进程。");
        }
        CleanupTestSession(gameSession);
    }

    private void TestLoadSaveReturnsDoesNotExistWhenCachedPayloadDisappears()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "缺失存档回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
                return;

            string saveId = gameSession.GetActiveSaveId();
            string savePath = gameSession.GetActiveSavePath();
            _test.True(
                HasSaveId(gameSession.ListSaveSlotsPlain(), saveId),
                "缺失存档回归前置：索引缓存中应包含刚创建的槽位。"
            );

            gameSession.ResetRuntimeCache();
            Error removeError = (Error)gameSession.RemoveFileIfExists(savePath);
            _test.Eq(removeError, Error.Ok, "缺失存档回归前置：应能只删除 save payload。");
            _test.False(FileAccess.FileExists(savePath), "缺失存档回归前置：save payload 应已不存在。");

            Error loadError = Error.Failed;
            try
            {
                loadError = (Error)gameSession.LoadSave(saveId);
            }
            catch (Exception exception)
            {
                _test.Fail($"缓存槽位的 save payload 消失后 LoadSave 不应抛异常。| error={exception}");
                return;
            }

            _test.Eq(
                loadError,
                Error.DoesNotExist,
                "缓存槽位的 save payload 消失后 LoadSave 应返回 DoesNotExist。"
            );
            _test.False(gameSession.HasActiveWorld(), "缺失 save payload 的加载失败不应创建 active world。");
            _test.False(
                HasSaveId(gameSession.ListSaveSlotsPlain(), saveId),
                "确认 save payload 缺失后应从存档索引移除失效槽位。"
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestWorldCoordinatesRequireNativeVector2I()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "Vector2I schema 回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
                return;

            SaveSerializer serializer = gameSession._save_serializer;
            Dictionary<string, object> worldData = gameSession.CaptureWorldDataPlain();
            Vector2I expectedCoord = new(12, 8);
            worldData["player_start_coord"] = expectedCoord;

            bool acceptedNative = serializer.TryNormalizeWorldDataPlain(
                worldData,
                out Dictionary<string, object> normalized
            );
            _test.True(acceptedNative, "world_data 应接受原生 Vector2I 坐标。");
            _test.True(
                acceptedNative
                    && normalized.TryGetValue("player_start_coord", out object normalizedCoord)
                    && normalizedCoord is Vector2I typedCoord
                    && typedCoord == expectedCoord,
                "原生 Vector2I 通过规范化后应保留坐标和值类型。"
            );

            worldData["player_start_coord"] = new Dictionary<string, object>
            {
                ["x"] = expectedCoord.X,
                ["y"] = expectedCoord.Y,
            };
            _test.False(
                serializer.TryNormalizeWorldDataPlain(worldData, out _),
                "world_data 应拒绝以 {x,y} Dictionary 表示的坐标。"
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestCreateNewSaveRejectsBadCreationIdentityWithoutCreatingSlot()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        Error clearError = (Error)gameSession.ClearPersistedGame();
        _test.Eq(clearError, Error.Ok, "坏建卡 identity 建档回归前置：应能清理旧存档。");

        Error createError = (Error)gameSession.CreateNewSave(
            TestWorldConfig,
            "",
            "",
            BuildBadCreationIdentityPayload()
        );
        _test.Eq(createError, Error.InvalidData, "非法 race/subrace 建卡 payload 应让 create_new_save() 返回 ERR_INVALID_DATA。");
        _test.False(gameSession.HasActiveWorld(), "非法建卡 payload 被拒后 fresh session 不应留下 active world。");
        _test.Eq(gameSession.ListSaveSlotsPlain().Count, 0, "非法建卡 payload 被拒后不应创建新存档槽。");
        CleanupTestSession(gameSession);
    }

    private void TestCreateNewSaveAcceptsValidCreationIdentityPayload()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        Error createError = (Error)gameSession.CreateNewSave(
            TestWorldConfig,
            "",
            "",
            BuildValidCreationIdentityPayload()
        );
        _test.Eq(createError, Error.Ok, "合法建卡 identity payload 应仍可创建新存档。");
        if (createError == Error.Ok)
        {
            PartyState partyState = gameSession.GetPartyState();
            PartyMemberState member = partyState?.GetMemberState(
                partyState.GetResolvedMainCharacterMemberId()
            );
            _test.True(member != null, "合法建卡 identity payload 创建后应能取得主角。");
            if (member != null)
            {
                _test.Eq(member.race_id, new StringName("human"), "合法建卡 payload 应保留 race_id。");
                _test.Eq(member.subrace_id, new StringName("common_human"), "合法建卡 payload 应保留 subrace_id。");
                _test.Eq(member.body_size_category, new StringName("medium"), "合法建卡 payload 应从内容规则派生 body_size_category。");
            }
        }
        CleanupTestSession(gameSession);
    }

    private void TestSaveIndexVersionRequiresExactInt()
    {
        var serializer = new SaveSerializer();
        _test.True(serializer.IsSaveIndexIntValue(3), "save index version 应接受 int。");
        _test.False(serializer.IsSaveIndexFloatValue(3.0), "save index version 不应接受 float。");
        _test.False(serializer.IsSaveIndexStringValue("3"), "save index version 不应接受 string。");
        _test.False(serializer.IsSaveIndexBoolValue(true), "save index version 不应接受 bool。");
    }

    private static GodotProjectionLease<GDictionary> BuildPayloadForSession(
        GameSession gameSession
    )
    {
        return gameSession._save_serializer.BuildSavePayloadLease(
            gameSession.GetActiveSaveId(),
            gameSession.GetGenerationConfigPath(),
            gameSession.CaptureActiveSaveMetaPlain(),
            gameSession.CaptureWorldDataPlain(),
            gameSession.GetPlayerCoord(),
            gameSession.GetPlayerFactionId(),
            gameSession.GetPartyState(),
            (int)Time.GetUnixTimeFromSystem()
        );
    }

    private static Error OverwriteActiveSavePayload(
        GameSession gameSession,
        GodotProjectionLease<GDictionary> payload
    )
    {
        string savePath = gameSession.GetActiveSavePath();
        if (string.IsNullOrEmpty(savePath))
            return Error.InvalidParameter;
        return (Error)FileIOCoordinator.WriteCompressedVariantAtomically(
            savePath,
            payload,
            (int)SaveCompressionMode,
            "test.invalid_save",
            "invalid save fixture"
        );
    }

    private static GDictionary BuildBadCreationIdentityPayload()
    {
        GDictionary payload = BuildValidCreationIdentityPayload();
        payload["subrace_id"] = new StringName("red_dragonborn");
        payload["body_size"] = 2;
        payload["body_size_category"] = new StringName("medium");
        return payload;
    }

    private static GDictionary BuildValidCreationIdentityPayload()
    {
        return new GDictionary
        {
            ["display_name"] = "Identity Gate Hero",
            ["reroll_count"] = 0,
            ["strength"] = 10,
            ["agility"] = 10,
            ["constitution"] = 10,
            ["perception"] = 10,
            ["intelligence"] = 10,
            ["willpower"] = 10,
            ["race_id"] = new StringName("human"),
            ["subrace_id"] = new StringName("common_human"),
            ["age_years"] = 24,
            ["birth_at_world_step"] = 0,
            ["age_profile_id"] = new StringName("human_age_profile"),
            ["natural_age_stage_id"] = new StringName("adult"),
            ["effective_age_stage_id"] = new StringName("adult"),
            ["effective_age_stage_source_type"] = new StringName(""),
            ["effective_age_stage_source_id"] = new StringName(""),
            ["body_size"] = 99,
            ["body_size_category"] = new StringName("boss"),
            ["versatility_pick"] = new StringName(""),
            ["active_stage_advancement_modifier_ids"] = new Godot.Collections.Array<StringName>(),
            ["bloodline_id"] = new StringName(""),
            ["bloodline_stage_id"] = new StringName(""),
            ["ascension_id"] = new StringName(""),
            ["ascension_stage_id"] = new StringName(""),
            ["ascension_started_at_world_step"] = -1,
            ["original_race_id_before_ascension"] = new StringName(""),
            ["biological_age_years"] = 24,
            ["astral_memory_years"] = 0,
        };
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static void RemoveUserFileIfExists(string path)
    {
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    private static bool HasSaveId(
        IReadOnlyList<Dictionary<string, object>> entries,
        string expectedSaveId
    )
    {
        if (entries == null)
            return false;
        foreach (IReadOnlyDictionary<string, object> entry in entries)
        {
            if (
                entry != null
                && entry.TryGetValue("save_id", out object saveId)
                && string.Equals(saveId as string, expectedSaveId, System.StringComparison.Ordinal)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static Dictionary<string, object> PlainDictionary(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is Dictionary<string, object> dictionary
            ? dictionary
            : new Dictionary<string, object>(System.StringComparer.Ordinal);
    }
}
