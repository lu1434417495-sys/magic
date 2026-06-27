using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_forge_service_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";
    private const string AshenIntersectionConfigPath = "res://data/configs/world_map/ashen_intersection_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        TestMasterReforgeServiceSuccess();
        TestMasterReforgeServiceMissingMaterials();
        await TestSettlementHandlerRoutesMasterReforge();
        await TestSettlementHandlerRoutesGenericForge();
        await TestNewWorldGenerationExposesMasterReforgeService();
        await TestAshenIntersectionGenerationExposesGenericForgeService();

        Quit(_test.Finish("Settlement forge service regression"));
    }

    private void TestMasterReforgeServiceSuccess()
    {
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = LoadItemDefs();
        PartyState partyState = BuildPartyState(6);
        var warehouseService = new PartyWarehouseService();
        warehouseService.Setup(partyState, itemDefs);
        warehouseService.AddItemTyped("bronze_sword", 1);
        warehouseService.AddItemTyped("iron_ore", 2);
        WarehouseBatchSwapResult preflight =
            warehouseService.PreviewBatchSwapEntriesTyped(
                new GArray
                {
                    new GDictionary { ["item_id"] = new StringName("bronze_sword") },
                    new GDictionary { ["item_id"] = new StringName("iron_ore") },
                    new GDictionary { ["item_id"] = new StringName("iron_ore") },
                },
                new GArray
                {
                    new GDictionary { ["item_id"] = new StringName("iron_greatsword") },
                }
            );
        _test.True(
            preflight.Allowed,
            $"重铸前置 batch swap 应允许装备输入和装备产出。error={preflight.ErrorCode} item={preflight.BlockedItemId} instance={preflight.BlockedInstanceId}"
        );

        var forgeService = new SettlementForgeService();
        SettlementServiceResult result = forgeService.ExecuteRecipeResultTyped(
            BuildSettlementRecord(),
            BuildReforgePayload(),
            itemDefs,
            default(IReadOnlyDictionary<StringName, RecipeDef>),
            warehouseService,
            partyState,
            new[]
            {
                QuestProgressService.QuestProgressEventData.FromDictionary(
                    new GDictionary
                    {
                        ["event_type"] = "progress",
                        ["quest_id"] = "forge_trial",
                        ["objective_id"] = "reforge_once",
                        ["progress_delta"] = 1,
                        ["target_value"] = 1,
                        ["world_step"] = 1,
                    }
                ),
            }
        );

        _test.True(result.Success, $"重铸服务成功路径应返回 success=true。message={result.Message}");
        _test.True(result.PersistPartyState, "重铸成功后应要求持久化队伍状态。");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 0, "重铸成功后应消耗青铜短剑。");
        _test.Eq(warehouseService.CountItem("iron_ore"), 0, "重铸成功后应消耗两份铁矿石。");
        _test.Eq(warehouseService.CountItem("iron_greatsword"), 1, "重铸成功后应产出铁制大剑。");
        _test.True(!string.IsNullOrEmpty(result.Message), "重铸成功应返回反馈信息。");
        _test.Eq(
            DictString(SettlementServiceResultProjection.ProjectInventoryDelta(result), "recipe_id", ""),
            "master_reforge_iron_greatsword",
            "inventory_delta 应记录 recipe_id。"
        );
        _test.Eq(result.QuestProgressEvents.Count, 1, "重铸服务应保留调用方传入的 quest_progress_events。");
    }

    private void TestMasterReforgeServiceMissingMaterials()
    {
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = LoadItemDefs();
        PartyState partyState = BuildPartyState(6);
        var warehouseService = new PartyWarehouseService();
        warehouseService.Setup(partyState, itemDefs);
        warehouseService.AddItemTyped("bronze_sword", 1);

        var forgeService = new SettlementForgeService();
        SettlementServiceResult result = forgeService.ExecuteRecipeResultTyped(
            BuildSettlementRecord(),
            BuildReforgePayload(),
            itemDefs,
            default(IReadOnlyDictionary<StringName, RecipeDef>),
            warehouseService,
            partyState
        );

        _test.False(result.Success, "缺少材料时重铸服务应失败。");
        _test.False(result.PersistPartyState, "重铸失败时不应要求持久化队伍状态。");
        _test.True(!string.IsNullOrEmpty(result.Message), "缺少材料时应返回失败信息。");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 1, "失败时不应吞掉已有材料。");
        _test.Eq(warehouseService.CountItem("iron_greatsword"), 0, "失败时不应提前写入产物。");
    }

    private async Task TestSettlementHandlerRoutesMasterReforge()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture("master", BuildPartyState(6), BuildSettlementRecord(true));
        try
        {
            fixture.WarehouseService.AddItemTyped("bronze_sword", 1);
            fixture.WarehouseService.AddItemTyped("iron_ore", 2);

            GDictionary windowData = fixture.Handler.GetSettlementWindowData("forge_town");
            GDictionary reforgeEntry = FindServiceEntry(DictArray(windowData, "available_services"), "service_master_reforge");
            _test.True(reforgeEntry.Count > 0, "据点窗口应暴露 service_master_reforge 服务入口。");
            _test.True(DictBool(reforgeEntry, "is_enabled", false), "存在可执行配方时，大师重铸入口应可用。");
            _test.Eq(DictString(reforgeEntry, "cost_label", ""), "按配方材料", "大师重铸入口应显示按配方材料计价。");

            GameRuntimeFacade.RuntimeCommandResult openResult =
                fixture.Handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:master_reforge",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "service:master_reforge 首次触发应成功打开 forge modal。");
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Forge, "首次点击大师重铸服务后应切换到 forge modal。");
            _test.True(fixture.Handler.GetForgeWindowData().Count > 0, "打开 forge modal 后应能读取 forge window data。");
            _test.True(DictArray(fixture.Handler.GetForgeWindowData(), "entries").Count > 0, "forge window data 应暴露可选配方。");
            _test.Eq(fixture.WarehouseService.CountItem("iron_greatsword"), 0, "仅打开 forge modal 时不应提前产出铁制大剑。");

            GameRuntimeFacade.RuntimeCommandResult commandResult =
                fixture.Handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:master_reforge",
                    new GDictionary
                    {
                        ["submission_source"] = "forge",
                        ["recipe_id"] = "master_reforge_iron_greatsword",
                    }
                );
            _test.True(
                commandResult.Ok,
                $"forge modal 提交配方后应成功执行重铸。message={commandResult.Message}"
            );
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Forge, "执行重铸后应继续停留在 forge modal。");
            _test.Eq(fixture.WarehouseService.CountItem("iron_greatsword"), 1, "通过 handler 执行后应真正产出铁制大剑。");
            _test.False(fixture.GameSession.HasPendingSave(), "重铸成功后应提交队伍状态持久化。");
            _test.True(fixture.Runtime.GetPartyState() == fixture.Runtime._character_management.GetPartyState(), "重铸成功后应同步角色管理侧队伍状态。");
            _test.True(!string.IsNullOrEmpty(fixture.Runtime._active_settlement_feedback_text), "handler 应把重铸反馈写入据点窗口。");
            _test.True(!string.IsNullOrEmpty(fixture.Runtime._current_status_message), "handler 应刷新重铸完成状态。");

            fixture.Handler.OnForgeWindowClosed();
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Settlement, "关闭 forge modal 后应返回 settlement modal。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestSettlementHandlerRoutesGenericForge()
    {
        PartyState partyState = BuildPartyState(10);
        AddMemberToParty(partyState, "mage", "Mage");
        RuntimeFixture fixture = await BuildRuntimeFixture("generic", partyState, BuildSettlementRecord(true, true));
        try
        {
            fixture.WarehouseService.AddItemTyped("iron_ore", 2);
            fixture.WarehouseService.AddItemTyped("hardwood_lumber", 1);
            fixture.WarehouseService.AddItemTyped("whetstone", 1);
            fixture.WarehouseService.AddItemTyped("forge_coal", 1);

            GDictionary windowData = fixture.Handler.GetSettlementWindowData("forge_town");
            GDictionary genericEntry = FindServiceEntry(DictArray(windowData, "available_services"), "service_repair_gear");
            _test.True(genericEntry.Count > 0, "据点窗口应暴露通用 forge 服务入口。");
            _test.True(DictBool(genericEntry, "is_enabled", false), "存在通用 forge 配方时，service_repair_gear 应可用。");
            _test.Eq(DictString(genericEntry, "cost_label", ""), "按配方材料", "通用 forge 入口应显示按配方材料计价。");

            GameRuntimeFacade.RuntimeCommandResult openResult =
                fixture.Handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:repair_gear",
                    new GDictionary { ["member_id"] = "mage" }
                );
            _test.True(
                openResult.Ok,
                $"service:repair_gear 首次触发应成功打开 forge modal。message={openResult.Message}"
            );
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Forge, "首次点击通用 forge 服务后应切换到 forge modal。");
            GDictionary forgeWindowData = fixture.Handler.GetForgeWindowData();
            _test.Eq(DictString(forgeWindowData, "action_id", ""), "service:repair_gear", "通用 forge modal 应保留原始 action_id。");
            _test.Eq(DictString(forgeWindowData, "default_member_id", ""), "mage", "通用 forge modal 应保留据点窗口选择的默认成员。");
            _test.Eq(DictString(forgeWindowData, "selected_member_id", ""), "mage", "通用 forge modal 应保留据点窗口选择的当前成员。");
            _test.True(!string.IsNullOrEmpty(DictString(forgeWindowData, "title", "")), "通用 forge modal 应提供标题。");
            GArray forgeEntries = DictArray(forgeWindowData, "entries");
            _test.True(forgeEntries.Count > 0, "通用 forge window data 应暴露可选配方。");
            HashSet<string> recipeIds = CollectRecipeIds(forgeEntries);
            _test.True(recipeIds.Contains("forge_smith_iron_greatsword"), "通用 forge modal 应继续暴露铁制大剑配方。");
            _test.True(recipeIds.Contains("forge_militia_axe"), "通用 forge modal 应暴露民兵手斧配方。");
            _test.True(recipeIds.Contains("forge_watchman_mace"), "通用 forge modal 应暴露卫兵钉锤配方。");

            GameRuntimeFacade.RuntimeCommandResult commandResult =
                fixture.Handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:repair_gear",
                    new GDictionary
                    {
                        ["submission_source"] = "forge",
                        ["member_id"] = DictString(forgeWindowData, "selected_member_id", ""),
                        ["recipe_id"] = "forge_militia_axe",
                    }
                );
            _test.True(
                commandResult.Ok,
                $"forge modal 提交通用配方后应成功执行锻造。message={commandResult.Message}"
            );
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Forge, "执行通用 forge 后应继续停留在 forge modal。");
            _test.Eq(fixture.WarehouseService.CountItem("iron_ore"), 1, "通用 forge 成功后应按配方扣除铁矿石。");
            _test.Eq(fixture.WarehouseService.CountItem("hardwood_lumber"), 0, "通用 forge 成功后应消耗硬木板。");
            _test.Eq(fixture.WarehouseService.CountItem("whetstone"), 0, "通用 forge 成功后应消耗磨刃石。");
            _test.Eq(fixture.WarehouseService.CountItem("militia_axe"), 1, "通用 forge 成功后应真正产出民兵手斧。");
            _test.False(fixture.GameSession.HasPendingSave(), "通用 forge 成功后应提交队伍状态持久化。");
            _test.True(fixture.Runtime.GetPartyState() == fixture.Runtime._character_management.GetPartyState(), "通用 forge 成功后应同步角色管理侧队伍状态。");
            _test.True(!string.IsNullOrEmpty(fixture.Runtime._active_settlement_feedback_text), "handler 应把通用 forge 反馈写入据点窗口。");
            _test.True(!string.IsNullOrEmpty(fixture.Runtime._current_status_message), "handler 应刷新通用 forge 完成状态。");

            fixture.Handler.OnForgeWindowClosed();
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Settlement, "关闭通用 forge modal 后应返回 settlement modal。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNewWorldGenerationExposesMasterReforgeService()
    {
        GameSession gameSession = await InstallGameSession("ForgeGameSession");
        try
        {
            int createError = gameSession.CreateNewSave(TestConfigPath, "forge_spawn_service", "大师重铸入口验证");
            _test.Eq(createError, (int)Error.Ok, "创建带重铸入口验证的新世界应成功。");
            if (createError == (int)Error.Ok)
            {
                bool foundReforgeService = false;
                foreach (GDictionary settlement in Dictionaries(DictArray(gameSession.GetWorldData(), "settlements")))
                {
                    foreach (GDictionary service in Dictionaries(DictArray(settlement, "available_services")))
                    {
                        if (DictString(service, "interaction_script_id", "") == "service_master_reforge")
                        {
                            foundReforgeService = true;
                            break;
                        }
                    }
                    if (foundReforgeService)
                    {
                        break;
                    }
                }
                _test.True(foundReforgeService, "新生成世界的 available_services 应包含 service_master_reforge。");
            }
        }
        finally
        {
            await DisposeGameSession(gameSession, "清理重铸入口验证存档应成功。");
        }
    }

    private async Task TestAshenIntersectionGenerationExposesGenericForgeService()
    {
        GameSession gameSession = await InstallGameSession("AshenForgeGameSession");
        try
        {
            int createError = gameSession.CreateNewSave(AshenIntersectionConfigPath, "generic_forge_spawn_service", "通用 forge 入口验证");
            _test.Eq(createError, (int)Error.Ok, "创建灰烬交界世界应成功。");
            if (createError == (int)Error.Ok)
            {
                GDictionary worldData = gameSession.GetWorldData();
                Vector2I playerStartCoord = DictVector2I(worldData, "player_start_coord", Vector2I.Zero);
                GDictionary startSettlement = FindSettlementCoveringCoord(DictArray(worldData, "settlements"), playerStartCoord);
                GDictionary genericEntry = FindServiceEntry(DictArray(startSettlement, "available_services"), "service_repair_gear");
                _test.True(startSettlement.Count > 0, "灰烬交界的起始坐标应落在一个据点上。");
                _test.True(genericEntry.Count > 0, "灰烬交界的起始据点应暴露通用 forge 服务入口。");
            }
        }
        finally
        {
            await DisposeGameSession(gameSession, "清理通用 forge 入口验证存档应成功。");
        }
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture(string suffix, PartyState partyState, GDictionary settlementRecord)
    {
        GameSession gameSession = await InstallGameSession($"ForgeHandlerGameSession_{suffix}");
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = gameSession.GetItemDefsTyped();
        GDictionary worldData = BuildWorldData(settlementRecord);
        ConfigureSessionForRuntimeTest(gameSession, $"forge_handler_{suffix}", worldData, partyState);

        var runtime = new GameRuntimeFacade
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _player_faction_id = "player",
        };
        runtime.SetActiveSettlementId("forge_town");
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
        runtime._fog_system.RebuildVisibilityForFaction(
            "player",
            new[] { new VisionSourceData("test_forge_visibility", Vector2I.Zero, 1, "player") }
        );
        runtime._character_management.setup(
            partyState,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetProfessionDefsTyped(),
            gameSession.GetAchievementDefsTyped(),
            itemDefs,
            gameSession.GetQuestDefsTyped(),
            default,
            gameSession.GetProgressionIdentityCatalogTyped()
        );
        runtime._party_warehouse_service.Setup(partyState, itemDefs);
        runtime._settlement_command_handler.SetupRuntime(runtime);

        return new RuntimeFixture(runtime, gameSession, runtime._settlement_command_handler, runtime._party_warehouse_service);
    }

    private static void ConfigureSessionForRuntimeTest(GameSession gameSession, string saveId, GDictionary worldData, PartyState partyState)
    {
        gameSession.ConfigureRuntimeWorldForTests(
            saveId,
            TestConfigPath,
            worldData,
            partyState,
            null,
            "forge_handler_test",
            "Forge Handler Test",
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
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        var gameSession = new GameSession { Name = nodeName };
        Root.AddChild(gameSession);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return gameSession;
    }

    private async Task DisposeFixture(RuntimeFixture fixture)
    {
        fixture.Runtime?.Dispose();
        await DisposeGameSession(fixture.GameSession, "清理 forge handler 验证存档应成功。");
    }

    private async Task DisposeGameSession(GameSession gameSession, string clearMessage)
    {
        if (gameSession == null)
        {
            return;
        }
        int clearError = gameSession.ClearPersistedGame();
        _test.Eq(clearError, (int)Error.Ok, clearMessage);
        gameSession.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static GDictionary BuildSettlementRecord(bool includeMasterServiceEntry = false, bool includeGenericServiceEntry = false)
    {
        var serviceNpcs = new GArray
        {
            new GDictionary
            {
                ["npc_id"] = "npc_blacksmith",
                ["display_name"] = "灰烬铁匠",
                ["service_type"] = "锻火",
                ["interaction_script_id"] = "service_repair_gear",
                ["facility_id"] = "ash_forge",
                ["facility_name"] = "灰烬工坊",
            },
            new GDictionary
            {
                ["npc_id"] = "npc_master_smith",
                ["display_name"] = "大师铁匠",
                ["service_type"] = "重铸",
                ["interaction_script_id"] = "service_master_reforge",
                ["facility_id"] = "ash_forge",
                ["facility_name"] = "灰烬工坊",
            },
        };
        var facility = new GDictionary
        {
            ["facility_id"] = "ash_forge",
            ["display_name"] = "灰烬工坊",
            ["category"] = "support",
            ["interaction_type"] = "craft",
            ["slot_tag"] = "support",
            ["service_npcs"] = serviceNpcs,
        };
        var availableServices = new GArray();
        if (includeGenericServiceEntry)
        {
            availableServices.Add(
                new GDictionary
                {
                    ["action_id"] = "service:repair_gear",
                    ["facility_id"] = "ash_forge",
                    ["facility_name"] = "灰烬工坊",
                    ["npc_id"] = "npc_blacksmith",
                    ["npc_name"] = "灰烬铁匠",
                    ["service_type"] = "锻火",
                    ["interaction_script_id"] = "service_repair_gear",
                }
            );
        }
        if (includeMasterServiceEntry)
        {
            availableServices.Add(
                new GDictionary
                {
                    ["action_id"] = "service:master_reforge",
                    ["facility_id"] = "ash_forge",
                    ["facility_name"] = "灰烬工坊",
                    ["npc_id"] = "npc_master_smith",
                    ["npc_name"] = "大师铁匠",
                    ["service_type"] = "重铸",
                    ["interaction_script_id"] = "service_master_reforge",
                }
            );
        }
        return new GDictionary
        {
            ["entity_id"] = "settlement_forge_town",
            ["template_id"] = "test_forge_town",
            ["settlement_id"] = "forge_town",
            ["display_name"] = "灰烬镇",
            ["tier"] = 1,
            ["tier_name"] = "村镇",
            ["faction_id"] = "neutral",
            ["origin"] = Vector2I.Zero,
            ["footprint_size"] = Vector2I.One,
            ["facilities"] = new GArray { facility },
            ["service_npcs"] = serviceNpcs,
            ["available_services"] = availableServices,
            ["is_player_start"] = true,
            ["settlement_state"] = BuildSettlementState(),
        };
    }

    private static GDictionary BuildWorldData(GDictionary settlementRecord)
    {
        return new GDictionary
        {
            ["map_seed"] = 1,
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = new GArray { settlementRecord },
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["mounted_submaps"] = new GDictionary(),
            ["world_npcs"] = new GArray(),
            ["player_start_coord"] = Vector2I.Zero,
            ["player_start_settlement_id"] = "forge_town",
            ["player_start_settlement_name"] = "灰烬镇",
            ["fog_states"] = new GDictionary(),
        };
    }

    private static GDictionary BuildSettlementState()
    {
        return new GDictionary
        {
            ["visited"] = true,
            ["reputation"] = 0,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["shop_inventory_seed"] = 0,
            ["shop_last_refresh_step"] = 0,
            ["shop_states"] = new GDictionary(),
        };
    }

    private static GDictionary BuildReforgePayload()
    {
        return new GDictionary
        {
            ["facility_id"] = "ash_forge",
            ["facility_name"] = "灰烬工坊",
            ["npc_id"] = "npc_master_smith",
            ["npc_name"] = "大师铁匠",
            ["service_type"] = "重铸",
            ["interaction_script_id"] = "service_master_reforge",
        };
    }

    private static PartyState BuildPartyState(int storageSpace)
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
        };
        var hero = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
        };
        var progression = new UnitProgress
        {
            unit_id = "hero",
            display_name = "Hero",
            unit_base_attributes = new UnitBaseAttributes(),
        };
        progression.unit_base_attributes.custom_stats["storage_space"] = storageSpace;
        hero.progression = progression;
        partyState.SetMemberState(hero);
        return partyState;
    }

    private static void AddMemberToParty(PartyState partyState, StringName memberId, string displayName)
    {
        var member = new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = displayName,
            },
        };
        partyState.SetMemberState(member);
        if (!partyState.active_member_ids.Contains(memberId))
        {
            partyState.active_member_ids.Add(memberId);
        }
    }

    private static IReadOnlyDictionary<StringName, ItemDef> LoadItemDefs()
    {
        return new ItemContentRegistry().GetItemDefsTyped();
    }

    private static GDictionary FindServiceEntry(GArray services, string interactionScriptId)
    {
        foreach (GDictionary serviceData in Dictionaries(services))
        {
            if (DictString(serviceData, "interaction_script_id", "") == interactionScriptId)
            {
                return serviceData;
            }
        }
        return new GDictionary();
    }

    private static GDictionary FindSettlementCoveringCoord(GArray settlements, Vector2I coord)
    {
        foreach (GDictionary settlement in Dictionaries(settlements))
        {
            Vector2I origin = DictVector2I(settlement, "origin", Vector2I.Zero);
            Vector2I footprintSize = DictVector2I(settlement, "footprint_size", Vector2I.One);
            var rect = new Rect2I(origin, footprintSize);
            if (rect.HasPoint(coord))
            {
                return settlement;
            }
        }
        return new GDictionary();
    }

    private static HashSet<string> CollectRecipeIds(GArray entries)
    {
        var recipeIds = new HashSet<string>();
        foreach (GDictionary entryData in Dictionaries(entries))
        {
            string recipeId = DictString(entryData, "recipe_id", "");
            if (!string.IsNullOrEmpty(recipeId))
            {
                recipeIds.Add(recipeId);
            }
        }
        return recipeIds;
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                yield return value.AsGodotDictionary();
            }
        }
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        return dictionary != null
            && dictionary.ContainsKey(key)
            && dictionary[key].VariantType == Variant.Type.Array
                ? dictionary[key].AsGodotArray()
                : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        return dictionary != null
            && dictionary.ContainsKey(key)
            && dictionary[key].VariantType == Variant.Type.Dictionary
                ? dictionary[key].AsGodotDictionary()
                : new GDictionary();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }

    private static Vector2I DictVector2I(GDictionary dictionary, string key, Vector2I fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsVector2I()
            : fallback;
    }

    private sealed class RuntimeFixture
    {
        public RuntimeFixture(
            GameRuntimeFacade runtime,
            GameSession gameSession,
            GameRuntimeSettlementCommandHandler handler,
            PartyWarehouseService warehouseService)
        {
            Runtime = runtime;
            GameSession = gameSession;
            Handler = handler;
            WarehouseService = warehouseService;
        }

        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public GameRuntimeSettlementCommandHandler Handler { get; }
        public PartyWarehouseService WarehouseService { get; }
    }
}
