using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_mage_sleep_dust_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "mage_sleep_dust";
    private static readonly StringName SkillId = "mage_sleep_dust";
    private static readonly StringName WakeDamageSkillId = "test_sleep_wake_damage";
    private readonly TestHarness _test = new();

    private sealed class DamageReactionStatusProbe : IBattleEquipmentCombatReactionSink
    {
        internal int ObservationCount { get; private set; }
        internal int BranchLocalObservationCount { get; private set; }
        internal int WakefulObservationCount { get; private set; }
        internal bool SawSleeping { get; private set; }

        public bool ResolveAttackCheck(BattleEquipmentAbilityAttackCheckContext context) => false;

        public BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
            BattleEquipmentAbilityAfterHitContext context
        ) => new();

        public BattleEquipmentAbilityAfterHitResult ResolveHitReceived(
            BattleEquipmentAbilityAfterHitContext context
        ) => new();

        public IReadOnlyList<StringName> RefreshEquipmentProjectionAfterDurabilityDestruction(
            BattleUnitState targetUnit,
            BattleEventBatch batch = null
        ) => Array.Empty<StringName>();

        public bool ResolveDamageApplied(BattleEquipmentAbilityDamageAppliedContext context)
        {
            Observe(context, context?.TargetUnit);
            return false;
        }

        public bool ResolveDamageTakenFinalized(
            BattleEquipmentAbilityDamageAppliedContext context
        )
        {
            Observe(context, context?.SourceUnit);
            return false;
        }

        private void Observe(
            BattleEquipmentAbilityDamageAppliedContext context,
            BattleUnitState damagedUnit
        )
        {
            ObservationCount++;
            if (context?.IsBranchLocalProjection == true)
                BranchLocalObservationCount++;
            SawSleeping = SawSleeping || damagedUnit?.HasStatusEffect("sleeping") == true;
            if (damagedUnit?.HasStatusEffect("wakeful") == true)
                WakefulObservationCount++;
        }
    }

    private sealed class GuaranteedPreviewFatalInterceptArbiter
        : IBattleFatalInterceptArbiter
    {
        public BattleFatalInterceptResult Resolve(BattleFatalInterceptContext context) =>
            BattleFatalInterceptResult.None;

        public BattleFatalInterceptPreviewResult Preview(BattleFatalInterceptContext context)
        {
            if (context?.TargetUnit == null || context.BattleState == null)
                return BattleFatalInterceptPreviewResult.None;

            context.TargetUnit.SetCurrentHp(1);
            return new BattleFatalInterceptPreviewResult
            {
                InterceptProbabilityBasisPoints = 10000,
                GuaranteedIntercept = true,
                ExpectedRecoveryHp = 1,
                ExpectedSurvivalHp = 1,
                ContinuationBranches = new[]
                {
                    new BattleFatalInterceptPreviewBranch
                    {
                        ProbabilityBasisPoints = 10000,
                        BattleState = context.BattleState,
                        SourceUnit = context.SourceUnit,
                        TargetUnit = context.TargetUnit,
                        Intercepted = true,
                    },
                },
            };
        }
    }

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition sleepSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
                SkillPath,
                "mage_sleep_dust_regression"
            );
            TestAuthoredContractAndLevelCurve(sleepSkill);
            TestSleepStateRoundTrip(sleepSkill);
            TestDamageReactionsObservePostWakeState(sleepSkill);
            TestSkippedTurnDamageWakeAntiChainAndNormalTurnConsumption(sleepSkill);
            TestShieldAbsorptionWakesWithoutHpDamage(sleepSkill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Mage sleep dust regression"));
    }

    private void TestAuthoredContractAndLevelCurve(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "沉眠粉雾正式资源与 combat_profile 应可加载。");
        if (skill == null || combat == null)
            return;

        _test.Eq(skill.SkillId, SkillId, "沉眠粉雾 skill_id 应稳定。");
        _test.Eq(combat.TargetModeKind, BattleTargetMode.Ground, "沉眠粉雾应选择地格。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), "沉眠粉雾应使用半径范围。");
        _test.Eq(combat.AreaValue, 1, "沉眠粉雾范围半径应为1格。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "沉眠粉雾不得限制武器家族。");
        _test.Eq(combat.RequiredWeaponTypeIds.Count, 0, "沉眠粉雾不得限制武器类型。");

        int[] expectedRange = { 4, 4, 4, 5, 5, 5, 5, 6 };
        int[] expectedMp = { 60, 60, 55, 55, 55, 50, 50, 50 };
        int[] expectedStamina = { 15, 15, 15, 15, 12, 12, 10, 10 };
        int[] expectedDuration = { 60, 60, 60, 80, 80, 100, 100, 120 };
        int[] expectedDcBonus = { 0, 1, 1, 1, 1, 1, 1, 2 };
        for (int level = 0; level <= 7; level++)
        {
            CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
            _test.Eq(costs.ApCost, 1, $"沉眠粉雾L{level}应消耗1 AP。");
            _test.Eq(costs.MpCost, expectedMp[level], $"沉眠粉雾L{level}法力消耗不符。");
            _test.Eq(
                costs.StaminaCost,
                expectedStamina[level],
                $"沉眠粉雾L{level}体力消耗不符。"
            );
            _test.Eq(costs.CooldownTu, 160, $"沉眠粉雾L{level}冷却应固定为160TU。");
            _test.True(
                costs.CooldownTu - expectedDuration[level] >= 40,
                $"沉眠粉雾L{level}睡眠结束后到冷却完成至少应保留40TU间隔。"
            );
            _test.Eq(
                combat.GetEffectiveRangeValue(level),
                expectedRange[level],
                $"沉眠粉雾L{level}射程不符。"
            );

            IReadOnlyList<CombatEffectDefinition> effects = ActiveEffectsAtLevel(
                combat.EffectDefinitions,
                level
            );
            _test.Eq(effects.Count, 1, $"沉眠粉雾L{level}应恰有一个生效状态效果。");
            CombatEffectDefinition effect = effects.FirstOrDefault();
            if (effect == null)
                continue;
            _test.Eq(effect.StatusId, new StringName("sleeping"), "状态ID应为sleeping。");
            _test.Eq(effect.DurationTu, expectedDuration[level], $"沉眠粉雾L{level}睡眠时长不符。");
            _test.Eq(effect.SaveDcBonus, expectedDcBonus[level], $"沉眠粉雾L{level}豁免DC成长不符。");
            _test.Eq(effect.SaveAbility, new StringName("willpower"), "沉眠粉雾应进行意志豁免。");
            _test.Eq(effect.SaveTag, new StringName("sleep"), "沉眠粉雾应进入sleep豁免链。");
            _test.True(effect.SkipTurn, "睡眠必须持续跳过行动。");
            _test.True(effect.BreakOnPositiveDamage, "睡眠必须在正伤害时解除。");
            _test.Eq(effect.OnRemovedStatusId, new StringName("wakeful"), "睡眠解除后应进入清醒。");
            _test.True(effect.OnRemovedStatusUndispellable, "清醒必须不可驱散。");
            _test.True(
                effect.OnRemovedStatusConsumeAfterNormalTurn,
                "清醒必须在完成下一次正常行动后消耗。"
            );
            _test.True(
                effect.SaveImmunityTags.Contains(new StringName("sleep")),
                "睡眠期间必须免疫再次睡眠。"
            );
            _test.True(
                effect.OnRemovedStatusSaveImmunityTags.Contains(new StringName("sleep")),
                "清醒期间必须免疫再次睡眠。"
            );
        }

        string levelSeven = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            7,
            new GDictionary()
        );
        _test.True(levelSeven.Contains("120TU"), "7级描述应公开120TU睡眠时长。");
        _test.True(levelSeven.Contains("冷却160TU"), "描述应公开160TU冷却。");
        _test.True(levelSeven.Contains("护盾吸收伤害时立即苏醒"), "描述应公开护盾伤害唤醒。");
        _test.True(levelSeven.Contains("0伤害不会唤醒"), "描述应公开零伤害不唤醒。");
        _test.True(levelSeven.Contains("完成下一次正常行动"), "描述应公开清醒的消耗条件。");
        _test.True(levelSeven.Contains("不会令攻击者获得额外命中或暴击"), "描述应公开无额外攻击收益。");
    }

    private void TestSleepStateRoundTrip(SkillDefinition skill)
    {
        CombatEffectDefinition effect = ActiveEffectsAtLevel(
            skill?.CombatProfile?.EffectDefinitions,
            7
        ).Single();
        BattleStatusEffectState state = BattleStatusSemanticTable.MergeStatus(
            effect,
            "roundtrip_caster",
            null,
            effect.StatusId
        );
        using GodotProjectionLease<GDictionary> lease = state.ToDictionaryLease();
        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(lease.Value);

        _test.True(restored?.skip_turn == true, "状态快照应保留skip_turn。");
        _test.True(
            restored?.break_on_positive_damage == true,
            "状态快照应保留break_on_positive_damage。"
        );
        _test.Eq(
            restored?.on_removed_status_id ?? new StringName(""),
            new StringName("wakeful"),
            "状态快照应保留解除后状态ID。"
        );
        _test.True(
            restored?.on_removed_status_save_immunity_tags.Contains(new StringName("sleep"))
                == true,
            "状态快照应保留解除后的睡眠免疫。"
        );

        BattleUnitState removalTarget = BuildUnit(
            "sleep_direct_removal_target",
            "enemy",
            Vector2I.Zero
        );
        try
        {
            removalTarget.SetStatusEffect(restored);
            removalTarget.EraseStatusEffect("sleeping");
            _test.False(removalTarget.HasStatusEffect("sleeping"), "通用状态移除入口应解除睡眠。");
            _test.True(
                removalTarget.HasStatusEffect("wakeful"),
                "到期、净化等共用的状态移除入口应生成清醒。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(removalTarget);
        }
    }

    private void TestDamageReactionsObservePostWakeState(SkillDefinition sleepSkill)
    {
        SkillDefinition damageSkill = BuildWakeDamageSkill();
        BattleUnitState attacker = BuildAttacker(
            "sleep_reaction_order_attacker",
            new Vector2I(2, 2)
        );
        BattleUnitState target = BuildUnit(
            "sleep_reaction_order_target",
            "enemy",
            new Vector2I(3, 2)
        );
        using BattleTestFixture fixture = CreateFixture(
            "sleep_reaction_order",
            sleepSkill,
            damageSkill,
            new[] { attacker },
            target
        );
        CombatEffectDefinition sleepEffect = ActiveEffectsAtLevel(
            sleepSkill.CombatProfile.EffectDefinitions,
            7
        ).Single();
        CombatEffectDefinition damageEffect = damageSkill.CombatProfile.EffectDefinitions.Single();

        target.SetStatusEffect(
            BattleStatusSemanticTable.MergeStatus(
                sleepEffect,
                attacker.unit_id,
                null,
                sleepEffect.StatusId
            )
        );
        var executionProbe = new DamageReactionStatusProbe();
        using (var executionResolver = new FixedHitMaxDamageResolver())
        {
            executionResolver.SetEquipmentAbilityPorts(null, executionProbe);
            AttackEffectResolutionResult result = executionResolver.ResolveEffects(
                attacker,
                target,
                new[] { damageEffect },
                DamageResolutionContext.Empty().WithBattleState(fixture.State)
            );
            _test.Eq(executionProbe.ObservationCount, 2, "正式正伤害应进入 taken/applied 两个 finalized reaction seam。");
            _test.False(executionProbe.SawSleeping, "正式 finalized reaction 不得观察到已被正伤害解除的睡眠。");
            _test.Eq(
                executionProbe.WakefulObservationCount,
                executionProbe.ObservationCount,
                "正式 finalized reaction 必须观察到睡眠解除后的清醒。"
            );
            _test.True(
                result.RemovedStatusEffectIds.Contains(new StringName("sleeping")),
                "提前解除不得丢失正式结果中的 removed status id。"
            );
        }

        target.EraseStatusEffect("wakeful");
        target.SetCurrentHp(5);
        target.SetStatusEffect(
            BattleStatusSemanticTable.MergeStatus(
                sleepEffect,
                attacker.unit_id,
                null,
                sleepEffect.StatusId
            )
        );
        var previewProbe = new DamageReactionStatusProbe();
        using (var previewResolver = new FixedHitMaxDamageResolver())
        {
            previewResolver.SetEquipmentAbilityPorts(null, previewProbe);
            previewResolver.SetFatalInterceptArbiter(
                new GuaranteedPreviewFatalInterceptArbiter()
            );
            BattleDamagePreviewResult preview = previewResolver.PreviewDamageEffectTyped(
                attacker,
                target,
                damageEffect,
                DamageResolutionContext.Empty().WithBattleState(fixture.State),
                BattleDamagePreviewRollMode.Maximum,
                BattleDamagePreviewSaveMode.Worst
            );
            _test.Eq(previewProbe.ObservationCount, 2, "fatal preview branch 应进入 taken/applied 两个 finalized reaction seam。");
            _test.Eq(
                previewProbe.BranchLocalObservationCount,
                previewProbe.ObservationCount,
                "fatal preview 的 finalized reaction 必须来自 branch-local projection。"
            );
            _test.False(previewProbe.SawSleeping, "fatal preview branch reaction 不得观察到已解除的睡眠。");
            _test.Eq(
                previewProbe.WakefulObservationCount,
                previewProbe.ObservationCount,
                "fatal preview branch reaction 必须观察到清醒 successor。"
            );
            _test.True(
                preview?.RemovedStatusEffectIds.Contains(new StringName("sleeping")) == true,
                "fatal preview branch 提前解除后仍须投影 removed status id。"
            );
            _test.True(
                preview?.TargetPreviewAfter?.HasStatusEffect("wakeful") == true,
                "fatal preview 最终目标快照应包含清醒 successor。"
            );
        }
        _test.True(target.HasStatusEffect("sleeping"), "fatal damage preview 不得修改正式目标睡眠状态。");
        _test.False(target.HasStatusEffect("wakeful"), "fatal damage preview 不得向正式目标写入清醒状态。");
        _test.Eq(target.GetCurrentHp(), 5, "fatal damage preview 不得修改正式目标生命值。");
    }

    private void TestSkippedTurnDamageWakeAntiChainAndNormalTurnConsumption(
        SkillDefinition sleepSkill
    )
    {
        SkillDefinition damageSkill = BuildWakeDamageSkill();
        BattleUnitState firstCaster = BuildCaster("sleep_first_caster", new Vector2I(1, 2), 7);
        BattleUnitState secondCaster = BuildCaster("sleep_second_caster", new Vector2I(1, 3), 7);
        BattleUnitState attacker = BuildAttacker("sleep_attacker", new Vector2I(2, 2));
        BattleUnitState target = BuildUnit("sleep_target", "enemy", new Vector2I(3, 2));
        using BattleTestFixture fixture = CreateFixture(
            "sleep_damage_wake",
            sleepSkill,
            damageSkill,
            new[] { firstCaster, secondCaster, attacker },
            target
        );

        AssertAiWeightsSleepByFormalSaveProbability(
            fixture,
            sleepSkill,
            firstCaster,
            target
        );
        CastSleep(fixture, firstCaster, target);
        BattleStatusEffectState sleeping = target.GetStatusEffect("sleeping");
        _test.True(sleeping != null, "意志豁免失败后应通过正式施法进入睡眠。");
        _test.Eq(sleeping?.duration ?? -1, 120, "7级正式施法应施加120TU睡眠。");
        _test.Eq(firstCaster.GetCurrentAp(), 1, "7级施法应消耗1 AP。");
        _test.Eq(firstCaster.GetCurrentMp(), 150, "7级施法应消耗50 MP。");
        _test.Eq(firstCaster.GetCurrentStamina(), 90, "7级施法应消耗10体力。");
        _test.Eq(firstCaster.GetCooldownTyped(SkillId), 160, "施法应启动160TU冷却。");
        AssertDebuffCount(
            fixture.Runtime._skill_turn_resolver,
            target,
            1,
            "睡眠必须服从 BattleStatusSemanticTable 的有害状态语义"
        );

        fixture.State.active_unit_id = "";
        fixture.State.PhaseKind = BattlePhaseKind.TimelineRunning;
        fixture.State.timeline.ready_unit_ids.Clear();
        fixture.State.timeline.ready_unit_ids.Add(target.unit_id);
        using (var skippedBatch = new BattleEventBatch())
        {
            fixture.Runtime._timeline_driver.ActivateNextReadyUnit(skippedBatch);
        }
        _test.Eq(fixture.State.active_unit_id, new StringName(""), "睡眠目标轮到行动时应直接跳过。");
        _test.True(target.HasStatusEffect("sleeping"), "跳过行动不得自动移除未到期睡眠。");

        CombatEffectDefinition zeroDamageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "force"
        );
        using (var zeroDamageResolver = new FixedHitMaxDamageResolver())
        {
            AttackEffectResolutionResult zeroDamageResult = zeroDamageResolver.ResolveEffects(
                attacker,
                target,
                new[] { zeroDamageEffect },
                DamageResolutionContext.Empty()
            );
            _test.Eq(zeroDamageResult.HpDamage, 0, "零值伤害效果不得造成生命伤害。");
            _test.Eq(zeroDamageResult.ShieldAbsorbed, 0, "零值伤害效果不得造成护盾吸收。");
            _test.True(target.HasStatusEffect("sleeping"), "0生命/0护盾伤害不得唤醒睡眠目标。");
        }

        CombatEffectDefinition damageEffect = damageSkill.CombatProfile.EffectDefinitions.Single();
        using (var previewResolver = new FixedHitMaxDamageResolver())
        {
            BattleDamagePreviewResult preview = previewResolver.PreviewDamageEffectTyped(
                attacker,
                target,
                damageEffect,
                DamageResolutionContext.Empty(),
                BattleDamagePreviewRollMode.Maximum,
                BattleDamagePreviewSaveMode.Worst
            );
            _test.False(
                preview?.TargetPreviewAfter?.HasStatusEffect("sleeping") ?? true,
                "canonical damage preview 的 detached 目标应在正伤害后苏醒。"
            );
            _test.True(
                preview?.TargetPreviewAfter?.HasStatusEffect("wakeful") == true,
                "canonical damage preview 应投影苏醒后的清醒状态。"
            );
            _test.True(
                preview?.RemovedStatusEffectIds.Contains(new StringName("sleeping")) == true,
                "canonical damage preview 应公开被正伤害移除的睡眠状态。"
            );
            if (preview != null)
            {
                using GodotProjectionLease<GDictionary> previewLease =
                    BattleDamagePreviewProjection.BuildLease(preview);
                using GArray projectedRemovedStatusIds =
                    previewLease.Value["removed_status_effect_ids"].AsGodotArray();
                _test.True(
                    projectedRemovedStatusIds.Contains(new StringName("sleeping")),
                    "Godot damage preview 投影应公开被正伤害移除的睡眠状态。"
                );
            }
            _test.True(target.HasStatusEffect("sleeping"), "伤害预览不得修改正式目标状态。");
        }

        int hpBefore = target.GetCurrentHp();
        using (var formalDamageResolver = new FixedHitMaxDamageResolver())
        {
            AttackEffectResolutionResult result = formalDamageResolver.ResolveEffects(
                attacker,
                target,
                new[] { damageEffect },
                DamageResolutionContext.ForSkill(WakeDamageSkillId)
                    .WithBattleState(fixture.State)
            );
            _test.True(target.GetCurrentHp() < hpBefore, "正式攻击应造成正生命伤害。");
            _test.True(
                result.RemovedStatusEffectIds.Contains(new StringName("sleeping")),
                "正式伤害结果应公开被攻击解除的睡眠状态。"
            );
        }
        AssertWakefulAfterDamage(target, "正生命伤害");
        AssertDebuffCount(
            fixture.Runtime._skill_turn_resolver,
            target,
            0,
            "清醒 successor 不得计为有害状态"
        );

        fixture.Runtime.ConfigureDamageResolverForTests(new FixedFailedSaveDamageResolver());
        BattleCommand immunePreviewCommand = BuildGroundCommand(
            secondCaster,
            target.GetAnchorCoord()
        );
        BattlePreview immunePreview = fixture.Runtime.PreviewCommand(immunePreviewCommand);
        BattleTestFixture.DisposeBattleCommand(immunePreviewCommand);
        var aiContext = new BattleAiContext
        {
            state = fixture.State,
            unit_state = secondCaster,
            grid_service = fixture.Runtime.GetGridService(),
            preview_command_callback = fixture.Runtime.PreviewCommand,
        };
        aiContext.SetSkillDefinitions(fixture.Runtime.GetSkillDefinitionIndexTyped());
        using (var scoreService = new BattleAiScoreService())
        {
            BattleCommand aiCommand = BuildGroundCommand(secondCaster, target.GetAnchorCoord());
            BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
                aiContext,
                sleepSkill,
                aiCommand,
                immunePreview,
                ActiveEffectsAtLevel(sleepSkill.CombatProfile.EffectDefinitions, 7),
                new Dictionary<string, object>(StringComparer.Ordinal)
            );
            _test.Eq(score?.effective_target_count ?? -1, 0, "AI不得把清醒目标计为有效睡眠目标。");
            _test.Eq(
                score?.estimated_control_probability_basis_points ?? -1,
                0,
                "AI对清醒目标的睡眠控制期望应为0。"
            );
            BattleTestFixture.DisposeBattleCommand(aiCommand);
        }
        BattleTestFixture.DisposeBattlePreview(immunePreview);

        fixture.State.active_unit_id = secondCaster.unit_id;
        fixture.State.PhaseKind = BattlePhaseKind.UnitActing;
        BattleCommand secondSleep = BuildGroundCommand(secondCaster, target.GetAnchorCoord());
        using (BattleEventBatch secondSleepBatch = fixture.Runtime.IssueCommand(secondSleep))
        {
            _test.False(target.HasStatusEffect("sleeping"), "清醒期间第二名施法者也不得连续睡眠目标。");
            _test.True(target.HasStatusEffect("wakeful"), "被免疫的睡眠不得消耗清醒。");
        }
        BattleTestFixture.DisposeBattleCommand(secondSleep);

        fixture.State.active_unit_id = "";
        fixture.State.PhaseKind = BattlePhaseKind.TimelineRunning;
        fixture.State.timeline.ready_unit_ids.Clear();
        fixture.State.timeline.ready_unit_ids.Add(target.unit_id);
        using (var activationBatch = new BattleEventBatch())
        {
            fixture.Runtime._timeline_driver.ActivateNextReadyUnit(activationBatch);
        }
        _test.Eq(fixture.State.active_unit_id, target.unit_id, "清醒目标应能正常获得行动窗口。");
        _test.True(target.HasStatusEffect("wakeful"), "正常行动开始时不得提前消耗清醒。");
        using (var endBatch = new BattleEventBatch())
        {
            fixture.Runtime._end_active_turn(endBatch);
        }
        _test.False(target.HasStatusEffect("wakeful"), "完成下一次正常行动后应消耗清醒。");
    }

    private void AssertAiWeightsSleepByFormalSaveProbability(
        BattleTestFixture fixture,
        SkillDefinition sleepSkill,
        BattleUnitState caster,
        BattleUnitState target
    )
    {
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedFailedSaveDamageResolver());
        BattleCommand command = BuildGroundCommand(caster, target.GetAnchorCoord());
        BattlePreview preview = null;
        try
        {
            preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "AI评分前的沉眠粉雾预览应合法。");
            _test.True(
                preview?.TargetUnitIdsTyped.Contains(target.unit_id) == true,
                "AI评分前的预览应包含未免疫目标。"
            );

            var context = new BattleAiContext
            {
                state = fixture.State,
                unit_state = caster,
                grid_service = fixture.Runtime.GetGridService(),
                preview_command_callback = fixture.Runtime.PreviewCommand,
            };
            context.SetSkillDefinitions(fixture.Runtime.GetSkillDefinitionIndexTyped());
            using var scoreService = new BattleAiScoreService();
            BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
                context,
                sleepSkill,
                command,
                preview,
                ActiveEffectsAtLevel(sleepSkill.CombatProfile.EffectDefinitions, 7),
                new Dictionary<string, object>(StringComparer.Ordinal)
            );
            _test.Eq(score?.effective_target_count ?? -1, 1, "AI应把未免疫敌人计为有效目标。");
            _test.Eq(
                score?.estimated_control_count ?? -1,
                0,
                "80% 成功率的睡眠不得伪装成100%确定控制。"
            );
            _test.Eq(
                score?.estimated_control_probability_basis_points ?? -1,
                8000,
                "L7施法者 DC17 对意志10目标应按正式豁免失败80%折算睡眠控制。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestShieldAbsorptionWakesWithoutHpDamage(SkillDefinition sleepSkill)
    {
        SkillDefinition damageSkill = BuildWakeDamageSkill();
        BattleUnitState caster = BuildCaster("shield_sleep_caster", new Vector2I(1, 2), 7);
        BattleUnitState attacker = BuildAttacker("shield_sleep_attacker", new Vector2I(2, 2));
        BattleUnitState target = BuildUnit("shield_sleep_target", "enemy", new Vector2I(3, 2));
        using BattleTestFixture fixture = CreateFixture(
            "sleep_shield_wake",
            sleepSkill,
            damageSkill,
            new[] { caster, attacker },
            target
        );
        CastSleep(fixture, caster, target);
        target.ReplaceShieldStateTyped(
            20,
            20,
            100,
            "sleep_test_shield",
            target.unit_id,
            "sleep_test_shield"
        );
        int hpBefore = target.GetCurrentHp();
        using (var formalDamageResolver = new FixedHitMaxDamageResolver())
        {
            AttackEffectResolutionResult result = formalDamageResolver.ResolveEffects(
                attacker,
                target,
                damageSkill.CombatProfile.EffectDefinitions,
                DamageResolutionContext.ForSkill(WakeDamageSkillId)
                    .WithBattleState(fixture.State)
            );
            _test.Eq(target.GetCurrentHp(), hpBefore, "护盾完全吸收时生命值应不变。");
            _test.True(
                target.GetShieldStateTyped().CurrentHp < 20,
                "正式攻击应造成正护盾吸收伤害。"
            );
            _test.True(
                result.RemovedStatusEffectIds.Contains(new StringName("sleeping")),
                "护盾吸收伤害的正式结果也应公开被解除的睡眠。"
            );
        }
        AssertWakefulAfterDamage(target, "正护盾吸收伤害");
    }

    private void CastSleep(
        BattleTestFixture fixture,
        BattleUnitState caster,
        BattleUnitState target
    )
    {
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedFailedSaveDamageResolver());
        fixture.State.active_unit_id = caster.unit_id;
        fixture.State.PhaseKind = BattlePhaseKind.UnitActing;
        BattleCommand command = BuildGroundCommand(caster, target.GetAnchorCoord());
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "合法地格应通过沉眠粉雾 canonical preview。");
        _test.True(
            preview?.TargetUnitIdsTyped.Contains(target.unit_id) == true,
            "沉眠粉雾预览应列出范围内敌人。"
        );
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void AssertWakefulAfterDamage(BattleUnitState target, string damageKind)
    {
        _test.False(target.HasStatusEffect("sleeping"), $"{damageKind}后睡眠必须立即解除。");
        BattleStatusEffectState wakeful = target.GetStatusEffect("wakeful");
        _test.True(wakeful != null, $"{damageKind}后必须获得清醒。");
        _test.True(wakeful?.undispellable == true, "清醒必须不可驱散。");
        _test.True(wakeful?.consume_after_normal_turn == true, "清醒必须等待正常行动结束后消耗。");
        _test.True(
            wakeful?.save_immunity_tags.Contains(new StringName("sleep")) == true,
            "清醒必须提供sleep豁免免疫。"
        );
    }

    private void AssertDebuffCount(
        BattleRuntimeSkillTurnResolver resolver,
        BattleUnitState target,
        int expected,
        string context
    )
    {
        _test.Eq(
            resolver.CountDebuffStatuses(target),
            expected,
            $"{context}（mutable state）。"
        );
        BattleUnitReadView targetView = target;
        _test.Eq(
            resolver.CountDebuffStatuses(targetView),
            expected,
            $"{context}（read view）。"
        );
    }

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition sleepSkill,
        SkillDefinition damageSkill,
        IReadOnlyList<BattleUnitState> allies,
        BattleUnitState target
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(9, 6),
            allies,
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                [SkillId] = sleepSkill,
                [WakeDamageSkillId] = damageSkill,
            }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDefinition BuildWakeDamageSkill()
    {
        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "force",
            diceCount: 1,
            diceSides: 6
        );
        return TestSkillDefinitionProjection.BuildSkill(
            WakeDamageSkillId,
            "测试唤醒攻击",
            TestSkillDefinitionProjection.BuildCombatProfile(
                WakeDamageSkillId,
                effects: new[] { damage },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangeValue: 4,
                targetSelectionMode: "single_unit",
                minTargetCount: 1,
                maxTargetCount: 1
            )
        );
    }

    private static BattleUnitState BuildCaster(StringName id, Vector2I coord, int level)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina));
        caster.attribute_snapshot.SetValue(AttributeService.INTELLIGENCE_MODIFIER, 4);
        caster.attribute_snapshot.SetValue("spell_proficiency_bonus", 3);
        caster.SetCurrentMp(200);
        caster.SetCurrentStamina(100);
        return caster;
    }

    private static BattleUnitState BuildAttacker(StringName id, Vector2I coord)
    {
        BattleUnitState attacker = BuildUnit(id, "player", coord);
        attacker.AddKnownActiveSkill(WakeDamageSkillId);
        attacker.SetKnownSkillLevelTyped(WakeDamageSkillId, 1);
        return attacker;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            id,
            faction,
            coord,
            currentAp: 2,
            currentHp: 100
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 200);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.SetCurrentHp(100);
        unit.SetCurrentAp(2);
        unit.SetCurrentMp(200);
        unit.SetCurrentStamina(100);
        return unit;
    }

    private static BattleCommand BuildGroundCommand(BattleUnitState caster, Vector2I coord) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = coord,
        };

    private static IReadOnlyList<CombatEffectDefinition> ActiveEffectsAtLevel(
        IReadOnlyList<CombatEffectDefinition> effects,
        int level
    ) =>
        (effects ?? Array.Empty<CombatEffectDefinition>())
            .Where(effect => effect != null && effect.IsUnlockedAtSkillLevel(level))
            .ToArray();
}
