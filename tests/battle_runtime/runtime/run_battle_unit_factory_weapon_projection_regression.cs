using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_unit_factory_weapon_projection_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBattleUnitFactoryUsesTypedSkillLevelsAndResourceCosts();
            TestBattleUnitFactoryProjectsPlayerWeaponProfiles();
            TestBattleUnitFactoryProjectsEffectiveTraits();
            TestBattleUnitFactoryRefreshesEffectiveTraitsFromBattleLocalEquipment();
            TestBattleUnitFactoryProjectsPlayerEquipmentAbilitySources();
            TestBattleUnitFactoryRefreshUsesBattleLocalEquipmentView();
            RequestTestExit(_test.Finish("Battle unit factory weapon projection regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Battle unit factory weapon projection regression"));
        }
    }

    private void TestBattleUnitFactoryProjectsPlayerWeaponProfiles()
    {
        ItemDef bronzeSword = MakeWeapon(
            "bronze_sword",
            "shortsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Pierce),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        ItemDef ironGreatsword = MakeWeapon(
            "iron_greatsword",
            "greatsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            null,
            MakeWeaponDice(2, 6, 0),
            Array.Empty<StringName>()
        );
        ItemDef trainingLongsword = MakeWeapon(
            "training_longsword",
            "longsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 8, 0),
            MakeWeaponDice(1, 10, 0),
            new[] { new StringName("versatile") }
        );
        ItemDef trainingShield = MakeOffHandEquipment("training_shield");

        using BattleRuntimeScope runtimeScope = BuildRuntimeWithMemberItems(
            bronzeSword,
            ironGreatsword,
            trainingLongsword,
            trainingShield
        );
        PartyState partyState = runtimeScope.PartyState;
        PartyMemberState memberState = partyState.GetMemberState("hero");
        BattleUnitFactory factory = runtimeScope.Runtime._unit_factory;

        memberState.equipment_state = new EquipmentState();
        BattleUnitState unarmed = BuildSingleAllyUnit(factory, partyState, "unarmed");
        _test.Eq(
            unarmed?.weapon_profile_kind ?? (StringName)"",
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed),
            "unarmed player should project unarmed weapon kind."
        );
        _test.Eq(
            unarmed?.weapon_profile_type_id ?? (StringName)"",
            (StringName)"unarmed",
            "unarmed player should project unarmed profile type."
        );
        _test.Eq(
            DiceInt(unarmed?.weapon_one_handed_dice, "dice_sides"),
            4,
            "unarmed player should project 1D4 weapon dice."
        );
        _test.Eq(
            unarmed?.weapon_physical_damage_tag ?? (StringName)"",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Blunt),
            "unarmed player should project blunt damage tag."
        );
        _test.Eq(
            unarmed?.weapon_attack_range ?? 0,
            1,
            "unarmed player should project range 1."
        );

        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            bronzeSword.item_id,
            SlotIds("main_hand"),
            MakeEquipmentInstance(bronzeSword.item_id, "weapon_projection_bronze")
        );
        BattleUnitState oneHanded = BuildSingleAllyUnit(factory, partyState, "one-handed");
        _test.Eq(
            oneHanded?.weapon_profile_kind ?? (StringName)"",
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            "one-handed weapon should project equipped weapon kind."
        );
        _test.Eq(
            oneHanded?.weapon_item_id ?? (StringName)"",
            bronzeSword.item_id,
            "one-handed weapon should preserve item id."
        );
        _test.Eq(
            oneHanded?.weapon_profile_type_id ?? (StringName)"",
            (StringName)"shortsword",
            "one-handed weapon should preserve profile type."
        );
        _test.Eq(
            DiceInt(oneHanded?.weapon_one_handed_dice, "dice_sides"),
            6,
            "one-handed weapon should preserve 1D6 dice."
        );
        _test.True(
            oneHanded == null || oneHanded.weapon_two_handed_dice == null || oneHanded.weapon_two_handed_dice.IsEmpty(),
            "one-handed weapon should not project two-handed dice."
        );
        _test.Eq(
            oneHanded?.weapon_physical_damage_tag ?? (StringName)"",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Pierce),
            "one-handed weapon should preserve damage tag."
        );
        _test.True(
            oneHanded != null && !oneHanded.weapon_uses_two_hands,
            "one-handed weapon should not mark two-handed grip."
        );

        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            ironGreatsword.item_id,
            SlotIds("main_hand", "off_hand"),
            MakeEquipmentInstance(ironGreatsword.item_id, "weapon_projection_greatsword")
        );
        BattleUnitState twoHanded = BuildSingleAllyUnit(factory, partyState, "two-handed");
        _test.Eq(
            twoHanded?.weapon_profile_type_id ?? (StringName)"",
            (StringName)"greatsword",
            "two-handed weapon should preserve greatsword profile."
        );
        _test.True(
            twoHanded == null || twoHanded.weapon_one_handed_dice == null || twoHanded.weapon_one_handed_dice.IsEmpty(),
            "two-handed weapon should not project one-handed dice."
        );
        _test.Eq(
            DiceInt(twoHanded?.weapon_two_handed_dice, "dice_count"),
            2,
            "two-handed weapon should preserve 2D6 dice count."
        );
        _test.Eq(
            twoHanded?.weapon_physical_damage_tag ?? (StringName)"",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            "two-handed weapon should preserve slash damage tag."
        );
        _test.Eq(
            twoHanded?.weapon_current_grip ?? (StringName)"",
            BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
            "two-handed weapon should project two-handed grip."
        );
        _test.True(
            twoHanded != null && twoHanded.weapon_uses_two_hands,
            "two-handed weapon should mark two-handed usage."
        );

        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            trainingLongsword.item_id,
            SlotIds("main_hand"),
            MakeEquipmentInstance(trainingLongsword.item_id, "weapon_projection_longsword")
        );
        BattleUnitState versatile = BuildSingleAllyUnit(factory, partyState, "versatile");
        _test.True(
            versatile != null && versatile.weapon_is_versatile,
            "versatile weapon should preserve versatile flag."
        );
        _test.Eq(
            DiceInt(versatile?.weapon_one_handed_dice, "dice_sides"),
            8,
            "versatile weapon should preserve one-handed dice."
        );
        _test.Eq(
            DiceInt(versatile?.weapon_two_handed_dice, "dice_sides"),
            10,
            "versatile weapon should preserve two-handed dice."
        );
        _test.Eq(
            versatile?.weapon_current_grip ?? (StringName)"",
            BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
            "versatile weapon with empty off-hand should use two-handed grip."
        );
        _test.True(
            versatile != null && versatile.weapon_uses_two_hands,
            "versatile weapon with empty off-hand should mark two-handed usage."
        );
        versatile?.GetEquipmentView()
            .SetEquippedEntry(
                "off_hand",
                trainingShield.item_id,
                SlotIds("off_hand"),
                MakeEquipmentInstance(trainingShield.item_id, "weapon_projection_shield")
            );
        factory.RefreshWeaponProjection(versatile);
        _test.Eq(
            versatile?.weapon_current_grip ?? (StringName)"",
            BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
            "versatile weapon with occupied off-hand should fall back to one-handed grip."
        );
        _test.True(
            versatile != null && !versatile.weapon_uses_two_hands,
            "versatile weapon with occupied off-hand should clear two-handed usage."
        );
        _test.Eq(
            DiceInt(versatile?.weapon_two_handed_dice, "dice_sides"),
            10,
            "versatile refresh should preserve two-handed dice for later switching."
        );
    }

    private void TestBattleUnitFactoryRefreshUsesBattleLocalEquipmentView()
    {
        ItemDef bronzeSword = MakeWeapon(
            "bronze_sword",
            "shortsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Pierce),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        ItemDef ironGreatsword = MakeWeapon(
            "iron_greatsword",
            "greatsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            null,
            MakeWeaponDice(2, 6, 0),
            Array.Empty<StringName>()
        );

        using BattleRuntimeScope runtimeScope = BuildRuntimeWithMemberItems(
            bronzeSword,
            ironGreatsword
        );
        PartyState partyState = runtimeScope.PartyState;
        PartyMemberState memberState = partyState.GetMemberState("hero");
        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            bronzeSword.item_id,
            SlotIds("main_hand"),
            MakeEquipmentInstance(bronzeSword.item_id, "battle_start_sword")
        );

        BattleUnitFactory factory = runtimeScope.Runtime._unit_factory;
        BattleUnitState unit = BuildSingleAllyUnit(factory, partyState, "battle-local");
        _test.True(unit != null, "test setup should build one ally unit.");
        if (unit == null)
        {
            return;
        }

        _test.True(
            !ReferenceEquals(unit.GetEquipmentView(), memberState.equipment_state),
            "battle unit should duplicate equipment view instead of sharing member state."
        );
        _test.Eq(
            unit.weapon_item_id,
            bronzeSword.item_id,
            "initial weapon projection should come from battle-local equipment view."
        );
        unit.GetEquipmentView()
            .SetEquippedEntry(
                "main_hand",
                ironGreatsword.item_id,
                SlotIds("main_hand", "off_hand"),
                MakeEquipmentInstance(ironGreatsword.item_id, "battle_swap_greatsword")
            );

        factory.RefreshWeaponProjection(unit);
        _test.Eq(
            unit.weapon_item_id,
            ironGreatsword.item_id,
            "refresh_weapon_projection should continue reading battle-local equipment view."
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(
                memberState.equipment_state.GetEquippedItemId("main_hand")
            ),
            bronzeSword.item_id,
            "refresh_weapon_projection should not write back to member equipment state."
        );

        factory.RefreshBattleUnit(unit);
        _test.Eq(
            unit.weapon_item_id,
            ironGreatsword.item_id,
            "refresh_battle_unit should not rehydrate equipment view from member state."
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(
                unit.GetEquipmentView().GetEquippedInstanceId("main_hand")
            ),
            (StringName)"battle_swap_greatsword",
            "battle-local equipment view should keep the swapped instance id after refresh."
        );
    }

    private void TestBattleUnitFactoryProjectsEffectiveTraits()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithMemberTrait();
        BattleUnitState unit = BuildSingleAllyUnit(
            runtimeScope.Runtime._unit_factory,
            runtimeScope.PartyState,
            "effective-traits"
        );

        _test.Eq(
            unit.effective_trait_instances.Count,
            1,
            "BattleUnitFactory should project effective trait payload for player units."
        );
        BattleEffectiveTraitInstanceState first = unit.effective_trait_instances[0];
        _test.Eq(
            first.trait_id,
            new StringName("halfling_luck"),
            "effective trait payload should preserve trait id."
        );
        _test.Eq(
            first.effective_instance_key,
            new StringName("halfling_luck"),
            "unique character trait should collapse to trait id effective key."
        );
        _test.Eq(
            first.source_type,
            new StringName("character"),
            "effective trait payload should preserve source type."
        );
        _test.True(
            unit.effective_trait_ids.Contains("halfling_luck"),
            "effective_trait_ids should be derived from projected effective payload."
        );
    }

    private void TestBattleUnitFactoryRefreshesEffectiveTraitsFromBattleLocalEquipment()
    {
        ItemDef luckySword = MakeWeapon(
            "lucky_sword",
            "shortsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        luckySword.trait_ids = new GStringNameArray { "lucky_blade_trait" };

        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEquipmentTrait(luckySword);
        BattleUnitFactory factory = runtimeScope.Runtime._unit_factory;
        BattleUnitState unit = BuildSingleAllyUnit(factory, runtimeScope.PartyState, "equipment-traits");
        _test.Eq(
            unit.effective_trait_instances.Count,
            0,
            "unit without equipped trait item should start with empty effective trait payload."
        );

        unit.GetEquipmentView()
            .SetEquippedEntry(
                "main_hand",
                luckySword.item_id,
                SlotIds("main_hand"),
                MakeEquipmentInstance(luckySword.item_id, "battle_lucky_sword")
            );
        factory.RefreshEquipmentProjection(unit);

        _test.Eq(
            unit.effective_trait_instances.Count,
            1,
            "refresh_equipment_projection should rebuild effective trait payload from battle-local equipment view."
        );
        BattleEffectiveTraitInstanceState first = unit.effective_trait_instances[0];
        _test.Eq(
            first.trait_id,
            new StringName("lucky_blade_trait"),
            "refreshed equipment trait payload should preserve fixed trait id."
        );
        _test.Eq(
            first.source_type,
            new StringName("equipment_fixed"),
            "refreshed equipment trait payload should preserve equipment fixed source type."
        );
        _test.Eq(
            first.source_id,
            new StringName("battle_lucky_sword"),
            "refreshed equipment trait payload should use battle-local equipment instance id."
        );
        _test.True(
            unit.effective_trait_ids.Contains("lucky_blade_trait"),
            "refreshed effective_trait_ids should be derived from battle-local equipment payload."
        );
        _test.Eq(
            DictInt(unit.per_turn_charges, "lucky_blade_trait", -1),
            1,
            "refresh_equipment_projection should seed newly added per-turn trait charges."
        );

        unit.GetEquipmentView().ClearSlot("main_hand");
        factory.RefreshEquipmentProjection(unit);

        _test.Eq(
            unit.effective_trait_instances.Count,
            0,
            "refresh_equipment_projection should clear effective trait payload after trait equipment removal."
        );
        _test.True(
            !unit.per_turn_charges.ContainsKey("lucky_blade_trait"),
            "refresh_equipment_projection should clear removed trait per-turn charges."
        );
    }

    private void TestBattleUnitFactoryProjectsPlayerEquipmentAbilitySources()
    {
        ItemDef flameSword = MakeWeapon(
            "flame_sword",
            "shortsword",
            ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        flameSword.trait_ids = new GStringNameArray { "trait.weapon.flame" };
        flameSword.tags = new GStringNameArray { "blade" };

        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEquipmentAbilityBinding(flameSword);
        BattleUnitFactory factory = runtimeScope.Runtime._unit_factory;
        PartyMemberState memberState = runtimeScope.PartyState.GetMemberState("hero");
        memberState.equipment_state = new EquipmentState();
        memberState.equipment_state.SetEquippedEntry(
            "main_hand",
            flameSword.item_id,
            SlotIds("main_hand"),
            MakeEquipmentInstance(flameSword.item_id, "eq_flame_sword")
        );

        BattleUnitState unit = BuildSingleAllyUnit(factory, runtimeScope.PartyState, "equipment-ability");
        _test.Eq(
            unit.equipment_ability_sources.Count,
            1,
            "BattleUnitFactory should project matching player equipment ability source."
        );
        BattleEquipmentAbilitySourceState source = unit.equipment_ability_sources[0];
        _test.Eq(
            source.SourceKind,
            EquipmentAbilitySourceKind.PlayerPersistentEquipment,
            "player equipment ability source should be marked persistent."
        );
        _test.Eq(
            source.SourceEquipmentInstanceId,
            new StringName("eq_flame_sword"),
            "player equipment ability source should retain equipment instance id for writeback."
        );
        _test.Eq(
            source.EquipmentDefId,
            flameSword.item_id,
            "player equipment ability source should preserve source item id."
        );
        _test.True(
            source.AbilityIds.Contains("binding.weapon.flame"),
            "player equipment ability source should list matching binding id."
        );

        unit.GetEquipmentView().ClearSlot("main_hand");
        factory.RefreshEquipmentProjection(unit);
        _test.Eq(
            unit.equipment_ability_sources.Count,
            0,
            "refresh_equipment_projection should clear ability sources after equipment removal."
        );
    }

    private void TestBattleUnitFactoryUsesTypedSkillLevelsAndResourceCosts()
    {
    }

    private static BattleRuntimeScope BuildRuntimeWithMemberItems(params ItemDef[] itemDefs)
    {
        PartyState partyState = BuildPartyState("hero");
        var typedItemDefs = new Dictionary<StringName, ItemDef>();
        foreach (ItemDef itemDef in itemDefs)
        {
            if (itemDef != null)
            {
                typedItemDefs[itemDef.item_id] = itemDef;
            }
        }

        var progressionRegistry = new ProgressionContentRegistry();
        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            progressionRegistry.GetSkillDefinitionsTyped(),
            progressionRegistry.GetProfessionDefsTyped(),
            item_defs: typedItemDefs
        );

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            characterManagement,
            progressionRegistry.GetSkillDefinitionsTyped(),
            item_defs: typedItemDefs
        );
        return new BattleRuntimeScope(runtime, partyState);
    }

    private static BattleRuntimeScope BuildRuntimeWithMemberTrait()
    {
        PartyState partyState = BuildPartyState("hero");
        PartyMemberState member = partyState.GetMemberState("hero");
        member.trait_instances.Add(
            TraitInstanceState.Create(
                "hero_trait_001",
                "halfling_luck",
                TraitSourceKind.Character,
                "hero"
            )
        );

        var progressionRegistry = new ProgressionContentRegistry();
        var traitDefs = new Dictionary<StringName, TraitDef>
        {
            ["halfling_luck"] = new TraitDef
            {
                trait_id = "halfling_luck",
                display_name = "Halfling Luck",
                description = "Fixture trait.",
                allowed_source_kinds = new GStringNameArray { "character" },
                effect_type = "halfling_luck",
                trigger_type = "on_natural_one",
                stack_policy = "unique_by_trait",
                charge_scope = "per_turn",
                charge_reset_timing = "turn_start",
            },
        };

        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            progressionRegistry.GetSkillDefinitionsTyped(),
            progressionRegistry.GetProfessionDefsTyped(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            traitDefs,
            null,
            new ProgressionIdentityCatalogData()
        );

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            characterManagement,
            progressionRegistry.GetSkillDefinitionsTyped(),
            item_defs: new Dictionary<StringName, ItemDef>()
        );
        return new BattleRuntimeScope(runtime, partyState);
    }

    private static BattleRuntimeScope BuildRuntimeWithEquipmentTrait(ItemDef itemDef)
    {
        PartyState partyState = BuildPartyState("hero");
        var progressionRegistry = new ProgressionContentRegistry();
        var itemDefs = new Dictionary<StringName, ItemDef>();
        if (itemDef != null)
            itemDefs[itemDef.item_id] = itemDef;
        var traitDefs = new Dictionary<StringName, TraitDef>
        {
            ["lucky_blade_trait"] = new TraitDef
            {
                trait_id = "lucky_blade_trait",
                display_name = "Lucky Blade",
                description = "Fixture equipment trait.",
                allowed_source_kinds = new GStringNameArray { "equipment_fixed" },
                effect_type = "halfling_luck",
                trigger_type = "on_natural_one",
                stack_policy = "unique_by_trait",
                charge_scope = "per_turn",
                charge_reset_timing = "turn_start",
            },
        };

        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            progressionRegistry.GetSkillDefinitionsTyped(),
            progressionRegistry.GetProfessionDefsTyped(),
            new Dictionary<StringName, AchievementDef>(),
            itemDefs,
            new Dictionary<StringName, QuestDef>(),
            traitDefs,
            null,
            new ProgressionIdentityCatalogData()
        );

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            characterManagement,
            progressionRegistry.GetSkillDefinitionsTyped(),
            item_defs: itemDefs
        );
        return new BattleRuntimeScope(runtime, partyState);
    }

    private static BattleRuntimeScope BuildRuntimeWithEquipmentAbilityBinding(ItemDef itemDef)
    {
        PartyState partyState = BuildPartyState("hero");
        var progressionRegistry = new ProgressionContentRegistry();
        var itemDefs = new Dictionary<StringName, ItemDef>();
        if (itemDef != null)
            itemDefs[itemDef.item_id] = itemDef;
        var traitDefs = new Dictionary<StringName, TraitDef>
        {
            ["trait.weapon.flame"] = new TraitDef
            {
                trait_id = "trait.weapon.flame",
                display_name = "Flame Weapon",
                description = "Fixture equipment ability trait.",
                categories = new GStringNameArray { "weapon_feat" },
                allowed_source_kinds = new GStringNameArray { "equipment_fixed" },
                effect_type = "halfling_luck",
                trigger_type = "on_natural_one",
                stack_policy = "stack_by_instance",
                charge_scope = "none",
                charge_reset_timing = "none",
            },
        };
        var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
        {
            ["binding.weapon.flame"] = new EquipmentAbilityBindingDefinition
            {
                BindingId = "binding.weapon.flame",
                TraitId = "trait.weapon.flame",
                AllowedSourceKinds = new HashSet<StringName> { "equipment_fixed" },
                RequiredTraitCategories = new HashSet<StringName> { "weapon_feat" },
                RequiredItemTags = new HashSet<StringName> { "blade" },
                SupportedEquipmentTypeIds = new HashSet<StringName> { "weapon" },
            },
        };

        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            progressionRegistry.GetSkillDefinitionsTyped(),
            progressionRegistry.GetProfessionDefsTyped(),
            new Dictionary<StringName, AchievementDef>(),
            itemDefs,
            new Dictionary<StringName, QuestDef>(),
            traitDefs,
            null,
            new ProgressionIdentityCatalogData()
        );

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            characterManagement,
            progressionRegistry.GetSkillDefinitionsTyped(),
            item_defs: itemDefs,
            trait_defs: traitDefs,
            equipment_ability_bindings: bindings
        );
        return new BattleRuntimeScope(runtime, partyState);
    }

    private static BattleUnitState BuildSingleAllyUnit(
        BattleUnitFactory factory,
        PartyState partyState,
        string label
    )
    {
        var units = factory.BuildAllyUnits(partyState, new GDictionary());
        if (units.Count != 1)
        {
            throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
        }
        return units[0];
    }

    private static GDictionary ProjectProfessionDefs(
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs
    )
    {
        GDictionary result = new();
        if (professionDefs == null)
            return result;
        foreach ((StringName professionId, ProfessionDef professionDef) in professionDefs)
        {
            if (professionId == "" || professionDef == null)
                continue;
            result[professionId] = professionDef;
        }
        return result;
    }

    private static AttributeSnapshot BuildEnemyAttributeSnapshot(
        int hpMax,
        int mpMax,
        int staminaMax,
        int auraMax,
        int actionPoints
    )
    {
        var snapshot = new AttributeSnapshot();
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), mpMax);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), staminaMax);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), auraMax);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), actionPoints);
        return snapshot;
    }

    private static PartyState BuildPartyState(StringName memberId)
    {
        var partyState = new PartyState();
        var memberState = new PartyMemberState
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

    private static ItemDef MakeWeapon(
        StringName itemId,
        StringName weaponTypeId,
        StringName damageTag,
        int attackRange,
        WeaponDamageDiceDef oneHandedDice,
        WeaponDamageDiceDef twoHandedDice,
        IReadOnlyList<StringName> properties
    )
    {
        var itemDef = new ItemDef
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
            tags = new GStringNameArray { "melee" },
        };
        var profile = new WeaponProfileDef
        {
            weapon_type_id = weaponTypeId,
            training_group = "martial",
            range_type = "melee",
            family = "sword",
            damage_tag = damageTag,
            attack_range = attackRange,
            one_handed_dice = oneHandedDice,
            two_handed_dice = twoHandedDice,
            properties_mode = (int)WeaponProfileDef.PropertyMergeMode.REPLACE,
        };
        foreach (StringName property in properties ?? Array.Empty<StringName>())
        {
            if (property != "")
            {
                profile.properties.Add(property);
            }
        }
        itemDef.weapon_profile = profile;
        return itemDef;
    }

    private static ItemDef MakeOffHandEquipment(StringName itemId)
    {
        return new ItemDef
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            equipment_type_id = "shield",
            equipment_slot_ids = new Godot.Collections.Array<string> { "off_hand" },
            is_stackable = false,
            max_stack = 1,
        };
    }

    private static WeaponDamageDiceDef MakeWeaponDice(int count, int sides, int bonus)
    {
        return new WeaponDamageDiceDef
        {
            dice_count = count,
            dice_sides = sides,
            flat_bonus = bonus,
        };
    }

    private static EquipmentInstanceState MakeEquipmentInstance(StringName itemId, StringName instanceId)
    {
        return EquipmentInstanceState.CreateInstance(itemId, instanceId);
    }

    private static GStringNameArray SlotIds(params StringName[] values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value != "")
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static int DiceInt(WeaponDice source, string key)
    {
        if (source == null)
        {
            return 0;
        }
        return key switch
        {
            "dice_count" => source.dice_count,
            "dice_sides" => source.dice_sides,
            "flat_bonus" => source.flat_bonus,
            _ => 0,
        };
    }

    private static int DictInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        return source[key].AsInt32();
    }

    private static int DictInt(BattleStringNameIntMap source, StringName key, int fallback)
    {
        return source?.Get(key, fallback) ?? fallback;
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        internal BattleRuntimeScope(BattleRuntimeModule runtime, PartyState partyState)
        {
            Runtime = runtime;
            PartyState = partyState;
        }

        internal BattleRuntimeModule Runtime { get; }

        internal PartyState PartyState { get; }

        public void Dispose()
        {
            Runtime?.dispose();
        }
    }
}
