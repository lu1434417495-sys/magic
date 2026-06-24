using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_data_context_regression : SceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedSyncResultClampsInvalidSelectedCoord();
        TestActiveWorldScalarQueries();
        TestSettlementTypedQueries();
        TestSettlementStateMutationUsesPublicWorldDataBoundary();
        TestWorldNpcTypedQueries();
        TestEncounterAnchorTypedQueries();
        TestActiveWorldFogStateRoundTrip();
        TestSubmapEntryAndReturnTypedResults();
        TestEnsureSubmapGeneratedBuildsTypedWorldData();
        TestWorldEventTypedQueries();
        TestStaleSubmapIdFallsBackToRootWorld();

        Quit(_test.Finish("World map data context regression"));
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
            context.BindRootWorldData(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            WorldMapEventData eventData = context.GetWorldEventAt(new Vector2I(1, 1));
            _test.Eq(eventData.EventId.ToString(), "visible_gate", "Typed world event query should expose event_id.");
            _test.Eq(eventData.DisplayName, "Visible Gate", "Typed world event query should expose display_name.");
            _test.Eq(eventData.TargetSubmapId.ToString(), "ash_submap", "Typed world event query should expose target_submap_id.");
            _test.True(eventData.IsTriggerableSubmapEntry, "Discovered enter_submap event with a target should be triggerable.");
            _test.Eq(context.GetDiscoveredWorldEvents().Count, 1, "Typed discovered event query should skip undiscovered events.");
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
            context.BindRootWorldData(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            _test.Eq(context.GetWorldStep(), 42, "Context should expose active world step.");
            _test.Eq(
                context.GetPlayerStartSettlementName(),
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
        EncounterAnchorData farAnchor = BuildEncounterAnchor("far_anchor", "Far Anchor", new Vector2I(3, 0));
        EncounterAnchorData nearAnchor = BuildEncounterAnchor("near_anchor", "Near Anchor", new Vector2I(1, 0));
        EncounterAnchorData clearedAnchor = BuildEncounterAnchor("cleared_anchor", "Cleared Anchor", new Vector2I(2, 0), true);
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["encounter_anchors"] = new GArray
            {
                WorldMapDataProjection.Project(farAnchor),
                WorldMapDataProjection.Project(nearAnchor),
                WorldMapDataProjection.Project(clearedAnchor),
            };
            context.BindRootWorldData(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            List<EncounterAnchorData> allAnchors = context.GetActiveEncounterAnchors();
            List<EncounterAnchorData> activeAnchors = context.GetActiveEncounterAnchors(includeCleared: false);

            _test.Eq(allAnchors.Count, 3, "Typed encounter anchor query should expose active world anchors.");
            _test.Eq(activeAnchors.Count, 2, "Typed encounter anchor query should optionally skip cleared anchors.");
            _test.Eq(
                context.GetEncounterAnchorAt(new Vector2I(1, 0))?.entity_id.ToString(),
                "near_anchor",
                "Typed encounter anchor coord lookup should use the rebuilt context index."
            );
            _test.Eq(
                context.GetEncounterAnchorById("far_anchor")?.world_coord,
                new Vector2I(3, 0),
                "Typed encounter anchor id lookup should resolve active anchors."
            );

            context.RemoveEncounterAnchorById("near_anchor");
            _test.True(
                context.GetEncounterAnchorAt(new Vector2I(1, 0)) == null,
                "Removing an encounter anchor should refresh the typed coord index."
            );
            _test.Eq(context.GetActiveEncounterAnchors().Count, 2, "Removed encounter anchor should leave active world data.");

            EncounterAnchorData far = context.GetEncounterAnchorById("far_anchor");
            far.growth_stage = 4;
            context.SyncActiveWorldPayloadFromTypedState();
            GDictionary projectedFar = FindEncounterAnchorPayload(
                context.active_world_data["encounter_anchors"].AsGodotArray(),
                "far_anchor"
            );
            _test.Eq(
                DictInt(projectedFar, "growth_stage", -1),
                4,
                "Mutating typed encounter anchor state should sync back to public world_data payload."
            );
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
            context.BindRootWorldData(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            WorldMapSettlementData firstCell = context.GetSettlementAt(new Vector2I(1, 1));
            WorldMapSettlementData footprintCell = context.GetSettlementAt(new Vector2I(2, 1));
            WorldMapSettlementData emptyCell = context.GetSettlementAt(new Vector2I(0, 0));

            _test.True(!firstCell.IsEmpty, "Typed settlement query should find settlement origin.");
            _test.Eq(firstCell.SettlementId, "spring_village", "Typed settlement query should expose settlement_id.");
            _test.Eq(firstCell.DisplayName, "Spring Village", "Typed settlement query should expose display_name.");
            _test.Eq(
                footprintCell.SettlementId,
                "spring_village",
                "Typed settlement query should cover the full settlement footprint."
            );
            _test.True(emptyCell.IsEmpty, "Typed settlement query should report empty cells.");
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestSettlementStateMutationUsesPublicWorldDataBoundary()
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
                    ["footprint_size"] = new Vector2I(1, 1),
                    ["settlement_state"] = new GDictionary
                    {
                        ["visited"] = false,
                        ["reputation"] = 0,
                        ["active_conditions"] = new GArray(),
                    },
                },
            };
            context.BindRootWorldData(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            _test.True(
                context.MarkSettlementVisited("spring_village"),
                "MarkSettlementVisited 应通过公开 world_data 边界写回 settlement_state。"
            );
            _test.True(
                context.IsSettlementVisited("spring_village"),
                "typed settlement state query 应反映公开 world_data 写回后的 visited。"
            );

            GDictionary settlementRecord =
                rootWorldData["settlements"].AsGodotArray()[0].AsGodotDictionary();
            GDictionary settlementState = settlementRecord["settlement_state"].AsGodotDictionary();
            _test.True(
                settlementState["visited"].AsBool(),
                "公开 settlement record 投影应返回更新后的 settlement_state。"
            );
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
            context.BindRootWorldData(rootWorldData);
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);

            WorldMapNpcData validNpc = context.GetWorldNpcAt(new Vector2I(1, 1));
            WorldMapNpcData missingNameNpc = context.GetWorldNpcAt(new Vector2I(2, 1));
            WorldMapNpcData numericFactionNpc = context.GetWorldNpcAt(new Vector2I(3, 1));
            WorldMapNpcData blankFactionNpc = context.GetWorldNpcAt(new Vector2I(1, 2));
            WorldMapNpcData emptyNpc = context.GetWorldNpcAt(new Vector2I(0, 0));

            _test.True(!validNpc.IsEmpty, "Typed world NPC query should find an indexed NPC.");
            _test.Eq(validNpc.DisplayName, "Gate Watcher", "Typed world NPC query should trim display_name.");
            _test.Eq(validNpc.FactionId, "neutral", "Typed world NPC query should expose faction_id.");
            _test.True(
                validNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should validate character info fields."
            );
            _test.Eq(
                WorldMapDataProjection.Project(validNpc)["display_name"].AsString(),
                " Gate Watcher ",
                "Typed world NPC query should preserve source data for section builders."
            );
            _test.True(
                !missingNameNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should reject missing display_name."
            );
            _test.True(
                !numericFactionNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should reject non-string faction_id."
            );
            _test.True(
                !blankFactionNpc.HasValidCharacterInfoFields,
                "Typed world NPC query should reject blank faction_id."
            );
            _test.True(emptyNpc.IsEmpty, "Typed world NPC query should report empty cells.");
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
            context.BindRootWorldData(BuildRootWorldData());
            context.SyncActiveWorldContext(BuildConfig(), grid, Vector2I.Zero, Vector2I.Zero);
            fogSystem.Setup(new Vector2I(4, 4));
            fogSystem.RevealDiamond(new Vector2I(1, 1), 1, "player");

            _test.True(context.SaveActiveWorldFogState(fogSystem), "Context should save active fog state into active world data.");
            GDictionary savedFogState = context.GetActiveWorldFogState();
            _test.Eq(
                savedFogState["version"].AsInt32(),
                WorldMapFogSystem.PersistentStateVersion,
                "Context should expose the saved fog state payload."
            );

            restoredFogSystem.Setup(new Vector2I(4, 4), savedFogState);
            _test.True(
                restoredFogSystem.IsExplored(new Vector2I(1, 1), "player"),
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
        SaveSerializer serializer = new();
        WorldMapFogSystem fogSystem = new();
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
            context.BindRootWorldData(rootWorldData);

            WorldMapSubmapEnterResult enterResult = context.EnterSubmap(
                "ash_submap",
                "",
                new Vector2I(1, 1)
            );

            _test.True(enterResult.Ok, "Entering a generated submap should succeed.");
            _test.Eq(enterResult.PlayerCoord, new Vector2I(3, 3), "Submap entry should use saved submap player_coord.");
            _test.Eq(enterResult.TargetDisplayName, "Ash Map", "Submap entry should expose display_name.");
            _test.Eq(rootWorldData["active_submap_id"].AsString(), "ash_submap", "Submap entry should set active_submap_id.");
            GArray returnStack = rootWorldData["submap_return_stack"].AsGodotArray();
            _test.Eq(returnStack.Count, 1, "Submap entry should push one return stack record.");
            GDictionary returnEntry = returnStack[0].AsGodotDictionary();
            _test.Eq(returnEntry["map_id"].AsString(), "", "Submap entry should preserve source map id.");
            _test.Eq(returnEntry["coord"].AsVector2I(), new Vector2I(1, 1), "Submap entry should preserve source coord.");

            context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                enterResult.PlayerCoord,
                enterResult.PlayerCoord
            );
            fogSystem.Setup(new Vector2I(4, 4));
            context.SaveActiveWorldFogState(fogSystem);
            _test.Eq(
                serializer.GetWorldDataNestedSchemaValidationError(rootWorldData),
                "",
                "子地图进入并写回后 mounted_submap entry 应继续满足正式 save schema。"
            );
            WorldMapSubmapReturnResult returnResult = context.ReturnFromActiveSubmap(new Vector2I(4, 4));

            _test.True(returnResult.Ok, "Returning from an active submap should succeed.");
            _test.Eq(returnResult.TargetMapId, "", "Submap return should restore the source map id.");
            _test.Eq(returnResult.PlayerCoord, new Vector2I(1, 1), "Submap return should restore the source coord.");
            _test.Eq(rootWorldData["active_submap_id"].AsString(), "", "Submap return should clear active_submap_id.");
            _test.Eq(rootWorldData["submap_return_stack"].AsGodotArray().Count, 0, "Submap return should pop the return stack.");
            GDictionary storedSubmap = rootWorldData["mounted_submaps"].AsGodotDictionary()["ash_submap"].AsGodotDictionary();
            _test.Eq(
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

    private void TestEnsureSubmapGeneratedBuildsTypedWorldData()
    {
        WorldMapDataContext context = new();
        try
        {
            GDictionary rootWorldData = BuildRootWorldData();
            rootWorldData["mounted_submaps"] = new GDictionary
            {
                ["generated_submap"] = new GDictionary
                {
                    ["display_name"] = "Generated Map",
                    ["generation_config_path"] = TestWorldConfig,
                    ["return_hint_text"] = "Return later",
                    ["is_generated"] = false,
                    ["player_coord"] = new Vector2I(-1, -1),
                    ["world_data"] = new GDictionary(),
                },
            };
            context.BindRootWorldData(rootWorldData);

            _test.True(
                context.EnsureSubmapGenerated("generated_submap"),
                "ensure_submap_generated 应成功构建 typed 子地图 world_data。"
            );

            GDictionary generatedEntry = rootWorldData["mounted_submaps"]
                .AsGodotDictionary()["generated_submap"]
                .AsGodotDictionary();
            GDictionary generatedWorldData = generatedEntry["world_data"].AsGodotDictionary();
            _test.True(
                generatedEntry["is_generated"].AsBool(),
                "生成后的 mounted submap 应标记 is_generated=true。"
            );
            _test.True(
                generatedWorldData["settlements"].AsGodotArray().Count > 0,
                "typed 子地图 world_data 应包含 settlement 投影。"
            );
            _test.Eq(
                generatedEntry["player_coord"].AsVector2I(),
                generatedWorldData["player_start_coord"].AsVector2I(),
                "子地图 player_coord 应来自 typed WorldBuildData.PlayerStartCoord。"
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
            context.BindRootWorldData(BuildRootWorldData());
            WorldMapContextSyncResult result = context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                new Vector2I(3, 3),
                new Vector2I(99, 99)
            );

            _test.Eq(result.PlayerCoord, new Vector2I(3, 3), "Typed sync should keep an in-bounds player coord.");
            _test.Eq(result.SelectedCoord, new Vector2I(3, 3), "Typed sync should clamp an out-of-bounds selected coord to the player coord.");
            _test.Eq(grid.GetWorldSizeCells(), new Vector2I(4, 4), "Sync should configure the grid from the active generation config.");
            _test.Eq(context.GetActiveWorldData().Count, context.root_world_data.Count, "Root world data should remain active when no submap is active.");
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
            context.BindRootWorldData(rootWorldData);

            WorldMapContextSyncResult result = context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                new Vector2I(1, 1),
                new Vector2I(1, 2)
            );

            _test.Eq(context.GetActiveMapId(), "", "Sync should clear a stale active submap id.");
            _test.Eq(rootWorldData["active_submap_id"].AsString(), "", "Sync should write the cleared active submap id back to root world data.");
            _test.Eq(result.SelectedCoord, new Vector2I(1, 2), "Sync should keep an in-bounds selected coord after falling back to root world.");
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
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "test_profile",
            growth_stage = isCleared ? 1 : 0,
            suppressed_until_step = 0,
        };

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Int
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private static GDictionary FindEncounterAnchorPayload(GArray values, string entityId)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary payload = value.AsGodotDictionary();
            if (
                payload.ContainsKey("entity_id")
                && payload["entity_id"].VariantType == Variant.Type.String
                && payload["entity_id"].AsString() == entityId
            )
                return payload;
        }
        return new GDictionary();
    }
}
