using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_counterattack_preview_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestPreviewProbabilityAndMutationFreedom();
        TestMultiStageBranchSemantics();
        TestUnsupportedCoverageStaysTyped();
        RequestTestExit(
            _test.Finish("Battle counterattack preview regression")
        );
    }

    private void TestPreviewProbabilityAndMutationFreedom()
    {
        using Fixture fixture = Fixture.Build();
        fixture.Defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                Capability(
                    "hit_capability",
                    BattleCounterattackTriggerKind.MeleeHitReceived,
                    chancePercent: 50
                ),
                Capability(
                    "miss_capability",
                    BattleCounterattackTriggerKind.MeleeAttackEvaded,
                    chancePercent: 20
                ),
            }
        );
        BattleUnitReactionSnapshot reactionBefore =
            fixture.Defender.CaptureReactionRawTyped();
        int staminaBefore = fixture.Defender.GetCurrentStamina();
        int queueBefore =
            fixture.Runtime._counterattackSystem.PendingCount;

        BattleCounterattackRiskProjection risk =
            fixture.Runtime._moduleBorrowers.CounterattackPreview.Build(
                fixture.State,
                fixture.Attacker.unit_id,
                BattleCounterattackRiskCoverage.Complete,
                new[]
                {
                    new BattleCounterattackPreviewTarget(
                        fixture.Defender.unit_id,
                        producesAttackResolutionFact: true,
                        includesWeaponDamage: true,
                        BattleAttackDeliveryKind.MeleeWeapon,
                        new[] { 6_000 }
                    ),
                }
            );

        _test.Eq(
            risk.Coverage,
            BattleCounterattackRiskCoverage.Complete,
            "deterministic melee preview must report complete coverage."
        );
        _test.Eq(
            risk.Entries.Count,
            1,
            "one defender must produce one deduplicated risk entry."
        );
        _test.Eq(
            risk.Entries[0]
                .PotentialCounterattackChanceBasisPoints,
            3_800,
            "hit and miss branches must combine using stage probability."
        );
        _test.Eq(
            risk.PotentialExpectedCountBasisPoints,
            3_800L,
            "aggregate expected count must equal the entry sum."
        );
        _test.True(
            risk.Entries[0]
                .ExecutableDefinitionDamageEnvelope
                .HasDamage,
            "available weapon action must expose a definition-only damage envelope."
        );
        _test.Eq(
            fixture.Defender.CaptureReactionRawTyped(),
            reactionBefore,
            "preview must not consume or refill reaction state."
        );
        _test.Eq(
            fixture.Defender.GetCurrentStamina(),
            staminaBefore,
            "preview must not spend stamina."
        );
        _test.Eq(
            fixture.Runtime._counterattackSystem.PendingCount,
            queueBefore,
            "preview must not enqueue counterattacks."
        );
    }

    private void TestUnsupportedCoverageStaysTyped()
    {
        using Fixture fixture = Fixture.Build();
        BattleCounterattackRiskProjection unsupported =
            fixture.Runtime._moduleBorrowers.CounterattackPreview.Build(
                fixture.State,
                fixture.Attacker.unit_id,
                BattleCounterattackRiskCoverage
                    .RandomTargetSelectionUnknown,
                Array.Empty<BattleCounterattackPreviewTarget>()
            );

        _test.Eq(
            unsupported.Coverage,
            BattleCounterattackRiskCoverage
                .RandomTargetSelectionUnknown,
            "random target uncertainty must not be projected as zero risk."
        );
        _test.Eq(
            unsupported.Entries.Count,
            0,
            "unsupported coverage must not carry fabricated numeric entries."
        );
    }

    private void TestMultiStageBranchSemantics()
    {
        using Fixture fixture = Fixture.Build();
        fixture.Defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                Capability(
                    "hit_only",
                    BattleCounterattackTriggerKind
                        .MeleeHitReceived,
                    chancePercent: 50
                ),
            }
        );
        _test.Eq(
            BuildRisk(
                fixture,
                new[] { 6_000, 6_000 }
            ).Entries[0]
                .PotentialCounterattackChanceBasisPoints,
            4_200,
            "hit-only capability must use the first supported hit across stages."
        );

        fixture.Defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                Capability(
                    "miss_only",
                    BattleCounterattackTriggerKind
                        .MeleeAttackEvaded,
                    chancePercent: 20
                ),
            }
        );
        _test.Eq(
            BuildRisk(
                fixture,
                new[] { 6_000, 6_000 }
            ).Entries[0]
                .PotentialCounterattackChanceBasisPoints,
            1_280,
            "miss-only capability must use the first supported miss across stages."
        );

        fixture.Defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                Capability(
                    "hit_first_stage",
                    BattleCounterattackTriggerKind
                        .MeleeHitReceived,
                    chancePercent: 50
                ),
                Capability(
                    "miss_first_stage",
                    BattleCounterattackTriggerKind
                        .MeleeAttackEvaded,
                    chancePercent: 20
                ),
            }
        );
        _test.Eq(
            BuildRisk(
                fixture,
                new[] { 6_000, 1_000 }
            ).Entries[0]
                .PotentialCounterattackChanceBasisPoints,
            3_800,
            "when both branches exist only the first stage may produce the opportunity."
        );

        fixture.Defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                Capability(
                    "zero_chance",
                    BattleCounterattackTriggerKind
                        .MeleeHitReceived,
                    chancePercent: 0
                ),
                Capability(
                    "full_chance",
                    BattleCounterattackTriggerKind
                        .MeleeAttackEvaded,
                    chancePercent: 100
                ),
            }
        );
        int bounded =
            BuildRisk(
                fixture,
                new[] { 0, 10_000 }
            ).Entries[0]
                .PotentialCounterattackChanceBasisPoints;
        _test.Eq(
            bounded,
            10_000,
            "0/100 stage and capability chances must stay exact and bounded."
        );
    }

    private static BattleCounterattackRiskProjection BuildRisk(
        Fixture fixture,
        IReadOnlyList<int> stageHitChanceBasisPoints
    ) =>
        fixture.Runtime._moduleBorrowers.CounterattackPreview.Build(
            fixture.State,
            fixture.Attacker.unit_id,
            BattleCounterattackRiskCoverage.Complete,
            new[]
            {
                new BattleCounterattackPreviewTarget(
                    fixture.Defender.unit_id,
                    producesAttackResolutionFact: true,
                    includesWeaponDamage: true,
                    BattleAttackDeliveryKind.MeleeWeapon,
                    stageHitChanceBasisPoints
                ),
            }
        );

    private static BattleCounterattackCapability Capability(
        StringName instanceId,
        BattleCounterattackTriggerKind triggerKind,
        int chancePercent
    ) =>
        new(
            instanceId,
            triggerKind,
            SelectionPriority: 1,
            ChancePercent: chancePercent,
            AttackRollBonus: 0,
            WeaponActionDefinitionId: "test_counter_weapon_action"
        );

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState attacker,
            BattleUnitState defender
        )
        {
            Runtime = runtime;
            State = state;
            Attacker = attacker;
            Defender = defender;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Attacker { get; }
        internal BattleUnitState Defender { get; }

        internal static Fixture Build()
        {
            SkillDefinition action = BuildWeaponAction();
            BattleUnitState attacker = BuildUnit(
                "preview_attacker",
                "player",
                Vector2I.Zero
            );
            BattleUnitState defender = BuildUnit(
                "preview_defender",
                "enemy",
                new Vector2I(1, 0)
            );
            defender.SetKnownSkillLevelTyped(action.SkillId, 1);
            var state = BattleTestFixture.BuildFlatState(
                "counterattack_preview",
                new Vector2I(3, 2)
            );
            BattleTestFixture.InstallUnits(
                state,
                new[] { attacker },
                new[] { defender }
            );
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                skill_definitions:
                    new Dictionary<StringName, SkillDefinition>
                    {
                        [action.SkillId] = action,
                    }
            );
            runtime.SetupStateForTests(state);
            return new Fixture(
                runtime,
                state,
                attacker,
                defender
            );
        }

        public void Dispose()
        {
            Runtime.dispose();
        }

        private static BattleUnitState BuildUnit(
            StringName unitId,
            StringName factionId,
            Vector2I coord
        )
        {
            BattleUnitState unit = BattleTestFixture.BuildUnit(
                unitId,
                factionId,
                coord,
                currentHp: 30
            );
            unit.SetCurrentStamina(10);
            unit.RestoreWeaponProjectionForMutationSnapshotExact(
                BattleUnitWeaponProjectionSnapshot.Present(
                    new BattleWeaponProjectionValues(
                        "equipped",
                        "test_sword",
                        "test_sword_profile",
                        "melee",
                        "sword",
                        "one_handed",
                        1,
                        new BattleWeaponDiceValues(
                            true,
                            1,
                            6,
                            0
                        ),
                        BattleWeaponDiceValues.PresentEmpty,
                        false,
                        false,
                        "physical_slash"
                    )
                )
            );
            return unit;
        }

        private static SkillDefinition BuildWeaponAction()
        {
            StringName skillId = "test_counter_weapon_action";
            CombatEffectDefinition weaponDamage =
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    addWeaponDice: true
                );
            return TestSkillDefinitionProjection.BuildSkill(
                skillId,
                combatProfile:
                    TestSkillDefinitionProjection.BuildCombatProfile(
                        skillId,
                        new[] { weaponDamage },
                        targetMode: "unit",
                        targetTeamFilter: "enemy",
                        rangeValue: 1,
                        staminaCost: 2
                    )
            );
        }
    }
}
