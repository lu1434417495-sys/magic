using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_phantom_through_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "warrior_phantom_through";
    private static readonly StringName SkillId = "warrior_phantom_through";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestLevelCurvesAndDescriptions(skill);
            TestSchemaRejectsForbiddenScaling();
            TestCanonicalTenCellPreview(skill);
            TestLegalityGatesBeforeCost(skill);
            TestLevelNineExecutionUsesFiveWeaponDice(skill);
            TestIntermediateMissDoesNotStop(skill);
            TestPrimaryMissPreventsLanding(skill);
            TestAiUsesCanonicalLinePlan(skill);
            TestAiExpectedDamageAndAuraNormalization(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior phantom through regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "幽影穿身正式资源与combat_profile应可加载。" );
        if (combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "技能ID必须保持 warrior_phantom_through。" );
        _test.Eq(skill.DisplayName, "幽影穿身", "显示名应保持不变。" );
        _test.Eq(skill.MaxLevel, 9, "技能等级上限应为9。" );
        _test.Eq(skill.NonCoreMaxLevel, 7, "非核心等级上限应为7。" );
        _test.True(skill.Description.Contains("终点目标"), "描述必须明确终点敌人是主目标。" );
        _test.True(skill.Description.Contains("3000斗气"), "描述必须公开固定3000斗气。" );
        _test.True(skill.Description.Contains("标准武器攻击"), "描述必须公开标准武器链。" );
        _test.True(skill.Description.Contains("5级起"), "描述必须公开途中命中的成长门槛。" );

        _test.Eq(combat.TargetMode, new StringName("unit"), "必须选择单位目标。" );
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "终点只能选择敌方。" );
        _test.Eq(combat.RangeValue, 10, "最大选择距离必须为10格。" );
        _test.Eq(combat.WeaponRangePolicy, new StringName("configured"), "射程必须采用配置值而非武器射程。" );
        _test.Eq(combat.MaxHitsPerTarget, 1, "终点主目标只能承受一次集中攻击。" );
        _test.True(combat.RequiresLos, "直线路径必须要求视线。" );
        _test.False(combat.AllowsNaturalWeapon, "不得使用天生武器。" );
        _test.True(combat.LineThroughAttack != null, "必须投影typed穿身攻击配置。" );
        _test.Eq(combat.LineThroughAttack?.MaximumWeaponRange ?? 0, 2, "只允许攻击距离不超过2的武器。" );
        _test.Eq(combat.EffectDefinitions.Count, 1, "只能声明一个基础武器伤害效果。" );
        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.Eq(damage.Power, 0, "不得使用固定伤害。" );
        _test.Eq(damage.DiceCount, 0, "不得使用脱离武器的固定骰。" );
        _test.Eq(damage.WeaponDiceMultiplier, 1, "资源基础效果必须保持1W。" );
        _test.True(damage.ResolveAsWeaponAttack, "必须进入标准武器攻击链。" );
    }

    private void TestLevelCurvesAndDescriptions(SkillDefinition skill)
    {
        CombatLineThroughAttackDefinition profile = skill.CombatProfile.LineThroughAttack;
        for (int level = 0; level <= 9; level++)
        {
            CombatSkillResourceCosts costs =
                skill.CombatProfile.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 1, $"{level}级应固定消耗1AP。" );
            _test.Eq(costs.StaminaCost, 0, $"{level}级不得消耗体力。" );
            _test.Eq(costs.AuraCost, 3000, $"{level}级应固定消耗3000斗气。" );
            _test.Eq(costs.CooldownTu, 100, $"{level}级应固定冷却100TU。" );
            _test.Eq(skill.CombatProfile.GetEffectiveAttackRollBonus(level), 0, $"{level}级通用攻击修正必须为0。" );
        }
        _test.Eq(profile.GetPrimaryWeaponDiceMultiplier(0), 2, "0级终点基础伤害应为2W。" );
        _test.Eq(profile.GetPrimaryWeaponDiceMultiplier(3), 3, "3级终点基础伤害应为3W。" );
        _test.Eq(profile.GetSuccessfulIntermediateHitBonusCap(4), 0, "4级尚不计入途中成功。" );
        _test.Eq(profile.GetSuccessfulIntermediateHitBonusCap(5), 1, "5级最多计入1次途中成功。" );
        _test.Eq(profile.GetPrimaryAttackRollBonus(7), 1, "7级终点基础攻击检定应+1。" );
        _test.Eq(profile.GetSuccessfulIntermediateHitBonusCap(9), 2, "9级最多计入2次途中成功。" );
        _test.Eq(profile.GetPrimaryWeaponDiceMultiplier(9, 2), 5, "9级两次铺垫后终点必须为5W。" );
        _test.Eq(profile.GetPrimaryAttackRollBonus(9, 2), 3, "9级两次铺垫后终点攻击检定必须+3。" );

        string levelNine = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            9,
            new GDictionary()
        );
        _test.True(levelNine.Contains("基础3W"), "9级文本应显示终点基础3W。" );
        _test.True(levelNine.Contains("攻击检定+1"), "9级文本应显示基础攻击检定+1。" );
        _test.True(levelNine.Contains("最多计入2次"), "9级文本应显示两次铺垫上限。" );
    }

    private void TestSchemaRejectsForbiddenScaling()
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
            "line_through_fixed_damage",
            fixedDamage
        );
        _test.True(
            ErrorsContain(fixedDamageErrors, "without fixed damage"),
            $"固定伤害必须被schema拒绝。errors={string.Join(" | ", fixedDamageErrors)}"
        );

        using CombatSkillDef percentageDamage = BuildSchemaProfile();
        percentageDamage.effect_defs[0].damage_ratio_percent = 150;
        var percentageErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            percentageErrors,
            "line_through_percentage_damage",
            percentageDamage
        );
        _test.True(
            ErrorsContain(percentageErrors, "percentage scaling"),
            $"百分比伤害成长必须被schema拒绝。errors={string.Join(" | ", percentageErrors)}"
        );

        using CombatSkillDef negativeAttack = BuildSchemaProfile();
        negativeAttack.line_through_attack_profile.primary_attack_roll_bonus_curve = new[] { -1 };
        var attackErrors = new GStringArray();
        validator.AppendCombatProfileValidationErrors(
            attackErrors,
            "line_through_negative_attack",
            negativeAttack
        );
        _test.True(
            ErrorsContain(attackErrors, "non-negative"),
            $"负攻击修正必须被schema拒绝。errors={string.Join(" | ", attackErrors)}"
        );
    }

    private void TestCanonicalTenCellPreview(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("preview_caster", new Vector2I(1, 2), 9);
        BattleUnitState middleOne = BuildUnit("preview_middle_1", "enemy", new Vector2I(4, 2));
        BattleUnitState middleTwo = BuildUnit("preview_middle_2", "enemy", new Vector2I(7, 2));
        BattleUnitState primary = BuildUnit("preview_primary", "enemy", new Vector2I(11, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, primary, middleOne, middleTwo);
        BattleCommand command = BuildCommand(caster, primary);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(preview?.allowed == true, $"10格终点应合法。logs={JoinLogs(preview)}" );
        _test.Eq(preview.TargetUnitIdsTyped.Count, 3, "预览应包含两名途中敌人与终点敌人。" );
        _test.Eq(preview.TargetUnitIdsTyped[0], middleOne.unit_id, "途中敌人必须按路径排序。" );
        _test.Eq(preview.TargetUnitIdsTyped[1], middleTwo.unit_id, "第二名途中敌人顺序必须稳定。" );
        _test.Eq(preview.TargetUnitIdsTyped[2], primary.unit_id, "终点敌人必须位于目标序列最后。" );
        _test.Eq(preview.resolved_anchor_coord, new Vector2I(12, 2), "预览必须公开敌后落点。" );
        _test.Eq(preview.SourceAdvancePathTyped.Count, 12, "位移路径应包含起点至敌后落点。" );
        _test.Eq(preview.hit_preview?.Source, "line_through_attack", "命中预览必须使用专用typed来源。" );
        _test.Eq(preview.hit_preview?.StageCount ?? 0, 5, "应包含2个途中阶段和3个终点状态。" );
        _test.Eq(preview.hit_preview?.Stages[2].DamageMultiplierPercent ?? 0, 300, "终点0次铺垫状态应显示3W。" );
        _test.Eq(preview.hit_preview?.Stages[4].DamageMultiplierPercent ?? 0, 500, "终点2次铺垫状态应显示5W。" );
        Dispose(command, preview);
    }

    private void TestLegalityGatesBeforeCost(SkillDefinition skill)
    {
        AssertRejected(
            skill,
            BuildReadyCaster("diagonal_caster", new Vector2I(1, 1), 9),
            BuildUnit("diagonal_target", "enemy", new Vector2I(4, 4)),
            Array.Empty<BattleUnitState>(),
            "斜向终点"
        );
        AssertRejected(
            skill,
            BuildReadyCaster("range_caster", new Vector2I(1, 1), 9),
            BuildUnit("range_target", "enemy", new Vector2I(12, 1)),
            Array.Empty<BattleUnitState>(),
            "超过10格"
        );
        AssertRejected(
            skill,
            BuildReadyCaster("ally_block_caster", new Vector2I(1, 1), 9),
            BuildUnit("ally_block_target", "enemy", new Vector2I(6, 1)),
            new[] { BuildUnit("ally_blocker", "player", new Vector2I(3, 1)) },
            "友方阻挡路径"
        );
        AssertRejected(
            skill,
            BuildReadyCaster("landing_caster", new Vector2I(1, 1), 9),
            BuildUnit("landing_target", "enemy", new Vector2I(6, 1)),
            new[] { BuildUnit("landing_blocker", "enemy", new Vector2I(7, 1)) },
            "敌后落点占用"
        );

        BattleUnitState wallCaster = BuildReadyCaster(
            "wall_caster",
            new Vector2I(1, 1),
            9
        );
        BattleUnitState wallTarget = BuildUnit(
            "wall_target",
            "enemy",
            new Vector2I(6, 1)
        );
        using (BattleTestFixture wallFixture = CreateFixture(skill, wallCaster, wallTarget))
        {
            _test.True(
                wallFixture.State.PutTemporaryEdgeFeature(
                    new BattleTemporaryEdgeFeatureState
                    {
                        OriginCoord = new Vector2I(3, 1),
                        Direction = Vector2I.Right,
                        BindingId = "phantom_through_test_wall",
                        ActionId = "phantom_through_test_wall",
                        CreatedAtTu = 0,
                        ExpiresAtTu = 100,
                        Feature = BattleEdgeFeatureState.MakeWall(),
                    },
                    refreshExisting: false,
                    maxActiveEdges: 0
                ),
                "测试前提：路径临时墙应可写入。"
            );
            BattleCommand wallCommand = BuildCommand(wallCaster, wallTarget);
            BattlePreview wallPreview = wallFixture.Runtime.PreviewCommand(wallCommand);
            _test.True(
                wallPreview != null && !wallPreview.allowed,
                $"路径墙体必须在付费前拒绝。logs={JoinLogs(wallPreview)}"
            );
            int wallAuraBefore = wallCaster.GetCurrentAura();
            BattleEventBatch wallBatch = wallFixture.Runtime.IssueCommand(wallCommand);
            _test.Eq(wallCaster.GetCurrentAura(), wallAuraBefore, "路径墙体不得扣斗气。" );
            _test.Eq(wallCaster.GetCooldownTyped(SkillId), 0, "路径墙体不得启动冷却。" );
            wallBatch?.Dispose();
            Dispose(wallCommand, wallPreview);
        }

        BattleUnitState longRangeWeaponCaster = BuildReadyCaster(
            "long_range_weapon_caster",
            new Vector2I(1, 1),
            9,
            weaponRange: 3
        );
        AssertRejected(
            skill,
            longRangeWeaponCaster,
            BuildUnit("long_range_weapon_target", "enemy", new Vector2I(6, 1)),
            Array.Empty<BattleUnitState>(),
            "武器攻击距离超过2"
        );

        BattleUnitState insufficientAura = BuildReadyCaster(
            "insufficient_aura_caster",
            new Vector2I(1, 1),
            9,
            aura: 2999
        );
        AssertRejected(
            skill,
            insufficientAura,
            BuildUnit("insufficient_aura_target", "enemy", new Vector2I(6, 1)),
            Array.Empty<BattleUnitState>(),
            "斗气不足"
        );
    }

    private void TestLevelNineExecutionUsesFiveWeaponDice(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("execute_caster", new Vector2I(1, 2), 9);
        BattleUnitState middleOne = BuildUnit("execute_middle_1", "enemy", new Vector2I(4, 2));
        BattleUnitState middleTwo = BuildUnit("execute_middle_2", "enemy", new Vector2I(7, 2));
        BattleUnitState primary = BuildUnit("execute_primary", "enemy", new Vector2I(11, 2));
        var masteryGateway = new MasteryGatewayStub();
        using BattleTestFixture fixture = CreateFixtureWithGateway(
            skill,
            caster,
            primary,
            masteryGateway,
            middleOne,
            middleTwo
        );
        var damageProbe = new LineThroughDamageProbe();
        ConfigureOutcomes(fixture, damageProbe, new[] { true, true, true });
        BattleCommand command = BuildCommand(caster, primary);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(damageProbe.TargetIds.Count, 3, "应执行三次独立武器攻击。" );
        AssertSequence(damageProbe.WeaponDiceMultipliers, new[] { 1, 1, 5 }, "两次铺垫成功后终点必须为5W。" );
        AssertSequence(damageProbe.SituationalAttackBonuses, new[] { 0, 0, 3 }, "途中无修正，终点攻击检定必须+3。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(12, 2), "终点命中后必须落到敌后。" );
        _test.Eq(caster.GetCurrentAura(), 2000, "执行后应准确支付3000斗气。" );
        _test.Eq(caster.GetCurrentAp(), 1, "执行后应支付1AP。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 100, "执行后应进入100TU冷却。" );
        _test.Eq(masteryGateway.Grants.Count, 1, "整次技能只能提交一笔本技能熟练度。" );
        _test.Eq(
            masteryGateway.Grants[0].MasteryAmount,
            1,
            "两次途中命中不得重复记录本技能熟练度，只有终点主攻击计1个普通目标。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestIntermediateMissDoesNotStop(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("continue_caster", new Vector2I(1, 2), 9);
        BattleUnitState middleOne = BuildUnit("continue_middle_1", "enemy", new Vector2I(4, 2));
        BattleUnitState middleTwo = BuildUnit("continue_middle_2", "enemy", new Vector2I(7, 2));
        BattleUnitState primary = BuildUnit("continue_primary", "enemy", new Vector2I(11, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, primary, middleOne, middleTwo);
        var damageProbe = new LineThroughDamageProbe();
        ConfigureOutcomes(fixture, damageProbe, new[] { false, true, true });
        BattleCommand command = BuildCommand(caster, primary);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(damageProbe.TargetIds.Count, 3, "首个途中攻击未命中后仍必须继续攻击。" );
        AssertSequence(damageProbe.WeaponDiceMultipliers, new[] { 1, 1, 4 }, "只有一次途中成功时终点应为4W。" );
        AssertSequence(damageProbe.SituationalAttackBonuses, new[] { 0, 0, 2 }, "一次途中成功时终点攻击检定应为+2。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(12, 2), "终点命中后仍应完成位移。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestPrimaryMissPreventsLanding(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("miss_caster", new Vector2I(1, 2), 9);
        BattleUnitState middleOne = BuildUnit("miss_middle_1", "enemy", new Vector2I(4, 2));
        BattleUnitState middleTwo = BuildUnit("miss_middle_2", "enemy", new Vector2I(7, 2));
        BattleUnitState primary = BuildUnit("miss_primary", "enemy", new Vector2I(11, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, primary, middleOne, middleTwo);
        var damageProbe = new LineThroughDamageProbe();
        ConfigureOutcomes(fixture, damageProbe, new[] { true, true, false });
        BattleCommand command = BuildCommand(caster, primary);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        AssertSequence(damageProbe.WeaponDiceMultipliers, new[] { 1, 1, 5 }, "终点未命中前仍应按5W发起主攻击。" );
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 2), "终点未命中时不得位移。" );
        _test.Eq(caster.GetCurrentAura(), 2000, "终点未命中仍必须支付3000斗气。" );
        _test.True(batch?.LogLinesTyped.Any(line => line.Contains("位移不发生")) == true, "日志应公开未命中不位移。" );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestAiUsesCanonicalLinePlan(SkillDefinition skill)
    {
        BattleUnitState caster = BuildReadyCaster("ai_caster", new Vector2I(1, 2), 9);
        BattleUnitState middle = BuildUnit("ai_middle", "enemy", new Vector2I(5, 2));
        BattleUnitState primary = BuildUnit("ai_primary", "enemy", new Vector2I(9, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, primary, middle);
        int scoredCandidates = 0;
        int primaryCandidates = 0;
        BattleAiContext context = BuildAiContext(
            fixture,
            caster,
            skill,
            (_, preview) =>
            {
                scoredCandidates++;
                _test.Eq(preview.hit_preview?.Source, "line_through_attack", "AI必须消费canonical穿身预览。" );
                if (preview.TargetUnitIdsTyped.LastOrDefault() != primary.unit_id)
                    return;
                primaryCandidates++;
                _test.Eq(preview.resolved_anchor_coord, new Vector2I(10, 2), "以远端敌人为终点时AI必须看到其敌后落点。" );
                _test.Eq(preview.TargetUnitIdsTyped.Count, 2, "以远端敌人为终点时AI必须看到途中与终点两个受击单位。" );
            }
        );
        BattleAiDecision decision = new BattleAiUnitSkillCandidateEvaluator().Evaluate(
            BuildAiAction(),
            context
        );
        _test.True(scoredCandidates >= 1, "至少一个合法终点应进入AI评分。" );
        _test.Eq(primaryCandidates, 1, "远端敌人应形成一个canonical终点候选。" );
        _test.True(decision?.command != null, "AI应能选择合法穿身攻击。" );
    }

    private void TestAiExpectedDamageAndAuraNormalization(SkillDefinition skill)
    {
        CombatLineThroughAttackDefinition profile = skill.CombatProfile.LineThroughAttack;
        var preview = new AttackPreviewData
        {
            Stages = new List<AttackPreviewStage>
            {
                new(100, 100, 100, 2, 2, "100%", 10000, 100),
                new(100, 100, 100, 2, 2, "100%", 10000, 100),
                new(100, 100, 100, 2, 2, "100%", 0, 300),
                new(100, 100, 100, 2, 2, "100%", 0, 400),
                new(100, 100, 100, 2, 2, "100%", 10000, 500),
            },
        };
        IReadOnlyList<CombatEffectDefinition> expectedPrimary =
            BattleAiScoreService.BuildLineThroughAttackExpectedEffects(
                skill.CombatProfile.EffectDefinitions,
                profile,
                9,
                2,
                3,
                preview
            );
        CombatEffectDefinition damage = expectedPrimary.Single(
            effect => effect.EffectKind == BattleEffectKind.Damage
        );
        _test.Eq(damage.WeaponDiceMultiplier, 1, "AI期望值应以1W基准归一。" );
        _test.True(Math.Abs(damage.PreResistanceDamageMultiplier - 5.0) < 0.0001, "AI应把两次铺垫后的终点成功伤害估为5W。" );

        BattleUnitState actor = BuildUnit("aura_score_actor", "enemy", Vector2I.Zero);
        actor.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AuraMax),
            10000
        );
        actor.SetCurrentAura(10000);
        _test.Eq(BattleAiScoreService.BuildNormalizedAuraCostPercent(actor, 3000), 30, "3000/10000斗气应按30%评分。" );
        actor.SetCurrentAura(5000);
        actor.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AuraMax),
            5000
        );
        _test.Eq(BattleAiScoreService.BuildNormalizedAuraCostPercent(actor, 3000), 60, "3000/5000斗气应按60%评分。" );
        BattleTestFixture.DisposeBattleUnit(actor);
    }

    private void AssertRejected(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState primary,
        IReadOnlyList<BattleUnitState> otherUnits,
        string label
    )
    {
        var enemies = new List<BattleUnitState>();
        var allies = new List<BattleUnitState> { caster };
        if (primary.faction_id == caster.faction_id)
            allies.Add(primary);
        else
            enemies.Add(primary);
        foreach (BattleUnitState unit in otherUnits ?? Array.Empty<BattleUnitState>())
        {
            if (unit.faction_id == caster.faction_id)
                allies.Add(unit);
            else
                enemies.Add(unit);
        }
        using BattleTestFixture fixture = CreateFixture(skill, allies, enemies);
        BattleCommand command = BuildCommand(caster, primary);
        int apBefore = caster.GetCurrentAp();
        int auraBefore = caster.GetCurrentAura();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, $"{label}时预览必须拒绝。logs={JoinLogs(preview)}" );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), apBefore, $"{label}不得扣AP。" );
        _test.Eq(caster.GetCurrentAura(), auraBefore, $"{label}不得扣斗气。" );
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{label}不得启动冷却。" );
        batch?.Dispose();
        Dispose(command, preview);
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
            skill_cast_block_reason_callback = (_, _) => BattleSkillCastBlockReasonKind.None,
            preview_command_callback = fixture.Runtime.PreviewCommand,
            skill_score_input_callback = (_, _, command, preview, _, _, _) =>
            {
                onScore?.Invoke(command, preview);
                return new BattleAiScoreInput
                {
                    command = command,
                    preview = preview,
                    effective_target_count = preview.TargetUnitIdsTyped.Count,
                    enemy_target_count = preview.TargetUnitIdsTyped.Count,
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
            "phantom_through_ai",
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

    private static CombatSkillDef BuildSchemaProfile()
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
            skill_id = "line_through_schema_probe",
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            min_target_count = 1,
            max_target_count = 1,
            max_hits_per_target = 1,
            range_value = 10,
            weapon_range_policy = "configured",
            requires_los = true,
            allows_natural_weapon = false,
            line_through_attack_profile = new CombatLineThroughAttackDef
            {
                maximum_weapon_range = 2,
                intermediate_weapon_dice_multiplier = 1,
                primary_weapon_dice_multiplier_curve = new[] { 2 },
                primary_attack_roll_bonus_curve = new[] { 0 },
                successful_intermediate_hit_bonus_weapon_dice = 1,
                successful_intermediate_hit_attack_roll_bonus = 1,
                successful_intermediate_hit_bonus_cap_curve = new[] { 0 },
            },
        };
        profile.effect_defs.Add(damage);
        return profile;
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "warrior_phantom_through_regression"
        );

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState primary,
        params BattleUnitState[] otherUnits
    )
    {
        var allies = new List<BattleUnitState> { caster };
        var enemies = new List<BattleUnitState> { primary };
        foreach (BattleUnitState unit in otherUnits ?? Array.Empty<BattleUnitState>())
        {
            if (unit.faction_id == caster.faction_id)
                allies.Add(unit);
            else
                enemies.Add(unit);
        }
        return CreateFixture(skill, allies, enemies, null);
    }

    private static BattleTestFixture CreateFixtureWithGateway(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState primary,
        IBattleRuntimeCharacterGateway gateway,
        params BattleUnitState[] otherUnits
    )
    {
        var allies = new List<BattleUnitState> { caster };
        var enemies = new List<BattleUnitState> { primary };
        foreach (BattleUnitState unit in otherUnits ?? Array.Empty<BattleUnitState>())
        {
            if (unit.faction_id == caster.faction_id)
                allies.Add(unit);
            else
                enemies.Add(unit);
        }
        return CreateFixture(skill, allies, enemies, gateway);
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        IReadOnlyList<BattleUnitState> allies,
        IReadOnlyList<BattleUnitState> enemies,
        IBattleRuntimeCharacterGateway gateway = null
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "warrior_phantom_through",
            new Vector2I(15, 5),
            allies,
            enemies
        );
        fixture.Runtime.setup(
            gateway,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = allies[0].unit_id;
        return fixture;
    }

    private static void ConfigureOutcomes(
        BattleTestFixture fixture,
        LineThroughDamageProbe damageProbe,
        IReadOnlyList<bool> outcomes
    )
    {
        fixture.Runtime.ConfigureDamageResolverForTests(damageProbe);
        fixture.Runtime.ConfigureHitResolverForTests(new SequenceHitResolver(outcomes));
    }

    private static BattleUnitState BuildReadyCaster(
        StringName id,
        Vector2I coord,
        int skillLevel,
        int aura = 5000,
        int weaponRange = 1
    )
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.source_member_id = $"member_{id}";
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, skillLevel);
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 10);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 10);
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AuraMax),
            5000
        );
        caster.SetCurrentAura(aura);
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        caster.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "phantom_test_longsword",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = "one_handed",
                weapon_attack_range = weaponRange,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 10 },
                weapon_physical_damage_tag = "physical_slash",
            }
        );
        return caster;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
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
        unit.SetCurrentMovePoints(3);
        return unit;
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, BattleUnitState target) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static bool ErrorsContain(IEnumerable<string> errors, string needle) =>
        (errors ?? Array.Empty<string>()).Any(
            error => error?.Contains(needle, StringComparison.Ordinal) == true
        );

    private void AssertSequence(
        IReadOnlyList<int> actual,
        IReadOnlyList<int> expected,
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
                if (actual[index] == expected[index])
                    continue;
                equal = false;
                break;
            }
        }
        _test.True(
            equal,
            $"{message} actual=[{string.Join(",", actual ?? Array.Empty<int>())}] expected=[{string.Join(",", expected ?? Array.Empty<int>())}]"
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

    private sealed class LineThroughDamageProbe : FixedHitMaxDamageResolver
    {
        internal List<StringName> TargetIds { get; } = new();
        internal List<int> WeaponDiceMultipliers { get; } = new();
        internal List<int> SituationalAttackBonuses { get; } = new();

        internal override AttackEffectResolutionResult ResolveAttackEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            TargetIds.Add(targetUnit?.unit_id ?? new StringName(""));
            SituationalAttackBonuses.Add(attackCheck.SituationalAttackBonus);
            CombatEffectDefinition damage = (effectDefinitions ?? Array.Empty<CombatEffectDefinition>())
                .FirstOrDefault(effect => effect?.EffectKind == BattleEffectKind.Damage);
            WeaponDiceMultipliers.Add(damage?.WeaponDiceMultiplier ?? 0);
            return base.ResolveAttackEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                attackCheck,
                attackContext
            );
        }
    }

    private sealed class MasteryGatewayStub : IBattleRuntimeCharacterGateway
    {
        internal List<CharacterMasteryChangeFact> Grants { get; } = new();

        public PartyState GetPartyState() => null;
        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDefinition>();
        public bool HasItemDefCatalog() => false;
        public ItemDefinition GetItemDef(StringName itemId) => null;
        public PartyMemberState GetMemberState(StringName memberId) => null;
        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName memberId,
            EquipmentState equipmentView
        ) => null;
        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName memberId,
            EquipmentState equipmentView
        ) => new();
        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
            StringName memberId,
            EquipmentState equipmentView
        ) => BattleEffectiveTraitProjection.Empty;
        public PassiveSourceContext BuildPassiveSourceContext(
            StringName memberId,
            UnitProgress progressionState
        ) => null;
        public CharacterProgressionDelta PromoteProfession(
            StringName memberId,
            StringName professionId,
            PromotionCommitRequest selection
        ) => new() { member_id = memberId };
        public BattleResourceCommitResult CommitBattleResources(
            StringName memberId,
            int currentHp,
            int currentMp,
            int currentAura
        ) => BattleResourceCommitResult.Success(memberId);
        public ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(
            StringName memberId,
            IReadOnlyCollection<StringName> consumedSetupIds
        ) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
            StringName memberId,
            IReadOnlyCollection<StringName> consumedSetupIds
        ) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public void CommitBattleDeath(StringName memberId) { }
        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName memberId,
            StringName skillId,
            int amount
        ) => RecordGrant(memberId, skillId, amount, "battle");

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType,
            string sourceLabel,
            string reasonText,
            bool emitAchievementEvent
        ) => RecordGrant(memberId, skillId, amount, sourceType);

        public IReadOnlyList<StringName> RecordAchievementEvent(
            StringName memberId,
            StringName eventType,
            int amount
        ) => Array.Empty<StringName>();

        public IReadOnlyList<StringName> RecordAchievementEvent(
            StringName memberId,
            StringName eventType,
            int amount,
            StringName subjectId,
            GDictionary meta
        ) => Array.Empty<StringName>();

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName memberId,
            StringName sourceType,
            string sourceLabel,
            IEnumerable<PendingCharacterRewardEntry> entryOptions,
            string summaryText
        ) => null;

        private CharacterProgressionDelta RecordGrant(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType
        )
        {
            var change = new CharacterMasteryChangeFact(
                skillId,
                skillId.ToString(),
                amount,
                sourceType,
                sourceType.ToString(),
                ""
            );
            Grants.Add(change);
            var delta = new CharacterProgressionDelta { member_id = memberId };
            delta.AddMasteryChange(change);
            return delta;
        }
    }
}
