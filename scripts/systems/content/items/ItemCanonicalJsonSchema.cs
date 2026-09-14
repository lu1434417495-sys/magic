#nullable enable

using System.Collections.Generic;

internal static class ItemCanonicalJsonSchema
{
    private static readonly ContentCanonicalJsonValueSchema<string> Text =
        ContentCanonicalJsonValue.Text;
    private static readonly ContentCanonicalJsonValueSchema<IReadOnlyList<string>> TextArray =
        ContentCanonicalJsonValue.Array(Text);

    private static readonly ContentCanonicalJsonValueSchema<ItemTraitRollGroupEntryImportModel>
        TraitRollEntry = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ItemTraitRollGroupEntryImportModel>(
                ContentCanonicalJsonProperty<ItemTraitRollGroupEntryImportModel>.Required(
                    "trait_id", static value => value.TraitId, Text
                ),
                ContentCanonicalJsonProperty<ItemTraitRollGroupEntryImportModel>.Required(
                    "weight", static value => value.Weight, ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemTraitRollGroupEntryImportModel>.Required(
                    "exclusive_group", static value => value.ExclusiveGroup, Text
                )
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<ItemTraitRollGroupImportModel>
        TraitRollGroup = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ItemTraitRollGroupImportModel>(
                ContentCanonicalJsonProperty<ItemTraitRollGroupImportModel>.Required(
                    "group_id", static value => value.GroupId, Text
                ),
                ContentCanonicalJsonProperty<ItemTraitRollGroupImportModel>.Required(
                    "roll_count", static value => value.RollCount, ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemTraitRollGroupImportModel>.Required(
                    "entries",
                    static value => value.Entries,
                    ContentCanonicalJsonValue.Array(TraitRollEntry)
                )
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<ItemAttributeModifierImportModel>
        AttributeModifier = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ItemAttributeModifierImportModel>(
                ContentCanonicalJsonProperty<ItemAttributeModifierImportModel>.Required(
                    "attribute_id", static value => value.AttributeId, Text
                ),
                ContentCanonicalJsonProperty<ItemAttributeModifierImportModel>.Required(
                    "mode", static value => value.Mode, Text
                ),
                ContentCanonicalJsonProperty<ItemAttributeModifierImportModel>.Required(
                    "value", static value => value.Value, ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemAttributeModifierImportModel>.Required(
                    "value_per_rank", static value => value.ValuePerRank,
                    ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemAttributeModifierImportModel>.Required(
                    "source_type", static value => value.SourceType, Text
                ),
                ContentCanonicalJsonProperty<ItemAttributeModifierImportModel>.Required(
                    "source_id", static value => value.SourceId, Text
                )
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<
        ItemEquipmentAttributeRequirementImportModel
    > EquipmentAttributeRequirement = ContentCanonicalJsonValue.Object(
        new ContentCanonicalJsonObjectSchema<ItemEquipmentAttributeRequirementImportModel>(
            ContentCanonicalJsonProperty<ItemEquipmentAttributeRequirementImportModel>.Required(
                "attribute_id", static value => value.AttributeId, Text
            ),
            ContentCanonicalJsonProperty<ItemEquipmentAttributeRequirementImportModel>.Required(
                "min_value", static value => value.MinValue, ContentCanonicalJsonValue.Int32
            )
        )
    );

    private static readonly ContentCanonicalJsonValueSchema<ItemEquipmentRequirementImportModel>
        EquipmentRequirement = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ItemEquipmentRequirementImportModel>(
                ContentCanonicalJsonProperty<ItemEquipmentRequirementImportModel>.Required(
                    "required_profession_ids",
                    static value => value.RequiredProfessionIds,
                    TextArray
                ),
                ContentCanonicalJsonProperty<ItemEquipmentRequirementImportModel>.Required(
                    "min_body_size", static value => value.MinBodySize,
                    ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemEquipmentRequirementImportModel>.Required(
                    "max_body_size", static value => value.MaxBodySize,
                    ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemEquipmentRequirementImportModel>.Required(
                    "attribute_requirements",
                    static value => value.AttributeRequirements,
                    ContentCanonicalJsonValue.Array(EquipmentAttributeRequirement)
                )
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<ItemWeaponDamageDiceImportModel>
        WeaponDice = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ItemWeaponDamageDiceImportModel>(
                ContentCanonicalJsonProperty<ItemWeaponDamageDiceImportModel>.Required(
                    "dice_count", static value => value.DiceCount, ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemWeaponDamageDiceImportModel>.Required(
                    "dice_sides", static value => value.DiceSides, ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemWeaponDamageDiceImportModel>.Required(
                    "flat_bonus", static value => value.FlatBonus, ContentCanonicalJsonValue.Int32
                )
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<ItemWeaponProfileImportModel>
        WeaponProfile = ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<ItemWeaponProfileImportModel>(
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "weapon_type_id", static value => value.WeaponTypeId, Text
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "training_group", static value => value.TrainingGroup, Text
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "range_type", static value => value.RangeType, Text
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "family", static value => value.Family, Text
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "damage_tag", static value => value.DamageTag, Text
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "attack_range", static value => value.AttackRange,
                    ContentCanonicalJsonValue.Int32
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "one_handed_dice",
                    static value => value.OneHandedDice!,
                    ContentCanonicalJsonValue.NullableReference(WeaponDice)
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "two_handed_dice",
                    static value => value.TwoHandedDice!,
                    ContentCanonicalJsonValue.NullableReference(WeaponDice)
                ),
                ContentCanonicalJsonProperty<ItemWeaponProfileImportModel>.Required(
                    "properties", static value => value.Properties, TextArray
                )
            )
        );

    internal static ContentCanonicalJsonObjectSchema<ItemImportModel> EntrySchema { get; } =
        new(
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "item_id", static value => value.ItemId, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "display_name", static value => value.DisplayName, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "description", static value => value.Description, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "icon_asset_id", static value => value.IconAssetId, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "is_stackable", static value => value.IsStackable,
                ContentCanonicalJsonValue.Boolean
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "base_price", static value => value.BasePrice, ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "buy_price", static value => value.BuyPrice, ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "sell_price", static value => value.SellPrice, ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "sellable", static value => value.Sellable, ContentCanonicalJsonValue.Boolean
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "max_stack", static value => value.MaxStack, ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "item_category", static value => value.ItemCategory, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "tags", static value => value.Tags, TextArray
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "crafting_groups", static value => value.CraftingGroups, TextArray
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "quest_groups", static value => value.QuestGroups, TextArray
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "trait_ids", static value => value.TraitIds, TextArray
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "trait_roll_groups",
                static value => value.TraitRollGroups,
                ContentCanonicalJsonValue.Array(TraitRollGroup)
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "equipment_slot_ids", static value => value.EquipmentSlotIds, TextArray
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "attribute_modifiers",
                static value => value.AttributeModifiers,
                ContentCanonicalJsonValue.Array(AttributeModifier)
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "granted_skill_id", static value => value.GrantedSkillId, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "occupied_slot_ids", static value => value.OccupiedSlotIds, TextArray
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "equip_requirement",
                static value => value.EquipRequirement!,
                ContentCanonicalJsonValue.NullableReference(EquipmentRequirement)
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "equipment_type_id", static value => value.EquipmentTypeId, Text
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "weapon_profile",
                static value => value.WeaponProfile!,
                ContentCanonicalJsonValue.NullableReference(WeaponProfile)
            ),
            ContentCanonicalJsonProperty<ItemImportModel>.Required(
                "max_dex_bonus", static value => value.MaxDexBonus,
                ContentCanonicalJsonValue.Int32
            )
        );
}
