using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_status_effect_typed_state_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCollectionRejectsMalformedPayloads();
        TestCollectionRoundTripsValidStatus();
        TestBattleUnitStatusProjectionIsNotLiveOwner();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle status effect typed state regression"));
    }

    private void TestCollectionRejectsMalformedPayloads()
    {
        ExpectArgumentException(
            () => BattleStatusEffectCollection.FromDictionary(new GDictionary { ["burning"] = "bad" }),
            "status_effects value that is not a dictionary must be rejected at the collection boundary."
        );

        ExpectArgumentException(
            () => BattleStatusEffectCollection.FromDictionary(
                new GDictionary { ["burning"] = ValidStatusPayload("slow") }
            ),
            "status_effects key/payload status_id mismatch must be rejected at the collection boundary."
        );
    }

    private void TestCollectionRoundTripsValidStatus()
    {
        var collection = new BattleStatusEffectCollection();
        BattleStatusEffectState originalStatus = new()
        {
            status_id = "burning",
            source_unit_id = "caster",
            power = 3,
            stacks = 2,
            duration = 10,
        };
        collection.Set(originalStatus);

        BattleStatusEffectCollection restored =
            BattleStatusEffectCollection.FromDictionary(collection.ToDictionary());
        BattleStatusEffectState status = restored.Get("burning");

        _test.True(status != null, "valid status should roundtrip through typed collection.");
        _test.Eq(status?.source_unit_id ?? new StringName(""), new StringName("caster"), "roundtrip should preserve source_unit_id.");
        _test.Eq(status?.power ?? -1, 3, "roundtrip should preserve power.");
        _test.Eq(status?.stacks ?? -1, 2, "roundtrip should preserve stacks.");
        _test.Eq(status?.duration ?? -1, 10, "roundtrip should preserve duration.");

        GodotSharpCleanup.DisposeGodotObject(originalStatus);
        GodotSharpCleanup.DisposeGodotObject(status);
    }

    private void TestBattleUnitStatusProjectionIsNotLiveOwner()
    {
        var unit = new BattleUnitState { unit_id = "projection_unit" };
        try
        {

            GDictionary projectedStatusEffects = StatusProjection(unit);
            projectedStatusEffects["burning"] = ValidStatusPayload("burning");

            _test.True(
                unit.GetStatusEffect("burning") == null,
                "mutating the status_effects projection must not add live runtime status state."
            );
            _test.False(
                StatusProjection(unit).ContainsKey("burning"),
                "status_effects projection should not retain external projection mutations."
            );

            unit.SetStatusEffect(new BattleStatusEffectState
            {
                status_id = "burning",
                source_unit_id = "caster",
                power = 1,
                stacks = 1,
            });
            _test.True(
                StatusProjection(unit).ContainsKey("burning"),
                "typed SetStatusEffect should still be visible through the save/projection dictionary."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(unit);
        }
    }

    private GDictionary StatusProjection(BattleUnitState unit) =>
        unit.ToDictionary()["status_effects"].AsGodotDictionary();

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
