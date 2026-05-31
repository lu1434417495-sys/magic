using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using VT = Godot.Variant.Type;

public partial class RunMixed6v12MirrorAnalysis : SceneTree
{
    private const int MaxIdleLoops = 25;
    private const int DefaultSimulationTimeoutSeconds = 30 * 60;
    private const string ScenarioPath = "res://data/configs/battle_sim/scenarios/mixed_6v12_mirror_simulation.tres";

    private bool _progressEnabled = true;

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        long startSeed = OS.HasEnvironment("START_SEED")
            ? ReadLongEnvironment("START_SEED", 101)
            : TrueRandomSeedService.generate_seed();
        string startSeedSource = OS.HasEnvironment("START_SEED") ? "environment" : "true_random";
        int requestedRunCount = ReadIntEnvironment("COUNT", 10);
        var explicitSeeds = ReadLongListEnvironment("SEEDS");
        if (explicitSeeds.Count > 0)
        {
            startSeedSource = "explicit_seeds";
            requestedRunCount = explicitSeeds.Count;
        }

        string outputPath = ReadStringEnvironment("OUTPUT_FILE", "");
        bool traceAi = ReadBoolEnvironment("TRACE_AI", true);
        int timeoutSeconds = ReadIntEnvironment("SIM_TIMEOUT_SECONDS", DefaultSimulationTimeoutSeconds);
        _progressEnabled = ReadBoolEnvironment("PROGRESS", true);
        bool aiMutationGuardEnabled = ReadBoolEnvironment("AI_MUTATION_GUARD", false);
        bool validateSpawnReachability = ReadBoolEnvironment("VALIDATE_SPAWN_REACHABILITY", true);
        bool validateBidirectionalSpawnReachability = ReadBoolEnvironment("VALIDATE_BIDIRECTIONAL_SPAWN_REACHABILITY", true);
        if (ReadBoolEnvironment("AI_PROFILE", false))
            GameLog.Warning("AI_PROFILE is not supported by the C# mixed 6v12 runner yet.", "bench.unsupported_flag", "bench");

        var scenario = ResourceLoader.Load<BattleSimScenarioDef>(ScenarioPath);
        if (scenario == null)
        {
            GameLog.Error($"Failed to load scenario: {ScenarioPath}", "bench.scenario.load_failed", "bench");
            return 1;
        }

        var contentProvider = new BattleSimContentProvider();
        var overrideApplier = new BattleSimOverrideApplier();
        var terrainGenerator = new BattleTerrainGenerator();
        var progressionRegistry = new ProgressionContentRegistry();
        var itemRegistry = new ItemContentRegistry();

        GDictionary skillDefs = contentProvider.get_skill_defs();
        GDictionary enemyAiBrains = contentProvider.get_enemy_ai_brains();
        if (skillDefs.Count == 0 || enemyAiBrains.Count == 0)
        {
            GameLog.Error($"Battle sim content provider returned empty content: skills={skillDefs.Count}, brains={enemyAiBrains.Count}.", "bench.content.empty", "bench");
            DisposeObjects(scenario, itemRegistry, progressionRegistry, terrainGenerator, overrideApplier, contentProvider);
            return 1;
        }

        var baseline = new BattleSimProfileDef
        {
            profile_id = "baseline",
            display_name = "Baseline",
        };
        GDictionary overrides = overrideApplier.apply_profile(skillDefs, enemyAiBrains, baseline);
        GDictionary rosterOptions = BuildRosterOptionsFromEnvironment();

        var rng = new RandomNumberGenerator { Seed = (ulong)Math.Max(startSeed, 1L) };
        var accum = new BatchAccumulator();
        var perUnitSummary = new GDictionary();
        var runDetails = new GArray();

        ulong batchStartMsec = Time.GetTicksMsec();
        int completedRunCount = 0;
        bool timedOut = false;

        PrintProgress(
            $"[Progress] start 6v12 C# runs={requestedRunCount} start_seed={startSeed} source={startSeedSource} timeout={timeoutSeconds}s output={(string.IsNullOrEmpty(outputPath) ? "<stdout>" : outputPath)}"
        );
        PrintProgress($"[Progress] ai_mutation_guard={aiMutationGuardEnabled}");
        PrintProgress($"[Progress] validate_spawn_reachability={validateSpawnReachability} validate_bidirectional_spawn_reachability={validateBidirectionalSpawnReachability}");

