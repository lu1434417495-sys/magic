using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_archer_long_draw_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "archer_long_draw";
    private static readonly StringName SkillId = "archer_long_draw";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestWeaponRangePolicySchema();
            TestAuthoredContract(skill);
            TestLevelCurveAndDescriptions(skill);
            TestBowGateRejectsBeforePayment(skill);
            TestMaxLevelAddsExactlyOneWeaponRange(skill);
            TestPreviewAndExecutionUseWeaponDice(skill);
            TestCompletionRevalidatesMaxLevelRange(skill);
            TestAiUsesCanonicalRangeAndEnumeratesTiers(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer long draw regression"));
    }

    private void TestWeaponRangePolicySchema()
    {
        foreach (
            CombatWeaponRangePolicy policy in new[]
            {
                CombatWeaponRangePolicy.CurrentWeapon,
                CombatWeaponRangePolicy.Configured,
                CombatWeaponRangePolicy.CurrentWeaponPlusConfigured,
            }
        )
        {
            StringName id = CombatWeaponRangePolicyRules.ToStringName(policy);
            _test.Eq(
                CombatWeaponRangePolicyRules.ToPolicy(id),
                policy,
                $"{id} weapon range policy should round-trip through the typed owner."
            );
        }
        _test.Eq(
            CombatWeaponRangePolicyRules.ToPolicy(""),
            CombatWeaponRangePolicy.CurrentWeapon,
            "Omitted weapon_range_policy should preserve current-weapon behavior."
        );

        using CombatSkillDef valid = new()
        {
            skill_id = "weapon_range_policy_valid_probe",
            range_value = 1,
            weapon_range_policy = "current_weapon_plus_configured",
        };
        _test.Eq(
            Validate(valid).Count,
            0,
            "The additive current-weapon range policy should pass content validation."
        );

        using CombatSkillDef invalid = new()
        {
            skill_id = "weapon_range_policy_invalid_probe",
            weapon_range_policy = "skill_id_specific_range",
        };
        string errors = string.Join(" | ", Validate(invalid));
        _test.True(
            errors.Contains("unsupported weapon_range_policy skill_id_specific_range"),
            $"Unknown range policies should fail schema validation. errors={errors}"
        );
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        CombatWindupDefinition windup = combat?.Windup;
        _test.True(skill != null && combat != null && windup != null, "满弦狙击应投影完整蓄力定义。");
        if (skill == null || combat == null || windup == null)
            return;

        _test.Eq(skill.MaxLevel, 5, "满弦狙击核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "满弦狙击非核心上限应为3级。");
        _test.Eq(skill.GrowthTier, new StringName("basic"), "满弦狙击应采用基础成长档。");
        _test.Eq(skill.MasteryCurve.Count, 5, "熟练度曲线应覆盖5级上限。");
        _test.Eq(ReadGrowth(skill, "agility"), 40, "核心成长应提供40敏捷进度。");
        _test.Eq(ReadGrowth(skill, "perception"), 20, "核心成长应提供20感知进度。");

        _test.Eq(combat.ApCost, 2, "满弦狙击应消耗2AP。");
        _test.True(combat.RequiresLos, "远程狙击应要求目标视线。");
        _test.Eq(
            combat.WeaponRangePolicy,
            new StringName("current_weapon_plus_configured"),
            "射程应使用当前武器加配置加值策略。"
        );
        _test.Eq(
            combat.WeaponRangePolicyKind,
            CombatWeaponRangePolicy.CurrentWeaponPlusConfigured,
            "immutable definition 应提供类型化射程策略。"
        );
        _test.Eq(combat.RangeValue, 0, "基础等级不得额外增加弓射程。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "只应声明一个武器家族门槛。");
        _test.Eq(combat.RequiredWeaponFamilies[0], new StringName("bow"), "必须装备弓。");
        _test.False(combat.AllowsNaturalWeapon, "天生武器不得替代弓。");
        _test.Eq(
            combat.MasteryTriggerModeKind,
            CombatSkillMasteryTriggerMode.WeaponAttackQuality,
            "熟练度应由标准武器攻击质量事实触发。"
        );
        _test.Eq(
            combat.MasteryAmountModeKind,
            CombatSkillMasteryAmountMode.PerTargetRank,
            "熟练度应按目标阶级结算。"
        );

        _test.Eq(combat.EffectDefinitions.Count, 1, "满弦狙击只应结算一次伤害效果。");
        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.Eq(damage.Power, 0, "物理技能不得保留固定16伤害。");
        _test.True(damage.AddWeaponDice, "伤害必须来自当前弓的武器骰。");
        _test.True(damage.RequiresWeapon, "伤害效果必须要求真实武器。");
        _test.True(damage.UseWeaponPhysicalDamageTag, "物理伤害类型必须来自当前弓。");
        _test.True(damage.ResolveAsWeaponAttack, "伤害必须进入标准武器攻击链。");

        _test.Eq(windup.StaminaCostPerTier, 8, "每挡应额外消耗8体力。");
        _test.Eq(windup.WeaponDicePerTier, 1, "每挡应增加1W。");
        _test.False(skill.Description.Contains("135%"), "旧移动后135%规则必须移除。");
        _test.True(
            skill.Description.Contains("5级") && skill.Description.Contains("增加1格"),
            "完整描述应公开满级射程奖励。"
        );
    }

    private void TestLevelCurveAndDescriptions(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        CombatWindupDefinition windup = combat?.Windup;
        if (combat == null || windup == null)
        {
            _test.Fail("等级曲线测试需要完整战斗与蓄力定义。");
            return;
        }

        int[] stamina = { 16, 14, 14, 12, 12, 10 };
        int[] cooldown = { 100, 100, 90, 90, 80, 80 };
        int[] attack = { 1, 1, 1, 2, 2, 3 };
        int[] rangeBonus = { 0, 0, 0, 0, 0, 1 };
        int[] tierCap = { 1, 1, 2, 2, 3, 3 };
        for (int level = 0; level <= 5; level++)
        {
            SkillEffectiveCombatDefinition effective =
                SkillEffectiveCombatDefinition.BuildUncached(skill, level);
            _test.Eq(effective.ResourceCosts.ApCost, 2, $"L{level}应保持2AP。");
            _test.Eq(
                effective.ResourceCosts.StaminaCost,
                stamina[level],
                $"L{level}基础体力不符。"
            );
            _test.Eq(
                effective.ResourceCosts.CooldownTu,
                cooldown[level],
                $"L{level}冷却不符。"
            );
            _test.Eq(effective.AttackRollBonus, attack[level], $"L{level}命中加值不符。");
            _test.Eq(effective.RangeValue, rangeBonus[level], $"L{level}射程加值不符。");
            _test.Eq(windup.GetSkillTierCap(level), tierCap[level], $"L{level}挡位上限不符。");
            _test.Eq(
                windup.GetBaseWeaponDiceMultiplier(level),
                1,
                $"L{level}基础伤害必须保持1W。"
            );
        }

        string levelFour = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            4,
            new GDictionary()
        );
        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new GDictionary()
        );
        _test.True(levelFour.Contains("射程随装备弓"), "4级描述应继续使用原弓射程。");
        _test.False(levelFour.Contains("+1格"), "4级不得提前获得满级射程奖励。");
        _test.True(levelFive.Contains("装备弓射程+1格"), "5级描述应明确额外1格射程。");
        _test.True(levelFive.Contains("攻击检定+3"), "5级描述应显示+3命中。");
    }

    private void TestBowGateRejectsBeforePayment(SkillDefinition skill)
    {
        foreach (WeaponKind weaponKind in new[] { WeaponKind.Sword, WeaponKind.Natural })
        {
            using Fixture fixture = BuildFixture(
                skill,
                level: 5,
                targetX: 1,
                weaponKind: weaponKind
            );
            int apBefore = fixture.Caster.GetCurrentAp();
            int staminaBefore = fixture.Caster.GetCurrentStamina();
            BattleCommand command = BuildCommand(fixture.Caster, fixture.Target, tier: 1);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.False(preview.allowed, $"{weaponKind}不得通过满弦狙击预览。");

            using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(fixture.Caster.GetCurrentAp(), apBefore, $"{weaponKind}拒绝不得扣AP。");
            _test.Eq(
                fixture.Caster.GetCurrentStamina(),
                staminaBefore,
                $"{weaponKind}拒绝不得扣体力。"
            );
            _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 0, $"{weaponKind}拒绝不得进入冷却。");
            _test.False(fixture.Caster.HasPendingCast(), $"{weaponKind}拒绝不得建立蓄力。");
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestMaxLevelAddsExactlyOneWeaponRange(SkillDefinition skill)
    {
        using Fixture levelFour = BuildFixture(skill, level: 4, targetX: 7);
        using Fixture levelFive = BuildFixture(skill, level: 5, targetX: 7);

        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(levelFour.Caster, skill),
            6,
            "4级有效射程应等于射程6的装备弓。"
        );
        _test.Eq(
            BattleRangeService.GetEffectiveSkillRange(levelFive.Caster, skill),
            7,
            "5级有效射程应为装备弓射程6加1。"
        );

        BattleCommand levelFourCommand = BuildCommand(levelFour.Caster, levelFour.Target, 1);
        BattleCommand levelFiveCommand = BuildCommand(levelFive.Caster, levelFive.Target, 1);
        BattlePreview levelFourPreview = levelFour.Runtime.PreviewCommand(levelFourCommand);
        BattlePreview levelFivePreview = levelFive.Runtime.PreviewCommand(levelFiveCommand);
        _test.False(levelFourPreview.allowed, "第7格目标在4级应超出射程。");
        _test.True(levelFivePreview.allowed, "第7格目标在5级应因+1射程变为合法。");

        using Fixture eighthCell = BuildFixture(skill, level: 5, targetX: 8);
        BattleCommand eighthCommand = BuildCommand(eighthCell.Caster, eighthCell.Target, 1);
        BattlePreview eighthPreview = eighthCell.Runtime.PreviewCommand(eighthCommand);
        _test.False(eighthPreview.allowed, "射程6弓的满级满弦狙击仍不得攻击第8格。");

        BattleTestFixture.DisposeBattlePreview(levelFourPreview);
        BattleTestFixture.DisposeBattlePreview(levelFivePreview);
        BattleTestFixture.DisposeBattlePreview(eighthPreview);
        BattleTestFixture.DisposeBattleCommand(levelFourCommand);
        BattleTestFixture.DisposeBattleCommand(levelFiveCommand);
        BattleTestFixture.DisposeBattleCommand(eighthCommand);
    }

    private void TestPreviewAndExecutionUseWeaponDice(SkillDefinition skill)
    {
        using Fixture fixture = BuildFixture(skill, level: 5, targetX: 7);
        BattleCommand tierThree = BuildCommand(fixture.Caster, fixture.Target, tier: 3);
        BattlePreview tierThreePreview = fixture.Runtime.PreviewCommand(tierThree);
        _test.True(tierThreePreview.allowed, "5级且体质自然上限足够时应允许3挡。");
        _test.Eq(
            tierThreePreview.DamagePreviewTyped?.DamageRanges[0].WeaponDiceRange.DiceCount
                ?? -1,
            4,
            "1D8弓的3挡预览应为4D8武器骰。"
        );
        _test.True(
            LogsContain(tierThreePreview.LogLinesTyped, "30 TU")
                && LogsContain(tierThreePreview.LogLinesTyped, "34 体力")
                && LogsContain(tierThreePreview.LogLinesTyped, "4W"),
            "3挡预览应显示canonical时间、总体力与武器骰倍率。"
        );

        BattleCommand tierFour = BuildCommand(fixture.Caster, fixture.Target, tier: 4);
        BattlePreview tierFourPreview = fixture.Runtime.PreviewCommand(tierFour);
        _test.False(tierFourPreview.allowed, "满级技能上限仍应拒绝4挡。");

        BattleCommand tierOne = BuildCommand(fixture.Caster, fixture.Target, tier: 1);
        int hpBefore = fixture.Target.GetCurrentHp();
        using BattleEventBatch startBatch = fixture.Runtime.IssueCommand(tierOne);
        _test.True(fixture.Caster.HasPendingCast(), "确认后应建立蓄力。");
        _test.Eq(fixture.Caster.GetCurrentAp(), 0, "支付2AP后剩余AP应清零并结束行动。");
        _test.Eq(fixture.Caster.GetCurrentStamina(), 82, "1挡应支付10+8共18体力。");
        _test.Eq(fixture.Target.GetCurrentHp(), hpBefore, "蓄力完成前不得提前造成伤害。");
        _test.Eq(
            fixture.Caster.pending_cast?.WindupSnapshot?.WeaponDiceMultiplier ?? -1,
            2,
            "1挡应冻结2W结算倍率。"
        );

        using BattleEventBatch completion = AdvanceTimelineTu(fixture, 10);
        _test.False(fixture.Caster.HasPendingCast(), "STR+3的1挡应在10TU完成。");
        _test.Eq(fixture.Target.GetCurrentHp(), hpBefore - 8, "两枚固定4点的1D8武器骰应造成8伤害。");
        _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 80, "完成后应进入5级80TU冷却。");

        BattleTestFixture.DisposeBattlePreview(tierThreePreview);
        BattleTestFixture.DisposeBattlePreview(tierFourPreview);
        BattleTestFixture.DisposeBattleCommand(tierThree);
        BattleTestFixture.DisposeBattleCommand(tierFour);
        BattleTestFixture.DisposeBattleCommand(tierOne);
    }

    private void TestCompletionRevalidatesMaxLevelRange(SkillDefinition skill)
    {
        using Fixture fixture = BuildFixture(skill, level: 5, targetX: 7);
        BattleCommand command = BuildCommand(fixture.Caster, fixture.Target, tier: 1);
        int hpBefore = fixture.Target.GetCurrentHp();
        using BattleEventBatch startBatch = fixture.Runtime.IssueCommand(command);
        _test.True(fixture.Caster.HasPendingCast(), "第7格目标应能开始满级蓄力。");
        _test.True(
            fixture.Runtime._grid_service.MoveUnit(
                fixture.State,
                fixture.Target,
                new Vector2I(8, 0)
            ),
            "测试目标应能移到第8格。"
        );
        using BattleEventBatch reconcileBatch = new();
        fixture.Runtime._casting_time_service.ReconcilePendingCasts(reconcileBatch);
        _test.True(fixture.Caster.HasPendingCast(), "目标移动不应提前打断蓄力。");

        using BattleEventBatch completion = AdvanceTimelineTu(fixture, 10);
        _test.False(fixture.Caster.HasPendingCast(), "完成复验后应清除蓄力。");
        _test.Eq(fixture.Target.GetCurrentHp(), hpBefore, "目标移到第8格后攻击应落空。");
        _test.Eq(fixture.Caster.GetCooldownTyped(SkillId), 80, "距离复验落空仍应进入完整冷却。");
        _test.True(LogsContain(completion.LogLinesTyped, "攻击落空"), "范围复验失败应留下落空日志。");
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiUsesCanonicalRangeAndEnumeratesTiers(SkillDefinition skill)
    {
        using Fixture levelFive = BuildFixture(skill, level: 5, targetX: 7);
        int scoreCalls = 0;
        BattleAiContext context = BuildAiContext(levelFive, skill);
        context.skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None;
        context.preview_command_callback = levelFive.Runtime.PreviewCommand;
        context.skill_score_input_callback = (
            _,
            _,
            command,
            preview,
            _,
            _,
            _
        ) =>
        {
            scoreCalls++;
            return BuildArtificialScoreInput(command, preview, command.windup_tier * 100);
        };

        int apBefore = levelFive.Caster.GetCurrentAp();
        int staminaBefore = levelFive.Caster.GetCurrentStamina();
        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildAiAction(),
            context
        );
        _test.Eq(decision?.command?.target_unit_id ?? new StringName(""), levelFive.Target.unit_id, "AI应识别第7格满级合法目标。");
        _test.Eq(decision?.command?.windup_tier ?? -1, 3, "AI应枚举3个合法挡位并选择测试评分最高的3挡。");
        _test.Eq(scoreCalls, 3, "三个合法挡位都应通过canonical preview后进入评分。");
        _test.Eq(levelFive.Caster.GetCurrentAp(), apBefore, "AI评估不得消耗AP。");
        _test.Eq(levelFive.Caster.GetCurrentStamina(), staminaBefore, "AI评估不得消耗体力。");
        _test.False(levelFive.Caster.HasPendingCast(), "AI评估不得建立真实蓄力。");

        using Fixture levelFour = BuildFixture(skill, level: 4, targetX: 7);
        BattleAiContext rejectedContext = BuildAiContext(levelFour, skill);
        rejectedContext.skill_cast_block_reason_callback = (_, _) =>
            BattleSkillCastBlockReasonKind.None;
        rejectedContext.preview_command_callback = levelFour.Runtime.PreviewCommand;
        rejectedContext.skill_score_input_callback = (
            _,
            _,
            command,
            preview,
            _,
            _,
            _
        ) => BuildArtificialScoreInput(command, preview, 100);
        BattleAiDecision rejected = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildAiAction(),
            rejectedContext
        );
        _test.True(rejected == null, "4级AI不得把第7格目标当作合法候选。");
    }

    private static GStringArray Validate(CombatSkillDef profile)
    {
        var errors = new GStringArray();
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        validator.AppendCombatProfileValidationErrors(errors, profile.skill_id, profile);
        return errors;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "archer_long_draw_regression"
        );

    private Fixture BuildFixture(
        SkillDefinition skill,
        int level,
        int targetX,
        WeaponKind weaponKind = WeaponKind.Bow
    )
    {
        BattleUnitState caster = BuildUnit("long_draw_caster", "player", Vector2I.Zero, hp: 100);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level, preserveZero: level == 0);
        caster.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        caster.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        caster.attribute_snapshot.SetValue("strength", 16);
        caster.attribute_snapshot.SetValue("strength_modifier", 3);
        caster.attribute_snapshot.SetValue("constitution", 22);
        caster.attribute_snapshot.SetValue("constitution_modifier", 6);
        ApplyWeapon(caster, weaponKind);

        BattleUnitState target = BuildUnit(
            "long_draw_target",
            "enemy",
            new Vector2I(targetX, 0),
            hp: 100
        );
        target.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);

        BattleTestFixture battle = BattleTestFixture.CreateFlatBattle(
            $"archer_long_draw_l{level}_{targetX}_{weaponKind}",
            new Vector2I(9, 1),
            new[] { caster },
            new[] { target }
        );
        battle.State.timeline.tu_per_tick = 5;
        battle.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        BattleTestFixture.ConfigureDamageResolverForTests(
            battle.Runtime,
            new FixedRollDamageResolver(
                new GArray { 4, 4, 4, 4, 4, 4 },
                new GArray { 15, 15, 15 }
            )
        );
        BattleTestFixture.ConfigureHitResolverForTests(
            battle.Runtime,
            new FixedHitResolver(15)
        );
        battle.Runtime.SetupStateForTests(battle.State);
        return new Fixture(battle, caster, target);
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord,
        int hp
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = id,
            source_member_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        }.WithCombatResourcesForTest(
            hp: hp,
            stamina: 100,
            ap: 3,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetAnchorCoord(coord);
        unit.SetActionThresholdTyped(1_000_000);
        return unit;
    }

    private static void ApplyWeapon(BattleUnitState unit, WeaponKind weaponKind)
    {
        if (weaponKind == WeaponKind.Natural)
        {
            unit.SetNaturalWeaponProjectionTyped(
                "long_draw_natural",
                "physical_pierce",
                6,
                new WeaponDice { dice_count = 1, dice_sides = 8 }
            );
            return;
        }

        bool bow = weaponKind == WeaponKind.Bow;
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = bow ? "long_draw_test_bow" : "long_draw_test_sword",
                weapon_instance_id = bow ? "long_draw_bow_instance" : "long_draw_sword_instance",
                weapon_profile_type_id = bow ? "longbow" : "longsword",
                weapon_range_type = bow ? "ranged" : "melee",
                weapon_family = bow ? "bow" : "sword",
                weapon_current_grip = "two_handed",
                weapon_attack_range = bow ? 6 : 1,
                weapon_one_handed_dice = new WeaponDice(),
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = bow ? "physical_pierce" : "physical_slash",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target,
        int tier
    )
    {
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
            windup_tier = tier,
        };
        command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private static BattleEventBatch AdvanceTimelineTu(Fixture fixture, int totalTu)
    {
        fixture.State.phase = "timeline_running";
        fixture.State.active_unit_id = "";
        fixture.State.timeline.ready_unit_ids.Clear();
        return fixture.Runtime.advance(totalTu / 5);
    }

    private static BattleAiContext BuildAiContext(Fixture fixture, SkillDefinition skill)
    {
        BattleAiContext context = new()
        {
            state = fixture.State,
            unit_state = fixture.Caster,
            grid_service = fixture.Runtime.GetGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        return context;
    }

    private static UseUnitSkillActionDefinition BuildAiAction() =>
        new(
            "long_draw_player_auto_ai",
            "test",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            1,
            EnemyAiDistanceReferences.ToStringName(
                EnemyAiDistanceReference.TargetUnit
            )
        );

    private static BattleAiScoreInput BuildArtificialScoreInput(
        BattleCommand command,
        BattlePreview preview,
        int totalScore
    ) =>
        new()
        {
            command = command,
            preview = preview,
            effective_target_count = 1,
            enemy_target_count = 1,
            total_score = totalScore,
        };

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill?.AttributeGrowthProgress != null
        && skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value)
            ? value
            : 0;

    private static bool LogsContain(IEnumerable<string> lines, string needle)
    {
        foreach (string line in lines ?? Array.Empty<string>())
        {
            if (line?.Contains(needle, StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }

    private enum WeaponKind
    {
        Bow,
        Sword,
        Natural,
    }

    private sealed class Fixture : IDisposable
    {
        private readonly BattleTestFixture _battle;

        internal Fixture(
            BattleTestFixture battle,
            BattleUnitState caster,
            BattleUnitState target
        )
        {
            _battle = battle;
            Caster = caster;
            Target = target;
        }

        internal BattleRuntimeModule Runtime => _battle.Runtime;
        internal BattleState State => _battle.State;
        internal BattleUnitState Caster { get; }
        internal BattleUnitState Target { get; }

        public void Dispose() => _battle.Dispose();
    }
}
