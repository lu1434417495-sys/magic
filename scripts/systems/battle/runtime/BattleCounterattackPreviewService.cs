using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleCounterattackPreviewService
    : BattleRuntimeModuleBorrower
{
    private readonly BattleCounterattackQueryService _queryService;
    private readonly BattleImmediateWeaponAttackService
        _immediateWeaponAttackService;

    internal BattleCounterattackPreviewService(
        BattleCounterattackQueryService queryService,
        BattleImmediateWeaponAttackService immediateWeaponAttackService
    )
    {
        _queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        _immediateWeaponAttackService =
            immediateWeaponAttackService
            ?? throw new ArgumentNullException(
                nameof(immediateWeaponAttackService)
            );
    }

    internal BattleCounterattackRiskProjection Build(
        BattleState state,
        StringName originalAttackerUnitId,
        BattleCounterattackRiskCoverage coverage,
        IReadOnlyList<BattleCounterattackPreviewTarget> targets
    )
    {
        BattleRuntimeModule runtime = _runtime
            ?? throw new InvalidOperationException(
                "counterattack preview service is not bound"
            );
        if (!ReferenceEquals(state, runtime.GetState()))
        {
            throw new InvalidOperationException(
                "counterattack preview belongs to another battle"
            );
        }
        if (
            coverage
                == BattleCounterattackRiskCoverage.NotEvaluated
        )
        {
            throw new ArgumentException(
                "preview service requires an evaluated coverage",
                nameof(coverage)
            );
        }
        if (coverage != BattleCounterattackRiskCoverage.Complete)
            return BattleCounterattackRiskProjection.Empty(coverage);
        if (
            !state.TryGetUnitTyped(
                originalAttackerUnitId,
                out BattleUnitState originalAttacker
            )
            || originalAttacker == null
        )
        {
            throw new InvalidOperationException(
                "counterattack preview attacker is not present"
            );
        }

        var seenDefenderIds = new HashSet<StringName>();
        var entries = new List<BattleCounterattackRiskEntry>();
        long aggregateBasisPoints = 0;
        foreach (
            BattleCounterattackPreviewTarget target
                in targets
                    ?? Array.Empty<
                        BattleCounterattackPreviewTarget
                    >()
        )
        {
            if (
                target == null
                || !seenDefenderIds.Add(target.DefenderUnitId)
                || !target.ProducesAttackResolutionFact
                || !target.IncludesWeaponDamage
                || target.DeliveryKind
                    != BattleAttackDeliveryKind.MeleeWeapon
            )
            {
                continue;
            }
            if (
                !state.TryGetUnitTyped(
                    target.DefenderUnitId,
                    out BattleUnitState defender
                )
                || defender == null
            )
            {
                throw new InvalidOperationException(
                    "counterattack preview defender is not present"
                );
            }

            BattleCounterattackEligibility actorPair =
                BattleCounterattackRules.EvaluateActorPair(
                    _queryService.BuildActorPairFacts(
                        defender,
                        originalAttacker
                    )
                );
            BattleCounterattackRiskBranch? onHit = BuildBranch(
                state,
                defender,
                originalAttacker,
                BattleCounterattackTriggerKind.MeleeHitReceived,
                actorPair
            );
            BattleCounterattackRiskBranch? onMiss = BuildBranch(
                state,
                defender,
                originalAttacker,
                BattleCounterattackTriggerKind.MeleeAttackEvaded,
                actorPair
            );
            if (!onHit.HasValue && !onMiss.HasValue)
                continue;

            int chanceBasisPoints =
                ComputePotentialCounterattackChanceBasisPoints(
                    target.StageHitChanceBasisPoints,
                    onHit,
                    onMiss
                );
            entries.Add(
                new BattleCounterattackRiskEntry(
                    defender.unit_id,
                    onHit,
                    onMiss,
                    chanceBasisPoints
                )
            );
            aggregateBasisPoints = checked(
                aggregateBasisPoints + chanceBasisPoints
            );
        }
        return new BattleCounterattackRiskProjection(
            BattleCounterattackRiskCoverage.Complete,
            entries,
            aggregateBasisPoints
        );
    }

    private BattleCounterattackRiskBranch? BuildBranch(
        BattleState state,
        BattleUnitState defender,
        BattleUnitState originalAttacker,
        BattleCounterattackTriggerKind triggerKind,
        BattleCounterattackEligibility actorPair
    )
    {
        IReadOnlyList<BattleCounterattackCapability> candidates =
            defender.GetCounterattackCandidatesTyped(triggerKind);
        if (candidates.Count == 0)
            return null;
        BattleCounterattackCapability capability = candidates[0];
        BattleImmediateWeaponAttackPlan plan =
            _immediateWeaponAttackService.PrepareCounterattack(
                new BattleCounterattackImmediateWeaponAttackRequest(
                    state,
                    defender,
                    originalAttacker,
                    capability
                )
            );
        BattleImmediateWeaponAttackAvailability availability =
            _immediateWeaponAttackService.Query(plan);
        BattleCounterattackEligibility eligibility =
            actorPair.IsAllowed
                ? BattleCounterattackRules
                    .EvaluateAttemptReadiness(
                        _queryService
                            .BuildAttemptReadinessFacts(
                                defender,
                                availability
                            )
                    )
                : actorPair;
        BattleCounterattackDefinitionDamageRange damageRange =
            plan.DefinitionAvailable
                ? BattleCounterattackDefinitionDamageRange.From(
                    BattleDamagePreviewRangeService
                        .BuildSkillDamagePreview(
                            defender,
                            plan.EffectDefinitions
                        )
                )
                : BattleCounterattackDefinitionDamageRange.Empty;
        return new BattleCounterattackRiskBranch(
            triggerKind,
            capability.InstanceId,
            capability.ChancePercent,
            eligibility,
            plan.StaminaCost,
            damageRange
        );
    }

    private static int
        ComputePotentialCounterattackChanceBasisPoints(
            IReadOnlyList<int> stageHitChanceBasisPoints,
            BattleCounterattackRiskBranch? onHit,
            BattleCounterattackRiskBranch? onMiss
        )
    {
        decimal unresolvedProbability = 1m;
        decimal counterattackProbability = 0m;
        foreach (
            int hitChanceBasisPoints
                in stageHitChanceBasisPoints
                    ?? Array.Empty<int>()
        )
        {
            decimal hitProbability =
                Math.Clamp(hitChanceBasisPoints, 0, 10_000)
                / 10_000m;
            decimal missProbability = 1m - hitProbability;
            if (
                onHit.HasValue
                && onHit.Value.Eligibility.IsAllowed
            )
            {
                counterattackProbability +=
                    unresolvedProbability
                    * hitProbability
                    * onHit.Value.CapabilityChancePercent
                    / 100m;
            }
            if (
                onMiss.HasValue
                && onMiss.Value.Eligibility.IsAllowed
            )
            {
                counterattackProbability +=
                    unresolvedProbability
                    * missProbability
                    * onMiss.Value.CapabilityChancePercent
                    / 100m;
            }
            decimal continuationProbability = 0m;
            if (!onHit.HasValue)
                continuationProbability += hitProbability;
            if (!onMiss.HasValue)
                continuationProbability += missProbability;
            unresolvedProbability *= continuationProbability;
            if (unresolvedProbability == 0m)
                break;
        }
        return Math.Clamp(
            decimal.ToInt32(
                decimal.Round(
                    counterattackProbability * 10_000m,
                    0,
                    MidpointRounding.AwayFromZero
                )
            ),
            0,
            10_000
        );
    }
}
