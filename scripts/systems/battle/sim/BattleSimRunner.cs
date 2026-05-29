using System;
using System.Linq;
using Godot;
using Godot.Collections;
using FileAccess = Godot.FileAccess;

[GlobalClass]
public partial class BattleSimRunner : RefCounted
{
    private static readonly string ReportDirectory = "user://simulation_reports";
    private const int MaxIdleLoops = 25;

    private BattleSimOverrideApplier _overrideApplier = new();
    private BattleSimReportBuilder _reportBuilder = new();
    private BattleSimContentProvider _contentProvider = new();
    private BattleSimTerrainGenerator _terrainGenerator = new();
    private BattleSimExecutionLoop _executionLoop = new();
    private BattleSimTraceSummaryBuilder _traceSummaryBuilder = new();

    public bool progress_logging_enabled = false;
    public string progress_log_path = "";
    private FileAccess _progressLogFile = null;

    public void Setup(
        BattleSimContentProvider contentProvider = null,
        BattleSimTerrainGenerator terrainGenerator = null
    )
    {
        _contentProvider = contentProvider ?? new BattleSimContentProvider();
        _terrainGenerator = terrainGenerator ?? new BattleSimTerrainGenerator();
    }

    public void SetProgressLoggingEnabled(bool enabled)
    {
        progress_logging_enabled = enabled;
    }

    public void SetProgressLogPath(string path)
    {
        progress_log_path = path;
    }

    public Dictionary RunScenario(
        BattleSimScenarioDef scenarioDef,
        Godot.Collections.Array profileDefs = null
    )
    {
        profileDefs ??= new Godot.Collections.Array();
        var resolvedProfiles = _ResolveProfiles(profileDefs);
        var resolvedSeeds = scenarioDef.resolve_seeds();
        var report = new Dictionary
        {
            ["scenario"] = scenarioDef.to_dict(),
            ["generated_at_unix"] = (int)Time.GetUnixTimeFromSystem(),
            ["profile_entries"] = new Godot.Collections.Array(),
            ["comparisons"] = new Godot.Collections.Array(),
            ["output_files"] = new Dictionary(),
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
            var profile = resolvedProfiles[profileIndex].AsGodotObject() as BattleSimProfileDef;
            var runs = new Godot.Collections.Array();
            for (int seedIndex = 0; seedIndex < resolvedSeeds.Count; seedIndex++)
            {
                var seed = (int)resolvedSeeds[seedIndex];
                if (progress_logging_enabled)
                {
                    _LogProgress(
                        $"[BattleSim] run-start profile={profile.profile_id} profile_index={profileIndex + 1}/{resolvedProfiles.Count} seed={seed} seed_index={seedIndex + 1}/{resolvedSeeds.Count}"
                    );
                }

                var runResult = _RunSingleSimulation(scenarioDef, profile, seed);
                runs.Add(runResult);

                if (progress_logging_enabled)
                {
                    _LogProgress(
                        $"[BattleSim] run-done profile={profile.profile_id} seed={seed} ended={runResult.GetValueOrDefault("battle_ended", false)} winner={runResult.GetValueOrDefault("winner_faction_id", "")} final_tu={runResult.GetValueOrDefault("final_tu", 0)} iterations={runResult.GetValueOrDefault("iterations", 0)} timeline_steps={runResult.GetValueOrDefault("timeline_steps", 0)} idle_loops={runResult.GetValueOrDefault("idle_loops", 0)} ally_alive={runResult.GetValueOrDefault("ally_alive", 0)} enemy_alive={runResult.GetValueOrDefault("enemy_alive", 0)}"
                    );
                }
            }
            report["profile_entries"]
                .AsGodotArray()
                .Add(
                    new Dictionary
                    {
                        ["profile"] = profile.to_dict(),
                        ["runs"] = runs,
                        ["summary"] = _reportBuilder.build_profile_summary(profile, runs),
                    }
                );
        }

        report["comparisons"] = _reportBuilder.build_profile_comparisons(
            report.GetValueOrDefault("profile_entries", new Godot.Collections.Array()).AsGodotArray()
        );
        report["output_files"] = _WriteReportFiles(scenarioDef, report);

        if (progress_logging_enabled)
        {
            var outputFiles = report.GetValueOrDefault("output_files", new Dictionary())
                .AsGodotDictionary();
            _LogProgress(
                $"[BattleSim] report-written report_json={outputFiles.GetValueOrDefault("report_json", "")} traces_jsonl={outputFiles.GetValueOrDefault("turn_trace_jsonl", "")}"
            );
            _CloseProgressLog();
        }

        return report;
    }

