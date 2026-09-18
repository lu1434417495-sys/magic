using System;
using System.Collections.Generic;
using Godot;

// Presentation only: consume the owner's effective instances without reapplying stack rules.
internal static class CharacterTraitDisplayText
{
    internal static IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildEntries(
        IEnumerable<BattleEffectiveTraitInstanceReadView> instances,
        EquipmentState equipment,
        ProgressionIdentityCatalogData identity,
        Func<StringName, TraitDefinition> getTrait,
        Func<StringName, SkillDefinition> getSkill,
        Func<StringName, ItemDefinition> getItem)
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        foreach (BattleEffectiveTraitInstanceReadView instance in instances)
        {
            if (!instance.IsPresent)
                continue;
            TraitDefinition definition = getTrait(instance.TraitId);
            string name = string.IsNullOrWhiteSpace(definition?.DisplayName)
                ? instance.TraitId.ToString() : definition.DisplayName;
            string source = FormatSource(instance, equipment, identity, getSkill, getItem);
            var lines = new List<string> { $"来源：{source}" };
            if (instance.Rank > 1)
                lines.Add($"等级 {instance.Rank}");
            if (instance.Stacks > 1)
                lines.Add($"层数 {instance.Stacks}");
            if (!string.IsNullOrWhiteSpace(definition?.Description))
                lines.Add(definition.Description.Trim());
            string rolls = FormatRollValues(instance.RollValues.CopyNormalized());
            if (rolls.Length > 0)
                lines.Add($"词条数值：{rolls}");
            entries.Add(GameRuntimeCharacterInfoEntry.Pair(name, string.Join("\n", lines)));
        }
        if (entries.Count == 0)
            entries.Add(GameRuntimeCharacterInfoEntry.TextEntry("当前没有生效特性。"));
        return entries.AsReadOnly();
    }

    private static string FormatSource(BattleEffectiveTraitInstanceReadView instance,
        EquipmentState equipment, ProgressionIdentityCatalogData identity,
        Func<StringName, SkillDefinition> getSkill, Func<StringName, ItemDefinition> getItem)
    {
        TraitSourceKind kind = TraitContentRules.ToSourceKind(instance.SourceType);
        StringName sourceId = instance.SourceId;
        if (kind == TraitSourceKind.Identity)
        {
            if (identity != null)
            {
                if (identity.RaceDefs.TryGetValue(sourceId, out var race))
                    return $"种族 · {race.DisplayName}";
                if (identity.SubraceDefs.TryGetValue(sourceId, out var subrace))
                    return $"亚种 · {subrace.DisplayName}";
                if (identity.BloodlineDefs.TryGetValue(sourceId, out var bloodline))
                    return $"血脉 · {bloodline.DisplayName}";
                if (identity.BloodlineStageDefs.TryGetValue(sourceId, out var bloodlineStage))
                    return $"血脉阶段 · {bloodlineStage.DisplayName}";
                if (identity.AscensionDefs.TryGetValue(sourceId, out var ascension))
                    return $"升华 · {ascension.DisplayName}";
                if (identity.AscensionStageDefs.TryGetValue(sourceId, out var ascensionStage))
                    return $"升华阶段 · {ascensionStage.DisplayName}";
            }
            return "种族 / 血脉 / 升华";
        }
        if (kind == TraitSourceKind.Character)
        {
            SkillDefinition skill = getSkill(sourceId);
            return skill != null ? $"技能 · {skill.DisplayName}" : "角色获得";
        }
        string label = kind switch
        {
            TraitSourceKind.EquipmentFixed => "装备",
            TraitSourceKind.EquipmentRoll => "装备词条",
            TraitSourceKind.GearSetThreshold => "套装",
            _ => "其他来源",
        };
        if (equipment != null)
            foreach (StringName slot in equipment.GetEntrySlotIdsTyped())
            {
                EquipmentEntryState entry = equipment.GetEntry(slot);
                if (entry == null || entry.instance_id != sourceId)
                    continue;
                ItemDefinition item = getItem(entry.item_id);
                if (item != null)
                    return $"{label} · {item.DisplayName}（{EquipmentRules.GetSlotLabel(slot)}）";
            }
        return label;
    }

    internal static string FormatRollValues(IEnumerable<TraitRollValueState> values)
    {
        var parts = new List<string>();
        foreach (TraitRollValueState value in values)
        {
            if (value == null)
                continue;
            string text = value.ValueTypeKind switch
            {
                TraitRollValueType.Int => value.int_value.ToString(),
                TraitRollValueType.StringName => value.string_name_value.ToString(),
                TraitRollValueType.Bool => value.bool_value ? "是" : "否",
                _ => "—",
            };
            parts.Add($"{CharacterAttributeDisplayText.GetLabel(value.key)}：{text}");
        }
        return string.Join("，", parts);
    }
}
