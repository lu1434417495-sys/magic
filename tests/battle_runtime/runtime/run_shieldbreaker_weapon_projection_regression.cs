using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_shieldbreaker_weapon_projection_regression : LifecycleTestSceneTree
{
    private static readonly StringName ShieldbreakerItemId =
        "weapon_unique_axe_shieldbreaker_098";
    private static readonly StringName GuardBreakerTraitId =
        "weapon.axe.shieldbreaker.guard_breaker";
    private static readonly StringName SiegeAxeTraitId =
        "weapon.axe.shieldbreaker.siege_axe";
    private static readonly StringName DefenseEnemyTraitId =
        "weapon.axe.shieldbreaker.defense_enemy";
    private static readonly StringName GuardBreakerBindingId =
        "binding.weapon.axe.shieldbreaker.guard_breaker";
    private static readonly StringName SiegeAxeBindingId =
        "binding.weapon.axe.shieldbreaker.siege_axe";
    private static readonly StringName DefenseEnemyBindingId =
        "binding.weapon.axe.shieldbreaker.defense_enemy";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        Run();
    }

    private void Run()
    {
        try
        {
            TestShieldbreakerProjectsRealContentOntoBattleUnit();
            RequestTestExit(_test.Finish("Shieldbreaker weapon projection regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Shieldbreaker weapon projection regression"));
        }
    }

    private void TestShieldbreakerProjectsRealContentOntoBattleUnit()
    {
        using ContentScope content = ContentScope.Load();
        _test.True(
            content.ItemDefs.ContainsKey(ShieldbreakerItemId),
            "真实物品内容应包含碎盾。"
        );
        _test.True(
            content.TraitDefs.ContainsKey(GuardBreakerTraitId),
            "真实 trait 内容应包含破盾者。"
        );
        _test.True(
            content.TraitDefs.ContainsKey(SiegeAxeTraitId),
            "真实 trait 内容应包含攻城之斧。"
        );
        _test.True(
            content.TraitDefs.ContainsKey(DefenseEnemyTraitId),
            "真实 trait 内容应包含防御之敌。"
        );
        _test.True(
            content.Bindings.ContainsKey(GuardBreakerBindingId),
            "真实装备能力内容应包含破盾者 binding。"
        );
        _test.True(
            content.Bindings.ContainsKey(SiegeAxeBindingId),
            "真实装备能力内容应包含攻城之斧 binding。"
        );
        _test.True(
            content.Bindings.ContainsKey(DefenseEnemyBindingId),
            "真实装备能力内容应包含防御之敌 binding。"
        );
        if (!content.ItemDefs.ContainsKey(ShieldbreakerItemId))
            return;

        ItemDef rawShieldbreaker = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_shieldbreaker.tres"
        );
        _test.True(rawShieldbreaker != null, "碎盾原始资源应能加载。");
        if (rawShieldbreaker != null)
        {
            _test.Eq(
                rawShieldbreaker.base_item_id,
                new StringName("weapon_type_greataxe_base"),
                "碎盾原始资源应声明继承 greataxe 模板。"
            );
        }

        ItemDefinition shieldbreaker = content.ItemDefs[ShieldbreakerItemId];
        AssertShieldbreakerItemContent(shieldbreaker);

        using BattleRuntimeScope runtimeScope = BuildRuntime(content);
        PartyState partyState = runtimeScope.PartyState;
        PartyMemberState member = partyState.GetMemberState("hero");
        BattleUnitFactory factory = runtimeScope.Runtime._unit_factory;

        member.equipment_state = new EquipmentState();
        BattleUnitState baseline = BuildSingleAllyUnit(factory, partyState, "baseline");
        int baselineAc = GetAttribute(baseline, AttributeService.ARMOR_CLASS);
        _test.False(
            baseline.HasEffectiveTrait(GuardBreakerTraitId),
            "装备前 unit 不应拥有破盾者 trait。"
        );
        _test.False(
            baseline.HasEffectiveTrait(SiegeAxeTraitId),
            "装备前 unit 不应拥有攻城之斧 trait。"
        );
        _test.False(
            baseline.HasEffectiveTrait(DefenseEnemyTraitId),
            "装备前 unit 不应拥有防御之敌 trait。"
        );
        _test.Eq(
            baseline.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "装备前 unit 不应投影碎盾装备能力源。"
        );

        member.equipment_state = new EquipmentState();
        member.equipment_state.SetEquippedEntry(
            "main_hand",
            ShieldbreakerItemId,
            new GStringNameArray { "main_hand", "off_hand" },
            EquipmentInstanceState.CreateInstance(
                ShieldbreakerItemId,
                "eq_shieldbreaker_projection"
            )
        );
        BattleUnitState equipped = BuildSingleAllyUnit(factory, partyState, "shieldbreaker");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;

        _test.Eq(
            equippedWeapon.ItemId,
            ShieldbreakerItemId,
            "碎盾装备后 unit 应保留真实 item_id。"
        );
        _test.Eq(
            equippedWeapon.ProfileTypeId,
            new StringName("greataxe"),
            "碎盾应投影为 greataxe。"
        );
        _test.Eq(
            equippedWeapon.PhysicalDamageTag,
            new StringName("physical_slash"),
            "碎盾应投影为 physical_slash。"
        );
        _test.Eq(equippedWeapon.AttackRange, 1, "碎盾攻击距离应为 1。");
        _test.True(equippedWeapon.UsesTwoHands, "碎盾应占用双手。");
        _test.Eq(
            equippedWeapon.CurrentGrip,
            BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
            "碎盾当前握持应为 two_handed。"
        );
        _test.Eq(
            equippedWeapon.TwoHandedDice.DiceCount,
            1,
            "碎盾双手骰数量应为 1。"
        );
        _test.Eq(
            equippedWeapon.TwoHandedDice.DiceSides,
            12,
            "碎盾双手骰面应为 12。"
        );
        _test.Eq(
            equippedWeapon.TwoHandedDice.FlatBonus,
            2,
            "碎盾双手骰固定加值应为 +2。"
        );
        _test.Eq(
            GetAttribute(equipped, AttributeService.ARMOR_CLASS),
            baselineAc + 1,
            "防御之敌应通过装备属性让 unit AC 比装备前 +1。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(
                equipped.GetEquipmentView().GetEquippedItemId("off_hand")
            ),
            ShieldbreakerItemId,
            "碎盾应占用 off_hand，阻止盾牌共存。"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            GuardBreakerTraitId,
            GuardBreakerBindingId,
            "eq_shieldbreaker_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            SiegeAxeTraitId,
            SiegeAxeBindingId,
            "eq_shieldbreaker_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DefenseEnemyTraitId,
            DefenseEnemyBindingId,
            "eq_shieldbreaker_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        factory.RefreshBattleUnit(equipped);
        AssertShieldbreakerClearedFromUnit(equipped, baseline, baselineAc);
    }

    private void AssertShieldbreakerClearedFromUnit(
        BattleUnitState unit,
        BattleUnitState baseline,
        int baselineAc
    )
    {
        BattleWeaponProjectionValues unitWeapon =
            unit.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(
            ProgressionDataUtils.to_string_name(
                unit.GetEquipmentView().GetEquippedItemId("main_hand")
            ),
            new StringName(""),
            "移除碎盾后 main_hand 应清空。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(
                unit.GetEquipmentView().GetEquippedItemId("off_hand")
            ),
            new StringName(""),
            "移除碎盾后 off_hand 占用应解除。"
        );
        _test.Eq(unitWeapon.ItemId, new StringName(""), "移除碎盾后 weapon_item_id 应清空。");
        _test.Eq(
            unitWeapon.ProfileTypeId,
            baselineWeapon.ProfileTypeId,
            "移除碎盾后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            unitWeapon.PhysicalDamageTag,
            baselineWeapon.PhysicalDamageTag,
            "移除碎盾后 weapon_physical_damage_tag 应回到装备前状态。"
        );
        _test.Eq(
            unitWeapon.AttackRange,
            baselineWeapon.AttackRange,
            "移除碎盾后武器攻击距离应回到装备前状态。"
        );
        _test.Eq(
            unitWeapon.UsesTwoHands,
            baselineWeapon.UsesTwoHands,
            "移除碎盾后双手武器标记应回到装备前状态。"
        );
        _test.Eq(
            unitWeapon.CurrentGrip,
            baselineWeapon.CurrentGrip,
            "移除碎盾后当前握持应回到装备前状态。"
        );
        _test.Eq(
            unitWeapon.TwoHandedDice.DiceCount,
            baselineWeapon.TwoHandedDice.DiceCount,
            "移除碎盾后双手武器骰数量应回到装备前状态。"
        );
        _test.Eq(
            unit.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除碎盾后装备 trait 实例应回到装备前状态。"
        );
        _test.Eq(
            GetAttribute(unit, AttributeService.ARMOR_CLASS),
            baselineAc,
            "移除碎盾后 AC 应回到装备前。"
        );
        _test.False(
            unit.HasEffectiveTrait(GuardBreakerTraitId),
            "移除碎盾后破盾者 trait 应消失。"
        );
        _test.False(
            unit.HasEffectiveTrait(SiegeAxeTraitId),
            "移除碎盾后攻城之斧 trait 应消失。"
        );
        _test.False(
            unit.HasEffectiveTrait(DefenseEnemyTraitId),
            "移除碎盾后防御之敌 trait 应消失。"
        );
        _test.Eq(
            unit.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除碎盾后装备能力源应清空。"
        );
    }

    private void AssertShieldbreakerItemContent(ItemDefinition item)
    {
        _test.Eq(item.DisplayName, "碎盾", "碎盾 display_name 应来自真实内容。");
        _test.Eq(item.BaseItemId, new StringName(""), "碎盾 resolved item 应已完成模板合并。");
        _test.True(item.IsWeapon(), "碎盾 resolved item 应继承 weapon equipment 类型。");
        _test.Eq(
            item.GetWeaponPhysicalDamageTag(),
            new StringName("physical_slash"),
            "碎盾 resolved item 应继承 greataxe 伤害类型。"
        );
        _test.Eq(item.GetWeaponAttackRange(), 1, "碎盾 resolved item 应继承 greataxe 攻击距离。");
        _test.True(item.GetTagsTyped().Contains("heavy"), "碎盾应带 heavy tag。");
        _test.True(item.GetTraitIdsTyped().Contains(GuardBreakerTraitId), "碎盾 item 应挂破盾者 trait。");
        _test.True(item.GetTraitIdsTyped().Contains(SiegeAxeTraitId), "碎盾 item 应挂攻城之斧 trait。");
        _test.True(item.GetTraitIdsTyped().Contains(DefenseEnemyTraitId), "碎盾 item 应挂防御之敌 trait。");
        _test.Eq(
            item.GetAttributeModifiersTyped().Count,
            1,
            "碎盾应只有防御之敌的 +1 AC 装备属性修正。"
        );
        if (item.GetAttributeModifiersTyped().Count > 0)
        {
            AttributeModifierDefinition modifier = item.GetAttributeModifiersTyped()[0];
            _test.Eq(
                modifier.AttributeId,
                AttributeContentRules.ArmorAcBonus,
                "碎盾 +1 AC 应使用 armor_ac_bonus 组件。"
            );
            _test.Eq(modifier.Value, 1, "碎盾 armor_ac_bonus 应为 +1。");
        }
    }

    private void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        _test.True(unit.HasEffectiveTrait(traitId), $"unit 应拥有 trait {traitId}。");
        BattleEquipmentAbilitySourceReadView source = FindSource(unit, bindingId);
        _test.True(source != null, $"unit 应投影装备能力源 {bindingId}。");
        if (source == null)
            return;
        _test.Eq(
            source.SourceKind,
            EquipmentAbilitySourceKind.PlayerPersistentEquipment,
            $"{bindingId} 应来自玩家持久装备。"
        );
        _test.Eq(
            source.SourceEquipmentInstanceId,
            expectedInstanceId,
            $"{bindingId} 应保留装备实例 id。"
        );
        _test.Eq(source.EquipmentDefId, ShieldbreakerItemId, $"{bindingId} 应保留碎盾 item id。");
    }

    private static BattleEquipmentAbilitySourceReadView FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source in
            unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildSingleAllyUnit(
        BattleUnitFactory factory,
        PartyState partyState,
        string label
    )
    {
        IReadOnlyList<BattleUnitState> units = factory.BuildAllyUnits(partyState, new GDictionary());
        if (units.Count != 1)
        {
            throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
        }
        return units[0];
    }

    private static BattleRuntimeScope BuildRuntime(ContentScope content)
    {
        PartyState partyState = BuildPartyState("hero");
        CharacterManagementModule characterManagement = new();
        characterManagement.setup(
            partyState,
            content.SkillDefs,
            content.ProfessionDefs,
            content.AchievementDefs,
            content.ItemDefs,
            content.QuestDefs,
            content.TraitDefs,
            null,
            new ProgressionIdentityCatalogData()
        );

        BattleRuntimeModule runtime = new();
        runtime.setup(
            characterManagement,
            content.SkillDefs,
            item_defs: content.ItemDefs,
            trait_defs: content.TraitDefs,
            equipment_ability_bindings: content.Bindings
        );
        return new BattleRuntimeScope(runtime, partyState, characterManagement);
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

    private static int GetAttribute(BattleUnitState unit, StringName attributeId)
    {
        return unit?.attribute_snapshot.GetValue(attributeId) ?? 0;
    }

    private sealed class ContentScope : IDisposable
    {
        private ContentScope(ContentSnapshot snapshot)
        {
            ItemDefs = snapshot.Items;
            SkillDefs = snapshot.Skills;
            ProfessionDefs = snapshot.Professions;
            AchievementDefs = snapshot.Achievements;
            QuestDefs = snapshot.Quests;
            TraitDefs = snapshot.Traits;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, ProfessionDefinition> ProfessionDefs { get; }
        internal IReadOnlyDictionary<StringName, AchievementDefinition> AchievementDefs { get; }
        internal IReadOnlyDictionary<StringName, QuestDefinition> QuestDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static ContentScope Load()
        {
            return new ContentScope(GameSessionTestFactory.GetProcessSnapshot());
        }

        public void Dispose() { }
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;

        internal BattleRuntimeScope(
            BattleRuntimeModule runtime,
            PartyState partyState,
            CharacterManagementModule characterManagement
        )
        {
            Runtime = runtime;
            PartyState = partyState;
            _characterManagement = characterManagement;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal PartyState PartyState { get; }

        public void Dispose()
        {
            Runtime?.dispose();
            _characterManagement?.Dispose();
        }
    }
}
