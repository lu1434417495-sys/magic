using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

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
    private const int PriceBasisPointsDenominator = 10000;

    public ItemDefinition(
        StringName itemId,
        StringName baseItemId,
        string displayName,
        string description,
        string icon,
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
        BaseItemId = baseItemId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Icon = icon ?? throw new ArgumentNullException(nameof(icon));
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
    public StringName BaseItemId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Icon { get; }
    public bool IsStackable { get; }
    public int BasePrice { get; }
    public int BuyPrice { get; }
    public int SellPrice { get; }
    public bool Sellable { get; }
    public int MaxStack { get; }
    public StringName ItemCategory { get; }
    public IReadOnlyList<StringName> Tags { get; }
    public IReadOnlyList<StringName> CraftingGroups { get; }
    public IReadOnlyList<StringName> QuestGroups { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<TraitRollGroupDefinition> TraitRollGroups { get; }
    public IReadOnlyList<string> EquipmentSlotIds { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public StringName GrantedSkillId { get; }
    public IReadOnlyList<string> OccupiedSlotIds { get; }
    public EquipmentRequirementDefinition EquipRequirement { get; }
    public StringName EquipmentTypeId { get; }
    public WeaponProfileDefinition WeaponProfile { get; }
    public int MaxDexBonus { get; }

    internal ItemCategoryKind CategoryKind => ToItemCategoryKind(ItemCategory);
    internal ItemEquipmentTypeKind EquipmentTypeKind =>
        ToEquipmentTypeKind(EquipmentTypeId);

    public int GetEffectiveMaxStack() => IsStackable ? Math.Max(MaxStack, 1) : 1;

    public int GetBasePrice() => Math.Max(BasePrice, 0);

    public int GetBuyPrice() => GetBuyPrice(PriceBasisPointsDenominator);

    public int GetBuyPrice(int priceBasisPoints) =>
        ApplyPriceBasisPoints(Math.Max(BuyPrice, 0), priceBasisPoints);

    public int GetSellPrice() => GetSellPrice(PriceBasisPointsDenominator);

    public int GetSellPrice(int priceBasisPoints) =>
        Sellable
            ? ApplyPriceBasisPoints(Math.Max(SellPrice, 0), priceBasisPoints)
            : 0;

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

    internal static ItemDefinition FromResource(ItemDef source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var traitRollGroups = new List<TraitRollGroupDefinition>();
        int groupIndex = 0;
        foreach (
            TraitRollGroupDef group in WarehouseDefinitionProjection.RequireCollection(
                source.TraitRollGroupsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.trait_roll_groups"
            )
        )
        {
            string groupPath =
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.trait_roll_groups[{groupIndex}]";
            if (group == null)
                throw WarehouseDefinitionProjection.Invalid(groupPath, "resource is null");
            traitRollGroups.Add(TraitRollGroupDefinition.FromResource(group, groupPath));
            groupIndex++;
        }

        var attributeModifiers = new List<AttributeModifierDefinition>();
        int modifierIndex = 0;
        foreach (
            AttributeModifier modifier in WarehouseDefinitionProjection.RequireCollection(
                source.AttributeModifiersProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.attribute_modifiers"
            )
        )
        {
            string modifierPath =
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.attribute_modifiers[{modifierIndex}]";
            if (modifier == null)
                throw WarehouseDefinitionProjection.Invalid(modifierPath, "resource is null");
            attributeModifiers.Add(AttributeModifierDefinition.FromResource(modifier));
            modifierIndex++;
        }

        return new ItemDefinition(
            source.item_id,
            source.base_item_id,
            source.display_name,
            source.description,
            source.icon,
            source.is_stackable,
            source.base_price,
            source.buy_price,
            source.sell_price,
            source.sellable,
            source.max_stack,
            source.item_category,
            CopyBorrowedValues(
                source.TagsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.tags"
            ),
            CopyBorrowedValues(
                source.CraftingGroupsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.crafting_groups"
            ),
            CopyBorrowedValues(
                source.QuestGroupsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.quest_groups"
            ),
            CopyBorrowedValues(
                source.TraitIdsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.trait_ids"
            ),
            traitRollGroups,
            CopyBorrowedValues(
                source.EquipmentSlotIdsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.equipment_slot_ids"
            ),
            attributeModifiers,
            source.granted_skill_id,
            CopyBorrowedValues(
                source.OccupiedSlotIdsProjectionBorrowed,
                $"item.{WarehouseDefinitionProjection.PathId(source.item_id)}.occupied_slot_ids"
            ),
            EquipmentRequirementDefinition.FromResource(
                source.EquipRequirementProjectionBorrowed
            ),
            source.equipment_type_id,
            WeaponProfileDefinition.FromResource(source.WeaponProfileProjectionBorrowed),
            source.max_dex_bonus
        );
    }

    internal static ItemDefinition MergeWithTemplate(
        ItemDefinition template,
        ItemDefinition instance
    )
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(instance);

        return new ItemDefinition(
            instance.ItemId,
            "",
            instance.DisplayName != "" ? instance.DisplayName : template.DisplayName,
            instance.Description != "" ? instance.Description : template.Description,
            instance.Icon != "" ? instance.Icon : template.Icon,
            instance.IsStackable,
            instance.BasePrice != 0 ? instance.BasePrice : template.BasePrice,
            instance.BuyPrice != 0 ? instance.BuyPrice : template.BuyPrice,
            instance.SellPrice != 0 ? instance.SellPrice : template.SellPrice,
            instance.Sellable,
            instance.MaxStack,
            instance.ItemCategory != "" ? instance.ItemCategory : template.ItemCategory,
            MergeStringNameList(template.Tags, instance.Tags),
            MergeStringNameList(template.CraftingGroups, instance.CraftingGroups),
            MergeStringNameList(template.QuestGroups, instance.QuestGroups),
            MergeStringNameList(template.TraitIds, instance.TraitIds),
            MergeTraitRollGroups(template.TraitRollGroups, instance.TraitRollGroups),
            instance.EquipmentSlotIds.Count > 0
                ? instance.EquipmentSlotIds
                : template.EquipmentSlotIds,
            MergeAttributeModifiers(
                template.AttributeModifiers,
                instance.AttributeModifiers,
                instance.ItemId
            ),
            instance.GrantedSkillId != ""
                ? instance.GrantedSkillId
                : template.GrantedSkillId,
            instance.OccupiedSlotIds.Count > 0
                ? instance.OccupiedSlotIds
                : template.OccupiedSlotIds,
            EquipmentRequirementDefinition.CopyOf(
                instance.EquipRequirement ?? template.EquipRequirement
            ),
            instance.EquipmentTypeId != ""
                ? instance.EquipmentTypeId
                : template.EquipmentTypeId,
            WeaponProfileDefinition.Merge(template.WeaponProfile, instance.WeaponProfile),
            instance.MaxDexBonus >= 0 ? instance.MaxDexBonus : template.MaxDexBonus
        );
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

    private static IReadOnlyList<TraitRollGroupDefinition> MergeTraitRollGroups(
        IReadOnlyList<TraitRollGroupDefinition> templateGroups,
        IReadOnlyList<TraitRollGroupDefinition> instanceGroups
    )
    {
        ValidateMergeTraitRollGroups(templateGroups, "template");
        ValidateMergeTraitRollGroups(instanceGroups, "instance");
        var result = new List<TraitRollGroupDefinition>();
        var indexById = new Dictionary<StringName, int>();

        void AddOrReplace(TraitRollGroupDefinition group)
        {
            if (group.GroupId == "")
                return;
            TraitRollGroupDefinition copy = TraitRollGroupDefinition.CopyOf(group);
            if (indexById.TryGetValue(copy.GroupId, out int existingIndex))
            {
                result[existingIndex] = copy;
                return;
            }
            indexById[copy.GroupId] = result.Count;
            result.Add(copy);
        }

        foreach (TraitRollGroupDefinition group in templateGroups)
            AddOrReplace(group);
        foreach (TraitRollGroupDefinition group in instanceGroups)
            AddOrReplace(group);
        return result;
    }

    private static void ValidateMergeTraitRollGroups(
        IReadOnlyList<TraitRollGroupDefinition> groups,
        string ownerPath
    )
    {
        ArgumentNullException.ThrowIfNull(groups);
        var seen = new HashSet<StringName>();
        for (int index = 0; index < groups.Count; index++)
        {
            TraitRollGroupDefinition group = groups[index];
            string path = $"item_merge.{ownerPath}.trait_roll_groups[{index}]";
            if (group == null)
                throw WarehouseDefinitionProjection.Invalid(path, "definition is null");
            if (group.GroupId == "")
                throw WarehouseDefinitionProjection.Invalid(path + ".group_id", "value is empty");
            if (!seen.Add(group.GroupId))
            {
                throw WarehouseDefinitionProjection.Invalid(
                    path + ".group_id",
                    $"duplicate value '{group.GroupId}'"
                );
            }
        }
    }

    private static IReadOnlyList<StringName> MergeStringNameList(
        IReadOnlyList<StringName> templateValues,
        IReadOnlyList<StringName> instanceValues
    )
    {
        var result = new List<StringName>();
        var seen = new HashSet<StringName>();
        void AddValues(IReadOnlyList<StringName> values)
        {
            foreach (StringName value in values)
            {
                StringName normalized = ProgressionDataUtils.to_string_name(value);
                if (normalized != "" && seen.Add(normalized))
                    result.Add(normalized);
            }
        }
        AddValues(templateValues);
        AddValues(instanceValues);
        return result;
    }

    private static IReadOnlyList<AttributeModifierDefinition> MergeAttributeModifiers(
        IReadOnlyList<AttributeModifierDefinition> templateModifiers,
        IReadOnlyList<AttributeModifierDefinition> instanceModifiers,
        StringName finalItemId
    )
    {
        var result = new List<AttributeModifierDefinition>();
        void AddValues(IReadOnlyList<AttributeModifierDefinition> values)
        {
            foreach (AttributeModifierDefinition modifier in values)
            {
                result.Add(
                    new AttributeModifierDefinition(
                        modifier.AttributeId,
                        modifier.Mode,
                        modifier.Value,
                        modifier.ValuePerRank,
                        modifier.SourceType,
                        finalItemId
                    )
                );
            }
        }
        AddValues(templateModifiers);
        AddValues(instanceModifiers);
        return result;
    }

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

    private static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string parameterName)
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

    private static IReadOnlyList<T> CopyBorrowedValues<T>(IEnumerable<T> values, string path) =>
        new List<T>(WarehouseDefinitionProjection.RequireCollection(values, path));

    private static int ApplyPriceBasisPoints(int price, int priceBasisPoints)
    {
        int normalizedPrice = Math.Max(price, 0);
        int normalizedBasisPoints = Math.Max(priceBasisPoints, 0);
        return (normalizedPrice * normalizedBasisPoints + PriceBasisPointsDenominator / 2)
            / PriceBasisPointsDenominator;
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
