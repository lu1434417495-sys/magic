using System;
using System.Collections.Generic;
using Godot;

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
        using BattleAiTraceSpan trace = new(
            "_resolve_target_role_threat_multiplier_basis_points"
        );
        StringName targetUnitId = ProgressionDataUtils.to_string_name(targetUnit?.unit_id ?? "");
        if (
            _decisionScopeActive
            && !IsEmpty(targetUnitId)
            && _targetRoleThreatMultiplierCache.TryGetValue(targetUnitId, out int cachedResult)
        )
        {
            return cachedResult;
        }
        int result = ResolveTargetRoleThreatMultiplierBasisPointsImpl(context, targetUnit);
        if (_decisionScopeActive && !IsEmpty(targetUnitId))
        {
            _targetRoleThreatMultiplierCache[targetUnitId] = result;
        }
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
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            ContextSkillDefinitions(context);
        foreach (
            StringName skillId in targetUnit.GetKnownActiveSkillsViewTyped()
        )
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (IsEmpty(normalizedSkillId))
            {
                continue;
            }
            SkillDefinition skillDefinition = GetSkillDefinition(
                skillDefinitions,
                normalizedSkillId
            );
            if (skillDefinition == null || skillDefinition.CombatProfile == null)
            {
                continue;
            }
            List<CombatEffectDefinition> roleEffectDefs = CollectRoleThreatEffectDefinitions(
                targetUnit,
                skillDefinition,
                ContextSkillCatalog(context)
            );
            if (IsHealOrSupportSkill(skillDefinition, roleEffectDefs))
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
                    skillDefinition
                );
                if (effectiveRange >= MinRangedThreatRange)
                {
                    bestRangedAttackRange = Math.Max(bestRangedAttackRange, effectiveRange);
                }
            }
        }

        int multiplierBasisPoints =
            ThreatMultiplierBasisPointsDenominator
            + healSkillCount * Math.Max(_scoreProfile.ThreatHealerBiasBasisPoints, 0)
            + controlSkillCount * Math.Max(_scoreProfile.ThreatControlBiasBasisPoints, 0);
        if (bestRangedAttackRange >= MinRangedThreatRange)
        {
            multiplierBasisPoints += Math.Max(_scoreProfile.ThreatRangedBiasBasisPoints, 0);
            multiplierBasisPoints +=
                (bestRangedAttackRange - (MinRangedThreatRange - 1))
                * Math.Max(_scoreProfile.ThreatRangeStepBiasBasisPoints, 0);
        }
        int capBasisPoints = _scoreProfile.ThreatMultiplierCapBasisPoints;
        if (capBasisPoints > ThreatMultiplierBasisPointsDenominator)
        {
            multiplierBasisPoints = Math.Min(multiplierBasisPoints, capBasisPoints);
        }
        return Math.Max(multiplierBasisPoints, ThreatMultiplierBasisPointsDenominator);
    }

    private static List<CombatEffectDefinition> CollectRoleThreatEffectDefinitions(
        BattleUnitState unitState,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        if (unitState == null || skillDefinition == null || skillDefinition.CombatProfile == null)
        {
            return effectDefinitions;
        }
        int skillLevel = GetUnitSkillLevel(unitState, skillDefinition.SkillId);
        foreach (CombatEffectDefinition effectDefinition in skillDefinition.CombatProfile.EffectDefinitions)
        {
            if (
                effectDefinition != null
                && IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel, true)
            )
            {
                effectDefinitions.Add(effectDefinition);
            }
        }
        SkillEffectiveCombatDefinition effectiveDefinition =
            skillCatalog != null
                ? skillCatalog.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
                : SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant == null)
            {
                continue;
            }
            foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions)
            {
                if (
                    effectDefinition != null
                    && IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel, true)
                )
                {
                    effectDefinitions.Add(effectDefinition);
                }
            }
        }
        return effectDefinitions;
    }

    private static bool IsHealOrSupportSkill(
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        if (skillDefinition?.CombatProfile != null)
        {
            if (
                BattleTypedNames.ToTargetFilter(skillDefinition.CombatProfile.TargetTeamFilter)
                == BattleTargetFilter.Ally
            )
            {
                return true;
            }
        }
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
            {
                continue;
            }
            if (effectDefinition.EffectKind == BattleEffectKind.Heal)
            {
                return true;
            }
            if (
                BattleTypedNames.ToTargetFilter(effectDefinition.EffectTargetTeamFilter)
                == BattleTargetFilter.Ally
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsControlSkill(IEnumerable<CombatEffectDefinition> effectDefinitions)
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
            {
                continue;
            }
            BattleEffectKind effectKind = effectDefinition.EffectKind;
            if (
                effectKind == BattleEffectKind.Status
                || effectKind == BattleEffectKind.ApplyStatus
                || effectKind == BattleEffectKind.ForcedMove
            )
            {
                return true;
            }
            if (
                !IsEmpty(effectDefinition.StatusId)
                || !IsEmpty(effectDefinition.SaveFailureStatusId)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsDamageSkill(IEnumerable<CombatEffectDefinition> effectDefinitions)
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition != null
                && (effectDefinition.EffectKind == BattleEffectKind.Damage
                    || effectDefinition.EffectKind == BattleEffectKind.Execute)
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
            : unitState.KnowsActiveSkill(skillId)
                ? 1
                : 0;
    }

    private static double GetPreResistanceDamageMultiplier(
        CombatEffectDefinition effectDefinition
    )
    {
        return effectDefinition == null
            ? 1.0
            : Math.Max(effectDefinition.PreResistanceDamageMultiplier, 0.0);
    }

    private static bool HasBonusCondition(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    ) => BattleDamageBonusConditionRules.IsMet(effectDefinition, targetUnit);

    private static double GetDamageRatioMultiplier(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
        {
            return 1.0;
        }
        return Math.Max(effectDefinition.DamageRatioPercent / 100.0, 0.0);
    }

    private static int EstimateConditionalBonusDamage(
        CombatEffectDefinition effectDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (
            effectDefinition == null
            || !HasBonusCondition(effectDefinition, targetUnit)
        )
        {
            return 0;
        }
        int bonusWeaponDamage = 0;
        if (
            effectDefinition.BonusWeaponDiceMultiplier > 0
            && effectDefinition.AddWeaponDice
            && sourceUnit != null
        )
        {
            BattleWeaponDiceValues weaponDice =
                sourceUnit.GetWeaponProjectionReadViewTyped().Values.ActiveDice;
            if (weaponDice.HasUsableDice)
            {
                bonusWeaponDamage = EstimateAverageDiceDamage(
                    weaponDice.DiceCount
                        * effectDefinition.BonusWeaponDiceMultiplier,
                    weaponDice.DiceSides,
                    0
                );
            }
        }
        int diceCount = Math.Max(effectDefinition.BonusDamageDiceCount, 0);
        int diceSides = Math.Max(effectDefinition.BonusDamageDiceSides, 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return bonusWeaponDamage;
        }
        int diceBonus = effectDefinition.BonusDamageDiceBonus;
        int numerator = diceCount * (diceSides + 1);
        int average = numerator / 2;
        if (numerator % 2 != 0)
        {
            average += 1;
        }
        return bonusWeaponDamage + average + diceBonus;
    }

    private int EstimateGroundControlScorePerCell(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        int terrainWeight = _scoreProfile?.TerrainWeight ?? 0;
        int heightWeight = _scoreProfile?.HeightWeight ?? 0;
        int score = 0;
        var seenTerrainControls = new HashSet<string>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
            {
                continue;
            }
            BattleEffectKind effectKind = effectDefinition.EffectKind;
            if (effectKind == BattleEffectKind.TerrainEffect)
            {
                if (IsEmpty(effectDefinition.TerrainEffectId))
                {
                    continue;
                }
                string effectKey = $"terrain_effect:{effectDefinition.TerrainEffectId}";
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
                if (IsEmpty(effectDefinition.TerrainReplaceTo))
                {
                    continue;
                }
                string terrainKey = $"terrain_replace:{effectDefinition.TerrainReplaceTo}";
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
                score += Math.Abs(effectDefinition.HeightDelta) * heightWeight;
            }
        }
        return score;
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
        SkillDefinition skillDefinition,
        IBattleAiScoreContext context
    )
    {
        if (scoreInput == null || skillDefinition == null || skillDefinition.CombatProfile == null)
        {
            return;
        }
        int skillLevel = GetContextSkillLevel(context, skillDefinition.SkillId);
        SkillEffectiveCombatDefinition effectiveDefinition =
            ContextSkillCatalog(context) != null
                ? ContextSkillCatalog(context)
                    .GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
                : SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        CombatSkillResourceCosts costs = effectiveDefinition.ResourceCosts;
        scoreInput.ap_cost = Math.Max(costs.ApCost, 0);
        scoreInput.mp_cost = Math.Max(costs.MpCost, 0);
        scoreInput.stamina_cost = Math.Max(costs.StaminaCost, 0);
        scoreInput.aura_cost = Math.Max(costs.AuraCost, 0);
        scoreInput.cooldown_tu = Math.Max(costs.CooldownTu, 0);
        scoreInput.resource_cost_score =
            scoreInput.ap_cost * _scoreProfile.ApCostWeight
            + scoreInput.mp_cost * _scoreProfile.MpCostWeight
            + scoreInput.stamina_cost * _scoreProfile.StaminaCostWeight
            + scoreInput.aura_cost * _scoreProfile.AuraCostWeight
            + scoreInput.cooldown_tu * _scoreProfile.CooldownWeight;
        scoreInput.resource_cost_score += BuildReserveResourceCost(
            ContextUnitState(context),
            scoreInput
        );
    }

    private int BuildReserveResourceCost(
        BattleUnitState actor,
        BattleAiScoreInput scoreInput
    )
    {
        if (actor == null || scoreInput == null || _scoreProfile == null)
        {
            return 0;
        }
        int sustainCost =
            BuildSingleReserveResourceCost(
                actor.GetCurrentMp(),
                GetActorResourceMax(actor, AttributeService.ToStringName(AttributeIdKind.MpMax)),
                scoreInput.mp_cost,
                _scoreProfile.MpReserveFloorBp,
                _scoreProfile.MpReservePressureWeight,
                _scoreProfile.MpReserveBreachPenalty
            )
            + BuildSingleReserveResourceCost(
                actor.GetCurrentStamina(),
                GetActorResourceMax(
                    actor,
                    AttributeService.ToStringName(AttributeIdKind.StaminaMax)
                ),
                scoreInput.stamina_cost,
                _scoreProfile.StaminaReserveFloorBp,
                _scoreProfile.StaminaReservePressureWeight,
                _scoreProfile.StaminaReserveBreachPenalty
            )
            + BuildSingleReserveResourceCost(
                actor.GetCurrentAura(),
                Math.Max(actor.GetAuraMax(), actor.GetCurrentAura()),
                scoreInput.aura_cost,
                _scoreProfile.AuraReserveFloorBp,
                _scoreProfile.AuraReservePressureWeight,
                _scoreProfile.AuraReserveBreachPenalty
            );
        if (sustainCost == 0)
        {
            return 0;
        }
        return ScaleByPercent(sustainCost, _scoreProfile.ResourceConservationWeight);
    }

    private static int BuildSingleReserveResourceCost(
        int current,
        int max,
        int cost,
        int floorBasisPoints,
        int pressureWeight,
        int breachPenalty
    )
    {
        if (
            cost <= 0
            || max <= 0
            || floorBasisPoints <= 0
            || (pressureWeight == 0 && breachPenalty == 0)
        )
        {
            return 0;
        }
        int clampedFloor = Mathf.Clamp(
            floorBasisPoints,
            0,
            ThreatMultiplierBasisPointsDenominator
        );
        int fillAfterBasisPoints = Mathf.Clamp(
            RoundToInt(
                (double)Math.Max(current - cost, 0)
                    * ThreatMultiplierBasisPointsDenominator
                    / max
            ),
            0,
            ThreatMultiplierBasisPointsDenominator
        );
        int belowBasisPoints = Math.Max(clampedFloor - fillAfterBasisPoints, 0);
        if (belowBasisPoints <= 0)
        {
            return 0;
        }
        int costScore = RoundToInt(
            (double)pressureWeight
                * belowBasisPoints
                / ThreatMultiplierBasisPointsDenominator
        );
        if (fillAfterBasisPoints < clampedFloor)
        {
            costScore += breachPenalty;
        }
        return costScore;
    }

    private static int GetActorResourceMax(BattleUnitState actor, StringName attributeId)
    {
        if (actor == null)
        {
            return 0;
        }
        int maxValue = actor.attribute_snapshot?.GetValue(attributeId) ?? 0;
        return Math.Max(maxValue, 0);
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
            : unitState.KnowsActiveSkill(skillId)
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
            scoreInput.position_anchor_coord = actor.GetAnchorCoord();
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
                    actor.GetAnchorCoord(),
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
        return actor.GetAnchorCoord();
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
