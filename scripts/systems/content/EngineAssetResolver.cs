using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class EngineAssetResolver : IDisposable
{
    private const string AuditPathPrefix = "engine-asset:";
    private const string CatalogAuditPathPrefix = "engine-asset-catalog:";

    private readonly Dictionary<string, Resource> _assets = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<StringName, Resource> _catalogAssets =
        new ReadOnlyDictionary<StringName, Resource>(
            new Dictionary<StringName, Resource>()
        );
    private IReadOnlyDictionary<string, StringName> _catalogAssetIdsByPath =
        new ReadOnlyDictionary<string, StringName>(
            new Dictionary<string, StringName>(StringComparer.Ordinal)
        );
    private EngineAssetCatalogDef _catalogRoot;
    private string _catalogCanonicalPath = "";
    private bool _catalogPublished;
    private bool _acceptingLoads = true;
    private bool _disposed;

    internal int CanonicalAssetCount => _assets.Count;
    internal int PublishedAssetCount => _catalogAssets.Count;
    internal bool HasCatalogRoot => _catalogRoot != null;
    internal bool HasPublishedCatalog => _catalogPublished;

    internal IReadOnlySet<StringName> GetPublishedContentAssetIds<T>()
        where T : Resource
    {
        ThrowIfDisposed();
        if (!_catalogPublished)
        {
            throw new InvalidOperationException(
                "Engine asset catalog has not been published."
            );
        }
        var result = new HashSet<StringName>();
        foreach ((StringName assetId, Resource asset) in _catalogAssets)
        {
            if (asset is T)
                result.Add(assetId);
        }
        return result;
    }

    internal EngineAssetCatalogDef LoadAndPublishCatalogBorrowed(string catalogPath)
    {
        ThrowIfLoadUnavailable();
        string canonicalPath = CanonicalizeResPath(catalogPath, nameof(catalogPath));
        if (_catalogRoot != null)
        {
            if (!string.Equals(_catalogCanonicalPath, canonicalPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Engine asset catalog root is already anchored at "
                        + $"{_catalogCanonicalPath}, not {canonicalPath}."
                );
            }

            if (!_catalogPublished)
                PublishCatalogIndex(_catalogRoot);
            return _catalogRoot;
        }

        EngineAssetCatalogDef loaded = ResourceLoader.Load<EngineAssetCatalogDef>(
            canonicalPath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        if (loaded == null)
        {
            throw new InvalidOperationException(
                $"Failed to load engine asset catalog root {canonicalPath}."
            );
        }

        _catalogRoot = loaded;
        _catalogCanonicalPath = canonicalPath;
        GodotWrapperOwnershipRegistry.Register(
            loaded,
            GodotWrapperOwnershipKind.BorrowedStaticContent,
            this,
            canonicalPath
        );
        LifecycleAuditRegistry.Shared.RegisterProcessContentRoot(
            CatalogAuditPathPrefix + canonicalPath,
            loaded.GetType(),
            loaded
        );

        PublishCatalogIndex(loaded);
        return loaded;
    }

    internal T ResolveContentAssetBorrowed<T>(StringName assetId, bool optional = false)
        where T : Resource
    {
        ThrowIfDisposed();
        if (IsEmptyAssetId(assetId))
        {
            if (optional)
                return null;
            throw new ArgumentException("Engine asset ID is required.", nameof(assetId));
        }
        if (IsPathLikeAssetId(assetId))
        {
            throw new ArgumentException(
                $"Engine asset ID must not be a resource path: {assetId}.",
                nameof(assetId)
            );
        }
        if (!_catalogPublished)
        {
            throw new InvalidOperationException(
                "Engine asset catalog has not been published."
            );
        }
        if (!_catalogAssets.TryGetValue(assetId, out Resource asset))
        {
            throw new KeyNotFoundException(
                $"Engine asset ID is not registered: {assetId}."
            );
        }
        if (asset is not T typed)
        {
            throw new InvalidOperationException(
                $"Engine asset {assetId} is {asset.GetType().Name}, not {typeof(T).Name}."
            );
        }
        return typed;
    }

    internal T ResolveCodeAssetBorrowed<T>(string codeOwnedPath)
        where T : Resource
    {
        ThrowIfLoadUnavailable();
        string canonicalPath = CanonicalizeResPath(codeOwnedPath, nameof(codeOwnedPath));
        return ResolveCanonicalPathBorrowed<T>(canonicalPath);
    }

    // Migration-only reverse lookup. Delete it at the terminal content-migration stage
    // after every authored presentation path has moved to a stable catalog ID.
    // Runtime content must resolve stable IDs in the forward direction only.
    internal StringName ResolveContentAssetIdForAuthoredPathDuringMigration<T>(
        string authoredContentPath
    )
        where T : Resource
    {
        ThrowIfLoadUnavailable();
        string canonicalPath = CanonicalizeResPath(
            authoredContentPath,
            nameof(authoredContentPath)
        );
        if (!_catalogPublished)
        {
            throw new InvalidOperationException(
                "Engine asset catalog has not been published."
            );
        }
        if (!_catalogAssetIdsByPath.TryGetValue(canonicalPath, out StringName assetId))
        {
            throw new KeyNotFoundException(
                $"Engine asset path is not registered for migration: {canonicalPath}."
            );
        }
        if (!_catalogAssets.TryGetValue(assetId, out Resource asset) || asset is not T)
        {
            throw new InvalidOperationException(
                $"Engine asset {assetId} registered for {canonicalPath} is not {typeof(T).Name}."
            );
        }
        return assetId;
    }

    // Delete this seam as the item, skill, and enemy domains migrate their authored
    // presentation paths to engine-asset catalog IDs. New code-owned callers must use
    // ResolveCodeAssetBorrowed instead.
    internal T ResolveAuthoredContentPathBorrowedDuringMigration<T>(
        string authoredContentPath
    )
        where T : Resource
    {
        ThrowIfLoadUnavailable();
        string canonicalPath = CanonicalizeResPath(
            authoredContentPath,
            nameof(authoredContentPath)
        );
        return ResolveCanonicalPathBorrowed<T>(canonicalPath);
    }

    private T ResolveCanonicalPathBorrowed<T>(string canonicalPath)
        where T : Resource
    {
        if (_assets.TryGetValue(canonicalPath, out Resource existing))
        {
            return existing is T typed
                ? typed
                : throw new InvalidOperationException(
                    $"Canonical engine asset {canonicalPath} was loaded as "
                        + $"{existing.GetType().Name}, not {typeof(T).Name}."
                );
        }

        T loaded = ResourceLoader.Load<T>(canonicalPath);
        if (loaded == null)
        {
            throw new InvalidOperationException(
                $"Failed to load canonical engine asset {canonicalPath} as {typeof(T).Name}."
            );
        }

        _assets.Add(canonicalPath, loaded);
        GodotWrapperOwnershipRegistry.Register(
            loaded,
            GodotWrapperOwnershipKind.BorrowedStaticContent,
            this,
            canonicalPath
        );
        LifecycleAuditRegistry.Shared.RegisterProcessContentRoot(
            AuditPathPrefix + canonicalPath,
            loaded.GetType(),
            loaded
        );
        return loaded;
    }

    internal void Quiesce()
    {
        if (!_disposed)
            _acceptingLoads = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _acceptingLoads = false;

        _catalogAssets = new ReadOnlyDictionary<StringName, Resource>(
            new Dictionary<StringName, Resource>()
        );
        _catalogAssetIdsByPath = new ReadOnlyDictionary<string, StringName>(
            new Dictionary<string, StringName>(StringComparer.Ordinal)
        );
        _catalogPublished = false;
        if (_catalogRoot != null)
        {
            LifecycleAuditRegistry.Shared.ReleaseProcessContentRoot(
                CatalogAuditPathPrefix + _catalogCanonicalPath
            );
            _catalogRoot = null;
            _catalogCanonicalPath = "";
        }

        foreach (string canonicalPath in _assets.Keys)
        {
            LifecycleAuditRegistry.Shared.ReleaseProcessContentRoot(
                AuditPathPrefix + canonicalPath
            );
        }
        _assets.Clear();
    }

    private void PublishCatalogIndex(EngineAssetCatalogDef catalog)
    {
        var assetsById = new Dictionary<StringName, Resource>();
        var idsByInstance = new Dictionary<ulong, StringName>();
        var idsByPath = new Dictionary<string, StringName>(StringComparer.Ordinal);

        RegisterTextureEntries(
            catalog.texture_assets,
            assetsById,
            idsByInstance,
            idsByPath
        );
        RegisterSceneEntries(
            catalog.scene_assets,
            assetsById,
            idsByInstance,
            idsByPath
        );
        RegisterAudioEntries(
            catalog.audio_assets,
            assetsById,
            idsByInstance,
            idsByPath
        );
        RegisterShaderEntries(
            catalog.shader_assets,
            assetsById,
            idsByInstance,
            idsByPath
        );

        _catalogAssets = new ReadOnlyDictionary<StringName, Resource>(assetsById);
        _catalogAssetIdsByPath = new ReadOnlyDictionary<string, StringName>(idsByPath);
        _catalogPublished = true;
    }

    private static void RegisterTextureEntries(
        Godot.Collections.Array<EngineTextureAssetEntryDef> entries,
        Dictionary<StringName, Resource> assetsById,
        Dictionary<ulong, StringName> idsByInstance,
        Dictionary<string, StringName> idsByPath
    )
    {
        RequireArray(entries, "texture");
        for (int index = 0; index < entries.Count; index++)
        {
            EngineTextureAssetEntryDef entry = entries[index];
            RegisterEntry(
                entry,
                entry?.asset_id,
                entry?.texture,
                "texture",
                index,
                assetsById,
                idsByInstance,
                idsByPath
            );
        }
    }

    private static void RegisterSceneEntries(
        Godot.Collections.Array<EngineSceneAssetEntryDef> entries,
        Dictionary<StringName, Resource> assetsById,
        Dictionary<ulong, StringName> idsByInstance,
        Dictionary<string, StringName> idsByPath
    )
    {
        RequireArray(entries, "scene");
        for (int index = 0; index < entries.Count; index++)
        {
            EngineSceneAssetEntryDef entry = entries[index];
            RegisterEntry(
                entry,
                entry?.asset_id,
                entry?.scene,
                "scene",
                index,
                assetsById,
                idsByInstance,
                idsByPath
            );
        }
    }

    private static void RegisterAudioEntries(
        Godot.Collections.Array<EngineAudioAssetEntryDef> entries,
        Dictionary<StringName, Resource> assetsById,
        Dictionary<ulong, StringName> idsByInstance,
        Dictionary<string, StringName> idsByPath
    )
    {
        RequireArray(entries, "audio");
        for (int index = 0; index < entries.Count; index++)
        {
            EngineAudioAssetEntryDef entry = entries[index];
            RegisterEntry(
                entry,
                entry?.asset_id,
                entry?.audio,
                "audio",
                index,
                assetsById,
                idsByInstance,
                idsByPath
            );
        }
    }

    private static void RegisterShaderEntries(
        Godot.Collections.Array<EngineShaderAssetEntryDef> entries,
        Dictionary<StringName, Resource> assetsById,
        Dictionary<ulong, StringName> idsByInstance,
        Dictionary<string, StringName> idsByPath
    )
    {
        RequireArray(entries, "shader");
        for (int index = 0; index < entries.Count; index++)
        {
            EngineShaderAssetEntryDef entry = entries[index];
            RegisterEntry(
                entry,
                entry?.asset_id,
                entry?.shader,
                "shader",
                index,
                assetsById,
                idsByInstance,
                idsByPath
            );
        }
    }

    private static void RequireArray(object entries, string assetKind)
    {
        if (entries == null)
            throw new InvalidOperationException($"Engine asset catalog {assetKind} array is missing.");
    }

    private static void RegisterEntry(
        Resource entry,
        StringName assetId,
        Resource asset,
        string assetKind,
        int index,
        Dictionary<StringName, Resource> assetsById,
        Dictionary<ulong, StringName> idsByInstance,
        Dictionary<string, StringName> idsByPath
    )
    {
        if (entry == null || !GodotObject.IsInstanceValid(entry))
        {
            throw new InvalidOperationException(
                $"Engine asset catalog {assetKind} entry {index} is missing."
            );
        }

        if (IsEmptyAssetId(assetId))
        {
            throw new InvalidOperationException(
                $"Engine asset catalog {assetKind} entry {index} has no asset_id."
            );
        }

        if (asset == null || !GodotObject.IsInstanceValid(asset))
        {
            throw new InvalidOperationException(
                $"Engine asset catalog {assetKind} entry {assetId} has no target."
            );
        }
        if (assetsById.ContainsKey(assetId))
        {
            throw new InvalidOperationException(
                $"Engine asset catalog declares duplicate asset_id {assetId}."
            );
        }

        ulong instanceId = asset.GetInstanceId();
        if (idsByInstance.TryGetValue(instanceId, out StringName existingId))
        {
            throw new InvalidOperationException(
                "Engine asset catalog registers the same underlying resource as "
                    + $"both {existingId} and {assetId}."
            );
        }

        assetsById.Add(assetId, asset);
        idsByInstance.Add(instanceId, assetId);

        string authoredPath = asset.ResourcePath;
        if (!string.IsNullOrWhiteSpace(authoredPath))
        {
            string canonicalPath = CanonicalizeResPath(authoredPath, nameof(asset));
            if (!idsByPath.TryAdd(canonicalPath, assetId))
            {
                throw new InvalidOperationException(
                    "Engine asset catalog registers the same authored path as "
                        + $"both {idsByPath[canonicalPath]} and {assetId}."
                );
            }
        }
    }

    private static bool IsEmptyAssetId(StringName assetId) =>
        assetId == null || string.IsNullOrWhiteSpace(assetId.ToString());

    private static bool IsPathLikeAssetId(StringName assetId)
    {
        string value = assetId.ToString();
        return value.Contains("://", StringComparison.Ordinal)
            || value.Contains('/')
            || value.Contains('\\');
    }

    private static string CanonicalizeResPath(string resourcePath, string parameterName)
    {
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        if (!canonicalPath.StartsWith("res://", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Engine asset paths must use the res:// scheme: {resourcePath}.",
                parameterName
            );
        }
        return canonicalPath;
    }

    private void ThrowIfLoadUnavailable()
    {
        ThrowIfDisposed();
        if (!_acceptingLoads)
        {
            throw new InvalidOperationException(
                "Engine assets cannot be loaded after application quiescing begins."
            );
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

internal static class ContentPathCanonicalizer
{
    internal static string Canonicalize(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            throw new ArgumentException("Resource path is required.", nameof(resourcePath));

        string normalized = resourcePath.Trim().Replace('\\', '/');
        int schemeEnd = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            throw new ArgumentException(
                $"Resource path must use an explicit Godot scheme: {resourcePath}",
                nameof(resourcePath)
            );
        }

        string scheme = normalized[..(schemeEnd + 3)];
        string remainder = normalized[(schemeEnd + 3)..];
        var segments = new List<string>();
        foreach (string segment in remainder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new ArgumentException(
                        $"Resource path escapes its scheme root: {resourcePath}",
                        nameof(resourcePath)
                    );
                }
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }

        if (segments.Count == 0)
            throw new ArgumentException($"Resource path has no target: {resourcePath}", nameof(resourcePath));
        return scheme + string.Join('/', segments);
    }
}
