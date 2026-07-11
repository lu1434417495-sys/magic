using System;

internal static class TestWorldGenerationDefinitionFactory
{
    internal static WorldGenerationDefinition Load(string resourcePath)
    {
        using var loader = new TestContentResourceLoader();
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
        using var loader = new TestContentResourceLoader();
        return source.ToDefinition(
            ContentPathCanonicalizer.Canonicalize(canonicalPath),
            loader
        );
    }
}
