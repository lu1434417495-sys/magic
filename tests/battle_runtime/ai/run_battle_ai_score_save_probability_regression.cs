using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_score_save_probability_regression : SceneTree
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

        Quit(_test.Finish("Battle AI score save probability regression"));
    }

    private void TestAiDamageEstimateWeightsPartialSaveProbability()
    {
        BattleUnitState source = MakeUnit("caster", "player", 30);
        BattleUnitState target = MakeUnit("target", "hostile", 35);
        var state = new BattleState();
        SkillDef skill = null;
        CombatEffectDef effect = null;
        BattlePreview preview = null;
        BattleAiScoreInput scoreInput = null;
        try
        {
            state.SetUnit(source);
            state.SetUnit(target);

            var context = new BattleAiContext
            {
                state = state,
                unit_state = source,
            };

            skill = new SkillDef
            {
                skill_id = "save_weighted_fire",
                display_name = "Save Weighted Fire",
            };

            effect = new CombatEffectDef
            {
                effect_type = "damage",
                damage_tag = "fire",
                power = 40,
                save_dc = 11,
                save_ability = "constitution",
                save_tag = "fireball",
                save_partial_on_success = true,
            };

            preview = new BattlePreview
            {
                allowed = true,
            };
            preview.AddTargetUnitId(target.unit_id);

            var scoreService = new BattleAiScoreService();
            scoreInput = scoreService.BuildSkillScoreInput(
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
        finally
        {
            if (scoreInput != null)
                BattleTestFixture.DisposeBattleAiScoreInput(scoreInput);
            else
                BattleTestFixture.DisposeBattlePreview(preview);
            GodotSharpCleanup.DisposeGodotObject(effect);
            BattleTestFixture.DisposeSkill(skill);
            BattleTestFixture.DisposeBattleState(state);
        }
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
