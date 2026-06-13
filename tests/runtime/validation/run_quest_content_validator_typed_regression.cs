using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_quest_content_validator_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialQuestValidationTypedBoundary();
        TestMissingReferenceErrorsUseTypedBoundary();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Quest content validator typed regression"));
    }

    private void TestOfficialQuestValidationTypedBoundary()
    {
        using ProgressionContentRegistry progressionRegistry = new();
        using ItemContentRegistry itemRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();

        IReadOnlyDictionary<StringName, QuestDef> questDefs =
            progressionRegistry.GetQuestDefsTyped();
        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        Dictionary<StringName, SkillDef> skillDefs = new(progressionRegistry.GetSkillDefsTyped());
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();
        IReadOnlyList<string> registrationErrors =
            progressionRegistry.GetQuestRegistrationErrorsTyped();

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            questDefs,
            itemDefs,
            skillDefs,
            enemyTemplates,
            registrationErrors
        );
        _test.Eq(
            typedErrors.Count,
            0,
            $"正式 quest typed boundary 不应报错: {FormatErrors(typedErrors)}"
        );
    }

    private void TestMissingReferenceErrorsUseTypedBoundary()
    {
        using ItemContentRegistry itemRegistry = new();
        using SkillContentRegistry skillRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();
        using QuestDef invalidQuest = BuildInvalidQuestDef();

        Dictionary<StringName, QuestDef> typedQuestDefs = new() { [invalidQuest.quest_id] = invalidQuest };
        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        Dictionary<StringName, SkillDef> skillDefs = new(skillRegistry.GetSkillDefsTyped());
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();
        List<string> registrationErrors = new() { "typed registration error" };

        List<string> typedErrors = QuestContentValidator.ValidateTyped(
            typedQuestDefs,
            itemDefs,
            skillDefs,
            enemyTemplates,
            registrationErrors
        );
        _test.True(
            typedErrors.Count >= 4,
            $"typed quest validator 应把缺失引用和 registration error 视为非法。 errors={FormatErrors(typedErrors)}"
        );
    }

    private static QuestDef BuildInvalidQuestDef()
    {
        return new QuestDef
        {
            quest_id = "typed_missing_reference_quest",
            display_name = "Typed Missing Reference Quest",
            description = "Regression quest for typed quest validation boundary.",
            provider_interaction_id = "quest_board",
            objective_defs = new Godot.Collections.Array<GDictionary>
            {
                new GDictionary
                {
                    ["objective_id"] = "deliver_missing_relic",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.SubmitItem),
                    ["target_id"] = "missing_relic",
                    ["target_value"] = 1,
                },
                new GDictionary
                {
                    ["objective_id"] = "defeat_missing_enemy",
                    ["objective_type"] = QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy),
                    ["target_id"] = "missing_enemy_template",
                    ["target_value"] = 1,
                },
            },
            reward_entries = new Godot.Collections.Array<GDictionary>
            {
                new GDictionary
                {
                    ["reward_type"] = QuestDef.ToStringName(QuestRewardKind.PendingCharacterReward),
                    ["member_id"] = "hero",
                    ["entries"] = new Godot.Collections.Array
                    {
                        new GDictionary
                        {
                            ["entry_type"] = PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillUnlock),
                            ["target_id"] = "missing_skill_reward",
                            ["amount"] = 1,
                        },
                    },
                },
            },
        };
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
        {
            values.Add(error ?? "");
        }
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

}
