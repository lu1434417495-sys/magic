using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_save_serializer_quest_round_trip_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSaveSerializerRoundTripPreservesPartyQuestSchema();
        TestDecodePayloadRejectsMissingPartySchemaFields();
        TestExtractSaveMetaRejectsMissingSlotFields();
        Quit(_test.Finish("Save serializer quest round trip regression"));
    }

    private void TestSaveSerializerRoundTripPreservesPartyQuestSchema()
    {
        var gameSession = new GameSession();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "GameSession 应能基于测试世界配置创建新存档。");
        if (createError != Error.Ok)
        {
            CleanupTestSession(gameSession);
            return;
        }

        PartyState partyState = gameSession.GetPartyState();
        var questState = new QuestState { quest_id = "contract_wolf_pack" };
        questState.MarkAccepted(8);
        questState.RecordObjectiveProgress(
            "defeat_wolves",
            2,
            3,
            QuestProgressContext.FromDictionary(
                new GDictionary { ["enemy_template_id"] = "wolf_raider" }
            )
        );
        partyState.SetActiveQuestState(questState);

        var claimableQuest = new QuestState { quest_id = "contract_settlement_warehouse" };
        claimableQuest.MarkAccepted(5);
        claimableQuest.MarkCompleted(11);
        partyState.SetClaimableQuestState(claimableQuest);
        partyState.AddCompletedQuestId("intro_contract");

        SaveSerializer serializer = gameSession._save_serializer;
        GDictionary payload = BuildSavePayloadForSession(gameSession, partyState);
        _test.Eq(
            DictInt(payload, "version", -1),
            10,
            "Typed state owner schema should bump top-level save version to 10."
        );
        GDictionary decodeResult = serializer.DecodePayload(
            payload,
            gameSession.GetGenerationConfigPath(),
            gameSession.GetGenerationConfig(),
            gameSession.GetActiveSaveMeta()
        );
        _test.Eq(DictInt(decodeResult, "error", (int)Error.InvalidData), (int)Error.Ok, "SaveSerializer 应能成功解码带 quest schema 的 payload。");

        PartyState restoredPartyState = decodeResult.ContainsKey("party_state")
            ? decodeResult["party_state"].As<PartyState>()
            : null;
        _test.True(restoredPartyState != null, "解码后的 payload 应返回 PartyState。");
        if (restoredPartyState != null)
        {
            _test.Eq(
                restoredPartyState.version,
                6,
                "Typed state owner schema should bump PartyState.version to 6."
            );
            _test.Eq(restoredPartyState.main_character_member_id, partyState.main_character_member_id, "完整 save round-trip 后应保留 main_character_member_id。");
            _test.True(restoredPartyState.HasActiveQuest("contract_wolf_pack"), "SaveSerializer 往返后应保留 active_quests。");
            _test.True(restoredPartyState.HasClaimableQuest("contract_settlement_warehouse"), "SaveSerializer 往返后应保留 claimable_quests。");
            _test.True(restoredPartyState.HasCompletedQuest("intro_contract"), "SaveSerializer 往返后应保留 completed_quest_ids。");

            QuestState restoredQuest = restoredPartyState.GetActiveQuestState("contract_wolf_pack");
            QuestState restoredClaimableQuest = restoredPartyState.GetClaimableQuestState(
                "contract_settlement_warehouse"
            );
            _test.True(restoredQuest != null, "SaveSerializer 往返后应恢复 QuestState。");
            _test.True(restoredClaimableQuest != null, "SaveSerializer 往返后应恢复待领奖励 QuestState。");
            if (restoredQuest != null)
            {
                _test.Eq(restoredQuest.GetObjectiveProgress("defeat_wolves"), 2, "QuestState 进度应穿过 save payload 保持稳定。");
                _test.Eq(restoredQuest.accepted_at_world_step, 8, "QuestState 接取时间应穿过 save payload 保持稳定。");
            }
            if (restoredClaimableQuest != null)
                _test.Eq(restoredClaimableQuest.completed_at_world_step, 11, "待领奖励 QuestState 完成时间应穿过 save payload 保持稳定。");
        }

        CleanupTestSession(gameSession);
    }

    private void TestDecodePayloadRejectsMissingPartySchemaFields()
    {
        foreach (string fieldName in new[] { "main_character_member_id", "claimable_quests" })
        {
            var gameSession = new GameSession();
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, $"缺 {fieldName} 字段的存档回归需要可创建的测试世界。");
            if (createError != Error.Ok)
            {
                CleanupTestSession(gameSession);
                continue;
            }

            SaveSerializer serializer = gameSession._save_serializer;
            GDictionary payload = BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
            GDictionary partyPayload = (GDictionary)payload["party_state"].AsGodotDictionary().Duplicate(true);
            partyPayload.Remove(fieldName);
            payload["party_state"] = partyPayload;
            GDictionary decodeResult = serializer.DecodePayload(
                payload,
                gameSession.GetGenerationConfigPath(),
                gameSession.GetGenerationConfig(),
                gameSession.GetActiveSaveMeta()
            );
            _test.Eq(
                DictInt(decodeResult, "error", (int)Error.Ok),
                (int)Error.InvalidData,
                $"缺少 {fieldName} 的存档应直接判为坏数据。"
            );

            CleanupTestSession(gameSession);
        }
    }

    private void TestExtractSaveMetaRejectsMissingSlotFields()
    {
        var gameSession = new GameSession();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "Save meta 严格校验回归需要可创建的测试世界。");
        if (createError != Error.Ok)
        {
            CleanupTestSession(gameSession);
            return;
        }

        SaveSerializer serializer = gameSession._save_serializer;
        GDictionary payload = BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
        payload["save_slot_meta"] = new GDictionary
        {
            ["save_id"] = gameSession.GetActiveSaveId(),
            ["generation_config_path"] = gameSession.GetGenerationConfigPath(),
            ["world_preset_id"] = DictString(gameSession.GetActiveSaveMeta(), "world_preset_id"),
        };
        _test.True(serializer.ExtractSaveMetaFromPayload(payload).Count == 0, "缺失 display_name/world_size/timestamps 的 save_slot_meta 应直接拒绝。");
        GDictionary decodeResult = serializer.DecodePayload(
            payload,
            gameSession.GetGenerationConfigPath(),
            gameSession.GetGenerationConfig(),
            gameSession.GetActiveSaveMeta()
        );
        _test.Eq(DictInt(decodeResult, "error", (int)Error.Ok), (int)Error.InvalidData, "缺失完整 save_slot_meta 的 payload 应直接判为坏数据。");

        CleanupTestSession(gameSession);
    }

    private static GDictionary BuildSavePayloadForSession(GameSession gameSession, PartyState partyState)
    {
        return gameSession._save_serializer.BuildSavePayload(
            gameSession.GetActiveSaveId(),
            gameSession.GetGenerationConfigPath(),
            gameSession.GetActiveSaveMeta(),
            gameSession.GetWorldData(),
            gameSession.GetPlayerCoord(),
            gameSession.GetPlayerFactionId(),
            partyState,
            (int)Time.GetUnixTimeFromSystem()
        );
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsString() : "";
    }
}
