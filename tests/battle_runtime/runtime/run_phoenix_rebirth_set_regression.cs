using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_phoenix_rebirth_set_regression : LifecycleTestSceneTree
{
    private static readonly StringName SetId = "phoenix_rebirth_set";
    private static readonly StringName BloodlineTraitId =
        "gear_set.phoenix_rebirth.3.phoenix_bloodline";
    private static readonly StringName EmberTraitId =
        "gear_set.phoenix_rebirth.7.ember_wings";
    private static readonly StringName SolarTraitId =
        "gear_set.phoenix_rebirth.10.solar_rebirth";
    private static readonly StringName EmberBindingId =
        "binding.gear_set.phoenix_rebirth.7.ember_wings";
    private static readonly StringName SolarBindingId =
        "binding.gear_set.phoenix_rebirth.10.solar_rebirth";
    private static readonly StringName SolarSkillId =
        "gear_set_phoenix_rebirth_solar_rebirth";
    private static readonly StringName SolarGrantId =
        "grant.phoenix_rebirth.solar_rebirth.skill";
    private static readonly StringName BlessingBindingId =
        "binding.phoenix_rebirth.status.blessing";
    private static readonly StringName EggBindingId =
        "binding.phoenix_rebirth.egg.nirvana";
    private static readonly StringName EggFormBindingId =
        "binding.phoenix_rebirth.status.egg_form";
    private static readonly StringName BadgeBindingId =
        "binding.phoenix_rebirth.badge.fire_shelter";
    private static readonly StringName BlessingSkillId =
        "equipment_phoenix_rebirth_badge_blessing";
    private static readonly StringName BlessingGrantId =
        "grant.phoenix_rebirth.badge.blessing";
    private static readonly StringName RebirthRingBindingId =
        "binding.phoenix_rebirth.rebirth_ring.life_rekindle";
    private static readonly StringName RebirthRingSkillId =
        "equipment_phoenix_rebirth_rebirth_ring_life_rekindle";
    private static readonly StringName RebirthRingGrantId =
        "grant.phoenix_rebirth.rebirth_ring.life_rekindle";
    private static readonly StringName EmberStatusId = "phoenix_form_ember";
    private static readonly StringName GoldenStatusId = "phoenix_form_golden";
    private static readonly StringName BlessingStatusId = "phoenix_blessing";
    private static readonly StringName EggFormStatusId = "phoenix_form_egg";
    private static readonly StringName PhoenixAppendGroupId = "phoenix_attack_append";

    private static readonly (StringName ItemId, StringName SlotId)[] Members =
    {
        ("armor_phoenix_rebirth_head", "head"),
        ("armor_phoenix_rebirth_body", "body"),
        ("armor_phoenix_rebirth_hands", "hands"),
        ("armor_phoenix_rebirth_feet", "feet"),
        ("acc_phoenix_rebirth_cloak", "cloak"),
        ("acc_phoenix_rebirth_necklace", "necklace"),
        ("acc_phoenix_rebirth_ring_1", "ring_1"),
        ("acc_phoenix_rebirth_ring_2", "ring_2"),
        ("acc_phoenix_rebirth_trinket", "special_trinket"),
        ("acc_phoenix_rebirth_badge", "badge"),
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
            TestRealContentAndFullSetProjection();
            TestFireMitigationUpgradesAtSevenPieces();
            TestRebirthRingUsesRealLowHpHealingSkill();
            TestEmberWingsUsesFinalizedDamagePathAndNonWeaponDamage();
            TestSolarRebirthExecutesRealAreaEffectsAndConsumesDailyUse();
            TestPowerWordKillCanOnlyBeRecoveredByTenPieceEgg();
            TestPhoenixEggUsesMonthlyFatalInterceptAndExecutesSuccessActions();
            TestBadgeBlessingPersistsAfterSourceRemovalAndConsumesDailyUse();
            TestPhoenixBonusDamageSourcesUseExclusivePriorityFallback();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Phoenix Rebirth set regression"));
    }

    private void TestRebirthRingUsesRealLowHpHealingSkill()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildFullSetUnit("life_rekindle");
        EquipmentInstanceState ringBeforeUse = holder
            .GetEquipmentView()
            .GetEquippedInstance("ring_1");
        if (ringBeforeUse == null)
            throw new InvalidOperationException("Phoenix fixture must equip rebirth ring_1.");
        ringBeforeUse.current_durability = 17;
        ringBeforeUse.trait_instances.Add(
            TraitInstanceState.Create(
                "trait_phoenix_ring_preserve",
                "test.phoenix_ring.preserve",
                TraitSourceKind.EquipmentRoll,
                ringBeforeUse.instance_id,
                rank: 2,
                stacks: 3,
                new[] { TraitRollValueState.CreateInt("seed", 17) }
            )
        );
        EquipmentInstanceState expectedRing = ringBeforeUse.DuplicateState();
        PrimeUnit(holder, hp: 20, maxHp: 100, ap: 2, new Vector2I(3, 3));
        BattleState state = fixture.SetupBattle(
            "phoenix_rebirth_ring_life_rekindle",
            new[] { holder },
            Array.Empty<BattleUnitState>(),
            worldStep: 5
        );
        ForceUnitActing(state, holder);
        BattleAvailableSkillEntry entry = FindEquipmentSkillEntry(
            fixture,
            holder,
            state,
            5,
            RebirthRingSkillId,
            RebirthRingBindingId,
            RebirthRingGrantId
        );
        _test.True(entry?.IsSelectable == true, "20/100生命时重生之戒主动治疗应可选择。");
        if (entry == null)
            return;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            holder,
            entry,
            RebirthRingSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"重生之戒正式预览应允许。logs={JoinLogs(preview)}"
        );
        _test.Eq(holder.GetCurrentHp(), 20, "重生之戒预览不得提前治疗。");
        _test.Eq(holder.GetCurrentAp(), 2, "重生之戒预览不得提前扣除AP。");

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "重生之戒正式IssueCommand应返回事件批。");
        _test.Eq(holder.GetCurrentHp(), 68, "重生之戒应恢复已损失生命的60%。");
        _test.Eq(holder.GetCurrentAp(), 0, "重生之戒主动治疗应消耗2 AP。");
        BattleAvailableSkillEntry spent = FindEquipmentSkillEntry(
            fixture,
            holder,
            state,
            5,
            RebirthRingSkillId,
            RebirthRingBindingId,
            RebirthRingGrantId
        );
        _test.True(spent != null && !spent.IsSelectable, "生命重燃使用后本场应显示已耗尽。");
        _test.False(
            fixture.Bindings.TryGetValue(RebirthRingBindingId, out EquipmentAbilityBindingDefinition ringBinding)
                && ringBinding.FatalIntercepts.Count > 0,
            "重生之戒正式binding不得残留致死拦截。"
        );
        AssertRebirthRingPreserved(
            expectedRing,
            holder.GetEquipmentView().GetEquippedInstance("ring_1")
        );
    }

    private void TestRealContentAndFullSetProjection()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(Array.Empty<int>());
        _test.True(
            fixture.GearSets.TryGetValue(SetId, out GearSetDefinition set),
            "真实内容快照应注册凤凰重生套装。"
        );
        if (set == null)
            return;

        _test.Eq(set.MemberItemIds.Count, 10, "凤凰重生必须包含十件既有成员装备。");
        _test.Eq(set.Thresholds.Count, 4, "凤凰重生应声明3/5/7/10四个非统一阈值。");
        int[] expectedThresholds = { 3, 5, 7, 10 };
        int totalPrice = 0;
        for (int index = 0; index < Members.Length; index++)
        {
            (StringName itemId, _) = Members[index];
            _test.True(
                fixture.Items.TryGetValue(itemId, out ItemDefinition item),
                $"真实内容快照缺少凤凰套装成员 {itemId}。"
            );
            if (item != null)
                totalPrice += item.BasePrice;
        }
        _test.Eq(totalPrice, 150000, "十件既有装备的基础价格总和必须保持150000不变。");
        for (int index = 0; index < expectedThresholds.Length; index++)
        {
            _test.Eq(
                set.Thresholds[index].RequiredPieceCount,
                expectedThresholds[index],
                $"第 {index + 1} 个套装阈值不符合3/5/7/10设计。"
            );
        }

        TraitDefinition bloodline = fixture.Traits[BloodlineTraitId];
        _test.Eq(
            bloodline.DamageResistanceEntries[0].DamageTag,
            new StringName("freeze"),
            "3件套应使用正式freeze伤害标签。"
        );
        _test.Eq(
            bloodline.DamageResistanceEntries[0].MitigationTier,
            new StringName("half"),
            "3件套应投影为离散half减伤。"
        );
        _test.Eq(bloodline.SaveBonusEntries[0].Bonus, 2, "3件套体质豁免应+2。");
        GearSetThresholdDefinition heartfire = set.GetThresholdById("undying_heartfire");
        _test.True(heartfire != null, "5件套不灭心火阈值必须存在。");
        _test.Eq(
            heartfire?.AttributeModifiers.Count ?? 0,
            1,
            "5件套纯静态生命加值应只走阈值直接属性通道。"
        );
        if (heartfire?.AttributeModifiers.Count > 0)
        {
            AttributeModifierDefinition modifier = heartfire.AttributeModifiers[0];
            _test.Eq(modifier.AttributeId, new StringName("hp_max"), "5件套应修改hp_max。");
            _test.Eq(modifier.Value, 20, "5件套应增加20最大生命。");
            _test.Eq(modifier.SourceType, new StringName("gear_set"), "阈值属性来源类型应由投影统一盖戳。");
            _test.Eq(
                modifier.SourceId,
                new StringName("gear_set::phoenix_rebirth_set::undying_heartfire"),
                "阈值属性来源ID应包含套装与阈值身份。"
            );
        }
        _test.Eq(
            heartfire?.GrantedTraitIds.Count ?? -1,
            0,
            "纯静态5件套不应再生成重复的派生trait。"
        );
        TraitDefinition emberWings = fixture.Traits[EmberTraitId];
        _test.Eq(emberWings.DamageResistanceEntries.Count, 1, "7件套应声明一条伤害抗性。" );
        if (emberWings.DamageResistanceEntries.Count > 0)
        {
            _test.Eq(
                emberWings.DamageResistanceEntries[0].DamageTag,
                new StringName("fire"),
                "7件套伤害抗性应作用于fire。"
            );
            _test.Eq(
                emberWings.DamageResistanceEntries[0].MitigationTier,
                new StringName("immune"),
                "7件套应把头冠的fire half升级为fire immune。"
            );
        }

        BattleUnitState holder = fixture.BuildFullSetUnit("projection");
        foreach (StringName traitId in new[]
        {
            BloodlineTraitId,
            EmberTraitId,
            SolarTraitId,
        })
        {
            _test.True(holder.HasEffectiveTrait(traitId), $"十件装备后应激活 {traitId}。");
        }
        _test.False(
            holder.HasEffectiveTrait("gear_set.phoenix_rebirth.5.undying_heartfire"),
            "5件套纯静态属性不应再投影空转trait。"
        );
        GearSetEvaluationSnapshot fullSetEvaluation = GearSetEvaluationService.Evaluate(
            holder.GetEquipmentView(),
            fixture.Items,
            fixture.GearSets
        );
        _test.Eq(fullSetEvaluation.AttributeModifiers.Count, 1, "完整套装应只产生一条阈值直接属性。");
        _test.Eq(
            fullSetEvaluation.AttributeModifiers[0].SourceId,
            new StringName("gear_set::phoenix_rebirth_set::undying_heartfire"),
            "完整套装的生命加值应保留稳定阈值provenance。"
        );
        AssertGearSetSource(holder, EmberBindingId);
        AssertGearSetSource(holder, SolarBindingId);
        BattleEquipmentAbilitySourceReadView eggSource = FindSource(holder, EggBindingId);
        _test.True(eggSource != null, "10件套完整时凤凰蛋能力源应被投影。" );
        if (eggSource != null)
        {
            _test.Eq(
                eggSource.SourceKind,
                EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                "凤凰蛋应保留本体装备来源，以便月度次数写入凤凰蛋实例。"
            );
        }
        _test.True(
            fixture.Bindings.ContainsKey(RebirthRingBindingId),
            "重生之戒的正式低血主动治疗binding应注册。"
        );
        _test.True(
            fixture.Bindings.ContainsKey("binding.phoenix_rebirth.cloak.fatal_ember"),
            "烈焰披风的正式濒死拦截应注册。"
        );

        holder.GetEquipmentView().PopEquippedInstance("badge");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.False(holder.HasEffectiveTrait(SolarTraitId), "战斗内移除第十件装备后应立即关闭10件阈值。");
        _test.True(holder.HasEffectiveTrait(EmberTraitId), "战斗内变为9件时仍应保留7件阈值。");
        _test.True(FindSource(holder, SolarBindingId) == null, "换装刷新后不得残留10件套能力来源。");
        _test.True(FindSource(holder, EggBindingId) == null, "换装降为9件后凤凰蛋能力必须立即关闭。");
        _test.True(FindSource(holder, EmberBindingId) != null, "换装刷新后应保留7件套能力来源。");
    }

    private void TestPowerWordKillCanOnlyBeRecoveredByTenPieceEgg()
    {
        using (PhoenixFixture fullFixture = PhoenixFixture.Build(new[] { 4, 4, 4 }))
        {
            BattleUnitState holder = fullFixture.BuildFullSetUnit("pwk_full_set");
            PrimeUnit(holder, hp: 1, maxHp: 100, ap: 3, new Vector2I(3, 3));
            BattleUnitState enemy = BuildUnit(
                "pwk_full_set_enemy",
                "enemy",
                hp: 100,
                maxHp: 100,
                new Vector2I(4, 3)
            );
            BattleState state = fullFixture.SetupBattle(
                "phoenix_egg_power_word_kill_full_set",
                new[] { holder },
                new[] { enemy },
                worldStep: 449
            );

            fullFixture.Runtime.GetDamageResolver().ResolveEffects(
                enemy,
                holder,
                new[] { BuildPowerWordKillEffect() },
                DamageResolutionContext.FromDictionary(
                    new GDictionary { ["save_roll_override"] = 1 }
                ).WithBattleState(state)
            );

            _test.True(holder.IsAlive(), "完整10件套的凤凰蛋应能从律令死亡中复活佩戴者。" );
            _test.Eq(holder.GetCurrentHp(), 30, "律令死亡触发凤凰蛋后应恢复30%最大生命。" );
            _test.True(holder.HasStatusEffect(EggFormStatusId), "律令死亡触发凤凰蛋后应进入火焰化身。" );
            _test.Eq(enemy.GetCurrentHp(), 88, "律令死亡触发凤凰蛋后仍应执行3D10爆发。" );
        }

        using (PhoenixFixture ninePieceFixture = PhoenixFixture.Build(Array.Empty<int>()))
        {
            BattleUnitState holder = ninePieceFixture.BuildFullSetUnit("pwk_nine_piece");
            holder.GetEquipmentView().PopEquippedInstance("badge");
            ninePieceFixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
            _test.True(FindSource(holder, EggBindingId) == null, "9件套不得投影凤凰蛋复活能力。" );
            PrimeUnit(holder, hp: 1, maxHp: 100, ap: 3, new Vector2I(3, 3));
            BattleUnitState enemy = BuildUnit(
                "pwk_nine_piece_enemy",
                "enemy",
                hp: 100,
                maxHp: 100,
                new Vector2I(4, 3)
            );
            BattleState state = ninePieceFixture.SetupBattle(
                "phoenix_egg_power_word_kill_nine_piece",
                new[] { holder },
                new[] { enemy },
                worldStep: 449
            );

            ninePieceFixture.Runtime.GetDamageResolver().ResolveEffects(
                enemy,
                holder,
                new[] { BuildPowerWordKillEffect() },
                DamageResolutionContext.FromDictionary(
                    new GDictionary { ["save_roll_override"] = 1 }
                ).WithBattleState(state)
            );

            _test.False(holder.IsAlive(), "缺少任意一件时，头冠、披风等普通保护均不得挡住律令死亡。" );
            _test.False(holder.HasStatusEffect(EggFormStatusId), "9件套死亡后不得生成凤凰蛋火焰化身。" );
        }
    }

    private void TestFireMitigationUpgradesAtSevenPieces()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildFullSetUnit("fire_mitigation_threshold");
        holder.GetEquipmentView().PopEquippedInstance("cloak");
        holder.GetEquipmentView().PopEquippedInstance("ring_2");
        holder.GetEquipmentView().PopEquippedInstance("badge");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);

        _test.True(holder.HasEffectiveTrait(EmberTraitId), "正好7件时应激活余烬展翼阈值。" );
        _test.Eq(
            holder.GetDamageResistanceTyped("fire"),
            new StringName("immune"),
            "正好7件时fire减半应升级为fire免疫。"
        );

        PrimeUnit(holder, hp: 100, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildUnit(
            "fire_mitigation_enemy",
            "enemy",
            hp: 100,
            maxHp: 100,
            new Vector2I(4, 3)
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_fire_mitigation_threshold",
            new[] { holder },
            new[] { enemy },
            worldStep: 0
        );
        fixture.Runtime
            .GetDamageResolver()
            .ApplyTaggedDirectDamageToTargetTyped(holder, 20, "fire", enemy, state);
        _test.Eq(holder.GetCurrentHp(), 100, "7件套的20点fire伤害应被完全免疫。" );

        holder.GetEquipmentView().PopEquippedInstance("special_trinket");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.False(holder.HasEffectiveTrait(EmberTraitId), "降为6件后应关闭余烬展翼阈值。" );
        _test.Eq(
            holder.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "降为6件后应回落到头冠单件的fire half。"
        );
        holder.SetCurrentHp(100);
        fixture.Runtime
            .GetDamageResolver()
            .ApplyTaggedDirectDamageToTargetTyped(holder, 20, "fire", enemy, state);
        _test.Eq(holder.GetCurrentHp(), 90, "6件时20点fire伤害应由头冠减半为10点。" );
    }

    private void TestEmberWingsUsesFinalizedDamagePathAndNonWeaponDamage()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(new[] { 4, 5 });
        BattleUnitState holder = fixture.BuildFullSetUnit("ember");
        PrimeUnit(holder, hp: 25, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildUnit(
            "ember_enemy",
            "enemy",
            hp: 100,
            maxHp: 100,
            new Vector2I(4, 3)
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_ember_finalized_damage",
            new[] { holder },
            new[] { enemy },
            worldStep: 12
        );

        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            effectType: "damage",
            effectTargetTeamFilter: "enemy",
            power: 5,
            damageTag: "physical_blunt"
        );
        BattleUnitState previewHolder = holder.DuplicateForPreview();
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveDamageTakenFinalized(
            new BattleEquipmentAbilityDamageAppliedContext
            {
                SourceUnit = previewHolder,
                TargetUnit = enemy,
                BattleState = state,
                HpDamage = 5,
                IsPreview = true,
            }
        );
        _test.Eq(previewHolder.GetCurrentHp(), 34, "预览应使用2D8期望值9治疗预览副本。");
        _test.True(previewHolder.HasStatusEffect(EmberStatusId), "预览副本应展示余烬形态。");
        _test.Eq(holder.GetCurrentHp(), 25, "预览不得修改正式单位生命。");
        _test.False(holder.HasStatusEffect(EmberStatusId), "预览不得把余烬形态写回正式单位。");

        fixture.Runtime.GetDamageResolver().ResolveEffects(
            enemy,
            holder,
            new[] { damage },
            DamageResolutionContext.Empty().WithBattleState(state)
        );
        _test.Eq(
            holder.GetCurrentHp(),
            29,
            "正式5点伤害结算至20生命后，应沿伤害resolver钩子恢复固定骰4+5。"
        );
        BattleStatusEffectState ember = holder.GetStatusEffect(EmberStatusId);
        _test.True(ember != null, "首次低血正式伤害后应进入余烬形态。");
        _test.Eq(ember?.duration ?? -1, 120, "余烬形态持续时间应为120 TU。");

        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> nonWeaponDice =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceOnHit(
                new BattleEquipmentAbilityBonusDamageDiceContext
                {
                    SourceUnit = holder,
                    TargetUnit = enemy,
                    BattleState = state,
                    AttackSucceeded = true,
                    IncludesWeaponDamage = false,
                }
            );
        _test.True(
            HasBonusDice(nonWeaponDice, EmberBindingId, 1, 4, "fire"),
            "余烬形态应给徒手或法术主直接伤害段追加1D4 fire。"
        );

        holder.SetCurrentHp(20);
        bool repeated = fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveDamageTakenFinalized(
            new BattleEquipmentAbilityDamageAppliedContext
            {
                SourceUnit = holder,
                TargetUnit = enemy,
                BattleState = state,
                HpDamage = 1,
            }
        );
        _test.False(repeated, "同一场战斗第二次低血伤害不得重复触发余烬展翼。");
        _test.Eq(holder.GetCurrentHp(), 20, "第二次低血伤害不得再次治疗。");
    }

    private void TestSolarRebirthExecutesRealAreaEffectsAndConsumesDailyUse()
    {
        var rolls = new List<int>();
        for (int index = 0; index < 40; index++)
            rolls.Add(5);
        using PhoenixFixture fixture = PhoenixFixture.Build(rolls);
        BattleUnitState holder = fixture.BuildFullSetUnit("solar");
        PrimeUnit(holder, hp: 20, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState allyC = BuildUnit("ally_c", "ally", 10, 100, new Vector2I(3, 2));
        BattleUnitState allyA = BuildUnit("ally_a", "ally", 20, 100, new Vector2I(2, 3));
        BattleUnitState allyB = BuildUnit("ally_b", "ally", 30, 100, new Vector2I(4, 3));
        BattleUnitState allyD = BuildUnit("ally_d", "ally", 40, 100, new Vector2I(3, 4));
        BattleUnitState allyE = BuildUnit("ally_e", "ally", 45, 100, new Vector2I(2, 2));
        BattleUnitState enemy = BuildUnit("solar_enemy", "enemy", 100, 100, new Vector2I(4, 4));
        BattleState state = fixture.SetupBattle(
            "phoenix_solar_rebirth",
            new[] { holder, allyC, allyA, allyB, allyD, allyE },
            new[] { enemy },
            worldStep: 27
        );
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = EmberStatusId,
                source_unit_id = holder.unit_id,
                duration = 120,
                stacks = 1,
            }
        );
        _test.True(holder.HasStatusEffect(EmberStatusId), "太阳涅槃测试必须先建立真实余烬形态前置状态。");

        BattleAvailableSkillEntry entry = FindSolarEntry(fixture, holder, state, 27);
        _test.True(entry?.IsSelectable == true, "生命不高于50%时太阳涅槃应可选择。");
        if (entry == null)
            return;
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_entry_id = entry.EntryRef.SkillEntryId,
            skill_id = SolarSkillId,
            target_coord = holder.GetAnchorCoord(),
        };
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"太阳涅槃真实预览应允许执行。logs={JoinLogs(preview)}"
        );
        _test.Eq(holder.GetCurrentHp(), 20, "预览不得提前治疗正式施法者。");
        _test.Eq(enemy.GetCurrentHp(), 100, "预览不得提前伤害正式敌人。");
        _test.True(holder.HasStatusEffect(EmberStatusId), "太阳涅槃预览不得提前清除余烬形态。");

        fixture.Runtime.IssueCommand(command);
        _test.Eq(holder.GetCurrentHp(), 50, "太阳涅槃应把施法者治疗到50%生命地板。");
        _test.Eq(holder.GetCurrentAp(), 0, "太阳涅槃应消耗3 AP。");
        _test.True(enemy.GetCurrentHp() < 100, "半径3格内敌人应受到4D10 fire或成功豁免后的半伤。");
        _test.Eq(allyC.GetCurrentHp(), 20, "最低生命友方应获得固定骰5+5的2D8治疗。");
        _test.Eq(allyA.GetCurrentHp(), 30, "第二名友方应获得2D8治疗。");
        _test.Eq(allyB.GetCurrentHp(), 40, "第三名友方应获得2D8治疗。");
        _test.Eq(allyD.GetCurrentHp(), 50, "第四名友方应获得2D8治疗。");
        _test.Eq(allyE.GetCurrentHp(), 45, "生命百分比第五名不得超过四目标上限。");
        _test.False(holder.HasStatusEffect(EmberStatusId), "太阳涅槃应清除余烬形态。");
        BattleStatusEffectState golden = holder.GetStatusEffect(GoldenStatusId);
        _test.True(golden != null, "太阳涅槃正式使用后应进入金焰形态。");
        _test.Eq(golden?.duration ?? -1, 240, "金焰形态持续时间应为240 TU。");

        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> goldenDice =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceOnHit(
                new BattleEquipmentAbilityBonusDamageDiceContext
                {
                    SourceUnit = holder,
                    TargetUnit = enemy,
                    BattleState = state,
                    AttackSucceeded = true,
                    IncludesWeaponDamage = false,
                }
            );
        _test.True(
            HasBonusDice(goldenDice, SolarBindingId, 1, 10, "fire"),
            "金焰形态应给徒手或法术主直接伤害段追加1D10 fire。"
        );
        BattleAvailableSkillEntry spent = FindSolarEntry(fixture, holder, state, 27);
        _test.True(spent != null && !spent.IsSelectable, "同一世界日再次查询应显示每日次数已耗尽。");
    }

    private void TestPhoenixEggUsesMonthlyFatalInterceptAndExecutesSuccessActions()
    {
        var rolls = new List<int>();
        for (int index = 0; index < 12; index++)
            rolls.Add(4);
        using PhoenixFixture fixture = PhoenixFixture.Build(rolls);
        BattleUnitState holder = fixture.BuildFullSetUnit("egg");
        PrimeUnit(holder, hp: 1, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildUnit(
            "egg_enemy",
            "enemy",
            hp: 100,
            maxHp: 100,
            new Vector2I(4, 3)
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_egg_monthly_fatal",
            new[] { holder },
            new[] { enemy },
            worldStep: 449
        );
        BattleEquipmentAbilityRuntimeService service =
            fixture.Runtime.GetEquipmentAbilityRuntimeService();

        using (var batch = new BattleEventBatch())
        {
            BattleFatalInterceptResult first = service.ResolveFatalIntercept(
                BuildFatalContext(
                    holder,
                    enemy,
                    state,
                    worldStep: 449,
                    batch,
                    BattleDeathResolutionRules.PowerWordKillExecuteContext()
                )
            );
            _test.True(first.Intercepted, "完整10件套的凤凰蛋应成功拦截律令死亡。" );
            _test.Eq(first.WinningBindingId, EggBindingId, "律令死亡只能由凤凰蛋候选获胜。" );
            _test.Eq(holder.GetCurrentHp(), 30, "凤凰蛋应按最大生命30%恢复生命。" );
            BattleStatusEffectState form = holder.GetStatusEffect(EggFormStatusId);
            _test.True(form != null, "凤凰蛋拦截成功后应生成火焰化身状态。" );
            _test.Eq(form?.duration ?? -1, 120, "凤凰蛋火焰化身应持续120 TU。" );
            _test.Eq(form?.damage_tag ?? "", new StringName("fire"), "火焰化身应声明fire标签。" );
            _test.Eq(form?.mitigation_tier ?? "", new StringName("immune"), "火焰化身应提供fire immune档位。" );
            _test.Eq(enemy.GetCurrentHp(), 88, "凤凰蛋爆发应对半径2格敌人造成固定测试骰3D10=12伤害，且不得叠加D16附伤。" );
        }

        holder.SetCurrentHp(1);
        int hpBeforeSameMonthAttempt = enemy.GetCurrentHp();
        BattleFatalInterceptResult sameMonth = service.ResolveFatalIntercept(
            BuildFatalContext(
                holder,
                enemy,
                state,
                worldStep: 449,
                deathContext: BattleDeathResolutionRules.PowerWordKillExecuteContext()
            )
        );
        _test.False(sameMonth.Intercepted, "同一世界月内凤凰蛋只能尝试一次。" );
        _test.True(
            HasAttemptOutcome(
                sameMonth,
                EggBindingId,
                BattleFatalInterceptAttemptOutcomeKind.Unavailable
            ),
            "同月第二次致死应明确显示月度次数已耗尽。"
        );
        _test.Eq(enemy.GetCurrentHp(), hpBeforeSameMonthAttempt, "月度次数耗尽后不得再次执行爆发。" );

        holder.SetCurrentHp(1);
        using (var batch = new BattleEventBatch())
        {
            BattleFatalInterceptResult nextMonth = service.ResolveFatalIntercept(
                BuildFatalContext(
                    holder,
                    enemy,
                    state,
                    worldStep: 450,
                    batch,
                    BattleDeathResolutionRules.PowerWordKillExecuteContext()
                )
            );
            _test.True(nextMonth.Intercepted, "世界步450进入下月后凤凰蛋应刷新。" );
        }
        EquipmentInstanceState egg = holder
            .GetEquipmentView()
            .GetEquippedInstance("special_trinket");
        _test.Eq(egg.ability_usage_periods.Count, 2, "凤凰蛋应把两个已用世界月写入实例账本。" );
        _test.Eq(egg.ability_usage_periods[0].PeriodIndex, 0, "世界步449应属于世界月0。" );
        _test.Eq(egg.ability_usage_periods[1].PeriodIndex, 1, "世界步450应属于世界月1。" );
    }

    private static CombatEffectDefinition BuildPowerWordKillEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "execute",
            saveDcMode: "static",
            saveDc: 10,
            saveAbility: "willpower",
            saveTag: "magic",
            thresholdMaxHpRatioPercent: 20,
            thresholdLevelAnchor: 17,
            thresholdLevelBonusPerDelta: 5,
            thresholdCapMaxHpRatioPercent: 50,
            soulFractureDurationTu: 60,
            healMultiplierPercent: 50,
            shieldGainMultiplierPercent: 50
        );

    private static bool HasAttemptOutcome(
        BattleFatalInterceptResult result,
        StringName bindingId,
        BattleFatalInterceptAttemptOutcomeKind outcome
    )
    {
        foreach (
            BattleFatalInterceptAttemptResult attempt
            in result?.Attempts ?? Array.Empty<BattleFatalInterceptAttemptResult>()
        )
        {
            if (attempt?.BindingId == bindingId && attempt.Outcome == outcome)
                return true;
        }
        return false;
    }

    private void TestBadgeBlessingPersistsAfterSourceRemovalAndConsumesDailyUse()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildFullSetUnit("badge");
        PrimeUnit(holder, hp: 70, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState nearAlly = BuildUnit(
            "badge_near_ally",
            "ally",
            hp: 40,
            maxHp: 100,
            new Vector2I(4, 3)
        );
        BattleUnitState farAlly = BuildUnit(
            "badge_far_ally",
            "ally",
            hp: 40,
            maxHp: 100,
            new Vector2I(6, 6)
        );
        BattleUnitState enemy = BuildUnit(
            "badge_enemy",
            "enemy",
            hp: 100,
            maxHp: 100,
            new Vector2I(5, 3)
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_badge_blessing",
            new[] { holder, nearAlly, farAlly },
            new[] { enemy },
            worldStep: 27
        );
        fixture.Runtime
            .GetDamageResolver()
            .ApplyTaggedDirectDamageToTargetTyped(nearAlly, 10, "fire", enemy, state);
        fixture.Runtime
            .GetDamageResolver()
            .ApplyTaggedDirectDamageToTargetTyped(farAlly, 10, "fire", enemy, state);
        _test.Eq(nearAlly.GetCurrentHp(), 35, "徽章半径2格内友方应把10点fire降为5点。" );
        _test.Eq(farAlly.GetCurrentHp(), 30, "徽章半径外友方不应获得fire半伤。" );

        BattleAvailableSkillEntry entry = FindEquipmentSkillEntry(
            fixture,
            holder,
            state,
            worldStep: 27,
            BlessingSkillId,
            BadgeBindingId,
            BlessingGrantId
        );
        _test.True(entry?.IsSelectable == true, "凤凰徽章的每日祝福应进入正式装备技能列表。" );
        if (entry == null)
            return;
        fixture.Runtime.IssueCommand(
            new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = holder.unit_id,
                skill_entry_id = entry.EntryRef.SkillEntryId,
                skill_id = BlessingSkillId,
                target_coord = holder.GetAnchorCoord(),
            }
        );
        _test.True(holder.HasStatusEffect(BlessingStatusId), "祝福应覆盖徽章佩戴者自身。" );
        _test.True(nearAlly.HasStatusEffect(BlessingStatusId), "祝福应覆盖半径2格内友方。" );
        _test.False(farAlly.HasStatusEffect(BlessingStatusId), "祝福不得覆盖半径2格外友方。" );
        _test.Eq(holder.GetCurrentAp(), 1, "凤凰祝福应消耗2 AP。" );
        BattleAvailableSkillEntry spent = FindEquipmentSkillEntry(
            fixture,
            holder,
            state,
            27,
            BlessingSkillId,
            BadgeBindingId,
            BlessingGrantId
        );
        _test.True(spent != null && !spent.IsSelectable, "同一世界日凤凰祝福应显示已耗尽。" );
        holder.SetCurrentAp(3);
        holder.ResetPerTurnCharges();
        BattleAvailableSkillEntry nextDay = FindEquipmentSkillEntry(
            fixture,
            holder,
            state,
            30,
            BlessingSkillId,
            BadgeBindingId,
            BlessingGrantId
        );
        _test.True(nextDay?.IsSelectable == true, "进入下一个世界日后凤凰祝福应刷新。" );

        holder.GetEquipmentView().PopEquippedInstance("badge");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        nearAlly.SetCurrentHp(1);
        fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .ConfigureFatalRecoveryValuesForTests(new[] { 6 });
        BattleFatalInterceptResult fatal = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .ResolveFatalIntercept(BuildFatalContext(nearAlly, enemy, state, worldStep: 27));
        _test.True(fatal.Intercepted, "徽章卸下后已经施加的祝福仍应独立完成一次致死拦截。" );
        _test.Eq(fatal.WinningBindingId, BlessingBindingId, "祝福状态应保留自己的稳定能力来源。" );
        _test.Eq(nearAlly.GetCurrentHp(), 6, "祝福致死拦截应按固定测试骰恢复1D10生命。" );
    }

    private void TestPhoenixBonusDamageSourcesUseExclusivePriorityFallback()
    {
        using PhoenixFixture fixture = PhoenixFixture.Build(new[] { 2, 7, 1, 1, 1 });
        BattleUnitState holder = fixture.BuildFullSetUnit("append_priority");
        PrimeUnit(holder, hp: 80, maxHp: 100, ap: 3, new Vector2I(3, 3));
        BattleUnitState enemy = BuildUnit(
            "append_priority_enemy",
            "enemy",
            hp: 100,
            maxHp: 100,
            new Vector2I(4, 3)
        );
        BattleState state = fixture.SetupBattle(
            "phoenix_append_priority",
            new[] { holder },
            new[] { enemy },
            worldStep: 20
        );
        foreach (StringName statusId in new[]
        {
            EmberStatusId,
            BlessingStatusId,
            EggFormStatusId,
            GoldenStatusId,
        })
        {
            holder.SetStatusEffect(new BattleStatusEffectState { status_id = statusId, duration = 120 });
        }

        AssertPhoenixAppendWinner(fixture, holder, enemy, state, SolarBindingId, 1, 10, 400, "金焰");
        holder.EraseStatusEffect(GoldenStatusId);
        AssertPhoenixAppendWinner(fixture, holder, enemy, state, EggFormBindingId, 1, 10, 300, "凤凰蛋形态");
        holder.EraseStatusEffect(EggFormStatusId);
        AssertPhoenixAppendWinner(fixture, holder, enemy, state, BlessingBindingId, 1, 6, 200, "凤凰祝福");
        holder.EraseStatusEffect(BlessingStatusId);
        AssertPhoenixAppendWinner(fixture, holder, enemy, state, EmberBindingId, 1, 4, 100, "余烬形态");

        foreach (StringName statusId in new[]
        {
            BlessingStatusId,
            EggFormStatusId,
            GoldenStatusId,
        })
        {
            holder.SetStatusEffect(new BattleStatusEffectState { status_id = statusId, duration = 120 });
        }
        enemy.SetCurrentHp(100);
        enemy.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(20));
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        ForceUnitActing(state, holder);
        BattleCommand attack = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            holder,
            enemy
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(attack);
        _test.True(
            preview?.allowed == true,
            $"凤凰附伤真实基础攻击预览应通过。logs={JoinLogs(preview)}"
        );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(attack);
        _test.True(batch != null, "凤凰附伤真实基础攻击应返回正式事件批次。");
        _test.Eq(
            enemy.GetCurrentHp(),
            91,
            "固定基础骰2与金焰骰7时，四个候选并存的真实攻击只能结算优先级400的1D10赢家。"
        );
    }

    private void AssertRebirthRingPreserved(
        EquipmentInstanceState expected,
        EquipmentInstanceState actual
    )
    {
        _test.True(actual != null, "重生之戒触发后原装备实例必须仍装备在ring_1槽位。");
        if (expected == null || actual == null)
            return;
        _test.Eq(actual.instance_id, expected.instance_id, "重生之戒触发不得替换装备实例ID。");
        _test.Eq(actual.item_id, expected.item_id, "重生之戒触发不得替换物品ID。");
        _test.Eq(actual.current_durability, expected.current_durability, "重生之戒触发不得损耗或重置耐久。");
        _test.Eq(actual.trait_instances.Count, expected.trait_instances.Count, "重生之戒触发不得清空或新增实例traits。");
        if (actual.trait_instances.Count != expected.trait_instances.Count)
            return;
        for (int index = 0; index < expected.trait_instances.Count; index++)
        {
            TraitInstanceState expectedTrait = expected.trait_instances[index];
            TraitInstanceState actualTrait = actual.trait_instances[index];
            _test.Eq(actualTrait?.trait_instance_id ?? new StringName(""), expectedTrait?.trait_instance_id ?? new StringName(""), $"重生之戒trait[{index}]实例ID应保持不变。");
            _test.Eq(actualTrait?.trait_id ?? new StringName(""), expectedTrait?.trait_id ?? new StringName(""), $"重生之戒trait[{index}]定义ID应保持不变。");
            _test.Eq(actualTrait?.source_type ?? new StringName(""), expectedTrait?.source_type ?? new StringName(""), $"重生之戒trait[{index}]来源类型应保持不变。");
            _test.Eq(actualTrait?.source_id ?? new StringName(""), expectedTrait?.source_id ?? new StringName(""), $"重生之戒trait[{index}]来源ID应保持不变。");
            _test.Eq(actualTrait?.rank ?? 0, expectedTrait?.rank ?? 0, $"重生之戒trait[{index}]rank应保持不变。");
            _test.Eq(actualTrait?.stacks ?? 0, expectedTrait?.stacks ?? 0, $"重生之戒trait[{index}]stacks应保持不变。");
            _test.Eq(actualTrait?.GetIntRoll("seed", -1) ?? -1, expectedTrait?.GetIntRoll("seed", -1) ?? -1, $"重生之戒trait[{index}]roll_values应保持不变。");
        }
    }

    private void AssertPhoenixAppendWinner(
        PhoenixFixture fixture,
        BattleUnitState holder,
        BattleUnitState enemy,
        BattleState state,
        StringName expectedBindingId,
        int diceCount,
        int diceSides,
        int priority,
        string label
    )
    {
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> results = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .CollectBonusDamageDiceOnHit(
                new BattleEquipmentAbilityBonusDamageDiceContext
                {
                    SourceUnit = holder,
                    TargetUnit = enemy,
                    BattleState = state,
                    AttackSucceeded = true,
                    IncludesWeaponDamage = false,
                }
            );
        int groupCount = 0;
        BattleEquipmentAbilityBonusDamageDiceResult winner = null;
        foreach (BattleEquipmentAbilityBonusDamageDiceResult result in results)
        {
            if (result?.ReplacementGroupId != PhoenixAppendGroupId)
                continue;
            groupCount += 1;
            winner = result;
        }
        _test.Eq(groupCount, 1, $"{label}存在时凤凰附伤组必须只保留一个结果。" );
        _test.Eq(winner?.BindingId ?? new StringName(""), expectedBindingId, $"{label}应按优先级成为互斥组胜者。" );
        _test.Eq(winner?.DiceCount ?? -1, diceCount, $"{label}附伤骰数不符。" );
        _test.Eq(winner?.DiceSides ?? -1, diceSides, $"{label}附伤骰面不符。" );
        _test.Eq(winner?.ReplacementPriority ?? -1, priority, $"{label}附伤优先级不符。" );
    }

    private static BattleFatalInterceptContext BuildFatalContext(
        BattleUnitState holder,
        BattleUnitState source,
        BattleState state,
        int worldStep,
        BattleEventBatch batch = null,
        DeathResolutionContext? deathContext = null
    ) =>
        new()
        {
            SourceUnit = source,
            TargetUnit = holder,
            BattleState = state,
            DeathContext = deathContext ?? BattleDeathResolutionRules.NormalFatalContext(),
            HpBefore = 1,
            HpDamage = 2,
            ProjectedHp = -1,
            WorldStep = worldStep,
            Batch = batch,
            SaveContext = BattleSaveContext.Empty,
        };

    private static BattleAvailableSkillEntry FindSolarEntry(
        PhoenixFixture fixture,
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
                entry?.EntryRef.SkillId == SolarSkillId
                && entry.EquipmentBindingId == SolarBindingId
                && entry.EquipmentGrantedActionId == SolarGrantId
            )
            {
                return entry;
            }
        }
        return null;
    }

    private static BattleAvailableSkillEntry FindEquipmentSkillEntry(
        PhoenixFixture fixture,
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

    private static void AssertGearSetSource(BattleUnitState unit, StringName bindingId)
    {
        BattleEquipmentAbilitySourceReadView source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"missing gear-set ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold)
            throw new InvalidOperationException($"{bindingId} must retain gear-set threshold provenance.");
        if (!source.SourceEquipmentInstanceId.ToString().Contains("armor_phoenix_rebirth_head", StringComparison.Ordinal))
            throw new InvalidOperationException($"{bindingId} must use the configured head-piece usage anchor.");
    }

    private static BattleEquipmentAbilitySourceReadView FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source in
            unit?.GetEquipmentAbilitySourcesReadViewTyped()
                ?? new BattleEquipmentAbilitySourceListReadView(null)
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static bool HasBonusDice(
        IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> results,
        StringName bindingId,
        int diceCount,
        int diceSides,
        StringName damageType
    )
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult result in results ?? Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>())
        {
            if (
                result?.BindingId == bindingId
                && result.DiceCount == diceCount
                && result.DiceSides == diceSides
                && result.DamageType == damageType
            )
            {
                return true;
            }
        }
        return false;
    }

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

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        int hp,
        int maxHp,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
        unit.SetCombatResources(hp, mp: 0, stamina: 0, aura: 0, ap: 2, movePoints: 2);
        unit.SetAnchorCoord(coord);
        unit.SetUnarmedWeaponProjectionTyped();
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private sealed class PhoenixFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private PhoenixFixture(
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
            Traits = snapshot.Traits;
            GearSets = snapshot.GearSets;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> Traits { get; }
        internal IReadOnlyDictionary<StringName, GearSetDefinition> GearSets { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static PhoenixFixture Build(IEnumerable<int> damageRolls)
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
                using GArray rollPayload = new();
                foreach (int roll in damageRolls ?? Array.Empty<int>())
                    rollPayload.Add(roll);
                BattleTestFixture.ConfigureDamageResolverForTests(
                    runtime,
                    new FixedRollDamageResolver(rollPayload)
                );
                return new PhoenixFixture(characterManagement, partyState, runtime, snapshot);
            }
            catch
            {
                BattleTestFixture.DisposeRuntime(runtime);
                characterManagement?.Dispose();
                throw;
            }
        }

        internal BattleUnitState BuildFullSetUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            foreach ((StringName itemId, StringName slotId) in Members)
            {
                member.equipment_state.SetEquippedEntry(
                    slotId,
                    itemId,
                    new[] { slotId },
                    EquipmentInstanceState.CreateInstance(
                        itemId,
                        $"eq_{label}_{itemId}"
                    )
                );
            }
            IReadOnlyList<BattleUnitState> units = Runtime._unit_factory.BuildAllyUnits(
                _partyState,
                null
            );
            if (units.Count != 1)
                throw new InvalidOperationException("Phoenix fixture must build exactly one ally.");
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
            BattleState state = BattleTestFixture.BuildFlatState(battleId, new Vector2I(7, 7));
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
            return party;
        }
    }
}
