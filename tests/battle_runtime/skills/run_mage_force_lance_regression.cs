using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_mage_force_lance_regression : LifecycleTestSceneTree
{
    private const string SkillPath = "mage_force_lance";
    private static readonly StringName SkillId = "mage_force_lance";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestLevelCurvesAndDescriptions(skill);
            TestSchemaRejectsFixedDamageAndShortCurves();
            TestCanonicalLevelSevenPreview(skill);
            TestLegalityGatesBeforePayment(skill);
            TestLevelSevenExecution(skill);
            TestMissStopsLaterStages(skill);
            TestAiUsesCanonicalPreview(skill);
            TestAiExpectedDamageUsesOrderedReachProbability(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage force lance regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "力场矛正式资源与combat_profile应可加载。" );
        if (combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "技能ID必须保持 mage_force_lance。" );
        _test.Eq(skill.DisplayName, "力场矛", "显示名必须保持不变。" );
        _test.Eq(skill.MaxLevel, 7, "技能等级上限应为7。" );
        _test.Eq(skill.NonCoreMaxLevel, 5, "非核心等级上限应为5。" );
        _test.True(skill.Description.Contains("首个敌人"), "描述必须公开首敌门禁。" );
        _test.True(skill.Description.Contains("接触AC"), "描述必须公开接触AC。" );
        _test.True(skill.Description.Contains("落空"), "描述必须公开未命中即停止。" );
        _test.False(skill.Tags.Contains(new StringName("control")), "无效的control标签必须移除。" );

        _test.Eq(combat.TargetMode, new StringName("unit"), "必须选择单位目标。" );
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "首目标必须为敌方。" );
        _test.Eq(combat.WeaponRangePolicy, new StringName("configured"), "法术射程必须读取配置。" );
        _test.Eq(combat.ProjectileKind, new StringName("magical"), "必须使用魔法投射物。" );
        _test.Eq(combat.AttackDefenseMode, new StringName("touch"), "每段必须攻击接触AC。" );
        _test.True(combat.RequiresLos, "直线路径必须要求视线。" );
        _test.Eq(combat.MaxHitsPerTarget, 1, "同一单位最多命中一次。" );
        _test.True(combat.SequentialLineHit != null, "必须投影typed连续直线命中配置。" );
        _test.Eq(combat.PassiveEffectDefinitions.Count, 0, "不得保留无效marked状态。" );
        _test.True(
            combat.EffectDefinitions.All(effect => effect.EffectKind == BattleEffectKind.Damage),
            "资源只能声明等级化法术伤害。"
        );
        _test.True(
            combat.EffectDefinitions.All(effect => !effect.RequiresWeapon),
            "力场伤害不得依赖武器骰。"
        );
    }

    private void TestLevelCurvesAndDescriptions(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        CombatSequentialLineHitDefinition profile = combat.SequentialLineHit;
        int[] expectedMp = { 360, 360, 330, 330, 330, 330, 300, 300 };
        int[] expectedCooldown = { 180, 180, 180, 180, 150, 150, 150, 150 };
        int[] expectedDice = { 2, 2, 3, 3, 4, 4, 5, 6 };
        int[] expectedAttack = { 0, 1, 1, 1, 1, 1, 1, 2 };
        for (int level = 0; level <= 7; level++)
        {
            CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 1, $"{level}级必须固定消耗1AP。" );
            _test.Eq(costs.MpCost, expectedMp[level], $"{level}级法力消耗不匹配。" );
            _test.True(costs.MpCost >= 300, $"{level}级法力消耗不得低于300。" );
            _test.Eq(costs.CooldownTu, expectedCooldown[level], $"{level}级冷却不匹配。" );
            _test.Eq(combat.GetEffectiveAttackRollBonus(level), expectedAttack[level], $"{level}级攻击修正不匹配。" );
            CombatEffectDefinition damage = CollectActiveDamage(skill, level).Single();
            _test.Eq(damage.DiceCount, expectedDice[level], $"{level}级伤害骰数不匹配。" );
            _test.Eq(damage.DiceSides, 8, $"{level}级必须使用d8。" );
        }
        _test.Eq(profile.GetMinimumPrimaryDistance(0), 2, "0级必须保持2格最短距离。" );
        _test.Eq(profile.GetMinimumPrimaryDistance(6), 1, "6级应允许邻接施放。" );
        _test.Eq(profile.GetContinuationRange(2), 2, "2级命中后续行2格。" );
        _test.Eq(profile.GetContinuationRange(3), 3, "3级命中后续行3格。" );
        _test.Eq(profile.GetFollowUpAttackPenalty(4), 2, "4级后续每段累计-2。" );
        _test.Eq(profile.GetFollowUpAttackPenalty(5), 1, "5级后续每段累计-1。" );
        _test.Eq(combat.GetEffectiveRangeValue(7), 6, "7级首目标射程应为6。" );
        _test.Eq(combat.GetEffectiveMaxTargetCount(7), 3, "7级最多攻击3个目标。" );

        string levelSeven = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            7,
            new GDictionary()
        );
        _test.True(levelSeven.Contains("6D8"), "7级文本必须显示6D8。" );
        _test.True(levelSeven.Contains("300法力"), "7级文本必须显示300法力。" );
        _test.True(levelSeven.Contains("150TU"), "7级文本必须显示150TU冷却。" );
        _test.True(levelSeven.Contains("最多3个目标"), "7级文本必须显示三目标上限。" );
    }

    private void TestSchemaRejectsFixedDamageAndShortCurves()
    {
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        using CombatSkillDef fixedDamage = BuildSchemaProfile();
        fixedDamage.effect_defs[0].power = 1;
        var fixedDamageErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            fixedDamageErrors,
            "force_lance_fixed_damage",
            fixedDamage
        );
        _test.True(
            ErrorsContain(fixedDamageErrors, "ordinary non-weapon dice damage"),
            $"固定伤害必须被schema拒绝。errors={string.Join(" | ", fixedDamageErrors)}"
        );

        using CombatSkillDef shortCurve = BuildSchemaProfile();
        shortCurve.sequential_line_hit_profile.continuation_range_curve = Array.Empty<int>();
        var curveErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            curveErrors,
            "force_lance_short_curve",
            shortCurve
        );
        _test.True(
            ErrorsContain(curveErrors, "continuation_range_curve"),
            $"缺失机制曲线必须被schema拒绝。errors={string.Join(" | ", curveErrors)}"
        );
    }

    private void TestCanonicalLevelSevenPreview(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("preview_caster", "player", new Vector2I(1, 2), 7);
        BattleUnitState first = BuildUnit("preview_first", "enemy", new Vector2I(3, 2));
        BattleUnitState second = BuildUnit("preview_second", "enemy", new Vector2I(6, 2));
        BattleUnitState third = BuildUnit("preview_third", "enemy", new Vector2I(9, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, first, second, third);
        BattleCommand command = BuildCommand(caster, first);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(preview?.allowed == true, $"三级直线阵列应合法。logs={JoinLogs(preview)}" );
        _test.Eq(preview.TargetUnitIdsTyped.Count, 3, "预览必须公开三个有序阶段。" );
        if (preview.TargetUnitIdsTyped.Count == 3)
        {
            _test.Eq(preview.TargetUnitIdsTyped[0], first.unit_id, "第一阶段必须为所选首敌。" );
            _test.Eq(preview.TargetUnitIdsTyped[1], second.unit_id, "第二阶段顺序必须稳定。" );
            _test.Eq(preview.TargetUnitIdsTyped[2], third.unit_id, "第三阶段顺序必须稳定。" );
        }
        _test.Eq(preview.hit_preview?.Source, "sequential_line_hit", "必须使用专用typed预览来源。" );
        _test.Eq(preview.hit_preview?.StageCount ?? 0, 3, "命中预览必须包含三个阶段。" );
        _test.Eq(preview.hit_preview?.Stages[0].ReachProbabilityBasisPoints ?? 0, 10000, "首段到达率必须为100%。" );
        _test.True(
            (preview.hit_preview?.Stages[1].ReachProbabilityBasisPoints ?? 0) < 10000,
            "第二段到达率必须乘入首段命中率。"
        );
        _test.True(
            (preview.hit_preview?.Stages[2].ReachProbabilityBasisPoints ?? 0)
                < (preview.hit_preview?.Stages[1].ReachProbabilityBasisPoints ?? 0),
            "第三段到达率必须继续乘入第二段命中率。"
        );
        Dispose(command, preview);
    }

    private void TestLegalityGatesBeforePayment(SkillDefinition skill)
    {
        AssertRejected(
            skill,
            BuildReadyCaster("diagonal_caster", "player", new Vector2I(1, 1), 7),
            BuildUnit("diagonal_target", "enemy", new Vector2I(3, 3)),
            Array.Empty<BattleUnitState>(),
            "斜向目标"
        );
        AssertRejected(
            skill,
            BuildReadyCaster("minimum_caster", "player", new Vector2I(1, 1), 0),
            BuildUnit("minimum_target", "enemy", new Vector2I(2, 1)),
            Array.Empty<BattleUnitState>(),
            "0级邻接目标"
        );
        AssertRejected(
            skill,
            BuildReadyCaster("blocker_caster", "player", new Vector2I(1, 1), 7),
            BuildUnit("blocker_target", "enemy", new Vector2I(4, 1)),
            new[] { BuildUnit("ally_blocker", "player", new Vector2I(2, 1)) },
            "首敌前存在友军"
        );
        AssertRejected(
            skill,
            BuildReadyCaster("mp_caster", "player", new Vector2I(1, 1), 7, mp: 299),
            BuildUnit("mp_target", "enemy", new Vector2I(3, 1)),
            Array.Empty<BattleUnitState>(),
            "法力不足"
        );

        BattleUnitState barrierCaster = BuildReadyCaster(
            "barrier_caster",
            "player",
            new Vector2I(1, 1),
            7
        );
        BattleUnitState barrierTarget = BuildUnit(
            "barrier_target",
            "enemy",
            new Vector2I(4, 1)
        );
        using BattleTestFixture barrierFixture = CreateFixture(
            skill,
            barrierCaster,
            barrierTarget
        );
        var ownBarrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "own_barrier",
            ProfileId = "force_lance_barrier_probe",
            SourceUnitId = barrierCaster.unit_id,
            AnchorCoord = new Vector2I(3, 1),
            RadiusCells = 0,
            AreaPattern = "single",
            RemainingTu = 100,
        };
        ownBarrier.SetLayers(
            new[]
            {
                new BattleBarrierLayerState
                {
                    LayerId = "active_layer",
                    DisplayName = "测试屏障",
                },
            }
        );
        barrierFixture.State.PutLayeredBarrierField("own_barrier", ownBarrier);
        BattleCommand barrierCommand = BuildCommand(barrierCaster, barrierTarget);
        int barrierMpBefore = barrierCaster.GetCurrentMp();
        BattlePreview barrierPreview = barrierFixture.Runtime.PreviewCommand(barrierCommand);
        _test.True(
            barrierPreview != null && !barrierPreview.allowed,
            $"施术者自己创建的屏障也必须截停。logs={JoinLogs(barrierPreview)}"
        );
        BattleEventBatch barrierBatch = barrierFixture.Runtime.IssueCommand(barrierCommand);
        _test.Eq(barrierCaster.GetCurrentMp(), barrierMpBefore, "屏障拒绝不得扣法力。" );
        _test.Eq(barrierCaster.GetCooldownTyped(SkillId), 0, "屏障拒绝不得启动冷却。" );
        barrierBatch?.Dispose();
        Dispose(barrierCommand, barrierPreview);
    }

    private void TestLevelSevenExecution(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("execute_caster", "player", new Vector2I(1, 2), 7);
        BattleUnitState first = BuildUnit("execute_first", "enemy", new Vector2I(3, 2));
        BattleUnitState second = BuildUnit("execute_second", "enemy", new Vector2I(6, 2));
        BattleUnitState third = BuildUnit("execute_third", "enemy", new Vector2I(9, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, first, second, third);
        var damageProbe = new SequentialDamageProbe();
        ConfigureOutcomes(fixture, damageProbe, new[] { true, true, true });
        BattleCommand command = BuildCommand(caster, first);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        AssertSequence(damageProbe.TargetIds, new[] { first.unit_id, second.unit_id, third.unit_id }, "三段必须按直线顺序结算。" );
        AssertSequence(damageProbe.DiceCounts, new[] { 6, 6, 6 }, "7级每段必须造成6d8。" );
        AssertSequence(damageProbe.DiceSides, new[] { 8, 8, 8 }, "7级每段必须使用d8。" );
        AssertSequence(damageProbe.SituationalAttackBonuses, new[] { 0, -1, -2 }, "7级阶段修正必须为+2/+1/+0对应的0/-1/-2附加值。" );
        _test.Eq(caster.GetCurrentAp(), 1, "执行后必须支付1AP。" );
        _test.Eq(caster.GetCurrentMp(), 700, "执行后必须支付300法力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 150, "执行后必须进入150TU冷却。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestMissStopsLaterStages(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("miss_caster", "player", new Vector2I(1, 2), 7);
        BattleUnitState first = BuildUnit("miss_first", "enemy", new Vector2I(3, 2));
        BattleUnitState second = BuildUnit("miss_second", "enemy", new Vector2I(6, 2));
        BattleUnitState third = BuildUnit("miss_third", "enemy", new Vector2I(9, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, first, second, third);
        var damageProbe = new SequentialDamageProbe();
        ConfigureOutcomes(fixture, damageProbe, new[] { true, false, true });
        BattleCommand command = BuildCommand(caster, first);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        AssertSequence(damageProbe.TargetIds, new[] { first.unit_id, second.unit_id }, "第二段落空后不得攻击第三目标。" );
        _test.True(batch?.LogLinesTyped.Any(line => line.Contains("立即消散")) == true, "日志必须公开落空即停止。" );
        _test.Eq(caster.GetCurrentMp(), 700, "中途落空仍必须支付完整300法力。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiUsesCanonicalPreview(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("ai_caster", "enemy", new Vector2I(1, 2), 7);
        BattleUnitState first = BuildUnit("ai_first", "player", new Vector2I(3, 2));
        BattleUnitState second = BuildUnit("ai_second", "player", new Vector2I(6, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, first, second);
        int canonicalCandidates = 0;
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
                if (preview?.hit_preview?.Source == "sequential_line_hit")
                {
                    canonicalCandidates++;
                    _test.True(preview.ContainsTargetUnitId(command.target_unit_id), "canonical预览必须保留所选首敌。" );
                }
                return new BattleAiScoreInput
                {
                    command = command,
                    preview = preview,
                    effective_target_count = preview?.TargetUnitIdsTyped.Count ?? 0,
                    enemy_target_count = preview?.TargetUnitIdsTyped.Count ?? 0,
                    total_score = 100,
                };
            },
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildAiAction(),
            context
        );
        _test.True(canonicalCandidates >= 1, "AI至少应评分一个canonical连续直线候选。" );
        _test.True(decision?.command != null, "AI应能选择合法力场矛。" );
    }

    private void TestAiExpectedDamageUsesOrderedReachProbability(SkillDefinition skill)
    {
        var preview = new AttackPreviewData
        {
            Stages = new List<AttackPreviewStage>
            {
                new(80, 80, 80, 5, 5, "80%", 10000, 100),
                new(70, 70, 70, 7, 7, "70%", 8000, 100),
                new(60, 60, 60, 9, 9, "60%", 5600, 100),
            },
        };
        CombatEffectDefinition damage = CollectActiveDamage(skill, 7).Single();
        IReadOnlyList<CombatEffectDefinition> thirdExpected =
            BattleAiScoreService.BuildSequentialLineHitExpectedEffects(
                new[] { damage },
                2,
                preview
            );
        _test.Eq(thirdExpected.Count, 1, "AI应保留第三段伤害效果。" );
        _test.True(
            Math.Abs(thirdExpected[0].PreResistanceDamageMultiplier - 0.336) < 0.0001,
            "第三段AI期望值必须为0.56到达率×0.60命中率。"
        );
    }

    private void AssertRejected(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState primary,
        IReadOnlyList<BattleUnitState> otherUnits,
        string label
    )
    {
        using BattleTestFixture fixture = CreateFixture(
            skill,
            caster,
            primary,
            otherUnits?.ToArray() ?? Array.Empty<BattleUnitState>()
        );
        BattleCommand command = BuildCommand(caster, primary);
        int apBefore = caster.GetCurrentAp();
        int mpBefore = caster.GetCurrentMp();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, $"{label}时预览必须拒绝。logs={JoinLogs(preview)}" );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), apBefore, $"{label}不得扣AP。" );
        _test.Eq(caster.GetCurrentMp(), mpBefore, $"{label}不得扣法力。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{label}不得启动冷却。" );
        batch?.Dispose();
        Dispose(command, preview);
    }

    private static IReadOnlyList<CombatEffectDefinition> CollectActiveDamage(
        SkillDefinition skill,
        int skillLevel
    ) =>
        skill.CombatProfile.EffectDefinitions
            .Where(
                effect =>
                    effect != null
                    && skillLevel >= Math.Max(effect.MinSkillLevel, 0)
                    && (effect.MaxSkillLevel < 0 || skillLevel <= effect.MaxSkillLevel)
            )
            .ToArray();

    private static CombatSkillDef BuildSchemaProfile()
    {
        var damage = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectDamage,
            damage_tag = "force",
            dice_count = 2,
            dice_sides = 8,
        };
        var profile = new CombatSkillDef
        {
            skill_id = "sequential_line_schema_probe",
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            min_target_count = 1,
            max_target_count = 2,
            max_hits_per_target = 1,
            range_value = 5,
            weapon_range_policy = "configured",
            requires_los = true,
            projectile_kind = "magical",
            attack_resolution_mode = "auto",
            sequential_line_hit_profile = new CombatSequentialLineHitDef
            {
                minimum_primary_distance_curve = new[] { 1 },
                continuation_range_curve = new[] { 2 },
                follow_up_attack_penalty_curve = new[] { 1 },
            },
        };
        profile.effect_defs.Add(damage);
        return profile;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "mage_force_lance_regression"
        );

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState primary,
        params BattleUnitState[] otherUnits
    )
    {
        var allies = new List<BattleUnitState> { caster };
        var enemies = new List<BattleUnitState>();
        AddByFaction(caster, primary, allies, enemies);
        foreach (BattleUnitState unit in otherUnits ?? Array.Empty<BattleUnitState>())
            AddByFaction(caster, unit, allies, enemies);
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "mage_force_lance",
            new Vector2I(12, 5),
            allies,
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

    private static void AddByFaction(
        BattleUnitState caster,
        BattleUnitState unit,
        List<BattleUnitState> allies,
        List<BattleUnitState> enemies
    )
    {
        if (unit.faction_id == caster.faction_id)
            allies.Add(unit);
        else
            enemies.Add(unit);
    }

    private static void ConfigureOutcomes(
        BattleTestFixture fixture,
        SequentialDamageProbe damageProbe,
        IReadOnlyList<bool> outcomes
    )
    {
        fixture.Runtime.ConfigureDamageResolverForTests(damageProbe);
        fixture.Runtime.ConfigureHitResolverForTests(new SequenceHitResolver(outcomes));
    }

    private static BattleUnitState BuildReadyCaster(
        StringName id,
        StringName faction,
        Vector2I coord,
        int skillLevel,
        int mp = 1000
    )
    {
        BattleUnitState caster = BuildUnit(id, faction, coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, skillLevel, preserveZero: true);
        caster.SetCurrentAp(2);
        caster.SetCurrentMp(mp);
        caster.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        return caster;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            id,
            faction,
            coord,
            currentAp: 2,
            currentHp: 500
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 500);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.SetCurrentHp(500);
        unit.SetCurrentAp(2);
        return unit;
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

    private static UseUnitSkillActionDefinition BuildAiAction() =>
        new(
            "force_lance_ai",
            "test",
            BattleAiActionIntent.Offense,
            new[] { SkillId },
            "nearest_enemy",
            1,
            0,
            false,
            0,
            10,
            EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.TargetUnit)
        );

    private static bool ErrorsContain(IEnumerable<string> errors, string needle) =>
        (errors ?? Array.Empty<string>()).Any(
            error => error?.Contains(needle, StringComparison.Ordinal) == true
        );

    private void AssertSequence<T>(
        IReadOnlyList<T> actual,
        IReadOnlyList<T> expected,
        string message
    )
    {
        bool equal = actual != null
            && expected != null
            && actual.Count == expected.Count;
        if (equal)
        {
            for (int index = 0; index < expected.Count; index++)
            {
                if (EqualityComparer<T>.Default.Equals(actual[index], expected[index]))
                    continue;
                equal = false;
                break;
            }
        }
        _test.True(
            equal,
            $"{message} actual=[{string.Join(",", actual ?? Array.Empty<T>())}] expected=[{string.Join(",", expected ?? Array.Empty<T>())}]"
        );
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static void Dispose(BattleCommand command, BattlePreview preview = null)
    {
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private sealed class SequenceHitResolver : FixedHitResolver
    {
        private readonly IReadOnlyList<bool> _outcomes;
        private int _index;

        internal SequenceHitResolver(IReadOnlyList<bool> outcomes)
        {
            _outcomes = outcomes ?? Array.Empty<bool>();
        }

        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            bool success = _index < _outcomes.Count && _outcomes[_index];
            _index++;
            return BuildFixedAttackMetadata(
                attackCheck,
                attackContext,
                success ? AttackResolutionHit : new StringName("miss"),
                success,
                false,
                !success
            );
        }
    }

    private sealed class SequentialDamageProbe : FixedHitMaxDamageResolver
    {
        internal List<StringName> TargetIds { get; } = new();
        internal List<int> DiceCounts { get; } = new();
        internal List<int> DiceSides { get; } = new();
        internal List<int> SituationalAttackBonuses { get; } = new();

        internal override AttackEffectResolutionResult ResolveAttackEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            AttackCheckInput attackCheck,
            AttackContext attackContext = null
        )
        {
            TargetIds.Add(targetUnit?.unit_id ?? new StringName(""));
            SituationalAttackBonuses.Add(attackCheck.SituationalAttackBonus);
            CombatEffectDefinition damage = (effectDefinitions ?? Array.Empty<CombatEffectDefinition>())
                .FirstOrDefault(effect => effect?.EffectKind == BattleEffectKind.Damage);
            DiceCounts.Add(damage?.DiceCount ?? 0);
            DiceSides.Add(damage?.DiceSides ?? 0);
            return base.ResolveAttackEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                attackCheck,
                attackContext
            );
        }
    }
}
