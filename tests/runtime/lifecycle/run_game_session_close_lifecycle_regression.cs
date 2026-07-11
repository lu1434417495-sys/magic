using Godot;

public partial class run_game_session_close_lifecycle_regression : LifecycleTestSceneTree
{
    private static readonly StringName KnownSkillId = "basic_attack";
    private static readonly StringName KnownItemId = "healing_herb";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private async void Run()
    {
        await TestNormalCloseKeepsProcessContentAvailableForNextSession();
        TestExitTreeCloseStillAllowsExplicitNativeDispose();
        RequestTestExit(_test.Finish("GameSession normal-close lifecycle regression"));
    }

    private async System.Threading.Tasks.Task TestNormalCloseKeepsProcessContentAvailableForNextSession()
    {
        LifecycleAuditRegistry audit = LifecycleAuditRegistry.Shared;
        long suppressCountBefore = audit.CaptureSnapshot().NormalPhaseSuppressCount;

        GameSession sessionA = new() { Name = "LifecycleSessionA" };
        Root.AddChild(sessionA);
        GameRoot rootA = sessionA.GetGameRootTyped();
        GameContentCatalog catalogA = sessionA.GetContentCatalogTyped();

        AssertKnownContent(catalogA, "session A");
        long revisionBeforeClose = catalogA.GetRevision();

        sessionA.Dispose();
        long revisionAfterFirstClose = catalogA.GetRevision();
        sessionA.Dispose();

        _test.Eq(
            revisionAfterFirstClose,
            revisionBeforeClose + 1,
            "session A normal close should invalidate its catalog exactly once."
        );
        _test.Eq(
            catalogA.GetRevision(),
            revisionAfterFirstClose,
            "disposing session A twice should not advance catalog revision again."
        );
        rootA.Dispose();
        rootA.Dispose();
        _test.Eq(
            catalogA.GetRevision(),
            revisionAfterFirstClose,
            "disposing session A's already-closed GameRoot twice should remain a no-op."
        );

        sessionA = null;
        rootA = null;
        catalogA = null;

        await LifecycleMeasurementBarrier.RunAsync(this);

        GameSession sessionB = new() { Name = "LifecycleSessionB" };
        Root.AddChild(sessionB);
        GameContentCatalog catalogB = sessionB.GetContentCatalogTyped();
        AssertKnownContent(catalogB, "session B after session A GC barrier");

        sessionB.Dispose();
        sessionB.Dispose();

        _test.Eq(
            audit.CaptureSnapshot().NormalPhaseSuppressCount,
            suppressCountBefore,
            "normal session close must not suppress process content finalizers."
        );
    }

    private void TestExitTreeCloseStillAllowsExplicitNativeDispose()
    {
        GameSession session = new() { Name = "ExitTreeFirstLifecycleSession" };
        Root.AddChild(session);
        GameContentCatalog catalog = session.GetContentCatalogTyped();
        long revisionBeforeExit = catalog.GetRevision();

        Root.RemoveChild(session);

        long revisionAfterExit = catalog.GetRevision();
        _test.Eq(
            revisionAfterExit,
            revisionBeforeExit + 1,
            "exit-tree normal close should invalidate the catalog exactly once."
        );
        _test.True(
            GodotObject.IsInstanceValid(session),
            "removing a session from the tree should leave its native object valid until disposed."
        );

        session.Dispose();

        _test.Eq(
            catalog.GetRevision(),
            revisionAfterExit,
            "explicit Dispose after exit-tree close should not invalidate the catalog again."
        );
        _test.False(
            GodotObject.IsInstanceValid(session),
            "explicit Dispose after exit-tree close should release the native session object."
        );
    }

    private void AssertKnownContent(GameContentCatalog catalog, string label)
    {
        _test.True(catalog != null, $"{label} should expose a content catalog.");
        if (catalog == null)
            return;

        _test.True(
            catalog.GetSkillDefinitionsTyped().TryGetValue(KnownSkillId, out SkillDefinition skill)
                && skill != null
                && skill.SkillId == KnownSkillId,
            $"{label} should resolve the known {KnownSkillId} skill."
        );
        _test.True(
            catalog.GetItemDefsTyped().TryGetValue(KnownItemId, out ItemDefinition item)
                && item != null
                && item.ItemId == KnownItemId,
            $"{label} should resolve the known {KnownItemId} item."
        );
    }
}
