using System.Collections.Generic;
using Godot;

public partial class run_battle_execute_target_gate_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHighHpTargetPreviewDeniedWithoutSaveDamageOrStatus();
        TestHighHpTargetAffordanceDenied();
        TestHighHpCommandDoesNotConsumeApMpCooldown();
        TestLowHpTargetPreviewAllowed();
        TestBossAndNormalUseSameThresholdGate();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle execute target gate regression"));
    }

    private void TestHighHpTargetPreviewDeniedWithoutSaveDamageOrStatus()
    {
        using SkillDef skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_preview", skill, source, target);
        BattleCommand command = MakeUnitCommand(source, target, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview.allowed, "高 HP 律令死亡目标应在 preview 阶段被拒绝。");
        _test.Eq(target.current_hp, 21, "高 HP preview 不应造成伤害。");
        _test.False(target.HasStatusEffect("soul_fracture"), "高 HP preview 不应附加 soul fracture。");

        BattleTestFixture.DisposeBattlePreview(preview);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private void TestHighHpTargetAffordanceDenied()
    {
        using SkillDef skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_affordance", skill, source, target);
        BattleUnitSkillTargetAffordance affordance =
            fixture.Runtime.GetUnitSkillTargetAffordance(source, target, skill);

        _test.False(affordance.Allowed, "高 HP 律令死亡目标 affordance 应标记为不可选。");
        _test.True(
            affordance.Reason.Contains("生命高于律令死亡阈值"),
            "高 HP 律令死亡 affordance 应返回玩家可读的阈值原因。"
        );
    }

    private void TestHighHpCommandDoesNotConsumeApMpCooldown()
    {
        using SkillDef skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        source.current_ap = 2;
        source.current_mp = 20;
        source.SetCooldownTyped(skill.skill_id, 0);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_issue", skill, source, target);
        BattleCommand command = MakeUnitCommand(source, target, skill);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(source.current_ap, 2, "高 HP 律令死亡命令失败时不应消耗 AP。");
        _test.Eq(source.current_mp, 20, "高 HP 律令死亡命令失败时不应消耗 MP。");
        _test.Eq(source.GetCooldownTyped(skill.skill_id), 0, "高 HP 律令死亡命令失败时不应进入冷却。");
        _test.Eq(target.current_hp, 21, "高 HP 律令死亡命令失败时不应造成伤害。");
        _test.False(target.HasStatusEffect("soul_fracture"), "高 HP 律令死亡命令失败时不应附加状态。");

        GodotSharpCleanup.DisposeGodotObject(batch);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private void TestLowHpTargetPreviewAllowed()
    {
        using SkillDef skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("weak_target", "enemy", new Vector2I(1, 0), 100, 20);
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);

        using BattleTestFixture fixture = CreateFixture("pwk_low_hp_preview", skill, source, target);
        BattleCommand command = MakeUnitCommand(source, target, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(preview.allowed, "HP 等于阈值的律令死亡目标应允许 preview。");

        BattleTestFixture.DisposeBattlePreview(preview);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private void TestBossAndNormalUseSameThresholdGate()
    {
        using SkillDef skill = MakeExecuteSkill(apCost: 1, mpCost: 5, cooldownTu: 30);
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState bossTarget = MakeUnit("boss_target", "enemy", new Vector2I(1, 0), 100, 21);
        bossTarget.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        bossTarget.attribute_snapshot.SetValue("boss_target", 1);

        using BattleTestFixture fixture = CreateFixture("pwk_boss_gate", skill, source, bossTarget);
        BattleCommand command = MakeUnitCommand(source, bossTarget, skill);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview.allowed, "boss_target 不应绕过律令死亡高 HP 阈值门禁。");
        _test.Eq(bossTarget.current_hp, 21, "boss 高 HP preview 不应被非致命削血。");

        BattleTestFixture.DisposeBattlePreview(preview);
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDef skill,
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
            new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDef MakeExecuteSkill(int apCost, int mpCost, int cooldownTu)
    {
        var skill = new SkillDef
        {
            skill_id = "test_power_word_kill",
            display_name = "测试律令死亡",
            max_level = 20,
            non_core_max_level = 20,
            combat_profile = new CombatSkillDef
            {
                target_mode = "unit",
                target_team_filter = "enemy",
                target_selection_mode = "single_unit",
                range_value = 5,
                ap_cost = apCost,
                mp_cost = mpCost,
                cooldown_tu = cooldownTu,
            },
        };
        skill.combat_profile.effect_defs.Add(new CombatEffectDef
        {
            effect_type = "execute",
            effect_target_team_filter = "enemy",
            save_dc_mode = "static",
            save_dc = 10,
            save_ability = "willpower",
            save_tag = "execute",
            damage_tag = "negative_energy",
            threshold_max_hp_ratio_percent = 20,
            threshold_level_anchor = 17,
            threshold_level_bonus_per_delta = 5,
            threshold_cap_max_hp_ratio_percent = 50,
            soul_fracture_duration_tu = 60,
            heal_multiplier_percent = 50,
            shield_gain_multiplier_percent = 50,
        });
        return skill;
    }

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
        unit.current_mp = 20;
        unit.current_stamina = 20;
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.UnlockCombatResource("mp");
        unit.known_active_skill_ids.Add("test_power_word_kill");
        unit.known_skill_level_map[new StringName("test_power_word_kill")] = 17;
        return unit;
    }

    private static BattleCommand MakeUnitCommand(
        BattleUnitState source,
        BattleUnitState target,
        SkillDef skill
    )
    {
        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = source.unit_id,
            skill_id = skill.skill_id,
            target_unit_id = target.unit_id,
        };
        command.AddTargetUnitId(target.unit_id);
        command.target_coord = target.coord;
        return command;
    }
}
