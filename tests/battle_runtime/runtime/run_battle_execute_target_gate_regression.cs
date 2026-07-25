using System.Collections.Generic;
using Godot;

public partial class run_battle_execute_target_gate_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHighHpTargetPreviewDeniedWithoutSaveDamageOrStatus();
        TestHighHpTargetAffordanceDenied();
        TestHighHpCommandDoesNotConsumeApMpCooldown();
        TestLowHpTargetPreviewAllowed();
        TestBossAndNormalUseSameThresholdGate();
        RequestTestExit(_test.Finish("Battle execute target gate regression"));
    }

    private void TestHighHpTargetPreviewDeniedWithoutSaveDamageOrStatus()
    {
        SkillDefinition skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_preview", skill, source, target);
        BattleCommand command = MakeUnitCommand(source, target, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview.allowed, "高 HP 律令死亡目标应在 preview 阶段被拒绝。");
        _test.Eq(target.GetCurrentHp(), 21, "高 HP preview 不应造成伤害。");
        _test.False(target.HasStatusEffect("soul_fracture"), "高 HP preview 不应附加 soul fracture。");

        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestHighHpTargetAffordanceDenied()
    {
        SkillDefinition skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_affordance", skill, source, target);
        SkillDefinition skillDefinition = fixture.Runtime.GetSkillDefinitionTyped(skill.SkillId);
        BattleUnitSkillTargetAffordance affordance =
            fixture.Runtime.GetUnitSkillTargetAffordance(source, target, skillDefinition);

        _test.False(affordance.Allowed, "高 HP 律令死亡目标 affordance 应标记为不可选。");
        _test.True(
            affordance.Reason.Contains("生命高于律令死亡阈值"),
            "高 HP 律令死亡 affordance 应返回玩家可读的阈值原因。"
        );
    }

    private void TestHighHpCommandDoesNotConsumeApMpCooldown()
    {
        SkillDefinition skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        source.SetCurrentAp(2);
        source.SetCurrentMp(20);
        source.SetCooldownTyped(skill.SkillId, 0);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_issue", skill, source, target);
        BattleCommand command = MakeUnitCommand(source, target, skill);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(source.GetCurrentAp(), 2, "高 HP 律令死亡命令失败时不应消耗 AP。");
        _test.Eq(source.GetCurrentMp(), 20, "高 HP 律令死亡命令失败时不应消耗 MP。");
        _test.Eq(source.GetCooldownTyped(skill.SkillId), 0, "高 HP 律令死亡命令失败时不应进入冷却。");
        _test.Eq(target.GetCurrentHp(), 21, "高 HP 律令死亡命令失败时不应造成伤害。");
        _test.False(target.HasStatusEffect("soul_fracture"), "高 HP 律令死亡命令失败时不应附加状态。");

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestLowHpTargetPreviewAllowed()
    {
        SkillDefinition skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("weak_target", "enemy", new Vector2I(1, 0), 100, 20);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);

        using BattleTestFixture fixture = CreateFixture("pwk_low_hp_preview", skill, source, target);
        BattleCommand command = MakeUnitCommand(source, target, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(
            preview.allowed,
            $"HP 等于阈值的律令死亡目标应允许 preview。preview={FormatPreview(preview)}"
        );

        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestBossAndNormalUseSameThresholdGate()
    {
        SkillDefinition skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState bossTarget = MakeUnit("boss_target", "enemy", new Vector2I(1, 0), 100, 21);
        bossTarget.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        bossTarget.attribute_snapshot.SetValue("boss_target", 1);

        using BattleTestFixture fixture = CreateFixture("pwk_boss_gate", skill, source, bossTarget);
        BattleCommand command = MakeUnitCommand(source, bossTarget, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview.allowed, "boss_target 不应绕过律令死亡高 HP 阈值门禁。");
        _test.Eq(bossTarget.GetCurrentHp(), 21, "boss 高 HP preview 不应被非致命削血。");

        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        BattleUnitState source,
        BattleUnitState target
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(4, 2),
            new[] { source },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDefinition MakeExecuteSkill(int apCost, int mpCost, int cooldownTu) =>
        TestSkillDefinitionProjection.BuildSkill(
            "test_power_word_kill",
            "测试律令死亡",
            TestSkillDefinitionProjection.BuildCombatProfile(
                "test_power_word_kill",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "execute",
                        effectTargetTeamFilter: "enemy",
                        saveDcMode: "static",
                        saveDc: 10,
                        saveAbility: "willpower",
                        saveTag: "execute",
                        damageTag: "negative_energy",
                        thresholdMaxHpRatioPercent: 20,
                        thresholdLevelAnchor: 17,
                        thresholdLevelBonusPerDelta: 5,
                        thresholdCapMaxHpRatioPercent: 50,
                        soulFractureDurationTu: 60,
                        healMultiplierPercent: 50,
                        shieldGainMultiplierPercent: 50
                    ),
                },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 5,
                apCost: apCost,
                mpCost: mpCost,
                cooldownTu: cooldownTu
            ),
            maxLevel: 20,
            nonCoreMaxLevel: 20
        );

    private static BattleUnitState MakeUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int maxHp,
        int currentHp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 2,
            currentHp: currentHp
        );
        unit.SetCurrentMp(20);
        unit.SetCurrentStamina(20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.UnlockCombatResource("mp");
        unit.AddKnownActiveSkill("test_power_word_kill");
        unit.SetKnownSkillLevelTyped("test_power_word_kill", 17);
        return unit;
    }

    private static BattleCommand MakeUnitCommand(
        BattleUnitState source,
        BattleUnitState target,
        SkillDefinition skill
    )
    {
        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = source.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skill.SkillId),
            skill_id = skill.SkillId,
            target_unit_id = target.unit_id,
        };
        command.AddTargetUnitId(target.unit_id);
        command.target_coord = target.GetAnchorCoord();
        return command;
    }

    private static string FormatPreview(BattlePreview preview)
    {
        if (preview == null)
            return "<null>";
        return
            $"allowed={preview.allowed}, logs=[{string.Join(" | ", preview.LogLinesTyped)}], target_units=[{string.Join(", ", preview.TargetUnitIdsTyped)}], target_coords=[{string.Join(", ", preview.TargetCoordsTyped)}]";
    }
}
