using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class LifecycleSoakScenario
{
    private const string TestWorldConfig =
        "res://data/configs/world_map/test_world_map_config.tres";
    private const int FixedBattleSeed = 0x5A17_2026;
    private const int MaximumBattleAdvanceCount = 64;

    private readonly SceneTree _tree;
    private readonly ApplicationLifetimeCoordinator _coordinator;
    private readonly ProcessContentHost _contentHost;
    private readonly GameSessionPersistenceOptions _persistenceOptions;
    private readonly string _processContentRootFingerprint;

    internal LifecycleSoakScenario(
        SceneTree tree,
        ApplicationLifetimeCoordinator coordinator,
        ProcessContentHost contentHost,
        GameSessionPersistenceOptions persistenceOptions
    )
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _contentHost = contentHost ?? throw new ArgumentNullException(nameof(contentHost));
        _persistenceOptions =
            persistenceOptions ?? throw new ArgumentNullException(nameof(persistenceOptions));
        if (!_contentHost.IsSealed)
            throw new InvalidOperationException("Lifecycle soak requires a sealed process content host.");
        _processContentRootFingerprint = FormatProcessContentRoots(
            _contentHost.GetCanonicalRootDiagnostics()
        );
    }

    internal async ValueTask<LifecycleSoakSample> RunCycleAsync(int cycle)
    {
        if (cycle <= 0)
            throw new ArgumentOutOfRangeException(nameof(cycle));

        LifecycleAuditActivitySnapshot before =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().Activity;
        GameSession session = GameSessionTestFactory.CreateForCoordinatorAttachment(
            _persistenceOptions
        );
        try
        {
            _tree.Root.AddChild(session);
            Require(
                session.GetContentSnapshotEpoch() == _contentHost.Epoch,
                $"cycle {cycle}: session did not borrow the process snapshot epoch."
            );
            RunSessionLifecycle(session, cycle);
        }
        finally
        {
            try
            {
                if (GodotObject.IsInstanceValid(session) && !session.IsClosed)
                {
                    int clearError = session.ClearPersistedGame();
                    Require(
                        clearError == (int)Error.Ok,
                        $"cycle {cycle}: lifecycle-soak save root cleanup failed with {(Error)clearError}."
                    );
                }
            }
            finally
            {
                await _coordinator.CloseSessionAsync(session);
                session = null;
            }
        }

        string currentRootFingerprint = FormatProcessContentRoots(
            _contentHost.GetCanonicalRootDiagnostics()
        );
        Require(
            string.Equals(
                currentRootFingerprint,
                _processContentRootFingerprint,
                StringComparison.Ordinal
            ),
            $"cycle {cycle}: canonical process-content roots changed."
        );
        currentRootFingerprint = null;
        await LifecycleMeasurementBarrier.RunAsync(_tree);
        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        LifecycleSoakCounterVector counters = CaptureCounterVector(after);
        LifecycleSoakActivityDelta activity = CaptureActivityDelta(before, after.Activity);
        long managedMemory = GC.GetTotalMemory(false);
        long privateMemory;
        using (Process process = Process.GetCurrentProcess())
            privateMemory = process.PrivateMemorySize64;

        return new LifecycleSoakSample(
            cycle,
            counters,
            activity,
            managedMemory,
            privateMemory
        );
    }

    private static void RunSessionLifecycle(GameSession session, int cycle)
    {
        int createError = session.CreateNewSave(TestWorldConfig);
        Require(
            createError == (int)Error.Ok,
            $"cycle {cycle}: CreateNewSave failed with {(Error)createError}."
        );
        Require(
            GameSession.CurrentSaveVersion == 18,
            $"cycle {cycle}: lifecycle soak requires save version 18."
        );

        GameRuntimeFacade facade = new(new FixedBattleSeedSource(FixedBattleSeed));
        try
        {
            facade.Setup(session);
            session.SetBattleSaveLock(true);
            facade.StartBattle(BuildFormalEncounter());
            Require(facade.IsBattleActive(), $"cycle {cycle}: formal battle did not start.");

            RuntimeCommandResult confirm =
                facade.CommandConfirmBattleStartTyped();
            Require(
                confirm.Ok,
                $"cycle {cycle}: battle confirmation failed: {confirm.Message}"
            );

            BattleRuntimeModule battleRuntime = facade.GetBattleRuntime();
            Require(battleRuntime != null, $"cycle {cycle}: battle runtime is unavailable.");
            battleRuntime.SetAiTraceEnabled(true);
            PreviewActiveUnitWait(facade, battleRuntime, cycle);
            AdvanceUntilAiTrace(facade, battleRuntime, cycle);
        }
        finally
        {
            facade.Dispose();
            facade = null;
        }

        Require(
            !session.IsBattleSaveLocked(),
            $"cycle {cycle}: facade disposal left the battle save lock enabled."
        );
        AssertRuntimeDomainsDrained(cycle);

        int saveError = session.SaveGameState();
        Require(
            saveError == (int)Error.Ok,
            $"cycle {cycle}: SaveGameState failed with {(Error)saveError}."
        );
        string saveId = session.GetActiveSaveId();
        Require(!string.IsNullOrWhiteSpace(saveId), $"cycle {cycle}: active save id is empty.");
        int loadError = session.LoadSave(saveId);
        Require(
            loadError == (int)Error.Ok,
            $"cycle {cycle}: LoadSave failed with {(Error)loadError}."
        );
    }

    private static EncounterAnchorData BuildFormalEncounter() =>
        new()
        {
            entity_id = "lifecycle_soak_wolf",
            display_name = "Lifecycle Soak Wolf",
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            encounter_profile_id = "wolf_wilds",
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
        };

    private static void PreviewActiveUnitWait(
        GameRuntimeFacade facade,
        BattleRuntimeModule battleRuntime,
        int cycle
    )
    {
        BattleUnitState activeUnit = ResolveActiveUnit(facade, cycle);
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Wait,
            unit_id = activeUnit.unit_id,
        };
        BattlePreview preview = null;
        try
        {
            preview = battleRuntime.PreviewCommand(command);
            Require(preview != null, $"cycle {cycle}: wait preview was not produced.");
            using GodotProjectionLease<GDictionary> lease =
                BattlePreviewProjection.BuildLease(preview);
            GDictionary projected = lease.Value;
            Require(
                projected.ContainsKey("allowed"),
                $"cycle {cycle}: wait preview projection is incomplete."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private static BattleUnitState ResolveActiveUnit(GameRuntimeFacade facade, int cycle)
    {
        for (int iteration = 0; iteration < MaximumBattleAdvanceCount; iteration++)
        {
            BattleState state = facade.GetBattleState();
            if (
                state != null
                && state.active_unit_id != ""
                && state.TryGetUnitTyped(state.active_unit_id, out BattleUnitState activeUnit)
                && activeUnit != null
            )
            {
                return activeUnit;
            }

            RuntimeCommandResult tick = facade.CommandBattleTickTyped(1);
            Require(
                tick.Ok,
                $"cycle {cycle}: failed to advance to an active unit: {tick.Message}"
            );
        }

        throw new InvalidOperationException(
            $"cycle {cycle}: no active battle unit appeared within {MaximumBattleAdvanceCount} ticks."
        );
    }

    private static void AdvanceUntilAiTrace(
        GameRuntimeFacade facade,
        BattleRuntimeModule battleRuntime,
        int cycle
    )
    {
        for (int iteration = 0; iteration < MaximumBattleAdvanceCount; iteration++)
        {
            if (battleRuntime.GetAiTurnTracesTyped().Count >= 1)
                return;

            BattleState state = facade.GetBattleState();
            Require(
                state != null && facade.IsBattleActive(),
                $"cycle {cycle}: battle ended before an AI decision was traced."
            );

            RuntimeCommandResult result;
            if (
                state.active_unit_id != ""
                && state.TryGetUnitTyped(state.active_unit_id, out BattleUnitState activeUnit)
                && activeUnit?.ControlModeKind == BattleUnitControlMode.Manual
            )
            {
                result = facade.CommandBattleWaitOrResolveTyped();
            }
            else
            {
                result = facade.CommandBattleTickTyped(1);
            }
            Require(
                result.Ok,
                $"cycle {cycle}: battle advance failed before AI trace: {result.Message}"
            );
        }

        Require(
            battleRuntime.GetAiTurnTracesTyped().Count >= 1,
            $"cycle {cycle}: no AI trace was produced within {MaximumBattleAdvanceCount} advances."
        );
    }

    private static void AssertRuntimeDomainsDrained(int cycle)
    {
        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        foreach (
            string domain in new[]
            {
                LifetimeDomain.Battle.ToString(),
                LifetimeDomain.Decision.ToString(),
                LifetimeDomain.Request.ToString(),
            }
        )
        {
            Require(
                Count(audit.ActiveOwnerCountsByDomain, domain) == 0,
                $"cycle {cycle}: owner domain {domain} remained active after facade disposal."
            );
            Require(
                Count(audit.ActiveProjectionLeaseCountsByDomain, domain) == 0,
                $"cycle {cycle}: projection domain {domain} remained active after facade disposal."
            );
            Require(
                Count(audit.ActiveNativeScopeCountsByDomain, domain) == 0,
                $"cycle {cycle}: native scope domain {domain} remained active after facade disposal."
            );
            Require(
                Count(audit.ActiveJobCountsByDomain, domain) == 0,
                $"cycle {cycle}: job domain {domain} remained active after facade disposal."
            );
        }
    }

    private LifecycleSoakCounterVector CaptureCounterVector(LifecycleAuditSnapshot audit) =>
        new(
            Count(audit.ActiveOwnerCountsByDomain, LifetimeDomain.Session.ToString()),
            Count(audit.ActiveOwnerCountsByDomain, LifetimeDomain.Battle.ToString()),
            Count(audit.ActiveOwnerCountsByDomain, LifetimeDomain.Decision.ToString()),
            Count(audit.ActiveOwnerCountsByDomain, LifetimeDomain.Request.ToString()),
            Count(audit.ActiveOwnerCountsByDomain, LifetimeDomain.SceneTree.ToString()),
            audit.ActiveContentBorrowerCount,
            audit.ActiveJobCount,
            FormatDomainCounts(audit.ActiveNativeScopeCountsByDomain),
            FormatDomainCounts(audit.ActiveProjectionLeaseCountsByDomain),
            audit.ActiveContentSnapshotEpoch,
            _processContentRootFingerprint,
            checked((int)audit.UnknownCount),
            checked((int)audit.OwnerConflictCount),
            checked((int)audit.EscapedCount),
            checked((int)audit.CloseAfterUseCount),
            checked((int)audit.NormalPhaseSuppressCount),
            checked((int)audit.QuarantineCount)
        );

    private static LifecycleSoakActivityDelta CaptureActivityDelta(
        LifecycleAuditActivitySnapshot before,
        LifecycleAuditActivitySnapshot after
    ) =>
        new(
            MonotonicDelta(before.OwnersRegistered, after.OwnersRegistered),
            MonotonicDelta(before.OwnersClosed, after.OwnersClosed),
            MonotonicDelta(before.NativeWrappersOwned, after.NativeWrappersOwned),
            MonotonicDelta(before.NativeWrappersDisposed, after.NativeWrappersDisposed),
            MonotonicDelta(
                before.ProjectionContainersOwned,
                after.ProjectionContainersOwned
            ),
            MonotonicDelta(
                before.ProjectionContainersDisposed,
                after.ProjectionContainersDisposed
            ),
            MonotonicDelta(before.TransfersOut, after.TransfersOut),
            MonotonicDelta(before.TransfersIn, after.TransfersIn)
        );

    private static long MonotonicDelta(long before, long after)
    {
        if (after < before)
            throw new InvalidOperationException("Lifecycle activity counters must be monotonic.");
        return after - before;
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string domain) =>
        counts != null && counts.TryGetValue(domain, out int count) ? count : 0;

    private static string FormatDomainCounts(IReadOnlyDictionary<string, int> counts) =>
        counts == null
            ? string.Empty
            : string.Join(
                ",",
                counts
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => $"{entry.Key}={entry.Value}")
            );

    private static string FormatProcessContentRoots(
        IReadOnlyList<ContentRootDiagnostic> diagnostics
    ) =>
        diagnostics == null
            ? string.Empty
            : string.Join(
                ";",
                diagnostics
                    .OrderBy(entry => entry.CanonicalPath, StringComparer.Ordinal)
                    .ThenBy(entry => entry.ResourceType, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Role)
                    .Select(entry =>
                        $"{entry.CanonicalPath}|{entry.ResourceType}|{entry.Role}"
                    )
            );

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
