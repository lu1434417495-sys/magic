using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

internal enum ItemCategoryKind
{
    Unknown = 0,
    Misc,
    Equipment,
    SkillBook,
}

internal enum ItemEquipmentTypeKind
{
    Unknown = 0,
    Weapon,
    Armor,
    Accessory,
}

internal enum WeaponPhysicalDamageTagKind
{
    Unknown = 0,
    Slash,
    Pierce,
    Blunt,
}

public sealed class ItemDefinition
{
    private static readonly StringName ItemCategoryMisc = "misc";
    private static readonly StringName ItemCategoryEquipment = "equipment";
    private static readonly StringName ItemCategorySkillBook = "skill_book";
    private static readonly StringName EquipmentTypeWeapon = "weapon";
    private static readonly StringName EquipmentTypeArmor = "armor";
    private static readonly StringName EquipmentTypeAccessory = "accessory";
    private static readonly StringName DamageTagPhysicalSlash = "physical_slash";
    private static readonly StringName DamageTagPhysicalPierce = "physical_pierce";
    private static readonly StringName DamageTagPhysicalBlunt = "physical_blunt";
    public ItemDefinition(
        StringName itemId,
        string displayName,
        string description,
        string iconAssetId,
        bool isStackable,
        int basePrice,
        int buyPrice,
        int sellPrice,
        bool sellable,
        int maxStack,
        StringName itemCategory,
        IReadOnlyList<StringName> tags,
        IReadOnlyList<StringName> craftingGroups,
        IReadOnlyList<StringName> questGroups,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<TraitRollGroupDefinition> traitRollGroups,
        IReadOnlyList<string> equipmentSlotIds,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        StringName grantedSkillId,
        IReadOnlyList<string> occupiedSlotIds,
        EquipmentRequirementDefinition equipRequirement,
        StringName equipmentTypeId,
        WeaponProfileDefinition weaponProfile,
        int maxDexBonus
    )
    {
        ItemId = itemId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        IconAssetId = iconAssetId ?? throw new ArgumentNullException(nameof(iconAssetId));
        IsStackable = isStackable;
        BasePrice = basePrice;
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
        Sellable = sellable;
        MaxStack = maxStack;
        ItemCategory = itemCategory;
        Tags = FreezeValues(tags, nameof(tags));
        CraftingGroups = FreezeValues(craftingGroups, nameof(craftingGroups));
        QuestGroups = FreezeValues(questGroups, nameof(questGroups));
        TraitIds = FreezeValues(traitIds, nameof(traitIds));
        TraitRollGroups = FreezeValues(traitRollGroups, nameof(traitRollGroups));
        EquipmentSlotIds = FreezeValues(equipmentSlotIds, nameof(equipmentSlotIds));
        AttributeModifiers = FreezeValues(attributeModifiers, nameof(attributeModifiers));
        GrantedSkillId = grantedSkillId;
        OccupiedSlotIds = FreezeValues(occupiedSlotIds, nameof(occupiedSlotIds));
        EquipRequirement = EquipmentRequirementDefinition.CopyOf(equipRequirement);
        EquipmentTypeId = equipmentTypeId;
        WeaponProfile = WeaponProfileDefinition.CopyOf(weaponProfile);
        MaxDexBonus = maxDexBonus;
    }

    public StringName ItemId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string IconAssetId { get; }
    public bool IsStackable { get; }
    public int BasePrice { get; }
    public int BuyPrice { get; }
    public int SellPrice { get; }
    public bool Sellable { get; }
    public int MaxStack { get; }
    public StringName ItemCategory { get; }
    public ReadOnlyCollection<StringName> Tags { get; }
    public ReadOnlyCollection<StringName> CraftingGroups { get; }
    public ReadOnlyCollection<StringName> QuestGroups { get; }
    public ReadOnlyCollection<StringName> TraitIds { get; }
    public ReadOnlyCollection<TraitRollGroupDefinition> TraitRollGroups { get; }
    public ReadOnlyCollection<string> EquipmentSlotIds { get; }
    public ReadOnlyCollection<AttributeModifierDefinition> AttributeModifiers { get; }
    public StringName GrantedSkillId { get; }
    public ReadOnlyCollection<string> OccupiedSlotIds { get; }
    public EquipmentRequirementDefinition EquipRequirement { get; }
    public StringName EquipmentTypeId { get; }
    public WeaponProfileDefinition WeaponProfile { get; }
    public int MaxDexBonus { get; }

