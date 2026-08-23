#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed class ItemImportModelValidator
{
    internal const string ItemIdRequiredRule = "item.validation.item_id_required";
    internal const string IconAssetIdPathRule = "item.validation.icon_asset_id_path_forbidden";
    internal const string CategoryRule = "item.validation.item_category";
    internal const string MaxStackRule = "item.validation.max_stack";
    internal const string BuyPriceRule = "item.validation.buy_price_required";
    internal const string SellPriceRule = "item.validation.sell_price_required";
    internal const string MaterialCraftingGroupRule = "item.validation.material_crafting_group";
    internal const string QuestGroupRule = "item.validation.quest_group";
    internal const string SkillBookGrantedSkillRule = "item.validation.skill_book_granted_skill";
    internal const string EquipmentNonStackableRule = "item.validation.equipment_non_stackable";
    internal const string EquipmentSlotRequiredRule = "item.validation.equipment_slot_required";
    internal const string EquipmentSlotRule = "item.validation.equipment_slot";
    internal const string EquipmentTypeRule = "item.validation.equipment_type";
    internal const string WeaponProfileRequiredRule = "item.validation.weapon_profile_required";
    internal const string WeaponTypeRule = "item.validation.weapon_type";
    internal const string WeaponFamilyRule = "item.validation.weapon_family";
    internal const string WeaponRangeTypeRule = "item.validation.weapon_range_type";
    internal const string WeaponDamageTagRequiredRule = "item.validation.weapon_damage_tag_required";
    internal const string WeaponAttackRangeRule = "item.validation.weapon_attack_range";
    internal const string WeaponDiceRequiredRule = "item.validation.weapon_dice_required";
    internal const string WeaponDiceCountRule = "item.validation.weapon_dice_count";
    internal const string WeaponDiceSidesRule = "item.validation.weapon_dice_sides";
    internal const string WeaponDiceFlatBonusRule = "item.validation.weapon_dice_flat_bonus";
    internal const string WeaponDamageTagRule = "item.validation.weapon_damage_tag";
    internal const string AttributeModifierIdRule = "item.validation.attribute_modifier_id";
    internal const string AttributeModifierModeRule = "item.validation.attribute_modifier_mode";
    internal const string OccupiedSlotEntryShapeRule = "item.validation.occupied_slot_entry_shape";
    internal const string OccupiedSlotRule = "item.validation.occupied_slot";
    internal const string OccupiedSlotDuplicateRule = "item.validation.occupied_slot_duplicate";
    internal const string OccupiedSlotMissingEntryRule = "item.validation.occupied_slot_missing_entry";

    internal static IReadOnlyList<string> RuleInventory { get; } =
        new ReadOnlyCollection<string>(
            new[]
            {
                ItemIdRequiredRule,
                IconAssetIdPathRule,
                CategoryRule,
                MaxStackRule,
                BuyPriceRule,
                SellPriceRule,
                MaterialCraftingGroupRule,
                QuestGroupRule,
                SkillBookGrantedSkillRule,
                EquipmentNonStackableRule,
                EquipmentSlotRequiredRule,
                EquipmentSlotRule,
                EquipmentTypeRule,
                WeaponProfileRequiredRule,
                WeaponTypeRule,
                WeaponFamilyRule,
                WeaponRangeTypeRule,
                WeaponDamageTagRequiredRule,
                WeaponAttackRangeRule,
                WeaponDiceRequiredRule,
                WeaponDiceCountRule,
                WeaponDiceSidesRule,
                WeaponDiceFlatBonusRule,
                WeaponDamageTagRule,
                AttributeModifierIdRule,
                AttributeModifierModeRule,
                OccupiedSlotEntryShapeRule,
                OccupiedSlotRule,
                OccupiedSlotDuplicateRule,
                OccupiedSlotMissingEntryRule,
            }
        );

    internal IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        ItemImportModel item
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(item);

        var diagnostics = new List<ContentJsonDiagnostic>();
        void Add(
            string ruleId,
            string message,
            string relativePointer,
            string expected = "",
            string actual = ""
        ) => diagnostics.Add(
            new ContentJsonDiagnostic(
                ruleId,
                message,
                context.SourceLabel,
                context.JsonPointer + relativePointer,
                expected,
                actual
            )
        );

        if (string.IsNullOrWhiteSpace(item.ItemId))
        {
            Add(ItemIdRequiredRule, "Item item_id must be non-empty.", "/item_id", "non-empty", item.ItemId);
        }
        if (IsPathLike(item.IconAssetId))
        {
            Add(
                IconAssetIdPathRule,
                $"Item {item.ItemId} icon_asset_id must be a catalog ID, not resource path {item.IconAssetId}.",
                "/icon_asset_id",
                "engine asset ID",
                item.IconAssetId
            );
        }
        if (!ItemImportValueRules.IsKnownCategory(item.ItemCategory))
        {
            Add(
                CategoryRule,
                $"Item {item.ItemId} declares unsupported item_category {item.ItemCategory}; expected one of misc / equipment / skill_book.",
                "/item_category",
                "misc|equipment|skill_book",
                item.ItemCategory
            );
        }
        if (item.IsStackable && item.MaxStack <= 0)
        {
            Add(MaxStackRule, $"Item {item.ItemId} must have max_stack >= 1.", "/max_stack", ">=1", item.MaxStack.ToString());
        }
        if (item.BasePrice > 0 && item.Sellable && item.BuyPrice <= 0)
        {
            Add(BuyPriceRule, $"Sellable item {item.ItemId} must declare explicit buy_price.", "/buy_price", ">0", item.BuyPrice.ToString());
        }
        if (item.BasePrice > 0 && item.Sellable && item.SellPrice <= 0)
        {
            Add(SellPriceRule, $"Sellable item {item.ItemId} must declare explicit sell_price.", "/sell_price", ">0", item.SellPrice.ToString());
        }
        if (Contains(item.Tags, "material") && item.CraftingGroups.Count == 0)
        {
            Add(MaterialCraftingGroupRule, $"Material item {item.ItemId} must declare at least one crafting_group.", "/crafting_groups", "non-empty", "empty");
        }
        if (Contains(item.Tags, "quest_item") && item.QuestGroups.Count == 0)
        {
            Add(QuestGroupRule, $"Quest item {item.ItemId} must declare at least one quest_group.", "/quest_groups", "non-empty", "empty");
        }
        if (item.ItemCategory == ItemImportValueRules.CategorySkillBook && item.GrantedSkillId.Length == 0)
        {
            Add(SkillBookGrantedSkillRule, $"Skill book item {item.ItemId} must declare granted_skill_id.", "/granted_skill_id", "non-empty", "empty");
        }

        if (item.ItemCategory == ItemImportValueRules.CategoryEquipment)
            ValidateEquipment(item, Add);

        return diagnostics.AsReadOnly();
    }

    private static void ValidateEquipment(
        ItemImportModel item,
        Action<string, string, string, string, string> add
    )
    {
        if (item.IsStackable || Math.Max(item.MaxStack, 1) != 1)
        {
            add(EquipmentNonStackableRule, $"Equipment item {item.ItemId} must be non-stackable.", "/is_stackable", "false and max_stack=1", $"is_stackable={item.IsStackable},max_stack={item.MaxStack}");
        }
        if (item.EquipmentSlotIds.Count == 0)
        {
            add(EquipmentSlotRequiredRule, $"Equipment item {item.ItemId} must declare at least one slot.", "/equipment_slot_ids", "non-empty", "empty");
        }
        for (int index = 0; index < item.EquipmentSlotIds.Count; index += 1)
        {
            string slot = item.EquipmentSlotIds[index];
            if (ItemImportValueRules.IsKnownEquipmentSlot(slot))
                continue;
            add(EquipmentSlotRule, $"Equipment item {item.ItemId} declares invalid slot {slot}.", $"/equipment_slot_ids/{index}", "known equipment slot", slot);
        }
        if (!ItemImportValueRules.IsKnownEquipmentType(item.EquipmentTypeId))
        {
            add(EquipmentTypeRule, $"Equipment item {item.ItemId} must declare equipment_type_id as weapon, armor, or accessory.", "/equipment_type_id", "weapon|armor|accessory", item.EquipmentTypeId);
        }

        if (item.EquipmentTypeId == ItemImportValueRules.EquipmentTypeWeapon)
            ValidateWeapon(item, add);

        for (int index = 0; index < item.AttributeModifiers.Count; index += 1)
        {
            ItemAttributeModifierImportModel modifier = item.AttributeModifiers[index];
            string pointer = $"/attribute_modifiers/{index}";
            if (modifier.AttributeId.Length == 0)
            {
                add(AttributeModifierIdRule, $"Item {item.ItemId} attribute_modifiers[{index}].attribute_id must be non-empty.", pointer + "/attribute_id", "non-empty", "empty");
            }
            if (!ItemImportValueRules.IsKnownAttributeModifierMode(modifier.Mode))
            {
                add(AttributeModifierModeRule, $"Item {item.ItemId} attribute_modifiers[{index}].mode uses unsupported value {modifier.Mode}.", pointer + "/mode", "flat|percent", modifier.Mode);
            }
        }

        ValidateOccupiedSlots(item, add);
    }

    private static void ValidateWeapon(
        ItemImportModel item,
        Action<string, string, string, string, string> add
    )
    {
        ItemWeaponProfileImportModel? profile = item.WeaponProfile;
        if (profile == null)
        {
            add(WeaponProfileRequiredRule, $"Weapon item {item.ItemId} must declare weapon_profile.", "/weapon_profile", "object", "null");
            return;
        }

        if (profile.WeaponTypeId.Length == 0)
            add(WeaponTypeRule, $"Weapon item {item.ItemId} weapon_profile.weapon_type_id must be non-empty.", "/weapon_profile/weapon_type_id", "non-empty", "empty");
        if (profile.Family.Length == 0)
            add(WeaponFamilyRule, $"Weapon item {item.ItemId} weapon_profile.family must be non-empty.", "/weapon_profile/family", "non-empty", "empty");
        if (profile.RangeType.Length == 0)
            add(WeaponRangeTypeRule, $"Weapon item {item.ItemId} weapon_profile.range_type must be non-empty.", "/weapon_profile/range_type", "non-empty", "empty");
        if (profile.DamageTag.Length == 0)
            add(WeaponDamageTagRequiredRule, $"Weapon item {item.ItemId} weapon_profile.damage_tag must be non-empty.", "/weapon_profile/damage_tag", "non-empty", "empty");
        if (profile.AttackRange <= 0)
            add(WeaponAttackRangeRule, $"Weapon item {item.ItemId} weapon_profile.attack_range must be >= 1 (got {profile.AttackRange}).", "/weapon_profile/attack_range", ">=1", profile.AttackRange.ToString());
        if (profile.OneHandedDice == null && profile.TwoHandedDice == null)
            add(WeaponDiceRequiredRule, $"Weapon item {item.ItemId} weapon_profile must declare at least one of one_handed_dice or two_handed_dice.", "/weapon_profile", "one dice object", "both null");

        ValidateDice(item.ItemId, "one_handed_dice", profile.OneHandedDice, add);
        ValidateDice(item.ItemId, "two_handed_dice", profile.TwoHandedDice, add);

        if (profile.DamageTag.Length > 0 && !ItemImportValueRules.IsKnownWeaponDamageTag(profile.DamageTag))
            add(WeaponDamageTagRule, $"Weapon item {item.ItemId} must declare one valid weapon_profile.damage_tag.", "/weapon_profile/damage_tag", "physical_slash|physical_pierce|physical_blunt", profile.DamageTag);
    }

    private static void ValidateDice(
        string itemId,
        string field,
        ItemWeaponDamageDiceImportModel? dice,
        Action<string, string, string, string, string> add
    )
    {
        if (dice == null)
            return;
        string pointer = $"/weapon_profile/{field}";
        if (
            dice.DiceCount < ItemImportValueRules.WeaponDiceCountMin
            || dice.DiceCount > ItemImportValueRules.WeaponDiceCountMax
        )
            add(WeaponDiceCountRule, $"Weapon item {itemId} weapon_profile.{field}.dice_count must be {ItemImportValueRules.WeaponDiceCountMin}..{ItemImportValueRules.WeaponDiceCountMax}, got {dice.DiceCount}.", pointer + "/dice_count", $"{ItemImportValueRules.WeaponDiceCountMin}..{ItemImportValueRules.WeaponDiceCountMax}", dice.DiceCount.ToString());
        if (
            dice.DiceSides < ItemImportValueRules.WeaponDiceSidesMin
            || dice.DiceSides > ItemImportValueRules.WeaponDiceSidesMax
        )
            add(WeaponDiceSidesRule, $"Weapon item {itemId} weapon_profile.{field}.dice_sides must be {ItemImportValueRules.WeaponDiceSidesMin}..{ItemImportValueRules.WeaponDiceSidesMax}, got {dice.DiceSides}.", pointer + "/dice_sides", $"{ItemImportValueRules.WeaponDiceSidesMin}..{ItemImportValueRules.WeaponDiceSidesMax}", dice.DiceSides.ToString());
        if (
            dice.FlatBonus < ItemImportValueRules.WeaponDiceFlatBonusMin
            || dice.FlatBonus > ItemImportValueRules.WeaponDiceFlatBonusMax
        )
            add(WeaponDiceFlatBonusRule, $"Weapon item {itemId} weapon_profile.{field}.flat_bonus must be {ItemImportValueRules.WeaponDiceFlatBonusMin}..{ItemImportValueRules.WeaponDiceFlatBonusMax}, got {dice.FlatBonus}.", pointer + "/flat_bonus", $"{ItemImportValueRules.WeaponDiceFlatBonusMin}..{ItemImportValueRules.WeaponDiceFlatBonusMax}", dice.FlatBonus.ToString());
    }

    private static void ValidateOccupiedSlots(
        ItemImportModel item,
        Action<string, string, string, string, string> add
    )
    {
        if (item.OccupiedSlotIds.Count == 0)
            return;
        if (item.EquipmentSlotIds.Count != 1)
        {
            add(OccupiedSlotEntryShapeRule, $"Equipment item {item.ItemId} declares occupied_slot_ids but equipment_slot_ids must be exactly 1 entry slot.", "/equipment_slot_ids", "exactly one", item.EquipmentSlotIds.Count.ToString());
            return;
        }

        string entrySlot = item.EquipmentSlotIds[0];
        bool containsEntry = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < item.OccupiedSlotIds.Count; index += 1)
        {
            string slot = item.OccupiedSlotIds[index];
            string pointer = $"/occupied_slot_ids/{index}";
            if (!ItemImportValueRules.IsKnownEquipmentSlot(slot))
                add(OccupiedSlotRule, $"Equipment item {item.ItemId} declares invalid occupied_slot {slot}.", pointer, "known equipment slot", slot);
            if (!seen.Add(slot))
                add(OccupiedSlotDuplicateRule, $"Equipment item {item.ItemId} declares duplicate occupied_slot {slot}.", pointer, "unique", slot);
            if (string.Equals(slot, entrySlot, StringComparison.Ordinal))
                containsEntry = true;
        }
        if (!containsEntry)
            add(OccupiedSlotMissingEntryRule, $"Equipment item {item.ItemId} occupied_slot_ids must include the entry_slot {entrySlot}.", "/occupied_slot_ids", entrySlot, "missing");
    }

    private static bool Contains(IReadOnlyList<string> values, string expected)
    {
        foreach (string value in values)
        {
            if (string.Equals(value, expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool IsPathLike(string value) =>
        !string.IsNullOrEmpty(value)
        && (
            value.Contains("/", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal)
        );
}
