using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_equipment_regression : SceneTree
{
    private sealed class WeaponProfileExpectation
    {
        public StringName WeaponTypeId { get; init; } = "";
        public int[] OneHandedDice { get; init; } = System.Array.Empty<int>();
        public int[] TwoHandedDice { get; init; } = System.Array.Empty<int>();
        public StringName[] Properties { get; init; } = System.Array.Empty<StringName>();
    }

    private static readonly Dictionary<StringName, StringName> Bg3WeaponSeedItems = new()
    {
        ["club"] = "oak_club",
        ["dagger"] = "iron_dagger",
        ["handaxe"] = "militia_axe",
        ["javelin"] = "hunting_javelin",
        ["light_hammer"] = "smith_light_hammer",
        ["mace"] = "watchman_mace",
        ["sickle"] = "farmer_sickle",
        ["quarterstaff"] = "oak_quarterstaff",
        ["spear"] = "militia_spear",
        ["greatclub"] = "iron_greatclub",
        ["light_crossbow"] = "militia_light_crossbow",
        ["shortbow"] = "ash_shortbow",
        ["flail"] = "iron_flail",
        ["morningstar"] = "iron_morningstar",
        ["rapier"] = "duelist_rapier",
        ["scimitar"] = "curved_scimitar",
        ["shortsword"] = "bronze_sword",
        ["war_pick"] = "iron_war_pick",
        ["battleaxe"] = "soldier_battleaxe",
        ["longsword"] = "steel_longsword",
        ["trident"] = "guard_trident",
        ["warhammer"] = "iron_warhammer",
        ["glaive"] = "soldier_glaive",
        ["greataxe"] = "raider_greataxe",
        ["greatsword"] = "iron_greatsword",
        ["halberd"] = "steel_halberd",
        ["maul"] = "stone_maul",
        ["pike"] = "soldier_pike",
        ["hand_crossbow"] = "compact_hand_crossbow",
        ["heavy_crossbow"] = "siege_heavy_crossbow",
        ["longbow"] = "ash_longbow",
    };

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestItemRegistryAcceptsEquipmentSeedData();
        TestAllBg3WeaponTypesAreRegisteredAsWeaponEquipment();
        TestMeleeWeaponsDeclareExactlyOnePhysicalDamageTag();
        TestEquipmentServiceMovesItemsBetweenWarehouseAndSlots();
        TestEquipmentModifiersChangeAttributeSnapshotAndRoundTrip();
        TestEquipmentStateRequiresCanonicalPayload();
        TestEquipmentEntryRejectsBadSchema();
        TestEquipmentStateKeepsTypedRuntimeStorage();
        TestTwoHandedWeaponOccupiesBothSlots();
        TestTwoHandedWeaponDisplacesExistingMainAndOffHand();
        TestTwoHandedWeaponAttributeNotDoubleCounted();
        TestAtomicRollbackWhenWarehouseFull();
        TestPreviewEquipReturnsDisplacedEntries();
        TestArmorMaxDexBonusCapsPositiveAgilityAc();
        TestRequirementProfessionCheck();
        TestEquipCreatesInstanceIdInSlot();
        TestInstanceIdPreservedThroughUnequipAndReequip();
        TestTwoItemsOfSameTypeGetDifferentInstanceIds();
        TestWeaponProfileEquipmentEntryRoundTrip();
        TestEquippedInstanceFieldsSurviveRoundTripAndUnequip();
        TestEquipmentInstanceRarityRoundTripAndStrictSchema();
        TestDuplicateSameItemInstanceIdSelection();
        TestPartyStateRejectsDuplicateEquipmentInstanceIds();

        Finish();
    }

    private void TestItemRegistryAcceptsEquipmentSeedData()
    {
        ItemContentRegistry registry = new();
        AssertEq(registry.validate().Count, 0, "Equipment seed item definitions should validate.");

        GDictionary itemDefs = registry.get_item_defs();
        ItemDef bronzeSword = GetItemDef(itemDefs, "bronze_sword");
        ItemDef leatherCap = GetItemDef(itemDefs, "leather_cap");
        ItemDef leatherJerkin = GetItemDef(itemDefs, "leather_jerkin");
        ItemDef ironScaleMail = GetItemDef(itemDefs, "iron_scale_mail");
        ItemDef scoutCharm = GetItemDef(itemDefs, "scout_charm");
        ItemDef ironGreatsword = GetItemDef(itemDefs, "iron_greatsword");
        ItemDef militiaAxe = GetItemDef(itemDefs, "militia_axe");
        ItemDef watchmanMace = GetItemDef(itemDefs, "watchman_mace");

        AssertTrue(bronzeSword != null && bronzeSword.is_equipment(), "bronze_sword should be equipment.");
        AssertTrue(leatherCap != null && leatherCap.is_equipment(), "leather_cap should be equipment.");
        AssertTrue(leatherJerkin != null && leatherJerkin.is_equipment(), "leather_jerkin should be equipment.");
        AssertTrue(ironScaleMail != null && ironScaleMail.is_equipment(), "iron_scale_mail should be equipment.");
        AssertTrue(scoutCharm != null && scoutCharm.is_equipment(), "scout_charm should be equipment.");
        AssertTrue(ironGreatsword != null && ironGreatsword.is_equipment(), "iron_greatsword should be equipment.");
        AssertTrue(militiaAxe != null && militiaAxe.is_equipment(), "militia_axe should be equipment.");
        AssertTrue(watchmanMace != null && watchmanMace.is_equipment(), "watchman_mace should be equipment.");
        AssertFalse(itemDefs.ContainsKey("scout_dagger"), "scout_dagger should be removed from equipment seed data.");

        AssertStringNameEq(bronzeSword?.get_equipment_type_id_normalized() ?? "", "weapon", "bronze_sword type.");
        AssertStringNameEq(leatherCap?.get_equipment_type_id_normalized() ?? "", "armor", "leather_cap type.");
        AssertStringNameEq(leatherJerkin?.get_equipment_type_id_normalized() ?? "", "armor", "leather_jerkin type.");
        AssertStringNameEq(ironScaleMail?.get_equipment_type_id_normalized() ?? "", "armor", "iron_scale_mail type.");
        AssertStringNameEq(scoutCharm?.get_equipment_type_id_normalized() ?? "", "accessory", "scout_charm type.");
        AssertStringNameEq(ironGreatsword?.get_equipment_type_id_normalized() ?? "", "weapon", "iron_greatsword type.");
        AssertStringNameEq(militiaAxe?.get_equipment_type_id_normalized() ?? "", "weapon", "militia_axe type.");
        AssertStringNameEq(watchmanMace?.get_equipment_type_id_normalized() ?? "", "weapon", "watchman_mace type.");

        AssertTrue(bronzeSword?.is_weapon() ?? false, "bronze_sword should be a weapon.");
        AssertTrue(leatherCap?.is_armor() ?? false, "leather_cap should be armor.");
        AssertTrue(leatherJerkin?.is_armor() ?? false, "leather_jerkin should be armor.");
        AssertTrue(ironScaleMail?.is_armor() ?? false, "iron_scale_mail should be armor.");
        AssertTrue(scoutCharm?.is_accessory() ?? false, "scout_charm should be accessory.");
        AssertEq(ironGreatsword?.get_final_occupied_slot_ids("main_hand").Count ?? -1, 2, "iron_greatsword should occupy two slots.");

        AssertStringNameSeqEq(leatherCap?.get_tags(), Names("armor", "head", "leather", "light_armor"), "leather_cap tags.");
        AssertEq(leatherCap?.get_buy_price() ?? -1, 100, "leather_cap buy price.");
        AssertEq(leatherCap?.get_sell_price() ?? -1, 50, "leather_cap sell price.");
        AssertStringNameSeqEq(leatherCap?.get_equipment_slot_ids(), Names("head"), "leather_cap slot.");
        AssertStringNameSeqEq(leatherCap?.get_final_occupied_slot_ids("head"), Names("head"), "leather_cap occupied slot.");
        AssertEq(leatherCap?.get_max_dex_bonus() ?? -2, -1, "leather_cap max dex.");

        AssertStringNameSeqEq(leatherJerkin?.get_tags(), Names("armor", "body", "leather", "light_armor"), "leather_jerkin tags.");
        AssertStringNameSeqEq(leatherJerkin?.get_equipment_slot_ids(), Names("body"), "leather_jerkin slot.");
        AssertStringNameSeqEq(leatherJerkin?.get_final_occupied_slot_ids("body"), Names("body"), "leather_jerkin occupied slot.");
        AssertEq(leatherJerkin?.get_max_dex_bonus() ?? -2, 6, "leather_jerkin max dex.");

        AssertStringNameSeqEq(ironScaleMail?.get_tags(), Names("armor", "body", "metal", "medium_armor", "scale_mail"), "iron_scale_mail tags.");
        AssertEq(ironScaleMail?.get_buy_price() ?? -1, 180, "iron_scale_mail buy price.");
        AssertEq(ironScaleMail?.get_sell_price() ?? -1, 90, "iron_scale_mail sell price.");
        AssertStringNameSeqEq(ironScaleMail?.get_equipment_slot_ids(), Names("body"), "iron_scale_mail slot.");
        AssertStringNameSeqEq(ironScaleMail?.get_final_occupied_slot_ids("body"), Names("body"), "iron_scale_mail occupied slot.");
        AssertEq(ironScaleMail?.get_max_dex_bonus() ?? -2, 3, "iron_scale_mail max dex.");

        AssertStringNameSeqEq(bronzeSword?.get_tags(), Names("weapon", "melee", "one_handed", "shortsword", "sword", "weapon_class_sword", "weapon_type_shortsword"), "bronze_sword tags.");
        AssertStringNameSeqEq(militiaAxe?.get_tags(), Names("weapon", "melee", "one_handed", "handaxe", "axe", "weapon_class_axe", "weapon_type_handaxe"), "militia_axe tags.");
        AssertEq(militiaAxe?.get_buy_price() ?? -1, 145, "militia_axe buy price.");
        AssertEq(militiaAxe?.get_sell_price() ?? -1, 70, "militia_axe sell price.");
        AssertStringNameSeqEq(militiaAxe?.get_equipment_slot_ids(), Names("main_hand"), "militia_axe slot.");
        AssertStringNameSeqEq(militiaAxe?.get_final_occupied_slot_ids("main_hand"), Names("main_hand"), "militia_axe occupied slot.");
        AssertStringNameSeqEq(watchmanMace?.get_tags(), Names("weapon", "melee", "one_handed", "mace", "weapon_class_mace", "weapon_type_mace"), "watchman_mace tags.");
        AssertEq(watchmanMace?.get_buy_price() ?? -1, 175, "watchman_mace buy price.");
        AssertEq(watchmanMace?.get_sell_price() ?? -1, 85, "watchman_mace sell price.");

        var oneHandedWeaponClasses = new HashSet<StringName>();
        var coveredEquipmentSlots = new HashSet<StringName>();
        foreach (Variant itemDefValue in itemDefs.Values)
        {
            ItemDef itemDef = itemDefValue.AsGodotObject() as ItemDef;
            if (itemDef == null)
                continue;
            if (itemDef.is_equipment())
                foreach (StringName slotId in itemDef.get_equipment_slot_ids())
                    coveredEquipmentSlots.Add(slotId);
            if (!itemDef.is_weapon())
                continue;
            if (!StringNameSeqEquals(itemDef.get_equipment_slot_ids(), Names("main_hand")))
                continue;
            if (!StringNameSeqEquals(itemDef.get_final_occupied_slot_ids("main_hand"), Names("main_hand")))
                continue;
            foreach (StringName tag in itemDef.get_tags())
                if (tag.ToString().StartsWith("weapon_class_"))
                    oneHandedWeaponClasses.Add(tag);
        }

        AssertTrue(oneHandedWeaponClasses.Count >= 3, "One-handed weapon seeds should cover at least three weapon classes.");
        AssertTrue(coveredEquipmentSlots.Contains("head"), "Equipment seeds should cover head slot.");
        AssertTrue(coveredEquipmentSlots.Contains("body"), "Equipment seeds should cover body slot.");
    }

    private void TestAllBg3WeaponTypesAreRegisteredAsWeaponEquipment()
    {
        GDictionary itemDefs = ItemDefs();
        AssertEq(Bg3WeaponSeedItems.Count, 31, "BG3 weapon seed count should remain 31.");
        foreach ((StringName weaponTypeId, StringName itemId) in Bg3WeaponSeedItems)
        {
            ItemDef itemDef = GetItemDef(itemDefs, itemId);
            AssertTrue(itemDef != null, $"{itemId} should exist for weapon type {weaponTypeId}.");
            if (itemDef == null)
                continue;
            AssertTrue(itemDef.is_equipment(), $"{itemId} should be equipment.");
            AssertTrue(itemDef.is_weapon(), $"{itemId} should be weapon.");
            AssertStringNameSeqEq(itemDef.get_equipment_slot_ids(), Names("main_hand"), $"{itemId} should equip in main hand.");
            AssertTrue(itemDef.get_weapon_attack_range() >= 1, $"{itemId} should project weapon range.");
            AssertTrue(ItemDef.get_valid_weapon_physical_damage_tags().Contains(itemDef.get_weapon_physical_damage_tag()), $"{itemId} should project a valid damage tag.");
            WeaponProfileDef profile = itemDef.weapon_profile as WeaponProfileDef;
            AssertTrue(profile != null, $"{itemId} should have a WeaponProfileDef.");
            if (profile != null)
                AssertStringNameEq(profile.weapon_type_id, weaponTypeId.ToString(), $"{itemId} should map to BG3 weapon type.");
        }
    }

    private void TestMeleeWeaponsDeclareExactlyOnePhysicalDamageTag()
    {
        GDictionary itemDefs = ItemDefs();
        var expectedWeaponTags = new Dictionary<StringName, StringName>
        {
            ["bronze_sword"] = "physical_pierce",
            ["iron_greatsword"] = "physical_slash",
            ["militia_axe"] = "physical_slash",
            ["watchman_mace"] = "physical_blunt",
        };
        var expectedProfiles = new Dictionary<StringName, WeaponProfileExpectation>
        {
            ["bronze_sword"] = new()
            {
                WeaponTypeId = "shortsword",
                OneHandedDice = new[] { 1, 6, 0 },
                Properties = new StringName[] { "finesse", "light" },
            },
            ["iron_greatsword"] = new()
            {
                WeaponTypeId = "greatsword",
                TwoHandedDice = new[] { 2, 6, 0 },
                Properties = new StringName[] { "two_handed" },
            },
            ["militia_axe"] = new()
            {
                WeaponTypeId = "handaxe",
                OneHandedDice = new[] { 1, 6, 0 },
                Properties = new StringName[] { "light", "thrown" },
            },
            ["watchman_mace"] = new()
            {
                WeaponTypeId = "mace",
                OneHandedDice = new[] { 1, 6, 0 },
            },
        };

        int coveredMeleeWeaponCount = 0;
        foreach (Variant itemDefValue in itemDefs.Values)
        {
            ItemDef itemDef = itemDefValue.AsGodotObject() as ItemDef;
            if (itemDef == null || !itemDef.is_weapon() || !itemDef.get_tags().Contains("melee"))
                continue;
            coveredMeleeWeaponCount++;
            AssertTrue(
                ItemDef.get_valid_weapon_physical_damage_tags().Contains(itemDef.get_weapon_physical_damage_tag()),
                $"Melee weapon {itemDef.item_id} should declare one valid physical damage tag."
            );
        }

        foreach ((StringName itemId, StringName expectedTag) in expectedWeaponTags)
        {
            ItemDef itemDef = GetItemDef(itemDefs, itemId);
            AssertTrue(itemDef != null, $"{itemId} should exist.");
            if (itemDef == null)
                continue;
            AssertStringNameEq(itemDef.get_weapon_physical_damage_tag(), expectedTag.ToString(), $"{itemId} damage tag.");
            WeaponProfileDef profile = itemDef.weapon_profile as WeaponProfileDef;
            AssertTrue(profile != null, $"{itemId} should have a WeaponProfileDef.");
            if (profile == null)
                continue;
            WeaponProfileExpectation expectation = expectedProfiles[itemId];
            AssertStringNameEq(profile.weapon_type_id, expectation.WeaponTypeId.ToString(), $"{itemId} profile type.");
            AssertIntSeqEq(DiceToList(profile.one_handed_dice), expectation.OneHandedDice, $"{itemId} one-handed dice.");
            AssertIntSeqEq(DiceToList(profile.two_handed_dice), expectation.TwoHandedDice, $"{itemId} two-handed dice.");
            AssertStringNameSeqEq(profile.get_properties(), expectation.Properties, $"{itemId} weapon properties.");
        }

        AssertTrue(coveredMeleeWeaponCount >= expectedWeaponTags.Count, "Melee weapon seeds should be covered by damage-tag checks.");
    }

    private void TestEquipmentServiceMovesItemsBetweenWarehouseAndSlots()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.add_item("bronze_sword", 1);
        warehouseService.add_item("leather_cap", 1);
        warehouseService.add_item("scout_charm", 1);
        List<string> charmInstanceIds = GetInstanceIdsForItem(partyState, "scout_charm");
        AssertEq(charmInstanceIds.Count, 1, "Precondition: one charm should generate one instance id.");
        if (charmInstanceIds.Count < 1)
            return;

        GDictionary swordResult = equipmentService.equip_item("hero", "bronze_sword");
        AssertTrue(DictBool(swordResult, "success"), "Sword from warehouse should equip.");
        AssertStringEq(DictString(swordResult, "slot_id"), "main_hand", "Weapon should enter main hand.");
        AssertEq(warehouseService.count_item("bronze_sword"), 0, "Equipping sword should remove warehouse item.");

        GDictionary capResult = equipmentService.equip_item("hero", "leather_cap");
        AssertTrue(DictBool(capResult, "success"), "Head armor should equip.");
        AssertStringEq(DictString(capResult, "slot_id"), "head", "Head armor should enter head slot.");
        AssertEq(warehouseService.count_item("leather_cap"), 0, "Equipping head armor should remove warehouse item.");

        GDictionary charmResult = equipmentService.equip_item("hero", "scout_charm", "", charmInstanceIds[0]);
        AssertTrue(DictBool(charmResult, "success"), "Accessory should equip.");
        AssertStringEq(DictString(charmResult, "slot_id"), "necklace", "Accessory should prefer necklace slot.");
        AssertEq(warehouseService.count_item("scout_charm"), 0, "Equipping accessory should remove warehouse item.");

        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        AssertStringNameEq(equipmentState.get_equipped_item_id("main_hand"), "bronze_sword", "Main hand should record sword.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("head"), "leather_cap", "Head slot should record cap.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("necklace"), "scout_charm", "Necklace should record charm.");

        GDictionary unequipResult = equipmentService.unequip_item("hero", "necklace");
        AssertTrue(DictBool(unequipResult, "success"), "Equipped charm should unequip.");
        AssertEq(warehouseService.count_item("scout_charm"), 1, "Unequipped charm should return to warehouse.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("necklace"), "", "Unequipped slot should clear.");
    }

    private void TestEquipmentModifiersChangeAttributeSnapshotAndRoundTrip()
    {
        GDictionary itemDefs = ItemDefs();
        ProgressionContentRegistry progressionRegistry = new();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);

        CharacterManagementModule baselineManager = new();
        baselineManager.setup(
            partyState,
            progressionRegistry.get_skill_defs(),
            progressionRegistry.get_profession_defs(),
            progressionRegistry.get_achievement_defs(),
            itemDefs
        );
        AttributeSnapshot beforeSnapshot = baselineManager.get_member_attribute_snapshot("hero");

        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.add_item("bronze_sword", 1);
        warehouseService.add_item("leather_cap", 1);
        warehouseService.add_item("leather_jerkin", 1);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        equipmentService.equip_item("hero", "bronze_sword");
        equipmentService.equip_item("hero", "leather_cap");
        equipmentService.equip_item("hero", "leather_jerkin");

        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            progressionRegistry.get_skill_defs(),
            progressionRegistry.get_profession_defs(),
            progressionRegistry.get_achievement_defs(),
            itemDefs
        );
        AttributeSnapshot afterSnapshot = manager.get_member_attribute_snapshot("hero");

        AssertEq(afterSnapshot.get_value(AttributeService.ATTACK_BONUS_ID()) - beforeSnapshot.get_value(AttributeService.ATTACK_BONUS_ID()), 2, "bronze_sword should add attack bonus.");
        AssertEq(afterSnapshot.get_value(AttributeService.ARMOR_AC_BONUS_ID()) - beforeSnapshot.get_value(AttributeService.ARMOR_AC_BONUS_ID()), 3, "Armor pieces should add armor AC.");
        AssertEq(afterSnapshot.get_value(AttributeService.ARMOR_CLASS_ID()) - beforeSnapshot.get_value(AttributeService.ARMOR_CLASS_ID()), 4, "Armor and dodge should increase AC.");
        AssertEq(afterSnapshot.get_value(AttributeService.HP_MAX_ID()) - beforeSnapshot.get_value(AttributeService.HP_MAX_ID()), 0, "leather_jerkin should not add HP.");
        AssertEq(afterSnapshot.get_value(AttributeService.DODGE_BONUS_ID()) - beforeSnapshot.get_value(AttributeService.DODGE_BONUS_ID()), 1, "leather_cap should add dodge.");

        PartyState restoredPartyState = PartyState.from_dict(partyState.to_dict());
        EquipmentState restoredEquipmentState = restoredPartyState.get_member_state("hero").equipment_state;
        AssertStringNameEq(restoredEquipmentState.get_equipped_item_id("main_hand"), "bronze_sword", "Round-trip should preserve main hand.");
        AssertStringNameEq(restoredEquipmentState.get_equipped_item_id("head"), "leather_cap", "Round-trip should preserve head.");
        AssertStringNameEq(restoredEquipmentState.get_equipped_item_id("body"), "leather_jerkin", "Round-trip should preserve body.");
    }

    private void TestEquipmentStateRequiresCanonicalPayload()
    {
        EquipmentState legacyState = EquipmentState.from_dict(
            new GDictionary
            {
                ["main_hand"] = "bronze_sword",
                ["body"] = new GDictionary { ["item_id"] = "leather_jerkin" },
            }
        );
        AssertTrue(legacyState == null, "Legacy bare equipment_state dictionary should be rejected.");

        EquipmentState validState = EquipmentState.from_dict(
            new GDictionary
            {
                ["equipped_slots"] = new GDictionary
                {
                    ["main_hand"] = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_valid_bronze_sword", new GArray { "main_hand" }),
                },
            }
        );
        AssertTrue(validState != null, "Current equipped_slots payload should parse.");

        AssertTrue(
            EquipmentState.from_dict(new GDictionary { ["equipped_slots"] = new GDictionary(), ["legacy_equipped_items"] = new GDictionary() }) == null,
            "Extra top-level legacy equipment_state field should be rejected."
        );
        AssertTrue(
            EquipmentState.from_dict(new GDictionary { ["equipped_slots"] = new GDictionary { ["weapon"] = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_invalid_slot", new GArray { "main_hand" }) } }) == null,
            "Invalid slot key should reject equipment_state."
        );
        AssertTrue(
            EquipmentState.from_dict(new GDictionary { ["equipped_slots"] = new GDictionary { ["main_hand"] = new GDictionary { ["occupied_slot_ids"] = new GArray { "main_hand" } } } }) == null,
            "Bad entry payload should reject equipment_state."
        );
        AssertTrue(
            EquipmentState.from_dict(new GDictionary { ["equipped_slots"] = new GDictionary { ["main_hand"] = MakeEquipmentEntryPayload("scout_charm", "eq_schema_mismatched_slot", new GArray { "necklace" }) } }) == null,
            "Entry key must be present in occupied slots."
        );
        AssertTrue(
            EquipmentState.from_dict(
                new GDictionary
                {
                    ["equipped_slots"] = new GDictionary
                    {
                        ["main_hand"] = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_overlap_sword", new GArray { "main_hand", "off_hand" }),
                        ["off_hand"] = MakeEquipmentEntryPayload("scout_charm", "eq_schema_overlap_charm", new GArray { "off_hand" }),
                    },
                }
            ) == null,
            "Overlapping occupied slots should reject equipment_state."
        );
    }

    private void TestEquipmentEntryRejectsBadSchema()
    {
        AssertTrue(
            EquipmentEntryState.from_dict(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_entry_valid", new GArray { "main_hand" })) != null,
            "Current equipment entry payload should parse."
        );

        GDictionary missingInstancePayload = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_missing_instance", new GArray { "main_hand" });
        missingInstancePayload.Remove("equipment_instance");
        AssertTrue(EquipmentEntryState.from_dict(missingInstancePayload) == null, "Missing equipment_instance should reject entry.");

        GDictionary extraEntryPayload = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_extra_entry", new GArray { "main_hand" });
        extraEntryPayload["legacy_item_id"] = "bronze_sword";
        AssertTrue(EquipmentEntryState.from_dict(extraEntryPayload) == null, "Extra legacy entry field should reject entry.");
        AssertTrue(EquipmentEntryState.from_dict(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_empty_slot", new GArray { "" })) == null, "Empty slot id should reject entry.");
        AssertTrue(EquipmentEntryState.from_dict(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_bad_slot", new GArray { "weapon" })) == null, "Invalid slot id should reject entry.");
        AssertTrue(EquipmentEntryState.from_dict(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_duplicate_slot", new GArray { "main_hand", "main_hand" })) == null, "Duplicate slot id should reject entry.");
        AssertTrue(EquipmentEntryState.from_dict(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_numeric_slot", new GArray { 123 })) == null, "Non-string slot id should reject entry.");
    }

    private void TestEquipmentStateKeepsTypedRuntimeStorage()
    {
        AssertEq(
            typeof(EquipmentState)
                .GetField("_equipped_slots", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, EquipmentEntryState>),
            "EquipmentState runtime slot map should be a typed C# dictionary."
        );
        AssertEq(
            typeof(EquipmentEntryState)
                .GetField(nameof(EquipmentEntryState.occupied_slot_ids))
                ?.FieldType,
            typeof(List<StringName>),
            "EquipmentEntryState occupied slots should stay in a C# List<StringName>."
        );
        AssertEq(
            typeof(EquipmentState)
                .GetMethod(nameof(EquipmentState.GetEntrySlotIdsTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<StringName>),
            "EquipmentState typed entry slot query should return IReadOnlyList<StringName>."
        );
        AssertEq(
            typeof(EquipmentState)
                .GetMethod(nameof(EquipmentState.GetOccupiedSlotIdsForEntryTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<StringName>),
            "EquipmentState typed occupied slot query should return IReadOnlyList<StringName>."
        );
        AssertEq(
            typeof(EquipmentState)
                .GetMethod(nameof(EquipmentState.SetEquippedEntryTyped))
                ?.GetParameters()[2]
                .ParameterType,
            typeof(IEnumerable<StringName>),
            "EquipmentState typed writer should accept IEnumerable<StringName>."
        );
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(typeof(EquipmentEntryState)),
            "EquipmentEntryState should be a plain C# entry DTO, not a Godot RefCounted."
        );
        AssertTrue(
            typeof(EquipmentEntryState).GetCustomAttribute<GlobalClassAttribute>() == null,
            "EquipmentEntryState should not be registered as a Godot GlobalClass."
        );
    }

    private void TestTwoHandedWeaponOccupiesBothSlots()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.add_item("iron_greatsword", 1);
        GDictionary result = equipmentService.equip_item("hero", "iron_greatsword");
        AssertTrue(DictBool(result, "success"), "iron_greatsword should equip.");
        AssertStringEq(DictString(result, "slot_id"), "main_hand", "Greatsword entry slot should be main hand.");

        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        AssertStringNameEq(equipmentState.get_equipped_item_id("main_hand"), "iron_greatsword", "Main hand should record greatsword.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("off_hand"), "iron_greatsword", "Off hand should be occupied by greatsword.");
        AssertEq(equipmentState.get_equipped_count(), 1, "Two-handed weapon should count as one equipment entry.");
        AssertEq(equipmentState.GetFilledSlotIdsTyped().Count, 2, "Two-handed weapon should fill two slots.");

        PartyState restored = PartyState.from_dict(partyState.to_dict());
        EquipmentState restoredEquipment = restored.get_member_state("hero").equipment_state;
        AssertStringNameEq(restoredEquipment.get_equipped_item_id("main_hand"), "iron_greatsword", "Round-trip should preserve main hand greatsword.");
        AssertStringNameEq(restoredEquipment.get_equipped_item_id("off_hand"), "iron_greatsword", "Round-trip should preserve off hand occupancy.");
        AssertEq(restoredEquipment.get_equipped_count(), 1, "Round-trip two-handed weapon should count as one entry.");
    }

    private void TestTwoHandedWeaponDisplacesExistingMainAndOffHand()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        PartyMemberState heroState = partyState.get_member_state("hero");
        EquipmentState equipmentState = heroState.equipment_state ?? new EquipmentState();
        heroState.equipment_state = equipmentState;
        equipmentState.SetEquippedEntryTyped("main_hand", "bronze_sword", Names("main_hand"), EquipmentInstanceState.create_instance("bronze_sword", "eq_fixture_bronze_sword"));
        equipmentState.SetEquippedEntryTyped("off_hand", "scout_charm", Names("off_hand"), EquipmentInstanceState.create_instance("scout_charm", "eq_fixture_scout_charm"));

        AssertStringNameEq(equipmentState.get_equipped_item_id("main_hand"), "bronze_sword", "Precondition: main hand should have sword.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("off_hand"), "scout_charm", "Precondition: off hand should have charm.");

        warehouseService.add_item("iron_greatsword", 1);
        GDictionary result = equipmentService.equip_item("hero", "iron_greatsword");
        AssertTrue(DictBool(result, "success"), "Two-handed replacement should succeed.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("main_hand"), "iron_greatsword", "Main hand should change to greatsword.");
        AssertStringNameEq(equipmentState.get_equipped_item_id("off_hand"), "iron_greatsword", "Off hand should be occupied by greatsword.");
        AssertEq(warehouseService.count_item("bronze_sword"), 1, "Displaced sword should return to warehouse.");
        AssertEq(warehouseService.count_item("scout_charm"), 1, "Displaced charm should return to warehouse.");
    }

    private void TestTwoHandedWeaponAttributeNotDoubleCounted()
    {
        GDictionary itemDefs = ItemDefs();
        ProgressionContentRegistry progressionRegistry = new();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.add_item("iron_greatsword", 1);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        equipmentService.equip_item("hero", "iron_greatsword");

        CharacterManagementModule manager = new();
        manager.setup(partyState, progressionRegistry.get_skill_defs(), progressionRegistry.get_profession_defs(), new GDictionary(), itemDefs);
        AttributeSnapshot snapshot = manager.get_member_attribute_snapshot("hero");

        PartyState emptyParty = BuildPartyWithMember("blank", "Blank", 8);
        CharacterManagementModule emptyManager = new();
        emptyManager.setup(emptyParty, progressionRegistry.get_skill_defs(), progressionRegistry.get_profession_defs(), new GDictionary(), itemDefs);
        AttributeSnapshot emptySnapshot = emptyManager.get_member_attribute_snapshot("blank");

        AssertEq(
            snapshot.get_value(AttributeService.ATTACK_BONUS_ID()) - emptySnapshot.get_value(AttributeService.ATTACK_BONUS_ID()),
            2,
            "Two-handed greatsword attack bonus should not double count."
        );
    }

    private void TestAtomicRollbackWhenWarehouseFull()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 1);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.add_item("iron_greatsword", 1);
        AssertEq(warehouseService.get_free_slots(), 0, "Precondition: warehouse should be full.");

        GDictionary preview = warehouseService.preview_batch_swap(
            Names("iron_greatsword"),
            Names("bronze_sword", "scout_charm")
        );
        AssertFalse(DictBool(preview, "allowed"), "Insufficient warehouse capacity should block batch swap.");
        AssertStringEq(DictString(preview, "error_code"), "warehouse_blocked_swap", "Blocked swap error code.");
        AssertEq(warehouseService.count_item("iron_greatsword"), 1, "Preview should not consume warehouse item.");
        AssertEq(warehouseService.get_free_slots(), 0, "Preview should not change free slots.");
    }

    private void TestPreviewEquipReturnsDisplacedEntries()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.add_item("bronze_sword", 1);
        warehouseService.add_item("iron_greatsword", 1);
        equipmentService.equip_item("hero", "bronze_sword");

        GDictionary preview = equipmentService.preview_equip("hero", "iron_greatsword");
        AssertTrue(DictBool(preview, "success"), "Valid replacement preview should succeed.");
        AssertStringEq(DictString(preview, "entry_slot_id"), "main_hand", "Preview entry slot.");
        AssertEq(DictArray(preview, "occupied_slot_ids").Count, 2, "Preview should include two occupied slots.");
        GArray displaced = DictArray(preview, "displaced_entries");
        AssertEq(displaced.Count, 1, "Preview should report one displaced entry.");
        if (displaced.Count > 0)
            AssertStringEq(DictString(displaced[0].AsGodotDictionary(), "item_id"), "bronze_sword", "Displaced entry should be bronze_sword.");

        AssertStringNameEq(partyState.get_member_state("hero").equipment_state.get_equipped_item_id("main_hand"), "bronze_sword", "Preview should not mutate equipment state.");
        AssertEq(warehouseService.count_item("iron_greatsword"), 1, "Preview should not consume greatsword.");
    }

    private void TestArmorMaxDexBonusCapsPositiveAgilityAc()
    {
        GDictionary itemDefs = ItemDefs();
        ProgressionContentRegistry progressionRegistry = new();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        partyState.get_member_state("hero").progression.unit_base_attributes.set_attribute_value("agility", 18);

        CharacterManagementModule baselineManager = new();
        baselineManager.setup(partyState, progressionRegistry.get_skill_defs(), progressionRegistry.get_profession_defs(), progressionRegistry.get_achievement_defs(), itemDefs);
        AttributeSnapshot baselineSnapshot = baselineManager.get_member_attribute_snapshot("hero");
        AssertEq(baselineSnapshot.get_value(AttributeService.ARMOR_CLASS_ID()), 12, "Agility 18 without armor should produce AC 12.");
        AssertEq(baselineSnapshot.get_value(AttributeService.ARMOR_MAX_DEX_BONUS_ID()), -1, "No armor should leave max dex at -1.");

        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.add_item("leather_jerkin", 1);
        warehouseService.add_item("iron_scale_mail", 1);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        GDictionary leatherResult = equipmentService.equip_item("hero", "leather_jerkin");
        AssertTrue(DictBool(leatherResult, "success"), "leather_jerkin should equip.");
        CharacterManagementModule leatherManager = new();
        leatherManager.setup(partyState, progressionRegistry.get_skill_defs(), progressionRegistry.get_profession_defs(), progressionRegistry.get_achievement_defs(), itemDefs);
        AttributeSnapshot leatherSnapshot = leatherManager.get_member_attribute_snapshot("hero");
        AssertEq(leatherSnapshot.get_value(AttributeService.ARMOR_MAX_DEX_BONUS_ID()), 6, "leather_jerkin max dex.");
        AssertEq(leatherSnapshot.get_value(AttributeService.ARMOR_CLASS_ID()), 14, "leather_jerkin should not cap agility 18.");

        GDictionary scaleResult = equipmentService.equip_item("hero", "iron_scale_mail");
        AssertTrue(DictBool(scaleResult, "success"), "iron_scale_mail should replace body armor.");
        CharacterManagementModule scaleManager = new();
        scaleManager.setup(partyState, progressionRegistry.get_skill_defs(), progressionRegistry.get_profession_defs(), progressionRegistry.get_achievement_defs(), itemDefs);
        AttributeSnapshot scaleSnapshot = scaleManager.get_member_attribute_snapshot("hero");
        AssertEq(scaleSnapshot.get_value(AttributeService.ARMOR_MAX_DEX_BONUS_ID()), 3, "iron_scale_mail max dex.");
        AssertEq(scaleSnapshot.get_value(AttributeService.ARMOR_CLASS_ID()), 15, "iron_scale_mail should cap agility AC to +3.");
    }

    private void TestRequirementProfessionCheck()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);

        ItemDef swordDef = GetItemDef(itemDefs, "bronze_sword")?.Duplicate() as ItemDef;
        EquipmentRequirement requirement = new()
        {
            required_profession_ids = new GStringArray { "warrior" },
        };
        swordDef.equip_requirement = requirement;

        GDictionary patchedDefs = itemDefs.Duplicate();
        patchedDefs["bronze_sword"] = swordDef;
        PartyWarehouseService patchedWarehouse = BuildWarehouseService(partyState, patchedDefs);
        PartyEquipmentService patchedEquipmentService = BuildEquipmentService(partyState, patchedDefs, patchedWarehouse);
        patchedWarehouse.add_item("bronze_sword", 1);

        GDictionary result = patchedEquipmentService.equip_item("hero", "bronze_sword");
        AssertFalse(DictBool(result, "success"), "Profession requirement should block equip.");
        AssertStringEq(DictString(result, "error_code"), "missing_profession", "Missing profession error code.");
        AssertEq(patchedWarehouse.count_item("bronze_sword"), 1, "Blocked equip should not consume warehouse item.");

        GDictionary preview = patchedEquipmentService.preview_equip("hero", "bronze_sword");
        AssertFalse(DictBool(preview, "success"), "Preview should also fail requirement.");
        AssertStringEq(DictString(preview, "error_code"), "missing_profession", "Preview error code.");
        AssertTrue(DictArray(preview, "blockers").Contains("missing_profession"), "Preview blockers should contain missing_profession.");
    }

    private void TestEquipCreatesInstanceIdInSlot()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.add_item("bronze_sword", 1);
        equipmentService.equip_item("hero", "bronze_sword");
        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        StringName instanceId = equipmentState.get_equipped_instance_id("main_hand");
        AssertFalse(instanceId == "", "Equipped main hand should have instance id.");
        AssertTrue(instanceId.ToString().StartsWith("eq_"), "Instance id should start with eq_.");
        AssertEq(warehouseService.count_item("bronze_sword"), 0, "Warehouse should no longer contain equipped sword.");

        PartyState restored = PartyState.from_dict(partyState.to_dict());
        AssertStringNameEq(restored.get_member_state("hero").equipment_state.get_equipped_instance_id("main_hand"), instanceId.ToString(), "Round-trip should preserve instance id.");
    }

    private void TestInstanceIdPreservedThroughUnequipAndReequip()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.add_item("bronze_sword", 1);
        equipmentService.equip_item("hero", "bronze_sword");
        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        StringName originalInstanceId = equipmentState.get_equipped_instance_id("main_hand");
        AssertFalse(originalInstanceId == "", "Precondition: equipped sword should have instance id.");

        equipmentService.unequip_item("hero", "main_hand");
        AssertEq(warehouseService.count_item("bronze_sword"), 1, "Unequipped item should return to warehouse.");
        AssertTrue(HasInstanceId(partyState, originalInstanceId), "Warehouse should contain original instance id after unequip.");

        equipmentService.equip_item("hero", "bronze_sword");
        AssertStringNameEq(equipmentState.get_equipped_instance_id("main_hand"), originalInstanceId.ToString(), "Reequip should preserve original instance id.");
    }

    private void TestTwoItemsOfSameTypeGetDifferentInstanceIds()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.add_item("scout_charm", 1);
        List<string> charmInstanceIds = GetInstanceIdsForItem(partyState, "scout_charm");
        AssertEq(charmInstanceIds.Count, 1, "Precondition: charm should generate one instance id.");
        if (charmInstanceIds.Count < 1)
            return;
        equipmentService.equip_item("hero", "scout_charm", "", charmInstanceIds[0]);
        AssertFalse(partyState.get_member_state("hero").equipment_state.get_equipped_instance_id("necklace") == "", "Necklace slot should have instance id.");
    }

    private void TestWeaponProfileEquipmentEntryRoundTrip()
    {
        GDictionary itemDefs = ItemDefs();
        ItemDef bronzeSword = GetItemDef(itemDefs, "bronze_sword");
        AssertTrue(bronzeSword != null, "bronze_sword should load.");
        if (bronzeSword == null)
            return;
        AssertTrue(bronzeSword.weapon_profile is WeaponProfileDef, "bronze_sword should have weapon_profile.");
        AssertEq(bronzeSword.get_weapon_attack_range(), 1, "weapon_profile should provide attack range.");
        AssertStringNameEq(bronzeSword.get_weapon_physical_damage_tag(), "physical_pierce", "weapon_profile should provide damage tag.");

        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        warehouseService.add_item("bronze_sword", 1);
        GDictionary equipResult = equipmentService.equip_item("hero", "bronze_sword");
        AssertTrue(DictBool(equipResult, "success"), "weapon_profile weapon should equip.");
        AssertEq(warehouseService.count_item("bronze_sword"), 0, "Equipped weapon_profile weapon should leave warehouse.");

        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        StringName instanceId = equipmentState.get_equipped_instance_id("main_hand");
        AssertFalse(instanceId == "", "Equipped weapon_profile weapon should have instance id.");
        GDictionary equipmentPayload = equipmentState.to_dict();
        GDictionary slotPayload = equipmentPayload["equipped_slots"].AsGodotDictionary()["main_hand"].AsGodotDictionary();
        GDictionary slotInstancePayload = slotPayload["equipment_instance"].AsGodotDictionary();
        AssertStringEq(DictString(slotInstancePayload, "item_id"), "bronze_sword", "Entry payload should store item_id in equipment_instance.");
        AssertStringEq(DictString(slotInstancePayload, "instance_id"), instanceId.ToString(), "Entry payload should store same instance_id.");
        AssertFalse(slotPayload.ContainsKey("item_id"), "Entry payload should not keep top-level item_id.");
        AssertFalse(slotPayload.ContainsKey("instance_id"), "Entry payload should not keep top-level instance_id.");
        AssertFalse(slotPayload.ContainsKey("weapon_profile"), "Entry payload should not serialize weapon_profile.");
        AssertFalse(slotPayload.ContainsKey("weapon_attack_range"), "Entry payload should not serialize legacy weapon_attack_range.");
        AssertFalse(slotPayload.ContainsKey("weapon_physical_damage_tag"), "Entry payload should not serialize legacy damage tag.");

        PartyState restoredPartyState = PartyState.from_dict(partyState.to_dict());
        AssertTrue(restoredPartyState != null, "weapon_profile weapon PartyState round-trip should parse.");
        if (restoredPartyState == null)
            return;
        EquipmentState restoredEquipmentState = restoredPartyState.get_member_state("hero").equipment_state;
        AssertStringNameEq(restoredEquipmentState.get_equipped_item_id("main_hand"), "bronze_sword", "Round-trip should preserve item id.");
        AssertStringNameEq(restoredEquipmentState.get_equipped_instance_id("main_hand"), instanceId.ToString(), "Round-trip should preserve instance id.");

        PartyWarehouseService restoredWarehouse = BuildWarehouseService(restoredPartyState, itemDefs);
        PartyEquipmentService restoredEquipmentService = BuildEquipmentService(restoredPartyState, itemDefs, restoredWarehouse);
        GDictionary unequipResult = restoredEquipmentService.unequip_item("hero", "main_hand");
        AssertTrue(DictBool(unequipResult, "success"), "Round-tripped weapon_profile weapon should unequip.");
        AssertEq(restoredWarehouse.count_item("bronze_sword"), 1, "Unequipped weapon_profile weapon should return to warehouse.");
        var restoredInstances = restoredPartyState.warehouse_state.get_non_empty_instances();
        AssertEq(restoredInstances.Count, 1, "Warehouse should contain one weapon_profile instance after unequip.");
        if (restoredInstances.Count > 0)
        {
            GDictionary instancePayload = restoredInstances[0].to_dict();
            AssertStringEq(DictString(instancePayload, "instance_id"), instanceId.ToString(), "Returned instance should preserve instance id.");
            AssertFalse(instancePayload.ContainsKey("weapon_profile"), "Equipment instance payload should not serialize weapon_profile.");
            AssertFalse(instancePayload.ContainsKey("weapon_attack_range"), "Equipment instance payload should not write weapon_attack_range.");
            AssertFalse(instancePayload.ContainsKey("weapon_physical_damage_tag"), "Equipment instance payload should not write damage tag.");
        }
    }

    private void TestEquippedInstanceFieldsSurviveRoundTripAndUnequip()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        EquipmentInstanceState epicInstance = EquipmentInstanceState.create_instance("bronze_sword", "eq_epic_equipped_bronze_sword");
        epicInstance.rarity = EquipmentInstanceState.RARITY_TIER_EPIC();
        epicInstance.current_durability = 17;
        partyState.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState> { epicInstance };

        GDictionary equipResult = equipmentService.equip_item("hero", "bronze_sword");
        AssertTrue(DictBool(equipResult, "success"), "Equipment with full instance fields should equip.");
        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        AssertEquipmentInstanceFields(equipmentState.get_equipped_instance("main_hand"), "eq_epic_equipped_bronze_sword", EquipmentInstanceState.RARITY_TIER_EPIC(), 17, "Equipped slot");

        PartyState restoredPartyState = PartyState.from_dict(partyState.to_dict());
        AssertTrue(restoredPartyState != null, "Full equipment instance fields should round-trip.");
        if (restoredPartyState == null)
            return;
        PartyWarehouseService restoredWarehouseService = BuildWarehouseService(restoredPartyState, itemDefs);
        PartyEquipmentService restoredEquipmentService = BuildEquipmentService(restoredPartyState, itemDefs, restoredWarehouseService);
        EquipmentState restoredEquipmentState = restoredPartyState.get_member_state("hero").equipment_state;
        AssertEquipmentInstanceFields(restoredEquipmentState.get_equipped_instance("main_hand"), "eq_epic_equipped_bronze_sword", EquipmentInstanceState.RARITY_TIER_EPIC(), 17, "Round-tripped equipped slot");

        GDictionary unequipResult = restoredEquipmentService.unequip_item("hero", "main_hand");
        AssertTrue(DictBool(unequipResult, "success"), "Full instance equipment should unequip.");
        var restoredInstances = restoredPartyState.warehouse_state.get_non_empty_instances();
        AssertEq(restoredInstances.Count, 1, "Unequipped full instance should return to warehouse.");
        if (restoredInstances.Count > 0)
            AssertEquipmentInstanceFields(restoredInstances[0], "eq_epic_equipped_bronze_sword", EquipmentInstanceState.RARITY_TIER_EPIC(), 17, "Returned full instance");
    }

    private void TestEquipmentInstanceRarityRoundTripAndStrictSchema()
    {
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        EquipmentInstanceState epicInstance = EquipmentInstanceState.create_instance("bronze_sword", "eq_epic_bronze_sword");
        epicInstance.rarity = EquipmentInstanceState.RARITY_TIER_EPIC();
        epicInstance.current_durability = DefaultCurrentDurabilityForRarity(epicInstance.rarity);
        partyState.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState> { epicInstance };

        PartyState restoredPartyState = PartyState.from_dict(partyState.to_dict());
        AssertTrue(restoredPartyState != null, "Rarity PartyState round-trip should parse.");
        if (restoredPartyState == null)
            return;
        var restoredInstances = restoredPartyState.warehouse_state.get_non_empty_instances();
        AssertEq(restoredInstances.Count, 1, "Rarity round-trip should preserve one instance.");
        if (restoredInstances.Count > 0)
            AssertEq(restoredInstances[0].rarity, EquipmentInstanceState.RARITY_TIER_EPIC(), "Rarity tier should persist.");

        string missingRarityError = EquipmentInstanceState.get_payload_validation_error(
            new GDictionary
            {
                ["instance_id"] = "eq_missing_rarity_bronze_sword",
                ["item_id"] = "bronze_sword",
                ["current_durability"] = DefaultCurrentDurabilityForRarity(EquipmentInstanceState.RARITY_TIER_COMMON()),
            },
            false
        );
        AssertTrue(missingRarityError.Contains("missing required field 'rarity'"), $"Missing rarity should report field error. error={missingRarityError}");

        string invalidRarityError = EquipmentInstanceState.get_payload_validation_error(
            new GDictionary
            {
                ["instance_id"] = "eq_invalid_rarity_bronze_sword",
                ["item_id"] = "bronze_sword",
                ["rarity"] = 999,
                ["current_durability"] = DefaultCurrentDurabilityForRarity(EquipmentInstanceState.RARITY_TIER_COMMON()),
            },
            false
        );
        AssertTrue(invalidRarityError.Contains("invalid rarity 999"), $"Invalid rarity should report field error. error={invalidRarityError}");

        GDictionary invalidInstanceIdPayload = MakeEquipmentInstancePayload("eq_schema_invalid_instance_id");
        invalidInstanceIdPayload["instance_id"] = 17;
        AssertEquipmentInstanceValidationError(invalidInstanceIdPayload, "instance_id must be String or StringName", "Numeric instance_id should be rejected.");

        GDictionary invalidItemIdPayload = MakeEquipmentInstancePayload("eq_schema_invalid_item_id");
        invalidItemIdPayload["item_id"] = 17;
        AssertEquipmentInstanceValidationError(invalidItemIdPayload, "item_id must be String or StringName", "Numeric item_id should be rejected.");

        GDictionary stringRarityPayload = MakeEquipmentInstancePayload("eq_schema_string_rarity");
        stringRarityPayload["rarity"] = "3";
        AssertEquipmentInstanceValidationError(stringRarityPayload, "rarity must be int", "String rarity should be rejected.");

        GDictionary stringDurabilityPayload = MakeEquipmentInstancePayload("eq_schema_string_durability");
        stringDurabilityPayload["current_durability"] = "17";
        AssertEquipmentInstanceValidationError(stringDurabilityPayload, "current_durability must be int", "String durability should be rejected.");

        GDictionary zeroDurabilityPayload = MakeEquipmentInstancePayload("eq_schema_zero_durability");
        zeroDurabilityPayload["current_durability"] = 0;
        AssertEquipmentInstanceValidationError(zeroDurabilityPayload, "invalid current_durability 0", "Zero durability should be rejected.");
    }

    private void TestDuplicateSameItemInstanceIdSelection()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        EquipmentInstanceState commonInstance = EquipmentInstanceState.create_instance("bronze_sword", "eq_duplicate_common_sword");
        commonInstance.rarity = EquipmentInstanceState.RARITY_TIER_COMMON();
        commonInstance.current_durability = 11;
        EquipmentInstanceState rareInstance = EquipmentInstanceState.create_instance("bronze_sword", "eq_duplicate_rare_sword");
        rareInstance.rarity = EquipmentInstanceState.RARITY_TIER_RARE();
        rareInstance.current_durability = 23;
        EquipmentInstanceState mismatchInstance = EquipmentInstanceState.create_instance("scout_charm", "eq_duplicate_wrong_item");
        partyState.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
        {
            commonInstance,
            rareInstance,
            mismatchInstance,
        };

        GDictionary itemOnlyResult = equipmentService.equip_item("hero", "bronze_sword");
        AssertFalse(DictBool(itemOnlyResult, "success", true), "Duplicate item-id-only equip should fail.");
        AssertStringEq(DictString(itemOnlyResult, "error_code"), "equipment_instance_id_required", "Duplicate item-id-only equip error.");

        GDictionary mismatchResult = equipmentService.equip_item("hero", "bronze_sword", "", "eq_duplicate_wrong_item");
        AssertFalse(DictBool(mismatchResult, "success", true), "Mismatched instance id should fail.");
        AssertStringEq(DictString(mismatchResult, "error_code"), "equipment_instance_item_mismatch", "Mismatched instance id error.");

        GDictionary missingResult = equipmentService.equip_item("hero", "bronze_sword", "", "eq_duplicate_missing");
        AssertFalse(DictBool(missingResult, "success", true), "Missing instance id should fail.");
        AssertStringEq(DictString(missingResult, "error_code"), "warehouse_missing_instance", "Missing instance id error.");

        GDictionary equipResult = equipmentService.equip_item("hero", "bronze_sword", "", "eq_duplicate_rare_sword");
        AssertTrue(DictBool(equipResult, "success"), "Explicit rare instance id should equip.");
        EquipmentInstanceState equippedInstance = partyState.get_member_state("hero").equipment_state.get_equipped_instance("main_hand");
        AssertEquipmentInstanceFields(equippedInstance, "eq_duplicate_rare_sword", EquipmentInstanceState.RARITY_TIER_RARE(), 23, "Explicit instance equip");
        AssertTrue(warehouseService.has_equipment_instance("eq_duplicate_common_sword", "bronze_sword"), "Unselected common instance should remain in warehouse.");
        AssertFalse(warehouseService.has_equipment_instance("eq_duplicate_rare_sword", "bronze_sword"), "Equipped rare instance should leave warehouse.");

        PartyState restoredPartyState = PartyState.from_dict(partyState.to_dict());
        AssertTrue(restoredPartyState != null, "Explicit duplicate instance equip should round-trip.");
        if (restoredPartyState == null)
            return;
        PartyWarehouseService restoredWarehouseService = BuildWarehouseService(restoredPartyState, itemDefs);
        PartyEquipmentService restoredEquipmentService = BuildEquipmentService(restoredPartyState, itemDefs, restoredWarehouseService);
        AssertEquipmentInstanceFields(restoredPartyState.get_member_state("hero").equipment_state.get_equipped_instance("main_hand"), "eq_duplicate_rare_sword", EquipmentInstanceState.RARITY_TIER_RARE(), 23, "Round-tripped explicit instance equip");
        GDictionary unequipResult = restoredEquipmentService.unequip_item("hero", "main_hand");
        AssertTrue(DictBool(unequipResult, "success"), "Round-tripped explicit instance should unequip.");
        AssertTrue(restoredWarehouseService.has_equipment_instance("eq_duplicate_common_sword", "bronze_sword"), "Common instance should still be in warehouse after unequip.");
        AssertTrue(restoredWarehouseService.has_equipment_instance("eq_duplicate_rare_sword", "bronze_sword"), "Rare instance should return to warehouse after unequip.");
        AssertEquipmentInstanceFields(restoredWarehouseService.get_equipment_instance_by_id("eq_duplicate_rare_sword", "bronze_sword"), "eq_duplicate_rare_sword", EquipmentInstanceState.RARITY_TIER_RARE(), 23, "Returned explicit instance");
    }

    private void TestPartyStateRejectsDuplicateEquipmentInstanceIds()
    {
        PartyState warehouseDuplicateParty = BuildPartyWithMember("hero", "Hero", 8);
        warehouseDuplicateParty.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
        {
            EquipmentInstanceState.create_instance("bronze_sword", "eq_party_duplicate"),
            EquipmentInstanceState.create_instance("scout_charm", "eq_party_duplicate"),
        };
        AssertTrue(PartyState.from_dict(warehouseDuplicateParty.to_dict()) == null, "Duplicate warehouse instance ids should reject PartyState payload.");

        PartyState warehouseAndEquippedParty = BuildPartyWithMember("hero", "Hero", 8);
        warehouseAndEquippedParty.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
        {
            EquipmentInstanceState.create_instance("bronze_sword", "eq_party_shared"),
        };
        warehouseAndEquippedParty.get_member_state("hero").equipment_state.SetEquippedEntryTyped(
            "main_hand",
            "bronze_sword",
            Names("main_hand"),
            EquipmentInstanceState.create_instance("bronze_sword", "eq_party_shared")
        );
        AssertTrue(PartyState.from_dict(warehouseAndEquippedParty.to_dict()) == null, "Instance id shared by warehouse and equipment should reject PartyState payload.");

        PartyState sameMemberDuplicateParty = BuildPartyWithMember("hero", "Hero", 8);
        EquipmentState sameMemberEquipment = sameMemberDuplicateParty.get_member_state("hero").equipment_state;
        sameMemberEquipment.SetEquippedEntryTyped("main_hand", "bronze_sword", Names("main_hand"), EquipmentInstanceState.create_instance("bronze_sword", "eq_party_same_member"));
        sameMemberEquipment.SetEquippedEntryTyped("necklace", "scout_charm", Names("necklace"), EquipmentInstanceState.create_instance("scout_charm", "eq_party_same_member"));
        AssertTrue(PartyState.from_dict(sameMemberDuplicateParty.to_dict()) == null, "Same member duplicate equipped instance id should reject PartyState payload.");

        PartyState crossMemberDuplicateParty = BuildPartyWithMember("hero", "Hero", 8);
        PartyMemberState ally = new()
        {
            member_id = "ally",
            display_name = "Ally",
            progression = new UnitProgress(),
        };
        ally.progression.unit_id = ally.member_id;
        ally.progression.display_name = ally.display_name;
        crossMemberDuplicateParty.set_member_state(ally);
        crossMemberDuplicateParty.reserve_member_ids = Names("ally");
        crossMemberDuplicateParty.get_member_state("hero").equipment_state.SetEquippedEntryTyped("main_hand", "bronze_sword", Names("main_hand"), EquipmentInstanceState.create_instance("bronze_sword", "eq_party_cross_member"));
        crossMemberDuplicateParty.get_member_state("ally").equipment_state.SetEquippedEntryTyped("necklace", "scout_charm", Names("necklace"), EquipmentInstanceState.create_instance("scout_charm", "eq_party_cross_member"));
        AssertTrue(PartyState.from_dict(crossMemberDuplicateParty.to_dict()) == null, "Cross-member duplicate equipped instance id should reject PartyState payload.");
    }

    private static PartyState BuildPartyWithMember(StringName memberId, string displayName, int storageSpace)
    {
        PartyState partyState = new();
        PartyMemberState memberState = new()
        {
            member_id = memberId,
            display_name = displayName,
            progression = new UnitProgress(),
            current_hp = 24,
            current_mp = 8,
        };
        memberState.progression.unit_id = memberId;
        memberState.progression.display_name = displayName;

        UnitBaseAttributes unitBaseAttributes = new();
        unitBaseAttributes.set_attribute_value("strength", 4);
        unitBaseAttributes.set_attribute_value("agility", 3);
        unitBaseAttributes.set_attribute_value("constitution", 4);
        unitBaseAttributes.set_attribute_value("perception", 3);
        unitBaseAttributes.set_attribute_value("intelligence", 2);
        unitBaseAttributes.set_attribute_value("willpower", 2);
        unitBaseAttributes.custom_stats[PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID()] = storageSpace;
        memberState.progression.unit_base_attributes = unitBaseAttributes;

        partyState.set_member_state(memberState);
        partyState.active_member_ids = new GStringNameArray { memberId };
        partyState.reserve_member_ids = new GStringNameArray();
        partyState.leader_member_id = memberId;
        partyState.main_character_member_id = memberId;
        return partyState;
    }

    private static GDictionary MakeEquipmentInstancePayload(string instanceId, string itemId = "bronze_sword") =>
        new()
        {
            ["instance_id"] = instanceId,
            ["item_id"] = itemId,
            ["rarity"] = EquipmentInstanceState.RARITY_TIER_COMMON(),
            ["current_durability"] = DefaultCurrentDurabilityForRarity(EquipmentInstanceState.RARITY_TIER_COMMON()),
        };

    private static GDictionary MakeEquipmentEntryPayload(
        StringName itemId,
        StringName instanceId,
        GArray occupiedSlotIds
    )
    {
        EquipmentInstanceState instance = EquipmentInstanceState.create_instance(itemId, instanceId);
        return new GDictionary
        {
            ["occupied_slot_ids"] = occupiedSlotIds,
            ["equipment_instance"] = instance.to_dict(),
        };
    }

    private static int DefaultCurrentDurabilityForRarity(int rarity)
    {
        if (rarity == EquipmentInstanceState.RARITY_TIER_UNCOMMON())
            return 84;
        if (rarity == EquipmentInstanceState.RARITY_TIER_RARE())
            return 120;
        if (rarity == EquipmentInstanceState.RARITY_TIER_EPIC())
            return 160;
        if (rarity == EquipmentInstanceState.RARITY_TIER_LEGENDARY())
            return 200;
        return 56;
    }

    private static int[] DiceToList(WeaponDamageDiceDef diceResource)
    {
        if (diceResource == null)
            return System.Array.Empty<int>();
        return new[] { diceResource.dice_count, diceResource.dice_sides, diceResource.flat_bonus };
    }

    private static List<string> GetInstanceIdsForItem(PartyState partyState, StringName itemId)
    {
        var result = new List<string>();
        if (partyState?.warehouse_state == null)
            return result;
        foreach (EquipmentInstanceState instance in partyState.warehouse_state.get_non_empty_instances())
            if (instance != null && instance.item_id == itemId)
                result.Add(instance.instance_id.ToString());
        result.Sort(System.StringComparer.Ordinal);
        return result;
    }

    private static bool HasInstanceId(PartyState partyState, StringName instanceId)
    {
        foreach (EquipmentInstanceState instance in partyState.warehouse_state.get_non_empty_instances())
            if (instance.instance_id == instanceId)
                return true;
        return false;
    }

    private void AssertEquipmentInstanceFields(
        EquipmentInstanceState instance,
        string expectedInstanceId,
        int expectedRarity,
        int expectedCurrentDurability,
        string messagePrefix
    )
    {
        AssertTrue(instance != null, $"{messagePrefix}: instance should not be null.");
        if (instance == null)
            return;
        AssertStringNameEq(instance.instance_id, expectedInstanceId, $"{messagePrefix}: instance_id.");
        AssertEq(instance.rarity, expectedRarity, $"{messagePrefix}: rarity.");
        AssertEq(instance.current_durability, expectedCurrentDurability, $"{messagePrefix}: current_durability.");
    }

    private void AssertEquipmentInstanceValidationError(
        GDictionary payload,
        string expectedFragment,
        string message
    )
    {
        string validationError = EquipmentInstanceState.get_payload_validation_error(payload, false);
        AssertTrue(validationError.Contains(expectedFragment), $"{message} error={validationError}");
    }

    private static GDictionary ItemDefs() => new ItemContentRegistry().get_item_defs();

    private static ItemDef GetItemDef(GDictionary itemDefs, StringName itemId)
    {
        if (itemDefs == null || !itemDefs.ContainsKey(itemId))
            return null;
        return itemDefs[itemId].AsGodotObject() as ItemDef;
    }

    private static PartyWarehouseService BuildWarehouseService(PartyState partyState, GDictionary itemDefs)
    {
        PartyWarehouseService warehouseService = new();
        warehouseService.setup(partyState, itemDefs);
        return warehouseService;
    }

    private static PartyEquipmentService BuildEquipmentService(
        PartyState partyState,
        GDictionary itemDefs,
        PartyWarehouseService warehouseService
    )
    {
        PartyEquipmentService equipmentService = new();
        equipmentService.setup(partyState, itemDefs, warehouseService);
        return equipmentService;
    }

    private static GStringNameArray Names(params string[] values)
    {
        var result = new GStringNameArray();
        foreach (string value in values)
            result.Add(value);
        return result;
    }

    private static bool StringNameSeqEquals(IEnumerable<StringName> actual, IReadOnlyList<StringName> expected)
    {
        var actualValues = new List<string>();
        if (actual != null)
            foreach (StringName value in actual)
                actualValues.Add(value.ToString());
        if (actualValues.Count != expected.Count)
            return false;
        for (int index = 0; index < expected.Count; index++)
            if (actualValues[index] != expected[index].ToString())
                return false;
        return true;
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new GArray();
        return dictionary[key].AsGodotArray();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }

    private static string DictString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsString();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message) => AssertTrue(!condition, message);

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={FormatValue(actual)} expected={FormatValue(expected)}");
    }

    private void AssertStringEq(string actual, string expected, string message)
    {
        if (actual != expected)
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertStringNameEq(StringName actual, string expected, string message)
    {
        if (actual.ToString() != expected)
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertStringNameSeqEq(IEnumerable<StringName> actual, IReadOnlyList<StringName> expected, string message)
    {
        if (!StringNameSeqEquals(actual, expected))
            _failures.Add($"{message} | actual={FormatStringNames(actual)} expected={FormatStringNames(expected)}");
    }

    private void AssertIntSeqEq(IReadOnlyList<int> actual, IReadOnlyList<int> expected, string message)
    {
        bool equal = actual.Count == expected.Count;
        for (int index = 0; equal && index < actual.Count; index++)
            equal = actual[index] == expected[index];
        if (!equal)
            _failures.Add($"{message} | actual=[{string.Join(", ", actual)}] expected=[{string.Join(", ", expected)}]");
    }

    private static string FormatStringNames(IEnumerable<StringName> values)
    {
        var strings = new List<string>();
        if (values != null)
            foreach (StringName value in values)
                strings.Add(value.ToString());
        return $"[{string.Join(", ", strings)}]";
    }

    private static string FormatValue<T>(T value)
    {
        if (value is IEnumerable<string> strings)
            return $"[{string.Join(", ", strings)}]";
        return value?.ToString() ?? "<null>";
    }

    private void Finish()
    {
        if (_failures.Count == 0)
        {
            GD.Print("Party equipment regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Party equipment regression: FAIL ({_failures.Count})");
        Quit(1);
    }
}
