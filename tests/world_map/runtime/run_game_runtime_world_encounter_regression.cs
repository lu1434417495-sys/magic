using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_world_encounter_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestNearbyEncounterEntriesUseTypedContextData();

        Quit(_test.Finish("Game runtime world encounter regression"));
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
            runtime._world_map_data_context.BindRootWorldData(rootWorldData);
            runtime._world_map_data_context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                Vector2I.Zero,
                Vector2I.Zero
            );
            runtime._player_coord = Vector2I.Zero;

            GArray entries = runtime.GetNearbyEncounterEntries(8);
            _test.Eq(entries.Count, 2, "Nearby encounter entries should skip cleared anchors.");
            GDictionary first = entries[0].AsGodotDictionary();
            GDictionary second = entries[1].AsGodotDictionary();
            _test.Eq(first["entity_id"].AsString(), "near_anchor", "Nearby encounters should be sorted by distance.");
            _test.Eq(first["display_name"].AsString(), "Near Anchor", "Nearby encounter entry should expose display_name.");
            _test.Eq(first["encounter_kind"].AsString(), EncounterAnchorData.ToStringName(EncounterAnchorKind.Single).ToString(), "Nearby encounter entry should expose encounter_kind.");
            _test.Eq(first["growth_stage"].AsInt32(), 0, "Nearby encounter entry should expose growth_stage.");
            _test.Eq(second["entity_id"].AsString(), "far_anchor", "Nearby encounter entries should retain farther active anchors.");

            GArray limitedEntries = runtime.GetNearbyEncounterEntries(1);
            _test.Eq(limitedEntries.Count, 1, "Nearby encounter entries should respect the requested limit.");
            _test.Eq(limitedEntries[0].AsGodotDictionary()["entity_id"].AsString(), "near_anchor", "Limited encounter entries should keep nearest anchor.");
        }
        finally
        {
            runtime.Dispose();
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
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "test_profile",
            growth_stage = isCleared ? 1 : 0,
            suppressed_until_step = 0,
        };
}
