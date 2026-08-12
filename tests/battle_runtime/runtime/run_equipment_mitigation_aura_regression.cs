using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_mitigation_aura_regression : LifecycleTestSceneTree
{
    private static readonly StringName BindingId = "binding.test.mitigation_aura";
    private static readonly StringName AuraId = "test_fire_guard";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestQueryTracksCurrentRangeTeamSourceAndLifeState();
        TestDuplicateHalfAurasApplyOneMitigationTier();

        RequestTestExit(_test.Finish("Equipment mitigation aura regression"));
    }

    private void TestQueryTracksCurrentRangeTeamSourceAndLifeState()
    {
        using AuraFixture fixture = AuraFixture.Create();

        IReadOnlyList<BattleEquipmentAbilityMitigationAuraResult> inRange =
            CollectAuras(fixture, fixture.Protected, "fire");
        _test.Eq(inRange.Count, 2, "two living allied in-range sources should be reported.");
        _test.True(
            ContainsSource(inRange, fixture.HolderA.unit_id),
            "first allied holder should provide the aura."
        );
        _test.True(
            ContainsSource(inRange, fixture.HolderB.unit_id),
            "second allied holder should provide the aura."
        );
        _test.False(
            ContainsSource(inRange, fixture.EnemyHolder.unit_id),
            "an enemy holder must not grant an ally-filtered aura across factions."
        );

        fixture.HolderB.ClearEquipmentAbilityProjectionTyped();
        _test.Eq(
            CollectAuras(fixture, fixture.Protected, "fire").Count,
            1,
            "removing the projected equipment source should remove its aura immediately."
        );
        AuraFixture.AttachAuraSource(fixture.HolderB, "holder_b");

        fixture.Protected.SetAnchorCoord(new Vector2I(6, 6));
        _test.Eq(
            CollectAuras(fixture, fixture.Protected, "fire").Count,
            0,
            "moving outside the authored cell radius should remove the aura immediately."
        );
        fixture.Protected.SetAnchorCoord(new Vector2I(2, 0));

        _test.Eq(
            CollectAuras(fixture, fixture.HolderA, "fire").Count,
            1,
            "the ally target filter should include the living source itself."
        );
        _test.Eq(
            CollectAuras(fixture, fixture.Protected, "force").Count,
            0,
            "an aura should only match its authored damage tag."
        );

        fixture.HolderA.SetCurrentHp(0);
        fixture.HolderB.SetCurrentHp(0);
        _test.Eq(
            CollectAuras(fixture, fixture.Protected, "fire").Count,
            0,
            "dead aura sources must stop contributing even while their projections remain."
        );
    }

    private void TestDuplicateHalfAurasApplyOneMitigationTier()
    {
        using AuraFixture fixture = AuraFixture.Create();
        using var resolver = new BattleDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );

        AttackEffectResolutionResult protectedResult = ResolveDamage(
            resolver,
            fixture,
            "fire"
        );
        _test.Eq(
            protectedResult.Damage,
            5,
            "two half auras should halve 10 fire damage once, not quarter it."
        );
        _test.True(
            protectedResult.HasDamageEvent,
            "the protected hit should retain a canonical damage event."
        );
        _test.Eq(
            protectedResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Half,
            "duplicate half sources should resolve to the single half tier."
        );
        _test.Eq(
            protectedResult.DamageEvents[0].MitigationSources.Length,
            2,
            "both distinct living aura sources should retain provenance."
        );

        fixture.Protected.SetCurrentHp(100);
        fixture.Protected.SetAnchorCoord(new Vector2I(6, 6));
        AttackEffectResolutionResult outsideResult = ResolveDamage(
            resolver,
            fixture,
            "fire"
        );
        _test.Eq(
            outsideResult.Damage,
            10,
            "the same fire hit should return to normal damage outside the radius."
        );
        _test.Eq(
            outsideResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Normal,
            "leaving the aura should restore the normal mitigation tier."
        );

        fixture.Protected.SetCurrentHp(100);
        fixture.Protected.SetAnchorCoord(new Vector2I(2, 0));
        AttackEffectResolutionResult otherTagResult = ResolveDamage(
            resolver,
            fixture,
            "force"
        );
        _test.Eq(
            otherTagResult.Damage,
            10,
            "a fire aura must not alter another damage tag."
        );

        fixture.Protected.SetCurrentHp(100);
        int taggedDirectDamage = resolver.ApplyTaggedDirectDamageToTargetTyped(
            fixture.Protected,
            rawDamage: 10,
            damageTag: "fire",
            sourceUnit: fixture.Attacker,
            battleState: fixture.State
        );
        _test.Eq(
            taggedDirectDamage,
            5,
            "tagged direct damage should use the same explicit-state aura mitigation query."
        );
    }

    private static AttackEffectResolutionResult ResolveDamage(
        BattleDamageResolver resolver,
        AuraFixture fixture,
        StringName damageTag
    )
    {
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: damageTag,
            power: 10
        );
        return resolver.ResolveEffects(
            fixture.Attacker,
            fixture.Protected,
            new[] { damageEffect },
            DamageResolutionContext
                .Create(
                    criticalHit: false,
                    attackSuccess: true,
                    secondaryHitSuccess: false,
                    skillId: "test_mitigation_aura_damage"
                )
                .WithBattleState(fixture.State)
        );
    }

    private static IReadOnlyList<BattleEquipmentAbilityMitigationAuraResult> CollectAuras(
        AuraFixture fixture,
        BattleUnitState target,
        StringName damageTag
    ) =>
        fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery.CollectMitigationAuras(
            new BattleEquipmentAbilityMitigationAuraContext
            {
                TargetUnit = target,
                BattleState = fixture.State,
                DamageTag = damageTag,
            }
        );

    private static bool ContainsSource(
        IReadOnlyList<BattleEquipmentAbilityMitigationAuraResult> results,
        StringName sourceUnitId
    )
    {
        foreach (
            BattleEquipmentAbilityMitigationAuraResult result
                in results ?? Array.Empty<BattleEquipmentAbilityMitigationAuraResult>()
        )
        {
            if (result?.SourceUnitId == sourceUnitId)
                return true;
        }
        return false;
    }

    private sealed class AuraFixture : IDisposable
    {
        private AuraFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState holderA,
            BattleUnitState holderB,
            BattleUnitState enemyHolder,
            BattleUnitState protectedUnit,
            BattleUnitState attacker
        )
        {
            Runtime = runtime;
            State = state;
            HolderA = holderA;
            HolderB = holderB;
            EnemyHolder = enemyHolder;
            Protected = protectedUnit;
            Attacker = attacker;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState HolderA { get; }
        internal BattleUnitState HolderB { get; }
        internal BattleUnitState EnemyHolder { get; }
        internal BattleUnitState Protected { get; }
        internal BattleUnitState Attacker { get; }

        internal static AuraFixture Create()
        {
            var binding = new EquipmentAbilityBindingDefinition
            {
                BindingId = BindingId,
                TraitId = "trait.test.mitigation_aura",
                MitigationAuras = new[]
                {
                    new EquipmentMitigationAuraDefinition
                    {
                        AuraId = AuraId,
                        Radius = 2,
                        TargetTeamFilter = "ally",
                        DamageTag = "fire",
                        MitigationTier = "half",
                        Label = "Test fire guard aura",
                    },
                },
            };
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                equipment_ability_bindings:
                    new Dictionary<StringName, EquipmentAbilityBindingDefinition>
                    {
                        [BindingId] = binding,
                    }
            );

            BattleUnitState holderA = Unit("aura_holder_a", "heroes", new Vector2I(0, 0));
            BattleUnitState holderB = Unit("aura_holder_b", "heroes", new Vector2I(2, 2));
            BattleUnitState enemyHolder = Unit(
                "aura_enemy_holder",
                "enemies",
                new Vector2I(2, 1)
            );
            BattleUnitState protectedUnit = Unit(
                "aura_protected",
                "heroes",
                new Vector2I(2, 0)
            );
            BattleUnitState attacker = Unit(
                "aura_attacker",
                "enemies",
                new Vector2I(4, 0)
            );
            AttachAuraSource(holderA, "holder_a");
            AttachAuraSource(holderB, "holder_b");
            AttachAuraSource(enemyHolder, "enemy_holder");

            var state = new BattleState { battle_id = "mitigation_aura_test" };
            state.SetUnit(holderA);
            state.SetUnit(holderB);
            state.SetUnit(enemyHolder);
            state.SetUnit(protectedUnit);
            state.SetUnit(attacker);
            return new AuraFixture(
                runtime,
                state,
                holderA,
                holderB,
                enemyHolder,
                protectedUnit,
                attacker
            );
        }

        internal static void AttachAuraSource(BattleUnitState unit, StringName suffix)
        {
            StringName instanceId = $"eq_aura_{suffix}";
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = "item.test.mitigation_aura",
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName> { BindingId },
                    },
                },
                temporalProgressModifiers: null
            );
        }

        private static BattleUnitState Unit(
            StringName unitId,
            StringName factionId,
            Vector2I coord
        )
        {
            BattleUnitState unit = new BattleUnitState
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
            }.WithCombatResourcesForTest(hp: 100, isAlive: true);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
            unit.SetAnchorCoord(coord);
            return unit;
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
