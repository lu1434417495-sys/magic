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

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterTraitEntries(
        BattleUnitState unit)
    {
        if (unit == null)
            return Array.Empty<GameRuntimeCharacterInfoEntry>();
        IGameRuntimeCharacterInfoQuery query = _query;
        return CharacterTraitDisplayText.BuildEntries(
            unit.GetEffectiveTraitsReadViewTyped().Instances, unit.GetEquipmentView(),
            query?.GetIdentityCatalog(),
            id => query != null && query.TryGetTraitDefinition(id, out var trait) ? trait : null,
            id => query != null && query.TryGetSkillDefinition(id, out var skill) ? skill : null,
            id => query != null && query.TryGetItemDefinition(id, out var item) ? item : null);
    }
}
