using System.Collections.Generic;
using Godot;

public partial class run_quest_config_validation : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        var registry = new QuestContentRegistry();
        registry.LoadFromDirectory("res://data/configs/quests");

        IReadOnlyList<string> registryErrors = registry.GetValidationErrors();
        if (registryErrors.Count > 0)
        {
            _test.Fail(string.Join("\n", registryErrors));
        }

        IReadOnlyDictionary<StringName, QuestDef> questDefs = registry.GetQuestDefsTyped();

        using ItemContentRegistry itemRegistry = new();
        using SkillContentRegistry skillRegistry = new();
        using EnemyContentRegistry enemyRegistry = new();

        Dictionary<StringName, ItemDef> itemDefs = new(itemRegistry.GetItemDefsTyped());
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            skillRegistry.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            enemyRegistry.GetEnemyTemplatesTyped();

        List<string> validatorErrors = QuestContentValidator.ValidateTyped(
            questDefs,
            itemDefs,
            skillDefinitions,
            enemyTemplates
        );

        if (validatorErrors.Count > 0)
        {
            _test.Fail(string.Join("\n", validatorErrors));
        }

        Quit(_test.Finish("Quest config validation"));
    }
}
