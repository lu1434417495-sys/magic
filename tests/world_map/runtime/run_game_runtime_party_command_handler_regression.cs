using System;
using System.Collections.Generic;
using System.Reflection;
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
        TestPartyHandlerUpdatesRuntimeStateAndInventoryServices();

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
        }
    }

    private void TestPartyHandlerUpdatesRuntimeStateAndInventoryServices()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState, true);
        GameRuntimePartyCommandHandler handler = runtime._party_command_handler;
        try
        {
            _test.True(
                typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_open_party",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
                "GameRuntimePartyCommandHandler 不应继续保留 wrapper-only command_open_party() dictionary surface。"
            );
            _test.True(
                typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandOpenParty",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
                "GameRuntimePartyCommandHandler 不应继续保留 wrapper-only CommandOpenParty() dictionary surface。"
            );
            _test.True(
                typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "apply_party_roster",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
                "GameRuntimePartyCommandHandler 不应继续保留 wrapper-only apply_party_roster() dictionary surface。"
            );

            GameRuntimeFacade.RuntimeCommandResult openResult = handler.CommandOpenPartyTyped();
            _test.True(openResult.Ok, $"打开队伍管理应成功。message={openResult.Message}");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Party, "打开队伍管理应切换 modal。");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "hero", "打开队伍管理应默认选中上阵第一人。");

            GameRuntimeFacade.RuntimeCommandResult selectResult = handler.CommandSelectPartyMemberTyped("mage");
            _test.True(selectResult.Ok, "选中队员应成功。");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "mage", "选中队员后应同步选中标记。");

            GameRuntimeFacade.RuntimeCommandResult ghostSelectResult =
                handler.CommandSelectPartyMemberTyped("ghost");
            _test.False(ghostSelectResult.Ok, "不在 active/reserve roster 的成员不应允许被选中。");
            _test.True(!string.IsNullOrEmpty(ghostSelectResult.Message), "越权选中非 roster 成员时应返回错误。");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "mage", "越权选中失败后不应改写当前选中成员。");

            GameRuntimeFacade.RuntimeCommandResult leaderResult = handler.CommandSetPartyLeaderTyped("hero");
            _test.True(leaderResult.Ok, "设置队长应成功。");
            _test.Eq(partyState.leader_member_id.ToString(), "hero", "设置队长后应更新队长成员。");
            _test.True(runtime._character_management.GetPartyState() == partyState, "设置队长后应同步队伍状态到角色管理。");

            GameRuntimeFacade.RuntimeCommandResult rosterResult =
                handler.CommandMoveMemberToActiveTyped("mage");
            _test.True(rosterResult.Ok, $"移动成员到上阵应成功。message={rosterResult.Message} status={runtime._current_status_message}");
            _test.True(HasMemberId(partyState.active_member_ids, "mage"), $"移动到上阵后应更新 active 列表。active={MemberIdList(partyState.active_member_ids)} reserve={MemberIdList(partyState.reserve_member_ids)} status={runtime._current_status_message}");
            _test.False(HasMemberId(partyState.reserve_member_ids, "mage"), $"移动到上阵后应从 reserve 列表移除。active={MemberIdList(partyState.active_member_ids)} reserve={MemberIdList(partyState.reserve_member_ids)} status={runtime._current_status_message}");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "mage", "移动成员后应保持当前选中成员。");

            GameRuntimeFacade.RuntimeCommandResult moveMainToReserveResult =
                handler.CommandMoveMemberToReserveTyped("hero");
            _test.False(moveMainToReserveResult.Ok, "主角不应允许被移到替补。");
            _test.True(!string.IsNullOrEmpty(moveMainToReserveResult.Message), "下阵主角时应返回错误。");
            _test.True(HasMemberId(partyState.active_member_ids, "hero"), "主角被拒绝下阵后仍应保留在 active roster。");
            _test.False(HasMemberId(partyState.reserve_member_ids, "hero"), "主角被拒绝下阵后不应进入 reserve roster。");

            GameRuntimeFacade.RuntimeCommandResult invalidRosterResult =
                handler.CommandApplyPartyRosterTyped(
                new GStringNameArray { new StringName("mage") },
                new GStringNameArray { new StringName("hero") }
            );
            _test.False(
                invalidRosterResult.Ok,
                "非法编成不应通过 CommandApplyPartyRosterTyped()。"
            );
            _test.True(HasMemberId(partyState.active_member_ids, "hero"), "非法编成被拒绝后 active roster 不应丢失主角。");
            _test.False(HasMemberId(partyState.reserve_member_ids, "hero"), "非法编成被拒绝后 reserve roster 不应出现主角。");

            _test.Eq(runtime._party_warehouse_service.CountItem("bronze_sword"), 1, "装备前共享仓库应有一件青铜短剑。");
            GameRuntimeFacade.RuntimeCommandResult equipResult =
                handler.CommandPartyEquipItemTyped("hero", "bronze_sword", "main_hand", "");
            _test.True(equipResult.Ok, "装备物品应成功。");
            _test.Eq(runtime.GetPartySelectedMemberId().ToString(), "hero", "装备后应更新当前选中成员。");
            _test.Eq(partyState.GetMemberState("hero").equipment_state.GetEquippedItemId("main_hand").ToString(), "bronze_sword", "装备后主手槽应记录青铜短剑。");
            _test.Eq(runtime._party_warehouse_service.CountItem("bronze_sword"), 0, "装备后共享仓库中的对应装备实例应被消耗。");

            GameRuntimeFacade.RuntimeCommandResult unequipResult =
                handler.CommandPartyUnequipItemTyped("hero", "main_hand");
            _test.True(unequipResult.Ok, "卸装物品应成功。");
            _test.Eq(partyState.GetMemberState("hero").equipment_state.GetEquippedItemId("main_hand").ToString(), "", "卸装后主手槽应清空。");
            _test.Eq(runtime._party_warehouse_service.CountItem("bronze_sword"), 1, "卸装后装备实例应回到共享仓库。");

            handler.OnPartyManagementWarehouseRequested();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Warehouse, "打开共享仓库时应进入真实 warehouse modal。");
            _test.Eq(runtime._active_warehouse_entry_label, "队伍管理", "打开共享仓库时应保留队伍管理入口标签。");
            _test.Eq(runtime._current_status_message, "已从队伍管理打开共享仓库。", "打开共享仓库应刷新正式状态文案。");

            runtime._active_modal_kind = RuntimeModalKind.Party;
            partyState.pending_character_rewards.Add(BuildPendingReward());
            handler.OnPartyManagementWindowClosed();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Reward, "关闭队伍窗口后应恢复待确认角色奖励。");
            _test.True(runtime._active_reward != null, "恢复待确认奖励时应设置 active reward。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState, bool addBronzeSword)
    {
        IReadOnlyDictionary<StringName, ItemDef> typedItemDefs = new ItemContentRegistry().GetItemDefsTyped();
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

    private static PendingCharacterReward BuildPendingReward()
    {
        PendingCharacterRewardEntry entry = new()
        {
            entry_type = "skill_mastery",
            target_id = "test_skill",
            target_label = "测试技能",
            amount = 1,
            reason_text = "测试奖励",
        };
        return new PendingCharacterReward
        {
            reward_id = "party_command_reward",
            member_id = "hero",
            member_name = "Hero",
            source_type = "test_reward",
            source_id = "party_command_reward",
            source_label = "测试奖励",
            summary_text = "测试奖励",
            entries = new Godot.Collections.Array<PendingCharacterRewardEntry> { entry },
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
