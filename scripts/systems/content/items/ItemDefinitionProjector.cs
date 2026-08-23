#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal static class ItemDefinitionProjector
{
    internal static ItemDefinition Project(ItemImportModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ItemDefinition(
            new StringName(source.ItemId),
            source.DisplayName,
            source.Description,
            source.IconAssetId,
            source.IsStackable,
            source.BasePrice,
            source.BuyPrice,
            source.SellPrice,
            source.Sellable,
            source.MaxStack,
            new StringName(source.ItemCategory),
            ToStringNames(source.Tags),
            ToStringNames(source.CraftingGroups),
            ToStringNames(source.QuestGroups),
            ToStringNames(source.TraitIds),
            ProjectTraitRollGroups(source.TraitRollGroups),
            new List<string>(source.EquipmentSlotIds),
            ProjectAttributeModifiers(source.AttributeModifiers),
            new StringName(source.GrantedSkillId),
            new List<string>(source.OccupiedSlotIds),
            ProjectRequirement(source.EquipRequirement),
            new StringName(source.EquipmentTypeId),
            ProjectWeaponProfile(source.WeaponProfile),
            source.MaxDexBonus
        );
    }

    private static IReadOnlyList<StringName> ToStringNames(IReadOnlyList<string> source)
    {
        var result = new List<StringName>(source.Count);
        foreach (string value in source)
            result.Add(new StringName(value));
        return result;
    }

    private static IReadOnlyList<TraitRollGroupDefinition> ProjectTraitRollGroups(
        IReadOnlyList<ItemTraitRollGroupImportModel> source
    )
    {
        var result = new List<TraitRollGroupDefinition>(source.Count);
        foreach (ItemTraitRollGroupImportModel group in source)
        {
            var entries = new List<TraitRollGroupEntryDefinition>(group.Entries.Count);
            foreach (ItemTraitRollGroupEntryImportModel entry in group.Entries)
            {
                entries.Add(
                    new TraitRollGroupEntryDefinition(
                        new StringName(entry.TraitId),
                        entry.Weight,
                        new StringName(entry.ExclusiveGroup)
                    )
                );
            }
            result.Add(
                new TraitRollGroupDefinition(
                    new StringName(group.GroupId),
                    group.RollCount,
                    entries
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<AttributeModifierDefinition> ProjectAttributeModifiers(
        IReadOnlyList<ItemAttributeModifierImportModel> source
    )
    {
        var result = new List<AttributeModifierDefinition>(source.Count);
        foreach (ItemAttributeModifierImportModel modifier in source)
        {
            result.Add(
                new AttributeModifierDefinition(
                    new StringName(modifier.AttributeId),
                    new StringName(modifier.Mode),
                    modifier.Value,
                    modifier.ValuePerRank,
                    new StringName(modifier.SourceType),
                    new StringName(modifier.SourceId)
                )
            );
        }
        return result;
    }

    private static EquipmentRequirementDefinition? ProjectRequirement(
        ItemEquipmentRequirementImportModel? source
    )
    {
        if (source == null)
            return null;
        var attributes = new List<EquipmentAttributeRequirementDefinition>(
            source.AttributeRequirements.Count
        );
        foreach (ItemEquipmentAttributeRequirementImportModel attribute in source.AttributeRequirements)
        {
            attributes.Add(
                new EquipmentAttributeRequirementDefinition(
                    new StringName(attribute.AttributeId),
                    attribute.MinValue
                )
            );
        }
        return new EquipmentRequirementDefinition(
            new List<string>(source.RequiredProfessionIds),
            source.MinBodySize,
            source.MaxBodySize,
            attributes
        );
    }

    private static WeaponProfileDefinition? ProjectWeaponProfile(
        ItemWeaponProfileImportModel? source
    )
    {
        if (source == null)
            return null;
        return new WeaponProfileDefinition(
            new StringName(source.WeaponTypeId),
            new StringName(source.TrainingGroup),
            new StringName(source.RangeType),
            new StringName(source.Family),
            new StringName(source.DamageTag),
            source.AttackRange,
            ProjectDice(source.OneHandedDice),
            ProjectDice(source.TwoHandedDice),
            (int)WeaponProfileDefinition.PropertyMergeMode.REPLACE,
            ToStringNames(source.Properties)
        );
    }

    private static WeaponDamageDiceDefinition? ProjectDice(
        ItemWeaponDamageDiceImportModel? source
    ) => source == null
        ? null
        : new WeaponDamageDiceDefinition(source.DiceCount, source.DiceSides, source.FlatBonus);
}
