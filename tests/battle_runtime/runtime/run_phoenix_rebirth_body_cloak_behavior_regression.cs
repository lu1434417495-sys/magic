using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_phoenix_rebirth_body_cloak_behavior_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName BodyItemId = "armor_phoenix_rebirth_body";
    private static readonly StringName BodyBindingId =
        "binding.phoenix_rebirth.body.burning_plate";
    private static readonly StringName FireShieldSkillId =
        "equipment_phoenix_rebirth_fire_shield";
    private static readonly StringName FireShieldGrantId =
        "grant.phoenix_rebirth.body.fire_shield";
    private static readonly StringName BurningStatusId =
        "phoenix_armor_burning_layers";
    private static readonly StringName FireShieldStatusId = "phoenix_fire_shield";
    private static readonly StringName CloakItemId = "acc_phoenix_rebirth_cloak";
    private static readonly StringName CloakBindingId =
        "binding.phoenix_rebirth.cloak.fatal_ember";
    private static readonly StringName CloakUsedStateKey = "low_hp_burst_used";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestBodyExternalFireFiltersStacksRefreshAndArmorClass();
            TestFireShieldUsesFormalGrantDailyLedgerAndCountersExactlyOnce();
            TestCloakCrossingFiltersOnceAndPreviewDoesNotPollute();
            TestCloakEmptyAreaConsumesAndFatalRecoveryCanCrossThreshold();
            TestCloakConditionalFatalPreviewIncludesFinalizedBurst();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(
            _test.Finish("Phoenix Rebirth body and cloak behavior regression")
        );
    }

    private void TestBodyExternalFireFiltersStacksRefreshAndArmorClass()
    {
        using Fixture fixture = Fixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildEquippedUnit(
            BodyItemId,
            "body",
            "eq_phoenix_body_filters",
            "body_filters"
        );
        PrimeUnit(holder, "ally", Vector2I.Zero, hp: 100, hpMax: 100);
        holder.SetDamageResistanceTyped("fire", "immune");
        BattleUnitState attacker = BuildUnit(
            "body_filter_attacker",
            "enemy",
            new Vector2I(1, 0),
            hp: 100,
            hpMax: 100
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_body_filters",
            new[] { holder },
            new[] { attacker },
            worldStep: 0
        );

        AttackEffectResolutionResult immuneFire = ResolveDamage(
            fixture,
            state,
            attacker,
            holder,
            "fire",
            10
        );
        _test.Eq(immuneFire.DamageEvents[0].RolledDamage, 10, "fire免疫仍应保留正原始伤害事实。");
        _test.Eq(holder.GetCurrentHp(), 100, "fire免疫应使最终HP伤害为0。");
        AssertStatus(holder, BurningStatusId, 1, 60, "fire免疫但raw>0仍应增加板甲燃烧层。");
        _test.Eq(holder.GetStatusEffect(BurningStatusId)?.armor_class_bonus_per_stack ?? 0, 1, "燃烧状态应携带每层AC+1 typed字段。");

        holder.GetStatusEffect(BurningStatusId).duration = 7;
        ResolveDamage(fixture, state, attacker, holder, "fire", 10);
        AssertStatus(holder, BurningStatusId, 2, 60, "第二次外部fire应加层并刷新至60 TU。");

        ResolveDamage(fixture, state, attacker, holder, "force", 4);
        AssertStatus(holder, BurningStatusId, 2, 60, "非fire伤害不得增加板甲燃烧层。");
        holder.SetCurrentHp(100);

        ResolveDamage(fixture, state, holder, holder, "fire", 10);
        AssertStatus(holder, BurningStatusId, 2, 60, "自身fire不得增加板甲燃烧层。");

        ResolveDamage(
            fixture,
            state,
            attacker,
            holder,
            "fire",
            10,
            BattleEffectOrigin.EquipmentAbility()
        );
        AssertStatus(holder, BurningStatusId, 2, 60, "装备能力生成的fire不得递归增加板甲燃烧层。");

        ResolveDamage(fixture, state, attacker, holder, "fire", 10);
        ResolveDamage(fixture, state, attacker, holder, "fire", 10);
        AssertStatus(holder, BurningStatusId, 3, 60, "板甲燃烧应封顶3层并继续刷新60 TU。");

        using BattleHitResolver hitResolver = new();
        AttackCheckInput check = hitResolver.BuildSkillAttackCheck(attacker, holder, null);
        _test.Eq(check.TargetArmorClass, 13, "基础AC10加3层燃烧后命中阈值应按AC13结算。");
    }

    private void TestFireShieldUsesFormalGrantDailyLedgerAndCountersExactlyOnce()
    {
        using Fixture fixture = Fixture.Build(new[] { 1, 3, 4, 1, 5, 6 });
        BattleUnitState holder = fixture.BuildEquippedUnit(
            BodyItemId,
            "body",
            "eq_phoenix_body_shield",
            "body_shield"
        );
        PrimeUnit(holder, "ally", Vector2I.Zero, hp: 100, hpMax: 100);
        holder.SetDamageResistanceTyped("fire", "immune");
        BattleUnitState attacker = BuildUnit(
            "fire_shield_attacker",
            "enemy",
            new Vector2I(1, 0),
            hp: 100,
            hpMax: 100
        );
        ApplyWeaponProjection(attacker, ranged: false);
        BattleState state = fixture.SetupBattle(
            "phoenix_body_fire_shield",
            new[] { holder },
            new[] { attacker },
            worldStep: 0
        );
        for (int index = 0; index < 3; index++)
            ResolveDamage(fixture, state, attacker, holder, "fire", 10);
        AssertStatus(holder, BurningStatusId, 3, 60, "释放火盾前应有3层燃烧。");

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 0,
            FireShieldSkillId,
            BodyBindingId,
            FireShieldGrantId
        );
        EquipmentInstanceState bodyInstance = holder.GetEquipmentView().GetEquippedInstance("body");
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder);
        holder.SetCurrentAp(2);
        ForceUnitActing(state, holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            holder,
            entry,
            FireShieldSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "任意燃烧层数下释放火盾的正式预览都应允许。");
        AssertStatus(holder, BurningStatusId, 3, 60, "正式预览不得清除canonical燃烧层。");
        _test.False(holder.HasStatusEffect(FireShieldStatusId), "正式预览不得向canonical单位应用火盾。");
        _test.Eq(holder.GetCurrentAp(), 2, "正式预览不得消耗canonical AP。");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                bodyInstance,
                FireShieldGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            0,
            "正式预览不得污染每日账本。"
        );

        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "释放火盾正式命令应成功执行。");
        _test.Eq(holder.GetCurrentAp(), 0, "释放火盾应消耗2 AP。");
        _test.False(holder.HasStatusEffect(BurningStatusId), "释放火盾成功后应清除全部燃烧层。");
        AssertStatus(holder, FireShieldStatusId, 1, 60, "释放火盾应应用60 TU凤凰火盾。");
        _test.Eq(holder.GetStatusEffect(FireShieldStatusId)?.armor_class_bonus_per_stack ?? 0, 3, "火盾应提供AC+3。");
        using (BattleHitResolver hitResolver = new())
        {
            AttackCheckInput shieldCheck = hitResolver.BuildSkillAttackCheck(attacker, holder, null);
            _test.Eq(shieldCheck.TargetArmorClass, 13, "清除3层燃烧后只保留火盾AC+3，命中阈值应为13而非16。");
        }

        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                bodyInstance,
                FireShieldGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            1,
            "释放火盾应写入板甲实例的世界日账本。"
        );
        BattleAvailableSkillEntry spent = FindEquipmentSkill(
            fixture,
            holder,
            state,
            0,
            FireShieldSkillId,
            BodyBindingId,
            FireShieldGrantId
        );
        _test.True(spent != null && !spent.IsSelectable, "同一世界日火盾应显示已耗尽。");
        holder.SetCurrentAp(2);
        holder.ResetPerTurnCharges();
        BattleAvailableSkillEntry nextDay = FindEquipmentSkill(
            fixture,
            holder,
            state,
            15,
            FireShieldSkillId,
            BodyBindingId,
            FireShieldGrantId
        );
        _test.True(nextDay?.IsSelectable == true, "下一个世界日火盾应刷新可用。");

        CombatSkillDefinition basicAttack = fixture.Skills["basic_attack"].CombatProfile;
        AttackCheckInput attackCheck = new(requiredRoll: 10, displayRequiredRoll: 10);
        BattleTestFixture.ConfigureHitResolverForTests(
            fixture.Runtime,
            new FixedHitResolver(20)
        );
        using var reactionBatch0 = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteLogicalAttack(
            fixture.Runtime, reactionBatch0, attacker, basicAttack.EffectDefinitions,
            actionContext => fixture.Runtime.GetDamageResolver().ResolveAttackEffects(
                attacker,
                holder,
                basicAttack.EffectDefinitions,
                attackCheck,
                new AttackContext { EventBatch = reactionBatch0, DamageOriginKind = BattleDamageOriginKind.MainDirectEffect, Action = actionContext,  BattleState = state, SkillId = "basic_attack" }
            )
        );
        _test.Eq(attacker.GetCurrentHp(), 93, "一次成功近战命中应只反击一次固定2D6=7 fire。");

        ApplyWeaponProjection(attacker, ranged: true);
        using var reactionBatch1 = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteLogicalAttack(
            fixture.Runtime, reactionBatch1, attacker, basicAttack.EffectDefinitions,
            actionContext => fixture.Runtime.GetDamageResolver().ResolveAttackEffects(
                attacker,
                holder,
                basicAttack.EffectDefinitions,
                attackCheck,
                new AttackContext { EventBatch = reactionBatch1, DamageOriginKind = BattleDamageOriginKind.MainDirectEffect, Action = actionContext,  BattleState = state, SkillId = "basic_attack" }
            )
        );
        _test.Eq(attacker.GetCurrentHp(), 93, "成功远程命中不得触发火盾反击。");

        ApplyWeaponProjection(attacker, ranged: false);
        BattleTestFixture.ConfigureHitResolverForTests(
            fixture.Runtime,
            new FixedMissResolver()
        );
        using var reactionBatch2 = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteLogicalAttack(
            fixture.Runtime, reactionBatch2, attacker, basicAttack.EffectDefinitions,
            actionContext => fixture.Runtime.GetDamageResolver().ResolveAttackEffects(
                attacker,
                holder,
                basicAttack.EffectDefinitions,
                attackCheck,
                new AttackContext { EventBatch = reactionBatch2, DamageOriginKind = BattleDamageOriginKind.MainDirectEffect, Action = actionContext,  BattleState = state, SkillId = "basic_attack" }
            )
        );
        _test.Eq(attacker.GetCurrentHp(), 93, "近战未命中不得触发火盾反击。");
    }

    private void TestCloakCrossingFiltersOnceAndPreviewDoesNotPollute()
    {
        using Fixture fixture = Fixture.Build(new[] { 3, 4 });
        BattleUnitState holder = fixture.BuildEquippedUnit(
            CloakItemId,
            "cloak",
            "eq_phoenix_cloak_crossing",
            "cloak_crossing"
        );
        PrimeUnit(holder, "ally", Vector2I.Zero, hp: 60, hpMax: 100);
        BattleUnitState attacker = BuildUnit(
            "cloak_crossing_attacker",
            "enemy",
            new Vector2I(1, 0),
            hp: 100,
            hpMax: 100
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_cloak_crossing",
            new[] { holder },
            new[] { attacker },
            worldStep: 0
        );

        holder.SetCurrentHp(45);
        bool previewChanged = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .ResolveDamageTakenFinalized(
                new BattleEquipmentAbilityDamageAppliedContext
                {
                    SourceUnit = holder,
                    TargetUnit = attacker,
                    BattleState = state,
                    RawDamage = 15,
                    HpDamage = 15,
                    HpBefore = 60,
                    DamageTag = "force",
                    IsPreview = true,
                }
            );
        _test.False(previewChanged, "finalized preview不应报告canonical mutation。");
        _test.Eq(holder.GetCurrentHp(), 45, "finalized preview不得再修改canonical披风穿戴者HP。");
        _test.Eq(attacker.GetCurrentHp(), 100, "finalized preview不得执行canonical凤凰怒火。");
        _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 0, "finalized preview不得污染披风本场状态。");
        holder.SetCurrentHp(60);

        ResolveDamage(fixture, state, attacker, holder, "force", 5);
        _test.Eq(holder.GetCurrentHp(), 55, "未跨到50%以下的外部伤害应正常结算。");
        _test.Eq(attacker.GetCurrentHp(), 100, "仍高于50%不得触发凤凰怒火。");
        _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 0, "未跨阈值不得消耗本场次数。");

        holder.SetCurrentHp(60);
        ResolveDamage(fixture, state, holder, holder, "force", 15);
        _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 0, "自身伤害跨阈值不得消耗披风次数。");
        _test.Eq(attacker.GetCurrentHp(), 100, "自身伤害不得触发凤凰怒火。");

        holder.SetCurrentHp(60);
        ResolveDamage(
            fixture,
            state,
            attacker,
            holder,
            "force",
            15,
            BattleEffectOrigin.EquipmentAbility()
        );
        _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 0, "装备能力伤害跨阈值不得消耗披风次数。");
        _test.Eq(attacker.GetCurrentHp(), 100, "装备能力伤害不得触发凤凰怒火。");

        holder.SetCurrentHp(60);
        ResolveDamage(fixture, state, attacker, holder, "force", 15);
        _test.Eq(holder.GetCurrentHp(), 45, "外部伤害应从60%跨到45%。");
        _test.Eq(attacker.GetCurrentHp(), 93, "首次有效跨阈值应造成固定2D6=7 fire。");
        _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 1, "首次有效跨阈值应标记本场已用。");

        ResolveDamage(fixture, state, attacker, holder, "force", 5);
        _test.Eq(attacker.GetCurrentHp(), 93, "持续处于低血量时不得重复触发。");
        holder.SetCurrentHp(60);
        ResolveDamage(fixture, state, attacker, holder, "force", 15);
        _test.Eq(attacker.GetCurrentHp(), 93, "同一场战斗再次跨阈值仍不得重复触发。");
    }

    private void TestCloakEmptyAreaConsumesAndFatalRecoveryCanCrossThreshold()
    {
        using (Fixture fixture = Fixture.Build(Array.Empty<int>()))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                CloakItemId,
                "cloak",
                "eq_phoenix_cloak_empty",
                "cloak_empty"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 60, hpMax: 100);
            BattleUnitState distant = BuildUnit(
                "cloak_distant_attacker",
                "enemy",
                new Vector2I(3, 0),
                hp: 100,
                hpMax: 100
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_cloak_empty_area",
                new[] { holder },
                new[] { distant },
                worldStep: 0
            );
            ResolveDamage(fixture, state, distant, holder, "force", 15);
            _test.Eq(distant.GetCurrentHp(), 100, "半径1内无敌人时凤凰怒火不应伤害远处来源。");
            _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 1, "半径1内无敌人仍应消耗本场凤凰怒火。");
        }

        using (Fixture fixture = Fixture.Build(new[] { 3, 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                CloakItemId,
                "cloak",
                "eq_phoenix_cloak_fatal",
                "cloak_fatal"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 60, hpMax: 100);
            BattleUnitState attacker = BuildUnit(
                "cloak_fatal_attacker",
                "enemy",
                new Vector2I(1, 0),
                hp: 100,
                hpMax: 100
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_cloak_fatal_crossing",
                new[] { holder },
                new[] { attacker },
                worldStep: 0
            );
            BattleEquipmentAbilityRuntimeService service =
                fixture.Runtime.GetEquipmentAbilityRuntimeService();
            service.ConfigureRollGateValuesForTests(new[] { 25 });
            service.ConfigureFatalRecoveryValuesForTests(new[] { 6 });

            ResolveDamage(fixture, state, attacker, holder, "force", 100);
            _test.True(holder.IsAlive(), "披风25%致死拦截成功后穿戴者应存活。");
            _test.Eq(holder.GetCurrentHp(), 6, "披风致死拦截应按固定1D12恢复6 HP。");
            _test.Eq(attacker.GetCurrentHp(), 93, "致死拦截恢复后跨50%仍应触发固定2D6=7凤凰怒火。");
            _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 1, "致死拦截后的跨阈值应消耗本场凤凰怒火。");
        }
    }

    private void TestCloakConditionalFatalPreviewIncludesFinalizedBurst()
    {
        using (Fixture fixture = Fixture.Build(new[] { 3, 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                CloakItemId,
                "cloak",
                "eq_phoenix_cloak_conditional_preview",
                "cloak_conditional_preview"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 60, hpMax: 100);
            BattleUnitState attacker = BuildUnit(
                "cloak_conditional_preview_attacker",
                "enemy",
                new Vector2I(1, 0),
                hp: 100,
                hpMax: 100
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_cloak_conditional_preview",
                new[] { holder },
                new[] { attacker },
                worldStep: 0
            );
            var mutationContext = new BattleAiContext
            {
                state = state,
                unit_state = attacker,
                grid_service = new BattleGridService(),
            };
            BattleAiMutationSnapshot mutationSnapshot =
                BattleAiMutationSnapshot.Capture(mutationContext);

            CombatEffectDefinition lethalEffect = TestSkillDefinitionProjection.BuildEffect(
                "damage",
                effectTargetTeamFilter: "enemy",
                power: 100,
                damageTag: "force"
            );
            BattleDamagePreviewWorkingSet workingSet =
                BattleDamagePreviewWorkingSet.CreateDetached(attacker, holder, state);
            DamageResolutionContext previewContext = DamageResolutionContext
                .ForSkill("test_cloak_conditional_preview")
                .WithBattleState(workingSet.BattleState);
            BattleDamagePreviewResult preview = fixture.Runtime
                .GetDamageResolver()
                .PreviewDamageEffectOnWorkingSetTyped(
                    workingSet,
                    lethalEffect,
                    previewContext
                );

            _test.Eq(
                mutationSnapshot.CompareCurrentState(mutationContext).Count,
                0,
                "真实披风 fatal preview 不得修改 canonical battle state。"
            );
            _test.Eq(preview.FatalInterceptProbabilityBasisPoints, 2500, "真实披风 fatal 应投影25%拦截概率。");
            _test.Eq(preview.LethalProbabilityBasisPoints, 7500, "真实披风 fatal 后应保留75%致死概率。");
            _test.Eq(preview.EquipmentActionPreviews.Count, 2, "披风成功分支的凤凰怒火与本场consume动作都应进入 finalized preview。");
            foreach (BattleEquipmentAbilityActionPreviewResult action in preview.EquipmentActionPreviews)
            {
                _test.Eq(action.TriggerProbabilityBasisPoints, 2500, "finalized action 应乘完整 fatal 成功路径概率。");
                _test.True(action.Conditional, "25% fatal 成功分支动作必须标记 conditional。");
                _test.False(action.Applied, "条件成功分支不得写成确定性 applied。");
            }
            if (preview.EquipmentActionPreviews.Count > 0)
            {
                _test.Eq(
                    preview.EquipmentActionPreviews[0].TriggerSkillId,
                    "equipment_phoenix_rebirth_cloak_low_hp_burst",
                    "真实披风 finalized preview 应保留凤凰怒火 skill_id。"
                );
                _test.False(
                    preview.EquipmentActionPreviews[0].Supported,
                    "ground/radius 凤凰怒火必须明确标记为当前 detached preview 不展开。"
                );
                _test.Eq(
                    preview.EquipmentActionPreviews[0].UnsupportedReason,
                    "ground_trigger_skill_requires_full_battle_preview",
                    "ground/radius 凤凰怒火应暴露稳定的 typed unsupported 原因。"
                );
                _test.Eq(
                    preview.EquipmentActionPreviews[0].DamagePreviews.Count,
                    0,
                    "不支持的 ground/radius trigger_skill 不得伪报已展开的子伤害。"
                );
            }
            BattleFatalInterceptPreviewBranch interceptedBranch = null;
            foreach (BattleFatalInterceptPreviewBranch branch in workingSet.ContinuationBranches)
            {
                if (branch.Intercepted)
                {
                    interceptedBranch = branch;
                    break;
                }
            }
            _test.True(interceptedBranch != null, "25% fatal 成功路径必须保留独立 detached continuation branch。");
            if (interceptedBranch != null)
            {
                _test.Eq(
                    GetAbilityState(
                        interceptedBranch.TargetUnit,
                        CloakBindingId,
                        CloakUsedStateKey
                    ),
                    1,
                    "fatal 成功分支内确定性的 consume 动作必须写入 detached 分支状态。"
                );
            }

            BattleDamagePreviewResult secondPreview = fixture.Runtime
                .GetDamageResolver()
                .PreviewDamageEffectOnWorkingSetTyped(
                    workingSet,
                    lethalEffect,
                    previewContext
                );
            _test.Eq(
                secondPreview.EquipmentActionPreviews.Count,
                0,
                "同一 preview frontier 的后续伤害不得再次触发已消费的披风凤凰怒火。"
            );
            _test.Eq(
                mutationSnapshot.CompareCurrentState(mutationContext).Count,
                0,
                "连续两段真实披风 preview 仍不得修改 canonical battle state。"
            );
            _test.Eq(holder.GetCurrentHp(), 60, "披风 fatal preview 不得改写 canonical HP。");
            _test.Eq(attacker.GetCurrentHp(), 100, "披风 fatal preview 不得伤害 canonical 攻击者。");
            _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 0, "披风 fatal preview 不得提交 canonical 本场状态。");
        }

        using (Fixture fixture = Fixture.Build(new[] { 3, 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                CloakItemId,
                "cloak",
                "eq_phoenix_cloak_conditional_issue_success",
                "cloak_conditional_issue_success"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 60, hpMax: 100);
            BattleUnitState attacker = BuildUnit(
                "cloak_conditional_issue_success_attacker",
                "enemy",
                new Vector2I(1, 0),
                hp: 100,
                hpMax: 100
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_cloak_conditional_issue_success",
                new[] { holder },
                new[] { attacker },
                worldStep: 0
            );
            fixture.Runtime.GetEquipmentAbilityRuntimeService()
                .ConfigureRollGateValuesForTests(new[] { 25 });
            fixture.Runtime.GetEquipmentAbilityRuntimeService()
                .ConfigureFatalRecoveryValuesForTests(new[] { 6 });

            ResolveDamage(fixture, state, attacker, holder, "force", 100);

            _test.True(holder.IsAlive(), "forced fatal 成功分支应救回披风穿戴者。");
            _test.Eq(attacker.GetCurrentHp(), 93, "forced fatal 成功后应正式触发凤凰怒火反击。");
            _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 1, "forced fatal 成功后应正式消费低血爆发。");
        }

        using (Fixture fixture = Fixture.Build(new[] { 3, 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                CloakItemId,
                "cloak",
                "eq_phoenix_cloak_conditional_issue_fail",
                "cloak_conditional_issue_fail"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 60, hpMax: 100);
            BattleUnitState attacker = BuildUnit(
                "cloak_conditional_issue_fail_attacker",
                "enemy",
                new Vector2I(1, 0),
                hp: 100,
                hpMax: 100
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_cloak_conditional_issue_fail",
                new[] { holder },
                new[] { attacker },
                worldStep: 0
            );
            fixture.Runtime.GetEquipmentAbilityRuntimeService()
                .ConfigureRollGateValuesForTests(new[] { 26 });

            ResolveDamage(fixture, state, attacker, holder, "force", 100);

            _test.False(holder.IsAlive(), "forced fatal 失败分支应保持死亡。");
            _test.Eq(attacker.GetCurrentHp(), 100, "forced fatal 失败后不得触发凤凰怒火。");
            _test.Eq(GetAbilityState(holder, CloakBindingId, CloakUsedStateKey), 0, "forced fatal 失败后不得消费低血爆发状态。");
        }
    }

    private static AttackEffectResolutionResult ResolveDamage(
        Fixture fixture,
        BattleState state,
        BattleUnitState source,
        BattleUnitState target,
        StringName damageTag,
        int power,
        BattleEffectOrigin origin = null,
        bool preview = false
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: power,
            damageTag: damageTag
        );
        DamageResolutionContext context = DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: "test_external_damage"
            )
            .WithBattleState(state)
            .WithDamageApplicationHookContext(null, origin ?? BattleEffectOrigin.PlayerCommand());
        if (preview)
            context = context.WithPreviewMode();
        return fixture.Runtime.GetDamageResolver().ResolveEffects(
            source,
            target,
            new[] { effect },
            context
        );
    }

    private void AssertStatus(
        BattleUnitState unit,
        StringName statusId,
        int stacks,
        int durationTu,
        string message
    )
    {
        BattleStatusEffectState status = unit.GetStatusEffect(statusId);
        _test.Eq(status?.stacks ?? 0, stacks, message);
        _test.Eq(status?.duration ?? -1, durationTu, $"{message}（持续时间）");
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp,
        int hpMax
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        };
        PrimeUnit(unit, factionId, coord, hp, hpMax);
        unit.SetEquipmentView(new EquipmentState());
        unit.SetUnarmedWeaponProjection();
        return unit;
    }

    private static void PrimeUnit(
        BattleUnitState unit,
        StringName factionId,
        Vector2I coord,
        int hp,
        int hpMax
    )
    {
        unit.faction_id = factionId;
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hpMax);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
        unit.WithCombatResourcesForTest(
            hp: hp,
            ap: 2,
            movePoints: 2,
            isAlive: hp > 0
        );
    }

    private static void ApplyWeaponProjection(BattleUnitState unit, bool ranged)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = ranged ? "test_phoenix_bow" : "test_phoenix_sword",
                weapon_profile_type_id = ranged ? "longbow" : "longsword",
                weapon_range_type = ranged ? "ranged" : "melee",
                weapon_family = ranged ? "bow" : "sword",
                weapon_current_grip = ranged ? "two_handed" : "one_handed",
                weapon_attack_range = ranged ? 6 : 1,
                weapon_one_handed_dice = ranged
                    ? new WeaponDice()
                    : new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_two_handed_dice = ranged
                    ? new WeaponDice { dice_count = 1, dice_sides = 8 }
                    : new WeaponDice(),
                weapon_uses_two_hands = ranged,
                weapon_physical_damage_tag = ranged
                    ? "physical_pierce"
                    : "physical_slash",
            }
        );
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        Fixture fixture,
        BattleUnitState holder,
        BattleState state,
        int worldStep,
        StringName skillId,
        StringName bindingId,
        StringName grantId
    )
    {
        BattleAvailableSkillEntry entry = FindEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep,
            skillId,
            bindingId,
            grantId
        );
        if (entry == null)
            throw new InvalidOperationException($"missing equipment skill {skillId}.");
        if (!entry.IsSelectable)
            throw new InvalidOperationException($"equipment skill {skillId} is not selectable: {entry.DisabledReason}");
        return entry;
    }

    private static BattleAvailableSkillEntry FindEquipmentSkill(
        Fixture fixture,
        BattleUnitState holder,
        BattleState state,
        int worldStep,
        StringName skillId,
        StringName bindingId,
        StringName grantId
    )
    {
        BattleSkillAvailabilityView view = new BattleSkillAvailabilityService(
            fixture.Skills,
            fixture.Bindings,
            fixture.Items
        ).BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = worldStep,
                BattleState = state,
            }
        );
        foreach (BattleAvailableSkillEntry entry in view.SkillEntries)
        {
            if (
                entry?.EntryRef.SkillId == skillId
                && entry.EquipmentBindingId == bindingId
                && entry.EquipmentGrantedActionId == grantId
            )
                return entry;
        }
        return null;
    }

    private static int GetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        string suffix = $"|{stateKey}";
        foreach (StringName key in unit.GetPerBattleChargesTyped().Keys)
        {
            if (key.ToString().EndsWith(suffix, StringComparison.Ordinal))
                return unit.GetPerBattleChargeTyped(key, 0);
        }
        foreach (BattleEquipmentAbilitySourceReadView source in unit.GetEquipmentAbilitySourcesReadViewTyped())
        {
            if (source?.AbilityIds?.Contains(bindingId) != true)
                continue;
            StringName sourceKey = source.SourceEquipmentInstanceId;
            if (sourceKey == "")
                sourceKey = source.EquipmentDefId;
            StringName key = $"equipment_ability|state|{sourceKey}|{stateKey}";
            return unit.GetPerBattleChargeTyped(key, 0);
        }
        return 0;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private Fixture(
            CharacterManagementModule characterManagement,
            PartyState partyState,
            BattleRuntimeModule runtime,
            ContentSnapshot snapshot
        )
        {
            _characterManagement = characterManagement;
            _partyState = partyState;
            Runtime = runtime;
            Items = snapshot.Items;
            Skills = snapshot.Skills;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static Fixture Build(IEnumerable<int> damageRolls)
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            PartyState partyState = BuildPartyState();
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                snapshot.Skills,
                snapshot.Professions,
                snapshot.Achievements,
                snapshot.Items,
                snapshot.Quests,
                snapshot.Traits,
                null,
                new ProgressionIdentityCatalogData(),
                snapshot.GearSets
            );
            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                snapshot.Skills,
                item_defs: snapshot.Items,
                trait_defs: snapshot.Traits,
                equipment_ability_bindings: snapshot.EquipmentAbilityBindings
            );
            using GArray rolls = new();
            foreach (int roll in damageRolls ?? Array.Empty<int>())
                rolls.Add(roll);
            BattleTestFixture.ConfigureDamageResolverForTests(
                runtime,
                new FixedRollDamageResolver(rolls)
            );
            BattleTestFixture.ConfigureHitResolverForTests(
                runtime,
                new FixedHitResolver(20)
            );
            return new Fixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildEquippedUnit(
            StringName itemId,
            StringName slotId,
            StringName instanceId,
            string label
        )
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                slotId,
                itemId,
                new[] { slotId },
                EquipmentInstanceState.CreateInstance(itemId, instanceId)
            );
            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(
                _partyState,
                null
            );
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} fixture must build one ally.");
            return units[0];
        }

        internal BattleState SetupBattle(
            StringName battleId,
            IReadOnlyList<BattleUnitState> allies,
            IReadOnlyList<BattleUnitState> enemies,
            int worldStep
        )
        {
            BattleState state = BattleTestFixture.BuildFlatState(
                battleId,
                new Vector2I(7, 7)
            );
            state.ReplaceEnvironmentSnapshot(
                BattleEnvironmentSnapshot.FromBattleStartContext(
                    new GDictionary { ["world_step"] = worldStep }
                )
            );
            BattleTestFixture.InstallUnits(state, allies, enemies);
            Runtime.SetupStateForTests(state);
            return state;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
            _characterManagement?.Dispose();
        }

        private static PartyState BuildPartyState()
        {
            PartyState party = new();
            PartyMemberState member = new()
            {
                member_id = "hero",
                display_name = "Hero",
                progression = new UnitProgress
                {
                    unit_id = "hero",
                    display_name = "Hero",
                },
                equipment_state = new EquipmentState(),
            };
            party.SetMemberState(member);
            party.active_member_ids.Add("hero");
            party.leader_member_id = "hero";
            return party;
        }
    }
}
