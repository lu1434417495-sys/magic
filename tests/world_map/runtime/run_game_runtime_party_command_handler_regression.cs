using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_party_command_handler_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFacadeUsesPartyHandlerSurface();
        TestPartyHandlerUpdatesRuntimeStateAndInventoryServices();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime party command handler regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime party command handler regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestFacadeUsesPartyHandlerSurface()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState, true);
        try
        {
            GDictionary openResult = runtime.command_open_party();
            AssertTrue(DictBool(openResult, "ok", false), $"command_open_party() 应委托给正式 party handler。message={DictString(openResult, "message", "")}");
            AssertEq(runtime._active_modal_id, "party", "facade 打开队伍管理后应切换到 party modal。");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "hero", "facade 打开队伍管理后应默认选中上阵第一人。");

            GDictionary selectResult = runtime.command_select_party_member("mage");
            AssertTrue(DictBool(selectResult, "ok", false), "command_select_party_member() 应委托给正式 party handler。");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "mage", "facade 选中队员后应同步选中成员。");

            runtime._on_party_management_warehouse_requested();
            AssertEq(runtime._active_modal_id, "warehouse", "_on_party_management_warehouse_requested() 应委托 handler 打开真实仓库 modal。");
            AssertEq(runtime._active_warehouse_entry_label, "队伍管理", "队伍管理打开仓库时应保留正式入口标签。");

            runtime.set_runtime_active_modal_id("party");
            runtime._on_party_management_window_closed();
            AssertEq(runtime._active_modal_id, "", "_on_party_management_window_closed() 应委托 handler 关闭 party modal。");
            AssertEq(runtime._current_status_message, "已关闭队伍管理窗口。", "关闭队伍窗口应刷新正式状态文案。");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestPartyHandlerUpdatesRuntimeStateAndInventoryServices()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState, true);
        GameRuntimePartyCommandHandler handler = runtime._party_command_handler;
        try
        {
            GDictionary openResult = handler.command_open_party();
            AssertTrue(DictBool(openResult, "ok", false), $"打开队伍管理应成功。message={DictString(openResult, "message", "")}");
            AssertEq(runtime._active_modal_id, "party", "打开队伍管理应切换 modal。");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "hero", "打开队伍管理应默认选中上阵第一人。");

            GDictionary selectResult = handler.command_select_party_member("mage");
            AssertTrue(DictBool(selectResult, "ok", false), "选中队员应成功。");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "mage", "选中队员后应同步选中标记。");

            GDictionary ghostSelectResult = handler.command_select_party_member("ghost");
            AssertFalse(DictBool(ghostSelectResult, "ok", true), "不在 active/reserve roster 的成员不应允许被选中。");
            AssertContains(DictString(ghostSelectResult, "message", ""), "不在队伍编成中", "越权选中非 roster 成员时应返回明确错误。");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "mage", "越权选中失败后不应改写当前选中成员。");

            GDictionary leaderResult = handler.command_set_party_leader("hero");
            AssertTrue(DictBool(leaderResult, "ok", false), "设置队长应成功。");
            AssertEq(partyState.leader_member_id.ToString(), "hero", "设置队长后应更新队长成员。");
            AssertTrue(runtime._character_management.get_party_state() == partyState, "设置队长后应同步队伍状态到角色管理。");

            GDictionary rosterResult = handler.command_move_member_to_active("mage");
            AssertTrue(DictBool(rosterResult, "ok", false), $"移动成员到上阵应成功。message={DictString(rosterResult, "message", "")} status={runtime._current_status_message}");
            AssertTrue(HasMemberId(partyState.active_member_ids, "mage"), $"移动到上阵后应更新 active 列表。active={MemberIdList(partyState.active_member_ids)} reserve={MemberIdList(partyState.reserve_member_ids)} status={runtime._current_status_message}");
            AssertFalse(HasMemberId(partyState.reserve_member_ids, "mage"), $"移动到上阵后应从 reserve 列表移除。active={MemberIdList(partyState.active_member_ids)} reserve={MemberIdList(partyState.reserve_member_ids)} status={runtime._current_status_message}");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "mage", "移动成员后应保持当前选中成员。");

            GDictionary moveMainToReserveResult = handler.command_move_member_to_reserve("hero");
            AssertFalse(DictBool(moveMainToReserveResult, "ok", true), "主角不应允许被移到替补。");
            AssertContains(DictString(moveMainToReserveResult, "message", ""), "主角必须保持上阵", "下阵主角时应返回明确错误。");
            AssertTrue(HasMemberId(partyState.active_member_ids, "hero"), "主角被拒绝下阵后仍应保留在 active roster。");
            AssertFalse(HasMemberId(partyState.reserve_member_ids, "hero"), "主角被拒绝下阵后不应进入 reserve roster。");

            GDictionary invalidRosterResult = handler.apply_party_roster(
                new GStringNameArray { new StringName("mage") },
                new GStringNameArray { new StringName("hero") }
            );
            AssertFalse(DictBool(invalidRosterResult, "ok", true), "非法编成不应通过 apply_party_roster()。");
            AssertTrue(HasMemberId(partyState.active_member_ids, "hero"), "非法编成被拒绝后 active roster 不应丢失主角。");
            AssertFalse(HasMemberId(partyState.reserve_member_ids, "hero"), "非法编成被拒绝后 reserve roster 不应出现主角。");

            AssertEq(runtime._party_warehouse_service.count_item("bronze_sword"), 1, "装备前共享仓库应有一件青铜短剑。");
            GDictionary equipResult = handler.command_party_equip_item("hero", "bronze_sword", "main_hand", "");
            AssertTrue(DictBool(equipResult, "ok", false), "装备物品应成功。");
            AssertEq(runtime.get_party_selected_member_id().ToString(), "hero", "装备后应更新当前选中成员。");
            AssertContains(runtime._current_status_message, "青铜短剑", "装备成功消息应包含正式物品名称。");
            AssertEq(partyState.get_member_state("hero").equipment_state.get_equipped_item_id("main_hand").ToString(), "bronze_sword", "装备后主手槽应记录青铜短剑。");
            AssertEq(runtime._party_warehouse_service.count_item("bronze_sword"), 0, "装备后共享仓库中的对应装备实例应被消耗。");

            GDictionary unequipResult = handler.command_party_unequip_item("hero", "main_hand");
            AssertTrue(DictBool(unequipResult, "ok", false), "卸装物品应成功。");
            AssertEq(partyState.get_member_state("hero").equipment_state.get_equipped_item_id("main_hand").ToString(), "", "卸装后主手槽应清空。");
            AssertEq(runtime._party_warehouse_service.count_item("bronze_sword"), 1, "卸装后装备实例应回到共享仓库。");

            handler.on_party_management_warehouse_requested();
            AssertEq(runtime._active_modal_id, "warehouse", "打开共享仓库时应进入真实 warehouse modal。");
            AssertEq(runtime._active_warehouse_entry_label, "队伍管理", "打开共享仓库时应保留队伍管理入口标签。");
            AssertEq(runtime._current_status_message, "已从队伍管理打开共享仓库。", "打开共享仓库应刷新正式状态文案。");

            runtime._active_modal_id = "party";
            partyState.pending_character_rewards.Add(BuildPendingReward());
            handler.on_party_management_window_closed();
            AssertEq(runtime._active_modal_id, "reward", "关闭队伍窗口后应恢复待确认角色奖励。");
            AssertTrue(runtime._active_reward != null, "恢复待确认奖励时应设置 active reward。");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState, bool addBronzeSword)
    {
        GDictionary itemDefs = new ItemContentRegistry().get_item_defs();
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
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            itemDefs,
            new GDictionary(),
            equipmentInstanceIdAllocator,
            new GDictionary()
        );
        runtime._party_warehouse_service.setup(
            partyState,
            itemDefs,
            equipmentInstanceIdAllocator
        );
        runtime._party_item_use_service.setup(
            partyState,
            itemDefs,
            new GDictionary(),
            runtime._party_warehouse_service,
            runtime._character_management
        );
        runtime._party_equipment_service.setup(
            partyState,
            itemDefs,
            runtime._party_warehouse_service,
            equipmentInstanceIdAllocator
        );
        runtime._warehouse_handler.setup(runtime);
        runtime._party_command_handler.setup(runtime);
        runtime._reward_flow_handler.setup(runtime);

        if (addBronzeSword)
        {
            runtime._party_warehouse_service.add_item("bronze_sword", 1);
        }

        return runtime;
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
        partyState.set_member_state(BuildMember("hero", "Hero"));
        partyState.set_member_state(BuildMember("mage", "Mage"));
        partyState.set_member_state(BuildMember("ghost", "Ghost"));
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
        attributes.set_attribute_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(), 8);

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

    private void AssertContains(string actual, string expectedSubstring, string message)
    {
        if (actual == null || !actual.Contains(expectedSubstring))
        {
            _failures.Add($"{message} | actual={actual} expected_substring={expectedSubstring}");
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
