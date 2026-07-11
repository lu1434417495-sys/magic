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
        TestCreateNewSaveRejectsInvalidGenerationConfigWithoutQuit();
        TestLoadSaveRejectsBadWorldDataWithoutQuit();
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

        var gameSession = new GameSession();
        Error createError = (Error)gameSession.CreateNewSave(InvalidGenerationConfigPath);
        _test.Eq(createError, Error.CantOpen, "类型错误的 generation config 应通过 create_new_save() 返回错误，不应中止进程。");
        _test.False(gameSession.HasActiveWorld(), "类型错误的 generation config 不应留下 active world。");
        CleanupTestSession(gameSession);
        RemoveUserFileIfExists(InvalidGenerationConfigPath);
    }

    private void TestLoadSaveRejectsBadWorldDataWithoutQuit()
    {
        var gameSession = new GameSession();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "坏 world_data 回归前置：应能创建测试存档。");
        if (createError != Error.Ok)
        {
            CleanupTestSession(gameSession);
            return;
        }

        GDictionary payload = BuildPayloadForSession(gameSession);
        GDictionary worldState = (GDictionary)Dict(payload, "world_state").Duplicate(true);
        GDictionary worldData = (GDictionary)Dict(worldState, "world_data").Duplicate(true);
        worldData.Remove("next_equipment_instance_serial");
        worldState["world_data"] = worldData;
        payload["world_state"] = worldState;

        Error writeError = OverwriteActiveSavePayload(gameSession, payload);
        _test.Eq(writeError, Error.Ok, "坏 world_data 回归前置：应能写入损坏存档 payload。");
        if (writeError == Error.Ok)
        {
            Error loadError = (Error)gameSession.LoadSave(gameSession.GetActiveSaveId());
            _test.Eq(loadError, Error.InvalidData, "坏 world_data 应通过 load_save() 返回 ERR_INVALID_DATA，不应中止进程。");
        }
        CleanupTestSession(gameSession);
    }

    private void TestCreateNewSaveRejectsBadCreationIdentityWithoutCreatingSlot()
    {
        var gameSession = new GameSession();
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
        _test.Eq(gameSession.ListSaveSlots().Count, 0, "非法建卡 payload 被拒后不应创建新存档槽。");
        CleanupTestSession(gameSession);
    }

    private void TestCreateNewSaveAcceptsValidCreationIdentityPayload()
    {
        var gameSession = new GameSession();
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

    private static GDictionary BuildPayloadForSession(GameSession gameSession)
    {
        return gameSession._save_serializer.BuildSavePayload(
            gameSession.GetActiveSaveId(),
            gameSession.GetGenerationConfigPath(),
            gameSession.GetActiveSaveMeta(),
            gameSession.GetWorldData(),
            gameSession.GetPlayerCoord(),
            gameSession.GetPlayerFactionId(),
            gameSession.GetPartyState(),
            (int)Time.GetUnixTimeFromSystem()
        );
    }

    private static Error OverwriteActiveSavePayload(GameSession gameSession, GDictionary payload)
    {
        string savePath = gameSession.GetActiveSavePath();
        if (string.IsNullOrEmpty(savePath))
            return Error.InvalidParameter;
        using FileAccess saveFile = FileAccess.OpenCompressed(
            savePath,
            FileAccess.ModeFlags.Write,
            SaveCompressionMode
        );
        if (saveFile == null)
            return FileAccess.GetOpenError();
        saveFile.StoreVar(payload, false);
        return Error.Ok;
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

    private static GDictionary Dict(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();
    }
}
