using System.Collections.Generic;
using Godot;

public static class SkillBookItemContentValidator
{
    public static List<string> Validate(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefs
    )
    {
        var errors = new List<string>();

        itemDefs ??= new Dictionary<StringName, ItemDefinition>();
        skillDefs ??= new Dictionary<StringName, SkillDefinition>();

        _append_skill_book_reference_errors(errors, itemDefs, skillDefs);

        _append_canonical_id_collision_errors(errors, itemDefs, skillDefs);

        return errors;
    }

    private static void _append_skill_book_reference_errors(
        List<string> errors,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefs
    )
    {
        foreach (var itemKey in SortedKeys(itemDefs.Keys))
        {
            var itemId = new StringName(itemKey);

            if (!itemDefs.TryGetValue(itemId, out ItemDefinition itemDef))
                continue;

            if (itemDef.CategoryKind != ItemCategoryKind.SkillBook)
                continue;
            if (itemDef.GrantedSkillId == "")
                continue;

            if (!skillDefs.TryGetValue(itemDef.GrantedSkillId, out SkillDefinition skillDef))
            {
                errors.Add(
                    $"Skill book item {itemDef.ItemId} references missing skill {itemDef.GrantedSkillId}."
                );
                continue;
            }

            if (skillDef.LearnSource != "book")
                errors.Add(
                    $"Skill book item {itemDef.ItemId} granted_skill_id {itemDef.GrantedSkillId} learn_source must be book, got {skillDef.LearnSource}."
                );
        }
    }

    private static void _append_canonical_id_collision_errors(
        List<string> errors,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefs
    )
    {
        foreach (var skillKey in SortedKeys(skillDefs.Keys))
        {
            var skillId = new StringName(skillKey);

            if (!skillDefs.TryGetValue(skillId, out SkillDefinition skillDef))
                continue;

            if (
                skillDef == null
                || skillDef.SkillId == ""
                || skillDef.LearnSource != "book"
            )
                continue;

            var canonicalItemId = SkillBookItemFactory.BuildItemIdForSkill(
                skillDef.SkillId
            );

            if (!itemDefs.TryGetValue(canonicalItemId, out ItemDefinition occupyingItem))
                continue;

            if (occupyingItem.CategoryKind != ItemCategoryKind.SkillBook)
            {
                errors.Add(
                    $"Item {canonicalItemId} occupies generated skill book id for skill {skillDef.SkillId} but item_category must be skill_book."
                );
                continue;
            }

            if (occupyingItem.GrantedSkillId != skillDef.SkillId)
                errors.Add(
                    $"Skill book item {canonicalItemId} occupies generated skill book id for skill {skillDef.SkillId} but grants {occupyingItem.GrantedSkillId}."
                );
        }
    }

    private static List<string> SortedKeys(
        IEnumerable<StringName> keys
    )
    {
        var sorted = new List<string>();
        foreach (StringName key in keys)
            sorted.Add(key.ToString());
        sorted.Sort(System.StringComparer.Ordinal);
        return sorted;
    }
}