        for (int runIndex = 0; runIndex < requestedRunCount; runIndex++)
        {
            if (HasReachedTimeout(batchStartMsec, timeoutSeconds))
            {
                timedOut = true;
                break;
            }

            long seed = runIndex < explicitSeeds.Count ? explicitSeeds[runIndex] : rng.Randi();
            ulong runStartMsec = Time.GetTicksMsec();
            PrintProgress(
                $"[Progress] run {runIndex + 1}/{requestedRunCount} start seed={seed} batch_elapsed={(Time.GetTicksMsec() - batchStartMsec) / 1000.0:F1}s"
            );

            var fixture = BuildFormalFixture(
                scenario,
                overrides,
                progressionRegistry,
                itemRegistry,
                rosterOptions,
                (int)seed
            );
            GDictionary result;
            try
            {
                result = RunSingleSimulation(
                    scenario,
                    overrides,
                    contentProvider,
                    terrainGenerator,
                    fixture,
                    seed,
                    traceAi,
                    aiMutationGuardEnabled,
                    validateSpawnReachability,
                    validateBidirectionalSpawnReachability
                );

                GDictionary metrics = GetDict(result, "metrics");
                GDictionary factions = GetDict(metrics, "factions");
                GDictionary units = GetDict(result, "units");
                MergePerUnitSummary(perUnitSummary, units);
                runDetails.Add(BuildRunDetail(runIndex, seed, result, factions, units, traceAi));
                accum.AbsorbRun(result, factions, fixture);
                completedRunCount++;
            }
            finally
            {
                fixture.Dispose();
            }

            double elapsed = (Time.GetTicksMsec() - batchStartMsec) / 1000.0;
            double runElapsed = (Time.GetTicksMsec() - runStartMsec) / 1000.0;
            PrintProgress(
                $"[Progress] run {runIndex + 1}/{requestedRunCount} done winner={GetString(result, "winner_faction_id")} ended={GetBool(result, "battle_ended")} iterations={GetInt(result, "iterations")} timeline_steps={GetInt(result, "timeline_steps")} run_elapsed={runElapsed:F1}s batch_elapsed={elapsed:F1}s rate={(runIndex + 1) / Math.Max(elapsed, 0.001):F2} runs/s"
            );

            if (HasReachedTimeout(batchStartMsec, timeoutSeconds) && completedRunCount < requestedRunCount)
            {
                timedOut = true;
                break;
            }
        }

        double elapsedTotal = (Time.GetTicksMsec() - batchStartMsec) / 1000.0;
        double n = Math.Max(completedRunCount, 1);
        var report = BuildReport(
            startSeed,
            startSeedSource,
            requestedRunCount,
            completedRunCount,
            timeoutSeconds,
            timedOut,
            elapsedTotal,
            aiMutationGuardEnabled,
            validateSpawnReachability,
            validateBidirectionalSpawnReachability,
            scenario,
            accum,
            perUnitSummary,
            runDetails,
            n
        );
        if (traceAi)
            report["trace_summary_file"] = ResolveTraceSummaryPath(outputPath);

        if (string.IsNullOrEmpty(outputPath))
        {
            GameLog.Info(
                Json.Stringify(GdInterop.GetValueOrDefault(null, "", NormalizeValue(report)), "\t"),
                "bench.report",
                "bench"
            );
        }
        else if (!WriteJsonFile(outputPath, report))
        {
            GameLog.Error($"[ERROR] Failed to write: {outputPath}.", "bench.output_write_failed", "bench");
        }
        else
        {
            PrintProgress($"[Progress] wrote report {outputPath}");
        }

        if (traceAi)
        {
            string traceSummaryPath = GetString(report, "trace_summary_file");
            var traceSummaryBuilder = new BattleSimTraceSummaryBuilder();
            var compactReport = traceSummaryBuilder.Build(report, outputPath, new GDictionary());
            traceSummaryBuilder.Dispose();
            if (!WriteJsonFile(traceSummaryPath, compactReport))
                GameLog.Error($"[ERROR] Failed to write trace summary: {traceSummaryPath}.", "bench.trace_write_failed", "bench");
            else
                PrintProgress($"[Progress] wrote trace summary {traceSummaryPath}");
        }

