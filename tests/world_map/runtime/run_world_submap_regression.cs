using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_submap_regression : LifecycleTestSceneTree
{
    private const string AshenWorldConfig = "res://data/configs/world_map/ashen_intersection_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestMountedSubmapSerializerContract();
        TestSubmapReturnBlocksWhileBattleActive();
        TestSubmapReturnBlocksWhileModalOpen();
        TestSubmapEntryReturnAndReload();

        RequestTestExit(_test.Finish("World submap regression"));
    }

    private void TestSubmapReturnBlocksWhileBattleActive()
    {
        RuntimeContext context = CreateAshenRuntimeContext();
        if (context == null)
        {
            return;
        }

        try
        {
            if (!EnterAshenSubmap(context.Facade))
            {
                return;
            }

            Vector2I expectedCoord = context.Facade.GetPlayerCoord();
            string expectedMapId = context.Facade.GetActiveMapId();
            string expectedActiveSubmapId = DictString(
                context.GameSession.GetWorldData(),
                "active_submap_id"
            );
            BattleState battleState = new() { battle_id = "submap_guard_battle" };
            context.Facade.SetRuntimeBattleState(battleState);

            GameRuntimeFacade.RuntimeCommandResult returnResult =
                context.Facade.CommandReturnFromSubmapTyped();
            _test.True(!returnResult.Ok, "子地图 battle active 时返回应被阻断。");
            _test.True(!string.IsNullOrEmpty(returnResult.Message), "battle active 阻断应返回错误。");
            _test.True(context.Facade.IsSubmapActive(), "battle active 阻断后应仍停留在子地图。");
            _test.True(context.Facade.IsBattleActive(), "battle active 阻断后不应清空 battle 状态。");
            _test.Eq(
                context.Facade.GetActiveMapId(),
                expectedMapId,
                "battle active 阻断后 active_map_id 应保持不变。"
            );
            _test.Eq(
                DictString(context.GameSession.GetWorldData(), "active_submap_id"),
                expectedActiveSubmapId,
                "battle active 阻断后 active_submap_id 应保持不变。"
            );
            _test.Eq(
                context.Facade.GetPlayerCoord(),
                expectedCoord,
                "battle active 阻断后运行时玩家坐标应保持不变。"
            );
            _test.Eq(
                context.GameSession.GetPlayerCoord(),
                expectedCoord,
                "battle active 阻断后存档侧玩家坐标应保持不变。"
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestSubmapReturnBlocksWhileModalOpen()
    {
        RuntimeContext context = CreateAshenRuntimeContext();
        if (context == null)
        {
            return;
        }

        try
        {
            if (!EnterAshenSubmap(context.Facade))
            {
                return;
            }

            Vector2I expectedCoord = context.Facade.GetPlayerCoord();
            string expectedMapId = context.Facade.GetActiveMapId();
            string expectedActiveSubmapId = DictString(
                context.GameSession.GetWorldData(),
                "active_submap_id"
            );
            context.Facade.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);

            GameRuntimeFacade.RuntimeCommandResult returnResult =
                context.Facade.CommandReturnFromSubmapTyped();
            _test.True(!returnResult.Ok, "子地图 modal 打开时返回应被阻断。");
            _test.True(!string.IsNullOrEmpty(returnResult.Message), "modal-open 阻断应返回错误。");
            _test.True(context.Facade.IsSubmapActive(), "modal-open 阻断后应仍停留在子地图。");
            _test.Eq(
                context.Facade.GetActiveMapId(),
                expectedMapId,
                "modal-open 阻断后 active_map_id 应保持不变。"
            );
            _test.Eq(
                DictString(context.GameSession.GetWorldData(), "active_submap_id"),
                expectedActiveSubmapId,
                "modal-open 阻断后 active_submap_id 应保持不变。"
            );
            _test.Eq(
                context.Facade.GetActiveModalId(),
                "settlement",
                "modal-open 阻断后当前 modal 应保持不变。"
            );
            _test.Eq(
                context.Facade.GetPlayerCoord(),
                expectedCoord,
                "modal-open 阻断后运行时玩家坐标应保持不变。"
            );
            _test.Eq(
                context.GameSession.GetPlayerCoord(),
                expectedCoord,
                "modal-open 阻断后存档侧玩家坐标应保持不变。"
            );
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestSubmapEntryReturnAndReload()
    {
        RuntimeContext context = CreateAshenRuntimeContext();
        if (context == null)
        {
            return;
        }

        try
        {
            if (!EnterAshenSubmap(context.Facade))
            {
                return;
            }

            GameRuntimeFacade.RuntimeCommandResult submapMoveResult =
                context.Facade.CommandWorldMoveTyped(Vector2I.Left, 1);
            _test.True(submapMoveResult.Ok, "子地图内移动应成功。");
            _test.Eq(context.Facade.GetPlayerCoord(), new Vector2I(14, 15), "子地图内移动后坐标应更新。");
            _test.True(context.GameSession.HasPendingSave(), "子地图内移动后应存在待提交存档状态。");
            _test.True(
                context.Facade.IsWorldCoordVisible(
                    context.Facade.GetPlayerCoord(),
                    context.Facade.GetPlayerFactionId()
                ),
                "子地图内移动后 active fog 应立即刷新到玩家当前位置。"
            );
            _test.True(
                IsFormalFogState(context.Facade.GetWorldData()["fog_states"]),
                "子地图 active world_data 应保存正式 fog_states。"
            );

            string activeSaveId = context.GameSession.GetActiveSaveId();
            context.Facade.Dispose();

            int reloadError = context.GameSession.LoadSave(activeSaveId);
            _test.True(reloadError == (int)Error.Ok, "子地图状态应能从存档中重新载入。");
            if (reloadError != (int)Error.Ok)
            {
                return;
            }

            GameRuntimeFacade reloadedFacade = new();
            reloadedFacade.Setup(context.GameSession);
            try
            {
                _test.True(reloadedFacade.IsSubmapActive(), "重新载入后应仍停留在灰烬地图。");
                _test.Eq(
                    reloadedFacade.GetPlayerCoord(),
                    new Vector2I(14, 15),
                    "重新载入后应恢复子地图内坐标。"
                );
                _test.True(
                    reloadedFacade.IsWorldCoordVisible(
                        reloadedFacade.GetPlayerCoord(),
                        reloadedFacade.GetPlayerFactionId()
                    ),
                    "重新载入 active submap 后 fog 应从 world_data 恢复并刷新当前位置。"
                );

                GameRuntimeFacade.RuntimeCommandResult returnResult =
                    reloadedFacade.CommandReturnFromSubmapTyped();
                _test.True(returnResult.Ok, "从子地图返回主世界应成功。");
                _test.True(!reloadedFacade.IsSubmapActive(), "返回后应回到主世界。");
                _test.Eq(
                    reloadedFacade.GetPlayerCoord(),
                    new Vector2I(52, 49),
                    "返回后应恢复到进入前的原坐标。"
                );
                _test.Eq(
                    DictString(context.GameSession.GetWorldData(), "active_submap_id"),
                    "",
                    "成功返回后应清空 active_submap_id。"
                );
                _test.Eq(
                    context.GameSession.GetPlayerCoord(),
                    new Vector2I(52, 49),
                    "成功返回后存档侧玩家坐标应同步回主世界原坐标。"
                );
                _test.True(
                    reloadedFacade.IsWorldCoordVisible(
                        reloadedFacade.GetPlayerCoord(),
                        reloadedFacade.GetPlayerFactionId()
                    ),
                    "返回主世界后 active fog 应立即刷新到返回坐标。"
                );
                _test.True(
                    IsFormalFogState(context.GameSession.GetWorldData()["fog_states"]),
                    "返回主世界后 root world_data 应保存正式 fog_states。"
                );
            }
            finally
            {
                reloadedFacade.Dispose();
            }
        }
        finally
        {
            context.Dispose();
        }
    }

    private void TestMountedSubmapSerializerContract()
    {
        SaveSerializer serializer = new();
        GDictionary rootWorldData = BuildMinimalWorldData(101);
        rootWorldData["mounted_submaps"] = new GDictionary
        {
            ["ashen_ashlands"] = BuildMountedSubmapEntry(false, new GDictionary()),
        };

        GDictionary normalizedWorldData = serializer.NormalizeWorldData(rootWorldData);
        GDictionary normalizedEntry = GetMountedSubmapEntry(normalizedWorldData, "ashen_ashlands");
        _test.True(normalizedEntry.Count > 0, "未生成子地图占位应能穿过 normalize_world_data。");
        _test.True(!DictBool(normalizedEntry, "is_generated", true), "未生成子地图 normalize 后应保持 is_generated=false。");
        _test.True(
            Dict(normalizedEntry, "world_data").Count == 0,
            "未生成子地图 normalize 后应保持空 world_data。"
        );

        GDictionary serializedWorldData = serializer.SerializeWorldData(normalizedWorldData);
        GDictionary serializedEntry = GetMountedSubmapEntry(serializedWorldData, "ashen_ashlands");
        _test.True(serializedEntry.Count > 0, "未生成子地图占位应能穿过 serialize_world_data。");
        _test.True(
            Dict(serializedEntry, "world_data").Count == 0,
            "未生成子地图 serialize 后应保持空 world_data。"
        );

        GDictionary generatedSubmapWorldData = BuildMinimalWorldData(202);
        GDictionary generatedRootWorldData = BuildMinimalWorldData(101);
        generatedRootWorldData["mounted_submaps"] = new GDictionary
        {
            ["ashen_ashlands"] = BuildMountedSubmapEntry(true, generatedSubmapWorldData),
        };
        GDictionary serializedGeneratedWorldData =
            serializer.SerializeWorldData(generatedRootWorldData);
        GDictionary serializedGeneratedEntry =
            GetMountedSubmapEntry(serializedGeneratedWorldData, "ashen_ashlands");
        GDictionary serializedGeneratedSubmapWorldData = Dict(
            serializedGeneratedEntry,
            "world_data"
        );
        _test.Eq(
            DictInt(serializedGeneratedSubmapWorldData, "next_equipment_instance_serial"),
            1,
            "已生成子地图应继续按完整 world_data 序列化。"
        );
        _test.Eq(
            DictInt(serializedGeneratedSubmapWorldData, "map_seed"),
            202,
            "已生成子地图应继续按完整 world_data 保留 map_seed。"
        );

        GDictionary rootMissingSeed = BuildMinimalWorldData(505);
        rootMissingSeed.Remove("map_seed");
        string missingRootSeedError = serializer.GetWorldDataSeedValidationError(rootMissingSeed);
        _test.True(
            !string.IsNullOrEmpty(missingRootSeedError),
            $"root world_data 缺少 map_seed 应直接判为坏档。 error={missingRootSeedError}"
        );

        GDictionary rootZeroSeed = BuildMinimalWorldData(0);
        string zeroRootSeedError = serializer.GetWorldDataSeedValidationError(rootZeroSeed);
        _test.True(
            !string.IsNullOrEmpty(zeroRootSeedError),
            $"root world_data 的 map_seed 非正数应直接判为坏档。 error={zeroRootSeedError}"
        );

        GDictionary rootMissingWorldStep = BuildMinimalWorldData(707);
        rootMissingWorldStep.Remove("world_step");
        string missingWorldStepError =
            serializer.GetWorldDataStepValidationError(rootMissingWorldStep);
        _test.True(
            !string.IsNullOrEmpty(missingWorldStepError),
            $"root world_data 缺少 world_step 应直接判为坏档。 error={missingWorldStepError}"
        );

        GDictionary rootStringWorldStep = BuildMinimalWorldData(708);
        rootStringWorldStep["world_step"] = "0";
        string stringWorldStepError =
            serializer.GetWorldDataStepValidationError(rootStringWorldStep);
        _test.True(
            !string.IsNullOrEmpty(stringWorldStepError),
            $"root world_data 的字符串 world_step 不应兼容转换。 error={stringWorldStepError}"
        );

        GDictionary rootNegativeWorldStep = BuildMinimalWorldData(709);
        rootNegativeWorldStep["world_step"] = -1;
        string negativeWorldStepError =
            serializer.GetWorldDataStepValidationError(rootNegativeWorldStep);
        _test.True(
            !string.IsNullOrEmpty(negativeWorldStepError),
            $"root world_data 的负数 world_step 应直接判为坏档。 error={negativeWorldStepError}"
        );

        GDictionary rootMissingResourceNodes = BuildMinimalWorldData(710);
        rootMissingResourceNodes.Remove("resource_nodes");
        string missingResourceNodesError =
            serializer.GetWorldDataSchemaValidationError(rootMissingResourceNodes);
        _test.True(
            !string.IsNullOrEmpty(missingResourceNodesError),
            $"root world_data 缺少 resource_nodes 应直接判为坏档。 error={missingResourceNodesError}"
        );

        GDictionary rootInvalidResourceNode = BuildMinimalWorldData(711);
        rootInvalidResourceNode["resource_nodes"] = new GArray
        {
            BuildResourceNode(
                "resource_bad_1",
                WorldMapResourceNodeData.KindFarm,
                "坏资源",
                new Vector2I(2, 2),
                WorldMapResourceNodeData.YieldIronOre,
                "",
                3,
                3
            ),
        };
        string invalidResourceNodeError =
            serializer.GetWorldDataValidationError(rootInvalidResourceNode);
        _test.True(
            !string.IsNullOrEmpty(invalidResourceNodeError),
            $"resource node 类型与产出不匹配应直接判为坏档。 error={invalidResourceNodeError}"
        );

        GDictionary generatedMissingSeed = BuildMinimalWorldData(606);
        generatedMissingSeed.Remove("map_seed");
        string missingSeedError = serializer.GetMountedSubmapWorldDataValidationError(
            "ashen_ashlands",
            true,
            generatedMissingSeed
        );
        _test.True(
            !string.IsNullOrEmpty(missingSeedError),
            $"已生成子地图缺少 map_seed 应判为坏档。 error={missingSeedError}"
        );

        GDictionary generatedMissingWorldStep = BuildMinimalWorldData(607);
        generatedMissingWorldStep.Remove("world_step");
        string missingSubmapWorldStepError =
            serializer.GetMountedSubmapWorldDataValidationError(
                "ashen_ashlands",
                true,
                generatedMissingWorldStep
            );
        _test.True(
            !string.IsNullOrEmpty(missingSubmapWorldStepError),
            $"已生成子地图缺少 world_step 应判为坏档。 error={missingSubmapWorldStepError}"
        );

        GDictionary generatedMissingSerial = BuildMinimalWorldData(303);
        generatedMissingSerial.Remove("next_equipment_instance_serial");
        string missingSerialError = serializer.GetMountedSubmapWorldDataValidationError(
            "ashen_ashlands",
            true,
            generatedMissingSerial
        );
        _test.True(
            !string.IsNullOrEmpty(missingSerialError),
            $"已生成子地图缺少装备实例序列仍应判为坏档。 error={missingSerialError}"
        );

        string ungeneratedWithWorldDataError =
            serializer.GetMountedSubmapWorldDataValidationError(
                "ashen_ashlands",
                false,
                BuildMinimalWorldData(404)
            );
        _test.True(
            !string.IsNullOrEmpty(ungeneratedWithWorldDataError),
            $"未生成子地图不应携带非空 world_data，避免把不一致状态静默降级。 error={ungeneratedWithWorldDataError}"
        );
    }

    private RuntimeContext CreateAshenRuntimeContext()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(AshenWorldConfig);
        _test.True(createError == (int)Error.Ok, "灰烬交界预设应能创建新存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupGameSession(gameSession);
            return null;
        }

        GameRuntimeFacade facade = new();
        facade.Setup(gameSession);
        return new RuntimeContext(gameSession, facade);
    }

    private bool EnterAshenSubmap(GameRuntimeFacade facade)
    {
        _test.True(!facade.IsSubmapActive(), "初始应停留在主世界。");
        _test.True(
            facade.GetNearbyWorldEventEntries().Count > 0,
            "主世界应能看到已发现的灰烬入口事件。"
        );

        GameRuntimeFacade.RuntimeCommandResult moveResult =
            facade.CommandWorldMoveTyped(Vector2I.Right, 3);
        _test.True(moveResult.Ok, "走向灰烬入口应成功。");
        _test.Eq(facade.GetActiveModalId(), "submap_confirm", "踩入入口事件后应弹出进入确认窗。");
        _test.Eq(
            DictString(facade.GetPendingSubmapPrompt(), "target_display_name"),
            "灰烬地图",
            "待确认入口应指向灰烬地图。"
        );

        GameRuntimeFacade.RuntimeCommandResult confirmResult =
            facade.CommandConfirmSubmapEntryTyped();
        _test.True(confirmResult.Ok, "确认后应进入灰烬地图。");
        _test.True(facade.IsSubmapActive(), "确认后当前地图应切换到子地图。");
        _test.Eq(facade.GetActiveMapId(), "ashen_ashlands", "子地图 ID 应写入运行时。");
        _test.Eq(facade.GetPlayerCoord(), new Vector2I(15, 15), "首次进入灰烬地图应落在子地图起点。");
        _test.True(
            facade.IsWorldCoordVisible(facade.GetPlayerCoord(), facade.GetPlayerFactionId()),
            "首次进入子地图后 active fog 应立即可见玩家起点。"
        );
        return confirmResult.Ok;
    }

    private static GDictionary BuildMinimalWorldData(int mapSeed)
    {
        return new GDictionary
        {
            ["map_seed"] = mapSeed,
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = new GArray(),
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["resource_nodes"] = new GArray(),
            ["mounted_submaps"] = new GDictionary(),
        };
    }

    private static GDictionary BuildResourceNode(
        string nodeId,
        string nodeKind,
        string displayName,
        Vector2I worldCoord,
        string yieldItemId,
        string sourceSettlementId,
        int maxCharges,
        int remainingCharges
    )
    {
        return new GDictionary
        {
            ["node_id"] = nodeId,
            ["node_kind"] = nodeKind,
            ["display_name"] = displayName,
            ["world_coord"] = worldCoord,
            ["yield_item_id"] = yieldItemId,
            ["source_settlement_id"] = sourceSettlementId,
            ["max_charges"] = maxCharges,
            ["remaining_charges"] = remainingCharges,
        };
    }

    private static GDictionary BuildMountedSubmapEntry(bool isGenerated, GDictionary worldData)
    {
        return new GDictionary
        {
            ["submap_id"] = "ashen_ashlands",
            ["display_name"] = "灰烬地图",
            ["generation_config_path"] = AshenWorldConfig,
            ["return_hint_text"] = "点击任意地点返回原位置。",
            ["is_generated"] = isGenerated,
            ["player_coord"] = new Vector2I(-1, -1),
            ["world_data"] = worldData ?? new GDictionary(),
        };
    }

    private static GDictionary GetMountedSubmapEntry(GDictionary worldData, string submapId)
    {
        GDictionary mountedSubmaps = Dict(worldData, "mounted_submaps");
        if (!mountedSubmaps.ContainsKey(submapId))
        {
            return new GDictionary();
        }

        Variant entry = mountedSubmaps[submapId];
        return entry.VariantType == Variant.Type.Dictionary
            ? entry.AsGodotDictionary()
            : new GDictionary();
    }

    private static bool IsFormalFogState(Variant value)
    {
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        GDictionary fogState = value.AsGodotDictionary();
        if (!fogState.ContainsKey("version") || fogState["version"].VariantType != Variant.Type.Int)
        {
            return false;
        }
        if (fogState["version"].AsInt32() != 1)
        {
            return false;
        }
        if (!fogState.ContainsKey("factions") || fogState["factions"].VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        GDictionary factions = fogState["factions"].AsGodotDictionary();
        return factions.ContainsKey("player");
    }

    private static void CleanupGameSession(GameSession gameSession)
    {
        if (gameSession == null)
        {
            return;
        }

        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }

        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }

        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }

        Variant value = dictionary[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static GDictionary Dict(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new GDictionary();
        }

        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private sealed class RuntimeContext
    {
        public RuntimeContext(GameSession gameSession, GameRuntimeFacade facade)
        {
            GameSession = gameSession;
            Facade = facade;
        }

        public GameSession GameSession { get; }
        public GameRuntimeFacade Facade { get; }

        public void Dispose()
        {
            Facade?.Dispose();
            CleanupGameSession(GameSession);
        }
    }
}
