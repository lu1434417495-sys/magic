using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public static class SkillBookItemFactory
{
    private const string DEFAULT_ICON_PATH = "res://icon.svg";
    private const int DEFAULT_MAX_STACK = 20;

    public static StringName BuildItemIdForSkill(StringName skillId) =>
        ProgressionDataUtils.to_string_name($"skill_book_{skillId}");

    internal static IReadOnlyDictionary<StringName, ItemDefinition> BuildGeneratedItemDefinitions(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefs,
        IReadOnlyDictionary<StringName, ItemDefinition> existingItemDefs = null
    )
    {
        var generatedDefs = new Dictionary<StringName, ItemDefinition>();
        if (skillDefs == null)
            return new ReadOnlyDictionary<StringName, ItemDefinition>(generatedDefs);

        foreach (SkillDefinition skillDef in skillDefs.Values)
        {
            if (skillDef == null)
                continue;
            if (
                skillDef.SkillId == ""
                || skillDef.LearnSource != "book"
            )
                continue;
            if (skillDef.DisplayName.StripEdges().Length == 0)
                continue;

            var itemId = BuildItemIdForSkill(skillDef.SkillId);
            if (existingItemDefs != null && existingItemDefs.ContainsKey(itemId))
                continue;

            var itemDef = new ItemDefinition(
                itemId,
                "",
                _build_display_name(skillDef),
                _build_description(skillDef),
                DEFAULT_ICON_PATH,
                true,
                0,
                0,
                0,
                true,
                DEFAULT_MAX_STACK,
                ItemDefinition.ToStringName(ItemCategoryKind.SkillBook),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<TraitRollGroupDefinition>(),
                Array.Empty<string>(),
                Array.Empty<AttributeModifierDefinition>(),
                skillDef.SkillId,
                Array.Empty<string>(),
                null,
                "",
                null,
                -1
            );
            generatedDefs[itemId] = itemDef;
        }

        return new ReadOnlyDictionary<StringName, ItemDefinition>(generatedDefs);
    }

    private static string _build_display_name(SkillDefinition skillDef) =>
        $"{skillDef.DisplayName.StripEdges()} 技能书";

    private static string _build_description(SkillDefinition skillDef)
    {
        var skillName = skillDef.DisplayName.StripEdges();
        var result = $"阅读后使一名队员学会技能：{skillName}。";
        if (skillDef.Description.Length > 0)
            result += $"\n{skillDef.Description}";
        return result;
    }
}
