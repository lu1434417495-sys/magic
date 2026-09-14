using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

internal static class EnemySpriteAssetCatalogValidator
{
    internal static void ThrowIfInvalid(
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> templates,
        EngineAssetResolver engineAssets
    )
    {
        var errors = new List<string>();
        foreach ((StringName templateId, EnemyTemplateDefinition template) in templates.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal))
        {
            StringName assetId = template?.BattleSpriteAssetId ?? "";
            if (assetId == "") continue;
            try { _ = engineAssets.ResolveContentAssetBorrowed<Texture2D>(assetId); }
            catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
            {
                errors.Add($"enemy_template.{templateId}.battle_sprite_asset_id '{assetId}' must reference a registered Texture2D engine asset: {exception.Message}");
            }
        }
        if (errors.Count != 0) throw new InvalidDataException($"Enemy sprite asset catalog validation failed with {errors.Count} error(s):\n" + string.Join("\n", errors));
    }
}
