using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_hit_resolver_bab_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestAttackCheckReadsAttackerBaseAttackBonus();
        TestAttackCheckFallsBackToZeroWhenAttributeAbsent();
        TestAttackCheckRejectsMissingTargetArmorClass();
        TestAttackCheckAddsBabOnTopOfExistingAttackBonus();
        TestLockedSkillHitBonusReducesRequiredRoll();
        TestLockedSkillHitBonusAppliesToSpellControlRoll();
        TestAttackerStatusAttackBonusesStackAdditively();
        TestBlackStarBrandPenaltySurvivesPositiveBonuses();
        TestAttackRollPenaltiesStackAdditively();

        RequestTestExit(_test.Finish("Battle hit resolver BAB regression"));
    }

    private void TestAttackCheckReadsAttackerBaseAttackBonus()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 5);
        BattleUnitState target = MakeUnitWithArmorClass(15);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(
            attackCheck.AttackerBaseAttackBonus,
            5,
            "attack_check 应暴露 attacker 的 base_attack_bonus。"
        );
        _test.Eq(
            attackCheck.AttackerAttackBonus,
            0,
            "attack_check 中的 attacker_attack_bonus 与 BAB 应保持独立字段。"
        );
        _test.Eq(attackCheck.RequiredRoll, 10, "BAB +5 应把 required_roll 从 15 拉到 10。");
    }

    private void TestAttackCheckFallsBackToZeroWhenAttributeAbsent()
    {
        var attacker = new BattleUnitState();
        BattleUnitState target = MakeUnitWithArmorClass(12);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(attackCheck.AttackerBaseAttackBonus, 0, "缺失 BASE_ATTACK_BONUS 时应回退为 0。");
        _test.Eq(attackCheck.RequiredRoll, 12, "缺失 BAB 时 required_roll 应等于裸 AC。");
    }

    private void TestAttackCheckRejectsMissingTargetArmorClass()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        var target = new BattleUnitState();
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.True(attackCheck.Invalid, "缺失目标 armor_class 时不应回退到隐藏 AC。");
        _test.Eq(
            attackCheck.ErrorId.ToString(),
            "missing_target_armor_class",
            "缺失目标 armor_class 应返回明确错误码。"
        );
        _test.Eq(attackCheck.SuccessRatePercent, 0, "无效命中检定不应被 AI/HUD 当作 100% 命中。");
    }

    private void TestAttackCheckAddsBabOnTopOfExistingAttackBonus()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(7, 3);
        BattleUnitState target = MakeUnitWithArmorClass(20);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(attackCheck.AttackerBaseAttackBonus, 3, "BAB 字段应取 BASE_ATTACK_BONUS。");
        _test.Eq(attackCheck.AttackerAttackBonus, 7, "ATTACK_BONUS 字段应保留原值，不被 BAB 覆盖。");
        _test.Eq(
            attackCheck.RequiredRoll,
            10,
            "BAB 与 ATTACK_BONUS 应叠加进 required_roll，而非择一。"
        );
    }

    private void TestLockedSkillHitBonusReducesRequiredRoll()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        attacker.SetKnownSkillLockHitBonusTyped("locked_slash", 2);
        BattleUnitState target = MakeUnitWithArmorClass(15);
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill("locked_slash");
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(
            attacker,
            target,
            skillDefinition
        );
        _test.Eq(attackCheck.LockedSkillHitBonus, 2, "attack_check 应读取锁定技能的命中加值。");
        _test.Eq(attackCheck.RequiredRoll, 13, "锁定命中 +2 应把 required_roll 从 15 降到 13。");
    }

    private void TestLockedSkillHitBonusAppliesToSpellControlRoll()
    {
        BattleUnitState source = MakeUnitWithAttackBonuses(0, 0);
        source.SetKnownSkillLockHitBonusTyped("locked_spell", 2);
        var resolver = new BattleHitResolver();

        BattleSpellControlMetadata metadata = resolver.ResolveSpellControlMetadataTyped(
            source,
            new AttackContext
            {
                SkillId = new StringName("locked_spell"),
                AttackRollOverride = 4,
            }
        );
        _test.Eq(metadata.LockedSkillHitBonus, 2, "法术检定元数据应记录锁定技能加值。");
        _test.Eq(metadata.HitRoll, 4, "法术检定应保留原始 d20。");
        _test.Eq(metadata.EffectiveHitRoll, 6, "法术检定应使用 d20 + 锁定加值作为有效检定值。");
    }

    private void TestAttackerStatusAttackBonusesStackAdditively()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        attacker.SetStatusEffect(
            new BattleStatusEffectState { status_id = "test_focus_buff", attack_roll_bonus = 2 }
        );
        attacker.SetStatusEffect(
            new BattleStatusEffectState { status_id = "test_war_chant", attack_roll_bonus = 1 }
        );
        BattleUnitState target = MakeUnitWithArmorClass(15);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(
            attackCheck.SituationalAttackBonus,
            3,
            "多个正面命中加值状态应累加(+2 +1 = +3),不取大。"
        );
        _test.Eq(attackCheck.RequiredRoll, 12, "累加 +3 应把 required_roll 从 15 降到 12。");
    }

    private void TestBlackStarBrandPenaltySurvivesPositiveBonuses()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        attacker.SetStatusEffect(
            new BattleStatusEffectState { status_id = "black_star_brand_elite" }
        );
        attacker.SetStatusEffect(
            new BattleStatusEffectState { status_id = "test_war_chant", attack_roll_bonus = 2 }
        );
        BattleUnitState target = MakeUnitWithArmorClass(15);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(
            attackCheck.SituationalAttackPenalty,
            1,
            "黑星烙印 -3 与 +2 buff 并存应按累加得净值 -1,不允许被正面加成整体覆盖。"
        );
        _test.Eq(attackCheck.SituationalAttackBonus, 0, "净值为负时不应再报正向情境加值。");
        _test.Eq(attackCheck.RequiredRoll, 16, "净 -1 应把 required_roll 从 15 抬到 16。");
    }

    private void TestAttackRollPenaltiesStackAdditively()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        attacker.SetStatusEffect(
            new BattleStatusEffectState { status_id = "test_dazzled", attack_roll_penalty = 2 }
        );
        attacker.SetStatusEffect(
            new BattleStatusEffectState { status_id = "test_wrist_sprain", attack_roll_penalty = 1 }
        );
        BattleUnitState target = MakeUnitWithArmorClass(15);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(
            attackCheck.SituationalAttackPenalty,
            3,
            "多个攻击惩罚状态默认累加(2+1=3);取大仅限语义表显式标记的特殊来源。"
        );
        _test.Eq(attackCheck.RequiredRoll, 18, "累加惩罚 -3 应把 required_roll 从 15 抬到 18。");
    }

    private static BattleUnitState MakeUnitWithAttackBonuses(int attackBonus, int baseAttackBonus)
    {
        var unit = new BattleUnitState();
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), attackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus), baseAttackBonus);
        return unit;
    }

    private static BattleUnitState MakeUnitWithArmorClass(int armorClass)
    {
        var unit = new BattleUnitState();
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), armorClass);
        return unit;
    }

}
