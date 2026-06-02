using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_world_encounter_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestNearbyEncounterEntriesUseTypedContextData();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime world encounter regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime world encounter regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestNearbyEncounterEntriesUseTypedContextData()
    {
        GameRuntimeFacade runtime = new();
        WorldMapGridSystem grid = new();
        using EncounterAnchorData farAnchor = BuildEncounterAnchor("far_anchor", "Far Anchor", new Vector2I(3, 0));
        using EncounterAnchorData nearAnchor = BuildEncounterAnchor("near_anchor", "Near Anchor", new Vector2I(1, 0));
        using EncounterAnchorData clearedAnchor = BuildEncounterAnchor("cleared_anchor", "Cleared Anchor", new Vector2I(0, 1), true);
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["encounter_anchors"] = new GArray
            {
                farAnchor,
                nearAnchor,
                clearedAnchor,
            };
            runtime._world_map_data_context.bind_root_world_data(rootWorldData);
            runtime._world_map_data_context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                Vector2I.Zero,
                Vector2I.Zero
            );
            runtime._player_coord = Vector2I.Zero;

            GArray entries = runtime.get_nearby_encounter_entries(8);
            AssertEq(entries.Count, 2, "Nearby encounter entries should skip cleared anchors.");
            GDictionary first = entries[0].AsGodotDictionary();
            GDictionary second = entries[1].AsGodotDictionary();
            AssertEq(first["entity_id"].AsString(), "near_anchor", "Nearby encounters should be sorted by distance.");
            AssertEq(first["display_name"].AsString(), "Near Anchor", "Nearby encounter entry should expose display_name.");
            AssertEq(first["encounter_kind"].AsString(), EncounterAnchorData.ENCOUNTER_KIND_SINGLE().ToString(), "Nearby encounter entry should expose encounter_kind.");
            AssertEq(first["growth_stage"].AsInt32(), 0, "Nearby encounter entry should expose growth_stage.");
            AssertEq(second["entity_id"].AsString(), "far_anchor", "Nearby encounter entries should retain farther active anchors.");

            GArray limitedEntries = runtime.get_nearby_encounter_entries(1);
            AssertEq(limitedEntries.Count, 1, "Nearby encounter entries should respect the requested limit.");
            AssertEq(limitedEntries[0].AsGodotDictionary()["entity_id"].AsString(), "near_anchor", "Limited encounter entries should keep nearest anchor.");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static GDictionary BuildRootWorldData() =>
        new()
        {
            ["world_step"] = 0,
            ["settlements"] = new GArray(),
            ["world_npcs"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["world_events"] = new GArray(),
        };

    private static WorldMapGenerationConfig BuildConfig() =>
        new()
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            player_start_coord = Vector2I.Zero,
        };

    private static EncounterAnchorData BuildEncounterAnchor(
        string entityId,
        string displayName,
        Vector2I coord,
        bool isCleared = false
    ) =>
        new()
        {
            entity_id = entityId,
            display_name = displayName,
            world_coord = coord,
            faction_id = "hostile",
            enemy_roster_template_id = "test_roster",
            region_tag = "test",
            vision_range = 2,
            is_cleared = isCleared,
            encounter_kind = EncounterAnchorData.ENCOUNTER_KIND_SINGLE(),
            encounter_profile_id = "test_profile",
            growth_stage = isCleared ? 1 : 0,
            suppressed_until_step = 0,
        };

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
