using System;
using System.Collections.Generic;
using Godot;

public partial class run_archer_suppressive_fire_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_suppressive_fire";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                "res://data/configs/skills/archer_suppressive_fire.tres",
                "archer_suppressive_fire_regression"
            );
            TestAuthoredContract(skill);
            TestLevelScaling(skill);
            TestNaturalWeaponContract(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer suppressive fire regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill != null, "压制射击正式资源应可加载。");
        if (skill == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "压制射击 skill_id 应稳定。");
        _test.Eq(skill.MaxLevel, 5, "压制射击核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "压制射击非核心上限应为3级。");
        _test.Eq(skill.MasteryCurve.Count, 5, "压制射击熟练度曲线应覆盖五级。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "压制射击应属于基础成长档。");
        _test.Eq(ReadGrowth(skill, "perception"), 40, "压制射击应提供40点感知成长进度。");
        _test.Eq(ReadGrowth(skill, "agility"), 20, "压制射击应提供20点敏捷成长进度。");
        _test.Eq(
            EnemySkillLevelGenerationService.ResolveCoreSkillLevel(skill),
            3,
            "敌方技能生成器应从压制射击3级核心档开始随机。"
        );

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "压制射击应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("ground"), "压制射击应以地面为目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "压制射击只应伤害敌方单位。");
        _test.Eq(combat.AreaPattern, new StringName("line"), "压制射击应覆盖直线路径。");
        _test.True(combat.RequiresLos, "压制射击应要求视线。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "压制射击应绑定弓类武器。");
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "压制射击应绑定 bow 家族。");
        _test.True(combat.AllowsNaturalWeapon, "雾沼猎压者应能用天生远程武器施放压制射击。");
        _test.Eq(
            combat.MasteryTriggerMode,
            new StringName("weapon_attack_quality"),
            "压制射击应按武器攻击质量结算熟练度。"
        );

        CombatEffectDefinition damage = FindEffect(combat.EffectDefinitions, "damage");
        _test.True(damage != null, "压制射击应包含武器伤害效果。");
        if (damage != null)
        {
            _test.True(damage.AddWeaponDice, "压制射击伤害应使用当前武器骰。");
            _test.True(damage.RequiresWeapon, "压制射击伤害应要求合法武器或天生武器。");
            _test.True(
                damage.UseWeaponPhysicalDamageTag,
                "压制射击应沿用武器的物理伤害类型。"
            );
            _test.True(damage.ResolveAsWeaponAttack, "压制射击应按武器攻击结算。");
        }
    }

    private void TestLevelScaling(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            _test.Fail("压制射击技能与 combat_profile 应可加载。");
            return;
        }

        AssertLevel(skill, 1, stamina: 32, cooldownTu: 80, attackBonus: -1, durationTu: 30);
        AssertLevel(skill, 2, stamina: 30, cooldownTu: 80, attackBonus: -1, durationTu: 35);
        AssertLevel(skill, 3, stamina: 28, cooldownTu: 70, attackBonus: 0, durationTu: 40);
        AssertLevel(skill, 4, stamina: 26, cooldownTu: 60, attackBonus: 0, durationTu: 50);
        AssertLevel(skill, 5, stamina: 24, cooldownTu: 50, attackBonus: 1, durationTu: 60);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldownTu,
        int attackBonus,
        int durationTu
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 2, $"压制射击{level}级应消耗2 AP。");
        _test.Eq(costs.StaminaCost, stamina, $"压制射击{level}级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"压制射击{level}级冷却应正确。");
        _test.Eq(
            combat.GetEffectiveAttackRollBonus(level),
            attackBonus,
            $"压制射击{level}级攻击检定应正确。"
        );

        BattleUnitState caster = BuildNaturalWeaponCaster(level);
        using var rules = new BattleSkillResolutionRules();
        IReadOnlyList<CombatEffectDefinition> terrainEffects =
            rules.CollectGroundTerrainEffectDefinitions(skill, null, caster);
        _test.Eq(terrainEffects.Count, 1, $"压制射击{level}级应只生成一个等级对应的压制地带。");
        if (terrainEffects.Count == 1)
        {
            CombatEffectDefinition terrain = terrainEffects[0];
            _test.Eq(
                terrain.TerrainEffectId,
                new StringName("suppressive_fire_zone"),
                $"压制射击{level}级应生成正式压制地带。"
            );
            _test.Eq(
                terrain.TickEffectType,
                new StringName("movement_cost"),
                $"压制射击{level}级应修改移动成本。"
            );
            _test.Eq(terrain.MoveCostDelta, 1, $"压制射击{level}级应使敌方移动成本+1。");
            _test.Eq(terrain.DurationTu, durationTu, $"压制射击{level}级持续时间应正确。");
            _test.Eq(
                terrain.DoesNotStackWithStatusId,
                new StringName("slow"),
                $"压制射击{level}级不应与 slow 重复叠加。"
            );
        }
        BattleTestFixture.DisposeBattleUnit(caster);
    }

    private void TestNaturalWeaponContract(SkillDefinition skill)
    {
        BattleUnitState caster = BuildNaturalWeaponCaster(3);
        try
        {
            _test.True(
                BattleRangeService.UnitMatchesRequiredWeaponFamilies(caster, skill),
                "允许的天生远程武器应通过压制射击的弓类门禁。"
            );
            _test.Eq(
                BattleRangeService.GetEffectiveSkillRange(caster, skill),
                5,
                "压制射击应采用天生远程武器的射程。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(caster);
        }
    }

    private static BattleUnitState BuildNaturalWeaponCaster(int skillLevel)
    {
        var caster = new BattleUnitState
        {
            unit_id = $"suppressive_fire_level_{skillLevel}",
            display_name = "压制射击测试单位",
            faction_id = "enemy",
        }.WithCombatResourcesForTest(
            hp: 30,
            ap: 2,
            stamina: 100,
            isAlive: true
        );
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, skillLevel);
        caster.ApplyWeaponProjectionTyped(
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
        return caster;
    }

    private static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effects,
        StringName effectType
    )
    {
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectType == effectType)
                return effect;
        }
        return null;
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;
}
