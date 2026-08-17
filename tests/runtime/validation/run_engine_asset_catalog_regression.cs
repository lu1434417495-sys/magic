using System;
using Godot;

public partial class run_engine_asset_catalog_regression : LifecycleTestSceneTree
{
    private const string FixtureRoot =
        "res://tests/runtime/fixtures/engine_asset_catalog/";
    private static readonly StringName ValidSceneId = "test.login_scene";
    private static readonly StringName ProductionTextureId =
        "battle.terrain.marker_preview";
    private static readonly StringName ProductionSceneId =
        "battle.board.prop_scene";
    private static readonly StringName ProductionShaderId =
        "ui.skill_icon.grayscale_shader";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            AssertProcessCatalogOwner();
            AssertDuplicateIdRejected();
            AssertDuplicateTargetRejected();
            AssertMissingTargetRejected();
            AssertSnapshotBuildFailureKeepsCatalogWhole();
            AssertTypedLookupOptionalAndLifecycle();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected engine asset catalog regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Engine asset catalog regression"));
    }

    private void AssertProcessCatalogOwner()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNode<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        EngineAssetResolver resolver = coordinator.ContentHost.EngineAssets;
        EngineAssetCatalogDef catalog = EngineAssetCatalogBootstrap.LoadAndPublish(resolver);
        _test.True(
            resolver.HasCatalogRoot,
            "process content host anchors the bootstrap engine asset catalog root"
        );
        _test.True(
            resolver.HasPublishedCatalog,
            "process content host publishes the bootstrap engine asset catalog before tests run"
        );
        _test.True(
            catalog.texture_assets != null && catalog.texture_assets.Count == 1,
            "production bootstrap deserializes the typed texture entry"
        );
        _test.True(
            catalog.scene_assets != null && catalog.scene_assets.Count == 1,
            "production bootstrap deserializes the typed scene entry"
        );
        _test.True(
            catalog.audio_assets != null && catalog.audio_assets.Count == 0,
            "production bootstrap deserializes the explicit typed audio array"
        );
        _test.True(
            catalog.shader_assets != null && catalog.shader_assets.Count == 1,
            "production bootstrap deserializes the typed shader entry"
        );
        _test.Eq(
            resolver.PublishedAssetCount,
            3,
            "production bootstrap publishes its three real engine assets"
        );
        _test.True(
            ReferenceEquals(
                resolver.ResolveContentAssetBorrowed<Texture2D>(ProductionTextureId),
                catalog.texture_assets[0].texture
            ),
            "production texture ID resolves the catalog-owned borrowed target"
        );
        _test.True(
            ReferenceEquals(
                resolver.ResolveContentAssetBorrowed<PackedScene>(ProductionSceneId),
                catalog.scene_assets[0].scene
            ),
            "production scene ID resolves the catalog-owned borrowed target"
        );
        _test.True(
            ReferenceEquals(
                resolver.ResolveContentAssetBorrowed<Shader>(ProductionShaderId),
                catalog.shader_assets[0].shader
            ),
            "production shader ID resolves the catalog-owned borrowed target"
        );
    }

    private void AssertDuplicateIdRejected() =>
        AssertCatalogRejected(
            FixtureRoot + "duplicate_id_catalog.tres",
            "duplicate asset_id",
            "duplicate asset IDs are rejected"
        );

    private void AssertDuplicateTargetRejected() =>
        AssertCatalogRejected(
            FixtureRoot + "duplicate_target_catalog.tres",
            "same underlying resource",
            "one underlying engine asset cannot be registered under multiple IDs"
        );

    private void AssertMissingTargetRejected() =>
        AssertCatalogRejected(
            FixtureRoot + "missing_target_catalog.tres",
            "has no target",
            "catalog entries without a typed target are rejected"
        );

    private void AssertCatalogRejected(
        string fixturePath,
        string expectedMessageFragment,
        string assertionLabel
    )
    {
        int auditBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ProcessContentRootCount;
        var resolver = new EngineAssetResolver();
        Exception failure = null;
        try
        {
            resolver.LoadAndPublishCatalogBorrowed(fixturePath);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        _test.True(failure is InvalidOperationException, assertionLabel);
        _test.True(
            failure?.Message.Contains(expectedMessageFragment, StringComparison.Ordinal) == true,
            $"{assertionLabel}: stable failure reason"
        );
        _test.True(
            resolver.HasCatalogRoot,
            $"{assertionLabel}: failed catalog remains anchored for owner cleanup"
        );
        _test.False(
            resolver.HasPublishedCatalog,
            $"{assertionLabel}: failed catalog does not publish a partial index"
        );
        resolver.Dispose();
        _test.Eq(
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ProcessContentRootCount,
            auditBaseline,
            $"{assertionLabel}: resolver cleanup restores the process-root baseline"
        );
    }

    private void AssertSnapshotBuildFailureKeepsCatalogWhole()
    {
        LifecycleAuditSnapshot auditBaseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var resolver = new EngineAssetResolver();
        var publication = new ContentSnapshotPublication();
        int rollbackCount = 0;
        try
        {
            resolver.LoadAndPublishCatalogBorrowed(FixtureRoot + "valid_catalog.tres");
            _test.True(
                Throws<InvalidOperationException>(() =>
                    publication.BuildAndSeal(
                        901,
                        () => throw new InvalidOperationException(
                            "injected snapshot projection failure"
                        ),
                        () => rollbackCount++,
                        _ => { },
                        _ => { }
                    )
                ),
                "snapshot projection failure propagates after catalog publication"
            );
            _test.Eq(rollbackCount, 1, "snapshot projection failure runs rollback once");
            _test.False(
                publication.IsSealed,
                "snapshot projection failure does not publish a partial snapshot"
            );
            _test.True(
                resolver.HasPublishedCatalog && resolver.PublishedAssetCount == 1,
                "snapshot projection failure leaves the independently owned catalog index whole"
            );
            _test.True(
                resolver.ResolveContentAssetBorrowed<PackedScene>(ValidSceneId) != null,
                "snapshot projection failure does not invalidate the published catalog lookup"
            );
        }
        finally
        {
            resolver.Dispose();
        }

        LifecycleAuditSnapshot auditAfter = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            auditAfter.ProcessContentRootCount,
            auditBaseline.ProcessContentRootCount,
            "snapshot build failure cleanup restores the process-root baseline"
        );
        _test.Eq(
            auditAfter.ViolationCount,
            auditBaseline.ViolationCount,
            "snapshot build failure cleanup adds no lifecycle violation"
        );
        _test.Eq(
            auditAfter.LegacyDebt.Count,
            auditBaseline.LegacyDebt.Count,
            "snapshot build failure cleanup adds no lifecycle debt"
        );
    }

    private void AssertTypedLookupOptionalAndLifecycle()
    {
        int auditBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ProcessContentRootCount;
        var resolver = new EngineAssetResolver();
        EngineAssetCatalogDef catalog = resolver.LoadAndPublishCatalogBorrowed(
            FixtureRoot + "valid_catalog.tres"
        );

        _test.True(catalog.texture_assets != null, "typed texture array is deserialized");
        _test.True(catalog.scene_assets != null, "typed scene array is deserialized");
        _test.True(catalog.audio_assets != null, "typed audio array is deserialized");
        _test.True(catalog.shader_assets != null, "typed shader array is deserialized");
        _test.Eq(resolver.PublishedAssetCount, 1, "valid catalog publishes one asset ID");

        PackedScene first = resolver.ResolveContentAssetBorrowed<PackedScene>(ValidSceneId);
        PackedScene repeated = resolver.ResolveContentAssetBorrowed<PackedScene>(ValidSceneId);
        _test.True(
            ReferenceEquals(first, repeated),
            "asset ID lookup returns the same borrowed typed Resource"
        );
        _test.True(
            ReferenceEquals(first, catalog.scene_assets[0].scene),
            "asset ID lookup borrows the catalog-owned typed target"
        );
        _test.True(
            Throws<InvalidOperationException>(() =>
                resolver.ResolveContentAssetBorrowed<Texture2D>(ValidSceneId)
            ),
            "requesting a registered asset with the wrong Resource type is rejected"
        );
        _test.True(
            Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                resolver.ResolveContentAssetBorrowed<PackedScene>("test.unknown")
            ),
            "unknown asset IDs are rejected"
        );
        _test.True(
            Throws<ArgumentException>(() =>
                resolver.ResolveContentAssetBorrowed<PackedScene>(default)
            ),
            "empty required asset IDs are rejected"
        );
        _test.True(
            resolver.ResolveContentAssetBorrowed<PackedScene>(default, optional: true) == null,
            "empty asset IDs return null only through the explicit optional lookup"
        );

        resolver.Quiesce();
        _test.True(
            ReferenceEquals(
                first,
                resolver.ResolveContentAssetBorrowed<PackedScene>(ValidSceneId)
            ),
            "published asset ID lookup remains available after quiesce"
        );
        _test.True(
            Throws<InvalidOperationException>(() =>
                resolver.ResolveCodeAssetBorrowed<PackedScene>(
                    "res://scenes/main/login_screen.tscn"
                )
            ),
            "quiesce rejects a new code-path engine asset load"
        );

        resolver.Dispose();
        resolver.Dispose();
        _test.Eq(resolver.PublishedAssetCount, 0, "shutdown clears the asset ID index");
        _test.False(resolver.HasCatalogRoot, "shutdown releases the catalog root");
        _test.False(resolver.HasPublishedCatalog, "shutdown invalidates catalog publication");
        _test.True(
            Throws<ObjectDisposedException>(() =>
                resolver.ResolveContentAssetBorrowed<PackedScene>(ValidSceneId)
            ),
            "shutdown rejects later asset ID lookup"
        );
        _test.Eq(
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ProcessContentRootCount,
            auditBaseline,
            "resolver shutdown restores the process-root baseline"
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
