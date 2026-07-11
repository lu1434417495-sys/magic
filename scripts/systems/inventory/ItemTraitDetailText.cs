using System.Collections.Generic;
using System.Text;
using Godot;

// Shared, content-driven detail-text builder: turns an item's equipment traits into
// player-facing mechanic lines (display_name + description) so every item-inspection
// surface (warehouse / shop / character info) can show mechanics without duplicating
// them into the item's flavor description.
internal static class ItemTraitDetailText
{
    // Per-trait lines: a "【name】" header followed by the trait description (if any).
    // Traits without a display_name are skipped; unknown trait ids are skipped.
    internal static List<string> BuildTraitLines(
        ItemDefinition itemDefinition,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs
    )
    {
        var lines = new List<string>();
        if (itemDefinition == null || traitDefs == null)
            return lines;
        foreach (StringName traitId in itemDefinition.GetTraitIdsTyped())
        {
            if (!traitDefs.TryGetValue(traitId, out TraitDefinition traitDef) || traitDef == null)
                continue;
            string name = (traitDef.DisplayName ?? "").Trim();
            if (name.Length == 0)
                continue;
            lines.Add($"【{name}】");
            string description = (traitDef.Description ?? "").Trim();
            if (description.Length > 0)
                lines.Add(description);
        }
        return lines;
    }

    // Compose a full detail block: base flavor description followed by the trait mechanic
    // lines (separated by a blank line when both are present). Returns the base description
    // unchanged when the item has no renderable traits.
    internal static string Compose(
        string baseDescription,
        ItemDefinition itemDefinition,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs
    )
    {
        string baseText = (baseDescription ?? "").Trim();
        List<string> traitLines = BuildTraitLines(itemDefinition, traitDefs);
        if (traitLines.Count == 0)
            return baseText;

        var builder = new StringBuilder();
        if (baseText.Length > 0)
        {
            builder.Append(baseText);
            builder.Append('\n');
        }
        for (int index = 0; index < traitLines.Count; index++)
        {
            if (index > 0)
                builder.Append('\n');
            builder.Append(traitLines[index]);
        }
        return builder.ToString();
    }
}
