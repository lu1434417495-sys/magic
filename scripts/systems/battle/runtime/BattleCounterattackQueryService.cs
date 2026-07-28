using System;
using Godot;

internal sealed class BattleCounterattackQueryService
    : BattleRuntimeModuleBorrower
{
    private readonly BattleImmediateWeaponAttackService
        _immediateWeaponAttackService;

    internal BattleCounterattackQueryService(
        BattleImmediateWeaponAttackService immediateWeaponAttackService
    )
    {
        _immediateWeaponAttackService =
            immediateWeaponAttackService
            ?? throw new ArgumentNullException(
                nameof(immediateWeaponAttackService)
            );
    }

    internal BattleCounterattackQueryResult Build(
        in BattleCounterattackQueueEntry entry
    )
    {
        BattleRuntimeModule runtime = _runtime
            ?? throw new InvalidOperationException(
                "counterattack query service is not bound"
            );
        BattleState state = runtime.GetState();
        BattleUnitState defender = null;
        BattleUnitState attacker = null;
        bool defenderPresent =
            state != null
            && state.TryGetUnitTyped(
                entry.Fact.DefenderUnitId,
                out defender
            )
            && defender != null;
        bool attackerPresent =
            state != null
            && state.TryGetUnitTyped(
                entry.Fact.AttackerUnitId,
                out attacker
            )
            && attacker != null;

        bool capabilityStillPresent =
            defenderPresent
            && defender.TryGetCounterattackCapabilityTyped(
                entry.Capability.InstanceId,
                out BattleCounterattackCapability currentCapability
            )
            && currentCapability.Equals(entry.Capability);
        BattleCounterattackActorPairFacts actorPair =
            BuildActorPairFacts(defender, attacker);

        BattleImmediateWeaponAttackPlan plan = null;
        BattleImmediateWeaponAttackAvailability availability =
            BattleImmediateWeaponAttackAvailability.Blocked(
                BattleImmediateWeaponAttackBlockReason.AttackUnavailable
            );
        if (defenderPresent && attackerPresent)
        {
            plan = _immediateWeaponAttackService.PrepareCounterattack(
                new BattleCounterattackImmediateWeaponAttackRequest(
                    state,
                    defender,
                    attacker,
                    entry.Capability
                )
            );
            availability =
                _immediateWeaponAttackService.Query(plan);
        }
        BattleCounterattackAttemptReadinessFacts readiness =
            BuildAttemptReadinessFacts(defender, availability);

        return new BattleCounterattackQueryResult(
            new BattleCounterattackEvaluationContext(
                entry.Fact,
                entry.Capability,
                capabilityStillPresent,
                actorPair.DefenderPresent,
                actorPair.DefenderAliveAndHpPositive,
                actorPair.AttackerPresent,
                actorPair.AttackerAliveAndHpPositive,
                actorPair.HostilePair,
                readiness.CounterattackLocked,
                readiness.HardControlled,
                readiness.HasReactionCharge,
                readiness.AttackAvailability
            ),
            plan
        );
    }

    internal BattleCounterattackActorPairFacts BuildActorPairFacts(
        BattleUnitState defender,
        BattleUnitState attacker
    )
    {
        bool defenderPresent = defender != null;
        bool attackerPresent = attacker != null;
        return new BattleCounterattackActorPairFacts(
            defenderPresent,
            defenderPresent
                && defender.IsAlive()
                && defender.GetCurrentHp() > 0,
            attackerPresent,
            attackerPresent
                && attacker.IsAlive()
                && attacker.GetCurrentHp() > 0,
            defenderPresent
                && attackerPresent
                && defender.unit_id != attacker.unit_id
                && defender.faction_id != new StringName("")
                && attacker.faction_id != new StringName("")
                && defender.faction_id != attacker.faction_id
        );
    }

    internal BattleCounterattackAttemptReadinessFacts
        BuildAttemptReadinessFacts(
            BattleUnitState defender,
            in BattleImmediateWeaponAttackAvailability availability
        )
    {
        BattleRuntimeModule runtime = _runtime
            ?? throw new InvalidOperationException(
                "counterattack query service is not bound"
            );
        bool defenderPresent = defender != null;
        bool hardControlled =
            defenderPresent
            && (
                BattleStatusSemanticTable.IsHardControlled(defender)
                || (
                    defender.GetStatusEffect(
                        BattleStatusSemanticTable.STATUS_TIME_STASIS
                    ) is BattleStatusEffectState stasis
                    && stasis.stacks > 0
                )
            );
        return new BattleCounterattackAttemptReadinessFacts(
            defenderPresent
                && runtime.IsUnitCounterattackLocked(defender),
            hardControlled,
            defenderPresent
                && defender.HasReactionChargeTyped(),
            availability
        );
    }
}
