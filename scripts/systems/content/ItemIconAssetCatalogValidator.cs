using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

internal static class ItemIconAssetCatalogValidator
{
    internal static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        EngineAssetResolver engineAssets
    )
    {
        ArgumentNullException.ThrowIfNull(itemDefinitions);
        ArgumentNullException.ThrowIfNull(engineAssets);

        var errors = new List<string>();
        foreach (
            (StringName itemId, ItemDefinition definition) in itemDefinitions
                .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
        )
        {
            if (definition == null)
            {
                errors.Add(
                    $"item.{itemId}.icon_asset_id cannot be validated because the definition is missing."
                );
                continue;
            }

            string iconAssetId = definition.IconAssetId;
            if (string.IsNullOrEmpty(iconAssetId))
                continue;

            try
            {
                _ = engineAssets.ResolveContentAssetBorrowed<Texture2D>(
                    new StringName(iconAssetId)
                );
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    or KeyNotFoundException
                    or InvalidOperationException)
            {
                errors.Add(
                    $"item.{itemId}.icon_asset_id '{iconAssetId}' must reference a registered Texture2D engine asset: {exception.Message}"
                );
            }
        }
        return errors.AsReadOnly();
    }
}

internal static class ContentIconAssetCatalogValidator
{
    internal static void ThrowIfInvalid(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        EngineAssetResolver engineAssets
    )
    {
        string[] errors = SkillIconAssetCatalogValidator.Validate(skillDefinitions, engineAssets)
            .Concat(ItemIconAssetCatalogValidator.Validate(itemDefinitions, engineAssets))
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(error => error, StringComparer.Ordinal)
            .ToArray();
        if (errors.Length == 0)
            return;
        throw new InvalidDataException(
            $"Content icon asset catalog validation failed with {errors.Length} error(s):\n"
                + string.Join("\n", errors)
        );
    }
}
