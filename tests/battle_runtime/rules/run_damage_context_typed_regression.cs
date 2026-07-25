using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_damage_context_typed_regression : LifecycleTestSceneTree
{
    private sealed class ProbeEquipmentPorts
        : IBattleEquipmentDamageQuery,
          IBattleEquipmentCombatReactionSink
    {
        internal BattleState LastDamageQueryState { get; private set; }
        internal BattleState LastReactionState { get; private set; }
        internal int DamageAppliedCount { get; private set; }

        public IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult>
            CollectBonusDamageDiceOnHit(BattleEquipmentAbilityBonusDamageDiceContext context)
        {
            LastDamageQueryState = context?.BattleState;
            return Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>();
        }

        public StringName ResolveDamageRollModeOverride(
            BattleEquipmentAbilityDamageRollModeContext context
        )
        {
            LastDamageQueryState = context?.BattleState;
            return context?.CurrentRollMode ?? new StringName("");
        }

        public IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
            BattleEquipmentAbilityDamageReductionContext context
        )
        {
            LastDamageQueryState = context?.BattleState;
            return Array.Empty<BattleEquipmentAbilityDamageReductionResult>();
        }

        public bool ResolveAttackCheck(BattleEquipmentAbilityAttackCheckContext context) => false;

        public BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
            BattleEquipmentAbilityAfterHitContext context
        ) => new();

        public BattleEquipmentAbilityAfterHitResult ResolveHitReceived(
            BattleEquipmentAbilityAfterHitContext context
        ) => new();

        public IReadOnlyList<StringName> RefreshEquipmentProjectionAfterDurabilityDestruction(
            BattleUnitState targetUnit,
            BattleEventBatch batch = null
        ) => Array.Empty<StringName>();

        public bool ResolveDamageApplied(BattleEquipmentAbilityDamageAppliedContext context)
        {
            DamageAppliedCount++;
            LastReactionState = context?.BattleState;
            return false;
        }
    }

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPartialDictionaryContextIsRejectedAtBoundary();
        TestTypedContextDrivesCriticalDamageAndVirtualDice();
        TestFixedMitigationSourcesRemainStructured();
        TestEquipmentPortsReceiveExplicitBattleState();

        RequestTestExit(_test.Finish("Damage context typed regression"));
    }

    private void TestEquipmentPortsReceiveExplicitBattleState()
    {
        using var resolver = new BattleDamageResolver();
        var ports = new ProbeEquipmentPorts();
        resolver.SetEquipmentAbilityPorts(ports, ports);
        var state = new BattleState();
        BattleUnitState source = BuildUnit("equipment_port_source");
        BattleUnitState target = BuildUnit("equipment_port_target");
        try
        {
            resolver.ResolveEffects(
                source,
                target,
                new[] { BuildDamageEffect(power: 5, damageTag: "physical_slash") },
                DamageResolutionContext
                    .Create(
                        criticalHit: false,
                        attackSuccess: true,
                        secondaryHitSuccess: false
                    )
                    .WithBattleState(state)
            );

            _test.True(
                ReferenceEquals(ports.LastDamageQueryState, state),
                "damage query should receive the explicitly supplied battle state."
            );
            _test.True(
                ReferenceEquals(ports.LastReactionState, state),
                "damage reaction should receive the explicitly supplied battle state."
            );
            _test.Eq(
                ports.DamageAppliedCount,
                1,
                "damage reaction should remain synchronous with committed hp damage."
            );

            int directDamage = resolver.ApplyTaggedDirectDamageToTargetTyped(
                target,
                3,
                "physical_slash",
                source,
                state
            );
            _test.True(directDamage > 0, "tagged direct damage fixture should commit hp damage.");
            _test.Eq(
                ports.DamageAppliedCount,
                2,
                "tagged direct damage should synchronously dispatch through the reaction sink."
            );
            _test.True(
                ReferenceEquals(ports.LastReactionState, state),
                "tagged direct damage should forward its explicit battle state."
            );
        }
        finally
        {
            resolver.SetEquipmentAbilityPorts(null, null);
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(target);
        }
    }

    private void TestPartialDictionaryContextIsRejectedAtBoundary()
    {
        using var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("source");
        BattleUnitState target = BuildUnit("target");
        try
        {
            ExpectArgumentException(
                () => DamageResolutionContext.FromDictionary(
                    new GDictionary { ["secondary_hit_success"] = true }
                ),
                "dictionary damage_context with secondary_hit_success but missing attack metadata must be rejected."
            );

            DamageResolutionContext context = DamageResolutionContext.FromDictionary(
                new GDictionary { ["attack_success"] = true }
            );
            _test.True(context.AttackSuccess, "attack_success-only damage_context should remain a valid effect gate.");
            _test.False(context.CriticalHit, "attack_success-only damage_context should not imply critical_hit.");
            _test.False(
                context.SecondaryHitSuccess,
                "attack_success-only damage_context should not imply secondary_hit_success."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(target);
        }
    }

    private void TestTypedContextDrivesCriticalDamageAndVirtualDice()
    {
        using var resolver = new FixedRollDamageResolver();
        BattleUnitState source = BuildUnit("typed_source");
        BattleUnitState target = BuildUnit("typed_target");
        try
        {
            DamageResolutionContext context = DamageResolutionContext.Create(
                criticalHit: true,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: "typed_damage"
            );

            AttackEffectResolutionResult result = resolver.ResolveEffects(
                source,
                target,
                new[] { BuildDamageEffect(power: 0, diceCount: 1, diceSides: 6) },
                context
            );

            _test.True(resolver.RollCalls > 0, "typed damage context path should use virtual dice dispatch.");
            _test.True(result.HasDamageEvent, "typed damage context should produce a damage event.");
            _test.True(result.DamageEvents[0].CriticalHit, "typed context critical_hit should drive critical damage metadata.");
            _test.Eq(result.DamageEvents[0].DamageDice.Rolls[0], 6, "virtual dice override should control damage die roll.");
            _test.Eq(result.SkillId, new StringName("typed_damage"), "typed context should preserve skill_id.");
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(target);
        }
    }

    private void TestFixedMitigationSourcesRemainStructured()
    {
        using var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("mitigation_source");
        BattleUnitState target = BuildUnit("mitigation_target");
        try
        {
            target.SetStatusEffect(new BattleStatusEffectState
            {
                status_id = "guarding",
                power = 2,
                stacks = 1,
            });
            target.SetStatusEffect(new BattleStatusEffectState
            {
                status_id = "passive_reduction",
                power = 1,
                stacks = 1,
                passive_reduction = 3,
            });

            AttackEffectResolutionResult result = resolver.ResolveEffects(
                source,
                target,
                new[] { BuildDamageEffect(power: 10, damageTag: "physical_slash") },
                DamageResolutionContext.Create(
                    criticalHit: false,
                    attackSuccess: true,
                    secondaryHitSuccess: false,
                    skillId: "mitigation_test"
                )
            );

            _test.True(result.HasDamageEvent, "mitigation test should produce a damage event.");
            _test.Eq(result.DamageEvents[0].StanceReduction, 2, "guarding should contribute stance reduction.");
            _test.Eq(result.DamageEvents[0].PassiveReduction, 3, "typed passive status should contribute passive reduction.");
            _test.Eq(result.DamageEvents[0].FixedMitigationTotal, 5, "fixed mitigation total should preserve typed source totals.");
            _test.True(
                Array.IndexOf(result.DamageEvents[0].FixedMitigationSourceLabels, "guarding") >= 0,
                "fixed mitigation labels should preserve guarding source."
            );
            _test.True(
                Array.IndexOf(result.DamageEvents[0].FixedMitigationSourceLabels, "passive_reduction") >= 0,
                "fixed mitigation labels should preserve passive source."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(target);
        }
    }

    private BattleUnitState BuildUnit(string unitId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId,
            faction_id = unitId.Contains("source") ? "heroes" : "enemies",
        }.WithCombatResourcesForTest(
            hp: 50
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 50);
        return unit;
    }

    private CombatEffectDefinition BuildDamageEffect(
        int power,
        int diceCount = 0,
        int diceSides = 0,
        StringName damageTag = default
    )
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: damageTag == default ? new StringName("force") : damageTag,
            power: power,
            diceCount: diceCount,
            diceSides: diceSides
        );
    }

    private void ExpectArgumentException(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (Exception ex)
        {
            _test.Fail($"{message} | wrong exception={ex.GetType().Name}: {ex.Message}");
            return;
        }

        _test.Fail(message);
    }

    private sealed class FixedRollDamageResolver : BattleDamageResolver
    {
        public int RollCalls { get; private set; }

        public override int _roll_damage_die(int dice_sides)
        {
            RollCalls++;
            return dice_sides;
        }
    }
}
