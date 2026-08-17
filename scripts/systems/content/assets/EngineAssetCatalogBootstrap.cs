using System;

internal static class EngineAssetCatalogBootstrap
{
    internal const string CatalogPath =
        "res://data/configs/engine_assets/engine_asset_catalog.tres";

    internal static EngineAssetCatalogDef LoadAndPublish(EngineAssetResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return resolver.LoadAndPublishCatalogBorrowed(CatalogPath);
    }
}
