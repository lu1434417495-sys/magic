using System.Collections.Generic;
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

    private readonly TestHarness _test = new();

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

        Quit(_test.Finish("Party equipment regression"));
    }

    private void TestItemRegistryAcceptsEquipmentSeedData()
    {
        ItemContentRegistry registry = new();
        _test.Eq(registry.Validate().Count, 0, "Equipment seed item definitions should validate.");

        GDictionary itemDefs = ProjectItemDefs(registry.GetItemDefsTyped());
        ItemDef bronzeSword = GetItemDef(itemDefs, "bronze_sword");
        ItemDef leatherCap = GetItemDef(itemDefs, "leather_cap");
        ItemDef leatherJerkin = GetItemDef(itemDefs, "leather_jerkin");
        ItemDef ironScaleMail = GetItemDef(itemDefs, "iron_scale_mail");
        ItemDef scoutCharm = GetItemDef(itemDefs, "scout_charm");
        ItemDef ironGreatsword = GetItemDef(itemDefs, "iron_greatsword");
        ItemDef militiaAxe = GetItemDef(itemDefs, "militia_axe");
        ItemDef watchmanMace = GetItemDef(itemDefs, "watchman_mace");

        _test.True(bronzeSword != null && bronzeSword.IsEquipment(), "bronze_sword should be equipment.");
        _test.True(leatherCap != null && leatherCap.IsEquipment(), "leather_cap should be equipment.");
        _test.True(leatherJerkin != null && leatherJerkin.IsEquipment(), "leather_jerkin should be equipment.");
        _test.True(ironScaleMail != null && ironScaleMail.IsEquipment(), "iron_scale_mail should be equipment.");
        _test.True(scoutCharm != null && scoutCharm.IsEquipment(), "scout_charm should be equipment.");
        _test.True(ironGreatsword != null && ironGreatsword.IsEquipment(), "iron_greatsword should be equipment.");
        _test.True(militiaAxe != null && militiaAxe.IsEquipment(), "militia_axe should be equipment.");
        _test.True(watchmanMace != null && watchmanMace.IsEquipment(), "watchman_mace should be equipment.");
        _test.False(itemDefs.ContainsKey("scout_dagger"), "scout_dagger should be removed from equipment seed data.");

        AssertStringNameEq(bronzeSword?.GetEquipmentTypeIdNormalized() ?? "", "weapon", "bronze_sword type.");
        AssertStringNameEq(leatherCap?.GetEquipmentTypeIdNormalized() ?? "", "armor", "leather_cap type.");
        AssertStringNameEq(leatherJerkin?.GetEquipmentTypeIdNormalized() ?? "", "armor", "leather_jerkin type.");
        AssertStringNameEq(ironScaleMail?.GetEquipmentTypeIdNormalized() ?? "", "armor", "iron_scale_mail type.");
        AssertStringNameEq(scoutCharm?.GetEquipmentTypeIdNormalized() ?? "", "accessory", "scout_charm type.");
        AssertStringNameEq(ironGreatsword?.GetEquipmentTypeIdNormalized() ?? "", "weapon", "iron_greatsword type.");
        AssertStringNameEq(militiaAxe?.GetEquipmentTypeIdNormalized() ?? "", "weapon", "militia_axe type.");
        AssertStringNameEq(watchmanMace?.GetEquipmentTypeIdNormalized() ?? "", "weapon", "watchman_mace type.");

        _test.True(bronzeSword?.IsWeapon() ?? false, "bronze_sword should be a weapon.");
        _test.True(leatherCap?.IsArmor() ?? false, "leather_cap should be armor.");
        _test.True(leatherJerkin?.IsArmor() ?? false, "leather_jerkin should be armor.");
        _test.True(ironScaleMail?.IsArmor() ?? false, "iron_scale_mail should be armor.");
        _test.True(scoutCharm?.IsAccessory() ?? false, "scout_charm should be accessory.");
        _test.Eq(ironGreatsword?.GetFinalOccupiedSlotIdsTyped("main_hand").Count ?? -1, 2, "iron_greatsword should occupy two slots.");

        AssertStringNameSeqEq(leatherCap?.GetTagsTyped(), Names("armor", "head", "leather", "light_armor"), "leather_cap tags.");
        _test.Eq(leatherCap?.GetBuyPrice() ?? -1, 100, "leather_cap buy price.");
        _test.Eq(leatherCap?.GetSellPrice() ?? -1, 50, "leather_cap sell price.");
        AssertStringNameSeqEq(leatherCap?.GetEquipmentSlotIdsTyped(), Names("head"), "leather_cap slot.");
        AssertStringNameSeqEq(leatherCap?.GetFinalOccupiedSlotIdsTyped("head"), Names("head"), "leather_cap occupied slot.");
        _test.Eq(leatherCap?.GetMaxDexBonus() ?? -2, -1, "leather_cap max dex.");

        AssertStringNameSeqEq(leatherJerkin?.GetTagsTyped(), Names("armor", "body", "leather", "light_armor"), "leather_jerkin tags.");
        AssertStringNameSeqEq(leatherJerkin?.GetEquipmentSlotIdsTyped(), Names("body"), "leather_jerkin slot.");
        AssertStringNameSeqEq(leatherJerkin?.GetFinalOccupiedSlotIdsTyped("body"), Names("body"), "leather_jerkin occupied slot.");
        _test.Eq(leatherJerkin?.GetMaxDexBonus() ?? -2, 6, "leather_jerkin max dex.");

        AssertStringNameSeqEq(ironScaleMail?.GetTagsTyped(), Names("armor", "body", "metal", "medium_armor", "scale_mail"), "iron_scale_mail tags.");
        _test.Eq(ironScaleMail?.GetBuyPrice() ?? -1, 180, "iron_scale_mail buy price.");
        _test.Eq(ironScaleMail?.GetSellPrice() ?? -1, 90, "iron_scale_mail sell price.");
        AssertStringNameSeqEq(ironScaleMail?.GetEquipmentSlotIdsTyped(), Names("body"), "iron_scale_mail slot.");
        AssertStringNameSeqEq(ironScaleMail?.GetFinalOccupiedSlotIdsTyped("body"), Names("body"), "iron_scale_mail occupied slot.");
        _test.Eq(ironScaleMail?.GetMaxDexBonus() ?? -2, 3, "iron_scale_mail max dex.");

        AssertStringNameSeqEq(bronzeSword?.GetTagsTyped(), Names("weapon", "melee", "one_handed", "shortsword", "sword", "weapon_class_sword", "weapon_type_shortsword"), "bronze_sword tags.");
        AssertStringNameSeqEq(militiaAxe?.GetTagsTyped(), Names("weapon", "melee", "one_handed", "handaxe", "axe", "weapon_class_axe", "weapon_type_handaxe"), "militia_axe tags.");
        _test.Eq(militiaAxe?.GetBuyPrice() ?? -1, 145, "militia_axe buy price.");
        _test.Eq(militiaAxe?.GetSellPrice() ?? -1, 70, "militia_axe sell price.");
        AssertStringNameSeqEq(militiaAxe?.GetEquipmentSlotIdsTyped(), Names("main_hand"), "militia_axe slot.");
        AssertStringNameSeqEq(militiaAxe?.GetFinalOccupiedSlotIdsTyped("main_hand"), Names("main_hand"), "militia_axe occupied slot.");
        AssertStringNameSeqEq(watchmanMace?.GetTagsTyped(), Names("weapon", "melee", "one_handed", "mace", "weapon_class_mace", "weapon_type_mace"), "watchman_mace tags.");
        _test.Eq(watchmanMace?.GetBuyPrice() ?? -1, 175, "watchman_mace buy price.");
        _test.Eq(watchmanMace?.GetSellPrice() ?? -1, 85, "watchman_mace sell price.");

        var oneHandedWeaponClasses = new HashSet<StringName>();
        var coveredEquipmentSlots = new HashSet<StringName>();
        foreach (Variant itemDefValue in itemDefs.Values)
        {
            ItemDef itemDef = itemDefValue.AsGodotObject() as ItemDef;
            if (itemDef == null)
                continue;
            if (itemDef.IsEquipment())
                foreach (StringName slotId in itemDef.GetEquipmentSlotIdsTyped())
                    coveredEquipmentSlots.Add(slotId);
            if (!itemDef.IsWeapon())
                continue;
            if (!StringNameSeqEquals(itemDef.GetEquipmentSlotIdsTyped(), Names("main_hand")))
                continue;
            if (!StringNameSeqEquals(itemDef.GetFinalOccupiedSlotIdsTyped("main_hand"), Names("main_hand")))
                continue;
            foreach (StringName tag in itemDef.GetTagsTyped())
                if (tag.ToString().StartsWith("weapon_class_"))
                    oneHandedWeaponClasses.Add(tag);
        }

        _test.True(oneHandedWeaponClasses.Count >= 3, "One-handed weapon seeds should cover at least three weapon classes.");
        _test.True(coveredEquipmentSlots.Contains("head"), "Equipment seeds should cover head slot.");
        _test.True(coveredEquipmentSlots.Contains("body"), "Equipment seeds should cover body slot.");
    }

    private void TestAllBg3WeaponTypesAreRegisteredAsWeaponEquipment()
    {
        GDictionary itemDefs = ItemDefs();
        _test.Eq(Bg3WeaponSeedItems.Count, 31, "BG3 weapon seed count should remain 31.");
        foreach ((StringName weaponTypeId, StringName itemId) in Bg3WeaponSeedItems)
        {
            ItemDef itemDef = GetItemDef(itemDefs, itemId);
            _test.True(itemDef != null, $"{itemId} should exist for weapon type {weaponTypeId}.");
            if (itemDef == null)
                continue;
            _test.True(itemDef.IsEquipment(), $"{itemId} should be equipment.");
            _test.True(itemDef.IsWeapon(), $"{itemId} should be weapon.");
            AssertStringNameSeqEq(itemDef.GetEquipmentSlotIdsTyped(), Names("main_hand"), $"{itemId} should equip in main hand.");
            _test.True(itemDef.GetWeaponAttackRange() >= 1, $"{itemId} should project weapon range.");
            _test.True(
                ItemDef.ToWeaponPhysicalDamageTagKind(itemDef.GetWeaponPhysicalDamageTag())
                    != WeaponPhysicalDamageTagKind.Unknown,
                $"{itemId} should project a valid damage tag."
            );
            WeaponProfileDef profile = itemDef.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, $"{itemId} should have a WeaponProfileDef.");
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
            if (itemDef == null || !itemDef.IsWeapon() || !itemDef.GetTagsTyped().Contains(new StringName("melee")))
                continue;
            coveredMeleeWeaponCount++;
            _test.True(
                ItemDef.ToWeaponPhysicalDamageTagKind(itemDef.GetWeaponPhysicalDamageTag())
                    != WeaponPhysicalDamageTagKind.Unknown,
                $"Melee weapon {itemDef.item_id} should declare one valid physical damage tag."
            );
        }

        foreach ((StringName itemId, StringName expectedTag) in expectedWeaponTags)
        {
            ItemDef itemDef = GetItemDef(itemDefs, itemId);
            _test.True(itemDef != null, $"{itemId} should exist.");
            if (itemDef == null)
                continue;
            AssertStringNameEq(itemDef.GetWeaponPhysicalDamageTag(), expectedTag.ToString(), $"{itemId} damage tag.");
            WeaponProfileDef profile = itemDef.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, $"{itemId} should have a WeaponProfileDef.");
            if (profile == null)
                continue;
            WeaponProfileExpectation expectation = expectedProfiles[itemId];
            AssertStringNameEq(profile.weapon_type_id, expectation.WeaponTypeId.ToString(), $"{itemId} profile type.");
            AssertIntSeqEq(DiceToList(profile.one_handed_dice), expectation.OneHandedDice, $"{itemId} one-handed dice.");
            AssertIntSeqEq(DiceToList(profile.two_handed_dice), expectation.TwoHandedDice, $"{itemId} two-handed dice.");
            AssertStringNameSeqEq(profile.GetPropertiesTyped(), expectation.Properties, $"{itemId} weapon properties.");
        }

        _test.True(coveredMeleeWeaponCount >= expectedWeaponTags.Count, "Melee weapon seeds should be covered by damage-tag checks.");
    }

    private void TestEquipmentServiceMovesItemsBetweenWarehouseAndSlots()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.AddItemTyped("bronze_sword", 1);
        warehouseService.AddItemTyped("leather_cap", 1);
        warehouseService.AddItemTyped("scout_charm", 1);
        List<string> charmInstanceIds = GetInstanceIdsForItem(partyState, "scout_charm");
        _test.Eq(charmInstanceIds.Count, 1, "Precondition: one charm should generate one instance id.");
        if (charmInstanceIds.Count < 1)
            return;

        var swordResult = equipmentService.EquipItemTyped("hero", "bronze_sword");
        _test.True(swordResult.Success, "Sword from warehouse should equip.");
        AssertStringEq(swordResult.SlotId.ToString(), "main_hand", "Weapon should enter main hand.");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 0, "Equipping sword should remove warehouse item.");

        var capResult = equipmentService.EquipItemTyped("hero", "leather_cap");
        _test.True(capResult.Success, "Head armor should equip.");
        AssertStringEq(capResult.SlotId.ToString(), "head", "Head armor should enter head slot.");
        _test.Eq(warehouseService.CountItem("leather_cap"), 0, "Equipping head armor should remove warehouse item.");

        var charmResult = equipmentService.EquipItemTyped("hero", "scout_charm", "", charmInstanceIds[0]);
        _test.True(charmResult.Success, "Accessory should equip.");
        AssertStringEq(charmResult.SlotId.ToString(), "necklace", "Accessory should prefer necklace slot.");
        _test.Eq(warehouseService.CountItem("scout_charm"), 0, "Equipping accessory should remove warehouse item.");

        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        AssertStringNameEq(equipmentState.GetEquippedItemId("main_hand"), "bronze_sword", "Main hand should record sword.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("head"), "leather_cap", "Head slot should record cap.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("necklace"), "scout_charm", "Necklace should record charm.");

        var unequipResult = equipmentService.UnequipItemTyped("hero", "necklace");
        _test.True(unequipResult.Success, "Equipped charm should unequip.");
        _test.Eq(warehouseService.CountItem("scout_charm"), 1, "Unequipped charm should return to warehouse.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("necklace"), "", "Unequipped slot should clear.");
    }

    private void TestEquipmentModifiersChangeAttributeSnapshotAndRoundTrip()
    {
        GDictionary itemDefs = ItemDefs();
        ProgressionContentRegistry progressionRegistry = new();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);

        CharacterManagementModule baselineManager = new();
        baselineManager.setup(
            partyState,
            SkillDefs(progressionRegistry),
            ProfessionDefs(progressionRegistry),
            AchievementDefs(progressionRegistry),
            itemDefs
        );
        AttributeSnapshot beforeSnapshot = baselineManager.GetMemberAttributeSnapshot("hero");

        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.AddItemTyped("bronze_sword", 1);
        warehouseService.AddItemTyped("leather_cap", 1);
        warehouseService.AddItemTyped("leather_jerkin", 1);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        equipmentService.EquipItemTyped("hero", "bronze_sword");
        equipmentService.EquipItemTyped("hero", "leather_cap");
        equipmentService.EquipItemTyped("hero", "leather_jerkin");

        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            SkillDefs(progressionRegistry),
            ProfessionDefs(progressionRegistry),
            AchievementDefs(progressionRegistry),
            itemDefs
        );
        AttributeSnapshot afterSnapshot = manager.GetMemberAttributeSnapshot("hero");

        _test.Eq(afterSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus)) - beforeSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus)), 2, "bronze_sword should add attack bonus.");
        _test.Eq(afterSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus)) - beforeSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus)), 3, "Armor pieces should add armor AC.");
        _test.Eq(afterSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)) - beforeSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)), 4, "Armor and dodge should increase AC.");
        _test.Eq(afterSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)) - beforeSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), 0, "leather_jerkin should not add HP.");
        _test.Eq(afterSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus)) - beforeSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.DodgeBonus)), 1, "leather_cap should add dodge.");

        PartyState restoredPartyState = PartyState.FromDictionary(partyState.ToDictionary());
        EquipmentState restoredEquipmentState = restoredPartyState.GetMemberState("hero").equipment_state;
        AssertStringNameEq(restoredEquipmentState.GetEquippedItemId("main_hand"), "bronze_sword", "Round-trip should preserve main hand.");
        AssertStringNameEq(restoredEquipmentState.GetEquippedItemId("head"), "leather_cap", "Round-trip should preserve head.");
        AssertStringNameEq(restoredEquipmentState.GetEquippedItemId("body"), "leather_jerkin", "Round-trip should preserve body.");
    }

    private void TestEquipmentStateRequiresCanonicalPayload()
    {
        EquipmentState legacyState = EquipmentState.FromDictionary(
            new GDictionary
            {
                ["main_hand"] = "bronze_sword",
                ["body"] = new GDictionary { ["item_id"] = "leather_jerkin" },
            }
        );
        _test.True(legacyState == null, "Legacy bare equipment_state dictionary should be rejected.");

        EquipmentState validState = EquipmentState.FromDictionary(
            new GDictionary
            {
                ["equipped_slots"] = new GDictionary
                {
                    ["main_hand"] = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_valid_bronze_sword", new GArray { "main_hand" }),
                },
            }
        );
        _test.True(validState != null, "Current equipped_slots payload should parse.");

        _test.True(
            EquipmentState.FromDictionary(new GDictionary { ["equipped_slots"] = new GDictionary(), ["legacy_equipped_items"] = new GDictionary() }) == null,
            "Extra top-level legacy equipment_state field should be rejected."
        );
        _test.True(
            EquipmentState.FromDictionary(new GDictionary { ["equipped_slots"] = new GDictionary { ["weapon"] = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_invalid_slot", new GArray { "main_hand" }) } }) == null,
            "Invalid slot key should reject equipment_state."
        );
        _test.True(
            EquipmentState.FromDictionary(new GDictionary { ["equipped_slots"] = new GDictionary { [new StringName("main_hand")] = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_string_name_slot_key", new GArray { "main_hand" }) } }) == null,
            "StringName slot key should reject equipment_state payload."
        );
        _test.True(
            EquipmentState.FromDictionary(new GDictionary { ["equipped_slots"] = new GDictionary { ["main_hand"] = new GDictionary { ["occupied_slot_ids"] = new GArray { "main_hand" } } } }) == null,
            "Bad entry payload should reject equipment_state."
        );
        _test.True(
            EquipmentState.FromDictionary(new GDictionary { ["equipped_slots"] = new GDictionary { ["main_hand"] = MakeEquipmentEntryPayload("scout_charm", "eq_schema_mismatched_slot", new GArray { "necklace" }) } }) == null,
            "Entry key must be present in occupied slots."
        );
        _test.True(
            EquipmentState.FromDictionary(
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
        _test.True(
            EquipmentEntryState.FromDictionary(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_entry_valid", new GArray { "main_hand" })) != null,
            "Current equipment entry payload should parse."
        );

        GDictionary missingInstancePayload = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_missing_instance", new GArray { "main_hand" });
        missingInstancePayload.Remove("equipment_instance");
        _test.True(EquipmentEntryState.FromDictionary(missingInstancePayload) == null, "Missing equipment_instance should reject entry.");

        GDictionary extraEntryPayload = MakeEquipmentEntryPayload("bronze_sword", "eq_schema_extra_entry", new GArray { "main_hand" });
        extraEntryPayload["legacy_item_id"] = "bronze_sword";
        _test.True(EquipmentEntryState.FromDictionary(extraEntryPayload) == null, "Extra legacy entry field should reject entry.");
        _test.True(EquipmentEntryState.FromDictionary(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_empty_slot", new GArray { "" })) == null, "Empty slot id should reject entry.");
        _test.True(EquipmentEntryState.FromDictionary(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_bad_slot", new GArray { "weapon" })) == null, "Invalid slot id should reject entry.");
        _test.True(EquipmentEntryState.FromDictionary(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_duplicate_slot", new GArray { "main_hand", "main_hand" })) == null, "Duplicate slot id should reject entry.");
        _test.True(EquipmentEntryState.FromDictionary(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_numeric_slot", new GArray { 123 })) == null, "Non-string slot id should reject entry.");
        _test.True(EquipmentEntryState.FromDictionary(MakeEquipmentEntryPayload("bronze_sword", "eq_schema_string_name_slot", new GArray { new StringName("main_hand") })) == null, "StringName slot id should reject entry.");
    }

    private void TestTwoHandedWeaponOccupiesBothSlots()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.AddItemTyped("iron_greatsword", 1);
        var result = equipmentService.EquipItemTyped("hero", "iron_greatsword");
        _test.True(result.Success, "iron_greatsword should equip.");
        AssertStringEq(result.SlotId.ToString(), "main_hand", "Greatsword entry slot should be main hand.");

        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        AssertStringNameEq(equipmentState.GetEquippedItemId("main_hand"), "iron_greatsword", "Main hand should record greatsword.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("off_hand"), "iron_greatsword", "Off hand should be occupied by greatsword.");
        _test.Eq(equipmentState.GetEquippedCount(), 1, "Two-handed weapon should count as one equipment entry.");
        _test.Eq(equipmentState.GetFilledSlotIdsTyped().Count, 2, "Two-handed weapon should fill two slots.");

        PartyState restored = PartyState.FromDictionary(partyState.ToDictionary());
        EquipmentState restoredEquipment = restored.GetMemberState("hero").equipment_state;
        AssertStringNameEq(restoredEquipment.GetEquippedItemId("main_hand"), "iron_greatsword", "Round-trip should preserve main hand greatsword.");
        AssertStringNameEq(restoredEquipment.GetEquippedItemId("off_hand"), "iron_greatsword", "Round-trip should preserve off hand occupancy.");
        _test.Eq(restoredEquipment.GetEquippedCount(), 1, "Round-trip two-handed weapon should count as one entry.");
    }

    private void TestTwoHandedWeaponDisplacesExistingMainAndOffHand()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        PartyMemberState heroState = partyState.GetMemberState("hero");
        EquipmentState equipmentState = heroState.equipment_state ?? new EquipmentState();
        heroState.equipment_state = equipmentState;
        equipmentState.SetEquippedEntry("main_hand", "bronze_sword", Names("main_hand"), EquipmentInstanceState.CreateInstance("bronze_sword", "eq_fixture_bronze_sword"));
        equipmentState.SetEquippedEntry("off_hand", "scout_charm", Names("off_hand"), EquipmentInstanceState.CreateInstance("scout_charm", "eq_fixture_scout_charm"));

        AssertStringNameEq(equipmentState.GetEquippedItemId("main_hand"), "bronze_sword", "Precondition: main hand should have sword.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("off_hand"), "scout_charm", "Precondition: off hand should have charm.");

        warehouseService.AddItemTyped("iron_greatsword", 1);
        var result = equipmentService.EquipItemTyped("hero", "iron_greatsword");
        _test.True(result.Success, "Two-handed replacement should succeed.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("main_hand"), "iron_greatsword", "Main hand should change to greatsword.");
        AssertStringNameEq(equipmentState.GetEquippedItemId("off_hand"), "iron_greatsword", "Off hand should be occupied by greatsword.");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 1, "Displaced sword should return to warehouse.");
        _test.Eq(warehouseService.CountItem("scout_charm"), 1, "Displaced charm should return to warehouse.");
    }

    private void TestTwoHandedWeaponAttributeNotDoubleCounted()
    {
        GDictionary itemDefs = ItemDefs();
        ProgressionContentRegistry progressionRegistry = new();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.AddItemTyped("iron_greatsword", 1);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        equipmentService.EquipItemTyped("hero", "iron_greatsword");

        CharacterManagementModule manager = new();
        manager.setup(partyState, SkillDefs(progressionRegistry), ProfessionDefs(progressionRegistry), new GDictionary(), itemDefs);
        AttributeSnapshot snapshot = manager.GetMemberAttributeSnapshot("hero");

        PartyState emptyParty = BuildPartyWithMember("blank", "Blank", 8);
        CharacterManagementModule emptyManager = new();
        emptyManager.setup(emptyParty, SkillDefs(progressionRegistry), ProfessionDefs(progressionRegistry), new GDictionary(), itemDefs);
        AttributeSnapshot emptySnapshot = emptyManager.GetMemberAttributeSnapshot("blank");

        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus)) - emptySnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus)),
            2,
            "Two-handed greatsword attack bonus should not double count."
        );
    }

    private void TestAtomicRollbackWhenWarehouseFull()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 1);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.AddItemTyped("iron_greatsword", 1);
        _test.Eq(warehouseService.GetFreeSlots(), 0, "Precondition: warehouse should be full.");

        GDictionary preview = warehouseService.PreviewBatchSwapTyped(
            Names("iron_greatsword"),
            Names("bronze_sword", "scout_charm")
        ).ToDictionary();
        _test.False(DictBool(preview, "allowed"), "Insufficient warehouse capacity should block batch swap.");
        AssertStringEq(DictString(preview, "error_code"), "warehouse_blocked_swap", "Blocked swap error code.");
        _test.Eq(warehouseService.CountItem("iron_greatsword"), 1, "Preview should not consume warehouse item.");
        _test.Eq(warehouseService.GetFreeSlots(), 0, "Preview should not change free slots.");
    }

    private void TestPreviewEquipReturnsDisplacedEntries()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.AddItemTyped("bronze_sword", 1);
        warehouseService.AddItemTyped("iron_greatsword", 1);
        equipmentService.EquipItemTyped("hero", "bronze_sword");

        var preview = equipmentService.PreviewEquipTyped(
            "hero",
            "iron_greatsword"
        );
        _test.True(preview.Success, "Valid replacement preview should succeed.");
        AssertStringEq(preview.EntrySlotId.ToString(), "main_hand", "Preview entry slot.");
        _test.Eq(preview.OccupiedSlotIds.Count, 2, "Preview should include two occupied slots.");
        _test.Eq(preview.DisplacedEntries.Count, 1, "Preview should report one displaced entry.");
        if (preview.DisplacedEntries.Count > 0)
            AssertStringEq(preview.DisplacedEntries[0].ItemId.ToString(), "bronze_sword", "Displaced entry should be bronze_sword.");

        AssertStringNameEq(partyState.GetMemberState("hero").equipment_state.GetEquippedItemId("main_hand"), "bronze_sword", "Preview should not mutate equipment state.");
        _test.Eq(warehouseService.CountItem("iron_greatsword"), 1, "Preview should not consume greatsword.");
    }

    private void TestArmorMaxDexBonusCapsPositiveAgilityAc()
    {
        GDictionary itemDefs = ItemDefs();
        ProgressionContentRegistry progressionRegistry = new();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        partyState.GetMemberState("hero").progression.unit_base_attributes.SetAttributeValue("agility", 18);

        CharacterManagementModule baselineManager = new();
        baselineManager.setup(partyState, SkillDefs(progressionRegistry), ProfessionDefs(progressionRegistry), AchievementDefs(progressionRegistry), itemDefs);
        AttributeSnapshot baselineSnapshot = baselineManager.GetMemberAttributeSnapshot("hero");
        _test.Eq(baselineSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)), 12, "Agility 18 without armor should produce AC 12.");
        _test.Eq(baselineSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus)), -1, "No armor should leave max dex at -1.");

        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        warehouseService.AddItemTyped("leather_jerkin", 1);
        warehouseService.AddItemTyped("iron_scale_mail", 1);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        var leatherResult = equipmentService.EquipItemTyped("hero", "leather_jerkin");
        _test.True(leatherResult.Success, "leather_jerkin should equip.");
        CharacterManagementModule leatherManager = new();
        leatherManager.setup(partyState, SkillDefs(progressionRegistry), ProfessionDefs(progressionRegistry), AchievementDefs(progressionRegistry), itemDefs);
        AttributeSnapshot leatherSnapshot = leatherManager.GetMemberAttributeSnapshot("hero");
        _test.Eq(leatherSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus)), 6, "leather_jerkin max dex.");
        _test.Eq(leatherSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)), 14, "leather_jerkin should not cap agility 18.");

        var scaleResult = equipmentService.EquipItemTyped("hero", "iron_scale_mail");
        _test.True(scaleResult.Success, "iron_scale_mail should replace body armor.");
        CharacterManagementModule scaleManager = new();
        scaleManager.setup(partyState, SkillDefs(progressionRegistry), ProfessionDefs(progressionRegistry), AchievementDefs(progressionRegistry), itemDefs);
        AttributeSnapshot scaleSnapshot = scaleManager.GetMemberAttributeSnapshot("hero");
        _test.Eq(scaleSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus)), 3, "iron_scale_mail max dex.");
        _test.Eq(scaleSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)), 15, "iron_scale_mail should cap agility AC to +3.");
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
        patchedWarehouse.AddItemTyped("bronze_sword", 1);

        var result = patchedEquipmentService.EquipItemTyped("hero", "bronze_sword");
        _test.False(result.Success, "Profession requirement should block equip.");
        AssertStringEq(result.ErrorCode, "missing_profession", "Missing profession error code.");
        _test.Eq(patchedWarehouse.CountItem("bronze_sword"), 1, "Blocked equip should not consume warehouse item.");

        var preview = patchedEquipmentService.PreviewEquipTyped(
            "hero",
            "bronze_sword"
        );
        _test.False(preview.Success, "Preview should also fail requirement.");
        AssertStringEq(preview.ErrorCode, "missing_profession", "Preview error code.");
        _test.True(preview.Blockers.Contains("missing_profession"), "Preview blockers should contain missing_profession.");
    }

    private void TestEquipCreatesInstanceIdInSlot()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.AddItemTyped("bronze_sword", 1);
        equipmentService.EquipItemTyped("hero", "bronze_sword");
        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        StringName instanceId = equipmentState.GetEquippedInstanceId("main_hand");
        _test.False(instanceId == "", "Equipped main hand should have instance id.");
        _test.True(instanceId.ToString().StartsWith("eq_"), "Instance id should start with eq_.");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 0, "Warehouse should no longer contain equipped sword.");

        PartyState restored = PartyState.FromDictionary(partyState.ToDictionary());
        AssertStringNameEq(restored.GetMemberState("hero").equipment_state.GetEquippedInstanceId("main_hand"), instanceId.ToString(), "Round-trip should preserve instance id.");
    }

    private void TestInstanceIdPreservedThroughUnequipAndReequip()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.AddItemTyped("bronze_sword", 1);
        equipmentService.EquipItemTyped("hero", "bronze_sword");
        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        StringName originalInstanceId = equipmentState.GetEquippedInstanceId("main_hand");
        _test.False(originalInstanceId == "", "Precondition: equipped sword should have instance id.");

        equipmentService.UnequipItemTyped("hero", "main_hand");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 1, "Unequipped item should return to warehouse.");
        _test.True(HasInstanceId(partyState, originalInstanceId), "Warehouse should contain original instance id after unequip.");

        equipmentService.EquipItemTyped("hero", "bronze_sword");
        AssertStringNameEq(equipmentState.GetEquippedInstanceId("main_hand"), originalInstanceId.ToString(), "Reequip should preserve original instance id.");
    }

    private void TestTwoItemsOfSameTypeGetDifferentInstanceIds()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        warehouseService.AddItemTyped("scout_charm", 1);
        List<string> charmInstanceIds = GetInstanceIdsForItem(partyState, "scout_charm");
        _test.Eq(charmInstanceIds.Count, 1, "Precondition: charm should generate one instance id.");
        if (charmInstanceIds.Count < 1)
            return;
        equipmentService.EquipItemTyped("hero", "scout_charm", "", charmInstanceIds[0]);
        _test.False(partyState.GetMemberState("hero").equipment_state.GetEquippedInstanceId("necklace") == "", "Necklace slot should have instance id.");
    }

    private void TestWeaponProfileEquipmentEntryRoundTrip()
    {
        GDictionary itemDefs = ItemDefs();
        ItemDef bronzeSword = GetItemDef(itemDefs, "bronze_sword");
        _test.True(bronzeSword != null, "bronze_sword should load.");
        if (bronzeSword == null)
            return;
        _test.True(bronzeSword.weapon_profile is WeaponProfileDef, "bronze_sword should have weapon_profile.");
        _test.Eq(bronzeSword.GetWeaponAttackRange(), 1, "weapon_profile should provide attack range.");
        AssertStringNameEq(bronzeSword.GetWeaponPhysicalDamageTag(), "physical_pierce", "weapon_profile should provide damage tag.");

        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);
        warehouseService.AddItemTyped("bronze_sword", 1);
        var equipResult = equipmentService.EquipItemTyped("hero", "bronze_sword");
        _test.True(equipResult.Success, "weapon_profile weapon should equip.");
        _test.Eq(warehouseService.CountItem("bronze_sword"), 0, "Equipped weapon_profile weapon should leave warehouse.");

        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        StringName instanceId = equipmentState.GetEquippedInstanceId("main_hand");
        _test.False(instanceId == "", "Equipped weapon_profile weapon should have instance id.");
        GDictionary equipmentPayload = equipmentState.ToDictionary();
        GDictionary slotPayload = equipmentPayload["equipped_slots"].AsGodotDictionary()["main_hand"].AsGodotDictionary();
        GDictionary slotInstancePayload = slotPayload["equipment_instance"].AsGodotDictionary();
        AssertStringEq(DictString(slotInstancePayload, "item_id"), "bronze_sword", "Entry payload should store item_id in equipment_instance.");
        AssertStringEq(DictString(slotInstancePayload, "instance_id"), instanceId.ToString(), "Entry payload should store same instance_id.");
        _test.False(slotPayload.ContainsKey("item_id"), "Entry payload should not keep top-level item_id.");
        _test.False(slotPayload.ContainsKey("instance_id"), "Entry payload should not keep top-level instance_id.");
        _test.False(slotPayload.ContainsKey("weapon_profile"), "Entry payload should not serialize weapon_profile.");
        _test.False(slotPayload.ContainsKey("weapon_attack_range"), "Entry payload should not serialize legacy weapon_attack_range.");
        _test.False(slotPayload.ContainsKey("weapon_physical_damage_tag"), "Entry payload should not serialize legacy damage tag.");

        PartyState restoredPartyState = PartyState.FromDictionary(partyState.ToDictionary());
        _test.True(restoredPartyState != null, "weapon_profile weapon PartyState round-trip should parse.");
        if (restoredPartyState == null)
            return;
        EquipmentState restoredEquipmentState = restoredPartyState.GetMemberState("hero").equipment_state;
        AssertStringNameEq(restoredEquipmentState.GetEquippedItemId("main_hand"), "bronze_sword", "Round-trip should preserve item id.");
        AssertStringNameEq(restoredEquipmentState.GetEquippedInstanceId("main_hand"), instanceId.ToString(), "Round-trip should preserve instance id.");

        PartyWarehouseService restoredWarehouse = BuildWarehouseService(restoredPartyState, itemDefs);
        PartyEquipmentService restoredEquipmentService = BuildEquipmentService(restoredPartyState, itemDefs, restoredWarehouse);
        var unequipResult = restoredEquipmentService.UnequipItemTyped("hero", "main_hand");
        _test.True(unequipResult.Success, "Round-tripped weapon_profile weapon should unequip.");
        _test.Eq(restoredWarehouse.CountItem("bronze_sword"), 1, "Unequipped weapon_profile weapon should return to warehouse.");
        var restoredInstances = restoredPartyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped();
        _test.Eq(restoredInstances.Count, 1, "Warehouse should contain one weapon_profile instance after unequip.");
        if (restoredInstances.Count > 0)
        {
            GDictionary instancePayload = restoredInstances[0].ToDictionary();
            AssertStringEq(DictString(instancePayload, "instance_id"), instanceId.ToString(), "Returned instance should preserve instance id.");
            _test.False(instancePayload.ContainsKey("weapon_profile"), "Equipment instance payload should not serialize weapon_profile.");
            _test.False(instancePayload.ContainsKey("weapon_attack_range"), "Equipment instance payload should not write weapon_attack_range.");
            _test.False(instancePayload.ContainsKey("weapon_physical_damage_tag"), "Equipment instance payload should not write damage tag.");
        }
    }

    private void TestEquippedInstanceFieldsSurviveRoundTripAndUnequip()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        EquipmentInstanceState epicInstance = EquipmentInstanceState.CreateInstance("bronze_sword", "eq_epic_equipped_bronze_sword");
        epicInstance.rarity = (int)EquipmentInstanceState.RarityTier.EPIC;
        epicInstance.current_durability = 17;
        partyState.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState> { epicInstance };

        var equipResult = equipmentService.EquipItemTyped("hero", "bronze_sword");
        _test.True(equipResult.Success, "Equipment with full instance fields should equip.");
        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        AssertEquipmentInstanceFields(equipmentState.GetEquippedInstance("main_hand"), "eq_epic_equipped_bronze_sword", (int)EquipmentInstanceState.RarityTier.EPIC, 17, "Equipped slot");

        PartyState restoredPartyState = PartyState.FromDictionary(partyState.ToDictionary());
        _test.True(restoredPartyState != null, "Full equipment instance fields should round-trip.");
        if (restoredPartyState == null)
            return;
        PartyWarehouseService restoredWarehouseService = BuildWarehouseService(restoredPartyState, itemDefs);
        PartyEquipmentService restoredEquipmentService = BuildEquipmentService(restoredPartyState, itemDefs, restoredWarehouseService);
        EquipmentState restoredEquipmentState = restoredPartyState.GetMemberState("hero").equipment_state;
        AssertEquipmentInstanceFields(restoredEquipmentState.GetEquippedInstance("main_hand"), "eq_epic_equipped_bronze_sword", (int)EquipmentInstanceState.RarityTier.EPIC, 17, "Round-tripped equipped slot");

        var unequipResult = restoredEquipmentService.UnequipItemTyped("hero", "main_hand");
        _test.True(unequipResult.Success, "Full instance equipment should unequip.");
        var restoredInstances = restoredPartyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped();
        _test.Eq(restoredInstances.Count, 1, "Unequipped full instance should return to warehouse.");
        if (restoredInstances.Count > 0)
            AssertEquipmentInstanceFields(restoredInstances[0], "eq_epic_equipped_bronze_sword", (int)EquipmentInstanceState.RarityTier.EPIC, 17, "Returned full instance");
    }

    private void TestEquipmentInstanceRarityRoundTripAndStrictSchema()
    {
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        EquipmentInstanceState epicInstance = EquipmentInstanceState.CreateInstance("bronze_sword", "eq_epic_bronze_sword");
        epicInstance.rarity = (int)EquipmentInstanceState.RarityTier.EPIC;
        epicInstance.current_durability = DefaultCurrentDurabilityForRarity(epicInstance.rarity);
        partyState.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState> { epicInstance };

        PartyState restoredPartyState = PartyState.FromDictionary(partyState.ToDictionary());
        _test.True(restoredPartyState != null, "Rarity PartyState round-trip should parse.");
        if (restoredPartyState == null)
            return;
        var restoredInstances = restoredPartyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped();
        _test.Eq(restoredInstances.Count, 1, "Rarity round-trip should preserve one instance.");
        if (restoredInstances.Count > 0)
            _test.Eq(restoredInstances[0].rarity, (int)EquipmentInstanceState.RarityTier.EPIC, "Rarity tier should persist.");

        string missingRarityError = EquipmentInstanceState.GetPayloadValidationError(
            new GDictionary
            {
                ["instance_id"] = "eq_missing_rarity_bronze_sword",
                ["item_id"] = "bronze_sword",
                ["current_durability"] = DefaultCurrentDurabilityForRarity((int)EquipmentInstanceState.RarityTier.COMMON),
            },
            false
        );
        _test.True(!string.IsNullOrEmpty(missingRarityError), "Missing rarity should report validation error.");

        string invalidRarityError = EquipmentInstanceState.GetPayloadValidationError(
            new GDictionary
            {
                ["instance_id"] = "eq_invalid_rarity_bronze_sword",
                ["item_id"] = "bronze_sword",
                ["rarity"] = 999,
                ["current_durability"] = DefaultCurrentDurabilityForRarity((int)EquipmentInstanceState.RarityTier.COMMON),
            },
            false
        );
        _test.True(!string.IsNullOrEmpty(invalidRarityError), "Invalid rarity should report validation error.");

        GDictionary invalidInstanceIdPayload = MakeEquipmentInstancePayload("eq_schema_invalid_instance_id");
        invalidInstanceIdPayload["instance_id"] = 17;
        AssertEquipmentInstanceValidationError(invalidInstanceIdPayload, "Numeric instance_id should be rejected.");

        GDictionary invalidItemIdPayload = MakeEquipmentInstancePayload("eq_schema_invalid_item_id");
        invalidItemIdPayload["item_id"] = 17;
        AssertEquipmentInstanceValidationError(invalidItemIdPayload, "Numeric item_id should be rejected.");

        GDictionary stringNameInstanceIdPayload = MakeEquipmentInstancePayload("eq_schema_string_name_instance_id");
        stringNameInstanceIdPayload["instance_id"] = new StringName("eq_schema_string_name_instance_id");
        AssertEquipmentInstanceValidationError(stringNameInstanceIdPayload, "StringName instance_id should be rejected.");

        GDictionary stringNameItemIdPayload = MakeEquipmentInstancePayload("eq_schema_string_name_item_id");
        stringNameItemIdPayload["item_id"] = new StringName("bronze_sword");
        AssertEquipmentInstanceValidationError(stringNameItemIdPayload, "StringName item_id should be rejected.");

        GDictionary stringRarityPayload = MakeEquipmentInstancePayload("eq_schema_string_rarity");
        stringRarityPayload["rarity"] = "3";
        AssertEquipmentInstanceValidationError(stringRarityPayload, "String rarity should be rejected.");

        GDictionary stringDurabilityPayload = MakeEquipmentInstancePayload("eq_schema_string_durability");
        stringDurabilityPayload["current_durability"] = "17";
        AssertEquipmentInstanceValidationError(stringDurabilityPayload, "String durability should be rejected.");

        GDictionary zeroDurabilityPayload = MakeEquipmentInstancePayload("eq_schema_zero_durability");
        zeroDurabilityPayload["current_durability"] = 0;
        AssertEquipmentInstanceValidationError(zeroDurabilityPayload, "Zero durability should be rejected.");
    }

    private void TestDuplicateSameItemInstanceIdSelection()
    {
        GDictionary itemDefs = ItemDefs();
        PartyState partyState = BuildPartyWithMember("hero", "Hero", 8);
        PartyWarehouseService warehouseService = BuildWarehouseService(partyState, itemDefs);
        PartyEquipmentService equipmentService = BuildEquipmentService(partyState, itemDefs, warehouseService);

        EquipmentInstanceState commonInstance = EquipmentInstanceState.CreateInstance("bronze_sword", "eq_duplicate_common_sword");
        commonInstance.rarity = (int)EquipmentInstanceState.RarityTier.COMMON;
        commonInstance.current_durability = 11;
        EquipmentInstanceState rareInstance = EquipmentInstanceState.CreateInstance("bronze_sword", "eq_duplicate_rare_sword");
        rareInstance.rarity = (int)EquipmentInstanceState.RarityTier.RARE;
        rareInstance.current_durability = 23;
        EquipmentInstanceState mismatchInstance = EquipmentInstanceState.CreateInstance("scout_charm", "eq_duplicate_wrong_item");
        partyState.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
        {
            commonInstance,
            rareInstance,
            mismatchInstance,
        };

        var itemOnlyResult = equipmentService.EquipItemTyped("hero", "bronze_sword");
        _test.False(itemOnlyResult.Success, "Duplicate item-id-only equip should fail.");
        AssertStringEq(itemOnlyResult.ErrorCode, "equipment_instance_id_required", "Duplicate item-id-only equip error.");

        var mismatchResult = equipmentService.EquipItemTyped("hero", "bronze_sword", "", "eq_duplicate_wrong_item");
        _test.False(mismatchResult.Success, "Mismatched instance id should fail.");
        AssertStringEq(mismatchResult.ErrorCode, "equipment_instance_item_mismatch", "Mismatched instance id error.");

        var missingResult = equipmentService.EquipItemTyped("hero", "bronze_sword", "", "eq_duplicate_missing");
        _test.False(missingResult.Success, "Missing instance id should fail.");
        AssertStringEq(missingResult.ErrorCode, "warehouse_missing_instance", "Missing instance id error.");

        var equipResult = equipmentService.EquipItemTyped("hero", "bronze_sword", "", "eq_duplicate_rare_sword");
        _test.True(equipResult.Success, "Explicit rare instance id should equip.");
        EquipmentInstanceState equippedInstance = partyState.GetMemberState("hero").equipment_state.GetEquippedInstance("main_hand");
        AssertEquipmentInstanceFields(equippedInstance, "eq_duplicate_rare_sword", (int)EquipmentInstanceState.RarityTier.RARE, 23, "Explicit instance equip");
        _test.True(warehouseService.HasEquipmentInstance("eq_duplicate_common_sword", "bronze_sword"), "Unselected common instance should remain in warehouse.");
        _test.False(warehouseService.HasEquipmentInstance("eq_duplicate_rare_sword", "bronze_sword"), "Equipped rare instance should leave warehouse.");

        PartyState restoredPartyState = PartyState.FromDictionary(partyState.ToDictionary());
        _test.True(restoredPartyState != null, "Explicit duplicate instance equip should round-trip.");
        if (restoredPartyState == null)
            return;
        PartyWarehouseService restoredWarehouseService = BuildWarehouseService(restoredPartyState, itemDefs);
        PartyEquipmentService restoredEquipmentService = BuildEquipmentService(restoredPartyState, itemDefs, restoredWarehouseService);
        AssertEquipmentInstanceFields(restoredPartyState.GetMemberState("hero").equipment_state.GetEquippedInstance("main_hand"), "eq_duplicate_rare_sword", (int)EquipmentInstanceState.RarityTier.RARE, 23, "Round-tripped explicit instance equip");
        var unequipResult = restoredEquipmentService.UnequipItemTyped("hero", "main_hand");
        _test.True(unequipResult.Success, "Round-tripped explicit instance should unequip.");
        _test.True(restoredWarehouseService.HasEquipmentInstance("eq_duplicate_common_sword", "bronze_sword"), "Common instance should still be in warehouse after unequip.");
        _test.True(restoredWarehouseService.HasEquipmentInstance("eq_duplicate_rare_sword", "bronze_sword"), "Rare instance should return to warehouse after unequip.");
        AssertEquipmentInstanceFields(restoredWarehouseService.GetEquipmentInstanceById("eq_duplicate_rare_sword", "bronze_sword"), "eq_duplicate_rare_sword", (int)EquipmentInstanceState.RarityTier.RARE, 23, "Returned explicit instance");
    }

    private void TestPartyStateRejectsDuplicateEquipmentInstanceIds()
    {
        PartyState warehouseDuplicateParty = BuildPartyWithMember("hero", "Hero", 8);
        warehouseDuplicateParty.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
        {
            EquipmentInstanceState.CreateInstance("bronze_sword", "eq_party_duplicate"),
            EquipmentInstanceState.CreateInstance("scout_charm", "eq_party_duplicate"),
        };
        _test.True(PartyState.FromDictionary(warehouseDuplicateParty.ToDictionary()) == null, "Duplicate warehouse instance ids should reject PartyState payload.");

        PartyState warehouseAndEquippedParty = BuildPartyWithMember("hero", "Hero", 8);
        warehouseAndEquippedParty.warehouse_state.equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
        {
            EquipmentInstanceState.CreateInstance("bronze_sword", "eq_party_shared"),
        };
        warehouseAndEquippedParty.GetMemberState("hero").equipment_state.SetEquippedEntry(
            "main_hand",
            "bronze_sword",
            Names("main_hand"),
            EquipmentInstanceState.CreateInstance("bronze_sword", "eq_party_shared")
        );
        _test.True(PartyState.FromDictionary(warehouseAndEquippedParty.ToDictionary()) == null, "Instance id shared by warehouse and equipment should reject PartyState payload.");

        PartyState sameMemberDuplicateParty = BuildPartyWithMember("hero", "Hero", 8);
        EquipmentState sameMemberEquipment = sameMemberDuplicateParty.GetMemberState("hero").equipment_state;
        sameMemberEquipment.SetEquippedEntry("main_hand", "bronze_sword", Names("main_hand"), EquipmentInstanceState.CreateInstance("bronze_sword", "eq_party_same_member"));
        sameMemberEquipment.SetEquippedEntry("necklace", "scout_charm", Names("necklace"), EquipmentInstanceState.CreateInstance("scout_charm", "eq_party_same_member"));
        _test.True(PartyState.FromDictionary(sameMemberDuplicateParty.ToDictionary()) == null, "Same member duplicate equipped instance id should reject PartyState payload.");

        PartyState crossMemberDuplicateParty = BuildPartyWithMember("hero", "Hero", 8);
        PartyMemberState ally = new()
        {
            member_id = "ally",
            display_name = "Ally",
            progression = new UnitProgress(),
        };
        ally.progression.unit_id = ally.member_id;
        ally.progression.display_name = ally.display_name;
        crossMemberDuplicateParty.SetMemberState(ally);
        crossMemberDuplicateParty.reserve_member_ids = Names("ally");
        crossMemberDuplicateParty.GetMemberState("hero").equipment_state.SetEquippedEntry("main_hand", "bronze_sword", Names("main_hand"), EquipmentInstanceState.CreateInstance("bronze_sword", "eq_party_cross_member"));
        crossMemberDuplicateParty.GetMemberState("ally").equipment_state.SetEquippedEntry("necklace", "scout_charm", Names("necklace"), EquipmentInstanceState.CreateInstance("scout_charm", "eq_party_cross_member"));
        _test.True(PartyState.FromDictionary(crossMemberDuplicateParty.ToDictionary()) == null, "Cross-member duplicate equipped instance id should reject PartyState payload.");
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
        unitBaseAttributes.SetAttributeValue("strength", 4);
        unitBaseAttributes.SetAttributeValue("agility", 3);
        unitBaseAttributes.SetAttributeValue("constitution", 4);
        unitBaseAttributes.SetAttributeValue("perception", 3);
        unitBaseAttributes.SetAttributeValue("intelligence", 2);
        unitBaseAttributes.SetAttributeValue("willpower", 2);
        unitBaseAttributes.custom_stats[PartyWarehouseService.StorageSpaceAttributeId] = storageSpace;
        memberState.progression.unit_base_attributes = unitBaseAttributes;

        partyState.SetMemberState(memberState);
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
            ["rarity"] = (int)EquipmentInstanceState.RarityTier.COMMON,
            ["current_durability"] = DefaultCurrentDurabilityForRarity((int)EquipmentInstanceState.RarityTier.COMMON),
        };

    private static GDictionary MakeEquipmentEntryPayload(
        StringName itemId,
        StringName instanceId,
        GArray occupiedSlotIds
    )
    {
        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(itemId, instanceId);
        return new GDictionary
        {
            ["occupied_slot_ids"] = occupiedSlotIds,
            ["equipment_instance"] = instance.ToDictionary(),
        };
    }

    private static int DefaultCurrentDurabilityForRarity(int rarity)
    {
        if (rarity == (int)EquipmentInstanceState.RarityTier.UNCOMMON)
            return 84;
        if (rarity == (int)EquipmentInstanceState.RarityTier.RARE)
            return 120;
        if (rarity == (int)EquipmentInstanceState.RarityTier.EPIC)
            return 160;
        if (rarity == (int)EquipmentInstanceState.RarityTier.LEGENDARY)
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
        foreach (EquipmentInstanceState instance in partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped())
            if (instance != null && instance.item_id == itemId)
                result.Add(instance.instance_id.ToString());
        result.Sort(System.StringComparer.Ordinal);
        return result;
    }

    private static bool HasInstanceId(PartyState partyState, StringName instanceId)
    {
        foreach (EquipmentInstanceState instance in partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped())
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
        _test.True(instance != null, $"{messagePrefix}: instance should not be null.");
        if (instance == null)
            return;
        AssertStringNameEq(instance.instance_id, expectedInstanceId, $"{messagePrefix}: instance_id.");
        _test.Eq(instance.rarity, expectedRarity, $"{messagePrefix}: rarity.");
        _test.Eq(instance.current_durability, expectedCurrentDurability, $"{messagePrefix}: current_durability.");
    }

    private void AssertEquipmentInstanceValidationError(
        GDictionary payload,
        string message
    )
    {
        string validationError = EquipmentInstanceState.GetPayloadValidationError(payload, false);
        _test.True(!string.IsNullOrEmpty(validationError), message);
    }

    private static GDictionary ItemDefs() => ProjectItemDefs(new ItemContentRegistry().GetItemDefsTyped());

    private static GDictionary SkillDefs(ProgressionContentRegistry registry) =>
        ProjectSkillDefs(registry.GetSkillDefsTyped());

    private static GDictionary ProfessionDefs(ProgressionContentRegistry registry) =>
        ProjectProfessionDefs(registry.GetProfessionDefsTyped());

    private static GDictionary AchievementDefs(ProgressionContentRegistry registry) =>
        ProjectAchievementDefs(registry.GetAchievementDefsTyped());

    private static GDictionary ProjectItemDefs(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null)
                continue;
            result[itemId] = itemDef;
        }
        return result;
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

    private static GDictionary ProjectProfessionDefs(IReadOnlyDictionary<StringName, ProfessionDef> professionDefs)
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

    private static GDictionary ProjectAchievementDefs(IReadOnlyDictionary<StringName, AchievementDef> achievementDefs)
    {
        GDictionary result = new();
        if (achievementDefs == null)
            return result;
        foreach ((StringName achievementId, AchievementDef achievementDef) in achievementDefs)
        {
            if (achievementId == "" || achievementDef == null)
                continue;
            result[achievementId] = achievementDef;
        }
        return result;
    }

    private static ItemDef GetItemDef(GDictionary itemDefs, StringName itemId)
    {
        if (itemDefs == null || !itemDefs.ContainsKey(itemId))
            return null;
        return itemDefs[itemId].AsGodotObject() as ItemDef;
    }

    private static PartyWarehouseService BuildWarehouseService(PartyState partyState, GDictionary itemDefs)
    {
        PartyWarehouseService warehouseService = new();
        warehouseService.Setup(partyState, BuildItemDefIndex(itemDefs));
        return warehouseService;
    }

    private static PartyEquipmentService BuildEquipmentService(
        PartyState partyState,
        GDictionary itemDefs,
        PartyWarehouseService warehouseService
    )
    {
        PartyEquipmentService equipmentService = new();
        equipmentService.Setup(partyState, BuildItemDefIndex(itemDefs), warehouseService);
        return equipmentService;
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
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

    private void AssertStringEq(string actual, string expected, string message)
    {
        if (actual != expected)
            _test.Fail($"{message} | actual={actual} expected={expected}");
    }

    private void AssertStringNameEq(StringName actual, string expected, string message)
    {
        if (actual.ToString() != expected)
            _test.Fail($"{message} | actual={actual} expected={expected}");
    }

    private void AssertStringNameSeqEq(IEnumerable<StringName> actual, IReadOnlyList<StringName> expected, string message)
    {
        if (!StringNameSeqEquals(actual, expected))
            _test.Fail($"{message} | actual={FormatStringNames(actual)} expected={FormatStringNames(expected)}");
    }

    private void AssertIntSeqEq(IReadOnlyList<int> actual, IReadOnlyList<int> expected, string message)
    {
        bool equal = actual.Count == expected.Count;
        for (int index = 0; equal && index < actual.Count; index++)
            equal = actual[index] == expected[index];
        if (!equal)
            _test.Fail($"{message} | actual=[{string.Join(", ", actual)}] expected=[{string.Join(", ", expected)}]");
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

}
