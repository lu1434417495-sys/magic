using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_pending_battle_request_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPendingBattleGenerationRequestUsesTypedState();

        Quit(_test.Finish("Game runtime pending battle request regression"));
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
                ["battle_party"] = new GArray { BuildUnit("pending_ally", "player", new Vector2I(0, 0)).ToDictionary() },
                ["enemy_units"] = new GArray { BuildUnit("pending_enemy", "hostile", new Vector2I(1, 0)).ToDictionary() },
            };

            StringName startResult = runtime.BeginBattleStart(anchor, 777, context);
            _test.Eq(startResult.ToString(), "pending", "Invalid fixture battle should leave generation pending.");
            _test.True(runtime.HasPendingBattleGenerationRequest(), "Runtime should report a typed pending battle generation request.");

            GameRuntimePendingBattleGenerationRequest request =
                runtime.GetPendingBattleGenerationRequestState();
            _test.True(request != null && !request.IsEmpty, "Pending request state should be non-empty.");
            _test.True(ReferenceEquals(request.EncounterAnchor, anchor), "Pending request should retain the typed encounter anchor.");
            _test.Eq(request.Seed, 777, "Pending request should retain the seed.");

            context["custom_flag"] = "mutated";
            GDictionary storedContext = request.CloneContext();
            _test.Eq(storedContext["custom_flag"].AsString(), "original", "Pending request should duplicate the input context.");

            storedContext["custom_flag"] = "clone_mutated";
            _test.Eq(request.CloneContext()["custom_flag"].AsString(), "original", "Pending request should return cloned contexts.");

            runtime.ClearPendingBattleGenerationRequest();
            _test.False(runtime.HasPendingBattleGenerationRequest(), "Clearing typed request should clear pending state.");
        }
        finally
        {
            runtime.Dispose();
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.RefreshFootprint();
        return unit;
    }
}
