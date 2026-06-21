using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_settlement_service_result_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        TestCanonicalResultDictionaryShape();
        TestTypedResultProjectionIsolation();

        return _test.Finish("Settlement service result regression");
    }

    private void TestCanonicalResultDictionaryShape()
    {
        using var result = new SettlementServiceResult
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
        PendingCharacterReward pendingReward = BuildPendingReward("hero_training_reward");
        result.SetPendingCharacterRewardsTyped(new[]
        {
            pendingReward,
        });
        GodotRefCountedDisposer.DisposeIfValid(pendingReward);
        result.SetQuestProgressEventsTyped(new[]
        {
            BuildSettlementQuestProgressEvent("service:training"),
        });
        result.SetServiceSideEffects(new GDictionary
        {
            ["hp_restored"] = new GDictionary { ["hero"] = 12 },
        });

        GDictionary dictionary = SettlementServiceResultProjection.Project(result);

        _test.True(dictionary.ContainsKey("pending_character_rewards"), "结果应包含 pending_character_rewards。");
        _test.True(dictionary.ContainsKey("service_side_effects"), "结果应包含 service_side_effects。");
        _test.False(dictionary.ContainsKey("pending_mastery_rewards"), "结果不应再包含 pending_mastery_rewards。");
        _test.False(dictionary.ContainsKey("effects"), "结果不应再包含 effects。");
        _test.Eq(DictInt(dictionary, "gold_delta", 0), -50, "gold_delta 应保留。");
        _test.Eq(DictArray(dictionary, "pending_character_rewards").Count, 1, "pending rewards 数量应保持稳定。");
        GDictionary sideEffects = DictDictionary(dictionary, "service_side_effects");
        GDictionary hpRestored = DictDictionary(sideEffects, "hp_restored");
        _test.Eq(DictInt(hpRestored, "hero", 0), 12, "service_side_effects 应保留具体副作用。");
    }

    private void TestTypedResultProjectionIsolation()
    {
        GDictionary inventoryDelta = new()
        {
            ["items_removed"] = new GArray
            {
                new GDictionary { ["item_id"] = "training_ticket", ["quantity"] = 1 },
            },
        };
        PendingCharacterReward pendingReward = BuildPendingReward("hero_projection_reward");
        QuestProgressService.QuestProgressEventData questProgressEvent =
            BuildSettlementQuestProgressEvent("service:training");
        GDictionary serviceSideEffects = new()
        {
            ["fog_revealed"] = new GArray { new Vector2I(1, 2) },
        };
        using var result = new SettlementServiceResult
        {
            Success = false,
            Message = "据点结果",
            PersistPartyState = true,
            PersistPlayerCoord = true,
            GoldDelta = -12,
        };
        result.SetInventoryDelta(inventoryDelta);
        result.SetPendingCharacterRewardsTyped(new[] { pendingReward });
        result.SetQuestProgressEventsTyped(new[] { questProgressEvent });
        result.SetServiceSideEffects(serviceSideEffects);

        inventoryDelta["items_removed"] = new GArray();
        pendingReward.summary_text = "mutated";
        serviceSideEffects["fog_revealed"] = new GArray();

        try
        {
            _test.False(result.Success, "typed result 应保留 success。");
            _test.Eq(result.Message, "据点结果", "typed result 应保留 message。");
            _test.True(result.PersistPartyState, "typed result 应保留 persist_party_state。");
            _test.True(result.PersistPlayerCoord, "typed result 应保留 persist_player_coord。");
            _test.Eq(result.GoldDelta, -12, "typed result 应保留 gold_delta。");
            _test.Eq(
                DictArray(SettlementServiceResultProjection.ProjectInventoryDelta(result), "items_removed").Count,
                1,
                "inventory_delta 应隔离输入 mutation。"
            );
            IReadOnlyList<PendingCharacterReward> rewards = result.PendingCharacterRewards;
            try
            {
                _test.Eq(rewards.Count, 1, "pending_character_rewards 应保留 typed 列表数量。");
                _test.Eq(
                    rewards[0].summary_text,
                    "Hero 完成旅店训练。",
                    "pending_character_rewards 应隔离输入 dictionary mutation。"
                );
            }
            finally
            {
                SettlementServiceResultProjection.DisposePendingRewards(rewards);
            }
            _test.Eq(result.QuestProgressEvents.Count, 1, "quest_progress_events 应保留 typed 列表数量。");
            _test.Eq(
                DictInt(
                    QuestProgressResultProjection.Project(result.QuestProgressEvents[0]),
                    "progress_delta",
                    0
                ),
                1,
                "quest_progress_events 应隔离输入 dictionary mutation。"
            );
            _test.Eq(
                DictArray(SettlementServiceResultProjection.ProjectServiceSideEffects(result), "fog_revealed").Count,
                1,
                "service_side_effects 应隔离输入 mutation。"
            );

            GDictionary projected = SettlementServiceResultProjection.Project(result);
            DictArray(projected, "pending_character_rewards")[0].AsGodotDictionary()["summary_text"] = "projection mutated";
            rewards = result.PendingCharacterRewards;
            try
            {
                _test.Eq(
                    rewards[0].summary_text,
                    "Hero 完成旅店训练。",
                    "SettlementServiceResultProjection mutation 不应回写 typed result。"
                );
            }
            finally
            {
                SettlementServiceResultProjection.DisposePendingRewards(rewards);
            }
        }
        finally
        {
            GodotRefCountedDisposer.DisposeIfValid(pendingReward);
        }
    }

    private static PendingCharacterReward BuildPendingReward(string rewardId)
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
        return reward;
    }

    private static QuestProgressService.QuestProgressEventData BuildSettlementQuestProgressEvent(
        string targetId
    )
    {
        return QuestProgressService.QuestProgressEventData.FromDictionary(
            new GDictionary
            {
                ["event_type"] = "progress",
                ["objective_type"] = "settlement_action",
                ["target_id"] = targetId,
                ["progress_delta"] = 1,
                ["world_step"] = 12,
                ["action_id"] = targetId,
                ["settlement_id"] = "settlement_alpha",
                ["member_id"] = "hero",
            }
        );
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
}
