using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GEquipmentInstanceArray = Godot.Collections.Array<EquipmentInstanceState>;
using GPendingRewardArray = Godot.Collections.Array<PendingCharacterReward>;
using GQuestStateArray = Godot.Collections.Array<QuestState>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GTraitInstanceArray = Godot.Collections.Array<TraitInstanceState>;
using GWarehouseStackArray = Godot.Collections.Array<WarehouseStackState>;

public partial class run_settlement_action_request_boundary_regression : SceneTree
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
        Quit(_test.Finish("Settlement action request boundary regression"));
    }

    private async Task TestClientPayloadCannotInjectSettlementRewardsOrSuppressQuestProgress()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture();
        GDictionary commandPayload = fixture.Payloads.Dictionary();
        GArray pendingRewards = fixture.Payloads.Array();
        pendingRewards.Add(BuildInjectedRewardPayload(fixture.Payloads));
        commandPayload["member_id"] = "hero";
        commandPayload["emit_default_quest_progress_event"] = false;
        commandPayload["pending_character_rewards"] = pendingRewards;
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
                    commandPayload
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
        var payloads = new OwnedGodotPayloads();
        GameSession gameSession = await InstallGameSession("SettlementActionBoundaryGameSession");
        PartyState partyState = BuildPartyState(payloads);
        GDictionary worldData = BuildWorldData(
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    BuildSettlementServices(payloads),
                    payloads
                ),
            },
            payloads
        );
        ConfigureSessionForRuntimeTest(gameSession, worldData, partyState, BuildQuestDefs(payloads));
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = gameSession.GetItemDefsTyped();

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
            gameSession._generation_config,
            contextGrid,
            Vector2I.Zero,
            Vector2I.Zero
        );
        runtime._fog_system.Setup(new Vector2I(8, 8));
        MakeVisible(runtime, Vector2I.Zero);
        runtime._character_management.setup(
            partyState,
            gameSession.GetSkillDefsTyped(),
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
            gameSession.GetSkillDefsTyped(),
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

        return new RuntimeFixture(runtime, gameSession, payloads);
    }

    private static void ConfigureSessionForRuntimeTest(
        GameSession gameSession,
        GDictionary worldData,
        PartyState partyState,
        GDictionary questDefs
    )
    {
        gameSession.ConfigureRuntimeWorldForTests(
            "settlement_action_boundary",
            TestConfigPath,
            worldData,
            partyState,
            questDefs,
            "settlement_action_boundary_test",
            "Settlement Action Boundary Test",
            new Vector2I(8, 8)
        );
    }

    private async Task<GameSession> InstallGameSession(string nodeName)
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == nodeName)
            {
                child.QueueFree();
            }
        }
        await ToSignal(this, SignalName.ProcessFrame);
        var gameSession = new GameSession { Name = nodeName };
        Root.AddChild(gameSession);
        await ToSignal(this, SignalName.ProcessFrame);
        return gameSession;
    }

    private async Task DisposeFixture(RuntimeFixture fixture)
    {
        try
        {
            fixture.Runtime?.Dispose();
            if (fixture.GameSession != null)
            {
                _test.Eq(
                    fixture.GameSession.ClearPersistedGame(),
                    (int)Error.Ok,
                    "清理 settlement action boundary 验证存档应成功。"
                );
                fixture.GameSession.Dispose();
                await ToSignal(this, SignalName.ProcessFrame);
            }
        }
        finally
        {
            fixture.Payloads?.Dispose();
            GodotSharpCleanup.CollectPendingFinalizers();
        }
    }

    private static GDictionary BuildWorldData(
        IReadOnlyList<GDictionary> settlements,
        OwnedGodotPayloads payloads
    )
    {
        GDictionary worldData = payloads.Dictionary();
        var settlementArray = payloads.Array();
        foreach (GDictionary settlement in settlements)
        {
            settlementArray.Add(settlement);
        }
        worldData["map_seed"] = 1;
        worldData["world_step"] = 0;
        worldData["next_equipment_instance_serial"] = 1;
        worldData["active_submap_id"] = "";
        worldData["submap_return_stack"] = payloads.Array();
        worldData["settlements"] = settlementArray;
        worldData["world_events"] = payloads.Array();
        worldData["encounter_anchors"] = payloads.Array();
        worldData["mounted_submaps"] = payloads.Dictionary();
        worldData["world_npcs"] = payloads.Array();
        worldData["player_start_coord"] = Vector2I.Zero;
        worldData["player_start_settlement_id"] = "spring_village_01";
        worldData["player_start_settlement_name"] = "春泉村";
        worldData["fog_states"] = payloads.Dictionary();
        return worldData;
    }

    private static GDictionary BuildSettlementRecord(
        string settlementId,
        string displayName,
        Vector2I origin,
        GArray services,
        OwnedGodotPayloads payloads
    )
    {
        GDictionary settlement = payloads.Dictionary();
        GDictionary state = payloads.Dictionary();
        state["visited"] = true;
        state["reputation"] = 0;
        state["active_conditions"] = payloads.Array();
        state["cooldowns"] = payloads.Dictionary();
        state["shop_inventory_seed"] = 0;
        state["shop_last_refresh_step"] = 0;
        state["shop_states"] = payloads.Dictionary();

        settlement["entity_id"] = $"settlement_{settlementId}";
        settlement["template_id"] = $"template_{settlementId}";
        settlement["settlement_id"] = settlementId;
        settlement["display_name"] = displayName;
        settlement["tier"] = 1;
        settlement["tier_name"] = "村镇";
        settlement["faction_id"] = "neutral";
        settlement["origin"] = origin;
        settlement["footprint_size"] = Vector2I.One;
        settlement["facilities"] = payloads.Array();
        settlement["service_npcs"] = payloads.Array();
        settlement["available_services"] = services;
        settlement["is_player_start"] = origin == Vector2I.Zero;
        settlement["settlement_state"] = state;
        return settlement;
    }

    private static GArray BuildSettlementServices(OwnedGodotPayloads payloads)
    {
        GArray services = payloads.Array();
        GDictionary service = payloads.Dictionary();
        service["action_id"] = "service:training";
        service["facility_name"] = "训练场";
        service["npc_name"] = "教官";
        service["service_type"] = "训练";
        service["interaction_script_id"] = "training_service";
        services.Add(service);
        return services;
    }

    private static GDictionary BuildQuestDefs(OwnedGodotPayloads payloads)
    {
        GDictionary questDefs = payloads.Dictionary();
        QuestDef quest = payloads.TrackObject(
            new QuestDef
            {
                quest_id = "contract_training",
                display_name = "训练追踪",
                description = "据点训练进度测试。",
                provider_interaction_id = "service_training_hidden",
            }
        );
        payloads.Track(quest.tags);
        payloads.Track(quest.accept_requirements);
        payloads.Track(quest.objective_defs);
        payloads.Track(quest.reward_entries);

        GDictionary objective = payloads.Dictionary();
        objective["objective_id"] = "train_once";
        objective["objective_type"] = "settlement_action";
        objective["target_id"] = "service:training";
        objective["target_value"] = 1;
        quest.objective_defs.Add(objective);

        GDictionary reward = payloads.Dictionary();
        reward["reward_type"] = "gold";
        reward["amount"] = 1;
        quest.reward_entries.Add(reward);

        questDefs[quest.quest_id] = quest;
        return questDefs;
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

    private static GDictionary BuildInjectedRewardPayload(OwnedGodotPayloads payloads)
    {
        GDictionary reward = payloads.Dictionary();
        GArray entries = payloads.Array();
        GDictionary entry = payloads.Dictionary();
        entry["entry_type"] = "skill_mastery";
        entry["target_id"] = "warrior_heavy_strike";
        entry["amount"] = 1;
        entries.Add(entry);

        reward["member_id"] = "hero";
        reward["source_type"] = "training";
        reward["source_id"] = "client_injected_reward";
        reward["source_label"] = "客户端注入";
        reward["entries"] = entries;
        return reward;
    }

    private static PartyState BuildPartyState(OwnedGodotPayloads payloads)
    {
        var partyState = new PartyState();
        TrackPartyStateCollections(payloads, partyState);
        partyState.leader_member_id = "hero";
        partyState.main_character_member_id = "hero";
        partyState.active_member_ids.Add("hero");
        partyState.gold = 100;

        var hero = new PartyMemberState();
        TrackPartyMemberCollections(payloads, hero);
        hero.member_id = "hero";
        hero.display_name = "Hero";
        hero.current_hp = 20;
        hero.current_mp = 4;
        hero.progression.unit_id = "hero";
        hero.progression.display_name = "Hero";
        hero.progression.unit_base_attributes.custom_stats["storage_space"] = 12;
        hero.progression.unit_base_attributes.custom_stats["hp_max"] = 40;
        hero.progression.unit_base_attributes.custom_stats["mp_max"] = 12;
        partyState.SetMemberState(hero);
        return partyState;
    }

    private static void TrackPartyStateCollections(OwnedGodotPayloads payloads, PartyState partyState)
    {
        payloads.Track(partyState.active_member_ids);
        payloads.Track(partyState.reserve_member_ids);
        payloads.Track(partyState.pending_character_rewards);
        payloads.Track(partyState.active_quests);
        payloads.Track(partyState.claimable_quests);
        payloads.Track(partyState.completed_quest_ids);
        payloads.Track(partyState.warehouse_state.stacks);
        payloads.Track(partyState.warehouse_state.equipment_instances);
    }

    private static void TrackPartyMemberCollections(
        OwnedGodotPayloads payloads,
        PartyMemberState member
    )
    {
        payloads.Track(member.trait_instances);
        payloads.Track(member.active_stage_advancement_modifier_ids);
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
        GameSession GameSession,
        OwnedGodotPayloads Payloads
    );

    private sealed class OwnedGodotPayloads : IDisposable
    {
        private readonly List<object> _collections = new();
        private readonly List<GodotObject> _objects = new();

        internal GDictionary Dictionary() => Track(new GDictionary());

        internal GArray Array() => Track(new GArray());

        internal T Track<T>(T value)
        {
            if (value != null)
                _collections.Add(value);
            return value;
        }

        internal T TrackObject<T>(T value)
            where T : GodotObject
        {
            if (value != null)
                _objects.Add(value);
            return value;
        }

        public void Dispose()
        {
            for (int index = _collections.Count - 1; index >= 0; index--)
                DisposeCollection(_collections[index]);
            _collections.Clear();

            for (int index = _objects.Count - 1; index >= 0; index--)
                BattleTestFixture.DisposeFixtureObject(_objects[index]);
            _objects.Clear();
        }

        private static void DisposeCollection(object value)
        {
            switch (value)
            {
                case null:
                    return;
                case GDictionary dictionary:
                    dictionary.Clear();
                    GC.SuppressFinalize(dictionary);
                    dictionary.Dispose();
                    return;
                case GArray array:
                    array.Clear();
                    GC.SuppressFinalize(array);
                    array.Dispose();
                    return;
                case GStringNameArray array:
                    DisposeTypedArray(array);
                    return;
                case GDictionaryArray array:
                    DisposeTypedArray(array);
                    return;
                case GPendingRewardArray array:
                    DisposeTypedArray(array);
                    return;
                case GQuestStateArray array:
                    DisposeTypedArray(array);
                    return;
                case GWarehouseStackArray array:
                    DisposeTypedArray(array);
                    return;
                case GEquipmentInstanceArray array:
                    DisposeTypedArray(array);
                    return;
                case GTraitInstanceArray array:
                    DisposeTypedArray(array);
                    return;
            }
        }

        private static void DisposeTypedArray<[MustBeVariant] T>(Godot.Collections.Array<T> array)
        {
            if (array == null)
                return;
            array.Clear();
            GC.SuppressFinalize(array);
            GArray rawArray = (GArray)array;
            rawArray.Dispose();
        }
    }
}
