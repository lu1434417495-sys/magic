using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class UseGroundRepositionSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_safe_distance { get; set; } = 3;

    [Export]
    public int safe_distance_margin { get; set; } = 1;

    [Export]
    public int desired_max_distance_bonus { get; set; } = 2;

    [Export]
    public int action_base_score { get; set; } = 1500;

    // Minimum survival-margin improvement (HP-budget points) an escape must buy before it
    // is offered, when the actor faces no lethal risk. Default 1 rejects zero-gain escapes
    // (the mage_blink_escape limit cycle: blinking every turn while already safe). Raise it
    // to make the unit commit to offense sooner; set negative to always escape (legacy).
    // Exposed for simulation-based tuning via action override patches.
    [Export]
    public int min_survival_margin_gain_to_escape { get; set; } = 1;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:ground_reposition_skill");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:ground_reposition_skill");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        var actionTrace = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "ground_reposition_skill" },
                { "target_selector", (string)target_selector },
                { "minimum_safe_distance", minimum_safe_distance },
                { "safe_distance_margin", safe_distance_margin },
                { "desired_max_distance_bonus", desired_max_distance_bonus },
                { "action_base_score", action_base_score },
            }
        );
        if (
            context == null
            || context.state == null
            || context.unit_state == null
            || context.grid_service == null
        )
        {
            _trace_add_block_reason(actionTrace, "missing_context");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        List<BattleUnitState> targets = _sort_target_units_typed(context, "enemy", target_selector);
        if (targets.Count == 0 || targets[0] == null)
        {
            _trace_add_block_reason(actionTrace, "no_valid_targets");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        var focusTarget = targets[0];
        var ctxUnitState = context.unit_state;
        int resolvedSafeDist = Mathf.Max(minimum_safe_distance + safe_distance_margin, 1);
        int currentDist = _distance_from_anchor_to_unit(
            context,
            ctxUnitState,
            ctxUnitState.coord,
            focusTarget
        );
        actionTrace.Metadata["focus_target_unit_id"] = focusTarget.unit_id.ToString();
        actionTrace.Metadata["current_distance"] = currentDist;
        actionTrace.Metadata["resolved_safe_distance"] = resolvedSafeDist;
        if (currentDist >= resolvedSafeDist)
        {
            _trace_add_block_reason(actionTrace, "already_safe");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        foreach (var sidV in _resolve_known_skill_ids(context, skill_ids))
        {
            var sid = sidV;
            _trace_count_increment(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _get_skill_definition(context, sid);
            if (
                skillDefinition?.CombatProfile == null
                || skillDefinition.CombatProfile.TargetModeKind != BattleTargetMode.Ground
            )
            {
                _trace_add_block_reason(
                    actionTrace,
                    skillDefinition == null ? "missing_skill_definition" : "non_ground_skill"
                );
                continue;
            }
            var blockReason = _get_skill_cast_block_reason(context, skillDefinition);
            if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
            {
                _trace_add_block_reason(
                    actionTrace,
                    BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                );
                continue;
            }
            int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
                ctxUnitState,
                skillDefinition,
                context.skill_catalog
            );
            foreach (
                CombatCastVariantDefinition cv in _get_ground_option_definitions(
                    context,
                    skillDefinition
                )
            )
            {
                if (cv == null || _is_charge_option(cv))
                    continue;
                if (!_has_reposition_effect(cv.EffectDefinitions))
                {
                    _trace_add_block_reason(actionTrace, "missing_reposition_effect");
                    continue;
                }
                foreach (
                    List<Vector2I> tcs in _enumerate_ground_target_coord_sets_typed(context, cv)
                )
                {
                    if (tcs.Count != 1)
                        continue;
                    var landingCoord = tcs[0];
                    int castDist = context.grid_service.GetDistanceFromUnitToCoord(
                        ctxUnitState,
                        landingCoord
                    );
                    if (effectiveRange >= 0 && castDist > effectiveRange)
                        continue;
                    int landingDist = _distance_from_anchor_to_unit(
                        context,
                        ctxUnitState,
                        landingCoord,
                        focusTarget
                    );
                    if (landingDist <= currentDist)
                    {
                        _trace_add_block_reason(actionTrace, "does_not_improve_safety");
                        continue;
                    }
                    _trace_count_increment(actionTrace, "evaluation_count", 1);
                    var command = _build_typed_ground_skill_command(
                        context,
                        sid,
                        cv.VariantId,
                        new[] { landingCoord }
                    );
                    BattlePreview preview = _build_fast_ground_skill_preview(
                        context,
                        command,
                        new[] { landingCoord },
                        System.Array.Empty<StringName>()
                    );
                    if (preview?.allowed != true)
                    {
                        _trace_count_increment(actionTrace, "preview_reject_count", 1);
                        continue;
                    }
                    var scoreInput = _build_typed_skill_score_input(
                        context,
                        skillDefinition,
                        command,
                        preview,
                        cv.EffectDefinitions,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            { "action_label", _format_skill_variant_label(skillDefinition, cv) },
                            { "action_base_score", action_base_score },
                            { "position_target_unit_id", focusTarget.unit_id },
                            { "position_anchor_coord", landingCoord },
                            { "position_current_distance", currentDist },
                            { "position_safe_distance", resolvedSafeDist },
                            { "desired_min_distance", resolvedSafeDist },
                            {
                                "desired_max_distance",
                                resolvedSafeDist + Mathf.Max(desired_max_distance_bonus, 0)
                            },
                            { "position_objective_kind", "distance_band_progress" },
                        }
                    );
                    if (_is_unthreatened_reposition(scoreInput, min_survival_margin_gain_to_escape))
                    {
                        _trace_add_block_reason(actionTrace, "no_survival_gain");
                        continue;
                    }
                    _trace_offer_candidate(
                        actionTrace,
                        _build_candidate_summary(
                            $"{_format_skill_variant_label(skillDefinition, cv)}_to_{landingCoord.X}_{landingCoord.Y}",
                            command,
                            scoreInput,
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "skill_id", (string)sid },
                                { "landing_distance", landingDist },
                                { "resolved_safe_distance", resolvedSafeDist },
                            }
                        )
                    );
                    if (!_is_better_skill_score_input(scoreInput, bestScoreInput))
                        continue;
                    bestScoreInput = scoreInput;
                    bestDecision = _create_scored_decision(
                        command,
                        scoreInput,
                        $"{ctxUnitState.display_name} 准备用 {skillDefinition.DisplayName} 拉开到 {landingDist} 格（评分 {_score_total(scoreInput)}）。"
                    );
                }
            }
        }
        _finalize_action_trace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            e.Add(
                $"UseGroundRepositionSkillAction {action_id} must declare at least one skill_id."
            );
        if (target_selector == "")
            e.Add($"UseGroundRepositionSkillAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(
            e,
            "UseGroundRepositionSkillAction",
            target_selector
        );
        if (minimum_safe_distance <= 0)
            e.Add(
                $"UseGroundRepositionSkillAction {action_id} minimum_safe_distance must be >= 1."
            );
        if (safe_distance_margin < 0)
            e.Add($"UseGroundRepositionSkillAction {action_id} safe_distance_margin must be >= 0.");
        if (desired_max_distance_bonus < 0)
            e.Add(
                $"UseGroundRepositionSkillAction {action_id} desired_max_distance_bonus must be >= 0."
            );
        return e;
    }

    private static bool _has_reposition_effect(IEnumerable<CombatEffectDefinition> effectDefs)
    {
        foreach (
            CombatEffectDefinition ed in effectDefs ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                ed != null
                && ed.EffectKind == BattleEffectKind.ForcedMove
                && (ed.ForcedMoveModeKind == BattleForcedMoveMode.Blink
                    || ed.ForcedMoveModeKind == BattleForcedMoveMode.Jump)
            )
                return true;
        }
        return false;
    }

}
