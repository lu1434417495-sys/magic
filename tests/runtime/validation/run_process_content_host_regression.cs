using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_process_content_host_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => CallDeferred(nameof(Run));

    private async void Run()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected process content host regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Process content host regression"));
    }

    private async Task RunAsync()
    {
        TestPublicationStateRollbackAndRelease();

        ApplicationLifetimeCoordinator coordinator = Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        ProcessContentHost host = coordinator.ContentHost;
        ContentSnapshot first = host.GetSnapshot();
        int rootCount = host.CanonicalRootCount;
        long epoch = host.Epoch;

        _test.True(host.IsSealed, "process content host should be sealed before tests run");
        _test.True(rootCount > 0, "process content host should retain canonical authored roots");
        _test.True(epoch > 0, "process content epoch should be positive");
        _test.True(
            ReferenceEquals(first, host.BuildAndSeal()),
            "BuildAndSeal should return the already-published snapshot"
        );
        _test.Eq(host.CanonicalRootCount, rootCount, "idempotent build should not add roots");
        _test.Eq(host.Epoch, epoch, "idempotent build should preserve epoch");
        _test.True(
            Throws<InvalidOperationException>(() =>
                host.LoadCanonical<Resource>(
                    "res://data/configs/world_map/./test_world_map_config.tres"
                )
            ),
            "authored loads should be rejected after seal"
        );

        IReadOnlyList<ContentRootDiagnostic> diagnostics = host.GetCanonicalRootDiagnostics();
        _test.Eq(diagnostics.Count, rootCount, "root diagnostics should match canonical root count");
        _test.Eq(
            diagnostics.Select(entry => entry.CanonicalPath).Distinct(StringComparer.Ordinal).Count(),
            diagnostics.Count,
            "canonical root diagnostics should not contain duplicate paths"
        );
        _test.True(
            diagnostics.All(entry => entry.Role == ReferenceRole.Borrowed),
            "path-backed content roots should be diagnosed as borrowed"
        );
        _test.True(
            first.EnemyTemplates.Count > 0,
            "the process snapshot should expose typed enemy definitions"
        );
        _test.Eq(
            first.BattleSimProfiles.Count,
            4,
            "the process snapshot should expose every formal BattleSim profile definition"
        );
        foreach (
            StringName profileId in new StringName[]
            {
                "baseline",
                "mist_controller_aggressive",
                "ranged_suppressor_cautious",
                "pinning_shot_blocked",
            }
        )
        {
            _test.True(
                first.BattleSimProfiles.ContainsKey(profileId),
                $"formal BattleSim profile definition is exposed: {profileId}"
            );
        }

        PackedScene firstScene = host.EngineAssets.ResolveBorrowed<PackedScene>(
            "res://scenes/main/login_screen.tscn"
        );
        PackedScene repeatedScene = host.EngineAssets.ResolveBorrowed<PackedScene>(
            "res://scenes/main/./login_screen.tscn"
        );
        _test.True(
            ReferenceEquals(firstScene, repeatedScene),
            "engine asset resolver should canonicalize shared path-backed assets"
        );
        _test.Eq(host.EngineAssets.CanonicalAssetCount, 1, "engine asset root should be anchored once");

        GameSession activeSession = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(activeSession != null, "canonical GameSession should borrow the process snapshot");
        string previousStrict = System.Environment.GetEnvironmentVariable(
            "MAGIC_LIFECYCLE_STRICT"
        );
        try
        {
            System.Environment.SetEnvironmentVariable("MAGIC_LIFECYCLE_STRICT", "1");
            _test.True(
                Throws<InvalidOperationException>(host.Dispose),
                "active snapshot borrower should block host disposal in strict mode"
            );
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(
                "MAGIC_LIFECYCLE_STRICT",
                previousStrict
            );
        }
        _test.True(
            ReferenceEquals(first, host.GetSnapshot()),
            "blocked disposal should leave the published snapshot intact"
        );

        host.Quiesce();
        _test.True(
            Throws<InvalidOperationException>(() =>
                host.EngineAssets.ResolveBorrowed<PackedScene>(
                    "res://scenes/main/login_screen.tscn"
                )
            ),
            "quiescing should reject engine asset resolution, including cached assets"
        );

        if (activeSession != null)
            await coordinator.CloseSessionAsync(activeSession);

        host.ReleaseSnapshot();
        host.ReleaseSnapshot();
        _test.True(
            Throws<InvalidOperationException>(() => host.GetSnapshot()),
            "GetSnapshot should fail after release"
        );
        host.Dispose();
        host.Dispose();
        _test.True(
            Throws<ObjectDisposedException>(() => host.GetCanonicalRootDiagnostics()),
            "disposed hosts should reject further diagnostics"
        );
    }

    private void TestPublicationStateRollbackAndRelease()
    {
        var publication = new ContentSnapshotPublication();
        _test.True(
            Throws<InvalidOperationException>(() => publication.GetSnapshot()),
            "GetSnapshot should fail before a successful build"
        );

        int rootCount = 2;
        int baselineRootCount = rootCount;
        _test.True(
            Throws<InvalidOperationException>(() =>
                publication.BuildAndSeal(
                    41,
                    () =>
                    {
                        rootCount++;
                        throw new InvalidOperationException("expected projector failure");
                    },
                    () => rootCount = baselineRootCount,
                    _ => { },
                    _ => { }
                )
            ),
            "projector failures should propagate"
        );
        _test.Eq(
            rootCount,
            baselineRootCount,
            "projector failure should roll attempt roots back to the pre-build baseline"
        );
        _test.False(publication.IsSealed, "failed publication should remain retryable");
        _test.Eq(publication.Epoch, 0L, "failed publication should not publish an epoch");
        _test.True(
            Throws<InvalidOperationException>(() => publication.GetSnapshot()),
            "failed publication should not expose a snapshot"
        );

        ContentSnapshot snapshot = SyntheticContentSnapshotFactory.CreateEmpty(41);
        ContentSnapshot published = publication.BuildAndSeal(
            41,
            () => new ContentSnapshotBuildArtifact(snapshot),
            () => rootCount = baselineRootCount,
            _ => { },
            _ => { }
        );
        _test.True(
            ReferenceEquals(snapshot, published),
            "a retry after projector rollback should publish normally"
        );
        publication.Release();
        publication.Release();
        _test.True(
            Throws<InvalidOperationException>(() => publication.GetSnapshot()),
            "publication release should be idempotent and invalidate GetSnapshot"
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
