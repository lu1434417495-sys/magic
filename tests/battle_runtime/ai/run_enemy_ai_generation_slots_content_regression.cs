using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_enemy_ai_generation_slots_content_regression : LifecycleTestSceneTree
{
    private static readonly string[] BrainPaths =
    {
        "res://data/configs/enemies/brains/frontline_bulwark.tres",
        "res://data/configs/enemies/brains/healer_controller.tres",
        "res://data/configs/enemies/brains/mage_controller.tres",
        "res://data/configs/enemies/brains/melee_aggressor.tres",
        "res://data/configs/enemies/brains/ranged_archer.tres",
        "res://data/configs/enemies/brains/ranged_controller.tres",
        "res://data/configs/enemies/brains/ranged_suppressor.tres",
    };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestEnemyContentRegistryAcceptsGenerationSlots();
        TestFormalBrainsDeclareGenerationSlots();
        TestFormalBrainsDeclareTransitionRules();

        RequestTestExit(_test.Finish("Enemy AI generation slots content regression"));
    }

    private void TestEnemyContentRegistryAcceptsGenerationSlots()
    {
        using TestContentResourceLoader loader = new();
        using SkillContentRegistry skills = new(loader);
        using ItemContentRegistry items = new(loader);
        using EnemyContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.Rebuild(
            new EnemyContentValidationContext(
                items.GetItemDefsTyped(),
                skills.GetSkillDefinitionsTyped()
            )
        );

        GStringArray errors = registry.Validate();
        _test.True(errors.Count == 0, $"EnemyContentRegistry 应接受正式 generation slots: {FormatErrors(errors)}");
        AssertNoDuplicateDependencyLoads(loader, "res://data/configs/skills/");
        AssertNoDuplicateDependencyLoads(loader, "res://data/configs/items/");
        AssertNoDuplicateDependencyLoads(loader, "res://data/configs/items_templates/");
    }

    private void AssertNoDuplicateDependencyLoads(
        TestContentResourceLoader loader,
        string contentPrefix
    )
    {
        IReadOnlyList<string> duplicateLoads = loader.GetDuplicateLoadsUnder(contentPrefix);
        _test.True(
            loader.CountLoadedPathsUnder(contentPrefix) > 0,
            $"EnemyContentRegistry 校验前应提供 {contentPrefix} definition 索引。"
        );
        _test.Eq(
            duplicateLoads.Count,
            0,
            $"EnemyContentRegistry 不应重新加载已提供的 {contentPrefix}: {string.Join(" | ", duplicateLoads)}"
        );
    }

    private void TestFormalBrainsDeclareGenerationSlots()
    {
        using var loader = new TestContentResourceLoader();
        foreach (string brainPath in BrainPaths)
        {
            EnemyAiBrainDef brain = loader.LoadCanonical<EnemyAiBrainDef>(brainPath);
            _test.True(brain != null, $"{brainPath} 应能加载。");
            if (brain == null)
            {
                continue;
            }

            foreach (EnemyAiStateDef stateDef in brain.GetResolvedStates())
            {
                if (stateDef == null)
                {
                    continue;
                }

                GStringArray stateErrors = stateDef.ValidateSchema(
                    brain.brain_id,
                    CollectDeclaredSkillDefinitions(stateDef)
                );
                _test.True(
                    stateErrors.Count == 0,
                    $"{brainPath} state {stateDef.state_id} full schema 应合法: {FormatErrors(stateErrors)}"
                );
                _test.True(
                    stateDef.generation_slots != null && stateDef.generation_slots.Count > 0,
                    $"{brainPath} state {stateDef.state_id} 应声明 generation_slots。"
                );
                EnemyAiStateDefinition stateDefinition = stateDef.ToDefinition();
                _test.Eq(
                    stateDefinition.GenerationSlots.Count,
                    stateDef.generation_slots?.Count ?? 0,
                    $"{brainPath} state {stateDef.state_id} generation slots 应完整投影。"
                );
                foreach (EnemyAiActionDefinition action in stateDefinition.Actions)
                {
                    _test.True(
                        action != null
                            && !typeof(Resource).IsAssignableFrom(action.GetType()),
                        $"{brainPath} state {stateDef.state_id} runtime action 应是 plain definition。"
                    );
                }
                if (stateDef.generation_slots == null)
                {
                    continue;
                }
                foreach (EnemyAiGenerationSlotDef slot in stateDef.generation_slots)
                {
                    _test.True(slot != null, $"{brainPath} state {stateDef.state_id} 不应包含空 generation slot。");
                    if (slot == null)
                    {
                        continue;
                    }

                    GArray errors = slot.ValidateSchema(
                        $"{brainPath} state {stateDef.state_id}",
                        stateDef.actions
                    );
                    _test.True(
                        errors.Count == 0,
                        $"{brainPath} state {stateDef.state_id} slot {slot.slot_id} schema 应合法: {FormatErrors(errors)}"
                    );
                }
            }
        }
    }

    private void TestFormalBrainsDeclareTransitionRules()
    {
        using var loader = new TestContentResourceLoader();
        foreach (string brainPath in BrainPaths)
        {
            EnemyAiBrainDef brain = loader.LoadCanonical<EnemyAiBrainDef>(brainPath);
            _test.True(brain != null, $"{brainPath} 应能加载。");
            if (brain == null)
            {
                continue;
            }

            _test.True(
                brain.transition_rules != null && brain.transition_rules.Count > 0,
                $"{brainPath} 应声明 transition_rules。"
            );
            GStringArray brainErrors = brain.ValidateSchema(CollectDeclaredSkillDefinitionsForBrain(brain));
            _test.True(
                brainErrors.Count == 0,
                $"{brainPath} transition/full schema 应合法: {FormatErrors(brainErrors)}"
            );
            EnemyAiBrainDefinition definition = brain.ToDefinition();
            _test.Eq(
                definition.TransitionRules.Count,
                brain.transition_rules.Count,
                $"{brainPath} transition rules 应完整投影到 immutable brain definition。"
            );
        }
    }

    private static Dictionary<StringName, SkillDefinition> CollectDeclaredSkillDefinitions(
        EnemyAiStateDef stateDef
    )
    {
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>();
        if (stateDef == null)
        {
            return skillDefinitions;
        }
        foreach (EnemyAiAction action in stateDef.GetTypedActions())
        {
            if (action == null)
            {
                continue;
            }
            foreach (StringName skillId in action.GetDeclaredSkillIds())
            {
                skillDefinitions[skillId] = null;
            }
        }
        return skillDefinitions;
    }

    private static Dictionary<StringName, SkillDefinition> CollectDeclaredSkillDefinitionsForBrain(
        EnemyAiBrainDef brain
    )
    {
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>();
        if (brain == null)
        {
            return skillDefinitions;
        }
        foreach (EnemyAiStateDef stateDef in brain.GetResolvedStates())
        {
            foreach (StringName key in CollectDeclaredSkillDefinitions(stateDef).Keys)
            {
                skillDefinitions[key] = null;
            }
        }
        return skillDefinitions;
    }

    private static string FormatErrors(IEnumerable errors)
    {
        var values = new System.Collections.Generic.List<string>();
        foreach (object error in errors)
        {
            values.Add(error?.ToString() ?? "");
        }
        return string.Join("; ", values);
    }

}
