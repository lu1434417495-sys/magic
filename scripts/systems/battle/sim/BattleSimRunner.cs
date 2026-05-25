using System;
using System.Linq;
using Godot;
using Godot.Collections;
using FileAccess = Godot.FileAccess;

[GlobalClass]
public partial class BattleSimRunner : RefCounted
{
    private static readonly string BattleRuntimeModuleScriptPath = "res://scripts/systems/battle/runtime/battle_runtime_module.gd";
    private static readonly string ReportDirectory = "user://simulation_reports";
    private const int MaxIdleLoops = 25;
    private const int ProgressIterationInterval = 100;

    private BattleSimOverrideApplier _overrideApplier = new();
    private BattleSimReportBuilder _reportBuilder = new();
    private BattleSimContentProvider _contentProvider = new();
    private BattleSimTerrainGenerator _terrainGenerator = new();
    private BattleSimExecutionLoop _executionLoop = new();
    private BattleSimTraceSummaryBuilder _traceSummaryBuilder = new();

    public bool progress_logging_enabled = false;
    public string progress_log_path = "";
    private FileAccess _progressLogFile = null;

    public void Setup(BattleSimContentProvider contentProvider = null, BattleSimTerrainGenerator terrainGenerator = null)
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

    public Dictionary RunScenario(BattleSimScenarioDef scenarioDef, Godot.Collections.Array profileDefs = null)
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
            _LogProgress($"[BattleSim] progress_log={ProjectSettings.GlobalizePath(progress_log_path)}");
            _LogProgress($"[BattleSim] start scenario={scenarioDef.scenario_id} profiles={resolvedProfiles.Count} seeds={resolvedSeeds.Count} max_iterations={scenarioDef.max_iterations}");
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
                    _LogProgress($"[BattleSim] run-start profile={profile.profile_id} profile_index={profileIndex + 1}/{resolvedProfiles.Count} seed={seed} seed_index={seedIndex + 1}/{resolvedSeeds.Count}");
                }

                var runResult = _RunSingleSimulation(scenarioDef, profile, seed);
                runs.Add(runResult);

                if (progress_logging_enabled)
                {
                    _LogProgress($"[BattleSim] run-done profile={profile.profile_id} seed={seed} ended={DictionaryGet(runResult, "battle_ended", false)} winner={DictionaryGet(runResult, "winner_faction_id", "")} final_tu={DictionaryGet(runResult, "final_tu", 0)} iterations={DictionaryGet(runResult, "iterations", 0)} timeline_steps={DictionaryGet(runResult, "timeline_steps", 0)} idle_loops={DictionaryGet(runResult, "idle_loops", 0)} ally_alive={DictionaryGet(runResult, "ally_alive", 0)} enemy_alive={DictionaryGet(runResult, "enemy_alive", 0)}");
                }
            }
            report["profile_entries"].AsGodotArray().Add(new Dictionary
            {
                ["profile"] = profile.to_dict(),
                ["runs"] = runs,
                ["summary"] = _reportBuilder.build_profile_summary(profile, runs),
            });
        }

        report["comparisons"] = _reportBuilder.build_profile_comparisons(DictionaryGet(report, "profile_entries", new Godot.Collections.Array()).AsGodotArray());
        report["output_files"] = _WriteReportFiles(scenarioDef, report);

        if (progress_logging_enabled)
        {
            var outputFiles = DictionaryGet(report, "output_files", new Dictionary()).AsGodotDictionary();
            _LogProgress($"[BattleSim] report-written report_json={DictionaryGet(outputFiles, "report_json", "")} traces_jsonl={DictionaryGet(outputFiles, "turn_trace_jsonl", "")}");
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

    private Dictionary _RunSingleSimulation(BattleSimScenarioDef scenarioDef, BattleSimProfileDef profile, int seed)
    {
        var runtimeScript = GD.Load<GDScript>(BattleRuntimeModuleScriptPath);
        GodotObject runtime = runtimeScript.New().AsGodotObject();
        var skillDefs = _GetContentDictionary("get_skill_defs");
        var enemyAiBrains = _GetContentDictionary("get_enemy_ai_brains");
        var overrides = _overrideApplier.apply_profile(skillDefs, enemyAiBrains, profile);
        var useFormalTerrain = scenarioDef != null && scenarioDef.use_formal_terrain_generation;

        runtime.Call("setup",
            default(Variant),
            DictionaryGet(overrides, "skill_defs", new Dictionary()),
            _GetContentDictionary("get_enemy_templates"),
            DictionaryGet(overrides, "enemy_ai_brains", new Dictionary()),
            default(Variant),
            default(Variant),
            new Dictionary(),
            useFormalTerrain ? default(Variant) : (Variant)(GodotObject)_terrainGenerator
        );
        runtime.Call("set_ai_trace_enabled", scenarioDef != null && scenarioDef.trace_enabled);
        runtime.Call("set_ai_score_profile", DictionaryGet(overrides, "ai_score_profile", default(Variant)));

        var encounterAnchor = _BuildEncounterAnchor(scenarioDef);
        var state = runtime.Call("start_battle", encounterAnchor, seed, scenarioDef.build_start_context()).As<BattleState>();

        var loopResult = _executionLoop.Call("run", runtime, state, scenarioDef, new Dictionary
        {
            ["max_idle_loops"] = MaxIdleLoops,
            ["progress_iteration_interval"] = progress_logging_enabled ? ProgressIterationInterval : 0,
            ["progress_callback"] = Callable.From((Dictionary progressData) => _HandleRunProgress(progressData)),
            ["progress_context"] = new Dictionary
            {
                ["profile_id"] = profile.profile_id.ToString(),
                ["seed"] = seed,
            },
        }).AsGodotDictionary();

        var iterations = (int)DictionaryGet(loopResult, "iterations", 0);
        var idleLoops = (int)DictionaryGet(loopResult, "idle_loops", 0);
        var timelineSteps = (int)DictionaryGet(loopResult, "timeline_steps", 0);

        var runResult = new Dictionary
        {
            ["scenario_id"] = scenarioDef != null ? scenarioDef.scenario_id.ToString() : "",
            ["profile_id"] = profile.profile_id.ToString(),
            ["seed"] = seed,
            ["battle_id"] = state != null ? state.battle_id.ToString() : "",
            ["battle_ended"] = state != null && state.phase == "battle_ended",
            ["winner_faction_id"] = state != null ? state.winner_faction_id.ToString() : "",
            ["final_tu"] = state != null && state.timeline != null ? (int)state.timeline.Get("current_tu") : 0,
            ["iterations"] = iterations,
            ["idle_loops"] = idleLoops,
            ["timeline_steps"] = timelineSteps,
            ["ally_alive"] = _CountLivingUnits(state, state != null ? (Godot.Collections.Array)state.ally_unit_ids : new Godot.Collections.Array()),
            ["enemy_alive"] = _CountLivingUnits(state, state != null ? (Godot.Collections.Array)state.enemy_unit_ids : new Godot.Collections.Array()),
            ["metrics"] = runtime.Call("get_battle_metrics").AsGodotDictionary().Duplicate(true),
            ["ai_turn_traces"] = runtime.Call("get_ai_turn_traces").AsGodotArray().Duplicate(true),
            ["final_units"] = _BuildFinalUnitSnapshots(state),
        };

        runtime.Call("dispose");
        return runResult;
    }

    private void _HandleRunProgress(Dictionary progressData)
    {
        if (!progress_logging_enabled)
            return;
        var state = DictionaryGet(progressData, "state", default(Variant)).As<BattleState>();
        if (state == null)
            return;
        var context = DictionaryGet(progressData, "context", new Dictionary()).AsGodotDictionary();
        _LogProgress($"[BattleSim] progress profile={DictionaryGet(context, "profile_id", "")} seed={DictionaryGet(context, "seed", 0)} iteration={DictionaryGet(progressData, "iterations", 0)} timeline_steps={DictionaryGet(progressData, "timeline_steps", 0)} phase={state.phase} active_unit={state.active_unit_id} tu={(state.timeline != null ? state.timeline.Get("current_tu") : 0)} idle_loops={DictionaryGet(progressData, "idle_loops", 0)} {_BuildActiveUnitProgressSummary(state)} last_log=\"{_GetLastLogLine(state)}\"");
    }

    private Dictionary _GetContentDictionary(string methodName)
    {
        if (_contentProvider == null || !_contentProvider.HasMethod(methodName))
            return new Dictionary();
        var value = _contentProvider.Call(methodName);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new Dictionary();
    }

    private EncounterAnchorData _BuildEncounterAnchor(BattleSimScenarioDef scenarioDef)
    {
        var encounterAnchor = new EncounterAnchorData();
        encounterAnchor.entity_id = scenarioDef != null && scenarioDef.scenario_id != "" ? scenarioDef.scenario_id : "battle_sim";
        encounterAnchor.display_name = scenarioDef != null && !string.IsNullOrEmpty(scenarioDef.display_name) ? scenarioDef.display_name : scenarioDef.scenario_id;
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
        foreach (var unitIdVariant in unitIds)
        {
            var unitId = unitIdVariant.AsStringName();
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
        var scenarioKey = scenarioDef != null && scenarioDef.scenario_id != "" ? scenarioDef.scenario_id.ToString() : "battle_sim";
        var timestamp = (int)Time.GetUnixTimeFromSystem();
        var reportDir = $"{ReportDirectory}/{scenarioKey}";
        var ensureDirError = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(reportDir));
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
            reportFile.StoreString(Json.Stringify(_NormalizeVariant(report), "\t"));
            reportFile.Close();
        }

        var traceFile = FileAccess.Open(tracePath, FileAccess.ModeFlags.Write);
        if (traceFile != null)
        {
            foreach (var profileEntryVariant in DictionaryGet(report, "profile_entries", new Godot.Collections.Array()).AsGodotArray())
            {
                if (profileEntryVariant.VariantType != Variant.Type.Dictionary)
                    continue;
                var profileEntry = profileEntryVariant.AsGodotDictionary();
                var profileId = DictionaryGet(DictionaryGet(profileEntry, "profile", new Dictionary()).AsGodotDictionary(), "profile_id", "").ToString();
                foreach (var runEntryVariant in DictionaryGet(profileEntry, "runs", new Godot.Collections.Array()).AsGodotArray())
                {
                    if (runEntryVariant.VariantType != Variant.Type.Dictionary)
                        continue;
                    var runEntry = runEntryVariant.AsGodotDictionary();
                    foreach (var traceEntryVariant in DictionaryGet(runEntry, "ai_turn_traces", new Godot.Collections.Array()).AsGodotArray())
                    {
                        if (traceEntryVariant.VariantType != Variant.Type.Dictionary)
                            continue;
                        var flattenedTrace = traceEntryVariant.AsGodotDictionary().Duplicate(true);
                        flattenedTrace["scenario_id"] = scenarioKey;
                        flattenedTrace["profile_id"] = profileId;
                        flattenedTrace["seed"] = (int)DictionaryGet(runEntry, "seed", 0);
                        traceFile.StoreLine(Json.Stringify(_NormalizeVariant(flattenedTrace)));
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
                summaryFile.StoreString(Json.Stringify(_NormalizeVariant(traceSummary), "\t"));
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
        GD.Print(message);
        if (_progressLogFile != null)
        {
            _progressLogFile.StoreLine(message);
            _progressLogFile.Flush();
        }
    }

    private string _BuildActiveUnitProgressSummary(BattleState state)
    {
        if (state == null || state.active_unit_id == "")
            return "";
        if (!state.units.ContainsKey(state.active_unit_id))
            return "";
        var activeUnit = state.units[state.active_unit_id].As<BattleUnitState>();
        if (activeUnit == null)
            return "";
        var aiBlackboard = activeUnit.Get("ai_blackboard").AsGodotDictionary();
        return $"coord=({activeUnit.coord.X},{activeUnit.coord.Y}) hp={activeUnit.current_hp} ap={activeUnit.current_ap} stamina={activeUnit.current_stamina} move={activeUnit.current_move_points} last_action={DictionaryGet(aiBlackboard, "last_action_id", "")} decisions={DictionaryGet(aiBlackboard, "turn_decision_count", 0)}";
    }

    private string _GetLastLogLine(BattleState state)
    {
        if (state == null || state.log_entries.Count == 0)
            return "";
        return state.log_entries[state.log_entries.Count - 1].ToString().Replace("\n", " ");
    }

    private Variant _NormalizeVariant(Variant value)
    {
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
                normalized.Add(_NormalizeVariant(entry));
            return normalized;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dict = value.AsGodotDictionary();
            var normalized = new Dictionary();
            foreach (var key in dict.Keys)
                normalized[key.ToString()] = _NormalizeVariant(dict[key]);
            return normalized;
        }
        if (value.VariantType == Variant.Type.Object)
        {
            var obj = value.AsGodotObject();
            if (obj != null && obj.HasMethod("to_dict"))
                return _NormalizeVariant(obj.Call("to_dict"));
        }
        return value;
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }
}
