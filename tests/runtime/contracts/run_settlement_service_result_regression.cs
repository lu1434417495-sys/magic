using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_settlement_service_result_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestCanonicalResultDictionaryShape();
        TestDictionaryRoundTrip();
        TestRejectsBadSchema();

        if (_failures.Count == 0)
        {
            GD.Print("Settlement service result regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Settlement service result regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestCanonicalResultDictionaryShape()
    {
        var result = new SettlementServiceResult
        {
            Success = true,
            Message = "settlement ok",
            PersistPartyState = true,
            PersistWorldData = true,
            PersistPlayerCoord = false,
            GoldDelta = -50,
        };
        result.SetInventoryDelta(new GDictionary
        {
            ["items_added"] = new GArray
            {
                new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2 },
            },
        });
        result.SetPendingCharacterRewardPayloads(new GArray
        {
            BuildPendingRewardDictionary("hero_training_reward"),
        });
        result.SetQuestProgressEventPayloads(new GArray
        {
            BuildSettlementQuestProgressEvent("service:training"),
        });
        result.SetServiceSideEffects(new GDictionary
        {
            ["hp_restored"] = new GDictionary { ["hero"] = 12 },
        });

        GDictionary dictionary = result.ToDictionary();

        AssertTrue(dictionary.ContainsKey("pending_character_rewards"), "结果应包含 pending_character_rewards。");
        AssertTrue(dictionary.ContainsKey("service_side_effects"), "结果应包含 service_side_effects。");
        AssertFalse(dictionary.ContainsKey("pending_mastery_rewards"), "结果不应再包含 pending_mastery_rewards。");
        AssertFalse(dictionary.ContainsKey("effects"), "结果不应再包含 effects。");
        AssertEq(DictInt(dictionary, "gold_delta", 0), -50, "gold_delta 应保留。");
        AssertEq(DictArray(dictionary, "pending_character_rewards").Count, 1, "pending rewards 数量应保持稳定。");
        GDictionary sideEffects = DictDictionary(dictionary, "service_side_effects");
        GDictionary hpRestored = DictDictionary(sideEffects, "hp_restored");
        AssertEq(DictInt(hpRestored, "hero", 0), 12, "service_side_effects 应保留具体副作用。");
    }

    private void TestDictionaryRoundTrip()
    {
        SettlementServiceResult defaultParsed = SettlementServiceResult.FromDictionary(
            new SettlementServiceResult().ToDictionary()
        );
        AssertTrue(defaultParsed != null, "默认正式 ToDictionary payload 应能 FromDictionary。");

        GDictionary input = new()
        {
            ["success"] = false,
            ["message"] = "据点结果",
            ["persist_party_state"] = true,
            ["persist_world_data"] = false,
            ["persist_player_coord"] = true,
            ["inventory_delta"] = new GDictionary
            {
                ["items_removed"] = new GArray
                {
                    new GDictionary { ["item_id"] = "training_ticket", ["quantity"] = 1 },
                },
            },
            ["gold_delta"] = -12,
            ["pending_character_rewards"] = new GArray
            {
                BuildPendingRewardDictionary("hero_roundtrip_reward"),
            },
            ["quest_progress_events"] = new GArray
            {
                BuildDirectQuestProgressEvent("quest_b", "train_once", 2),
            },
            ["service_side_effects"] = new GDictionary
            {
                ["fog_revealed"] = new GArray { new Vector2I(1, 2) },
            },
        };

        SettlementServiceResult result = SettlementServiceResult.FromDictionary(input);
        GDictionary dictionary = result?.ToDictionary() ?? new GDictionary();

        AssertTrue(result != null, "合法输入应返回 result 实例。");
        AssertFalse(result?.Success ?? true, "输入应保留 success。");
        AssertEq(result?.Message, "据点结果", "输入应保留 message。");
        AssertTrue(result?.PersistPartyState ?? false, "输入应保留 persist_party_state。");
        AssertTrue(result?.PersistPlayerCoord ?? false, "输入应保留 persist_player_coord。");
        AssertTrue(result?.InventoryDelta.ContainsKey("items_removed") ?? false, "输入应保留 inventory_delta。");
        AssertEq(result?.GoldDelta ?? 0, -12, "输入应回填 gold_delta。");
        AssertEq(result?.PendingCharacterRewards.Count ?? 0, 1, "pending_character_rewards 应回填。");
        AssertEq(result?.QuestProgressEvents.Count ?? 0, 1, "quest_progress_events 应保留。");
        AssertTrue(result?.ServiceSideEffects.ContainsKey("fog_revealed") ?? false, "service_side_effects 应回填。");
        AssertTrue(dictionary.ContainsKey("inventory_delta"), "round trip 后应保留 inventory_delta。");
        AssertTrue(dictionary.ContainsKey("pending_character_rewards"), "round trip 后应保留 pending_character_rewards。");
        AssertTrue(dictionary.ContainsKey("service_side_effects"), "round trip 后应保留 service_side_effects。");
        AssertFalse(dictionary.ContainsKey("pending_mastery_rewards"), "round trip 后不应出现 pending_mastery_rewards。");
        AssertFalse(dictionary.ContainsKey("effects"), "round trip 后不应出现 effects。");
    }

    private void TestRejectsBadSchema()
    {
        AssertRejects(new GDictionary(), "空 Dictionary payload 应被拒绝。");

        GDictionary missingField = ValidDictionary();
        missingField.Remove("inventory_delta");
        AssertRejects(missingField, "缺少必需字段时应被拒绝。");

        GDictionary extraField = ValidDictionary();
        extraField["effects"] = new GDictionary();
        AssertRejects(extraField, "包含非当前字段时应被拒绝。");

        GDictionary nonStringKey = ValidDictionary();
        var successValue = nonStringKey["success"];
        nonStringKey.Remove("success");
        nonStringKey[1] = successValue;
        AssertRejects(nonStringKey, "顶层字段 key 不是 String 时应被拒绝。");

        AssertRejects(DictionaryWith("success", "true"), "success 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("message", 12), "message 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("persist_party_state", 1), "persist_party_state 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("persist_world_data", "false"), "persist_world_data 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("persist_player_coord", 0), "persist_player_coord 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("inventory_delta", new GArray()), "inventory_delta 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("gold_delta", "-12"), "gold_delta 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("pending_character_rewards", new GDictionary()), "pending_character_rewards 非 Array 时应被拒绝。");
        AssertRejects(DictionaryWith("quest_progress_events", new GDictionary()), "quest_progress_events 非 Array 时应被拒绝。");
        AssertRejects(DictionaryWith("service_side_effects", new GArray()), "service_side_effects 类型错误时应被拒绝。");
        AssertRejects(DictionaryWith("pending_character_rewards", new GArray { "bad" }), "pending_character_rewards 含非 Dictionary 元素时应被拒绝。");
        AssertRejects(DictionaryWith("quest_progress_events", new GArray { 12 }), "quest_progress_events 含非 Dictionary 元素时应被拒绝。");

        GDictionary missingRewardField = ValidDictionary();
        GDictionary rewardMissingEntries = BuildPendingRewardDictionary("bad_reward_missing_entries");
        rewardMissingEntries.Remove("entries");
        missingRewardField["pending_character_rewards"] = new GArray { rewardMissingEntries };
        AssertRejects(missingRewardField, "pending_character_rewards 内奖励缺字段时应被拒绝。");

        GDictionary extraRewardField = ValidDictionary();
        GDictionary rewardWithExtra = BuildPendingRewardDictionary("bad_reward_extra");
        rewardWithExtra["pending_mastery_rewards"] = new GArray();
        extraRewardField["pending_character_rewards"] = new GArray { rewardWithExtra };
        AssertRejects(extraRewardField, "pending_character_rewards 内奖励含旧字段时应被拒绝。");

        GDictionary badRewardEntryAmount = ValidDictionary();
        GDictionary rewardWithStringAmount = BuildPendingRewardDictionary("bad_reward_entry_amount");
        GArray stringAmountEntries = rewardWithStringAmount["entries"].AsGodotArray();
        GDictionary stringAmountEntry = stringAmountEntries[0].AsGodotDictionary();
        stringAmountEntry["amount"] = "1";
        rewardWithStringAmount["entries"] = stringAmountEntries;
        badRewardEntryAmount["pending_character_rewards"] = new GArray { rewardWithStringAmount };
        AssertRejects(badRewardEntryAmount, "pending_character_rewards 内 entry 字符串数字应被拒绝。");

        GDictionary extraRewardEntryField = ValidDictionary();
        GDictionary rewardWithExtraEntry = BuildPendingRewardDictionary("bad_reward_entry_extra");
        GArray extraEntries = rewardWithExtraEntry["entries"].AsGodotArray();
        GDictionary extraEntry = extraEntries[0].AsGodotDictionary();
        extraEntry["amount_alias"] = 1;
        rewardWithExtraEntry["entries"] = extraEntries;
        extraRewardEntryField["pending_character_rewards"] = new GArray { rewardWithExtraEntry };
        AssertRejects(extraRewardEntryField, "pending_character_rewards 内 entry 额外字段应被拒绝。");

        GDictionary missingQuestEventType = ValidDictionary();
        GDictionary questEventMissingType = BuildDirectQuestProgressEvent("quest_a", "train_once", 1);
        questEventMissingType.Remove("event_type");
        missingQuestEventType["quest_progress_events"] = new GArray { questEventMissingType };
        AssertRejects(missingQuestEventType, "quest_progress_events 缺 event_type 时应被拒绝。");

        GDictionary questAmountAlias = ValidDictionary();
        questAmountAlias["quest_progress_events"] = new GArray
        {
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = "quest_a",
                ["objective_id"] = "train_once",
                ["amount"] = 1,
            },
        };
        AssertRejects(questAmountAlias, "quest_progress_events 使用 amount 旧字段时应被拒绝。");

        AssertRejects(DictionaryWith("quest_progress_events", new GArray { QuestEventWith("progress_delta", "1") }), "quest_progress_events 字符串 progress_delta 应被拒绝。");
        AssertRejects(DictionaryWith("quest_progress_events", new GArray { QuestEventWith("target_value", "2") }), "quest_progress_events 字符串 target_value 应被拒绝。");
        AssertRejects(DictionaryWith("quest_progress_events", new GArray { QuestEventWith("unexpected_field", "bad") }), "quest_progress_events 含额外字段时应被拒绝。");
        AssertRejects(DictionaryWith("quest_progress_events", new GArray
        {
            new GDictionary
            {
                ["event_type"] = "accept",
                ["quest_id"] = "quest_a",
                ["allow_reaccept"] = "false",
            },
        }), "quest_progress_events 字符串 bool 应被拒绝。");
    }

    private static GDictionary ValidDictionary()
    {
        return new GDictionary
        {
            ["success"] = true,
            ["message"] = "valid settlement result",
            ["persist_party_state"] = true,
            ["persist_world_data"] = false,
            ["persist_player_coord"] = true,
            ["inventory_delta"] = new GDictionary
            {
                ["items_added"] = new GArray
                {
                    new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 1 },
                },
            },
            ["gold_delta"] = 5,
            ["pending_character_rewards"] = new GArray
            {
                BuildPendingRewardDictionary("hero_valid_reward"),
            },
            ["quest_progress_events"] = new GArray
            {
                BuildDirectQuestProgressEvent("quest_a", "train_once", 1),
            },
            ["service_side_effects"] = new GDictionary
            {
                ["hp_restored"] = new GDictionary { ["hero"] = 2 },
            },
        };
    }

    private static GDictionary BuildPendingRewardDictionary(string rewardId)
    {
        var reward = new PendingCharacterReward
        {
            reward_id = new StringName(rewardId),
            member_id = "hero",
            member_name = "Hero",
            source_type = "training",
            source_id = "training",
            source_label = "旅店训练",
            summary_text = "Hero 完成旅店训练。",
        };
        var entry = new PendingCharacterRewardEntry
        {
            entry_type = "skill_mastery",
            target_id = "basic_sword",
            target_label = "基础剑术",
            amount = 1,
            reason_text = "训练奖励",
        };
        reward.entries.Add(entry);
        return reward.to_dict();
    }

    private static GDictionary BuildDirectQuestProgressEvent(string questId, string objectiveId, int progressDelta)
    {
        return new GDictionary
        {
            ["event_type"] = "progress",
            ["quest_id"] = questId,
            ["objective_id"] = objectiveId,
            ["progress_delta"] = progressDelta,
        };
    }

    private static GDictionary BuildSettlementQuestProgressEvent(string targetId)
    {
        return new GDictionary
        {
            ["event_type"] = "progress",
            ["objective_type"] = "settlement_action",
            ["target_id"] = targetId,
            ["progress_delta"] = 1,
            ["action_id"] = targetId,
            ["settlement_id"] = "settlement_alpha",
            ["member_id"] = "hero",
        };
    }

    private static GDictionary QuestEventWith(string fieldName, string fieldValue)
    {
        GDictionary eventData = BuildDirectQuestProgressEvent("quest_a", "train_once", 1);
        eventData[fieldName] = fieldValue;
        return eventData;
    }

    private static GDictionary DictionaryWith(string fieldName, string fieldValue)
    {
        GDictionary dictionary = ValidDictionary();
        dictionary[fieldName] = fieldValue;
        return dictionary;
    }

    private static GDictionary DictionaryWith(string fieldName, int fieldValue)
    {
        GDictionary dictionary = ValidDictionary();
        dictionary[fieldName] = fieldValue;
        return dictionary;
    }

    private static GDictionary DictionaryWith(string fieldName, GArray fieldValue)
    {
        GDictionary dictionary = ValidDictionary();
        dictionary[fieldName] = fieldValue;
        return dictionary;
    }

    private static GDictionary DictionaryWith(string fieldName, GDictionary fieldValue)
    {
        GDictionary dictionary = ValidDictionary();
        dictionary[fieldName] = fieldValue;
        return dictionary;
    }

    private void AssertRejects(GDictionary payload, string message)
    {
        SettlementServiceResult result = SettlementServiceResult.FromDictionary(payload);
        AssertTrue(result == null, message);
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
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
}
