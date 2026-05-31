using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class EnemyAiAction : Resource
{
    public static readonly StringName TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY =
        "nearest_role_threat_enemy";
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

    public virtual BattleAiDecision decide(BattleAiContext context) => null;

    public virtual bool uses_candidate_request() => false;

    public virtual BattleAiCandidateRequest build_candidate_request(BattleAiQueryService query) => null;

    public virtual Godot.Collections.Array<string> validate_schema() =>
        _collect_base_validation_errors();

    public Godot.Collections.Array<StringName> get_declared_skill_ids()
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

    public Godot.Collections.Array<string> validate_skill_references(
        Godot.Collections.Dictionary skillDefs
    )
    {
        var e = new Godot.Collections.Array<string>();
        foreach (var sid in get_declared_skill_ids())
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
        s == "nearest_enemy"
        || s == "lowest_hp_enemy"
        || s == TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY
        || s == "nearest_ally"
        || s == "lowest_hp_ally"
        || s == "self";

    protected void _append_enemy_focus_target_selector_errors(
        Godot.Collections.Array<string> errors,
        string actionLabel,
        StringName selector
    )
    {
        if (selector == "nearest_ally" || selector == "lowest_hp_ally" || selector == "self")
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
        EnemyAiActionHelper.create_decision(action_id, score_bucket_id, command, reasonText);

    protected BattleAiDecision _create_scored_decision(
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string reasonText = ""
    ) =>
        EnemyAiActionHelper.create_scored_decision(
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
        context != null
        && skillId != ""
        && context.skill_defs.ContainsKey(skillId)
            ? context.skill_defs[skillId].AsGodotObject() as SkillDef
            : null;

    protected string _get_skill_cast_block_reason(BattleAiContext context, SkillDef skillDef)
    {
        if (context?.unit_state == null || skillDef?.combat_profile == null)
            return "技能或目标无效。";
        BattleUnitState us = context.unit_state;
        var cp = skillDef.combat_profile as CombatSkillDef;
        var costs = cp.get_effective_resource_costs(_get_skill_level(us, skillDef.skill_id));
        int cd = us.cooldowns.ContainsKey(skillDef.skill_id)
            ? us.cooldowns[skillDef.skill_id].AsInt32()
            : 0;
        if (cd > 0)
            return $"{skillDef.display_name} 仍在冷却中（{cd}）。";
        var lrbr = _get_locked_combat_resource_block_reason(us, costs);
        if (lrbr.Length > 0)
            return lrbr;
        if (
            us.current_ap < (costs.ContainsKey("ap_cost") ? costs["ap_cost"].AsInt32() : cp.ap_cost)
        )
            return "AP不足，无法施放该技能。";
        if (
            us.current_mp < (costs.ContainsKey("mp_cost") ? costs["mp_cost"].AsInt32() : cp.mp_cost)
        )
            return "法力不足，无法施放该技能。";
        if (
            us.current_stamina
            < (
                costs.ContainsKey("stamina_cost")
                    ? costs["stamina_cost"].AsInt32()
                    : cp.stamina_cost
            )
        )
            return "体力不足，无法施放该技能。";
        if (
            us.current_aura
            < (costs.ContainsKey("aura_cost") ? costs["aura_cost"].AsInt32() : cp.aura_cost)
        )
            return "斗气不足，无法施放该技能。";
        return "";
    }

    protected static string _get_locked_combat_resource_block_reason(
        BattleUnitState us,
        Godot.Collections.Dictionary costs
    )
    {
        if (us == null)
            return "技能施放者无效。";
        if (
            (costs.ContainsKey("mp_cost") ? costs["mp_cost"].AsInt32() : 0) > 0
            && !us.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP())
        )
            return "法力尚未解锁，无法施放该技能。";
        if (
            (costs.ContainsKey("stamina_cost") ? costs["stamina_cost"].AsInt32() : 0) > 0
            && !us.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA())
        )
            return "体力尚未解锁，无法施放该技能。";
        if (
            (costs.ContainsKey("aura_cost") ? costs["aura_cost"].AsInt32() : 0) > 0
            && !us.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA())
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
        if (!grid.can_place_unit(state, actor, targetCoord))
        {
            return preview;
        }
        preview.allowed = true;
        preview.resolved_anchor_coord = targetCoord;
        preview.move_cost =
            moveCost >= 0
                ? moveCost
                : Math.Max(context.move_cost_callback?.Invoke(actor, targetCoord) ?? 0, 0);
        foreach (Vector2I coord in grid.get_unit_target_coords(actor, targetCoord))
        {
            preview.target_coords.Add(coord);
        }
        return preview;
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
        if (combatProfile.target_selection_mode == "random_chain")
        {
            return _build_fast_random_chain_skill_preview(context, skillDef, castVariant, command);
        }

        var targetIds = new Godot.Collections.Array<StringName>();
        AddUniqueTargetId(targetIds, targetUnit?.unit_id ?? "");
        AddUniqueTargetId(targetIds, command.target_unit_id);
        foreach (StringName id in command.target_unit_ids)
        {
            AddUniqueTargetId(targetIds, id);
        }
        if (targetIds.Count == 0)
        {
            return preview;
        }

        bool isMultiTarget = combatProfile.target_selection_mode == "multi_unit";
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
            preview.target_unit_ids.Add(candidate.unit_id);
            candidate.refresh_footprint();
            foreach (Vector2I coord in candidate.occupied_coords)
            {
                if (!preview.target_coords.Contains(coord))
                {
                    preview.target_coords.Add(coord);
                }
            }
        }

        preview.allowed = preview.target_unit_ids.Count > 0;
        preview.resolved_anchor_coord =
            preview.target_coords.Count > 0 ? preview.target_coords[0] : new Vector2I(-1, -1);
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
            preview.random_chain_candidate_unit_ids.Add(candidate.unit_id);
        }
        preview.allowed = preview.random_chain_candidate_unit_ids.Count > 0;
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
                preview.target_coords.Add(coord);
            }
        }
        var seenUnitIds = new HashSet<StringName>();
        foreach (StringName unitId in targetUnitIds ?? System.Array.Empty<StringName>())
        {
            if (unitId != "" && seenUnitIds.Add(unitId))
            {
                preview.target_unit_ids.Add(unitId);
            }
        }
        preview.resolved_anchor_coord = command.target_coord;
        preview.allowed = preview.target_coords.Count > 0 || preview.target_unit_ids.Count > 0;
        return preview;
    }

    private static void AddUniqueTargetId(
        Godot.Collections.Array<StringName> targetIds,
        StringName unitId
    )
    {
        if (targetIds == null || unitId == "" || targetIds.Contains(unitId))
        {
            return;
        }
        targetIds.Add(unitId);
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
        int effectiveRange = BattleRangeService.get_effective_skill_range(actor, skillDef);
        return context.grid_service.get_distance_between_units(actor, targetUnit) <= effectiveRange;
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

    private BattleAiScoreInput _build_skill_score_input(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        Godot.Collections.Array effectDefs = null,
        Godot.Collections.Dictionary metadata = null
    )
    {
        if (context == null)
            return null;
        var sm = metadata?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        sm["score_bucket_id"] = score_bucket_id;
        sm["action_kind"] = ProgressionDataUtils.to_string_name(
            sm.ContainsKey("action_kind") ? sm["action_kind"] : "skill"
        );
        sm["action_label"] = sm.ContainsKey("action_label")
            ? sm["action_label"].AsString()
            : (skillDef != null ? skillDef.display_name : (string)action_id);
        sm = _merge_runtime_action_metadata(context, sm);
        sm["score_bucket_id"] = ProgressionDataUtils.to_string_name(
            sm.ContainsKey("score_bucket_id") ? sm["score_bucket_id"] : score_bucket_id
        );
        return context.build_skill_score_input(
            skillDef,
            command,
            preview,
            effectDefs ?? new Godot.Collections.Array(),
            sm
        );
    }

    protected BattleAiScoreInput _build_typed_skill_score_input(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDef> effectDefs = null,
        Godot.Collections.Dictionary metadata = null
    )
    {
        return _build_skill_score_input(
            context,
            skillDef,
            command,
            preview,
            ToCombatEffectArray(effectDefs),
            metadata
        );
    }

    protected BattleAiScoreInput _build_action_score_input(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        BattleCommand command,
        BattlePreview preview,
        Godot.Collections.Dictionary metadata = null
    )
    {
        if (context == null)
            return null;
        var sm = metadata?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        sm["score_bucket_id"] = score_bucket_id;
        sm = _merge_runtime_action_metadata(context, sm);
        var rsb = ProgressionDataUtils.to_string_name(
            sm.ContainsKey("score_bucket_id") ? sm["score_bucket_id"] : score_bucket_id
        );
        return context.build_action_score_input(actionKind, actionLabel, rsb, command, preview, sm);
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
        EnemyAiActionHelper.build_wait_command(context);

    protected static BattleCommand _build_move_command(BattleAiContext context, Vector2I targetCoord) =>
        EnemyAiActionHelper.build_move_command(context, targetCoord);

    protected static BattleCommand _build_unit_skill_command(
        BattleAiContext context,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName skillVariantId = default
    ) =>
        EnemyAiActionHelper.build_unit_skill_command(
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

    protected StringName _get_cast_variant_target_mode(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    ) => _skill_resolution_rules.get_cast_variant_target_mode(skillDef, castVariant);

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
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = context.unit_state.unit_id,
            skill_id = skillId,
            skill_variant_id = skillVariantId,
        };
        foreach (Vector2I coord in targetCoords ?? System.Array.Empty<Vector2I>())
        {
            command.target_coords.Add(coord);
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
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            cu,
            us,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: madness
            )
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
        if (!_is_supported_target_selector(selector))
            return new List<BattleUnitState>();
        var ef = targetFilter;
        if (context?.unit_state != null)
        {
            BattleUnitState cu = context.unit_state;
            if (
                cu.ai_blackboard?.madness_target_any_team == true
                && selector != "self"
            )
                ef = "any";
            else if (
                selector == "nearest_enemy"
                || selector == "lowest_hp_enemy"
                || selector == TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY
            )
                ef = "enemy";
            else if (selector == "nearest_ally" || selector == "lowest_hp_ally")
                ef = "ally";
            else if (selector == "self")
                ef = "self";
        }
        List<BattleUnitState> units = _collect_units_by_filter_typed(context, ef);
        var ft = _resolve_forced_target_unit(context, ef);
        if (ft != null)
            return new List<BattleUnitState> { ft };
        if (selector == "self")
            return units;
        int nd = _resolve_nearest_distance(context, units);
        var list = new List<BattleUnitState>(units);
        list.Sort(
            (l, r) =>
            {
                var lu = l;
                var ru = r;
                int lhp = _get_hp_basis_points(lu),
                    rhp = _get_hp_basis_points(ru);
                int ld = _distance_between_units(
                        context,
                        context.unit_state,
                        lu
                    ),
                    rd = _distance_between_units(
                        context,
                        context.unit_state,
                        ru
                    );
                if (selector == TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY)
                {
                    int ls = _get_role_threat_selector_score(context, lu, nd, ld),
                        rs = _get_role_threat_selector_score(context, ru, nd, rd);
                    if (ls != rs)
                        return rs.CompareTo(ls);
                }
                if (selector == "lowest_hp_enemy" || selector == "lowest_hp_ally")
                {
                    if (lhp != rhp)
                        return lhp.CompareTo(rhp);
                    if (ld != rd)
                        return ld.CompareTo(rd);
                    return ((string)lu.unit_id).CompareTo((string)ru.unit_id);
                }
                if (ld == rd)
                {
                    if (lhp != rhp)
                        return lhp.CompareTo(rhp);
                    return ((string)lu.unit_id).CompareTo((string)ru.unit_id);
                }
                return ld.CompareTo(rd);
            }
        );
        return list;
    }

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
            int er = BattleRangeService.get_effective_skill_range(tu, sd);
            if (er <= 0 && _skill_has_tag(sd, "melee"))
                er = 1;
            if (er > ROLE_THREAT_MAX_CONTACT_RANGE)
                continue;
            br = Mathf.Max(br, er);
        }
        int wr = BattleRangeService.get_weapon_attack_range(tu);
        if (wr > 0 && wr <= ROLE_THREAT_MAX_CONTACT_RANGE)
            br = Mathf.Max(br, wr);
        return br;
    }

    protected static BattleUnitState _resolve_forced_target_unit(
        BattleAiContext context,
        StringName targetFilter
    ) =>
        context?.resolve_forced_target_unit(targetFilter);

    protected static int _get_hp_basis_points(BattleUnitState us)
    {
        if (us?.attribute_snapshot == null)
            return HP_BASIS_POINTS_DENOMINATOR;
        int hpm = Mathf.Max(
            us.attribute_snapshot.get_value(new StringName("hp_max")),
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
            ? context.grid_service.get_distance_between_units(a, b)
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
        us.refresh_footprint();
        tu.refresh_footprint();
        int bd = 999999;
        foreach (Vector2I sc in gs.get_footprint_coords(anchor, us.footprint_size))
        foreach (var tc in tu.occupied_coords)
            bd = Mathf.Min(bd, gs.get_distance(sc, tc));
        return bd;
    }

    protected static int _get_skill_level(BattleUnitState us, StringName sid)
    {
        if (us == null || sid == "")
            return 0;
        if (us.known_skill_level_map.ContainsKey(sid))
            return us.known_skill_level_map[sid].AsInt32();
        return us.known_active_skill_ids.Contains(sid) ? 1 : 0;
    }

    protected Godot.Collections.Dictionary _resolve_desired_distance_contract(
        BattleAiContext context,
        SkillDef skillDef = null,
        Godot.Collections.Array<StringName> rangeSkillIds = null
    )
    {
        rangeSkillIds ??= new Godot.Collections.Array<StringName>();
        int cm = Get("desired_min_distance").AsInt32();
        int cx = Get("desired_max_distance").AsInt32();
        int ear = _resolve_effective_attack_range(context, skillDef, rangeSkillIds);
        int rx = cx;
        if (ear >= 0)
            rx = ear;
        int rm = cm;
        if (rx >= 0 && rm > rx)
            rm = rx;
        return new Godot.Collections.Dictionary
        {
            { "desired_min_distance", rm },
            { "desired_max_distance", Mathf.Max(rx, rm) },
            { "configured_desired_min_distance", cm },
            { "configured_desired_max_distance", cx },
            { "effective_attack_range", ear },
        };
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
            return BattleRangeService.get_effective_skill_distance_contract_range(us, skillDef);
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
                BattleRangeService.get_effective_skill_distance_contract_range(us, csd)
            );
        }
        return br;
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
            br = Mathf.Max(br, BattleRangeService.get_effective_skill_threat_range(tu, sd));
        }
        if (br < 0)
            br = BattleRangeService.get_weapon_attack_range(tu);
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
        var tf = ProgressionDataUtils.to_string_name(cp.target_team_filter);
        if (tf == "ally" || tf == "self")
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
            var et = ProgressionDataUtils.to_string_name(ed.effect_type);
            if (
                et == "damage"
                || et == "chain_damage"
                || et == "charge"
                || et == "forced_move"
                || et == "path_step_aoe"
                || et == "status"
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
            || (sd.combat_profile as CombatSkillDef).target_mode != "ground"
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
        foreach (var cv in cp.get_unlocked_cast_variants(sl))
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
            target_mode = "ground",
            footprint_pattern = "single",
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
            if (ed != null && ed.effect_type == "charge")
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
        if (cv.footprint_pattern == "line2")
        {
            for (int y = 0; y < state.map_size.Y; y++)
            for (int x = 0; x < state.map_size.X; x++)
            {
                var f = new Vector2I(x, y);
                foreach (var d in new[] { Vector2I.Right, Vector2I.Down })
                {
                    var s = f + d;
                    if (!gs.is_inside(state, s))
                        continue;
                    var pair = _sort_coords(new[] { f, s });
                    var k = _coord_set_key(pair);
                    if (!seen.Add(k))
                        continue;
                    r.Add(pair);
                }
            }
        }
        else if (cv.footprint_pattern == "square2")
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

    protected Godot.Collections.Dictionary _begin_action_trace(
        BattleAiContext context,
        Godot.Collections.Dictionary metadata = null
    )
    {
        var tm = _merge_runtime_action_metadata(
            context,
            metadata ?? new Godot.Collections.Dictionary()
        );
        var rsb = ProgressionDataUtils.to_string_name(
            tm.ContainsKey("score_bucket_id") ? tm["score_bucket_id"] : score_bucket_id
        );
        return EnemyAiActionHelper.begin_action_trace(action_id, rsb, context, tm);
    }

    protected Godot.Collections.Dictionary _merge_runtime_action_metadata(
        BattleAiContext context,
        Godot.Collections.Dictionary metadata
    )
    {
        return context != null
            ? context.merge_current_action_metadata(metadata)
            : metadata.Duplicate(true);
    }

    protected static void _trace_count_increment(
        Godot.Collections.Dictionary actionTrace,
        string key,
        int amount = 1
    ) => EnemyAiActionHelper.trace_count_increment(actionTrace, key, amount);

    protected static void _trace_add_block_reason(
        Godot.Collections.Dictionary actionTrace,
        string reasonKey
    ) => EnemyAiActionHelper.trace_add_block_reason(actionTrace, reasonKey);

    protected static void _trace_offer_candidate(
        Godot.Collections.Dictionary actionTrace,
        Godot.Collections.Dictionary candidateSummary,
        int keepCount = 5
    ) => EnemyAiActionHelper.trace_offer_candidate(actionTrace, candidateSummary, keepCount);

    protected static StringName _finalize_action_trace(
        BattleAiContext context,
        Godot.Collections.Dictionary actionTrace,
        BattleAiDecision bestDecision = null
    ) => EnemyAiActionHelper.finalize_action_trace(context, actionTrace, bestDecision);

    protected static Godot.Collections.Dictionary _build_candidate_summary(
        string label,
        BattleCommand command,
        BattleAiScoreInput scoreInput = null,
        Godot.Collections.Dictionary extra = null
    ) =>
        EnemyAiActionHelper.build_candidate_summary(
            label,
            command,
            scoreInput,
            extra ?? new Godot.Collections.Dictionary()
        );

    protected static string _format_skill_variant_label(SkillDef sd, CombatCastVariantDef cv) =>
        EnemyAiActionHelper.format_skill_variant_label(sd, cv);

    protected static Godot.Collections.Dictionary _build_command_summary(BattleCommand command) =>
        EnemyAiActionHelper.build_command_summary(command);

}