        DisposeObjects(baseline, scenario, itemRegistry, progressionRegistry, terrainGenerator, overrideApplier, contentProvider);
        return 0;
    }

    private static void DisposeObjects(params GodotObject[] objects)
    {
        foreach (GodotObject obj in objects)
            obj?.Dispose();
    }

    private static BattleSimFormalCombatFixture BuildFormalFixture(
        BattleSimScenarioDef scenario,
        GDictionary overrides,
        ProgressionContentRegistry progressionRegistry,
        ItemContentRegistry itemRegistry,
        GDictionary rosterOptions,
        int attributeRollSeed
    )
    {
        var fixture = new BattleSimFormalCombatFixture();
        fixture.setup_content(
            new GDictionary
            {
                ["skill_defs"] = GetDict(overrides, "skill_defs"),
                ["profession_defs"] = progressionRegistry.get_profession_defs(),
                ["achievement_defs"] = progressionRegistry.get_achievement_defs(),
                ["item_defs"] = itemRegistry.get_item_defs(),
                ["progression_content_bundle"] = progressionRegistry.get_bundle(),
            }
        );
        GDictionary effectiveRosterOptions = rosterOptions.Duplicate(true).AsGodotDictionary();
        string attributeSeedKey = BattleSimFormalCombatFixture.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED_VALUE();
        if (!effectiveRosterOptions.ContainsKey(attributeSeedKey) && !effectiveRosterOptions.ContainsKey(new StringName(attributeSeedKey)))
            effectiveRosterOptions[attributeSeedKey] = attributeRollSeed;
        if (!fixture.build_roster(scenario.scenario_id, effectiveRosterOptions))
            GameLog.Error($"Unsupported formal battle sim roster: {scenario.scenario_id}", "bench.roster.unsupported", "bench");
        return fixture;
    }

    private GDictionary RunSingleSimulation(
        BattleSimScenarioDef scenario,
        GDictionary overrides,
        BattleSimContentProvider contentProvider,
        BattleTerrainGenerator terrainGenerator,
        BattleSimFormalCombatFixture fixture,
        long seed,
        bool traceAi,
        bool aiMutationGuardEnabled,
        bool validateSpawnReachability,
        bool validateBidirectionalSpawnReachability
    )
    {
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        EncounterAnchorData encounterAnchor = null;
        try
        {
            bool useFormalTerrain = scenario != null && scenario.use_formal_terrain_generation;
            PrintProgress($"[Progress] run seed={seed} runtime setup start");
            runtime.setup(
                fixture,
                GetDict(overrides, "skill_defs"),
                contentProvider.get_enemy_templates(),
                GetDict(overrides, "enemy_ai_brains"),
                null,
                default,
                fixture.get_item_defs(),
                useFormalTerrain ? null : terrainGenerator,
                default,
                new GDictionary()
            );
            PrintProgress($"[Progress] run seed={seed} runtime setup done");
            runtime.set_ai_trace_enabled(traceAi);
            runtime._ai_service.enable_mutation_guard = aiMutationGuardEnabled;
            runtime.set_ai_score_profile(GetObject(overrides, "ai_score_profile") as BattleAiScoreProfile);

            encounterAnchor = new EncounterAnchorData
            {
                entity_id = scenario != null && scenario.scenario_id != "" ? scenario.scenario_id : "battle_sim",
                display_name = scenario != null && !string.IsNullOrEmpty(scenario.display_name) ? scenario.display_name : scenario?.scenario_id.ToString() ?? "battle_sim",
                faction_id = "hostile",
                world_coord = Vector2I.Zero,
                region_tag = "simulation",
            };

            GDictionary context = fixture.build_runtime_context(runtime, scenario.build_start_context());
            context["validate_spawn_reachability"] = validateSpawnReachability;
            context["validate_bidirectional_spawn_reachability"] = validateBidirectionalSpawnReachability;
            PrintProgress($"[Progress] run seed={seed} start_battle start");
            state = runtime.start_battle(encounterAnchor, (int)seed, context);
            PrintProgress($"[Progress] run seed={seed} start_battle done phase={state?.phase}");
            GDictionary startFailure = runtime.get_last_start_failure();
            fixture.apply_started_battle_metadata(state);

            PrintProgress($"[Progress] run seed={seed} execution_loop start");
            var loopResult = new BattleSimExecutionLoop().Run(runtime, state, scenario, MaxIdleLoops);
            PrintProgress($"[Progress] run seed={seed} execution_loop done");

            GDictionary rawMetrics = runtime.get_battle_metrics().Duplicate(true).AsGodotDictionary();
            GDictionary metrics = NormalizeValue(rawMetrics) is GDictionary normalizedMetrics
                ? normalizedMetrics
                : new GDictionary();
            var result = new GDictionary
            {
                ["battle_ended"] = state != null && state.phase == "battle_ended",
                ["winner_faction_id"] = state != null ? state.winner_faction_id.ToString() : "",
                ["iterations"] = loopResult.iterations,
                ["timeline_steps"] = loopResult.timeline_steps,
                ["metrics"] = metrics,
                ["units"] = GetDict(metrics, "units"),
                ["factions"] = GetDict(metrics, "factions"),
            };
            if (startFailure.Count > 0)
                result["start_failure"] = startFailure;
            if (traceAi)
            {
                GArray rawTraces = (GArray)runtime.get_ai_turn_traces().Duplicate(true);
                result["ai_turn_traces"] = NormalizeValue(rawTraces) is GArray normalizedTraces
                    ? normalizedTraces
                    : new GArray();
            }
            return result;
        }
        finally
        {
            runtime.dispose();
            state?.Dispose();
            encounterAnchor?.Dispose();
        }
    }

    private static GDictionary BuildRosterOptionsFromEnvironment()
    {
        var options = new GDictionary();
        if (OS.HasEnvironment("MAIN_CHARACTER_MEMBER_ID"))
        {
            string memberId = OS.GetEnvironment("MAIN_CHARACTER_MEMBER_ID").StripEdges();
            if (!string.IsNullOrEmpty(memberId))
                options[BattleSimFormalCombatFixture.ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID_VALUE()] = new StringName(memberId);
        }
        if (OS.HasEnvironment("LEADER_MEMBER_ID"))
        {
            string leaderId = OS.GetEnvironment("LEADER_MEMBER_ID").StripEdges();
            if (!string.IsNullOrEmpty(leaderId))
                options[BattleSimFormalCombatFixture.ROSTER_OPTION_LEADER_MEMBER_ID_VALUE()] = new StringName(leaderId);
        }
        if (OS.HasEnvironment("MAIN_CHARACTER_REROLL_COUNT"))
            options[BattleSimFormalCombatFixture.ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT_VALUE()] = ReadIntEnvironment("MAIN_CHARACTER_REROLL_COUNT", 0);
        if (OS.HasEnvironment("ATTRIBUTE_ROLL_SEED"))
            options[BattleSimFormalCombatFixture.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED_VALUE()] = ReadIntEnvironment("ATTRIBUTE_ROLL_SEED", 0);
        return options;
    }

    private static GDictionary BuildRunDetail(
        int runIndex,
        long seed,
        GDictionary result,
        GDictionary factions,
        GDictionary units,
        bool traceAi
    )
    {
        var runFactions = new GDictionary();
        foreach (var factionKey in factions.Keys)
        {
            GDictionary factionData = GetDict(factions, factionKey);
            if (factionData.Count == 0)
                continue;
            runFactions[factionKey] = new GDictionary
            {
                ["total_damage_done"] = GetInt(factionData, "total_damage_done"),
                ["total_damage_taken"] = GetInt(factionData, "total_damage_taken"),
                ["kill_count"] = GetInt(factionData, "kill_count"),
                ["death_count"] = GetInt(factionData, "death_count"),
                ["turn_count"] = GetInt(factionData, "turn_count"),
            };
        }

        var runUnits = new GDictionary();
        foreach (var unitId in units.Keys)
        {
            GDictionary unitData = GetDict(units, unitId);
            if (unitData.Count == 0)
                continue;
            runUnits[unitId] = new GDictionary
            {
                ["display_name"] = GetString(unitData, "display_name"),
                ["faction_id"] = GetString(unitData, "faction_id"),
                ["turn_count"] = GetInt(unitData, "turn_count"),
                ["total_damage_done"] = GetInt(unitData, "total_damage_done"),
                ["total_damage_taken"] = GetInt(unitData, "total_damage_taken"),
                ["kill_count"] = GetInt(unitData, "kill_count"),
                ["death_count"] = GetInt(unitData, "death_count"),
                ["skill_attempts"] = GetDict(unitData, "skill_attempt_counts"),
                ["skill_successes"] = GetDict(unitData, "skill_success_counts"),
            };
        }

        var detail = new GDictionary
        {
            ["run_index"] = runIndex,
            ["seed"] = seed,
            ["winner_faction_id"] = GetString(result, "winner_faction_id"),
            ["iterations"] = GetInt(result, "iterations"),
            ["timeline_steps"] = GetInt(result, "timeline_steps"),
            ["factions"] = runFactions,
            ["units"] = runUnits,
        };
        GDictionary startFailure = GetDict(result, "start_failure");
        if (startFailure.Count > 0)
            detail["start_failure"] = startFailure;
        if (traceAi)
            detail["ai_turn_traces"] = GetArray(result, "ai_turn_traces");
        return detail;
    }

    private static void MergePerUnitSummary(GDictionary perUnitSummary, GDictionary units)
    {
        foreach (var unitId in units.Keys)
        {
            GDictionary unitData = GetDict(units, unitId);
            if (unitData.Count == 0)
                continue;
            if (!perUnitSummary.ContainsKey(unitId))
            {
                perUnitSummary[unitId] = new GDictionary
                {
                    ["display_name"] = GetString(unitData, "display_name"),
                    ["faction_id"] = GetString(unitData, "faction_id"),
                    ["runs"] = 0,
                    ["turn_count"] = 0,
                    ["total_damage_done"] = 0,
                    ["total_damage_taken"] = 0,
                    ["total_healing_done"] = 0,
                    ["total_healing_received"] = 0,
                    ["kill_count"] = 0,
                    ["death_count"] = 0,
                    ["skill_attempts"] = new GDictionary(),
                    ["skill_successes"] = new GDictionary(),
                };
            }
            GDictionary summary = GetDict(perUnitSummary, unitId);
            summary["runs"] = GetInt(summary, "runs") + 1;
            summary["turn_count"] = GetInt(summary, "turn_count") + GetInt(unitData, "turn_count");
            summary["total_damage_done"] = GetInt(summary, "total_damage_done") + GetInt(unitData, "total_damage_done");
            summary["total_damage_taken"] = GetInt(summary, "total_damage_taken") + GetInt(unitData, "total_damage_taken");
            summary["total_healing_done"] = GetInt(summary, "total_healing_done") + GetInt(unitData, "total_healing_done");
            summary["total_healing_received"] = GetInt(summary, "total_healing_received") + GetInt(unitData, "total_healing_received");
            summary["kill_count"] = GetInt(summary, "kill_count") + GetInt(unitData, "kill_count");
            summary["death_count"] = GetInt(summary, "death_count") + GetInt(unitData, "death_count");
            MergeSkillCounters(GetDict(summary, "skill_attempts"), GetDict(unitData, "skill_attempt_counts"));
            MergeSkillCounters(GetDict(summary, "skill_successes"), GetDict(unitData, "skill_success_counts"));
        }
    }

    private static void MergeSkillCounters(GDictionary target, GDictionary source)
    {
        foreach (var skillId in source.Keys)
            target[skillId] = GetInt(target, skillId) + source[skillId].AsInt32();
    }

    private static GDictionary BuildReport(
        long startSeed,
        string startSeedSource,
        int requestedRunCount,
        int completedRunCount,
        int timeoutSeconds,
        bool timedOut,
        double elapsedSeconds,
        bool aiMutationGuardEnabled,
        bool validateSpawnReachability,
        bool validateBidirectionalSpawnReachability,
        BattleSimScenarioDef scenario,
        BatchAccumulator accum,
        GDictionary perUnitSummary,
        GArray runDetails,
        double n
    )
    {
        return new GDictionary
        {
            ["scenario"] = scenario.to_dict(),
            ["generated_at_unix"] = (long)Time.GetUnixTimeFromSystem(),
            ["batch_id"] = startSeed,
            ["start_seed"] = startSeed,
            ["start_seed_source"] = startSeedSource,
            ["run_count"] = completedRunCount,
            ["requested_run_count"] = requestedRunCount,
            ["completed_run_count"] = completedRunCount,
            ["timeout_seconds"] = timeoutSeconds,
            ["timed_out"] = timedOut,
            ["elapsed_seconds"] = elapsedSeconds,
            ["ai_mutation_guard_enabled"] = aiMutationGuardEnabled,
            ["validate_spawn_reachability"] = validateSpawnReachability,
            ["validate_bidirectional_spawn_reachability"] = validateBidirectionalSpawnReachability,
            ["ended_count"] = accum.EndedCount,
            ["avg_iterations"] = accum.TotalIterations / n,
            ["avg_timeline_steps"] = accum.TotalTimelineSteps / n,
            ["win_rate"] = new GDictionary
            {
                ["player"] = accum.TotalWinsPlayer,
                ["hostile"] = accum.TotalWinsHostile,
                ["draw"] = accum.TotalDraws,
            },
            ["global"] = new GDictionary
            {
                ["charge"] = BuildSkillReport(accum.TotalChargeAttempts, accum.TotalChargeSuccesses, accum.TotalChargeMastery, n),
                ["warrior_heavy_strike"] = BuildSkillReport(accum.TotalHeavyAttempts, accum.TotalHeavySuccesses, accum.TotalHeavyMastery, n),
                ["archer_aimed_shot"] = BuildSkillReport(accum.TotalAimedAttempts, accum.TotalAimedSuccesses, accum.TotalAimedMastery, n),
                ["archer_multishot"] = BuildSkillReport(accum.TotalMultishotAttempts, accum.TotalMultishotSuccesses, accum.TotalMultishotMastery, n),
                ["basic_attack"] = BuildSkillReport(accum.TotalBasicAttempts, accum.TotalBasicSuccesses, accum.TotalBasicMastery, n),
            },
            ["player"] = BuildFactionReport(accum.PlayerDamageDone, accum.PlayerDamageTaken, accum.PlayerChargeAttempts, accum.PlayerChargeSuccesses, accum.PlayerHeavyAttempts, accum.PlayerHeavySuccesses, accum.PlayerAimedAttempts, accum.PlayerAimedSuccesses, accum.PlayerMultishotAttempts, accum.PlayerMultishotSuccesses, accum.PlayerBasicAttempts, accum.PlayerBasicSuccesses, n),
            ["hostile"] = BuildFactionReport(accum.HostileDamageDone, accum.HostileDamageTaken, accum.HostileChargeAttempts, accum.HostileChargeSuccesses, accum.HostileHeavyAttempts, accum.HostileHeavySuccesses, accum.HostileAimedAttempts, accum.HostileAimedSuccesses, accum.HostileMultishotAttempts, accum.HostileMultishotSuccesses, accum.HostileBasicAttempts, accum.HostileBasicSuccesses, n),
            ["per_unit_summary"] = perUnitSummary,
            ["runs"] = runDetails,
        };
    }

    private static GDictionary BuildFactionReport(
        int damageDone,
        int damageTaken,
        int chargeAttempts,
        int chargeSuccesses,
        int heavyAttempts,
        int heavySuccesses,
        int aimedAttempts,
        int aimedSuccesses,
        int multishotAttempts,
        int multishotSuccesses,
        int basicAttempts,
        int basicSuccesses,
        double n
    )
    {
        return new GDictionary
        {
            ["total_damage_done"] = damageDone,
            ["total_damage_taken"] = damageTaken,
            ["avg_damage_done_per_run"] = damageDone / n,
            ["avg_damage_taken_per_run"] = damageTaken / n,
            ["charge"] = BuildSkillReport(chargeAttempts, chargeSuccesses, 0, n),
            ["warrior_heavy_strike"] = BuildSkillReport(heavyAttempts, heavySuccesses, 0, n),
            ["archer_aimed_shot"] = BuildSkillReport(aimedAttempts, aimedSuccesses, 0, n),
            ["archer_multishot"] = BuildSkillReport(multishotAttempts, multishotSuccesses, 0, n),
            ["basic_attack"] = BuildSkillReport(basicAttempts, basicSuccesses, 0, n),
        };
    }

    private static GDictionary BuildSkillReport(int attempts, int successes, int mastery, double runCount)
    {
        return new GDictionary
        {
            ["attempts"] = attempts,
            ["successes"] = successes,
            ["mastery"] = mastery,
            ["avg_attempts_per_run"] = attempts / runCount,
            ["avg_mastery_per_run"] = mastery / runCount,
        };
    }

    private bool HasReachedTimeout(ulong startTimeMsec, int timeoutSeconds)
    {
        if (timeoutSeconds <= 0)
            return false;
        return (Time.GetTicksMsec() - startTimeMsec) / 1000.0 >= timeoutSeconds;
    }

    private void PrintProgress(string message)
    {
        if (_progressEnabled)
            GameLog.Info(message, "bench.summary", "bench");
    }

    private static string ResolveTraceSummaryPath(string outputPath)
    {
        if (OS.HasEnvironment("TRACE_SUMMARY_FILE"))
        {
            string explicitPath = OS.GetEnvironment("TRACE_SUMMARY_FILE").StripEdges();
            if (!string.IsNullOrEmpty(explicitPath))
                return explicitPath;
        }
        if (!string.IsNullOrEmpty(outputPath))
        {
            int dot = outputPath.LastIndexOf('.');
            return dot > 0
                ? outputPath.Substring(0, dot) + "_trace_summary.json"
                : outputPath + "_trace_summary.json";
        }
        return $"user://simulation_reports/mixed_6v12_trace_summary_{(long)Time.GetUnixTimeFromSystem()}.json";
    }

    private static bool WriteJsonFile(string path, GDictionary payload)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string absolutePath = path.StartsWith("res://") || path.StartsWith("user://")
            ? ProjectSettings.GlobalizePath(path)
            : path;
        string directory = absolutePath.GetBaseDir();
        if (!string.IsNullOrEmpty(directory))
            DirAccess.MakeDirRecursiveAbsolute(directory);
        using var file = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;
        file.StoreString(
            Json.Stringify(GdInterop.GetValueOrDefault(null, "", NormalizeValue(payload)), "\t")
        );
        return true;
    }

    private static object NormalizeValue(Variant rawValue)
    {
        if (rawValue.VariantType == Variant.Type.Dictionary) { var rawDictionary = rawValue.AsGodotDictionary();
            var normalized = new GDictionary();
            foreach (var key in rawDictionary.Keys)
                normalized[key.ToString()] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    NormalizeValue(rawDictionary[key])
                );
            return normalized;
        }
        if (rawValue.VariantType == Variant.Type.Array) { var rawArray = rawValue.AsGodotArray();
            var normalized = new GArray();
            foreach (var entry in rawArray)
                normalized.Add(GdInterop.GetValueOrDefault(null, "", NormalizeValue(entry)));
            return normalized;
        }

        if (rawValue.VariantType == Variant.Type.StringName)
            return rawValue.AsStringName().ToString();
        if (rawValue.VariantType == Variant.Type.Vector2I)
        {
            Vector2I v = rawValue.AsVector2I();
            return new GDictionary { ["x"] = v.X, ["y"] = v.Y };
        }
        if (rawValue.VariantType == Variant.Type.Array)
        {
            var normalized = new GArray();
            foreach (var entry in rawValue.AsGodotArray())
                normalized.Add(GdInterop.GetValueOrDefault(null, "", NormalizeValue(entry)));
            return normalized;
        }
        if (rawValue.VariantType == Variant.Type.Dictionary)
        {
            var normalized = new GDictionary();
            foreach (var key in rawValue.AsGodotDictionary().Keys)
                normalized[key.ToString()] = GdInterop.GetValueOrDefault(
                    null,
                    "",
                    NormalizeValue(rawValue.AsGodotDictionary()[key])
                );
            return normalized;
        }
        if (rawValue.VariantType == Variant.Type.Object)
        {
            GodotObject obj = rawValue.AsGodotObject();
            if (obj is BattleSimScenarioDef scenarioDef)
                return NormalizeValue(scenarioDef.to_dict());
            if (obj is BattleSimProfileDef profileDef)
                return NormalizeValue(profileDef.to_dict());
            if (obj is BattleUnitState unitState)
                return NormalizeValue(unitState.to_dict());
            return obj?.ToString() ?? "";
        }
        return rawValue;
    }

    private static bool ReadBoolEnvironment(string name, bool defaultValue)
    {
        if (!OS.HasEnvironment(name))
            return defaultValue;
        string value = OS.GetEnvironment(name).StripEdges().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "on";
    }

    private static int ReadIntEnvironment(string name, int defaultValue)
    {
        if (!OS.HasEnvironment(name))
            return defaultValue;
        string value = OS.GetEnvironment(name).StripEdges();
        return string.IsNullOrEmpty(value) ? defaultValue : int.Parse(value);
    }

    private static long ReadLongEnvironment(string name, long defaultValue)
    {
        if (!OS.HasEnvironment(name))
            return defaultValue;
        string value = OS.GetEnvironment(name).StripEdges();
        return string.IsNullOrEmpty(value) ? defaultValue : long.Parse(value);
    }

    private static float ReadFloatEnvironment(string name, float defaultValue)
    {
        if (!OS.HasEnvironment(name))
            return defaultValue;
        string value = OS.GetEnvironment(name).StripEdges();
        return string.IsNullOrEmpty(value) ? defaultValue : float.Parse(value);
    }

    private static string ReadStringEnvironment(string name, string defaultValue)
    {
        if (!OS.HasEnvironment(name))
            return defaultValue;
        string value = OS.GetEnvironment(name).StripEdges();
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    private static Godot.Collections.Array<long> ReadLongListEnvironment(string name)
    {
        var values = new Godot.Collections.Array<long>();
        if (!OS.HasEnvironment(name))
            return values;
        string rawValue = OS.GetEnvironment(name).StripEdges().Replace(";", ",");
        if (string.IsNullOrEmpty(rawValue))
            return values;
        foreach (string part in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                values.Add(long.Parse(trimmed));
        }
        return values;
    }

    private static GDictionary GetDict(GDictionary dictionary, object key)
    {
        return GdInterop.GetDictionary(dictionary, key);
    }

    private static GArray GetArray(GDictionary dictionary, object key)
    {
        return GdInterop.GetArray(dictionary, key);
    }

    private static GodotObject GetObject(GDictionary dictionary, object key)
    {
        return GdInterop.GetObject(dictionary, key);
    }

    private static int GetInt(GDictionary dictionary, object key, int fallback = 0)
    {
        return GdInterop.GetInt(dictionary, key, fallback);
    }

    private static long GetLong(GDictionary dictionary, object key, long fallback = 0L)
    {
        return GdInterop.TryGet(dictionary, key, out var value)
            && value.VariantType == Variant.Type.Int
            ? value.AsInt64()
            : fallback;
    }

    private static string GetString(GDictionary dictionary, object key, string fallback = "")
    {
        return GdInterop.HasString(dictionary, key)
            ? GdInterop.GetString(dictionary, key)
            : fallback;
    }

    private static bool GetBool(GDictionary dictionary, object key, bool fallback = false)
    {
        return GdInterop.GetBool(dictionary, key, fallback);
    }

    private sealed class BatchAccumulator
    {
        public int TotalChargeAttempts;
        public int TotalChargeSuccesses;
        public int TotalHeavyAttempts;
        public int TotalHeavySuccesses;
        public int TotalAimedAttempts;
        public int TotalAimedSuccesses;
        public int TotalMultishotAttempts;
        public int TotalMultishotSuccesses;
        public int TotalBasicAttempts;
        public int TotalBasicSuccesses;
        public int TotalChargeMastery;
        public int TotalHeavyMastery;
        public int TotalAimedMastery;
        public int TotalMultishotMastery;
        public int TotalBasicMastery;
        public int PlayerChargeAttempts;
        public int PlayerChargeSuccesses;
        public int PlayerHeavyAttempts;
        public int PlayerHeavySuccesses;
        public int PlayerAimedAttempts;
        public int PlayerAimedSuccesses;
        public int PlayerMultishotAttempts;
        public int PlayerMultishotSuccesses;
        public int PlayerBasicAttempts;
        public int PlayerBasicSuccesses;
        public int PlayerDamageDone;
        public int PlayerDamageTaken;
        public int HostileChargeAttempts;
        public int HostileChargeSuccesses;
        public int HostileHeavyAttempts;
        public int HostileHeavySuccesses;
        public int HostileAimedAttempts;
        public int HostileAimedSuccesses;
        public int HostileMultishotAttempts;
        public int HostileMultishotSuccesses;
        public int HostileBasicAttempts;
        public int HostileBasicSuccesses;
        public int HostileDamageDone;
        public int HostileDamageTaken;
        public int EndedCount;
        public int TotalIterations;
        public int TotalTimelineSteps;
        public int TotalWinsPlayer;
        public int TotalWinsHostile;
        public int TotalDraws;

        public void AbsorbRun(GDictionary result, GDictionary factions, BattleSimFormalCombatFixture fixture)
        {
            int chargeAttempts = 0;
            int chargeSuccesses = 0;
            int heavyAttempts = 0;
            int heavySuccesses = 0;
            int aimedAttempts = 0;
            int aimedSuccesses = 0;
            int multishotAttempts = 0;
            int multishotSuccesses = 0;
            int basicAttempts = 0;
            int basicSuccesses = 0;

            foreach (var factionKey in factions.Keys)
            {
                GDictionary factionData = GetDict(factions, factionKey);
                GDictionary skillAttempts = GetDict(factionData, "skill_attempt_counts");
                GDictionary skillSuccesses = GetDict(factionData, "skill_success_counts");
                int facChargeA = GetInt(skillAttempts, "charge");
                int facChargeS = GetInt(skillSuccesses, "charge");
                int facHeavyA = GetInt(skillAttempts, "warrior_heavy_strike");
                int facHeavyS = GetInt(skillSuccesses, "warrior_heavy_strike");
                int facAimedA = GetInt(skillAttempts, "archer_aimed_shot");
                int facAimedS = GetInt(skillSuccesses, "archer_aimed_shot");
                int facMultiA = GetInt(skillAttempts, "archer_multishot");
                int facMultiS = GetInt(skillSuccesses, "archer_multishot");
                int facBasicA = GetInt(skillAttempts, "basic_attack");
                int facBasicS = GetInt(skillSuccesses, "basic_attack");
                int facDamageDone = GetInt(factionData, "total_damage_done");
                int facDamageTaken = GetInt(factionData, "total_damage_taken");

                chargeAttempts += facChargeA;
                chargeSuccesses += facChargeS;
                heavyAttempts += facHeavyA;
                heavySuccesses += facHeavyS;
                aimedAttempts += facAimedA;
                aimedSuccesses += facAimedS;
                multishotAttempts += facMultiA;
                multishotSuccesses += facMultiS;
                basicAttempts += facBasicA;
                basicSuccesses += facBasicS;

                if (factionKey.ToString() == "player")
                {
                    PlayerChargeAttempts += facChargeA;
                    PlayerChargeSuccesses += facChargeS;
                    PlayerHeavyAttempts += facHeavyA;
                    PlayerHeavySuccesses += facHeavyS;
                    PlayerAimedAttempts += facAimedA;
                    PlayerAimedSuccesses += facAimedS;
                    PlayerMultishotAttempts += facMultiA;
                    PlayerMultishotSuccesses += facMultiS;
                    PlayerBasicAttempts += facBasicA;
                    PlayerBasicSuccesses += facBasicS;
                    PlayerDamageDone += facDamageDone;
                    PlayerDamageTaken += facDamageTaken;
                }
                else
                {
                    HostileChargeAttempts += facChargeA;
                    HostileChargeSuccesses += facChargeS;
                    HostileHeavyAttempts += facHeavyA;
                    HostileHeavySuccesses += facHeavyS;
                    HostileAimedAttempts += facAimedA;
                    HostileAimedSuccesses += facAimedS;
                    HostileMultishotAttempts += facMultiA;
                    HostileMultishotSuccesses += facMultiS;
                    HostileBasicAttempts += facBasicA;
                    HostileBasicSuccesses += facBasicS;
                    HostileDamageDone += facDamageDone;
                    HostileDamageTaken += facDamageTaken;
                }
            }

            TotalChargeAttempts += chargeAttempts;
            TotalChargeSuccesses += chargeSuccesses;
            TotalHeavyAttempts += heavyAttempts;
            TotalHeavySuccesses += heavySuccesses;
            TotalAimedAttempts += aimedAttempts;
            TotalAimedSuccesses += aimedSuccesses;
            TotalMultishotAttempts += multishotAttempts;
            TotalMultishotSuccesses += multishotSuccesses;
            TotalBasicAttempts += basicAttempts;
            TotalBasicSuccesses += basicSuccesses;
            TotalChargeMastery += fixture.charge_mastery;
            TotalHeavyMastery += fixture.heavy_mastery;
            TotalAimedMastery += fixture.aimed_mastery;
            TotalMultishotMastery += fixture.multishot_mastery;
            TotalBasicMastery += fixture.basic_mastery;

            if (GetBool(result, "battle_ended"))
            {
                EndedCount++;
                string winner = GetString(result, "winner_faction_id");
                if (winner == "player")
                    TotalWinsPlayer++;
                else if (winner == "hostile")
                    TotalWinsHostile++;
                else
                    TotalDraws++;
            }
            TotalIterations += GetInt(result, "iterations");
            TotalTimelineSteps += GetInt(result, "timeline_steps");
        }
    }
}
