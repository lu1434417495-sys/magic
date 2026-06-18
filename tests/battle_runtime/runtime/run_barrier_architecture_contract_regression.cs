using System;
using System.Collections.Generic;
using Godot;

public partial class run_barrier_architecture_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRequiredRuntimeBarrierFilesExist();
            Quit(_test.Finish("Barrier architecture contract regression"));
        }
        catch (Exception ex)
        {
            GD.PushError($"Barrier architecture contract regression crashed: {ex}");
            Quit(1);
        }
    }

    private void TestRequiredRuntimeBarrierFilesExist()
    {
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleBarrierService.cs",
            "BattleBarrierService must own barrier instances and interaction coordination."
        );
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleLayeredBarrierService.cs",
            "BattleLayeredBarrierService should remain the runtime-facing service type."
        );
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleBarrierGeometryService.cs",
            "BattleBarrierGeometryService must own footprint/line/area barrier geometry."
        );
        AssertFileExists(
            "res://scripts/systems/battle/runtime/BattleBarrierOutcomeResolver.cs",
            "BattleBarrierOutcomeResolver must own whitelist outcome translation."
        );
        AssertFileExists(
            "res://scripts/systems/battle/core/BattleBarrierInstanceState.cs",
            "Typed barrier instance state must replace anonymous barrier dictionaries."
        );
        AssertFileExists(
            "res://scripts/systems/battle/core/BattleBarrierLayerState.cs",
            "Typed barrier layer state must replace anonymous layer dictionaries."
        );
        AssertFileExists(
            "res://scripts/systems/battle/core/BattleBarrierOutcomeState.cs",
            "Typed barrier outcome state must replace anonymous outcome dictionaries."
        );
    }

    private void AssertFileExists(string path, string message)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            _test.Fail(message);
        }
    }

    private void RequireNull(object value, string message)
    {
        if (value != null)
        {
            _test.Fail(message);
        }
    }
}
