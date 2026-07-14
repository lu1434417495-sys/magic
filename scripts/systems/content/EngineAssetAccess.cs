using System;
using Godot;

/// <summary>
/// Narrow bridge from scene/static presentation code to the process-owned engine asset cache.
/// It never owns the returned Resource; lifetime remains with ProcessContentHost.EngineAssets.
/// </summary>
internal static class EngineAssetAccess
{
    internal static T ResolveBorrowed<T>(Node context, string resourcePath)
        where T : Resource => ResolveResolver(context).ResolveBorrowed<T>(resourcePath);

    internal static T ResolveBorrowed<T>(string resourcePath)
        where T : Resource => ResolveResolver(null).ResolveBorrowed<T>(resourcePath);

    private static EngineAssetResolver ResolveResolver(Node context)
    {
        SceneTree tree = null;
        if (context != null && GodotObject.IsInstanceValid(context) && context.IsInsideTree())
            tree = context.GetTree();
        tree ??= Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            throw new InvalidOperationException(
                "A running SceneTree is required to resolve canonical engine assets."
            );
        }

        ApplicationLifetimeCoordinator coordinator =
            tree.Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        if (coordinator == null || !GodotObject.IsInstanceValid(coordinator))
        {
            throw new InvalidOperationException(
                "ApplicationLifetimeCoordinator is required to resolve canonical engine assets."
            );
        }
        return coordinator.ContentHost.EngineAssets;
    }
}
