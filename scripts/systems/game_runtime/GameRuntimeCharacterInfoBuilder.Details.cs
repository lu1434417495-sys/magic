using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class GameRuntimeCharacterInfoBuilder
{
    internal static IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBaseAttributeEntries(
        AttributeSnapshot snapshot
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        if (snapshot == null)
            return new[] { GameRuntimeCharacterInfoEntry.TextEntry("暂无属性数据。") };
        foreach (StringName id in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            StringName modifierId = AttributeSnapshot.GetBaseAttributeModifierId(id);
            string value = snapshot.HasValue(id) ? snapshot.GetValue(id).ToString() : "—";
            if (snapshot.HasValue(modifierId))
                value += $"（调整值 {snapshot.GetValue(modifierId):+0;-0;0}）";
            entries.Add(GameRuntimeCharacterInfoEntry.Pair(CharacterAttributeDisplayText.GetLabel(id), value));
        }
        return entries.AsReadOnly();
    }

    internal static IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildCombatAttributeEntries(
        AttributeSnapshot snapshot
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        var seen = new HashSet<StringName>();
        void Append(StringName id)
        {
            if (snapshot == null || !snapshot.HasValue(id) || !seen.Add(id))
                return;
            int value = snapshot.GetValue(id);
            string text = id == AttributeService.ARMOR_MAX_DEX_BONUS && value < 0
                ? "不限" : value.ToString();
            entries.Add(GameRuntimeCharacterInfoEntry.Pair(CharacterAttributeDisplayText.GetLabel(id), text));
        }

        Append(AttributeService.BASE_ATTACK_BONUS);
        Append(AttributeService.ATTACK_BONUS);
        Append(AttributeService.SPELL_PROFICIENCY_BONUS);
        Append(AttributeService.WEAPON_ATTACK_RANGE);
        foreach (StringName id in AttributeService.COMBAT_ATTRIBUTE_IDS)
            Append(id);
        foreach (StringName id in AttributeService.RESOURCE_ATTRIBUTE_IDS)
            Append(id);
        Append(AttributeService.MP_MAX_UNRESERVED);
        Append(AttributeService.RESERVED_MP_MAX);
        if (entries.Count == 0)
            entries.Add(GameRuntimeCharacterInfoEntry.TextEntry("暂无战斗属性数据。"));
        return entries.AsReadOnly();
    }

    // Render the canonical battle instances as-is. Stack selection and removal belong
    // to the trait runtime, not to the window or the member's world equipment state.
    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterTraitEntries(
        BattleUnitState unit
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        IGameRuntimeCharacterInfoQuery query = _query;
        if (unit == null)
            return entries.AsReadOnly();
        foreach (BattleEffectiveTraitInstanceReadView instance in unit.GetEffectiveTraitsReadViewTyped().Instances)
        {
            if (!instance.IsPresent)
                continue;
            TraitDefinition definition = null;
            query?.TryGetTraitDefinition(instance.TraitId, out definition);
            string name = string.IsNullOrWhiteSpace(definition?.DisplayName)
                ? instance.TraitId.ToString() : definition.DisplayName;
            string source = FormatTraitSource(unit, instance, query);
            var lines = new List<string> { $"来源：{source}" };
            if (instance.Rank > 1)
                lines.Add($"等级 {instance.Rank}");
            if (instance.Stacks > 1)
                lines.Add($"层数 {instance.Stacks}");
            if (!string.IsNullOrWhiteSpace(definition?.Description))
                lines.Add(definition.Description.Trim());
            entries.Add(GameRuntimeCharacterInfoEntry.Pair(name, string.Join("\n", lines)));
        }
        if (entries.Count == 0)
            entries.Add(GameRuntimeCharacterInfoEntry.TextEntry("当前没有生效特性。"));
        return entries.AsReadOnly();
    }

    private static string FormatTraitSource(
        BattleUnitState unit,
        BattleEffectiveTraitInstanceReadView instance,
        IGameRuntimeCharacterInfoQuery query
    )
    {
        TraitSourceKind kind = TraitContentRules.ToSourceKind(instance.SourceType);
        StringName sourceId = instance.SourceId;
        if (kind == TraitSourceKind.Identity)
        {
            ProgressionIdentityCatalogData catalog = query?.GetIdentityCatalog();
            if (catalog != null)
            {
                if (catalog.RaceDefs.TryGetValue(sourceId, out var race))
                    return $"种族 · {race.DisplayName}";
                if (catalog.SubraceDefs.TryGetValue(sourceId, out var subrace))
                    return $"亚种 · {subrace.DisplayName}";
                if (catalog.BloodlineDefs.TryGetValue(sourceId, out var bloodline))
                    return $"血脉 · {bloodline.DisplayName}";
                if (catalog.BloodlineStageDefs.TryGetValue(sourceId, out var bloodlineStage))
                    return $"血脉阶段 · {bloodlineStage.DisplayName}";
                if (catalog.AscensionDefs.TryGetValue(sourceId, out var ascension))
                    return $"升华 · {ascension.DisplayName}";
                if (catalog.AscensionStageDefs.TryGetValue(sourceId, out var ascensionStage))
                    return $"升华阶段 · {ascensionStage.DisplayName}";
            }
            return "种族 / 血脉 / 升华";
        }
        if (kind == TraitSourceKind.Character)
        {
            if (query != null && query.TryGetSkillDefinition(sourceId, out var skill))
                return $"技能 · {skill.DisplayName}";
            return "角色获得";
        }

        string sourceLabel = kind switch
        {
            TraitSourceKind.EquipmentFixed => "装备",
            TraitSourceKind.EquipmentRoll => "装备词条",
            TraitSourceKind.GearSetThreshold => "套装",
            _ => "其他来源",
        };
        EquipmentState equipment = unit.GetEquipmentView();
        if (equipment != null && query != null)
        {
            foreach (StringName slotId in equipment.GetEntrySlotIdsTyped())
            {
                EquipmentEntryState entry = equipment.GetEntry(slotId);
                if (entry == null || entry.instance_id != sourceId)
                    continue;
                if (query.TryGetItemDefinition(entry.item_id, out ItemDefinition item))
                    return $"{sourceLabel} · {item.DisplayName}（{EquipmentRules.GetSlotLabel(slotId)}）";
            }
        }
        return sourceLabel;
    }
}
