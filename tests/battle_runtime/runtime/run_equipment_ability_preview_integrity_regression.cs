using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_equipment_ability_preview_integrity_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName FatalBindingId = "binding.test.preview.fatal";
    private static readonly StringName GuaranteedBindingId =
        "binding.test.preview.guaranteed_fatal";
    private static readonly StringName GrantBindingId = "binding.test.preview.grant";
    private static readonly StringName GrantId = "grant.test.preview.cleanse";
    private static readonly StringName GrantSkillId = "skill.test.preview.cleanse";
    private static readonly StringName FinalizedBindingId =
        "binding.test.preview.finalized";
    private static readonly StringName GroundSkillId = "skill.test.preview.ground_burst";
    private static readonly StringName LockedSkillId = "skill.test.preview.locked_burst";
    private static readonly StringName RecursiveSkillId = "skill.test.preview.recursive_burst";
    private static readonly StringName MonthlyBindingId = "binding.test.preview.monthly";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestOrderedFatalProbabilityProjectionAndForcedRollsRemainQueued();
            TestGuaranteedFatalActionsProjectOnDetachedStateAndStopRecursivePreview();
            TestFatalAttemptUsageAdvancesAcrossSharedWorkingSet();
            TestProbabilisticFatalBranchesCarryDistinctUsageAcrossEffects();
            TestDeadConsumeOnSuccessLaneCanRetryOnNextEffect();
            TestGrantedSkillPreviewProjectsPostUseWithoutCanonicalMutation();
            TestFinalizedPreviewDoesNotRollAndReportsUnsupportedSkills();
            TestSaveBranchesKeepFatalActionsConditionalAndObservable();
            TestSaveBranchesThatBothKillMergeFatalSurfaceOnce();
            TestAiUsesMonthlyFatalProbabilityForEnemyAndFriendlyDamage();
            TestRequiredPreviewUnitsOverrideCanonicalStateClone();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(
            _test.Finish("Equipment ability preview integrity regression")
        );
    }

    private void TestOrderedFatalProbabilityProjectionAndForcedRollsRemainQueued()
    {
        EquipmentFatalInterceptDefinition first = FatalIntercept(
            "first_thirty",
            order: 10,
            recoveryBasisPoints: 2000,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: D100Gate(30),
            successActions: new[] { ApplyStatus("first_preview_status") }
        );
        EquipmentFatalInterceptDefinition second = FatalIntercept(
            "second_fifty",
            order: 20,
            recoveryBasisPoints: 4000,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: D100Gate(50),
            successActions: new[] { ApplyStatus("second_preview_status") }
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = FatalBindingId,
            TraitId = "trait.test.preview.fatal",
            FatalIntercepts = new[] { second, first },
        };

        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_ordered_fatal",
            binding,
            skills: null,
            attachBindingToSource: false
        );
        fixture.Service.ConfigureRollGateValuesForTests(new[] { 10, 100 });
        BattleEquipmentAbilitySourceReadView source = fixture.FindTargetSource(FatalBindingId);
        int hpBefore = fixture.Target.GetCurrentHp();
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            fixture.Source,
            fixture.Target,
            DamageEffect(power: 200),
            DamageResolutionContext.ForSkill("skill.test.preview.lethal")
                .WithBattleState(fixture.State)
        );
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "fatal damage PreviewDamageEffectTyped"
        );

        _test.Eq(preview.LethalProbabilityBasisPoints, 3500, "30%后接50%的有序拦截应留下35%致死概率。");
        _test.Eq(preview.FatalInterceptProbabilityBasisPoints, 6500, "有序拦截总概率应为65%。");
        _test.Eq(preview.ExpectedSurvivalHp, 20, "期望存活HP应按各候选贡献概率加权。 ");
        _test.False(preview.StableLethal, "存在65%拦截时不得标记稳定致死。");
        _test.Eq(preview.FatalInterceptPreview.Candidates.Count, 2, "正式伤害预览应保留两个候选。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[0].ReachProbabilityBasisPoints, 10000, "首候选到达率应为100%。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[0].ContributionProbabilityBasisPoints, 3000, "首候选贡献率应为30%。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[1].ReachProbabilityBasisPoints, 7000, "第二候选只在首候选失败的70%路径到达。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[1].ContributionProbabilityBasisPoints, 3500, "第二候选贡献率应为35%。");
        _test.Eq(preview.FatalInterceptPreview.SuccessActionPreviews.Count, 2, "两个概率候选都应暴露条件成功动作。");
        foreach (BattleEquipmentAbilityActionPreviewResult action in preview.FatalInterceptPreview.SuccessActionPreviews)
        {
            _test.True(action.Conditional, "概率 fatal success action 应显式标记 conditional。");
            _test.False(action.Applied, "概率候选不得污染确定性 detached 状态。");
        }
        _test.Eq(fixture.Target.GetCurrentHp(), hpBefore, "正式伤害预览不得改写 canonical HP。");
        _test.False(fixture.Target.HasStatusEffect("first_preview_status"), "概率预览不得写入 canonical 状态。");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                source,
                binding,
                first,
                worldStep: 0
            ),
            0,
            "正式伤害预览不得消费 fatal 使用次数。"
        );

        using (GodotProjectionLease<GDictionary> lease =
            BattleDamagePreviewProjection.BuildLease(preview))
        {
            GDictionary fatal = lease.Value["fatal_intercept_preview"].AsGodotDictionary();
            GArray candidates = fatal["candidates"].AsGodotArray();
            GDictionary secondCandidate = candidates[1].AsGodotDictionary();
            _test.Eq(secondCandidate["reach_probability_basis_points"].AsInt32(), 7000, "正式投影应暴露候选到达率。");
            _test.Eq(secondCandidate["contribution_probability_basis_points"].AsInt32(), 3500, "正式投影应暴露候选贡献率。");
            GArray actions = fatal["success_actions"].AsGodotArray();
            _test.True(actions[0].AsGodotDictionary()["conditional"].AsBool(), "正式投影应暴露条件动作而非伪报 applied。");
        }

        fixture.Target.SetCurrentHp(0);
        BattleFatalInterceptResult committed = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture, worldStep: 0)
        );
        _test.True(committed.Intercepted, "预览后首个 forced roll=10 应仍在队列并命中30%候选。");
        _test.Eq(committed.Attempts[0].RolledValue, 10, "预览不得 dequeue forced roll。");
        _test.True(fixture.Target.HasStatusEffect("first_preview_status"), "正式 fatal 结算仍应执行成功动作。");
    }

    private void TestGuaranteedFatalActionsProjectOnDetachedStateAndStopRecursivePreview()
    {
        EquipmentAbilityActionDefinition recursiveTrigger = new()
        {
            ActionId = "action.test.preview.recursive_trigger",
            Kind = "trigger_skill",
            PayloadDefinition = new TriggerSkillActionPayloadDefinition
            {
                SkillId = RecursiveSkillId,
                SkillLevel = 1,
                TargetSelector = "target",
            },
        };
        EquipmentFatalInterceptDefinition intercept = FatalIntercept(
            "guaranteed_rebirth",
            order: 10,
            recoveryBasisPoints: 2500,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: null,
            successActions: new[]
            {
                ApplyStatus("guaranteed_preview_status"),
                recursiveTrigger,
            }
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = GuaranteedBindingId,
            TraitId = "trait.test.preview.guaranteed_fatal",
            FatalIntercepts = new[] { intercept },
        };
        SkillDefinition recursiveSkill = DamageSkill(
            RecursiveSkillId,
            targetMode: "unit",
            targetFilter: "enemy",
            DamageEffect(power: 200)
        );

        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_guaranteed_fatal",
            binding,
            new Dictionary<StringName, SkillDefinition>
            {
                [RecursiveSkillId] = recursiveSkill,
            },
            attachBindingToSource: true
        );
        int sourceHpBefore = fixture.Source.GetCurrentHp();
        int targetHpBefore = fixture.Target.GetCurrentHp();
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            fixture.Source,
            fixture.Target,
            DamageEffect(power: 200),
            DamageResolutionContext.ForSkill("skill.test.preview.outer_lethal")
                .WithBattleState(fixture.State)
        );
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "guaranteed fatal nested trigger preview"
        );

        _test.Eq(preview.LethalProbabilityBasisPoints, 0, "必定 fatal intercept 应把剩余致死概率降为0。");
        _test.True(preview.TargetPreviewAfter.IsAlive(), "detached 目标应投影成功恢复。");
        _test.Eq(preview.TargetPreviewAfter.GetCurrentHp(), 25, "detached 目标应投影25%恢复。");
        _test.True(preview.TargetPreviewAfter.HasStatusEffect("guaranteed_preview_status"), "必定 success apply_status 应落到 detached 目标。");
        _test.False(fixture.Target.HasStatusEffect("guaranteed_preview_status"), "必定预览动作不得污染 canonical 目标。");
        _test.Eq(fixture.Target.GetCurrentHp(), targetHpBefore, "fatal 预览不得修改 canonical 目标HP。");
        _test.Eq(fixture.Source.GetCurrentHp(), sourceHpBefore, "嵌套 trigger_skill 预览不得修改 canonical 来源HP。");

        BattleEquipmentAbilityActionPreviewResult trigger =
            preview.FatalInterceptPreview.SuccessActionPreviews[1];
        _test.True(trigger.Supported && trigger.Applied, "首层 direct trigger_skill 应生成可观察的伤害预览。");
        _test.Eq(trigger.DamagePreviews.Count, 1, "首层 direct trigger_skill 应含一个伤害预览。");
        BattleDamagePreviewResult nestedDamage = trigger.DamagePreviews[0];
        _test.True(nestedDamage.FatalInterceptPreview != null, "嵌套致死伤害仍应进入 fatal preview。");
        BattleEquipmentAbilityActionPreviewResult nestedTrigger =
            nestedDamage.FatalInterceptPreview.SuccessActionPreviews[1];
        _test.False(nestedTrigger.Supported, "第二层 fatal success trigger_skill 必须被深度护栏截断。");
        _test.Eq(nestedTrigger.UnsupportedReason, "trigger_skill_detached_preview_depth_limit", "递归截断应提供 typed 原因。");

        using (GodotProjectionLease<GDictionary> lease =
            BattleDamagePreviewProjection.BuildLease(preview))
        {
            GDictionary fatal = lease.Value["fatal_intercept_preview"].AsGodotDictionary();
            GArray actions = fatal["success_actions"].AsGodotArray();
            GDictionary projectedTrigger = actions[1].AsGodotDictionary();
            GArray nestedPreviews = projectedTrigger["damage_previews"].AsGodotArray();
            _test.Eq(nestedPreviews.Count, 1, "正式投影应保留受深度保护的嵌套伤害摘要。");
        }

        fixture.Source.ReplaceEquipmentAbilityProjectionTyped(
            Array.Empty<BattleEquipmentAbilitySourceState>(),
            temporalProgressModifiers: null
        );
        fixture.Resolver.ResolveEffects(
            fixture.Source,
            fixture.Target,
            new[] { DamageEffect(power: 200) },
            DamageResolutionContext.ForSkill("skill.test.preview.issue_lethal")
                .WithBattleState(fixture.State)
        );
        _test.True(fixture.Target.IsAlive(), "正式 Issue 语义仍应由 guaranteed fatal intercept 救活目标。");
        _test.True(fixture.Target.HasStatusEffect("guaranteed_preview_status"), "正式结算仍应应用 fatal success status。");
    }

    private void TestFatalAttemptUsageAdvancesAcrossSharedWorkingSet()
    {
        EquipmentFatalInterceptDefinition intercept = FatalIntercept(
            "single_shared_attempt",
            order: 10,
            recoveryBasisPoints: 2500,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: null,
            successActions: Array.Empty<EquipmentAbilityActionDefinition>()
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = "binding.test.preview.shared_attempt",
            TraitId = "trait.test.preview.shared_attempt",
            FatalIntercepts = new[] { intercept },
        };
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_shared_fatal_attempt",
            binding,
            skills: null,
            attachBindingToSource: false
        );
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);
        BattleDamagePreviewWorkingSet workingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(
                fixture.Source,
                fixture.Target,
                fixture.State
            );
        DamageResolutionContext context = DamageResolutionContext
            .ForSkill("skill.test.preview.two_lethal_segments")
            .WithBattleState(workingSet.BattleState);

        BattleDamagePreviewResult first = fixture.Resolver
            .PreviewDamageEffectOnWorkingSetTyped(
                workingSet,
                DamageEffect(power: 200),
                context
            );
        BattleDamagePreviewResult second = fixture.Resolver
            .PreviewDamageEffectOnWorkingSetTyped(
                workingSet,
                DamageEffect(power: 200),
                context
            );

        _test.Eq(first.LethalProbabilityBasisPoints, 0, "首段致死伤害应被唯一一次必定拦截救回。");
        _test.Eq(second.LethalProbabilityBasisPoints, 10000, "共享 working set 的第二段致死伤害必须看到 attempt 已耗尽。");
        _test.True(second.StableLethal, "第二段在唯一 attempt 已耗尽后应稳定致死。");
        _test.False(second.TargetPreviewAfter.IsAlive(), "第二段预览后的 detached 目标应死亡。");
        BattleEquipmentAbilitySourceReadView detachedSource =
            FindAbilitySource(workingSet.TargetPreview, binding.BindingId);
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                workingSet.TargetPreview,
                detachedSource,
                binding,
                intercept,
                worldStep: 0
            ),
            1,
            "detached per-battle fatal attempt 应按正式 once scope 推进。"
        );
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "shared working-set fatal attempt preview"
        );

        fixture.Resolver.ResolveEffects(
            fixture.Source,
            fixture.Target,
            new[] { DamageEffect(power: 200), DamageEffect(power: 200) },
            DamageResolutionContext.ForSkill("skill.test.issue.two_lethal_segments")
                .WithBattleState(fixture.State)
        );
        _test.False(fixture.Target.IsAlive(), "正式 Issue 应由首段救回、第二段耗尽后击倒目标。");
        BattleEquipmentAbilitySourceReadView canonicalSource =
            fixture.FindTargetSource(binding.BindingId);
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                canonicalSource,
                binding,
                intercept,
                worldStep: 0
            ),
            1,
            "正式 Issue 应只提交一次 fatal attempt。"
        );
    }

    private void TestProbabilisticFatalBranchesCarryDistinctUsageAcrossEffects()
    {
        EquipmentFatalInterceptDefinition first = new()
        {
            InterceptId = "branch_first_fifty_on_success",
            ResolutionOrder = 10,
            ProtectionPriority = 100,
            UsagePeriodKind = EquipmentAbilityUsagePeriodKind.PerBattle,
            MaxAttemptsPerPeriod = 1,
            ConsumeOnAttempt = false,
            RollGate = D100Gate(50),
            RecoveryKind = EquipmentFatalInterceptRecoveryKind.MaxHpPercent,
            RecoveryPercentBasisPoints = 2500,
        };
        EquipmentFatalInterceptDefinition second = FatalIntercept(
            "branch_second_guaranteed",
            order: 20,
            recoveryBasisPoints: 2500,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: null,
            successActions: Array.Empty<EquipmentAbilityActionDefinition>()
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = "binding.test.preview.probability_branches",
            TraitId = "trait.test.preview.probability_branches",
            FatalIntercepts = new[] { first, second },
        };
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_probability_branches",
            binding,
            skills: null,
            attachBindingToSource: false
        );
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);
        BattleDamagePreviewWorkingSet workingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(
                fixture.Source,
                fixture.Target,
                fixture.State
            );
        DamageResolutionContext context = DamageResolutionContext
            .ForSkill("skill.test.preview.probability_branch_sequence")
            .WithBattleState(workingSet.BattleState);

        BattleDamagePreviewResult firstSegment = fixture.Resolver
            .PreviewDamageEffectOnWorkingSetTyped(
                workingSet,
                DamageEffect(power: 200),
                context
            );
        BattleDamagePreviewResult secondSegment = fixture.Resolver
            .PreviewDamageEffectOnWorkingSetTyped(
                workingSet,
                DamageEffect(power: 200),
                context
            );

        _test.Eq(firstSegment.LethalProbabilityBasisPoints, 0, "50%候选失败后仍有必定后序候选，首段应必定存活。");
        _test.Eq(secondSegment.LethalProbabilityBasisPoints, 2500, "第二段必须按两条 branch-local usage 路径得到25%最终致死，而非把候选无条件全耗尽。");
        _test.False(secondSegment.StableLethal, "概率 branch 序列的25%致死不得误报稳定致死。");
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "probabilistic fatal branch sequence preview"
        );
    }

    private void TestDeadConsumeOnSuccessLaneCanRetryOnNextEffect()
    {
        EquipmentFatalInterceptDefinition intercept = new()
        {
            InterceptId = "retry_fifty_on_success",
            ResolutionOrder = 10,
            ProtectionPriority = 100,
            UsagePeriodKind = EquipmentAbilityUsagePeriodKind.PerBattle,
            MaxAttemptsPerPeriod = 1,
            ConsumeOnAttempt = false,
            RollGate = D100Gate(50),
            RecoveryKind = EquipmentFatalInterceptRecoveryKind.MaxHpPercent,
            RecoveryPercentBasisPoints = 2500,
        };
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = "binding.test.preview.dead_retry",
            TraitId = "trait.test.preview.dead_retry",
            FatalIntercepts = new[] { intercept },
        };
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_dead_retry",
            binding,
            skills: null,
            attachBindingToSource: false
        );
        BattleDamagePreviewWorkingSet workingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(
                fixture.Source,
                fixture.Target,
                fixture.State
            );
        DamageResolutionContext context = DamageResolutionContext
            .ForSkill("skill.test.preview.dead_retry_sequence")
            .WithBattleState(workingSet.BattleState);

        BattleDamagePreviewResult firstSegment = fixture.Resolver
            .PreviewDamageEffectOnWorkingSetTyped(
                workingSet,
                DamageEffect(power: 200),
                context
            );
        BattleDamagePreviewResult secondSegment = fixture.Resolver
            .PreviewDamageEffectOnWorkingSetTyped(
                workingSet,
                DamageEffect(power: 200),
                context
            );

        _test.Eq(firstSegment.LethalProbabilityBasisPoints, 5000, "单一50% consume-on-success候选首段应有50%致死。");
        _test.Eq(secondSegment.LethalProbabilityBasisPoints, 7500, "首段失败未耗用的dead lane必须在第二段重试，累计致死应为75%。");
    }

    private void TestGrantedSkillPreviewProjectsPostUseWithoutCanonicalMutation()
    {
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = GrantBindingId,
            TraitId = "trait.test.preview.grant",
            GrantedActions = new[]
            {
                new EquipmentGrantedActionDefinition
                {
                    GrantedActionId = GrantId,
                    GrantedKind = EquipmentGrantedActionKind.Skill,
                    SkillId = GrantSkillId,
                    SkillLevel = 1,
                    UsagePeriodKind = EquipmentAbilityUsagePeriodKind.PerBattle,
                    MaxUsesPerPeriod = 1,
                },
            },
            Reactions = new[]
            {
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = "reaction.test.preview.grant_post_use",
                    Trigger = EquipmentAbilityTriggerKind.OnGrantedSkillUsed,
                    Timing = EquipmentAbilityTimingKind.AfterSkill,
                    Actions = new[]
                    {
                        new EquipmentAbilityActionDefinition
                        {
                            ActionId = "action.test.preview.clear_old",
                            Kind = "clear_status",
                            PayloadDefinition = new ClearStatusActionPayloadDefinition
                            {
                                TargetSelector = "source",
                                StatusId = "preview_old_status",
                            },
                        },
                        ApplyStatus("preview_new_status", durationTu: 60),
                    },
                },
            },
        };
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            GrantSkillId,
            "Preview Cleanse",
            TestSkillDefinitionProjection.BuildCombatProfile(
                GrantSkillId,
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "status",
                        effectTargetTeamFilter: "self",
                        statusId: "preview_cast_surface",
                        durationTu: 10
                    ),
                },
                targetMode: "unit",
                targetTeamFilter: "self",
                rangeValue: 0,
                apCost: 1
            )
        );
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_granted_skill",
            binding,
            new Dictionary<StringName, SkillDefinition> { [GrantSkillId] = skill },
            attachBindingToSource: false,
            sourceActs: false
        );
        fixture.Target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "preview_old_status",
                duration = 30,
                stacks = 2,
            }
        );
        WeaponAbilityCommandTestSupport.PrimeActionResources(fixture.Target, ap: 2);
        fixture.Target.SetCurrentAp(2);
        fixture.State.active_unit_id = fixture.Target.unit_id;
        fixture.State.PhaseKind = BattlePhaseKind.UnitActing;
        BattleAvailableSkillEntry entry = FindEquipmentSkill(
            fixture,
            GrantSkillId,
            GrantBindingId,
            GrantId
        );
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            fixture.Target,
            fixture.Target,
            entry,
            GrantSkillId
        );
        EquipmentInstanceState instance = fixture.Target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        int perBattleChargeCountBefore = fixture.Target.GetPerBattleChargesTyped().Count;
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Target);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "equipment granted skill PreviewCommand"
        );
        _test.True(preview?.allowed == true, "canonical PreviewCommand 应允许装备授予技能。");
        BattleEquipmentAbilityCommandPreviewResult equipmentPreview =
            preview.EquipmentAbilityPreviewTyped;
        _test.True(equipmentPreview.Triggered, "PreviewCommand 应执行同一 OnGrantedSkillUsed 反应链。");
        _test.False(equipmentPreview.SourceUnitAfter.HasStatusEffect("preview_old_status"), "detached post-use 应清除旧状态。");
        _test.True(equipmentPreview.SourceUnitAfter.HasStatusEffect("preview_new_status"), "detached post-use 应应用新状态。");
        _test.Eq(equipmentPreview.Actions.Count, 2, "post-use preview 应暴露两个已执行动作。");
        _test.True(equipmentPreview.Actions[0].Applied && equipmentPreview.Actions[1].Applied, "确定性 clear/apply 应标记 applied。");
        _test.True(fixture.Target.HasStatusEffect("preview_old_status"), "PreviewCommand 不得清除 canonical 旧状态。");
        _test.False(fixture.Target.HasStatusEffect("preview_new_status"), "PreviewCommand 不得写入 canonical 新状态。");
        _test.Eq(fixture.Target.GetCurrentAp(), 2, "PreviewCommand 不得消费 canonical AP。");
        _test.Eq(instance.ability_usage_periods.Count, 0, "PreviewCommand 不得写入装备使用账本。");
        _test.Eq(
            fixture.Target.GetPerBattleChargesTyped().Count,
            perBattleChargeCountBefore,
            "PreviewCommand 不得创建 canonical per-battle grant charge。"
        );

        using (GodotProjectionLease<GDictionary> lease = BattlePreviewProjection.BuildLease(preview))
        {
            GDictionary equipment = lease.Value["equipment_ability_preview"].AsGodotDictionary();
            GDictionary sourceAfter = equipment["source_preview_after"].AsGodotDictionary();
            _test.True(ProjectedUnitHasStatus(sourceAfter, "preview_new_status"), "正式 command projection 应暴露 detached 新状态。");
            _test.False(ProjectedUnitHasStatus(sourceAfter, "preview_old_status"), "正式 command projection 应暴露旧状态已清除。");
            GArray actions = equipment["actions"].AsGodotArray();
            _test.True(actions[0].AsGodotDictionary()["applied"].AsBool(), "正式 command projection 应暴露动作 applied。");
        }

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "装备授予技能正式 Issue 应保持可执行。");
        _test.False(fixture.Target.HasStatusEffect("preview_old_status"), "正式 Issue 应清除旧状态。");
        _test.True(fixture.Target.HasStatusEffect("preview_new_status"), "正式 Issue 应应用新状态。");
        _test.Eq(fixture.Target.GetCurrentAp(), 1, "正式 Issue 应消费技能的1 AP。");
        Dictionary<StringName, int> committedCharges = fixture.Target.GetPerBattleChargesTyped();
        _test.Eq(committedCharges.Count, perBattleChargeCountBefore + 1, "正式 Issue 应提交一次 per-battle 使用账本。");
        _test.True(committedCharges.ContainsValue(0), "正式 Issue 应把一次 grant charge 消耗到0。 ");
    }

    private void TestFinalizedPreviewDoesNotRollAndReportsUnsupportedSkills()
    {
        EquipmentAbilityReactionDefinition gated = new()
        {
            ReactionId = "reaction.test.preview.finalized_gate",
            Trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
            Timing = EquipmentAbilityTimingKind.AfterDamage,
            RollGate = D100Gate(50),
            Actions = new[]
            {
                ApplyStatus("preview_gated_status"),
                new EquipmentAbilityActionDefinition
                {
                    ActionId = "action.test.preview.ground_trigger",
                    Kind = "trigger_skill",
                    PayloadDefinition = new TriggerSkillActionPayloadDefinition
                    {
                        SkillId = GroundSkillId,
                        SkillLevel = 1,
                        TargetSelector = "source",
                    },
                },
            },
        };
        EquipmentAbilityReactionDefinition locked = new()
        {
            ReactionId = "reaction.test.preview.level_filter",
            Trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
            Timing = EquipmentAbilityTimingKind.AfterDamage,
            Actions = new[]
            {
                new EquipmentAbilityActionDefinition
                {
                    ActionId = "action.test.preview.locked_trigger",
                    Kind = "trigger_skill",
                    PayloadDefinition = new TriggerSkillActionPayloadDefinition
                    {
                        SkillId = LockedSkillId,
                        SkillLevel = 1,
                        TargetSelector = "target",
                    },
                },
            },
        };
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = FinalizedBindingId,
            TraitId = "trait.test.preview.finalized",
            Reactions = new[] { gated, locked },
        };
        SkillDefinition groundSkill = DamageSkill(
            GroundSkillId,
            "ground",
            "enemy",
            DamageEffect(power: 7)
        );
        SkillDefinition lockedSkill = DamageSkill(
            LockedSkillId,
            "unit",
            "enemy",
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 99,
                damageTag: "fire",
                minSkillLevel: 2
            )
        );
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_finalized_roll_gate",
            binding,
            new Dictionary<StringName, SkillDefinition>
            {
                [GroundSkillId] = groundSkill,
                [LockedSkillId] = lockedSkill,
            },
            attachBindingToSource: false
        );
        fixture.Service.ConfigureRollGateValuesForTests(new[] { 100, 1 });
        int sourceHpBefore = fixture.Source.GetCurrentHp();
        int targetHpBefore = fixture.Target.GetCurrentHp();
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            fixture.Source,
            fixture.Target,
            DamageEffect(power: 5),
            DamageResolutionContext.ForSkill("skill.test.preview.finalized_hit")
                .WithBattleState(fixture.State)
        );
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "finalized damage PreviewDamageEffectTyped"
        );
        _test.Eq(preview.EquipmentActionPreviews.Count, 3, "finalized preview 应暴露 gated apply、ground trigger 与 level-filter trigger。");
        BattleEquipmentAbilityActionPreviewResult gatedStatus = preview.EquipmentActionPreviews[0];
        _test.Eq(gatedStatus.TriggerProbabilityBasisPoints, 5000, "reaction roll gate 应投影为50%而非正式掷骰。");
        _test.True(gatedStatus.Conditional && !gatedStatus.Applied, "概率 apply_status 不得写入确定性状态。");
        BattleEquipmentAbilityActionPreviewResult ground = preview.EquipmentActionPreviews[1];
        _test.False(ground.Supported, "ground trigger_skill 不得伪执行。");
        _test.Eq(ground.TriggerSkillId, GroundSkillId, "ground unsupported 摘要应保留 skill_id。");
        _test.Eq(ground.TriggerProbabilityBasisPoints, 5000, "ground unsupported 摘要应保留触发概率。");
        _test.Eq(ground.UnsupportedReason, "ground_trigger_skill_requires_full_battle_preview", "ground unsupported 摘要应给出明确原因。");
        BattleEquipmentAbilityActionPreviewResult levelFiltered = preview.EquipmentActionPreviews[2];
        _test.False(levelFiltered.Supported, "未到技能等级窗口的 effect 不得进入 trigger preview。");
        _test.Eq(levelFiltered.DamagePreviews.Count, 0, "等级未解锁 effect 不得生成伤害预览。");
        _test.False(preview.TargetPreviewAfter.HasStatusEffect("preview_gated_status"), "概率状态不得写入 detached 确定性结果。");
        _test.False(fixture.Target.HasStatusEffect("preview_gated_status"), "finalized preview 不得写入 canonical 状态。");
        _test.Eq(fixture.Source.GetCurrentHp(), sourceHpBefore, "unsupported trigger preview 不得改写 canonical 来源HP。");
        _test.Eq(fixture.Target.GetCurrentHp(), targetHpBefore, "finalized preview 不得改写 canonical 目标HP。");

        fixture.Resolver.ResolveEffects(
            fixture.Source,
            fixture.Target,
            new[] { DamageEffect(power: 5) },
            DamageResolutionContext.ForSkill("skill.test.preview.finalized_issue")
                .WithBattleState(fixture.State)
        );
        _test.False(fixture.Target.HasStatusEffect("preview_gated_status"), "预览未消费 forced=100，正式结算首掷应失败且不应用状态。");
        _test.Eq(fixture.Target.GetCurrentHp(), targetHpBefore - 5, "正式伤害语义应保持不变。");

        using (GodotProjectionLease<GDictionary> lease =
            BattleDamagePreviewProjection.BuildLease(preview))
        {
            GArray actions = lease.Value["equipment_action_previews"].AsGodotArray();
            GDictionary groundProjection = actions[1].AsGodotDictionary();
            _test.False(groundProjection["applied"].AsBool(), "正式投影不得把 ground preview 误报为 applied。");
            _test.Eq(groundProjection["unsupported_reason"].AsString(), "ground_trigger_skill_requires_full_battle_preview", "正式投影应暴露 unsupported 原因。");
        }
    }

    private void TestSaveBranchesKeepFatalActionsConditionalAndObservable()
    {
        EquipmentFatalInterceptDefinition intercept = FatalIntercept(
            "save_branch_rebirth",
            order: 10,
            recoveryBasisPoints: 2500,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: null,
            successActions: new[] { ApplyStatus("save_branch_rebirth_status") }
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = "binding.test.preview.save_branch",
            TraitId = "trait.test.preview.save_branch",
            FatalIntercepts = new[] { intercept },
        };
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_save_branch",
            binding,
            skills: null,
            attachBindingToSource: false
        );
        fixture.Target.SetCurrentHp(75);
        CombatEffectDefinition savedDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 100,
            damageTag: "fire",
            saveDc: 11,
            saveDcMode: "static",
            saveAbility: "constitution",
            saveTag: "preview_save_branch"
        );
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            fixture.Source,
            fixture.Target,
            savedDamage,
            DamageResolutionContext.ForSkill("skill.test.preview.save_branch")
                .WithBattleState(fixture.State)
        );
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "save-branch damage PreviewDamageEffectTyped"
        );

        _test.True(preview.SaveEstimate.HasSave, "fixture 必须真实进入50% save 分支预览。");
        _test.Eq(preview.SaveEstimate.SaveSuccessProbabilityBasisPoints, 5000, "DC11/CON0 应提供50% save成功分支。");
        _test.Eq(preview.SaveEstimate.SaveFailureProbabilityBasisPoints, 5000, "DC11/CON0 应提供50% save失败分支。");
        _test.Eq(preview.PostSaveDamage, 50, "期望伤害应为失败100/成功0的均值50。");
        _test.True(preview.FatalInterceptPreview != null, "均值伤害不致死时仍必须保留失败分支 fatal 候选摘要。");
        _test.Eq(preview.FatalInterceptProbabilityBasisPoints, 5000, "只在 save 失败分支到达的必定拦截应贡献50%总概率。");
        _test.Eq(preview.FatalInterceptPreview.Candidates.Count, 1, "save 分支摘要不应丢失唯一 fatal 候选。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[0].ReachProbabilityBasisPoints, 5000, "候选到达率应乘以 save 失败分支概率。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[0].ContributionProbabilityBasisPoints, 5000, "候选贡献率应乘以 save 失败分支概率。");
        BattleEquipmentAbilityActionPreviewResult action =
            preview.FatalInterceptPreview.SuccessActionPreviews[0];
        _test.True(action.Conditional, "仅 save 失败分支触发的 success action 必须是 conditional。");
        _test.False(action.Guaranteed, "分支专属 success action 不得误报 guaranteed。");
        _test.False(action.Applied, "分支专属 success action 不得误报已应用。");
        _test.False(preview.TargetPreviewAfter.HasStatusEffect("save_branch_rebirth_status"), "TargetPreviewAfter 不得把 save 失败分支状态写成确定结果。");
        _test.False(fixture.Target.HasStatusEffect("save_branch_rebirth_status"), "save 分支预览不得污染 canonical 状态。");
        _test.Eq(fixture.Target.GetCurrentHp(), 75, "save 分支预览不得污染 canonical HP。");
        using (GodotProjectionLease<GDictionary> lease =
            BattleDamagePreviewProjection.BuildLease(preview))
        {
            GDictionary fatal = lease.Value["fatal_intercept_preview"].AsGodotDictionary();
            GDictionary candidate = fatal["candidates"].AsGodotArray()[0].AsGodotDictionary();
            GDictionary projectedAction = fatal["success_actions"].AsGodotArray()[0].AsGodotDictionary();
            _test.Eq(candidate["reach_probability_basis_points"].AsInt32(), 5000, "正式投影必须保留 save 分支候选到达率。");
            _test.True(projectedAction["conditional"].AsBool(), "正式投影必须把 save 分支动作标为 conditional。");
            _test.False(projectedAction["applied"].AsBool(), "正式投影不得误报 save 分支动作已应用。");
        }

        fixture.Resolver.ResolveEffects(
            fixture.Source,
            fixture.Target,
            new[] { savedDamage },
            DamageResolutionContext.ForSkill("skill.test.preview.save_success_issue")
                .WithBattleState(fixture.State)
                .WithSaveRollOverrides(new[] { 20 })
        );
        _test.Eq(fixture.Target.GetCurrentHp(), 75, "正式 save 成功分支应完全免除伤害。");
        _test.False(fixture.Target.HasStatusEffect("save_branch_rebirth_status"), "正式 save 成功分支不得触发 fatal success action。");

        fixture.Resolver.ResolveEffects(
            fixture.Source,
            fixture.Target,
            new[] { savedDamage },
            DamageResolutionContext.ForSkill("skill.test.preview.save_failure_issue")
                .WithBattleState(fixture.State)
                .WithSaveRollOverrides(new[] { 1 })
        );
        _test.Eq(fixture.Target.GetCurrentHp(), 25, "正式 save 失败分支应触发 guaranteed fatal 并恢复25%HP。");
        _test.True(fixture.Target.HasStatusEffect("save_branch_rebirth_status"), "正式 save 失败分支应执行 fatal success action。");
    }

    private void TestSaveBranchesThatBothKillMergeFatalSurfaceOnce()
    {
        EquipmentFatalInterceptDefinition intercept = FatalIntercept(
            "both_save_branches_rebirth",
            order: 10,
            recoveryBasisPoints: 2500,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            rollGate: null,
            successActions: new[] { ApplyStatus("both_save_branches_status") }
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = "binding.test.preview.both_save_branches",
            TraitId = "trait.test.preview.both_save_branches",
            FatalIntercepts = new[] { intercept },
        };
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_both_save_branches",
            binding,
            skills: null,
            attachBindingToSource: false
        );
        fixture.Target.SetCurrentHp(40);
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 100,
            damageTag: "fire",
            saveDc: 11,
            saveDcMode: "static",
            saveAbility: "constitution",
            savePartialOnSuccess: true,
            saveTag: "preview_both_save_branches"
        );
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            fixture.Source,
            fixture.Target,
            effect,
            DamageResolutionContext.ForSkill("skill.test.preview.both_save_branches")
                .WithBattleState(fixture.State)
        );

        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "both-lethal save branch fatal preview"
        );
        _test.Eq(preview.SaveEstimate.DamageOnSaveFailure, 100, "save失败分支必须致死。");
        _test.Eq(preview.SaveEstimate.DamageOnSaveSuccess, 50, "半伤save成功分支也必须致死。");
        _test.Eq(preview.FatalInterceptProbabilityBasisPoints, 10000, "两条致死分支的同一必定拦截总概率只能是100%。");
        _test.Eq(preview.FatalInterceptPreview.Candidates.Count, 1, "相同 fatal candidate 跨save分支只能投影一份。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[0].ReachProbabilityBasisPoints, 10000, "相同候选的合并到达率不得翻倍。");
        _test.Eq(preview.FatalInterceptPreview.Candidates[0].ContributionProbabilityBasisPoints, 10000, "相同候选的合并贡献率不得翻倍。");
        _test.Eq(preview.FatalInterceptPreview.SuccessActionPreviews.Count, 1, "相同 fatal action 跨save分支只能投影一份。");
        _test.Eq(preview.FatalInterceptPreview.SuccessActionPreviews[0].TriggerProbabilityBasisPoints, 10000, "相同动作的触发概率不得翻倍。");
    }

    private void TestAiUsesMonthlyFatalProbabilityForEnemyAndFriendlyDamage()
    {
        EquipmentFatalInterceptDefinition monthly = FatalIntercept(
            "monthly_thirty",
            order: 10,
            recoveryBasisPoints: 3000,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerWorldMonth,
            rollGate: D100Gate(30),
            successActions: Array.Empty<EquipmentAbilityActionDefinition>()
        );
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = MonthlyBindingId,
            TraitId = "trait.test.preview.monthly",
            FatalIntercepts = new[] { monthly },
        };
        using PreviewFixture fixture = PreviewFixture.Create(
            "preview_ai_monthly",
            binding,
            skills: null,
            attachBindingToSource: false,
            worldStep: 449
        );
        BattleUnitState friendlyCaster = BuildUnit(
            "preview_monthly_friendly_caster",
            fixture.Target.faction_id,
            new Vector2I(2, 2),
            hp: 20,
            maxHp: 20
        );
        fixture.State.SetUnit(friendlyCaster);
        fixture.State.ally_unit_ids.Add(friendlyCaster.unit_id);
        BattleUnitState unprotectedTarget = BuildUnit(
            "preview_monthly_unprotected_target",
            fixture.Target.faction_id,
            new Vector2I(4, 1),
            hp: 100,
            maxHp: 100
        );
        fixture.State.SetUnit(unprotectedTarget);
        fixture.State.ally_unit_ids.Add(unprotectedTarget.unit_id);
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "any",
            power: 200,
            damageTag: "physical_slash"
        );
        SkillDefinition skill = DamageSkill(
            "skill.test.preview.ai_lethal",
            "unit",
            "any",
            effect
        );
        using var scoreService = new BattleAiScoreService();
        scoreService.Setup(fixture.Resolver);
        BattleAiContext mutationContext = BuildMutationOracleContext(fixture, fixture.Source);
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleAiScoreInput enemyScore = ScoreDamage(
            scoreService,
            fixture.State,
            fixture.Source,
            fixture.Target,
            skill,
            effect
        );
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "monthly fatal AI scoring preview"
        );
        BattleAiScoreInput friendlyScore = ScoreDamage(
            scoreService,
            fixture.State,
            friendlyCaster,
            fixture.Target,
            skill,
            effect
        );
        BattleAiScoreInput unprotectedScore = ScoreDamage(
            scoreService,
            fixture.State,
            fixture.Source,
            unprotectedTarget,
            skill,
            effect
        );
        _test.Eq(DamageLethalProbability(enemyScore, fixture.Target), 7000, "未耗月度拦截时 AI 应消费30%拦截后的70%致死概率。");
        _test.Eq(enemyScore.estimated_lethal_target_count, 0, "70%致死不得被 AI 覆盖为100%击杀。");
        _test.Eq(friendlyScore.estimated_friendly_lethal_target_count, 0, "有月度拦截的友军不得被当作100%友伤致死。");
        _test.Eq(unprotectedScore.estimated_lethal_target_count, 1, "无拦截对照目标应保持稳定击杀候选。");
        _test.True(unprotectedScore.total_score > enemyScore.total_score, "typed剩余致死概率必须进入最终 total_score，而非只停留在 breakdown。");
        var decisionEngine = new BattleAiDecisionEngine();
        _test.True(
            decisionEngine.IsBetterScoreInput(unprotectedScore, enemyScore),
            "最终候选排序必须优先100%致死对照，而不是把70%目标误排为同级稳定击杀。"
        );
        _test.Eq(fixture.Target.GetCurrentHp(), 100, "AI preview 不得修改 canonical HP。");
        _test.Eq(fixture.Target.GetEquipmentView().GetEquippedInstance("main_hand").ability_usage_periods.Count, 0, "AI preview 不得消费月度账本。");

        fixture.Service.ConfigureRollGateValuesForTests(new[] { 1 });
        fixture.Target.SetCurrentHp(0);
        BattleFatalInterceptResult consumed = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture, worldStep: 449)
        );
        _test.True(consumed.Intercepted, "fixture 应正式消费月度拦截。");
        fixture.Target.SetCurrentHp(100);
        BattleAiMutationSnapshot spentMutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleAiScoreInput spentEnemyScore = ScoreDamage(
            scoreService,
            fixture.State,
            fixture.Source,
            fixture.Target,
            skill,
            effect
        );
        AssertCanonicalStateUnchanged(
            spentMutationSnapshot,
            mutationContext,
            "spent monthly fatal AI scoring preview"
        );
        BattleAiScoreInput spentFriendlyScore = ScoreDamage(
            scoreService,
            fixture.State,
            friendlyCaster,
            fixture.Target,
            skill,
            effect
        );
        BattleAiScoreInput spentUnprotectedScore = ScoreDamage(
            scoreService,
            fixture.State,
            fixture.Source,
            unprotectedTarget,
            skill,
            effect
        );
        _test.Eq(DamageLethalProbability(spentEnemyScore, fixture.Target), 10000, "同月已耗后 AI 应看到100%致死。");
        _test.Eq(spentEnemyScore.estimated_lethal_target_count, 1, "同月已耗后应恢复稳定击杀计数。");
        _test.Eq(spentFriendlyScore.estimated_friendly_lethal_target_count, 1, "同月已耗后友伤致死计数应恢复。");
        _test.Eq(spentEnemyScore.total_score, spentUnprotectedScore.total_score, "月度拦截耗尽后，原受保护目标应在最终 scorer 中恢复为无拦截同级候选。");
        _test.False(
            decisionEngine.IsBetterScoreInput(spentUnprotectedScore, spentEnemyScore),
            "同月已耗后的同分候选不应仍残留旧的拦截排序惩罚。"
        );

    }

    private void TestRequiredPreviewUnitsOverrideCanonicalStateClone()
    {
        BattleUnitState source = BuildUnit("required_override_source", "enemy", Vector2I.Zero, 20, 20);
        BattleUnitState canonical = BuildUnit("required_override_target", "heroes", new Vector2I(1, 0), 30, 30);
        canonical.SetStatusEffect(new BattleStatusEffectState { status_id = "guarding", duration = 30 });
        var state = new BattleState { battle_id = "required_override_state" };
        state.SetUnit(source);
        state.SetUnit(canonical);
        BattleUnitState projected = canonical.DuplicateForPreview();
        projected.SetCurrentHp(7);
        projected.EraseStatusEffect("guarding");
        var mutationContext = new BattleAiContext
        {
            state = state,
            unit_state = source,
            grid_service = new BattleGridService(),
        };
        BattleAiMutationSnapshot mutationSnapshot =
            BattleAiMutationSnapshot.Capture(mutationContext);

        BattleDamagePreviewWorkingSet workingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(source, projected, state);
        AssertCanonicalStateUnchanged(
            mutationSnapshot,
            mutationContext,
            "required-unit detached state construction"
        );
        _test.Eq(workingSet.TargetPreview.GetCurrentHp(), 7, "required target clone 应覆盖 state 中同ID canonical clone。");
        _test.False(workingSet.TargetPreview.HasStatusEffect("guarding"), "required target 的只读投影修改必须胜出。");
        _test.Eq(canonical.GetCurrentHp(), 30, "构造 detached state 不得修改 canonical HP。");
        _test.True(canonical.HasStatusEffect("guarding"), "构造 detached state 不得修改 canonical 状态。");

        BattleTestFixture.DisposeBattleUnit(source);
        BattleTestFixture.DisposeBattleUnit(canonical);
        BattleTestFixture.DisposeBattleUnit(projected);
    }

    private static BattleAiContext BuildMutationOracleContext(
        PreviewFixture fixture,
        BattleUnitState actor
    )
    {
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = actor,
            grid_service = new BattleGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>(fixture.Skills)
        );
        return context;
    }

    private void AssertCanonicalStateUnchanged(
        BattleAiMutationSnapshot snapshot,
        BattleAiContext context,
        string label
    )
    {
        List<string> differences = snapshot.CompareCurrentState(context);
        _test.Eq(
            differences.Count,
            0,
            $"{label} 不得修改完整 canonical battle state（units/cells/environment/world_step）：{string.Join(" | ", differences)}"
        );
    }

    private static BattleAiScoreInput ScoreDamage(
        BattleAiScoreService scoreService,
        BattleState state,
        BattleUnitState actor,
        BattleUnitState target,
        SkillDefinition skill,
        CombatEffectDefinition effect
    )
    {
        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = new BattleGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = actor.unit_id,
            skill_id = skill.SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
        command.AddTargetUnitId(target.unit_id);
        command.AddTargetCoord(target.GetAnchorCoord());
        var preview = new BattlePreview { allowed = true };
        preview.AddTargetUnitId(target.unit_id);
        preview.AddTargetCoord(target.GetAnchorCoord());
        return scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            new[] { effect },
            new Dictionary<string, object>()
        );
    }

    private static int DamageLethalProbability(
        BattleAiScoreInput score,
        BattleUnitState target
    )
    {
        if (
            score?.damage_estimates_by_target_id.TryGetValue(
                target.unit_id,
                out List<BattleAiScoreService.DamageEstimateBreakdown> estimates
            ) == true
            && estimates?.Count > 0
        )
        {
            return estimates[0].LethalProbabilityBasisPoints;
        }
        return -1;
    }

    private static BattleAvailableSkillEntry FindEquipmentSkill(
        PreviewFixture fixture,
        StringName skillId,
        StringName bindingId,
        StringName grantId
    )
    {
        BattleSkillAvailabilityView view = new BattleSkillAvailabilityService(
            fixture.Skills,
            fixture.Bindings,
            new Dictionary<StringName, ItemDefinition>()
        ).BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = fixture.Target,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                WorldStep = fixture.State.GetEnvironmentSnapshot()?.WorldStep ?? -1,
                BattleState = fixture.State,
            }
        );
        foreach (BattleAvailableSkillEntry entry in view.SkillEntries)
        {
            if (
                entry?.EntryRef.SkillId == skillId
                && entry.EquipmentBindingId == bindingId
                && entry.EquipmentGrantedActionId == grantId
            )
            {
                return entry;
            }
        }
        throw new InvalidOperationException("equipment preview fixture skill entry missing");
    }

    private static BattleEquipmentAbilitySourceReadView FindAbilitySource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        if (unit == null)
            return null;
        foreach (
            BattleEquipmentAbilitySourceReadView source
            in unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool ProjectedUnitHasStatus(GDictionary unit, StringName statusId)
    {
        if (unit == null || !unit.ContainsKey("status_effects"))
            return false;
        GDictionary statuses = unit["status_effects"].AsGodotDictionary();
        return statuses.ContainsKey(statusId.ToString());
    }

    private static EquipmentAbilityActionDefinition ApplyStatus(
        StringName statusId,
        int durationTu = 30
    ) =>
        new()
        {
            ActionId = $"action.test.preview.apply.{statusId}",
            Kind = "apply_status",
            PayloadDefinition = new ApplyStatusActionPayloadDefinition
            {
                TargetSelector = "source",
                StatusId = statusId,
                DurationTu = durationTu,
                StackDelta = 1,
                StackBehavior = "refresh",
                StackLimit = 1,
            },
        };

    private static EquipmentFatalInterceptDefinition FatalIntercept(
        StringName interceptId,
        int order,
        int recoveryBasisPoints,
        EquipmentAbilityUsagePeriodKind usagePeriod,
        EquipmentRollGateDefinition rollGate,
        IReadOnlyList<EquipmentAbilityActionDefinition> successActions
    ) =>
        new()
        {
            InterceptId = interceptId,
            ResolutionOrder = order,
            ProtectionPriority = 100,
            UsagePeriodKind = usagePeriod,
            MaxAttemptsPerPeriod = 1,
            ConsumeOnAttempt = true,
            RollGate = rollGate,
            RecoveryKind = EquipmentFatalInterceptRecoveryKind.MaxHpPercent,
            RecoveryPercentBasisPoints = recoveryBasisPoints,
            SuccessActions = successActions ?? Array.Empty<EquipmentAbilityActionDefinition>(),
        };

    private static EquipmentRollGateDefinition D100Gate(int threshold) =>
        new()
        {
            RngStream = "test.preview",
            Roll = new DiceExpressionDefinition
            {
                Terms = new[]
                {
                    new DiceExpressionTermDefinition { DiceCount = 1, DiceSides = 100 },
                },
            },
            Compare = "lte",
            Threshold = threshold,
        };

    private static CombatEffectDefinition DamageEffect(int power) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: power,
            damageTag: "physical_slash"
        );

    private static SkillDefinition DamageSkill(
        StringName skillId,
        StringName targetMode,
        StringName targetFilter,
        params CombatEffectDefinition[] effects
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            skillId.ToString(),
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects ?? Array.Empty<CombatEffectDefinition>(),
                targetMode: targetMode,
                targetTeamFilter: targetFilter,
                rangeValue: 99
            )
        );

    private static BattleFatalInterceptContext FatalContext(
        PreviewFixture fixture,
        int worldStep
    ) =>
        new()
        {
            SourceUnit = fixture.Source,
            TargetUnit = fixture.Target,
            BattleState = fixture.State,
            DeathContext = BattleDeathResolutionRules.NormalFatalContext(),
            HpBefore = 1,
            HpDamage = 2,
            ProjectedHp = -1,
            WorldStep = worldStep,
        };

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp,
        int maxHp
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(hp: hp, mp: 20, stamina: 20, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private sealed class PreviewFixture : IDisposable
    {
        private PreviewFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState source,
            BattleUnitState target,
            IReadOnlyDictionary<StringName, SkillDefinition> skills,
            IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings
        )
        {
            Runtime = runtime;
            State = state;
            Source = source;
            Target = target;
            Skills = skills;
            Bindings = bindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Source { get; }
        internal BattleUnitState Target { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }
        internal BattleDamageResolver Resolver => Runtime.GetDamageResolver();
        internal BattleEquipmentAbilityRuntimeService Service =>
            Runtime.GetEquipmentAbilityRuntimeService();

        internal static PreviewFixture Create(
            StringName battleId,
            EquipmentAbilityBindingDefinition binding,
            IReadOnlyDictionary<StringName, SkillDefinition> skills,
            bool attachBindingToSource,
            bool sourceActs = true,
            int worldStep = 0
        )
        {
            var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [binding.BindingId] = binding,
            };
            var skillMap = new Dictionary<StringName, SkillDefinition>();
            foreach (
                KeyValuePair<StringName, SkillDefinition> entry
                in skills ?? new Dictionary<StringName, SkillDefinition>()
            )
            {
                skillMap[entry.Key] = entry.Value;
            }
            var runtime = new BattleRuntimeModule();
            BattleState state = null;
            try
            {
                runtime.setup(
                    skill_definitions: skillMap,
                    equipment_ability_bindings: bindings
                );
                BattleUnitState source = BuildUnit(
                    $"{battleId}.source",
                    "enemy",
                    new Vector2I(1, 1),
                    hp: 100,
                    maxHp: 100
                );
                BattleUnitState target = BuildUnit(
                    $"{battleId}.target",
                    "heroes",
                    new Vector2I(2, 1),
                    hp: 100,
                    maxHp: 100
                );
                AttachBinding(target, binding.BindingId, $"eq.{battleId}.target");
                if (attachBindingToSource)
                    AttachBinding(source, binding.BindingId, $"eq.{battleId}.source");
                state = BattleTestFixture.BuildFlatState(battleId, new Vector2I(6, 4));
                state.ReplaceEnvironmentSnapshot(
                    BattleEnvironmentSnapshot.FromBattleStartContext(
                        new GDictionary { ["world_step"] = worldStep }
                    )
                );
                BattleTestFixture.InstallUnits(
                    state,
                    new[] { target },
                    new[] { source }
                );
                state.PhaseKind = BattlePhaseKind.UnitActing;
                state.active_unit_id = sourceActs ? source.unit_id : target.unit_id;
                runtime.SetupStateForTests(state);
                return new PreviewFixture(runtime, state, source, target, skillMap, bindings);
            }
            catch
            {
                BattleTestFixture.DisposeBattleFixture(runtime, state);
                throw;
            }
        }

        internal BattleEquipmentAbilitySourceReadView FindTargetSource(StringName bindingId)
        {
            foreach (
                BattleEquipmentAbilitySourceReadView source
                in Target.GetEquipmentAbilitySourcesReadViewTyped()
            )
            {
                if (source?.AbilityIds?.Contains(bindingId) == true)
                    return source;
            }
            return null;
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }

        private static void AttachBinding(
            BattleUnitState unit,
            StringName bindingId,
            StringName instanceId
        )
        {
            StringName itemId = $"item.{bindingId}";
            var equipment = new EquipmentState();
            equipment.SetEquippedEntry(
                "main_hand",
                itemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(itemId, instanceId)
            );
            unit.SetEquipmentView(equipment);
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = itemId,
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName> { bindingId },
                    },
                },
                temporalProgressModifiers: null
            );
        }
    }
}
