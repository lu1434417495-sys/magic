using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_backstep_shot_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "res://data/configs/skills/archer_backstep_shot.tres";
    private static readonly StringName SkillId = "archer_backstep_shot";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestLevelCurveAndDescriptions(skill);
            TestBowGateAndWeaponRange(skill);
            TestSchemaRejectsInvalidSourceRetreatProfiles();
            TestDirectionMustBeExplicitCardinalAndAway(skill);
            TestFullRetreatUsesNoMovePoints(skill);
            TestFirstAndSecondStepBlockageShortenRetreat(skill);
            TestEdgeAndBarrierBlockageShortenRetreat(skill);
            TestMissAndLethalHitStillRetreat(skill);
            TestMovementLocksRejectBeforeCost(skill);
            TestManualSelectionRequiresTargetThenDirection(skill);
            TestAiEnumeratesCanonicalRetreatDirections(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer backstep shot regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "后跃射正式资源与 combat_profile 应可加载。");
        if (combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "后跃射 skill_id 应稳定。");
        _test.Eq(skill.DisplayName, "后跃射", "后跃射显示名应稳定。");
        _test.Eq(skill.MaxLevel, 5, "后跃射核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "后跃射非核心上限应为3级。");
        _test.Eq(skill.MasteryCurve.Count, 5, "熟练度曲线应覆盖五级。");
        _test.Eq(skill.MasteryCurve[0], 100, "1级熟练度阈值应为100。");
        _test.Eq(skill.MasteryCurve[4], 1600, "5级熟练度阈值应为1600。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "后跃射应属于基础成长档。");
        _test.Eq(ReadGrowth(skill, "agility"), 40, "后跃射应提供40点敏捷成长进度。");
        _test.Eq(ReadGrowth(skill, "perception"), 20, "后跃射应提供20点感知成长进度。");
        _test.True(skill.Description.Contains("标准武器攻击"), "描述应明确这是标准武器攻击。");
        _test.True(skill.Description.Contains("最多2格"), "描述应明确后撤距离上限。");
        _test.True(skill.Description.Contains("不消耗移动力"), "描述应明确免费后撤。");
        _test.False(skill.Description.Contains("固定伤害"), "非魔法弓术不得声明固定伤害。");
        _test.False(skill.Description.Contains("攻击检定+0"), "描述不得显示无意义的攻击检定+0。");

        _test.Eq(combat.TargetMode, new StringName("unit"), "后跃射应以单位为目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "后跃射只能选择敌方。");
        _test.Eq(
            combat.TargetSelectionMode,
            new StringName("single_unit"),
            "后跃射必须选择单个单位目标。"
        );
        _test.Eq(combat.MinTargetCount, 1, "后跃射必须恰好选择一个目标。");
        _test.Eq(combat.MaxTargetCount, 1, "后跃射不得选择多个目标。");
        _test.True(combat.RequiresLos, "弓箭标准武器攻击应要求视线。");
        _test.Eq(combat.RangeValue, 0, "配置射程应交给当前弓武器提供。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "后跃射应只允许弓家族。");
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "武器家族应为 bow。");
        _test.False(combat.AllowsNaturalWeapon, "天生武器不得绕过弓装备门禁。");
        _test.Eq(
            combat.MasteryTriggerMode,
            new StringName("weapon_attack_quality"),
            "后跃射熟练度应按武器攻击质量结算。"
        );

        CombatEffectDefinition damage = FindEffect(combat, BattleEffectKind.Damage);
        CombatEffectDefinition retreat = FindEffect(combat, BattleEffectKind.SourceRetreat);
        _test.True(damage != null, "后跃射应包含标准武器伤害效果。");
        _test.True(retreat != null, "后跃射应包含数据驱动的 source_retreat 效果。");
        if (damage != null)
        {
            _test.Eq(damage.Power, 0, "标准武器攻击不得附带固定伤害。");
            _test.True(damage.AddWeaponDice, "伤害应使用当前武器骰。");
            _test.True(damage.RequiresWeapon, "伤害效果应要求真实武器。");
            _test.True(damage.UseWeaponPhysicalDamageTag, "伤害类型应来自当前武器。");
            _test.True(damage.ResolveAsWeaponAttack, "伤害应进入标准武器攻击检定。");
        }
        _test.Eq(retreat?.SourceRetreatDistance ?? -1, 2, "后撤距离必须来自 typed 数据字段。");
    }

    private void TestLevelCurveAndDescriptions(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            _test.Fail("后跃射等级曲线需要有效 combat_profile。");
            return;
        }

        AssertLevel(skill, 1, stamina: 28, cooldownTu: 80, attackBonus: 0);
        AssertLevel(skill, 2, stamina: 26, cooldownTu: 80, attackBonus: 0);
        AssertLevel(skill, 3, stamina: 26, cooldownTu: 70, attackBonus: 0);
        AssertLevel(skill, 4, stamina: 24, cooldownTu: 70, attackBonus: 0);
        AssertLevel(skill, 5, stamina: 24, cooldownTu: 70, attackBonus: 1);

        string levelOne = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            1,
            new GDictionary()
        );
        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new GDictionary()
        );
        _test.False(levelOne.Contains("攻击检定"), "1级描述不得显示攻击检定+0。");
        _test.True(levelFive.Contains("攻击检定+1"), "5级描述应显示唯一的命中升级。");
        _test.True(levelOne.Contains("28体力"), "1级描述应显示28体力。");
        _test.True(levelFive.Contains("24体力"), "5级描述应显示24体力。");
    }

    private void TestBowGateAndWeaponRange(SkillDefinition skill)
    {
        BattleUnitState equippedBow = BuildReadyCaster("backstep_bow_gate", new Vector2I(1, 1));
        BattleUnitState equippedSword = BuildUnit("backstep_sword_gate", "player", Vector2I.Zero);
        ApplyWeapon(equippedSword, "sword", "melee", 1, "equipped");
        BattleUnitState naturalBow = BuildUnit("backstep_natural_gate", "player", Vector2I.Zero);
        ApplyWeapon(naturalBow, "bow", "ranged", 5, "natural");
        try
        {
            _test.True(
                BattleRangeService.UnitMatchesRequiredWeaponFamilies(equippedBow, skill),
                "装备弓应通过后跃射武器门禁。"
            );
            _test.Eq(
                BattleRangeService.GetEffectiveSkillRange(equippedBow, skill),
                4,
                "后跃射应采用当前弓的4格射程。"
            );
            _test.False(
                BattleRangeService.UnitMatchesRequiredWeaponFamilies(equippedSword, skill),
                "近战武器不得使用后跃射。"
            );
            _test.False(
                BattleRangeService.UnitMatchesRequiredWeaponFamilies(naturalBow, skill),
                "即使天生武器标记为 bow，也不得绕过装备弓要求。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(equippedBow);
            BattleTestFixture.DisposeBattleUnit(equippedSword);
            BattleTestFixture.DisposeBattleUnit(naturalBow);
        }
    }

    private void TestSchemaRejectsInvalidSourceRetreatProfiles()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using var invalidDistance = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 0,
        };
        using var invalidDistanceProfile = BuildSourceRetreatProfile(invalidDistance);
        var distanceErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            distanceErrors,
            "invalid_source_retreat_distance",
            invalidDistanceProfile
        );
        _test.True(
            ErrorsContain(distanceErrors, "source_retreat_distance >= 1"),
            $"0格 source_retreat 必须被 schema 拒绝。errors={string.Join(" | ", distanceErrors)}"
        );

        using var validRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 2,
        };
        using var delayedProfile = BuildSourceRetreatProfile(validRetreat);
        delayedProfile.casting_time_tu = 10;
        var delayedErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            delayedErrors,
            "invalid_delayed_source_retreat",
            delayedProfile
        );
        _test.True(
            ErrorsContain(delayedErrors, "cannot be combined with casting_time_tu"),
            $"需要即时选向的 source_retreat 不得进入读条。errors={string.Join(" | ", delayedErrors)}"
        );

        using var windupRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 2,
        };
        using var windupProfile = BuildSourceRetreatProfile(windupRetreat);
        using var windup = new CombatWindupDef();
        windupProfile.windup_profile = windup;
        var windupErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            windupErrors,
            "invalid_windup_source_retreat",
            windupProfile
        );
        _test.True(
            ErrorsContain(windupErrors, "or windup_profile"),
            $"source_retreat 不得与蓄力组合。errors={string.Join(" | ", windupErrors)}"
        );

        using var variantRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 2,
        };
        using var variantProfile = BuildSourceRetreatProfile(variantRetreat);
        using var castVariant = new CombatCastVariantDef
        {
            variant_id = "invalid_retreat_variant",
            required_coord_count = 1,
        };
        variantProfile.cast_variants.Add(castVariant);
        var variantErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            variantErrors,
            "invalid_variant_source_retreat",
            variantProfile
        );
        _test.True(
            ErrorsContain(variantErrors, "cannot be placed behind cast_variants"),
            $"source_retreat 不得依赖施法变体选向。errors={string.Join(" | ", variantErrors)}"
        );

        using var specialRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 2,
        };
        using var specialProfile = BuildSourceRetreatProfile(specialRetreat);
        specialProfile.random_chain_attack_count = 2;
        var specialErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            specialErrors,
            "invalid_special_source_retreat",
            specialProfile
        );
        _test.True(
            ErrorsContain(specialErrors, "cannot use special or random-chain resolution"),
            $"随机链不得自动生成后撤方向。errors={string.Join(" | ", specialErrors)}"
        );

        using var automaticRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 2,
        };
        using var automaticProfile = BuildSourceRetreatProfile(automaticRetreat);
        using var contingencyAutomation = new ContingencyAutomationDef
        {
            can_be_stored_in_contingency = true,
        };
        using var automaticSkill = new SkillDef
        {
            skill_id = "invalid_contingency_source_retreat",
            contingency_automation_profile = contingencyAutomation,
        };
        var automaticErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            automaticErrors,
            automaticSkill.skill_id,
            automaticProfile,
            automaticSkill
        );
        _test.True(
            ErrorsContain(automaticErrors, "cannot be stored in contingency"),
            $"Contingency 不得在无人选向时储存后撤技能。errors={string.Join(" | ", automaticErrors)}"
        );

        using var firstDuplicateRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 1,
        };
        using var secondDuplicateRetreat = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectSourceRetreat,
            source_retreat_distance = 2,
        };
        using var duplicateProfile = BuildSourceRetreatProfile(firstDuplicateRetreat);
        duplicateProfile.effect_defs.Add(secondDuplicateRetreat);
        var duplicateErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            duplicateErrors,
            "invalid_duplicate_source_retreat",
            duplicateProfile
        );
        _test.True(
            ErrorsContain(duplicateErrors, "must appear exactly once"),
            $"一个技能不得声明多个后撤提交。errors={string.Join(" | ", duplicateErrors)}"
        );

        using var wrongOwner = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectDamage,
            power = 1,
            source_retreat_distance = 2,
        };
        var ownerErrors = new GStringArray();
        validator.AppendEffectValidationErrors(
            ownerErrors,
            "invalid_source_retreat_owner",
            wrongOwner,
            "combat_profile.effect_defs[0]"
        );
        _test.True(
            ErrorsContain(ownerErrors, "only supported on source_retreat"),
            $"后撤距离字段不得挂到其他效果。errors={string.Join(" | ", ownerErrors)}"
        );
    }

    private void TestDirectionMustBeExplicitCardinalAndAway(SkillDefinition skill)
    {
        using BattleTestFixture fixture = CreateFixture(
            skill,
            BuildReadyCaster("direction_caster", new Vector2I(2, 2)),
            BuildUnit("direction_target", "enemy", new Vector2I(3, 2))
        );
        AssertRejectedWithoutCost(
            fixture,
            BuildCommand(fixture.Allies[0], fixture.Enemies[0], Vector2I.Zero),
            "缺少后撤方向时"
        );
        AssertRejectedWithoutCost(
            fixture,
            BuildCommand(fixture.Allies[0], fixture.Enemies[0], new Vector2I(-1, -1)),
            "方向不是单位正交向量时"
        );
        AssertRejectedWithoutCost(
            fixture,
            BuildCommand(fixture.Allies[0], fixture.Enemies[0], Vector2I.Right),
            "方向靠近目标时"
        );
    }

    private void TestFullRetreatUsesNoMovePoints(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("full_retreat_caster", new Vector2I(2, 2));
        BattleUnitState target = BuildUnit("full_retreat_target", "enemy", new Vector2I(3, 2));
        caster.SetCurrentMovePoints(0);
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        ConfigureHit(fixture);

        BattleCommand command = BuildCommand(caster, target, Vector2I.Left);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "移动力为0时，免费后撤仍应可预览。");
        _test.Eq(preview?.move_cost ?? -1, 0, "后跃射预览移动消耗应为0。");
        _test.Eq(preview?.SourceRetreatPathTyped.Count ?? -1, 3, "完整预览应包含起点和两步后撤。");
        _test.Eq(preview?.resolved_anchor_coord ?? new Vector2I(-1, -1), new Vector2I(0, 2), "预览落点应为直线后撤2格。");
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(2, 2), "预览不得修改正式坐标。");

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(target.GetCurrentHp() < hpBefore, "后跃射应完成真实标准武器伤害。");
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(0, 2), "命中后应沿选定直线后撤2格。");
        _test.Eq(caster.GetCurrentMovePoints(), 0, "后撤不得消耗或要求移动力。");
        _test.Eq(caster.GetCurrentAp(), 1, "施放应只消耗技能的1 AP。");
        _test.Eq(caster.GetCurrentStamina(), 72, "1级施放应消耗28体力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 80, "1级施放应进入80TU冷却。");
        _test.True(batch?.changed_unit_ids.Contains(caster.unit_id) == true, "后撤单位应进入变更集合。");
        _test.True(batch?.changed_unit_ids.Contains(target.unit_id) == true, "受击目标应进入变更集合。");
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestFirstAndSecondStepBlockageShortenRetreat(SkillDefinition skill)
    {
        BattleUnitState firstCaster = BuildReadyCaster("first_block_caster", new Vector2I(2, 2));
        BattleUnitState firstTarget = BuildUnit("first_block_target", "enemy", new Vector2I(3, 2));
        BattleUnitState firstBlocker = BuildUnit("first_blocker", "enemy", new Vector2I(1, 2));
        using (BattleTestFixture fixture = CreateFixture(skill, firstCaster, firstTarget, firstBlocker))
        {
            ConfigureHit(fixture);
            BattleCommand command = BuildCommand(firstCaster, firstTarget, Vector2I.Left);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "第一步受阻时攻击本身仍应允许。");
            _test.Eq(preview?.SourceRetreatPathTyped.Count ?? -1, 1, "第一步受阻的预览应只保留起点。");
            int hpBefore = firstTarget.GetCurrentHp();
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(firstTarget.GetCurrentHp() < hpBefore, "第一步受阻不能取消攻击。");
            _test.Eq(firstCaster.GetAnchorCoord(), new Vector2I(2, 2), "第一步受阻应停在原地。");
            _test.Eq(firstCaster.GetCurrentStamina(), 72, "攻击成功仍只支付一次技能体力。");
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }

        BattleUnitState secondCaster = BuildReadyCaster("second_block_caster", new Vector2I(2, 2));
        BattleUnitState secondTarget = BuildUnit("second_block_target", "enemy", new Vector2I(3, 2));
        BattleUnitState secondBlocker = BuildUnit("second_blocker", "enemy", new Vector2I(0, 2));
        using (BattleTestFixture fixture = CreateFixture(skill, secondCaster, secondTarget, secondBlocker))
        {
            ConfigureHit(fixture);
            BattleCommand command = BuildCommand(secondCaster, secondTarget, Vector2I.Left);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "第二步受阻时技能仍应允许。");
            _test.Eq(preview?.SourceRetreatPathTyped.Count ?? -1, 2, "第二步受阻应预览一格后撤。");
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(secondCaster.GetAnchorCoord(), new Vector2I(1, 2), "第二步受阻应只后撤1格。");
            _test.Eq(secondCaster.GetCurrentMovePoints(), 3, "缩短后撤仍不得消耗移动力。");
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestEdgeAndBarrierBlockageShortenRetreat(SkillDefinition skill)
    {
        BattleUnitState wallCaster = BuildReadyCaster(
            "wall_retreat_caster",
            new Vector2I(2, 2)
        );
        BattleUnitState wallTarget = BuildUnit(
            "wall_retreat_target",
            "enemy",
            new Vector2I(3, 2)
        );
        using (BattleTestFixture fixture = CreateFixture(skill, wallCaster, wallTarget))
        {
            ConfigureHit(fixture);
            fixture.Runtime
                .GetGridService()
                .SetEdgeFeature(
                    fixture.State,
                    wallCaster.GetAnchorCoord() + Vector2I.Left,
                    Vector2I.Right,
                    BattleEdgeFeatureState.MakeWall()
                );
            BattleCommand command = BuildCommand(wallCaster, wallTarget, Vector2I.Left);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "第一步遇墙时不能取消攻击。");
            _test.Eq(preview?.SourceRetreatPathTyped.Count ?? -1, 1, "墙应把后撤截断在起点。");
            int hpBefore = wallTarget.GetCurrentHp();
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.True(wallTarget.GetCurrentHp() < hpBefore, "墙只应阻挡后撤，不应阻挡朝反方向的射击。");
            _test.Eq(wallCaster.GetAnchorCoord(), new Vector2I(2, 2), "第一步遇墙应留在原地。");
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }

        BattleUnitState barrierCaster = BuildReadyCaster(
            "barrier_retreat_caster",
            new Vector2I(2, 2)
        );
        BattleUnitState barrierTarget = BuildUnit(
            "barrier_retreat_target",
            "enemy",
            new Vector2I(3, 2)
        );
        using (BattleTestFixture fixture = CreateFixture(skill, barrierCaster, barrierTarget))
        {
            ConfigureHit(fixture);
            var barrier = new BattleBarrierInstanceState
            {
                BarrierInstanceId = "backstep_retreat_barrier",
                ProfileId = "backstep_retreat_barrier",
                DisplayName = "后撤测试屏障",
                SourceUnitId = "other_unit",
                AnchorCoord = new Vector2I(0, 2),
                RadiusCells = 0,
                AreaPattern = "diamond",
                RemainingTu = 100,
            };
            fixture.State.PutLayeredBarrierFieldPayload(
                barrier.BarrierInstanceId,
                barrier.ToRuntimeDict()
            );
            BattleCommand command = BuildCommand(
                barrierCaster,
                barrierTarget,
                Vector2I.Left
            );
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "第二步遇屏障时技能仍应允许。");
            _test.Eq(
                preview?.SourceRetreatPathTyped.Count ?? -1,
                2,
                "屏障边界应把后撤缩短为1格。"
            );
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(
                barrierCaster.GetAnchorCoord(),
                new Vector2I(1, 2),
                "第二步跨越屏障边界时应停在边界前。"
            );
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestMissAndLethalHitStillRetreat(SkillDefinition skill)
    {
        BattleUnitState missCaster = BuildReadyCaster("miss_retreat_caster", new Vector2I(2, 2));
        BattleUnitState missTarget = BuildUnit("miss_retreat_target", "enemy", new Vector2I(3, 2));
        using (BattleTestFixture fixture = CreateFixture(skill, missCaster, missTarget))
        {
            fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
            fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
            int hpBefore = missTarget.GetCurrentHp();
            BattleCommand command = BuildCommand(missCaster, missTarget, Vector2I.Left);
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(missTarget.GetCurrentHp(), hpBefore, "未命中不得造成伤害。");
            _test.Eq(missCaster.GetAnchorCoord(), new Vector2I(0, 2), "未命中仍必须后撤。");
            _test.Eq(missCaster.GetCurrentStamina(), 72, "未命中仍应支付一次技能体力。");
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }

        BattleUnitState lethalCaster = BuildReadyCaster("lethal_retreat_caster", new Vector2I(2, 2));
        BattleUnitState lethalTarget = BuildUnit("lethal_retreat_target", "enemy", new Vector2I(3, 2));
        lethalTarget.SetCurrentHp(1);
        using (BattleTestFixture fixture = CreateFixture(skill, lethalCaster, lethalTarget))
        {
            ConfigureHit(fixture);
            BattleCommand command = BuildCommand(lethalCaster, lethalTarget, Vector2I.Left);
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.False(lethalTarget.IsAlive(), "1HP目标应被标准武器攻击击倒。");
            _test.Eq(lethalCaster.GetAnchorCoord(), new Vector2I(0, 2), "目标被击倒也不得取消后撤。");
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestMovementLocksRejectBeforeCost(SkillDefinition skill)
    {
        StringName[] movementLocks =
        {
            BattleStatusSemanticTable.STATUS_PINNED,
            BattleStatusSemanticTable.STATUS_ROOTED,
            BattleStatusSemanticTable.STATUS_TENDON_CUT,
            BattleStatusSemanticTable.STATUS_PETRIFIED,
            BattleStatusSemanticTable.STATUS_PARALYZED,
            BattleStatusSemanticTable.STATUS_TIME_STASIS,
        };
        foreach (StringName statusId in movementLocks)
        {
            BattleUnitState caster = BuildReadyCaster(
                $"{statusId}_retreat_caster",
                new Vector2I(2, 2)
            );
            BattleUnitState target = BuildUnit(
                $"{statusId}_retreat_target",
                "enemy",
                new Vector2I(3, 2)
            );
            caster.SetStatusEffect(
                new BattleStatusEffectState
                {
                    status_id = statusId,
                    duration = 20,
                    stacks = 1,
                }
            );
            using BattleTestFixture fixture = CreateFixture(skill, caster, target);
            BattleCommand command = BuildCommand(caster, target, Vector2I.Left);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(
                preview != null && !preview.allowed,
                $"{statusId} 应在预览阶段拒绝整个技能。"
            );
            _test.True(
                LogsContain(preview?.LogLinesTyped, "限制移动")
                    || statusId == BattleStatusSemanticTable.STATUS_PETRIFIED
                    || statusId == BattleStatusSemanticTable.STATUS_PARALYZED,
                $"{statusId} 的拒绝原因应保持正式状态门禁语义。logs={string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(caster.GetCurrentAp(), 2, $"{statusId} 拒绝不得消耗AP。");
            _test.Eq(caster.GetCurrentStamina(), 100, $"{statusId} 拒绝不得消耗体力。");
            _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{statusId} 拒绝不得启动冷却。");
            _test.Eq(
                caster.GetAnchorCoord(),
                new Vector2I(2, 2),
                $"{statusId} 拒绝不得改变坐标。"
            );
            batch?.Dispose();
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestManualSelectionRequiresTargetThenDirection(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("manual_retreat_caster", new Vector2I(2, 2));
        BattleUnitState target = BuildUnit("manual_retreat_target", "enemy", new Vector2I(3, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        var port = new TestBattleSelectionPort(fixture, new SingleSkillCatalog(skill))
        {
            SelectedSkillId = SkillId,
            SelectedSkillEntryId = BattleSkillEntryIds.KnownSkill(SkillId),
        };
        using var selection = new GameRuntimeBattleSelection();
        selection.Setup(port);

        BattleRefreshMode targetResult = selection.AttemptBattleMoveTo(target.GetAnchorCoord());
        _test.Eq(targetResult, BattleRefreshMode.Overlay, "第一次点击应只锁定攻击目标。");
        _test.Eq(
            port.SelectionStage,
            GameRuntimeBattleSelectionStage.SourceRetreatDirection,
            "选中目标后应进入后撤方向阶段。"
        );
        _test.Eq(port.TargetUnitIds.Count, 1, "方向阶段应保留唯一攻击目标。");
        _test.True(port.LastIssuedCommand == null, "选择目标时不得提前施放技能。");

        BattleRefreshMode invalidDirection = selection.AttemptBattleMoveTo(new Vector2I(3, 3));
        _test.Eq(invalidDirection, BattleRefreshMode.Error, "斜向点击必须被拒绝。");
        _test.True(port.LastIssuedCommand == null, "非法方向不得发出命令。");
        _test.Eq(
            port.SelectionStage,
            GameRuntimeBattleSelectionStage.SourceRetreatDirection,
            "非法方向后应继续等待方向选择。"
        );

        selection.AttemptBattleMoveTo(new Vector2I(1, 2));
        _test.True(port.LastIssuedCommand != null, "合法直线方向应发出技能命令。");
        _test.Eq(
            port.LastIssuedCommand?.source_retreat_direction ?? Vector2I.Zero,
            Vector2I.Left,
            "玩家选中的方向必须原样进入 BattleCommand。"
        );
        _test.Eq(
            port.LastIssuedCommand?.target_unit_id ?? new StringName(""),
            target.unit_id,
            "方向确认后的命令必须保留第一阶段目标。"
        );
    }

    private void TestAiEnumeratesCanonicalRetreatDirections(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("ai_retreat_caster", new Vector2I(2, 2));
        BattleUnitState target = BuildUnit("ai_retreat_target", "enemy", new Vector2I(3, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        var evaluatedDirections = new HashSet<Vector2I>();
        BattleAiContext context = new()
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
            trace_enabled = true,
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            preview_command_callback = fixture.Runtime.PreviewCommand,
            skill_score_input_callback = (
                _,
                _,
                command,
                preview,
                _,
                _,
                _
            ) =>
            {
                evaluatedDirections.Add(command.source_retreat_direction);
                return new BattleAiScoreInput
                {
                    command = command,
                    preview = preview,
                    effective_target_count = 1,
                    enemy_target_count = 1,
                    total_score =
                        command.source_retreat_direction == Vector2I.Left
                            ? 100
                            : 10,
                };
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        var action = new UseUnitSkillActionDefinition(
            "backstep_shot_ai",
            "test",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            1,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );

        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            action,
            context
        );
        _test.Eq(evaluatedDirections.Count, 3, "目标在右侧时，AI应独立评估上、下、左三个远离方向。");
        _test.True(evaluatedDirections.Contains(Vector2I.Up), "AI应枚举向上后撤。");
        _test.True(evaluatedDirections.Contains(Vector2I.Down), "AI应枚举向下后撤。");
        _test.True(evaluatedDirections.Contains(Vector2I.Left), "AI应枚举向左后撤。");
        _test.False(evaluatedDirections.Contains(Vector2I.Right), "AI不得枚举靠近目标的方向。");
        _test.Eq(
            decision?.command?.source_retreat_direction ?? Vector2I.Zero,
            Vector2I.Left,
            "AI应保留评分最高候选的实际后撤方向。"
        );
        _test.Eq(
            decision?.score_input?.preview?.resolved_anchor_coord
                ?? new Vector2I(-1, -1),
            new Vector2I(0, 2),
            "AI决策应携带该方向的 canonical 最终落点。"
        );
        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "AI后撤技能应记录一次 action trace。");
        _test.Eq(
            traces.Count == 1 ? traces[0].EvaluationCount : -1,
            3,
            "AI trace 应证明三个合法方向都经过候选评估。"
        );
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldownTu,
        int attackBonus
    )
    {
        CombatSkillResourceCosts costs =
            skill.CombatProfile.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 1, $"后跃射{level}级应消耗1 AP。");
        _test.Eq(costs.StaminaCost, stamina, $"后跃射{level}级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"后跃射{level}级冷却应正确。");
        _test.Eq(
            skill.CombatProfile.GetEffectiveAttackRollBonus(level),
            attackBonus,
            $"后跃射{level}级攻击检定应正确。"
        );
    }

    private void AssertRejectedWithoutCost(
        BattleTestFixture fixture,
        BattleCommand command,
        string label
    )
    {
        BattleUnitState caster = fixture.Allies[0];
        int apBefore = caster.GetCurrentAp();
        int staminaBefore = caster.GetCurrentStamina();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, $"{label}预览必须拒绝。");
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), apBefore, $"{label}不得消耗AP。");
        _test.Eq(caster.GetCurrentStamina(), staminaBefore, $"{label}不得消耗体力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{label}不得启动冷却。");
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static CombatSkillDef BuildSourceRetreatProfile(CombatEffectDef effect)
    {
        var profile = new CombatSkillDef
        {
            skill_id = "source_retreat_schema_probe",
            target_mode = "unit",
            target_selection_mode = "single_unit",
            min_target_count = 1,
            max_target_count = 1,
        };
        profile.effect_defs.Add(effect);
        return profile;
    }

    private static bool ErrorsContain(IEnumerable<string> errors, string needle)
    {
        foreach (string error in errors ?? Array.Empty<string>())
        {
            if (error?.Contains(needle, StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }

    private static bool LogsContain(IEnumerable<string> lines, string needle)
    {
        foreach (string line in lines ?? Array.Empty<string>())
        {
            if (line?.Contains(needle, StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }

    private static CombatEffectDefinition FindEffect(
        CombatSkillDefinition combat,
        BattleEffectKind effectKind
    )
    {
        foreach (
            CombatEffectDefinition effect
            in combat?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect?.EffectKind == effectKind)
                return effect;
        }
        return null;
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "archer_backstep_shot_regression"
        );

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState target,
        params BattleUnitState[] blockers
    )
    {
        var enemies = new List<BattleUnitState> { target };
        enemies.AddRange(blockers ?? Array.Empty<BattleUnitState>());
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "archer_backstep_shot",
            new Vector2I(7, 5),
            new[] { caster },
            enemies
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        return fixture;
    }

    private static void ConfigureHit(BattleTestFixture fixture)
    {
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
    }

    private static BattleUnitState BuildReadyCaster(StringName id, Vector2I coord)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 1);
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        caster.SetCurrentMovePoints(3);
        ApplyWeapon(caster, "bow", "ranged", 4, "equipped");
        return caster;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentHp(100);
        unit.SetCurrentAp(2);
        unit.SetCurrentStamina(100);
        unit.SetCurrentMovePoints(3);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int attackRange,
        StringName profileKind
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = profileKind,
                weapon_item_id =
                    profileKind == "equipped" ? $"backstep_test_{family}" : "",
                weapon_profile_type_id = $"backstep_test_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target,
        Vector2I direction
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
            source_retreat_direction = direction,
        };

    private sealed class SingleSkillCatalog : ISkillCatalog
    {
        private readonly IReadOnlyDictionary<StringName, SkillDefinition> _definitions;

        internal SingleSkillCatalog(SkillDefinition skillDefinition)
        {
            _definitions = new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            };
        }

        public long GetRevision() => 1;

        public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
            _definitions;

        public bool HasSkill(StringName skillId) => _definitions.ContainsKey(skillId);

        public bool TryGetSkillDefinition(
            StringName skillId,
            out SkillDefinition skillDefinition
        ) => _definitions.TryGetValue(skillId, out skillDefinition);

        public SkillEffectiveCombatDefinition GetEffectiveCombatDefinition(
            StringName skillId,
            int skillLevel
        ) =>
            TryGetSkillDefinition(skillId, out SkillDefinition skillDefinition)
                ? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel)
                : SkillEffectiveCombatDefinition.BuildMissing(skillLevel);

        public CombatSkillResourceCosts GetEffectiveResourceCostValues(
            StringName skillId,
            int skillLevel
        ) => GetEffectiveCombatDefinition(skillId, skillLevel).ResourceCosts;

        public int GetEffectiveAttackRollBonus(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).AttackRollBonus;

        public StringName GetEffectiveAreaPattern(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).AreaPattern;

        public int GetEffectiveAreaValue(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).AreaValue;

        public int GetEffectiveRangeValue(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).RangeValue;

        public int GetEffectiveMaxTargetCount(StringName skillId, int skillLevel) =>
            GetEffectiveCombatDefinition(skillId, skillLevel).MaxTargetCount;

        public IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariantDefinitions(
            StringName skillId,
            int skillLevel
        ) => GetEffectiveCombatDefinition(skillId, skillLevel).UnlockedCastVariants;
    }

    private sealed class TestBattleSelectionPort : IGameRuntimeBattleSelectionPort
    {
        private static readonly IReadOnlyDictionary<
            StringName,
            EquipmentAbilityBindingDefinition
        > EmptyBindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
        private readonly BattleTestFixture _fixture;
        private readonly ISkillCatalog _skillCatalog;

        internal TestBattleSelectionPort(
            BattleTestFixture fixture,
            ISkillCatalog skillCatalog
        )
        {
            _fixture = fixture;
            _skillCatalog = skillCatalog;
        }

        internal StringName SelectedSkillId { get; set; } = "";
        internal StringName SelectedSkillEntryId { get; set; } = "";
        internal StringName SelectedSkillVariantId { get; set; } = "";
        internal int SelectedWindupTier { get; set; } = 1;
        internal GameRuntimeBattleSelectionStage SelectionStage { get; set; } =
            GameRuntimeBattleSelectionStage.Target;
        internal StringName LastManualUnitId { get; set; } = "";
        internal List<Vector2I> TargetCoords { get; } = new();
        internal List<StringName> TargetUnitIds { get; } = new();
        internal Vector2I SelectedCoord { get; set; } = new(-1, -1);
        internal int RefreshCount { get; private set; }
        internal string LastStatus { get; private set; } = "";
        internal BattleCommand LastIssuedCommand { get; private set; }
        internal BattleCommand LastPreviewCommand { get; private set; }

        public Vector2I GetBattleSelectedCoord() => SelectedCoord;

        public BattleUnitState GetManualBattleUnit() => _fixture.Allies[0];

        public BattleUnitState GetRuntimeBattleActiveUnit() => _fixture.Allies[0];

        public BattleUnitState GetRuntimeBattleUnitAtCoord(Vector2I coord)
        {
            foreach (BattleUnitState unit in _fixture.State.GetUnitsTyped())
            {
                if (unit?.OccupiesCoord(coord) == true)
                    return unit;
            }
            return null;
        }

        public BattleUnitState GetRuntimeBattleUnitById(StringName unitId) =>
            _fixture.State.GetUnit(unitId);

        public BattleState GetBattleState() => _fixture.State;

        public BattleGridService GetBattleGridService() =>
            _fixture.Runtime.GetGridService();

        public ISkillCatalog GetSkillCatalog() => _skillCatalog;

        public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
            GetEquipmentAbilityBindings() => EmptyBindings;

        public int GetBattleWorldStep() => 0;

        public BattlePreview PreviewBattleCommand(BattleCommand command)
        {
            LastPreviewCommand = command;
            return _fixture.Runtime.PreviewCommand(command);
        }

        public string GetBattleSkillCastBlockMessage(
            BattleUnitState activeUnit,
            StringName skillId
        ) => "";

        public BattleRefreshMode IssueBattleCommand(BattleCommand command)
        {
            LastIssuedCommand = command;
            return BattleRefreshMode.None;
        }

        public void RefreshBattleSelectionState() => RefreshCount++;

        public void UpdateStatus(string message) => LastStatus = message ?? "";

        public string FormatCoord(Vector2I coord) => coord.ToString();

        public bool IsBattleActive() => true;

        public StringName GetSelectedSkillId() => SelectedSkillId;

        public StringName GetSelectedSkillEntryId() => SelectedSkillEntryId;

        public void SetSelectedSkillEntryId(StringName skillEntryId) =>
            SelectedSkillEntryId = skillEntryId;

        public void SetSelectedSkillId(StringName skillId) =>
            SelectedSkillId = skillId;

        public StringName GetSelectedSkillVariantId() => SelectedSkillVariantId;

        public void SetSelectedSkillVariantId(StringName variantId) =>
            SelectedSkillVariantId = variantId;

        public int GetSelectedWindupTier() => SelectedWindupTier;

        public void SetSelectedWindupTier(int tier) =>
            SelectedWindupTier = Math.Max(tier, 1);

        public GameRuntimeBattleSelectionStage GetSelectionStage() => SelectionStage;

        public void SetSelectionStage(GameRuntimeBattleSelectionStage stage) =>
            SelectionStage = stage;

        public StringName GetLastManualUnitId() => LastManualUnitId;

        public void SetLastManualUnitId(StringName unitId) =>
            LastManualUnitId = unitId;

        public IReadOnlyList<Vector2I> GetTargetCoords() => TargetCoords;

        public void SetTargetCoords(IEnumerable<Vector2I> targetCoords)
        {
            TargetCoords.Clear();
            TargetCoords.AddRange(targetCoords ?? Array.Empty<Vector2I>());
        }

        public IReadOnlyList<StringName> GetTargetUnitIds() => TargetUnitIds;

        public void SetTargetUnitIds(IEnumerable<StringName> targetUnitIds)
        {
            TargetUnitIds.Clear();
            TargetUnitIds.AddRange(targetUnitIds ?? Array.Empty<StringName>());
        }

        public void SetBattleSelectedCoord(Vector2I coord) => SelectedCoord = coord;
    }
}
