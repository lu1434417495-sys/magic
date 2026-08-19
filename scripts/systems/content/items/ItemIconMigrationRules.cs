#nullable enable

using System;
using System.IO;

internal static class ItemIconMigrationRules
{
    internal const string LegacyDefaultIconPath = "res://icon.svg";
    internal const string DefaultIconAssetId = "ui.item.icon.default";

    internal static bool TryMapLegacyPath(string? legacyPath, out string assetId)
    {
        if (string.IsNullOrEmpty(legacyPath))
        {
            assetId = "";
            return true;
        }
        if (string.Equals(legacyPath, LegacyDefaultIconPath, StringComparison.Ordinal))
        {
            assetId = DefaultIconAssetId;
            return true;
        }

        assetId = "";
        return false;
    }

    internal static string RequireMappedAssetId(string? legacyPath, string sourceLabel)
    {
        if (TryMapLegacyPath(legacyPath, out string assetId))
            return assetId;
        throw new InvalidDataException(
            $"{sourceLabel} icon path '{legacyPath}' has no explicit engine-asset migration mapping."
        );
    }

    internal static string RequireLegacyPath(string assetId, string sourceLabel)
    {
        if (string.IsNullOrEmpty(assetId))
            return "";
        if (string.Equals(assetId, DefaultIconAssetId, StringComparison.Ordinal))
            return LegacyDefaultIconPath;
        throw new InvalidDataException(
            $"{sourceLabel} icon asset ID '{assetId}' has no recorded legacy-path mapping."
        );
    }
}