    internal ItemCategoryKind CategoryKind => ToItemCategoryKind(ItemCategory);
    internal ItemEquipmentTypeKind EquipmentTypeKind =>
        ToEquipmentTypeKind(EquipmentTypeId);

    public int GetEffectiveMaxStack() => IsStackable ? Math.Max(MaxStack, 1) : 1;

    public int GetBasePrice() => Math.Max(BasePrice, 0);

    public int GetBuyPrice() => GetBuyPrice(ItemPriceRules.BasisPointsDenominator);

    public int GetBuyPrice(int priceBasisPoints) =>
        ItemPriceRules.ApplyBasisPoints(BuyPrice, priceBasisPoints);

    public int GetSellPrice() => GetSellPrice(ItemPriceRules.BasisPointsDenominator);

    public int GetSellPrice(int priceBasisPoints)
    {
        if (!Sellable)
            return 0;
        int sellPrice = Math.Max(SellPrice, 0);
        // 防商店套利:折扣商店买价最低可到九折(basis points 9000),卖价一旦达到
        // 买价的 90% 就构成"折扣买入→原价卖出"的无限金币循环。生效卖价上限为
        // 买价的 50% 向上取整——现有内容对奇数买价按四舍五入配 50%(如 275/138),
        // 向上取整让它们原值生效,同时对 90% 危险线仍留足余量。超限配置不报错,
        // 静默回退到上限值;加载期由 ItemContentRegistry 对超限值打告警提示内容
        // 作者。回退放在这个消费出口而非内容校验层,是为了让模板链继承、
        // SkillBookItemFactory 生成等所有构造路径都无法绕过此线。
        if (BuyPrice > 0)
            sellPrice = Math.Min(sellPrice, (BuyPrice + 1) / 2);
        return ItemPriceRules.ApplyBasisPoints(sellPrice, priceBasisPoints);
    }

    public List<StringName> GetTagsTyped() => NormalizeStringNameList(Tags);

    public List<StringName> GetCraftingGroupsTyped() =>
        NormalizeStringNameList(CraftingGroups);

    public List<StringName> GetQuestGroupsTyped() =>
        NormalizeStringNameList(QuestGroups);

    public List<StringName> GetTraitIdsTyped() => NormalizeStringNameList(TraitIds);

    public List<TraitRollGroupDefinition> GetTraitRollGroupsTyped() =>
        new(TraitRollGroups);

    public StringName GetItemCategoryNormalized() => ToStringName(CategoryKind);

    public bool HasEquipmentCategory() => CategoryKind == ItemCategoryKind.Equipment;

    public List<StringName> GetEquipmentSlotIdsTyped() =>
        new(EquipmentRules.NormalizeSlotIdsTyped(EquipmentSlotIds));

    public bool IsEquipment() =>
        HasEquipmentCategory() && GetEquipmentSlotIdsTyped().Count > 0;

    public StringName GetEquipmentTypeIdNormalized() => ToStringName(EquipmentTypeKind);

    public bool HasValidEquipmentType() => GetEquipmentTypeIdNormalized() != "";

    public bool IsWeapon() =>
        HasEquipmentCategory() && EquipmentTypeKind == ItemEquipmentTypeKind.Weapon;

    public int GetWeaponAttackRange() =>
        IsWeapon() && WeaponProfile != null ? Math.Max(WeaponProfile.AttackRange, 0) : 0;

    public StringName GetWeaponRangeType() =>
        IsWeapon() && WeaponProfile != null
            ? ProgressionDataUtils.to_string_name(WeaponProfile.RangeType)
            : "";

    public StringName GetWeaponPhysicalDamageTag()
    {
        if (!IsWeapon() || WeaponProfile == null)
            return "";
        StringName normalized = ProgressionDataUtils.to_string_name(WeaponProfile.DamageTag);
        return ToStringName(ToWeaponPhysicalDamageTagKind(normalized));
    }

    internal WeaponPhysicalDamageTagKind GetWeaponPhysicalDamageTagKind()
    {
        if (!IsWeapon() || WeaponProfile == null)
            return WeaponPhysicalDamageTagKind.Unknown;
        return ToWeaponPhysicalDamageTagKind(
            ProgressionDataUtils.to_string_name(WeaponProfile.DamageTag)
        );
    }

    public bool IsArmor() =>
        HasEquipmentCategory() && EquipmentTypeKind == ItemEquipmentTypeKind.Armor;

