using System.Collections.Generic;
using Godot;

public static class SkillBookItemContentValidator
{
    public static List<string> Validate(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        var errors = new List<string>();

        itemDefs ??= new Dictionary<StringName, ItemDef>();
        skillDefs ??= new Dictionary<StringName, SkillDef>();

        _append_skill_book_reference_errors(errors, itemDefs, skillDefs);

        _append_canonical_id_collision_errors(errors, itemDefs, skillDefs);

        return errors;
    }

    private static void _append_skill_book_reference_errors(
        List<string> errors,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        foreach (var itemKey in SortedKeys(itemDefs.Keys))
        {
            var itemId = new StringName(itemKey);

            if (!itemDefs.TryGetValue(itemId, out ItemDef itemDef))
                continue;

            if (itemDef.get_item_category_normalized() != ItemDef.ITEM_CATEGORY_SKILL_BOOK())
                continue;
            if (itemDef.granted_skill_id == "")
                continue;

            if (!skillDefs.TryGetValue(itemDef.granted_skill_id, out SkillDef skillDef))
            {
                errors.Add(
                    $"Skill book item {itemDef.item_id} references missing skill {itemDef.granted_skill_id}."
                );
                continue;
            }

            if (skillDef.learn_source != "book")
                errors.Add(
                    $"Skill book item {itemDef.item_id} granted_skill_id {itemDef.granted_skill_id} learn_source must be book, got {skillDef.learn_source}."
                );
        }
    }

    private static void _append_canonical_id_collision_errors(
        List<string> errors,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        foreach (var skillKey in SortedKeys(skillDefs.Keys))
        {
            var skillId = new StringName(skillKey);

            if (!skillDefs.TryGetValue(skillId, out SkillDef skillDef))
                continue;

            if (
                skillDef == null
                || skillDef.skill_id == ""
                || skillDef.learn_source != "book"
            )
                continue;

            var canonicalItemId = SkillBookItemFactory.BuildItemIdForSkill(
                skillDef.skill_id
            );

            if (!itemDefs.TryGetValue(canonicalItemId, out ItemDef occupyingItem))
                continue;

            if (occupyingItem.get_item_category_normalized() != ItemDef.ITEM_CATEGORY_SKILL_BOOK())
            {
                errors.Add(
                    $"Item {canonicalItemId} occupies generated skill book id for skill {skillDef.skill_id} but item_category must be skill_book."
                );
                continue;
            }

            if (occupyingItem.granted_skill_id != skillDef.skill_id)
                errors.Add(
                    $"Skill book item {canonicalItemId} occupies generated skill book id for skill {skillDef.skill_id} but grants {occupyingItem.granted_skill_id}."
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
