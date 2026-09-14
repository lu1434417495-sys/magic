using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_quest_progress_regression : LifecycleTestSceneTree
{
    private const string QuestId = "contract_manual_drill";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestQuestProgressPayloadUsesFormalStringFields();
        TestProgressionDeltaFactsUseFormalProjection();
        TestTextCommandQuestProgressUsesTypedPayloadBoundary();

        RequestTestExit(_test.Finish("Text command quest progress regression"));
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
            CharacterManagementModule characterManagement = runtime.GetCharacterManagement();
            _test.True(
                characterManagement.AcceptQuest(QuestId, runtime.GetWorldStep(), false),
                "quest progress 文本回归前置：应能直接把 contract_manual_drill 置为 active。"
            );
            runtime.SetPartyState(characterManagement.GetPartyState());
            IReadOnlyDictionary<string, object> acceptedSnapshot =
                session.BuildSnapshotPlain();
            _test.True(
                SnapshotStringArrayContains(
                    acceptedSnapshot,
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

            IReadOnlyDictionary<string, object> activeQuestEntry = FindQuestEntry(
                progressResult.SnapshotTyped,
                QuestId,
                "active_quests"
            );
            _test.True(activeQuestEntry.Count > 0, "quest progress 后应能在 active_quests 找到目标任务。");
            if (activeQuestEntry.Count == 0)
                return;

            IReadOnlyDictionary<string, object> objectiveProgress = PlainDict(
                activeQuestEntry,
                "objective_progress"
            );
            _test.Eq(
                PlainInt(objectiveProgress, "train_once", -1),
                1,
                "quest progress 后 objective_progress.train_once 应递增为 1。"
            );
            IReadOnlyDictionary<string, object> lastProgressContext = PlainDict(
                activeQuestEntry,
                "last_progress_context"
            );
            _test.Eq(
                PlainString(lastProgressContext, "action_id", ""),
                "service:training",
                "quest progress 后应保留 typed action_id 上下文。"
            );

            GameTextCommandResult completeResult = runner.ExecuteLine(
                "quest complete contract_manual_drill"
            );
            AssertCommandApplied(completeResult, "quest complete 应成功。");
            _test.True(
                SnapshotStringArrayContains(
                    completeResult.SnapshotTyped,
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

    private void TestQuestProgressPayloadUsesFormalStringFields()
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
    }

    private void TestProgressionDeltaFactsUseFormalProjection()
    {
        var progressionDelta = new CharacterProgressionDelta { member_id = "hero_a" };
        progressionDelta.AddKnowledgeChange(
            new CharacterKnowledgeChangeFact(
                "alchemy",
                "Alchemy",
                "Discovered in archive"
            )
        );
        progressionDelta.AddAttributeChange(
            new CharacterAttributeChangeFact(
                "agility",
                "Agility",
                2,
                "Training",
                40,
                10,
                50,
                6,
                8
            )
        );
        progressionDelta.AddMasteryChange(
            new CharacterMasteryChangeFact(
                "slash",
                "Slash",
                3,
                "battle",
                "Battle",
                "First hit"
            )
        );

        using GDictionary deltaPayload = progressionDelta.ToDictionary();
        using GArray knowledgeChanges = ReadFactArray(deltaPayload, "knowledge_changes");
        using GArray attributeChanges = ReadFactArray(deltaPayload, "attribute_changes");
        using GArray masteryChanges = ReadFactArray(deltaPayload, "mastery_changes");
        _test.Eq(knowledgeChanges.Count, 1, "progression delta 应投影一条 knowledge change。");
        _test.Eq(attributeChanges.Count, 1, "progression delta 应投影一条 attribute change。");
        _test.Eq(masteryChanges.Count, 1, "progression delta 应投影一条 mastery change。");

        using GDictionary knowledgePayload = ReadFirstFactPayload(
            knowledgeChanges,
            "knowledge_changes"
        );
        AssertStringNameField(knowledgePayload, "knowledge_id", "alchemy", "knowledge change");
        AssertStringField(knowledgePayload, "knowledge_label", "Alchemy", "knowledge change");
        AssertStringField(
            knowledgePayload,
            "reason_text",
            "Discovered in archive",
            "knowledge change"
        );

        using GDictionary attributePayload = ReadFirstFactPayload(
            attributeChanges,
            "attribute_changes"
        );
        AssertStringNameField(attributePayload, "attribute_id", "agility", "attribute change");
        AssertStringField(attributePayload, "attribute_label", "Agility", "attribute change");
        AssertIntField(attributePayload, "delta", 2, "attribute change");
        AssertStringField(attributePayload, "reason_text", "Training", "attribute change");
        AssertIntField(attributePayload, "progress_delta", 40, "attribute change");
        AssertIntField(attributePayload, "progress_before", 10, "attribute change");
        AssertIntField(attributePayload, "progress_after", 50, "attribute change");
        AssertIntField(attributePayload, "attribute_before", 6, "attribute change");
        AssertIntField(attributePayload, "attribute_after", 8, "attribute change");

        using GDictionary masteryPayload = ReadFirstFactPayload(
            masteryChanges,
            "mastery_changes"
        );
        AssertStringNameField(masteryPayload, "skill_id", "slash", "mastery change");
        AssertStringField(masteryPayload, "skill_name", "Slash", "mastery change");
        AssertIntField(masteryPayload, "mastery_amount", 3, "mastery change");
        AssertStringNameField(masteryPayload, "source_type", "battle", "mastery change");
        AssertStringField(masteryPayload, "source_label", "Battle", "mastery change");
        AssertStringField(masteryPayload, "reason_text", "First hit", "mastery change");

        CharacterProgressionDelta restoredDelta = CharacterProgressionDelta.FromDictionary(
            deltaPayload
        );
        _test.True(restoredDelta != null, "CharacterProgressionDelta formal payload 应能 roundtrip。");
        if (restoredDelta != null)
        {
            _test.Eq(restoredDelta.KnowledgeChangesTyped.Count, 1, "roundtrip 应保留 knowledge change。");
            _test.Eq(restoredDelta.AttributeChangesTyped.Count, 1, "roundtrip 应保留 attribute change。");
            _test.Eq(restoredDelta.MasteryChangesTyped.Count, 1, "roundtrip 应保留 mastery change。");
            if (restoredDelta.KnowledgeChangesTyped.Count == 1)
            {
                CharacterKnowledgeChangeFact restoredKnowledge =
                    restoredDelta.KnowledgeChangesTyped[0];
                _test.Eq(restoredKnowledge.KnowledgeId, new StringName("alchemy"), "roundtrip 应保留 knowledge_id。");
                _test.Eq(restoredKnowledge.KnowledgeLabel, "Alchemy", "roundtrip 应保留 knowledge_label。");
                _test.Eq(restoredKnowledge.ReasonText, "Discovered in archive", "roundtrip 应保留 knowledge reason_text。");
            }
            if (restoredDelta.AttributeChangesTyped.Count == 1)
            {
                CharacterAttributeChangeFact restoredAttribute =
                    restoredDelta.AttributeChangesTyped[0];
                _test.Eq(restoredAttribute.AttributeId, new StringName("agility"), "roundtrip 应保留 attribute_id。");
                _test.Eq(restoredAttribute.Delta, 2, "roundtrip 应保留 attribute delta。");
                _test.Eq(restoredAttribute.ProgressAfter, 50, "roundtrip 应保留 attribute progress_after。");
                _test.Eq(restoredAttribute.AttributeAfter, 8, "roundtrip 应保留 attribute_after。");
            }
            if (restoredDelta.MasteryChangesTyped.Count == 1)
            {
                CharacterMasteryChangeFact restoredMastery = restoredDelta.MasteryChangesTyped[0];
                _test.Eq(restoredMastery.SkillId, new StringName("slash"), "roundtrip 应保留 mastery skill_id。");
                _test.Eq(restoredMastery.MasteryAmount, 3, "roundtrip 应保留 mastery_amount。");
                _test.Eq(restoredMastery.SourceType, new StringName("battle"), "roundtrip 应保留 mastery source_type。");
            }
        }

        AssertProgressionDeltaRejectsWrongFactFieldType(
            deltaPayload,
            "knowledge_changes",
            "knowledge_label",
            new StringName("Alchemy"),
            "knowledge_label 的 StringName 冒充 string 时应拒绝整个 progression delta。"
        );
        AssertProgressionDeltaRejectsWrongFactFieldType(
            deltaPayload,
            "attribute_changes",
            "delta",
            "2",
            "attribute delta 的 string 冒充 int 时应拒绝整个 progression delta。"
        );
        AssertProgressionDeltaRejectsWrongFactFieldType(
            deltaPayload,
            "mastery_changes",
            "source_label",
            new StringName("Battle"),
            "mastery source_label 的 StringName 冒充 string 时应拒绝整个 progression delta。"
        );
    }

    private GArray ReadFactArray(GDictionary payload, string key)
    {
        _test.True(payload != null && payload.ContainsKey(key), $"progression delta 应包含 {key} formal key。");
        if (payload == null || !payload.ContainsKey(key))
            return new GArray();
        Variant value = payload[key];
        _test.Eq(value.VariantType, Variant.Type.Array, $"progression delta {key} 应是 Array。");
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private GDictionary ReadFirstFactPayload(GArray values, string key)
    {
        if (values == null || values.Count == 0)
        {
            _test.Fail($"progression delta {key} 应至少包含一条 fixture fact。");
            return new GDictionary();
        }
        Variant value = values[0];
        _test.Eq(value.VariantType, Variant.Type.Dictionary, $"progression delta {key}[0] 应是 Dictionary。");
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private void AssertStringNameField(
        GDictionary payload,
        string key,
        StringName expected,
        string ownerLabel
    )
    {
        _test.True(payload != null && payload.ContainsKey(key), $"{ownerLabel} 应包含 {key} formal key。");
        if (payload == null || !payload.ContainsKey(key))
            return;
        Variant value = payload[key];
        _test.Eq(value.VariantType, Variant.Type.StringName, $"{ownerLabel}.{key} 应投影为 StringName。");
        if (value.VariantType == Variant.Type.StringName)
            _test.Eq(value.AsStringName(), expected, $"{ownerLabel}.{key} 应保留 formal value。");
    }

    private void AssertStringField(
        GDictionary payload,
        string key,
        string expected,
        string ownerLabel
    )
    {
        _test.True(payload != null && payload.ContainsKey(key), $"{ownerLabel} 应包含 {key} formal key。");
        if (payload == null || !payload.ContainsKey(key))
            return;
        Variant value = payload[key];
        _test.Eq(value.VariantType, Variant.Type.String, $"{ownerLabel}.{key} 应投影为 string。");
        if (value.VariantType == Variant.Type.String)
            _test.Eq(value.AsString(), expected, $"{ownerLabel}.{key} 应保留 formal value。");
    }

    private void AssertIntField(
        GDictionary payload,
        string key,
        int expected,
        string ownerLabel
    )
    {
        _test.True(payload != null && payload.ContainsKey(key), $"{ownerLabel} 应包含 {key} formal key。");
        if (payload == null || !payload.ContainsKey(key))
            return;
        Variant value = payload[key];
        _test.Eq(value.VariantType, Variant.Type.Int, $"{ownerLabel}.{key} 应投影为 int。");
        if (value.VariantType == Variant.Type.Int)
            _test.Eq(value.AsInt32(), expected, $"{ownerLabel}.{key} 应保留 formal value。");
    }

    private void AssertProgressionDeltaRejectsWrongFactFieldType(
        GDictionary validPayload,
        string collectionKey,
        string fieldKey,
        Variant wrongValue,
        string message
    )
    {
        using GDictionary invalidPayload = (GDictionary)validPayload.Duplicate(true);
        if (!invalidPayload.ContainsKey(collectionKey))
        {
            _test.Fail($"类型拒绝 fixture 缺少 {collectionKey}。");
            return;
        }
        using GArray changes = invalidPayload[collectionKey].AsGodotArray();
        if (changes.Count == 0 || changes[0].VariantType != Variant.Type.Dictionary)
        {
            _test.Fail($"类型拒绝 fixture 缺少 {collectionKey}[0] dictionary。");
            return;
        }
        using GDictionary change = changes[0].AsGodotDictionary();
        change[fieldKey] = wrongValue;

        _test.True(CharacterProgressionDelta.FromDictionary(invalidPayload) == null, message);
    }

    private static IReadOnlyDictionary<string, object> FindQuestEntry(
        IReadOnlyDictionary<string, object> snapshot,
        string questId,
        string listKey
    )
    {
        IReadOnlyDictionary<string, object> party = PlainDict(snapshot, "party");
        IReadOnlyDictionary<string, object> quests = PlainDict(party, "quests");
        foreach (object entryValue in PlainArray(quests, listKey))
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (PlainString(entry, "quest_id", "") == questId)
                return entry;
        }
        return new Dictionary<string, object>();
    }

    private static bool SnapshotStringArrayContains(
        IReadOnlyDictionary<string, object> snapshot,
        string topLevelKey,
        string nestedKey,
        string arrayKey,
        string expectedValue
    )
    {
        IReadOnlyDictionary<string, object> topLevel = PlainDict(snapshot, topLevelKey);
        IReadOnlyDictionary<string, object> nested = PlainDict(topLevel, nestedKey);
        foreach (object value in PlainArray(nested, arrayKey))
        {
            if (PlainStringValue(value) == expectedValue)
                return true;
        }
        return false;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private static IReadOnlyDictionary<string, object> PlainDict(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is IReadOnlyDictionary<string, object> nested
            ? nested
            : new Dictionary<string, object>();

    private static IReadOnlyList<object> PlainArray(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is IReadOnlyList<object> array
            ? array
            : System.Array.Empty<object>();

    private static int PlainInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback
    ) =>
        dictionary != null && dictionary.TryGetValue(key, out object value)
            ? value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                _ => fallback,
            }
            : fallback;

    private static string PlainString(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        string fallback
    ) =>
        dictionary != null && dictionary.TryGetValue(key, out object value)
            ? PlainStringValue(value, fallback)
            : fallback;

    private static string PlainStringValue(object value, string fallback = "") =>
        value switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => fallback,
        };

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(
            result != null && !result.skipped && result.ok,
            $"{message} skipped={result?.skipped} message={result?.message}"
        );
    }

    private void AssertCommandApplied(GameTextCommandResult result, string message)
    {
        _test.True(
            result != null && !result.skipped && result.ok,
            $"{message} skipped={result?.skipped} message={result?.message}"
        );
    }
}
