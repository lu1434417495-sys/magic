using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_save_serializer_quest_round_trip_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestSaveSerializerRoundTripPreservesPartyQuestSchema();
        TestDecodePayloadRejectsInvalidQuestProgressContext();
        TestDecodePayloadRejectsMissingPartySchemaFields();
        TestDecodePayloadRejectsFailedStateInActiveCollection();
        TestDecodePayloadRejectsIncompleteSettlementState();
        TestCurrentRootRejectsPartyVersion8WithoutMigration();
        TestRootVersion17IsRejectedWithoutMigration();
        TestRootVersion10PartyVersion6OldEquipmentPayloadIsRejectedByVersionGate();
        TestExtractSaveMetaRejectsMissingSlotFields();
        RequestTestExit(_test.Finish("Save serializer quest round trip regression"));
    }

    private void TestSaveSerializerRoundTripPreservesPartyQuestSchema()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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
        var failedQuest = new QuestState { quest_id = "contract_failed_patrol" };
        failedQuest.MarkAccepted(6);
        failedQuest.MarkFailed(
            13,
            "protected_target_lost",
            QuestProgressContext.FromDictionary(
                new GDictionary { ["source_type"] = "world_event" }
            )
        );
        partyState.SetFailedQuestState(failedQuest);
        partyState.AddCompletedQuestId("intro_contract");
        partyState.SetWorldRenown(72);
        partyState.SetCountryReputation("frost_ash_empire", 35);
        partyState.SetCountryReputation("starfall_federation", -20);

        SaveSerializer serializer = gameSession._save_serializer;
        using GodotProjectionLease<GDictionary> payloadLease =
            BuildSavePayloadForSession(gameSession, partyState);
        GDictionary payload = payloadLease.Value;
        _test.Eq(
            DictInt(payload, "version", -1),
            18,
            "Current strict world schema should use top-level save version 18."
        );
        Dictionary<string, object> payloadPlain = RuntimePlainPayload.RestoreSaveDictionary(
            payload,
            "quest-round-trip.payload"
        );
        bool decoded = serializer.TryDecodePayload(
            payloadPlain,
            gameSession.GetGenerationConfigPath(),
            gameSession.CaptureActiveSaveMetaPlain(),
            out SaveDecodeResult decodeResult
        );
        _test.True(decoded, "SaveSerializer 应能成功解码带 quest schema 的 payload。");
        _test.Eq(decodeResult.Error, (int)Error.Ok, "成功解码应返回 Ok。");

        PartyState restoredPartyState = decoded ? decodeResult.PartyState : null;
        _test.True(restoredPartyState != null, "解码后的 payload 应返回 PartyState。");
        if (restoredPartyState != null)
        {
            _test.Eq(
                restoredPartyState.version,
                9,
                "Social standing schema should bump PartyState.version to 9."
            );
            _test.Eq(restoredPartyState.main_character_member_id, partyState.main_character_member_id, "完整 save round-trip 后应保留 main_character_member_id。");
            _test.Eq(restoredPartyState.GetWorldRenown(), 72, "完整 save round-trip 后应保留世界名望。");
            _test.Eq(
                restoredPartyState.GetCountryReputation("frost_ash_empire"),
                35,
                "完整 save round-trip 后应保留帝国声望。"
            );
            _test.Eq(
                restoredPartyState.GetCountryReputation("starfall_federation"),
                -20,
                "完整 save round-trip 后应保留联邦声望。"
            );
            _test.True(restoredPartyState.HasActiveQuest("contract_wolf_pack"), "SaveSerializer 往返后应保留 active_quests。");
            _test.True(restoredPartyState.HasClaimableQuest("contract_settlement_warehouse"), "SaveSerializer 往返后应保留 claimable_quests。");
            _test.True(restoredPartyState.HasFailedQuest("contract_failed_patrol"), "SaveSerializer 往返后应保留 failed_quests。");
            _test.True(restoredPartyState.HasCompletedQuest("intro_contract"), "SaveSerializer 往返后应保留 completed_quest_ids。");

            QuestState restoredQuest = restoredPartyState.GetActiveQuestState("contract_wolf_pack");
            QuestState restoredClaimableQuest = restoredPartyState.GetClaimableQuestState(
                "contract_settlement_warehouse"
            );
            QuestState restoredFailedQuest = restoredPartyState.GetFailedQuestState(
                "contract_failed_patrol"
            );
            _test.True(restoredQuest != null, "SaveSerializer 往返后应恢复 QuestState。");
            _test.True(restoredClaimableQuest != null, "SaveSerializer 往返后应恢复待领奖励 QuestState。");
            _test.True(restoredFailedQuest != null, "SaveSerializer 往返后应恢复失败 QuestState。");
            if (restoredQuest != null)
            {
                _test.Eq(restoredQuest.GetObjectiveProgress("defeat_wolves"), 2, "QuestState 进度应穿过 save payload 保持稳定。");
                _test.Eq(restoredQuest.accepted_at_world_step, 8, "QuestState 接取时间应穿过 save payload 保持稳定。");
            }
            if (restoredClaimableQuest != null)
                _test.Eq(restoredClaimableQuest.completed_at_world_step, 11, "待领奖励 QuestState 完成时间应穿过 save payload 保持稳定。");
            if (restoredFailedQuest != null)
            {
                _test.Eq(restoredFailedQuest.failed_at_world_step, 13, "失败 QuestState 应保留失败时间。");
                _test.Eq(
                    restoredFailedQuest.failure_reason_id,
                    new StringName("protected_target_lost"),
                    "失败 QuestState 应保留失败原因。"
                );
            }
        }

        CleanupTestSession(gameSession);
    }

    private void TestRootVersion10PartyVersion6OldEquipmentPayloadIsRejectedByVersionGate()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "旧装备存档 version gate 回归需要可创建的测试世界。");
        if (createError != Error.Ok)
        {
            CleanupTestSession(gameSession);
            return;
        }

        SaveSerializer serializer = gameSession._save_serializer;
        using GodotProjectionLease<GDictionary> payloadLease =
            BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
        Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
            payloadLease.Value,
            "old-equipment.payload"
        );
        payload["version"] = 10;

        Dictionary<string, object> partyPayload = PlainDictionary(payload, "party_state");
        partyPayload["version"] = 6;
        Dictionary<string, object> warehousePayload = PlainDictionary(
            partyPayload,
            "warehouse_state"
        );
        warehousePayload["equipment_instances"] = new List<object>
        {
            MakeOldFiveFieldEquipmentInstancePayload("eq_old_version_gate")
        };

        _test.True(
            !serializer.TryExtractSaveMetaPlain(payload, out _),
            "Root save version 10 should be rejected by the top-level version gate before party/equipment parsing."
        );

        bool decoded = serializer.TryDecodePayload(
            payload,
            gameSession.GetGenerationConfigPath(),
            gameSession.CaptureActiveSaveMetaPlain(),
            out SaveDecodeResult decodeResult
        );
        _test.True(!decoded, "Root version 10 / PartyState version 6 / five-field equipment save should reject as old schema.");
        _test.Eq(decodeResult.Error, (int)Error.InvalidData, "旧 schema 解码结果应标记 InvalidData。");

        CleanupTestSession(gameSession);
    }

    private void TestRootVersion17IsRejectedWithoutMigration()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "v17 rejection regression requires a valid current save.");
            if (createError != Error.Ok)
                return;

            SaveSerializer serializer = gameSession._save_serializer;
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
            Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                "social-standing-v17.payload"
            );
            payload["version"] = 17;

            _test.True(
                !serializer.TryExtractSaveMetaPlain(payload, out _),
                "v17 save metadata must be rejected; current social standing schema has no compatibility path."
            );
            bool decoded = serializer.TryDecodePayload(
                payload,
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                out SaveDecodeResult decodeResult
            );
            _test.True(!decoded, "v17 save payload must be rejected without migration.");
            _test.Eq(
                decodeResult.Error,
                (int)Error.InvalidData,
                "v17 rejection must report InvalidData."
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestCurrentRootRejectsPartyVersion8WithoutMigration()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "PartyState v8 rejection regression requires a valid current save.");
            if (createError != Error.Ok)
                return;

            SaveSerializer serializer = gameSession._save_serializer;
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
            Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                "party-v8.payload"
            );
            PlainDictionary(payload, "party_state")["version"] = 8;

            bool decoded = serializer.TryDecodePayload(
                payload,
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                out SaveDecodeResult decodeResult
            );
            _test.True(
                !decoded,
                "Current root save with PartyState v8 must reject because social standing has no compatibility migration."
            );
            _test.Eq(
                decodeResult.Error,
                (int)Error.InvalidData,
                "PartyState v8 rejection must report InvalidData."
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestDecodePayloadRejectsFailedStateInActiveCollection()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "Quest collection mismatch regression requires a valid current save.");
            if (createError != Error.Ok)
                return;

            var failedQuest = new QuestState { quest_id = "contract_failed_patrol" };
            failedQuest.MarkAccepted(6);
            failedQuest.MarkFailed(13, "protected_target_lost");
            gameSession.GetPartyState().SetFailedQuestState(failedQuest);

            SaveSerializer serializer = gameSession._save_serializer;
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
            Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                "failed-state-in-active.payload"
            );
            Dictionary<string, object> partyPayload = PlainDictionary(payload, "party_state");
            if (
                !partyPayload.TryGetValue("failed_quests", out object failedQuestsValue)
                || failedQuestsValue is not List<object> failedQuests
                || failedQuests.Count != 1
            )
            {
                _test.True(false, "Test payload should contain one failed quest.");
                return;
            }

            partyPayload["active_quests"] = new List<object> { failedQuests[0] };
            partyPayload["failed_quests"] = new List<object>();
            bool decoded = serializer.TryDecodePayload(
                payload,
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                out SaveDecodeResult decodeResult
            );
            _test.True(
                !decoded,
                "A valid Failed QuestState placed in active_quests must be rejected."
            );
            _test.Eq(
                decodeResult.Error,
                (int)Error.InvalidData,
                "Quest collection/status mismatch must report InvalidData."
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestDecodePayloadRejectsIncompleteSettlementState()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "nested settlement-state validation requires a valid save.");
            if (createError != Error.Ok)
                return;

            SaveSerializer serializer = gameSession._save_serializer;
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
            Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                "invalid-settlement-state.payload"
            );
            Dictionary<string, object> worldState = PlainDictionary(payload, "world_state");
            Dictionary<string, object> worldData = PlainDictionary(worldState, "world_data");
            var settlements = (List<object>)worldData["settlements"];
            var settlement = (Dictionary<string, object>)settlements[0];
            var settlementState = (Dictionary<string, object>)settlement["settlement_state"];
            settlementState.Remove("shop_states");

            bool decoded = serializer.TryDecodePayload(
                payload,
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                out SaveDecodeResult decodeResult
            );
            _test.True(
                !decoded,
                "current-version payload with incomplete settlement_state must be rejected."
            );
            _test.Eq(
                decodeResult.Error,
                (int)Error.InvalidData,
                "incomplete settlement_state must report InvalidData."
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestDecodePayloadRejectsInvalidQuestProgressContext()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "损坏任务上下文回归需要可创建的测试世界。");
            if (createError != Error.Ok)
                return;

            PartyState partyState = gameSession.GetPartyState();
            var questState = new QuestState { quest_id = "contract_wolf_pack" };
            questState.MarkAccepted(8);
            partyState.SetActiveQuestState(questState);

            SaveSerializer serializer = gameSession._save_serializer;
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildSavePayloadForSession(gameSession, partyState);
            Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                "invalid-quest-progress-context.payload"
            );
            Dictionary<string, object> partyPayload = PlainDictionary(payload, "party_state");
            IReadOnlyList<object> activeQuests =
                partyPayload.TryGetValue("active_quests", out object activeQuestsValue)
                && activeQuestsValue is IReadOnlyList<object> questList
                    ? questList
                    : Array.Empty<object>();
            _test.Eq(activeQuests.Count, 1, "测试 payload 应包含一个 active quest。");
            if (
                activeQuests.Count != 1
                || activeQuests[0] is not Dictionary<string, object> activeQuestPayload
            )
            {
                _test.True(false, "测试 payload 的 active quest 应为 plain dictionary。");
                return;
            }

            activeQuestPayload["last_progress_context"] = new Dictionary<string, object>(
                StringComparer.Ordinal
            )
            {
                ["submitted_quantity"] = "bad",
            };

            bool decoded = false;
            SaveDecodeResult decodeResult = null;
            Exception decodeException = null;
            try
            {
                decoded = serializer.TryDecodePayload(
                    payload,
                    gameSession.GetGenerationConfigPath(),
                    gameSession.CaptureActiveSaveMetaPlain(),
                    out decodeResult
                );
            }
            catch (Exception exception)
            {
                decodeException = exception;
            }

            _test.True(
                decodeException == null,
                "SaveSerializer 应安全拒绝损坏的任务上下文，而不是抛出异常。"
            );
            _test.True(!decoded, "损坏的任务上下文不应通过存档解码。");
            _test.True(
                decodeResult != null && decodeResult.Error == (int)Error.InvalidData,
                "损坏的任务上下文应返回 Error.InvalidData。"
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestDecodePayloadRejectsMissingPartySchemaFields()
    {
        foreach (
            string fieldName in new[]
            {
                "main_character_member_id",
                "claimable_quests",
                "failed_quests",
                "world_renown",
                "country_reputations",
            }
        )
        {
            var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, $"缺 {fieldName} 字段的存档回归需要可创建的测试世界。");
            if (createError != Error.Ok)
            {
                CleanupTestSession(gameSession);
                continue;
            }

            SaveSerializer serializer = gameSession._save_serializer;
            using GodotProjectionLease<GDictionary> payloadLease =
                BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
            Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
                payloadLease.Value,
                $"missing-party-field.{fieldName}"
            );
            Dictionary<string, object> partyPayload = PlainDictionary(
                payload,
                "party_state"
            );
            partyPayload.Remove(fieldName);
            bool decoded = serializer.TryDecodePayload(
                payload,
                gameSession.GetGenerationConfigPath(),
                gameSession.CaptureActiveSaveMetaPlain(),
                out SaveDecodeResult decodeResult
            );
            _test.True(!decoded, $"缺少 {fieldName} 的存档应直接判为坏数据。");
            _test.Eq(decodeResult.Error, (int)Error.InvalidData, $"缺少 {fieldName} 应返回 InvalidData。");

            CleanupTestSession(gameSession);
        }
    }

    private void TestExtractSaveMetaRejectsMissingSlotFields()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, Error.Ok, "Save meta 严格校验回归需要可创建的测试世界。");
        if (createError != Error.Ok)
        {
            CleanupTestSession(gameSession);
            return;
        }

        SaveSerializer serializer = gameSession._save_serializer;
        using GodotProjectionLease<GDictionary> payloadLease =
            BuildSavePayloadForSession(gameSession, gameSession.GetPartyState());
        Dictionary<string, object> payload = RuntimePlainPayload.RestoreSaveDictionary(
            payloadLease.Value,
            "missing-save-meta.payload"
        );
        Dictionary<string, object> activeSaveMeta = gameSession.CaptureActiveSaveMetaPlain();
        payload["save_slot_meta"] = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["save_id"] = gameSession.GetActiveSaveId(),
            ["generation_config_path"] = gameSession.GetGenerationConfigPath(),
            ["world_preset_id"] = PlainString(activeSaveMeta, "world_preset_id"),
        };
        _test.True(
            !serializer.TryExtractSaveMetaPlain(payload, out _),
            "缺失 display_name/world_size/timestamps 的 save_slot_meta 应直接拒绝。"
        );
        bool decoded = serializer.TryDecodePayload(
            payload,
            gameSession.GetGenerationConfigPath(),
            activeSaveMeta,
            out SaveDecodeResult decodeResult
        );
        _test.True(!decoded, "缺失完整 save_slot_meta 的 payload 应直接判为坏数据。");
        _test.Eq(decodeResult.Error, (int)Error.InvalidData, "缺失完整 save_slot_meta 应返回 InvalidData。");

        CleanupTestSession(gameSession);
    }

    private static GodotProjectionLease<GDictionary> BuildSavePayloadForSession(
        GameSession gameSession,
        PartyState partyState
    )
    {
        return gameSession._save_serializer.BuildSavePayloadLease(
            gameSession.GetActiveSaveId(),
            gameSession.GetGenerationConfigPath(),
            gameSession.CaptureActiveSaveMetaPlain(),
            gameSession.CaptureWorldDataPlain(),
            gameSession.GetPlayerCoord(),
            gameSession.GetPlayerFactionId(),
            partyState,
            (int)Time.GetUnixTimeFromSystem()
        );
    }

    private static Dictionary<string, object> MakeOldFiveFieldEquipmentInstancePayload(
        string instanceId
    ) =>
        new(StringComparer.Ordinal)
        {
            ["instance_id"] = instanceId,
            ["item_id"] = "bronze_sword",
            ["rarity"] = (int)EquipmentInstanceState.RarityTier.COMMON,
            ["current_durability"] = EquipmentDurabilityRules.GetDefaultCurrentDurability(
                (int)EquipmentInstanceState.RarityTier.COMMON
            ),
            ["trait_instances"] = new List<object>(),
        };

    private static Dictionary<string, object> PlainDictionary(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is Dictionary<string, object> dictionary
            ? dictionary
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is string text
            ? text
            : "";
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

}
