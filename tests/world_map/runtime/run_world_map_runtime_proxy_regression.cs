using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_world_map_runtime_proxy_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestGettersForwardToRuntime();
        TestSnapshotMethodsForwardToRuntime();
        TestPartyCommandsDelegateToRuntime();
        TestMissingRuntimeReturnsError();

        if (_failures.Count == 0)
        {
            GD.Print("World map runtime proxy regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map runtime proxy regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestGettersForwardToRuntime()
    {
        GameRuntimeFacade runtime = BuildRuntime(BuildPartyState());
        runtime._current_status_message = "runtime-status";
        runtime._active_modal_id = "settlement";
        runtime._active_settlement_id = "settlement_alpha";
        runtime._settlement_entry_active = true;
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
        runtime._pending_battle_start_prompt = new GDictionary
        {
            ["title"] = "开始战斗",
            ["confirm_text"] = "开始战斗",
        };
        runtime.set_battle_selection_target_unit_ids_state(
            new GStringNameArray { "enemy_alpha", "enemy_beta" }
        );

        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        try
        {
            AssertEq(proxy.GetStatusText(), "runtime-status", "GetStatusText() 应直接读取 runtime。");
            AssertEq(proxy.GetActiveModalId(), "settlement", "GetActiveModalId() 应直接读取 runtime。");
            AssertEq(proxy.GetActiveSettlementId(), "settlement_alpha", "GetActiveSettlementId() 应直接读取 runtime。");
            AssertEq(proxy.GetActiveMapId(), "ashen_ashlands", "GetActiveMapId() 应直接读取 runtime。");
            AssertEq(proxy.GetActiveMapDisplayName(), "灰烬地图", "GetActiveMapDisplayName() 应直接读取 runtime。");
            AssertEq(proxy.GetPendingBattleStartPrompt()["confirm_text"].AsString(), "开始战斗", "GetPendingBattleStartPrompt() 应直接读取 runtime。");
            AssertEq(proxy.GetPendingSubmapPrompt()["target_display_name"].AsString(), "灰烬地图", "GetPendingSubmapPrompt() 应直接读取 runtime。");
            AssertFalse(proxy.IsPlayerVisibleOnWorldMap(), "IsPlayerVisibleOnWorldMap() 应直接读取 runtime。");
            AssertTrue(proxy.IsSubmapActive(), "IsSubmapActive() 应直接读取 runtime。");
            AssertSequence(
                proxy.GetSelectedBattleSkillTargetUnitIds(),
                new[] { "enemy_alpha", "enemy_beta" },
                "GetSelectedBattleSkillTargetUnitIds() 应直接读取 runtime。"
            );
        }
        finally
        {
            proxy.dispose();
            runtime.dispose();
        }
    }

    private void TestSnapshotMethodsForwardToRuntime()
    {
        GameRuntimeFacade runtime = BuildRuntime(BuildPartyState());
        runtime._current_status_message = "runtime-status";
        runtime._world_map_data_context.active_map_id = "snapshot_map";
        runtime._world_map_data_context.active_map_display_name = "快照地图";

        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        try
        {
            GDictionary headlessSnapshot = proxy.BuildHeadlessSnapshot();
            AssertEq(
                DictString(Dict(headlessSnapshot, "status"), "text", ""),
                "runtime-status",
                "BuildHeadlessSnapshot() 应返回 runtime 快照。"
            );
            AssertEq(
                DictString(Dict(headlessSnapshot, "world"), "map_id", ""),
                "snapshot_map",
                "BuildHeadlessSnapshot() 应包含 runtime 世界上下文。"
            );
            AssertContains(proxy.BuildTextSnapshot(), "runtime-status", "BuildTextSnapshot() 应使用 runtime 快照渲染文本。");
        }
        finally
        {
            proxy.dispose();
            runtime.dispose();
        }
    }

    private void TestPartyCommandsDelegateToRuntime()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        try
        {
            GDictionary openResult = proxy.CommandOpenParty();
            AssertTrue(DictBool(openResult, "ok", false), $"CommandOpenParty() 应委托 runtime。message={DictString(openResult, "message", "")}");
            AssertEq(runtime._active_modal_id, "party", "CommandOpenParty() 成功后应更新 runtime modal。");
            AssertEq(proxy.GetPartySelectedMemberId().ToString(), "hero", "CommandOpenParty() 应通过 runtime 选中上阵第一人。");

            GDictionary selectResult = proxy.CommandSelectPartyMember("mage");
            AssertTrue(DictBool(selectResult, "ok", false), $"CommandSelectPartyMember() 应委托 runtime。message={DictString(selectResult, "message", "")}");
            AssertEq(proxy.GetPartySelectedMemberId().ToString(), "mage", "CommandSelectPartyMember() 应更新 runtime 选中成员。");

            GDictionary warehouseResult = proxy.CommandOpenPartyWarehouse();
            AssertTrue(DictBool(warehouseResult, "ok", false), $"CommandOpenPartyWarehouse() 应委托 runtime。message={DictString(warehouseResult, "message", "")}");
            AssertEq(runtime._active_modal_id, "warehouse", "CommandOpenPartyWarehouse() 成功后应打开 warehouse modal。");
            AssertEq(runtime._active_warehouse_entry_label, "队伍管理", "CommandOpenPartyWarehouse() 应保留正式入口标签。");
        }
        finally
        {
            proxy.dispose();
            runtime.dispose();
        }
    }

    private void TestMissingRuntimeReturnsError()
    {
        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(null);
        GDictionary result = proxy.CommandWorldMove(Vector2I.Right, 1);
        AssertFalse(DictBool(result, "ok", true), "缺少 runtime 时命令应返回失败。");
        AssertEq(DictString(result, "message", ""), "运行时尚未初始化。", "缺少 runtime 时应返回正式错误文案。");
        AssertEq(proxy.GetStatusText(), "", "缺少 runtime 时 getter 应返回安全默认值。");
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState)
    {
        GameRuntimeFacade runtime = new()
        {
            _party_state = partyState,
            _generation_config =
                ResourceLoader.Load<WorldMapGenerationConfig>(TestConfigPath)
                ?? new WorldMapGenerationConfig(),
        };
        runtime._world_map_data_context.active_generation_config = runtime._generation_config;
        runtime._world_map_data_context.active_world_data = new GDictionary
        {
            ["world_step"] = 0,
            ["world_events"] = new Godot.Collections.Array(),
            ["encounter_anchors"] = new Godot.Collections.Array(),
        };
        runtime._character_management.setup(
            partyState,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary()
        );
        runtime._battle_selection.setup(runtime);
        runtime._battle_session_facade.setup(runtime);
        runtime._party_command_handler.setup(runtime);
        runtime._warehouse_handler.setup(runtime);
        runtime._reward_flow_handler.setup(runtime);
        return runtime;
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
        partyState.set_member_state(BuildMember("hero", "Hero"));
        partyState.set_member_state(BuildMember("mage", "Mage"));
        return partyState;
    }

    private static PartyMemberState BuildMember(StringName memberId, string displayName)
    {
        return new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
            current_hp = 20,
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = displayName,
                unit_base_attributes = new UnitBaseAttributes(),
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
            _failures.Add($"{message} | actual={string.Join(",", values)} expected={string.Join(",", expected)}");
            return;
        }
        for (int index = 0; index < expected.Length; index++)
        {
            if (values[index] != expected[index])
            {
                _failures.Add($"{message} | actual={string.Join(",", values)} expected={string.Join(",", expected)}");
                return;
            }
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
