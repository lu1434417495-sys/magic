using Godot;

[GlobalClass]
public partial class SkillBookItemContentValidator : RefCounted
{
    public static Godot.Collections.Array<string> validate(
        Godot.Collections.Dictionary itemDefs,
        Godot.Collections.Dictionary skillDefs
    )
    {
        var errors = new Godot.Collections.Array<string>();

        var normalizedItemDefs = _normalize_item_defs(itemDefs);

        var normalizedSkillDefs = _normalize_skill_defs(skillDefs);

        _append_skill_book_reference_errors(errors, normalizedItemDefs, normalizedSkillDefs);

        _append_canonical_id_collision_errors(errors, normalizedItemDefs, normalizedSkillDefs);

        return errors;
    }

    private static void _append_skill_book_reference_errors(
        Godot.Collections.Array<string> errors,
        System.Collections.Generic.Dictionary<StringName, ItemDef> itemDefs,
        System.Collections.Generic.Dictionary<StringName, SkillDef> skillDefs
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
        Godot.Collections.Array<string> errors,
        System.Collections.Generic.Dictionary<StringName, ItemDef> itemDefs,
        System.Collections.Generic.Dictionary<StringName, SkillDef> skillDefs
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

            var canonicalItemId = SkillBookItemFactory.build_item_id_for_skill(
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

    private static System.Collections.Generic.Dictionary<StringName, ItemDef> _normalize_item_defs(
        Godot.Collections.Dictionary itemDefs
    )
    {
        var normalized = new System.Collections.Generic.Dictionary<StringName, ItemDef>();

        foreach (var key in itemDefs.Keys)
        {
            var itemDef = itemDefs[key].AsGodotObject() as ItemDef;

            if (itemDef == null || itemDef.item_id == "")
                continue;

            normalized[itemDef.item_id] = itemDef;
        }

        return normalized;
    }

    private static System.Collections.Generic.Dictionary<StringName, SkillDef> _normalize_skill_defs(
        Godot.Collections.Dictionary skillDefs
    )
    {
        var normalized = new System.Collections.Generic.Dictionary<StringName, SkillDef>();

        foreach (var key in skillDefs.Keys)
        {
            var skillDef = skillDefs[key].AsGodotObject() as SkillDef;

            if (skillDef == null || skillDef.skill_id == "")
                continue;

            normalized[skillDef.skill_id] = skillDef;
        }

        return normalized;
    }

    private static Godot.Collections.Array<string> SortedKeys(
        System.Collections.Generic.IEnumerable<StringName> keys
    )
    {
        var sorted = new System.Collections.Generic.List<string>();
        foreach (StringName key in keys)
            sorted.Add(key.ToString());
        sorted.Sort(System.StringComparer.Ordinal);
        var result = new Godot.Collections.Array<string>();
        foreach (string key in sorted)
            result.Add(key);
        return result;
    }
}
