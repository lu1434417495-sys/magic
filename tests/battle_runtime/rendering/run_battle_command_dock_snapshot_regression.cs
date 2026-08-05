using System;
using System.Collections.Generic;
using Godot;

// A2.1 data-layer regression: locks the command_dock / hint_text /
// recent_battle_log_lines contract that BattleHudAdapter.BuildSnapshot now
// exposes for the rebuilt battle command dock. These derive purely from
// battle_state + selection inputs, so the fixture needs no content catalog.
public partial class run_battle_command_dock_snapshot_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "test_command_dock_skill";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestManualUnitActingDockAndHint();
            TestSelectedSkillDockAndSingleTargetHint();
            TestWindupSkillDockAndHint();
            TestModalBlockDisablesDock();
            TestAutoModeHint();
            TestRecentBattleLogLinesTrimAndOrder();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Battle command dock snapshot regression"));
    }

    private void TestManualUnitActingDockAndHint()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "manual");
        BattleHudSnapshot snapshot = BuildSnapshot(fixture, new StringName(""));
        BattleHudCommandDockSnapshot dock = snapshot.CommandDock;

        _test.True(dock.ResolveEnabled, "手动单位行动期 resolve_enabled 应为真。");
        _test.False(dock.ClearSkillEnabled, "未选技能时 clear_skill_enabled 应为假。");
        _test.False(dock.PrevVariantEnabled, "未选技能时 prev_variant_enabled 应为假。");
        _test.False(dock.NextVariantEnabled, "未选技能时 next_variant_enabled 应为假。");
        _test.Eq(
            Hint(snapshot),
            "点选技能或移动；Enter 结束行动",
            "未选技能的手动单位应提示选择技能或移动。"
        );
    }

    private void TestSelectedSkillDockAndSingleTargetHint()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "manual");
        BattleHudSnapshot snapshot = BuildSnapshot(fixture, SkillId);
        BattleHudCommandDockSnapshot dock = snapshot.CommandDock;

        _test.True(dock.ClearSkillEnabled, "已选技能时 clear_skill_enabled 应为真。");
        // No content catalog → the adapter cannot resolve cast variants, so the
        // wraparound-cycle buttons stay disabled (count <= 1).
        _test.False(dock.PrevVariantEnabled, "无可切换形态时 prev_variant_enabled 应为假。");
        _test.False(dock.NextVariantEnabled, "无可切换形态时 next_variant_enabled 应为假。");
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
        BattleHudSnapshot snapshot = BuildSnapshot(fixture, SkillId);
        BattleHudCommandDockSnapshot dock = snapshot.CommandDock;

        _test.False(dock.ResolveEnabled, "模态阻断时 resolve_enabled 应为假。");
        _test.False(dock.ClearSkillEnabled, "模态阻断时 clear_skill_enabled 应为假。");
        _test.Eq(Hint(snapshot), "战斗结算中…请稍候", "模态阻断时应提示战斗结算中。");
    }

    private void TestWindupSkillDockAndHint()
    {
        using BattleTestFixture fixture = BuildFixture(
            out BattleUnitState caster,
            "manual"
        );
        SkillDefinition windupSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_heavy_blow.tres",
            "battle_command_dock_windup"
        );
        caster.AddKnownActiveSkill(windupSkill.SkillId);
        caster.SetKnownSkillLevelTyped(windupSkill.SkillId, 2);
        caster.attribute_snapshot.SetValue("constitution_modifier", 4);
        using var adapter = new BattleHudAdapter();
        adapter.SetupRuntimeContext(new WindupHudContext(fixture.State, windupSkill));
        BattleHudSnapshot snapshot = adapter.BuildSnapshot(
            fixture.State,
            new Vector2I(1, 1),
            windupSkill.SkillId,
            windupSkill.DisplayName,
            "蓄力 1 挡 · 10 TU · 18 体力 · 2W",
            Array.Empty<Vector2I>(),
            1,
            Array.Empty<StringName>(),
            new StringName(""),
            "遭遇",
            null
        );
        BattleHudCommandDockSnapshot dock = snapshot.CommandDock;

        _test.True(dock.PrevVariantEnabled, "多个蓄力挡位时 Q 向前切换应启用。");
        _test.True(dock.NextVariantEnabled, "多个蓄力挡位时 E 向后切换应启用。");
        _test.Eq(
            Hint(snapshot),
            "左键选择目标释放；Esc 清除未确认选择，Q/E 调整蓄力挡位",
            "蓄力技能 HUD 应区分未确认选择与进入 pending 后不可取消。"
        );
    }

    private void TestAutoModeHint()
    {
        using BattleTestFixture fixture = BuildFixture(out BattleUnitState caster, "ai");
        BattleHudSnapshot snapshot = BuildSnapshot(fixture, new StringName(""));
        BattleHudCommandDockSnapshot dock = snapshot.CommandDock;

        _test.False(dock.ResolveEnabled, "自动模式单位 resolve_enabled 应为假。");
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

        BattleHudSnapshot snapshot = BuildSnapshot(fixture, new StringName(""));
        var lines = snapshot.RecentBattleLogLines;

        _test.Eq(lines.Count, 3, "recent_battle_log_lines 应裁剪到最近 3 条非空记录。");
        _test.Eq(lines[0], "第二条", "应保留最旧在前的顺序（裁剪后第 1 条）。");
        _test.Eq(lines[1], "第三条", "空白记录应被跳过。");
        _test.Eq(lines[2], "第四条", "最新记录应排在最后。");
    }

    private static BattleTestFixture BuildFixture(out BattleUnitState caster, StringName controlMode)
    {
        caster = BattleTestFixture.BuildUnit("dock_caster", "player", new Vector2I(1, 1), currentAp: 3);
        caster.control_mode = controlMode;
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 1);
        BattleUnitState enemy = BattleTestFixture.BuildUnit("dock_enemy", "enemy", new Vector2I(3, 1));
        return BattleTestFixture.CreateFlatBattle(
            "command_dock_snapshot",
            new Vector2I(6, 6),
            new[] { caster },
            new[] { enemy }
        );
    }

    private static BattleHudSnapshot BuildSnapshot(
        BattleTestFixture fixture,
        StringName selectedSkillId
    )
    {
        using var adapter = new BattleHudAdapter();
        return adapter.BuildSnapshot(
            fixture.State,
            new Vector2I(1, 1),
            selectedSkillId,
            selectedSkillId == "" ? "" : "指令带技能",
            "",
            Array.Empty<Vector2I>(),
            1,
            Array.Empty<StringName>(),
            new StringName(""),
            "遭遇",
            null
        );
    }

    private static string Hint(BattleHudSnapshot snapshot) => snapshot?.HintText ?? "";

    private sealed class WindupHudContext : IBattleHudContext
    {
        private static readonly IReadOnlyDictionary<
            StringName,
            EquipmentAbilityBindingDefinition
        > EmptyBindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
        private static readonly IReadOnlyDictionary<StringName, ItemDefinition> EmptyItems =
            new Dictionary<StringName, ItemDefinition>();
        private readonly BattleState _state;
        private readonly IReadOnlyDictionary<StringName, SkillDefinition> _skills;

        internal WindupHudContext(BattleState state, SkillDefinition skillDefinition)
        {
            _state = state;
            _skills = new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            };
        }

        public BattleState GetBattleState() => _state;

        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
            GetEquipmentAbilityBindings() => EmptyBindings;

        public int GetBattleWorldStep() => 0;

        public BattlePreview PreviewBattleCommand(BattleCommand command) => null;

        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitions() =>
            EmptyItems;

        public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitions() =>
            _skills;

        public ISkillCatalog GetSkillCatalog() => null;

        public PartyMemberState GetPartyMemberState(StringName memberId) => null;

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName memberId,
            EquipmentState equipmentView
        ) => null;

        public string GetBattleSkillCastBlockMessage(
            BattleUnitState activeUnit,
            StringName skillId
        ) => "";
    }
}
