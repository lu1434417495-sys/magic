using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class BattleAiScoreService
{
    private sealed class HitRatePreviewEstimate
    {
        public readonly List<int> StageSuccessRates = new();
        public bool HasSuccessRatePercent;
        public int SuccessRatePercent = 100;

        public int ResolveEstimatedPercent()
        {
            if (StageSuccessRates.Count > 0)
            {
                int total = 0;
                foreach (int stageRate in StageSuccessRates)
                {
                    total += stageRate;
                }
                return Math.Max(RoundToInt((double)total / StageSuccessRates.Count), 0);
            }
            return HasSuccessRatePercent ? Math.Max(SuccessRatePercent, 0) : 100;
        }

        public static HitRatePreviewEstimate FromPreviewData(AttackPreviewData hitPreview)
        {
            var result = new HitRatePreviewEstimate();
            if (hitPreview == null || hitPreview.IsEmpty)
            {
                return result;
            }
            foreach (int rate in hitPreview.StageSuccessRates)
            {
                result.StageSuccessRates.Add(rate);
            }
            if (hitPreview.SuccessRatePercent > 0)
            {
                result.HasSuccessRatePercent = true;
                result.SuccessRatePercent = hitPreview.SuccessRatePercent;
            }
            return result;
        }
    }

    private int ResolveTargetRoleThreatMultiplierBasisPoints(
        IBattleAiScoreContext context,
        BattleUnitState targetUnit
    )
    {
        AiTraceRecorder.enter("_resolve_target_role_threat_multiplier_basis_points");
        int result = ResolveTargetRoleThreatMultiplierBasisPointsImpl(context, targetUnit);
        AiTraceRecorder.exit("_resolve_target_role_threat_multiplier_basis_points");
        return result;
    }

    private int ResolveTargetRoleThreatMultiplierBasisPointsImpl(
        IBattleAiScoreContext context,
        BattleUnitState targetUnit
    )
    {
        if (context == null || targetUnit == null || _scoreProfile == null)
        {
            return ThreatMultiplierBasisPointsDenominator;
        }
        int healSkillCount = 0;
        int controlSkillCount = 0;
        int bestRangedAttackRange = 0;
        GDictionary skillDefs = ContextSkillDefs(context);
        foreach (StringName skillId in targetUnit.known_active_skill_ids)
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (IsEmpty(normalizedSkillId))
            {
                continue;
            }
            SkillDef skillDef = GetSkillDef(skillDefs, normalizedSkillId);
            if (skillDef == null || skillDef.combat_profile == null)
            {
                continue;
            }
            List<CombatEffectDef> roleEffectDefs = CollectRoleThreatEffectDefs(
                targetUnit,
                skillDef
            );
            if (IsHealOrSupportSkill(skillDef, roleEffectDefs))
            {
                healSkillCount += 1;
            }
            if (IsControlSkill(roleEffectDefs))
            {
                controlSkillCount += 1;
            }
            if (IsDamageSkill(roleEffectDefs))
            {
                int effectiveRange = BattleRangeService.get_effective_skill_threat_range(
                    targetUnit,
                    skillDef
                );
                if (effectiveRange >= MinRangedThreatRange)
                {
                    bestRangedAttackRange = Math.Max(bestRangedAttackRange, effectiveRange);
                }
            }
        }

        int multiplierBasisPoints =
            ThreatMultiplierBasisPointsDenominator
            + healSkillCount * Math.Max(_scoreProfile.threat_healer_bias_basis_points, 0)
            + controlSkillCount * Math.Max(_scoreProfile.threat_control_bias_basis_points, 0);
        if (bestRangedAttackRange >= MinRangedThreatRange)
        {
            multiplierBasisPoints += Math.Max(_scoreProfile.threat_ranged_bias_basis_points, 0);
            multiplierBasisPoints +=
                (bestRangedAttackRange - (MinRangedThreatRange - 1))
                * Math.Max(_scoreProfile.threat_range_step_bias_basis_points, 0);
        }
        int capBasisPoints = _scoreProfile.threat_multiplier_cap_basis_points;
        if (capBasisPoints > ThreatMultiplierBasisPointsDenominator)
        {
            multiplierBasisPoints = Math.Min(multiplierBasisPoints, capBasisPoints);
        }
        return Math.Max(multiplierBasisPoints, ThreatMultiplierBasisPointsDenominator);
    }

    private static List<CombatEffectDef> CollectRoleThreatEffectDefs(
        BattleUnitState unitState,
        SkillDef skillDef
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        if (unitState == null || skillDef == null || skillDef.combat_profile == null)
        {
            return effectDefs;
        }
        int skillLevel = GetUnitSkillLevel(unitState, skillDef.skill_id);
        foreach (CombatEffectDef effectDef in skillDef.combat_profile.effect_defs)
        {
            if (effectDef != null && IsEffectUnlockedForSkillLevel(effectDef, skillLevel, true))
            {
                effectDefs.Add(effectDef);
            }
        }
        foreach (
            CombatCastVariantDef castVariant in skillDef.combat_profile.get_unlocked_cast_variants(
                skillLevel
            )
        )
        {
            if (castVariant == null)
            {
                continue;
            }
            foreach (CombatEffectDef effectDef in castVariant.effect_defs)
            {
                if (effectDef != null && IsEffectUnlockedForSkillLevel(effectDef, skillLevel, true))
                {
                    effectDefs.Add(effectDef);
                }
            }
        }
        return effectDefs;
    }

    private static bool IsHealOrSupportSkill(
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        if (skillDef != null && skillDef.combat_profile != null)
        {
            if (
                ProgressionDataUtils.to_string_name(skillDef.combat_profile.target_team_filter)
                == "ally"
            )
            {
                return true;
            }
        }
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef == null)
            {
                continue;
            }
            if (effectDef.effect_type == "heal")
            {
                return true;
            }
            if (ProgressionDataUtils.to_string_name(effectDef.effect_target_team_filter) == "ally")
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsControlSkill(IEnumerable<CombatEffectDef> effectDefs)
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = ProgressionDataUtils.to_string_name(effectDef.effect_type);
            if (
                effectType == "status"
                || effectType == "apply_status"
                || effectType == "forced_move"
            )
            {
                return true;
            }
            if (!IsEmpty(effectDef.status_id) || !IsEmpty(effectDef.save_failure_status_id))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsDamageSkill(IEnumerable<CombatEffectDef> effectDefs)
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (
                effectDef != null
                && (effectDef.effect_type == "damage" || effectDef.effect_type == "execute")
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || IsEmpty(skillId))
        {
            return 0;
        }
        return ReadKnownSkillLevel(unitState, skillId);
    }

    private static double GetPreResistanceDamageMultiplier(CombatEffectDef effectDef)
    {
        if (effectDef == null || effectDef.@params == null)
        {
            return 1.0;
        }
        return Math.Max(
            DictDouble(effectDef.@params, "runtime_pre_resistance_damage_multiplier", 1.0),
            0.0
        );
    }

    private static bool HasBonusCondition(CombatEffectDef effectDef, BattleUnitState targetUnit)
    {
        if (effectDef == null || targetUnit == null)
        {
            return false;
        }
        return effectDef.bonus_condition == BonusConditionTargetLowHp
            && IsTargetLowHp(effectDef, targetUnit);
    }

    private static bool IsTargetLowHp(CombatEffectDef effectDef, BattleUnitState targetUnit)
    {
        int maxHp = 0;
        if (targetUnit.attribute_snapshot != null)
        {
            maxHp = targetUnit
                .attribute_snapshot.get_value(AttributeService.HP_MAX_ID());
        }
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.current_hp, 1);
        }
        int thresholdPercent = 50;
        if (
            effectDef != null
            && effectDef.@params != null
            && HasKey(effectDef.@params, "hp_ratio_threshold_percent")
        )
        {
            thresholdPercent = Mathf.Clamp(
                DictInt(effectDef.@params, "hp_ratio_threshold_percent", thresholdPercent),
                0,
                100
            );
        }
        return targetUnit.current_hp * 100 <= maxHp * thresholdPercent;
    }

    private static double GetDamageRatioMultiplier(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return 1.0;
        }
        return Math.Max(effectDef.damage_ratio_percent / 100.0, 0.0);
    }

    private static int EstimateConditionalBonusDamage(
        CombatEffectDef effectDef,
        BattleUnitState targetUnit
    )
    {
        if (
            effectDef == null
            || effectDef.@params == null
            || !HasBonusCondition(effectDef, targetUnit)
        )
        {
            return 0;
        }
        int diceCount = Math.Max(DictInt(effectDef.@params, "bonus_damage_dice_count", 0), 0);
        int diceSides = Math.Max(DictInt(effectDef.@params, "bonus_damage_dice_sides", 0), 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return 0;
        }
        int diceBonus = DictInt(effectDef.@params, "bonus_damage_dice_bonus", 0);
        int numerator = diceCount * (diceSides + 1);
        int average = numerator / 2;
        if (numerator % 2 != 0)
        {
            average += 1;
        }
        return average + diceBonus;
    }

    private int EstimateGroundControlScorePerCell(IEnumerable<CombatEffectDef> effectDefs)
    {
        int terrainWeight = _scoreProfile?.terrain_weight ?? 0;
        int heightWeight = _scoreProfile?.height_weight ?? 0;
        int score = 0;
        var seenTerrainControls = new HashSet<string>();
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = ProgressionDataUtils.to_string_name(effectDef.effect_type);
            if (effectType == "terrain_effect")
            {
                if (IsEmpty(effectDef.terrain_effect_id))
                {
                    continue;
                }
                string effectKey = $"terrain_effect:{effectDef.terrain_effect_id}";
                if (seenTerrainControls.Add(effectKey))
                {
                    score += terrainWeight;
                }
            }
            else if (
                effectType == "terrain"
                || effectType == "terrain_replace"
                || effectType == "terrain_replace_to"
            )
            {
                if (IsEmpty(effectDef.terrain_replace_to))
                {
                    continue;
                }
                string terrainKey = $"terrain_replace:{effectDef.terrain_replace_to}";
                if (seenTerrainControls.Add(terrainKey))
                {
                    score += terrainWeight;
                }
            }
            else if (effectType == "height" || effectType == "height_delta")
            {
                score += Math.Abs(effectDef.height_delta) * heightWeight;
            }
        }
        return score;
    }

    private static GArray ToEffectArray(IEnumerable<CombatEffectDef> effectDefs)
    {
        var result = new GArray();
        if (effectDefs == null)
        {
            return result;
        }
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
        return result;
    }

    private static List<CombatEffectDef> DecodeEffectDefs(GArray effectDefs)
    {
        var result = new List<CombatEffectDef>();
        if (effectDefs == null)
        {
            return result;
        }
        foreach (var effectValue in effectDefs)
        {
            if (effectValue.AsGodotObject() is CombatEffectDef effectDef)
            {
                result.Add(effectDef);
            }
        }
        return result;
    }

    public int _resolve_estimated_hit_rate_percent(GodotObject preview)
    {
        if (preview is BattlePreview typedPreview)
        {
            return ResolveEstimatedHitRatePercent(typedPreview?.hit_preview);
        }
        return 100;
    }

    private static int ResolveEstimatedHitRatePercent(AttackPreviewData hitPreview)
    {
        return HitRatePreviewEstimate.FromPreviewData(hitPreview).ResolveEstimatedPercent();
    }

    private void PopulateResourceCostMetrics(
        BattleAiScoreInput scoreInput,
        SkillDef skillDef,
        IBattleAiScoreContext context
    )
    {
        if (scoreInput == null || skillDef == null || skillDef.combat_profile == null)
        {
            return;
        }
        int skillLevel = GetContextSkillLevel(context, skillDef.skill_id);
        GDictionary costs = skillDef.combat_profile.get_effective_resource_costs(skillLevel);
        scoreInput.ap_cost = Math.Max(
            DictInt(costs, "ap_cost", skillDef.combat_profile.ap_cost),
            0
        );
        scoreInput.mp_cost = Math.Max(
            DictInt(costs, "mp_cost", skillDef.combat_profile.mp_cost),
            0
        );
        scoreInput.stamina_cost = Math.Max(
            DictInt(costs, "stamina_cost", skillDef.combat_profile.stamina_cost),
            0
        );
        scoreInput.aura_cost = Math.Max(
            DictInt(costs, "aura_cost", skillDef.combat_profile.aura_cost),
            0
        );
        scoreInput.cooldown_tu = Math.Max(
            DictInt(costs, "cooldown_tu", skillDef.combat_profile.cooldown_tu),
            0
        );
        scoreInput.resource_cost_score =
            scoreInput.ap_cost * _scoreProfile.ap_cost_weight
            + scoreInput.mp_cost * _scoreProfile.mp_cost_weight
            + scoreInput.stamina_cost * _scoreProfile.stamina_cost_weight
            + scoreInput.aura_cost * _scoreProfile.aura_cost_weight
            + scoreInput.cooldown_tu * _scoreProfile.cooldown_weight;
    }

    private static int GetContextSkillLevel(IBattleAiScoreContext context, StringName skillId)
    {
        if (context == null || IsEmpty(skillId))
        {
            return 0;
        }
        BattleUnitState unitState = ContextUnitState(context);
        if (unitState == null)
        {
            return 0;
        }
        return ReadKnownSkillLevel(unitState, skillId);
    }

    private static int ReadKnownSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (TryRead(unitState.known_skill_level_map, skillId, out var levelValue))
        {
            return levelValue.AsInt32();
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private void PopulatePositionMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        ScorePositionMetadata metadata
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        BattleGridService gridService = ContextGridService(context);
        if (scoreInput == null || actor == null || gridService == null)
        {
            return;
        }
        int desiredMinDistance = metadata?.DesiredMinDistance ?? -1;
        int desiredMaxDistance = metadata?.DesiredMaxDistance ?? desiredMinDistance;
        scoreInput.desired_min_distance = desiredMinDistance;
        scoreInput.desired_max_distance =
            desiredMinDistance >= 0 && desiredMaxDistance >= 0
                ? Math.Max(desiredMaxDistance, desiredMinDistance)
                : -1;
        scoreInput.position_current_distance = metadata?.CurrentDistance ?? -1;
        scoreInput.position_safe_distance = metadata?.SafeDistance ?? -1;
        StringName explicitObjectiveKind = metadata?.ObjectiveKind ?? "";
        if (explicitObjectiveKind == "none")
        {
            scoreInput.position_objective_kind = "none";
            scoreInput.position_anchor_coord = actor.coord;
            scoreInput.distance_to_primary_coord = -1;
            scoreInput.position_objective_score = 0;
            return;
        }

        BattleUnitState positionTargetUnit = ResolvePositionTargetUnit(context, metadata);
        int currentDistanceToTarget = -1;
        if (positionTargetUnit != null)
        {
            scoreInput.position_objective_kind = !IsEmpty(explicitObjectiveKind)
                ? explicitObjectiveKind
                : "distance_band";
            scoreInput.position_anchor_coord = ResolvePositionAnchorCoord(
                scoreInput,
                context,
                metadata
            );
            scoreInput.distance_to_primary_coord = DistanceFromAnchorToUnit(
                context,
                scoreInput.position_anchor_coord,
                positionTargetUnit
            );
            if (scoreInput.position_objective_kind == "distance_band_progress")
            {
                currentDistanceToTarget = DistanceFromAnchorToUnit(
                    context,
                    actor.coord,
                    positionTargetUnit
                );
            }
        }
        else
        {
            scoreInput.position_objective_kind = !IsEmpty(explicitObjectiveKind)
                ? explicitObjectiveKind
                : "cast_distance";
            scoreInput.position_anchor_coord = ResolvePositionAnchorCoord(
                scoreInput,
                context,
                metadata
            );
            scoreInput.distance_to_primary_coord =
                scoreInput.primary_coord != new Vector2I(-1, -1)
                    ? gridService.get_distance_from_unit_to_coord(actor, scoreInput.primary_coord)
                    : -1;
        }
        scoreInput.position_objective_score = BuildPositionObjectiveScore(
            scoreInput.position_objective_kind,
            scoreInput.distance_to_primary_coord,
            scoreInput.desired_min_distance,
            scoreInput.desired_max_distance,
            currentDistanceToTarget
        );
    }

    private static BattleUnitState ResolvePositionTargetUnit(
        IBattleAiScoreContext context,
        ScorePositionMetadata metadata
    )
    {
        if (metadata == null || IsEmpty(metadata.TargetUnitId))
        {
            return null;
        }
        return GetUnit(ContextState(context), metadata.TargetUnitId);
    }

    private static Vector2I ResolvePositionAnchorCoord(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        ScorePositionMetadata metadata
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null)
        {
            return new Vector2I(-1, -1);
        }
        Vector2I metadataAnchor = metadata?.AnchorCoord ?? new Vector2I(-1, -1);
        if (metadataAnchor != new Vector2I(-1, -1))
        {
            return metadataAnchor;
        }
        if (
            scoreInput != null
            && scoreInput.preview != null
            && scoreInput.preview.resolved_anchor_coord != new Vector2I(-1, -1)
        )
        {
            return scoreInput.preview.resolved_anchor_coord;
        }
        return actor.coord;
    }

    private void PopulatePostActionThreatProjection(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        ScorePositionMetadata metadata
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        BattleGridService gridService = ContextGridService(context);
        if (scoreInput == null || state == null || actor == null || gridService == null)
        {
            return;
        }
        if (!ShouldPopulateSurvivalProjection(scoreInput, context))
        {
            return;
        }
        int actorHpBudget = ResolveActorSurvivalBudget(actor);
        Vector2I projectedCoord = ResolveProjectedActorCoord(scoreInput, context, metadata);
        HashSet<StringName> suppressedThreatIds = BuildSuppressedThreatUnitIds(scoreInput);
        ThreatProjection preProjection = GetCurrentActorThreatProjection(context);
        ThreatProjection postProjection = GetProjectedActorThreatProjection(
            context,
            projectedCoord,
            suppressedThreatIds,
            preProjection
        );
        scoreInput.has_post_action_threat_projection = true;
        scoreInput.projected_actor_coord = projectedCoord;
        scoreInput.pre_action_threat_unit_ids = CopyStringNameArray(preProjection.UnitIds);
        scoreInput.pre_action_threat_count = preProjection.Count;
        scoreInput.pre_action_threat_expected_damage = preProjection.ExpectedDamage;
        scoreInput.pre_action_survival_margin =
            actorHpBudget - scoreInput.pre_action_threat_expected_damage;
        scoreInput.pre_action_is_lethal_survival_risk =
            scoreInput.pre_action_threat_count > 0
            && scoreInput.pre_action_threat_expected_damage >= actorHpBudget;
        scoreInput.post_action_remaining_threat_unit_ids = CopyStringNameArray(
            postProjection.UnitIds
        );
        scoreInput.post_action_remaining_threat_count = postProjection.Count;
        scoreInput.post_action_remaining_threat_expected_damage = postProjection.ExpectedDamage;
        scoreInput.post_action_survival_margin =
            actorHpBudget - scoreInput.post_action_remaining_threat_expected_damage;
        scoreInput.post_action_is_lethal_survival_risk =
            scoreInput.post_action_remaining_threat_count > 0
            && scoreInput.post_action_remaining_threat_expected_damage >= actorHpBudget;
    }
}
