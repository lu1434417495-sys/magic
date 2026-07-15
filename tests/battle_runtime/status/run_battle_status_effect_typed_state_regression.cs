using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_status_effect_typed_state_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestCollectionRejectsMalformedPayloads();
        TestCollectionRoundTripsValidStatus();
        TestBattleUnitStatusProjectionIsNotLiveOwner();

        RequestTestExit(_test.Finish("Battle status effect typed state regression"));
    }

    private void TestCollectionRejectsMalformedPayloads()
    {
        using GDictionary malformed = new() { ["burning"] = "bad" };
        ExpectArgumentException(
            () => BattleStatusEffectCollection.FromDictionary(malformed),
            "status_effects value that is not a dictionary must be rejected at the collection boundary."
        );

        using GDictionary mismatchedStatus = ValidStatusPayload("slow");
        using GDictionary mismatched = new() { ["burning"] = mismatchedStatus };
        ExpectArgumentException(
            () => BattleStatusEffectCollection.FromDictionary(mismatched),
            "status_effects key/payload status_id mismatch must be rejected at the collection boundary."
        );
    }

    private void TestCollectionRoundTripsValidStatus()
    {
        var collection = new BattleStatusEffectCollection();
        collection.Set(new BattleStatusEffectState
        {
            status_id = "burning",
            source_unit_id = "caster",
            power = 3,
            stacks = 2,
            duration = 10,
        });

        using GodotProjectionLease<GDictionary> collectionLease = collection.ToDictionaryLease(
            LifetimeDomain.Request,
            "battle-status-effect-typed-state-roundtrip"
        );
        BattleStatusEffectCollection restored = BattleStatusEffectCollection.FromDictionary(
            collectionLease.Value
        );
        BattleStatusEffectState status = restored.Get("burning");

        _test.True(status != null, "valid status should roundtrip through typed collection.");
        _test.Eq(status?.source_unit_id ?? new StringName(""), new StringName("caster"), "roundtrip should preserve source_unit_id.");
        _test.Eq(status?.power ?? -1, 3, "roundtrip should preserve power.");
        _test.Eq(status?.stacks ?? -1, 2, "roundtrip should preserve stacks.");
        _test.Eq(status?.duration ?? -1, 10, "roundtrip should preserve duration.");
    }

    private void TestBattleUnitStatusProjectionIsNotLiveOwner()
    {
        var unit = new BattleUnitState { unit_id = "projection_unit" };

        using GodotProjectionLease<GDictionary> firstLease = unit.ToDictionaryLease(
            LifetimeDomain.Request,
            "battle-status-effect-projection-first"
        );
        using GDictionary projectedStatusEffects = firstLease.Value["status_effects"]
            .AsGodotDictionary();
        using GDictionary injectedStatus = ValidStatusPayload("burning");
        projectedStatusEffects["burning"] = injectedStatus;

        _test.True(
            unit.GetStatusEffect("burning") == null,
            "mutating the status_effects projection must not add live runtime status state."
        );
        using GodotProjectionLease<GDictionary> secondLease = unit.ToDictionaryLease(
            LifetimeDomain.Request,
            "battle-status-effect-projection-second"
        );
        using GDictionary secondProjection = secondLease.Value["status_effects"]
            .AsGodotDictionary();
        _test.False(
            secondProjection.ContainsKey("burning"),
            "status_effects projection should not retain external projection mutations."
        );

        unit.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "burning",
            source_unit_id = "caster",
            power = 1,
            stacks = 1,
        });
        using GodotProjectionLease<GDictionary> thirdLease = unit.ToDictionaryLease(
            LifetimeDomain.Request,
            "battle-status-effect-projection-third"
        );
        using GDictionary thirdProjection = thirdLease.Value["status_effects"]
            .AsGodotDictionary();
        _test.True(
            thirdProjection.ContainsKey("burning"),
            "typed SetStatusEffect should still be visible through the save/projection dictionary."
        );
    }

    private GDictionary ValidStatusPayload(string statusId) =>
        new()
        {
            ["status_id"] = statusId,
            ["source_unit_id"] = "caster",
            ["power"] = 1,
            ["params"] = new GDictionary(),
            ["stacks"] = 1,
        };

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
}
