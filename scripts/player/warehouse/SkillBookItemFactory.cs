using System.Collections.Generic;
using Godot;

public static class SkillBookItemFactory
{
    private const string DEFAULT_ICON_PATH = "res://icon.svg";
    private const int DEFAULT_MAX_STACK = 20;

    public static StringName BuildItemIdForSkill(StringName skillId) =>
        ProgressionDataUtils.to_string_name($"skill_book_{skillId}");

    public static Dictionary<StringName, ItemDef> BuildGeneratedItemDefs(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefs,
        IReadOnlyDictionary<StringName, ItemDef> existingItemDefs = null
    )
    {
        var generatedDefs = new Dictionary<StringName, ItemDef>();
        if (skillDefs == null)
            return generatedDefs;

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

            var itemDef = new ItemDef
            {
                item_id = itemId,
                display_name = _build_display_name(skillDef),
                description = _build_description(skillDef),
                icon = DEFAULT_ICON_PATH,
                is_stackable = true,
                max_stack = DEFAULT_MAX_STACK,
                CategoryKind = ItemCategoryKind.SkillBook,
                granted_skill_id = skillDef.SkillId,
            };
            GodotContentOwnership.RegisterDerivedContent(
                itemDef,
                $"skill_book_item:{itemId}",
                "SkillBookItemFactory.BuildGeneratedItemDefs"
            );
            generatedDefs[itemId] = itemDef;
        }

        return generatedDefs;
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