    public int GetMaxDexBonus() => Math.Max(MaxDexBonus, -1);

    public bool IsAccessory() =>
        HasEquipmentCategory() && EquipmentTypeKind == ItemEquipmentTypeKind.Accessory;

    public bool IsSkillBook() =>
        CategoryKind == ItemCategoryKind.SkillBook && GrantedSkillId != "";

    public List<AttributeModifierDefinition> GetAttributeModifiersTyped() =>
        new(AttributeModifiers);

    public List<StringName> GetFinalOccupiedSlotIdsTyped(StringName entrySlotId)
    {
        if (OccupiedSlotIds.Count > 0)
            return new List<StringName>(EquipmentRules.NormalizeSlotIdsTyped(OccupiedSlotIds));
        StringName normalized = ProgressionDataUtils.to_string_name(entrySlotId);
        return EquipmentRules.IsValidSlot(normalized)
            ? new List<StringName> { normalized }
            : new List<StringName>();
    }

    internal static ItemCategoryKind ToItemCategoryKind(StringName value)
    {
        if (value == "" || value == ItemCategoryMisc)
            return ItemCategoryKind.Misc;
        if (value == ItemCategoryEquipment)
            return ItemCategoryKind.Equipment;
        if (value == ItemCategorySkillBook)
            return ItemCategoryKind.SkillBook;
        return ItemCategoryKind.Unknown;
    }

    internal static ItemEquipmentTypeKind ToEquipmentTypeKind(StringName value)
    {
        if (value == EquipmentTypeWeapon)
            return ItemEquipmentTypeKind.Weapon;
        if (value == EquipmentTypeArmor)
            return ItemEquipmentTypeKind.Armor;
        if (value == EquipmentTypeAccessory)
            return ItemEquipmentTypeKind.Accessory;
        return ItemEquipmentTypeKind.Unknown;
    }

    internal static WeaponPhysicalDamageTagKind ToWeaponPhysicalDamageTagKind(StringName value)
    {
        if (value == DamageTagPhysicalSlash)
            return WeaponPhysicalDamageTagKind.Slash;
        if (value == DamageTagPhysicalPierce)
            return WeaponPhysicalDamageTagKind.Pierce;
        if (value == DamageTagPhysicalBlunt)
            return WeaponPhysicalDamageTagKind.Blunt;
        return WeaponPhysicalDamageTagKind.Unknown;
    }

    internal static StringName ToStringName(ItemCategoryKind kind) =>
        kind switch
        {
            ItemCategoryKind.Misc => ItemCategoryMisc,
            ItemCategoryKind.Equipment => ItemCategoryEquipment,
            ItemCategoryKind.SkillBook => ItemCategorySkillBook,
            _ => "",
        };

    internal static StringName ToStringName(ItemEquipmentTypeKind kind) =>
        kind switch
        {
            ItemEquipmentTypeKind.Weapon => EquipmentTypeWeapon,
            ItemEquipmentTypeKind.Armor => EquipmentTypeArmor,
            ItemEquipmentTypeKind.Accessory => EquipmentTypeAccessory,
            _ => "",
        };

    internal static StringName ToStringName(WeaponPhysicalDamageTagKind kind) =>
        kind switch
        {
            WeaponPhysicalDamageTagKind.Slash => DamageTagPhysicalSlash,
            WeaponPhysicalDamageTagKind.Pierce => DamageTagPhysicalPierce,
            WeaponPhysicalDamageTagKind.Blunt => DamageTagPhysicalBlunt,
            _ => "",
        };

    private static List<StringName> NormalizeStringNameList(
        IReadOnlyList<StringName> values
    )
    {
        var result = new List<StringName>();
        foreach (StringName rawValue in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static ReadOnlyCollection<T> FreezeValues<T>(
        IReadOnlyList<T> values,
        string parameterName
    )
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copied = new List<T>(values.Count);
        foreach (T value in values)
        {
            if (value is null)
                throw new ArgumentException("Definition lists must not contain null.", parameterName);
            copied.Add(value);
        }
        return new ReadOnlyCollection<T>(copied);
    }

}

internal static class WarehouseDefinitionProjection
{
    internal static IEnumerable<T> RequireCollection<T>(IEnumerable<T> values, string path) =>
        values ?? throw Invalid(path, "collection is null");

    internal static string PathId(StringName value) => value == "" ? "<missing>" : value.ToString();

    internal static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid authored content at '{path}': {message}.");
}
