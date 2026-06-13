using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_quest_progress_regression : SceneTree
{
    private const string QuestId = "contract_manual_drill";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestQuestCommandUsesTypedRuntimeResults();
        TestQuestProgressPayloadAndProgressionFactsUseFormalStringKeys();
        TestTextCommandQuestProgressUsesTypedPayloadBoundary();

        Quit(_test.Finish("Text command quest progress regression"));
    }

    private void TestQuestCommandUsesTypedRuntimeResults()
    {
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandAcceptQuestTyped", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandAcceptQuestTyped() 应暴露 typed runtime result。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandProgressQuestTyped", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandProgressQuestTyped() 应暴露 typed runtime result。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod("CommandCompleteQuestTyped", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade.CommandCompleteQuestTyped() 应暴露 typed runtime result。"
        );
        _test.True(
            typeof(GameRuntimeQuestCommandHandler).GetMethod(
                "command_accept_quest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            ) == null
                && typeof(GameRuntimeQuestCommandHandler).GetMethod(
                    "command_progress_quest",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                ) == null
                && typeof(GameRuntimeQuestCommandHandler).GetMethod(
                    "command_complete_quest",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                ) == null,
            "GameRuntimeQuestCommandHandler 不应继续保留 accept/progress/complete 的 wrapper-only dictionary surface。"
        );
    }

    private void TestTextCommandQuestProgressUsesTypedPayloadBoundary()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            AssertCommandOk(runner.ExecuteLine("game new test"), "game new test 应成功。");

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            _test.True(runtime != null, "quest progress 文本回归应拿到 typed runtime。");
            if (runtime == null)
                return;
            _test.True(
                runtime._character_management.AcceptQuest(QuestId, runtime.GetWorldStep(), false),
                "quest progress 文本回归前置：应能直接把 contract_manual_drill 置为 active。"
            );
            runtime._party_state = runtime._character_management.GetPartyState();
            _test.True(
                SnapshotStringArrayContains(
                    session.BuildSnapshot(),
                    "party",
                    "quests",
                    "active_quest_ids",
                    QuestId
                ),
                "接取 contract_manual_drill 后 active_quest_ids 应包含该任务。"
            );

            GameTextCommandResult progressResult = runner.ExecuteLine(
                "quest progress contract_manual_drill train_once 1 target_value=2 action_id=service:training"
            );
            AssertCommandApplied(progressResult, "quest progress 应成功。");

            GDictionary activeQuestEntry = FindQuestEntry(progressResult.snapshot, QuestId, "active_quests");
            _test.True(activeQuestEntry.Count > 0, "quest progress 后应能在 active_quests 找到目标任务。");
            if (activeQuestEntry.Count == 0)
                return;

            GDictionary objectiveProgress = Dict(activeQuestEntry, "objective_progress");
            _test.Eq(
                DictInt(objectiveProgress, "train_once", -1),
                1,
                "quest progress 后 objective_progress.train_once 应递增为 1。"
            );
            GDictionary lastProgressContext = Dict(activeQuestEntry, "last_progress_context");
            _test.Eq(
                DictString(lastProgressContext, "action_id", ""),
                "service:training",
                "quest progress 后应保留 typed action_id 上下文。"
            );

            GameTextCommandResult completeResult = runner.ExecuteLine(
                "quest complete contract_manual_drill"
            );
            AssertCommandApplied(completeResult, "quest complete 应成功。");
            _test.True(
                SnapshotStringArrayContains(
                    completeResult.snapshot,
                    "party",
                    "quests",
                    "claimable_quest_ids",
                    QuestId
                ),
                "quest complete 后 claimable_quest_ids 应包含该任务。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private void TestQuestProgressPayloadAndProgressionFactsUseFormalStringKeys()
    {
        var progressPayload = new GDictionary
        {
            ["world_step"] = 12,
            ["target_value"] = 2,
            ["action_id"] = "service:training",
            ["member_id"] = "hero_a",
            ["enemy_template_id"] = "training_dummy",
            ["settlement_id"] = "settlement_alpha",
            ["source_type"] = "facility",
            ["source_id"] = "training_ground",
        };

        QuestProgressCommandPayloadData typedPayload =
            QuestProgressCommandPayloadData.FromDictionary(progressPayload, 3);

        _test.True(typedPayload.IsValid, "quest progress formal payload 应成功解码。");
        _test.Eq(typedPayload.WorldStep, 12, "quest progress formal payload 应保留 world_step。");
        _test.True(typedPayload.HasTargetValue, "quest progress formal payload 应保留 target_value presence。");
        _test.Eq(typedPayload.TargetValue, 2, "quest progress formal payload 应保留 target_value。");
        _test.Eq(typedPayload.ActionId, "service:training", "quest progress formal payload 应保留 action_id。");
        _test.Eq(typedPayload.MemberId, new StringName("hero_a"), "quest progress formal payload 应保留 member_id。");
        _test.Eq(typedPayload.EnemyTemplateId, new StringName("training_dummy"), "quest progress formal payload 应保留 enemy_template_id。");
        _test.Eq(typedPayload.SettlementId, "settlement_alpha", "quest progress formal payload 应保留 settlement_id。");
        _test.Eq(typedPayload.SourceType, new StringName("facility"), "quest progress formal payload 应保留 source_type。");
        _test.Eq(typedPayload.SourceId, new StringName("training_ground"), "quest progress formal payload 应保留 source_id。");

        foreach (
            string fieldName in new[]
            {
                "action_id",
                "member_id",
                "enemy_template_id",
                "settlement_id",
                "source_type",
                "source_id",
            }
        )
        {
            GDictionary stringNamePayload = (GDictionary)progressPayload.Duplicate(true);
            stringNamePayload[fieldName] = new StringName(DictString(progressPayload, fieldName, ""));
            QuestProgressCommandPayloadData rejectedPayload =
                QuestProgressCommandPayloadData.FromDictionary(stringNamePayload, 3);
            _test.True(
                !rejectedPayload.IsValid,
                $"StringName {fieldName} 不应被 quest progress payload 当作正式字符串字段。"
            );
        }

        CharacterKnowledgeChangeFact knowledgeFact =
            CharacterKnowledgeChangeFact.FromDictionary(
                new GDictionary
                {
                    ["knowledge_id"] = "alchemy",
                    ["knowledge_label"] = "Alchemy",
                    ["reason_text"] = "Discovered in archive",
                }
            );
        _test.True(knowledgeFact != null, "knowledge fact formal payload 应成功 materialize。");
        if (knowledgeFact != null)
        {
            _test.Eq(knowledgeFact.KnowledgeId, new StringName("alchemy"), "knowledge fact 应保留 knowledge_id。");
            _test.Eq(knowledgeFact.KnowledgeLabel, "Alchemy", "knowledge fact 应保留 knowledge_label。");
            _test.Eq(knowledgeFact.ReasonText, "Discovered in archive", "knowledge fact 应保留 reason_text。");
        }

        CharacterAttributeChangeFact attributeFact =
            CharacterAttributeChangeFact.FromDictionary(
                new GDictionary
                {
                    ["attribute_id"] = "agility",
                    ["attribute_label"] = "Agility",
                    ["delta"] = 2,
                    ["reason_text"] = "Training",
                    ["progress_delta"] = 40,
                    ["progress_before"] = 10,
                    ["progress_after"] = 50,
                    ["attribute_before"] = 6,
                    ["attribute_after"] = 8,
                }
            );
        _test.True(attributeFact != null, "attribute fact formal payload 应成功 materialize。");
        if (attributeFact != null)
        {
            _test.Eq(attributeFact.AttributeId, new StringName("agility"), "attribute fact 应保留 attribute_id。");
            _test.Eq(attributeFact.AttributeLabel, "Agility", "attribute fact 应保留 attribute_label。");
            _test.Eq(attributeFact.Delta, 2, "attribute fact 应保留 delta。");
            _test.Eq(attributeFact.ReasonText, "Training", "attribute fact 应保留 reason_text。");
            _test.Eq(attributeFact.ProgressDelta, 40, "attribute fact 应保留 progress_delta。");
            _test.Eq(attributeFact.ProgressBefore, 10, "attribute fact 应保留 progress_before。");
            _test.Eq(attributeFact.ProgressAfter, 50, "attribute fact 应保留 progress_after。");
            _test.Eq(attributeFact.AttributeBefore, 6, "attribute fact 应保留 attribute_before。");
            _test.Eq(attributeFact.AttributeAfter, 8, "attribute fact 应保留 attribute_after。");
        }

        CharacterMasteryChangeFact masteryFact =
            CharacterMasteryChangeFact.FromDictionary(
                new GDictionary
                {
                    ["skill_id"] = "slash",
                    ["skill_name"] = "Slash",
                    ["mastery_amount"] = 3,
                    ["source_type"] = "battle",
                    ["source_label"] = "Battle",
                    ["reason_text"] = "First hit",
                }
            );
        _test.True(masteryFact != null, "mastery fact formal payload 应成功 materialize。");
        if (masteryFact != null)
        {
            _test.Eq(masteryFact.SkillId, new StringName("slash"), "mastery fact 应保留 skill_id。");
            _test.Eq(masteryFact.SkillName, "Slash", "mastery fact 应保留 skill_name。");
            _test.Eq(masteryFact.MasteryAmount, 3, "mastery fact 应保留 mastery_amount。");
            _test.Eq(masteryFact.SourceType, new StringName("battle"), "mastery fact 应保留 source_type。");
            _test.Eq(masteryFact.SourceLabel, "Battle", "mastery fact 应保留 source_label。");
            _test.Eq(masteryFact.ReasonText, "First hit", "mastery fact 应保留 reason_text。");
        }
    }

    private static GDictionary FindQuestEntry(
        GDictionary snapshot,
        string questId,
        string listKey
    )
    {
        GDictionary party = Dict(snapshot, "party");
        GDictionary quests = Dict(party, "quests");
        foreach (Variant entryValue in DictArray(quests, listKey))
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictString(entry, "quest_id", "") == questId)
                return entry;
        }
        return new GDictionary();
    }

    private static bool SnapshotStringArrayContains(
        GDictionary snapshot,
        string topLevelKey,
        string nestedKey,
        string arrayKey,
        string expectedValue
    )
    {
        GDictionary topLevel = Dict(snapshot, topLevelKey);
        GDictionary nested = Dict(topLevel, nestedKey);
        foreach (Variant value in DictArray(nested, arrayKey))
        {
            if (value.AsString() == expectedValue)
                return true;
        }
        return false;
    }

    private static string SnapshotString(GDictionary snapshot, string topLevelKey, string nestedKey) =>
        DictString(Dict(snapshot, topLevelKey), nestedKey, "");

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static GArray DictArray(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();

    private static int DictInt(GDictionary dictionary, string key, int fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }

    private void AssertCommandApplied(GameTextCommandResult result, string message)
    {
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }
}
