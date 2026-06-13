using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_progression_text_snapshot_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPartyTextSnapshotRendersProgressionState();

        Quit(_test.Finish("Progression text snapshot regression"));
    }

    private void TestPartyTextSnapshotRendersProgressionState()
    {
        List<string> lines = TextSnapshotLines(
            GameTextSnapshotRenderer.RenderFullSnapshot(
                new GDictionary
                {
                    ["party"] = new GDictionary
                    {
                        ["gold"] = 0,
                        ["leader_member_id"] = "player_sword_01",
                        ["active_member_ids"] = new GArray { "player_sword_01" },
                        ["reserve_member_ids"] = new GArray(),
                        ["selected_member_id"] = "player_sword_01",
                        ["pending_reward_count"] = 0,
                        ["members"] = new GArray
                        {
                            new GDictionary
                            {
                                ["member_id"] = "player_sword_01",
                                ["roster_role"] = "active",
                                ["is_leader"] = true,
                                ["current_hp"] = 14,
                                ["current_mp"] = 20,
                                ["current_aura"] = 2,
                                ["achievement_summary"] = new GDictionary(),
                                ["attributes"] = new GDictionary { ["armor_class"] = 8 },
                                ["equipment"] = new GArray(),
                                ["unlocked_combat_resource_ids"] = new GArray
                                {
                                    "hp",
                                    "stamina",
                                    "mp",
                                    "aura",
                                },
                                ["active_core_skill_ids"] = new GArray { "warrior_heavy_strike" },
                                ["active_level_trigger_core_skill_id"] = "warrior_heavy_strike",
                                ["locked_level_trigger_skill_ids"] = new GArray { "mage_blink" },
                                ["blocked_relearn_skill_ids"] = new GArray { "old_focus" },
                                ["skill_entries"] = new GArray
                                {
                                    new GDictionary
                                    {
                                        ["skill_id"] = "warrior_heavy_strike",
                                        ["level"] = 3,
                                        ["is_core"] = true,
                                        ["assigned_profession_id"] = "warrior",
                                        ["is_level_trigger_active"] = true,
                                        ["is_level_trigger_locked"] = false,
                                        ["core_max_growth_claimed"] = false,
                                    },
                                    new GDictionary
                                    {
                                        ["skill_id"] = "mage_blink",
                                        ["level"] = 1,
                                        ["is_core"] = false,
                                        ["assigned_profession_id"] = "",
                                        ["is_level_trigger_active"] = false,
                                        ["is_level_trigger_locked"] = true,
                                        ["core_max_growth_claimed"] = true,
                                    },
                                },
                                ["profession_entries"] = new GArray
                                {
                                    new GDictionary
                                    {
                                        ["profession_id"] = "warrior",
                                        ["rank"] = 2,
                                        ["is_active"] = true,
                                        ["core_skill_ids"] = new GArray { "warrior_heavy_strike" },
                                        ["granted_skill_ids"] = new GArray { "warrior_guard_break" },
                                    },
                                },
                            },
                        },
                    },
                }
            )
        );

        AssertLine(
            lines,
            "member_progression=player_sword_01 | resources=hp stamina mp aura | aura=2 | active_core=warrior_heavy_strike | active_trigger=warrior_heavy_strike | locked_trigger=mage_blink | blocked_relearn=old_focus",
            "文本快照应渲染成员 progression 资源、核心和触发状态。"
        );
        AssertLine(
            lines,
            "member_skill=player_sword_01 | warrior_heavy_strike | lv=3 | core=true | trigger_active=true | trigger_locked=false | growth_claimed=false | profession=warrior",
            "文本快照应渲染核心技能等级和 active trigger 状态。"
        );
        AssertLine(
            lines,
            "member_skill=player_sword_01 | mage_blink | lv=1 | core=false | trigger_active=false | trigger_locked=true | growth_claimed=true | profession=",
            "文本快照应渲染 locked trigger 技能状态。"
        );
        AssertLine(
            lines,
            "member_profession=player_sword_01 | warrior | rank=2 | active=true | core=warrior_heavy_strike | granted=warrior_guard_break",
            "文本快照应渲染职业 rank、核心位和授予技能。"
        );
    }

    private static List<string> TextSnapshotLines(string text)
    {
        var lines = new List<string>();
        string normalized = (text ?? "").Replace("\r", "");
        foreach (string line in normalized.Split('\n'))
        {
            if (line.Length > 0)
                lines.Add(line);
        }
        return lines;
    }

    private void AssertLine(List<string> lines, string expected, string message)
    {
        foreach (string line in lines)
        {
            if (line == expected)
                return;
        }
        _test.Fail($"{message} | missing={expected} | line_count={lines.Count}");
    }
}
