using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSimRunner
{
    private const int MaxIdleLoops = 25;

    private BattleSimOverrideApplier _overrideApplier = new();
    private BattleSimReportBuilder _reportBuilder = new();
    private BattleSimContentProvider _contentProvider;
    private BattleTerrainGenerator _terrainGenerator = new();
    private BattleSimExecutionLoop _executionLoop = new();
    private readonly BattleSimReportFileWriter _reportFileWriter = new();
    private readonly System.Func<BattleRuntimeModule> _runtimeFactory;

    public bool progress_logging_enabled = false;
    public string progress_log_path = "";
    private FileAccess _progressLogFile = null;
    private NativeLeaseScope _progressLogScope = null;

    internal BattleSimRunner(BattleSimContentProvider contentProvider)
        : this(contentProvider, static () => new BattleRuntimeModule())
    {
    }

    internal BattleSimRunner(
        BattleSimContentProvider contentProvider,
        System.Func<BattleRuntimeModule> runtimeFactory
    )
    {
        _contentProvider = contentProvider
            ?? throw new System.ArgumentNullException(nameof(contentProvider));
        _runtimeFactory = runtimeFactory
            ?? throw new System.ArgumentNullException(nameof(runtimeFactory));
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
        BattleSimScenarioDefinition scenarioDefinition,
        IReadOnlyList<BattleSimProfileDefinition> profileDefs = null
    )
    {
        List<BattleSimProfileDefinition> resolvedProfiles = _ResolveProfiles(profileDefs);
        ValidateProfileOverrides(resolvedProfiles);
        System.ArgumentNullException.ThrowIfNull(scenarioDefinition);
        IReadOnlyList<int> resolvedSeeds = scenarioDefinition.Seeds;
        var report = new BattleSimScenarioReport
        {
            Scenario = scenarioDefinition,
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
                    $"[BattleSim] start scenario={scenarioDefinition.ScenarioId} profiles={resolvedProfiles.Count} seeds={resolvedSeeds.Count} max_iterations={scenarioDefinition.MaxIterations}"
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
                        scenarioDefinition,
                        profile,
                        seed
                    );
                    runs.Add(runResult);

                    if (progress_logging_enabled)
                    {
                        _LogProgress(
                            $"[BattleSim] run-done profile={profile.ProfileId} seed={seed} termination={BattleSimTerminationKindCodec.ToWireValue(runResult.TerminationKind)} ended={runResult.BattleEnded} winner={runResult.WinnerFactionId} final_tu={runResult.FinalTu} iterations={runResult.Iterations} timeline_steps={runResult.TimelineSteps} idle_loops={runResult.IdleLoops} ally_alive={runResult.AllyAlive} enemy_alive={runResult.EnemyAlive}"
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
            report.OutputFiles = _reportFileWriter.Write(scenarioDefinition, report);

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
        BattleSimScenarioDefinition scenarioDefinition,
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
        BattleRuntimeModule runtime = _runtimeFactory()
            ?? throw new System.InvalidOperationException(
                "Battle simulation runtime factory returned null."
            );
        bool useFormalTerrain = scenarioDefinition.UseFormalTerrainGeneration;
        BattleSimRunReport runResult;
        try
        {
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
            runtime.SetAiTraceEnabled(scenarioDefinition.TraceEnabled);
            runtime.SetAiScoreProfile(overrides.AiScoreProfile);
            runtime.SetFactionAiScoreProfiles(overrides.FactionAiScoreProfiles);

            EncounterAnchorData encounterAnchor = _BuildEncounterAnchor(scenarioDefinition);
            using GodotProjectionLease<Godot.Collections.Dictionary> startContextLease =
                scenarioDefinition.BuildStartContextLease();
            BattleState state = runtime.StartBattleBorrowingContext(
                encounterAnchor,
                seed,
                BattleEliminationObjectiveDefinition.Instance,
                startContextLease.Value
            );
            BattleSimExecutionLoopResult loopResult = _executionLoop.Run(
                runtime,
                state,
                scenarioDefinition,
                MaxIdleLoops
            );
            BattleStartFailureSnapshot startFailure =
                loopResult.termination_kind == BattleSimTerminationKind.InvalidRuntime
                    ? runtime.GetLastStartFailureSnapshot()
                    : new BattleStartFailureSnapshot();

            runResult = new BattleSimRunReport
            {
                ScenarioId = scenarioDefinition.ScenarioId.ToString(),
                ProfileId = profile.ProfileId.ToString(),
                Seed = seed,
                BattleId = state != null ? state.battle_id.ToString() : "",
                TerminationKind = loopResult.termination_kind,
                StartFailure = startFailure,
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
            runResult.SetFinalDecision(state?.FinalDecision);
        }
        catch (System.Exception runFailure)
        {
            try
            {
                runtime.Dispose();
            }
            catch (System.Exception cleanupFailure)
            {
                throw new System.AggregateException(
                    "Battle simulation and runtime cleanup both failed.",
                    runFailure,
                    cleanupFailure
                );
            }
            throw;
        }

        runtime.Dispose();
        return runResult;
    }

    private EncounterAnchorData _BuildEncounterAnchor(
        BattleSimScenarioDefinition scenarioDefinition
    )
    {
        var encounterAnchor = new EncounterAnchorData();
        encounterAnchor.entity_id =
            scenarioDefinition.ScenarioId != ""
                ? scenarioDefinition.ScenarioId
                : "battle_sim";
        encounterAnchor.display_name =
            !string.IsNullOrEmpty(scenarioDefinition.DisplayName)
                ? scenarioDefinition.DisplayName
                : scenarioDefinition.ScenarioId;
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
