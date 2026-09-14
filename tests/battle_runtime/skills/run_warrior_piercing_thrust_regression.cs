using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_piercing_thrust_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "warrior_piercing_thrust";
    private static readonly StringName SkillId = "warrior_piercing_thrust";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestLevelCurveAndDescriptions(skill);
            TestWeaponTypeGate(skill);
            TestSchemaRejectsInvalidProfiles();
            TestCanonicalPathLegality(skill);
            TestEveryPathCellMustMatchOriginHeight(skill);
            TestNormalMovementLockDoesNotReject(skill);
            TestExecutionUsesStandardWeaponAttackAndNoMovePoints(skill);
            TestTerrainInterruptionCancelsAttackWithoutRefund(skill);
            TestMissStillLeavesSourceAdvanced(skill);
            TestAiUsesCanonicalAdvancePreview(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior piercing thrust regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "踏步穿刺正式资源与 combat_profile 应可加载。");
        if (combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "技能ID必须保持 warrior_piercing_thrust。" );
        _test.Eq(skill.DisplayName, "踏步穿刺", "显示名应体现推进攻击身份。" );
        _test.Eq(skill.MaxLevel, 5, "核心等级上限应为5。" );
        _test.Eq(skill.NonCoreMaxLevel, 3, "非核心等级上限应为3。" );
        _test.Eq(skill.GrowthTier, new StringName("intermediate"), "成长档应保持 intermediate。" );
        _test.Eq(ReadGrowth(skill, "strength"), 60, "力量成长进度应保持60。" );
        _test.Eq(ReadGrowth(skill, "perception"), 60, "感知成长进度应保持60。" );
        _test.True(skill.Description.Contains("标准武器攻击"), "描述必须明确标准武器攻击链。" );
        _test.True(skill.Description.Contains("同一高度"), "描述必须公开绝对同高限制。" );
        _test.True(skill.Description.Contains("不消耗移动力"), "描述必须公开推进不耗移动力。" );
        _test.True(skill.Description.Contains("不返还"), "描述必须公开中断不退款。" );

        _test.Eq(combat.TargetMode, new StringName("unit"), "技能必须选择单位目标。" );
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "技能只能选择敌方。" );
        _test.Eq(combat.MinTargetCount, 1, "技能必须选择恰好一个目标。" );
        _test.Eq(combat.MaxTargetCount, 1, "技能不得选择多个目标。" );
        _test.Eq(combat.MaxHitsPerTarget, 1, "技能只能进行一次攻击。" );
        _test.True(combat.RequiresLos, "技能必须要求视线。" );
        _test.Eq(combat.RangeValue, 1, "0级最大推进距离应为1格。" );
        _test.Eq(
            combat.WeaponRangePolicy,
            new StringName("current_weapon_plus_configured"),
            "目标枚举射程应为当前武器射程加推进距离。"
        );
        _test.True(combat.ApproachAttack != null, "资源必须投影 typed approach profile。" );
        _test.Eq(
            combat.ApproachAttack?.MaximumPathHeightDeltaFromOrigin ?? -1,
            0,
            "路径格与起始格允许的绝对高度差必须为0。"
        );
        _test.Eq(combat.RequiredWeaponTypeIds.Count, 3, "只允许三种已批准的武器类型。" );
        _test.True(combat.RequiredWeaponTypeIds.Contains(new StringName("spear")), "应允许长矛。" );
        _test.True(combat.RequiredWeaponTypeIds.Contains(new StringName("trident")), "应允许三叉戟。" );
        _test.True(combat.RequiredWeaponTypeIds.Contains(new StringName("pike")), "应允许长枪。" );
        _test.False(combat.AllowsNaturalWeapon, "天生武器不得绕过装备门禁。" );

        _test.Eq(combat.EffectDefinitions.Count, 1, "技能只能声明一次伤害效果。" );
        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.Eq(damage.EffectKind, BattleEffectKind.Damage, "唯一效果必须是伤害。" );
        _test.Eq(damage.Power, 0, "物理技能不得附带固定伤害。" );
        _test.Eq(damage.DiceCount, 0, "物理技能不得附带额外固定骰。" );
        _test.True(damage.AddWeaponDice, "伤害必须来自当前武器骰。" );
        _test.True(damage.RequiresWeapon, "伤害必须要求真实武器。" );
        _test.True(damage.UseWeaponPhysicalDamageTag, "伤害类型必须来自武器。" );
        _test.True(damage.ResolveAsWeaponAttack, "伤害必须进入标准武器攻击链。" );
    }

    private void TestLevelCurveAndDescriptions(SkillDefinition skill)
    {
        AssertLevel(skill, 0, 1, 24, 80, -1);
        AssertLevel(skill, 1, 1, 24, 80, 0);
        AssertLevel(skill, 2, 1, 22, 80, 0);
        AssertLevel(skill, 3, 2, 22, 80, 0);
        AssertLevel(skill, 4, 2, 22, 70, 0);
        AssertLevel(skill, 5, 2, 20, 70, 1);

        string levelZero = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            0,
            new GDictionary()
        );
        string levelFive = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            5,
            new GDictionary()
        );
        _test.True(levelZero.Contains("推进最多1格"), "0级文本应显示1格推进。" );
        _test.True(levelZero.Contains("攻击检定-1"), "0级文本应显示命中代价。" );
        _test.True(levelFive.Contains("推进最多2格"), "5级文本应显示2格推进。" );
        _test.True(levelFive.Contains("攻击检定+1"), "5级文本应显示命中成长。" );
        _test.True(levelFive.Contains("20体力"), "5级文本应显示20体力。" );
    }

    private void TestWeaponTypeGate(SkillDefinition skill)
    {
        BattleUnitState spear = BuildReadyCaster("spear_gate", Vector2I.Zero, "spear", 1);
        BattleUnitState trident = BuildReadyCaster("trident_gate", Vector2I.Zero, "trident", 1);
        BattleUnitState pike = BuildReadyCaster("pike_gate", Vector2I.Zero, "pike", 2);
        BattleUnitState javelin = BuildReadyCaster("javelin_gate", Vector2I.Zero, "javelin", 1);
        BattleUnitState naturalSpear = BuildReadyCaster(
            "natural_gate",
            Vector2I.Zero,
            "spear",
            1,
            "natural"
        );
        try
        {
            _test.True(BattleRangeService.UnitMatchesRequiredWeaponTypeIds(spear, skill), "长矛应通过门禁。" );
            _test.True(BattleRangeService.UnitMatchesRequiredWeaponTypeIds(trident, skill), "三叉戟应通过门禁。" );
            _test.True(BattleRangeService.UnitMatchesRequiredWeaponTypeIds(pike, skill), "长枪应通过门禁。" );
            _test.False(BattleRangeService.UnitMatchesRequiredWeaponTypeIds(javelin, skill), "标枪不得因同属 spear 家族而通过。" );
            _test.False(BattleRangeService.UnitMatchesRequiredWeaponTypeIds(naturalSpear, skill), "天生长矛不得绕过装备要求。" );
            _test.Eq(BattleRangeService.GetEffectiveSkillRange(spear, skill), 2, "长矛0级威胁射程应为1武器+1推进。" );
            _test.Eq(BattleRangeService.GetEffectiveSkillRange(pike, skill), 3, "长枪0级威胁射程应为2武器+1推进。" );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(spear);
            BattleTestFixture.DisposeBattleUnit(trident);
            BattleTestFixture.DisposeBattleUnit(pike);
            BattleTestFixture.DisposeBattleUnit(javelin);
            BattleTestFixture.DisposeBattleUnit(naturalSpear);
        }
    }

    private void TestSchemaRejectsInvalidProfiles()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using CombatSkillDef invalidHeight = BuildApproachProfile();
        using var invalidHeightDef = new CombatApproachAttackDef
        {
            maximum_path_height_delta_from_origin = -1,
        };
        invalidHeight.approach_attack_profile = invalidHeightDef;
        var heightErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            heightErrors,
            "invalid_approach_height",
            invalidHeight
        );
        _test.True(
            ErrorsContain(heightErrors, "maximum_path_height_delta_from_origin must be >= 0"),
            $"负绝对高度差必须被schema拒绝。errors={string.Join(" | ", heightErrors)}"
        );

        using CombatSkillDef fixedDamage = BuildApproachProfile();
        fixedDamage.effect_defs[0].power = 1;
        var damageErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            damageErrors,
            "invalid_approach_fixed_damage",
            fixedDamage
        );
        _test.True(
            ErrorsContain(damageErrors, "ordinary current-weapon attack"),
            $"固定伤害必须被schema拒绝。errors={string.Join(" | ", damageErrors)}"
        );
    }

    private void TestCanonicalPathLegality(SkillDefinition skill)
    {
        using (BattleTestFixture fixture = CreateFixture(
            skill,
            BuildReadyCaster("diagonal_caster", new Vector2I(1, 1)),
            BuildUnit("diagonal_target", "enemy", new Vector2I(2, 2))
        ))
        {
            AssertRejectedWithoutCost(fixture, BuildCommand(fixture.Allies[0], fixture.Enemies[0]), "斜向目标");
        }
        using (BattleTestFixture fixture = CreateFixture(
            skill,
            BuildReadyCaster("in_range_caster", new Vector2I(1, 1)),
            BuildUnit("in_range_target", "enemy", new Vector2I(2, 1))
        ))
        {
            AssertRejectedWithoutCost(fixture, BuildCommand(fixture.Allies[0], fixture.Enemies[0]), "已在武器射程内");
        }
        using (BattleTestFixture fixture = CreateFixture(
            skill,
            BuildReadyCaster("blocked_caster", new Vector2I(1, 1)),
            BuildUnit("blocked_target", "enemy", new Vector2I(3, 1)),
            BuildUnit("path_blocker", "enemy", new Vector2I(2, 1))
        ))
        {
            AssertRejectedWithoutCost(fixture, BuildCommand(fixture.Allies[0], fixture.Enemies[0]), "路径有单位阻挡");
        }
    }

    private void TestEveryPathCellMustMatchOriginHeight(SkillDefinition skill)
    {
        BattleUnitState intermediateCaster = BuildReadyCaster(
            "intermediate_height_caster",
            new Vector2I(1, 1)
        );
        intermediateCaster.SetKnownSkillLevelTyped(SkillId, 3);
        using (BattleTestFixture fixture = CreateFixture(
            skill,
            intermediateCaster,
            BuildUnit("intermediate_height_target", "enemy", new Vector2I(4, 1))
        ))
        {
            fixture.State.GetCell(new Vector2I(2, 1)).current_height = 1;
            AssertRejectedWithoutCost(
                fixture,
                BuildCommand(fixture.Allies[0], fixture.Enemies[0]),
                "中间推进格高于起始格"
            );
        }

        BattleUnitState landingCaster = BuildReadyCaster(
            "landing_height_caster",
            new Vector2I(1, 1)
        );
        landingCaster.SetKnownSkillLevelTyped(SkillId, 3);
        using (BattleTestFixture fixture = CreateFixture(
            skill,
            landingCaster,
            BuildUnit("landing_height_target", "enemy", new Vector2I(4, 1))
        ))
        {
            fixture.State.GetCell(new Vector2I(3, 1)).current_height = -1;
            AssertRejectedWithoutCost(
                fixture,
                BuildCommand(fixture.Allies[0], fixture.Enemies[0]),
                "最终落脚格低于起始格"
            );
        }
    }

    private void TestExecutionUsesStandardWeaponAttackAndNoMovePoints(
        SkillDefinition skill
    )
    {
        BattleUnitState caster = BuildReadyCaster(
            "execution_caster",
            new Vector2I(1, 1)
        );
        caster.SetCurrentMovePoints(0);
        BattleUnitState target = BuildUnit(
            "execution_target",
            "enemy",
            new Vector2I(3, 1)
        );
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        ConfigureHit(fixture);
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "移动力为0时canonical预览仍应允许技能。" );
        _test.Eq(preview.SourceAdvancePathTyped.Count, 2, "预览路径应包含起点和一个推进落点。" );
        _test.Eq(preview.resolved_anchor_coord, new Vector2I(2, 1), "预览应公开最终落点。" );
        _test.Eq(preview.move_cost, 0, "预览应公开0移动力消耗。" );

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(2, 1), "正式执行应推进最少1格。" );
        _test.Eq(caster.GetCurrentMovePoints(), 0, "推进不得生成或消耗普通移动力。" );
        _test.True(caster.IsNormalMovementLockedThisTurnTyped(), "技能行动后普通移动应按行动规则锁定。" );
        _test.Eq(caster.GetCurrentAp(), 1, "技能应消耗1 AP。" );
        _test.Eq(caster.GetCurrentStamina(), 76, "1级技能应消耗24体力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 80, "1级技能应进入80 TU冷却。" );
        _test.True(target.GetCurrentHp() < hpBefore, "标准武器攻击命中后应造成武器骰伤害。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestNormalMovementLockDoesNotReject(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster(
            "normal_lock_caster",
            new Vector2I(1, 1)
        );
        caster.CommitActionTakenThisTurnTyped();
        caster.SetCurrentMovePoints(0);
        BattleUnitState target = BuildUnit(
            "normal_lock_target",
            "enemy",
            new Vector2I(3, 1)
        );
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            "普通移动已经因行动锁定且移动力为0时，踏步攻击仍应允许。"
        );
        _test.Eq(
            preview?.resolved_anchor_coord ?? new Vector2I(-1, -1),
            new Vector2I(2, 1),
            "普通移动锁定不得改变canonical推进落点。"
        );
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestMissStillLeavesSourceAdvanced(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("miss_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("miss_target", "enemy", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, target);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(target.GetCurrentHp(), hpBefore, "未命中不得造成伤害。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(2, 1), "未命中仍应停在推进后的格子。" );
        _test.Eq(caster.GetCurrentStamina(), 76, "未命中仍应支付体力。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestTerrainInterruptionCancelsAttackWithoutRefund(
        SkillDefinition skill
    )
    {
        BattleUnitState caster = BuildReadyCaster(
            "terrain_interrupt_caster",
            new Vector2I(1, 1)
        );
        BattleUnitState target = BuildUnit(
            "terrain_interrupt_target",
            "enemy",
            new Vector2I(3, 1)
        );
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.State.GetCell(new Vector2I(2, 1)).timed_terrain_effects.Add(
            new BattleTerrainEffectState
            {
                field_instance_id = "approach_interrupt_field",
                effect_id = "approach_interrupt_tripwire",
                effect_type = "terrain_effect",
                display_name = "测试绊索",
                source_unit_id = target.unit_id,
                source_skill_id = "terrain_interrupt_probe",
                target_team_filter = "enemy",
                terrain_contact_mode = "interrupt_movement_on_failed_save",
                terrain_remaining_effective_triggers = 1,
                terrain_requires_ground_contact = true,
                contact_save_dc = 99,
                contact_save_ability = "dexterity",
                contact_save_tag = "reflex",
                remaining_tu = 20,
                tick_interval_tu = 5,
                stack_behavior = "refresh",
            }
        );
        fixture.Runtime._terrain_effect_system
            .ConfigureMovementContactSaveRollOverridesForTests(new[] { 1 });
        ConfigureHit(fixture);
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "付费前静态路径合法时应允许预览。" );

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(2, 1), "地形接触应在进入落脚格后拦停。" );
        _test.Eq(target.GetCurrentHp(), hpBefore, "推进被动态地形拦停后不得执行攻击。" );
        _test.Eq(caster.GetCurrentAp(), 1, "动态中断仍应支付1 AP。" );
        _test.Eq(caster.GetCurrentStamina(), 76, "动态中断仍应支付24体力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 80, "动态中断仍应进入完整冷却。" );
        _test.True(
            batch?.LogLinesTyped.Any(line => line.Contains("后续攻击取消")) == true,
            "动态中断日志应明确后续攻击取消。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiUsesCanonicalAdvancePreview(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("ai_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("ai_target", "enemy", new Vector2I(3, 1));
        using (BattleTestFixture fixture = CreateFixture(skill, caster, target))
        {
            int scoredCandidates = 0;
            BattleAiContext context = BuildAiContext(
                fixture,
                caster,
                skill,
                (_, preview) =>
                {
                    scoredCandidates++;
                    _test.Eq(preview.resolved_anchor_coord, new Vector2I(2, 1), "AI评分必须使用推进后的最终落点。" );
                    _test.Eq(preview.SourceAdvancePathTyped.Count, 2, "AI评分必须携带canonical推进路径。" );
                }
            );
            BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
                BuildAiAction(),
                context
            );
            _test.Eq(scoredCandidates, 1, "同一合法目标应形成一个可评分候选。" );
            _test.True(decision?.command != null, "合法同高路径应产生AI技能决策。" );
            _test.Eq(
                decision?.score_input?.preview?.resolved_anchor_coord
                    ?? new Vector2I(-1, -1),
                new Vector2I(2, 1),
                "AI最终决策应保留canonical落点。"
            );
        }

        BattleUnitState blockedCaster = BuildReadyCaster(
            "ai_height_caster",
            new Vector2I(1, 1)
        );
        BattleUnitState blockedTarget = BuildUnit(
            "ai_height_target",
            "enemy",
            new Vector2I(3, 1)
        );
        using (BattleTestFixture fixture = CreateFixture(skill, blockedCaster, blockedTarget))
        {
            fixture.State.GetCell(new Vector2I(2, 1)).current_height = 1;
            int scoredCandidates = 0;
            BattleAiContext context = BuildAiContext(
                fixture,
                blockedCaster,
                skill,
                (_, _) => scoredCandidates++
            );
            BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
                BuildAiAction(),
                context
            );
            _test.Eq(scoredCandidates, 0, "异高路径必须在canonical预览阶段从AI候选中剔除。" );
            _test.True(decision == null, "没有其他候选时AI不得选择异高踏步攻击。" );
        }
    }

    private BattleAiContext BuildAiContext(
        BattleTestFixture fixture,
        BattleUnitState caster,
        SkillDefinition skill,
        Action<BattleCommand, BattlePreview> onScore
    )
    {
        BattleAiContext context = new()
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
            trace_enabled = true,
            skill_cast_block_reason_callback = (_, _) =>
                BattleSkillCastBlockReasonKind.None,
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
                onScore?.Invoke(command, preview);
                return new BattleAiScoreInput
                {
                    command = command,
                    preview = preview,
                    effective_target_count = 1,
                    enemy_target_count = 1,
                    total_score = 100,
                };
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        return context;
    }

    private static UseUnitSkillActionDefinition BuildAiAction() =>
        new(
            "piercing_thrust_ai",
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

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int advance,
        int stamina,
        int cooldownTu,
        int attackBonus
    )
    {
        CombatSkillResourceCosts costs =
            skill.CombatProfile.GetEffectiveResourceCostValues(level);
        _test.Eq(skill.CombatProfile.GetEffectiveRangeValue(level), advance, $"{level}级推进距离应正确。" );
        _test.Eq(costs.ApCost, 1, $"{level}级应消耗1 AP。" );
        _test.Eq(costs.StaminaCost, stamina, $"{level}级体力消耗应正确。" );
        _test.Eq(costs.CooldownTu, cooldownTu, $"{level}级冷却应正确。" );
        _test.Eq(skill.CombatProfile.GetEffectiveAttackRollBonus(level), attackBonus, $"{level}级攻击检定应正确。" );
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
        _test.True(preview != null && !preview.allowed, $"{label}时预览必须拒绝。" );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), apBefore, $"{label}不得消耗AP。" );
        _test.Eq(caster.GetCurrentStamina(), staminaBefore, $"{label}不得消耗体力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{label}不得启动冷却。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static CombatSkillDef BuildApproachProfile()
    {
        var damage = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectDamage,
            add_weapon_dice = true,
            requires_weapon = true,
            use_weapon_physical_damage_tag = true,
            resolve_as_weapon_attack = true,
        };
        var profile = new CombatSkillDef
        {
            skill_id = "approach_schema_probe",
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            min_target_count = 1,
            max_target_count = 1,
            max_hits_per_target = 1,
            range_value = 1,
            weapon_range_policy = "current_weapon_plus_configured",
            requires_los = true,
            allows_natural_weapon = false,
            approach_attack_profile = new CombatApproachAttackDef(),
        };
        profile.required_weapon_type_ids.Add("spear");
        profile.effect_defs.Add(damage);
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

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value)
            ? value
            : 0;

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "warrior_piercing_thrust_regression"
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
            "warrior_piercing_thrust",
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
        fixture.Runtime.ConfigureDamageResolverForTests(
            new FixedHitMaxDamageResolver()
        );
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
    }

    private static BattleUnitState BuildReadyCaster(
        StringName id,
        Vector2I coord,
        StringName weaponType = default,
        int attackRange = 1,
        StringName profileKind = default
    )
    {
        if (weaponType == default)
            weaponType = "spear";
        if (profileKind == default)
            profileKind = "equipped";
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 1);
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        ApplyWeapon(caster, weaponType, attackRange, profileKind);
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
        StringName weaponType,
        int attackRange,
        StringName profileKind
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = profileKind,
                weapon_item_id = profileKind == "equipped" ? $"test_{weaponType}" : "",
                weapon_profile_type_id = weaponType,
                weapon_range_type = "melee",
                weapon_family = weaponType == "pike" ? "polearm" : "spear",
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
                    dice_sides = weaponType == "pike" ? 10 : 8,
                },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
}
