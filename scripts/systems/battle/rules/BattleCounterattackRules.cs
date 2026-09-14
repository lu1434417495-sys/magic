using Godot;

internal static class BattleCounterattackRules
{
    internal static bool TryMapTrigger(
        in BattleAttackResolutionFact fact,
        out BattleCounterattackTriggerKind triggerKind
    )
    {
        if (
            !fact.IncludesWeaponDamage
            || fact.DeliveryKind
                != BattleAttackDeliveryKind.MeleeWeapon
        )
        {
            triggerKind = default;
            return false;
        }
        triggerKind = fact.AttackSucceeded
            ? BattleCounterattackTriggerKind.MeleeHitReceived
            : BattleCounterattackTriggerKind.MeleeAttackEvaded;
        return true;
    }

    internal static BattleCounterattackEligibility Evaluate(
        in BattleCounterattackEvaluationContext context
    )
    {
        BattleAttackResolutionFact fact = context.Fact;
        if (
            !fact.ActionId.IsValid
            || fact.RootBoundaryId <= 0
            || fact.Origin == null
            || !fact.Origin.CanTriggerReactions
            || fact.AttackerUnitId == new StringName("")
            || fact.DefenderUnitId == new StringName("")
        )
        {
            return Blocked(
                BattleCounterattackBlockReason.InvalidFact
            );
        }
        if (!context.CapabilityStillPresent)
        {
            return Blocked(
                BattleCounterattackBlockReason.CapabilityGone
            );
        }
        BattleCounterattackEligibility actorPair =
            EvaluateActorPair(
                new BattleCounterattackActorPairFacts(
                    context.DefenderPresent,
                    context.DefenderAliveAndHpPositive,
                    context.AttackerPresent,
                    context.AttackerAliveAndHpPositive,
                    context.HostilePair
                )
            );
        if (!actorPair.IsAllowed)
            return actorPair;
        if (
            !TryMapTrigger(
                fact,
                out BattleCounterattackTriggerKind triggerKind
            )
            || triggerKind != context.Capability.TriggerKind
        )
        {
            return Blocked(
                BattleCounterattackBlockReason.TriggerMismatch
            );
        }
        return EvaluateAttemptReadiness(
            new BattleCounterattackAttemptReadinessFacts(
                context.CounterattackLocked,
                context.HardControlled,
                context.HasReactionCharge,
                context.AttackAvailability
            )
        );
    }

    internal static BattleCounterattackEligibility EvaluateActorPair(
        in BattleCounterattackActorPairFacts facts
    )
    {
        if (
            !facts.DefenderPresent
            || !facts.DefenderAliveAndHpPositive
        )
        {
            return Blocked(
                BattleCounterattackBlockReason.DefenderDown
            );
        }
        if (
            !facts.AttackerPresent
            || !facts.AttackerAliveAndHpPositive
        )
        {
            return Blocked(
                BattleCounterattackBlockReason.AttackerGone
            );
        }
        if (!facts.HostilePair)
        {
            return Blocked(
                BattleCounterattackBlockReason.NotHostile
            );
        }
        return BattleCounterattackEligibility.Allowed;
    }

    internal static BattleCounterattackEligibility
        EvaluateAttemptReadiness(
            in BattleCounterattackAttemptReadinessFacts facts
        )
    {
        if (facts.CounterattackLocked)
        {
            return Blocked(
                BattleCounterattackBlockReason.CounterattackLocked
            );
        }
        if (facts.HardControlled)
        {
            return Blocked(
                BattleCounterattackBlockReason.HardControlled
            );
        }
        if (!facts.HasReactionCharge)
        {
            return Blocked(
                BattleCounterattackBlockReason.NoReactionCharge
            );
        }

        return facts.AttackAvailability.Reason switch
        {
            BattleImmediateWeaponAttackBlockReason.None
                when facts.AttackAvailability.IsAllowed =>
                    BattleCounterattackEligibility.Allowed,
            BattleImmediateWeaponAttackBlockReason.AttackUnavailable =>
                Blocked(
                    BattleCounterattackBlockReason.AttackUnavailable
                ),
            BattleImmediateWeaponAttackBlockReason.OutOfReach =>
                Blocked(
                    BattleCounterattackBlockReason.OutOfReach
                ),
            BattleImmediateWeaponAttackBlockReason.BarrierBlocked =>
                Blocked(
                    BattleCounterattackBlockReason.BarrierBlocked
                ),
            BattleImmediateWeaponAttackBlockReason
                .InsufficientStamina =>
                    Blocked(
                        BattleCounterattackBlockReason
                            .InsufficientStamina
                    ),
            _ => Blocked(
                BattleCounterattackBlockReason.AttackUnavailable
            ),
        };
    }

    private static BattleCounterattackEligibility Blocked(
        BattleCounterattackBlockReason reason
    ) =>
        BattleCounterattackEligibility.Blocked(reason);
}
