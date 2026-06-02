using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_score_save_probability_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestScoreServiceHasTypedEffectListBuildInput();
            TestAiDamageEstimateWeightsPartialSaveProbability();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI score save probability regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI score save probability regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestScoreServiceHasTypedEffectListBuildInput()
    {
        MethodInfo typedBuild = typeof(BattleAiScoreService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                if (method.Name != nameof(BattleAiScoreService.BuildSkillScoreInput))
                {
                    return false;
                }
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length >= 5
                    && parameters[4].ParameterType == typeof(IReadOnlyList<CombatEffectDef>);
            });
        AssertTrue(
            typedBuild != null,
            "BattleAiScoreService 应提供 typed IReadOnlyList<CombatEffectDef> score input 入口。"
        );
    }

    private void TestAiDamageEstimateWeightsPartialSaveProbability()
    {
        BattleUnitState source = MakeUnit("caster", "player", 30);
        BattleUnitState target = MakeUnit("target", "hostile", 35);
        var state = new BattleState();
        state.units[source.unit_id] = source;
        state.units[target.unit_id] = target;

        var context = new BattleAiContext
        {
            state = state,
            unit_state = source,
        };

        var skill = new SkillDef
        {
            skill_id = "save_weighted_fire",
            display_name = "Save Weighted Fire",
        };

        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "fire",
            power = 40,
            save_dc = 11,
            save_ability = "constitution",
            save_tag = "fireball",
            save_partial_on_success = true,
        };

        var preview = new BattlePreview
        {
            allowed = true,
        };
        preview.target_unit_ids.Add(target.unit_id);

        var scoreService = new BattleAiScoreService();
        BattleAiScoreInput scoreInput = scoreService.BuildSkillScoreInput(
            context,
            skill,
            null,
            preview,
            new[] { effect },
            new GDictionary()
        );

        AssertEq(scoreInput.estimated_damage, 30, "40 点伤害、50% 半伤豁免时，AI 期望伤害应为 30。");
        AssertEq(
            scoreInput.estimated_lethal_target_count,
            0,
            "目标 35 HP 时，豁免加权后不应再被估成稳定击杀。"
        );

        string targetKey = target.unit_id.ToString();
        AssertTrue(
            scoreInput.save_estimates_by_target_id.ContainsKey(targetKey),
            "score_input 应暴露目标豁免概率估算。"
        );
        if (!scoreInput.save_estimates_by_target_id.ContainsKey(targetKey))
        {
            return;
        }

        GArray targetEstimates = scoreInput
            .save_estimates_by_target_id[targetKey]
            .AsGodotArray();
        AssertTrue(targetEstimates.Count > 0, "目标豁免估算列表不应为空。");
        if (targetEstimates.Count == 0)
        {
            return;
        }

        GDictionary estimate = targetEstimates[0].AsGodotDictionary();
        AssertEq(
            estimate.GetValueOrDefault("save_success_rate_percent", -1).AsInt32(),
            50,
            "DC11/CON0 的豁免成功率应为 50%。"
        );
        AssertEq(
            estimate.GetValueOrDefault("damage_after_save_estimate", -1).AsInt32(),
            30,
            "trace 中也应保留豁免加权后的期望伤害。"
        );
    }

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId, int hp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_hp = hp,
            is_alive = true,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), hp);
        unit.attribute_snapshot.set_value("constitution", 10);
        unit.attribute_snapshot.set_value("intelligence", 10);
        unit.attribute_snapshot.set_value("willpower", 10);
        unit.attribute_snapshot.set_value("agility", 10);
        return unit;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} (expected={expected}, actual={actual})");
        }
    }
}
