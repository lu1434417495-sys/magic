using System;
using Godot;

public partial class run_archer_harrier_mark_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_harrier_mark";
    private static readonly StringName HarrierMarkedStatusId = "harrier_marked";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestFormalContentContract();
            TestLevelScalingAndStatusApplication();
            TestHarrierMarkAddsSourceBoundNaturalWeaponDamage();
            RequestTestExit(_test.Finish("Archer Harrier Mark regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Archer Harrier Mark regression"));
        }
    }

    private void TestFormalContentContract()
    {
        SkillDefinition skill = LoadSkill();
        if (skill == null)
        {
            _test.Fail("应能加载真实技能资源 res://data/configs/skills/archer_harrier_mark.tres。");
            return;
        }

        _test.Eq(skill.SkillId, SkillId, "猎印追缉 skill_id 应稳定。");
        _test.Eq(skill.DisplayName, "猎印追缉", "猎印追缉显示名应稳定。");
        _test.True(
            skill.Description.Contains("只有施放者以武器或天生武器命中"),
            "技能描述应明确来源绑定的武器与天生武器触发条件。"
        );
        _test.False(skill.Description.Contains("提高10%"), "技能描述不应再使用百分比承伤。");
        _test.Eq(skill.MaxLevel, 5, "猎印追缉应使用正式五级成长。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "猎印追缉非核心上限应为 3。");
        _test.Eq(skill.MasteryCurve.Count, 5, "猎印追缉熟练度曲线应覆盖五级。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "猎印追缉应属于基础成长档。");
        _test.Eq(
            ReadGrowth(skill, "agility"),
            40,
            "猎印追缉应提供 40 点敏捷成长进度。"
        );
        _test.Eq(
            ReadGrowth(skill, "perception"),
            20,
            "猎印追缉应提供 20 点感知成长进度。"
        );
        _test.True(ContainsStringName(skill.Tags, "mark"), "猎印追缉应带 mark 标签。");
        _test.True(ContainsStringName(skill.Tags, "debuff"), "猎印追缉应带 debuff 标签。");
        _test.False(
            ContainsStringName(skill.Tags, "bow"),
            "不要求弓的猎印追缉不应保留误导性的 bow 标签。"
        );

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "猎印追缉应有 combat_profile。");
        if (combat == null)
        {
            return;
        }

        _test.Eq(combat.TargetMode, new StringName("unit"), "猎印追缉应以单位为目标。");
        _test.Eq(
            combat.TargetTeamFilter,
            new StringName("enemy"),
            "猎印追缉只能选择敌方单位。"
        );
        _test.Eq(
            combat.TargetSelectionMode,
            new StringName("single_unit"),
            "猎印追缉应选择单个单位。"
        );
        _test.True(combat.RequiresLos, "猎印追缉应要求视线。");
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.DirectEffect,
            "纯状态技能应声明 direct_effect，不应伪装成攻击检定。"
        );
        _test.Eq(combat.AttackRollBonus, 0, "猎印追缉不应保留无效的攻击检定加值。");
        _test.Eq(
            combat.MasteryTriggerModeKind,
            CombatSkillMasteryTriggerMode.SourceBoundWeaponBonusDamage,
            "猎印追缉熟练度应在施放者触发来源绑定追加骰时结算。"
        );
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "猎印追缉不应绑定弓或其他武器。");
        _test.Eq(combat.EffectDefinitions.Count, 5, "猎印追缉应按五级配置五个状态效果。");
    }

    private void TestLevelScalingAndStatusApplication()
    {
        SkillDefinition skill = LoadSkill();
        if (skill?.CombatProfile == null)
        {
            _test.Fail("猎印追缉技能与 combat_profile 应可加载。");
            return;
        }

        AssertLevel(skill, 1, range: 4, stamina: 18, cooldownTu: 90, durationTu: 60, dieSides: 4);
        AssertLevel(skill, 2, range: 4, stamina: 17, cooldownTu: 80, durationTu: 70, dieSides: 4);
        AssertLevel(skill, 3, range: 5, stamina: 16, cooldownTu: 70, durationTu: 80, dieSides: 6);
        AssertLevel(skill, 4, range: 5, stamina: 15, cooldownTu: 60, durationTu: 90, dieSides: 6);
        AssertLevel(skill, 5, range: 6, stamina: 14, cooldownTu: 60, durationTu: 100, dieSides: 8);
    }

    private void TestHarrierMarkAddsSourceBoundNaturalWeaponDamage()
    {
        SkillDefinition skill = LoadSkill();
        if (skill == null)
        {
            _test.Fail("猎印追缉技能应可加载。");
            return;
        }

        BattleUnitState caster = MakeUnit("harrier_mark_caster", "enemy", 100);
        BattleUnitState ally = MakeUnit("harrier_mark_ally", "enemy", 100);
        BattleUnitState markedTarget = MakeUnit("harrier_mark_target", "player", 100);
        BattleUnitState plainTarget = MakeUnit("harrier_mark_plain_target", "player", 100);
        ApplyNaturalWeapon(caster);
        ApplyNaturalWeapon(ally);
        caster.SetKnownSkillLevelTyped(SkillId, 1);

        var resolver = new FixedHitMaxDamageResolver();
        AttackEffectResolutionResult markResult =
            resolver.ResolveSkillResult(caster, markedTarget, skill);
        _test.True(markResult.Applied, "猎印追缉应能通过正式效果路径施加 harrier_marked。");
        _test.True(
            markedTarget.HasStatusEffect(HarrierMarkedStatusId),
            "目标应获得独立的 harrier_marked。"
        );

        CombatEffectDefinition naturalWeaponDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_pierce",
            addWeaponDice: true,
            useWeaponPhysicalDamageTag: true,
            resolveAsWeaponAttack: true
        );
        AttackEffectResolutionResult plainCasterHit =
            resolver.ResolveEffects(caster, plainTarget, new[] { naturalWeaponDamage });
        AttackEffectResolutionResult allyHit =
            resolver.ResolveEffects(ally, markedTarget, new[] { naturalWeaponDamage });
        markedTarget.SetCurrentHp(100);
        AttackEffectResolutionResult casterHit =
            resolver.ResolveEffects(caster, markedTarget, new[] { naturalWeaponDamage });

        _test.Eq(plainCasterHit.Damage, 8, "未标记目标只应承受 1D6+2 天生武器伤害。");
        _test.Eq(allyHit.Damage, 8, "非标记来源攻击同一目标时不得获得追加骰。");
        _test.Eq(casterHit.Damage, 12, "施放者命中标记目标时应追加 1D4，合计 12 点。");

        markedTarget.SetCurrentHp(100);
        CombatEffectDefinition nonWeaponDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 10,
            damageTag: "fire"
        );
        AttackEffectResolutionResult nonWeaponResult =
            resolver.ResolveEffects(caster, markedTarget, new[] { nonWeaponDamage });
        _test.Eq(nonWeaponResult.Damage, 10, "施放者的非武器伤害也不得触发猎印追加骰。");
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int range,
        int stamina,
        int cooldownTu,
        int durationTu,
        int dieSides
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(combat.GetEffectiveRangeValue(level), range, $"猎印追缉 {level} 级射程应正确。");
        _test.Eq(costs.ApCost, 1, $"猎印追缉 {level} 级应消耗 1 AP。");
        _test.Eq(costs.StaminaCost, stamina, $"猎印追缉 {level} 级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"猎印追缉 {level} 级冷却应正确。");

        BattleUnitState caster = MakeUnit($"harrier_mark_level_{level}_caster", "enemy", 100);
        BattleUnitState target = MakeUnit($"harrier_mark_level_{level}_target", "player", 100);
        caster.SetKnownSkillLevelTyped(SkillId, level);
        new FixedHitMaxDamageResolver().ResolveSkillResult(caster, target, skill);
        BattleStatusEffectState mark = target.GetStatusEffect(HarrierMarkedStatusId);
        _test.Eq(
            mark?.duration ?? -1,
            durationTu,
            $"猎印追缉 {level} 级应施加 {durationTu}TU 的 harrier_marked。"
        );
        _test.Eq(
            mark?.source_unit_id ?? new StringName(""),
            caster.unit_id,
            $"猎印追缉 {level} 级应记录状态来源。"
        );
        _test.Eq(
            mark?.source_bound_weapon_bonus_damage_dice_count ?? -1,
            1,
            $"猎印追缉 {level} 级应追加一个来源绑定伤害骰。"
        );
        _test.Eq(
            mark?.source_bound_weapon_bonus_damage_dice_sides ?? -1,
            dieSides,
            $"猎印追缉 {level} 级追加骰应为 D{dieSides}。"
        );
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/archer_harrier_mark.tres",
            "archer_harrier_mark_regression"
        );

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId, int hp)
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
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        return unit;
    }

    private static void ApplyNaturalWeapon(BattleUnitState unit)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "natural",
                weapon_profile_type_id = "natural_weapon",
                weapon_range_type = "ranged",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 5,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 2,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;

    private static bool ContainsStringName(
        System.Collections.Generic.IEnumerable<StringName> values,
        StringName expected
    )
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
