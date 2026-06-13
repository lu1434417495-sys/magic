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
        Quit(_test.Finish("Battle map panel schema regression"));
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

