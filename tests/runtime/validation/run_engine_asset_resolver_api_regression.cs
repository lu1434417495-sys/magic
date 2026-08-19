using System;
using System.Collections.Generic;
using Godot;

public partial class run_engine_asset_resolver_api_regression : LifecycleTestSceneTree
{
    private const string CatalogFixture =
        "res://tests/runtime/fixtures/engine_asset_catalog/valid_catalog.tres";
    private const string CodeOwnedScenePath =
        "res://scenes/main/login_screen.tscn";
    private const string AuthoredItemIconPath = "res://icon.svg";
    private static readonly StringName ContentSceneId = "test.login_scene";
    private static readonly StringName ItemIconId = "ui.item.icon.default";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        LifecycleAuditSnapshot auditBaseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var resolver = new EngineAssetResolver();
        try
        {
            resolver.LoadAndPublishCatalogBorrowed(CatalogFixture);
            AssertTypedContentIdApi(resolver);
            AssertCodeOwnedPathApi(resolver);
            AssertAuthoredPathReverseIndex(resolver);
            AssertAuthoredPathMigrationSeam(resolver);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected engine asset resolver API exception: {exception}");
        }
        finally
        {
            resolver.Dispose();
        }

        LifecycleAuditSnapshot auditAfter = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            auditAfter.ProcessContentRootCount,
            auditBaseline.ProcessContentRootCount,
            "resolver API test restores the process content-root baseline"
        );
        _test.Eq(
            auditAfter.ViolationCount,
            auditBaseline.ViolationCount,
            "resolver API test adds no lifecycle violation"
        );
        _test.Eq(
            auditAfter.LegacyDebt.Count,
            auditBaseline.LegacyDebt.Count,
            "resolver API test adds no lifecycle debt"
        );

        RequestTestExit(_test.Finish("Engine asset resolver API regression"));
    }

    private void AssertTypedContentIdApi(EngineAssetResolver resolver)
    {
        IReadOnlySet<StringName> textureIds =
            resolver.GetPublishedContentAssetIds<Texture2D>();
        _test.True(
            textureIds.Contains(ItemIconId) && !textureIds.Contains(ContentSceneId),
            "typed asset-ID list exposes textures without leaking scene IDs"
        );
        StringName contentAssetId = ContentSceneId;
        PackedScene scene = resolver.ResolveContentAssetBorrowed<PackedScene>(contentAssetId);
        _test.True(scene != null, "content asset API resolves a typed catalog ID");
        _test.True(
            Throws<InvalidOperationException>(() =>
                resolver.ResolveContentAssetBorrowed<Texture2D>(contentAssetId)
            ),
            "content asset API rejects a mismatched query type"
        );
        _test.True(
            Throws<KeyNotFoundException>(() =>
                resolver.ResolveContentAssetBorrowed<PackedScene>("test.unknown")
            ),
            "content asset API rejects an unknown ID"
        );
        _test.True(
            resolver.ResolveContentAssetBorrowed<PackedScene>(default, optional: true) == null,
            "content asset API returns null only for an explicitly optional empty ID"
        );
        _test.True(
            Throws<ArgumentException>(() =>
                resolver.ResolveContentAssetBorrowed<PackedScene>(CodeOwnedScenePath)
            ),
            "content asset API rejects a res path passed through StringName"
        );
    }

    private void AssertCodeOwnedPathApi(EngineAssetResolver resolver)
    {
        string codeOwnedPath = CodeOwnedScenePath;
        PackedScene first = resolver.ResolveCodeAssetBorrowed<PackedScene>(codeOwnedPath);
        PackedScene repeated = resolver.ResolveCodeAssetBorrowed<PackedScene>(
            "res://scenes/main/./login_screen.tscn"
        );
        _test.True(
            ReferenceEquals(first, repeated),
            "code asset API canonicalizes and caches a code-owned res path"
        );
        _test.True(
            Throws<ArgumentException>(() =>
                resolver.ResolveCodeAssetBorrowed<PackedScene>(ContentSceneId.ToString())
            ),
            "code asset API rejects a content ID without an explicit path scheme"
        );
        _test.True(
            Throws<ArgumentException>(() =>
                resolver.ResolveCodeAssetBorrowed<PackedScene>("user://login_screen.tscn")
            ),
            "code asset API rejects non-res schemes"
        );
    }

    private void AssertAuthoredPathMigrationSeam(EngineAssetResolver resolver)
    {
        Texture2D texture = resolver
            .ResolveAuthoredContentPathBorrowedDuringMigration<Texture2D>(
                AuthoredItemIconPath
            );
        _test.True(
            texture != null,
            "the explicitly named migration seam still resolves an authored res path"
        );
        _test.True(
            Throws<ArgumentException>(() =>
                resolver.ResolveAuthoredContentPathBorrowedDuringMigration<Texture2D>(
                    ContentSceneId.ToString()
                )
            ),
            "the migration seam rejects an asset ID passed as a path"
        );
    }

    private void AssertAuthoredPathReverseIndex(EngineAssetResolver resolver)
    {
        _test.Eq(
            resolver.ResolveContentAssetIdForAuthoredPathDuringMigration<Texture2D>(
                AuthoredItemIconPath
            ),
            ItemIconId,
            "migration reverse index maps an authored item icon path to its stable asset ID"
        );
        _test.True(
            Throws<InvalidOperationException>(() =>
                resolver.ResolveContentAssetIdForAuthoredPathDuringMigration<PackedScene>(
                    AuthoredItemIconPath
                )
            ),
            "migration reverse index rejects a mismatched target type"
        );
        _test.True(
            Throws<KeyNotFoundException>(() =>
                resolver.ResolveContentAssetIdForAuthoredPathDuringMigration<Texture2D>(
                    "res://assets/main/battle/terrain/canyon/marker_preview.png"
                )
            ),
            "migration reverse index rejects an unregistered authored path"
        );
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
