using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// A2.1 data-layer regression: locks the command_dock / hint_text /
// recent_battle_log_lines contract that BattleHudAdapter.BuildSnapshot now
// exposes for the rebuilt battle command dock. These derive purely from
// battle_state + selection inputs, so the fixture needs no content catalog.
public partial class run_battle_command_dock_snapshot_regression : SceneTree
{
    private static readonly StringName SkillId = "test_command_dock_skill";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestManualUnitActingDockAndHint();
            TestSelectedSkillDockAndSingleTargetHint();
            TestModalBlockDisablesDock();
            TestAutoModeHint();
            TestRecentBattleLogLinesTrimAndOrder();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        Quit(_test.Finish("Battle command dock snapshot regression"));
    }

    private void TestManualUnitActingDockAndHint()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "manual");
        GDictionary snapshot = BuildSnapshot(fixture, new StringName(""));
        GDictionary dock = Dock(snapshot);

        _test.True(DockBool(dock, "resolve_enabled"), "手动单位行动期 resolve_enabled 应为真。");
        _test.False(DockBool(dock, "clear_skill_enabled"), "未选技能时 clear_skill_enabled 应为假。");
        _test.False(DockBool(dock, "prev_variant_enabled"), "未选技能时 prev_variant_enabled 应为假。");
        _test.False(DockBool(dock, "next_variant_enabled"), "未选技能时 next_variant_enabled 应为假。");
        _test.Eq(
            Hint(snapshot),
            "点选技能或移动；Enter 结束行动",
            "未选技能的手动单位应提示选择技能或移动。"
        );
    }

    private void TestSelectedSkillDockAndSingleTargetHint()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "manual");
        GDictionary snapshot = BuildSnapshot(fixture, SkillId);
        GDictionary dock = Dock(snapshot);

        _test.True(DockBool(dock, "clear_skill_enabled"), "已选技能时 clear_skill_enabled 应为真。");
        // No content catalog → the adapter cannot resolve cast variants, so the
        // wraparound-cycle buttons stay disabled (count <= 1).
        _test.False(DockBool(dock, "prev_variant_enabled"), "无可切换形态时 prev_variant_enabled 应为假。");
        _test.False(DockBool(dock, "next_variant_enabled"), "无可切换形态时 next_variant_enabled 应为假。");
        _test.Eq(
            Hint(snapshot),
            "左键选择目标格释放；Esc 取消，Q/E 切换形态",
            "单体目标技能应提示左键选择目标格。"
        );
    }

    private void TestModalBlockDisablesDock()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "manual");
        fixture.State.modal_state = "battle_resolving";
        GDictionary snapshot = BuildSnapshot(fixture, SkillId);
        GDictionary dock = Dock(snapshot);

        _test.False(DockBool(dock, "resolve_enabled"), "模态阻断时 resolve_enabled 应为假。");
        _test.False(DockBool(dock, "clear_skill_enabled"), "模态阻断时 clear_skill_enabled 应为假。");
        _test.Eq(Hint(snapshot), "战斗结算中…请稍候", "模态阻断时应提示战斗结算中。");
    }

    private void TestAutoModeHint()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "ai");
        GDictionary snapshot = BuildSnapshot(fixture, new StringName(""));
        GDictionary dock = Dock(snapshot);

        _test.False(DockBool(dock, "resolve_enabled"), "自动模式单位 resolve_enabled 应为假。");
        _test.Eq(Hint(snapshot), "自动模式：等待 AI 行动", "自动控制单位应提示等待 AI 行动。");
    }

    private void TestRecentBattleLogLinesTrimAndOrder()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "manual");
        fixture.State.log_entries.Clear();
        fixture.State.log_entries.Add("第一条");
        fixture.State.log_entries.Add("第二条");
        fixture.State.log_entries.Add("   ");
        fixture.State.log_entries.Add("第三条");
        fixture.State.log_entries.Add("第四条");

        GDictionary snapshot = BuildSnapshot(fixture, new StringName(""));
        GStringArray lines = snapshot["recent_battle_log_lines"].As<GStringArray>();

        _test.Eq(lines.Count, 3, "recent_battle_log_lines 应裁剪到最近 3 条非空记录。");
        _test.Eq(lines[0], "第二条", "应保留最旧在前的顺序（裁剪后第 1 条）。");
        _test.Eq(lines[1], "第三条", "空白记录应被跳过。");
        _test.Eq(lines[2], "第四条", "最新记录应排在最后。");
    }

    private static BattleTestFixture BuildFixture(out BattleUnitState caster, StringName controlMode)
    {
        caster = BattleTestFixture.BuildUnit("dock_caster", "player", new Vector2I(1, 1), currentAp: 3);
        caster.control_mode = controlMode;
        caster.known_active_skill_ids.Add(SkillId);
        caster.known_skill_level_map[SkillId] = 1;
        BattleUnitState enemy = BattleTestFixture.BuildUnit("dock_enemy", "enemy", new Vector2I(3, 1));
        return BattleTestFixture.CreateFlatBattle(
            "command_dock_snapshot",
            new Vector2I(6, 6),
            new[] { caster },
            new[] { enemy }
        );
    }

    private static GDictionary BuildSnapshot(BattleTestFixture fixture, StringName selectedSkillId)
    {
        using var adapter = new BattleHudAdapter();
        return adapter.BuildSnapshot(
            fixture.State,
            new Vector2I(1, 1),
            selectedSkillId,
            selectedSkillId == "" ? "" : "指令带技能",
            "",
            new GVector2IArray(),
            1,
            new GStringNameArray(),
            new StringName(""),
            "遭遇",
            null
        );
    }

    private static GDictionary Dock(GDictionary snapshot)
    {
        return snapshot.ContainsKey("command_dock")
            ? snapshot["command_dock"].As<GDictionary>()
            : new GDictionary();
    }

    private static bool DockBool(GDictionary dock, string key)
    {
        return dock.ContainsKey(key) && dock[key].AsBool();
    }

    private static string Hint(GDictionary snapshot)
    {
        return snapshot.ContainsKey("hint_text") ? snapshot["hint_text"].AsString() : "";
    }
}
