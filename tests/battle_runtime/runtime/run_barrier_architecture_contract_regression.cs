using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_barrier_architecture_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRequiredRuntimeBarrierFilesExist();
            TestBarrierRuntimeStateTypesArePlainTypedCSharp();
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

    private void TestBarrierRuntimeStateTypesArePlainTypedCSharp()
    {
        foreach (Type type in new[]
        {
            typeof(BattleBarrierInstanceState),
            typeof(BattleBarrierLayerState),
            typeof(BattleBarrierOutcomeState),
        })
        {
            RequireNull(
                type.GetMethod("from_runtime_dict", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
                $"{type.Name} must not keep the old snake_case from_runtime_dict API."
            );
            RequireNull(
                type.GetMethod("to_runtime_dict", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance),
                $"{type.Name} must not keep the old snake_case to_runtime_dict API."
            );
            AssertNoGodotCollectionProperties(type);
        }

        RequireNull(
            typeof(BattleBarrierOutcomeResolver).GetMethod("ApplyPassageOutcomes"),
            "BattleBarrierOutcomeResolver must not expose a Dictionary wrapper for passage outcomes."
        );

        var outcome = new BattleBarrierOutcomeState
        {
            OutcomeKind = BarrierOutcomeKind.Damage,
            Amount = 7,
        };
        var layer = new BattleBarrierLayerState
        {
            LayerId = "green",
            DisplayName = "Green Layer",
            SaveRollOverride = 9,
            HasSaveRollOverride = true,
        };
        layer.SetBlockedCategories(new[] { new StringName("poison") });
        layer.SetBreakerSkillIds(new[] { new StringName("mage_passwall") });
        layer.SetPassageOutcomes(new[] { outcome });

        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "barrier_1",
            ProfileId = "prismatic_sphere",
            AnchorCoord = new Vector2I(3, 4),
            RadiusCells = 2,
        };
        barrier.SetLayers(new[] { layer });

        _test.False(
            (object)barrier.Layers is Godot.Collections.Array,
            "BattleBarrierInstanceState.Layers must be a typed C# list view, not Godot Array."
        );
        _test.False(
            (object)layer.BlockedCategories is Godot.Collections.Array,
            "BattleBarrierLayerState.BlockedCategories must be a typed C# list view, not Godot Array."
        );
        _test.False(
            (object)layer.BreakerSkillIds is Godot.Collections.Array,
            "BattleBarrierLayerState.BreakerSkillIds must be a typed C# list view, not Godot Array."
        );
        _test.False(
            (object)layer.PassageOutcomes is Godot.Collections.Array,
            "BattleBarrierLayerState.PassageOutcomes must be a typed C# list view, not Godot Array."
        );

        BattleBarrierInstanceState roundTrip =
            BattleBarrierInstanceState.FromRuntimeDict(barrier.ToRuntimeDict());
        _test.Eq(roundTrip.Layers.Count, 1, "Barrier runtime projection should preserve typed layers.");
        _test.Eq(
            roundTrip.Layers[0].PassageOutcomes.Count,
            1,
            "Barrier runtime projection should preserve typed passage outcomes."
        );
        _test.Eq(
            roundTrip.Layers[0].PassageOutcomes[0].Amount,
            7,
            "Barrier runtime projection should preserve outcome fields."
        );
    }

    private void AssertNoGodotCollectionProperties(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            string propertyType = property.PropertyType.FullName ?? "";
            if (propertyType.Contains("Godot.Collections.", StringComparison.Ordinal))
            {
                _test.Fail(
                    $"{type.Name}.{property.Name} must not expose Godot collection state."
                );
            }
        }
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
