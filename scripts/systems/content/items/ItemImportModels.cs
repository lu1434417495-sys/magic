#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed class ItemImportModel
{
    internal ItemImportModel(
        string itemId,
        string displayName,
        string description,
        string iconAssetId,
        bool isStackable,
        int basePrice,
        int buyPrice,
        int sellPrice,
        bool sellable,
        int maxStack,
        string itemCategory,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> craftingGroups,
        IReadOnlyList<string> questGroups,
        IReadOnlyList<string> traitIds,
        IReadOnlyList<ItemTraitRollGroupImportModel> traitRollGroups,
        IReadOnlyList<string> equipmentSlotIds,
        IReadOnlyList<ItemAttributeModifierImportModel> attributeModifiers,
        string grantedSkillId,
        IReadOnlyList<string> occupiedSlotIds,
        ItemEquipmentRequirementImportModel? equipRequirement,
        string equipmentTypeId,
        ItemWeaponProfileImportModel? weaponProfile,
        int maxDexBonus
    )
    {
        ItemId = RequireString(itemId, nameof(itemId));
        DisplayName = RequireString(displayName, nameof(displayName));
        Description = RequireString(description, nameof(description));
        IconAssetId = RequireString(iconAssetId, nameof(iconAssetId));
        IsStackable = isStackable;
        BasePrice = basePrice;
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
        Sellable = sellable;
        MaxStack = maxStack;
        ItemCategory = RequireString(itemCategory, nameof(itemCategory));
        Tags = Freeze(tags, nameof(tags));
        CraftingGroups = Freeze(craftingGroups, nameof(craftingGroups));
        QuestGroups = Freeze(questGroups, nameof(questGroups));
        TraitIds = Freeze(traitIds, nameof(traitIds));
        TraitRollGroups = Freeze(traitRollGroups, nameof(traitRollGroups));
        EquipmentSlotIds = Freeze(equipmentSlotIds, nameof(equipmentSlotIds));
        AttributeModifiers = Freeze(attributeModifiers, nameof(attributeModifiers));
        GrantedSkillId = RequireString(grantedSkillId, nameof(grantedSkillId));
        OccupiedSlotIds = Freeze(occupiedSlotIds, nameof(occupiedSlotIds));
        EquipRequirement = equipRequirement;
        EquipmentTypeId = RequireString(equipmentTypeId, nameof(equipmentTypeId));
        WeaponProfile = weaponProfile;
        MaxDexBonus = maxDexBonus;
    }

    internal string ItemId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal string IconAssetId { get; }
    internal bool IsStackable { get; }
    internal int BasePrice { get; }
    internal int BuyPrice { get; }
    internal int SellPrice { get; }
    internal bool Sellable { get; }
    internal int MaxStack { get; }
    internal string ItemCategory { get; }
    internal IReadOnlyList<string> Tags { get; }
    internal IReadOnlyList<string> CraftingGroups { get; }
    internal IReadOnlyList<string> QuestGroups { get; }
    internal IReadOnlyList<string> TraitIds { get; }
    internal IReadOnlyList<ItemTraitRollGroupImportModel> TraitRollGroups { get; }
    internal IReadOnlyList<string> EquipmentSlotIds { get; }
    internal IReadOnlyList<ItemAttributeModifierImportModel> AttributeModifiers { get; }
    internal string GrantedSkillId { get; }
    internal IReadOnlyList<string> OccupiedSlotIds { get; }
    internal ItemEquipmentRequirementImportModel? EquipRequirement { get; }
    internal string EquipmentTypeId { get; }
    internal ItemWeaponProfileImportModel? WeaponProfile { get; }
    internal int MaxDexBonus { get; }

    private static string RequireString(string value, string parameterName) =>
        value ?? throw new ArgumentNullException(parameterName);

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copy = new List<T>(values.Count);
        foreach (T value in values)
        {
            if (value is null)
                throw new ArgumentException("Item import lists must not contain null.", parameterName);
            copy.Add(value);
        }
        return new ReadOnlyCollection<T>(copy);
    }
}

internal sealed record ItemTraitRollGroupImportModel(
    string GroupId,
    int RollCount,
    IReadOnlyList<ItemTraitRollGroupEntryImportModel> Entries
);

internal sealed record ItemTraitRollGroupEntryImportModel(
    string TraitId,
    int Weight,
    string ExclusiveGroup
);

