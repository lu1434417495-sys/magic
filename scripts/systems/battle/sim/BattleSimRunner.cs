using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;

public sealed class BattleSimRunner
{
    private static readonly string ReportDirectory = "user://simulation_reports";
    private const int MaxIdleLoops = 25;

    private BattleSimOverrideApplier _overrideApplier = new();
    private BattleSimReportBuilder _reportBuilder = new();
    private BattleSimContentProvider _contentProvider = new();
    private BattleTerrainGenerator _terrainGenerator = new();
    private BattleSimExecutionLoop _executionLoop = new();
    private BattleSimTraceSummaryBuilder _traceSummaryBuilder = new();

    public bool progress_logging_enabled = false;
    public string progress_log_path = "";
    private FileAccess _progressLogFile = null;

    public void Setup(
        BattleSimContentProvider contentProvider = null,
        BattleTerrainGenerator terrainGenerator = null
    )
    {
        _contentProvider = contentProvider ?? new BattleSimContentProvider();
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

    public BattleSimScenarioReport RunScenario(
        BattleSimScenarioDef scenarioDef,
        IReadOnlyList<BattleSimProfileDef> profileDefs = null
    )
    {
        List<BattleSimProfileDef> resolvedProfiles = _ResolveProfiles(profileDefs);
        var resolvedSeeds = scenarioDef.ResolveSeeds();
        var report = new BattleSimScenarioReport
        {
            ScenarioDef = scenarioDef,
            GeneratedAtUnix = (int)Time.GetUnixTimeFromSystem(),
        };

        if (progress_logging_enabled)
        {
            _OpenProgressLog();
            _LogProgress(
                $"[BattleSim] progress_log={ProjectSettings.GlobalizePath(progress_log_path)}"
            );
            _LogProgress(
                $"[BattleSim] start scenario={scenarioDef.scenario_id} profiles={resolvedProfiles.Count} seeds={resolvedSeeds.Count} max_iterations={scenarioDef.max_iterations}"
            );
        }

        for (int profileIndex = 0; profileIndex < resolvedProfiles.Count; profileIndex++)
        {
            BattleSimProfileDef profile = resolvedProfiles[profileIndex];
            var runs = new List<BattleSimRunReport>();
            for (int seedIndex = 0; seedIndex < resolvedSeeds.Count; seedIndex++)
            {
                int seed = (int)resolvedSeeds[seedIndex];
                if (progress_logging_enabled)
                {
                    _LogProgress(
                        $"[BattleSim] run-start profile={profile.profile_id} profile_index={profileIndex + 1}/{resolvedProfiles.Count} seed={seed} seed_index={seedIndex + 1}/{resolvedSeeds.Count}"
                    );
                }

                BattleSimRunReport runResult = _RunSingleSimulation(scenarioDef, profile, seed);
                runs.Add(runResult);

                if (progress_logging_enabled)
                {
                    _LogProgress(
                        $"[BattleSim] run-done profile={profile.profile_id} seed={seed} ended={runResult.BattleEnded} winner={runResult.WinnerFactionId} final_tu={runResult.FinalTu} iterations={runResult.Iterations} timeline_steps={runResult.TimelineSteps} idle_loops={runResult.IdleLoops} ally_alive={runResult.AllyAlive} enemy_alive={runResult.EnemyAlive}"
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

        report.Comparisons.AddRange(_reportBuilder.BuildProfileComparisons(report.ProfileEntries));
        report.OutputFiles = _WriteReportFiles(scenarioDef, report);

        if (progress_logging_enabled)
        {
            _LogProgress(
                $"[BattleSim] report-written report_json={report.OutputFiles.ReportJson} traces_jsonl={report.OutputFiles.TurnTraceJsonl}"
            );
            _CloseProgressLog();
        }

        return report;
    }

    private List<BattleSimProfileDef> _ResolveProfiles(IReadOnlyList<BattleSimProfileDef> profileDefs)
    {
        var resolved = new List<BattleSimProfileDef>();
        if (profileDefs != null)
        {
            foreach (BattleSimProfileDef profile in profileDefs)
            {
                if (profile != null)
                    resolved.Add(profile);
            }
        }
        if (resolved.Count == 0)
        {
            var baseline = new BattleSimProfileDef();
            baseline.profile_id = "baseline";
            baseline.display_name = "Baseline";
            resolved.Add(baseline);
        }
        return resolved;
    }

    private BattleSimRunReport _RunSingleSimulation(
        BattleSimScenarioDef scenarioDef,
        BattleSimProfileDef profile,
        int seed
    )
    {
        var runtime = new BattleRuntimeModule();
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = _contentProvider.GetSkillDefsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            _contentProvider.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains =
            _contentProvider.GetEnemyAiBrainsTyped();
        BattleSimOverrideApplyResult overrides = _overrideApplier.ApplyProfileTyped(
            skillDefs,
            enemyAiBrains,
            profile
        );
        bool useFormalTerrain = scenarioDef != null && scenarioDef.use_formal_terrain_generation;

        runtime.setup(
            null,
            overrides.SkillDefs,
            enemyTemplates,
            overrides.EnemyAiBrains,
            null,
            default,
            null,
            useFormalTerrain ? null : _terrainGenerator
        );
        runtime.SetAiTraceEnabled(scenarioDef != null && scenarioDef.trace_enabled);
        runtime.SetAiScoreProfile(overrides.AiScoreProfile);
        runtime.SetFactionAiScoreProfiles(overrides.FactionAiScoreProfiles);

        EncounterAnchorData encounterAnchor = _BuildEncounterAnchor(scenarioDef);
        BattleState state = runtime.StartBattle(encounterAnchor, seed, scenarioDef.BuildStartContext());
        BattleSimExecutionLoopResult loopResult = _executionLoop.Run(
            runtime,
            state,
            scenarioDef,
            MaxIdleLoops
        );

        var runResult = new BattleSimRunReport
        {
            ScenarioId = scenarioDef != null ? scenarioDef.scenario_id.ToString() : "",
            ProfileId = profile.profile_id.ToString(),
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
                state != null
                    ? (Godot.Collections.Array)state.ally_unit_ids
                    : new Godot.Collections.Array()
            ),
            EnemyAlive = _CountLivingUnits(
                state,
                state != null
                    ? (Godot.Collections.Array)state.enemy_unit_ids
                    : new Godot.Collections.Array()
            ),
            Metrics = runtime.GetBattleMetricsTyped().ToDictionary(),
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

    private int _CountLivingUnits(BattleState state, Godot.Collections.Array unitIds)
    {
        if (state == null)
            return 0;
        int count = 0;
        foreach (Variant unitIdValue in unitIds)
        {
            StringName unitId = unitIdValue.AsStringName();
            BattleUnitState unitState = state.GetUnit(unitId);
            if (unitState != null && unitState.is_alive)
                count++;
        }
        return count;
    }

    private Godot.Collections.Array _BuildFinalUnitSnapshots(BattleState state)
    {
        var snapshots = new Godot.Collections.Array();
        if (state == null)
            return snapshots;
        foreach ((StringName _, BattleUnitState unitState) in state.UnitEntries(sorted: true))
        {
            if (unitState == null)
                continue;
            snapshots.Add(unitState.ToDictionary());
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

        var reportFile = FileAccess.Open(reportPath, FileAccess.ModeFlags.Write);
        if (reportFile != null)
        {
            reportFile.StoreString(
                Json.Stringify(ToVariant(_NormalizeValue(report.ToDictionary())), "\t")
            );
            reportFile.Close();
        }

        var traceFile = FileAccess.Open(tracePath, FileAccess.ModeFlags.Write);
        if (traceFile != null)
        {
            foreach (BattleSimProfileReportEntry profileEntry in report.ProfileEntries)
            {
                string profileId = profileEntry?.Profile?.profile_id.ToString() ?? "";
                if (profileEntry == null)
                    continue;

                foreach (BattleSimRunReport runEntry in profileEntry.Runs)
                {
                    if (runEntry?.AiTurnTraces == null)
                        continue;

                    foreach (BattleAiTurnTraceProjection traceEntry in runEntry.AiTurnTraces)
                    {
                        if (traceEntry == null)
                            continue;

                        var flattenedTrace = TraceDictionaryProjection.ToDictionary(
                            traceEntry.ToTraceDictionary()
                        );
                        flattenedTrace["scenario_id"] = scenarioKey;
                        flattenedTrace["profile_id"] = profileId;
                        flattenedTrace["seed"] = runEntry.Seed;
                        traceFile.StoreLine(
                            Json.Stringify(ToVariant(_NormalizeValue(flattenedTrace)))
                        );
                    }
                }
            }
            traceFile.Close();
        }

        if (hasTraces)
        {
            var summaryFile = FileAccess.Open(traceSummaryPath, FileAccess.ModeFlags.Write);
            if (summaryFile != null)
            {
                Godot.Collections.Dictionary traceSummary = _traceSummaryBuilder.Build(report, reportPath);
                summaryFile.StoreString(
                    Json.Stringify(ToVariant(_NormalizeValue(traceSummary)), "\t")
                );
                summaryFile.Close();
            }
        }

        return outputFiles;
    }

    private void _OpenProgressLog()
    {
        if (string.IsNullOrEmpty(progress_log_path))
            return;
        string baseDir = progress_log_path.GetBaseDir();
        if (!string.IsNullOrEmpty(baseDir))
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(baseDir));
        _progressLogFile = FileAccess.Open(progress_log_path, FileAccess.ModeFlags.Write);
    }

    private void _CloseProgressLog()
    {
        if (_progressLogFile != null)
        {
            _progressLogFile.Close();
            _progressLogFile = null;
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

    private object _NormalizeValue(object rawValue)
    {
        if (rawValue is Godot.Collections.Dictionary rawDictionary)
        {
            var normalized = new Godot.Collections.Dictionary();
            foreach (Variant key in rawDictionary.Keys)
                normalized[key.ToString()] = ToVariant(_NormalizeValue(rawDictionary[key]));
            return normalized;
        }
        if (rawValue is Godot.Collections.Array rawArray)
        {
            var normalized = new Godot.Collections.Array();
            foreach (Variant entry in rawArray)
                normalized.Add(ToVariant(_NormalizeValue(entry)));
            return normalized;
        }
        if (rawValue is not Variant value)
            return rawValue;

        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName().ToString();
        if (value.VariantType == Variant.Type.Vector2I)
        {
            Vector2I v = value.AsVector2I();
            return new Godot.Collections.Dictionary { ["x"] = v.X, ["y"] = v.Y };
        }
        if (value.VariantType == Variant.Type.Array)
        {
            var arr = value.AsGodotArray();
            var normalized = new Godot.Collections.Array();
            foreach (Variant entry in arr)
                normalized.Add(ToVariant(_NormalizeValue(entry)));
            return normalized;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dict = value.AsGodotDictionary();
            var normalized = new Godot.Collections.Dictionary();
            foreach (Variant key in dict.Keys)
                normalized[key.ToString()] = ToVariant(_NormalizeValue(dict[key]));
            return normalized;
        }
        if (value.VariantType == Variant.Type.Object)
        {
            GodotObject obj = value.AsGodotObject();
            if (obj is BattleSimScenarioDef scenarioDef)
                return _NormalizeValue(scenarioDef.ToDictionary());
            if (obj is BattleSimProfileDef profileDef)
                return _NormalizeValue(profileDef.ToDictionary());
            if (obj is BattleUnitState unitState)
                return _NormalizeValue(unitState.ToDictionary());
            return obj?.ToString() ?? "";
        }
        return value;
    }

    private static Variant ToVariant(object value) =>
        value switch
        {
            Variant variant => variant,
            string text => text,
            StringName stringName => stringName,
            int intValue => intValue,
            long longValue => longValue,
            bool boolValue => boolValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            Vector2I coord => coord,
            Godot.Collections.Array array => array,
            Godot.Collections.Dictionary dictionary => dictionary,
            GodotObject godotObject => godotObject,
            _ => default,
        };
}
