using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_archer_breach_barrage_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "archer_breach_barrage";
    private const string SkillPath = "res://data/configs/skills/archer_breach_barrage.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestAuthoredSchemaValidation();
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestStaminaFormula(skill);
            TestNoTargetCapUsesFullWeaponRange(skill);
            TestOrderedHeightChannelPlan(skill);
            TestPreviewExecutionFriendlyFireAndDecay(skill);
            TestMissDoesNotAdvanceDecay(skill);
            TestSuccessfulCheckWithZeroHpDamageAdvancesDecay(skill);
            TestAllMissesStillConsumeCosts(skill);
            TestLosBlockerStopsPath(skill);
            TestInvalidAndEmptyDirectionsRejectBeforeCost(skill);
            TestAiEnumeratesCardinalDirectionsAndUsesCanonicalPreview(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Archer breach barrage regression"));
    }

    private void TestAuthoredSchemaValidation()
    {
        SkillDef authored = ResourceLoader.Load<SkillDef>(
            SkillPath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        _test.True(authored != null, "贯阵一矢正式资源必须可加载并进入 schema 校验。" );
        if (authored == null)
            return;
        GodotContentOwnership.RegisterBorrowedContent(
            authored,
            "archer_breach_barrage_schema_regression"
        );
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        var validErrors = new Godot.Collections.Array<string>();
        validator.AppendCombatProfileValidationErrors(
            validErrors,
            authored.skill_id,
            authored.combat_profile,
            authored
        );
        _test.Eq(
            validErrors.Count,
            0,
            $"贯阵一矢正式资源必须通过 directional piercing schema。errors={string.Join(" | ", validErrors)}"
        );

        authored.combat_profile.target_team_filter = "enemy";
        var invalidErrors = new Godot.Collections.Array<string>();
        validator.AppendCombatProfileValidationErrors(
            invalidErrors,
            authored.skill_id,
            authored.combat_profile,
            authored
        );
        _test.True(
            invalidErrors.Any(error => error.Contains("target_team_filter any")),
            $"破坏友军误伤契约后必须由 schema fail closed。errors={string.Join(" | ", invalidErrors)}"
        );
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill != null, "贯阵一矢资源必须可投影。" );
        CombatSkillDefinition combat = skill?.CombatProfile;
        CombatDirectionalPiercingDefinition profile = combat?.DirectionalPiercing;
        _test.True(combat != null, "贯阵一矢必须有战斗配置。" );
        _test.True(profile != null, "贯穿规则必须由typed profile声明。" );
        if (skill == null || combat == null || profile == null)
            return;

        _test.Eq(skill.DisplayName, "贯阵一矢", "名称必须反映单箭贯穿。" );
        _test.Eq(skill.MaxLevel, 7, "核心等级上限必须为7。" );
        _test.Eq(skill.NonCoreMaxLevel, 5, "非核心等级上限必须为5。" );
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Ground, "玩家必须选择地面方向。" );
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Any, "贯穿必须允许友军误伤。" );
        _test.Eq(combat.RangeValue, 0, "射程必须来自当前弓。" );
        _test.True(combat.RequiresLos, "阻挡视线的边缘必须能截断箭矢。" );
        _test.True(
            combat.RequiredWeaponFamilies.SequenceEqual(new[] { new StringName("bow") }),
            "武器门禁必须精确要求弓。"
        );
        _test.False(combat.AllowsNaturalWeapon, "天生武器不能替代弓。" );
        _test.Eq(profile.SuccessfulHitDecayPercent, 20, "每次成功命中衰减20%。" );
        _test.Eq(profile.MinimumDamagePercent, 40, "衰减下限必须为40%。" );
        _test.Eq(profile.MaximumHeightDelta, 1, "高度差上限必须为1。" );
        int[] expectedDamage = { 200, 200, 220, 240, 260, 280, 290, 300 };
        for (int level = 0; level <= 7; level++)
        {
            _test.Eq(
                profile.GetBaseDamagePercent(level),
                expectedDamage[level],
                $"L{level}基础伤害倍率不符。"
            );
        }
        AssertLevel(combat, 0, -2, 120);
        AssertLevel(combat, 1, -2, 120);
        AssertLevel(combat, 2, -1, 120);
        AssertLevel(combat, 3, -1, 120);
        AssertLevel(combat, 4, 0, 120);
        AssertLevel(combat, 5, 0, 110);
        AssertLevel(combat, 6, 1, 110);
        AssertLevel(combat, 7, 1, 100);

        _test.Eq(combat.EffectDefinitions.Count, 1, "技能只能携带一个标准武器伤害效果。" );
        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.True(damage.AddWeaponDice, "伤害必须读取当前武器骰。" );
        _test.True(damage.ResolveAsWeaponAttack, "每个目标必须独立进行标准武器攻击。" );
        _test.True(damage.UseWeaponPhysicalDamageTag, "伤害类型必须来自弓。" );
        _test.Eq(damage.Power, 0, "非魔法贯穿技不得配置固定伤害。" );
        _test.True(skill.Description.Contains("友军与中立单位也会受击"), "描述必须披露友军误伤。" );
        _test.True(skill.Description.Contains("玩家不能选择高度方向"), "描述必须披露自动高度锁定。" );
        _test.True(skill.Description.Contains("B=32+R²"), "描述必须披露动态体力公式。" );
    }

    private void TestStaminaFormula(SkillDefinition skill)
    {
        CombatDirectionalPiercingDefinition profile = skill.CombatProfile.DirectionalPiercing;
        _test.Eq(BattleDirectionalPiercingRules.CalculateStaminaCost(profile, 6, 0), 68, "R6/S0体力应为68。" );
        _test.Eq(BattleDirectionalPiercingRules.CalculateStaminaCost(profile, 6, 10), 34, "R6/S10体力应为34。" );
        _test.Eq(BattleDirectionalPiercingRules.CalculateStaminaCost(profile, 6, 20), 14, "R6/S20应使用非线性平方减耗。" );
        _test.Eq(BattleDirectionalPiercingRules.CalculateStaminaCost(profile, 6, -2), 71, "负力量调整值应平方增耗并向上取整。" );
        _test.Eq(BattleDirectionalPiercingRules.CalculateStaminaCost(profile, 15, 20), 52, "长射程与高力量都必须按平方项计算。" );
        _test.True(
            Math.Abs(
                BattleDirectionalPiercingRules.GetExpectedDecayMultiplier(profile, 3, 50)
                    - 0.7
            ) < 0.0001,
            "AI四状态DP必须给出前三目标50%命中率下的期望衰减0.7。"
        );
        var alternateProfile = new CombatDirectionalPiercingDefinition(
            new[] { 100 },
            successfulHitDecayPercent: 10,
            minimumDamagePercent: 40,
            staminaFlatBase: 32,
            staminaRangeSquareCoefficient: 1,
            staminaStrengthSquareScale: 100,
            minimumStaminaCost: 1,
            maximumHeightDelta: 1
        );
        _test.True(
            Math.Abs(
                BattleDirectionalPiercingRules.GetExpectedDecayMultiplier(
                    alternateProfile,
                    priorTargetCount: 6,
                    hitRatePercent: 100
                ) - 0.4
            ) < 0.0001,
            "AI DP状态数必须随profile衰减参数扩展，不能把四状态写死。"
        );
    }

    private void TestNoTargetCapUsesFullWeaponRange(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher(
            "long_range_archer",
            "player",
            Vector2I.Zero,
            1,
            15,
            0
        );
        var targets = new List<BattleUnitState>();
        for (int x = 1; x <= 15; x++)
            targets.Add(BuildUnit($"long_range_target_{x}", "enemy", new Vector2I(x, 0)));
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "breach_no_target_cap",
            new Vector2I(16, 1),
            new[] { archer },
            targets
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            fixture.State,
            fixture.Runtime._grid_service,
            archer,
            skill,
            Vector2I.Right,
            15
        );
        _test.True(plan.Allowed, $"15格弓的完整直线路径应允许。message={plan.Message}" );
        _test.Eq(plan.PathCoords.Count, 15, "路径必须覆盖完整15格射程。" );
        _test.Eq(plan.Targets.Count, 15, "贯穿目标数量不得设置上限。" );
    }

    private void TestOrderedHeightChannelPlan(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("plan_archer", "player", new Vector2I(1, 1), 1, 6, 0);
        BattleUnitState deadLow = BuildUnit("plan_dead_low", "enemy", new Vector2I(2, 1));
        BattleUnitState same = BuildUnit("plan_same", "enemy", new Vector2I(3, 1));
        BattleUnitState tooHigh = BuildUnit("plan_too_high", "enemy", new Vector2I(4, 1));
        BattleUnitState high = BuildUnit("plan_high", "enemy", new Vector2I(5, 1));
        BattleUnitState low = BuildUnit("plan_low", "enemy", new Vector2I(6, 1));
        BattleUnitState highAgain = BuildUnit("plan_high_again", "enemy", new Vector2I(7, 1));
        deadLow.SetCurrentHp(0);
        using BattleTestFixture fixture = CreateFixture(
            "breach_plan",
            skill,
            new[] { archer },
            deadLow,
            same,
            tooHigh,
            high,
            low,
            highAgain
        );
        SetHeight(fixture, deadLow.GetAnchorCoord(), -1);
        SetHeight(fixture, high.GetAnchorCoord(), 1);
        SetHeight(fixture, low.GetAnchorCoord(), -1);
        SetHeight(fixture, highAgain.GetAnchorCoord(), 1);
        SetHeight(fixture, tooHigh.GetAnchorCoord(), 2);

        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            fixture.State,
            fixture.Runtime._grid_service,
            archer,
            skill,
            archer.GetAnchorCoord() + Vector2I.Right,
            6
        );
        _test.True(plan.Allowed, "存在同层/+1层目标的路径应允许。" );
        _test.Eq(plan.LockedHeightSign, 1, "死者与超高目标不得锁定通道，首个合格的+1目标必须锁定。" );
        AssertIds(
            plan.Targets.Select(target => target.unit_id),
            same.unit_id,
            high.unit_id,
            highAgain.unit_id
        );
        _test.Eq(plan.SkippedTargets.Count, 2, "超高目标与相反通道都应被跳过。" );
        _test.Eq(plan.SkippedTargets[0].UnitId, tooHigh.unit_id, "+2层目标应被跳过。" );
        _test.Eq(
            plan.SkippedTargets[0].Reason,
            BattleDirectionalPiercingSkipReason.HeightOutOfRange,
            "超过一层必须有typed跳过原因。"
        );
        _test.Eq(plan.SkippedTargets[1].UnitId, low.unit_id, "-1层目标应因相反通道跳过。" );
        _test.Eq(
            plan.SkippedTargets[1].Reason,
            BattleDirectionalPiercingSkipReason.OppositeHeightChannel,
            "相反高度必须有typed跳过原因。"
        );
    }

    private void TestPreviewExecutionFriendlyFireAndDecay(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("runtime_archer", "player", new Vector2I(1, 1), 1, 6, 0);
        BattleUnitState ally = BuildUnit("runtime_ally", "player", new Vector2I(2, 1));
        BattleUnitState neutral = BuildUnit("runtime_neutral", "neutral", new Vector2I(3, 1));
        BattleUnitState skipped = BuildUnit("runtime_skipped", "enemy", new Vector2I(4, 1));
        BattleUnitState enemy2 = BuildUnit("runtime_enemy_2", "enemy", new Vector2I(5, 1));
        using BattleTestFixture fixture = CreateFixture(
            "breach_runtime",
            skill,
            new[] { archer, ally },
            neutral,
            skipped,
            enemy2
        );
        SetHeight(fixture, ally.GetAnchorCoord(), 1);
        SetHeight(fixture, skipped.GetAnchorCoord(), -1);
        SetHeight(fixture, enemy2.GetAnchorCoord(), 1);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));

        BattleCommand command = BuildCommand(archer, Vector2I.Right);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"正式预览应允许。logs={JoinLogs(preview)}" );
        AssertIds(preview.TargetUnitIdsTyped, ally.unit_id, neutral.unit_id, enemy2.unit_id);
        _test.True(ContainsLog(preview, "同层/+1层"), "预览必须展示自动锁定的高度通道。" );
        _test.True(ContainsLog(preview, "跳过 runtime_skipped"), "预览必须展示被跳过单位。" );
        _test.True(ContainsLog(preview, "本次消耗 68"), "预览必须展示按完整R6计算的动态体力。" );

        int allyHp = ally.GetCurrentHp();
        int neutralHp = neutral.GetCurrentHp();
        int skippedHp = skipped.GetCurrentHp();
        int enemy2Hp = enemy2.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        string batchLogs = string.Join(" | ", batch.log_lines);
        _test.Eq(allyHp - ally.GetCurrentHp(), 20, $"首个友军应承受200%武器伤害。logs={batchLogs}" );
        _test.Eq(neutralHp - neutral.GetCurrentHp(), 16, $"中立单位必须受击，第二次成功命中应承受160%武器伤害。logs={batchLogs}" );
        _test.Eq(skipped.GetCurrentHp(), skippedHp, "相反高度通道不得进行攻击检定或受伤。" );
        _test.Eq(enemy2Hp - enemy2.GetCurrentHp(), 12, $"第三次成功命中应承受120%武器伤害。logs={batchLogs}" );
        _test.Eq(archer.GetCurrentAp(), 0, "成功施放必须消耗2AP。" );
        _test.Eq(archer.GetCurrentStamina(), 132, "R6/S0必须消耗68体力。" );
        _test.Eq(archer.GetCooldownTyped(SkillId), 120, "L1必须启动120TU冷却。" );
        Dispose(command, preview);
    }

    private void TestMissDoesNotAdvanceDecay(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("miss_archer", "player", new Vector2I(1, 1), 1, 6, 0);
        BattleUnitState first = BuildUnit("miss_first", "enemy", new Vector2I(2, 1));
        BattleUnitState opposite = BuildUnit("miss_opposite", "enemy", new Vector2I(3, 1));
        BattleUnitState second = BuildUnit("miss_second", "enemy", new Vector2I(4, 1));
        BattleUnitState third = BuildUnit("miss_third", "enemy", new Vector2I(5, 1));
        using BattleTestFixture fixture = CreateFixture(
            "breach_miss",
            skill,
            new[] { archer },
            first,
            opposite,
            second,
            third
        );
        SetHeight(fixture, first.GetAnchorCoord(), 1);
        SetHeight(fixture, opposite.GetAnchorCoord(), -1);
        SetHeight(fixture, third.GetAnchorCoord(), 1);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        var hitProbe = new FirstMissThenHitProbe();
        fixture.Runtime.ConfigureHitResolverForTests(hitProbe);
        BattleCommand command = BuildCommand(archer, Vector2I.Right);
        int firstHp = first.GetCurrentHp();
        int oppositeHp = opposite.GetCurrentHp();
        int secondHp = second.GetCurrentHp();
        int thirdHp = third.GetCurrentHp();

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(first.GetCurrentHp(), firstHp, "首个未命中不得造成伤害。" );
        _test.Eq(opposite.GetCurrentHp(), oppositeHp, "首个+1目标即使未命中也不得解除通道锁，-1目标必须跳过。" );
        string batchLogs = string.Join(" | ", batch.log_lines);
        _test.Eq(secondHp - second.GetCurrentHp(), 20, $"未命中不得推进衰减，第二目标仍应为200%。logs={batchLogs}" );
        _test.Eq(thirdHp - third.GetCurrentHp(), 16, $"第二目标命中后第三目标才衰减到160%。logs={batchLogs}" );
        _test.Eq(hitProbe.CallCount, 3, "三个合格目标必须各进行一次独立攻击检定。" );
        Dispose(command);
    }

    private void TestSuccessfulCheckWithZeroHpDamageAdvancesDecay(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher(
            "shield_archer",
            "player",
            new Vector2I(1, 1),
            1,
            6,
            0
        );
        BattleUnitState shielded = BuildUnit(
            "shield_first",
            "enemy",
            new Vector2I(2, 1)
        );
        BattleUnitState second = BuildUnit("shield_second", "enemy", new Vector2I(3, 1));
        shielded.ReplaceShieldStateTyped(
            100,
            100,
            100,
            "breach_test_shield",
            shielded.unit_id,
            "breach_test_shield"
        );
        using BattleTestFixture fixture = CreateFixture(
            "breach_zero_hp_damage",
            skill,
            new[] { archer },
            shielded,
            second
        );
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        int shieldedHp = shielded.GetCurrentHp();
        int secondHp = second.GetCurrentHp();
        BattleCommand command = BuildCommand(archer, Vector2I.Right);

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        string batchLogs = string.Join(" | ", batch.log_lines);
        _test.Eq(
            shielded.GetCurrentHp(),
            shieldedHp,
            $"首个攻击检定成功但由护盾吸收时HP伤害必须为0。logs={batchLogs}"
        );
        _test.Eq(
            secondHp - second.GetCurrentHp(),
            16,
            $"成功检定即使没有造成HP伤害也必须推进衰减，第二目标应为160%。logs={batchLogs}"
        );
        Dispose(command);
    }

    private void TestAllMissesStillConsumeCosts(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher(
            "all_miss_archer",
            "player",
            new Vector2I(1, 1),
            1,
            6,
            0
        );
        BattleUnitState target = BuildUnit("all_miss_target", "enemy", new Vector2I(2, 1));
        using BattleTestFixture fixture = CreateFixture(
            "breach_all_miss",
            skill,
            new[] { archer },
            target
        );
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        int targetHp = target.GetCurrentHp();
        BattleCommand command = BuildCommand(archer, Vector2I.Right);

        using (BattleEventBatch batch = fixture.Runtime.IssueCommand(command)) { }
        _test.Eq(target.GetCurrentHp(), targetHp, "全部未命中时目标HP不得变化。" );
        _test.Eq(archer.GetCurrentAp(), 0, "全部未命中仍必须消耗2AP。" );
        _test.Eq(archer.GetCurrentStamina(), 132, "全部未命中仍必须消耗动态体力68。" );
        _test.Eq(archer.GetCooldownTyped(SkillId), 120, "全部未命中仍必须启动完整冷却。" );
        Dispose(command);
    }

    private void TestLosBlockerStopsPath(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("wall_archer", "player", new Vector2I(1, 1), 1, 6, 0);
        BattleUnitState beforeWall = BuildUnit("wall_before", "enemy", new Vector2I(3, 1));
        BattleUnitState afterWall = BuildUnit("wall_after", "enemy", new Vector2I(5, 1));
        using BattleTestFixture fixture = CreateFixture(
            "breach_wall",
            skill,
            new[] { archer },
            beforeWall,
            afterWall
        );
        fixture.Runtime._grid_service.SetEdgeFeature(
            fixture.State,
            new Vector2I(3, 1),
            Vector2I.Right,
            BattleEdgeFeatureState.MakeWall()
        );
        SetHeight(fixture, afterWall.GetAnchorCoord(), 1);
        BattleCommand command = BuildCommand(archer, Vector2I.Right);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview.allowed, "墙前存在目标时仍应允许施放。" );
        AssertIds(preview.TargetUnitIdsTyped, beforeWall.unit_id);
        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            fixture.State,
            fixture.Runtime._grid_service,
            archer,
            skill,
            archer.GetAnchorCoord() + Vector2I.Right,
            6
        );
        _test.Eq(plan.LockedHeightSign, 0, "墙后的+1层单位不得先于阻挡边锁定高度通道。" );
        _test.True(ContainsLog(preview, "前被阻挡"), "预览必须显示路径被边缘阻挡。" );
        Dispose(command, preview);
    }

    private void TestInvalidAndEmptyDirectionsRejectBeforeCost(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("reject_archer", "player", new Vector2I(2, 2), 1, 6, 0);
        BattleUnitState distantEnemy = BuildUnit("reject_enemy", "enemy", new Vector2I(7, 2));
        using BattleTestFixture fixture = CreateFixture(
            "breach_reject",
            skill,
            new[] { archer },
            distantEnemy
        );
        int ap = archer.GetCurrentAp();
        int stamina = archer.GetCurrentStamina();

        BattleCommand emptyCommand = BuildCommand(archer, Vector2I.Up);
        BattlePreview emptyPreview = fixture.Runtime.PreviewCommand(emptyCommand);
        _test.False(emptyPreview.allowed, "有效路径没有单位时必须在付费前拒绝。" );
        using (BattleEventBatch batch = fixture.Runtime.IssueCommand(emptyCommand)) { }
        _test.Eq(archer.GetCurrentAp(), ap, "空路径拒绝不得消耗AP。" );
        _test.Eq(archer.GetCurrentStamina(), stamina, "空路径拒绝不得消耗体力。" );
        _test.Eq(archer.GetCooldownTyped(SkillId), 0, "空路径拒绝不得启动冷却。" );

        BattleCommand diagonal = BuildCommandToCoord(
            archer,
            archer.GetAnchorCoord() + new Vector2I(1, 1)
        );
        BattlePreview diagonalPreview = fixture.Runtime.PreviewCommand(diagonal);
        _test.False(diagonalPreview.allowed, "斜向选择必须拒绝。" );
        Dispose(emptyCommand, emptyPreview);
        Dispose(diagonal, diagonalPreview);
    }

    private void TestAiEnumeratesCardinalDirectionsAndUsesCanonicalPreview(SkillDefinition skill)
    {
        BattleUnitState archer = BuildArcher("ai_archer", "hostile", new Vector2I(3, 3), 1, 6, 10);
        archer.control_mode = "ai";
        BattleUnitState eastTarget = BuildUnit("ai_east", "player", new Vector2I(5, 3));
        using BattleTestFixture fixture = CreateFixture(
            "breach_ai",
            skill,
            new[] { eastTarget },
            archer
        );
        fixture.Runtime._ensure_ai_action_plan_for_unit(archer);
        fixture.Runtime.TryGetAiActionPlanForUnit(
            archer.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = archer,
            grid_service = fixture.Runtime._grid_service,
            runtime_action_plan = actionPlan,
            preview_command_callback = fixture.Runtime.PreviewCommand,
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime._bind_ai_helper_services_for_decision(archer, context);
        using var action = new UseGroundSkillAction
        {
            action_id = "breach_ai_probe",
            score_bucket_id = "archer_pressure",
            minimum_hit_count = 1,
            maximum_friendly_fire_target_count = 0,
            desired_min_distance = 1,
            desired_max_distance = 6,
            distance_reference = "target_coord",
        };
        action.skill_ids.Add(SkillId);
        BattleAiDecision decision = new BattleAiGroundSkillActionEvaluator().Evaluate(
            (UseGroundSkillActionDefinition)action.ToDefinition(),
            context
        );
        _test.True(decision?.command != null, "AI必须能为贯阵一矢生成候选。" );
        _test.Eq(decision?.command?.skill_id ?? new StringName(""), SkillId, "AI应选择贯阵一矢。" );
        _test.Eq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            archer.GetAnchorCoord() + Vector2I.Right,
            "AI命令必须选择相邻的东方向格，而非远端目标格。"
        );
        if (decision?.command == null)
            return;
        BattlePreview preview = fixture.Runtime.PreviewCommand(decision.command);
        _test.True(preview.allowed, "AI产生的方向命令必须通过同一canonical preview。" );
        AssertIds(preview.TargetUnitIdsTyped, eastTarget.unit_id);
        _test.Eq(decision.score_input?.stamina_cost ?? -1, 34, "AI资源评分必须使用R6/S10的最终动态体力34。" );
        Dispose(decision.command, preview);
    }

    private void AssertLevel(CombatSkillDefinition combat, int level, int attackBonus, int cooldown)
    {
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 2, $"L{level}必须消耗2AP。" );
        _test.Eq(costs.CooldownTu, cooldown, $"L{level}冷却不符。" );
        _test.Eq(combat.GetEffectiveAttackRollBonus(level), attackBonus, $"L{level}攻击加值不符。" );
    }

    private void AssertIds(IEnumerable<StringName> actual, params StringName[] expected)
    {
        var values = new List<StringName>(actual ?? Array.Empty<StringName>());
        _test.Eq(values.Count, expected.Length, $"目标数量不符，actual={string.Join(",", values)}" );
        for (int index = 0; index < Math.Min(values.Count, expected.Length); index++)
            _test.Eq(values[index], expected[index], $"第{index + 1}个目标顺序不符。" );
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(SkillPath, "archer_breach_barrage_regression");

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        IEnumerable<BattleUnitState> allies,
        params BattleUnitState[] enemies
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(10, 7),
            allies,
            enemies
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        BattleUnitState active = allies?.FirstOrDefault(unit => unit.KnowsActiveSkill(SkillId))
            ?? enemies.FirstOrDefault(unit => unit.KnowsActiveSkill(SkillId));
        if (active != null)
            fixture.State.active_unit_id = active.unit_id;
        return fixture;
    }

    private static BattleUnitState BuildArcher(
        StringName id,
        StringName faction,
        Vector2I coord,
        int skillLevel,
        int range,
        int strengthModifier
    )
    {
        BattleUnitState unit = BuildUnit(id, faction, coord);
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, skillLevel, preserveZero: true);
        unit.attribute_snapshot.SetValue("strength_modifier", strengthModifier);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "breach_test_bow",
                weapon_profile_type_id = "breach_test_bow",
                weapon_range_type = "ranged",
                weapon_family = "bow",
                weapon_current_grip = "two_handed",
                weapon_attack_range = range,
                weapon_uses_two_hands = true,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 10 },
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 10 },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
        return unit;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            id,
            faction,
            coord,
            currentAp: 2,
            currentHp: 200
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 200);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.SetCurrentHp(200);
        unit.SetCurrentStamina(200);
        unit.SetCurrentAp(2);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina));
        return unit;
    }

    private static BattleCommand BuildCommand(BattleUnitState archer, Vector2I direction) =>
        BuildCommandToCoord(archer, archer.GetAnchorCoord() + direction);

    private static BattleCommand BuildCommandToCoord(BattleUnitState archer, Vector2I coord)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = archer.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = coord,
        };
        command.AddTargetCoord(coord);
        return command;
    }

    private static void SetHeight(BattleTestFixture fixture, Vector2I coord, int offset) =>
        fixture.Runtime._grid_service.SetHeightOffset(fixture.State, coord, offset);

    private static bool ContainsLog(BattlePreview preview, string fragment) =>
        preview?.LogLinesTyped.Any(line => line.Contains(fragment, StringComparison.Ordinal)) == true;

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static void Dispose(BattleCommand command, BattlePreview preview = null)
    {
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private sealed class FirstMissThenHitProbe : FixedHitResolver
    {
        internal int CallCount { get; private set; }

        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            CallCount++;
            bool success = CallCount > 1;
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
}
