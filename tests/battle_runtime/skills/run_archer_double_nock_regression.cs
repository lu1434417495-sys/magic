using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_double_nock_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_double_nock";
    private const string SkillPath = "archer_double_nock";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestTypedRepeatSchema();
            TestContentContract(skill);
            TestCanonicalPreview(skill);
            TestFirstMissStopsSecondArrowAndChargesCommand(skill);
            TestWeaponGateRejectsNonBow(skill);
            TestFormalDamageUsesOnePlusTwoWeaponAttacks(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer double nock regression"));
    }

    private void TestTypedRepeatSchema()
    {
        using SkillContentRegistry registry = new(loadDefaultContent: false);
        using CombatEffectDef invalidMultiplier = new()
        {
            effect_type = "fixed_repeat_attack",
            fixed_attack_count = 2,
            follow_up_damage_multiplier_percent = 0,
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(
            errors,
            "invalid_repeat_multiplier",
            invalidMultiplier,
            "test_effect"
        );
        _test.True(
            ContainsError(errors, "follow_up_damage_multiplier_percent must be > 0"),
            "连击后续段伤害倍率必须拒绝0或负数。"
        );

        using CombatEffectDef misplacedCurve = new()
        {
            effect_type = "damage",
            dice_count = 1,
            dice_sides = 4,
            follow_up_attack_roll_bonus_curve = new[] { 0, 1 },
        };
        errors.Clear();
        registry.AppendEffectValidationErrors(
            errors,
            "misplaced_repeat_curve",
            misplacedCurve,
            "test_effect"
        );
        _test.True(
            ContainsError(errors, "follow_up_attack_roll_bonus_curve is only supported"),
            "非连击效果不得误用后续段命中曲线。"
        );

        using CombatEffectDef weakAlias = new()
        {
            effect_type = "fixed_repeat_attack",
            fixed_attack_count = 2,
            @params = new GDictionary
            {
                ["follow_up_damage_multiplier_percent"] = 200,
            },
        };
        errors.Clear();
        registry.AppendEffectValidationErrors(
            errors,
            "weak_repeat_multiplier",
            weakAlias,
            "test_effect"
        );
        _test.True(
            ContainsError(errors, "use CombatEffectDef.follow_up_damage_multiplier_percent"),
            "旧params倍率必须被内容校验拒绝并指向typed字段。"
        );

        int[] authoredCurve = { 0, 1, 2 };
        using CombatEffectDef detachedSource = new()
        {
            effect_type = "fixed_repeat_attack",
            fixed_attack_count = 2,
            follow_up_damage_multiplier_percent = 200,
            follow_up_attack_roll_bonus_curve = authoredCurve,
        };
        CombatEffectDefinition detachedDefinition = CombatEffectDefinition.FromDiagnosticFixture(
            detachedSource,
            "archer_double_nock.detached_repeat"
        );
        authoredCurve[2] = 99;
        detachedSource.follow_up_damage_multiplier_percent = 50;
        _test.Eq(
            detachedDefinition.GetFollowUpAttackRollBonus(2),
            2,
            "immutable definition必须复制后续段命中曲线，不能借用Resource数组。"
        );
        _test.Eq(
            detachedDefinition.FollowUpDamageMultiplierPercent,
            200,
            "immutable definition必须冻结后续段伤害倍率。"
        );
    }

    private void TestContentContract(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "双弦连射正式资源应可投影为 typed definition。");
        if (skill?.CombatProfile == null)
        {
            return;
        }

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(skill.MaxLevel, 5, "双弦连射最高等级应为5。" );
        _test.Eq(skill.NonCoreMaxLevel, 3, "双弦连射非核心上限应为3。" );
        _test.Eq(skill.GrowthTier, new StringName("basic"), "双弦连射应属于基础成长档。" );
        _test.Eq(skill.AttributeGrowthProgress.GetValueOrDefault("agility"), 40, "敏捷成长应为40。" );
        _test.Eq(skill.AttributeGrowthProgress.GetValueOrDefault("perception"), 20, "感知成长应为20。" );
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "双弦连射应只声明一个武器家族。" );
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "双弦连射必须使用弓。" );
        _test.False(combat.AllowsNaturalWeapon, "双弦连射不得把天生武器视为弓。" );

        CombatEffectDefinition damage = FindEffect(skill, BattleEffectKind.Damage);
        CombatEffectDefinition repeat = FindEffect(skill, BattleEffectKind.FixedRepeatAttack);
        _test.True(damage?.AddWeaponDice == true, "每一箭必须使用当前弓武器骰。" );
        _test.True(damage?.RequiresWeapon == true, "伤害载荷必须要求实际武器。" );
        _test.True(damage?.UseWeaponPhysicalDamageTag == true, "伤害类型必须来自当前弓。" );
        _test.True(damage?.ResolveAsWeaponAttack == true, "两箭必须走标准武器攻击链。" );
        _test.Eq(repeat?.FixedAttackCount ?? 0, 2, "双弦连射应固定最多结算两箭。" );
        _test.True(repeat?.StopOnMiss == true, "第一箭未命中必须停止第二箭。" );
        _test.True(repeat?.StopOnTargetDown == true, "第一箭击倒目标后不得再射第二箭。" );
        _test.Eq(repeat?.FollowUpDamageMultiplierPercent ?? 0, 200, "第二箭默认应造成200%伤害。" );

        int[] expectedFirstBonuses = { 0, 0, 0, 1, 1, 2 };
        int[] expectedSecondBonuses = { 0, 0, 1, 2, 3, 4 };
        for (int level = 0; level <= 5; level++)
        {
            BattleRepeatAttackStageSpec first =
                BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                    repeat,
                    0,
                    2,
                    level
                );
            BattleRepeatAttackStageSpec second =
                BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                    repeat,
                    1,
                    2,
                    level
                );
            int firstBonus = combat.GetEffectiveAttackRollBonus(level)
                + first.stage_base_attack_bonus;
            int secondBonus = combat.GetEffectiveAttackRollBonus(level)
                + second.stage_base_attack_bonus;
            _test.Eq(firstBonus, expectedFirstBonuses[level], $"L{level}第一箭攻击加值不符。" );
            _test.Eq(secondBonus, expectedSecondBonuses[level], $"L{level}第二箭攻击加值不符。" );
            _test.Eq(first.stage_damage_multiplier_percent, 100, $"L{level}第一箭必须保持100%伤害。" );
            _test.Eq(second.stage_damage_multiplier_percent, 200, $"L{level}第二箭必须保持200%伤害。" );

            CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 1, $"L{level}应消耗1AP。" );
            _test.Eq(costs.StaminaCost, 24, $"L{level}应消耗24体力。" );
            _test.Eq(costs.CooldownTu, 60, $"L{level}应有60TU冷却。" );
        }

        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new GDictionary()
        );
        _test.True(levelFive.Contains("第一箭"), "玩家描述必须明确第一箭规则。" );
        _test.True(levelFive.Contains("+2"), "L5描述必须显示第一箭+2。" );
        _test.True(levelFive.Contains("+4"), "L5描述必须显示第二箭+4。" );
        _test.True(levelFive.Contains("200%"), "玩家描述必须显示第二箭200%伤害。" );
    }

    private void TestCanonicalPreview(SkillDefinition skill)
    {
        using BattleTestFixture fixture = BuildFixture(
            skill,
            level: 3,
            weaponFamily: "bow",
            weaponRangeType: "ranged",
            weaponRange: 4
        );
        BattleCommand command = BuildCommand(fixture.Allies[0], fixture.Enemies[0], skill.SkillId);
        BattlePreview preview = null;
        try
        {
            preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "持弓时双弦连射预览应合法。" );
            _test.Eq(preview?.hit_preview?.StageCount ?? 0, 2, "命中预览必须展示两箭。" );
            if (preview?.hit_preview?.StageCount == 2)
            {
                AttackPreviewStage first = preview.hit_preview.Stages[0];
                AttackPreviewStage second = preview.hit_preview.Stages[1];
                _test.Eq(first.DamageMultiplierPercent, 100, "预览第一箭应显示100%伤害。" );
                _test.Eq(second.DamageMultiplierPercent, 200, "预览第二箭应显示200%伤害。" );
                _test.Eq(first.ReachProbabilityBasisPoints, 10000, "第一箭到达率应为100%。" );
                _test.Eq(
                    second.ReachProbabilityBasisPoints,
                    first.SuccessRatePercent * 100,
                    "第二箭到达率必须由第一箭成功率决定。"
                );
                int expectedDamageBasisPoints =
                    first.SuccessRatePercent * 100
                    + second.ReachProbabilityBasisPoints
                        * second.SuccessRatePercent
                        * 200
                        / 10000;
                _test.Eq(
                    preview.hit_preview.RepeatAttackExpectedDamageBasisPoints,
                    expectedDamageBasisPoints,
                    "连击预览必须按条件命中率计算伤害期望。"
                );
                _test.Eq(
                    preview.hit_preview.RepeatAttackPotentialDamageBasisPoints,
                    30000,
                    "两箭全部命中时应有300%单箭伤害潜力。"
                );
            }
            _test.True(preview?.DamagePreviewTyped != null, "双弦连射应提供 canonical 伤害预览。" );
            if (preview?.DamagePreviewTyped is { } damagePreview)
            {
                _test.Eq(damagePreview.MinDamage, 3, "1D6弓的两箭预览最小伤害应为1+2。" );
                _test.Eq(damagePreview.MaxDamage, 18, "1D6弓的两箭预览最大伤害应为6+12。" );
            }
            using GodotProjectionLease<GDictionary> lease = BattlePreviewProjection.BuildLease(preview);
            GDictionary hitPayload = lease.Value["hit_preview"].AsGodotDictionary();
            _test.Eq(
                hitPayload["repeat_attack_potential_damage_basis_points"].AsInt32(),
                30000,
                "HUD投影必须保留两箭总伤害潜力。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestFirstMissStopsSecondArrowAndChargesCommand(SkillDefinition skill)
    {
        using BattleTestFixture fixture = BuildFixture(
            skill,
            level: 0,
            weaponFamily: "bow",
            weaponRangeType: "ranged",
            weaponRange: 4
        );
        StageOutcomeDamageResolver resolver = new();
        resolver.stage_successes.Add(false);
        resolver.stage_successes.Add(true);
        resolver.stage_damage.Add(0);
        resolver.stage_damage.Add(99);
        fixture.Runtime.ConfigureDamageResolverForTests(resolver);
        BattleUnitState archer = fixture.Allies[0];
        BattleUnitState target = fixture.Enemies[0];
        BattleCommand command = BuildCommand(archer, target, skill.SkillId);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(batch != null, "第一箭落空仍应完成正式技能命令。" );
            _test.Eq(resolver.call_count, 1, "第一箭落空后不得调用第二箭伤害结算。" );
            _test.Eq(target.GetCurrentHp(), 1000, "未发射的第二箭不得造成伤害。" );
            _test.Eq(archer.GetCurrentAp(), 1, "技能命令应只消耗1AP。" );
            _test.Eq(archer.GetCurrentStamina(), 76, "技能命令应只消耗24体力。" );
            _test.Eq(archer.GetCooldownTyped(SkillId), 60, "第一箭落空仍应启动60TU冷却。" );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestWeaponGateRejectsNonBow(SkillDefinition skill)
    {
        using BattleTestFixture fixture = BuildFixture(
            skill,
            level: 0,
            weaponFamily: "sword",
            weaponRangeType: "melee",
            weaponRange: 1
        );
        BattleUnitState archer = fixture.Allies[0];
        BattleCommand command = BuildCommand(archer, fixture.Enemies[0], skill.SkillId);
        BattlePreview preview = null;
        try
        {
            preview = fixture.Runtime.PreviewCommand(command);
            _test.False(preview?.allowed == true, "非弓武器必须被 targeting 合法性拒绝。" );
            _test.Eq(archer.GetCurrentAp(), 2, "武器门槛拒绝不得消耗AP。" );
            _test.Eq(archer.GetCurrentStamina(), 100, "武器门槛拒绝不得消耗体力。" );
            _test.Eq(archer.GetCooldownTyped(SkillId), 0, "武器门槛拒绝不得启动冷却。" );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestFormalDamageUsesOnePlusTwoWeaponAttacks(SkillDefinition skill)
    {
        int doubleNockDamage = ExecuteMaxDamage(skill);
        _test.Eq(
            doubleNockDamage,
            18,
            "1D6弓取最大骰时，正式结算应为第一箭6加第二箭12，而不是两个固定伤害。"
        );
    }

    private int ExecuteMaxDamage(SkillDefinition skill)
    {
        using BattleTestFixture fixture = BuildFixture(
            skill,
            level: 0,
            weaponFamily: "bow",
            weaponRangeType: "ranged",
            weaponRange: 4
        );
        BattleTestFixture.ConfigureDamageResolverForTests(
            fixture.Runtime,
            new FixedHitMaxDamageResolver()
        );
        BattleTestFixture.ConfigureHitResolverForTests(
            fixture.Runtime,
            new FixedHitResolver(10)
        );
        BattleUnitState target = fixture.Enemies[0];
        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(fixture.Allies[0], target, skill.SkillId);
        try
        {
            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(batch != null, $"{skill.SkillId}应通过正式命令完成伤害结算。" );
            return hpBefore - target.GetCurrentHp();
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(SkillPath, "archer_double_nock_regression");

    private static BattleTestFixture BuildFixture(
        SkillDefinition skill,
        int level,
        StringName weaponFamily,
        StringName weaponRangeType,
        int weaponRange
    )
    {
        BattleUnitState archer = BattleTestFixture.BuildUnit(
            $"{skill.SkillId}_archer",
            "player",
            new Vector2I(1, 1),
            currentHp: 1000
        );
        BattleUnitState target = BattleTestFixture.BuildUnit(
            $"{skill.SkillId}_target",
            "enemy",
            new Vector2I(3, 1),
            currentHp: 1000
        );
        archer.AddKnownActiveSkill(skill.SkillId);
        archer.SetKnownSkillLevelTyped(skill.SkillId, level, preserveZero: true);
        archer.SetCurrentAp(2);
        archer.SetCurrentStamina(100);
        archer.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        archer.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        ApplyWeapon(archer, weaponFamily, weaponRangeType, weaponRange);

        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "archer_double_nock_regression",
            new Vector2I(7, 4),
            new[] { archer },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = archer.unit_id;
        return fixture;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int range
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"double_nock_{family}_weapon",
                weapon_profile_type_id = $"test_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = range,
                weapon_one_handed_dice = new WeaponDice(),
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = family == "bow"
                    ? "physical_pierce"
                    : "physical_slash",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState source,
        BattleUnitState target,
        StringName skillId
    ) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = source.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static CombatEffectDefinition FindEffect(
        SkillDefinition skill,
        BattleEffectKind kind
    )
    {
        foreach (
            CombatEffectDefinition effect in
                skill?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect?.EffectKind == kind)
            {
                return effect;
            }
        }
        return null;
    }

    private static bool ContainsError(GStringArray errors, string fragment)
    {
        foreach (string error in errors ?? new GStringArray())
        {
            if (error.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
