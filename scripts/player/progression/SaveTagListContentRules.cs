using System;
using System.Collections.Generic;
using Godot;

internal static class SaveTagListContentRules
{
    private static readonly string[] RemovedSuffixes =
    {
        "_advantage",
        "_disadvantage",
        "_immunity",
    };

    internal static void AppendValidationErrors(
        ICollection<string> errors,
        string fieldLabel,
        IReadOnlyList<StringName> tags
    )
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (tags == null)
        {
            errors.Add($"{fieldLabel} must be a non-null list.");
            return;
        }

        HashSet<StringName> seenTags = new();
        for (int index = 0; index < tags.Count; index++)
        {
            StringName tag = tags[index];
            if (tag == "")
            {
                errors.Add($"{fieldLabel}[{index}] must be a non-empty StringName.");
                continue;
            }

            string text = tag.ToString();
            string removedSuffix = FindRemovedSuffix(text);
            if (removedSuffix != null)
            {
                errors.Add(
                    $"{fieldLabel}[{index}] entry {tag} uses removed suffix syntax; write the bare save tag {text[..^removedSuffix.Length]} in the field matching its semantics."
                );
            }
            else if (!BattleSaveContentRules.IsValidSaveTag(tag))
            {
                errors.Add(
                    $"{fieldLabel}[{index}] entry {tag} is not a supported save tag."
                );
            }

            if (!seenTags.Add(tag))
                errors.Add($"{fieldLabel}[{index}] duplicates save tag {tag}.");
        }
    }

    private static string FindRemovedSuffix(string value)
    {
        foreach (string suffix in RemovedSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
                return suffix;
        }
        return null;
    }
}
