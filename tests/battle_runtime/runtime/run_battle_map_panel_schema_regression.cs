using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_map_panel_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private static readonly PackedScene BattleMapPanelScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_map_panel.tscn"
    );

    public override async void _Initialize()
    {
        await TestBattleMapPanelAppliesFormalSnapshot();
        await TestBattleMapPanelAppliesCommandDock();
        Quit(_test.Finish("Battle map panel schema regression"));
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelAppliesCommandDock()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        panel._apply_snapshot(
            new GDictionary
            {
                ["focus_unit"] = new GDictionary { ["resource_info"] = new GDictionary() },
                ["queue_entries"] = new GArray(),
                ["skill_slots"] = new GArray(),
                ["selected_skill_fate_badges"] = new GArray(),
                ["equipment_panel"] = new GDictionary(),
                ["selected_skill_variant_name"] = "重击形态",
                ["selected_skill_target_selection_mode"] = "multi_unit",
                ["selected_skill_target_count"] = 2,
                ["selected_skill_target_max_count"] = 3,
                ["hint_text"] = "继续点选目标，还需 1 个；Esc 取消",
                ["recent_battle_log_lines"] = new GArray { "甲 命中 乙", "乙 倒地" },
                ["selected_skill_confirm_ready"] = true,
                ["command_dock"] = new GDictionary
                {
                    ["resolve_enabled"] = true,
                    ["clear_skill_enabled"] = true,
                    ["prev_variant_enabled"] = true,
                    ["next_variant_enabled"] = false,
                },
            }
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(panel.resolve_button.Disabled, "resolve_enabled=true 时结算按钮应可用。");
        _test.True(
            panel.resolve_button.HasThemeColorOverride("font_color"),
            "selected_skill_confirm_ready=true 时结算按钮应高亮（font_color override）。"
        );
        _test.False(panel.clear_skill_button.Disabled, "clear_skill_enabled=true 时取消按钮应可用。");
        _test.False(panel.prev_variant_button.Disabled, "prev_variant_enabled=true 时上一形态按钮应可用。");
        _test.True(panel.next_variant_button.Disabled, "next_variant_enabled=false 时下一形态按钮应置灰。");
        _test.Eq(panel.variant_name_label.Text, "重击形态", "VariantNameLabel 应显示 selected_skill_variant_name。");
        _test.Eq(panel.command_summary_label.Text, "已选 2/3 目标", "CommandSummaryLabel 应汇总多目标进度。");
        _test.Eq(panel.hint_label.Text, "继续点选目标，还需 1 个；Esc 取消", "HintLabel 应显示 hint_text。");
        _test.Eq(panel.log_label.Text, "甲 命中 乙\n乙 倒地", "LogLabel 应换行拼接 recent_battle_log_lines。");

        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async System.Threading.Tasks.Task TestBattleMapPanelAppliesFormalSnapshot()
    {
        var panel = BattleMapPanelScene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        panel._apply_snapshot(
            new GDictionary
            {
                ["header_title"] = "战斗地图",
                ["round_badge"] = new GDictionary
                {
                    ["tu_text"] = "TU 12",
                    ["ready_text"] = "READY 3",
                },
                ["mode_text"] = "手动",
                ["focus_unit"] = new GDictionary
                {
                    ["glyph"] = "W",
                    ["name"] = "见习战士",
                    ["role_text"] = "玩家前排",
                    ["edge_color"] = new Color(0.9f, 0.5f, 0.2f, 1.0f),
                    ["primary_color"] = new Color(0.2f, 0.2f, 0.2f, 1.0f),
                    ["hp_current"] = 18,
                    ["hp_max"] = 30,
                    ["mp_current"] = 6,
                    ["mp_max"] = 10,
                    ["move_current"] = 3,
                    ["move_max"] = 5,
                    ["resource_info"] = new GDictionary(),
                },
                ["queue_entries"] = new GArray(),
                ["skill_slots"] = new GArray(),
                ["skill_subtitle"] = "预计命中率 75%",
                ["selected_skill_preview_tooltip_text"] = "需要掷出 6+",
                ["selected_skill_fate_badges"] = new GArray(),
                ["equipment_panel"] = new GDictionary(),
            }
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.False(panel.Visible, "仅应用 snapshot 不应强制改变 BattleMapPanel 可见性。");
        _test.Eq((int)panel.hp_bar.Value, 18, "BattleMapPanel 应应用 formal focus_unit.hp_current。");
        _test.Eq((int)panel.hp_bar.MaxValue, 30, "BattleMapPanel 应应用 formal focus_unit.hp_max。");
        _test.Eq((int)panel.mp_bar.Value, 6, "BattleMapPanel 应应用 formal focus_unit.mp_current。");
        _test.Eq((int)panel.mp_bar.MaxValue, 10, "BattleMapPanel 应应用 formal focus_unit.mp_max。");

        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }
}

