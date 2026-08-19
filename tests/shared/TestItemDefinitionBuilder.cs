#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Godot;

/// <summary>
/// Plain test authoring helper. It deliberately builds the same import model consumed by
/// production JSON and never creates or projects a Godot Resource.
/// </summary>
internal sealed class TestItemDefinitionBuilder : IDisposable
{
    internal StringName item_id = "";
    internal string display_name = "";
    internal string description = "";
    internal string icon_asset_id = "";
    internal bool is_stackable = true;
    internal int base_price;
    internal int buy_price;
    internal int sell_price;
    internal bool sellable = true;
    internal int max_stack = 99;
    internal StringName item_category = ItemImportValueRules.CategoryMisc;
    internal TestStringNameList tags = new();
    internal TestStringNameList crafting_groups = new();
    internal TestStringNameList quest_groups = new();
    internal TestStringNameList trait_ids = new();
    internal List<TestTraitRollGroupDefinitionBuilder> trait_roll_groups = new();
    internal TestStringList equipment_slot_ids = new();
    internal List<AttributeModifierDefinition> attribute_modifiers = new();
    internal StringName granted_skill_id = "";
    internal TestStringList occupied_slot_ids = new();
    internal EquipmentRequirementDefinition? equip_requirement;
    internal StringName equipment_type_id = "";
    internal TestWeaponProfileDefinitionBuilder? weapon_profile;
    internal int max_dex_bonus = -1;

    internal ItemCategoryKind CategoryKind
    {
        get => ItemDefinition.ToItemCategoryKind(item_category);
        set => item_category = ItemDefinition.ToStringName(value);
    }

    internal ItemEquipmentTypeKind EquipmentTypeKind
    {
        get => ItemDefinition.ToEquipmentTypeKind(equipment_type_id);
        set => equipment_type_id = ItemDefinition.ToStringName(value);
    }

    internal ItemDefinition ToDefinition() => ItemDefinitionProjector.Project(ToImportModel());

    public void Dispose() { }

    internal int GetEffectiveMaxStack() => is_stackable ? Math.Max(max_stack, 1) : 1;

    internal int GetBasePrice() => Math.Max(base_price, 0);

    internal int GetBuyPrice() => GetBuyPrice(ItemPriceRules.BasisPointsDenominator);

    internal int GetBuyPrice(int priceBasisPoints) =>
        ItemPriceRules.ApplyBasisPoints(buy_price, priceBasisPoints);

    internal int GetSellPrice() => GetSellPrice(ItemPriceRules.BasisPointsDenominator);

    internal int GetSellPrice(int priceBasisPoints) =>
        ToDefinition().GetSellPrice(priceBasisPoints);

    internal ItemImportModel ToImportModel() =>
        new(
            item_id.ToString(),
            display_name,
            description,
            icon_asset_id,
            is_stackable,
            base_price,
            buy_price,
            sell_price,
            sellable,
            max_stack,
            item_category.ToString(),
            ToStrings(tags),
            ToStrings(crafting_groups),
            ToStrings(quest_groups),
            ToStrings(trait_ids),
            ConvertGroups(trait_roll_groups),
            new List<string>(equipment_slot_ids),
            ConvertModifiers(attribute_modifiers),
            granted_skill_id.ToString(),
            new List<string>(occupied_slot_ids),
            ConvertRequirement(equip_requirement),
            equipment_type_id.ToString(),
            weapon_profile?.ToImportModel(),
            max_dex_bonus
        );

    internal static StringName ToStringName(ItemCategoryKind kind) =>
        ItemDefinition.ToStringName(kind);

    internal static StringName ToStringName(ItemEquipmentTypeKind kind) =>
        ItemDefinition.ToStringName(kind);

    internal static StringName ToStringName(WeaponPhysicalDamageTagKind kind) =>
        ItemDefinition.ToStringName(kind);

    private static IReadOnlyList<string> ToStrings(IReadOnlyList<StringName> values)
    {
        var result = new List<string>(values.Count);
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static IReadOnlyList<ItemTraitRollGroupImportModel> ConvertGroups(
        IReadOnlyList<TestTraitRollGroupDefinitionBuilder> groups
    )
    {
        var result = new List<ItemTraitRollGroupImportModel>(groups.Count);
        for (int index = 0; index < groups.Count; index++)
        {
            TestTraitRollGroupDefinitionBuilder? group = groups[index];
            if (group == null)
                throw new InvalidDataException($"trait_roll_groups[{index}] must not be null.");
            result.Add(group.ToImportModel());
        }
        return result;
    }

    private static IReadOnlyList<ItemAttributeModifierImportModel> ConvertModifiers(
        IReadOnlyList<AttributeModifierDefinition> modifiers
    )
    {
        var result = new List<ItemAttributeModifierImportModel>(modifiers.Count);
        foreach (AttributeModifierDefinition modifier in modifiers)
        {
            result.Add(
                new ItemAttributeModifierImportModel(
                    modifier.AttributeId.ToString(),
                    modifier.Mode.ToString(),
                    modifier.Value,
                    modifier.ValuePerRank,
                    modifier.SourceType.ToString(),
                    modifier.SourceId.ToString()
                )
            );
        }
        return result;
    }

    private static ItemEquipmentRequirementImportModel? ConvertRequirement(
        EquipmentRequirementDefinition? requirement
    )
    {
        if (requirement == null)
            return null;
        var attributes = new List<ItemEquipmentAttributeRequirementImportModel>(
            requirement.AttributeRequirements.Count
        );
        foreach (EquipmentAttributeRequirementDefinition attribute in requirement.AttributeRequirements)
        {
            attributes.Add(
                new ItemEquipmentAttributeRequirementImportModel(
                    attribute.AttributeId.ToString(),
                    attribute.MinValue
                )
            );
        }
        return new ItemEquipmentRequirementImportModel(
            new List<string>(requirement.RequiredProfessionIds),
            requirement.MinBodySize,
            requirement.MaxBodySize,
            attributes
        );
    }
}

internal sealed class TestTraitRollGroupDefinitionBuilder
{
    internal StringName group_id = "";
    internal int roll_count = 1;
    internal List<TestTraitRollGroupEntryDefinitionBuilder> entries = new();

