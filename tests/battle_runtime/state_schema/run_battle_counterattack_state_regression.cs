using System;
using Godot;

public partial class run_battle_counterattack_state_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestOwnerPresenceAndCloneIsolation();
        TestBudgetRefillAndFrozenAnchor();
        TestAttemptCostIsAtomic();
        RequestTestExit(
            _test.Finish("Battle counterattack state regression")
        );
    }

    private void TestOwnerPresenceAndCloneIsolation()
    {
        BattleUnitState unit = BuildUnit("counter_state_owner");
        _test.False(
            unit.CaptureReactionRawTyped().OwnerPresent,
            "new unit must distinguish a missing reaction owner."
        );
        _test.False(
            unit.CaptureCounterattackCapabilitiesRawTyped().OwnerPresent,
            "new unit must distinguish a missing capability owner."
        );

        unit.InitializeReactionBudgetTyped(
            20,
            new BattleReactionBudgetConfig(1, 60),
            startFull: true
        );
        unit.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                Capability(
                    "low",
                    BattleCounterattackTriggerKind.MeleeAttackEvaded,
                    priority: 1
                ),
                Capability(
                    "high",
                    BattleCounterattackTriggerKind.MeleeHitReceived,
                    priority: 10
                ),
            }
        );

        BattleUnitState clone = unit.clone();
        _test.True(
            clone.CaptureReactionRawTyped().OwnerPresent,
            "clone must preserve a present reaction owner."
        );
        _test.Eq(
            clone
                .CaptureCounterattackCapabilitiesRawTyped()
                .Values[0]
                .InstanceId,
            new StringName("high"),
            "capability projection must preserve deterministic priority order."
        );

        clone.ReplaceCounterattackCapabilitiesTyped(
            Array.Empty<BattleCounterattackCapability>()
        );
        _test.Eq(
            unit
                .CaptureCounterattackCapabilitiesRawTyped()
                .Values.Count,
            2,
            "mutating a cloned capability owner must not affect the source."
        );
    }

    private void TestBudgetRefillAndFrozenAnchor()
    {
        BattleUnitState unit = BuildUnit("counter_state_timeline");
        unit.InitializeReactionBudgetTyped(
            10,
            new BattleReactionBudgetConfig(2, 60),
            startFull: false
        );
        _test.False(
            unit.AdvanceReactionBudgetTyped(69),
            "reaction budget must not refill before its threshold."
        );
        _test.True(
            unit.AdvanceReactionBudgetTyped(190),
            "crossing multiple intervals must refill to capacity."
        );
        BattleUnitReactionSnapshot refilled =
            unit.CaptureReactionRawTyped();
        _test.Eq(
            refilled.ChargesRemaining,
            2,
            "multiple crossed intervals refill to capacity, not above it."
        );
        _test.Eq(
            refilled.NextRechargeAtTu,
            250,
            "recharge threshold must advance from the old anchor."
        );
        _test.True(
            unit.AdvanceFrozenReactionAnchorTyped(30),
            "time stasis must shift the recharge anchor."
        );
        _test.Eq(
            unit.CaptureReactionRawTyped().NextRechargeAtTu,
            280,
            "stasis shift must not clamp the future threshold to current TU."
        );
    }

    private void TestAttemptCostIsAtomic()
    {
        BattleUnitState unit = BuildUnit("counter_state_cost");
        unit.SetCurrentStamina(3);
        unit.InitializeReactionBudgetTyped(
            0,
            BattleReactionBudgetRules.EngineDefault,
            startFull: true
        );
        using var blockedBatch = new BattleEventBatch();
        _test.False(
            unit.TryCommitCounterattackAttemptCostTyped(
                4,
                blockedBatch
            ),
            "insufficient stamina must reject the whole attempt cost."
        );
        _test.Eq(
            unit.CaptureReactionRawTyped().ChargesRemaining,
            1,
            "failed stamina validation must not consume reaction charge."
        );
        _test.Eq(
            unit.GetCurrentStamina(),
            3,
            "failed charge/stamina transaction must not spend stamina."
        );

        using var allowedBatch = new BattleEventBatch();
        _test.True(
            unit.TryCommitCounterattackAttemptCostTyped(
                2,
                allowedBatch
            ),
            "available charge and stamina must commit together."
        );
        _test.Eq(
            unit.CaptureReactionRawTyped().ChargesRemaining,
            0,
            "successful attempt must consume exactly one charge."
        );
        _test.Eq(
            unit.GetCurrentStamina(),
            1,
            "successful attempt must spend the configured stamina."
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId) =>
        BattleTestFixture.BuildUnit(
            unitId,
            "player",
            Vector2I.Zero,
            currentHp: 20
        );

    private static BattleCounterattackCapability Capability(
        StringName instanceId,
        BattleCounterattackTriggerKind triggerKind,
        int priority
    ) =>
        new(
            instanceId,
            triggerKind,
            priority,
            ChancePercent: 100,
            AttackRollBonus: 0,
            WeaponActionDefinitionId: "basic_attack"
        );
}
