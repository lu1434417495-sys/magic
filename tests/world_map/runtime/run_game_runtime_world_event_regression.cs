using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_world_event_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestNearbyWorldEventEntriesUseTypedContextData();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime world event regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime world event regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestNearbyWorldEventEntriesUseTypedContextData()
    {
        GameRuntimeFacade runtime = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["world_events"] = new GArray
            {
                BuildWorldEvent("far_event", "Far Event", new Vector2I(3, 0), true),
                BuildWorldEvent("near_event", "Near Event", new Vector2I(1, 0), true),
                BuildWorldEvent("hidden_event", "Hidden Event", new Vector2I(0, 1), false),
            };
            runtime._world_map_data_context.bind_root_world_data(rootWorldData);
            runtime._world_map_data_context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                Vector2I.Zero,
                Vector2I.Zero
            );
            runtime._player_coord = Vector2I.Zero;

            GArray entries = runtime.get_nearby_world_event_entries(8);
            AssertEq(entries.Count, 2, "Nearby world event entries should include discovered events only.");
            GDictionary first = entries[0].AsGodotDictionary();
            GDictionary second = entries[1].AsGodotDictionary();
            AssertEq(first["event_id"].AsString(), "near_event", "Nearby world events should be sorted by distance.");
            AssertEq(first["display_name"].AsString(), "Near Event", "Nearby world event entry should expose display_name.");
            AssertEq(first["event_type"].AsString(), "enter_submap", "Nearby world event entry should expose event_type.");
            AssertEq(first["target_submap_id"].AsString(), "submap_near_event", "Nearby world event entry should expose target_submap_id.");
            AssertEq(second["event_id"].AsString(), "far_event", "Nearby world event entries should retain farther discovered events.");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static GDictionary BuildWorldEvent(
        string eventId,
        string displayName,
        Vector2I coord,
        bool discovered
    ) =>
        new()
        {
            ["event_id"] = eventId,
            ["display_name"] = displayName,
            ["world_coord"] = coord,
            ["is_discovered"] = discovered,
            ["event_type"] = "enter_submap",
            ["target_submap_id"] = $"submap_{eventId}",
            ["discovery_condition_id"] = discovered ? "always_true" : "never_true",
            ["prompt_title"] = "",
            ["prompt_text"] = "",
        };

    private static WorldMapGenerationConfig BuildConfig() =>
        new()
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            player_start_coord = Vector2I.Zero,
        };

    private static GDictionary BuildRootWorldData() =>
        new()
        {
            ["world_step"] = 0,
            ["settlements"] = new GArray(),
            ["world_npcs"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["world_events"] = new GArray(),
        };

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
