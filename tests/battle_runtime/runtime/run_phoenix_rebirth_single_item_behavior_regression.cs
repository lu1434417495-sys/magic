using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_phoenix_rebirth_single_item_behavior_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName CrownItemId = "armor_phoenix_rebirth_head";
    private static readonly StringName CrownBindingId =
        "binding.phoenix_rebirth.crown.nirvana";
    private static readonly StringName GauntletItemId = "armor_phoenix_rebirth_hands";
    private static readonly StringName GauntletBindingId =
        "binding.phoenix_rebirth.gauntlet.flames";
    private static readonly StringName GauntletStrikeSkillId =
        "equipment_phoenix_rebirth_gauntlet_fire_strike";
    private static readonly StringName GauntletStrikeGrantId =
        "grant.phoenix_rebirth.gauntlet.fire_strike";
    private static readonly StringName GauntletHealSkillId =
        "equipment_phoenix_rebirth_gauntlet_healing_flame";
    private static readonly StringName GauntletHealGrantId =
        "grant.phoenix_rebirth.gauntlet.healing_flame";
    private static readonly StringName FeetItemId = "armor_phoenix_rebirth_feet";
    private static readonly StringName FeetBindingId =
        "binding.phoenix_rebirth.feet.fire_step";
    private static readonly StringName FlameChargeSkillId =
        "equipment_phoenix_rebirth_flame_charge";
    private static readonly StringName FlameChargeGrantId =
        "grant.phoenix_rebirth.feet.flame_charge";
    private static readonly StringName FlameChargeVariantId = "flame_charge_line";
    private static readonly StringName PassiveTrailId = "phoenix_rebirth_fire_step";
    private static readonly StringName FlameChargeTrailId =
        "phoenix_rebirth_flame_charge_trail";
    private static readonly StringName NecklaceItemId = "acc_phoenix_rebirth_necklace";
    private static readonly StringName NecklaceBindingId =
        "binding.phoenix_rebirth.necklace.nirvana_fire";
    private static readonly StringName NecklaceSkillId =
        "equipment_phoenix_rebirth_necklace_nirvana_fire";
    private static readonly StringName NecklaceGrantId =
        "grant.phoenix_rebirth.necklace.nirvana_fire";
    private static readonly StringName AshRingItemId = "acc_phoenix_rebirth_ring_2";
    private static readonly StringName AshRingBindingId =
        "binding.phoenix_rebirth.ash_ring.fire";
    private static readonly StringName AshRingSkillId =
        "equipment_phoenix_rebirth_ash_ring_fire";
    private static readonly StringName AshRingGrantId =
        "grant.phoenix_rebirth.ash_ring.fire";
    private static readonly StringName RebirthRingItemId = "acc_phoenix_rebirth_ring_1";
    private static readonly StringName RebirthRingBindingId =
        "binding.phoenix_rebirth.rebirth_ring.life_rekindle";
    private static readonly StringName RebirthRingSkillId =
        "equipment_phoenix_rebirth_rebirth_ring_life_rekindle";
    private static readonly StringName RebirthRingGrantId =
        "grant.phoenix_rebirth.rebirth_ring.life_rekindle";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestRebirthRingHealsMissingHpAtLowHealthOncePerBattle();
            TestCrownFatalInterceptRecoversAndBursts();
            TestCrownRoll26FailsAndConsumesAttempt();
            TestCrownRevivesWhenNoEnemyCanReceiveBurst();
            TestGauntletAttackBonusProjectsIntoCanonicalAttackChecks();
            TestGauntletStrikeUsesUnarmedAttackAndSeparateFireSegment();
            TestGauntletHealingFlameHealsAndConsumesWorldDayUse();
            TestFeetNormalMoveAndDailyFlameChargeUseFormalRuntime();
            TestNecklaceHealsAndDispelsOneHarmfulStatus();
            TestAshRingHealAndDamageBranchesShareOneWorldDayGrant();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(
            _test.Finish("Phoenix Rebirth single-item behavior regression")
        );
    }

    private void TestGauntletAttackBonusProjectsIntoCanonicalAttackChecks()
    {
        int controlAttackBonus = 0;
        int controlRequiredRoll = 0;
        int fixedRoll = 0;
        using (PhoenixSinglesFixture controlFixture = PhoenixSinglesFixture.Build(Array.Empty<int>()))
        {
            BattleUnitState control = controlFixture.BuildEquippedUnit(
                FeetItemId,
                "feet",
                "eq_phoenix_gauntlet_attack_control",
                "gauntlet_attack_control"
            );
            BattleUnitState target = BuildUnit(
                "gauntlet_attack_control_target",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            try
            {
                controlAttackBonus = control.attribute_snapshot.GetValue(
                    AttributeService.ATTACK_BONUS
                );
                var hitResolver = new BattleHitResolver();
                AttackCheckInput check = hitResolver.BuildSkillAttackCheck(
                    control,
                    target,
                    controlFixture.Skills[GauntletStrikeSkillId]
                );
                controlRequiredRoll = check.RequiredRoll;
                fixedRoll = controlRequiredRoll - 1;
                _test.True(
                    fixedRoll > 1 && fixedRoll < 20,
                    "护手攻击加值边界用例必须使用非自然1/20的普通攻击骰。"
                );
                var thresholdResolver = new FixedThresholdRollHitResolver(fixedRoll);
                AttackResolutionMetadata miss = thresholdResolver.ResolveAttackMetadata(
                    control,
                    target,
                    check,
                    new AttackContext { SkillId = GauntletStrikeSkillId }
                );
                _test.False(
                    miss.AttackSuccess,
                    "未装备攻击+1护手时，固定边界骰应未命中。"
                );
            }
            finally
            {
                BattleTestFixture.DisposeBattleUnit(control);
                BattleTestFixture.DisposeBattleUnit(target);
            }
        }

        using (PhoenixSinglesFixture gauntletFixture = PhoenixSinglesFixture.Build(Array.Empty<int>()))
        {
            BattleUnitState holder = gauntletFixture.BuildEquippedUnit(
                GauntletItemId,
                "hands",
                "eq_phoenix_gauntlet_attack_bonus",
                "gauntlet_attack_bonus"
            );
            BattleUnitState target = BuildUnit(
                "gauntlet_attack_bonus_target",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            try
            {
                int gauntletAttackBonus = holder.attribute_snapshot.GetValue(
                    AttributeService.ATTACK_BONUS
                );
                _test.Eq(
                    gauntletAttackBonus,
                    controlAttackBonus + 1,
                    "凤凰护手应通过正式装备属性投影提供常驻攻击检定+1。"
                );
                var hitResolver = new BattleHitResolver();
                AttackCheckInput check = hitResolver.BuildSkillAttackCheck(
                    holder,
                    target,
                    gauntletFixture.Skills[GauntletStrikeSkillId]
                );
                _test.Eq(
                    check.AttackerAttackBonus,
                    gauntletAttackBonus,
                    "canonical命中检定必须读取护手投影后的attack_bonus。"
                );
                AttackCheckInput genericAttackCheck = hitResolver.BuildSkillAttackCheck(
                    holder,
                    target,
                    null
                );
                _test.Eq(
                    genericAttackCheck.AttackerAttackBonus,
                    gauntletAttackBonus,
                    "护手攻击+1应进入通用攻击检定，而不是只绑定火焰打击技能。"
                );
                _test.Eq(
                    check.RequiredRoll,
                    controlRequiredRoll - 1,
                    "攻击检定+1应把同一目标的所需命中骰降低1点。"
                );
                var thresholdResolver = new FixedThresholdRollHitResolver(fixedRoll);
                AttackResolutionMetadata hit = thresholdResolver.ResolveAttackMetadata(
                    holder,
                    target,
                    check,
                    new AttackContext { SkillId = GauntletStrikeSkillId }
                );
                _test.True(
                    hit.AttackSuccess,
                    "装备凤凰护手后，同一固定攻击骰应由未命中变为命中。"
                );
            }
            finally
            {
                BattleTestFixture.DisposeBattleUnit(holder);
                BattleTestFixture.DisposeBattleUnit(target);
            }
        }
    }

    private void TestRebirthRingHealsMissingHpAtLowHealthOncePerBattle()
    {
        using PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildEquippedUnit(
            RebirthRingItemId,
            "ring_1",
            "eq_phoenix_rebirth_ring",
            "rebirth_ring"
        );
        PrimeUnit(holder, "ally", new Vector2I(3, 3), hp: 51, hpMax: 100);
        BattleState state = fixture.SetupBattle(
            "phoenix_rebirth_ring_life_rekindle",
            new[] { holder },
            Array.Empty<BattleUnitState>(),
            worldStep: 5
        );
        ForceUnitActing(state, holder);

        BattleAvailableSkillEntry blocked = FindEquipmentSkill(
            fixture,
            holder,
            state,
            5,
            RebirthRingSkillId,
            RebirthRingBindingId,
            RebirthRingGrantId
        );
        _test.True(blocked != null, "生命重燃应出现在正式装备技能列表中。");
        _test.False(blocked?.IsSelectable ?? true, "生命高于50%时生命重燃必须不可选。");

        holder.SetCurrentHp(50);
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            5,
            RebirthRingSkillId,
            RebirthRingBindingId,
            RebirthRingGrantId
        );
        _test.True(entry.IsSelectable, "生命恰好50%时生命重燃必须可选。");
        holder.SetCurrentHp(20);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            holder,
            entry,
            RebirthRingSkillId
        );
        int hpBeforePreview = holder.GetCurrentHp();
        int apBeforePreview = holder.GetCurrentAp();
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "20/100生命时生命重燃正式预览应允许。");
        _test.Eq(holder.GetCurrentHp(), hpBeforePreview, "生命重燃预览不得修改真实HP。");
        _test.Eq(holder.GetCurrentAp(), apBeforePreview, "生命重燃预览不得消耗真实AP。");

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "生命重燃正式IssueCommand应成功。");
        _test.Eq(holder.GetCurrentHp(), 68, "20/100生命应恢复已损失生命的60%，最终为68。");
        _test.Eq(holder.GetCurrentAp(), 0, "生命重燃应消耗2 AP。");

        BattleAvailableSkillEntry spent = FindEquipmentSkill(
            fixture,
            holder,
            state,
            5,
            RebirthRingSkillId,
            RebirthRingBindingId,
            RebirthRingGrantId
        );
        _test.True(spent != null, "已使用的生命重燃仍应保留可解释的技能入口。");
        _test.False(spent?.IsSelectable ?? true, "生命重燃使用后本场不得再次选择。");
    }

    private void TestCrownFatalInterceptRecoversAndBursts()
    {
        using PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 4, 4, 4 });
        BattleUnitState holder = fixture.BuildEquippedUnit(
            CrownItemId,
            "head",
            "eq_phoenix_crown",
            "crown"
        );
        PrimeUnit(holder, "ally", new Vector2I(3, 3), hp: 1, hpMax: 100);
        BattleUnitState enemy = BuildUnit(
            "crown_enemy",
            "enemy",
            new Vector2I(5, 3),
            hp: 50,
            hpMax: 50
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_crown_fatal",
            new[] { holder },
            new[] { enemy },
            worldStep: 0
        );
        BattleEquipmentAbilityRuntimeService service =
            fixture.Runtime.GetEquipmentAbilityRuntimeService();
        service.ConfigureRollGateValuesForTests(new[] { 25 });

        using var batch = new BattleEventBatch();
        BattleFatalInterceptResult result = service.ResolveFatalIntercept(
            new BattleFatalInterceptContext
            {
                SourceUnit = enemy,
                TargetUnit = holder,
                BattleState = state,
                DeathContext = BattleDeathResolutionRules.NormalFatalContext(),
                HpBefore = 1,
                HpDamage = 2,
                ProjectedHp = -1,
                WorldStep = 0,
                Batch = batch,
                SaveContext = BattleSaveContext.Empty,
            }
        );

        _test.True(result.Intercepted, "凤凰头冠25%检定成功时应拦截普通致死伤害。");
        _test.Eq(result.WinningBindingId, CrownBindingId, "凤凰头冠应成为本次致死拦截胜者。");
        _test.Eq(holder.GetCurrentHp(), 25, "凤凰头冠应恢复至25%最大生命。");
        _test.Eq(enemy.GetCurrentHp(), 38, "固定骰4、4、4时，头冠爆发应造成3D10=12 fire。");

        holder.SetCurrentHp(1);
        BattleFatalInterceptResult spent = service.ResolveFatalIntercept(
            new BattleFatalInterceptContext
            {
                SourceUnit = enemy,
                TargetUnit = holder,
                BattleState = state,
                DeathContext = BattleDeathResolutionRules.NormalFatalContext(),
                HpBefore = 1,
                HpDamage = 2,
                ProjectedHp = -1,
                WorldStep = 0,
                SaveContext = BattleSaveContext.Empty,
            }
        );
        _test.False(spent.Intercepted, "同一场战斗内头冠正式尝试后不得再次拦截。");
        _test.True(
            spent.Attempts.Count == 1
                && spent.Attempts[0].Outcome
                    == BattleFatalInterceptAttemptOutcomeKind.Unavailable,
            "头冠第二次致死应明确返回per_battle次数耗尽。"
        );
    }

    private void TestCrownRoll26FailsAndConsumesAttempt()
    {
        using PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildEquippedUnit(
            CrownItemId,
            "head",
            "eq_phoenix_crown_roll_26",
            "crown_roll_26"
        );
        PrimeUnit(holder, "ally", new Vector2I(3, 3), hp: 1, hpMax: 100);
        BattleUnitState enemy = BuildUnit(
            "crown_roll_26_enemy",
            "enemy",
            new Vector2I(5, 3),
            hp: 50,
            hpMax: 50
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_crown_roll_26",
            new[] { holder },
            new[] { enemy },
            worldStep: 0
        );
        BattleEquipmentAbilityRuntimeService service =
            fixture.Runtime.GetEquipmentAbilityRuntimeService();
        service.ConfigureRollGateValuesForTests(new[] { 26 });

        BattleFatalInterceptResult failed = service.ResolveFatalIntercept(
            BuildFatalContext(holder, enemy, state)
        );
        _test.False(failed.Intercepted, "凤凰头冠D100=26时必须失败，不得恢复生命。");
        _test.Eq(holder.GetCurrentHp(), 1, "凤凰头冠检定失败不得改变佩戴者生命。");
        _test.True(
            failed.Attempts.Count == 1
                && failed.Attempts[0].Outcome
                    == BattleFatalInterceptAttemptOutcomeKind.RollFailed,
            "凤凰头冠D100=26应明确记录RollFailed。"
        );
        if (failed.Attempts.Count == 1)
        {
            _test.True(failed.Attempts[0].UsageConsumed, "凤凰头冠失败也必须消耗per_battle尝试。");
            _test.Eq(failed.Attempts[0].RolledValue, 26, "凤凰头冠失败结果应保留正式D100=26证据。");
        }

        BattleFatalInterceptResult spent = service.ResolveFatalIntercept(
            BuildFatalContext(holder, enemy, state)
        );
        _test.False(spent.Intercepted, "凤凰头冠D100失败后本场不得再次尝试。");
        _test.True(
            spent.Attempts.Count == 1
                && spent.Attempts[0].Outcome
                    == BattleFatalInterceptAttemptOutcomeKind.Unavailable,
            "凤凰头冠失败后的第二次致死应明确返回per_battle次数耗尽。"
        );
    }

    private void TestCrownRevivesWhenNoEnemyCanReceiveBurst()
    {
        using PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildEquippedUnit(
            CrownItemId,
            "head",
            "eq_phoenix_crown_empty_area",
            "crown_empty_area"
        );
        PrimeUnit(holder, "ally", new Vector2I(3, 3), hp: 1, hpMax: 100);
        BattleState state = fixture.SetupBattle(
            "phoenix_crown_empty_area",
            new[] { holder },
            Array.Empty<BattleUnitState>(),
            worldStep: 0
        );
        BattleEquipmentAbilityRuntimeService service =
            fixture.Runtime.GetEquipmentAbilityRuntimeService();
        service.ConfigureRollGateValuesForTests(new[] { 25 });

        using var batch = new BattleEventBatch();
        BattleFatalInterceptResult result = service.ResolveFatalIntercept(
            new BattleFatalInterceptContext
            {
                TargetUnit = holder,
                BattleState = state,
                DeathContext = BattleDeathResolutionRules.NormalFatalContext(),
                HpBefore = 1,
                HpDamage = 2,
                ProjectedHp = -1,
                WorldStep = 0,
                Batch = batch,
                SaveContext = BattleSaveContext.Empty,
            }
        );
        _test.True(result.Intercepted, "爆发范围内没有敌人时凤凰头冠仍必须完成复活。");
        _test.Eq(result.WinningBindingId, CrownBindingId, "空敌场景仍应由凤凰头冠赢得致死仲裁。");
        _test.Eq(holder.GetCurrentHp(), 25, "空敌场景仍应恢复25%最大生命。");
    }

    private void TestGauntletStrikeUsesUnarmedAttackAndSeparateFireSegment()
    {
        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 4, 5 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                GauntletItemId,
                "hands",
                "eq_phoenix_gauntlet_strike",
                "gauntlet_strike"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "gauntlet_enemy",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_gauntlet_strike",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                GauntletStrikeSkillId,
                GauntletBindingId,
                GauntletStrikeGrantId
            );
            _test.Eq(
                holder.GetWeaponProjectionReadViewTyped().Values.Family,
                new StringName("unarmed"),
                "火焰打击行为测试必须使用正式徒手投影。"
            );

            IssueUnitSkill(fixture.Runtime, state, holder, enemy, entry, GauntletStrikeSkillId);
            _test.Eq(holder.GetCurrentAp(), 1, "火焰打击正式执行应只消耗1 AP。");
            _test.Eq(
                enemy.GetCurrentHp(),
                41,
                "固定骰4、5时，火焰打击应造成1D8 blunt与1D10 fire共9点伤害。"
            );
        }

        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 4, 5 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                GauntletItemId,
                "hands",
                "eq_phoenix_gauntlet_fire_immune",
                "gauntlet_fire_immune"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "gauntlet_fire_immune_enemy",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            enemy.SetDamageResistanceTyped("fire", "immune");
            BattleState state = fixture.SetupBattle(
                "phoenix_gauntlet_fire_immune",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                0,
                GauntletStrikeSkillId,
                GauntletBindingId,
                GauntletStrikeGrantId
            );
            IssueUnitSkill(fixture.Runtime, state, holder, enemy, entry, GauntletStrikeSkillId);
            _test.Eq(
                enemy.GetCurrentHp(),
                46,
                "fire immune只应免疫独立1D10 fire段，1D8 blunt主段仍应造成固定4点伤害。"
            );
        }

        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 4, 4, 5, 5 }))
        {
            fixture.Runtime.ConfigureHitResolverForTests(new FixedCriticalHitResolver());
            BattleUnitState holder = fixture.BuildEquippedUnit(
                GauntletItemId,
                "hands",
                "eq_phoenix_gauntlet_critical",
                "gauntlet_critical"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "gauntlet_critical_enemy",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_gauntlet_critical",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                0,
                GauntletStrikeSkillId,
                GauntletBindingId,
                GauntletStrikeGrantId
            );
            IssueUnitSkill(fixture.Runtime, state, holder, enemy, entry, GauntletStrikeSkillId);
            _test.Eq(
                enemy.GetCurrentHp(),
                32,
                "固定暴击骰4+4与5+5时，1D8 blunt和独立1D10 fire两段都应翻倍为18点。"
            );
            _test.Eq(holder.GetCurrentAp(), 1, "火焰打击暴击也只能消耗1 AP。");
        }

        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(Array.Empty<int>()))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                GauntletItemId,
                "hands",
                "eq_phoenix_gauntlet_weapon_block",
                "gauntlet_weapon_block",
                equipBronzeSword: true
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "gauntlet_weapon_block_enemy",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_gauntlet_weapon_block",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            _test.True(
                holder.GetWeaponProjectionReadViewTyped().Values.Family != "unarmed",
                "持武器禁用用例必须走正式已装备武器投影。"
            );
            BattleAvailableSkillEntry blocked = FindEquipmentSkill(
                fixture,
                holder,
                state,
                0,
                GauntletStrikeSkillId,
                GauntletBindingId,
                GauntletStrikeGrantId
            );
            _test.True(blocked != null && !blocked.IsSelectable, "主手持武器时火焰打击入口应保留但禁用。");
            _test.Eq(
                blocked?.DisabledReason ?? new StringName(""),
                new StringName("equipment_skill_availability_blocked"),
                "主手持武器禁用火焰打击的原因应稳定。"
            );
        }
    }

    private void TestGauntletHealingFlameHealsAndConsumesWorldDayUse()
    {
        using PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(
            new[] { 4, 5, 4, 5, 4, 5, 4, 5 }
        );
        BattleUnitState holder = fixture.BuildEquippedUnit(
            GauntletItemId,
            "hands",
            "eq_phoenix_gauntlet_heal",
            "gauntlet_heal"
        );
        PrimeUnit(holder, "ally", Vector2I.Zero, hp: 40, hpMax: 50);
        BattleUnitState ally = BuildUnit(
            "gauntlet_ally",
            "ally",
            new Vector2I(1, 0),
            hp: 10,
            hpMax: 100
        );
        BattleUnitState distantAlly = BuildUnit(
            "gauntlet_distant_ally",
            "ally",
            new Vector2I(3, 0),
            hp: 10,
            hpMax: 100
        );
        BattleUnitState deadAlly = BuildUnit(
            "gauntlet_dead_ally",
            "ally",
            new Vector2I(0, 1),
            hp: 0,
            hpMax: 100
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_gauntlet_heal",
            new[] { holder, ally, distantAlly, deadAlly },
            Array.Empty<BattleUnitState>(),
            worldStep: 0
        );
        BattleAvailableSkillEntry initialEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 0,
            GauntletHealSkillId,
            GauntletBindingId,
            GauntletHealGrantId
        );

        AssertSkillPreviewBlocked(
            fixture.Runtime,
            state,
            holder,
            holder,
            initialEntry,
            GauntletHealSkillId,
            "治愈之火不得选择施法者自身。"
        );
        AssertSkillPreviewBlocked(
            fixture.Runtime,
            state,
            holder,
            distantAlly,
            initialEntry,
            GauntletHealSkillId,
            "治愈之火不得选择距离超过1格的友方。"
        );
        AssertSkillPreviewBlocked(
            fixture.Runtime,
            state,
            holder,
            deadAlly,
            initialEntry,
            GauntletHealSkillId,
            "治愈之火不得选择已经死亡的友方。"
        );

        for (int use = 1; use <= 3; use++)
        {
            PrepareNextAction(state, holder);
            BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                GauntletHealSkillId,
                GauntletBindingId,
                GauntletHealGrantId
            );
            IssueUnitSkill(fixture.Runtime, state, holder, ally, entry, GauntletHealSkillId);
            _test.Eq(holder.GetCurrentAp(), 1, $"治愈之火第{use}次正式执行应只消耗1 AP。");
            _test.Eq(ally.GetCurrentHp(), 10 + use * 9, $"治愈之火第{use}次应按固定2D10=9累计治疗相邻存活友方。");
        }
        EquipmentInstanceState instance = holder.GetEquipmentView().GetEquippedInstance("hands");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                GauntletHealGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            3,
            "治愈之火应把三次使用写入护手装备实例的世界日账本。"
        );

        PrepareNextAction(state, holder);
        BattleAvailableSkillEntry exhausted = FindEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 0,
            GauntletHealSkillId,
            GauntletBindingId,
            GauntletHealGrantId
        );
        _test.True(exhausted != null && !exhausted.IsSelectable, "治愈之火同日第4次入口应被正式账本禁用。");
        _test.Eq(
            exhausted?.DisabledReason ?? new StringName(""),
            new StringName("equipment_skill_usage_exhausted"),
            "治愈之火同日第4次禁用原因应稳定。"
        );
        int hpBeforeRejectedFourth = ally.GetCurrentHp();
        int apBeforeRejectedFourth = holder.GetCurrentAp();
        BattleCommand fourth = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            ally,
            exhausted,
            GauntletHealSkillId
        );
        BattlePreview fourthPreview = fixture.Runtime.PreviewCommand(fourth);
        _test.False(fourthPreview?.allowed ?? true, "治愈之火同日第4次正式预览必须拒绝。");
        BattleEventBatch rejectedFourth = fixture.Runtime.IssueCommand(fourth);
        _test.True(rejectedFourth != null, "治愈之火同日第4次拒绝仍应返回正式事件批次。");
        _test.Eq(ally.GetCurrentHp(), hpBeforeRejectedFourth, "被拒绝的第4次治愈之火不得治疗目标。");
        _test.Eq(holder.GetCurrentAp(), apBeforeRejectedFourth, "被拒绝的第4次治愈之火不得消耗AP。");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                GauntletHealGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            3,
            "被拒绝的第4次治愈之火不得污染每日账本。"
        );

        SetWorldStep(state, 15);
        PrepareNextAction(state, holder);
        BattleAvailableSkillEntry nextDay = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 15,
            GauntletHealSkillId,
            GauntletBindingId,
            GauntletHealGrantId
        );
        IssueUnitSkill(fixture.Runtime, state, holder, ally, nextDay, GauntletHealSkillId);
        _test.Eq(holder.GetCurrentAp(), 1, "次日刷新的治愈之火仍应消耗1 AP。");
        _test.Eq(ally.GetCurrentHp(), 46, "次日刷新后治愈之火应再次按固定2D10=9治疗。");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                GauntletHealGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(15)
            ),
            1,
            "进入下一个世界日后治愈之火应建立新的每日账本。"
        );
    }

    private void TestFeetNormalMoveAndDailyFlameChargeUseFormalRuntime()
    {
        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                FeetItemId,
                "feet",
                "eq_phoenix_feet_normal",
                "feet_normal"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "feet_normal_enemy",
                "enemy",
                new Vector2I(0, 1),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_feet_normal",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            ForceUnitActing(state, holder);
            BattleCommand move = new()
            {
                CommandKind = BattleCommandKind.Move,
                unit_id = holder.unit_id,
                target_coord = new Vector2I(2, 0),
            };
            BattlePreview movePreview = fixture.Runtime.PreviewCommand(move);
            _test.True(movePreview?.allowed == true, "凤凰胫甲普通移动应通过正式preview。 ");
            BattleEventBatch moveBatch = fixture.Runtime.IssueCommand(move);
            _test.True(moveBatch != null, "凤凰胫甲普通移动应通过正式IssueCommand执行。 ");
            _test.Eq(holder.GetAnchorCoord(), new Vector2I(2, 0), "普通移动应到达目标格。 ");
            for (int x = 0; x < 2; x++)
            {
                BattleTerrainEffectState trail = SingleTrailAt(
                    fixture.Runtime,
                    state,
                    new Vector2I(x, 0)
                );
                _test.Eq(
                    trail?.effect_id ?? new StringName(""),
                    PassiveTrailId,
                    "普通移动的每个离开格都应留下1D6火焰足迹。"
                );
                _test.Eq(trail?.remaining_tu ?? -1, 60, "普通火焰足迹应持续60TU。 ");
            }
            _test.Eq(
                TrailCountAt(fixture.Runtime, state, new Vector2I(2, 0)),
                0,
                "普通移动终点不是离开格，不应生成足迹。"
            );
            using BattleEventBatch contactBatch = new();
            BattleValidatedMoveExecutionResult contact =
                fixture.Runtime._movement_service.MoveUnitAlongValidatedPathTyped(
                    enemy,
                    new[]
                    {
                        new Vector2I(0, 1),
                        new Vector2I(0, 0),
                        new Vector2I(1, 0),
                    },
                    new Vector2I(1, 0),
                    contactBatch
                );
            _test.True(contact.Executed, "敌人应能在一次正式移动中跨过两个普通足迹格。 ");
            _test.Eq(
                enemy.GetCurrentHp(),
                46,
                "同一移动命令跨过同一足迹场的多个格时，1D6固定骰4只应结算一次。"
            );
        }

        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 3, 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                FeetItemId,
                "feet",
                "eq_phoenix_feet_charge",
                "feet_charge"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "feet_charge_enemy",
                "enemy",
                new Vector2I(0, 1),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_feet_charge",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                FlameChargeSkillId,
                FeetBindingId,
                FlameChargeGrantId
            );
            _test.Eq(holder.GetMovePointCapacity(), 2, "行为fixture的有效移动力容量应为2。 ");
            int mpBefore = holder.GetCurrentMp();
            int staminaBefore = holder.GetCurrentStamina();
            ForceUnitActing(state, holder);
            BattleCommand charge = new()
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = holder.unit_id,
                skill_entry_id = entry.EntryRef.SkillEntryId,
                skill_id = FlameChargeSkillId,
                skill_variant_id = FlameChargeVariantId,
                target_coord = new Vector2I(6, 0),
            };
            BattlePreview chargePreview = fixture.Runtime.PreviewCommand(charge);
            _test.True(
                chargePreview?.allowed == true,
                $"有效移动力容量2时，正式preview应允许最大6格火焰冲锋。logs={string.Join(" | ", chargePreview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
            BattleEventBatch chargeBatch = fixture.Runtime.IssueCommand(charge);
            _test.True(chargeBatch != null, "火焰冲锋应通过正式装备技能入口执行。 ");
            _test.Eq(holder.GetAnchorCoord(), new Vector2I(6, 0), "火焰冲锋应移动完整6格。 ");
            _test.Eq(holder.GetCurrentAp(), 0, "火焰冲锋应消耗2 AP。 ");
            _test.Eq(holder.GetCurrentMp(), mpBefore, "火焰冲锋不应消耗MP。 ");
            _test.Eq(holder.GetCurrentStamina(), staminaBefore, "火焰冲锋不应消耗体力。 ");
            for (int x = 0; x < 6; x++)
            {
                Vector2I coord = new(x, 0);
                BattleTerrainEffectState trail = SingleTrailAt(fixture.Runtime, state, coord);
                _test.Eq(
                    trail?.effect_id ?? new StringName(""),
                    FlameChargeTrailId,
                    "火焰冲锋的离开格应只生成2D6冲锋轨迹。"
                );
                _test.Eq(
                    TrailCountAt(fixture.Runtime, state, coord),
                    1,
                    "2D6冲锋轨迹应在创建前替换1D6普通足迹，不能重复生成。"
                );
                _test.Eq(trail?.remaining_tu ?? -1, 60, "火焰冲锋轨迹应持续60TU。 ");
            }
            using BattleEventBatch contactBatch = new();
            BattleValidatedMoveExecutionResult contact =
                fixture.Runtime._movement_service.MoveUnitAlongValidatedPathTyped(
                    enemy,
                    new[]
                    {
                        new Vector2I(0, 1),
                        new Vector2I(0, 0),
                        new Vector2I(1, 0),
                    },
                    new Vector2I(1, 0),
                    contactBatch
                );
            _test.True(contact.Executed, "敌人应能在一次正式移动中跨过两个冲锋轨迹格。 ");
            _test.Eq(
                enemy.GetCurrentHp(),
                43,
                "2D6冲锋轨迹固定骰3、4只应结算7点，不能再叠加普通1D6。"
            );

            EquipmentInstanceState instance = holder.GetEquipmentView().GetEquippedInstance("feet");
            _test.Eq(
                EquipmentAbilityUsageRuntime.GetUsedCount(
                    instance,
                    FlameChargeGrantId,
                    EquipmentAbilityUsagePeriodKind.PerWorldDay,
                    WorldTimeSystem.StepToDay(0)
                ),
                1,
                "火焰冲锋应把使用次数写入胫甲装备实例的世界日账本。"
            );
            BattleAvailableSkillEntry spent = FindEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                FlameChargeSkillId,
                FeetBindingId,
                FlameChargeGrantId
            );
            _test.True(spent != null && !spent.IsSelectable, "同一世界日火焰冲锋应显示已耗尽。 ");
            holder.SetCurrentAp(2);
            holder.ResetPerTurnCharges();
            BattleAvailableSkillEntry nextDay = FindEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 15,
                FlameChargeSkillId,
                FeetBindingId,
                FlameChargeGrantId
            );
            _test.True(nextDay?.IsSelectable == true, "进入下一个世界日后火焰冲锋应刷新。 ");
        }
    }

    private void TestNecklaceHealsAndDispelsOneHarmfulStatus()
    {
        using PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(
            new[] { 3, 4, 5, 3, 4, 5 }
        );
        BattleUnitState holder = fixture.BuildEquippedUnit(
            NecklaceItemId,
            "necklace",
            "eq_phoenix_necklace",
            "necklace"
        );
        PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
        BattleUnitState ally = BuildUnit(
            "necklace_ally",
            "ally",
            new Vector2I(1, 0),
            hp: 10,
            hpMax: 50
        );
        ally.SetStatusEffect(
            BuildHarmfulMagicStatus("test_disease", undispellable: false)
        );
        ally.SetStatusEffect(
            BuildHarmfulMagicStatus("test_hex", undispellable: false)
        );
        ally.SetStatusEffect(
            BuildHarmfulMagicStatus("test_legendary_curse", undispellable: true)
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_necklace",
            new[] { holder, ally },
            Array.Empty<BattleUnitState>(),
            worldStep: 0
        );
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 0,
            NecklaceSkillId,
            NecklaceBindingId,
            NecklaceGrantId
        );

        IssueUnitSkill(fixture.Runtime, state, holder, ally, entry, NecklaceSkillId);
        _test.Eq(holder.GetCurrentAp(), 1, "涅槃之火正式执行应消耗1 AP。");
        _test.Eq(ally.GetCurrentHp(), 22, "固定骰3、4、5时，涅槃之火应恢复3D8=12 HP。");
        int remainingEligible =
            (ally.HasStatusEffect("test_disease") ? 1 : 0)
            + (ally.HasStatusEffect("test_hex") ? 1 : 0);
        _test.Eq(remainingEligible, 1, "存在两个可驱散有害状态时，涅槃之火必须且只能移除一个。");
        _test.True(
            ally.HasStatusEffect("test_legendary_curse"),
            "传奇/undispellable诅咒不得被当前净化分支移除。"
        );
        EquipmentInstanceState instance = holder.GetEquipmentView().GetEquippedInstance("necklace");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                NecklaceGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(0)
            ),
            1,
            "涅槃之火应把使用写入项链装备实例的世界日账本。"
        );
        PrepareNextAction(state, holder);
        BattleAvailableSkillEntry spent = FindEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 0,
            NecklaceSkillId,
            NecklaceBindingId,
            NecklaceGrantId
        );
        _test.True(spent != null && !spent.IsSelectable, "涅槃之火同一世界日第二次应被禁用。");
        _test.Eq(
            spent?.DisabledReason ?? new StringName(""),
            new StringName("equipment_skill_usage_exhausted"),
            "涅槃之火同日耗尽原因应稳定。"
        );

        SetWorldStep(state, 15);
        PrepareNextAction(state, holder);
        BattleAvailableSkillEntry nextDay = FindRequiredEquipmentSkill(
            fixture,
            holder,
            state,
            worldStep: 15,
            NecklaceSkillId,
            NecklaceBindingId,
            NecklaceGrantId
        );
        IssueUnitSkill(fixture.Runtime, state, holder, ally, nextDay, NecklaceSkillId);
        _test.Eq(holder.GetCurrentAp(), 1, "次日刷新的涅槃之火仍应消耗1 AP。");
        _test.Eq(ally.GetCurrentHp(), 34, "次日刷新后涅槃之火应再次恢复固定3D8=12 HP。");
        _test.Eq(
            (ally.HasStatusEffect("test_disease") ? 1 : 0)
                + (ally.HasStatusEffect("test_hex") ? 1 : 0),
            0,
            "次日第二次正式施放应移除剩余的唯一可驱散有害状态。"
        );
        _test.True(ally.HasStatusEffect("test_legendary_curse"), "次日施放仍不得移除undispellable诅咒。");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetUsedCount(
                instance,
                NecklaceGrantId,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                WorldTimeSystem.StepToDay(15)
            ),
            1,
            "涅槃之火进入下一世界日后应建立新账本。"
        );
    }

    private void TestAshRingHealAndDamageBranchesShareOneWorldDayGrant()
    {
        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 4, 5 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                AshRingItemId,
                "ring_2",
                "eq_phoenix_ash_ring_heal",
                "ash_ring_heal"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState ally = BuildUnit(
                "ash_ring_ally",
                "ally",
                new Vector2I(1, 0),
                hp: 10,
                hpMax: 50
            );
            BattleUnitState enemy = BuildUnit(
                "ash_ring_enemy_after_heal",
                "enemy",
                new Vector2I(0, 1),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_ash_ring_heal",
                new[] { holder, ally },
                new[] { enemy },
                worldStep: 0
            );
            BattleAvailableSkillEntry healEntry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                AshRingSkillId,
                AshRingBindingId,
                AshRingGrantId
            );
            IssueUnitSkill(fixture.Runtime, state, holder, ally, healEntry, AshRingSkillId);
            _test.Eq(ally.GetCurrentHp(), 19, "灰烬之火选择友方时应恢复2D10=9 HP。");

            holder.ResetPerTurnCharges();
            holder.SetCurrentAp(2);
            ForceUnitActing(state, holder);
            BattleAvailableSkillEntry spent = FindEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                AshRingSkillId,
                AshRingBindingId,
                AshRingGrantId
            );
            _test.True(spent != null, "治疗分支使用后灰烬之火入口仍应可见。");
            _test.False(spent?.IsSelectable ?? true, "治疗分支应同时耗尽攻击分支的共享每日次数。");
            _test.Eq(
                spent?.DisabledReason ?? new StringName(""),
                new StringName("equipment_skill_usage_exhausted"),
                "灰烬之火共享次数耗尽原因应稳定。"
            );
        }

        using (PhoenixSinglesFixture fixture = PhoenixSinglesFixture.Build(new[] { 2, 3, 4 }))
        {
            BattleUnitState holder = fixture.BuildEquippedUnit(
                AshRingItemId,
                "ring_2",
                "eq_phoenix_ash_ring_attack",
                "ash_ring_attack"
            );
            PrimeUnit(holder, "ally", Vector2I.Zero, hp: 50, hpMax: 50);
            BattleUnitState enemy = BuildUnit(
                "ash_ring_enemy",
                "enemy",
                new Vector2I(1, 0),
                hp: 50,
                hpMax: 50
            );
            BattleState state = fixture.SetupBattle(
                "phoenix_ash_ring_attack",
                new[] { holder },
                new[] { enemy },
                worldStep: 0
            );
            BattleAvailableSkillEntry attackEntry = FindRequiredEquipmentSkill(
                fixture,
                holder,
                state,
                worldStep: 0,
                AshRingSkillId,
                AshRingBindingId,
                AshRingGrantId
            );
            IssueUnitSkill(fixture.Runtime, state, holder, enemy, attackEntry, AshRingSkillId);
            _test.Eq(enemy.GetCurrentHp(), 41, "灰烬之火选择敌人时应造成3D10=9 fire。");
        }
    }

    private static BattleStatusEffectState BuildHarmfulMagicStatus(
        StringName statusId,
        bool undispellable
    ) =>
        new()
        {
            status_id = statusId,
            source_unit_id = "enemy_source",
            power = 1,
            stacks = 1,
            duration = 120,
            counts_as_debuff_override = true,
            counts_as_debuff = true,
            dispellable_magic = true,
            dispellable_harmful_magic = true,
            undispellable = undispellable,
        };

    private static BattleFatalInterceptContext BuildFatalContext(
        BattleUnitState holder,
        BattleUnitState source,
        BattleState state
    ) =>
        new()
        {
            SourceUnit = source,
            TargetUnit = holder,
            BattleState = state,
            DeathContext = BattleDeathResolutionRules.NormalFatalContext(),
            HpBefore = 1,
            HpDamage = 2,
            ProjectedHp = -1,
            WorldStep = 0,
            SaveContext = BattleSaveContext.Empty,
        };

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

    private static void IssueUnitSkill(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(user);
        ForceUnitActing(state, user);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            user,
            target,
            entry,
            skillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"{skillId} preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        BattleEventBatch batch = runtime.IssueCommand(command);
        if (batch == null)
            throw new InvalidOperationException($"{skillId} IssueCommand returned null.");
    }

    private void AssertSkillPreviewBlocked(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        string message
    )
    {
        PrepareNextAction(state, user);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            user,
            target,
            entry,
            skillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        _test.False(preview?.allowed ?? true, message);
    }

    private static void PrepareNextAction(BattleState state, BattleUnitState unit)
    {
        unit.ResetPerTurnCharges();
        unit.SetCurrentAp(2);
        ForceUnitActing(state, unit);
    }

    private static void SetWorldStep(BattleState state, int worldStep)
    {
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["world_step"] = worldStep }
            )
        );
    }

    private static BattleTerrainEffectState SingleTrailAt(
        BattleRuntimeModule runtime,
        BattleState state,
        Vector2I coord
    )
    {
        BattleCellState cell = runtime?._grid_service?.GetCellState(state, coord);
        return cell?.timed_terrain_effects?.Count == 1
            ? cell.timed_terrain_effects[0]
            : null;
    }

    private static int TrailCountAt(
        BattleRuntimeModule runtime,
        BattleState state,
        Vector2I coord
    )
    {
        BattleCellState cell = runtime?._grid_service?.GetCellState(state, coord);
        return cell?.timed_terrain_effects?.Count ?? 0;
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        PhoenixSinglesFixture fixture,
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
        {
            throw new InvalidOperationException(
                $"equipment skill {skillId} is not selectable: {entry.DisabledReason}"
            );
        }
        return entry;
    }

    private static BattleAvailableSkillEntry FindEquipmentSkill(
        PhoenixSinglesFixture fixture,
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
            {
                return entry;
            }
        }
        return null;
    }

    private sealed class FixedThresholdRollHitResolver : BattleHitResolver
    {
        private readonly int _fixedRoll;

        internal FixedThresholdRollHitResolver(int fixedRoll)
        {
            _fixedRoll = Math.Clamp(fixedRoll, 1, 20);
        }

        protected override int RollTrueRandomAttackRange(
            int minValue,
            int maxValue,
            BattleState battleState
        )
        {
            battleState?.NextAttackRollNonce();
            return Math.Clamp(
                _fixedRoll,
                Math.Min(minValue, maxValue),
                Math.Max(minValue, maxValue)
            );
        }
    }

    private sealed class PhoenixSinglesFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private PhoenixSinglesFixture(
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

        internal static PhoenixSinglesFixture Build(IEnumerable<int> damageRolls)
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
            return new PhoenixSinglesFixture(
                characterManagement,
                partyState,
                runtime,
                snapshot
            );
        }

        internal BattleUnitState BuildEquippedUnit(
            StringName itemId,
            StringName slotId,
            StringName instanceId,
            string label,
            bool equipBronzeSword = false
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
            if (equipBronzeSword)
            {
                member.equipment_state.SetEquippedEntry(
                    "main_hand",
                    "bronze_sword",
                    new[] { new StringName("main_hand") },
                    EquipmentInstanceState.CreateInstance(
                        "bronze_sword",
                        $"eq_{label}_bronze_sword"
                    )
                );
            }
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
