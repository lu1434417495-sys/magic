#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemJsonDto
{
    [JsonPropertyName("item_id")]
    [JsonRequired]
    public string ItemId { get; init; } = null!;

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = null!;

    [JsonPropertyName("description")]
    [JsonRequired]
    public string Description { get; init; } = null!;

    [JsonPropertyName("icon_asset_id")]
    [JsonRequired]
    public string IconAssetId { get; init; } = null!;

    [JsonPropertyName("is_stackable")]
    [JsonRequired]
    public bool IsStackable { get; init; }

    [JsonPropertyName("base_price")]
    [JsonRequired]
    public int BasePrice { get; init; }

    [JsonPropertyName("buy_price")]
    [JsonRequired]
    public int BuyPrice { get; init; }

    [JsonPropertyName("sell_price")]
    [JsonRequired]
    public int SellPrice { get; init; }

    [JsonPropertyName("sellable")]
    [JsonRequired]
    public bool Sellable { get; init; }

    [JsonPropertyName("max_stack")]
    [JsonRequired]
    public int MaxStack { get; init; }

    [JsonPropertyName("item_category")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(ItemCategorySchemaValues))]
    public string ItemCategory { get; init; } = null!;

    [JsonPropertyName("tags")]
    [JsonRequired]
    public IReadOnlyList<string> Tags { get; init; } = null!;

    [JsonPropertyName("crafting_groups")]
    [JsonRequired]
    public IReadOnlyList<string> CraftingGroups { get; init; } = null!;

    [JsonPropertyName("quest_groups")]
    [JsonRequired]
    public IReadOnlyList<string> QuestGroups { get; init; } = null!;

    [JsonPropertyName("trait_ids")]
    [JsonRequired]
    public IReadOnlyList<string> TraitIds { get; init; } = null!;

    [JsonPropertyName("trait_roll_groups")]
    [JsonRequired]
    public IReadOnlyList<ItemTraitRollGroupJsonDto> TraitRollGroups { get; init; } = null!;

    [JsonPropertyName("equipment_slot_ids")]
    [JsonRequired]
    public IReadOnlyList<string> EquipmentSlotIds { get; init; } = null!;

    [JsonPropertyName("attribute_modifiers")]
    [JsonRequired]
    public IReadOnlyList<ItemAttributeModifierJsonDto> AttributeModifiers { get; init; } = null!;

    [JsonPropertyName("granted_skill_id")]
    [JsonRequired]
    public string GrantedSkillId { get; init; } = null!;

    [JsonPropertyName("occupied_slot_ids")]
    [JsonRequired]
    public IReadOnlyList<string> OccupiedSlotIds { get; init; } = null!;

    [JsonPropertyName("equip_requirement")]
    [JsonRequired]
    public ItemEquipmentRequirementJsonDto? EquipRequirement { get; init; }

    [JsonPropertyName("equipment_type_id")]
    [JsonRequired]
    public string EquipmentTypeId { get; init; } = null!;

    [JsonPropertyName("weapon_profile")]
    [JsonRequired]
    public ItemWeaponProfileJsonDto? WeaponProfile { get; init; }

    [JsonPropertyName("max_dex_bonus")]
    [JsonRequired]
    public int MaxDexBonus { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemTraitRollGroupJsonDto
{
    [JsonPropertyName("group_id")]
    [JsonRequired]
    public string GroupId { get; init; } = null!;

    [JsonPropertyName("roll_count")]
    [JsonRequired]
    public int RollCount { get; init; }

    [JsonPropertyName("entries")]
    [JsonRequired]
    public IReadOnlyList<ItemTraitRollGroupEntryJsonDto> Entries { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemTraitRollGroupEntryJsonDto
{
    [JsonPropertyName("trait_id")]
    [JsonRequired]
    public string TraitId { get; init; } = null!;

    [JsonPropertyName("weight")]
    [JsonRequired]
    public int Weight { get; init; }

    [JsonPropertyName("exclusive_group")]
    [JsonRequired]
    public string ExclusiveGroup { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemAttributeModifierJsonDto
{
    [JsonPropertyName("attribute_id")]
    [JsonRequired]
    public string AttributeId { get; init; } = null!;

    [JsonPropertyName("mode")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(ItemAttributeModifierModeSchemaValues))]
    public string Mode { get; init; } = null!;

    [JsonPropertyName("value")]
    [JsonRequired]
    public int Value { get; init; }

    [JsonPropertyName("value_per_rank")]
    [JsonRequired]
    public int ValuePerRank { get; init; }

    [JsonPropertyName("source_type")]
    [JsonRequired]
    public string SourceType { get; init; } = null!;

    [JsonPropertyName("source_id")]
    [JsonRequired]
    public string SourceId { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemEquipmentRequirementJsonDto
{
    [JsonPropertyName("required_profession_ids")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredProfessionIds { get; init; } = null!;

    [JsonPropertyName("min_body_size")]
    [JsonRequired]
    public int MinBodySize { get; init; }

    [JsonPropertyName("max_body_size")]
    [JsonRequired]
    public int MaxBodySize { get; init; }

    [JsonPropertyName("attribute_requirements")]
    [JsonRequired]
    public IReadOnlyList<ItemEquipmentAttributeRequirementJsonDto> AttributeRequirements { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemEquipmentAttributeRequirementJsonDto
{
    [JsonPropertyName("attribute_id")]
    [JsonRequired]
    public string AttributeId { get; init; } = null!;

    [JsonPropertyName("min_value")]
    [JsonRequired]
    public int MinValue { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemWeaponProfileJsonDto
{
    [JsonPropertyName("weapon_type_id")]
    [JsonRequired]
    public string WeaponTypeId { get; init; } = null!;

    [JsonPropertyName("training_group")]
    [JsonRequired]
    public string TrainingGroup { get; init; } = null!;

    [JsonPropertyName("range_type")]
    [JsonRequired]
    public string RangeType { get; init; } = null!;

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = null!;

    [JsonPropertyName("damage_tag")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(ItemWeaponDamageTagSchemaValues))]
    public string DamageTag { get; init; } = null!;

    [JsonPropertyName("attack_range")]
    [JsonRequired]
    public int AttackRange { get; init; }

    [JsonPropertyName("one_handed_dice")]
    [JsonRequired]
    public ItemWeaponDamageDiceJsonDto? OneHandedDice { get; init; }

    [JsonPropertyName("two_handed_dice")]
    [JsonRequired]
    public ItemWeaponDamageDiceJsonDto? TwoHandedDice { get; init; }

    [JsonPropertyName("properties")]
    [JsonRequired]
    public IReadOnlyList<string> Properties { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemWeaponDamageDiceJsonDto
{
    [JsonPropertyName("dice_count")]
    [JsonRequired]
    public int DiceCount { get; init; }

    [JsonPropertyName("dice_sides")]
    [JsonRequired]
    public int DiceSides { get; init; }

    [JsonPropertyName("flat_bonus")]
    [JsonRequired]
    public int FlatBonus { get; init; }
}

internal sealed class ItemCategorySchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => ItemImportValueRules.ItemCategoryValues;
}

internal sealed class ItemWeaponDamageTagSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => ItemImportValueRules.WeaponDamageTagValues;
}

internal sealed class ItemAttributeModifierModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => ItemImportValueRules.AttributeModifierModeValues;
}

[Description("Flat, fully resolved item authoring document. Legacy base_item_id and item-template merge controls are not part of this contract.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ItemJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(ItemContentJsonAuthoringDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(ItemContentJsonAuthoringDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, ItemJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, ItemJsonDto>(new Dictionary<string, ItemJsonDto>());

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        ItemContentJsonAuthoringDomain.EntryIdPropertyName,
        "template"
    )]
    public IReadOnlyList<ItemJsonDto> Entries { get; init; } = Array.Empty<ItemJsonDto>();
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(ItemJsonDto))]
[JsonSerializable(typeof(ItemTraitRollGroupJsonDto))]
[JsonSerializable(typeof(ItemTraitRollGroupEntryJsonDto))]
[JsonSerializable(typeof(ItemAttributeModifierJsonDto))]
[JsonSerializable(typeof(ItemEquipmentRequirementJsonDto))]
[JsonSerializable(typeof(ItemEquipmentAttributeRequirementJsonDto))]
[JsonSerializable(typeof(ItemWeaponProfileJsonDto))]
[JsonSerializable(typeof(ItemWeaponDamageDiceJsonDto))]
internal partial class ItemJsonImportSerializerContext : JsonSerializerContext { }
