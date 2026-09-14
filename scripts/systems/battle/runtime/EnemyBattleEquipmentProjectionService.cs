using System;
using System.Collections.Generic;
using Godot;

internal static class EnemyBattleEquipmentProjectionService
{
    private static readonly IReadOnlyDictionary<StringName, int> EmptyIntMap =
        new Dictionary<StringName, int>();

    internal static EquipmentState BuildEquipmentState(
        EnemyTemplateDefinition template,
        StringName unitId,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        var equipment = new EquipmentState();
        foreach (
            EnemyBattleEquipmentDefinition entry in template?.BattleEquipmentEntries
                ?? Array.Empty<EnemyBattleEquipmentDefinition>()
        )
        {
            if (
                entry == null
                || entry.SlotId == ""
                || entry.ItemId == ""
                || itemDefinitions == null
                || !itemDefinitions.TryGetValue(entry.ItemId, out ItemDefinition itemDefinition)
                || itemDefinition == null
            )
            {
                continue;
            }
            StringName instanceId = new(
                $"enemy_battle_only::{unitId}::{entry.SlotId}::{entry.ItemId}"
            );
            EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
                entry.ItemId,
                instanceId
            );
            instance.rarity = entry.Rarity;
            instance.current_durability = entry.CurrentDurability;
            equipment.SetEquippedEntry(
                entry.SlotId,
                entry.ItemId,
                itemDefinition.GetFinalOccupiedSlotIdsTyped(entry.SlotId),
                instance
            );
        }
        return equipment;
    }

    internal static AttributeSnapshot BuildAttributeSnapshot(
        EnemyTemplateDefinition template,
        EquipmentState equipment,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        IReadOnlyDictionary<StringName, int> baseAttributes =
            template?.BaseAttributeOverrides ?? EmptyIntMap;
        var unitProgress = new UnitProgress();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            unitProgress.unit_base_attributes.SetAttributeValue(
                attributeId,
                baseAttributes.TryGetValue(attributeId, out int value) ? value : 0
            );
        }
        IReadOnlyDictionary<StringName, int> stats =
            template?.AttributeOverrides ?? EmptyIntMap;
        ApplyAcComponentOverrides(unitProgress, stats);
        var attributeService = new AttributeService();
        attributeService.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = unitProgress,
                equipment_state = BuildAttributeModifiers(equipment, itemDefinitions),
            }
        );
        AttributeSnapshot snapshot = attributeService.GetSnapshot();
        ApplyAttributeOverrides(snapshot, stats);
        ApplyDerivedCombatStats(snapshot, template, stats);
        ApplyTargetRank(snapshot, template?.TargetRankKind ?? EnemyTargetRankKind.Normal);
        return snapshot;
    }

    private static IReadOnlyList<AttributeModifierDefinition> BuildAttributeModifiers(
        EquipmentState equipment,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        var result = new List<AttributeModifierDefinition>();
        if (equipment == null || itemDefinitions == null)
            return result;
        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            StringName itemId = equipment.GetEquippedItemId(entrySlotId);
            if (
                itemId == ""
                || !itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
                || itemDefinition == null
                || !itemDefinition.IsEquipment()
            )
            {
                continue;
            }
            foreach (AttributeModifierDefinition modifier in itemDefinition.GetAttributeModifiersTyped())
            {
                if (modifier != null)
                    result.Add(modifier);
            }
            if (itemDefinition.IsArmor() && itemDefinition.GetMaxDexBonus() >= 0)
            {
                result.Add(
                    new AttributeModifierDefinition(
                        AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus),
                        AttributeModifierContentRules.ToStringName(AttributeModifierMode.Flat),
                        itemDefinition.GetMaxDexBonus(),
                        0,
                        "equipment",
                        itemDefinition.ItemId
                    )
                );
            }
        }
        return result;
    }

    private static void ApplyDerivedCombatStats(
        AttributeSnapshot snapshot,
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, int> declaredStats
    )
    {
        if (snapshot == null || template == null)
            return;
        StringName hpMaxId = AttributeService.ToStringName(AttributeIdKind.HpMax);
        if (!declaredStats.ContainsKey(hpMaxId))
            snapshot.SetValue(hpMaxId, template.DerivedHpMax);
        StringName attackBonusId = AttributeService.ToStringName(AttributeIdKind.AttackBonus);
        if (!declaredStats.ContainsKey(attackBonusId))
            snapshot.SetValue(attackBonusId, template.DerivedAttackBonus);
    }

    private static void ApplyAttributeOverrides(
        AttributeSnapshot snapshot,
        IReadOnlyDictionary<StringName, int> stats
    )
    {
        if (snapshot == null || stats == null)
            return;
        foreach ((StringName attributeId, int configuredValue) in stats)
        {
            int value = configuredValue;
            if (attributeId == AttributeService.ToStringName(AttributeIdKind.HpMax))
                value = Mathf.Max(value, 1);
            else if (
                attributeId == AttributeService.ToStringName(AttributeIdKind.MpMax)
                || attributeId == AttributeService.ToStringName(AttributeIdKind.StaminaMax)
                || attributeId == AttributeService.ToStringName(AttributeIdKind.AuraMax)
            )
                value = Mathf.Max(value, 0);
            else if (attributeId == AttributeService.ToStringName(AttributeIdKind.ActionPoints))
                value = Mathf.Max(value, 1);
            snapshot.SetValue(attributeId, value);
        }
    }

    private static void ApplyAcComponentOverrides(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, int> stats
    )
    {
        if (unitProgress?.unit_base_attributes == null || stats == null)
            return;
        foreach (StringName componentId in AttributeContentRules.ArmorClassComponentAttributeIds)
        {
            if (stats.TryGetValue(componentId, out int componentValue))
            {
                unitProgress.unit_base_attributes.SetAttributeValue(
                    componentId,
                    Mathf.Max(componentValue, 0)
                );
            }
        }
    }

    private static void ApplyTargetRank(
        AttributeSnapshot snapshot,
        EnemyTargetRankKind targetRank
    )
    {
        if (snapshot == null)
            return;
        snapshot.SetValue("fortune_mark_target", targetRank switch
        {
            EnemyTargetRankKind.Boss => 2,
            EnemyTargetRankKind.Elite => 1,
            _ => 0,
        });
        snapshot.SetValue("boss_target", targetRank == EnemyTargetRankKind.Boss ? 1 : 0);
    }
}
