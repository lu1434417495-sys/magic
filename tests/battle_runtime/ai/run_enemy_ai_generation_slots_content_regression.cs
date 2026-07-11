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
        var registry = new EnemyContentRegistry(new TestContentResourceLoader());
        GStringArray errors = registry.Validate();
        _test.True(errors.Count == 0, $"EnemyContentRegistry 应接受正式 generation slots: {FormatErrors(errors)}");
    }

    private void TestFormalBrainsDeclareGenerationSlots()
    {
        foreach (string brainPath in BrainPaths)
        {
            EnemyAiBrainDef brain = ResourceLoader.Load<EnemyAiBrainDef>(brainPath);
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
        foreach (string brainPath in BrainPaths)
        {
            EnemyAiBrainDef brain = ResourceLoader.Load<EnemyAiBrainDef>(brainPath);
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
