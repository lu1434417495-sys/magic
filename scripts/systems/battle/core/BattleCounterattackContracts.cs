using System;
using Godot;

internal enum BattleCounterattackTriggerKind
{
    MeleeHitReceived = 0,
    MeleeAttackEvaded,
}

internal static class BattleCounterattackTriggerNames
{
    internal static StringName ToStringName(
        BattleCounterattackTriggerKind value
    ) => value switch
    {
        BattleCounterattackTriggerKind.MeleeHitReceived =>
            new StringName("melee_hit_received"),
        BattleCounterattackTriggerKind.MeleeAttackEvaded =>
            new StringName("melee_attack_evaded"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            null
        ),
    };

    internal static bool TryParse(
        StringName value,
        out BattleCounterattackTriggerKind result
    )
    {
        if (value == new StringName("melee_hit_received"))
        {
            result = BattleCounterattackTriggerKind.MeleeHitReceived;
            return true;
        }
        if (value == new StringName("melee_attack_evaded"))
        {
            result = BattleCounterattackTriggerKind.MeleeAttackEvaded;
            return true;
        }
        result = default;
        return false;
    }
}

internal readonly record struct BattleCounterattackCapability(
    StringName InstanceId,
    BattleCounterattackTriggerKind TriggerKind,
    int SelectionPriority,
    int ChancePercent,
    int AttackRollBonus,
    StringName WeaponActionDefinitionId
);

internal readonly record struct BattleCounterattackDedupeKey(
    BattleAttackActionId ActionId,
    StringName AttackerUnitId,
    StringName DefenderUnitId
);

internal readonly record struct BattleCounterattackQueueEntry(
    BattleAttackResolutionFact Fact,
    BattleCounterattackCapability Capability
);

internal enum BattleImmediateWeaponAttackBlockReason
{
    None = 0,
    AttackUnavailable,
    OutOfReach,
    BarrierBlocked,
    InsufficientStamina,
}

internal enum BattleImmediateWeaponAttackCheckState
{
    NotEvaluated = 0,
    Passed,
    Failed,
}

internal readonly record struct BattleImmediateWeaponAttackAvailability(
    bool IsAllowed,
    BattleImmediateWeaponAttackBlockReason Reason,
    BattleImmediateWeaponAttackCheckState DefinitionCheck,
    BattleImmediateWeaponAttackCheckState RangeCheck,
    BattleImmediateWeaponAttackCheckState BarrierCheck,
    BattleImmediateWeaponAttackCheckState StaminaCheck,
    int EffectiveRange,
    int CurrentDistance,
    int StaminaCost,
    int CurrentStamina
)
{
    internal static BattleImmediateWeaponAttackAvailability Blocked(
        BattleImmediateWeaponAttackBlockReason reason,
        int effectiveRange = -1,
        int currentDistance = -1,
        int staminaCost = -1,
        int currentStamina = -1
    ) => reason switch
    {
        BattleImmediateWeaponAttackBlockReason.AttackUnavailable =>
            new(
                false,
                reason,
                BattleImmediateWeaponAttackCheckState.Failed,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                effectiveRange,
                currentDistance,
                staminaCost,
                currentStamina
            ),
        BattleImmediateWeaponAttackBlockReason.OutOfReach =>
            new(
                false,
                reason,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Failed,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                effectiveRange,
                currentDistance,
                staminaCost,
                currentStamina
            ),
        BattleImmediateWeaponAttackBlockReason.BarrierBlocked =>
            new(
                false,
                reason,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Failed,
                BattleImmediateWeaponAttackCheckState.NotEvaluated,
                effectiveRange,
                currentDistance,
                staminaCost,
                currentStamina
            ),
        BattleImmediateWeaponAttackBlockReason.InsufficientStamina =>
            new(
                false,
                reason,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Passed,
                BattleImmediateWeaponAttackCheckState.Failed,
                effectiveRange,
                currentDistance,
                staminaCost,
                currentStamina
            ),
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            null
        ),
    };

    internal static BattleImmediateWeaponAttackAvailability Allowed(
        int effectiveRange,
        int currentDistance,
        int staminaCost,
        int currentStamina
    ) =>
        new(
            true,
            BattleImmediateWeaponAttackBlockReason.None,
            BattleImmediateWeaponAttackCheckState.Passed,
            BattleImmediateWeaponAttackCheckState.Passed,
            BattleImmediateWeaponAttackCheckState.Passed,
            BattleImmediateWeaponAttackCheckState.Passed,
            effectiveRange,
            currentDistance,
            staminaCost,
            currentStamina
        );
}

internal enum BattleCounterattackBlockReason
{
    None = 0,
    InvalidFact,
    CapabilityGone,
    DefenderDown,
    AttackerGone,
    NotHostile,
    TriggerMismatch,
    CounterattackLocked,
    HardControlled,
    NoReactionCharge,
    AttackUnavailable,
    OutOfReach,
    BarrierBlocked,
    InsufficientStamina,
}

internal readonly record struct BattleCounterattackEligibility(
    bool IsAllowed,
    BattleCounterattackBlockReason Reason
)
{
    internal static BattleCounterattackEligibility Allowed =>
        new(true, BattleCounterattackBlockReason.None);

    internal static BattleCounterattackEligibility Blocked(
        BattleCounterattackBlockReason reason
    ) =>
        new(false, reason);
}

internal readonly record struct BattleCounterattackActorPairFacts(
    bool DefenderPresent,
    bool DefenderAliveAndHpPositive,
    bool AttackerPresent,
    bool AttackerAliveAndHpPositive,
    bool HostilePair
);

internal readonly record struct BattleCounterattackAttemptReadinessFacts(
    bool CounterattackLocked,
    bool HardControlled,
    bool HasReactionCharge,
    BattleImmediateWeaponAttackAvailability AttackAvailability
);

internal readonly record struct BattleCounterattackEvaluationContext(
    BattleAttackResolutionFact Fact,
    BattleCounterattackCapability Capability,
    bool CapabilityStillPresent,
    bool DefenderPresent,
    bool DefenderAliveAndHpPositive,
    bool AttackerPresent,
    bool AttackerAliveAndHpPositive,
    bool HostilePair,
    bool CounterattackLocked,
    bool HardControlled,
    bool HasReactionCharge,
    BattleImmediateWeaponAttackAvailability AttackAvailability
);

internal sealed class BattleCounterattackQueryResult
{
    internal BattleCounterattackQueryResult(
        BattleCounterattackEvaluationContext evaluation,
        BattleImmediateWeaponAttackPlan plan
    )
    {
        Evaluation = evaluation;
        Plan = plan;
    }

    internal BattleCounterattackEvaluationContext Evaluation { get; }
    internal BattleImmediateWeaponAttackPlan Plan { get; }

    internal BattleImmediateWeaponAttackPlan RequireExecutablePlan()
    {
        if (Plan == null || !Plan.DefinitionAvailable)
        {
            throw new InvalidOperationException(
                "allowed counterattack must carry an executable plan"
            );
        }
        return Plan;
    }
}
