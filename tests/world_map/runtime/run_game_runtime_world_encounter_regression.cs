using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_world_encounter_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestNearbyEncounterEntriesUseTypedContextData();
        TestWorldStepSurvivesEncounterGrowthPayloadSync();

        RequestTestExit(_test.Finish("Game runtime world encounter regression"));
    }

    private void TestNearbyEncounterEntriesUseTypedContextData()
    {
        GameRuntimeFacade runtime = new();
        WorldMapGridSystem grid = new();
        EncounterAnchorData farAnchor = BuildEncounterAnchor("far_anchor", "Far Anchor", new Vector2I(3, 0));
        EncounterAnchorData nearAnchor = BuildEncounterAnchor("near_anchor", "Near Anchor", new Vector2I(1, 0));
        EncounterAnchorData clearedAnchor = BuildEncounterAnchor("cleared_anchor", "Cleared Anchor", new Vector2I(0, 1), true);
        try
        {
            using GodotProjectionLease<GDictionary> rootWorldDataLease =
                BuildRootWorldDataLease(farAnchor, nearAnchor, clearedAnchor);
            GDictionary rootWorldData = rootWorldDataLease.Value;
            runtime._world_map_data_context.BindRootWorldData(rootWorldData);
            runtime._world_map_data_context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                Vector2I.Zero,
                Vector2I.Zero
            );
            runtime.SetPlayerCoord(Vector2I.Zero);

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

    private void TestWorldStepSurvivesEncounterGrowthPayloadSync()
    {
        GameRuntimeFacade runtime = new();
        WorldMapGridSystem grid = new();
        EncounterAnchorData settlementAnchor = BuildEncounterAnchor(
            "settlement_anchor",
            "Settlement Anchor",
            new Vector2I(0, 0)
        );
        settlementAnchor.encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement);
        settlementAnchor.encounter_profile_id = "wolf_den";
        settlementAnchor.growth_stage = 0;
        try
        {
            using GodotProjectionLease<GDictionary> rootWorldDataLease =
                BuildRootWorldDataLease(settlementAnchor);
            GDictionary rootWorldData = rootWorldDataLease.Value;
            runtime._world_map_data_context.BindRootWorldData(rootWorldData);
            runtime._world_map_data_context.SyncActiveWorldContext(
                BuildConfig(),
                grid,
                Vector2I.Zero,
                Vector2I.Zero
            );
            using WildEncounterRosterDef growthRoster = BuildGrowthRoster();
            InstallWildEncounterRoster(runtime, growthRoster.ToDefinition());
            runtime.SetBattleEncounterDefinitionForTests(
                new BattleEncounterDefinition(
                    "wolf_den",
                    "Wolf Den",
                    "wolf_den",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Suppress,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        3
                    )
                )
            );

            runtime.AdvanceWorldTimeBySteps(1);

            using GodotProjectionLease<GDictionary> activeWorldDataLease =
                runtime._world_map_data_context.GetActiveWorldDataLease();
            GDictionary activeWorldData = activeWorldDataLease.Value;
            _test.Eq(runtime.GetWorldStep(), 1, "Encounter growth sync should keep advanced world_step.");
            _test.Eq(
                DictInt(activeWorldData, "world_step", -1),
                1,
                "Encounter growth sync should keep public world_data world_step."
            );
            GDictionary projectedAnchor = FindEncounterAnchorPayload(
                activeWorldData["encounter_anchors"].AsGodotArray(),
                "settlement_anchor"
            );
            _test.Eq(
                DictInt(projectedAnchor, "growth_stage", -1),
                1,
                "Encounter growth sync should still project the grown settlement encounter stage."
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static GodotProjectionLease<GDictionary> BuildRootWorldDataLease(
        params EncounterAnchorData[] encounterAnchors
    )
    {
        var encounterAnchorPayloads = new List<object>();
        foreach (EncounterAnchorData encounterAnchor in encounterAnchors)
        {
            if (encounterAnchor != null)
                encounterAnchorPayloads.Add(encounterAnchor.BuildSaveSnapshotPlain());
        }
        return RuntimePlainPayload.ProjectDictionaryLease(
            new Dictionary<string, object>(System.StringComparer.Ordinal)
            {
                ["world_step"] = 0,
                ["settlements"] = new List<object>(),
                ["world_npcs"] = new List<object>(),
                ["encounter_anchors"] = encounterAnchorPayloads,
                ["world_events"] = new List<object>(),
            },
            "test.game_runtime_world_encounter.root_world_data",
            LifetimeDomain.Request,
            "test.game_runtime_world_encounter.root_world_data"
        );
    }

    private static WorldGenerationDefinition BuildConfig()
    {
        WorldMapGenerationConfig source = new()
        {
            world_size_in_chunks = new Vector2I(1, 1),
            chunk_size = new Vector2I(4, 4),
            player_start_coord = Vector2I.Zero,
        };
        return TestWorldGenerationDefinitionFactory.Project(
            "res://tests/world_map/runtime/world_encounter_generation.tres",
            source
        );
    }

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
            region_tag = "test",
            vision_range = 2,
            is_cleared = isCleared,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "test_profile",
            growth_stage = isCleared ? 1 : 0,
            suppressed_until_step = 0,
        };

    private static WildEncounterRosterDef BuildGrowthRoster() =>
        new()
        {
            profile_id = "wolf_den",
            display_name = "Wolf Den",
            initial_stage = 0,
            growth_step_interval = 1,
            stages = new Godot.Collections.Array<WildEncounterRosterStageDef>
            {
                new WildEncounterRosterStageDef { stage = 0 },
                new WildEncounterRosterStageDef { stage = 1 },
            },
        };

    private void InstallWildEncounterRoster(
        GameRuntimeFacade runtime,
        WildEncounterRosterDefinition roster
    )
        => runtime.SetWildEncounterRosterDefinitionForTests(roster);

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