    internal ItemTraitRollGroupImportModel ToImportModel()
    {
        var converted = new List<ItemTraitRollGroupEntryImportModel>(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            TestTraitRollGroupEntryDefinitionBuilder? entry = entries[index];
            if (entry == null)
                throw new InvalidDataException($"entries[{index}] must not be null.");
            converted.Add(entry.ToImportModel());
        }
        return new ItemTraitRollGroupImportModel(group_id.ToString(), roll_count, converted);
    }

    internal TraitRollGroupDefinition ToDefinition()
    {
        ItemTraitRollGroupImportModel import = ToImportModel();
        var converted = new List<TraitRollGroupEntryDefinition>(import.Entries.Count);
        foreach (ItemTraitRollGroupEntryImportModel entry in import.Entries)
        {
            converted.Add(
                new TraitRollGroupEntryDefinition(
                    entry.TraitId,
                    entry.Weight,
                    entry.ExclusiveGroup
                )
            );
        }
        return new TraitRollGroupDefinition(import.GroupId, import.RollCount, converted);
    }
}

internal sealed class TestTraitRollGroupEntryDefinitionBuilder
{
    internal StringName trait_id = "";
    internal int weight = 1;
    internal StringName exclusive_group = "";

    internal ItemTraitRollGroupEntryImportModel ToImportModel() =>
        new(trait_id.ToString(), weight, exclusive_group.ToString());
}

internal sealed class TestWeaponProfileDefinitionBuilder
{
    internal StringName weapon_type_id = "";
    internal StringName training_group = "";
    internal StringName range_type = "";
    internal StringName family = "";
    internal StringName damage_tag = "";
    internal int attack_range = 1;
    internal TestWeaponDamageDiceDefinitionBuilder? one_handed_dice;
    internal TestWeaponDamageDiceDefinitionBuilder? two_handed_dice;
    internal TestStringNameList properties = new();

    internal ItemWeaponProfileImportModel ToImportModel() =>
        new(
            weapon_type_id.ToString(),
            training_group.ToString(),
            range_type.ToString(),
            family.ToString(),
            damage_tag.ToString(),
            attack_range,
            one_handed_dice?.ToImportModel(),
            two_handed_dice?.ToImportModel(),
            ToStrings(properties)
        );

    internal List<StringName> GetPropertiesTyped() => new(properties);

    private static IReadOnlyList<string> ToStrings(IReadOnlyList<StringName> values)
    {
        var result = new List<string>(values.Count);
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }
}

internal sealed class TestWeaponDamageDiceDefinitionBuilder
{
    internal int dice_count = 1;
    internal int dice_sides = 6;
    internal int flat_bonus;

    internal ItemWeaponDamageDiceImportModel ToImportModel() =>
        new(dice_count, dice_sides, flat_bonus);
}

internal sealed class TestStringList : List<string>
{
    public static implicit operator TestStringList(
        Godot.Collections.Array<string> values
    ) => values == null ? new TestStringList() : new TestStringList(values);

    internal TestStringList() { }

    private TestStringList(IEnumerable<string> values)
        : base(values) { }
}

internal sealed class TestStringNameList : List<StringName>
{
    public static implicit operator TestStringNameList(
        Godot.Collections.Array<StringName> values
    ) => values == null ? new TestStringNameList() : new TestStringNameList(values);

    internal TestStringNameList() { }

    private TestStringNameList(IEnumerable<StringName> values)
        : base(values) { }
}

internal static class TestItemDefinitionLookup
{
    internal static ItemDefinition GetProductionItem(StringName itemId)
    {
        using var registry = new ItemContentRegistry();
        registry.Rebuild();
        IReadOnlyList<string> errors = registry.ValidateTyped();
        if (errors.Count > 0)
            throw new InvalidDataException(
                $"Production item registry failed: {string.Join(" | ", errors)}"
            );
        if (!registry.GetItemDefsTyped().TryGetValue(itemId, out ItemDefinition? definition))
            throw new KeyNotFoundException($"Production item registry does not contain '{itemId}'.");
        return definition;
    }
}
