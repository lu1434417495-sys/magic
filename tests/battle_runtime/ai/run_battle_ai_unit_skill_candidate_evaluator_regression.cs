using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_unit_skill_candidate_evaluator_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestEvaluatorIsPlainCSharpHelper();
            TestFastPreviewRejectsExposeOutOfRangeCounter();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI unit skill candidate evaluator regression"));
    }

    private void TestEvaluatorIsPlainCSharpHelper()
    {
        Type evaluatorType = typeof(BattleAiUnitSkillCandidateEvaluator);
        _test.True(
            evaluatorType.IsSealed,
            "BattleAiUnitSkillCandidateEvaluator 应是 sealed helper。"
        );
    }

    private void TestFastPreviewRejectsExposeOutOfRangeCounter()
    {
        StringName skillId = "ai_preview_range_probe";
        BattleUnitState actor = BuildUnit("preview_range_actor", "hostile", new Vector2I(0, 0));
        BattleUnitState target = BuildUnit("preview_range_target", "player", new Vector2I(5, 0));
        actor.known_active_skill_ids.Add(skillId);

        SkillDef skill = BuildUnitSkill(skillId, rangeValue: 1);
        BattleState state = new()
        {
            battle_id = "unit_skill_preview_counter_regression",
            phase = "unit_acting",
            map_size = new Vector2I(8, 2),
            timeline = new BattleTimelineState(),
            active_unit_id = actor.unit_id,
        };
        state.SetUnit(actor);
        state.SetUnit(target);

        BattleAiContext context = new()
        {
            state = state,
            unit_state = actor,
            grid_service = new BattleGridService(),
            trace_enabled = true,
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
        };
        context.SetSkillDefs(
            new Dictionary<StringName, SkillDef>
            {
                [skill.skill_id] = skill,
            }
        );

        UseUnitSkillAction action = new()
        {
            action_id = "preview_range_action",
            score_bucket_id = "test",
            target_selector = "nearest_enemy",
            desired_min_distance = 0,
            desired_max_distance = 1,
            distance_reference = "target_unit",
        };
        action.skill_ids.Add(skillId);

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(action, context);
        _test.True(decision == null, "超出 fast preview 射程时不应生成 unit-skill 决策。");

        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "trace_enabled 时 evaluator 应记录 action trace。");
        if (traces.Count == 0)
            return;

        AiActionTrace trace = traces[0];
        _test.Eq(trace.EvaluationCount, 1, "单技能单目标应评估一次。");
        _test.Eq(trace.PreviewRejectCount, 1, "fast preview 拒绝仍应计入 preview_reject_count 总数。");
        _test.True(
            trace.CandidateTraceCounters.TryGetValue(
                "fast_preview_reject_out_of_range",
                out int outOfRangeCount
            ),
            "fast preview 射程拒绝应写入细分 counter。"
        );
        _test.Eq(outOfRangeCount, 1, "fast preview 射程拒绝细分 counter 应与总拒绝数一致。");
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            current_hp = 20,
            current_ap = 2,
            current_mp = 10,
            current_stamina = 10,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        unit.RefreshFootprint();
        return unit;
    }

    private static SkillDef BuildUnitSkill(StringName skillId, int rangeValue)
    {
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = new CombatSkillDef
            {
                skill_id = skillId,
                target_mode = "unit",
                target_team_filter = "enemy",
                range_value = rangeValue,
                ap_cost = 0,
                mp_cost = 0,
                stamina_cost = 0,
            },
        };
    }

}
