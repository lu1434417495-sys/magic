using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using Variant = Godot.Variant;

public partial class run_giants_heel_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName GiantsHeelItemId =
        "weapon_unique_greatsword_giants_heel_024";
    private static readonly StringName GiantSlayerTraitId =
        "weapon.greatsword.giants_heel.giant_slayer";
    private static readonly StringName AnkleChopTraitId =
        "weapon.greatsword.giants_heel.ankle_chop";
    private static readonly StringName PrimordialWeightTraitId =
        "weapon.greatsword.giants_heel.primordial_weight";
    private static readonly StringName GiantSlayerBindingId =
        "binding.weapon.greatsword.giants_heel.giant_slayer";
    private static readonly StringName AnkleChopBindingId =
        "binding.weapon.greatsword.giants_heel.ankle_chop";
    private static readonly StringName PrimordialWeightBindingId =
        "binding.weapon.greatsword.giants_heel.primordial_weight";
    private static readonly StringName ProneStatusId = "prone";
    private static readonly StringName HobbledStatusId = "giants_heel_hobbled";
    private static readonly StringName LockoutStatusId = "giants_heel_ankle_chop_lockout";
    private static readonly StringName KnockdownImmunityStatusId = "knockdown_immunity";
    private static readonly StringName StrengthAttributeId =
        UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength);
    private static readonly StringName StrengthModifierAttributeId =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.StrengthModifier);

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGiantsHeelProjectsRealContentAndEquipmentRequirement();
            TestPrimordialWeightAttackPenaltyUsesBodySizeAndStrengthFacts();
            TestGiantSlayerDamageDiceAndGiantAdvantage();
            TestAnkleChopRequiresRealDamageAndAppliesFailedSaveStatusesOnce();
            RequestTestExit(_test.Finish("Giant's Heel weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Giant's Heel weapon ability regression"));
        }
    }

    private void TestGiantsHeelProjectsRealContentAndEquipmentRequirement()
    {
        using GiantsHeelFixture fixture = GiantsHeelFixture.Build(new GArray { 1, 1, 1, 1 });
        _test.True(fixture.ItemDefs.ContainsKey(GiantsHeelItemId), "真实物品内容应包含巨人之踵。");
        _test.True(fixture.TraitDefs.ContainsKey(GiantSlayerTraitId), "真实 trait 应包含巨人杀手。");
        _test.True(fixture.TraitDefs.ContainsKey(AnkleChopTraitId), "真实 trait 应包含斩踵。");
        _test.True(
            fixture.TraitDefs.ContainsKey(PrimordialWeightTraitId),
            "真实 trait 应包含原始重量。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(GiantSlayerBindingId),
            "真实装备能力内容应包含巨人杀手 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(AnkleChopBindingId),
            "真实装备能力内容应包含斩踵 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(PrimordialWeightBindingId),
            "真实装备能力内容应包含原始重量 binding。"
        );
        if (!fixture.ItemDefs.ContainsKey(GiantsHeelItemId))
            return;

        ItemDef rawGiantsHeel = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greatsword_giants_heel.tres"
        );
        _test.True(rawGiantsHeel != null, "巨人之踵原始资源应能加载。");
        if (rawGiantsHeel != null)
        {
            _test.Eq(
                rawGiantsHeel.base_item_id,
                new StringName("weapon_type_greatsword_base"),
                "巨人之踵应继承 greatsword 模板。"
            );
            _test.True(
                rawGiantsHeel.equip_requirement is EquipmentRequirement requirement
                    && requirement.min_body_size == 3,
                "巨人之踵应通过 EquipmentRequirement.min_body_size=3 限制装备。"
            );
        }

        AssertEquipmentRequirementBlocksMediumAndAllowsLarge(fixture.ItemDefs);

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildGiantsHeelUnit("projection", "large", strength: 18);
        _test.Eq(equipped.weapon_item_id, GiantsHeelItemId, "巨人之踵装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("greatsword"),
            "巨人之踵应投影为 greatsword。"
        );
        _test.True(equipped.weapon_uses_two_hands, "巨人之踵应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 2, "巨人之踵应是 2D6+4。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 6, "巨人之踵应是 2D6+4。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 4, "巨人之踵应是 2D6+4。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "巨人之踵应造成斩击。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            GiantSlayerTraitId,
            GiantSlayerBindingId,
            "eq_giants_heel_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            AnkleChopTraitId,
            AnkleChopBindingId,
            "eq_giants_heel_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            PrimordialWeightTraitId,
            PrimordialWeightBindingId,
            "eq_giants_heel_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除巨人之踵后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除巨人之踵后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除巨人之踵后装备能力源应清空。");
    }

    private void TestPrimordialWeightAttackPenaltyUsesBodySizeAndStrengthFacts()
    {
        using GiantsHeelFixture fixture = GiantsHeelFixture.Build(new GArray { 1, 1 });
        if (!fixture.Bindings.ContainsKey(PrimordialWeightBindingId))
            return;
        BattleUnitState strength17 = fixture.BuildGiantsHeelUnit("str17", "large", strength: 17);
        BattleUnitState strength18 = fixture.BuildGiantsHeelUnit("str18", "large", strength: 18);
        BattleUnitState target = BuildTarget("primordial_target", new Vector2I(1, 0), "large");
        BattleAttackRollModifierBundle lowBundle = BuildAttackBundle(
            fixture,
            strength17,
            target,
            "giants_heel_primordial_low"
        );
        BattleAttackRollModifierBundle highBundle = BuildAttackBundle(
            fixture,
            strength18,
            target,
            "giants_heel_primordial_high"
        );

        _test.Eq(lowBundle.GetEffectiveModifierDelta(), -4, "Large 且 strength 17 应吃原始重量 -4。");
        _test.True(
            HasModifier(lowBundle, PrimordialWeightBindingId, -4),
            "原始重量 -4 应在 modifier breakdown 中标明装备能力来源。"
        );
        _test.Eq(highBundle.GetEffectiveModifierDelta(), 0, "Large 且 strength 18 不应吃原始重量惩罚。");
    }

    private void TestGiantSlayerDamageDiceAndGiantAdvantage()
    {
        int plainLargeDamage = MeasureBasicAttackDamage("large", Array.Empty<StringName>(), true);
        int largeDamage = MeasureBasicAttackDamage("large", Array.Empty<StringName>(), false);
        int mediumDamage = MeasureBasicAttackDamage("medium", Array.Empty<StringName>(), false);
        _test.Eq(plainLargeDamage, 6, "对照组巨人之踵基础真实攻击应为 2D6+4。");
        _test.Eq(largeDamage, 8, "命中 Large+ 目标应追加 2D8 physical_slash。");
        _test.Eq(mediumDamage, 6, "命中 Medium 目标不应追加巨人杀手 2D8。");

        using GiantsHeelFixture fixture = GiantsHeelFixture.Build(new GArray { 1, 1 });
        if (!fixture.Bindings.ContainsKey(GiantSlayerBindingId))
            return;
        BattleUnitState attacker = fixture.BuildGiantsHeelUnit("advantage", "large", strength: 18);
        BattleUnitState giant = BuildTarget(
            "giant_target",
            new Vector2I(1, 0),
            "large",
            new[] { new StringName("giant") }
        );
        BattleUnitState nonGiant = BuildTarget("non_giant_target", new Vector2I(1, 0), "large");

        BattleAttackRollModifierBundle giantBundle = BuildAttackBundle(
            fixture,
            attacker,
            giant,
            "giants_heel_giant_advantage"
        );
        BattleAttackRollModifierBundle nonGiantBundle = BuildAttackBundle(
            fixture,
            attacker,
            nonGiant,
            "giants_heel_giant_no_advantage"
        );

        _test.True(giantBundle.HasAdvantage, "目标 creature_type_tags 包含 giant 时应获得攻击优势。");
        _test.True(
            HasAdvantageModifier(giantBundle, GiantSlayerBindingId),
            "巨人杀手优势应在 modifier breakdown 中标明装备能力来源。"
        );
        _test.False(nonGiantBundle.HasAdvantage, "Large 但非 giant 类型目标不应获得攻击优势。");
    }

    private void TestAnkleChopRequiresRealDamageAndAppliesFailedSaveStatusesOnce()
    {
        using GiantsHeelFixture fixture = GiantsHeelFixture.Build(new GArray { 1, 1 });
        if (!fixture.Bindings.ContainsKey(AnkleChopBindingId))
            return;
        _test.False(
            BattleStatusSemanticTable.HasSemantic(HobbledStatusId),
            "斩踵迟缓状态语义应由装备配置提供，不应硬编码在全局状态表。"
        );

        BattleUnitState attacker = fixture.BuildGiantsHeelUnit("ankle", "large", strength: 18);
        BattleUnitState failedTarget = BuildTarget("ankle_fail_target", new Vector2I(1, 0), "large");
        failedTarget.SetCurrentMovePoints(BattleUnitState.DefaultMovePointsPerTurn);
        ResolveAnkleChop(
            fixture,
            attacker,
            failedTarget,
            "giants_heel_ankle_fail",
            hpDamage: 1,
            saveRollOverride: 1
        );
        BattleStatusEffectState lockout = failedTarget.GetStatusEffect(LockoutStatusId);
        BattleStatusEffectState prone = failedTarget.GetStatusEffect(ProneStatusId);
        BattleStatusEffectState hobbled = failedTarget.GetStatusEffect(HobbledStatusId);
        _test.True(lockout != null, "斩踵豁免失败后应施加 lockout。");
        _test.True(prone != null, "斩踵豁免失败后应施加 prone。");
        _test.True(hobbled != null, "斩踵豁免失败后应施加 hobbled。");
        _test.Eq(lockout?.duration ?? -1, 50, "lockout 应持续 50TU。");
        _test.Eq(prone?.duration ?? -1, 50, "prone 应持续 50TU。");
        _test.Eq(hobbled?.duration ?? -1, 50, "hobbled 应持续 50TU。");
        _test.Eq(hobbled?.move_point_capacity_delta ?? 0, -2, "hobbled 应声明移动力上限 -2。");
        _test.Eq(failedTarget.GetMovePointCapacity(), 0, "hobbled 后普通移动力上限应降到 0。");
        _test.Eq(failedTarget.current_move_points, 0, "施加 hobbled 时当前移动力应被 clamp 到上限。");

        lockout.duration = 37;
        failedTarget.SetStatusEffect(lockout);
        ResolveAnkleChop(
            fixture,
            attacker,
            failedTarget,
            "giants_heel_ankle_lockout",
            hpDamage: 1,
            saveRollOverride: 1
        );
        _test.Eq(
            failedTarget.GetStatusEffect(LockoutStatusId)?.duration ?? -1,
            37,
            "已有 lockout 时再次命中不应刷新斩踵 lockout。"
        );

        BattleUnitState successTarget = BuildTarget("ankle_success_target", new Vector2I(1, 0), "large");
        ResolveAnkleChop(
            fixture,
            attacker,
            successTarget,
            "giants_heel_ankle_success",
            hpDamage: 1,
            saveRollOverride: 20
        );
        _test.False(successTarget.HasStatusEffect(LockoutStatusId), "豁免成功不应施加 lockout。");
        _test.False(successTarget.HasStatusEffect(HobbledStatusId), "豁免成功不应施加 hobbled。");
        _test.False(successTarget.HasStatusEffect(ProneStatusId), "豁免成功不应施加 prone。");

        BattleUnitState zeroDamageTarget = BuildTarget("ankle_zero_damage_target", new Vector2I(1, 0), "large");
        ResolveAnkleChop(
            fixture,
            attacker,
            zeroDamageTarget,
            "giants_heel_ankle_zero_damage",
            hpDamage: 0,
            saveRollOverride: 1
        );
        _test.False(zeroDamageTarget.HasStatusEffect(LockoutStatusId), "0 hp_damage 不应触发斩踵。");

        BattleUnitState mediumTarget = BuildTarget("ankle_medium_target", new Vector2I(1, 0), "medium");
        ResolveAnkleChop(
            fixture,
            attacker,
            mediumTarget,
            "giants_heel_ankle_medium",
            hpDamage: 1,
            saveRollOverride: 1
        );
        _test.False(mediumTarget.HasStatusEffect(LockoutStatusId), "Medium 目标不应触发斩踵。");

        BattleUnitState immuneTarget = BuildTarget("ankle_immune_target", new Vector2I(1, 0), "large");
        immuneTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = KnockdownImmunityStatusId,
                source_unit_id = "fixture",
                stacks = 1,
                power = 1,
                duration = 50,
            }
        );
        ResolveAnkleChop(
            fixture,
            attacker,
            immuneTarget,
            "giants_heel_ankle_immune",
            hpDamage: 1,
            saveRollOverride: 1
        );
        _test.False(immuneTarget.HasStatusEffect(LockoutStatusId), "knockdown_immunity 应阻止斩踵。");
    }

    private static void AssertEquipmentRequirementBlocksMediumAndAllowsLarge(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        PartyState mediumParty = BuildPartyState("medium_hero", "medium", strength: 18);
        PartyWarehouseService mediumWarehouse = BuildWarehouseService(mediumParty, itemDefs);
        PartyEquipmentService mediumEquipment = BuildEquipmentService(
            mediumParty,
            itemDefs,
            mediumWarehouse
        );
        mediumWarehouse.AddItemTyped(GiantsHeelItemId, 1);
        PartyEquipmentService.EquipmentActionResult mediumResult =
            mediumEquipment.EquipItemTyped("medium_hero", GiantsHeelItemId);
        if (mediumResult.Success || mediumResult.ErrorCode != "body_size_too_small")
        {
            throw new InvalidOperationException(
                $"Medium 装备巨人之踵应失败 body_size_too_small，actual success={mediumResult.Success} error={mediumResult.ErrorCode}。"
            );
        }

        PartyState largeParty = BuildPartyState("large_hero", "large", strength: 17);
        PartyWarehouseService largeWarehouse = BuildWarehouseService(largeParty, itemDefs);
        PartyEquipmentService largeEquipment = BuildEquipmentService(
            largeParty,
            itemDefs,
            largeWarehouse
        );
        largeWarehouse.AddItemTyped(GiantsHeelItemId, 1);
        PartyEquipmentService.EquipmentActionResult largeResult =
            largeEquipment.EquipItemTyped("large_hero", GiantsHeelItemId);
        if (!largeResult.Success)
        {
            throw new InvalidOperationException(
                $"Large strength 17 装备巨人之踵应成功，actual error={largeResult.ErrorCode}。"
            );
        }
    }

    private static int MeasureBasicAttackDamage(
        StringName targetBodySizeCategory,
        IReadOnlyList<StringName> targetTags,
        bool stripAbilitySources
    )
    {
        using GiantsHeelFixture fixture = GiantsHeelFixture.Build(new GArray { 1, 1, 1, 1 });
        BattleUnitState attacker = fixture.BuildGiantsHeelUnit(
            stripAbilitySources ? "plain_damage" : $"damage_{targetBodySizeCategory}",
            "large",
            strength: 18
        );
        if (stripAbilitySources)
            attacker.equipment_ability_sources.Clear();
        BattleUnitState target = BuildTarget("damage_target", new Vector2I(1, 0), targetBodySizeCategory, targetTags);
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            $"giants_heel_damage_{targetBodySizeCategory}_{stripAbilitySources}",
            previewCommand: false
        );
        return 100 - target.current_hp;
    }

    private static BattleAttackRollModifierBundle BuildAttackBundle(
        GiantsHeelFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName traceSource
    )
    {
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        return attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                target,
                attackSkill,
                "skill_attack_check",
                traceSource,
                force_hit_no_crit: false
            )
        );
    }

    private static void ResolveAnkleChop(
        GiantsHeelFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int hpDamage,
        int saveRollOverride
    )
    {
        PrimeStrengthSave(target);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveDamageApplied(
            new BattleEquipmentAbilityDamageAppliedContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = state,
                HpDamage = hpDamage,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static void PrimeStrengthSave(BattleUnitState unit)
    {
        unit.attribute_snapshot.SetValue(StrengthAttributeId, 10);
        unit.attribute_snapshot.SetValue(StrengthModifierAttributeId, 0);
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (
                spec.source_domain == "equipment_ability"
                && spec.source_id == sourceId
                && spec.modifier_delta == delta
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAdvantageModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (
                spec.source_domain == "equipment_ability"
                && spec.source_id == sourceId
                && spec.applies_to == "attack_advantage"
            )
            {
                return true;
            }
        }
        return false;
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        if (unit == null)
            throw new InvalidOperationException("unit is null.");
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"unit missing equipment ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentEquipment)
            throw new InvalidOperationException($"{bindingId} should come from persistent equipment.");
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
        {
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
        }
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources ?? new List<BattleEquipmentAbilitySourceState>())
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        StringName bodySizeCategory,
        IReadOnlyList<StringName> tags = null
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 30,
        };
        unit.SetAnchorCoord(coord);
        unit.SetBodySizeCategory(bodySizeCategory);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        PrimeStrengthSave(unit);
        foreach (StringName tag in tags ?? Array.Empty<StringName>())
        {
            if (tag != "")
                unit.creature_type_tags.Add(tag);
        }
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static PartyWarehouseService BuildWarehouseService(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        PartyWarehouseService warehouseService = new();
        warehouseService.Setup(partyState, itemDefs);
        return warehouseService;
    }

    private static PartyEquipmentService BuildEquipmentService(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        PartyWarehouseService warehouseService
    )
    {
        PartyEquipmentService equipmentService = new();
        equipmentService.Setup(partyState, itemDefs, warehouseService);
        return equipmentService;
    }

    private static PartyState BuildPartyState(
        StringName memberId,
        StringName bodySizeCategory,
        int strength
    )
    {
        PartyState partyState = new();
        PartyMemberState memberState = new()
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            progression = new UnitProgress(),
            equipment_state = new EquipmentState(),
            current_hp = 40,
            current_mp = 8,
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = memberState.display_name;
        memberState.SetBodySizeCategory(bodySizeCategory);
        memberState.progression.unit_base_attributes = BuildAttributes(strength);
        partyState.SetMemberState(memberState);
        partyState.active_member_ids = new GStringNameArray { memberId };
        partyState.reserve_member_ids = new GStringNameArray();
        partyState.leader_member_id = memberId;
        partyState.main_character_member_id = memberId;
        return partyState;
    }

    private static UnitBaseAttributes BuildAttributes(int strength)
    {
        UnitBaseAttributes attributes = new();
        attributes.SetAttributeValue(StrengthAttributeId, strength);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence), 10);
        attributes.SetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower), 10);
        attributes.custom_stats[PartyWarehouseService.StorageSpaceAttributeId] = 20;
        return attributes;
    }

    private sealed class GiantsHeelFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private GiantsHeelFixture(
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            PartyState partyState,
            BattleRuntimeModule runtime
        )
        {
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = itemRegistry.GetItemDefsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static GiantsHeelFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            PartyState partyState = BuildPartyState("hero", "large", strength: 18);
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemRegistry.GetItemDefsTyped(),
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemRegistry.GetItemDefsTyped(),
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new GiantsHeelFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildGiantsHeelUnit(
            string label,
            StringName bodySizeCategory,
            int strength
        )
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.SetBodySizeCategory(bodySizeCategory);
            member.progression.unit_base_attributes = BuildAttributes(strength);
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                GiantsHeelItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    GiantsHeelItemId,
                    $"eq_giants_heel_{label}"
                )
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.SetBodySizeCategory(bodySizeCategory);
            unit.attribute_snapshot.SetValue(StrengthAttributeId, strength);
            unit.attribute_snapshot.SetValue(StrengthModifierAttributeId, 0);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            return units[0];
        }
    }
}
