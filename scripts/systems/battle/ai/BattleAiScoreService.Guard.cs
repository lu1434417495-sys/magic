using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private readonly record struct TimedPhysicalMitigation(int Power, int DurationTu)
    {
        internal bool IsActive => Power > 0 && DurationTu > 0;
    }

    private static bool IsDedicatedThreatMitigationStatus(StringName statusId) =>
        ProgressionDataUtils.to_string_name(statusId)
        == BattleStatusSemanticTable.STATUS_GUARDING;

    private static BattleUnitState BuildUnguardedThreatProjectionTarget(
        BattleUnitState actor
    )
    {
        if (
            actor == null
            || !actor.HasStatusEffect(BattleStatusSemanticTable.STATUS_GUARDING)
        )
        {
            return actor;
        }
        BattleUnitState projectionTarget = actor.DuplicateForPreview();
        projectionTarget?.EraseStatusEffect(BattleStatusSemanticTable.STATUS_GUARDING);
        return projectionTarget ?? actor;
    }

    private static IReadOnlyList<int> CollectPhysicalDamageInstances(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        DamageEstimateResult damageEstimate
    )
    {
        var result = new List<int>();
        if (damageEstimate == null)
        {
            return result;
        }
        int breakdownIndex = 0;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.Damage
            )
            {
                continue;
            }
            DamageEstimateBreakdown breakdown =
                breakdownIndex < damageEstimate.DamageEstimates.Count
                    ? damageEstimate.DamageEstimates[breakdownIndex]
                    : null;
            breakdownIndex += 1;
            if (
                breakdown == null
                || !IsPhysicalDamageEffect(sourceUnit, effectDefinition)
            )
            {
                continue;
            }
            result.Add(Math.Max(breakdown.IncomingBudgetDamage, 0));
        }
        return result;
    }

    private static bool IsPhysicalDamageEffect(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        StringName damageTag = effectDefinition.UseWeaponPhysicalDamageTag
            ? sourceUnit
                ?.GetWeaponProjectionReadViewTyped()
                .Values.PhysicalDamageTag
                ?? new StringName("")
            : ProgressionDataUtils.to_string_name(effectDefinition.DamageTag);
        return DamageTagContentRules.IsPhysicalDamageTag(
            DamageTagContentRules.ToDamageTagKind(damageTag)
        );
    }

    private void PopulateGuardAwareWeaponThreat(
        ThreatProfile profile,
        BattleUnitState threatUnit,
        BattleUnitState actor,
        BattleUnitState unguardedActor
    )
    {
        if (
            profile == null
            || profile.GuardAwareWeaponInitialized
            || threatUnit == null
            || actor == null
        )
        {
            return;
        }
        profile.GuardAwareWeaponInitialized = true;
        BattleWeaponProjectionValues weapon =
            threatUnit.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponDiceValues dice = weapon.ActiveDice;
        if (
            !dice.HasUsableDice
            || !DamageTagContentRules.IsPhysicalDamageTag(
                DamageTagContentRules.ToDamageTagKind(weapon.PhysicalDamageTag)
            )
        )
        {
            profile.UnguardedWeaponDamage = profile.WeaponDamage;
            return;
        }
        CombatEffectDefinition weaponEffect = BattleRuntimeEffectDefinitions.Damage(
            weapon.PhysicalDamageTag,
            Math.Max(dice.DiceCount, 0),
            Math.Max(dice.DiceSides, 0),
            Math.Max(dice.FlatBonus, 0)
        );
        DamageEstimateResult unguardedEstimate = EstimateDamageForTargetResult(
            threatUnit,
            new[] { weaponEffect },
            unguardedActor
        );
        profile.UnguardedWeaponDamage = unguardedEstimate.IncomingBudgetDamage;
        profile.UnguardedWeaponPhysicalDamageByInstance.AddRange(
            CollectPhysicalDamageInstances(
                threatUnit,
                new[] { weaponEffect },
                unguardedEstimate
            )
        );
    }

    private void ApplyTimedPhysicalMitigationProjection(
        IBattleAiScoreContext context,
        Vector2I projectedCoord,
        HashSet<StringName> suppressedThreatIds,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        ref ThreatProjection preProjection,
        ref ThreatProjection postProjection
    )
    {
        TimedPhysicalMitigation candidate = ResolveTimedPhysicalMitigation(
            effectDefinitions
        );
        if (!candidate.IsActive)
        {
            return;
        }
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null)
        {
            return;
        }
        TimedPhysicalMitigation existing = ResolveExistingPhysicalMitigation(actor);
        preProjection = BuildTimedMitigationThreatProjection(
            context,
            actor.GetAnchorCoord(),
            new HashSet<StringName>(),
            existing
        );
        postProjection = BuildTimedMitigationThreatProjection(
            context,
            projectedCoord,
            suppressedThreatIds,
            candidate
        );
    }

    private ThreatProjection BuildTimedMitigationThreatProjection(
        IBattleAiScoreContext context,
        Vector2I actorCoord,
        HashSet<StringName> suppressedThreatIds,
        TimedPhysicalMitigation mitigation
    )
    {
        BattleState state = ContextState(context);
        var projection = new ThreatProjection();
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null)
        {
            return projection;
        }
        BattleUnitState unguardedActor = BuildUnguardedThreatProjectionTarget(actor);
        foreach (BattleUnitState threatUnit in GetHostileThreatUnitsForActor(context))
        {
            if (
                threatUnit == null
                || (
                    suppressedThreatIds != null
                    && suppressedThreatIds.Contains(threatUnit.unit_id)
                )
            )
            {
                continue;
            }
            ThreatProfile profile = GetUnitThreatProfile(context, threatUnit);
            PopulateGuardAwareWeaponThreat(
                profile,
                threatUnit,
                actor,
                unguardedActor
            );
            if (profile.Range <= 0)
            {
                continue;
            }
            int distance = DistanceFromAnchorToUnitCached(context, actorCoord, threatUnit);
            if (distance < 0 || distance > profile.Range)
            {
                continue;
            }
            int readyInTu = ResolveThreatReadyInTu(state, threatUnit);
            int activePower =
                mitigation.IsActive && readyInTu < mitigation.DurationTu
                    ? mitigation.Power
                    : 0;
            int damage = EstimateThreatProfileDamageAtDistanceWithGuard(
                profile,
                distance,
                activePower
            );
            projection.UnitIds.Add(threatUnit.unit_id);
            projection.ExpectedDamageByUnitId[threatUnit.unit_id] = damage;
            projection.ExpectedDamage += damage;
        }
        projection.UnitIds.Sort(
            (left, right) => string.CompareOrdinal(left.ToString(), right.ToString())
        );
        return projection;
    }

    private static TimedPhysicalMitigation ResolveTimedPhysicalMitigation(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        TimedPhysicalMitigation strongest = default;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || (
                    effectDefinition.EffectKind != BattleEffectKind.Status
                    && effectDefinition.EffectKind != BattleEffectKind.ApplyStatus
                )
                || !IsDedicatedThreatMitigationStatus(effectDefinition.StatusId)
            )
            {
                continue;
            }
            TimedPhysicalMitigation candidate = new(
                Math.Max(effectDefinition.Power, 0),
                Math.Max(effectDefinition.DurationTu, 0)
            );
            if (
                candidate.Power > strongest.Power
                || (
                    candidate.Power == strongest.Power
                    && candidate.DurationTu > strongest.DurationTu
                )
            )
            {
                strongest = candidate;
            }
        }
        return strongest;
    }

    private static TimedPhysicalMitigation ResolveExistingPhysicalMitigation(
        BattleUnitState actor
    )
    {
        BattleStatusEffectState status = actor?.GetStatusEffect(
            BattleStatusSemanticTable.STATUS_GUARDING
        );
        return status == null
            ? default
            : new TimedPhysicalMitigation(
                Math.Max(status.power, 0),
                Math.Max(status.duration, 0)
            );
    }

    private static int ResolveThreatReadyInTu(
        BattleState state,
        BattleUnitState threatUnit
    )
    {
        if (threatUnit == null)
        {
            return int.MaxValue;
        }
        if (state?.timeline?.ready_unit_ids?.Contains(threatUnit.unit_id) == true)
        {
            return 0;
        }
        int ratePercent = BattleTemporalStatusService.GetActionProgressRatePercent(
            threatUnit
        );
        if (ratePercent <= 0)
        {
            return int.MaxValue;
        }
        int threshold = threatUnit.GetActionThresholdTyped();
        if (threshold <= 0)
        {
            threshold = BattleUnitState.DefaultActionThreshold;
        }
        int progress = Math.Clamp(
            threatUnit.GetActionProgressTyped(),
            0,
            Math.Max(threshold - 1, 0)
        );
        int remainingProgress = Math.Max(threshold - progress, 0);
        return (remainingProgress * 100 + ratePercent - 1) / ratePercent;
    }

    private static int EstimateThreatProfileDamageAtDistanceWithGuard(
        ThreatProfile profile,
        int distance,
        int guardPower
    )
    {
        if (profile == null)
        {
            return 0;
        }
        int bestDamage = 0;
        foreach (ThreatSkillEntry entry in profile.SkillEntries)
        {
            if (entry == null || (distance >= 0 && entry.Range < distance))
            {
                continue;
            }
            bestDamage = Math.Max(
                bestDamage,
                ApplyPerInstancePhysicalMitigation(
                    entry.UnguardedDamage,
                    entry.UnguardedPhysicalDamageByInstance,
                    guardPower
                )
            );
        }
        if (distance < 0 || profile.WeaponRange >= distance)
        {
            bestDamage = Math.Max(
                bestDamage,
                ApplyPerInstancePhysicalMitigation(
                    profile.UnguardedWeaponDamage,
                    profile.UnguardedWeaponPhysicalDamageByInstance,
                    guardPower
                )
            );
        }
        return bestDamage;
    }

    private static int ApplyPerInstancePhysicalMitigation(
        int unguardedDamage,
        IReadOnlyList<int> physicalDamageByInstance,
        int guardPower
    )
    {
        int result = Math.Max(unguardedDamage, 0);
        int normalizedPower = Math.Max(guardPower, 0);
        if (normalizedPower <= 0)
        {
            return result;
        }
        foreach (int rawDamage in physicalDamageByInstance ?? Array.Empty<int>())
        {
            int damage = Math.Max(rawDamage, 0);
            int mitigatedDamage = damage > 0
                ? Math.Max(damage - normalizedPower, 1)
                : 0;
            result -= Math.Max(damage - mitigatedDamage, 0);
        }
        return Math.Max(result, 0);
    }

    private void PopulateSelfAppliedMovementPenaltyCost(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || actor == null || actor.GetCurrentMovePoints() <= 0)
        {
            return;
        }
        var candidateMoveCostDeltaByStatus = new Dictionary<StringName, int>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || (
                    effectDefinition.EffectKind != BattleEffectKind.Status
                    && effectDefinition.EffectKind != BattleEffectKind.ApplyStatus
                )
            )
            {
                continue;
            }
            StringName targetFilter = ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            );
            if (!IsBeneficialEffectFilter(targetFilter))
            {
                continue;
            }
            BattleStatusSemantic semantic = BattleStatusSemanticTable.GetSemantic(
                effectDefinition.StatusId
            );
            if (
                !BattleStatusSemanticTable.IsHarmfulStatus(effectDefinition.StatusId)
                || semantic.MoveCostDelta <= 0
                || effectDefinition.DurationTu <= 0
            )
            {
                continue;
            }
            StringName statusId = ProgressionDataUtils.to_string_name(
                effectDefinition.StatusId
            );
            int moveCostDelta =
                semantic.MoveCostDelta * Math.Max(effectDefinition.Power, 1);
            if (
                !candidateMoveCostDeltaByStatus.TryGetValue(
                    statusId,
                    out int currentCandidateDelta
                )
                || moveCostDelta > currentCandidateDelta
            )
            {
                candidateMoveCostDeltaByStatus[statusId] = moveCostDelta;
            }
        }
        if (candidateMoveCostDeltaByStatus.Count <= 0)
        {
            return;
        }
        int existingMoveCostDelta = 0;
        foreach (StringName statusId in actor.GetSortedStatusEffectIdsTyped())
        {
            existingMoveCostDelta += BattleStatusSemanticTable.GetMoveCostDelta(
                actor.GetStatusEffect(statusId)
            );
        }
        int projectedMoveCostDelta = existingMoveCostDelta;
        foreach (
            KeyValuePair<StringName, int> candidateEntry
                in candidateMoveCostDeltaByStatus
        )
        {
            int existingSameStatusDelta = BattleStatusSemanticTable.GetMoveCostDelta(
                actor.GetStatusEffect(candidateEntry.Key)
            );
            projectedMoveCostDelta += Math.Max(
                candidateEntry.Value - existingSameStatusDelta,
                0
            );
        }
        int movePoints = Math.Max(actor.GetCurrentMovePoints(), 0);
        int reachableBefore = movePoints / Math.Max(existingMoveCostDelta + 1, 1);
        int reachableAfter = movePoints / Math.Max(projectedMoveCostDelta + 1, 1);
        int lostReachableCells = Math.Max(reachableBefore - reachableAfter, 0);
        scoreInput.resource_cost_score +=
            lostReachableCells * _scoreProfile.MovementCostWeight;
    }
}
