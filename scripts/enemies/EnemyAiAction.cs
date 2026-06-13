using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class EnemyAiAction : Resource
{
    protected const int HP_BASIS_POINTS_DENOMINATOR = 10000,
        ROLE_THREAT_MIN_EFFECTIVE_RANGE = 4,
        ROLE_THREAT_DISTANCE_WINDOW = 4,
        ROLE_THREAT_MAX_APPROACH_DISTANCE = 7,
        ROLE_THREAT_MAX_CONTACT_RANGE = 2;

    [Export]
    public StringName action_id { get; set; } = "";

    [Export]
    public StringName score_bucket_id { get; set; } = "";

    [Export]
    public StringName action_intent { get; set; } = "positioning";

    protected readonly BattleSkillResolutionRules _skill_resolution_rules =
        new BattleSkillResolutionRules();

    internal virtual BattleAiDecision Decide(BattleAiContext context) => null;

    public virtual bool UsesCandidateRequest() => false;

    internal virtual BattleAiCandidateRequest BuildCandidateRequest(BattleAiQueryService query) =>
        null;

    public virtual Godot.Collections.Array<string> ValidateSchema() =>
        _collect_base_validation_errors();

    internal Godot.Collections.Array<StringName> GetDeclaredSkillIds()
    {
        var r = new Godot.Collections.Array<StringName>();
        var s = new HashSet<StringName>();
        _append_declared_skill_id(r, s, Get("skill_id"));
        var sv = Get("skill_ids");
        if (sv.VariantType == Variant.Type.Array)
            foreach (var rsi in sv.AsGodotArray())
                _append_declared_skill_id(r, s, rsi);
        var rsv = Get("range_skill_ids");
        if (rsv.VariantType == Variant.Type.Array)
            foreach (var rsi in rsv.AsGodotArray())
                _append_declared_skill_id(r, s, rsi);
        return r;
    }

    internal Godot.Collections.Array<string> ValidateSkillReferences(
        Godot.Collections.Dictionary skillDefs
    )
    {
        var e = new Godot.Collections.Array<string>();
        foreach (var sid in GetDeclaredSkillIds())
        {
            if (sid == "")
                e.Add($"AI action {action_id} references an empty skill_id.");
            else if (!skillDefs.ContainsKey(sid))
                e.Add($"AI action {action_id} references missing skill {sid}.");
        }
        return e;
    }

    protected Godot.Collections.Array<string> _collect_base_validation_errors()
    {
        var e = new Godot.Collections.Array<string>();
        if (action_id == "")
            e.Add("AI action is missing action_id.");
        var ts = Get("target_selector");
        if (ts.VariantType == Variant.Type.String || ts.VariantType == Variant.Type.StringName)
        {
            var tsn = ProgressionDataUtils.to_string_name(ts);
            if (tsn != "" && !_is_supported_target_selector(tsn))
                e.Add($"AI action {action_id} has unsupported target_selector {tsn}.");
        }
        return e;
    }

    protected static bool _is_supported_target_selector(StringName s) =>
        EnemyAiTargetSelectorRules.IsSupportedSelector(s);

    protected void _append_enemy_focus_target_selector_errors(
        Godot.Collections.Array<string> errors,
        string actionLabel,
        StringName selector
    )
    {
        if (!EnemyAiTargetSelectorRules.IsEnemyFocusSelector(selector))
            errors.Add($"{actionLabel} {action_id} has unsupported target_selector {selector}.");
    }

    protected static void _append_declared_skill_id(
        Godot.Collections.Array<StringName> results,
        HashSet<StringName> seen,
        Variant rawSkillId
    )
    {
        if (
            rawSkillId.VariantType != Variant.Type.String
            && rawSkillId.VariantType != Variant.Type.StringName
        )
        {
            return;
        }
        var sid = ProgressionDataUtils.to_string_name(rawSkillId);
        if (!seen.Add(sid))
            return;
        results.Add(sid);
    }

    protected BattleAiDecision _create_decision(BattleCommand command, string reasonText = "") =>
        EnemyAiActionHelper.CreateDecision(action_id, score_bucket_id, command, reasonText);

    protected BattleAiDecision _create_scored_decision(
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string reasonText = ""
    ) =>
        EnemyAiActionHelper.CreateScoredDecision(
            action_id,
            score_bucket_id,
            command,
            scoreInput,
            reasonText
        );

    protected Godot.Collections.Array<StringName> _resolve_known_skill_ids(
        BattleAiContext context,
        Godot.Collections.Array<StringName> preferredSkillIds = null
    )
    {
        var r = new Godot.Collections.Array<StringName>();
        if (context?.unit_state == null)
            return r;
        var seen = new HashSet<StringName>();
        BattleUnitState us = context.unit_state;
        var srcIds =
            preferredSkillIds != null && preferredSkillIds.Count > 0
                ? preferredSkillIds
                : us.known_active_skill_ids;
        foreach (var rsi in srcIds)
        {
            var sid = new StringName(rsi.ToString());
            if (sid == "" || !seen.Add(sid))
                continue;
            if (us.known_active_skill_ids.Contains(sid))
                r.Add(sid);
        }
        return r;
    }

    protected static SkillDef _get_skill_def(BattleAiContext context, StringName skillId) =>
        context?.GetSkillDefTyped(skillId);

    protected string _get_skill_cast_block_reason(BattleAiContext context, SkillDef skillDef)
    {
        if (context?.unit_state == null || skillDef?.combat_profile == null)
            return "技能或目标无效。";
        BattleUnitState us = context.unit_state;
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(
                context.skill_catalog,
                skillDef,
                _get_skill_level(us, skillDef.skill_id)
            );
        CombatSkillResourceCosts costs = effectiveProfile.ResourceCosts;
        int cd = us.GetCooldownTyped(skillDef.skill_id);
        if (cd > 0)
            return $"{skillDef.display_name} 仍在冷却中（{cd}）。";
        var lrbr = _get_locked_combat_resource_block_reason(us, costs);
        if (lrbr.Length > 0)
            return lrbr;
        if (us.current_ap < costs.ApCost)
            return "AP不足，无法施放该技能。";
        if (us.current_mp < costs.MpCost)
            return "法力不足，无法施放该技能。";
        if (us.current_stamina < costs.StaminaCost)
            return "体力不足，无法施放该技能。";
        if (us.current_aura < costs.AuraCost)
            return "斗气不足，无法施放该技能。";
        return "";
    }

    protected static string _get_locked_combat_resource_block_reason(
        BattleUnitState us,
        CombatSkillResourceCosts costs
    )
    {
        if (us == null)
            return "技能施放者无效。";
        if (costs.MpCost > 0 && !us.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)))
            return "法力尚未解锁，无法施放该技能。";
        if (
            costs.StaminaCost > 0
            && !us.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina))
        )
            return "体力尚未解锁，无法施放该技能。";
        if (
            costs.AuraCost > 0
            && !us.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura))
        )
            return "斗气尚未解锁，无法施放该技能。";
        return "";
    }

    protected static BattlePreview _build_fast_typed_move_preview(
        BattleAiContext context,
        Vector2I targetCoord,
        int moveCost = -1
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleGridService grid = context?.grid_service;
        BattleState state = context?.state;
        if (actor == null || grid == null || state == null || targetCoord == new Vector2I(-1, -1))
        {
            return preview;
        }
        if (!grid.CanPlaceUnit(state, actor, targetCoord))
        {
            return preview;
        }
        preview.allowed = true;
        preview.resolved_anchor_coord = targetCoord;
        preview.move_cost =
            moveCost >= 0
                ? moveCost
                : Math.Max(context.move_cost_callback?.Invoke(actor, targetCoord) ?? 0, 0);
        foreach (Vector2I coord in grid.GetUnitTargetCoords(actor, targetCoord))
        {
            preview.AddTargetCoord(coord);
        }
        return preview;
    }

    protected static int _resolve_current_move_budget(BattleUnitState unitState)
    {
        if (unitState == null || unitState.current_move_points <= 0)
        {
            return 0;
        }
        return _is_normal_movement_locked(unitState)
            && !unitState.can_use_locked_move_points_this_turn
            ? 0
            : Mathf.Max(unitState.current_move_points, 0);
    }

    protected static bool _is_normal_movement_locked(BattleUnitState unitState)
    {
        return unitState != null
            && (unitState.has_taken_action_this_turn || unitState.has_moved_this_turn);
    }

    protected static bool _is_unit_movement_blocked(
        BattleAiContext context,
        BattleUnitState unitState
    )
    {
        if (unitState == null)
        {
            return true;
        }
        return context?.ai_query_service?.IsUnitMovementBlocked(unitState.unit_id) == true;
    }

    protected BattlePreview _build_fast_unit_skill_preview(
        BattleAiContext context,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleCommand command,
        BattleUnitState targetUnit = null
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleState state = context?.state;
        if (
            actor == null
            || state == null
            || skillDef?.combat_profile == null
            || command == null
        )
        {
            return preview;
        }

        CombatSkillDef combatProfile = skillDef.combat_profile as CombatSkillDef;
        if (combatProfile == null)
        {
            return preview;
        }
        if (combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain)
        {
            return _build_fast_random_chain_skill_preview(context, skillDef, castVariant, command);
        }

        var targetIds = new Godot.Collections.Array<StringName>();
        AddUniqueTargetId(targetIds, targetUnit?.unit_id ?? "");
        AddUniqueTargetId(targetIds, command.target_unit_id);
        foreach (StringName id in command.TargetUnitIdsTyped)
        {
            AddUniqueTargetId(targetIds, id);
        }
        if (targetIds.Count == 0)
        {
            return preview;
        }

        bool isMultiTarget =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit;
        if (!isMultiTarget && targetIds.Count != 1)
        {
            return preview;
        }

        foreach (StringName targetId in targetIds)
        {
            BattleUnitState candidate = state.TryGetUnitTyped(targetId, out BattleUnitState found)
                ? found
                : null;
            if (
                candidate == null
                || !candidate.is_alive
                || !_matches_target_filter(context, candidate, combatProfile.target_team_filter)
                || !_is_fast_unit_skill_target_in_range(context, actor, candidate, skillDef)
            )
            {
                return preview;
            }
            preview.AddTargetUnitId(candidate.unit_id);
            candidate.RefreshFootprint();
            foreach (Vector2I coord in candidate.occupied_coords)
            {
                if (!preview.ContainsTargetCoord(coord))
                {
                    preview.AddTargetCoord(coord);
                }
            }
        }

        preview.allowed = preview.TargetUnitIdsTyped.Count > 0;
        preview.resolved_anchor_coord =
            preview.TargetCoordsTyped.Count > 0
                ? preview.TargetCoordsTyped[0]
                : new Vector2I(-1, -1);
        return preview;
    }

    protected BattlePreview _build_fast_random_chain_skill_preview(
        BattleAiContext context,
        SkillDef skillDef,
        CombatCastVariantDef _castVariant,
        BattleCommand _command
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleState state = context?.state;
        if (actor == null || state == null || skillDef?.combat_profile == null)
        {
            return preview;
        }
        CombatSkillDef combatProfile = skillDef.combat_profile as CombatSkillDef;
        if (combatProfile == null)
        {
            return preview;
        }
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || candidate == actor
                || !candidate.is_alive
                || !_matches_target_filter(context, candidate, combatProfile.target_team_filter)
                || !_is_fast_unit_skill_target_in_range(context, actor, candidate, skillDef)
            )
            {
                continue;
            }
            preview.AddRandomChainCandidateUnitId(candidate.unit_id);
        }
        preview.allowed = preview.RandomChainCandidateUnitIdsTyped.Count > 0;
        return preview;
    }

    protected static BattlePreview _build_fast_ground_skill_preview(
        BattleAiContext context,
        BattleCommand command,
        IEnumerable<Vector2I> previewCoords,
        IEnumerable<StringName> targetUnitIds
    )
    {
        var preview = new BattlePreview();
        if (context?.unit_state == null || command == null)
        {
            return preview;
        }
        var seenCoords = new HashSet<Vector2I>();
        foreach (Vector2I coord in previewCoords ?? System.Array.Empty<Vector2I>())
        {
            if (seenCoords.Add(coord))
            {
                preview.AddTargetCoord(coord);
            }
        }
        var seenUnitIds = new HashSet<StringName>();
        foreach (StringName unitId in targetUnitIds ?? System.Array.Empty<StringName>())
        {
            if (unitId != "" && seenUnitIds.Add(unitId))
            {
                preview.AddTargetUnitId(unitId);
            }
        }
        preview.resolved_anchor_coord = command.target_coord;
        preview.allowed = preview.TargetCoordsTyped.Count > 0 || preview.TargetUnitIdsTyped.Count > 0;
        return preview;
    }

    private static void AddUniqueTargetId(
        Godot.Collections.Array<StringName> targetIds,
        StringName unitId
    )
    {
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        if (targetIds == null || normalized == "" || targetIds.Contains(normalized))
        {
            return;
        }
        targetIds.Add(normalized);
    }

    private static bool _is_fast_unit_skill_target_in_range(
        BattleAiContext context,
        BattleUnitState actor,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (context?.grid_service == null || actor == null || targetUnit == null || skillDef == null)
        {
            return false;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
            actor,
            skillDef,
            context.skill_catalog
        );
        return context.grid_service.GetDistanceBetweenUnits(actor, targetUnit) <= effectiveRange;
    }

    protected static int _score_total(BattleAiScoreInput scoreInput) => scoreInput?.total_score ?? 0;

    protected static int _score_target_count(BattleAiScoreInput scoreInput) =>
        scoreInput?.target_count ?? 0;

    protected static int _score_position_objective(BattleAiScoreInput scoreInput) =>
        scoreInput?.position_objective_score ?? 0;

    protected static int _score_resource_cost(BattleAiScoreInput scoreInput) =>
        scoreInput?.resource_cost_score ?? 0;

    protected static int _score_distance_to_primary_coord(BattleAiScoreInput scoreInput) =>
        scoreInput?.distance_to_primary_coord ?? -1;

    protected BattleAiScoreInput _build_typed_skill_score_input(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDef> effectDefs = null,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (context == null)
            return null;
        var scoreMetadata = _clone_metadata_typed(metadata);
        scoreMetadata["score_bucket_id"] = score_bucket_id;
        scoreMetadata["action_kind"] = _read_metadata_string_name_typed(
            scoreMetadata,
            "action_kind",
            new StringName("skill")
        );
        scoreMetadata["action_label"] = _read_metadata_string_typed(
            scoreMetadata,
            "action_label",
            skillDef != null ? skillDef.display_name : action_id.ToString()
        );
        scoreMetadata = _merge_runtime_action_trace_metadata_typed(context, scoreMetadata);
        scoreMetadata["score_bucket_id"] = _read_metadata_string_name_typed(
            scoreMetadata,
            "score_bucket_id",
            score_bucket_id
        );
        return context.BuildSkillScoreInputTyped(
            skillDef,
            command,
            preview,
            effectDefs,
            scoreMetadata
        );
    }

    protected static bool _is_better_skill_score_input(
        BattleAiScoreInput candidate,
        BattleAiScoreInput best
    )
    {
        var c = candidate;
        var b = best;
        if (c == null)
            return false;
        if (b == null)
            return true;
        if (c.estimated_friendly_lethal_target_count != b.estimated_friendly_lethal_target_count)
            return c.estimated_friendly_lethal_target_count
                < b.estimated_friendly_lethal_target_count;
        if (c.estimated_friendly_fire_target_count != b.estimated_friendly_fire_target_count)
            return c.estimated_friendly_fire_target_count < b.estimated_friendly_fire_target_count;
        if (c.friendly_fire_penalty_score != b.friendly_fire_penalty_score)
            return c.friendly_fire_penalty_score < b.friendly_fire_penalty_score;
        int src = _compare_post_action_survival_risk(c, b);
        if (src != 0)
            return src > 0;
        if (c.estimated_lethal_threat_target_count != b.estimated_lethal_threat_target_count)
            return c.estimated_lethal_threat_target_count > b.estimated_lethal_threat_target_count;
        if (c.estimated_lethal_target_count != b.estimated_lethal_target_count)
            return c.estimated_lethal_target_count > b.estimated_lethal_target_count;
        bool ci = _is_emergency_survival_score_input(c),
            bi = _is_emergency_survival_score_input(b);
        if (ci != bi)
            return ci;
        if (c.estimated_lethal_target_count > 0 && b.estimated_lethal_target_count > 0)
        {
            if (c.total_score != b.total_score)
                return c.total_score > b.total_score;
            if (c.hit_payoff_score != b.hit_payoff_score)
                return c.hit_payoff_score > b.hit_payoff_score;
            if (c.effective_target_count != b.effective_target_count)
                return c.effective_target_count > b.effective_target_count;
            int lnr = _compare_nonfatal_post_action_survival_risk(c, b);
            if (lnr != 0)
                return lnr > 0;
            if (c.resource_cost_score != b.resource_cost_score)
                return c.resource_cost_score < b.resource_cost_score;
        }
        if (c.score_bucket_priority != b.score_bucket_priority)
            return c.score_bucket_priority > b.score_bucket_priority;
        if (c.total_score != b.total_score)
            return c.total_score > b.total_score;
        if (c.hit_payoff_score != b.hit_payoff_score)
            return c.hit_payoff_score > b.hit_payoff_score;
        if (c.effective_target_count != b.effective_target_count)
            return c.effective_target_count > b.effective_target_count;
        if (c.target_count != b.target_count)
            return c.target_count > b.target_count;
        int nr = _compare_nonfatal_post_action_survival_risk(c, b);
        if (nr != 0)
            return nr > 0;
        if (c.position_objective_score != b.position_objective_score)
            return c.position_objective_score > b.position_objective_score;
        return c.resource_cost_score < b.resource_cost_score;
    }

    private static bool _is_emergency_survival_score_input(BattleAiScoreInput si)
    {
        if (si == null)
            return false;
        if (si.score_bucket_id != "archer_survival")
            return false;
        if (si.has_post_action_threat_projection)
        {
            if (si.pre_action_is_lethal_survival_risk && !si.post_action_is_lethal_survival_risk)
                return true;
            if (
                si.pre_action_threat_expected_damage
                    > si.post_action_remaining_threat_expected_damage
                && si.post_action_survival_margin >= 0
            )
                return true;
        }
        if (si.target_count > 0 || si.effective_target_count > 0)
            return false;
        if (si.enemy_target_count > 0 || si.ally_target_count > 0)
            return false;
        if (si.estimated_damage != 0 || si.estimated_control_count != 0)
            return false;
        if (si.position_current_distance >= 0 && si.position_safe_distance > 0)
        {
            int currentGap = si.position_safe_distance - si.position_current_distance;
            if (currentGap < 2)
                return false;
            if (si.distance_to_primary_coord >= 0)
                return si.distance_to_primary_coord >= si.position_safe_distance;
            return si.position_objective_score > 0;
        }
        return si.position_objective_score > 0;
    }

    private static int _compare_post_action_survival_risk(
        BattleAiScoreInput c,
        BattleAiScoreInput b
    )
    {
        if (c == null || b == null)
            return 0;
        if (!c.has_post_action_threat_projection || !b.has_post_action_threat_projection)
            return 0;
        if (c.post_action_is_lethal_survival_risk != b.post_action_is_lethal_survival_risk)
            return c.post_action_is_lethal_survival_risk ? -1 : 1;
        return 0;
    }

    private static int _compare_nonfatal_post_action_survival_risk(
        BattleAiScoreInput c,
        BattleAiScoreInput b
    )
    {
        if (c == null || b == null)
            return 0;
        if (!c.has_post_action_threat_projection || !b.has_post_action_threat_projection)
            return 0;
        if (c.post_action_is_lethal_survival_risk || b.post_action_is_lethal_survival_risk)
            return 0;
        bool candidateThreatFree = c.post_action_remaining_threat_count <= 0;
        bool bestThreatFree = b.post_action_remaining_threat_count <= 0;
        if (candidateThreatFree != bestThreatFree)
            return candidateThreatFree ? 1 : -1;
        if (
            c.post_action_remaining_threat_expected_damage
            != b.post_action_remaining_threat_expected_damage
        )
            return
                c.post_action_remaining_threat_expected_damage
                < b.post_action_remaining_threat_expected_damage
                ? 1
                : -1;
        if (c.post_action_remaining_threat_count != b.post_action_remaining_threat_count)
            return c.post_action_remaining_threat_count < b.post_action_remaining_threat_count
                ? 1
                : -1;
        if (c.post_action_survival_margin != b.post_action_survival_margin)
            return c.post_action_survival_margin > b.post_action_survival_margin ? 1 : -1;
        return 0;
    }

    protected static BattleCommand _build_wait_command(BattleAiContext context) =>
        EnemyAiActionHelper.BuildWaitCommand(context);

    protected static BattleCommand _build_move_command(BattleAiContext context, Vector2I targetCoord) =>
        EnemyAiActionHelper.BuildMoveCommand(context, targetCoord);

    protected static BattleCommand _build_unit_skill_command(
        BattleAiContext context,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName skillVariantId = default
    ) =>
        EnemyAiActionHelper.BuildUnitSkillCommand(
            context,
            skillId,
            targetUnit,
            skillVariantId
        );

    protected static Godot.Collections.Array ToCombatEffectArray(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        var result = new Godot.Collections.Array();
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null)
                result.Add(effectDef);
        }
        return result;
    }

    protected static BattleCommand _build_typed_ground_skill_command(
        BattleAiContext context,
        StringName skillId,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    )
    {
        if (context?.unit_state == null)
            return null;
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_id = skillId,
            skill_variant_id = skillVariantId,
        };
        foreach (Vector2I coord in targetCoords ?? System.Array.Empty<Vector2I>())
        {
            command.AddTargetCoord(coord);
            if (command.target_coord == new Vector2I(-1, -1))
            {
                command.target_coord = coord;
            }
        }
        return command;
    }

    protected List<BattleUnitState> _collect_units_by_filter_typed(
        BattleAiContext context,
        StringName targetFilter
    )
    {
        var r = new List<BattleUnitState>();
        if (context?.state == null || context.unit_state == null)
            return r;
        BattleState state = context.state;
        foreach (BattleUnitState us in state.GetUnitsTyped())
        {
            if (us != null && us.is_alive && _matches_target_filter(context, us, targetFilter))
                r.Add(us);
        }
        return r;
    }

    protected static bool _matches_target_filter(
        BattleAiContext context,
        BattleUnitState us,
        StringName targetFilter
    )
    {
        if (context?.unit_state == null || us == null)
            return false;
        BattleUnitState cu = context.unit_state;
        bool madness = cu.ai_blackboard?.madness_target_any_team == true;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            cu,
            us,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: madness
            )
        );
    }

    protected BattleAiScoreInput _build_typed_action_score_input(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (context == null)
            return null;
        var scoreMetadata = _clone_metadata_typed(metadata);
        scoreMetadata["score_bucket_id"] = score_bucket_id;
        scoreMetadata = _merge_runtime_action_trace_metadata_typed(context, scoreMetadata);
        StringName resolvedScoreBucketId = _read_metadata_string_name_typed(
            scoreMetadata,
            "score_bucket_id",
            score_bucket_id
        );
        return context.BuildActionScoreInputTyped(
            actionKind,
            actionLabel,
            resolvedScoreBucketId,
            command,
            preview,
            scoreMetadata
        );
    }

    protected Godot.Collections.Array _sort_target_units(
        BattleAiContext context,
        StringName targetFilter,
        StringName selector
    )
    {
        return ToUnitArray(_sort_target_units_typed(context, targetFilter, selector));
    }

    protected List<BattleUnitState> _sort_target_units_typed(
        BattleAiContext context,
        StringName targetFilter,
        StringName selector
    )
    {
        EnemyAiTargetSelector selectorKind = EnemyAiTargetSelectorRules.ToKind(selector);
        if (!EnemyAiTargetSelectorRules.IsSupportedSelector(selector))
            return new List<BattleUnitState>();
        var ef = targetFilter;
        if (context?.unit_state != null)
        {
            BattleUnitState cu = context.unit_state;
            if (
                cu.ai_blackboard?.madness_target_any_team == true
                && selectorKind != EnemyAiTargetSelector.Self
            )
                ef = BattleTypedNames.ToStringName(BattleTargetFilter.Any);
            else if (EnemyAiTargetSelectorRules.IsEnemyFocusSelector(selectorKind))
                ef = BattleTypedNames.ToStringName(BattleTargetFilter.Enemy);
            else if (
                selectorKind
                    is EnemyAiTargetSelector.NearestAlly
                        or EnemyAiTargetSelector.LowestHpAlly
            )
                ef = BattleTypedNames.ToStringName(BattleTargetFilter.Ally);
            else if (selectorKind == EnemyAiTargetSelector.Self)
                ef = BattleTypedNames.ToStringName(BattleTargetFilter.Self);
        }
        if (context != null && context.TryGetSortedTargetUnits(ef, selector, out var cachedTargets))
        {
            return cachedTargets;
        }
        List<BattleUnitState> units = _collect_units_by_filter_typed(context, ef);
        var ft = _resolve_forced_target_unit(context, ef);
        if (ft != null)
        {
            var forced = new List<BattleUnitState> { ft };
            context?.CacheSortedTargetUnits(ef, selector, forced);
            return forced;
        }
        if (selectorKind == EnemyAiTargetSelector.Self)
        {
            context?.CacheSortedTargetUnits(ef, selector, units);
            return units;
        }
        var entries = new List<TargetSortEntry>(units.Count);
        int nearestDistance = 999999;
        foreach (BattleUnitState unit in units)
        {
            if (unit == null)
            {
                continue;
            }
            int distance = _distance_between_units(
                context,
                context.unit_state,
                unit
            );
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
            entries.Add(
                new TargetSortEntry(
                    unit,
                    _get_hp_basis_points(unit),
                    distance,
                    0
                )
            );
        }
        if (selectorKind == EnemyAiTargetSelector.NearestRoleThreatEnemy)
        {
            for (int index = 0; index < entries.Count; index += 1)
            {
                TargetSortEntry entry = entries[index];
                entries[index] = entry with
                {
                    RoleThreatScore = _get_role_threat_selector_score(
                        context,
                        entry.Unit,
                        nearestDistance,
                        entry.Distance
                    ),
                };
            }
        }
        entries.Sort(
            (l, r) =>
            {
                if (selectorKind == EnemyAiTargetSelector.NearestRoleThreatEnemy)
                {
                    if (l.RoleThreatScore != r.RoleThreatScore)
                        return r.RoleThreatScore.CompareTo(l.RoleThreatScore);
                }
                if (
                    selectorKind
                        is EnemyAiTargetSelector.LowestHpEnemy
                            or EnemyAiTargetSelector.LowestHpAlly
                )
                {
                    if (l.HpBasisPoints != r.HpBasisPoints)
                        return l.HpBasisPoints.CompareTo(r.HpBasisPoints);
                    if (l.Distance != r.Distance)
                        return l.Distance.CompareTo(r.Distance);
                    return ((string)l.Unit.unit_id).CompareTo((string)r.Unit.unit_id);
                }
                if (l.Distance == r.Distance)
                {
                    if (l.HpBasisPoints != r.HpBasisPoints)
                        return l.HpBasisPoints.CompareTo(r.HpBasisPoints);
                    return ((string)l.Unit.unit_id).CompareTo((string)r.Unit.unit_id);
                }
                return l.Distance.CompareTo(r.Distance);
            }
        );
        var sorted = new List<BattleUnitState>(entries.Count);
        foreach (TargetSortEntry entry in entries)
        {
            sorted.Add(entry.Unit);
        }
        context?.CacheSortedTargetUnits(ef, selector, sorted);
        return sorted;
    }

    private readonly record struct TargetSortEntry(
        BattleUnitState Unit,
        int HpBasisPoints,
        int Distance,
        int RoleThreatScore
    );

    protected int _resolve_nearest_distance(
        BattleAiContext context,
        IEnumerable<BattleUnitState> units
    )
    {
        int nd = 999999;
        BattleUnitState cu = context?.unit_state;
        foreach (BattleUnitState us in units ?? System.Array.Empty<BattleUnitState>())
        {
            if (us != null)
                nd = Mathf.Min(nd, _distance_between_units(context, cu, us));
        }
        return nd;
    }

    protected static Godot.Collections.Array ToUnitArray(IEnumerable<BattleUnitState> units)
    {
        var result = new Godot.Collections.Array();
        if (units == null)
            return result;
        foreach (BattleUnitState unit in units)
            if (unit != null)
                result.Add(unit);
        return result;
    }

    protected int _get_role_threat_selector_score(
        BattleAiContext context,
        BattleUnitState us,
        int nearestDist,
        int dist
    )
    {
        if (us == null)
            return 0;
        int tr = _resolve_unit_effective_threat_range(context, us);
        bool lrt =
            tr >= ROLE_THREAT_MIN_EFFECTIVE_RANGE
            && dist <= nearestDist + ROLE_THREAT_DISTANCE_WINDOW
            && dist <= ROLE_THREAT_MAX_APPROACH_DISTANCE;
        if (lrt)
            return 1000 + tr * 10;
        if (_resolve_unit_contact_threat_range(context, us) > 0)
            return 500;
        return 0;
    }

    protected int _resolve_unit_contact_threat_range(BattleAiContext context, BattleUnitState tu)
    {
        if (context == null || tu == null)
            return -1;
        int br = -1;
        foreach (var rsi in tu.known_active_skill_ids)
        {
            var sid = ProgressionDataUtils.to_string_name(rsi);
            if (sid == "")
                continue;
            SkillDef sd = _get_skill_def(context, sid);
            if (!_is_hostile_threat_skill(sd))
                continue;
            if (!_skill_has_tag(sd, "melee") && !_skill_has_tag(sd, "weapon"))
                continue;
            int er = BattleRangeService.GetEffectiveSkillRange(tu, sd, context.skill_catalog);
            if (er <= 0 && _skill_has_tag(sd, "melee"))
                er = 1;
            if (er > ROLE_THREAT_MAX_CONTACT_RANGE)
                continue;
            br = Mathf.Max(br, er);
        }
        int wr = BattleRangeService.GetWeaponAttackRange(tu);
        if (wr > 0 && wr <= ROLE_THREAT_MAX_CONTACT_RANGE)
            br = Mathf.Max(br, wr);
        return br;
    }

    protected static BattleUnitState _resolve_forced_target_unit(
        BattleAiContext context,
        StringName targetFilter
    ) =>
        context?.ResolveForcedTargetUnit(targetFilter);

    protected static int _get_hp_basis_points(BattleUnitState us)
    {
        if (us?.attribute_snapshot == null)
            return HP_BASIS_POINTS_DENOMINATOR;
        int hpm = Mathf.Max(
            us.attribute_snapshot.GetValue(new StringName("hp_max")),
            1
        );
        int chp = Mathf.Clamp(us.current_hp, 0, hpm);
        return Mathf.Clamp(chp * HP_BASIS_POINTS_DENOMINATOR / hpm, 0, HP_BASIS_POINTS_DENOMINATOR);
    }

    protected static int _distance_between_units(
        BattleAiContext context,
        BattleUnitState a,
        BattleUnitState b
    ) =>
        context?.grid_service != null
            ? context.grid_service.GetDistanceBetweenUnits(a, b)
            : 999999;

    protected static int _distance_from_anchor_to_unit(
        BattleAiContext context,
        BattleUnitState us,
        Vector2I anchor,
        BattleUnitState tu
    )
    {
        if (context?.grid_service == null || us == null || tu == null)
            return 999999;
        BattleGridService gs = context.grid_service;
        us.RefreshFootprint();
        tu.RefreshFootprint();
        int bd = 999999;
        foreach (Vector2I sc in gs.GetFootprintCoords(anchor, us.footprint_size))
        foreach (var tc in tu.occupied_coords)
            bd = Mathf.Min(bd, gs.GetDistance(sc, tc));
        return bd;
    }

    protected static int _get_skill_level(BattleUnitState us, StringName sid)
    {
        if (us == null || sid == "")
            return 0;
        int knownSkillLevel = us.GetKnownSkillLevelTyped(sid);
        return knownSkillLevel > 0 ? knownSkillLevel : us.known_active_skill_ids.Contains(sid) ? 1 : 0;
    }

    protected int _resolve_effective_attack_range(
        BattleAiContext context,
        SkillDef skillDef = null,
        Godot.Collections.Array<StringName> rangeSkillIds = null
    )
    {
        rangeSkillIds ??= new Godot.Collections.Array<StringName>();
        if (context?.unit_state == null)
            return -1;
        BattleUnitState us = context.unit_state;
        if (skillDef != null)
            return BattleRangeService.GetEffectiveSkillDistanceContractRange(
                us,
                skillDef,
                context.skill_catalog
            );
        int br = -1;
        foreach (var sid in _resolve_known_skill_ids(context, rangeSkillIds))
        {
            var csd = _get_skill_def(context, sid);
            if (csd?.combat_profile == null)
                continue;
            if (_get_skill_cast_block_reason(context, csd).Length > 0)
                continue;
            br = Mathf.Max(
                br,
                BattleRangeService.GetEffectiveSkillDistanceContractRange(
                    us,
                    csd,
                    context.skill_catalog
                )
            );
        }
        return br;
    }

    protected Dictionary<string, object> _resolve_desired_distance_contract_typed(
        BattleAiContext context,
        SkillDef skillDef = null,
        Godot.Collections.Array<StringName> rangeSkillIds = null
    )
    {
        rangeSkillIds ??= new Godot.Collections.Array<StringName>();
        int configuredMinDistance = Get("desired_min_distance").AsInt32();
        int configuredMaxDistance = Get("desired_max_distance").AsInt32();
        int effectiveAttackRange = _resolve_effective_attack_range(context, skillDef, rangeSkillIds);
        int resolvedMaxDistance = configuredMaxDistance;
        if (effectiveAttackRange >= 0)
            resolvedMaxDistance = effectiveAttackRange;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMinDistance,
            ["desired_max_distance"] = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ["configured_desired_min_distance"] = configuredMinDistance,
            ["configured_desired_max_distance"] = configuredMaxDistance,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    protected int _resolve_target_safe_distance(
        BattleAiContext context,
        BattleUnitState tu,
        int cmsd,
        int sdm = 1
    )
    {
        int rm = Mathf.Max(cmsd, 0);
        int tr = _resolve_unit_effective_threat_range(context, tu);
        return tr <= 0 ? rm : Mathf.Max(rm, tr + Mathf.Max(sdm, 0));
    }

    protected int _resolve_unit_effective_threat_range(BattleAiContext context, BattleUnitState tu)
    {
        if (context == null || tu == null)
            return -1;
        int br = -1;
        foreach (var rsi in tu.known_active_skill_ids)
        {
            var sid = ProgressionDataUtils.to_string_name(rsi);
            if (sid == "")
                continue;
            SkillDef sd = _get_skill_def(context, sid);
            if (!_is_hostile_threat_skill(sd))
                continue;
            br = Mathf.Max(
                br,
                BattleRangeService.GetEffectiveSkillThreatRange(tu, sd, context.skill_catalog)
            );
        }
        if (br < 0)
            br = BattleRangeService.GetWeaponAttackRange(tu);
        return br;
    }

    protected BattleUnitState _select_most_unsafe_target_typed(
        BattleAiContext context,
        IEnumerable<BattleUnitState> targets,
        Vector2I anchorCoord,
        int cmsd,
        int sdm = 1
    )
    {
        BattleUnitState bt = null;
        int bg = -1,
            bd = 999999;
        BattleUnitState cu = context?.unit_state;
        foreach (BattleUnitState tu in targets ?? System.Array.Empty<BattleUnitState>())
        {
            if (tu == null)
                continue;
            int dist = _distance_from_anchor_to_unit(context, cu, anchorCoord, tu);
            int sd = _resolve_target_safe_distance(context, tu, cmsd, sdm);
            int ug = Mathf.Max(sd - dist, 0);
            if (bt == null || ug > bg || (ug == bg && dist < bd))
            {
                bt = tu;
                bg = ug;
                bd = dist;
            }
        }
        return bt;
    }

    protected static bool _is_hostile_threat_skill(SkillDef sd)
    {
        if (sd?.combat_profile == null)
            return false;
        var cp = sd.combat_profile as CombatSkillDef;
        BattleTargetFilter targetFilter = cp.TargetFilterKind;
        if (
            targetFilter == BattleTargetFilter.Ally
            || targetFilter == BattleTargetFilter.Self
        )
            return false;
        if (
            _skill_has_tag(sd, "output")
            || _skill_has_tag(sd, "melee")
            || _skill_has_tag(sd, "bow")
            || _skill_has_tag(sd, "weapon")
        )
            return true;
        if (_effect_list_has_hostile_threat(cp.effect_defs))
            return true;
        foreach (var cv in cp.cast_variants)
        {
            if (cv != null && _effect_list_has_hostile_threat(cv.effect_defs))
                return true;
        }
        return false;
    }

    protected static bool _skill_has_tag(SkillDef sd, StringName et)
    {
        if (sd == null || et == "")
            return false;
        foreach (var t in sd.tags)
            if (ProgressionDataUtils.to_string_name(t) == et)
                return true;
        return false;
    }

    protected static bool _effect_list_has_hostile_threat(
        Godot.Collections.Array<CombatEffectDef> eds
    )
    {
        foreach (var ed in eds)
        {
            if (ed == null)
                continue;
            if (
                ed.EffectKind
                is BattleEffectKind.Damage
                    or BattleEffectKind.ChainDamage
                    or BattleEffectKind.Charge
                    or BattleEffectKind.ForcedMove
                    or BattleEffectKind.PathStepAoe
                    or BattleEffectKind.Status
            )
                return true;
        }
        return false;
    }

    protected Godot.Collections.Array _get_ground_options(BattleAiContext context, SkillDef sd)
    {
        return ToCastVariantArray(_get_ground_options_typed(context, sd));
    }

    protected List<CombatCastVariantDef> _get_ground_options_typed(
        BattleAiContext context,
        SkillDef sd
    )
    {
        var v = new List<CombatCastVariantDef>();
        if (
            sd?.combat_profile == null
            || (sd.combat_profile as CombatSkillDef).TargetModeKind != BattleTargetMode.Ground
        )
            return v;
        var cp = sd.combat_profile as CombatSkillDef;
        if (cp.cast_variants.Count == 0)
        {
            v.Add(_build_implicit_ground_option(sd));
            return v;
        }
        int sl = _get_skill_level(
            context?.unit_state,
            sd.skill_id
        );
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(context?.skill_catalog, sd, sl);
        foreach (var cv in effectiveProfile.UnlockedCastVariants)
        {
            if (cv != null)
                v.Add(cv);
        }
        return v;
    }

    protected static Godot.Collections.Array ToCastVariantArray(
        IEnumerable<CombatCastVariantDef> castVariants
    )
    {
        var result = new Godot.Collections.Array();
        if (castVariants == null)
            return result;
        foreach (CombatCastVariantDef castVariant in castVariants)
            if (castVariant != null)
                result.Add(castVariant);
        return result;
    }

    protected CombatCastVariantDef _build_implicit_ground_option(SkillDef sd)
    {
        var effects = new Godot.Collections.Array<CombatEffectDef>();
        if (sd?.combat_profile is CombatSkillDef profile)
            foreach (var effect in profile.effect_defs)
                if (effect != null)
                    effects.Add(effect);
        var cv = new CombatCastVariantDef
        {
            variant_id = "",
            display_name = "",
            TargetModeKind = BattleTargetMode.Ground,
            FootprintPatternKind = CombatCastFootprintPattern.Single,
            required_coord_count = 1,
            effect_defs = effects,
        };
        return cv;
    }

    protected static bool _is_charge_option(CombatCastVariantDef cv)
    {
        if (cv == null)
            return false;
        foreach (var ed in cv.effect_defs)
        {
            if (ed != null && ed.EffectKind == BattleEffectKind.Charge)
                return true;
        }
        return false;
    }

    protected List<List<Vector2I>> _enumerate_ground_target_coord_sets_typed(
        BattleAiContext context,
        CombatCastVariantDef cv
    )
    {
        var r = new List<List<Vector2I>>();
        if (
            context?.state == null
            || context?.grid_service == null
            || cv == null
        )
            return r;
        BattleState state = context.state;
        BattleGridService gs = context.grid_service;
        var seen = new HashSet<string>();
        if (cv.FootprintPatternKind == CombatCastFootprintPattern.Line2)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            for (int x = 0; x < state.map_size.X; x++)
            {
                var f = new Vector2I(x, y);
                foreach (var d in new[] { Vector2I.Right, Vector2I.Down })
                {
                    var s = f + d;
                    if (!gs.IsInside(state, s))
                        continue;
                    var pair = _sort_coords(new[] { f, s });
                    var k = _coord_set_key(pair);
                    if (!seen.Add(k))
                        continue;
                    r.Add(pair);
                }
            }
        }
        else if (cv.FootprintPatternKind == CombatCastFootprintPattern.Square2)
        {
            for (int y = 0; y < Mathf.Max(state.map_size.Y - 1, 0); y++)
            for (int x = 0; x < Mathf.Max(state.map_size.X - 1, 0); x++)
            {
                var coords = _sort_coords(
                    new Vector2I[]
                    {
                        new(x, y),
                        new(x + 1, y),
                        new(x, y + 1),
                        new(x + 1, y + 1),
                    }
                );
                var k = _coord_set_key(coords);
                if (!seen.Add(k))
                    continue;
                r.Add(coords);
            }
        }
        else
        {
            for (int y = 0; y < state.map_size.Y; y++)
            for (int x = 0; x < state.map_size.X; x++)
                r.Add(new List<Vector2I> { new(x, y) });
        }
        return r;
    }

    protected static List<Vector2I> _sort_coords(IEnumerable<Vector2I> coords)
    {
        var sorted = new List<Vector2I>();
        foreach (Vector2I coord in coords ?? System.Array.Empty<Vector2I>())
        {
            sorted.Add(coord);
        }
        sorted.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
        return sorted;
    }

    protected static string _coord_set_key(IEnumerable<Vector2I> coords)
    {
        var parts = new List<string>();
        foreach (Vector2I coord in coords ?? System.Array.Empty<Vector2I>())
        {
            parts.Add($"{coord.X},{coord.Y}");
        }
        return string.Join("|", parts);
    }

    protected AiActionTrace _begin_action_trace(
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        var tm = _merge_runtime_action_trace_metadata_typed(context, metadata);
        var rsb = ProgressionDataUtils.to_string_name(
            tm.ContainsKey("score_bucket_id") ? tm["score_bucket_id"] : score_bucket_id
        );
        return EnemyAiActionHelper.BeginActionTrace(action_id, rsb, context, tm);
    }

    protected Dictionary<string, object> _merge_runtime_action_trace_metadata_typed(
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (context != null)
            return context.MergeCurrentActionMetadataTyped(metadata);

        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (metadata == null)
            return result;

        foreach (KeyValuePair<string, object> entry in metadata)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    protected static Dictionary<string, object> _clone_metadata_typed(
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (metadata == null)
            return result;

        foreach (KeyValuePair<string, object> entry in metadata)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    protected static StringName _read_metadata_string_name_typed(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        StringName fallback = default
    )
    {
        if (
            metadata == null
            || string.IsNullOrEmpty(key)
            || !metadata.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }

        return value switch
        {
            StringName stringName => stringName,
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String =>
                new StringName(variant.AsString()),
            _ => fallback,
        };
    }

    protected static string _read_metadata_string_typed(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        string fallback = ""
    )
    {
        if (
            metadata == null
            || string.IsNullOrEmpty(key)
            || !metadata.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }

        return value switch
        {
            string text => text,
            StringName stringName => stringName.ToString(),
            Variant variant when variant.VariantType == Variant.Type.String => variant.AsString(),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName().ToString(),
            _ => value.ToString() ?? fallback,
        };
    }

    protected static void _trace_count_increment(
        AiActionTrace actionTrace,
        string key,
        int amount = 1
    ) => EnemyAiActionHelper.TraceCountIncrement(actionTrace, key, amount);

    protected static void _trace_add_block_reason(
        AiActionTrace actionTrace,
        string reasonKey
    ) => EnemyAiActionHelper.TraceAddBlockReason(actionTrace, reasonKey);

    protected static void _trace_offer_candidate(
        AiActionTrace actionTrace,
        AiCandidateSummary candidateSummary,
        int keepCount = 5
    ) => EnemyAiActionHelper.TraceOfferCandidate(actionTrace, candidateSummary, keepCount);

    protected static StringName _finalize_action_trace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision = null
    ) => EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, bestDecision);

    protected static AiCandidateSummary _build_candidate_summary(
        string label,
        BattleCommand command,
        BattleAiScoreInput scoreInput = null,
        IReadOnlyDictionary<string, object> extra = null
    ) =>
        EnemyAiActionHelper.BuildCandidateSummary(
            label,
            command,
            scoreInput,
            extra
        );

    protected static string _format_skill_variant_label(SkillDef sd, CombatCastVariantDef cv) =>
        EnemyAiActionHelper.FormatSkillVariantLabel(sd, cv);

    protected static AiCommandSummary _build_command_summary(BattleCommand command) =>
        EnemyAiActionHelper.BuildCommandSummary(command);

}
