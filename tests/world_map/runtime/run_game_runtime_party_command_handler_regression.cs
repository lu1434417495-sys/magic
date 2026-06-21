using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_party_command_handler_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFacadeUsesPartyHandlerSurface();

        Quit(_test.Finish("Game runtime party command handler regression"));
    }

    private void TestFacadeUsesPartyHandlerSurface()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState, true);
        try
        {
            GameRuntimeFacade.RuntimeCommandResult openResult = runtime.CommandOpenPartyTyped();
            _test.True(openResult.Ok, $"command_open_party() 应委托给正式 party handler。message={openResult.Message}");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Party, "facade 打开队伍管理后应切换到 party modal。");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "hero", "facade 打开队伍管理后应默认选中上阵第一人。");

            GameRuntimeFacade.RuntimeCommandResult selectResult =
                runtime.CommandSelectPartyMemberTyped("mage");
            _test.True(selectResult.Ok, "command_select_party_member() 应委托给正式 party handler。");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "mage", "facade 选中队员后应同步选中成员。");

            runtime._party_command_handler.OnPartyManagementWarehouseRequested();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Warehouse, "OnPartyManagementWarehouseRequested() 应委托 handler 打开真实仓库 modal。");
            _test.Eq(runtime._active_warehouse_entry_label, "队伍管理", "队伍管理打开仓库时应保留正式入口标签。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Party);
            runtime._party_command_handler.OnPartyManagementWindowClosed();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "OnPartyManagementWindowClosed() 应委托 handler 关闭 party modal。");
            _test.Eq(runtime._current_status_message, "已关闭队伍管理窗口。", "关闭队伍窗口应刷新正式状态文案。");
        }
        finally
        {
            runtime.Dispose();
            GodotRefCountedDisposer.DisposeIfValid(partyState);
        }
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState, bool addBronzeSword)
    {
        using var registry = new ItemContentRegistry();
        IReadOnlyDictionary<StringName, ItemDef> typedItemDefs =
            new Dictionary<StringName, ItemDef>(registry.GetItemDefsTyped());
        GDictionary itemDefs = ProjectItemDefs(typedItemDefs);
        int equipmentSerial = 1;
        Func<StringName> equipmentInstanceIdAllocator = () =>
            new StringName($"eq_party_command_{equipmentSerial++:000}");

        GameRuntimeFacade runtime = new()
        {
            _party_state = partyState,
            _generation_config =
                ResourceLoader.Load<WorldMapGenerationConfig>(TestConfigPath)
                ?? new WorldMapGenerationConfig(),
        };
        runtime._world_map_data_context.active_generation_config = runtime._generation_config;
        runtime._character_management.setup(
            partyState,
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            typedItemDefs,
            new Dictionary<StringName, QuestDef>(),
            equipmentInstanceIdAllocator,
            new ProgressionIdentityCatalogData()
        );
        runtime._party_warehouse_service.Setup(
            partyState,
            typedItemDefs,
            equipmentInstanceIdAllocator
        );
        runtime._party_item_use_service.Setup(
            partyState,
            typedItemDefs,
            new Dictionary<StringName, SkillDef>(),
            runtime._party_warehouse_service,
            runtime._character_management
        );
        runtime._party_equipment_service.Setup(
            partyState,
            typedItemDefs,
            runtime._party_warehouse_service,
            equipmentInstanceIdAllocator
        );
        runtime._warehouse_handler.Setup(runtime);
        runtime._party_command_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);

        if (addBronzeSword)
        {
            runtime._party_warehouse_service.AddItemTyped("bronze_sword", 1);
        }

        return runtime;
    }

    private static GDictionary ProjectItemDefs(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null)
                continue;
            result[itemId] = itemDef;
        }
        return result;
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { new StringName("hero") },
            reserve_member_ids = new GStringNameArray { new StringName("mage") },
        };
        partyState.SetMemberState(BuildMember("hero", "Hero"));
        partyState.SetMemberState(BuildMember("mage", "Mage"));
        partyState.SetMemberState(BuildMember("ghost", "Ghost"));
        return partyState;
    }

    private static PartyMemberState BuildMember(StringName memberId, string displayName)
    {
        UnitBaseAttributes attributes = new()
        {
            strength = 10,
            agility = 10,
            constitution = 10,
            perception = 10,
            intelligence = 10,
            willpower = 10,
        };
        attributes.SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, 8);

        return new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
            current_hp = 20,
            current_mp = 4,
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = displayName,
                unit_base_attributes = attributes,
            },
            equipment_state = new EquipmentState(),
        };
    }

    private static bool HasMemberId(GStringNameArray memberIds, StringName memberId)
    {
        if (memberIds == null)
        {
            return false;
        }
        foreach (StringName currentMemberId in memberIds)
        {
            if (currentMemberId.ToString() == memberId.ToString())
            {
                return true;
            }
        }
        return false;
    }

    private static string MemberIdList(GStringNameArray memberIds)
    {
        if (memberIds == null)
        {
            return "<null>";
        }
        List<string> result = new();
        foreach (StringName memberId in memberIds)
        {
            result.Add(memberId.ToString());
        }
        return string.Join(",", result);
    }
}
