using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_system_surface_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestWorldMapSystemDoesNotExposeRuntimePassthroughSurface();
        TestStagecoachModalAcceptsOnlyFormalTargetPayload();
        TestWorldMapRuntimeProxyKeepsExpectedContract();

        if (_failures.Count == 0)
        {
            GD.Print("World map system surface regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map system surface regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestWorldMapSystemDoesNotExposeRuntimePassthroughSurface()
    {
        WorldMapSystem system = new();
        try
        {
            AssertTrue(system.HasMethod("_render_from_runtime"), "WorldMapSystem 应保留渲染同步入口。");
            AssertTrue(system.HasMethod("_on_world_map_cell_clicked"), "WorldMapSystem 应保留场景回调。");
            AssertTrue(system.HasMethod("_on_character_reward_confirmed"), "WorldMapSystem 应保留窗口回调。");

            string[] forbiddenMethods =
            {
                "get_status_text",
                "get_active_modal_id",
                "get_active_settlement_id",
                "build_headless_snapshot",
                "build_text_snapshot",
                "command_world_move",
                "command_world_select",
                "command_open_settlement",
                "command_world_inspect",
                "command_open_party",
                "command_select_party_member",
                "command_set_party_leader",
                "command_move_member_to_active",
                "command_move_member_to_reserve",
                "command_open_party_warehouse",
                "command_warehouse_discard_one",
                "command_warehouse_discard_all",
                "command_warehouse_use_item",
                "command_execute_settlement_action",
                "command_battle_tick",
                "command_battle_select_skill",
                "command_battle_cycle_variant",
                "command_battle_clear_skill",
                "command_battle_move_to",
                "command_battle_move_direction",
                "command_battle_wait_or_resolve",
                "command_battle_inspect",
                "command_confirm_pending_reward",
                "command_choose_promotion",
                "command_close_active_modal",
                "apply_party_roster",
                "submit_promotion_choice",
                "cancel_promotion_choice",
                "confirm_active_reward",
                "reset_battle_focus",
                "select_world_cell",
                "inspect_world_cell",
                "select_battle_cell",
                "inspect_battle_cell",
                "_on_settlement_shop_requested",
                "_on_settlement_stagecoach_requested",
                "_open_local_service_window",
            };

            foreach (string methodName in forbiddenMethods)
            {
                AssertFalse(
                    system.HasMethod(methodName),
                    $"WorldMapSystem 不应再暴露 {methodName} 这类 runtime 透传 API。"
                );
            }
        }
        finally
        {
            system.Free();
        }
    }

    private void TestStagecoachModalAcceptsOnlyFormalTargetPayload()
    {
        GameRuntimeFacade runtime = BuildRuntime();
        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        WorldMapSystem system = new()
        {
            _runtime = runtime,
            _runtime_proxy = proxy,
        };
        try
        {
            runtime._current_status_message = "unchanged";
            system._on_stagecoach_service_modal_action_requested(
                "spring_village_01",
                "service:stagecoach",
                new GDictionary { ["settlement_id"] = "legacy_destination" }
            );
            AssertEq(
                runtime._current_status_message,
                "unchanged",
                "Stagecoach modal payload 只有 settlement_id 时不应触发旅行命令。"
            );

            system._on_stagecoach_service_modal_action_requested(
                "spring_village_01",
                "service:stagecoach",
                new GDictionary { ["target_settlement_id"] = "north_outpost" }
            );
            AssertEq(
                runtime._current_status_message,
                "当前没有打开驿站路线窗口。",
                "Stagecoach modal 使用 target_settlement_id 时应委托正式旅行命令。"
            );
        }
        finally
        {
            proxy.dispose();
            runtime.dispose();
            system.Free();
        }
    }

    private void TestWorldMapRuntimeProxyKeepsExpectedContract()
    {
        WorldMapRuntimeProxy proxy = new();
        try
        {
            string[] expectedMethods =
            {
                "GetStatusText",
                "GetLogSnapshot",
                "GetActiveModalId",
                "GetActiveSettlementId",
                "GetActiveMapId",
                "GetPendingBattleStartPrompt",
                "GetSelectedBattleSkillTargetUnitIds",
                "BuildHeadlessSnapshot",
                "BuildTextSnapshot",
                "CommandWorldMove",
                "CommandConfirmSubmapEntry",
                "CommandConfirmBattleStart",
                "CommandReturnFromSubmap",
                "CommandBattleWaitOrResolve",
                "ResetBattleFocus",
                "SelectWorldCell",
                "SelectBattleCell",
            };

            foreach (string methodName in expectedMethods)
            {
                AssertTrue(
                    typeof(WorldMapRuntimeProxy).GetMethod(
                        methodName,
                        BindingFlags.Public | BindingFlags.Instance
                    ) != null,
                    $"WorldMapRuntimeProxy 应保留 {methodName} 作为场景层正式边界。"
                );
            }
        }
        finally
        {
            proxy.dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime()
    {
        GameRuntimeFacade runtime = new();
        runtime._settlement_command_handler.setup(runtime);
        return runtime;
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
}
