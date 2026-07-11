using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_battle_ai_score_save_probability_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestAiDamageEstimateWeightsPartialSaveProbability();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI score save probability regression"));
    }

    private void TestAiDamageEstimateWeightsPartialSaveProbability()
    {
        BattleUnitState source = MakeUnit("caster", "player", 30);
        BattleUnitState target = MakeUnit("target", "hostile", 35);
        var state = new BattleState();
        state.SetUnit(source);
        state.SetUnit(target);

        var context = new BattleAiContext
        {
            state = state,
            unit_state = source,
        };

        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "fire",
            power: 40,
            saveDc: 11,
            saveAbility: "constitution",
            saveTag: "fireball",
            savePartialOnSuccess: true
        );
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "save_weighted_fire",
            "Save Weighted Fire",
            TestSkillDefinitionProjection.BuildCombatProfile(
                "save_weighted_fire",
                effects: new[] { effect }
            )
        );

        var preview = new BattlePreview
        {
            allowed = true,
        };
        preview.AddTargetUnitId(target.unit_id);

        var scoreService = new BattleAiScoreService();
        BattleAiScoreInput scoreInput = scoreService.BuildSkillScoreInput(
            context,
            skill,
            null,
            preview,
            new[] { effect },
            new Dictionary<string, object>(StringComparer.Ordinal)
        );

        _test.Eq(scoreInput.estimated_damage, 30, "40 点伤害、50% 半伤豁免时，AI 期望伤害应为 30。");
        _test.Eq(
            scoreInput.estimated_lethal_target_count,
            0,
            "目标 35 HP 时，豁免加权后不应再被估成稳定击杀。"
        );

        StringName targetKey = target.unit_id;
        _test.True(
            scoreInput.save_estimates_by_target_id.ContainsKey(targetKey),
            "score_input 应暴露目标豁免概率估算。"
        );
        if (!scoreInput.save_estimates_by_target_id.ContainsKey(targetKey))
        {
            return;
        }

        List<BattleAiScoreService.DamageSaveEstimate> targetEstimates = scoreInput
            .save_estimates_by_target_id[targetKey];
        _test.True(targetEstimates.Count > 0, "目标豁免估算列表不应为空。");
        if (targetEstimates.Count == 0)
        {
            return;
        }

        BattleAiScoreService.DamageSaveEstimate estimate = targetEstimates[0];
        _test.Eq(
            estimate.SaveSuccessRatePercent,
            50,
            "DC11/CON0 的豁免成功率应为 50%。"
        );
        _test.Eq(
            estimate.DamageAfterSaveEstimate,
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        return unit;
    }

}
