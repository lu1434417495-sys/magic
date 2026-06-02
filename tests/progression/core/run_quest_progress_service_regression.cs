using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_quest_progress_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFormalProgressEventSchema();
        TestStringKeyOnlyQuestDefsAreRejected();
        TestMissingObjectiveTargetValueDoesNotDefaultToOne();

        if (_failures.Count == 0)
        {
            GD.Print("Quest progress service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Quest progress service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestFormalProgressEventSchema()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_formal_progress_event",
            "正式进度事件",
            "train_once",
            QuestDef.OBJECTIVE_SETTLEMENT_ACTION(),
            "service:training",
            2
        );
        PartyState partyState = new();
        CharacterManagementModule manager = BuildManager(partyState, questDef);

        AssertTrue(manager.accept_quest(questDef.quest_id, 1), "测试任务应可被正式接取。");
        QuestState activeQuest = partyState.get_active_quest_state(questDef.quest_id);
        AssertTrue(activeQuest != null, "接取后应存在 active quest。");
        if (activeQuest == null)
            return;

        int badEventIndex = 0;
        foreach (GDictionary badEvent in BuildBadProgressEvents(questDef.quest_id))
        {
            badEventIndex++;
            GDictionary summary = manager.apply_quest_progress_events(new GArray { badEvent }, 2);
            AssertEq(
                SummaryCount(summary, "progressed_quest_ids"),
                0,
                $"坏 quest progress event #{badEventIndex} 不应推进任务：{Json.Stringify(badEvent)}"
            );
        }
        AssertEq(
            activeQuest.get_objective_progress("train_once"),
            0,
            "amount / 缺 event_type / 字符串字段 / 缺 world_step 不应被兼容成任务进度。"
        );
        AssertTrue(!partyState.has_claimable_quest(questDef.quest_id), "坏 progress event 不应把任务推进到 claimable。");

        GDictionary formalSummary = manager.apply_quest_progress_events(
            new GArray
            {
                new GDictionary
                {
                    ["event_type"] = "progress",
                    ["quest_id"] = questDef.quest_id.ToString(),
                    ["objective_id"] = "train_once",
                    ["progress_delta"] = 1,
                    ["world_step"] = 3,
                },
            },
            3
        );
        AssertEq(SummaryCount(formalSummary, "progressed_quest_ids"), 1, "正式 progress_delta 应能推进任务。");
        AssertEq(activeQuest.get_objective_progress("train_once"), 1, "直接 quest progress event 应从 QuestDef 读取 target_value。");
        AssertTrue(!partyState.has_claimable_quest(questDef.quest_id), "未达到 QuestDef target_value 前不应完成。");

        GDictionary matchedSummary = manager.apply_quest_progress_events(
            new GArray
            {
                new GDictionary
                {
                    ["event_type"] = "progress",
                    ["objective_type"] = QuestDef.OBJECTIVE_SETTLEMENT_ACTION().ToString(),
                    ["target_id"] = "service:training",
                    ["progress_delta"] = 1,
                    ["world_step"] = 4,
                },
            },
            4
        );
        AssertEq(SummaryCount(matchedSummary, "progressed_quest_ids"), 1, "按 objective_type/target_id 匹配的正式事件应推进任务。");
        AssertTrue(partyState.has_claimable_quest(questDef.quest_id), "达到正式 objective target_value 后任务应进入 claimable。");
    }

    private void TestStringKeyOnlyQuestDefsAreRejected()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_string_key_progress",
            "旧 String key 进度",
            "train_once",
            QuestDef.OBJECTIVE_SETTLEMENT_ACTION(),
            "service:training",
            1
        );
        PartyState partyState = new();
        GDictionary questDefs = new() { [questDef.quest_id.ToString()] = questDef };
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            questDefs
        );

        AssertTrue(
            !manager.accept_quest(questDef.quest_id, 1),
            "String key-only quest_def 不应被 QuestProgressService 恢复。"
        );
        AssertTrue(
            !partyState.has_active_quest(questDef.quest_id),
            "String key-only quest_def accept 失败后不应写入 active_quests。"
        );
    }

    private void TestMissingObjectiveTargetValueDoesNotDefaultToOne()
    {
        QuestDef questDef = BuildQuestDef(
            "contract_missing_target_value",
            "缺目标值",
            "bad_target",
            QuestDef.OBJECTIVE_SETTLEMENT_ACTION(),
            "service:bad",
            0
        );
        questDef.objective_defs[0].Remove("target_value");
        PartyState partyState = new();
        CharacterManagementModule manager = BuildManager(partyState, questDef);

        AssertTrue(manager.accept_quest(questDef.quest_id, 5), "缺 target_value 的坏夹具仍可用于验证 service 拒绝进度事件。");
        manager.apply_quest_progress_events(
            new GArray
            {
                new GDictionary
                {
                    ["event_type"] = "progress",
                    ["quest_id"] = questDef.quest_id.ToString(),
                    ["objective_id"] = "bad_target",
                    ["progress_delta"] = 1,
                    ["world_step"] = 6,
                },
            },
            6
        );
        QuestState questState = partyState.get_active_quest_state(questDef.quest_id);
        AssertTrue(questState != null, "缺 target_value 任务应保持 active。");
        if (questState != null)
            AssertEq(questState.get_objective_progress("bad_target"), 0, "缺正式 target_value 时不应按默认 1 推进任务。");
    }

    private static CharacterManagementModule BuildManager(PartyState partyState, QuestDef questDef)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary { [questDef.quest_id] = questDef }
        );
        return manager;
    }

    private static QuestDef BuildQuestDef(
        string questId,
        string displayName,
        string objectiveId,
        StringName objectiveType,
        string targetId,
        int targetValue
    )
    {
        QuestDef questDef = new()
        {
            quest_id = questId,
            display_name = displayName,
        };
        GDictionary objectiveDef = new()
        {
            ["objective_id"] = objectiveId,
            ["objective_type"] = objectiveType,
            ["target_id"] = targetId,
            ["target_value"] = targetValue,
        };
        questDef.objective_defs.Add(objectiveDef);
        return questDef;
    }

    private static GArray BuildBadProgressEvents(StringName questId) =>
        new()
        {
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["world_step"] = 2,
                ["amount"] = 1,
            },
            new GDictionary
            {
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = 2,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = "1",
                ["world_step"] = 2,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = 2,
                ["target_value"] = "2",
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
            },
            new GDictionary
            {
                ["event_type"] = "progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = "2",
            },
            new GDictionary
            {
                ["event_type"] = "legacy_progress",
                ["quest_id"] = questId.ToString(),
                ["objective_id"] = "train_once",
                ["progress_delta"] = 1,
                ["world_step"] = 2,
            },
        };

    private static int SummaryCount(GDictionary summary, string key)
    {
        if (summary == null || !summary.ContainsKey(key))
            return 0;
        Variant value = summary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray().Count : 0;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }
}
