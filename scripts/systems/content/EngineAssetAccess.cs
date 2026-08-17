using System;
using Godot;

/// <summary>
/// Narrow bridge from scene/static presentation code to the process-owned engine asset cache.
/// It never owns the returned Resource; lifetime remains with ProcessContentHost.EngineAssets.
/// </summary>
internal static class EngineAssetAccess
{
    internal static T ResolveContentAssetBorrowed<T>(
        Node context,
        StringName assetId,
        bool optional = false
    )
        where T : Resource =>
        ResolveResolver(context).ResolveContentAssetBorrowed<T>(assetId, optional);

    internal static T ResolveContentAssetBorrowed<T>(
        StringName assetId,
        bool optional = false
    )
        where T : Resource =>
        ResolveResolver(null).ResolveContentAssetBorrowed<T>(assetId, optional);

    internal static T ResolveCodeAssetBorrowed<T>(Node context, string codeOwnedPath)
        where T : Resource =>
        ResolveResolver(context).ResolveCodeAssetBorrowed<T>(codeOwnedPath);

    internal static T ResolveCodeAssetBorrowed<T>(string codeOwnedPath)
        where T : Resource =>
        ResolveResolver(null).ResolveCodeAssetBorrowed<T>(codeOwnedPath);

    // Delete these two overloads with the item, skill, and enemy authored-path
    // migrations. They are not a compatibility alias for code-owned paths.
    internal static T ResolveAuthoredContentPathBorrowedDuringMigration<T>(
        Node context,
        string authoredContentPath
    )
        where T : Resource =>
        ResolveResolver(context)
            .ResolveAuthoredContentPathBorrowedDuringMigration<T>(authoredContentPath);

    internal static T ResolveAuthoredContentPathBorrowedDuringMigration<T>(
        string authoredContentPath
    )
        where T : Resource =>
        ResolveResolver(null)
            .ResolveAuthoredContentPathBorrowedDuringMigration<T>(authoredContentPath);

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
