using System;
using System.Collections;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_enemy_ai_generation_slots_content_regression : SceneTree
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

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestEnemyContentRegistryAcceptsGenerationSlots();
        TestFormalBrainsDeclareGenerationSlots();
        TestFormalBrainsDeclareTransitionRules();

        if (_failures.Count == 0)
        {
            GD.Print("Enemy AI generation slots content regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Enemy AI generation slots content regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestEnemyContentRegistryAcceptsGenerationSlots()
    {
        var registry = new EnemyContentRegistry();
        GStringArray errors = registry.validate();
        AssertTrue(errors.Count == 0, $"EnemyContentRegistry 应接受正式 generation slots: {FormatErrors(errors)}");
    }

    private void TestFormalBrainsDeclareGenerationSlots()
    {
        foreach (string brainPath in BrainPaths)
        {
            EnemyAiBrainDef brain = ResourceLoader.Load<EnemyAiBrainDef>(brainPath);
            AssertTrue(brain != null, $"{brainPath} 应能加载。");
            if (brain == null)
            {
                continue;
            }

            foreach (EnemyAiStateDef stateDef in brain.get_resolved_states())
            {
                if (stateDef == null)
                {
                    continue;
                }

                GStringArray stateErrors = stateDef.validate_schema(
                    brain.brain_id,
                    CollectDeclaredSkillDefs(stateDef)
                );
                AssertTrue(
                    stateErrors.Count == 0,
                    $"{brainPath} state {stateDef.state_id} full schema 应合法: {FormatErrors(stateErrors)}"
                );
                AssertTrue(
                    stateDef.generation_slots != null && stateDef.generation_slots.Count > 0,
                    $"{brainPath} state {stateDef.state_id} 应声明 generation_slots。"
                );
                if (stateDef.generation_slots == null)
                {
                    continue;
                }
                foreach (EnemyAiGenerationSlotDef slot in stateDef.generation_slots)
                {
                    AssertTrue(slot != null, $"{brainPath} state {stateDef.state_id} 不应包含空 generation slot。");
                    if (slot == null)
                    {
                        continue;
                    }

                    GArray errors = slot.validate_schema(
                        $"{brainPath} state {stateDef.state_id}",
                        stateDef.actions
                    );
                    AssertTrue(
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
            AssertTrue(brain != null, $"{brainPath} 应能加载。");
            if (brain == null)
            {
                continue;
            }

            AssertTrue(
                brain.transition_rules != null && brain.transition_rules.Count > 0,
                $"{brainPath} 应声明 transition_rules。"
            );
            GStringArray brainErrors = brain.validate_schema(CollectDeclaredSkillDefsForBrain(brain));
            AssertTrue(
                brainErrors.Count == 0,
                $"{brainPath} transition/full schema 应合法: {FormatErrors(brainErrors)}"
            );

            string rawText = FileAccess.GetFileAsString(brainPath);
            foreach (string oldField in new[] { "retreat_hp_basis_points", "support_hp_basis_points", "pressure_distance" })
            {
                AssertTrue(
                    !rawText.Contains(oldField, StringComparison.Ordinal),
                    $"{brainPath} 不应继续声明旧 transition 字段 {oldField}。"
                );
            }
        }
    }

    private static GDictionary CollectDeclaredSkillDefs(EnemyAiStateDef stateDef)
    {
        var skillDefs = new GDictionary();
        if (stateDef == null)
        {
            return skillDefs;
        }
        foreach (EnemyAiAction action in stateDef.GetTypedActions())
        {
            if (action == null)
            {
                continue;
            }
            foreach (StringName skillId in action.get_declared_skill_ids())
            {
                skillDefs[skillId] = true;
            }
        }
        return skillDefs;
    }

    private static GDictionary CollectDeclaredSkillDefsForBrain(EnemyAiBrainDef brain)
    {
        var skillDefs = new GDictionary();
        if (brain == null)
        {
            return skillDefs;
        }
        foreach (EnemyAiStateDef stateDef in brain.get_resolved_states())
        {
            foreach (Variant key in CollectDeclaredSkillDefs(stateDef).Keys)
            {
                skillDefs[key] = true;
            }
        }
        return skillDefs;
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
