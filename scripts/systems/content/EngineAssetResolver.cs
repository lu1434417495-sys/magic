using System;
using System.Collections.Generic;
using Godot;

internal sealed class EngineAssetResolver : IDisposable
{
    private const string AuditPathPrefix = "engine-asset:";

    private readonly Dictionary<string, Resource> _assets = new(StringComparer.Ordinal);
    private bool _acceptingLoads = true;
    private bool _disposed;

    internal int CanonicalAssetCount => _assets.Count;

    internal T ResolveBorrowed<T>(string resourcePath)
        where T : Resource
    {
        ThrowIfUnavailable();
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
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

        foreach (string canonicalPath in _assets.Keys)
        {
            LifecycleAuditRegistry.Shared.ReleaseProcessContentRoot(
                AuditPathPrefix + canonicalPath
            );
        }
        _assets.Clear();
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EngineAssetResolver));
        if (!_acceptingLoads)
        {
            throw new InvalidOperationException(
                "Engine assets cannot be loaded after application quiescing begins."
            );
        }
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
