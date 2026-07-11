using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSimRunner
{
    private static readonly string ReportDirectory = "user://simulation_reports";
    private const int MaxIdleLoops = 25;

    private BattleSimOverrideApplier _overrideApplier = new();
    private BattleSimReportBuilder _reportBuilder = new();
    private BattleSimContentProvider _contentProvider;
    private BattleTerrainGenerator _terrainGenerator = new();
    private BattleSimExecutionLoop _executionLoop = new();
    private BattleSimTraceSummaryBuilder _traceSummaryBuilder = new();

    public bool progress_logging_enabled = false;
    public string progress_log_path = "";
    private FileAccess _progressLogFile = null;
    private NativeLeaseScope _progressLogScope = null;

    internal BattleSimRunner(BattleSimContentProvider contentProvider)
    {
        _contentProvider = contentProvider
            ?? throw new System.ArgumentNullException(nameof(contentProvider));
    }

    public void Setup(
        BattleSimContentProvider contentProvider,
        BattleTerrainGenerator terrainGenerator = null
    )
    {
        _contentProvider = contentProvider
            ?? throw new System.ArgumentNullException(nameof(contentProvider));
        _terrainGenerator = terrainGenerator ?? new BattleTerrainGenerator();
    }

    public void SetProgressLoggingEnabled(bool enabled)
    {
        progress_logging_enabled = enabled;
    }

    public void SetProgressLogPath(string path)
    {
        progress_log_path = path;
    }

    internal BattleSimScenarioReport RunScenario(
        BattleSimScenarioDef scenarioDef,
        IReadOnlyList<BattleSimProfileDefinition> profileDefs = null
    )
    {
        List<BattleSimProfileDefinition> resolvedProfiles = _ResolveProfiles(profileDefs);
        ValidateProfileOverrides(resolvedProfiles);
        var resolvedSeeds = scenarioDef.ResolveSeeds();
        var report = new BattleSimScenarioReport
        {
            ScenarioDef = scenarioDef,
            GeneratedAtUnix = (int)Time.GetUnixTimeFromSystem(),
        };

        return RunInProgressLogScope(() =>
        {
            if (progress_logging_enabled)
            {
                _LogProgress(
                    $"[BattleSim] progress_log={ProjectSettings.GlobalizePath(progress_log_path)}"
                );
                _LogProgress(
                    $"[BattleSim] start scenario={scenarioDef.scenario_id} profiles={resolvedProfiles.Count} seeds={resolvedSeeds.Count} max_iterations={scenarioDef.max_iterations}"
                );
            }

            for (int profileIndex = 0; profileIndex < resolvedProfiles.Count; profileIndex++)
            {
                BattleSimProfileDefinition profile = resolvedProfiles[profileIndex];
                var runs = new List<BattleSimRunReport>();
                for (int seedIndex = 0; seedIndex < resolvedSeeds.Count; seedIndex++)
                {
                    int seed = (int)resolvedSeeds[seedIndex];
                    if (progress_logging_enabled)
                    {
                        _LogProgress(
                            $"[BattleSim] run-start profile={profile.ProfileId} profile_index={profileIndex + 1}/{resolvedProfiles.Count} seed={seed} seed_index={seedIndex + 1}/{resolvedSeeds.Count}"
                        );
                    }

                    BattleSimRunReport runResult = _RunSingleSimulation(
                        scenarioDef,
                        profile,
                        seed
                    );
                    runs.Add(runResult);

                    if (progress_logging_enabled)
                    {
                        _LogProgress(
                            $"[BattleSim] run-done profile={profile.ProfileId} seed={seed} ended={runResult.BattleEnded} winner={runResult.WinnerFactionId} final_tu={runResult.FinalTu} iterations={runResult.Iterations} timeline_steps={runResult.TimelineSteps} idle_loops={runResult.IdleLoops} ally_alive={runResult.AllyAlive} enemy_alive={runResult.EnemyAlive}"
                        );
                    }
                }

                report.ProfileEntries.Add(
                    new BattleSimProfileReportEntry
                    {
                        Profile = profile,
                        Summary = _reportBuilder.BuildProfileSummary(profile, runs),
                    }
                );
                report.ProfileEntries[^1].Runs.AddRange(runs);
            }

            report.Comparisons.AddRange(
                _reportBuilder.BuildProfileComparisons(report.ProfileEntries)
            );
            report.OutputFiles = _WriteReportFiles(scenarioDef, report);

            if (progress_logging_enabled)
            {
                _LogProgress(
                    $"[BattleSim] report-written report_json={report.OutputFiles.ReportJson} traces_jsonl={report.OutputFiles.TurnTraceJsonl}"
                );
            }

            return report;
        });
    }

    internal TResult RunInProgressLogScope<TResult>(System.Func<TResult> action)
    {
        System.ArgumentNullException.ThrowIfNull(action);
        try
        {
            if (progress_logging_enabled)
                _OpenProgressLog();
            return action();
        }
        finally
        {
            _CloseProgressLog();
        }
    }

    private List<BattleSimProfileDefinition> _ResolveProfiles(
        IReadOnlyList<BattleSimProfileDefinition> profileDefs
    )
    {
        var resolved = new List<BattleSimProfileDefinition>();
        if (profileDefs != null)
        {
            foreach (BattleSimProfileDefinition profile in profileDefs)
            {
                if (profile != null)
                    resolved.Add(profile);
            }
        }
        if (resolved.Count == 0)
        {
            IReadOnlyDictionary<StringName, BattleSimProfileDefinition> formalProfiles =
                _contentProvider.GetBattleSimProfilesTyped();
            if (formalProfiles.TryGetValue("baseline", out BattleSimProfileDefinition baseline))
                resolved.Add(baseline);
            else
                resolved.Add(
                    new BattleSimProfileDefinition(
                        "baseline",
                        "Baseline",
                        "",
                        BattleAiScoreProfileDefinition.Default,
                        System.Array.Empty<BattleSimOverridePatchDefinition>()
                    )
                );
        }
        return resolved;
    }

    private void ValidateProfileOverrides(
        IReadOnlyList<BattleSimProfileDefinition> profiles
    )
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skills =
            _contentProvider.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> brains =
            _contentProvider.GetEnemyAiBrainsTyped();
        foreach (BattleSimProfileDefinition profile in profiles)
        {
            BattleSimOverrideApplyResult result = _overrideApplier.ApplyProfileTyped(
                skills,
                brains,
                profile
            );
            ThrowIfOverrideErrors(profile, result);
        }
    }

    private static void ThrowIfOverrideErrors(
        BattleSimProfileDefinition profile,
        BattleSimOverrideApplyResult result
    )
    {
        if (result?.Errors == null || result.Errors.Count == 0)
            return;
        string profileId = profile?.ProfileId.ToString() ?? "<null>";
        throw new System.InvalidOperationException(
            $"Battle sim profile {profileId} override validation failed: {string.Join(" | ", result.Errors)}"
        );
    }

    private BattleSimRunReport _RunSingleSimulation(
        BattleSimScenarioDef scenarioDef,
        BattleSimProfileDefinition profile,
        int seed
    )
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            _contentProvider.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates =
            _contentProvider.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains =
            _contentProvider.GetEnemyAiBrainsTyped();
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfileDefinitions =
            _contentProvider.GetBarrierProfileDefinitionsTyped();
        BattleSimOverrideApplyResult overrides = _overrideApplier.ApplyProfileTyped(
            skillDefinitions,
            enemyAiBrains,
            profile
        );
        ThrowIfOverrideErrors(profile, overrides);
        var runtime = new BattleRuntimeModule();
        bool useFormalTerrain = scenarioDef != null && scenarioDef.use_formal_terrain_generation;

        runtime.setup(
            character_gateway: null,
            skill_definitions: overrides.SkillDefinitions,
            enemy_templates: enemyTemplates,
            enemy_ai_brains: overrides.EnemyAiBrains,
            encounter_builder: null,
            equipment_drop_service: default,
            item_defs: null,
            terrain_generator: useFormalTerrain ? null : _terrainGenerator,
            barrier_profile_definitions: barrierProfileDefinitions
        );
        runtime.SetAiTraceEnabled(scenarioDef != null && scenarioDef.trace_enabled);
        runtime.SetAiScoreProfile(overrides.AiScoreProfile);
        runtime.SetFactionAiScoreProfiles(overrides.FactionAiScoreProfiles);

        EncounterAnchorData encounterAnchor = _BuildEncounterAnchor(scenarioDef);
        using GodotProjectionLease<Godot.Collections.Dictionary> startContextLease =
            scenarioDef.BuildStartContextLease();
        BattleState state = runtime.StartBattle(encounterAnchor, seed, startContextLease.Value);
        BattleSimExecutionLoopResult loopResult = _executionLoop.Run(
            runtime,
            state,
            scenarioDef,
            MaxIdleLoops
        );

        var runResult = new BattleSimRunReport
        {
            ScenarioId = scenarioDef != null ? scenarioDef.scenario_id.ToString() : "",
            ProfileId = profile.ProfileId.ToString(),
            Seed = seed,
            BattleId = state != null ? state.battle_id.ToString() : "",
            BattleEnded = state != null && state.PhaseKind == BattlePhaseKind.BattleEnded,
            WinnerFactionId = state != null ? state.winner_faction_id.ToString() : "",
            FinalTu = state?.timeline?.current_tu ?? 0,
            Iterations = loopResult.iterations,
            IdleLoops = loopResult.idle_loops,
            TimelineSteps = loopResult.timeline_steps,
            AllyAlive = _CountLivingUnits(
                state,
                state?.ally_unit_ids
            ),
            EnemyAlive = _CountLivingUnits(
                state,
                state?.enemy_unit_ids
            ),
            MetricsSnapshot = BattleSimMetricsSnapshot.Capture(
                runtime.GetBattleMetricsTyped()
            ),
            AiTurnTraces = CloneAiTurnTraces(runtime.GetAiTurnTracesTyped()),
            FinalUnits = _BuildFinalUnitSnapshots(state),
        };

        runtime.dispose();
        return runResult;
    }

    private EncounterAnchorData _BuildEncounterAnchor(BattleSimScenarioDef scenarioDef)
    {
        var encounterAnchor = new EncounterAnchorData();
        encounterAnchor.entity_id =
            scenarioDef != null && scenarioDef.scenario_id != ""
                ? scenarioDef.scenario_id
                : "battle_sim";
        encounterAnchor.display_name =
            scenarioDef != null && !string.IsNullOrEmpty(scenarioDef.display_name)
                ? scenarioDef.display_name
                : scenarioDef.scenario_id;
        encounterAnchor.faction_id = "hostile";
        encounterAnchor.world_coord = Vector2I.Zero;
        encounterAnchor.region_tag = "simulation";
        return encounterAnchor;
    }

    private int _CountLivingUnits(BattleState state, IEnumerable<StringName> unitIds)
    {
        if (state == null || unitIds == null)
            return 0;
        int count = 0;
        foreach (StringName unitId in unitIds)
        {
            BattleUnitState unitState = state.GetUnit(unitId);
            if (unitState != null && unitState.is_alive)
                count++;
        }
        return count;
    }

    private static IReadOnlyList<Dictionary<string, object>> _BuildFinalUnitSnapshots(
        BattleState state
    )
    {
        var snapshots = new List<Dictionary<string, object>>();
        if (state == null)
            return snapshots;
        foreach ((StringName _, BattleUnitState unitState) in state.UnitEntries(sorted: true))
        {
            if (unitState != null)
                snapshots.Add(BattleUnitStatePlainSnapshot.Build(unitState));
        }
        return snapshots;
    }

    private static IReadOnlyList<BattleAiTurnTraceProjection> CloneAiTurnTraces(
        IReadOnlyList<BattleAiTurnTraceProjection> source
    )
    {
        var traces = new List<BattleAiTurnTraceProjection>();
        if (source == null)
            return traces;

        foreach (BattleAiTurnTraceProjection entry in source)
            traces.Add(entry?.Clone() ?? new BattleAiTurnTraceProjection());
        return traces;
    }

    private BattleSimOutputFiles _WriteReportFiles(
        BattleSimScenarioDef scenarioDef,
        BattleSimScenarioReport report
    )
    {
        string scenarioKey =
            scenarioDef != null && scenarioDef.scenario_id != ""
                ? scenarioDef.scenario_id.ToString()
                : "battle_sim";
        int timestamp = (int)Time.GetUnixTimeFromSystem();
        string reportDir = $"{ReportDirectory}/{scenarioKey}";
        Error ensureDirError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(reportDir)
        );
        if (ensureDirError != Error.Ok)
            return new BattleSimOutputFiles();

        string reportPath = $"{reportDir}/{scenarioKey}_{timestamp}_report.json";
        string tracePath = $"{reportDir}/{scenarioKey}_{timestamp}_turn_traces.jsonl";
        string traceSummaryPath = $"{reportDir}/{scenarioKey}_{timestamp}_trace_summary.json";
        var outputFiles = new BattleSimOutputFiles
        {
            ReportJson = reportPath,
            TurnTraceJsonl = tracePath,
        };

        bool hasTraces = _traceSummaryBuilder.HasTraces(report);
        if (hasTraces)
            outputFiles.TraceSummaryJson = traceSummaryPath;

        report.OutputFiles = outputFiles;

        using (NativeLeaseScope reportFileScope = new(
            "battle-sim-report-file",
            LifetimeDomain.Request
        ))
        {
            FileAccess openedReportFile = FileAccess.Open(
                reportPath,
                FileAccess.ModeFlags.Write
            );
            if (openedReportFile != null)
            {
                try
                {
                    FileAccess reportFile = reportFileScope.Own(
                        openedReportFile,
                        $"open:{reportPath}"
                    );
                    using GodotProjectionLease<GDictionary> reportLease =
                        BattleSimFilePayloadProjection.BuildReportLease(report);
                    reportFile.StoreString(Json.Stringify(reportLease.Value, "\t"));
                }
                finally
                {
                    openedReportFile.Close();
                }
            }
        }

        using (NativeLeaseScope traceFileScope = new(
            "battle-sim-trace-file",
            LifetimeDomain.Request
        ))
        {
            FileAccess openedTraceFile = FileAccess.Open(
                tracePath,
                FileAccess.ModeFlags.Write
            );
            if (openedTraceFile != null)
            {
                try
                {
                    FileAccess traceFile = traceFileScope.Own(
                        openedTraceFile,
                        $"open:{tracePath}"
                    );
                    foreach (BattleSimProfileReportEntry profileEntry in report.ProfileEntries)
                    {
                        string profileId = profileEntry?.Profile?.ProfileId.ToString() ?? "";
                        if (profileEntry == null)
                            continue;

                        foreach (BattleSimRunReport runEntry in profileEntry.Runs)
                        {
                            if (runEntry?.AiTurnTraces == null)
                                continue;

                            foreach (
                                BattleAiTurnTraceProjection traceEntry
                                in runEntry.AiTurnTraces
                            )
                            {
                                if (traceEntry == null)
                                    continue;

                                using GodotProjectionLease<GDictionary> traceLease =
                                    BattleSimFilePayloadProjection.BuildFlattenedTraceLease(
                                        traceEntry,
                                        scenarioKey,
                                        profileId,
                                        runEntry.Seed
                                    );
                                traceFile.StoreLine(Json.Stringify(traceLease.Value));
                            }
                        }
                    }
                }
                finally
                {
                    openedTraceFile.Close();
                }
            }
        }

        if (hasTraces)
        {
            using NativeLeaseScope summaryFileScope = new(
                "battle-sim-trace-summary-file",
                LifetimeDomain.Request
            );
            FileAccess openedSummaryFile = FileAccess.Open(
                traceSummaryPath,
                FileAccess.ModeFlags.Write
            );
            if (openedSummaryFile != null)
            {
                try
                {
                    FileAccess summaryFile = summaryFileScope.Own(
                        openedSummaryFile,
                        $"open:{traceSummaryPath}"
                    );
                    using GodotProjectionLease<GDictionary> traceSummaryLease =
                        _traceSummaryBuilder.BuildFileLease(report, reportPath);
                    summaryFile.StoreString(
                        Json.Stringify(traceSummaryLease.Value, "\t")
                    );
                }
                finally
                {
                    openedSummaryFile.Close();
                }
            }
        }

        return outputFiles;
    }

    private void _OpenProgressLog()
    {
        _CloseProgressLog();
        if (string.IsNullOrEmpty(progress_log_path))
            return;
        string baseDir = progress_log_path.GetBaseDir();
        if (!string.IsNullOrEmpty(baseDir))
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(baseDir));
        var scope = new NativeLeaseScope("battle-sim-progress-log", LifetimeDomain.Request);
        FileAccess openedFile = FileAccess.Open(
            progress_log_path,
            FileAccess.ModeFlags.Write
        );
        if (openedFile == null)
        {
            scope.Dispose();
            return;
        }
        try
        {
            _progressLogFile = scope.Own(openedFile, $"open:{progress_log_path}");
            _progressLogScope = scope;
        }
        catch
        {
            openedFile.Close();
            scope.Dispose();
            throw;
        }
    }

    private void _CloseProgressLog()
    {
        FileAccess file = _progressLogFile;
        NativeLeaseScope scope = _progressLogScope;
        _progressLogFile = null;
        _progressLogScope = null;
        try
        {
            file?.Close();
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private void _LogProgress(string message)
    {
        GameLog.Info(message, "battlesim.runner.progress", "battlesim");
        if (_progressLogFile != null)
        {
            _progressLogFile.StoreLine(message);
            _progressLogFile.Flush();
        }
    }

}
