using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_data_context_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedSyncResultClampsInvalidSelectedCoord();
        TestActiveWorldScalarQueries();
        TestSettlementTypedQueries();
        TestWorldNpcTypedQueries();
        TestEncounterAnchorTypedQueries();
        TestActiveWorldFogStateRoundTrip();
        TestSubmapEntryAndReturnTypedResults();
        TestWorldEventTypedQueries();
        TestStaleSubmapIdFallsBackToRootWorld();

        if (_failures.Count == 0)
        {
            GD.Print("World map data context regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map data context regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestWorldEventTypedQueries()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["world_events"] = new GArray
            {
                new GDictionary
                {
                    ["event_id"] = "visible_gate",
                    ["display_name"] = "Visible Gate",
                    ["world_coord"] = new Vector2I(1, 1),
                    ["is_discovered"] = true,
                    ["event_type"] = "enter_submap",
                    ["target_submap_id"] = "ash_submap",
                    ["discovery_condition_id"] = "always_true",
                    ["prompt_title"] = "Enter Ash",
                    ["prompt_text"] = "Go now.",
                },
                new GDictionary
                {
                    ["event_id"] = "hidden_gate",
                    ["display_name"] = "Hidden Gate",
                    ["world_coord"] = new Vector2I(2, 2),
                    ["is_discovered"] = false,
                    ["event_type"] = "enter_submap",
                    ["target_submap_id"] = "hidden_submap",
                    ["discovery_condition_id"] = "never_true",
                    ["prompt_title"] = "",
                    ["prompt_text"] = "",
                },
            };
            context.bind_root_world_data(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            WorldMapEventData eventData = context.GetWorldEventAt(new Vector2I(1, 1));
            AssertEq(eventData.EventId.ToString(), "visible_gate", "Typed world event query should expose event_id.");
            AssertEq(eventData.DisplayName, "Visible Gate", "Typed world event query should expose display_name.");
            AssertEq(eventData.TargetSubmapId.ToString(), "ash_submap", "Typed world event query should expose target_submap_id.");
            AssertTrue(eventData.IsTriggerableSubmapEntry, "Discovered enter_submap event with a target should be triggerable.");
            AssertEq(context.GetDiscoveredWorldEvents().Count, 1, "Typed discovered event query should skip undiscovered events.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestActiveWorldScalarQueries()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["world_step"] = 42;
            rootWorldData["player_start_settlement_name"] = "Spring Village";
            context.bind_root_world_data(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            AssertEq(context.get_world_step(), 42, "Context should expose active world step.");
            AssertEq(
                context.get_player_start_settlement_name(),
                "Spring Village",
                "Context should expose active world player_start_settlement_name."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestEncounterAnchorTypedQueries()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        using EncounterAnchorData farAnchor = BuildEncounterAnchor("far_anchor", "Far Anchor", new Vector2I(3, 0));
        using EncounterAnchorData nearAnchor = BuildEncounterAnchor("near_anchor", "Near Anchor", new Vector2I(1, 0));
        using EncounterAnchorData clearedAnchor = BuildEncounterAnchor("cleared_anchor", "Cleared Anchor", new Vector2I(2, 0), true);
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["encounter_anchors"] = new GArray
            {
                farAnchor,
                nearAnchor,
                clearedAnchor,
            };
            context.bind_root_world_data(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            List<EncounterAnchorData> allAnchors = context.GetActiveEncounterAnchors();
            List<EncounterAnchorData> activeAnchors = context.GetActiveEncounterAnchors(includeCleared: false);

            AssertEq(allAnchors.Count, 3, "Typed encounter anchor query should expose active world anchors.");
            AssertEq(activeAnchors.Count, 2, "Typed encounter anchor query should optionally skip cleared anchors.");
            AssertEq(
                context.get_encounter_anchor_at(new Vector2I(1, 0))?.entity_id.ToString(),
                "near_anchor",
                "Typed encounter anchor coord lookup should use the rebuilt context index."
            );
            AssertEq(
                context.get_encounter_anchor_by_id("far_anchor")?.world_coord,
                new Vector2I(3, 0),
                "Typed encounter anchor id lookup should resolve active anchors."
            );

            context.remove_encounter_anchor_by_id("near_anchor");
            AssertTrue(
                context.get_encounter_anchor_at(new Vector2I(1, 0)) == null,
                "Removing an encounter anchor should refresh the typed coord index."
            );
            AssertEq(context.GetActiveEncounterAnchors().Count, 2, "Removed encounter anchor should leave active world data.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestSettlementTypedQueries()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["settlements"] = new GArray
            {
                new GDictionary
                {
                    ["entity_id"] = "settlement_spring_village",
                    ["settlement_id"] = "spring_village",
                    ["display_name"] = "Spring Village",
                    ["origin"] = new Vector2I(1, 1),
                    ["footprint_size"] = new Vector2I(2, 1),
                    ["settlement_state"] = new GDictionary(),
                },
            };
            context.bind_root_world_data(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            WorldMapSettlementData firstCell = context.GetSettlementAt(new Vector2I(1, 1));
            WorldMapSettlementData footprintCell = context.GetSettlementAt(new Vector2I(2, 1));
            WorldMapSettlementData emptyCell = context.GetSettlementAt(new Vector2I(0, 0));

            AssertTrue(!firstCell.IsEmpty, "Typed settlement query should find settlement origin.");
            AssertEq(firstCell.SettlementId, "spring_village", "Typed settlement query should expose settlement_id.");
            AssertEq(firstCell.DisplayName, "Spring Village", "Typed settlement query should expose display_name.");
            AssertEq(
                footprintCell.SettlementId,
                "spring_village",
                "Typed settlement query should cover the full settlement footprint."
            );
            AssertTrue(emptyCell.IsEmpty, "Typed settlement query should report empty cells.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestWorldNpcTypedQueries()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["world_npcs"] = new GArray
            {
                new GDictionary
                {
                    ["coord"] = new Vector2I(1, 1),
                    ["display_name"] = " Gate Watcher ",
                    ["faction_id"] = "neutral",
                },
                new GDictionary
                {
                    ["coord"] = new Vector2I(2, 1),
                    ["faction_id"] = "neutral",
                },
                new GDictionary
                {
                    ["coord"] = new Vector2I(3, 1),
                    ["display_name"] = "Wrong Faction",
                    ["faction_id"] = 7,
                },
                new GDictionary
                {
                    ["coord"] = new Vector2I(1, 2),
                    ["display_name"] = "Blank Faction",
                    ["faction_id"] = " ",
                },
            };
            context.bind_root_world_data(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            WorldMapNpcData validNpc = context.GetWorldNpcAt(new Vector2I(1, 1));
            WorldMapNpcData missingNameNpc = context.GetWorldNpcAt(new Vector2I(2, 1));
            WorldMapNpcData numericFactionNpc = context.GetWorldNpcAt(new Vector2I(3, 1));
            WorldMapNpcData blankFactionNpc = context.GetWorldNpcAt(new Vector2I(1, 2));
            WorldMapNpcData emptyNpc = context.GetWorldNpcAt(new Vector2I(0, 0));

            AssertTrue(!validNpc.IsEmpty, "Typed world NPC query should find an indexed NPC.");
            AssertEq(validNpc.DisplayName, "Gate Watcher", "Typed world NPC query should trim display_name.");
            AssertEq(validNpc.FactionId, "neutral", "Typed world NPC query should expose faction_id.");
            AssertTrue(
                validNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should validate character info fields."
            );
            AssertEq(
                validNpc.ToDictionary()["display_name"].AsString(),
                " Gate Watcher ",
                "Typed world NPC query should preserve source data for section builders."
            );
            AssertTrue(
                !missingNameNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should reject missing display_name."
            );
            AssertTrue(
                !numericFactionNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should reject non-string faction_id."
            );
            AssertTrue(
                !blankFactionNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should reject blank faction_id."
            );
            AssertTrue(emptyNpc.IsEmpty, "Typed world NPC query should report empty cells.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestActiveWorldFogStateRoundTrip()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        WorldMapFogSystem fogSystem = new();
        WorldMapFogSystem restoredFogSystem = new();
        try
        {
            context.bind_root_world_data(BuildRootWorldData());
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);
            fogSystem.setup(new Vector2I(4, 4));
            fogSystem.RevealDiamond(new Vector2I(1, 1), 1, "player");

            AssertTrue(context.SaveActiveWorldFogState(fogSystem), "Context should save active fog state into active world data.");
            GDictionary savedFogState = context.GetActiveWorldFogState();
            AssertEq(
                savedFogState["version"].AsInt32(),
                WorldMapFogSystem.PERSISTENT_STATE_VERSION_ID(),
                "Context should expose the saved fog state payload."
            );

            restoredFogSystem.setup(new Vector2I(4, 4), savedFogState);
            AssertTrue(
                restoredFogSystem.is_explored(new Vector2I(1, 1), "player"),
                "Saved active fog state should restore explored player cells."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestSubmapEntryAndReturnTypedResults()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["active_submap_id"] = "";
            rootWorldData["submap_return_stack"] = new GArray();
            rootWorldData["mounted_submaps"] = new GDictionary
            {
                ["ash_submap"] = new GDictionary
                {
                    ["display_name"] = "Ash Map",
                    ["is_generated"] = true,
                    ["player_coord"] = new Vector2I(3, 3),
                    ["world_data"] = new GDictionary
                    {
                        ["world_step"] = 0,
                        ["player_start_coord"] = new Vector2I(2, 2),
                        ["settlements"] = new GArray(),
                        ["world_npcs"] = new GArray(),
                        ["encounter_anchors"] = new GArray(),
                        ["world_events"] = new GArray(),
                    },
                },
            };
            context.bind_root_world_data(rootWorldData);

            WorldMapSubmapEnterResult enterResult = context.EnterSubmap(
                "ash_submap",
                "",
                new Vector2I(1, 1)
            );

            AssertTrue(enterResult.Ok, "Entering a generated submap should succeed.");
            AssertEq(enterResult.PlayerCoord, new Vector2I(3, 3), "Submap entry should use saved submap player_coord.");
            AssertEq(enterResult.TargetDisplayName, "Ash Map", "Submap entry should expose display_name.");
            AssertEq(rootWorldData["active_submap_id"].AsString(), "ash_submap", "Submap entry should set active_submap_id.");
            GArray returnStack = rootWorldData["submap_return_stack"].AsGodotArray();
            AssertEq(returnStack.Count, 1, "Submap entry should push one return stack record.");
            GDictionary returnEntry = returnStack[0].AsGodotDictionary();
            AssertEq(returnEntry["map_id"].AsString(), "", "Submap entry should preserve source map id.");
            AssertEq(returnEntry["coord"].AsVector2I(), new Vector2I(1, 1), "Submap entry should preserve source coord.");

            context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                enterResult.PlayerCoord,
                enterResult.PlayerCoord
            );
            WorldMapSubmapReturnResult returnResult = context.ReturnFromActiveSubmap(new Vector2I(4, 4));

            AssertTrue(returnResult.Ok, "Returning from an active submap should succeed.");
            AssertEq(returnResult.TargetMapId, "", "Submap return should restore the source map id.");
            AssertEq(returnResult.PlayerCoord, new Vector2I(1, 1), "Submap return should restore the source coord.");
            AssertEq(rootWorldData["active_submap_id"].AsString(), "", "Submap return should clear active_submap_id.");
            AssertEq(rootWorldData["submap_return_stack"].AsGodotArray().Count, 0, "Submap return should pop the return stack.");
            GDictionary storedSubmap = rootWorldData["mounted_submaps"].AsGodotDictionary()["ash_submap"].AsGodotDictionary();
            AssertEq(
                storedSubmap["player_coord"].AsVector2I(),
                new Vector2I(4, 4),
                "Submap return should save the latest submap player coord."
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestTypedSyncResultClampsInvalidSelectedCoord()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            context.bind_root_world_data(BuildRootWorldData());
            WorldMapContextSyncResult result = context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                new Vector2I(3, 3),
                new Vector2I(99, 99)
            );

            AssertEq(result.PlayerCoord, new Vector2I(3, 3), "Typed sync should keep an in-bounds player coord.");
            AssertEq(result.SelectedCoord, new Vector2I(3, 3), "Typed sync should clamp an out-of-bounds selected coord to the player coord.");
            AssertEq(grid.get_world_size_cells(), new Vector2I(4, 4), "Sync should configure the grid from the active generation config.");
            AssertEq(context.get_active_world_data().Count, context.root_world_data.Count, "Root world data should remain active when no submap is active.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestStaleSubmapIdFallsBackToRootWorld()
    {
        WorldMapDataContext context = new();
        WorldMapGridSystem grid = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["active_submap_id"] = "missing_submap";
            rootWorldData["mounted_submaps"] = new GDictionary();
            context.bind_root_world_data(rootWorldData);

            WorldMapContextSyncResult result = context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                new Vector2I(1, 1),
                new Vector2I(1, 2)
            );

            AssertEq(context.get_active_map_id(), "", "Sync should clear a stale active submap id.");
            AssertEq(rootWorldData["active_submap_id"].AsString(), "", "Sync should write the cleared active submap id back to root world data.");
            AssertEq(result.SelectedCoord, new Vector2I(1, 2), "Sync should keep an in-bounds selected coord after falling back to root world.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private static WorldMapGenerationConfig BuildConfig() =>
        new()
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            player_start_coord = new Vector2I(1, 1),
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
