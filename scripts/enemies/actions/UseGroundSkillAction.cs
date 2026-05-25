using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class UseGroundSkillAction : EnemyAiAction
{
    public StringName DISTANCE_REF_TARGET_COORD => "target_coord";
    public StringName DISTANCE_REF_ENEMY_FRONTLINE => "enemy_frontline";

    private static readonly StringName DistanceRefTargetCoord = "target_coord";
    private static readonly StringName DistanceRefEnemyFrontline = "enemy_frontline";

    [Export] public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();
    [Export] public int minimum_hit_count { get; set; } = 1;
    [Export] public bool allow_empty_ground_control { get; set; } = false;
    [Export] public bool allow_ground_control_supplement_partial_hits { get; set; } = false;
    [Export] public int minimum_ground_control_score { get; set; } = 1;
    [Export] public int minimum_ally_threat_hit_count { get; set; } = 0;
    [Export] public int maximum_friendly_fire_target_count { get; set; } = 0;
    [Export] public bool allow_friendly_lethal { get; set; } = false;
    [Export] public int threat_minimum_safe_distance { get; set; } = 0;
    [Export] public int threat_safe_distance_margin { get; set; } = 0;
    [Export] public int desired_min_distance { get; set; } = -1;
    [Export] public int desired_max_distance { get; set; } = -1;
    [Export] public StringName distance_reference { get; set; } = "";

    public override BattleAiDecision decide(GodotObject context)
    {
        AiTraceRecorder.enter("decide:ground_skill");
        try
        {
            return _decide_impl(context);
        }
        finally
        {
            AiTraceRecorder.exit("decide:ground_skill");
        }
    }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        if (!_has_explicit_distance_contract())
        {
            return null;
        }

        GDictionary actionTrace = _begin_action_trace(
            context,
            new GDictionary
            {
                ["action_kind"] = "ground_skill",
                ["minimum_hit_count"] = minimum_hit_count,
                ["allow_empty_ground_control"] = allow_empty_ground_control,
                ["allow_ground_control_supplement_partial_hits"] = allow_ground_control_supplement_partial_hits,
                ["minimum_ground_control_score"] = minimum_ground_control_score,
                ["minimum_ally_threat_hit_count"] = minimum_ally_threat_hit_count,
                ["maximum_friendly_fire_target_count"] = maximum_friendly_fire_target_count,
                ["allow_friendly_lethal"] = allow_friendly_lethal,
                ["threat_minimum_safe_distance"] = threat_minimum_safe_distance,
                ["threat_safe_distance_margin"] = threat_safe_distance_margin,
                ["distance_reference"] = distance_reference.ToString(),
                ["desired_min_distance"] = desired_min_distance,
                ["desired_max_distance"] = desired_max_distance,
            });

        BattleAiDecision bestDecision = null;
        GodotObject bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        BattleUnitState unitState = GdInterop.GetObject(context, "unit_state") as BattleUnitState;

        foreach (StringName skillId in _resolve_known_skill_ids(context, skill_ids))
        {
            _trace_count_increment(actionTrace, "skill_considered_count");
            SkillDef skillDef = _get_skill_def(context, skillId);
            CombatSkillDef combatProfile = skillDef?.combat_profile as CombatSkillDef;
            if (skillDef == null || combatProfile == null)
            {
                _trace_add_block_reason(actionTrace, "missing_skill_def");
                continue;
            }
            if (combatProfile.target_mode != "ground")
            {
                _trace_add_block_reason(actionTrace, "non_ground_skill");
                continue;
            }

            string blockReason = _get_skill_cast_block_reason(context, skillDef);
            if (!string.IsNullOrEmpty(blockReason))
            {
                _trace_add_block_reason(actionTrace, blockReason);
                continue;
            }

            foreach (Variant castVariantValue in _get_ground_variants(context, skillDef))
            {
                CombatCastVariantDef castVariant = castVariantValue.AsGodotObject() as CombatCastVariantDef;
                if (castVariant == null || _is_charge_variant(castVariant))
                {
                    continue;
                }

                int effectiveSkillRange = BattleRangeService.get_effective_skill_range(unitState, skillDef);
                bool usesRelocationDistance = BattleRangeService.is_ground_relocation_skill(skillDef);
                foreach (Variant targetCoordsValue in _enumerate_ground_target_coord_sets(context, castVariant))
                {
                    GArray targetCoords = targetCoordsValue.AsGodotArray();
                    if (!_is_ground_coord_set_within_cast_range(context, targetCoords, effectiveSkillRange, usesRelocationDistance))
                    {
                        _trace_count_increment(actionTrace, "range_prefilter_reject_count");
                        continue;
                    }

                    _trace_count_increment(actionTrace, "evaluation_count");
                    BattleCommand command = _build_ground_skill_command(context, skillId, castVariant.variant_id, targetCoords);
                    GodotObject preview = context?.Call("preview_command", command).AsGodotObject();
                    if (preview == null || !GdInterop.GetBool(preview, "allowed"))
                    {
                        _trace_count_increment(actionTrace, "preview_reject_count");
                        continue;
                    }

                    GArray previewTargetIds = GdInterop.GetArray(preview, "target_unit_ids");
                    int rawHitCount = previewTargetIds.Count;
                    int allyThreatHitCount = _count_ally_threatening_preview_targets(context, preview);
                    if (minimum_ally_threat_hit_count > 0 && allyThreatHitCount < minimum_ally_threat_hit_count)
                    {
                        _trace_add_block_reason(actionTrace, "minimum_ally_threat_hit_count");
                        continue;
                    }

                    GDictionary positionMetadata = _build_position_metadata(context, command, skillDef);
                    positionMetadata["action_label"] = _format_skill_variant_label(skillDef, castVariant);
                    GodotObject scoreInput = _build_skill_score_input(
                        context,
                        skillDef,
                        command,
                        preview,
                        _collect_ground_skill_effect_defs(skillDef, castVariant),
                        positionMetadata);

                    if (scoreInput == null)
                    {
                        if (fallbackDecision == null && rawHitCount > 0)
                        {
                            fallbackDecision = _create_decision(
                                command,
                                $"{unitState?.display_name} 准备用 {skillDef.display_name} 覆盖 {rawHitCount} 个单位。");
                        }
                        _trace_offer_candidate(
                            actionTrace,
                            _build_candidate_summary(
                                _format_skill_variant_label(skillDef, castVariant),
                                command,
                                null,
                                new GDictionary
                                {
                                    ["raw_hit_count"] = rawHitCount,
                                    ["ally_threat_hit_count"] = allyThreatHitCount,
                                    ["skill_id"] = skillId.ToString(),
                                }));
                        continue;
                    }

                    if (!_passes_minimum_effective_target_or_ground_control(scoreInput))
                    {
                        _trace_add_block_reason(actionTrace, _resolve_minimum_hit_block_reason(scoreInput));
                        continue;
                    }
                    if (!_passes_friendly_fire_limits(scoreInput))
                    {
                        _trace_add_block_reason(actionTrace, "friendly_fire_limit");
                        continue;
                    }

                    _trace_offer_candidate(
                        actionTrace,
                        _build_candidate_summary(
                            _format_skill_variant_label(skillDef, castVariant),
                            command,
                            scoreInput,
                            new GDictionary
                            {
                                ["raw_hit_count"] = rawHitCount,
                                ["effective_hit_count"] = GdInterop.GetInt(scoreInput, "effective_target_count"),
                                ["ally_threat_hit_count"] = allyThreatHitCount,
                                ["allow_empty_ground_control"] = allow_empty_ground_control,
                                ["allow_ground_control_supplement_partial_hits"] = allow_ground_control_supplement_partial_hits,
                                ["estimated_ground_control_cell_count"] = GdInterop.GetInt(scoreInput, "estimated_ground_control_cell_count"),
                                ["ground_control_score"] = GdInterop.GetInt(scoreInput, "ground_control_score"),
                                ["acceptance_reason"] = _resolve_candidate_acceptance_reason(scoreInput),
                                ["skill_id"] = skillId.ToString(),
                            }));

                    if (!_is_better_skill_score_input(scoreInput, bestScoreInput))
                    {
                        continue;
                    }
                    bestScoreInput = scoreInput;
                    bestDecision = _create_scored_decision(command, scoreInput, _build_decision_reason(context, skillDef, scoreInput));
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        _finalize_action_trace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private bool _is_ground_coord_set_within_cast_range(GodotObject context, GArray targetCoords, int effectiveSkillRange, bool usesRelocationDistance)
    {
        GodotObject gridService = GdInterop.GetObject(context, "grid_service");
        BattleUnitState unitState = GdInterop.GetObject(context, "unit_state") as BattleUnitState;
        if (context == null || gridService == null || unitState == null)
        {
            return true;
        }

        foreach (Variant coordValue in targetCoords)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
            {
                return true;
            }
            Vector2I coord = coordValue.AsVector2I();
            int distance = usesRelocationDistance
                ? gridService.Call("get_chebyshev_distance", unitState.coord, coord).AsInt32()
                : gridService.Call("get_distance_from_unit_to_coord", unitState, coord).AsInt32();
            if (distance > effectiveSkillRange)
            {
                return false;
            }
        }
        return true;
    }

    public bool _passes_minimum_effective_target_or_ground_control(GodotObject scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (GdInterop.GetInt(scoreInput, "effective_target_count") >= minimum_hit_count)
        {
            return true;
        }
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return true;
        }
        return _is_ground_control_supplement_candidate(scoreInput);
    }

    private bool _is_empty_ground_control_candidate(GodotObject scoreInput)
    {
        if (scoreInput == null || !allow_empty_ground_control)
        {
            return false;
        }
        if (GdInterop.GetInt(scoreInput, "effective_target_count") != 0)
        {
            return false;
        }
        if (GdInterop.GetInt(scoreInput, "estimated_ground_control_cell_count") <= 0)
        {
            return false;
        }
        return GdInterop.GetInt(scoreInput, "ground_control_score") >= minimum_ground_control_score;
    }

    private bool _is_ground_control_supplement_candidate(GodotObject scoreInput)
    {
        if (scoreInput == null || !allow_ground_control_supplement_partial_hits)
        {
            return false;
        }
        int effectiveTargetCount = GdInterop.GetInt(scoreInput, "effective_target_count");
        if (effectiveTargetCount <= 0 || effectiveTargetCount >= minimum_hit_count)
        {
            return false;
        }
        if (GdInterop.GetInt(scoreInput, "estimated_ground_control_cell_count") <= 0)
        {
            return false;
        }
        return GdInterop.GetInt(scoreInput, "ground_control_score") >= minimum_ground_control_score;
    }

    private string _resolve_minimum_hit_block_reason(GodotObject scoreInput)
    {
        if (scoreInput != null)
        {
            int effectiveTargetCount = GdInterop.GetInt(scoreInput, "effective_target_count");
            int groundCellCount = GdInterop.GetInt(scoreInput, "estimated_ground_control_cell_count");
            if (effectiveTargetCount == 0 && groundCellCount > 0)
            {
                if (!allow_empty_ground_control)
                {
                    return "empty_ground_control_not_allowed";
                }
                if (GdInterop.GetInt(scoreInput, "ground_control_score") < minimum_ground_control_score)
                {
                    return "minimum_ground_control_score";
                }
            }
            else if (effectiveTargetCount == 0 && allow_empty_ground_control)
            {
                return "no_ground_control_score";
            }
            else if (effectiveTargetCount > 0 && effectiveTargetCount < minimum_hit_count && groundCellCount > 0)
            {
                if (!allow_ground_control_supplement_partial_hits)
                {
                    return "ground_control_supplement_not_allowed";
                }
                if (GdInterop.GetInt(scoreInput, "ground_control_score") < minimum_ground_control_score)
                {
                    return "minimum_ground_control_score";
                }
            }
        }
        return "minimum_effective_hit_count";
    }

    private string _resolve_candidate_acceptance_reason(GodotObject scoreInput)
    {
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return "ground_control";
        }
        if (_is_ground_control_supplement_candidate(scoreInput))
        {
            return "ground_control_supplement";
        }
        return "effective_targets";
    }

    private string _build_decision_reason(GodotObject context, SkillDef skillDef, GodotObject scoreInput)
    {
        string unitName = (GdInterop.GetObject(context, "unit_state") as BattleUnitState)?.display_name ?? "";
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return $"{unitName} 准备用 {skillDef.display_name} 控制 {GdInterop.GetInt(scoreInput, "estimated_ground_control_cell_count")} 个地格（评分 {GdInterop.GetInt(scoreInput, "total_score")}）。";
        }
        if (_is_ground_control_supplement_candidate(scoreInput))
        {
            return $"{unitName} 准备用 {skillDef.display_name} 覆盖 {GdInterop.GetInt(scoreInput, "effective_target_count")} 个有效目标并控制 {GdInterop.GetInt(scoreInput, "estimated_ground_control_cell_count")} 个地格（评分 {GdInterop.GetInt(scoreInput, "total_score")}）。";
        }
        return $"{unitName} 准备用 {skillDef.display_name} 覆盖 {GdInterop.GetInt(scoreInput, "effective_target_count")} 个有效目标（评分 {GdInterop.GetInt(scoreInput, "total_score")}）。";
    }

    public bool _passes_friendly_fire_limits(GodotObject scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        string rejectReason = GdInterop.GetString(scoreInput, "friendly_fire_reject_reason");
        if (!string.IsNullOrEmpty(rejectReason))
        {
            return false;
        }
        if (_is_meteor_special_score_input(scoreInput))
        {
            return true;
        }
        if (GdInterop.GetInt(scoreInput, "estimated_friendly_fire_target_count") > maximum_friendly_fire_target_count)
        {
            return false;
        }
        if (!allow_friendly_lethal && GdInterop.GetInt(scoreInput, "estimated_friendly_lethal_target_count") > 0)
        {
            return false;
        }
        return true;
    }

    private static bool _is_meteor_special_score_input(GodotObject scoreInput)
    {
        GDictionary facts = GdInterop.GetDictionary(scoreInput, "special_profile_preview_facts");
        return GdInterop.GetString(facts, "profile_id") == "meteor_swarm";
    }

    private GDictionary _build_position_metadata(GodotObject context, BattleCommand command, SkillDef skillDef)
    {
        GDictionary metadata = _resolve_desired_distance_contract(context, skillDef).Duplicate(true);
        if (distance_reference == DistanceRefTargetCoord)
        {
            metadata["position_objective_kind"] = "cast_distance";
            metadata["position_target_coord"] = command != null ? command.target_coord : new Vector2I(-1, -1);
        }
        else if (distance_reference == DistanceRefEnemyFrontline)
        {
            GodotObject frontlineUnit = _resolve_enemy_frontline_unit(context);
            if (frontlineUnit != null)
            {
                metadata["position_target_unit"] = frontlineUnit;
            }
            else
            {
                metadata["position_objective_kind"] = "none";
            }
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private int _count_ally_threatening_preview_targets(GodotObject context, GodotObject preview)
    {
        if (minimum_ally_threat_hit_count <= 0 || context == null || preview == null)
        {
            return 0;
        }
        BattleState state = GdInterop.GetObject(context, "state") as BattleState;
        BattleUnitState unitState = GdInterop.GetObject(context, "unit_state") as BattleUnitState;
        if (state == null || unitState == null)
        {
            return 0;
        }

        GArray allies = _collect_units_by_filter(context, "ally");
        if (allies.Count == 0)
        {
            return 0;
        }

        int count = 0;
        foreach (Variant targetUnitIdValue in GdInterop.GetArray(preview, "target_unit_ids"))
        {
            StringName targetUnitId = ProgressionDataUtils.to_string_name(targetUnitIdValue);
            BattleUnitState targetUnit = state.units.ContainsKey(targetUnitId) ? state.units[targetUnitId].AsGodotObject() as BattleUnitState : null;
            if (targetUnit == null || targetUnit.faction_id == unitState.faction_id)
            {
                continue;
            }
            if (_is_target_threatening_any_ally(context, targetUnit, allies))
            {
                count += 1;
            }
        }
        return count;
    }

    private bool _is_target_threatening_any_ally(GodotObject context, BattleUnitState targetUnit, GArray allies)
    {
        if (context == null || targetUnit == null)
        {
            return false;
        }
        int safeDistance = _resolve_target_safe_distance(context, targetUnit, threat_minimum_safe_distance, threat_safe_distance_margin);
        foreach (Variant allyValue in allies)
        {
            BattleUnitState allyUnit = allyValue.AsGodotObject() as BattleUnitState;
            if (allyUnit == null || !allyUnit.is_alive)
            {
                continue;
            }
            if (_distance_between_units(context, targetUnit, allyUnit) <= safeDistance)
            {
                return true;
            }
        }
        return false;
    }

    private static GArray _collect_ground_skill_effect_defs(SkillDef skillDef, CombatCastVariantDef castVariant)
    {
        var effectDefs = new GArray();
        CombatSkillDef combatProfile = skillDef?.combat_profile as CombatSkillDef;
        if (combatProfile != null)
        {
            if (combatProfile.cast_variants.Count == 0)
            {
                if (castVariant != null)
                {
                    AppendEffects(effectDefs, castVariant.effect_defs);
                }
                else
                {
                    AppendEffects(effectDefs, combatProfile.effect_defs);
                }
                return effectDefs;
            }
            AppendEffects(effectDefs, combatProfile.effect_defs);
        }
        if (castVariant != null)
        {
            AppendEffects(effectDefs, castVariant.effect_defs);
        }
        return effectDefs;
    }

    private GodotObject _resolve_enemy_frontline_unit(GodotObject context)
    {
        GArray targets = _sort_target_units(context, "enemy", "nearest_enemy");
        return targets.Count > 0 ? targets[0].AsGodotObject() : null;
    }

    private bool _has_explicit_distance_contract()
    {
        return desired_min_distance >= 0
            && desired_max_distance >= desired_min_distance
            && (distance_reference == DistanceRefTargetCoord || distance_reference == DistanceRefEnemyFrontline);
    }

    public override Godot.Collections.Array<string> validate_schema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} must declare at least one skill_id.");
        }
        if (minimum_hit_count <= 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} minimum_hit_count must be >= 1.");
        }
        if (minimum_ground_control_score <= 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} minimum_ground_control_score must be >= 1.");
        }
        if (minimum_ally_threat_hit_count < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} minimum_ally_threat_hit_count must be >= 0.");
        }
        if (maximum_friendly_fire_target_count < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} maximum_friendly_fire_target_count must be >= 0.");
        }
        if (threat_minimum_safe_distance < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} threat_minimum_safe_distance must be >= 0.");
        }
        if (threat_safe_distance_margin < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} threat_safe_distance_margin must be >= 0.");
        }
        if (desired_min_distance < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} desired_min_distance must be >= 0.");
        }
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add($"UseGroundSkillAction {action_id} desired_max_distance must be >= desired_min_distance.");
        }
        if (distance_reference != DistanceRefTargetCoord && distance_reference != DistanceRefEnemyFrontline)
        {
            errors.Add($"UseGroundSkillAction {action_id} distance_reference must be target_coord or enemy_frontline.");
        }
        return errors;
    }

    private static void AppendEffects(GArray target, System.Collections.IEnumerable effects)
    {
        if (effects == null)
        {
            return;
        }
        foreach (object effect in effects)
        {
            if (effect != null)
            {
                target.Add(Variant.From(effect));
            }
        }
    }
}

