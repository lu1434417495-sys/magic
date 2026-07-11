using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_action_request_boundary_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        await TestClientPayloadCannotInjectSettlementRewardsOrSuppressQuestProgress();
        RequestTestExit(_test.Finish("Settlement action request boundary regression"));
    }

    private async Task TestClientPayloadCannotInjectSettlementRewardsOrSuppressQuestProgress()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture();
        try
        {
            GameRuntimeFacade runtime = fixture.Runtime;
            var questState = new QuestState { quest_id = "contract_training" };
            questState.MarkAccepted(runtime.GetWorldStep());
            runtime._party_state.SetActiveQuestState(questState);
            runtime._character_management.SetPartyState(runtime._party_state);

            GameRuntimeFacade.RuntimeCommandResult result =
                runtime.CommandExecuteSettlementActionTyped(
                    "service:training",
                    new GDictionary
                    {
                        ["member_id"] = "hero",
                        ["emit_default_quest_progress_event"] = false,
                        ["pending_character_rewards"] = new GArray
                        {
                            BuildInjectedRewardPayload(),
                        },
                    }
                );

            _test.True(result.Ok, $"baseline settlement action should still execute. message={result.Message}");
            _test.False(
                HasPendingRewardSource(runtime._party_state, "client_injected_reward"),
                "client payload must not inject settlement pending rewards."
            );
            _test.True(
                runtime._party_state.HasClaimableQuest("contract_training"),
                "client payload must not suppress server-generated settlement quest progress."
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildQuestDefs();
        GameSession gameSession = await InstallGameSession(
            "SettlementActionBoundaryGameSession",
            questDefs
        );
        PartyState partyState = BuildPartyState();
        GDictionary worldData = BuildWorldData(
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    BuildSettlementServices()
                ),
            }
        );
        ConfigureSessionForRuntimeTest(gameSession, worldData, partyState);
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs = gameSession.GetItemDefsTyped();

        var runtime = new GameRuntimeFacade
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _player_faction_id = "player",
        };
        runtime.SetActiveSettlementId("spring_village_01");
        runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
        runtime._world_map_data_context.BindRootWorldData(worldData);
        var contextGrid = new WorldMapGridSystem();
        runtime._world_map_data_context.SyncActiveWorldContext(
            gameSession._generation_definition,
            contextGrid,
            Vector2I.Zero,
            Vector2I.Zero
        );
        runtime._fog_system.Setup(new Vector2I(8, 8));
        MakeVisible(runtime, Vector2I.Zero);
        runtime._character_management.setup(
            partyState,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetProfessionDefsTyped(),
            gameSession.GetAchievementDefsTyped(),
            itemDefs,
            gameSession.GetQuestDefsTyped(),
            gameSession.AllocateEquipmentInstanceId,
            gameSession.GetProgressionIdentityCatalogTyped()
        );
        runtime._party_warehouse_service.Setup(
            partyState,
            itemDefs,
            gameSession.AllocateEquipmentInstanceId
        );
        runtime._party_item_use_service.Setup(
            partyState,
            itemDefs,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            runtime._party_warehouse_service,
            runtime._character_management
        );
        runtime._party_equipment_service.Setup(
            partyState,
            itemDefs,
            runtime._party_warehouse_service,
            gameSession.AllocateEquipmentInstanceId
        );
        runtime._settlement_command_handler.SetupRuntime(runtime);
        runtime._warehouse_handler.Setup(runtime);
        runtime._quest_command_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);

        return new RuntimeFixture(runtime, gameSession);
    }

    private static void ConfigureSessionForRuntimeTest(
        GameSession gameSession,
        GDictionary worldData,
        PartyState partyState
    )
    {
        gameSession.ConfigureRuntimeWorldForTests(
            "settlement_action_boundary",
            TestConfigPath,
            worldData,
            partyState,
            "settlement_action_boundary_test",
            "Settlement Action Boundary Test",
            new Vector2I(8, 8),
            TestWorldGenerationDefinitionFactory.Load(TestConfigPath)
        );
    }

    private async Task<GameSession> InstallGameSession(
        string nodeName,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == nodeName)
            {
                child.QueueFree();
            }
        }
        await ToSignal(this, SignalName.ProcessFrame);
        GameSession gameSession = GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed => seed.Quests = questDefs
        );
        gameSession.Name = nodeName;
        Root.AddChild(gameSession);
        await ToSignal(this, SignalName.ProcessFrame);
        return gameSession;
    }

    private async Task DisposeFixture(RuntimeFixture fixture)
    {
        fixture.GameSession?.DiscardPendingSave();
        fixture.Runtime?.Dispose();
        if (fixture.GameSession != null)
        {
            _test.Eq(
                fixture.GameSession.ClearPersistedGame(),
                (int)Error.Ok,
                "清理 settlement action boundary 验证存档应成功。"
            );
            fixture.GameSession.QueueFree();
            await ToSignal(this, SignalName.ProcessFrame);
        }
    }

    private static GDictionary BuildWorldData(IReadOnlyList<GDictionary> settlements)
    {
        var settlementArray = new GArray();
        foreach (GDictionary settlement in settlements)
        {
            settlementArray.Add(settlement);
        }
        return new GDictionary
        {
            ["map_seed"] = 1,
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = settlementArray,
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["resource_nodes"] = new GArray(),
            ["mounted_submaps"] = new GDictionary(),
            ["world_npcs"] = new GArray(),
            ["player_start_coord"] = Vector2I.Zero,
            ["player_start_settlement_id"] = "spring_village_01",
            ["player_start_settlement_name"] = "春泉村",
            ["fog_states"] = new GDictionary(),
        };
    }

    private static GDictionary BuildSettlementRecord(
        string settlementId,
        string displayName,
        Vector2I origin,
        GArray services
    )
    {
        return new GDictionary
        {
            ["entity_id"] = $"settlement_{settlementId}",
            ["template_id"] = $"template_{settlementId}",
            ["settlement_id"] = settlementId,
            ["display_name"] = displayName,
            ["tier"] = 1,
            ["tier_name"] = "村镇",
            ["faction_id"] = "neutral",
            ["origin"] = origin,
            ["footprint_size"] = Vector2I.One,
            ["facilities"] = new GArray(),
            ["service_npcs"] = new GArray(),
            ["available_services"] = services,
            ["is_player_start"] = origin == Vector2I.Zero,
            ["settlement_state"] = new GDictionary
            {
                ["visited"] = true,
                ["reputation"] = 0,
                ["active_conditions"] = new GArray(),
                ["cooldowns"] = new GDictionary(),
                ["shop_inventory_seed"] = 0,
                ["shop_last_refresh_step"] = 0,
                ["shop_states"] = new GDictionary(),
            },
        };
    }

    private static GArray BuildSettlementServices()
    {
        return new GArray
        {
            new GDictionary
            {
                ["action_id"] = "service:training",
                ["facility_name"] = "训练场",
                ["npc_name"] = "教官",
                ["service_type"] = "训练",
                ["interaction_script_id"] = "training_service",
            },
        };
    }

    private static IReadOnlyDictionary<StringName, QuestDefinition> BuildQuestDefs()
    {
        QuestDefinition quest = new(
            "contract_training",
            "训练追踪",
            "据点训练进度测试。",
            "service_training_hidden",
            System.Array.Empty<StringName>(),
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            new QuestObjectiveDefinition[]
            {
                new("train_once", "settlement_action", "service:training", 1),
            },
            new QuestRewardDefinition[]
            {
                new(
                    "gold",
                    1,
                    "",
                    0,
                    "",
                    System.Array.Empty<QuestPendingRewardEntryDefinition>()
                ),
            },
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            "",
            "",
            ""
        );
        return new Dictionary<StringName, QuestDefinition> { [quest.QuestId] = quest };
    }

    private static bool HasPendingRewardSource(PartyState partyState, StringName sourceId)
    {
        if (partyState == null || sourceId == "")
            return false;
        foreach (PendingCharacterReward reward in partyState.pending_character_rewards)
        {
            if (reward != null && reward.source_id == sourceId)
                return true;
        }
        return false;
    }

    private static GDictionary BuildInjectedRewardPayload()
    {
        return new GDictionary
        {
            ["member_id"] = "hero",
            ["source_type"] = "training",
            ["source_id"] = "client_injected_reward",
            ["source_label"] = "客户端注入",
            ["entries"] = new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = "skill_mastery",
                    ["target_id"] = "warrior_heavy_strike",
                    ["amount"] = 1,
                },
            },
        };
    }

    private static PartyState BuildPartyState()
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            gold = 100,
        };
        var hero = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            current_hp = 20,
            current_mp = 4,
            progression = new UnitProgress
            {
                unit_id = "hero",
                display_name = "Hero",
                unit_base_attributes = new UnitBaseAttributes(),
            },
        };
        hero.progression.unit_base_attributes.custom_stats["storage_space"] = 12;
        hero.progression.unit_base_attributes.custom_stats["hp_max"] = 40;
        hero.progression.unit_base_attributes.custom_stats["mp_max"] = 12;
        partyState.SetMemberState(hero);
        return partyState;
    }

    private static void MakeVisible(GameRuntimeFacade runtime, Vector2I center)
    {
        runtime._fog_system.RebuildVisibilityForFaction(
            "player",
            new[] { new VisionSourceData("settlement_action_boundary_visibility", center, 6, "player") }
        );
    }

    private readonly record struct RuntimeFixture(
        GameRuntimeFacade Runtime,
        GameSession GameSession
    );
}
