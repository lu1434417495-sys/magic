using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

// 龙血沸腾真实战斗回归（提案 §3.4 / §14.5）。
// 覆盖：仅 4 件可见；每锚点实例每日一次；180 TU 到期边界；对 dragon +3（与头盔/4件套相加）；
// 前三次真实攻击命中各疗 1D6、第四次不触发；miss/save-only/preview/AI 不消费；满血仍消费；
// source 失效（拆套）立即清 buff 且不返还当日次数；同日重穿不恢复；次日恢复；
// 整套四件实例转给另一成员后，该成员看到的是装备自身剩余额度（次数跟装备走，不跟人走）。
public partial class run_dragon_scale_dragon_blood_boil_regression : LifecycleTestSceneTree
{
    private static readonly StringName BoilSkillId = "equipment_dragon_scale_dragon_blood_boil";
    private static readonly StringName BoilStatusId = "dragon_scale_dragon_blood_boil";
    private static readonly StringName OathBindingId = "binding.gear_set.dragon_scale.4.dragonslayer_oath";
    private static readonly StringName BoilGrantId = "grant.dragon_scale.oath.dragon_blood_boil";
    private static readonly StringName TestSkillId = "test_dragon_blood_boil_hit";

    private static readonly (StringName ItemId, StringName SlotId)[] Members =
    {
        ("armor_dragon_scale_head", "head"),
        ("armor_dragon_scale_body", "body"),
        ("armor_dragon_scale_hands", "hands"),
        ("armor_dragon_scale_feet", "feet"),
    };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestGrantedOnlyAtFourPiecesAndDailyUsage();
            TestBoilAttackBonusVsDragon();
            TestBoilHealsOnThreeRealHits();
            TestBoilNotConsumedByMissSaveOnlyPreviewAi();
            TestBoilFullHealthHitStillConsumes();
            TestBoilExpiresAtExactDurationBoundary();
            TestSourceDeactivationClearsBuffAndDoesNotRefund();
            TestBoilUsageLedgerFollowsEquipmentAcrossMembers();
            TestHudSummaryUsesBattleLocalView();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Dragon Scale dragon blood boil regression"));
    }

    private void TestGrantedOnlyAtFourPiecesAndDailyUsage()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState threePiece = fixture.BuildSetUnit("boil_three_piece", new[] { 0, 1, 2 });
        PrimeUnit(threePiece, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_grant_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_grant",
            new[] { threePiece },
            new[] { enemy },
            worldStep: 27
        );
        _test.True(
            FindBoilEntry(fixture, threePiece, state, 27) == null,
            "仅 3 件时龙血沸腾不得出现在装备技能列表。"
        );

        BattleUnitState holder = fixture.BuildSetUnit("boil_full_set", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        state.SetUnit(holder);
        state.ally_unit_ids.Add(holder.unit_id);
        BattleAvailableSkillEntry entry = FindBoilEntry(fixture, holder, state, 27);
        _test.True(entry?.IsSelectable == true, "4 件时龙血沸腾应可选择。");
        if (entry == null)
            return;

        ForceUnitActing(state, holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            holder,
            entry,
            BoilSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"龙血沸腾真实预览应允许。logs={JoinLogs(preview)}"
        );
        _test.Eq(holder.GetCurrentAp(), 3, "预览不得提前扣除 AP。");
        _test.False(holder.HasStatusEffect(BoilStatusId), "预览不得提前施加龙血沸腾。");
        _test.True(
            FindBoilEntry(fixture, holder, state, 27)?.IsSelectable == true,
            "预览不得消费每日次数。"
        );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "龙血沸腾 IssueCommand 应返回事件批。");
        _test.Eq(holder.GetCurrentAp(), 2, "龙血沸腾应消耗 1 AP。");
        BattleStatusEffectState boil = holder.GetStatusEffect(BoilStatusId);
        _test.True(boil != null, "龙血沸腾应施加 buff。");
        if (boil != null)
        {
            _test.Eq(boil.duration, 180, "龙血沸腾应持续 180 TU。");
            _test.Eq(boil.stacks, 3, "龙血沸腾应初始 3 层治疗 charge。");
            _test.True(
                boil.remove_on_source_deactivated,
                "龙血沸腾应声明 remove_on_source_deactivated。"
            );
            _test.Eq(
                boil.source_provenance_binding_id,
                OathBindingId,
                "龙血沸腾 provenance 应记录四件套 binding。"
            );
            _test.Eq(
                boil.source_provenance_source_kind,
                new StringName("player_persistent_gear_set_threshold"),
                "龙血沸腾 provenance 应记录 gear-set threshold source kind。"
            );
        }

        _test.True(
            FindBoilEntry(fixture, holder, state, 27)?.IsSelectable == false,
            "同一世界日再次查询应显示每日次数已耗尽。"
        );
        holder.SetCurrentAp(3);
        holder.ResetPerTurnCharges();
        _test.True(
            FindBoilEntry(fixture, holder, state, 30)?.IsSelectable == true,
            "进入下一个世界日后龙血沸腾应恢复可用。"
        );

        EquipmentInstanceState anchor = holder.GetEquipmentView().GetEquippedInstance("head");
        _test.True(anchor != null, "锚点头盔实例应仍装备。");
        _test.Eq(
            anchor?.ability_usage_periods.Count ?? 0,
            1,
            "每日次数应写入锚点头盔实例账本。"
        );
        _test.Eq(
            anchor.ability_usage_periods[0].PeriodIndex,
            1,
            "世界步 27 应属于世界日 1（15 步/日）。"
        );
    }

    private void TestBoilAttackBonusVsDragon()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_attack", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragon = BuildEnemyUnit("boil_attack_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleUnitState plain = BuildEnemyUnit("boil_attack_plain", new Vector2I(4, 4), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_attack",
            new[] { holder },
            new[] { dragon, plain },
            worldStep: 27
        );
        _test.Eq(
            AttackBonusDelta(fixture, state, holder, dragon),
            4,
            "龙血沸腾激活前，头盔 +2 与四件套 +2 对 dragon 应为 +4。"
        );

        ActivateBoil(fixture, holder, state, 27);
        _test.Eq(
            AttackBonusDelta(fixture, state, holder, dragon),
            7,
            "龙血沸腾激活后对 dragon 攻击应为 +4+3=+7。"
        );
        _test.Eq(
            AttackBonusDelta(fixture, state, holder, plain),
            0,
            "龙血沸腾对非 dragon 目标不应加值。"
        );
    }

    private void TestBoilHealsOnThreeRealHits()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_heal", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 50, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_heal_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_heal",
            new[] { holder },
            new[] { enemy },
            worldStep: 27
        );
        ActivateBoil(fixture, holder, state, 27);
        holder.SetCurrentHp(50);

        for (int hit = 1; hit <= 3; hit++)
        {
            ResolveSpellHit(fixture, holder, enemy, state);
            _test.Eq(
                holder.GetCurrentHp(),
                50 + hit * 6,
                $"第 {hit} 次真实攻击命中应自疗 1D6（固定骰 6）并消费 1 层。"
            );
        }
        _test.False(
            holder.HasStatusEffect(BoilStatusId),
            "第三层消费后龙血沸腾应移除。"
        );

        ResolveSpellHit(fixture, holder, enemy, state);
        _test.Eq(holder.GetCurrentHp(), 68, "第四次命中不得再触发治疗（charge 已耗尽）。");
    }

    private void TestBoilNotConsumedByMissSaveOnlyPreviewAi()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_no_consume", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 50, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_no_consume_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_no_consume",
            new[] { holder },
            new[] { enemy },
            worldStep: 27
        );
        ActivateBoil(fixture, holder, state, 27);
        holder.SetCurrentHp(50);

        // miss：显式固定失败，避免角色/装备修正把阈值检定抬成命中。
        BattleTestFixture.ConfigureHitResolverForTests(fixture.Runtime, new FixedMissResolver());
        using var reactionBatch0 = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteLogicalAttack(
            fixture.Runtime, reactionBatch0, holder, new[] { BuildSpellEffect() },
            actionContext => fixture.Resolver.ResolveAttackEffects(
                holder,
                enemy,
                new[] { BuildSpellEffect() },
                new AttackCheckInput(
                requiredRoll: 21,
                naturalOneAutoMiss: true,
                naturalTwentyAutoHit: false,
                skillId: TestSkillId
            ),
                new AttackContext { EventBatch = reactionBatch0, DamageOriginKind = BattleDamageOriginKind.MainDirectEffect, Action = actionContext,  BattleState = state, SkillId = TestSkillId }
            )
        );
        _test.Eq(BoilStacks(holder), 3, "miss 不得消费 charge。");
        _test.Eq(holder.GetCurrentHp(), 50, "miss 不得治疗。");

        // save-only：无攻击检定的豁免伤害不算“命中”。
        fixture.Resolver.ResolveEffects(
            holder,
            enemy,
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    damageTag: "fire",
                    power: 8,
                    saveDc: 10,
                    saveAbility: "agility",
                    saveTag: "magic",
                    savePartialOnSuccess: true
                ),
            },
            DamageResolutionContext.ForSkill(TestSkillId).WithBattleState(state)
        );
        _test.Eq(BoilStacks(holder), 3, "save-only 伤害不得消费 charge。");
        _test.Eq(holder.GetCurrentHp(), 50, "save-only 伤害不得治疗。");

        // preview：canonical 预览不得消费。
        DamageResolutionContext previewContext = DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: TestSkillId
            )
            .WithBattleState(state)
            .WithAttackCheck();
        fixture.Resolver.PreviewDamageEffectTyped(
            holder,
            enemy,
            BuildSpellEffect(),
            previewContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(BoilStacks(holder), 3, "preview 不得消费 charge。");
        _test.Eq(holder.GetCurrentHp(), 50, "preview 不得治疗。");

        // AI：detached working set 评分不得消费。
        BattleDamagePreviewWorkingSet workingSet = BattleDamagePreviewWorkingSet.CreateDetached(
            holder,
            enemy,
            state
        );
        fixture.Resolver.PreviewDamageScoreOnWorkingSetTyped(
            workingSet,
            BuildSpellEffect(),
            previewContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(BoilStacks(holder), 3, "AI 估值不得消费 charge。");
        _test.Eq(holder.GetCurrentHp(), 50, "AI 估值不得治疗。");
    }

    private void TestBoilFullHealthHitStillConsumes()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_full_hp", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_full_hp_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_full_hp",
            new[] { holder },
            new[] { enemy },
            worldStep: 27
        );
        ActivateBoil(fixture, holder, state, 27);
        holder.SetCurrentHp(100);

        ResolveSpellHit(fixture, holder, enemy, state);
        _test.Eq(BoilStacks(holder), 2, "满血命中仍应消费 1 层。");
        _test.Eq(holder.GetCurrentHp(), 100, "满血命中不得溢出治疗。");
    }

    private void TestBoilExpiresAtExactDurationBoundary()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_expiry", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 50, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_expiry_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_expiry",
            new[] { holder },
            new[] { enemy },
            worldStep: 27
        );
        ActivateBoil(fixture, holder, state, 27);
        holder.SetCurrentHp(50);

        using (var timelineBatch = new BattleEventBatch())
        {
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, timelineBatch,
                BattleEffectOrigin.Timeline("timeline_tick"),
                () => fixture.Runtime._timeline_driver.ApplyTimelineStep(timelineBatch, 175)
            );
        }
        _test.True(
            holder.HasStatusEffect(BoilStatusId),
            "推进 175 TU 后龙血沸腾应仍在（180 TU 持续）。"
        );
        using (var timelineBatch = new BattleEventBatch())
        {
            BattleReactionRootTestHelper.ExecuteInReactionRoot(
                fixture.Runtime, timelineBatch,
                BattleEffectOrigin.Timeline("timeline_tick"),
                () => fixture.Runtime._timeline_driver.ApplyTimelineStep(timelineBatch, 5)
            );
        }
        _test.False(
            holder.HasStatusEffect(BoilStatusId),
            "推进到 180 TU 边界后龙血沸腾应到期。"
        );

        ResolveSpellHit(fixture, holder, enemy, state);
        _test.Eq(holder.GetCurrentHp(), 50, "到期后命中不得再治疗。");
    }

    private void TestSourceDeactivationClearsBuffAndDoesNotRefund()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_source_loss", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 50, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_source_loss_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_source_loss",
            new[] { holder },
            new[] { enemy },
            worldStep: 27
        );
        ActivateBoil(fixture, holder, state, 27);
        _test.True(holder.HasStatusEffect(BoilStatusId), "前提：龙血沸腾应已激活。");

        // 拆套：卸下胫甲使 4 件 source 失效，opt-in 清除应立即移除 buff。
        EquipmentInstanceState feet = holder.GetEquipmentView().PopEquippedInstance("feet");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.True(
            FindGearSetSource(holder) == null,
            "拆套后 gear-set threshold source 应消失。"
        );
        _test.False(
            holder.HasStatusEffect(BoilStatusId),
            "source 失效应立即清除声明 opt-in 的龙血沸腾 buff。"
        );

        // 同日重穿同一实例：source 恢复，但当日次数不返还、buff 不自动恢复。
        holder.GetEquipmentView().SetEquippedEntry("feet", feet.item_id, new StringName[] { "feet" }, feet);
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.True(
            FindGearSetSource(holder) != null,
            "重穿四件后 gear-set threshold source 应恢复。"
        );
        _test.False(
            holder.HasStatusEffect(BoilStatusId),
            "重穿后龙血沸腾 buff 不得自动恢复。"
        );
        _test.True(
            FindBoilEntry(fixture, holder, state, 27)?.IsSelectable == false,
            "同日卸下重穿不得恢复每日次数。"
        );
        holder.SetCurrentAp(3);
        holder.ResetPerTurnCharges();
        _test.True(
            FindBoilEntry(fixture, holder, state, 30)?.IsSelectable == true,
            "次日龙血沸腾应恢复可用。"
        );
    }

    private void TestBoilUsageLedgerFollowsEquipmentAcrossMembers()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState hero = fixture.BuildSetUnit("transfer_source", new[] { 0, 1, 2, 3 });
        PrimeUnit(hero, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState firstEnemy = BuildEnemyUnit(
            "boil_transfer_enemy_day1",
            new Vector2I(4, 3),
            withDragonTag: false
        );
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_transfer_source",
            new[] { hero },
            new[] { firstEnemy },
            worldStep: 27
        );
        ActivateBoil(fixture, hero, state, 27);
        EquipmentInstanceState heroAnchor = hero.GetEquipmentView().GetEquippedInstance("head");
        _test.True(heroAnchor != null, "源成员的锚点头盔实例应仍装备。");
        if (heroAnchor == null)
            return;
        StringName anchorInstanceId = heroAnchor.instance_id;
        _test.Eq(
            heroAnchor.ability_usage_periods.Count,
            1,
            "源成员使用后锚点实例账本应记录当日一次。"
        );

        // 同一组实例转给 companion：次数跟装备走，不跟人走。战斗内 usage 写在
        // battle-local 实例上，战斗结束 writeback 保留实例身份与账本；此处直接转移
        // 携带账本的实例，等价于 writeback 后再移交。
        fixture.TransferFullSetToCompanion(hero);
        BattleUnitState companion = fixture.BuildSingleActiveAlly("transfer_target");
        PrimeUnit(companion, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState secondEnemy = BuildEnemyUnit(
            "boil_transfer_enemy_companion",
            new Vector2I(4, 3),
            withDragonTag: false
        );
        BattleState companionState = fixture.SetupBattle(
            "dragon_scale_boil_transfer_target",
            new[] { companion },
            new[] { secondEnemy },
            worldStep: 27
        );
        EquipmentInstanceState companionAnchor = companion
            .GetEquipmentView()
            .GetEquippedInstance("head");
        _test.Eq(
            companionAnchor?.instance_id ?? new StringName(""),
            anchorInstanceId,
            "受让成员的锚点应保持同一装备实例。"
        );
        _test.Eq(
            companionAnchor?.ability_usage_periods.Count ?? 0,
            1,
            "当日 usage 账本应随装备实例一起转移。"
        );
        _test.True(
            FindGearSetSource(companion) != null,
            "受让成员穿齐四件后应重新投影 gear-set threshold source。"
        );
        BattleAvailableSkillEntry sameDayEntry = FindBoilEntry(fixture, companion, companionState, 27);
        _test.True(
            sameDayEntry == null || sameDayEntry.IsSelectable != true,
            "同一世界日受让成员只能看到该装备自身的剩余额度（当日已耗尽）。"
        );

        // 次日该装备恢复次数，受让成员可以正常使用并写入同一实例账本。
        companionState.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new Godot.Collections.Dictionary { ["world_step"] = 30 }
            )
        );
        ActivateBoil(fixture, companion, companionState, 30);
        _test.Eq(
            companionAnchor?.ability_usage_periods.Count ?? 0,
            2,
            "次日受让成员的使用应追加到同一锚点实例账本。"
        );
    }

    private void TestHudSummaryUsesBattleLocalView()
    {
        using BoilFixture fixture = BoilFixture.Build();
        BattleUnitState holder = fixture.BuildSetUnit("boil_hud", new[] { 0, 1, 2, 3 });
        PrimeUnit(holder, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildEnemyUnit("boil_hud_enemy", new Vector2I(4, 3), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_boil_hud",
            new[] { holder },
            new[] { enemy },
            worldStep: 27
        );
        ForceUnitActing(state, holder);

        BattleHudGearSetSummarySnapshot summary = BuildHudGearSetSummary(fixture, state, holder);
        _test.True(summary != null, "四件时 battle HUD 套装摘要应包含龙鳞套装。");
        if (summary == null)
            return;
        _test.Eq(summary.EquippedPieceCount, 4, "battle HUD 摘要应显示 4 件。");
        _test.Eq(summary.TotalPieceCount, 4, "battle HUD 摘要应显示总 4 件。");
        _test.Eq(summary.Thresholds.Count, 2, "battle HUD 摘要应包含两档阈值。");
        _test.True(
            summary.Thresholds[1].IsActive && summary.Thresholds[1].RequiredPieceCount == 4,
            "四件时 4 件阈值在 battle HUD 摘要中应激活。"
        );
        _test.Eq(summary.GrantedActions.Count, 1, "四件时应有一条龙血沸腾 granted action。");
        if (summary.GrantedActions.Count == 0)
            return;
        BattleHudGearSetGrantedActionSnapshot action = summary.GrantedActions[0];
        _test.Eq(action.GrantedActionId, BoilGrantId.ToString(), "granted action id 应为龙血沸腾授予。");
        _test.Eq(action.SkillId, BoilSkillId.ToString(), "granted action 应指向龙血沸腾技能。");
        _test.Eq(action.UsagePeriodKind, "per_world_day", "龙血沸腾应为每日周期。");
        _test.True(action.IsAvailable, "未使用时龙血沸腾在 battle HUD 摘要中应可用。");
        _test.Eq(action.RemainingUses, 1, "未使用时龙血沸腾应剩余 1 次。");
        _test.Eq(action.DisabledReason, "", "可用时不得有 disabled reason。");

        ActivateBoil(fixture, holder, state, 27);
        action = BuildHudGearSetSummary(fixture, state, holder)?.GrantedActions[0];
        _test.True(action != null, "已使用后龙血沸腾仍应出现在套装摘要。");
        if (action != null)
        {
            _test.False(action.IsAvailable, "当日已用后龙血沸腾在 battle HUD 摘要中应不可用。");
            _test.Eq(action.RemainingUses, 0, "当日已用后龙血沸腾应剩余 0 次。");
            _test.Eq(
                action.DisabledReason,
                "equipment_skill_usage_exhausted",
                "当日已用后应暴露 usage exhausted disabled reason。"
            );
        }

        // 战斗中卸下胫甲：battle-local view 掉回 3 件，4 件阈值与 granted action 应消失，
        // 不得复用入场前 Party snapshot。
        EquipmentInstanceState feet = holder.GetEquipmentView().PopEquippedInstance("feet");
        _test.True(feet != null, "拆套测试应能卸下龙鳞胫甲。");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        summary = BuildHudGearSetSummary(fixture, state, holder);
        _test.True(summary != null, "拆套后仍有三件，套装摘要应继续存在。");
        if (summary == null)
            return;
        _test.Eq(summary.EquippedPieceCount, 3, "卸下一件后 battle HUD 摘要应显示 3 件。");
        _test.False(
            summary.Thresholds[1].IsActive,
            "卸下一件后 4 件阈值在 battle HUD 摘要中应转为未激活。"
        );
        _test.Eq(
            summary.GrantedActions.Count,
            0,
            "卸下一件后龙血沸腾 granted action 应从 battle HUD 摘要消失。"
        );
    }

    private static BattleHudGearSetSummarySnapshot BuildHudGearSetSummary(
        BoilFixture fixture,
        BattleState state,
        BattleUnitState holder
    )
    {
        var context = new SnapshotTestRuntime
        {
            BattleState = state,
            BattleRuntime = fixture.Runtime,
            ItemDefinitions = fixture.Items,
            SkillDefinitions = fixture.Skills,
            EquipmentAbilityBindings = fixture.Bindings,
            TraitDefinitions = fixture.Traits,
            GearSetDefinitions = fixture.GearSets,
            WorldStep = 27,
        };
        BattleHudSnapshot hudSnapshot;
        using (var adapter = new BattleHudAdapter())
        {
            adapter.SetupRuntimeContext(context);
            hudSnapshot = adapter.BuildSnapshot(
                state,
                holder.GetAnchorCoord(),
                new StringName(""),
                "",
                "",
                Array.Empty<Vector2I>(),
                0,
                Array.Empty<StringName>(),
                new StringName(""),
                "龙鳞 HUD 遭遇",
                null
            );
        }
        foreach (BattleHudGearSetSummarySnapshot summary in hudSnapshot.GearSetSummaries)
        {
            if (summary?.GearSetId == "dragon_scale_set")
                return summary;
        }
        return null;
    }

    private void ActivateBoil(
        BoilFixture fixture,
        BattleUnitState holder,
        BattleState state,
        int worldStep
    )
    {
        BattleAvailableSkillEntry entry = FindBoilEntry(fixture, holder, state, worldStep);
        if (entry?.IsSelectable != true)
            throw new InvalidOperationException("dragon blood boil must be selectable in this scenario.");
        ForceUnitActing(state, holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            holder,
            entry,
            BoilSkillId
        );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        if (batch == null)
            throw new InvalidOperationException("dragon blood boil command must return a batch.");
        if (!holder.HasStatusEffect(BoilStatusId))
            throw new InvalidOperationException(
                "dragon blood boil must apply its status. logs="
                    + string.Join(" | ", batch.LogLinesTyped ?? System.Array.Empty<string>())
            );
    }

    private void ResolveSpellHit(
        BoilFixture fixture,
        BattleUnitState holder,
        BattleUnitState target,
        BattleState state
    )
    {
        using var reactionBatch1 = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteLogicalAttack(
            fixture.Runtime, reactionBatch1, holder, new[] { BuildSpellEffect() },
            actionContext => fixture.Resolver.ResolveAttackEffects(
                holder,
                target,
                new[] { BuildSpellEffect() },
                new AttackCheckInput(forceHitNoCrit: true, skillId: TestSkillId),
                new AttackContext { EventBatch = reactionBatch1, DamageOriginKind = BattleDamageOriginKind.MainDirectEffect, Action = actionContext,  BattleState = state, SkillId = TestSkillId }
            )
        );
    }

    private static CombatEffectDefinition BuildSpellEffect() =>
        TestSkillDefinitionProjection.BuildEffect("damage", damageTag: "fire", power: 4);

    private static int BoilStacks(BattleUnitState holder) =>
        holder.GetStatusEffect(BoilStatusId)?.stacks ?? 0;

    private int AttackBonusDelta(
        BoilFixture fixture,
        BattleState state,
        BattleUnitState holder,
        BattleUnitState target
    )
    {
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill(
            "dragon_scale_boil_attack"
        );
        BattleAttackRollModifierBundle bundle = fixture.Runtime
            .GetAttackCheckPolicyService()
            .BuildModifierBundle(
                fixture.Runtime.GetAttackCheckPolicyService().BuildSkillDefinitionAttackContext(
                    state,
                    holder,
                    target,
                    attackSkill,
                    "skill_attack_check",
                    "dragon_scale_boil_attack",
                    force_hit_no_crit: false
                )
            );
        return bundle.GetEffectiveModifierDelta();
    }

    private static BattleAvailableSkillEntry FindBoilEntry(
        BoilFixture fixture,
        BattleUnitState holder,
        BattleState state,
        int worldStep
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
                entry?.EntryRef.SkillId == BoilSkillId
                && entry.EquipmentBindingId == OathBindingId
                && entry.EquipmentGrantedActionId == BoilGrantId
            )
            {
                return entry;
            }
        }
        return null;
    }

    private static BattleEquipmentAbilitySourceReadView FindGearSetSource(BattleUnitState unit)
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source in
            unit?.GetEquipmentAbilitySourcesReadViewTyped()
                ?? new BattleEquipmentAbilitySourceListReadView(null)
        )
        {
            if (source?.SourceKind == EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold)
                return source;
        }
        return null;
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static void PrimeUnit(
        BattleUnitState unit,
        int hp,
        int maxHp,
        int ap,
        Vector2I coord
    )
    {
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
        unit.SetCombatResources(hp, mp: 30, stamina: 30, aura: 0, ap, movePoints: 4);
        unit.SetAnchorCoord(coord);
    }

    private static BattleUnitState BuildEnemyUnit(StringName unitId, Vector2I coord, bool withDragonTag)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 300);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.SetCombatResources(300, mp: 0, stamina: 0, aura: 0, ap: 2, movePoints: 2);
        unit.SetAnchorCoord(coord);
        unit.SetUnarmedWeaponProjectionTyped();
        unit.SetEquipmentView(new EquipmentState());
        if (withDragonTag)
            unit.ReplaceCreatureTypeTagsTyped(new StringName[] { "dragon" });
        return unit;
    }

    private sealed class BoilFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private BoilFixture(
            CharacterManagementModule characterManagement,
            PartyState partyState,
            BattleRuntimeModule runtime,
            FixedRollDamageResolver resolver,
            ContentSnapshot snapshot
        )
        {
            _characterManagement = characterManagement;
            _partyState = partyState;
            Runtime = runtime;
            Resolver = resolver;
            Items = snapshot.Items;
            Skills = snapshot.Skills;
            Bindings = snapshot.EquipmentAbilityBindings;
            Traits = snapshot.Traits;
            GearSets = snapshot.GearSets;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal FixedRollDamageResolver Resolver { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> Traits { get; }
        internal IReadOnlyDictionary<StringName, GearSetDefinition> GearSets { get; }

        internal static BoilFixture Build()
        {
            CharacterManagementModule characterManagement = null;
            BattleRuntimeModule runtime = null;
            try
            {
                ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
                PartyState partyState = BuildPartyState();
                characterManagement = new CharacterManagementModule();
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
                runtime = new BattleRuntimeModule();
                runtime.setup(
                    characterManagement,
                    snapshot.Skills,
                    item_defs: snapshot.Items,
                    trait_defs: snapshot.Traits,
                    equipment_ability_bindings: snapshot.EquipmentAbilityBindings
                );
                var resolver = new FixedRollDamageResolver(new GArray());
                BattleTestFixture.ConfigureDamageResolverForTests(runtime, resolver);
                BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver());
                return new BoilFixture(characterManagement, partyState, runtime, resolver, snapshot);
            }
            catch
            {
                BattleTestFixture.DisposeRuntime(runtime);
                characterManagement?.Dispose();
                throw;
            }
        }

        internal BattleUnitState BuildSetUnit(string label, IReadOnlyList<int> memberIndexes)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            foreach (int index in memberIndexes ?? Array.Empty<int>())
            {
                (StringName itemId, StringName slotId) = Members[index];
                member.equipment_state.SetEquippedEntry(
                    slotId,
                    itemId,
                    new[] { slotId },
                    EquipmentInstanceState.CreateInstance(itemId, $"eq_{label}_{itemId}")
                );
            }
            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(
                _partyState,
                null
            );
            if (units.Count != 1)
                throw new InvalidOperationException("Boil fixture must build exactly one ally.");
            units[0].faction_id = "ally";
            return units[0];
        }

        internal void TransferFullSetToCompanion(BattleUnitState sourceUnit)
        {
            EquipmentState sourceView = sourceUnit.GetEquipmentView();
            PartyMemberState companion = _partyState.GetMemberState("companion");
            companion.equipment_state = new EquipmentState();
            foreach ((StringName itemId, StringName slotId) in Members)
            {
                EquipmentInstanceState instance = sourceView.PopEquippedInstance(slotId);
                if (instance == null)
                    throw new InvalidOperationException($"transfer requires {itemId} equipped on source unit.");
                companion.equipment_state.SetEquippedEntry(slotId, itemId, new[] { slotId }, instance);
            }
            _partyState.active_member_ids.Clear();
            _partyState.active_member_ids.Add("companion");
        }

        internal BattleUnitState BuildSingleActiveAlly(string label)
        {
            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(
                _partyState,
                null
            );
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} must build exactly one active ally.");
            units[0].faction_id = "ally";
            return units[0];
        }

        internal BattleState SetupBattle(
            StringName battleId,
            IReadOnlyList<BattleUnitState> allies,
            IReadOnlyList<BattleUnitState> enemies,
            int worldStep
        )
        {
            BattleState state = BattleTestFixture.BuildFlatState(battleId, new Vector2I(8, 8));
            state.ReplaceEnvironmentSnapshot(
                BattleEnvironmentSnapshot.FromBattleStartContext(
                    new Godot.Collections.Dictionary { ["world_step"] = worldStep }
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
            var party = new PartyState();
            var member = new PartyMemberState
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
            // companion 默认不出战；只有成员间转移装备的用例会把它切为唯一 active 成员。
            var companion = new PartyMemberState
            {
                member_id = "companion",
                display_name = "Companion",
                progression = new UnitProgress
                {
                    unit_id = "companion",
                    display_name = "Companion",
                },
                equipment_state = new EquipmentState(),
            };
            party.SetMemberState(companion);
            return party;
        }
    }
}
