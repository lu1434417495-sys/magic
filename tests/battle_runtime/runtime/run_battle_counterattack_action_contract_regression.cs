using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_counterattack_action_contract_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDeliveryClassification();
        TestActionContextRejectsInvalidIdentity();
        TestOriginReactionPolicy();
        TestEligibilityFailClosedOrdering();
        RequestTestExit(
            _test.Finish("Counterattack action contract regression")
        );
    }

    private void TestDeliveryClassification()
    {
        IReadOnlyList<CombatEffectDefinition> weaponEffects =
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    addWeaponDice: true
                ),
            };
        IReadOnlyList<CombatEffectDefinition> nonWeaponEffects =
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect("damage"),
            };

        _test.Eq(
            BattleAttackDeliveryRules.Resolve(
                weaponEffects,
                MakeProjection("melee", 1)
            ),
            BattleAttackDeliveryKind.MeleeWeapon,
            "melee weapon projection must produce melee delivery."
        );
        _test.Eq(
            BattleAttackDeliveryRules.Resolve(
                weaponEffects,
                MakeProjection("ranged", 6)
            ),
            BattleAttackDeliveryKind.RangedWeapon,
            "ranged weapon projection must produce ranged delivery."
        );
        _test.Eq(
            BattleAttackDeliveryRules.Resolve(
                nonWeaponEffects,
                BattleUnitWeaponProjectionReadView.MissingOwner
            ),
            BattleAttackDeliveryKind.NonWeapon,
            "non-weapon effects must not require a weapon projection."
        );
        _test.Eq(
            BattleAttackDeliveryRules.Resolve(
                weaponEffects,
                BattleUnitWeaponProjectionReadView.MissingOwner
            ),
            BattleAttackDeliveryKind.Unknown,
            "weapon effects with no projection owner must fail closed."
        );
        _test.Eq(
            BattleAttackDeliveryRules.Resolve(
                weaponEffects,
                MakeProjection("unsupported", 1)
            ),
            BattleAttackDeliveryKind.Unknown,
            "unknown range types must fail closed."
        );
    }

    private void TestActionContextRejectsInvalidIdentity()
    {
        _test.True(
            Throws<ArgumentException>(
                () =>
                    new BattleAttackActionContext(
                        default,
                        1,
                        BattleEffectOrigin.PlayerCommand(),
                        BattleAttackDeliveryKind.MeleeWeapon
                    )
            ),
            "logical attack context must reject an invalid action id."
        );
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () =>
                    new BattleAttackActionContext(
                        new BattleAttackActionId(1),
                        0,
                        BattleEffectOrigin.PlayerCommand(),
                        BattleAttackDeliveryKind.MeleeWeapon
                    )
            ),
            "logical attack context must reject a missing root boundary."
        );
        _test.True(
            Throws<ArgumentException>(
                () =>
                    new BattleAttackActionContext(
                        new BattleAttackActionId(1),
                        1,
                        BattleEffectOrigin.PlayerCommand(),
                        BattleAttackDeliveryKind.Unknown
                    )
            ),
            "logical attack context must reject unknown delivery."
        );
    }

    private void TestOriginReactionPolicy()
    {
        BattleEffectOrigin command = BattleEffectOrigin.PlayerCommand();
        BattleEffectOrigin timeline =
            BattleEffectOrigin.Timeline("timeline_tick");
        BattleEffectOrigin counterattack =
            BattleEffectOrigin.Counterattack(
                new BattleAttackActionId(7),
                "fixture_counter"
            );

        _test.True(
            command.CanTriggerReactions,
            "player commands must allow reaction capture."
        );
        _test.True(
            timeline.CanTriggerReactions,
            "timeline attack segments must allow reaction capture."
        );
        _test.False(
            counterattack.CanTriggerReactions,
            "counterattacks must not directly trigger another counterattack."
        );
        _test.Eq(
            counterattack.TriggeringAttackActionId,
            7L,
            "counterattack origin must retain its triggering action id."
        );
    }

    private void TestEligibilityFailClosedOrdering()
    {
        BattleAttackResolutionFact validFact = new(
            new BattleAttackActionId(1),
            1,
            "attacker",
            "defender",
            Vector2I.Zero,
            new Vector2I(1, 0),
            BattleAttackDeliveryKind.MeleeWeapon,
            AttackSucceeded: true,
            CriticalHit: false,
            IncludesWeaponDamage: true,
            BattleEffectOrigin.PlayerCommand()
        );
        BattleCounterattackCapability capability = new(
            "capability",
            BattleCounterattackTriggerKind
                .MeleeHitReceived,
            SelectionPriority: 1,
            ChancePercent: 100,
            AttackRollBonus: 0,
            WeaponActionDefinitionId: "counter_action"
        );
        BattleCounterattackEvaluationContext allowed = new(
            validFact,
            capability,
            CapabilityStillPresent: true,
            DefenderPresent: true,
            DefenderAliveAndHpPositive: true,
            AttackerPresent: true,
            AttackerAliveAndHpPositive: true,
            HostilePair: true,
            CounterattackLocked: false,
            HardControlled: false,
            HasReactionCharge: true,
            BattleImmediateWeaponAttackAvailability.Allowed(
                effectiveRange: 1,
                currentDistance: 1,
                staminaCost: 2,
                currentStamina: 10
            )
        );

        AssertBlockReason(
            allowed with
            {
                Fact = validFact with
                {
                    RootBoundaryId = 0,
                },
            },
            BattleCounterattackBlockReason.InvalidFact
        );
        AssertBlockReason(
            allowed with
            {
                CapabilityStillPresent = false,
                DefenderPresent = false,
            },
            BattleCounterattackBlockReason.CapabilityGone
        );
        AssertBlockReason(
            allowed with
            {
                DefenderAliveAndHpPositive = false,
            },
            BattleCounterattackBlockReason.DefenderDown
        );
        AssertBlockReason(
            allowed with
            {
                AttackerPresent = false,
            },
            BattleCounterattackBlockReason.AttackerGone
        );
        AssertBlockReason(
            allowed with { HostilePair = false },
            BattleCounterattackBlockReason.NotHostile
        );
        AssertBlockReason(
            allowed with
            {
                Capability = capability with
                {
                    TriggerKind =
                        BattleCounterattackTriggerKind
                            .MeleeAttackEvaded,
                },
            },
            BattleCounterattackBlockReason.TriggerMismatch
        );
        AssertBlockReason(
            allowed with
            {
                CounterattackLocked = true,
                HardControlled = true,
                HasReactionCharge = false,
            },
            BattleCounterattackBlockReason
                .CounterattackLocked
        );
        AssertBlockReason(
            allowed with
            {
                HardControlled = true,
                HasReactionCharge = false,
            },
            BattleCounterattackBlockReason.HardControlled
        );
        AssertBlockReason(
            allowed with
            {
                HasReactionCharge = false,
            },
            BattleCounterattackBlockReason.NoReactionCharge
        );
        foreach (
            (
                BattleImmediateWeaponAttackBlockReason
                    availabilityReason,
                BattleCounterattackBlockReason expectedReason
            ) pair in
            new[]
            {
                (
                    BattleImmediateWeaponAttackBlockReason
                        .AttackUnavailable,
                    BattleCounterattackBlockReason
                        .AttackUnavailable
                ),
                (
                    BattleImmediateWeaponAttackBlockReason
                        .OutOfReach,
                    BattleCounterattackBlockReason.OutOfReach
                ),
                (
                    BattleImmediateWeaponAttackBlockReason
                        .BarrierBlocked,
                    BattleCounterattackBlockReason
                        .BarrierBlocked
                ),
                (
                    BattleImmediateWeaponAttackBlockReason
                        .InsufficientStamina,
                    BattleCounterattackBlockReason
                        .InsufficientStamina
                ),
            }
        )
        {
            BattleImmediateWeaponAttackAvailability
                availability =
                    BattleImmediateWeaponAttackAvailability
                        .Blocked(pair.availabilityReason);
            AssertBlockReason(
                allowed with
                {
                    AttackAvailability = availability,
                },
                pair.expectedReason
            );
            _test.Eq(
                availability.DefinitionCheck,
                pair.availabilityReason
                    == BattleImmediateWeaponAttackBlockReason
                        .AttackUnavailable
                    ? BattleImmediateWeaponAttackCheckState
                        .Failed
                    : BattleImmediateWeaponAttackCheckState
                        .Passed,
                "availability must distinguish failed from not-evaluated checks."
            );
        }
        _test.True(
            BattleCounterattackRules.Evaluate(allowed)
                .IsAllowed,
            "fully valid eligibility facts must be allowed."
        );
    }

    private void AssertBlockReason(
        in BattleCounterattackEvaluationContext context,
        BattleCounterattackBlockReason expected
    )
    {
        BattleCounterattackEligibility eligibility =
            BattleCounterattackRules.Evaluate(context);
        _test.False(
            eligibility.IsAllowed,
            $"{expected} fixture must be blocked."
        );
        _test.Eq(
            eligibility.Reason,
            expected,
            "eligibility must preserve the fixed fail-closed ordering."
        );
    }

    private static BattleUnitWeaponProjectionReadView MakeProjection(
        StringName rangeType,
        int attackRange
    ) =>
        BattleUnitWeaponProjectionReadView.Present(
            new BattleWeaponProjectionValues(
                "equipped",
                "fixture_weapon",
                "fixture_profile",
                rangeType,
                "fixture_family",
                "one_handed",
                attackRange,
                BattleWeaponDiceValues.PresentEmpty,
                BattleWeaponDiceValues.PresentEmpty,
                false,
                false,
                "physical"
            )
        );

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