    private Godot.Collections.Array _ResolveProfiles(Godot.Collections.Array profileDefs)
    {
        var resolved = new Godot.Collections.Array();
        foreach (var profileDef in profileDefs)
        {
            var profile = profileDef.AsGodotObject() as BattleSimProfileDef;
            if (profile != null)
                resolved.Add(profile);
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

    private Dictionary _RunSingleSimulation(
        BattleSimScenarioDef scenarioDef,
        BattleSimProfileDef profile,
        int seed
    )
    {
        var runtime = new BattleRuntimeModule();
        var skillDefs = _contentProvider.get_skill_defs();
        var enemyAiBrains = _contentProvider.get_enemy_ai_brains();
        var overrides = _overrideApplier.apply_profile(skillDefs, enemyAiBrains, profile);
        var useFormalTerrain = scenarioDef != null && scenarioDef.use_formal_terrain_generation;

        runtime.setup(
            null,
            GetDictionary(overrides, "skill_defs"),
            _contentProvider.get_enemy_templates(),
            GetDictionary(overrides, "enemy_ai_brains"),
            null,
            default,
            new Dictionary(),
            useFormalTerrain ? null : _terrainGenerator
        );
        runtime.set_ai_trace_enabled(scenarioDef != null && scenarioDef.trace_enabled);
        runtime.set_ai_score_profile(GetObject(overrides, "ai_score_profile") as BattleAiScoreProfile);

        var encounterAnchor = _BuildEncounterAnchor(scenarioDef);
        var state = runtime.start_battle(encounterAnchor, seed, scenarioDef.build_start_context());
        var loopResult = _executionLoop.Run(runtime, state, scenarioDef, MaxIdleLoops);

        var runResult = new Dictionary
        {
            ["scenario_id"] = scenarioDef != null ? scenarioDef.scenario_id.ToString() : "",
            ["profile_id"] = profile.profile_id.ToString(),
            ["seed"] = seed,
            ["battle_id"] = state != null ? state.battle_id.ToString() : "",
            ["battle_ended"] = state != null && state.phase == "battle_ended",
            ["winner_faction_id"] = state != null ? state.winner_faction_id.ToString() : "",
            ["final_tu"] = state?.timeline?.current_tu ?? 0,
            ["iterations"] = loopResult.iterations,
            ["idle_loops"] = loopResult.idle_loops,
            ["timeline_steps"] = loopResult.timeline_steps,
            ["ally_alive"] = _CountLivingUnits(
                state,
                state != null
                    ? (Godot.Collections.Array)state.ally_unit_ids
                    : new Godot.Collections.Array()
            ),
            ["enemy_alive"] = _CountLivingUnits(
                state,
                state != null
                    ? (Godot.Collections.Array)state.enemy_unit_ids
                    : new Godot.Collections.Array()
            ),
            ["metrics"] = runtime.get_battle_metrics().Duplicate(true),
            ["ai_turn_traces"] = runtime.get_ai_turn_traces().Duplicate(true),
            ["final_units"] = _BuildFinalUnitSnapshots(state),
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
        foreach (var unitIdValue in unitIds)
        {
            var unitId = unitIdValue.AsStringName();
            if (!state.units.ContainsKey(unitId))
                continue;
            var unitState = state.units[unitId].As<BattleUnitState>();
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
        var sortedKeys = ProgressionDataUtils.sorted_string_keys(state.units);
        foreach (var unitIdStr in sortedKeys)
        {
            var unitId = new StringName(unitIdStr.ToString());
            if (!state.units.ContainsKey(unitId))
                continue;
            var unitState = state.units[unitId].As<BattleUnitState>();
            if (unitState == null)
                continue;
            snapshots.Add(unitState.to_dict());
        }
        return snapshots;
    }

    private Dictionary _WriteReportFiles(BattleSimScenarioDef scenarioDef, Dictionary report)
    {
        var scenarioKey =
            scenarioDef != null && scenarioDef.scenario_id != ""
                ? scenarioDef.scenario_id.ToString()
                : "battle_sim";
        var timestamp = (int)Time.GetUnixTimeFromSystem();
        var reportDir = $"{ReportDirectory}/{scenarioKey}";
        var ensureDirError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(reportDir)
        );
        if (ensureDirError != Error.Ok)
            return new Dictionary();

        var reportPath = $"{reportDir}/{scenarioKey}_{timestamp}_report.json";
        var tracePath = $"{reportDir}/{scenarioKey}_{timestamp}_turn_traces.jsonl";
        var traceSummaryPath = $"{reportDir}/{scenarioKey}_{timestamp}_trace_summary.json";
        var outputFiles = new Dictionary
        {
            ["report_json"] = reportPath,
            ["turn_trace_jsonl"] = tracePath,
        };

        var hasTraces = _traceSummaryBuilder.HasTraces(report);
        if (hasTraces)
            outputFiles["trace_summary_json"] = traceSummaryPath;

        report["output_files"] = outputFiles;

        var reportFile = FileAccess.Open(reportPath, FileAccess.ModeFlags.Write);
        if (reportFile != null)
        {
            reportFile.StoreString(
                Json.Stringify(GdInterop.GetValueOrDefault(null, "", _NormalizeValue(report)), "\t")
            );
            reportFile.Close();
        }

        var traceFile = FileAccess.Open(tracePath, FileAccess.ModeFlags.Write);
        if (traceFile != null)
        {
            foreach (
                var profileEntryValue in report
                    .GetValueOrDefault("profile_entries", new Godot.Collections.Array())
                    .AsGodotArray()
            )
            {
                if (profileEntryValue.VariantType != Variant.Type.Dictionary)
                    continue;
                var profileEntry = profileEntryValue.AsGodotDictionary();
                var profileId = profileEntry
                    .GetValueOrDefault("profile", new Dictionary())
                    .AsGodotDictionary()
                    .GetValueOrDefault("profile_id", "")
                    .ToString();
                foreach (
                    var runEntryValue in profileEntry
                        .GetValueOrDefault("runs", new Godot.Collections.Array())
                        .AsGodotArray()
                )
                {
                    if (runEntryValue.VariantType != Variant.Type.Dictionary)
                        continue;
                    var runEntry = runEntryValue.AsGodotDictionary();
                    foreach (
                        var traceEntryValue in runEntry
                            .GetValueOrDefault("ai_turn_traces", new Godot.Collections.Array())
                            .AsGodotArray()
                    )
                    {
                        if (traceEntryValue.VariantType != Variant.Type.Dictionary)
                            continue;
                        var flattenedTrace = traceEntryValue.AsGodotDictionary().Duplicate(true);
                        flattenedTrace["scenario_id"] = scenarioKey;
                        flattenedTrace["profile_id"] = profileId;
                        flattenedTrace["seed"] = (int)runEntry.GetValueOrDefault("seed", 0);
                        traceFile.StoreLine(
                            Json.Stringify(
                                GdInterop.GetValueOrDefault(null, "", _NormalizeValue(flattenedTrace))
                            )
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
                var traceSummary = _traceSummaryBuilder.Build(report, reportPath);
                summaryFile.StoreString(
                    Json.Stringify(
                        GdInterop.GetValueOrDefault(null, "", _NormalizeValue(traceSummary)),
                        "\t"
                    )
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
        var baseDir = progress_log_path.GetBaseDir();
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
        if (rawValue is Dictionary rawDictionary)
        {
            var normalized = new Dictionary();
            foreach (var key in rawDictionary.Keys)
                normalized[key.ToString()] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    _NormalizeValue(rawDictionary[key])
                );
            return normalized;
        }
        if (rawValue is Godot.Collections.Array rawArray)
        {
            var normalized = new Godot.Collections.Array();
            foreach (var entry in rawArray)
                normalized.Add(GdInterop.GetValueOrDefault(null, "", _NormalizeValue(entry)));
            return normalized;
        }
        if (rawValue is not Variant value)
            return rawValue;

        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName().ToString();
        if (value.VariantType == Variant.Type.Vector2I)
        {
            var v = value.AsVector2I();
            return new Dictionary { ["x"] = v.X, ["y"] = v.Y };
        }
        if (value.VariantType == Variant.Type.Array)
        {
            var arr = value.AsGodotArray();
            var normalized = new Godot.Collections.Array();
            foreach (var entry in arr)
                normalized.Add(GdInterop.GetValueOrDefault(null, "", _NormalizeValue(entry)));
            return normalized;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dict = value.AsGodotDictionary();
            var normalized = new Dictionary();
            foreach (var key in dict.Keys)
                normalized[key.ToString()] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    _NormalizeValue(dict[key])
                );
            return normalized;
        }
        if (value.VariantType == Variant.Type.Object)
        {
            var obj = value.AsGodotObject();
            if (obj is BattleSimScenarioDef scenarioDef)
                return _NormalizeValue(scenarioDef.to_dict());
            if (obj is BattleSimProfileDef profileDef)
                return _NormalizeValue(profileDef.to_dict());
            if (obj is BattleUnitState unitState)
                return _NormalizeValue(unitState.to_dict());
            return obj?.ToString() ?? "";
        }
        return value;
    }

    private static Dictionary GetDictionary(Dictionary dictionary, object key)
    {
        var value = dictionary.GetValueOrDefault(key, new Dictionary());
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Dictionary();
    }

    private static GodotObject GetObject(Dictionary dictionary, object key)
    {
        var value = dictionary.GetValueOrDefault(key);
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() : null;
    }
}
