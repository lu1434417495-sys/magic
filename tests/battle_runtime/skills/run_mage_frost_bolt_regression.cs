using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_mage_frost_bolt_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "res://data/configs/skills/mage_frost_bolt.tres";
    private static readonly StringName SkillId = "mage_frost_bolt";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestLevelCurveAndDescriptions(skill);
            TestHitAppliesDamageSlowAndRetreat(skill);
            TestMissStillRetreatsWithoutDamageOrSlow(skill);
            TestBlockedRetreatStillResolvesAttack(skill);
            TestLosAndMovementLockRejectBeforeCost(skill);
            TestAiEnumeratesCanonicalRetreatDirections(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage frost bolt regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "霜击术正式资源与 combat_profile 应可加载。" );
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "霜击术 skill_id 应稳定。" );
        _test.Eq(skill.DisplayName, "霜击术", "霜击术显示名应稳定。" );
        _test.Eq(skill.MaxLevel, 7, "霜击术等级上限应为7。" );
        _test.Eq(skill.NonCoreMaxLevel, 5, "霜击术非核心等级上限应为5。" );
        _test.Eq(skill.MasteryCurve.Count, 7, "熟练度曲线应覆盖1至7级。" );
        _test.Eq(skill.MasteryCurve[0], 400, "1级熟练度阈值应保持400。" );
        _test.Eq(skill.MasteryCurve[6], 13000, "7级熟练度阈值应保持13000。" );
        _test.Eq(skill.GrowthTier, new StringName("advanced"), "霜击术应属于 advanced 成长档。" );
        _test.Eq(skill.LearnSourceKind, SkillLearnSourceKind.Book, "霜击术应继续通过书籍学习。" );
        _test.Eq(ReadGrowth(skill, "intelligence"), 110, "霜击术应保留110点智力成长进度。" );
        _test.Eq(ReadGrowth(skill, "perception"), 20, "霜击术应保留20点感知成长进度。" );
        _test.Eq(ReadGrowth(skill, "willpower"), 50, "霜击术应保留50点意志成长进度。" );
        _test.True(skill.Description.Contains("接触AC"), "描述应公开接触AC攻击。" );
        _test.True(skill.Description.Contains("移动耗费+1"), "描述应公开缓速的具体效果。" );
        _test.True(skill.Description.Contains("只刷新持续时间，不叠加"), "描述应公开缓速刷新规则。" );
        _test.True(skill.Description.Contains("无论攻击命中、落空或击倒目标"), "描述应公开后撤触发时机。" );
        _test.True(skill.Description.Contains("不消耗移动力"), "描述应公开免费后撤。" );

        _test.Eq(combat.TargetModeKind, BattleTargetMode.Unit, "霜击术必须选择单位目标。" );
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Enemy, "霜击术只能选择敌方。" );
        _test.Eq(
            combat.TargetSelectionModeKind,
            BattleTargetSelectionMode.SingleUnit,
            "霜击术必须选择单个敌人。"
        );
        _test.Eq(combat.MinTargetCount, 1, "霜击术必须恰好选择一个目标。" );
        _test.Eq(combat.MaxTargetCount, 1, "霜击术不得选择多个目标。" );
        _test.True(combat.RequiresLos, "霜击术必须要求视线。" );
        _test.Eq(combat.ProjectileKind, new StringName("magical"), "霜击术应使用魔法投射物。" );
        _test.Eq(combat.AttackDefenseMode, new StringName("touch"), "霜击术必须攻击接触AC。" );
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "霜击术不得要求特定武器。" );
        _test.True(
            HasStringName(combat.DeliveryCategories, "spell"),
            "霜击术应投影 spell delivery category。"
        );
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect?.EffectKind == BattleEffectKind.Damage)
            {
                _test.False(effect.RequiresWeapon, "霜击术伤害不得要求武器。" );
                _test.False(effect.AddWeaponDice, "霜击术不得混入武器骰。" );
                _test.Eq(effect.DamageTag, new StringName("freeze"), "霜击术应使用冰冻伤害标签。" );
            }
        }
        CombatEffectDefinition retreat = FindEffect(
            combat.EffectDefinitions,
            BattleEffectKind.SourceRetreat
        );
        _test.Eq(retreat?.SourceRetreatDistance ?? -1, 1, "后撤距离必须来自 typed source_retreat 字段。" );
    }

    private void TestLevelCurveAndDescriptions(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            _test.Fail("霜击术等级曲线需要有效 combat_profile。" );
            return;
        }
        int[] expectedRange = { 4, 4, 4, 4, 5, 5, 5, 5 };
        int[] expectedAttackBonus = { 0, 1, 1, 1, 1, 1, 1, 1 };
        int[] expectedMp = { 25, 25, 20, 20, 20, 20, 20, 20 };
        int[] expectedCooldown = { 180, 180, 180, 180, 180, 160, 160, 160 };
        int[] expectedDice = { 1, 1, 1, 2, 2, 2, 2, 3 };
        int[] expectedSlowDuration = { 60, 60, 60, 60, 60, 60, 80, 80 };
        for (int level = 0; level <= 7; level += 1)
        {
            CombatSkillResourceCosts costs =
                skill.CombatProfile.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 1, $"霜击术L{level}应消耗1 AP。" );
            _test.Eq(costs.MpCost, expectedMp[level], $"霜击术L{level}法力消耗不符。" );
            _test.Eq(costs.StaminaCost, 0, $"霜击术L{level}不得消耗体力。" );
            _test.Eq(costs.CooldownTu, expectedCooldown[level], $"霜击术L{level}冷却不符。" );
            _test.Eq(
                skill.CombatProfile.GetEffectiveRangeValue(level),
                expectedRange[level],
                $"霜击术L{level}射程不符。"
            );
            _test.Eq(
                skill.CombatProfile.GetEffectiveAttackRollBonus(level),
                expectedAttackBonus[level],
                $"霜击术L{level}攻击检定加值不符。"
            );

            IReadOnlyList<CombatEffectDefinition> activeEffects = ActiveEffectsAtLevel(
                skill.CombatProfile.EffectDefinitions,
                level
            );
            CombatEffectDefinition damage = FindEffect(activeEffects, BattleEffectKind.Damage);
            CombatEffectDefinition slow = FindStatusEffect(activeEffects, "slow");
            CombatEffectDefinition retreat = FindEffect(
                activeEffects,
                BattleEffectKind.SourceRetreat
            );
            _test.Eq(damage?.DiceCount ?? -1, expectedDice[level], $"霜击术L{level}伤害骰数不符。" );
            _test.Eq(damage?.DiceSides ?? -1, 6, $"霜击术L{level}必须使用D6。" );
            _test.Eq(slow?.Power ?? -1, 1, $"霜击术L{level}缓速强度应为1。" );
            _test.Eq(
                slow?.DurationTu ?? -1,
                expectedSlowDuration[level],
                $"霜击术L{level}缓速持续时间不符。"
            );
            _test.Eq(retreat?.SourceRetreatDistance ?? -1, 1, $"霜击术L{level}都应后撤最多1格。" );
        }

        string levelZero = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            0,
            new GDictionary()
        );
        string levelSeven = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            7,
            new GDictionary()
        );
        _test.False(levelZero.Contains("攻击检定+0"), "0级描述不得显示无意义的攻击检定+0。" );
        _test.True(levelZero.Contains("持续60TU"), "0级描述应显示60TU缓速。" );
        _test.True(levelSeven.Contains("3D6"), "7级描述应显示3D6伤害。" );
        _test.True(levelSeven.Contains("持续80TU"), "7级描述应显示80TU缓速。" );
        _test.True(levelSeven.Contains("冷却160TU"), "7级描述应显示160TU冷却。" );
    }

    private void TestHitAppliesDamageSlowAndRetreat(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("frost_hit_caster", new Vector2I(2, 2), 3);
        BattleUnitState target = BuildUnit("frost_hit_target", "enemy", new Vector2I(3, 2));
        caster.SetCurrentMovePoints(0);
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        ConfigureHit(fixture);

        BattleCommand command = BuildCommand(caster, target, Vector2I.Left);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "合法目标与后撤方向应通过 canonical preview。" );
        _test.Eq(preview?.SourceRetreatPathTyped.Count ?? -1, 2, "1格后撤预览应包含起点与终点。" );
        _test.Eq(
            preview?.resolved_anchor_coord ?? new Vector2I(-1, -1),
            new Vector2I(1, 2),
            "预览应公开后撤后的最终落点。"
        );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(2, 2), "预览不得修改施法者坐标。" );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        BattleStatusEffectState slow = target.GetStatusEffect("slow");
        _test.Eq(target.GetCurrentHp(), 88, "L3命中应按2D6最大骰造成12点冰冻伤害。" );
        _test.True(slow != null, "命中应施加缓速。" );
        _test.Eq(slow?.duration ?? -1, 60, "L3命中应施加60TU缓速。" );
        _test.Eq(slow?.power ?? -1, 1, "缓速强度应为1。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 2), "命中后应沿选定方向后撤1格。" );
        _test.Eq(caster.GetCurrentMovePoints(), 0, "后撤不得要求或消耗移动力。" );
        _test.Eq(caster.GetCurrentAp(), 1, "施放应消耗1 AP。" );
        _test.Eq(caster.GetCurrentMp(), 80, "L3施放应消耗20 MP。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 180, "L3施放应进入180TU冷却。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestMissStillRetreatsWithoutDamageOrSlow(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("frost_miss_caster", new Vector2I(2, 2), 3);
        BattleUnitState target = BuildUnit("frost_miss_target", "enemy", new Vector2I(3, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());

        BattleCommand command = BuildCommand(caster, target, Vector2I.Left);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(target.GetCurrentHp(), 100, "攻击落空不得造成伤害。" );
        _test.False(target.HasStatusEffect("slow"), "攻击落空不得施加缓速。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 2), "攻击落空仍必须后撤1格。" );
        _test.Eq(caster.GetCurrentAp(), 1, "攻击落空仍应支付1 AP。" );
        _test.Eq(caster.GetCurrentMp(), 80, "攻击落空仍应支付20 MP。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 180, "攻击落空仍应启动180TU冷却。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestBlockedRetreatStillResolvesAttack(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("frost_blocked_caster", new Vector2I(2, 2), 3);
        BattleUnitState target = BuildUnit("frost_blocked_target", "enemy", new Vector2I(3, 2));
        BattleUnitState blocker = BuildUnit("frost_retreat_blocker", "enemy", new Vector2I(1, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target, blocker);
        ConfigureHit(fixture);

        BattleCommand command = BuildCommand(caster, target, Vector2I.Left);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "后撤第一格被占用时攻击本身仍应允许。" );
        _test.Eq(preview?.SourceRetreatPathTyped.Count ?? -1, 1, "受阻后撤预览应只保留起点。" );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(target.GetCurrentHp() < 100, "后撤受阻不得取消命中伤害。" );
        _test.True(target.HasStatusEffect("slow"), "后撤受阻不得取消命中的缓速。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(2, 2), "后撤受阻应停在原地。" );

        batch?.Dispose();
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestLosAndMovementLockRejectBeforeCost(SkillDefinition skill)
    {
        BattleUnitState wallCaster = BuildReadyCaster("frost_wall_caster", new Vector2I(1, 1), 3);
        BattleUnitState wallTarget = BuildUnit("frost_wall_target", "enemy", new Vector2I(4, 1));
        using (BattleTestFixture fixture = CreateFixture(skill, wallCaster, wallTarget))
        {
            fixture.Runtime.GetGridService().SetEdgeFeature(
                fixture.State,
                new Vector2I(2, 1),
                Vector2I.Right,
                BattleEdgeFeatureState.MakeWall()
            );
            BattleCommand command = BuildCommand(wallCaster, wallTarget, Vector2I.Left);
            AssertRejectedWithoutCost(fixture, command, "视线被墙阻挡时");
        }

        BattleUnitState rootedCaster = BuildReadyCaster("frost_rooted_caster", new Vector2I(2, 2), 3);
        BattleUnitState rootedTarget = BuildUnit("frost_rooted_target", "enemy", new Vector2I(3, 2));
        rootedCaster.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = BattleStatusSemanticTable.STATUS_ROOTED,
                duration = 60,
                power = 1,
                stacks = 1,
            }
        );
        using (BattleTestFixture fixture = CreateFixture(skill, rootedCaster, rootedTarget))
        {
            BattleCommand command = BuildCommand(rootedCaster, rootedTarget, Vector2I.Left);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview != null && !preview.allowed, "定身应在付费前拒绝整个技能。" );
            _test.Eq(
                fixture.Runtime.GetSkillCastBlockReason(rootedCaster, skill),
                BattleSkillCastBlockReasonKind.MovementRestricted,
                "定身应报告 typed movement-restricted 原因。"
            );
            BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
            _test.Eq(rootedCaster.GetCurrentAp(), 2, "定身拒绝不得消耗AP。" );
            _test.Eq(rootedCaster.GetCurrentMp(), 100, "定身拒绝不得消耗MP。" );
            _test.Eq(rootedCaster.GetCooldownTyped(SkillId), 0, "定身拒绝不得启动冷却。" );
            _test.Eq(rootedCaster.GetAnchorCoord(), new Vector2I(2, 2), "定身拒绝不得改变坐标。" );
            batch?.Dispose();
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestAiEnumeratesCanonicalRetreatDirections(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("frost_ai_caster", new Vector2I(2, 2), 3);
        BattleUnitState target = BuildUnit("frost_ai_target", "enemy", new Vector2I(3, 2));
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
            skill_score_input_callback = (_, _, command, preview, _, _, _) =>
            {
                evaluatedDirections.Add(command.source_retreat_direction);
                return new BattleAiScoreInput
                {
                    command = command,
                    preview = preview,
                    effective_target_count = 1,
                    enemy_target_count = 1,
                    total_score = command.source_retreat_direction == Vector2I.Left ? 100 : 10,
                };
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        var action = new UseUnitSkillActionDefinition(
            "frost_bolt_ai",
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
        _test.Eq(evaluatedDirections.Count, 3, "目标在右侧时，AI应分别评估上、下、左三个远离方向。" );
        _test.True(evaluatedDirections.Contains(Vector2I.Up), "AI应枚举向上后撤。" );
        _test.True(evaluatedDirections.Contains(Vector2I.Down), "AI应枚举向下后撤。" );
        _test.True(evaluatedDirections.Contains(Vector2I.Left), "AI应枚举向左后撤。" );
        _test.False(evaluatedDirections.Contains(Vector2I.Right), "AI不得枚举靠近目标的方向。" );
        _test.Eq(
            decision?.command?.source_retreat_direction ?? Vector2I.Zero,
            Vector2I.Left,
            "AI应保留评分最高的真实后撤方向。"
        );
        _test.Eq(
            decision?.score_input?.preview?.resolved_anchor_coord ?? new Vector2I(-1, -1),
            new Vector2I(1, 2),
            "AI决策应携带 canonical 最终落点。"
        );
        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "AI霜击术应记录一次 action trace。" );
        _test.Eq(traces.Count == 1 ? traces[0].EvaluationCount : -1, 3, "AI trace 应证明三个方向均被评估。" );
    }

    private void AssertRejectedWithoutCost(
        BattleTestFixture fixture,
        BattleCommand command,
        string label
    )
    {
        BattleUnitState caster = fixture.Allies[0];
        Vector2I coordBefore = caster.GetAnchorCoord();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, $"{label}预览必须拒绝。" );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), 2, $"{label}不得消耗AP。" );
        _test.Eq(caster.GetCurrentMp(), 100, $"{label}不得消耗MP。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{label}不得启动冷却。" );
        _test.Eq(caster.GetAnchorCoord(), coordBefore, $"{label}不得改变坐标。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "mage_frost_bolt_regression"
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
            "mage_frost_bolt",
            new Vector2I(8, 5),
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

    private static BattleUnitState BuildReadyCaster(
        StringName id,
        Vector2I coord,
        int level
    )
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level);
        caster.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
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
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentHp(100);
        unit.SetCurrentMp(100);
        unit.SetCurrentAp(2);
        unit.SetCurrentStamina(100);
        unit.SetCurrentMovePoints(3);
        unit.SetAnchorCoord(coord);
        return unit;
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

    private static IReadOnlyList<CombatEffectDefinition> ActiveEffectsAtLevel(
        IReadOnlyList<CombatEffectDefinition> effects,
        int level
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect != null
                && level >= Math.Max(effect.MinSkillLevel, 0)
                && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel)
            )
            {
                result.Add(effect);
            }
        }
        return result;
    }

    private static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effects,
        BattleEffectKind kind
    )
    {
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == kind)
                return effect;
        }
        return null;
    }

    private static CombatEffectDefinition FindStatusEffect(
        IEnumerable<CombatEffectDefinition> effects,
        StringName statusId
    )
    {
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect?.EffectKind == BattleEffectKind.Status
                && effect.StatusId == statusId
            )
            {
                return effect;
            }
        }
        return null;
    }

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;

    private static bool HasStringName(
        IEnumerable<StringName> values,
        StringName expected
    )
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }
}