internal sealed record ItemAttributeModifierImportModel(
    string AttributeId,
    string Mode,
    int Value,
    int ValuePerRank,
    string SourceType,
    string SourceId
);

internal sealed record ItemEquipmentRequirementImportModel(
    IReadOnlyList<string> RequiredProfessionIds,
    int MinBodySize,
    int MaxBodySize,
    IReadOnlyList<ItemEquipmentAttributeRequirementImportModel> AttributeRequirements
);

internal sealed record ItemEquipmentAttributeRequirementImportModel(
    string AttributeId,
    int MinValue
);

internal sealed record ItemWeaponProfileImportModel(
    string WeaponTypeId,
    string TrainingGroup,
    string RangeType,
    string Family,
    string DamageTag,
    int AttackRange,
    ItemWeaponDamageDiceImportModel? OneHandedDice,
    ItemWeaponDamageDiceImportModel? TwoHandedDice,
    IReadOnlyList<string> Properties
);

internal sealed record ItemWeaponDamageDiceImportModel(
    int DiceCount,
    int DiceSides,
    int FlatBonus
);

internal static class ItemImportValueRules
{
    internal const string CategoryMisc = "misc";
    internal const string CategoryEquipment = "equipment";
    internal const string CategorySkillBook = "skill_book";
    internal const string EquipmentTypeWeapon = "weapon";
    internal const string EquipmentTypeArmor = "armor";
    internal const string EquipmentTypeAccessory = "accessory";
    internal const string DamageTagSlash = "physical_slash";
    internal const string DamageTagPierce = "physical_pierce";
    internal const string DamageTagBlunt = "physical_blunt";
    internal const string WeaponRangeMelee = "melee";
    internal const string WeaponRangeRanged = "ranged";
    internal const int WeaponDiceCountMin = 1;
    internal const int WeaponDiceCountMax = 99;
    internal const int WeaponDiceSidesMin = 1;
    internal const int WeaponDiceSidesMax = 999;
    internal const int WeaponDiceFlatBonusMin = -999;
    internal const int WeaponDiceFlatBonusMax = 999;

    internal static IReadOnlyList<string> ItemCategoryValues { get; } =
        Array.AsReadOnly(new[] { CategoryMisc, CategoryEquipment, CategorySkillBook });
    internal static IReadOnlyList<string> EquipmentTypeValues { get; } =
        Array.AsReadOnly(
            new[] { EquipmentTypeWeapon, EquipmentTypeArmor, EquipmentTypeAccessory }
        );
    internal static IReadOnlyList<string> WeaponDamageTagValues { get; } =
        Array.AsReadOnly(new[] { DamageTagSlash, DamageTagPierce, DamageTagBlunt });
    internal static IReadOnlyList<string> WeaponRangeTypeValues { get; } =
        Array.AsReadOnly(new[] { WeaponRangeMelee, WeaponRangeRanged });
    internal static IReadOnlyList<string> AttributeModifierModeValues { get; } =
        Array.AsReadOnly(new[] { "flat", "percent" });
    internal static IReadOnlyList<string> EquipmentSlotValues { get; } =
        Array.AsReadOnly(
            new[]
            {
                "main_hand",
                "off_hand",
                "head",
                "body",
                "hands",
                "feet",
                "cloak",
                "necklace",
                "ring_1",
                "ring_2",
                "special_trinket",
                "badge",
            }
        );

    internal static bool IsKnownCategory(string value) =>
        ContainsOrdinal(ItemCategoryValues, value);

    internal static bool IsKnownEquipmentType(string value) =>
        ContainsOrdinal(EquipmentTypeValues, value);

    internal static bool IsKnownWeaponDamageTag(string value) =>
        ContainsOrdinal(WeaponDamageTagValues, value);

    internal static bool IsKnownWeaponRangeType(string value) =>
        ContainsOrdinal(WeaponRangeTypeValues, value);

    internal static bool IsKnownAttributeModifierMode(string value) =>
        ContainsOrdinal(AttributeModifierModeValues, value);

    internal static bool IsKnownEquipmentSlot(string value) =>
        ContainsOrdinal(EquipmentSlotValues, value);

    private static bool ContainsOrdinal(IReadOnlyList<string> values, string candidate)
    {
        foreach (string value in values)
        {
            if (string.Equals(value, candidate, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
