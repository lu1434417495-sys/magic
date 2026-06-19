using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_persist_failure_rollback_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        await TestStagecoachTravelRollbackOnPersistFailure();
        await TestStagecoachTravelRollbackPreservesPendingSaveMetadataOnPersistFailure();
        await TestShopBuyRollbackOnPersistFailure();
        await TestShopSellRollbackOnPersistFailure();
        await TestSettlementServiceRollbackOnPersistFailure();
        await TestWarehouseSettlementServiceRollbackOnPersistFailure();
        TestRuntimeTransactionRollbackStateUsesTypedSessionSnapshot();

        Quit(_test.Finish("Settlement persist failure rollback regression"));
    }

    private async Task TestStagecoachTravelRollbackOnPersistFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "stagecoach",
            BuildPartyState(12, 200),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildShopAndStagecoachServices()),
                BuildSettlementRecord("graystone_town_01", "灰石镇", new Vector2I(2, 1), new GArray()),
            }
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;
            Vector2I settlementEntrySourceCoord = new(1, 0);
            Vector2I settlementEntryTargetCoord = Vector2I.Zero;

            runtime.SetPlayerCoord(settlementEntrySourceCoord);
            runtime.SetSelectedCoord(settlementEntryTargetCoord);
            runtime.SetSettlementEntryContext(
                settlementEntrySourceCoord,
                settlementEntryTargetCoord
            );

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:stagecoach",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "驿站回滚测试前置：应能打开驿站路线。");
            fixture.GameSession.fail_payload_write = true;

            int goldBefore = runtime._party_state.GetGold();
            Vector2I playerCoordBefore = runtime.GetPlayerCoord();

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandStagecoachTravelTyped("graystone_town_01");

            _test.False(result.Ok, "驿站持久化失败时命令应返回失败。");
            _test.Eq(runtime._party_state.gold, goldBefore, "驿站失败后金币应回滚。");
            _test.Eq(runtime.GetPlayerCoord(), playerCoordBefore, "驿站失败后玩家坐标应回滚。");
            _test.True(
                runtime._settlement_entry_active,
                "驿站持久化失败后应恢复 settlement entry 激活状态。"
            );
            _test.Eq(
                runtime._settlement_entry_source_coord,
                settlementEntrySourceCoord,
                "驿站持久化失败后应恢复 settlement entry source coord。"
            );
            _test.Eq(
                runtime._settlement_entry_target_coord,
                settlementEntryTargetCoord,
                "驿站持久化失败后应恢复 settlement entry target coord。"
            );
            _test.False(
                runtime.IsPlayerVisibleOnWorldMap(),
                "驿站持久化失败后在 service modal 返回时世界地图不应显示玩家。"
            );
            _test.Eq(
                runtime._active_modal_kind,
                RuntimeModalKind.Stagecoach,
                "驿站持久化失败后应回到 stagecoach modal。"
            );
            _test.Eq(
                result.Message,
                "存档提交失败，操作已回滚。",
                "驿站持久化失败时应返回统一回滚错误。"
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private async Task TestStagecoachTravelRollbackPreservesPendingSaveMetadataOnPersistFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "stagecoach_pending_save_metadata",
            BuildPartyState(12, 200),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildShopAndStagecoachServices()),
                BuildSettlementRecord("graystone_town_01", "灰石镇", new Vector2I(2, 1), new GArray()),
            }
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:stagecoach",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "pending save 回滚测试前置：应能打开驿站路线。");

            int dirtyWorldError = fixture.GameSession.SetWorldData(runtime.GetWorldData());
            _test.Eq(
                dirtyWorldError,
                (int)Error.Ok,
                "pending save 回滚测试前置：标记 world dirty 应成功。"
            );
            fixture.GameSession.SetBattleSaveLock(true);
            int preexistingCommitError = fixture.GameSession.CommitRuntimeState(
                new StringName("preexisting_pending_save")
            );
            _test.Eq(
                preexistingCommitError,
                (int)Error.Busy,
                "pending save 回滚测试前置：battle save lock 应留下既有保存错误元数据。"
            );
            fixture.GameSession.SetBattleSaveLock(false);
            GDictionary runtimeStateBefore = fixture.GameSession.CaptureRuntimeState();
            _test.True(
                fixture.GameSession.HasPendingSave(),
                "pending save 回滚测试前置：命令开始前应存在既有 dirty session 状态。"
            );

            fixture.GameSession.fail_payload_write = true;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandStagecoachTravelTyped("graystone_town_01");

            _test.False(result.Ok, "已有 pending save 时驿站持久化失败也应返回失败。");
            AssertRuntimeSaveMetadataEqual(
                fixture.GameSession.CaptureRuntimeState(),
                runtimeStateBefore,
                "已有 pending save 的驿站回滚后，session save metadata 应恢复到命令前状态。"
            );
            _test.Eq(
                result.Message,
                "存档提交失败，操作已回滚。",
                "已有 pending save 时也应返回统一回滚错误。"
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            fixture.GameSession.SetBattleSaveLock(false);
            await DisposeFixture(fixture);
        }
    }

    private async Task TestShopBuyRollbackOnPersistFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "shop_buy",
            BuildPartyState(12, 200),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    BuildShopAndStagecoachServices(),
                    BuildSettlementState(
                        new GArray
                        {
                            new GDictionary
                            {
                                ["item_id"] = "healing_herb",
                                ["quantity"] = 2,
                                ["unit_price"] = 12,
                                ["sold_out"] = false,
                            },
                        }
                    )
                ),
            }
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:basic_supply",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "商店购买回滚测试前置：应能打开商店。");
            fixture.GameSession.fail_payload_write = true;

            int goldBefore = runtime._party_state.GetGold();
            int herbCountBefore = fixture.WarehouseService.CountItem("healing_herb");

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandShopBuyTyped("healing_herb", 1);

            _test.False(result.Ok, "购买持久化失败时命令应返回失败。");
            _test.Eq(runtime._party_state.GetGold(), goldBefore, "购买失败后金币应回滚。");
            _test.Eq(
                fixture.WarehouseService.CountItem("healing_herb"),
                herbCountBefore,
                "购买失败后仓库物品不应增加。"
            );
            _test.Eq(
                result.Message,
                "存档提交失败，操作已回滚。",
                "购买持久化失败时应返回统一回滚错误。"
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private async Task TestShopSellRollbackOnPersistFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "shop_sell",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildShopAndStagecoachServices()),
            }
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;
            fixture.WarehouseService.AddItemTyped("travel_ration", 2);

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:basic_supply",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "商店出售回滚测试前置：应能打开商店。");
            fixture.GameSession.fail_payload_write = true;

            int goldBefore = runtime._party_state.GetGold();
            int rationCountBefore = fixture.WarehouseService.CountItem("travel_ration");

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandShopSellTyped("travel_ration", 1);

            _test.False(result.Ok, "出售持久化失败时命令应返回失败。");
            _test.Eq(runtime._party_state.GetGold(), goldBefore, "出售失败后金币应回滚。");
            _test.Eq(
                fixture.WarehouseService.CountItem("travel_ration"),
                rationCountBefore,
                "出售失败后仓库物品不应减少。"
            );
            _test.Eq(
                result.Message,
                "存档提交失败，操作已回滚。",
                "出售持久化失败时应返回统一回滚错误。"
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private async Task TestSettlementServiceRollbackOnPersistFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "service_action",
            BuildPartyState(12, 200),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildBasicSettlementServices(false)),
            }
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;
            PartyMemberState hero = runtime._party_state.GetMemberState("hero");
            hero.current_hp = 10;
            runtime._character_management.SetPartyState(runtime._party_state);
            fixture.GameSession.fail_payload_write = true;

            int goldBefore = runtime._party_state.GetGold();
            int hpBefore = hero.current_hp;
            int worldStepBefore = runtime.GetWorldStep();

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:rest_full",
                    new GDictionary()
                );

            _test.False(result.Ok, "据点服务持久化失败时命令应返回失败。");
            _test.Eq(runtime._party_state.GetGold(), goldBefore, "据点服务失败后金币应回滚。");
            _test.Eq(
                runtime._party_state.GetMemberState("hero").current_hp,
                hpBefore,
                "据点服务失败后成员生命应回滚。"
            );
            _test.Eq(runtime.GetWorldStep(), worldStepBefore, "据点服务失败后 world_step 应回滚。");
            _test.Eq(
                result.Message,
                "存档提交失败，操作已回滚。",
                "据点服务持久化失败时应返回统一回滚错误。"
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private async Task TestWarehouseSettlementServiceRollbackOnPersistFailure()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "warehouse_service_action",
            BuildPartyState(12, 200),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildBasicSettlementServices(false)),
            },
            BuildWarehouseQuestDefs()
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            var warehouseQuest = new QuestState { quest_id = "contract_warehouse_visit" };
            warehouseQuest.MarkAccepted(runtime.GetWorldStep());
            runtime._party_state.SetActiveQuestState(warehouseQuest);
            runtime._character_management.SetPartyState(runtime._party_state);

            GDictionary runtimeStateBefore = fixture.GameSession.CaptureRuntimeState();
            fixture.GameSession.fail_payload_write = true;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:warehouse",
                    new GDictionary()
                );

            _test.False(result.Ok, "仓储动作持久化失败时命令应返回失败。");
            _test.False(
                runtime._party_state.HasClaimableQuest("contract_warehouse_visit"),
                "仓储动作持久化失败后 quest progress 不应提交到队伍状态。"
            );
            _test.Eq(
                runtime._active_modal_kind,
                RuntimeModalKind.Settlement,
                "仓储动作持久化失败后不应打开共享仓库 modal。"
            );
            _test.Eq(
                runtime._active_warehouse_entry_label,
                "",
                "仓储动作持久化失败后不应记录仓库入口标签。"
            );
            AssertRuntimeSaveMetadataEqual(
                fixture.GameSession.CaptureRuntimeState(),
                runtimeStateBefore,
                "仓储动作持久化失败后，session save metadata 应恢复到命令前状态。"
            );
            _test.Eq(
                result.Message,
                "存档提交失败，操作已回滚。",
                "仓储动作持久化失败时应返回统一回滚错误。"
            );
        }
        finally
        {
            fixture.GameSession.fail_payload_write = false;
            await DisposeFixture(fixture);
        }
    }

    private void TestRuntimeTransactionRollbackStateUsesTypedSessionSnapshot()
    {
        Type rollbackType = typeof(RuntimeTransactionRollbackState);
        ConstructorInfo constructor = rollbackType.GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        )[0];
        ParameterInfo sessionSnapshotParameter = Array.Find(
            constructor.GetParameters(),
            parameter => parameter.Name == "sessionRuntimeState"
        );
        _test.True(
            sessionSnapshotParameter == null
                || sessionSnapshotParameter.ParameterType != typeof(GDictionary),
            "RuntimeTransactionRollbackState 不应使用 GDictionary sessionRuntimeState 作为回滚合同。"
        );

        FieldInfo sessionSnapshotField = rollbackType.GetField(
            "_sessionRuntimeState",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(
            sessionSnapshotField == null
                || sessionSnapshotField.FieldType != typeof(GDictionary),
            "RuntimeTransactionRollbackState 不应保存 GDictionary session rollback 快照。"
        );
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture(
        string suffix,
        PartyState partyState,
        IReadOnlyList<GDictionary> settlements,
        GDictionary questDefs = null
    )
    {
        GameSession gameSession = await InstallGameSession($"SettlementPersistFailure_{suffix}");
        GDictionary worldData = BuildWorldData(settlements);
        ConfigureSessionForRuntimeTest(
            gameSession,
            $"settlement_persist_failure_{suffix}",
            worldData,
            partyState,
            questDefs
        );
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = gameSession.GetItemDefsTyped();

        var runtime = new GameRuntimeFacade
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _player_faction_id = "player",
        };
        runtime.SetActiveSettlementId(DictString(settlements[0], "settlement_id", ""));
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
        runtime._party_warehouse_service.Setup(partyState, itemDefs, gameSession.AllocateEquipmentInstanceId);
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

        return new RuntimeFixture(
            runtime,
            gameSession,
            runtime._settlement_command_handler,
            runtime._party_warehouse_service
        );
    }

    private static void ConfigureSessionForRuntimeTest(
        GameSession gameSession,
        string saveId,
        GDictionary worldData,
        PartyState partyState,
        GDictionary questDefs = null
    )
    {
        gameSession.ConfigureRuntimeWorldForTests(
            saveId,
            TestConfigPath,
            worldData,
            partyState,
            questDefs ?? new GDictionary(),
            "settlement_persist_failure_test",
            "Settlement Persist Failure Test",
            new Vector2I(8, 8)
        );
    }

    private async Task<GameSession> InstallGameSession(string nodeName)
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == nodeName)
                child.QueueFree();
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
        await DisposeGameSession(fixture.GameSession);
    }

    private async Task DisposeGameSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        int clearError = gameSession.ClearPersistedGame();
        _test.Eq(clearError, (int)Error.Ok, "清理 persist failure 回归存档应成功。");
        gameSession.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static GDictionary BuildWorldData(IReadOnlyList<GDictionary> settlements)
    {
        var settlementArray = new GArray();
        foreach (GDictionary settlement in settlements)
            settlementArray.Add(settlement);
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
            ["mounted_submaps"] = new GDictionary(),
            ["world_npcs"] = new GArray(),
            ["player_start_coord"] = Vector2I.Zero,
            ["player_start_settlement_id"] = DictString(settlements[0], "settlement_id", ""),
            ["player_start_settlement_name"] = DictString(settlements[0], "display_name", ""),
            ["fog_states"] = new GDictionary(),
        };
    }

    private static GDictionary BuildSettlementRecord(
        string settlementId,
        string displayName,
        Vector2I origin,
        GArray services,
        GDictionary settlementState = null
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
            ["settlement_state"] = settlementState?.Duplicate(true) ?? BuildSettlementState(),
        };
    }

    private static GDictionary BuildSettlementState(GArray currentInventory = null)
    {
        currentInventory ??= new GArray();
        return new GDictionary
        {
            ["visited"] = true,
            ["reputation"] = 0,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["world_step"] = 0,
            ["shop_inventory_seed"] = 0,
            ["shop_last_refresh_step"] = 0,
            ["shop_states"] = new GDictionary
            {
                ["village_basic_supply"] = new GDictionary
                {
                    ["shop_id"] = "village_basic_supply",
                    ["current_inventory"] = currentInventory,
                    ["seed"] = 1,
                    ["last_refresh_step"] = 0,
                },
            },
        };
    }

    private static GArray BuildBasicSettlementServices(bool includeStagecoach)
    {
        var services = new GArray
        {
            new GDictionary { ["action_id"] = "service:warehouse", ["facility_name"] = "据点服务台", ["npc_name"] = "军需官", ["service_type"] = "仓储", ["interaction_script_id"] = "party_warehouse" },
            new GDictionary { ["action_id"] = "service:training", ["facility_name"] = "训练场", ["npc_name"] = "教官", ["service_type"] = "训练", ["interaction_script_id"] = "training_service" },
            new GDictionary { ["action_id"] = "service:rest_full", ["facility_name"] = "旅店", ["npc_name"] = "店主", ["service_type"] = "整备", ["interaction_script_id"] = "service_rest_full" },
        };
        if (includeStagecoach)
        {
            services.Add(new GDictionary { ["action_id"] = "service:stagecoach", ["facility_name"] = "驿站", ["npc_name"] = "驿夫", ["service_type"] = "驿站", ["interaction_script_id"] = "service_stagecoach" });
        }
        return services;
    }

    private static GArray BuildShopAndStagecoachServices()
    {
        return new GArray
        {
            new GDictionary { ["action_id"] = "service:basic_supply", ["facility_name"] = "补给铺", ["npc_name"] = "行商", ["service_type"] = "补给", ["interaction_script_id"] = "service_basic_supply" },
            new GDictionary { ["action_id"] = "service:stagecoach", ["facility_name"] = "驿站", ["npc_name"] = "驿夫", ["service_type"] = "驿站", ["interaction_script_id"] = "service_stagecoach" },
        };
    }

    private static GDictionary BuildWarehouseQuestDefs()
    {
        var questDefs = new GDictionary();
        AddQuestDef(
            questDefs,
            BuildQuestDef(
                "contract_warehouse_visit",
                "仓储访问追踪",
                "据点仓储动作进度测试。",
                "service_warehouse_hidden",
                new GArray { BuildObjective("warehouse_visit", "settlement_action", "service:warehouse", 1) },
                new GArray { BuildGoldReward(1) }
            )
        );
        return questDefs;
    }

    private static PartyState BuildPartyState(int storageSpace, int gold)
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            gold = gold,
        };
        var hero = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            current_hp = 20,
            current_mp = 4,
        };
        var progression = new UnitProgress
        {
            unit_id = "hero",
            display_name = "Hero",
            unit_base_attributes = new UnitBaseAttributes(),
        };
        progression.unit_base_attributes.custom_stats["storage_space"] = storageSpace;
        progression.unit_base_attributes.custom_stats["hp_max"] = 40;
        progression.unit_base_attributes.custom_stats["mp_max"] = 12;
        hero.progression = progression;
        partyState.SetMemberState(hero);
        return partyState;
    }

    private static void MakeVisible(GameRuntimeFacade runtime, Vector2I center)
    {
        runtime._fog_system.RebuildVisibilityForFaction(
            "player",
            new[] { new VisionSourceData("settlement_persist_failure_visibility", center, 6, "player") }
        );
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null)
            return fallback;
        if (dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.String)
            return dictionary[key].AsString();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            && dictionary[stringNameKey].VariantType == Variant.Type.String
            ? dictionary[stringNameKey].AsString()
            : fallback;
    }

    private void AssertRuntimeSaveMetadataEqual(
        GDictionary actual,
        GDictionary expected,
        string messagePrefix
    )
    {
        _test.Eq(
            DictBool(actual, "battle_save_lock_enabled", false),
            DictBool(expected, "battle_save_lock_enabled", false),
            $"{messagePrefix} battle_save_lock_enabled 不一致。"
        );
        _test.Eq(
            DictBool(actual, "battle_save_dirty", false),
            DictBool(expected, "battle_save_dirty", false),
            $"{messagePrefix} battle_save_dirty 不一致。"
        );
        _test.Eq(
            DictBool(actual, "runtime_save_dirty", false),
            DictBool(expected, "runtime_save_dirty", false),
            $"{messagePrefix} runtime_save_dirty 不一致。"
        );
        _test.Eq(
            DictInt(actual, "last_save_error", (int)Error.Ok),
            DictInt(expected, "last_save_error", (int)Error.Ok),
            $"{messagePrefix} last_save_error 不一致。"
        );
        _test.Eq(
            DictString(actual, "last_save_error_reason", ""),
            DictString(expected, "last_save_error_reason", ""),
            $"{messagePrefix} last_save_error_reason 不一致。"
        );
        _test.Eq(
            DictBool(actual, "post_decode_save_pending", false),
            DictBool(expected, "post_decode_save_pending", false),
            $"{messagePrefix} post_decode_save_pending 不一致。"
        );
        _test.Eq(
            StringifyVariantArray(DictArray(actual, "runtime_save_dirty_scopes")),
            StringifyVariantArray(DictArray(expected, "runtime_save_dirty_scopes")),
            $"{messagePrefix} runtime_save_dirty_scopes 不一致。"
        );
        _test.Eq(
            StringifyVariantArray(DictArray(actual, "post_decode_save_reasons")),
            StringifyVariantArray(DictArray(expected, "post_decode_save_reasons")),
            $"{messagePrefix} post_decode_save_reasons 不一致。"
        );
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null)
            return fallback;
        if (dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Bool)
            return dictionary[key].AsBool();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            && dictionary[stringNameKey].VariantType == Variant.Type.Bool
            ? dictionary[stringNameKey].AsBool()
            : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null)
            return fallback;
        if (dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Int)
            return dictionary[key].AsInt32();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            && dictionary[stringNameKey].VariantType == Variant.Type.Int
            ? dictionary[stringNameKey].AsInt32()
            : fallback;
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null)
            return new GArray();
        if (dictionary.ContainsKey(key) && dictionary[key].VariantType == Variant.Type.Array)
            return dictionary[key].AsGodotArray();
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            && dictionary[stringNameKey].VariantType == Variant.Type.Array
            ? dictionary[stringNameKey].AsGodotArray()
            : new GArray();
    }

    private static string StringifyVariantArray(GArray values)
    {
        List<string> entries = new();
        if (values != null)
        {
            foreach (Variant value in values)
                entries.Add(value.ToString());
        }
        return string.Join("|", entries);
    }

    private static void AddQuestDef(GDictionary questDefs, QuestDef questDef)
    {
        questDefs[questDef.quest_id] = questDef;
    }

    private static QuestDef BuildQuestDef(
        string questId,
        string displayName,
        string description,
        string providerInteractionId,
        GArray objectiveDefs,
        GArray rewardEntries,
        bool isRepeatable = false
    )
    {
        var quest = new QuestDef
        {
            quest_id = questId,
            display_name = displayName,
            description = description,
            provider_interaction_id = providerInteractionId,
            is_repeatable = isRepeatable,
        };
        foreach (GDictionary objective in objectiveDefs)
            quest.objective_defs.Add((GDictionary)objective.Duplicate(true));
        foreach (GDictionary reward in rewardEntries)
            quest.reward_entries.Add((GDictionary)reward.Duplicate(true));
        return quest;
    }

    private static GDictionary BuildObjective(
        string objectiveId,
        string objectiveType,
        string targetId,
        int targetValue
    )
    {
        return new GDictionary
        {
            ["objective_id"] = objectiveId,
            ["objective_type"] = objectiveType,
            ["target_id"] = targetId,
            ["target_value"] = targetValue,
        };
    }

    private static GDictionary BuildGoldReward(int amount)
    {
        return new GDictionary { ["reward_type"] = "gold", ["amount"] = amount };
    }

    private sealed class RuntimeFixture
    {
        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public GameRuntimeSettlementCommandHandler Handler { get; }
        public PartyWarehouseService WarehouseService { get; }

        public RuntimeFixture(
            GameRuntimeFacade runtime,
            GameSession gameSession,
            GameRuntimeSettlementCommandHandler handler,
            PartyWarehouseService warehouseService
        )
        {
            Runtime = runtime;
            GameSession = gameSession;
            Handler = handler;
            WarehouseService = warehouseService;
        }
    }
}
