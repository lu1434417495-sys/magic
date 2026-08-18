using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

internal static class SkillIconAssetCatalogValidator
{
    internal static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        EngineAssetResolver engineAssets
    )
    {
        ArgumentNullException.ThrowIfNull(skillDefinitions);
        ArgumentNullException.ThrowIfNull(engineAssets);

        var errors = new List<string>();
        foreach (
            (StringName skillId, SkillDefinition definition) in skillDefinitions
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
        )
        {
            if (definition == null)
            {
                errors.Add($"skill.{skillId}.icon_id cannot be validated because the definition is missing.");
                continue;
            }

            StringName iconId = definition.IconId;
            if (iconId == null || string.IsNullOrEmpty(iconId.ToString()))
                continue;

            try
            {
                _ = engineAssets.ResolveContentAssetBorrowed<Texture2D>(iconId);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    or KeyNotFoundException
                    or InvalidOperationException)
            {
                errors.Add(
                    $"skill.{skillId}.icon_id '{iconId}' must reference a registered Texture2D engine asset: {exception.Message}"
                );
            }
        }
        return errors.AsReadOnly();
    }

    internal static void ThrowIfInvalid(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        EngineAssetResolver engineAssets
    )
    {
        string[] errors = Validate(skillDefinitions, engineAssets)
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(error => error, StringComparer.Ordinal)
            .ToArray();
        if (errors.Length == 0)
            return;
        throw new InvalidDataException(
            $"Skill icon asset catalog validation failed with {errors.Length} error(s):\n"
                + string.Join("\n", errors)
        );
    }
}
