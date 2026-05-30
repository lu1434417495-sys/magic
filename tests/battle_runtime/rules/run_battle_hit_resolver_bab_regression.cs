using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_hit_resolver_bab_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestAttackCheckReadsAttackerBaseAttackBonus();
        TestAttackCheckFallsBackToZeroWhenAttributeAbsent();
        TestAttackCheckRejectsMissingTargetArmorClass();
        TestAttackCheckAddsBabOnTopOfExistingAttackBonus();
        TestLockedSkillHitBonusReducesRequiredRoll();
        TestLockedSkillHitBonusAppliesToSpellControlRoll();

        if (_failures.Count == 0)
        {
            GD.Print("Battle hit resolver BAB regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle hit resolver BAB regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestAttackCheckReadsAttackerBaseAttackBonus()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 5);
        BattleUnitState target = MakeUnitWithArmorClass(15);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.build_skill_attack_check(attacker, target, null);
        AssertEq(
            attackCheck.AttackerBaseAttackBonus,
            5,
            "attack_check 应暴露 attacker 的 base_attack_bonus。"
        );
        AssertEq(
            attackCheck.AttackerAttackBonus,
            0,
            "attack_check 中的 attacker_attack_bonus 与 BAB 应保持独立字段。"
        );
        AssertEq(attackCheck.RequiredRoll, 10, "BAB +5 应把 required_roll 从 15 拉到 10。");
    }

    private void TestAttackCheckFallsBackToZeroWhenAttributeAbsent()
    {
        var attacker = new BattleUnitState();
        BattleUnitState target = MakeUnitWithArmorClass(12);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.build_skill_attack_check(attacker, target, null);
        AssertEq(attackCheck.AttackerBaseAttackBonus, 0, "缺失 BASE_ATTACK_BONUS 时应回退为 0。");
        AssertEq(attackCheck.RequiredRoll, 12, "缺失 BAB 时 required_roll 应等于裸 AC。");
    }

    private void TestAttackCheckRejectsMissingTargetArmorClass()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        var target = new BattleUnitState();
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.build_skill_attack_check(attacker, target, null);
        AssertTrue(attackCheck.Invalid, "缺失目标 armor_class 时不应回退到隐藏 AC。");
        AssertEq(
            attackCheck.ErrorId.ToString(),
            "missing_target_armor_class",
            "缺失目标 armor_class 应返回明确错误码。"
        );
        AssertEq(attackCheck.SuccessRatePercent, 0, "无效命中检定不应被 AI/HUD 当作 100% 命中。");
    }

    private void TestAttackCheckAddsBabOnTopOfExistingAttackBonus()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(7, 3);
        BattleUnitState target = MakeUnitWithArmorClass(20);
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.build_skill_attack_check(attacker, target, null);
        AssertEq(attackCheck.AttackerBaseAttackBonus, 3, "BAB 字段应取 BASE_ATTACK_BONUS。");
        AssertEq(attackCheck.AttackerAttackBonus, 7, "ATTACK_BONUS 字段应保留原值，不被 BAB 覆盖。");
        AssertEq(
            attackCheck.RequiredRoll,
            10,
            "BAB 与 ATTACK_BONUS 应叠加进 required_roll，而非择一。"
        );
    }

    private void TestLockedSkillHitBonusReducesRequiredRoll()
    {
        BattleUnitState attacker = MakeUnitWithAttackBonuses(0, 0);
        attacker.known_skill_lock_hit_bonus_map[new StringName("locked_slash")] = 2;
        BattleUnitState target = MakeUnitWithArmorClass(15);
        var skillDef = new SkillDef { skill_id = "locked_slash" };
        var resolver = new BattleHitResolver();

        AttackCheckInput attackCheck = resolver.build_skill_attack_check(attacker, target, skillDef);
        AssertEq(attackCheck.LockedSkillHitBonus, 2, "attack_check 应读取锁定技能的命中加值。");
        AssertEq(attackCheck.RequiredRoll, 13, "锁定命中 +2 应把 required_roll 从 15 降到 13。");
    }

    private void TestLockedSkillHitBonusAppliesToSpellControlRoll()
    {
        BattleUnitState source = MakeUnitWithAttackBonuses(0, 0);
        source.known_skill_lock_hit_bonus_map[new StringName("locked_spell")] = 2;
        var resolver = new BattleHitResolver();

        GDictionary metadata = resolver.resolve_spell_control_metadata(
            source,
            new GDictionary
            {
                ["skill_id"] = new StringName("locked_spell"),
                ["attack_roll_override"] = 4,
            }
        );
        AssertEq(DictInt(metadata, "locked_skill_hit_bonus", -1), 2, "法术检定元数据应记录锁定技能加值。");
        AssertEq(DictInt(metadata, "hit_roll", -1), 4, "法术检定应保留原始 d20。");
        AssertEq(DictInt(metadata, "effective_hit_roll", -1), 6, "法术检定应使用 d20 + 锁定加值作为有效检定值。");
    }

    private static BattleUnitState MakeUnitWithAttackBonuses(int attackBonus, int baseAttackBonus)
    {
        var unit = new BattleUnitState();
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), attackBonus);
        unit.attribute_snapshot.set_value(AttributeService.BASE_ATTACK_BONUS_ID(), baseAttackBonus);
        return unit;
    }

    private static BattleUnitState MakeUnitWithArmorClass(int armorClass)
    {
        var unit = new BattleUnitState();
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), armorClass);
        return unit;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }
}
