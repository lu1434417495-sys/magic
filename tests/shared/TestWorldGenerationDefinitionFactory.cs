using System;
using System.Collections.Generic;
using Godot;

internal static class TestWorldGenerationDefinitionFactory
{
    internal static WorldGenerationDefinition Load(string resourcePath)
    {
        var loader = new TestWorldContentResourceLoader();
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        WorldMapGenerationConfig source = loader.LoadCanonical<WorldMapGenerationConfig>(
            canonicalPath
        );
        return source.ToDefinition(canonicalPath, loader);
    }

    internal static WorldGenerationDefinition Project(
        string canonicalPath,
        WorldMapGenerationConfig source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var loader = new TestWorldContentResourceLoader();
        return source.ToDefinition(
            ContentPathCanonicalizer.Canonicalize(canonicalPath),
            loader
        );
    }
}

internal sealed class TestWorldContentResourceLoader : IContentResourceLoader
{
    private readonly Dictionary<string, Resource> _resources = new(StringComparer.Ordinal);

    public T LoadCanonical<T>(string resourcePath)
        where T : Resource
    {
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        if (_resources.TryGetValue(canonicalPath, out Resource cached))
        {
            return cached is T typed
                ? typed
                : throw new InvalidOperationException(
                    $"Test content {canonicalPath} was already loaded as {cached.GetType().Name}, not {typeof(T).Name}."
                );
        }

        T loaded = ResourceLoader.Load<T>(canonicalPath);
        if (loaded == null)
        {
            throw new InvalidOperationException(
                $"Unable to load test content {canonicalPath} as {typeof(T).Name}."
            );
        }
        GodotContentOwnership.RegisterBorrowedContent(loaded, canonicalPath);
        _resources[canonicalPath] = loaded;
        return loaded;
    }
}
