using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
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
        AiTraceRecorder.Enter("_resolve_target_role_threat_multiplier_basis_points");
        StringName targetUnitId = ProgressionDataUtils.to_string_name(targetUnit?.unit_id ?? "");
        if (
            _decisionScopeActive
            && !IsEmpty(targetUnitId)
            && _targetRoleThreatMultiplierCache.TryGetValue(targetUnitId, out int cachedResult)
        )
        {
            AiTraceRecorder.Exit("_resolve_target_role_threat_multiplier_basis_points");
            return cachedResult;
        }
        int result = ResolveTargetRoleThreatMultiplierBasisPointsImpl(context, targetUnit);
        if (_decisionScopeActive && !IsEmpty(targetUnitId))
        {
            _targetRoleThreatMultiplierCache[targetUnitId] = result;
        }
        AiTraceRecorder.Exit("_resolve_target_role_threat_multiplier_basis_points");
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = ContextSkillDefs(context);
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
                skillDef,
                ContextSkillCatalog(context)
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
                int effectiveRange = BattleRangeService.GetEffectiveSkillThreatRange(
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
        SkillDef skillDef,
        ISkillCatalog skillCatalog
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
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(skillCatalog, skillDef, skillLevel);
        foreach (CombatCastVariantDef castVariant in effectiveProfile.UnlockedCastVariants)
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
                BattleTypedNames.ToTargetFilter(skillDef.combat_profile.target_team_filter)
                == BattleTargetFilter.Ally
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
            if (effectDef.EffectKind == BattleEffectKind.Heal)
            {
                return true;
            }
            if (
                BattleTypedNames.ToTargetFilter(effectDef.effect_target_team_filter)
                == BattleTargetFilter.Ally
            )
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
            BattleEffectKind effectKind = effectDef.EffectKind;
            if (
                effectKind == BattleEffectKind.Status
                || effectKind == BattleEffectKind.ApplyStatus
                || effectKind == BattleEffectKind.ForcedMove
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
                && (effectDef.EffectKind == BattleEffectKind.Damage
                    || effectDef.EffectKind == BattleEffectKind.Execute)
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
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
    }

    private static double GetPreResistanceDamageMultiplier(CombatEffectDef effectDef)
    {
        return effectDef == null
            ? 1.0
            : Math.Max(effectDef.pre_resistance_damage_multiplier, 0.0);
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
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax));
        }
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.current_hp, 1);
        }
        int thresholdPercent =
            effectDef != null && effectDef.hp_ratio_threshold_percent > 0
                ? Mathf.Clamp(effectDef.hp_ratio_threshold_percent, 0, 100)
                : 50;
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
            || !HasBonusCondition(effectDef, targetUnit)
        )
        {
            return 0;
        }
        int diceCount = Math.Max(effectDef.bonus_damage_dice_count, 0);
        int diceSides = Math.Max(effectDef.bonus_damage_dice_sides, 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return 0;
        }
        int diceBonus = effectDef.bonus_damage_dice_bonus;
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
            BattleEffectKind effectKind = effectDef.EffectKind;
            if (effectKind == BattleEffectKind.TerrainEffect)
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
                effectKind == BattleEffectKind.Terrain
                || effectKind == BattleEffectKind.TerrainReplace
                || effectKind == BattleEffectKind.TerrainReplaceTo
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
            else if (
                effectKind == BattleEffectKind.Height
                || effectKind == BattleEffectKind.HeightDelta
            )
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
            CombatEffectDef effectDef = effectValue.As<CombatEffectDef>();
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
        return result;
    }

    internal int ResolveEstimatedHitRatePercent(BattlePreview preview)
    {
        return ResolveEstimatedHitRatePercent(preview?.hit_preview);
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
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(
                ContextSkillCatalog(context),
                skillDef,
                skillLevel
            );
        CombatSkillResourceCosts costs = effectiveProfile.ResourceCosts;
        scoreInput.ap_cost = Math.Max(costs.ApCost, 0);
        scoreInput.mp_cost = Math.Max(costs.MpCost, 0);
        scoreInput.stamina_cost = Math.Max(costs.StaminaCost, 0);
        scoreInput.aura_cost = Math.Max(costs.AuraCost, 0);
        scoreInput.cooldown_tu = Math.Max(costs.CooldownTu, 0);
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
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
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
        StringName explicitObjectiveId = metadata?.ObjectiveKind ?? "";
        BattlePositionObjectiveKind explicitObjectiveKind =
            BattleTypedNames.ToPositionObjectiveKind(explicitObjectiveId);
        if (explicitObjectiveKind == BattlePositionObjectiveKind.None)
        {
            scoreInput.position_objective_kind =
                BattleTypedNames.ToStringName(BattlePositionObjectiveKind.None);
            scoreInput.position_anchor_coord = actor.coord;
            scoreInput.distance_to_primary_coord = -1;
            scoreInput.position_objective_score = 0;
            return;
        }

        BattleUnitState positionTargetUnit = ResolvePositionTargetUnit(context, metadata);
        int currentDistanceToTarget = -1;
        bool hasExplicitObjective =
            explicitObjectiveKind != BattlePositionObjectiveKind.Unknown
            && explicitObjectiveKind != BattlePositionObjectiveKind.None;
        if (positionTargetUnit != null)
        {
            scoreInput.position_objective_kind = BattleTypedNames.ToStringName(
                hasExplicitObjective
                    ? explicitObjectiveKind
                    : BattlePositionObjectiveKind.DistanceBand
            );
            scoreInput.position_anchor_coord = ResolvePositionAnchorCoord(
                scoreInput,
                context,
                metadata
            );
            scoreInput.distance_to_primary_coord = DistanceFromAnchorToUnitCached(
                context,
                scoreInput.position_anchor_coord,
                positionTargetUnit
            );
            if (
                BattleTypedNames.ToPositionObjectiveKind(scoreInput.position_objective_kind)
                == BattlePositionObjectiveKind.DistanceBandProgress
            )
            {
                currentDistanceToTarget = DistanceFromAnchorToUnitCached(
                    context,
                    actor.coord,
                    positionTargetUnit
                );
            }
        }
        else
        {
            scoreInput.position_objective_kind = BattleTypedNames.ToStringName(
                hasExplicitObjective
                    ? explicitObjectiveKind
                    : BattlePositionObjectiveKind.CastDistance
            );
            scoreInput.position_anchor_coord = ResolvePositionAnchorCoord(
                scoreInput,
                context,
                metadata
            );
            scoreInput.distance_to_primary_coord =
                scoreInput.primary_coord != new Vector2I(-1, -1)
                    ? gridService.GetDistanceFromUnitToCoord(actor, scoreInput.primary_coord)
                    : -1;
        }
        scoreInput.position_objective_score = BuildPositionObjectiveScore(
            BattleTypedNames.ToPositionObjectiveKind(scoreInput.position_objective_kind),
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
