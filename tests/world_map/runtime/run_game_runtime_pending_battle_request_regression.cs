using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_pending_battle_request_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPendingBattleGenerationRequestUsesTypedState();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime pending battle request regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime pending battle request regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestPendingBattleGenerationRequestUsesTypedState()
    {
        GameRuntimeFacade runtime = new();
        try
        {
            EncounterAnchorData anchor = new()
            {
                entity_id = "pending_anchor",
                display_name = "Pending Anchor",
                world_coord = new Vector2I(2, 3),
            };
            GDictionary context = new()
            {
                ["world_coord"] = anchor.world_coord,
                ["custom_flag"] = "original",
                ["battle_terrain_profile"] = "missing_profile",
                ["battle_party"] = new GArray { BuildUnit("pending_ally", "player", new Vector2I(0, 0)) },
                ["enemy_units"] = new GArray { BuildUnit("pending_enemy", "hostile", new Vector2I(1, 0)) },
            };

            StringName startResult = runtime.begin_battle_start(anchor, 777, context);
            AssertEq(startResult.ToString(), "pending", "Invalid fixture battle should leave generation pending.");
            AssertTrue(runtime._has_pending_battle_generation_request(), "Runtime should report a typed pending battle generation request.");

            GameRuntimePendingBattleGenerationRequest request =
                runtime.GetPendingBattleGenerationRequestState();
            AssertTrue(request != null && !request.IsEmpty, "Pending request state should be non-empty.");
            AssertTrue(ReferenceEquals(request.EncounterAnchor, anchor), "Pending request should retain the typed encounter anchor.");
            AssertEq(request.Seed, 777, "Pending request should retain the seed.");

            context["custom_flag"] = "mutated";
            GDictionary storedContext = request.CloneContext();
            AssertEq(storedContext["custom_flag"].AsString(), "original", "Pending request should duplicate the input context.");

            storedContext["custom_flag"] = "clone_mutated";
            AssertEq(request.CloneContext()["custom_flag"].AsString(), "original", "Pending request should return cloned contexts.");

            runtime.ClearPendingBattleGenerationRequest();
            AssertFalse(runtime._has_pending_battle_generation_request(), "Clearing typed request should clear pending state.");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = coord,
            is_alive = true,
            current_hp = 10,
        };
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.refresh_footprint();
        return unit;
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
