using System;
using Godot;

public partial class run_hunter_mark_skill_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_hunter_mark";
    private static readonly StringName HunterMarkedStatusId = "hunter_marked";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestHunterMarkLevelScaling();
            TestHunterMarkResourceAndStatusApplication();
            TestHunterMarkAddsSourceBoundWeaponDamage();
            RequestTestExit(_test.Finish("Hunter Mark skill regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Hunter Mark skill regression"));
        }
    }

    private void TestHunterMarkLevelScaling()
    {
        SkillDefinition skill = LoadHunterMarkSkill();
        if (skill == null)
        {
            _test.Fail("应能加载真实技能资源 res://data/configs/skills/archer_hunter_mark.tres。");
            return;
        }

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "猎人标记应有 combat_profile。");
        if (combat == null)
        {
            return;
        }

        AssertLevelProfile(combat, 1, range: 4, stamina: 20, cooldownTu: 90);
        AssertLevelProfile(combat, 2, range: 4, stamina: 19, cooldownTu: 80);
        AssertLevelProfile(combat, 3, range: 5, stamina: 18, cooldownTu: 70);
        AssertLevelProfile(combat, 4, range: 5, stamina: 17, cooldownTu: 60);
        AssertLevelProfile(combat, 5, range: 6, stamina: 16, cooldownTu: 60);

        AssertAppliedStatus(skill, 1, expectedDurationTu: 60, expectedDiceCount: 1, expectedDiceSides: 6);
        AssertAppliedStatus(skill, 2, expectedDurationTu: 70, expectedDiceCount: 1, expectedDiceSides: 6);
        AssertAppliedStatus(skill, 3, expectedDurationTu: 80, expectedDiceCount: 1, expectedDiceSides: 6);
        AssertAppliedStatus(skill, 4, expectedDurationTu: 100, expectedDiceCount: 1, expectedDiceSides: 6);
        AssertAppliedStatus(skill, 5, expectedDurationTu: 120, expectedDiceCount: 2, expectedDiceSides: 4);
    }

    private void TestHunterMarkResourceAndStatusApplication()
    {
        SkillDefinition skill = LoadHunterMarkSkill();
        if (skill == null)
        {
            _test.Fail("应能加载真实技能资源 res://data/configs/skills/archer_hunter_mark.tres。");
            return;
        }

        _test.Eq(skill.SkillId, SkillId, "猎人标记 skill_id 应为共享 archer_hunter_mark。");
        _test.Eq(skill.DisplayName, "猎人标记", "猎人标记显示名应稳定。");
        _test.True(ContainsStringName(skill.Tags, "mark"), "猎人标记应带 mark 标签。");
        _test.True(ContainsStringName(skill.Tags, "ranger"), "猎人标记应带 ranger 标签。");
        _test.Eq(skill.MaxLevel, 5, "猎人标记应是 3->5 的普通成长技能。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "猎人标记非核心上限应为 3。");

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "猎人标记应有 combat_profile。");
        if (combat == null)
        {
            return;
        }
        _test.Eq(combat.RangeValue, 4, "猎人标记 1 级基础射程应为 4。");
        _test.Eq(combat.ApCost, 1, "猎人标记应消耗 1 AP。");
        _test.Eq(combat.StaminaCost, 20, "猎人标记 1 级应消耗 20 体力。");
        _test.Eq(combat.CooldownTu, 90, "猎人标记 1 级冷却应为 90TU。");
        _test.Eq(
            combat.MasteryTriggerModeKind,
            CombatSkillMasteryTriggerMode.SourceBoundWeaponBonusDamage,
            "猎人标记熟练度应来自标记后的来源绑定武器追加伤害，而不是施加状态。"
        );
        _test.Eq(combat.EffectDefinitions.Count, 5, "猎人标记应按等级门槛配置 5 个状态效果。");

        BattleUnitState hunter = MakeUnit("hunter_mark_caster", "player");
        hunter.SetKnownSkillLevelTyped(SkillId, 3);
        BattleUnitState quarry = MakeUnit("hunter_mark_quarry", "enemy");
        AttackEffectResolutionResult markResult =
            new FixedHitMaxDamageResolver().ResolveSkillResult(hunter, quarry, skill);

        _test.True(markResult.Applied, "猎人标记应能施加状态。");
        _test.True(quarry.HasStatusEffect(HunterMarkedStatusId), "目标应获得 hunter_marked。");
        BattleStatusEffectState mark = quarry.GetStatusEffect(HunterMarkedStatusId);
        _test.Eq(mark?.source_unit_id ?? new StringName(""), hunter.unit_id, "hunter_marked 应记录施放者。");
        _test.Eq(mark?.duration ?? -1, 80, "hunter_marked 3 级持续时间应为 80TU。");
    }

    private void TestHunterMarkAddsSourceBoundWeaponDamage()
    {
        SkillDefinition skill = LoadHunterMarkSkill();
        if (skill == null)
        {
            _test.Fail("应能加载真实技能资源 res://data/configs/skills/archer_hunter_mark.tres。");
            return;
        }

        BattleUnitState hunter = MakeUnit("hunter_mark_damage_caster", "player");
        BattleUnitState ally = MakeUnit("hunter_mark_damage_ally", "player");
        BattleUnitState quarry = MakeUnit("hunter_mark_damage_quarry", "enemy", hp: 100);
        var resolver = new FixedHitMaxDamageResolver();
        CombatEffectDefinition weaponDamage = MakeWeaponDamageEffect();
        AttackCheckInput attackCheck = new(requiredRoll: 10, displayRequiredRoll: 10);

        hunter.SetKnownSkillLevelTyped(SkillId, 3);
        resolver.ResolveSkillResult(hunter, quarry, skill);

        int beforeHunterHitHp = quarry.GetCurrentHp();
        AttackEffectResolutionResult hunterHit = resolver.ResolveAttackEffects(
            hunter,
            quarry,
            new[] { weaponDamage },
            attackCheck
        );
        _test.Eq(hunterHit.Damage, 16, "施放者命中自己的猎人标记目标时应为 1D8+2+1D6 最大伤害。");
        _test.Eq(beforeHunterHitHp - quarry.GetCurrentHp(), 16, "hunter_marked 额外伤害应实际扣血。");

        quarry.SetCurrentHp(100);
        int beforeAllyHitHp = quarry.GetCurrentHp();
        AttackEffectResolutionResult allyHit = resolver.ResolveAttackEffects(
            ally,
            quarry,
            new[] { weaponDamage },
            attackCheck
        );
        _test.Eq(allyHit.Damage, 10, "非施放者攻击同一个 hunter_marked 目标不应获得 1D6。");
        _test.Eq(beforeAllyHitHp - quarry.GetCurrentHp(), 10, "非施放者不应触发来源绑定额外伤害。");

        quarry.SetCurrentHp(100);
        hunter.SetKnownSkillLevelTyped(SkillId, 5);
        resolver.ResolveSkillResult(hunter, quarry, skill);
        int beforeLevelFiveHunterHitHp = quarry.GetCurrentHp();
        AttackEffectResolutionResult levelFiveHunterHit = resolver.ResolveAttackEffects(
            hunter,
            quarry,
            new[] { weaponDamage },
            attackCheck
        );
        _test.Eq(levelFiveHunterHit.Damage, 18, "5 级猎人标记应升级为 1D8+2+2D4 最大伤害。");
        _test.Eq(beforeLevelFiveHunterHitHp - quarry.GetCurrentHp(), 18, "5 级 2D4 额外伤害应实际扣血。");
    }

    private static SkillDefinition LoadHunterMarkSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/archer_hunter_mark.tres",
            "hunter_mark_skill_regression"
        );

    private void AssertLevelProfile(
        CombatSkillDefinition combat,
        int level,
        int range,
        int stamina,
        int cooldownTu
    )
    {
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(combat.GetEffectiveRangeValue(level), range, $"猎人标记 {level} 级射程应正确。");
        _test.Eq(costs.ApCost, 1, $"猎人标记 {level} 级 AP 消耗应保持 1。");
        _test.Eq(costs.StaminaCost, stamina, $"猎人标记 {level} 级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"猎人标记 {level} 级冷却应正确。");
    }

    private void AssertAppliedStatus(
        SkillDefinition skill,
        int level,
        int expectedDurationTu,
        int expectedDiceCount,
        int expectedDiceSides
    )
    {
        BattleUnitState hunter = MakeUnit($"hunter_mark_level_{level}_caster", "player");
        hunter.SetKnownSkillLevelTyped(SkillId, level);
        BattleUnitState quarry = MakeUnit($"hunter_mark_level_{level}_quarry", "enemy");

        new FixedHitMaxDamageResolver().ResolveSkillResult(hunter, quarry, skill);

        BattleStatusEffectState mark = quarry.GetStatusEffect(HunterMarkedStatusId);
        _test.Eq(
            mark?.duration ?? -1,
            expectedDurationTu,
            $"猎人标记 {level} 级应施加 {expectedDurationTu}TU 持续时间。"
        );
        _test.Eq(
            mark?.source_bound_weapon_bonus_damage_dice_count ?? -1,
            expectedDiceCount,
            $"猎人标记 {level} 级来源绑定武器额外伤害骰数量应正确。"
        );
        _test.Eq(
            mark?.source_bound_weapon_bonus_damage_dice_sides ?? -1,
            expectedDiceSides,
            $"猎人标记 {level} 级来源绑定武器额外伤害骰面数应正确。"
        );
    }

    private static CombatEffectDefinition MakeWeaponDamageEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            addWeaponDice: true
        );

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId, int hp = 60)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            hp: hp,
            isAlive: true
        );
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "hunter_mark_test_weapon",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 2,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_is_versatile = false,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 80);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 8);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 8);
        unit.SetCurrentStamina(80);
        return unit;
    }

    private static bool ContainsStringName(System.Collections.Generic.IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
            {
                return true;
            }
        }
        return false;
    }
}
