using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;

// 龙鳞铠甲套装真实战斗回归（提案 §14.1-14.4 的龙鳞特化部分）。
// 内容校验：四件 slot/价格/hp_max/traits/modifier、0/1/2/3/4 件阈值累计、锚点唯一性 tag。
// 单件：头盔 fire=half 与对 dragon ±攻击加值；胸甲三元素 half；护手近战命中 dragon +1D4；
// 胫甲 fear+3 与龙威免疫。2/4 件：fear bonus 0/+3/+3/+6、fire half 不叠加、卸下无 stale tier、
// +2 只对 dragon、四件套 +1D4 覆盖 weapon/非武器法术/save 直接伤害、三段合计 6D4、miss 不触发、
// execute/preview/AI 一致。龙息：五种吐息 half、非吐息/非 dragon 不触发、half/double 抵消、
// immune 优先、多份 half 不 quarter、power=12 fire breath 满套与头盔单件都是 6/3、
// 带固定 DR 的 synthetic case 锁定 tier → fixed DR → save 整数顺序并保留 source 报告。
public partial class run_dragon_scale_set_regression : LifecycleTestSceneTree
{
    private static readonly StringName SetId = "dragon_scale_set";
    private static readonly StringName HeadItemId = "armor_dragon_scale_head";
    private static readonly StringName BodyItemId = "armor_dragon_scale_body";
    private static readonly StringName HandsItemId = "armor_dragon_scale_hands";
    private static readonly StringName FeetItemId = "armor_dragon_scale_feet";
    private static readonly StringName DeterrenceTraitId = "gear_set.dragon_scale.2.dragons_deterrence";
    private static readonly StringName OathTraitId = "gear_set.dragon_scale.4.dragonslayer_oath";
    private static readonly StringName HeadFocusBindingId = "binding.armor.dragon_scale.head.dragon_slayer_focus";
    private static readonly StringName HandsRendBindingId = "binding.armor.dragon_scale.hands.dragon_rend";
    private static readonly StringName OathBindingId = "binding.gear_set.dragon_scale.4.dragonslayer_oath";
    private static readonly StringName BoilSkillId = "equipment_dragon_scale_dragon_blood_boil";
    private static readonly StringName FrightfulPresenceSkillId = "dragon_frightful_presence";
    private static readonly StringName DragonBreathSaveTag = "dragon_breath";
    private static readonly StringName FrightenedTag = "frightened";
    private static readonly StringName DragonFrightfulPresenceTag = "dragon_frightful_presence";

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
            TestContentValidation();
            TestPieceCountThresholds();
            TestHeadFireResistanceAndDragonAttackBonus();
            TestBodyElementResistancesAndHpMax();
            TestHandsMeleeDragonBonusDice();
            TestFeetFearSaveBonusAndDragonPresenceImmunity();
            TestFearBonusStackingMatrix();
            TestFireHalfNonStackingAndUnequipClearsStaleTier();
            TestOathAttackBonusOnlyVsDragon();
            TestOathPerEffectDiceAcrossDamageShapes();
            TestOathDiceExecutePreviewParity();
            TestDragonBreathConditionalTier();
            TestBreathTierAggregationRules();
            TestBreathTierFixedDrSaveOrdering();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Dragon Scale set regression"));
    }

    private void TestContentValidation()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        _test.True(
            fixture.GearSets.TryGetValue(SetId, out GearSetDefinition set),
            "真实内容快照应注册龙鳞铠甲套装。"
        );
        if (set == null)
            return;
        _test.Eq(set.MemberItemIds.Count, 4, "龙鳞铠甲必须包含四件成员装备。");
        _test.Eq(set.UsageAnchorItemId, HeadItemId, "龙鳞铠甲应以头盔作为周期用量锚点。");
        _test.Eq(set.Thresholds.Count, 2, "龙鳞铠甲应声明 2/4 件两个阈值。");
        _test.Eq(set.Thresholds[0].RequiredPieceCount, 2, "首个阈值应为 2 件。");
        _test.Eq(set.Thresholds[1].RequiredPieceCount, 4, "第二个阈值应为 4 件。");
        _test.Eq(
            set.Thresholds[0].GrantedTraitIds[0],
            DeterrenceTraitId,
            "2 件阈值应授予龙之威慑 trait。"
        );
        _test.Eq(
            set.Thresholds[1].GrantedTraitIds[0],
            OathTraitId,
            "4 件阈值应授予屠龙者之誓 trait。"
        );

        AssertItem(fixture, HeadItemId, "head", 20000, 10000, 1, ("armor_ac_bonus", 2));
        AssertItem(fixture, BodyItemId, "body", 36000, 18000, 1, ("armor_ac_bonus", 7), ("hp_max", 10));
        AssertItem(fixture, HandsItemId, "hands", 16000, 8000, -1, ("armor_ac_bonus", 1), ("attack_bonus", 2));
        AssertItem(fixture, FeetItemId, "feet", 16000, 8000, -1, ("armor_ac_bonus", 1));

        _test.True(
            ContainsStringName(
                fixture.Items[HeadItemId].TraitIds,
                "armor.dragon_scale.head.fire_resistance"
            ),
            "头盔应携带火焰抗性 trait。"
        );
        _test.True(
            ContainsStringName(
                fixture.Items[HeadItemId].TraitIds,
                "armor.dragon_scale.head.dragon_slayer_focus"
            ),
            "头盔应携带屠龙专注 trait。"
        );
        _test.True(
            ContainsStringName(
                fixture.Items[BodyItemId].TraitIds,
                "armor.dragon_scale.body.element_resistance"
            ),
            "胸甲应携带元素抗性 trait。"
        );
        _test.True(
            ContainsStringName(
                fixture.Items[HandsItemId].TraitIds,
                "armor.dragon_scale.hands.dragon_rend"
            ),
            "护手应携带逆鳞撕裂 trait。"
        );
        _test.True(
            ContainsStringName(
                fixture.Items[FeetItemId].TraitIds,
                "armor.dragon_scale.feet.fear_ward"
            ),
            "胫甲应携带恐惧守护 trait。"
        );

        TraitDefinition deterrence = fixture.Traits[DeterrenceTraitId];
        _test.Eq(
            deterrence.DamageResistanceEntries[0].DamageTag,
            new StringName("fire"),
            "2 件套应授予 fire 抗性。"
        );
        _test.Eq(
            deterrence.DamageResistanceEntries[0].MitigationTier,
            new StringName("half"),
            "2 件套 fire 抗性应为 half。"
        );
        _test.Eq(
            deterrence.SaveTagBonusEntries.Count,
            2,
            "2 件套应同时覆盖 frightened 与 dragon_frightful_presence 两条 save tag 加值。"
        );
        _test.True(
            ContainsStringName(deterrence.SaveImmunityTags, DragonFrightfulPresenceTag),
            "2 件套应免疫 dragon_frightful_presence。"
        );
        _test.False(
            ContainsStringName(deterrence.SaveImmunityTags, FrightenedTag),
            "龙威免疫不得粗暴覆盖普通 frightened。"
        );

        _test.True(
            fixture.Bindings.ContainsKey(HeadFocusBindingId),
            "头盔屠龙专注 binding 应注册。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(HandsRendBindingId),
            "护手逆鳞撕裂 binding 应注册。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(OathBindingId),
            "屠龙者之誓 binding 应注册。"
        );
        EquipmentAbilityBindingDefinition oath = fixture.Bindings[OathBindingId];
        _test.Eq(oath.GrantedActions.Count, 1, "屠龙者之誓应授予一个主动动作。");
        _test.Eq(
            oath.GrantedActions[0].SkillId,
            BoilSkillId,
            "屠龙者之誓授予动作应指向独立的龙血沸腾技能。"
        );
        _test.Eq(
            oath.GrantedActions[0].UsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "龙血沸腾应为每个世界日一次。"
        );
        _test.Eq(oath.GrantedActions[0].MaxUsesPerPeriod, 1, "龙血沸腾每日最多一次。");

        SkillDefinition boil = fixture.Skills[BoilSkillId];
        _test.Eq(boil.CombatProfile.ApCost, 1, "龙血沸腾应消耗 1 AP。");
        _test.Eq(boil.CombatProfile.MpCost, 0, "龙血沸腾不应消耗法力。");
        _test.Eq(boil.CombatProfile.StaminaCost, 0, "龙血沸腾不应消耗体力。");
        _test.Eq(
            boil.CombatProfile.TargetTeamFilter,
            new StringName("self"),
            "龙血沸腾应为自身目标。"
        );
        _test.False(
            boil.SkillId == new StringName("warrior_dragon_blood_boil"),
            "龙鳞龙血沸腾不得复用战士职业技能 id。"
        );

        SkillDefinition presence = fixture.Skills[FrightfulPresenceSkillId];
        CombatEffectDefinition presenceEffect = presence.CombatProfile.EffectDefinitions[0];
        _test.Eq(
            presenceEffect.SaveTag,
            DragonFrightfulPresenceTag,
            "龙威技能应使用 canonical dragon_frightful_presence save tag。"
        );
        _test.Eq(
            presenceEffect.SaveFailureStatusId,
            FrightenedTag,
            "龙威豁免失败应施加 frightened。"
        );
        _test.Eq(presenceEffect.SaveDc, 15, "龙威豁免 DC 应为 15。");
    }

    private void AssertItem(
        DragonScaleFixture fixture,
        StringName itemId,
        string slotId,
        int buyPrice,
        int sellPrice,
        int maxDexBonus,
        params (StringName AttributeId, int Value)[] modifiers
    )
    {
        _test.True(
            fixture.Items.TryGetValue(itemId, out ItemDefinition item),
            $"真实内容快照缺少龙鳞成员 {itemId}。"
        );
        if (item == null)
            return;
        _test.Eq(item.EquipmentSlotIds.Count, 1, $"{itemId} 应只有一个允许槽位。");
        _test.Eq(item.EquipmentSlotIds[0], slotId, $"{itemId} 应落在 {slotId} 槽位。");
        _test.Eq(item.BuyPrice, buyPrice, $"{itemId} 买入价格不符。");
        _test.Eq(item.SellPrice, sellPrice, $"{itemId} 卖出价格不符。");
        _test.Eq(item.MaxDexBonus, maxDexBonus, $"{itemId} max_dex_bonus 不符。");
        _test.True(
            ContainsStringName(item.Tags, WorldUniqueEquipmentContentRules.WorldUniqueEquipmentTag),
            $"{itemId} 应声明 world_unique_equipment 唯一获取约束。"
        );
        _test.Eq(item.AttributeModifiers.Count, modifiers.Length, $"{itemId} 静态 modifier 数不符。");
        foreach ((StringName attributeId, int value) in modifiers)
        {
            bool found = false;
            foreach (AttributeModifierDefinition modifier in item.AttributeModifiers)
            {
                if (modifier.AttributeId == attributeId && modifier.Value == value)
                {
                    found = true;
                    break;
                }
            }
            _test.True(found, $"{itemId} 缺少 modifier {attributeId}={value}。");
        }
    }

    private void TestPieceCountThresholds()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        for (int count = 0; count <= 4; count++)
        {
            var equipment = new EquipmentState();
            for (int index = 0; index < count; index++)
            {
                (StringName itemId, StringName slotId) = Members[index];
                equipment.SetEquippedEntry(
                    slotId,
                    itemId,
                    new StringName[] { slotId },
                    EquipmentInstanceState.CreateInstance(itemId, $"eq_count_{count}_{itemId}")
                );
            }
            GearSetEvaluationSnapshot snapshot = GearSetEvaluationService.Evaluate(
                equipment,
                fixture.Items,
                fixture.GearSets
            );
            if (count == 0)
            {
                _test.Eq(snapshot.ActiveSets.Count, 0, "0 件时不应出现激活套装。");
                continue;
            }
            _test.Eq(snapshot.ActiveSets.Count, 1, $"{count} 件时套装应保持可见。");
            GearSetActivationSummary summary = snapshot.ActiveSets[0];
            _test.Eq(summary.EquippedPieceCount, count, $"{count} 件时计数不符。");
            _test.Eq(
                summary.Thresholds[0].IsActive,
                count >= 2,
                $"{count} 件时 2 件阈值激活状态不符。"
            );
            _test.Eq(
                summary.Thresholds[1].IsActive,
                count >= 4,
                $"{count} 件时 4 件阈值激活状态不符。"
            );
            _test.Eq(
                snapshot.DerivedTraitInstances.Count,
                count >= 4 ? 2 : (count >= 2 ? 1 : 0),
                $"{count} 件时派生 threshold trait 数不符（阈值应累计）。"
            );
        }
    }

    private void TestHeadFireResistanceAndDragonAttackBonus()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("head_only", MemberSlice(0, 1));
        _test.Eq(
            holder.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "头盔单件应投影 fire=half。"
        );
        AssertAttackBonusVsCreature(fixture, holder, dragonTarget: true, 2, "头盔对 dragon 攻击应 +2。");
        AssertAttackBonusVsCreature(fixture, holder, dragonTarget: false, 0, "头盔对非 dragon 不应加值。");
    }

    private void TestBodyElementResistancesAndHpMax()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("body_only", MemberSlice(1, 2));
        foreach (StringName tag in new StringName[] { "fire", "freeze", "lightning" })
        {
            _test.Eq(
                holder.GetDamageResistanceTyped(tag),
                new StringName("half"),
                $"胸甲单件应投影 {tag}=half。"
            );
        }
        _test.Eq(
            MitigationOrEmpty(holder, "poison"),
            new StringName(""),
            "胸甲不应覆盖 poison。"
        );

        BattleUnitState bare = fixture.BuildSetUnit("body_bare", Array.Empty<int>());
        int bareHpMax = bare.attribute_snapshot.GetValue(AttributeService.HP_MAX);
        int bodyHpMax = holder.attribute_snapshot.GetValue(AttributeService.HP_MAX);
        _test.Eq(bodyHpMax, bareHpMax + 10, "胸甲应提供 hp_max +10。");
    }

    private void TestHandsMeleeDragonBonusDice()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("hands_only", MemberSlice(2, 3));
        holder.SetUnarmedWeaponProjectionTyped(
            "physical_slash",
            new WeaponDice { dice_count = 1, dice_sides = 6, flat_bonus = 0 },
            1
        );
        BattleUnitState dragon = BuildEnemyUnit("hands_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleUnitState plain = BuildEnemyUnit("hands_plain", new Vector2I(4, 4), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_hands",
            new[] { holder },
            new[] { dragon, plain },
            worldStep: 0
        );

        // 近战武器命中 dragon：三段逐段触发，每段 1D4（固定骰 max=4）。
        int total = 0;
        for (int segment = 0; segment < 3; segment++)
        {
            AttackEffectResolutionResult result = fixture.Resolver.ResolveEffects(
                holder,
                dragon,
                new[] { BuildWeaponEffect() },
                BuildMainContext(state, holder, dragon, attackSuccess: true, hasAttackCheck: true)
            );
            _test.Eq(
                result.DamageEvents[0].BonusDamageDice.Total,
                4,
                $"护手第 {segment + 1} 段近战命中 dragon 应追加 1D4。"
            );
            _test.Eq(result.Damage, 14, $"护手第 {segment + 1} 段总伤应为 4+6+4=14。");
            total += result.DamageEvents[0].BonusDamageDice.Total;
        }
        _test.Eq(total, 12, "护手三段近战命中应累计 3D4=12。");

        // 近战武器命中非 dragon：不触发。
        AttackEffectResolutionResult plainResult = fixture.Resolver.ResolveEffects(
            holder,
            plain,
            new[] { BuildWeaponEffect() },
            BuildMainContext(state, holder, plain, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            plainResult.DamageEvents[0].BonusDamageDice.Total,
            0,
            "护手对非 dragon 目标不应追加骰。"
        );

        // 非武器法术命中 dragon：护手有 require_weapon_damage，不触发。
        AttackEffectResolutionResult spellResult = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
            new[] { BuildSpellEffect(power: 8, damageTag: "fire") },
            BuildMainContext(state, holder, dragon, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            spellResult.DamageEvents[0].BonusDamageDice.Total,
            0,
            "护手不应对非武器法术追加骰。"
        );

        // 远程武器命中 dragon：护手限定近战，不触发。
        ApplyRangedWeaponProjection(holder);
        AttackEffectResolutionResult rangedResult = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
            new[] { BuildWeaponEffect() },
            BuildMainContext(state, holder, dragon, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            rangedResult.DamageEvents[0].BonusDamageDice.Total,
            0,
            "护手不应对远程武器命中追加骰。"
        );

        // 逐段查询 provenance：护手 binding 应逐段出现在 query 结果中。
        holder.SetUnarmedWeaponProjectionTyped(
            "physical_slash",
            new WeaponDice { dice_count = 1, dice_sides = 6, flat_bonus = 0 },
            1
        );
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> query =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceForEffect(
                new BattleEquipmentAbilityDirectDamageContext
                {
                    SourceUnit = holder,
                    TargetUnit = dragon,
                    BattleState = state,
                    SkillId = "test_dragon_scale_hands",
                    PrimaryDamageTag = "physical_slash",
                    DamageOriginKind = BattleDamageOriginKind.MainDirectEffect,
                    IsMainDirectEffect = true,
                    IncludesWeaponDamage = true,
                    HasAttackCheck = true,
                    AttackSucceeded = true,
                }
            );
        _test.Eq(query.Count, 1, "护手 query 应只产出一条加骰结果。");
        _test.Eq(query[0].BindingId, HandsRendBindingId, "护手加骰应保留 binding provenance。");
        _test.Eq(query[0].DiceCount, 1, "护手加骰应为 1D4。");
        _test.Eq(query[0].DiceSides, 4, "护手加骰应为 1D4。");
    }

    private void TestFeetFearSaveBonusAndDragonPresenceImmunity()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        CombatEffectDefinition presenceEffect = fixture
            .Skills[FrightfulPresenceSkillId]
            .CombatProfile
            .EffectDefinitions[0];
        CombatEffectDefinition fearEffect = fixture
            .Skills["weapon_axe_executioner_execution_fear"]
            .CombatProfile
            .EffectDefinitions[0];

        BattleUnitState holder = fixture.BuildSetUnit("feet_only", MemberSlice(3, 4));
        _test.Eq(
            holder.GetSaveBonusByTagTyped(FrightenedTag),
            3,
            "胫甲应对 frightened save tag 提供 +3。"
        );
        _test.Eq(
            holder.GetSaveBonusByTagTyped(DragonFrightfulPresenceTag),
            3,
            "胫甲应对 dragon_frightful_presence save tag 提供 +3。"
        );

        BattleSaveResult presenceSave = BattleSaveResolver.ResolveSaveResult(
            null,
            holder,
            presenceEffect,
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.True(presenceSave.Immune, "胫甲应免疫真实龙威技能的豁免。");

        BattleSaveResult fearSave = BattleSaveResolver.ResolveSaveResult(
            null,
            holder,
            fearEffect,
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.False(fearSave.Immune, "胫甲不得免疫普通 frightened 豁免。");
        _test.Eq(fearSave.Bonus, 3, "胫甲应对普通恐惧豁免保留 +3 加值。");

        BattleUnitState bare = fixture.BuildSetUnit("feet_bare", Array.Empty<int>());
        BattleSaveResult bareSave = BattleSaveResolver.ResolveSaveResult(
            null,
            bare,
            presenceEffect,
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.False(bareSave.Immune, "无胫甲单位不应免疫龙威。");
        _test.Eq(bareSave.Bonus, 0, "无胫甲单位不应有恐惧 save tag 加值。");
    }

    private void TestFearBonusStackingMatrix()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        AssertFearBonus(fixture, "fear_none", Array.Empty<int>(), 0, "0 件");
        AssertFearBonus(fixture, "fear_feet", MemberSlice(3, 4), 3, "仅胫甲");
        AssertFearBonus(fixture, "fear_two_piece", MemberSlice(0, 2), 3, "仅 2 件阈值");
        AssertFearBonus(
            fixture,
            "fear_feet_plus_two",
            new[] { 0, 3 },
            6,
            "胫甲加 2 件阈值应按 add 叠加为 +6"
        );
    }

    private void AssertFearBonus(
        DragonScaleFixture fixture,
        string label,
        IReadOnlyList<int> memberIndexes,
        int expected,
        string scenario
    )
    {
        BattleUnitState holder = fixture.BuildSetUnit(label, memberIndexes);
        _test.Eq(
            holder.GetSaveBonusByTagTyped(FrightenedTag),
            expected,
            $"{scenario}：frightened tag bonus 应为 +{expected}。"
        );
        _test.Eq(
            holder.GetSaveBonusByTagTyped(DragonFrightfulPresenceTag),
            expected,
            $"{scenario}：dragon_frightful_presence tag bonus 应为 +{expected}。"
        );
    }

    private void TestFireHalfNonStackingAndUnequipClearsStaleTier()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("fire_stacking", MemberSlice(0, 4));
        _test.Eq(
            holder.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "头盔+胸甲+2件套的多份 fire=half 在 unit map 中仍只保留一档 half。"
        );
        AssertFireDamageFromDragonBreath(fixture, holder, "fire", 6, "满套 fire 直接伤害应减半一次而非多次。");

        holder.GetEquipmentView().PopEquippedInstance("head");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.Eq(
            holder.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "卸下头盔后胸甲+2件套仍应保留 fire=half。"
        );

        holder.GetEquipmentView().PopEquippedInstance("body");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.Eq(
            holder.GetDamageResistanceTyped("fire"),
            new StringName("half"),
            "降为 2 件（护手+胫甲）后 2 件套阈值仍应提供 fire=half。"
        );

        holder.GetEquipmentView().PopEquippedInstance("hands");
        fixture.Runtime._unit_factory.RefreshEquipmentProjection(holder);
        _test.Eq(
            MitigationOrEmpty(holder, "fire"),
            new StringName(""),
            "降为 1 件后不得残留 stale fire tier。"
        );
    }

    private void TestOathAttackBonusOnlyVsDragon()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("oath_attack", MemberSlice(0, 4));
        _test.True(holder.HasEffectiveTrait(OathTraitId), "四件时应激活屠龙者之誓 trait。");
        AssertGearSetSource(holder, OathBindingId);
        AssertAttackBonusVsCreature(
            fixture,
            holder,
            dragonTarget: true,
            4,
            "头盔 +2 与 4 件套 +2 对 dragon 攻击应相加为 +4。"
        );
        AssertAttackBonusVsCreature(fixture, holder, dragonTarget: false, 0, "4 件套对非 dragon 不应加值。");
    }

    private void TestOathPerEffectDiceAcrossDamageShapes()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("oath_dice", MemberSlice(0, 4));
        holder.SetUnarmedWeaponProjectionTyped(
            "physical_slash",
            new WeaponDice { dice_count = 1, dice_sides = 6, flat_bonus = 0 },
            1
        );
        BattleUnitState dragon = BuildEnemyUnit("oath_dice_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleUnitState plain = BuildEnemyUnit("oath_dice_plain", new Vector2I(4, 4), withDragonTag: false);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_oath_dice",
            new[] { holder },
            new[] { dragon, plain },
            worldStep: 0
        );

        // 近战武器命中 dragon：护手 1D4 与四件套 1D4 同段合并为 2D4；三段合计 6D4。
        int bonusTotal = 0;
        for (int segment = 0; segment < 3; segment++)
        {
            AttackEffectResolutionResult result = fixture.Resolver.ResolveEffects(
                holder,
                dragon,
                new[] { BuildWeaponEffect() },
                BuildMainContext(state, holder, dragon, attackSuccess: true, hasAttackCheck: true)
            );
            _test.Eq(
                result.DamageEvents[0].BonusDamageDice.Count,
                2,
                $"满套第 {segment + 1} 段近战命中应合并护手与四件套两颗 D4。"
            );
            _test.Eq(
                result.DamageEvents[0].BonusDamageDice.Total,
                8,
                $"满套第 {segment + 1} 段近战命中应为 2D4=8。"
            );
            bonusTotal += result.DamageEvents[0].BonusDamageDice.Total;
        }
        _test.Eq(bonusTotal, 24, "满套三段近战命中应合计 6D4=24。");

        // 非武器 attack spell 命中 dragon：仅四件套 1D4，继承主 fire 标签。
        dragon.SetCurrentHp(200);
        AttackEffectResolutionResult spellResult = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
            new[] { BuildSpellEffect(power: 8, damageTag: "fire") },
            BuildMainContext(state, holder, dragon, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(spellResult.Damage, 12, "非武器法术命中 dragon 应只追加四件套 1D4。");
        _test.Eq(
            spellResult.DamageEvents[0].DamageTag,
            new StringName("fire"),
            "四件套加骰应继承主段 fire 标签。"
        );

        // save-only 主直接伤害：无攻击检定不误判 miss，仍触发四件套 1D4。
        AttackEffectResolutionResult saveOnly = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
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
            BuildMainContext(state, holder, dragon, attackSuccess: false, hasAttackCheck: false)
                .WithSaveRollOverrides(new[] { 1 })
        );
        _test.Eq(saveOnly.Damage, 12, "豁免失败的主直接伤害应追加四件套 1D4（8+4=12）。");

        // miss 段不触发。
        AttackEffectResolutionResult miss = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
            new[] { BuildSpellEffect(power: 8, damageTag: "fire") },
            BuildMainContext(state, holder, dragon, attackSuccess: false, hasAttackCheck: true)
        );
        _test.Eq(miss.Damage, 8, "miss 段不得触发四件套加骰。");
        _test.Eq(miss.DamageEvents[0].BonusDamageDice.Total, 0, "miss 段不应有加骰。");

        // 非 dragon 目标不触发四件套加骰。
        AttackEffectResolutionResult plainResult = fixture.Resolver.ResolveEffects(
            holder,
            plain,
            new[] { BuildSpellEffect(power: 8, damageTag: "fire") },
            BuildMainContext(state, holder, plain, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            plainResult.DamageEvents[0].BonusDamageDice.Total,
            0,
            "四件套对非 dragon 目标不应追加骰。"
        );

        // 远程武器命中 dragon：仅四件套 1D4（护手限定近战）。
        ApplyRangedWeaponProjection(holder);
        AttackEffectResolutionResult ranged = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
            new[] { BuildWeaponEffect() },
            BuildMainContext(state, holder, dragon, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            ranged.DamageEvents[0].BonusDamageDice.Total,
            4,
            "远程武器命中 dragon 应只触发四件套 1D4。"
        );
        holder.SetUnarmedWeaponProjectionTyped(
            "physical_slash",
            new WeaponDice { dice_count = 1, dice_sides = 6, flat_bonus = 0 },
            1
        );

        // query 级 provenance：护手与四件套来源分别可辨。
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> query =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceForEffect(
                new BattleEquipmentAbilityDirectDamageContext
                {
                    SourceUnit = holder,
                    TargetUnit = dragon,
                    BattleState = state,
                    SkillId = "test_dragon_scale_oath",
                    PrimaryDamageTag = "physical_slash",
                    DamageOriginKind = BattleDamageOriginKind.MainDirectEffect,
                    IsMainDirectEffect = true,
                    IncludesWeaponDamage = true,
                    HasAttackCheck = true,
                    AttackSucceeded = true,
                }
            );
        _test.Eq(query.Count, 2, "近战命中 dragon 时应有护手与四件套两条加骰。");
        _test.True(
            HasBonusDiceSource(query, HandsRendBindingId),
            "query 应保留护手 binding provenance。"
        );
        _test.True(
            HasBonusDiceSource(query, OathBindingId),
            "query 应保留四件套 binding provenance。"
        );
    }

    private void TestOathDiceExecutePreviewParity()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("oath_parity", MemberSlice(0, 4));
        BattleUnitState dragon = BuildEnemyUnit("oath_parity_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_oath_parity",
            new[] { holder },
            new[] { dragon },
            worldStep: 0
        );
        CombatEffectDefinition effect = BuildSpellEffect(power: 8, damageTag: "fire");
        // 固定骰 resolver 的 average 模式：1D4 期望 RoundToInt(2.5)=3，合计 8+3=11。
        DamageResolutionContext averageContext = BuildMainContext(
                state,
                holder,
                dragon,
                attackSuccess: true,
                hasAttackCheck: true
            )
            .WithDamageRollMode("average");
        AttackEffectResolutionResult execute = fixture.Resolver.ResolveEffects(
            holder,
            dragon,
            new[] { effect },
            averageContext
        );
        _test.Eq(execute.Damage, 11, "execute 应结算 8+1D4(avg 3)=11。");

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            holder,
            dragon,
            effect,
            averageContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(preview.Damage, execute.Damage, "preview 应与 execute 对四件套加骰解释一致。");

        BattleDamagePreviewWorkingSet workingSet = BattleDamagePreviewWorkingSet.CreateDetached(
            holder,
            dragon,
            state
        );
        BattleDamagePreviewScoreResult score = fixture.Resolver.PreviewDamageScoreOnWorkingSetTyped(
            workingSet,
            effect,
            averageContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(score.Damage, execute.Damage, "AI 估值应与 execute 对四件套加骰解释一致。");

        // 对照：非 dragon 目标三条链路都不加骰。
        BattleUnitState plain = BuildEnemyUnit("oath_parity_plain", new Vector2I(4, 4), withDragonTag: false);
        state.SetUnit(plain);
        state.enemy_unit_ids.Add(plain.unit_id);
        DamageResolutionContext plainContext = BuildMainContext(
                state,
                holder,
                plain,
                attackSuccess: true,
                hasAttackCheck: true
            )
            .WithDamageRollMode("average");
        AttackEffectResolutionResult plainExecute = fixture.Resolver.ResolveEffects(
            holder,
            plain,
            new[] { effect },
            plainContext
        );
        _test.Eq(plainExecute.Damage, 8, "非 dragon 目标 execute 应保持 power 8。");
        BattleDamagePreviewResult plainPreview = fixture.Resolver.PreviewDamageEffectTyped(
            holder,
            plain,
            effect,
            plainContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(plainPreview.Damage, plainExecute.Damage, "非 dragon 目标 preview 应与 execute 一致。");
    }

    private void TestDragonBreathConditionalTier()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        // 五种吐息（fire/freeze/poison/acid/lightning）：满套对 dragon 吐息全部 half。
        (StringName SkillId, StringName Tag)[] breaths =
        {
            ("dragon_breath_fire_cone", "fire"),
            ("dragon_breath_freeze_cone", "freeze"),
            ("dragon_breath_poison_cone", "poison"),
            ("dragon_breath_acid_line", "acid"),
            ("dragon_breath_lightning_line", "lightning"),
        };
        foreach ((StringName skillId, StringName tag) in breaths)
        {
            BattleUnitState holder = fixture.BuildSetUnit($"breath_full_{tag}", MemberSlice(0, 4));
            PrimeUnit(holder, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
            BattleUnitState dragon = BuildEnemyUnit($"breath_dragon_{tag}", new Vector2I(4, 3), withDragonTag: true);
            BattleState state = fixture.SetupBattle(
                $"dragon_scale_breath_{tag}",
                new[] { holder },
                new[] { dragon },
                worldStep: 0
            );
            CombatEffectDefinition breath = fixture.Skills[skillId].CombatProfile.EffectDefinitions[0];
            _test.Eq(breath.Power, 12, $"{skillId} 应保持 power=12 基线。");
            _test.Eq(breath.SaveTag, DragonBreathSaveTag, $"{skillId} 应使用 dragon_breath save tag。");
            AttackEffectResolutionResult failedSave = fixture.Resolver.ResolveEffects(
                dragon,
                holder,
                new[] { breath },
                BuildBreathContext(state, dragon, holder, skillId, saveRoll: 1)
            );
            _test.Eq(failedSave.Damage, 6, $"满套受到 dragon {tag} 吐息豁免失败应为 12/2=6。");
            holder.SetCurrentHp(200);
            AttackEffectResolutionResult successSave = fixture.Resolver.ResolveEffects(
                dragon,
                holder,
                new[] { breath },
                BuildBreathContext(state, dragon, holder, skillId, saveRoll: 20)
            );
            _test.Eq(successSave.Damage, 3, $"满套受到 dragon {tag} 吐息豁免成功应为 6/2=3。");
        }

        // 2 件（头盔+胸甲）：poison 吐息无静态抗性也无 4 件条件 tier → 12。
        BattleUnitState twoPiece = fixture.BuildSetUnit("breath_two_piece", MemberSlice(0, 2));
        PrimeUnit(twoPiece, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragonA = BuildEnemyUnit("breath_two_piece_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleState twoPieceState = fixture.SetupBattle(
            "dragon_scale_breath_two_piece",
            new[] { twoPiece },
            new[] { dragonA },
            worldStep: 0
        );
        CombatEffectDefinition poisonBreath = fixture
            .Skills["dragon_breath_poison_cone"]
            .CombatProfile
            .EffectDefinitions[0];
        AttackEffectResolutionResult twoPieceResult = fixture.Resolver.ResolveEffects(
            dragonA,
            twoPiece,
            new[] { poisonBreath },
            BuildBreathContext(twoPieceState, dragonA, twoPiece, "dragon_breath_poison_cone", saveRoll: 1)
        );
        _test.Eq(twoPieceResult.Damage, 12, "仅 2 件时 dragon poison 吐息不应减半。");

        // dragon 普通元素法术（非吐息 save tag）不触发条件 tier。
        BattleUnitState fullHolder = fixture.BuildSetUnit("breath_spell_holder", MemberSlice(0, 4));
        PrimeUnit(fullHolder, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragonB = BuildEnemyUnit("breath_spell_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleState spellState = fixture.SetupBattle(
            "dragon_scale_breath_spell",
            new[] { fullHolder },
            new[] { dragonB },
            worldStep: 0
        );
        AttackEffectResolutionResult spellResult = fixture.Resolver.ResolveEffects(
            dragonB,
            fullHolder,
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    damageTag: "poison",
                    power: 12,
                    saveDc: 10,
                    saveAbility: "constitution",
                    saveTag: "magic",
                    savePartialOnSuccess: true
                ),
            },
            BuildBreathContext(spellState, dragonB, fullHolder, "test_dragon_poison_spell", saveRoll: 1)
        );
        _test.Eq(spellResult.Damage, 12, "dragon 的普通 poison 法术不应触发吐息 half。");

        // 非 dragon 的 breath 不触发。
        BattleUnitState fullHolder2 = fixture.BuildSetUnit("breath_nondragon_holder", MemberSlice(0, 4));
        PrimeUnit(fullHolder2, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState plainCaster = BuildEnemyUnit("breath_nondragon_caster", new Vector2I(4, 3), withDragonTag: false);
        BattleState plainState = fixture.SetupBattle(
            "dragon_scale_breath_nondragon",
            new[] { fullHolder2 },
            new[] { plainCaster },
            worldStep: 0
        );
        AttackEffectResolutionResult plainBreath = fixture.Resolver.ResolveEffects(
            plainCaster,
            fullHolder2,
            new[] { poisonBreath },
            BuildBreathContext(plainState, plainCaster, fullHolder2, "dragon_breath_poison_cone", saveRoll: 1)
        );
        _test.Eq(plainBreath.Damage, 12, "非 dragon 来源的吐息不应触发条件 tier。");

        // query 级 provenance：条件 tier 应保留 binding/action 来源。
        IReadOnlyList<BattleEquipmentAbilityMitigationTierResult> tiers = fixture
            .Runtime.GetEquipmentAbilityRuntimeService()
            .DamageQuery.CollectMitigationTiers(
                new BattleEquipmentAbilityMitigationTierContext
                {
                    SourceUnit = dragonB,
                    TargetUnit = fullHolder,
                    BattleState = spellState,
                    SkillId = "dragon_breath_poison_cone",
                    SaveTag = DragonBreathSaveTag,
                    DamageTag = "poison",
                    DamageOriginKind = BattleDamageOriginKind.MainDirectEffect,
                }
            );
        _test.Eq(tiers.Count, 1, "dragon poison 吐息应授予一条条件 tier。");
        _test.Eq(tiers[0].BindingId, OathBindingId, "条件 tier 应保留四件套 binding provenance。");
        _test.Eq(tiers[0].MitigationTier, new StringName("half"), "条件 tier 应为 half。");
    }

    private void TestBreathTierAggregationRules()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("breath_aggregation", MemberSlice(0, 4));
        PrimeUnit(holder, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragon = BuildEnemyUnit("breath_aggregation_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleState state = fixture.SetupBattle(
            "dragon_scale_breath_aggregation",
            new[] { holder },
            new[] { dragon },
            worldStep: 0
        );
        CombatEffectDefinition poisonBreath = fixture
            .Skills["dragon_breath_poison_cone"]
            .CombatProfile
            .EffectDefinitions[0];

        // immune 优先于 half。
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "test_dragon_scale_poison_immune",
                source_unit_id = holder.unit_id,
                stacks = 1,
                duration = -1,
                damage_tag = "poison",
                mitigation_tier = "immune",
            }
        );
        AttackEffectResolutionResult immuneResult = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { poisonBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_poison_cone", saveRoll: 1)
        );
        _test.Eq(immuneResult.Damage, 0, "poison immune 应优先于 half 并归零吐息伤害。");
        _test.Eq(
            immuneResult.DamageEvents[0].MitigationTier,
            MitigationTierKind.Immune,
            "报告应记录 immune 最终 tier。"
        );

        // half 与 double 抵消回 normal。
        holder.EraseStatusEffect("test_dragon_scale_poison_immune");
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "test_dragon_scale_poison_double",
                source_unit_id = holder.unit_id,
                stacks = 1,
                duration = -1,
                damage_tag = "poison",
                mitigation_tier = "double",
            }
        );
        holder.SetCurrentHp(200);
        AttackEffectResolutionResult cancelled = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { poisonBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_poison_cone", saveRoll: 1)
        );
        _test.Eq(cancelled.Damage, 12, "条件 half 与 status double 应抵消回 12。");
        _test.Eq(
            cancelled.DamageEvents[0].MitigationTier,
            MitigationTierKind.Normal,
            "half/double 抵消后报告应记录 normal tier。"
        );
        holder.EraseStatusEffect("test_dragon_scale_poison_double");

        // power=12 fire breath：满套与头盔单件都是 6/3，证明多份 half 不 quarter。
        CombatEffectDefinition fireBreath = fixture
            .Skills["dragon_breath_fire_cone"]
            .CombatProfile
            .EffectDefinitions[0];
        holder.SetCurrentHp(200);
        AttackEffectResolutionResult fullFailed = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { fireBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_fire_cone", saveRoll: 1)
        );
        _test.Eq(fullFailed.Damage, 6, "满套 fire 吐息豁免失败应为 6。");
        holder.SetCurrentHp(200);
        AttackEffectResolutionResult fullSuccess = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { fireBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_fire_cone", saveRoll: 20)
        );
        _test.Eq(fullSuccess.Damage, 3, "满套 fire 吐息豁免成功应为 3。");

        BattleUnitState headOnly = fixture.BuildSetUnit("breath_head_only", MemberSlice(0, 1));
        PrimeUnit(headOnly, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragon2 = BuildEnemyUnit("breath_head_only_dragon", new Vector2I(4, 3), withDragonTag: true);
        BattleState headState = fixture.SetupBattle(
            "dragon_scale_breath_head_only",
            new[] { headOnly },
            new[] { dragon2 },
            worldStep: 0
        );
        AttackEffectResolutionResult headFailed = fixture.Resolver.ResolveEffects(
            dragon2,
            headOnly,
            new[] { fireBreath },
            BuildBreathContext(headState, dragon2, headOnly, "dragon_breath_fire_cone", saveRoll: 1)
        );
        _test.Eq(headFailed.Damage, 6, "头盔单件 fire 吐息豁免失败应为 6。");
        headOnly.SetCurrentHp(200);
        AttackEffectResolutionResult headSuccess = fixture.Resolver.ResolveEffects(
            dragon2,
            headOnly,
            new[] { fireBreath },
            BuildBreathContext(headState, dragon2, headOnly, "dragon_breath_fire_cone", saveRoll: 20)
        );
        _test.Eq(headSuccess.Damage, 3, "头盔单件 fire 吐息豁免成功应为 3。");
    }

    private void TestBreathTierFixedDrSaveOrdering()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(Array.Empty<int>());
        BattleUnitState holder = fixture.BuildSetUnit("breath_fixed_dr", MemberSlice(0, 4));
        PrimeUnit(holder, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragon = BuildEnemyUnit(
            "breath_fixed_dr_dragon",
            new Vector2I(4, 3),
            withDragonTag: true
        );
        BattleState state = fixture.SetupBattle(
            "dragon_scale_breath_fixed_dr",
            new[] { holder },
            new[] { dragon },
            worldStep: 0
        );
        CombatEffectDefinition fireBreath = fixture
            .Skills["dragon_breath_fire_cone"]
            .CombatProfile
            .EffectDefinitions[0];
        // synthetic 固定 DR=2（damage_reduction_up strength 1 × 2），锁定 tier → fixed DR → save 整数顺序。
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "damage_reduction_up",
                source_unit_id = holder.unit_id,
                power = 1,
                stacks = 1,
                duration = -1,
            }
        );

        AttackEffectResolutionResult failed = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { fireBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_fire_cone", saveRoll: 1)
        );
        DamageEventResult failedEvent = failed.DamageEvents[0];
        _test.Eq(failedEvent.RolledDamage, 12, "固定 DR 场景吐息基础伤害应为 12。");
        _test.Eq(failedEvent.TierAdjustedDamage, 6, "tier half 应在固定 DR 前把 12 减半为 6。");
        _test.Eq(failedEvent.FixedMitigationTotal, 2, "固定 DR 2 应在 tier 之后扣除。");
        _test.Eq(failedEvent.ResolvedDamage, 4, "豁免失败最终应为 12/2-2=4。");
        _test.Eq(failed.Damage, 4, "豁免失败承伤应为 4。");
        _test.Eq(
            failedEvent.MitigationTier,
            MitigationTierKind.Half,
            "报告应记录 half 最终 tier。"
        );
        _test.True(
            HasEquipmentTierSource(failedEvent.MitigationSources, "龙鳞预判吐息"),
            "报告应保留四件套条件 tier source 标签。"
        );
        _test.True(
            failedEvent.FixedMitigationSourceLabels.Contains("damage_reduction_up"),
            "报告应保留固定 DR source。"
        );

        holder.SetCurrentHp(200);
        AttackEffectResolutionResult success = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { fireBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_fire_cone", saveRoll: 20)
        );
        DamageEventResult successEvent = success.DamageEvents[0];
        _test.Eq(successEvent.PreSaveDamage, 4, "豁免前伤害应为 DR 后的 4。");
        _test.Eq(successEvent.SaveAdjustedDamage, 2, "成功豁免应把 DR 后的 4 减半为 2。");
        _test.Eq(success.Damage, 2, "豁免成功承伤应为 2。");

        // immune tier 在固定 DR 场景下仍应归零。
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "test_dragon_scale_fire_immune",
                source_unit_id = holder.unit_id,
                stacks = 1,
                duration = -1,
                damage_tag = "fire",
                mitigation_tier = "immune",
            }
        );
        holder.SetCurrentHp(200);
        AttackEffectResolutionResult immune = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { fireBreath },
            BuildBreathContext(state, dragon, holder, "dragon_breath_fire_cone", saveRoll: 1)
        );
        _test.Eq(immune.Damage, 0, "immune tier 在固定 DR 场景下仍应归零吐息伤害。");
        _test.Eq(
            immune.DamageEvents[0].MitigationTier,
            MitigationTierKind.Immune,
            "固定 DR 场景下报告应记录 immune 最终 tier。"
        );
    }

    private static bool HasEquipmentTierSource(MitigationSourceResult[] sources, string sourceId)
    {
        foreach (MitigationSourceResult source in sources ?? Array.Empty<MitigationSourceResult>())
        {
            if (source.Type == "equipment_ability_mitigation_tier" && source.StatusId == sourceId)
                return true;
        }
        return false;
    }

    private void AssertFireDamageFromDragonBreath(
        DragonScaleFixture fixture,
        BattleUnitState holder,
        StringName damageTag,
        int expectedDamage,
        string message
    )
    {
        PrimeUnit(holder, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState dragon = BuildEnemyUnit(
            new StringName($"fire_stacking_dragon_{damageTag}"),
            new Vector2I(4, 3),
            withDragonTag: true
        );
        BattleState state = fixture.SetupBattle(
            new StringName($"dragon_scale_fire_stacking_{damageTag}"),
            new[] { holder },
            new[] { dragon },
            worldStep: 0
        );
        AttackEffectResolutionResult result = fixture.Resolver.ResolveEffects(
            dragon,
            holder,
            new[] { BuildSpellEffect(power: 12, damageTag: damageTag) },
            BuildMainContext(state, dragon, holder, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(result.Damage, expectedDamage, message);
    }

    private void AssertAttackBonusVsCreature(
        DragonScaleFixture fixture,
        BattleUnitState holder,
        bool dragonTarget,
        int expectedDelta,
        string message
    )
    {
        PrimeUnit(holder, hp: 200, maxHp: 200, ap: 3, new Vector2I(3, 3));
        BattleUnitState target = BuildEnemyUnit(
            new StringName($"attack_bonus_target_{holder.unit_id}_{dragonTarget}"),
            new Vector2I(4, 3),
            withDragonTag: dragonTarget
        );
        BattleState state = fixture.SetupBattle(
            new StringName($"dragon_scale_attack_bonus_{holder.unit_id}_{dragonTarget}"),
            new[] { holder },
            new[] { target },
            worldStep: 0
        );
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill(
            "dragon_scale_test_attack"
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
                    "dragon_scale_attack_bonus",
                    force_hit_no_crit: false
                )
            );
        _test.Eq(bundle.GetEffectiveModifierDelta(), expectedDelta, message);
    }

    private void AssertGearSetSource(BattleUnitState unit, StringName bindingId)
    {
        BattleEquipmentAbilitySourceReadView source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"missing gear-set ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold)
            throw new InvalidOperationException($"{bindingId} must retain gear-set threshold provenance.");
        if (!source.SourceEquipmentInstanceId.ToString().Contains(HeadItemId.ToString(), StringComparison.Ordinal))
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

    private static bool HasBonusDiceSource(
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> results,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult result in results ?? Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>())
        {
            if (result?.BindingId == bindingId)
                return true;
        }
        return false;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Enumerable.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private static StringName MitigationOrEmpty(BattleUnitState unit, StringName damageTag) =>
        unit != null && unit.TryGetDamageResistanceTyped(damageTag, out StringName tier)
            ? ProgressionDataUtils.to_string_name(tier)
            : new StringName("");

    private static int[] MemberSlice(int fromInclusive, int toExclusive)
    {
        var result = new List<int>();
        for (int index = fromInclusive; index < toExclusive; index++)
            result.Add(index);
        return result.ToArray();
    }

    private static CombatEffectDefinition BuildWeaponEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            power: 4,
            addWeaponDice: true
        );

    private static CombatEffectDefinition BuildSpellEffect(int power, StringName damageTag) =>
        TestSkillDefinitionProjection.BuildEffect("damage", damageTag: damageTag, power: power);

    private static DamageResolutionContext BuildMainContext(
        BattleState state,
        BattleUnitState source,
        BattleUnitState target,
        bool attackSuccess,
        bool hasAttackCheck
    )
    {
        DamageResolutionContext context = DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: attackSuccess,
                secondaryHitSuccess: false,
                skillId: "test_dragon_scale_direct"
            )
            .WithBattleState(state)
            .WithDamageOriginKind(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    source,
                    target
                )
            );
        return hasAttackCheck ? context.WithAttackCheck() : context;
    }

    private static DamageResolutionContext BuildBreathContext(
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState holder,
        StringName skillId,
        int saveRoll
    ) =>
        DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: skillId,
                saveRollOverrides: new[] { saveRoll }
            )
            .WithBattleState(state)
            .WithDamageOriginKind(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    attacker,
                    holder
                )
            );

    private static void ApplyRangedWeaponProjection(BattleUnitState unit)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "dragon_scale_test_bow",
                weapon_profile_type_id = "longbow",
                weapon_range_type = "ranged",
                weapon_family = "bow",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 6,
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
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

    private static BattleUnitState BuildEnemyUnit(StringName unitId, Vector2I coord, bool withDragonTag)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.SetCombatResources(200, mp: 0, stamina: 0, aura: 0, ap: 2, movePoints: 2);
        unit.SetAnchorCoord(coord);
        unit.SetUnarmedWeaponProjectionTyped();
        unit.SetEquipmentView(new EquipmentState());
        if (withDragonTag)
            unit.ReplaceCreatureTypeTagsTyped(new StringName[] { "dragon" });
        return unit;
    }

    private sealed class DragonScaleFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private DragonScaleFixture(
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
            Traits = snapshot.Traits;
            GearSets = snapshot.GearSets;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal FixedRollDamageResolver Resolver { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> Traits { get; }
        internal IReadOnlyDictionary<StringName, GearSetDefinition> GearSets { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static DragonScaleFixture Build(IEnumerable<int> damageRolls)
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
                var resolver = new FixedRollDamageResolver(rollPayload);
                BattleTestFixture.ConfigureDamageResolverForTests(runtime, resolver);
                return new DragonScaleFixture(characterManagement, partyState, runtime, resolver, snapshot);
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
                throw new InvalidOperationException("Dragon scale fixture must build exactly one ally.");
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
            return party;
        }
    }
}
