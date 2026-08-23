#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public partial class run_item_validator_diagnostic_golden_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly ItemImportModelValidator _validator = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        try
        {
            TestPureClrClosedValueParity();
            TestPositiveCorpus();
            TestPerRuleGoldenCorpus();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected item validator golden exception: {exception}");
        }
        RequestTestExit(_test.Finish("Item validator diagnostic golden regression"));
    }

    private void TestPureClrClosedValueParity()
    {
        string[] runtimeSlots = EquipmentRules.GetAllSlotIdsTyped()
            .Select(value => value.ToString())
            .ToArray();
        _test.True(
            runtimeSlots.SequenceEqual(ItemImportValueRules.EquipmentSlotValues),
            "pure CLR item equipment-slot vocabulary must match runtime EquipmentRules"
        );
        _test.Eq(
            ItemImportValueRules.WeaponDiceCountMax,
            WeaponDamageDiceDefinition.DiceCountMax,
            "pure CLR dice_count max must match runtime definition"
        );
        _test.Eq(
            ItemImportValueRules.WeaponDiceSidesMax,
            WeaponDamageDiceDefinition.DiceSidesMax,
            "pure CLR dice_sides max must match runtime definition"
        );
        _test.Eq(
            ItemImportValueRules.WeaponDiceFlatBonusMin,
            WeaponDamageDiceDefinition.FlatBonusMin,
            "pure CLR flat_bonus min must match runtime definition"
        );
        _test.Eq(
            ItemImportValueRules.WeaponDiceFlatBonusMax,
            WeaponDamageDiceDefinition.FlatBonusMax,
            "pure CLR flat_bonus max must match runtime definition"
        );
    }

    private void TestPositiveCorpus()
    {
        var positives = new[]
        {
            ItemBuilder.Misc("valid_misc").Build(),
            ItemBuilder.Misc("valid_material").WithTag("material").WithCraftingGroup("ore").Build(),
            ItemBuilder.Misc("valid_quest").WithTag("quest_item").WithQuestGroup("quest").Build(),
            ItemBuilder.SkillBook("valid_book", "mage_arcane_missile").Build(),
            ItemBuilder.Equipment("valid_armor", "armor").Build(),
            ItemBuilder.Weapon("valid_weapon").Build(),
        };

        for (int index = 0; index < positives.Length; index += 1)
        {
            ItemImportModel item = positives[index];
            IReadOnlyList<ContentJsonDiagnostic> diagnostics = _validator.ValidateDomainLocal(
                Context(item.ItemId, index),
                item
            );
            _test.Eq(
                diagnostics.Count,
                0,
                $"positive item golden {item.ItemId} should pass: {Format(diagnostics)}"
            );
        }
    }

    private void TestPerRuleGoldenCorpus()
    {
        GoldenCase[] cases = BuildGoldenCases();
        _test.Eq(
            cases.Length,
            ItemImportModelValidator.RuleInventory.Count,
            "golden corpus must contain exactly one case per validator rule"
        );
        _test.True(
            cases.Select(value => value.RuleId).SequenceEqual(ItemImportModelValidator.RuleInventory),
            "golden corpus order and rule inventory must remain byte-stable"
        );

        for (int index = 0; index < cases.Length; index += 1)
        {
            GoldenCase golden = cases[index];
            IReadOnlyList<ContentJsonDiagnostic> diagnostics = _validator.ValidateDomainLocal(
                Context(golden.Item.ItemId, index),
                golden.Item
            );
            _test.Eq(
                diagnostics.Count,
                1,
                $"golden {golden.RuleId} should emit exactly one diagnostic: {Format(diagnostics)}"
            );
            if (diagnostics.Count != 1)
                continue;
            ContentJsonDiagnostic actual = diagnostics[0];
            _test.Eq(actual.RuleId, golden.RuleId, $"golden rule ID {golden.RuleId}");
            _test.Eq(
                actual.JsonPointer,
                $"/cases/{index}{golden.RelativePointer}",
                $"golden JSON pointer {golden.RuleId}"
            );
        }
    }

    private static GoldenCase[] BuildGoldenCases()
    {
        var invalidId = ItemBuilder.Misc("").Build();
        var iconPath = ItemBuilder.Misc("icon_path").WithIcon("res://icon.svg").Build();
        var category = ItemBuilder.Misc("category").WithCategory("unknown").Build();
        var maxStack = ItemBuilder.Misc("max_stack").WithStacking(true, 0).Build();
        var buyPrice = ItemBuilder.Misc("buy_price").WithPrices(10, 0, 5).Build();
        var sellPrice = ItemBuilder.Misc("sell_price").WithPrices(10, 20, 0).Build();
        var material = ItemBuilder.Misc("material").WithTag("material").Build();
        var quest = ItemBuilder.Misc("quest").WithTag("quest_item").Build();
        var book = ItemBuilder.SkillBook("book", "").Build();
        var stackableEquipment = ItemBuilder.Equipment("stackable_equipment", "armor")
            .WithStacking(true, 1).Build();
        var noSlot = ItemBuilder.Equipment("no_slot", "armor").WithSlots().Build();
        var invalidSlot = ItemBuilder.Equipment("invalid_slot", "armor").WithSlots("phantom_slot").Build();
        var invalidType = ItemBuilder.Equipment("invalid_type", "").Build();
        var noProfile = ItemBuilder.Weapon("no_profile").WithWeaponProfile(null).Build();
        var weaponType = ItemBuilder.Weapon("weapon_type").MutateProfile(profile => profile.WeaponTypeId = "").Build();
        var family = ItemBuilder.Weapon("family").MutateProfile(profile => profile.Family = "").Build();
        var rangeType = ItemBuilder.Weapon("range_type").MutateProfile(profile => profile.RangeType = "").Build();
        var damageRequired = ItemBuilder.Weapon("damage_required").MutateProfile(profile => profile.DamageTag = "").Build();
        var attackRange = ItemBuilder.Weapon("attack_range").MutateProfile(profile => profile.AttackRange = 0).Build();
        var diceRequired = ItemBuilder.Weapon("dice_required").MutateProfile(profile =>
        {
            profile.OneHandedDice = null;
            profile.TwoHandedDice = null;
        }).Build();
        var diceCount = ItemBuilder.Weapon("dice_count").MutateProfile(profile => profile.OneHandedDice!.DiceCount = 0).Build();
        var diceSides = ItemBuilder.Weapon("dice_sides").MutateProfile(profile => profile.OneHandedDice!.DiceSides = 0).Build();
        var flatBonus = ItemBuilder.Weapon("flat_bonus").MutateProfile(profile => profile.OneHandedDice!.FlatBonus = 1000).Build();
        var damageTag = ItemBuilder.Weapon("damage_tag").MutateProfile(profile => profile.DamageTag = "fire").Build();
        var attributeId = ItemBuilder.Equipment("attribute_id", "armor")
            .WithModifier("", "flat").Build();
        var attributeMode = ItemBuilder.Equipment("attribute_mode", "armor")
            .WithModifier("strength", "multiply").Build();
        var occupiedShape = ItemBuilder.Equipment("occupied_shape", "armor")
            .WithSlots("main_hand", "off_hand").WithOccupiedSlots("main_hand").Build();
        var occupiedInvalid = ItemBuilder.Equipment("occupied_invalid", "armor")
            .WithOccupiedSlots("main_hand", "phantom_slot").Build();
        var occupiedDuplicate = ItemBuilder.Equipment("occupied_duplicate", "armor")
            .WithOccupiedSlots("main_hand", "main_hand").Build();
        var occupiedMissing = ItemBuilder.Equipment("occupied_missing", "armor")
            .WithOccupiedSlots("off_hand").Build();

        return new[]
        {
            new GoldenCase(ItemImportModelValidator.ItemIdRequiredRule, "/item_id", invalidId),
            new GoldenCase(ItemImportModelValidator.IconAssetIdPathRule, "/icon_asset_id", iconPath),
            new GoldenCase(ItemImportModelValidator.CategoryRule, "/item_category", category),
            new GoldenCase(ItemImportModelValidator.MaxStackRule, "/max_stack", maxStack),
            new GoldenCase(ItemImportModelValidator.BuyPriceRule, "/buy_price", buyPrice),
            new GoldenCase(ItemImportModelValidator.SellPriceRule, "/sell_price", sellPrice),
            new GoldenCase(ItemImportModelValidator.MaterialCraftingGroupRule, "/crafting_groups", material),
            new GoldenCase(ItemImportModelValidator.QuestGroupRule, "/quest_groups", quest),
            new GoldenCase(ItemImportModelValidator.SkillBookGrantedSkillRule, "/granted_skill_id", book),
            new GoldenCase(ItemImportModelValidator.EquipmentNonStackableRule, "/is_stackable", stackableEquipment),
            new GoldenCase(ItemImportModelValidator.EquipmentSlotRequiredRule, "/equipment_slot_ids", noSlot),
            new GoldenCase(ItemImportModelValidator.EquipmentSlotRule, "/equipment_slot_ids/0", invalidSlot),
            new GoldenCase(ItemImportModelValidator.EquipmentTypeRule, "/equipment_type_id", invalidType),
            new GoldenCase(ItemImportModelValidator.WeaponProfileRequiredRule, "/weapon_profile", noProfile),
            new GoldenCase(ItemImportModelValidator.WeaponTypeRule, "/weapon_profile/weapon_type_id", weaponType),
            new GoldenCase(ItemImportModelValidator.WeaponFamilyRule, "/weapon_profile/family", family),
            new GoldenCase(ItemImportModelValidator.WeaponRangeTypeRule, "/weapon_profile/range_type", rangeType),
            new GoldenCase(ItemImportModelValidator.WeaponDamageTagRequiredRule, "/weapon_profile/damage_tag", damageRequired),
            new GoldenCase(ItemImportModelValidator.WeaponAttackRangeRule, "/weapon_profile/attack_range", attackRange),
            new GoldenCase(ItemImportModelValidator.WeaponDiceRequiredRule, "/weapon_profile", diceRequired),
            new GoldenCase(ItemImportModelValidator.WeaponDiceCountRule, "/weapon_profile/one_handed_dice/dice_count", diceCount),
            new GoldenCase(ItemImportModelValidator.WeaponDiceSidesRule, "/weapon_profile/one_handed_dice/dice_sides", diceSides),
            new GoldenCase(ItemImportModelValidator.WeaponDiceFlatBonusRule, "/weapon_profile/one_handed_dice/flat_bonus", flatBonus),
            new GoldenCase(ItemImportModelValidator.WeaponDamageTagRule, "/weapon_profile/damage_tag", damageTag),
            new GoldenCase(ItemImportModelValidator.AttributeModifierIdRule, "/attribute_modifiers/0/attribute_id", attributeId),
            new GoldenCase(ItemImportModelValidator.AttributeModifierModeRule, "/attribute_modifiers/0/mode", attributeMode),
            new GoldenCase(ItemImportModelValidator.OccupiedSlotEntryShapeRule, "/equipment_slot_ids", occupiedShape),
            new GoldenCase(ItemImportModelValidator.OccupiedSlotRule, "/occupied_slot_ids/1", occupiedInvalid),
            new GoldenCase(ItemImportModelValidator.OccupiedSlotDuplicateRule, "/occupied_slot_ids/1", occupiedDuplicate),
            new GoldenCase(ItemImportModelValidator.OccupiedSlotMissingEntryRule, "/occupied_slot_ids", occupiedMissing),
        };
    }

    private static JsonContentEntryContext Context(string itemId, int index) => new(
        ItemContentJsonAuthoringDomain.DomainId,
        itemId,
        $"golden.json#{itemId}",
        $"/cases/{index}"
    );

    private static string Format(IReadOnlyList<ContentJsonDiagnostic> values) =>
        string.Join(" | ", values.Select(value => $"{value.RuleId}@{value.JsonPointer}"));

    private sealed record GoldenCase(
        string RuleId,
        string RelativePointer,
        ItemImportModel Item
    );

    private sealed class ItemBuilder
    {
        private string _itemId;
        private string _iconAssetId = "";
        private bool _isStackable = true;
        private int _basePrice;
        private int _buyPrice;
        private int _sellPrice;
        private int _maxStack = 99;
        private string _category = ItemImportValueRules.CategoryMisc;
        private readonly List<string> _tags = new();
        private readonly List<string> _craftingGroups = new();
        private readonly List<string> _questGroups = new();
        private readonly List<string> _slots = new();
        private readonly List<string> _occupiedSlots = new();
        private readonly List<ItemAttributeModifierImportModel> _modifiers = new();
        private string _grantedSkillId = "";
        private string _equipmentTypeId = "";
        private WeaponProfileBuilder? _weaponProfile;

        private ItemBuilder(string itemId)
        {
            _itemId = itemId;
        }

        internal static ItemBuilder Misc(string itemId) => new(itemId);

        internal static ItemBuilder SkillBook(string itemId, string skillId) =>
            new ItemBuilder(itemId)
                .WithCategory(ItemImportValueRules.CategorySkillBook)
                .WithGrantedSkill(skillId);

        internal static ItemBuilder Equipment(string itemId, string equipmentType) =>
            new ItemBuilder(itemId)
                .WithCategory(ItemImportValueRules.CategoryEquipment)
                .WithStacking(false, 1)
                .WithEquipmentType(equipmentType)
                .WithSlots("main_hand");

        internal static ItemBuilder Weapon(string itemId) =>
            Equipment(itemId, ItemImportValueRules.EquipmentTypeWeapon)
                .WithWeaponProfile(new WeaponProfileBuilder());

        internal ItemBuilder WithIcon(string value) { _iconAssetId = value; return this; }
        internal ItemBuilder WithCategory(string value) { _category = value; return this; }
        internal ItemBuilder WithStacking(bool value, int maxStack) { _isStackable = value; _maxStack = maxStack; return this; }
        internal ItemBuilder WithPrices(int basePrice, int buyPrice, int sellPrice) { _basePrice = basePrice; _buyPrice = buyPrice; _sellPrice = sellPrice; return this; }
        internal ItemBuilder WithTag(string value) { _tags.Add(value); return this; }
        internal ItemBuilder WithCraftingGroup(string value) { _craftingGroups.Add(value); return this; }
        internal ItemBuilder WithQuestGroup(string value) { _questGroups.Add(value); return this; }
        internal ItemBuilder WithGrantedSkill(string value) { _grantedSkillId = value; return this; }
        internal ItemBuilder WithEquipmentType(string value) { _equipmentTypeId = value; return this; }
        internal ItemBuilder WithSlots(params string[] values) { _slots.Clear(); _slots.AddRange(values); return this; }
        internal ItemBuilder WithOccupiedSlots(params string[] values) { _occupiedSlots.Clear(); _occupiedSlots.AddRange(values); return this; }
        internal ItemBuilder WithModifier(string attributeId, string mode) { _modifiers.Add(new ItemAttributeModifierImportModel(attributeId, mode, 1, 0, "item", _itemId)); return this; }
        internal ItemBuilder WithWeaponProfile(WeaponProfileBuilder? value) { _weaponProfile = value; return this; }
        internal ItemBuilder MutateProfile(Action<WeaponProfileBuilder> mutation) { mutation(_weaponProfile ?? throw new InvalidOperationException()); return this; }

        internal ItemImportModel Build() => new(
            _itemId,
            _itemId,
            "fixture",
            _iconAssetId,
            _isStackable,
            _basePrice,
            _buyPrice,
            _sellPrice,
            true,
            _maxStack,
            _category,
            _tags,
            _craftingGroups,
            _questGroups,
            Array.Empty<string>(),
            Array.Empty<ItemTraitRollGroupImportModel>(),
            _slots,
            _modifiers,
            _grantedSkillId,
            _occupiedSlots,
            null,
            _equipmentTypeId,
            _weaponProfile?.Build(),
            -1
        );
    }

    private sealed class WeaponProfileBuilder
    {
        internal string WeaponTypeId = "longsword";
        internal string TrainingGroup = "martial";
        internal string RangeType = "melee";
        internal string Family = "sword";
        internal string DamageTag = ItemImportValueRules.DamageTagSlash;
        internal int AttackRange = 1;
        internal DiceBuilder? OneHandedDice = new();
        internal DiceBuilder? TwoHandedDice;

        internal ItemWeaponProfileImportModel Build() => new(
            WeaponTypeId,
            TrainingGroup,
            RangeType,
            Family,
            DamageTag,
            AttackRange,
            OneHandedDice?.Build(),
            TwoHandedDice?.Build(),
            Array.Empty<string>()
        );
    }

    private sealed class DiceBuilder
    {
        internal int DiceCount = 1;
        internal int DiceSides = 8;
        internal int FlatBonus;
        internal ItemWeaponDamageDiceImportModel Build() => new(DiceCount, DiceSides, FlatBonus);
    }
}
