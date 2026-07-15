using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Test-only canonical content boundary. Path-backed resources are isolated from
/// Godot's global cache and retained for the loader lifetime; synthetic resources
/// can be registered explicitly.
/// </summary>
internal sealed class TestContentResourceLoader : IContentResourceLoader, IDisposable
{
    private readonly Dictionary<string, Resource> _borrowedResources =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Resource> _syntheticResources =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _canonicalLoadCounts =
        new(StringComparer.Ordinal);
    private bool _disposed;

    internal TestContentResourceLoader RegisterCanonical<T>(string resourcePath, T resource)
        where T : Resource
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(resource);
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        if (_syntheticResources.TryGetValue(canonicalPath, out Resource existingSynthetic))
        {
            if (!ReferenceEquals(existingSynthetic, resource))
            {
                throw new InvalidOperationException(
                    $"Test content {canonicalPath} already has a different canonical root."
                );
            }
            return this;
        }

        if (_borrowedResources.TryGetValue(canonicalPath, out Resource existingBorrowed))
        {
            if (!ReferenceEquals(existingBorrowed, resource))
            {
                throw new InvalidOperationException(
                    $"Test content {canonicalPath} already has a different borrowed canonical root."
                );
            }
            return this;
        }

        _borrowedResources.Remove(canonicalPath);
        _syntheticResources.Add(canonicalPath, resource);
        return this;
    }

    public T LoadCanonical<T>(string resourcePath)
        where T : Resource
    {
        ThrowIfDisposed();
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
        _canonicalLoadCounts.TryGetValue(canonicalPath, out int loadCount);
        _canonicalLoadCounts[canonicalPath] = loadCount + 1;
        if (_syntheticResources.TryGetValue(canonicalPath, out Resource synthetic))
        {
            return synthetic is T typedSynthetic
                ? typedSynthetic
                : throw new InvalidOperationException(
                    $"Synthetic test content {canonicalPath} was registered as "
                        + $"{synthetic.GetType().Name}, not {typeof(T).Name}."
                );
        }
        if (_borrowedResources.TryGetValue(canonicalPath, out Resource borrowed))
        {
            return borrowed is T typedBorrowed
                ? typedBorrowed
                : throw new InvalidOperationException(
                    $"Borrowed test content {canonicalPath} was loaded as {borrowed.GetType().Name}, "
                        + $"not {typeof(T).Name}."
                );
        }

        _borrowedResources.Remove(canonicalPath);
        T loaded = ResourceLoader.Load<T>(
            canonicalPath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        if (loaded == null)
        {
            throw new InvalidOperationException(
                $"Unable to load test content {canonicalPath} as {typeof(T).Name}."
            );
        }
        _borrowedResources.Add(canonicalPath, loaded);
        return loaded;
    }

    internal int CountLoadedPathsUnder(string contentPrefix)
    {
        ThrowIfDisposed();
        int count = 0;
        foreach (string path in _canonicalLoadCounts.Keys)
        {
            if (path.StartsWith(contentPrefix, StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    internal IReadOnlyList<string> GetDuplicateLoadsUnder(string contentPrefix)
    {
        ThrowIfDisposed();
        var duplicates = new List<string>();
        foreach ((string path, int loadCount) in _canonicalLoadCounts)
        {
            if (
                loadCount > 1
                && path.StartsWith(contentPrefix, StringComparison.Ordinal)
            )
            {
                duplicates.Add($"{path} x{loadCount}");
            }
        }
        duplicates.Sort(StringComparer.Ordinal);
        return duplicates;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _borrowedResources.Clear();
        _syntheticResources.Clear();
        _canonicalLoadCounts.Clear();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
