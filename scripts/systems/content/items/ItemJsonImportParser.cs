#nullable enable

using System;
using System.Collections.Generic;

internal static class ItemJsonImportParser
{
    internal const string InvalidDtoRule = "item.import.invalid_dto";
    internal const string InvalidShapeRule = "item.import.invalid_shape";

    internal static ContentImportStageResult<ItemJsonDto> Parse(
        JsonContentEntryContext context,
        string json
    ) => ContentJsonStrictDtoParser.Parse(
        context,
        json,
        ItemJsonImportSerializerContext.Default.ItemJsonDto,
        InvalidDtoRule
    );

    internal static ContentImportStageResult<ItemImportModel> Normalize(
        JsonContentEntryContext context,
        ItemJsonDto dto
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dto);

        try
        {
            return ContentImportStageResult<ItemImportModel>.Success(
                new ItemImportModel(
                    RequireString(dto.ItemId, "/item_id"),
                    RequireString(dto.DisplayName, "/display_name"),
                    RequireString(dto.Description, "/description"),
                    RequireString(dto.IconAssetId, "/icon_asset_id"),
                    dto.IsStackable,
                    dto.BasePrice,
                    dto.BuyPrice,
                    dto.SellPrice,
                    dto.Sellable,
                    dto.MaxStack,
                    RequireString(dto.ItemCategory, "/item_category"),
                    CopyStrings(dto.Tags, "/tags"),
                    CopyStrings(dto.CraftingGroups, "/crafting_groups"),
                    CopyStrings(dto.QuestGroups, "/quest_groups"),
                    CopyStrings(dto.TraitIds, "/trait_ids"),
                    CopyTraitRollGroups(dto.TraitRollGroups, "/trait_roll_groups"),
                    CopyStrings(dto.EquipmentSlotIds, "/equipment_slot_ids"),
                    CopyAttributeModifiers(dto.AttributeModifiers, "/attribute_modifiers"),
                    RequireString(dto.GrantedSkillId, "/granted_skill_id"),
                    CopyStrings(dto.OccupiedSlotIds, "/occupied_slot_ids"),
                    CopyEquipmentRequirement(dto.EquipRequirement, "/equip_requirement"),
                    RequireString(dto.EquipmentTypeId, "/equipment_type_id"),
                    CopyWeaponProfile(dto.WeaponProfile, "/weapon_profile"),
                    dto.MaxDexBonus
                )
            );
        }
        catch (ItemImportShapeException exception)
        {
            return ContentImportStageResult<ItemImportModel>.Failure(
                new ContentJsonDiagnostic(
                    InvalidShapeRule,
                    exception.Message,
                    context.SourceLabel,
                    context.JsonPointer + exception.RelativePointer
                )
            );
        }
    }

    private static IReadOnlyList<string> CopyStrings(
        IReadOnlyList<string>? values,
        string pointer
    )
    {
        RequireList(values, pointer);
        var copy = new List<string>(values!.Count);
        for (int index = 0; index < values.Count; index += 1)
            copy.Add(RequireString(values[index], $"{pointer}/{index}"));
        return copy.AsReadOnly();
    }

    private static IReadOnlyList<ItemTraitRollGroupImportModel> CopyTraitRollGroups(
        IReadOnlyList<ItemTraitRollGroupJsonDto>? values,
        string pointer
    )
    {
        RequireList(values, pointer);
        var copy = new List<ItemTraitRollGroupImportModel>(values!.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            string itemPointer = $"{pointer}/{index}";
            ItemTraitRollGroupJsonDto value = RequireObject(values[index], itemPointer);
            copy.Add(
                new ItemTraitRollGroupImportModel(
                    RequireString(value.GroupId, itemPointer + "/group_id"),
                    value.RollCount,
                    CopyTraitRollEntries(value.Entries, itemPointer + "/entries")
                )
            );
        }
        return copy.AsReadOnly();
    }

    private static IReadOnlyList<ItemTraitRollGroupEntryImportModel> CopyTraitRollEntries(
        IReadOnlyList<ItemTraitRollGroupEntryJsonDto>? values,
        string pointer
    )
    {
        RequireList(values, pointer);
        var copy = new List<ItemTraitRollGroupEntryImportModel>(values!.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            string itemPointer = $"{pointer}/{index}";
            ItemTraitRollGroupEntryJsonDto value = RequireObject(values[index], itemPointer);
            copy.Add(
                new ItemTraitRollGroupEntryImportModel(
                    RequireString(value.TraitId, itemPointer + "/trait_id"),
                    value.Weight,
                    RequireString(value.ExclusiveGroup, itemPointer + "/exclusive_group")
                )
            );
        }
        return copy.AsReadOnly();
    }

    private static IReadOnlyList<ItemAttributeModifierImportModel> CopyAttributeModifiers(
        IReadOnlyList<ItemAttributeModifierJsonDto>? values,
        string pointer
    )
    {
        RequireList(values, pointer);
        var copy = new List<ItemAttributeModifierImportModel>(values!.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            string itemPointer = $"{pointer}/{index}";
            ItemAttributeModifierJsonDto value = RequireObject(values[index], itemPointer);
            copy.Add(
                new ItemAttributeModifierImportModel(
                    RequireString(value.AttributeId, itemPointer + "/attribute_id"),
                    RequireString(value.Mode, itemPointer + "/mode"),
                    value.Value,
                    value.ValuePerRank,
                    RequireString(value.SourceType, itemPointer + "/source_type"),
                    RequireString(value.SourceId, itemPointer + "/source_id")
                )
            );
        }
        return copy.AsReadOnly();
    }

    private static ItemEquipmentRequirementImportModel? CopyEquipmentRequirement(
        ItemEquipmentRequirementJsonDto? value,
        string pointer
    )
    {
        if (value == null)
            return null;

        IReadOnlyList<ItemEquipmentAttributeRequirementJsonDto>? rawAttributes =
            value.AttributeRequirements;
        RequireList(rawAttributes, pointer + "/attribute_requirements");
        var attributes = new List<ItemEquipmentAttributeRequirementImportModel>(
            rawAttributes!.Count
        );
        for (int index = 0; index < rawAttributes.Count; index += 1)
        {
            string itemPointer = $"{pointer}/attribute_requirements/{index}";
            ItemEquipmentAttributeRequirementJsonDto attribute = RequireObject(
                rawAttributes[index],
                itemPointer
            );
            attributes.Add(
                new ItemEquipmentAttributeRequirementImportModel(
                    RequireString(attribute.AttributeId, itemPointer + "/attribute_id"),
                    attribute.MinValue
                )
            );
        }

        return new ItemEquipmentRequirementImportModel(
            CopyStrings(value.RequiredProfessionIds, pointer + "/required_profession_ids"),
            value.MinBodySize,
            value.MaxBodySize,
            attributes.AsReadOnly()
        );
    }

    private static ItemWeaponProfileImportModel? CopyWeaponProfile(
        ItemWeaponProfileJsonDto? value,
        string pointer
    )
    {
        if (value == null)
            return null;

        return new ItemWeaponProfileImportModel(
            RequireString(value.WeaponTypeId, pointer + "/weapon_type_id"),
            RequireString(value.TrainingGroup, pointer + "/training_group"),
            RequireString(value.RangeType, pointer + "/range_type"),
            RequireString(value.Family, pointer + "/family"),
            RequireString(value.DamageTag, pointer + "/damage_tag"),
            value.AttackRange,
            CopyDice(value.OneHandedDice),
            CopyDice(value.TwoHandedDice),
            CopyStrings(value.Properties, pointer + "/properties")
        );
    }

    private static ItemWeaponDamageDiceImportModel? CopyDice(
        ItemWeaponDamageDiceJsonDto? value
    ) => value == null
        ? null
        : new ItemWeaponDamageDiceImportModel(
            value.DiceCount,
            value.DiceSides,
            value.FlatBonus
        );

    private static string RequireString(string? value, string pointer) =>
        value ?? throw new ItemImportShapeException(pointer, "JSON string value must not be null.");

    private static T RequireObject<T>(T? value, string pointer)
        where T : class => value ?? throw new ItemImportShapeException(
            pointer,
            "JSON object value must not be null."
        );

    private static void RequireList<T>(IReadOnlyList<T>? values, string pointer)
    {
        if (values == null)
            throw new ItemImportShapeException(pointer, "JSON array value must not be null.");
    }

    private sealed class ItemImportShapeException : Exception
    {
        internal ItemImportShapeException(string relativePointer, string message)
            : base(message)
        {
            RelativePointer = relativePointer;
        }

        internal string RelativePointer { get; }
    }
}
