using System;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_fate_typed_event_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTypedFateEventCannotLoseAttackerMemberIdToDictionaryTypo();
        TestTypedMisfortuneRequestCannotLoseUnitStateToDictionaryTypo();
        TestRawDictionaryFateEventSurfaceIsAbsent();

        RequestTestExit(_test.Finish("Fate typed event regression"));
    }

    private void TestTypedFateEventCannotLoseAttackerMemberIdToDictionaryTypo()
    {
        using var bus = new BattleFateEventBus();
        var fateRuntime = new FateRuntimeModule();
        BattleUnitState hero = BuildUnit("hero_unit", "hero_member");
        try
        {
            fateRuntime.Setup(
                fate_event_bus: bus,
                unit_by_member_id_resolver: memberId =>
                    memberId == new StringName("hero_member") ? hero : null
            );
            fateRuntime.BeginBattle(new BattleCalamityStore());

            bus.Dispatch(
                BattleFateEventPayload.CriticalFail(
                    battleId: "battle_typed",
                    attackerMemberId: "hero_member",
                    attackerId: "hero_unit",
                    defenderId: "enemy_unit",
                    hiddenLuckAtBirth: null
                )
            );

            _test.Eq(
                fateRuntime.GetMemberCalamity("hero_member"),
                1,
                "typed critical_fail event should grant calamity without reading raw dictionary keys."
            );
        }
        finally
        {
            fateRuntime.DisposeRuntime();
            BattleTestFixture.DisposeBattleUnit(hero);
        }
    }

    private void TestTypedMisfortuneRequestCannotLoseUnitStateToDictionaryTypo()
    {
        var fateRuntime = new FateRuntimeModule();
        BattleUnitState hero = BuildUnit("low_hp_unit", "low_hp_member");
        try
        {
            hero.current_hp = 1;
            fateRuntime.Setup(
                unit_by_member_id_resolver: memberId =>
                    memberId == new StringName("low_hp_member") ? hero : null
            );
            fateRuntime.BeginBattle(new BattleCalamityStore());

            fateRuntime.HandleMisfortuneTrigger(
                MisfortuneTriggerRequest.LowHpTurnEnd(hero)
            );

            _test.Eq(
                fateRuntime.GetMemberCalamity("low_hp_member"),
                1,
                "typed low_hp_end_turn request should grant calamity without reading a unit_state dictionary key."
            );
        }
        finally
        {
            fateRuntime.DisposeRuntime();
            BattleTestFixture.DisposeBattleUnit(hero);
        }
    }

    private void TestRawDictionaryFateEventSurfaceIsAbsent()
    {
        foreach (
            MethodInfo method in typeof(BattleFateEventBus).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.True(
                    parameter.ParameterType != typeof(GDictionary),
                    $"BattleFateEventBus.{method.Name} should not expose raw Dictionary parameter '{parameter.Name}'."
                );
            }
        }

        EventInfo eventInfo = typeof(BattleFateEventBus).GetEvent(
            "EventDispatched",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        MethodInfo invoke = eventInfo?.EventHandlerType?.GetMethod("Invoke");
        _test.True(invoke != null, "BattleFateEventBus.EventDispatched should remain available.");
        if (invoke == null)
        {
            return;
        }

        foreach (ParameterInfo parameter in invoke.GetParameters())
        {
            _test.True(
                parameter.ParameterType != typeof(GDictionary),
                $"BattleFateEventBus.EventDispatched should not expose raw Dictionary parameter '{parameter.Name}'."
            );
        }
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName memberId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = memberId,
            faction_id = "player",
            is_alive = true,
            current_hp = 10,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        return unit;
    }
}
