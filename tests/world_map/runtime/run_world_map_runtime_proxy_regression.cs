using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using RuntimeCommandResult = GameRuntimeFacade.RuntimeCommandResult;

public partial class run_world_map_runtime_proxy_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    private sealed class BookSkillPickData
    {
        public StringName SkillId { get; set; }
        public StringName ItemId { get; set; }
    }

    private sealed class RuntimeFixture : IDisposable
    {
        public GameRuntimeFacade Runtime { get; init; }
        public WorldMapGenerationConfig GenerationConfig { get; init; }
        public GDictionary ItemDefs { get; init; }

        public void Dispose()
        {
            Runtime?.Dispose();
            ItemDefs?.Clear();
        }
    }

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestGettersForwardToRuntime();
        TestSnapshotMethodsForwardToRuntime();
        TestPartyCommandsDelegateToRuntime();
        TestWarehouseUseTypedOptionsDelegateToRuntime();
        TestMissingRuntimeReturnsError();

        RequestTestExit(_test.Finish("World map runtime proxy regression"));
    }

    private void TestGettersForwardToRuntime()
    {
        RuntimeFixture fixture = BuildRuntime(BuildPartyState());
        GameRuntimeFacade runtime = fixture.Runtime;
        runtime.UpdateStatus("runtime-status");
        runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
        runtime.SetActiveSettlementId("settlement_alpha");
        runtime.SetSettlementEntryContext(Vector2I.Zero, Vector2I.Zero);
        runtime._world_map_data_context.active_map_id = "ashen_ashlands";
        runtime._world_map_data_context.active_map_display_name = "灰烬地图";
        runtime.GetPendingSubmapPromptState().Set(
            "",
            "",
            Vector2I.Zero,
            "ashen_ashlands",
            "灰烬地图",
            "进入灰烬地图",
            ""
        );
        runtime.SetPendingBattleStartPrompt(new GDictionary
        {
            ["title"] = "开始战斗",
            ["confirm_text"] = "开始战斗",
        });
        runtime.SetBattleSelectionTargetUnitIdsState(
            new GStringNameArray { "enemy_alpha", "enemy_beta" }
        );

        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        try
        {
            _test.Eq(proxy.GetStatusText(), "runtime-status", "GetStatusText() 应直接读取 runtime。");
            _test.Eq(proxy.GetActiveModalId(), "settlement", "GetActiveModalId() 应直接读取 runtime。");
            _test.Eq(proxy.GetActiveSettlementId(), "settlement_alpha", "GetActiveSettlementId() 应直接读取 runtime。");
            _test.Eq(proxy.GetActiveMapId(), "ashen_ashlands", "GetActiveMapId() 应直接读取 runtime。");
            _test.Eq(proxy.GetActiveMapDisplayName(), "灰烬地图", "GetActiveMapDisplayName() 应直接读取 runtime。");
            WorldRuntimeViewModel viewModel = proxy.GetWorldRuntimeViewModel();
            _test.Eq(viewModel.StatusText, "runtime-status", "World view model 应读取 runtime status。");
            _test.Eq(viewModel.ActiveModalId, "settlement", "World view model 应读取 runtime modal id。");
            _test.Eq(viewModel.ActiveMapId, "ashen_ashlands", "World view model 应读取 active map id。");
            _test.Eq(viewModel.ActiveMapDisplayName, "灰烬地图", "World view model 应读取 active map name。");
            _test.Eq(viewModel.PlayerCoord, runtime.GetPlayerCoord(), "World view model 应读取 player coord。");
            _test.Eq(viewModel.SelectedCoord, runtime.GetSelectedCoord(), "World view model 应读取 selected coord。");
            _test.False(viewModel.PlayerVisible, "World view model 应读取 player visibility。");
            _test.Eq(proxy.GetPendingBattleStartPrompt()["confirm_text"].AsString(), "开始战斗", "GetPendingBattleStartPrompt() 应直接读取 runtime。");
            _test.Eq(proxy.GetPendingSubmapPrompt()["target_display_name"].AsString(), "灰烬地图", "GetPendingSubmapPrompt() 应直接读取 runtime。");
            _test.False(proxy.IsPlayerVisibleOnWorldMap(), "IsPlayerVisibleOnWorldMap() 应直接读取 runtime。");
            _test.True(proxy.IsSubmapActive(), "IsSubmapActive() 应直接读取 runtime。");
            AssertSequence(
                proxy.GetSelectedBattleSkillTargetUnitIds(),
                new[] { "enemy_alpha", "enemy_beta" },
                "GetSelectedBattleSkillTargetUnitIds() 应直接读取 runtime。"
            );
        }
        finally
        {
            proxy.Dispose();
            fixture.Dispose();
        }
    }

    private void TestSnapshotMethodsForwardToRuntime()
    {
        RuntimeFixture fixture = BuildRuntime(BuildPartyState());
        GameRuntimeFacade runtime = fixture.Runtime;
        runtime.UpdateStatus("runtime-status");
        runtime._world_map_data_context.active_map_id = "snapshot_map";
        runtime._world_map_data_context.active_map_display_name = "快照地图";

        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        try
        {
            GDictionary headlessSnapshot = proxy.BuildHeadlessSnapshot();
            _test.Eq(
                DictString(Dict(headlessSnapshot, "status"), "text", ""),
                "runtime-status",
                "BuildHeadlessSnapshot() 应返回 runtime 快照。"
            );
            _test.Eq(
                DictString(Dict(headlessSnapshot, "world"), "map_id", ""),
                "snapshot_map",
                "BuildHeadlessSnapshot() 应包含 runtime 世界上下文。"
            );
            _test.Eq(
                proxy.BuildTextSnapshot(),
                runtime.BuildTextSnapshot(),
                "BuildTextSnapshot() 应直接转发 runtime 文本快照。"
            );
        }
        finally
        {
            proxy.Dispose();
            fixture.Dispose();
        }
    }

    private void TestPartyCommandsDelegateToRuntime()
    {
        PartyState partyState = BuildPartyState();
        RuntimeFixture fixture = BuildRuntime(partyState);
        GameRuntimeFacade runtime = fixture.Runtime;
        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        try
        {
            RuntimeCommandResult openResult = proxy.CommandOpenParty();
            _test.True(openResult.Ok, $"CommandOpenParty() 应委托 runtime。message={openResult.Message}");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Party, "CommandOpenParty() 成功后应更新 runtime modal。");
            _test.Eq(proxy.GetPartySelectedMemberId().ToString(), "hero", "CommandOpenParty() 应通过 runtime 选中上阵第一人。");

            RuntimeCommandResult selectResult = proxy.CommandSelectPartyMember("mage");
            _test.True(selectResult.Ok, $"CommandSelectPartyMember() 应委托 runtime。message={selectResult.Message}");
            _test.Eq(proxy.GetPartySelectedMemberId().ToString(), "mage", "CommandSelectPartyMember() 应更新 runtime 选中成员。");

            RuntimeCommandResult warehouseResult = proxy.CommandOpenPartyWarehouse();
            _test.True(warehouseResult.Ok, $"CommandOpenPartyWarehouse() 应委托 runtime。message={warehouseResult.Message}");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Warehouse, "CommandOpenPartyWarehouse() 成功后应打开 warehouse modal。");
            _test.Eq(runtime._active_warehouse_entry_label, "队伍管理", "CommandOpenPartyWarehouse() 应保留正式入口标签。");
        }
        finally
        {
            proxy.Dispose();
            fixture.Dispose();
        }
    }

    private void TestMissingRuntimeReturnsError()
    {
        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(null);
        RuntimeCommandResult result = proxy.CommandWorldMove(Vector2I.Right, 1);
        _test.False(result.Ok, "缺少 runtime 时命令应返回失败。");
        _test.Eq(result.Message, "运行时尚未初始化。", "缺少 runtime 时应返回正式错误文案。");
        _test.Eq(
            result.Code,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable,
            "缺少 runtime 时 typed command 应返回 RuntimeUnavailable。"
        );
        _test.Eq(proxy.GetStatusText(), "", "缺少 runtime 时 getter 应返回安全默认值。");
    }

    private void TestWarehouseUseTypedOptionsDelegateToRuntime()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            GameTextCommandResult newGameResult = runner.ExecuteLine("game new test");
            _test.True(
                newGameResult.ok,
                $"proxy warehouse use regression 应能创建测试世界。message={newGameResult.message}"
            );
            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            GameSession gameSession = session?.GetGameSessionTyped();
            _test.True(runtime != null, "proxy warehouse use regression 应拿到 typed runtime。");
            _test.True(gameSession != null, "proxy warehouse use regression 应拿到 typed game session。");
            if (runtime == null || gameSession == null)
                return;

            StringName memberId = "player_sword_01";
            BookSkillPickData bookSkill = PickUnlearnedBookSkillForMember(gameSession, memberId);
            _test.True(
                bookSkill.SkillId != "" && bookSkill.ItemId != "",
                "proxy warehouse use regression 应能找到可生成技能书的未学会 book 技能。"
            );
            if (bookSkill.SkillId == "" || bookSkill.ItemId == "")
                return;

            PartyWarehouseService.WarehouseAddItemResult addResult =
                runtime._party_warehouse_service.AddItemTyped(bookSkill.ItemId, 1);
            _test.True(
                addResult != null && addResult.AddedQuantity == 1,
                "warehouse add typed 预置库存应直接成功，不应依赖持久化命令链。"
            );
            GameRuntimeFacade.RuntimeCommandResult openPartyResult = runtime.CommandOpenPartyTyped();
            _test.True(
                openPartyResult.Ok,
                $"warehouse use 前置的 party open 应成功。message={openPartyResult.Message}"
            );
            GameRuntimeFacade.RuntimeCommandResult selectPartyResult =
                runtime.CommandSelectPartyMemberTyped(memberId);
            _test.True(
                selectPartyResult.Ok,
                $"warehouse use 前置的 party select 应成功。message={selectPartyResult.Message}"
            );

            WorldMapRuntimeProxy proxy = new();
            proxy.Setup(runtime);
            RuntimeCommandResult openWarehouseResult = proxy.CommandOpenPartyWarehouse();
            _test.True(
                openWarehouseResult.Ok,
                $"CommandOpenPartyWarehouse() 应先成功打开共享仓库。message={openWarehouseResult.Message}"
            );
            RuntimeCommandResult result = proxy.CommandWarehouseUseItem(
                bookSkill.ItemId,
                memberId,
                new PartyItemUseService.PartyItemUseOptions(true)
            );
            _test.True(
                result.Ok
                    || result.Code == GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure,
                $"CommandWarehouseUseItem(typed options) 应委托 runtime，并返回正式成功或持久化失败 code。message={result.Message}"
            );
            UnitSkillProgress skillProgress = gameSession
                .GetPartyState()
                .GetMemberState(memberId)
                ?.progression
                ?.GetSkillProgress(bookSkill.SkillId);
            if (result.Ok)
            {
                _test.Eq(
                    runtime._party_warehouse_service.CountItem(bookSkill.ItemId),
                    0,
                    "typed warehouse use 成功后应消耗技能书。"
                );
                _test.True(
                    skillProgress != null && skillProgress.is_learned,
                    "typed warehouse use 成功时应通过正式链让目标成员学会技能。"
                );
            }
            else
            {
                _test.Eq(
                    result.Code,
                    GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure,
                    "typed warehouse use 失败时应暴露正式 PersistenceFailure code。"
                );
                _test.Eq(
                    runtime._party_warehouse_service.CountItem(bookSkill.ItemId),
                    1,
                    "持久化失败回滚后，技能书库存应保持不变。"
                );
                _test.True(
                    skillProgress == null || !skillProgress.is_learned,
                    "持久化失败回滚后，成员不应保留已学会状态。"
                );
            }

            proxy.Dispose();
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static BookSkillPickData PickUnlearnedBookSkillForMember(
        GameSession gameSession,
        StringName memberId
    )
    {
        PartyState partyState = gameSession?.GetPartyState();
        PartyMemberState memberState = partyState?.GetMemberState(memberId);
        if (memberState?.progression == null)
            return new BookSkillPickData();

        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            gameSession.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = gameSession.GetItemDefsTyped();
        var sortedSkillIds = new List<StringName>(skillDefinitions.Keys);
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        foreach (StringName skillId in sortedSkillIds)
        {
            if (
                !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
                || skillDefinition == null
            )
                continue;
            if (skillDefinition.LearnSource != "book")
                continue;
            UnitSkillProgress skillProgress = memberState.progression.GetSkillProgress(skillId);
            if (skillProgress != null && skillProgress.is_learned)
                continue;
            StringName itemId = new($"skill_book_{skillId}");
            if (!itemDefs.ContainsKey(itemId))
                continue;
            return new BookSkillPickData { SkillId = skillId, ItemId = itemId };
        }
        return new BookSkillPickData();
    }

    private static RuntimeFixture BuildRuntime(PartyState partyState)
    {
        Dictionary<StringName, SkillDefinition> skillDefinitions = BuildSkillDefinitions();
        GDictionary itemDefs = BuildItemDefs();
        WorldMapGenerationConfig generationConfig = new();
        GameRuntimeFacade runtime = new()
        {
            _party_state = partyState,
            _generation_config = generationConfig,
        };
        runtime._world_map_data_context.active_generation_config = generationConfig;
        runtime._world_map_data_context.active_world_data = new GDictionary
        {
            ["world_step"] = 0,
            ["world_events"] = new Godot.Collections.Array(),
            ["encounter_anchors"] = new Godot.Collections.Array(),
        };
        runtime._character_management.setup(
            partyState,
            skillDefinitions,
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            BuildTypedItemDefs(itemDefs)
        );
        runtime._party_warehouse_service.Setup(partyState, BuildTypedItemDefs(itemDefs));
        runtime._party_item_use_service.Setup(
            partyState,
            new Dictionary<StringName, ItemDef>
            {
                ["skill_book_focus"] = (ItemDef)
                    itemDefs[new StringName("skill_book_focus")].AsGodotObject(),
            },
            skillDefinitions,
            runtime._party_warehouse_service,
            runtime._character_management
        );
        runtime._battle_selection.Setup(runtime);
        runtime._battle_session_facade.Setup(runtime);
        runtime._party_command_handler.Setup(runtime);
        runtime._warehouse_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);
        return new RuntimeFixture
        {
            Runtime = runtime,
            GenerationConfig = generationConfig,
            ItemDefs = itemDefs,
        };
    }

    private static GDictionary BuildItemDefs()
    {
        GDictionary result = TestResourceOwnership.OwnWrapper(
            new GDictionary(),
            "world_map_runtime_proxy.item_defs"
        );
        result[new StringName("skill_book_focus")] = TestResourceOwnership.Own(
            new ItemDef
            {
                item_id = "skill_book_focus",
                display_name = "Focus Manual",
                CategoryKind = ItemCategoryKind.SkillBook,
                is_stackable = true,
                max_stack = 20,
                granted_skill_id = "focus",
            },
            "world_map_runtime_proxy.skill_book_focus"
        );
        return result;
    }

    private static Dictionary<StringName, ItemDef> BuildTypedItemDefs(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }

    private static Dictionary<StringName, SkillDefinition> BuildSkillDefinitions()
    {
        Dictionary<StringName, SkillDefinition> result = new();
        StringName skillId = "focus";
        result[skillId] = new SkillDefinition(
            skillId,
            "Focus",
            "",
            "",
            "passive",
            1,
            1,
            "",
            0,
            0,
            Array.Empty<int>(),
            Array.Empty<StringName>(),
            "book",
            Array.Empty<StringName>(),
            "",
            Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            false,
            "",
            Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, Variant>>(),
            null
        );
        return result;
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            reserve_member_ids = new GStringNameArray { "mage" },
        };
        partyState.SetMemberState(BuildMember("hero", "Hero"));
        partyState.SetMemberState(BuildMember("mage", "Mage"));
        return partyState;
    }

    private static PartyMemberState BuildMember(StringName memberId, string displayName)
    {
        UnitBaseAttributes attributes = new();
        attributes.SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, 4);
        return new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
            current_hp = 20,
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = displayName,
                unit_base_attributes = attributes,
            },
        };
    }

    private static GDictionary Dict(GDictionary dictionary, string key)
    {
        return dictionary != null && dictionary.ContainsKey(key)
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

    private void AssertSequence(GStringNameArray actual, string[] expected, string message)
    {
        List<string> values = new();
        if (actual != null)
        {
            foreach (StringName value in actual)
            {
                values.Add(value.ToString());
            }
        }
        if (values.Count != expected.Length)
        {
            _test.Fail($"{message} | actual={string.Join(",", values)} expected={string.Join(",", expected)}");
            return;
        }
        for (int index = 0; index < expected.Length; index++)
        {
            if (values[index] != expected[index])
            {
                _test.Fail($"{message} | actual={string.Join(",", values)} expected={string.Join(",", expected)}");
                return;
            }
        }
    }
}
