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

    private readonly List<string> _failures = new();

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

        if (_failures.Count == 0)
        {
            GD.Print("Settlement forge service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Settlement forge service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestMasterReforgeServiceSuccess()
    {
        GDictionary itemDefs = LoadItemDefs();
        PartyState partyState = BuildPartyState(6);
        var warehouseService = new PartyWarehouseService();
        warehouseService.setup(partyState, itemDefs);
        warehouseService.add_item("bronze_sword", 1);
        warehouseService.add_item("iron_ore", 2);

        var forgeService = new SettlementForgeService();
        GDictionary result = forgeService.execute_master_reforge(
            BuildSettlementRecord(),
            BuildReforgePayload(),
            itemDefs,
            new GDictionary(),
            warehouseService,
            partyState,
            new GArray
            {
                new GDictionary
                {
                    ["event_type"] = "progress",
                    ["quest_id"] = "forge_trial",
                    ["objective_id"] = "reforge_once",
                    ["progress_delta"] = 1,
                    ["target_value"] = 1,
                },
            }
        );

        AssertTrue(DictBool(result, "success", false), "重铸服务成功路径应返回 success=true。");
        AssertTrue(DictBool(result, "persist_party_state", false), "重铸成功后应要求持久化队伍状态。");
        AssertEq(warehouseService.count_item("bronze_sword"), 0, "重铸成功后应消耗青铜短剑。");
        AssertEq(warehouseService.count_item("iron_ore"), 0, "重铸成功后应消耗两份铁矿石。");
        AssertEq(warehouseService.count_item("iron_greatsword"), 1, "重铸成功后应产出铁制大剑。");
        AssertTrue(DictString(result, "message", "").Contains("铁制大剑"), "重铸成功文案应包含产出名称。");
        AssertEq(DictString(DictDictionary(result, "inventory_delta"), "recipe_id", ""), "master_reforge_iron_greatsword", "inventory_delta 应记录 recipe_id。");
        AssertEq(DictArray(result, "quest_progress_events").Count, 1, "重铸服务应保留调用方传入的 quest_progress_events。");
    }

    private void TestMasterReforgeServiceMissingMaterials()
    {
        GDictionary itemDefs = LoadItemDefs();
        PartyState partyState = BuildPartyState(6);
        var warehouseService = new PartyWarehouseService();
        warehouseService.setup(partyState, itemDefs);
        warehouseService.add_item("bronze_sword", 1);

        var forgeService = new SettlementForgeService();
        GDictionary result = forgeService.execute_master_reforge(
            BuildSettlementRecord(),
            BuildReforgePayload(),
            itemDefs,
            new GDictionary(),
            warehouseService,
            partyState,
            new GArray()
        );

        AssertFalse(DictBool(result, "success", true), "缺少材料时重铸服务应失败。");
        AssertFalse(DictBool(result, "persist_party_state", true), "重铸失败时不应要求持久化队伍状态。");
        AssertTrue(DictString(result, "message", "").Contains("铁矿石"), "缺少材料时应指出具体短缺材料。");
        AssertEq(warehouseService.count_item("bronze_sword"), 1, "失败时不应吞掉已有材料。");
        AssertEq(warehouseService.count_item("iron_greatsword"), 0, "失败时不应提前写入产物。");
    }

    private async Task TestSettlementHandlerRoutesMasterReforge()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture("master", BuildPartyState(6), BuildSettlementRecord(true));
        try
        {
            fixture.WarehouseService.add_item("bronze_sword", 1);
            fixture.WarehouseService.add_item("iron_ore", 2);

            GDictionary windowData = fixture.Handler.get_settlement_window_data("forge_town");
            GDictionary reforgeEntry = FindServiceEntry(DictArray(windowData, "available_services"), "service_master_reforge");
            AssertTrue(reforgeEntry.Count > 0, "据点窗口应暴露 service_master_reforge 服务入口。");
            AssertTrue(DictBool(reforgeEntry, "is_enabled", false), "存在可执行配方时，大师重铸入口应可用。");
            AssertEq(DictString(reforgeEntry, "cost_label", ""), "按配方材料", "大师重铸入口应显示按配方材料计价。");

            GDictionary openResult = fixture.Handler.command_execute_settlement_action("service:master_reforge", new GDictionary());
            AssertTrue(DictBool(openResult, "ok", false), "service:master_reforge 首次触发应成功打开 forge modal。");
            AssertEq(fixture.Runtime._active_modal_id, "forge", "首次点击大师重铸服务后应切换到 forge modal。");
            AssertTrue(fixture.Handler.get_forge_window_data().Count > 0, "打开 forge modal 后应能读取 forge window data。");
            AssertTrue(DictArray(fixture.Handler.get_forge_window_data(), "entries").Count > 0, "forge window data 应暴露可选配方。");
            AssertEq(fixture.WarehouseService.count_item("iron_greatsword"), 0, "仅打开 forge modal 时不应提前产出铁制大剑。");

            GDictionary commandResult = fixture.Handler.command_execute_settlement_action(
                "service:master_reforge",
                new GDictionary
                {
                    ["submission_source"] = "forge",
                    ["recipe_id"] = "master_reforge_iron_greatsword",
                }
            );
            AssertTrue(DictBool(commandResult, "ok", false), $"forge modal 提交配方后应成功执行重铸。message={DictString(commandResult, "message", "")}");
            AssertEq(fixture.Runtime._active_modal_id, "forge", "执行重铸后应继续停留在 forge modal。");
            AssertEq(fixture.WarehouseService.count_item("iron_greatsword"), 1, "通过 handler 执行后应真正产出铁制大剑。");
            AssertFalse(fixture.GameSession.has_pending_save(), "重铸成功后应提交队伍状态持久化。");
            AssertTrue(fixture.Runtime._party_state == fixture.Runtime._character_management.get_party_state(), "重铸成功后应同步角色管理侧队伍状态。");
            AssertTrue(fixture.Runtime._active_settlement_feedback_text.Contains("铁制大剑"), "handler 应把重铸反馈写入据点窗口。");
            AssertTrue(fixture.Runtime._current_status_message.Contains("铁制大剑"), "handler 应刷新重铸完成状态文案。");

            fixture.Handler.on_forge_window_closed();
            AssertEq(fixture.Runtime._active_modal_id, "settlement", "关闭 forge modal 后应返回 settlement modal。");
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
            fixture.WarehouseService.add_item("iron_ore", 2);
            fixture.WarehouseService.add_item("hardwood_lumber", 1);
            fixture.WarehouseService.add_item("whetstone", 1);
            fixture.WarehouseService.add_item("forge_coal", 1);

            GDictionary windowData = fixture.Handler.get_settlement_window_data("forge_town");
            GDictionary genericEntry = FindServiceEntry(DictArray(windowData, "available_services"), "service_repair_gear");
            AssertTrue(genericEntry.Count > 0, "据点窗口应暴露通用 forge 服务入口。");
            AssertTrue(DictBool(genericEntry, "is_enabled", false), "存在通用 forge 配方时，service_repair_gear 应可用。");
            AssertEq(DictString(genericEntry, "cost_label", ""), "按配方材料", "通用 forge 入口应显示按配方材料计价。");

            GDictionary openResult = fixture.Handler.command_execute_settlement_action(
                "service:repair_gear",
                new GDictionary { ["member_id"] = "mage" }
            );
            AssertTrue(DictBool(openResult, "ok", false), $"service:repair_gear 首次触发应成功打开 forge modal。message={DictString(openResult, "message", "")}");
            AssertEq(fixture.Runtime._active_modal_id, "forge", "首次点击通用 forge 服务后应切换到 forge modal。");
            GDictionary forgeWindowData = fixture.Handler.get_forge_window_data();
            AssertEq(DictString(forgeWindowData, "action_id", ""), "service:repair_gear", "通用 forge modal 应保留原始 action_id。");
            AssertEq(DictString(forgeWindowData, "default_member_id", ""), "mage", "通用 forge modal 应保留据点窗口选择的默认成员。");
            AssertEq(DictString(forgeWindowData, "selected_member_id", ""), "mage", "通用 forge modal 应保留据点窗口选择的当前成员。");
            AssertFalse(DictString(forgeWindowData, "title", "").Contains("重铸"), "通用 forge modal 标题不应回退成大师重铸。");
            GArray forgeEntries = DictArray(forgeWindowData, "entries");
            AssertTrue(forgeEntries.Count > 0, "通用 forge window data 应暴露可选配方。");
            HashSet<string> recipeIds = CollectRecipeIds(forgeEntries);
            AssertTrue(recipeIds.Contains("forge_smith_iron_greatsword"), "通用 forge modal 应继续暴露铁制大剑配方。");
            AssertTrue(recipeIds.Contains("forge_militia_axe"), "通用 forge modal 应暴露民兵手斧配方。");
            AssertTrue(recipeIds.Contains("forge_watchman_mace"), "通用 forge modal 应暴露卫兵钉锤配方。");

            GDictionary commandResult = fixture.Handler.command_execute_settlement_action(
                "service:repair_gear",
                new GDictionary
                {
                    ["submission_source"] = "forge",
                    ["member_id"] = DictString(forgeWindowData, "selected_member_id", ""),
                    ["recipe_id"] = "forge_militia_axe",
                }
            );
            AssertTrue(DictBool(commandResult, "ok", false), $"forge modal 提交通用配方后应成功执行锻造。message={DictString(commandResult, "message", "")}");
            AssertEq(fixture.Runtime._active_modal_id, "forge", "执行通用 forge 后应继续停留在 forge modal。");
            AssertEq(fixture.WarehouseService.count_item("iron_ore"), 1, "通用 forge 成功后应按配方扣除铁矿石。");
            AssertEq(fixture.WarehouseService.count_item("hardwood_lumber"), 0, "通用 forge 成功后应消耗硬木板。");
            AssertEq(fixture.WarehouseService.count_item("whetstone"), 0, "通用 forge 成功后应消耗磨刃石。");
            AssertEq(fixture.WarehouseService.count_item("militia_axe"), 1, "通用 forge 成功后应真正产出民兵手斧。");
            AssertFalse(fixture.GameSession.has_pending_save(), "通用 forge 成功后应提交队伍状态持久化。");
            AssertTrue(fixture.Runtime._party_state == fixture.Runtime._character_management.get_party_state(), "通用 forge 成功后应同步角色管理侧队伍状态。");
            AssertTrue(fixture.Runtime._active_settlement_feedback_text.Contains("民兵手斧"), "handler 应把通用 forge 反馈写入据点窗口。");
            AssertTrue(fixture.Runtime._current_status_message.Contains("民兵手斧"), "handler 应刷新通用 forge 完成状态文案。");

            fixture.Handler.on_forge_window_closed();
            AssertEq(fixture.Runtime._active_modal_id, "settlement", "关闭通用 forge modal 后应返回 settlement modal。");
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
            int createError = gameSession.create_new_save(TestConfigPath, "forge_spawn_service", "大师重铸入口验证");
            AssertEq(createError, (int)Error.Ok, "创建带重铸入口验证的新世界应成功。");
            if (createError == (int)Error.Ok)
            {
                bool foundReforgeService = false;
                foreach (GDictionary settlement in Dictionaries(DictArray(gameSession.get_world_data(), "settlements")))
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
                AssertTrue(foundReforgeService, "新生成世界的 available_services 应包含 service_master_reforge。");
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
            int createError = gameSession.create_new_save(AshenIntersectionConfigPath, "generic_forge_spawn_service", "通用 forge 入口验证");
            AssertEq(createError, (int)Error.Ok, "创建灰烬交界世界应成功。");
            if (createError == (int)Error.Ok)
            {
                GDictionary worldData = gameSession.get_world_data();
                Vector2I playerStartCoord = DictVector2I(worldData, "player_start_coord", Vector2I.Zero);
                GDictionary startSettlement = FindSettlementCoveringCoord(DictArray(worldData, "settlements"), playerStartCoord);
                GDictionary genericEntry = FindServiceEntry(DictArray(startSettlement, "available_services"), "service_repair_gear");
                AssertTrue(startSettlement.Count > 0, "灰烬交界的起始坐标应落在一个据点上。");
                AssertTrue(genericEntry.Count > 0, "灰烬交界的起始据点应暴露通用 forge 服务入口。");
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
        GDictionary itemDefs = gameSession.get_item_defs();
        GDictionary worldData = BuildWorldData(settlementRecord);
        ConfigureSessionForRuntimeTest(gameSession, $"forge_handler_{suffix}", worldData, partyState);

        var runtime = new GameRuntimeFacade
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _active_settlement_id = "forge_town",
            _active_modal_id = "settlement",
            _player_faction_id = "player",
        };
        runtime._world_map_data_context.bind_root_world_data(worldData);
        runtime._world_map_data_context.active_world_data = worldData;
        runtime._world_map_data_context.settlements_by_id["forge_town"] = settlementRecord;
        runtime._fog_system.setup(new Vector2I(8, 8));
        runtime._fog_system.rebuild_visibility_for_faction(
            "player",
            new GArray { new VisionSourceData("test_forge_visibility", Vector2I.Zero, 1, "player") }
        );
        runtime._character_management.setup(
            partyState,
            gameSession.get_skill_defs(),
            gameSession.get_profession_defs(),
            gameSession.get_achievement_defs(),
            itemDefs,
            gameSession.get_quest_defs(),
            default,
            gameSession.get_progression_content_bundle()
        );
        runtime._party_warehouse_service.setup(partyState, itemDefs);
        runtime._settlement_command_handler.setup(runtime);

        return new RuntimeFixture(runtime, gameSession, runtime._settlement_command_handler, runtime._party_warehouse_service);
    }

    private static void ConfigureSessionForRuntimeTest(GameSession gameSession, string saveId, GDictionary worldData, PartyState partyState)
    {
        int now = (int)Time.GetUnixTimeFromSystem();
        gameSession._active_save_id = saveId;
        gameSession._active_save_path = gameSession._build_save_file_path(saveId);
        gameSession._generation_config_path = TestConfigPath;
        gameSession._generation_config = ResourceLoader.Load<WorldMapGenerationConfig>(TestConfigPath);
        gameSession._world_data = worldData;
        gameSession._player_coord = Vector2I.Zero;
        gameSession._player_faction_id = "player";
        gameSession._party_state = partyState;
        gameSession._has_active_world = true;
        gameSession._battle_save_lock_enabled = false;
        gameSession._active_save_meta = gameSession._build_save_meta(
            saveId,
            saveId,
            TestConfigPath,
            "forge_handler_test",
            "Forge Handler Test",
            new Vector2I(8, 8),
            now,
            now
        );
        gameSession.discard_pending_save();
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
        fixture.Runtime?.dispose();
        await DisposeGameSession(fixture.GameSession, "清理 forge handler 验证存档应成功。");
    }

    private async Task DisposeGameSession(GameSession gameSession, string clearMessage)
    {
        if (gameSession == null)
        {
            return;
        }
        int clearError = gameSession.clear_persisted_game();
        AssertEq(clearError, (int)Error.Ok, clearMessage);
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
        partyState.set_member_state(hero);
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
        partyState.set_member_state(member);
        if (!partyState.active_member_ids.Contains(memberId))
        {
            partyState.active_member_ids.Add(memberId);
        }
    }

    private static GDictionary LoadItemDefs()
    {
        return new ItemContentRegistry().get_item_defs();
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
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
