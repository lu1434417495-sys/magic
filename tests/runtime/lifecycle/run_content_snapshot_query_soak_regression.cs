using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class run_content_snapshot_query_soak_regression : LifecycleTestSceneTree
{
    private const int SessionCycleCount = 10;
    private const int QueriesPerSession = 1_000;
    private static readonly StringName KnownSkillId = "mage_meteor_swarm";
    private static readonly StringName MeteorProfileId = "meteor_swarm";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            await RunSoakAsync();
        }
        catch (Exception exception)
        {
            _test.Fail($"content snapshot query soak threw: {exception}");
        }

        RequestTestExit(_test.Finish("Content snapshot query soak regression"));
    }

    private async Task RunSoakAsync()
    {
        ApplicationLifetimeCoordinator coordinator = Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        ProcessContentHost host = coordinator.ContentHost;
        ContentSnapshot snapshot = host.GetSnapshot();
        long expectedEpoch = snapshot.Epoch;
        int expectedRootCount = host.CanonicalRootCount;
        int expectedAuditRootCount =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ProcessContentRootCount;
        int expectedSnapshotObjectCount = CountSnapshotObjects(snapshot);
        StringName itemId = FirstKey(snapshot.Items);

        _test.True(expectedRootCount > 0, "soak precondition: process host has canonical roots");
        _test.True(
            expectedSnapshotObjectCount > 0,
            "soak precondition: immutable snapshot graph is populated"
        );
        _test.True(
            snapshot.Skills.ContainsKey(KnownSkillId),
            $"soak precondition: snapshot contains {KnownSkillId}"
        );
        _test.True(
            !StringNameIsEmpty(itemId),
            "soak precondition: snapshot contains an item definition"
        );

        GameSession initialSession = Root.GetNodeOrNull<GameSession>("GameSession");
        if (initialSession != null)
            await coordinator.CloseSessionAsync(initialSession);
        initialSession = null;
        await LifecycleMeasurementBarrier.RunAsync(this);
        AssertStable(
            host,
            snapshot,
            expectedEpoch,
            expectedRootCount,
            expectedAuditRootCount,
            expectedSnapshotObjectCount,
            "after initial session close"
        );

        int completedQueries = 0;
        int queryFailures = 0;
        for (int cycle = 0; cycle < SessionCycleCount; cycle++)
        {
            GameSession session = GameSessionTestFactory.CreateForCoordinatorAttachment();
            Root.AddChild(session);
            try
            {
                _test.Eq(
                    session.GetContentSnapshotEpoch(),
                    expectedEpoch,
                    $"session cycle {cycle + 1} borrows the process snapshot epoch"
                );
                RunQueries(
                    session.GetContentCatalogTyped(),
                    itemId,
                    QueriesPerSession,
                    ref completedQueries,
                    ref queryFailures
                );
            }
            finally
            {
                await coordinator.CloseSessionAsync(session);
            }
            session = null;

            await LifecycleMeasurementBarrier.RunAsync(this);
            AssertStable(
                host,
                snapshot,
                expectedEpoch,
                expectedRootCount,
                expectedAuditRootCount,
                expectedSnapshotObjectCount,
                $"after session cycle {cycle + 1}"
            );
        }

        _test.Eq(
            completedQueries,
            SessionCycleCount * QueriesPerSession,
            "soak executes exactly 10,000 catalog/special-profile queries"
        );
        _test.Eq(queryFailures, 0, "all 10,000 content queries resolve typed data");
    }

    private static void RunQueries(
        GameContentCatalog catalog,
        StringName itemId,
        int count,
        ref int completedQueries,
        ref int failures
    )
    {
        for (int index = 0; index < count; index++)
        {
            bool success;
            switch (completedQueries % 3)
            {
                case 0:
                    success =
                        catalog
                            .GetSkillDefinitionsTyped()
                            .TryGetValue(KnownSkillId, out SkillDefinition skill)
                        && skill != null;
                    break;
                case 1:
                    success =
                        catalog
                            .GetItemDefsTyped()
                            .TryGetValue(itemId, out ItemDefinition item)
                        && item != null;
                    break;
                default:
                    success =
                        catalog
                            .GetBattleSpecialProfileView()
                            .TryGetMeteorSwarmProfile(
                                MeteorProfileId,
                                out MeteorSwarmProfileData profile
                            )
                        && profile != null;
                    break;
            }

            completedQueries++;
            if (!success)
                failures++;
        }
    }

    private void AssertStable(
        ProcessContentHost host,
        ContentSnapshot expectedSnapshot,
        long expectedEpoch,
        int expectedRootCount,
        int expectedAuditRootCount,
        int expectedSnapshotObjectCount,
        string label
    )
    {
        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.True(
            ReferenceEquals(host.GetSnapshot(), expectedSnapshot),
            $"{label}: process snapshot identity"
        );
        _test.Eq(host.Epoch, expectedEpoch, $"{label}: snapshot epoch");
        _test.Eq(host.CanonicalRootCount, expectedRootCount, $"{label}: canonical root count");
        _test.Eq(
            audit.ProcessContentRootCount,
            expectedAuditRootCount,
            $"{label}: audited process root count"
        );
        _test.Eq(
            CountSnapshotObjects(expectedSnapshot),
            expectedSnapshotObjectCount,
            $"{label}: immutable snapshot object count"
        );
        _test.Eq(audit.ActiveContentBorrowerCount, 0, $"{label}: active content borrowers");
    }

    private static int CountSnapshotObjects(ContentSnapshot snapshot)
    {
        var objects = new HashSet<object>(
            System.Collections.Generic.ReferenceEqualityComparer.Instance
        );
        VisitSnapshotObject(snapshot, objects);
        return objects.Count;
    }

    private static void VisitSnapshotObject(object value, HashSet<object> objects)
    {
        if (value == null || value is string || value is Delegate)
            return;

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type.IsValueType || !objects.Add(value))
            return;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                VisitSnapshotObject(entry.Key, objects);
                VisitSnapshotObject(entry.Value, objects);
            }
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object item in enumerable)
                VisitSnapshotObject(item, objects);
            return;
        }

        if (type.Assembly != typeof(ContentSnapshot).Assembly)
            return;

        foreach (
            FieldInfo field in type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            if (field.IsStatic)
                continue;
            VisitSnapshotObject(field.GetValue(value), objects);
        }
    }

    private static StringName FirstKey<T>(IReadOnlyDictionary<StringName, T> values)
    {
        foreach (StringName key in values.Keys)
            return key;
        return default;
    }

    private static bool StringNameIsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
