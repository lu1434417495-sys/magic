using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_dragon_scale_battleaxe_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_battleaxe_dragon_scale";
    private static readonly StringName FivefoldScalesTraitId =
        "weapon.battleaxe.dragon_scale.fivefold_scales";
    private static readonly StringName DragonToothEdgeTraitId =
        "weapon.battleaxe.dragon_scale.dragon_tooth_edge";
    private static readonly StringName ScaleGuardTraitId =
        "weapon.battleaxe.dragon_scale.scale_guard";
    private static readonly StringName ScaleRiftTraitId =
        "weapon.battleaxe.dragon_scale.scale_rift";
    private static readonly StringName DragonBalanceTraitId =
        "weapon.battleaxe.dragon_scale.dragon_balance";
    private static readonly StringName DragonToothEdgeBindingId =
        "binding.weapon.battleaxe.dragon_scale.dragon_tooth_edge";
    private static readonly StringName ScaleGuardBindingId =
        "binding.weapon.battleaxe.dragon_scale.scale_guard";
    private static readonly StringName ScaleRiftBindingId =
        "binding.weapon.battleaxe.dragon_scale.scale_rift";
    private static readonly StringName DragonBalanceBindingId =
        "binding.weapon.battleaxe.dragon_scale.dragon_balance";
    private static readonly StringName ScaleRiftStatusId = "dragon_scale_rift";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestContentLoadsAndProjectsFiveTraits();
            TestDragonToothEdgeIsOncePerHolderTurnAndDragonBalanceOnlyAffectsDragons();
            TestScaleGuardReducesOnlyMeleePhysicalDamage();
            TestScaleRiftAppliesOnActualDamageWithConstitutionSaveAndSourceBoundAttackBonus();
            TestEquipmentBonusDiceDoNotTriggerForNonWeaponDamageEffects();
            RequestTestExit(_test.Finish("Dragon Scale Battleaxe weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Dragon Scale Battleaxe weapon ability regression"));
        }
    }

    private void TestContentLoadsAndProjectsFiveTraits()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含龙鳞之斧。");
        _test.True(fixture.TraitDefs.ContainsKey(FivefoldScalesTraitId), "真实 trait 应包含五色龙鳞。");
        _test.True(fixture.TraitDefs.ContainsKey(DragonToothEdgeTraitId), "真实 trait 应包含龙牙锋刃。");
        _test.True(fixture.TraitDefs.ContainsKey(ScaleGuardTraitId), "真实 trait 应包含龙鳞护面。");
        _test.True(fixture.TraitDefs.ContainsKey(ScaleRiftTraitId), "真实 trait 应包含破鳞裂痕。");
        _test.True(fixture.TraitDefs.ContainsKey(DragonBalanceTraitId), "真实 trait 应包含屠龙制衡。");
        _test.True(fixture.Bindings.ContainsKey(DragonToothEdgeBindingId), "真实装备能力内容应包含龙牙锋刃 binding。");
        _test.True(fixture.Bindings.ContainsKey(ScaleGuardBindingId), "真实装备能力内容应包含龙鳞护面 binding。");
        _test.True(fixture.Bindings.ContainsKey(ScaleRiftBindingId), "真实装备能力内容应包含破鳞裂痕 binding。");
        _test.True(fixture.Bindings.ContainsKey(DragonBalanceBindingId), "真实装备能力内容应包含屠龙制衡 binding。");
        if (!fixture.ItemDefs.ContainsKey(ItemId))
            return;

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_dragon_scale.tres"
        );
        _test.True(rawItem != null, "龙鳞之斧原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "龙鳞之斧 item_id 不应包含来源编号。");
            _test.Eq(rawItem.display_name, "龙鳞之斧", "龙鳞之斧显示名应来自设计源。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_battleaxe_base"), "龙鳞之斧应继承 battleaxe 模板。");
            _test.Eq(rawItem.base_price, 75000, "龙鳞之斧基础价格应为 75000。");
            _test.True(ContainsStringName(rawItem.trait_ids, FivefoldScalesTraitId), "龙鳞之斧应固定五色龙鳞。");
            _test.True(ContainsStringName(rawItem.trait_ids, DragonToothEdgeTraitId), "龙鳞之斧应固定龙牙锋刃。");
            _test.True(ContainsStringName(rawItem.trait_ids, ScaleGuardTraitId), "龙鳞之斧应固定龙鳞护面。");
            _test.True(ContainsStringName(rawItem.trait_ids, ScaleRiftTraitId), "龙鳞之斧应固定破鳞裂痕。");
            _test.True(ContainsStringName(rawItem.trait_ids, DragonBalanceTraitId), "龙鳞之斧应固定屠龙制衡。");
        }

        AssertDragonToothPayload(fixture.Bindings[DragonToothEdgeBindingId], "registry");
        AssertScaleGuardPayload(fixture.Bindings[ScaleGuardBindingId], "registry");
        AssertScaleRiftPayload(fixture.Bindings[ScaleRiftBindingId], "registry");
        AssertDragonBalancePayload(fixture.Bindings[DragonBalanceBindingId], "registry");

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildDragonScaleUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "龙鳞之斧装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("battleaxe"), "龙鳞之斧应投影为 battleaxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "龙鳞之斧武器族应为 axe。");
        _test.Eq(equipped.weapon_attack_range, 1, "龙鳞之斧攻击距离应为 1。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "龙鳞之斧基础伤害应为 physical_slash。");
        _test.True(equipped.weapon_is_versatile, "龙鳞之斧应保留 versatile 投影。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "龙鳞之斧单手应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "龙鳞之斧单手应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "龙鳞之斧单手应为 1D8+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "龙鳞之斧双手应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "龙鳞之斧双手应为 1D10+3。");

        AssertTraitProjected(equipped, FivefoldScalesTraitId);
        AssertUnitHasTraitAndAbilitySource(equipped, DragonToothEdgeTraitId, DragonToothEdgeBindingId, "eq_dragon_scale_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, ScaleGuardTraitId, ScaleGuardBindingId, "eq_dragon_scale_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, ScaleRiftTraitId, ScaleRiftBindingId, "eq_dragon_scale_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, DragonBalanceTraitId, DragonBalanceBindingId, "eq_dragon_scale_projection");
        AssertMitigation(equipped, "fire", "half");
        AssertMitigation(equipped, "lightning", "half");
        AssertMitigation(equipped, "poison", "half");
        AssertMitigation(equipped, "acid", "half");
        AssertMitigation(equipped, "freeze", "half");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除龙鳞之斧后 weapon_item_id 应清空。");
        _test.Eq(equipped.weapon_profile_type_id, baseline.weapon_profile_type_id, "移除后武器 profile 应回到装备前状态。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除后装备能力源应清空。");
        _test.Eq(equipped.effective_trait_instances.Count, baseline.effective_trait_instances.Count, "移除后装备 trait 实例应回到装备前状态。");
    }

    private void TestDragonToothEdgeIsOncePerHolderTurnAndDragonBalanceOnlyAffectsDragons()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildDragonScaleUnit("damage");
        BattleUnitState humanoid = BuildTarget("dragon_tooth_humanoid", new Vector2I(1, 0), "humanoid");
        humanoid.current_hp = 160;
        humanoid.attribute_snapshot.SetValue(AttributeService.HP_MAX, 160);

        int beforeFirst = humanoid.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            humanoid,
            "dragon_scale_tooth_first",
            previewCommand: false
        );
        int firstDamage = beforeFirst - humanoid.current_hp;

        int beforeSecond = humanoid.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            humanoid,
            "dragon_scale_tooth_second",
            previewCommand: false
        );
        int secondDamage = beforeSecond - humanoid.current_hp;
        _test.True(firstDamage > secondDamage, "龙牙锋刃同一持有者回合第一次真实武器命中应多出 1D6。");

        attacker.ResetPerTurnCharges();
        int beforeNextTurn = humanoid.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            humanoid,
            "dragon_scale_tooth_next_turn",
            previewCommand: false
        );
        int nextTurnDamage = beforeNextTurn - humanoid.current_hp;
        _test.True(nextTurnDamage > secondDamage, "重置持有者 per-turn charge 后龙牙锋刃应再次触发。");

        BattleUnitState warmup = BuildTarget("dragon_balance_warmup", new Vector2I(1, 0), "humanoid");
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            warmup,
            "dragon_scale_balance_warmup",
            previewCommand: false
        );
        BattleUnitState dragon = BuildTarget("dragon_balance_dragon", new Vector2I(1, 0), "dragon");
        dragon.current_hp = 160;
        dragon.attribute_snapshot.SetValue(AttributeService.HP_MAX, 160);
        int beforeDragon = dragon.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            dragon,
            "dragon_scale_balance_dragon",
            previewCommand: false
        );
        int dragonDamage = beforeDragon - dragon.current_hp;

        using DragonScaleFixture plainFixture = DragonScaleFixture.Build(new GArray());
        BattleUnitState plainAttacker = plainFixture.BuildDragonScaleUnit("plain");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainDragon = BuildTarget("plain_dragon", new Vector2I(1, 0), "dragon");
        plainDragon.current_hp = 160;
        plainDragon.attribute_snapshot.SetValue(AttributeService.HP_MAX, 160);
        int beforePlainDragon = plainDragon.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainDragon,
            "dragon_scale_balance_plain",
            previewCommand: false
        );
        int plainDragonDamage = beforePlainDragon - plainDragon.current_hp;
        _test.True(dragonDamage > plainDragonDamage, "屠龙制衡应只给 dragon 目标额外 1D8。");
    }

    private void TestScaleGuardReducesOnlyMeleePhysicalDamage()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(new GArray());
        BattleUnitState meleeSource = BuildAttacker("scale_guard_melee", new Vector2I(1, 0), "enemy", "melee");
        BattleUnitState holder = fixture.BuildDragonScaleUnit("scale_guard_holder");
        GDictionary meleeEvent;
        int meleeDamage = ResolveDamage(
            fixture,
            meleeSource,
            holder,
            "dragon_scale_guard_melee",
            "physical_slash",
            out meleeEvent
        );
        _test.Eq(meleeDamage, 8, "龙鳞护面应使近战 physical_slash 10 点伤害减为 8。");
        _test.Eq(DictInt(meleeEvent, "fixed_mitigation_total", -1), 2, "近战物理伤害固定减免应为 2。");
        _test.True(FixedSourcesInclude(meleeEvent, ScaleGuardBindingId), "龙鳞护面应进入固定减免来源。");

        BattleUnitState rangedSource = BuildAttacker("scale_guard_ranged", new Vector2I(3, 0), "enemy", "ranged");
        BattleUnitState rangedHolder = fixture.BuildDragonScaleUnit("scale_guard_ranged_holder");
        GDictionary rangedEvent;
        int rangedDamage = ResolveDamage(
            fixture,
            rangedSource,
            rangedHolder,
            "dragon_scale_guard_ranged",
            "physical_pierce",
            out rangedEvent
        );
        _test.Eq(rangedDamage, 10, "远程物理伤害不应触发龙鳞护面。");
        _test.Eq(DictInt(rangedEvent, "fixed_mitigation_total", -1), 0, "远程物理伤害固定减免应为 0。");

        BattleUnitState fireSource = BuildAttacker("scale_guard_fire", new Vector2I(1, 0), "enemy", "melee");
        BattleUnitState fireHolder = fixture.BuildDragonScaleUnit("scale_guard_fire_holder");
        GDictionary fireEvent;
        int fireDamage = ResolveDamage(
            fixture,
            fireSource,
            fireHolder,
            "dragon_scale_guard_fire",
            "fire",
            out fireEvent
        );
        _test.Eq(fireDamage, 5, "fire 伤害应只受五色龙鳞 half 抗性影响，不应再触发龙鳞护面固定减伤。");
        _test.Eq(DictInt(fireEvent, "fixed_mitigation_total", -1), 0, "非物理伤害固定减免应为 0。");
    }

    private void TestScaleRiftAppliesOnActualDamageWithConstitutionSaveAndSourceBoundAttackBonus()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildDragonScaleUnit("rift_holder");
        BattleUnitState target = BuildTarget("rift_target", new Vector2I(1, 0), "humanoid");
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "dragon_scale_rift",
            holder,
            target
        );
        fixture.Runtime.SetupStateForTests(state);

        ResolveScaleRift(fixture, holder, target, state, hpDamage: 0, saveRollOverride: 1);
        _test.False(target.HasStatusEffect(ScaleRiftStatusId), "没有实际 HP 伤害时不应施加破鳞裂痕。");

        ResolveScaleRift(fixture, holder, target, state, hpDamage: 7, saveRollOverride: 20);
        _test.False(target.HasStatusEffect(ScaleRiftStatusId), "体质豁免成功时不应施加破鳞裂痕。");

        ResolveScaleRift(fixture, holder, target, state, hpDamage: 7, saveRollOverride: 1);
        BattleStatusEffectState rift = target.GetStatusEffect(ScaleRiftStatusId);
        _test.True(rift != null, "实际伤害且体质豁免失败时应施加破鳞裂痕。");
        _test.Eq(rift?.duration ?? -1, 60, "破鳞裂痕持续时间必须是 60TU。");
        _test.Eq(rift?.stacks ?? 0, 1, "破鳞裂痕第一次失败应为 1 层。");
        _test.Eq(rift?.stack_behavior ?? new StringName(""), new StringName("add"), "破鳞裂痕应叠层。");
        _test.Eq(rift?.stack_limit ?? 0, 2, "破鳞裂痕最多 2 层。");
        _test.Eq(rift?.source_unit_id ?? new StringName(""), holder.unit_id, "破鳞裂痕应记录持有者来源。");
        _test.Eq(
            rift?.source_bound_incoming_attack_roll_bonus_per_stack ?? 0,
            1,
            "破鳞裂痕每层只给来源持有者攻击该目标 +1。"
        );
        _test.True(rift?.counts_as_debuff == true, "破鳞裂痕应标记为 debuff。");
        _test.Eq(rift?.attack_roll_penalty ?? 0, -1, "破鳞裂痕不应使用普通全局 attack_roll_penalty。");
        _test.Eq(
            BattleStatusSemanticTable.GetAttackRollPenalty(rift),
            0,
            "破鳞裂痕不应让目标自身承受全局命中惩罚。"
        );

        ResolveScaleRift(fixture, holder, target, state, hpDamage: 7, saveRollOverride: 1);
        ResolveScaleRift(fixture, holder, target, state, hpDamage: 7, saveRollOverride: 1);
        rift = target.GetStatusEffect(ScaleRiftStatusId);
        _test.Eq(rift?.stacks ?? 0, 2, "破鳞裂痕连续失败也不能超过 2 层。");
        _test.Eq(rift?.duration ?? -1, 60, "破鳞裂痕刷新后仍应保持 60TU。");

        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("dragon_scale_rift_attack");
        BattleAttackRollModifierBundle holderBundle =
            fixture.Runtime.GetAttackCheckPolicyService().BuildModifierBundle(
                fixture.Runtime.GetAttackCheckPolicyService().BuildSkillDefinitionAttackContext(
                    state,
                    holder,
                    target,
                    attackSkill,
                    "skill_attack_check",
                    "dragon_scale_rift_holder",
                    force_hit_no_crit: false
                )
            );
        _test.Eq(holderBundle.GetEffectiveModifierDelta(), 2, "持有者攻击 2 层破鳞裂痕目标应获得 +2。");

        BattleUnitState otherAttacker = BuildAttacker("rift_other_attacker", new Vector2I(0, 1), "ally", "melee");
        BattleAttackRollModifierBundle otherBundle =
            fixture.Runtime.GetAttackCheckPolicyService().BuildModifierBundle(
                fixture.Runtime.GetAttackCheckPolicyService().BuildSkillDefinitionAttackContext(
                    state,
                    otherAttacker,
                    target,
                    attackSkill,
                    "skill_attack_check",
                    "dragon_scale_rift_other",
                    force_hit_no_crit: false
                )
            );
        _test.Eq(otherBundle.GetEffectiveModifierDelta(), 0, "非来源单位攻击破鳞裂痕目标不应获得命中加值。");
    }

    private void TestEquipmentBonusDiceDoNotTriggerForNonWeaponDamageEffects()
    {
        using DragonScaleFixture fixture = DragonScaleFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildDragonScaleUnit("non_weapon_holder");
        BattleUnitState target = BuildTarget("non_weapon_target", new Vector2I(1, 0), "humanoid");
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "dragon_scale_non_weapon_damage",
            holder,
            target
        );
        fixture.Runtime.SetupStateForTests(state);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(
            fixture.Runtime.GetDamageResolver().ResolveEffects(
                holder,
                target,
                new[] { TestSkillDefinitionProjection.BuildEffect("damage", damageTag: "fire", power: 10) },
                DamageResolutionContext.Create(
                    criticalHit: false,
                    attackSuccess: true,
                    secondaryHitSuccess: false,
                    skillId: "fixture_spell_attack"
                )
            )
        );
        _test.Eq(DictInt(result, "damage", -1), 10, "非武器攻击伤害不应吃到龙牙锋刃等装备追加骰。");
    }

    private void AssertDragonToothPayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        EquipmentAbilityReactionDefinition reaction = binding?.Reactions?[0];
        EquipmentAbilityActionDefinition action = reaction?.Actions?[0];
        _test.True(reaction?.OnceScope == new StringName("turn"), $"{ownerLabel} 龙牙锋刃应每持有者回合限一次。");
        _test.True(action?.PayloadDefinition is AddDamageDiceActionPayloadDefinition, $"{ownerLabel} 龙牙锋刃应投影为 add_damage_dice。");
        AddDamageDiceActionPayloadDefinition payload = action?.PayloadDefinition as AddDamageDiceActionPayloadDefinition;
        _test.Eq(payload?.Dice?.Terms?[0]?.DiceCount ?? 0, 1, $"{ownerLabel} 龙牙锋刃应为 1D6。");
        _test.Eq(payload?.Dice?.Terms?[0]?.DiceSides ?? 0, 6, $"{ownerLabel} 龙牙锋刃应为 1D6。");
        _test.Eq(payload?.DamageType ?? new StringName(""), new StringName("physical_slash"), $"{ownerLabel} 龙牙锋刃应为 physical_slash。");
    }

    private void AssertScaleGuardPayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        _test.True(action?.PayloadDefinition is DamageReductionActionPayloadDefinition, $"{ownerLabel} 龙鳞护面应投影为 damage_reduction。");
        DamageReductionActionPayloadDefinition payload =
            action?.PayloadDefinition as DamageReductionActionPayloadDefinition;
        if (payload == null)
            return;
        _test.Eq(payload.TargetSelector, new StringName("damage_target"), $"{ownerLabel} 龙鳞护面应减免 damage_target。");
        _test.Eq(payload.Amount, 2, $"{ownerLabel} 龙鳞护面固定减伤应为 2。");
        _test.True(ContainsStringName(payload.DamageTags, "physical_slash"), $"{ownerLabel} 龙鳞护面应覆盖 physical_slash。");
        _test.True(ContainsStringName(payload.DamageTags, "physical_blunt"), $"{ownerLabel} 龙鳞护面应覆盖 physical_blunt。");
        _test.True(ContainsStringName(payload.DamageTags, "physical_pierce"), $"{ownerLabel} 龙鳞护面应覆盖 physical_pierce。");
    }

    private void AssertScaleRiftPayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        EquipmentAbilityReactionDefinition reaction = binding?.Reactions?[0];
        EquipmentAbilityActionDefinition action = reaction?.Actions?[0];
        _test.Eq(reaction?.Trigger ?? 0, EquipmentAbilityTriggerKind.OnDamageApplied, $"{ownerLabel} 破鳞裂痕应在实际伤害后触发。");
        _test.Eq(reaction?.Timing ?? 0, EquipmentAbilityTimingKind.AfterDamage, $"{ownerLabel} 破鳞裂痕应在 after_damage 触发。");
        _test.True(action?.PayloadDefinition is ApplyStatusActionPayloadDefinition, $"{ownerLabel} 破鳞裂痕应投影为 apply_status。");
        ApplyStatusActionPayloadDefinition payload =
            action?.PayloadDefinition as ApplyStatusActionPayloadDefinition;
        if (payload == null)
            return;
        _test.Eq(payload.TargetSelector, new StringName("attack_target"), $"{ownerLabel} 破鳞裂痕应写入攻击目标。");
        _test.Eq(payload.StatusId, ScaleRiftStatusId, $"{ownerLabel} 破鳞裂痕 status_id 应正确。");
        _test.Eq(payload.DurationTu, 60, $"{ownerLabel} 破鳞裂痕持续时间必须用 60TU。");
        _test.Eq(payload.DurationTurns, 0, $"{ownerLabel} 破鳞裂痕不应使用 duration_turns。");
        _test.Eq(payload.SaveDc, 15, $"{ownerLabel} 破鳞裂痕体质豁免 DC 应为 15。");
        _test.Eq(payload.SaveAbility, new StringName("constitution"), $"{ownerLabel} 破鳞裂痕应使用 constitution save ability。");
        _test.Eq(payload.SaveTag, new StringName("constitution"), $"{ownerLabel} 破鳞裂痕应使用 constitution save tag。");
        _test.True(payload.ApplyOnSaveFailure, $"{ownerLabel} 破鳞裂痕应在豁免失败时施加。");
        _test.Eq(payload.StackLimit, 2, $"{ownerLabel} 破鳞裂痕最多 2 层。");
        _test.Eq(payload.SourceBoundIncomingAttackRollBonusPerStack, 1, $"{ownerLabel} 破鳞裂痕每层给来源持有者 +1。");
    }

    private void AssertDragonBalancePayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        _test.True(action?.PayloadDefinition is AddDamageDiceActionPayloadDefinition, $"{ownerLabel} 屠龙制衡应投影为 add_damage_dice。");
        AddDamageDiceActionPayloadDefinition payload =
            action?.PayloadDefinition as AddDamageDiceActionPayloadDefinition;
        if (payload == null)
            return;
        _test.Eq(payload.Dice?.Terms?[0]?.DiceCount ?? 0, 1, $"{ownerLabel} 屠龙制衡应为 1D8。");
        _test.Eq(payload.Dice?.Terms?[0]?.DiceSides ?? 0, 8, $"{ownerLabel} 屠龙制衡应为 1D8。");
        _test.Eq(payload.DamageType, new StringName("physical_slash"), $"{ownerLabel} 屠龙制衡应为 physical_slash。");
    }

    private void ResolveScaleRift(
        DragonScaleFixture fixture,
        BattleUnitState holder,
        BattleUnitState target,
        BattleState state,
        int hpDamage,
        int saveRollOverride
    )
    {
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveDamageApplied(
            new BattleEquipmentAbilityDamageAppliedContext
            {
                SourceUnit = holder,
                TargetUnit = target,
                BattleState = state,
                HpDamage = hpDamage,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static int ResolveDamage(
        DragonScaleFixture fixture,
        BattleUnitState source,
        BattleUnitState target,
        StringName battleId,
        StringName damageTag,
        out GDictionary firstDamageEvent
    )
    {
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            source,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(
            fixture.Runtime.GetDamageResolver().ResolveEffects(
                source,
                target,
                new[] { TestSkillDefinitionProjection.BuildEffect("damage", damageTag: damageTag, power: 10) },
                DamageResolutionContext.Empty()
            )
        );
        firstDamageEvent = FirstDamageEvent(result);
        return DictInt(result, "damage", -1);
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, StringName tag)
    {
        BattleUnitState unit = BuildAttacker(unitId, coord, "enemy", "melee");
        unit.creature_type_tags.Add(tag);
        return unit;
    }

    private static BattleUnitState BuildAttacker(
        StringName unitId,
        Vector2I coord,
        StringName factionId,
        StringName weaponRangeType
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            is_alive = true,
            current_hp = 100,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 10);
        unit.attribute_snapshot.SetValue(AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.ConstitutionModifier), 0);
        unit.weapon_attack_range = weaponRangeType == "ranged" ? 6 : 1;
        unit.weapon_range_type = weaponRangeType;
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private void AssertTraitProjected(BattleUnitState unit, StringName traitId)
    {
        _test.True(unit.effective_trait_ids.Contains(traitId), $"unit 应投影 trait {traitId}。");
    }

    private void AssertMitigation(BattleUnitState unit, StringName damageTag, StringName mitigation)
    {
        _test.Eq(GetDamageMitigation(unit, damageTag), mitigation, $"{damageTag} 抗性应为 {mitigation}。");
    }

    private static StringName GetDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit?.damage_resistances == null)
            return "";
        if (unit.damage_resistances.TryGetValue(damageTag, out StringName value))
            return ProgressionDataUtils.to_string_name(value);
        if (unit.damage_resistances.TryGetValue(new StringName(damageTag.ToString()), out value))
            return ProgressionDataUtils.to_string_name(value);
        return "";
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

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private static bool FixedSourcesInclude(GDictionary damageEvent, StringName expected)
    {
        if (damageEvent == null || !damageEvent.ContainsKey("fixed_mitigation_sources"))
            return false;
        foreach (Variant value in damageEvent["fixed_mitigation_sources"].AsGodotArray())
        {
            if (
                value.VariantType == Variant.Type.Dictionary
                && value.AsGodotDictionary().ContainsKey("status_id")
                && value.AsGodotDictionary()["status_id"].AsString() == expected.ToString()
            )
            {
                return true;
            }
            if (value.AsString() == expected.ToString())
                return true;
        }
        return false;
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        if (result == null || !result.ContainsKey("damage_events"))
            return new GDictionary();
        GArray events = result["damage_events"].AsGodotArray();
        return events.Count > 0 ? events[0].AsGodotDictionary() : new GDictionary();
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private sealed class DragonScaleFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private DragonScaleFixture(
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

        internal static DragonScaleFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            PartyState partyState = BuildPartyState("hero");
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
            return new DragonScaleFixture(
                itemRegistry,
                progressionRegistry,
                partyState,
                runtime
            );
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildDragonScaleUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_dragon_scale_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
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

        private static PartyState BuildPartyState(StringName memberId)
        {
            PartyState partyState = new();
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = new UnitProgress(),
                equipment_state = new EquipmentState(),
            };
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            partyState.leader_member_id = memberId;
            return partyState;
        }
    }
}
