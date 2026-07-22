using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_party_command_handler_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestFacadeUsesPartyHandlerSurface();
        TestRosterCommandsKeepCanonicalStateAndSelectionInSync();
        TestEquipmentPersistenceFailureKeepsSuccessfulMutation();

        RequestTestExit(_test.Finish("Game runtime party command handler regression"));
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

            runtime._party_command_handler.OnPartyManagementWindowClosed();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "OnPartyManagementWindowClosed() 应委托 handler 关闭 party modal。");
            _test.Eq(runtime._current_status_message, "已关闭队伍管理窗口。", "关闭队伍窗口应刷新正式状态文案。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestRosterCommandsKeepCanonicalStateAndSelectionInSync()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState, false);
        try
        {
            PartyState detachedCharacterManagementState = BuildPartyState();
            runtime._character_management.SetPartyState(detachedCharacterManagementState);
            _test.False(
                ReferenceEquals(runtime.GetPartyState(), runtime._character_management.GetPartyState()),
                "同步回归的前置条件应让 CharacterManagement 暂时绑定另一份 PartyState。"
            );

            GameRuntimeFacade.RuntimeCommandResult activateResult =
                runtime.CommandMoveMemberToActiveTyped("mage");
            _test.True(
                activateResult.Ok,
                $"替补转上阵应成功，即使 fixture 未绑定持久化 session。message={activateResult.Message}"
            );
            _test.True(
                HasMemberId(partyState.active_member_ids, "mage"),
                $"替补转上阵后 mage 应进入 active。active={MemberIdList(partyState.active_member_ids)}"
            );
            _test.False(
                HasMemberId(partyState.reserve_member_ids, "mage"),
                $"替补转上阵后 mage 应离开 reserve。reserve={MemberIdList(partyState.reserve_member_ids)}"
            );
            _test.Eq(
                runtime.GetPartySelectedMemberId().ToString(),
                "mage",
                "替补转上阵后仍应按原顺序更新选中成员。"
            );
            _test.True(
                ReferenceEquals(runtime.GetPartyState(), runtime._character_management.GetPartyState()),
                "编成变更后 CharacterManagement 应继续绑定 canonical PartyState。"
            );
            _test.True(
                runtime._current_status_message.EndsWith(
                    "但队伍状态持久化失败。",
                    StringComparison.Ordinal
                ),
                $"编成变更仍应尝试正式持久化，并仅在状态中告警。status={runtime._current_status_message}"
            );

            GameRuntimeFacade.RuntimeCommandResult mainCharacterReserveResult =
                runtime.CommandMoveMemberToReserveTyped("hero");
            _test.False(
                mainCharacterReserveResult.Ok,
                "存活主角仍必须保持上阵。"
            );
            _test.True(
                HasMemberId(partyState.active_member_ids, "hero"),
                "拒绝主角移至替补后不应改变 active roster。"
            );

            runtime.SetPartySelectedMemberId("hero");
            GameRuntimeFacade.RuntimeCommandResult leaderResult =
                runtime.CommandSetPartyLeaderTyped("mage");
            _test.True(leaderResult.Ok, "上阵成员应能切换为队长。");
            _test.Eq(
                partyState.leader_member_id,
                new StringName("mage"),
                "队长切换应修改 canonical PartyState。"
            );
            _test.Eq(
                runtime.GetPartySelectedMemberId().ToString(),
                "mage",
                "切换队长后应按原顺序选中该成员。"
            );

            runtime.SetPartySelectedMemberId("hero");
            GameRuntimeFacade.RuntimeCommandResult reserveResult =
                runtime.CommandMoveMemberToReserveTyped("mage");
            _test.True(reserveResult.Ok, "非主角成员应能从上阵移至替补。");
            _test.True(
                HasMemberId(partyState.reserve_member_ids, "mage"),
                $"移至替补后 mage 应进入 reserve。reserve={MemberIdList(partyState.reserve_member_ids)}"
            );
            _test.Eq(
                partyState.leader_member_id,
                new StringName("hero"),
                "当前队长离开 active 后应回退到第一名上阵成员。"
            );
            _test.Eq(
                runtime.GetPartySelectedMemberId().ToString(),
                "mage",
                "移至替补后仍应保留对该成员的选择。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestEquipmentPersistenceFailureKeepsSuccessfulMutation()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState, true);
        try
        {
            runtime.SetPartySelectedMemberId("mage");
            GameRuntimeFacade.RuntimeCommandResult equipResult =
                runtime.CommandPartyEquipItemTyped("hero", "bronze_sword", "", "");
            _test.True(
                equipResult.Ok,
                $"装备已成功时，后续持久化不可用不应把命令改判为失败。message={equipResult.Message}"
            );
            _test.Eq(
                partyState
                    .GetMemberState("hero")
                    ?.equipment_state
                    ?.GetEquippedItemId("main_hand") ?? "",
                new StringName("bronze_sword"),
                "持久化失败不应回滚已经完成的装备变更。"
            );
            _test.Eq(
                runtime._party_warehouse_service.CountItem("bronze_sword"),
                0,
                "持久化失败不应回滚已经完成的仓库扣减。"
            );
            _test.Eq(
                runtime.GetPartySelectedMemberId().ToString(),
                "hero",
                "装备成功后应在持久化前选中对应成员。"
            );
            _test.True(
                runtime._current_status_message.Contains(
                    "但队伍状态持久化失败。",
                    StringComparison.Ordinal
                ),
                $"持久化失败应只追加状态告警。status={runtime._current_status_message}"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState, bool addBronzeSword)
    {
        IReadOnlyDictionary<StringName, ItemDefinition> typedItemDefs =
            GameSessionTestFactory.GetProcessSnapshot().Items;
        int equipmentSerial = 1;
        Func<StringName> equipmentInstanceIdAllocator = () =>
            new StringName($"eq_party_command_{equipmentSerial++:000}");

        GameRuntimeFacade runtime = new()
        {
            _party_state = partyState,
            _generation_definition = TestWorldGenerationDefinitionFactory.Load(TestConfigPath),
        };
        runtime._world_map_data_context.active_generation_definition =
            runtime._generation_definition;
        runtime._character_management.setup(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            typedItemDefs,
            new Dictionary<StringName, QuestDefinition>(),
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
            new Dictionary<StringName, SkillDefinition>(),
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

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new StringNameList { new StringName("hero") },
            reserve_member_ids = new StringNameList { new StringName("mage") },
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
            entries = new List<PendingCharacterRewardEntry> { entry },
        };
    }

    private static bool HasMemberId(StringNameList memberIds, StringName memberId)
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

    private static string MemberIdList(StringNameList memberIds)
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
