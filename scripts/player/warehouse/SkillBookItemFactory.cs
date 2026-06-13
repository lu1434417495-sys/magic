using System.Collections.Generic;
using Godot;

public static class SkillBookItemFactory
{
    private const string DEFAULT_ICON_PATH = "res://icon.svg";
    private const int DEFAULT_MAX_STACK = 20;

    public static StringName BuildItemIdForSkill(StringName skillId) =>
        ProgressionDataUtils.to_string_name($"skill_book_{skillId}");

    public static Dictionary<StringName, ItemDef> BuildGeneratedItemDefs(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ItemDef> existingItemDefs = null
    )
    {
        var generatedDefs = new Dictionary<StringName, ItemDef>();
        if (skillDefs == null)
            return generatedDefs;

        foreach (SkillDef skillDef in skillDefs.Values)
        {
            if (skillDef == null)
                continue;
            if (
                skillDef.skill_id == ""
                || skillDef.LearnSourceKind != SkillLearnSourceKind.Book
            )
                continue;
            if (skillDef.display_name.StripEdges().Length == 0)
                continue;

            var itemId = BuildItemIdForSkill(skillDef.skill_id);
            if (existingItemDefs != null && existingItemDefs.ContainsKey(itemId))
                continue;

            generatedDefs[itemId] = new ItemDef
            {
                item_id = itemId,
                display_name = _build_display_name(skillDef),
                description = _build_description(skillDef),
                icon = DEFAULT_ICON_PATH,
                is_stackable = true,
                max_stack = DEFAULT_MAX_STACK,
                CategoryKind = ItemCategoryKind.SkillBook,
                granted_skill_id = skillDef.skill_id,
            };
        }

        return generatedDefs;
    }

    private static string _build_display_name(SkillDef skillDef) =>
        $"{skillDef.display_name.StripEdges()} 技能书";

    private static string _build_description(SkillDef skillDef)
    {
        var skillName = skillDef.display_name.StripEdges();
        var result = $"阅读后使一名队员学会技能：{skillName}。";
        if (skillDef.description.Length > 0)
            result += $"\n{skillDef.description}";
        return result;
    }
}
