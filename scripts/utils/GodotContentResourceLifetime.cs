using System.Collections.Generic;
using Godot;

public static class GodotContentResourceLifetime
{
    private static readonly List<Resource> ResourceRoots = new();

    public static T Keep<T>(T resource)
        where T : Resource
    {
        if (resource == null)
            return null;
        System.GC.SuppressFinalize(resource);
        ResourceRoots.Add(resource);
        return resource;
    }
}
