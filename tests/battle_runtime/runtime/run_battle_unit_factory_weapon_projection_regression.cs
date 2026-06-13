using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_unit_factory_weapon_projection_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBattleUnitFactoryUsesTypedSkillLevelsAndResourceCosts();
            TestBattleUnitFactoryUsesTypedDefaultBuildDefaults();
            TestBattleUnitFactoryProjectsPlayerWeaponProfiles();
            TestBattleUnitFactoryRefreshUsesBattleLocalEquipmentView();
            TestBattleUnitFactoryEnemyResourceSyncUsesTypedCosts();
            TestBattleUnitFactoryEnemyResourceSyncHandlesMissingAttributeSnapshot();
            Quit(_test.Finish("Battle unit factory weapon projection regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(1);
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
            DictInt(unarmed?.weapon_one_handed_dice, "dice_sides"),
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
            DictInt(oneHanded?.weapon_one_handed_dice, "dice_sides"),
            6,
            "one-handed weapon should preserve 1D6 dice."
        );
        _test.True(
            oneHanded?.weapon_two_handed_dice == null || oneHanded.weapon_two_handed_dice.Count == 0,
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
            twoHanded?.weapon_one_handed_dice == null || twoHanded.weapon_one_handed_dice.Count == 0,
            "two-handed weapon should not project one-handed dice."
        );
        _test.Eq(
            DictInt(twoHanded?.weapon_two_handed_dice, "dice_count"),
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
            DictInt(versatile?.weapon_one_handed_dice, "dice_sides"),
            8,
            "versatile weapon should preserve one-handed dice."
        );
        _test.Eq(
            DictInt(versatile?.weapon_two_handed_dice, "dice_sides"),
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
            DictInt(versatile?.weapon_two_handed_dice, "dice_sides"),
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

    private void TestBattleUnitFactoryUsesTypedSkillLevelsAndResourceCosts()
    {
    }

    private void TestBattleUnitFactoryUsesTypedDefaultBuildDefaults()
    {
        Type factoryType = typeof(BattleUnitFactory);
        _test.True(
            factoryType.GetNestedType("AllyUnitDefaults", BindingFlags.NonPublic) != null
                && factoryType.GetNestedType("EnemyUnitDefaults", BindingFlags.NonPublic) != null
                && factoryType.GetNestedType("EnemyWeaponDefaults", BindingFlags.NonPublic) != null,
            "BattleUnitFactory 应先把 default_* payload 解码成 typed defaults，再驱动 ally/enemy build。"
        );
    }

    private void TestBattleUnitFactoryEnemyResourceSyncUsesTypedCosts()
    {
        var progressionRegistry = new ProgressionContentRegistry();
        using BattleRuntimeModule runtime = new();
        runtime.setup(
            null,
            progressionRegistry.GetSkillDefsTyped()
        );

        BattleUnitFactory factory = new();
        factory.Setup(runtime);

        MethodInfo syncResources = typeof(BattleUnitFactory).GetMethod(
            "_sync_enemy_unlocked_resources",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(syncResources != null, "应能反射到 BattleUnitFactory._sync_enemy_unlocked_resources。");
        if (syncResources == null)
        {
            factory.DisposeRuntime();
            return;
        }

        BattleUnitState caster = new()
        {
            attribute_snapshot = BuildEnemyAttributeSnapshot(24, 0, 8, 0, 1),
            current_hp = 24,
            current_mp = 0,
            current_stamina = 8,
            current_aura = 0,
        };
        caster.known_active_skill_ids.Add("mage_glacial_prison");
        caster.known_skill_level_map["mage_glacial_prison"] = 3;

        syncResources.Invoke(factory, new object[] { caster });

        _test.True(
            caster.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "带 MP 消耗技能的敌方单位应通过 typed cost 同步解锁 MP。"
        );

        factory.DisposeRuntime();
    }

    private void TestBattleUnitFactoryEnemyResourceSyncHandlesMissingAttributeSnapshot()
    {
        BattleUnitFactory factory = new();
        MethodInfo syncResources = typeof(BattleUnitFactory).GetMethod(
            "_sync_enemy_unlocked_resources",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(syncResources != null, "应能反射到 BattleUnitFactory._sync_enemy_unlocked_resources。");
        if (syncResources == null)
        {
            return;
        }

        BattleUnitState unit = new()
        {
            attribute_snapshot = null,
            current_mp = 3,
            current_aura = 2,
        };

        syncResources.Invoke(factory, new object[] { unit });
        _test.True(
            unit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Hp)),
            "缺属性快照时仍应保留默认 HP 资源。"
        );
        _test.True(
            unit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina)),
            "缺属性快照时仍应保留默认 stamina 资源。"
        );
        _test.True(
            unit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "缺属性快照但 current_mp 大于 0 时应解锁 MP。"
        );
        _test.True(
            unit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "缺属性快照但 current_aura 大于 0 时应解锁 aura。"
        );

        BattleUnitState emptyUnit = new() { attribute_snapshot = null };
        syncResources.Invoke(factory, new object[] { emptyUnit });
        _test.False(
            emptyUnit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
            "缺属性快照且 current_mp 为 0 时不应解锁 MP。"
        );
        _test.False(
            emptyUnit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura)),
            "缺属性快照且 current_aura 为 0 时不应解锁 aura。"
        );
    }

    private static BattleRuntimeScope BuildRuntimeWithMemberItems(params ItemDef[] itemDefs)
    {
        PartyState partyState = BuildPartyState("hero");
        GDictionary indexedItemDefs = new();
        var typedItemDefs = new Dictionary<StringName, ItemDef>();
        foreach (ItemDef itemDef in itemDefs)
        {
            if (itemDef != null)
            {
                indexedItemDefs[itemDef.item_id] = itemDef;
                typedItemDefs[itemDef.item_id] = itemDef;
            }
        }

        var progressionRegistry = new ProgressionContentRegistry();
        var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            ProjectSkillDefs(progressionRegistry.GetSkillDefsTyped()),
            ProjectProfessionDefs(progressionRegistry.GetProfessionDefsTyped()),
            new GDictionary(),
            indexedItemDefs
        );

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            characterManagement,
            progressionRegistry.GetSkillDefsTyped(),
            item_defs: typedItemDefs
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

    private static GDictionary ProjectSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        GDictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillId == "" || skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
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

    private static int DictInt(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;
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
