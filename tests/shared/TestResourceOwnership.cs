using Godot;

internal static class TestResourceOwnership
{
    private static readonly object Sync = new();
    private static GodotTransientResourceScope _scope;

    internal static T Own<T>(T resource, string reason)
        where T : class
    {
        if (resource is Resource godotResource)
            GetScope().Own(godotResource, reason);
        else
            GetScope().OwnWrapper(resource, reason);
        return resource;
    }

    internal static T OwnWrapper<T>(T wrapper, string reason)
        where T : class
    {
        return GetScope().OwnWrapper(wrapper, reason);
    }

    internal static void Close()
    {
        GodotTransientResourceScope scope;
        lock (Sync)
        {
            scope = _scope;
            _scope = null;
        }

        scope?.Dispose();
    }

    private static GodotTransientResourceScope GetScope()
    {
        lock (Sync)
        {
            _scope ??= new GodotTransientResourceScope("TestResourceOwnership");
            return _scope;
        }
    }
}
