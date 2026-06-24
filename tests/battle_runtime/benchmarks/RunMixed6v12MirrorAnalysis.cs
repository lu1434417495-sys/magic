using System;
using System.Collections.Generic;
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
            : TrueRandomSeedService.GenerateSeed();
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
        bool aiProfileEnabled = ReadBoolEnvironment("AI_PROFILE", false);
        var aiProfiler = aiProfileEnabled ? new AiProfileCapture() : null;
        if (aiProfiler != null)
        {
            aiProfiler.Setup(
                scenarioId: "mixed_6v12",
                outputDir: ReadStringEnvironment(
                    "AI_PROFILE_OUTPUT_DIR",
                    "user://simulation_reports/ai_profiles/"
                ),
                topN: ReadIntEnvironment("AI_PROFILE_TOP_N", 30),
                sortBy: ReadStringEnvironment("AI_PROFILE_SORT", "self_usec"),
                nameFilter: ReadStringEnvironment("AI_PROFILE_FILTER", ""),
                dumpTraceJson: ReadBoolEnvironment("AI_PROFILE_TRACE_JSON", false),
                gitCommit: AiProfileCapture.ResolveGitCommit(),
                filePrefix: "ai_profile"
            );
        }

        // Opt-in arena override: SCENARIO_FILE lets a 6v12 *variant* (e.g. the two-archer
        // roster) reuse this runner. Unset = the immutable mixed_6v12_mirror_simulation.
        string scenarioPath = OS.HasEnvironment("SCENARIO_FILE")
            ? OS.GetEnvironment("SCENARIO_FILE").StripEdges()
            : ScenarioPath;
        if (string.IsNullOrEmpty(scenarioPath))
            scenarioPath = ScenarioPath;
        var scenario = ResourceLoader.Load<BattleSimScenarioDef>(scenarioPath);
        if (scenario == null)
        {
            GameLog.Error($"Failed to load scenario: {scenarioPath}", "bench.scenario.load_failed", "bench");
            return 1;
        }

        var contentProvider = new BattleSimContentProvider();
        var overrideApplier = new BattleSimOverrideApplier();
        var terrainGenerator = new BattleTerrainGenerator();
        var progressionRegistry = new ProgressionContentRegistry();
        var itemRegistry = new ItemContentRegistry();

        IReadOnlyDictionary<StringName, SkillDef> skillDefs = contentProvider.GetSkillDefsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            contentProvider.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains =
            contentProvider.GetEnemyAiBrainsTyped();
        if (skillDefs.Count == 0 || enemyAiBrains.Count == 0)
        {
            GameLog.Error($"Battle sim content provider returned empty content: skills={skillDefs.Count}, brains={enemyAiBrains.Count}.", "bench.content.empty", "bench");
            DisposeObjects(scenario, itemRegistry, progressionRegistry, terrainGenerator, overrideApplier, contentProvider);
            return 1;
        }

        // Opt-in tuning hook: when AI_PROFILE_OVERRIDE_FILE points to a BattleSimProfileDef,
        // its override_patches (incl. faction_ai_score_profile) are applied. Unset = the
        // immutable empty baseline, so the standard 6v12 matchup is unchanged.
        var baseline = LoadOverrideProfile() ?? new BattleSimProfileDef
        {
            profile_id = "baseline",
            display_name = "Baseline",
        };
        var traceSummaryReport = new BattleSimScenarioReport
        {
            ScenarioDef = scenario,
            GeneratedAtUnix = (int)Time.GetUnixTimeFromSystem(),
        };
        traceSummaryReport.ProfileEntries.Add(
            new BattleSimProfileReportEntry
            {
                Profile = baseline,
                Summary = new BattleSimProfileSummary(),
            }
        );
        BattleSimOverrideApplyResult overrides = overrideApplier.ApplyProfileTyped(
            skillDefs,
            enemyAiBrains,
            baseline
        );
        BattleSimFormalRosterOptionsData rosterOptions = BuildRosterOptionsFromEnvironment();

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
        PrintProgress($"[Progress] ai_profile={aiProfileEnabled}");

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
                seed
            );
            GDictionary result;
            try
            {
                result = RunSingleSimulation(
                    scenario,
                    overrides,
                    enemyTemplates,
                    terrainGenerator,
                    fixture,
                    seed,
                    traceAi,
                    aiMutationGuardEnabled,
                    validateSpawnReachability,
                    validateBidirectionalSpawnReachability,
                    aiProfiler
                );

                GDictionary metrics = GetDict(result, "metrics");
                GDictionary factions = GetDict(metrics, "factions");
                GDictionary units = GetDict(result, "units");
                MergePerUnitSummary(perUnitSummary, units);
                runDetails.Add(BuildRunDetail(runIndex, seed, result, factions, units, traceAi));
                accum.AbsorbRun(result, factions, fixture);
                traceSummaryReport.ProfileEntries[0].Runs.Add(
                    BuildTraceSummaryRun(seed, result, traceAi)
                );
                completedRunCount++;
            }
            finally
            {
                fixture.Dispose();
            }

            double elapsed = (Time.GetTicksMsec() - batchStartMsec) / 1000.0;
            double runElapsed = (Time.GetTicksMsec() - runStartMsec) / 1000.0;
            PrintProgress(
                $"[Progress] run {runIndex + 1}/{requestedRunCount} done winner={GetString(result, "winner_faction_id")} ended={ReadExactBool(result, "battle_ended")} iterations={GetInt(result, "iterations")} timeline_steps={GetInt(result, "timeline_steps")} run_elapsed={runElapsed:F1}s batch_elapsed={elapsed:F1}s rate={(runIndex + 1) / Math.Max(elapsed, 0.001):F2} runs/s"
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
        if (aiProfiler != null)
        {
            GDictionary profileReport = aiProfiler.WriteReports();
            report["ai_profile"] = profileReport;
            PrintProgress(
                $"[Progress] wrote AI profile {GetString(profileReport, "hotspots_path")}"
            );
        }
        traceSummaryReport.ProfileEntries[0].Summary = new BattleSimProfileSummary
        {
            AverageIterations = completedRunCount > 0 ? (float)(accum.TotalIterations / n) : 0.0f,
        };

        if (string.IsNullOrEmpty(outputPath))
        {
            GameLog.Info(
                Json.Stringify(ToVariant(NormalizeValue(report)), "\t"),
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
            var compactReport = traceSummaryBuilder.Build(traceSummaryReport, outputPath);
            if (!WriteJsonFile(traceSummaryPath, compactReport))
                GameLog.Error($"[ERROR] Failed to write trace summary: {traceSummaryPath}.", "bench.trace_write_failed", "bench");
            else
                PrintProgress($"[Progress] wrote trace summary {traceSummaryPath}");
        }

        DisposeObjects(
            baseline,
            scenario,
            itemRegistry,
            progressionRegistry,
            terrainGenerator,
            overrideApplier,
            contentProvider
        );
        return 0;
    }

    private static void DisposeObjects(params object[] objects)
    {
        foreach (object obj in objects)
        {
            if (obj is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private static BattleSimFormalCombatFixture BuildFormalFixture(
        BattleSimScenarioDef scenario,
        BattleSimOverrideApplyResult overrides,
        ProgressionContentRegistry progressionRegistry,
        ItemContentRegistry itemRegistry,
        BattleSimFormalRosterOptionsData rosterOptions,
        long attributeRollSeed
    )
    {
        var fixture = new BattleSimFormalCombatFixture();
        fixture.SetupContent(
            progressionRegistry,
            itemRegistry,
            overrides.SkillDefs
        );
        BattleSimFormalRosterOptionsData effectiveRosterOptions = new()
        {
            MainCharacterMemberId = rosterOptions?.MainCharacterMemberId ?? "",
            LeaderMemberId = rosterOptions?.LeaderMemberId ?? "",
            MainCharacterRerollCount = rosterOptions?.MainCharacterRerollCount ?? 0,
            AttributeRollSeed = rosterOptions?.AttributeRollSeed ?? attributeRollSeed,
        };
        if (effectiveRosterOptions.AttributeRollSeed == 0)
        {
            effectiveRosterOptions = new BattleSimFormalRosterOptionsData
            {
                MainCharacterMemberId = effectiveRosterOptions.MainCharacterMemberId,
                LeaderMemberId = effectiveRosterOptions.LeaderMemberId,
                MainCharacterRerollCount = effectiveRosterOptions.MainCharacterRerollCount,
                AttributeRollSeed = attributeRollSeed,
            };
        }
        if (
            !fixture.BuildRoster(scenario.scenario_id, effectiveRosterOptions)
        )
            GameLog.Error($"Unsupported formal battle sim roster: {scenario.scenario_id}", "bench.roster.unsupported", "bench");
        return fixture;
    }

    private GDictionary RunSingleSimulation(
        BattleSimScenarioDef scenario,
        BattleSimOverrideApplyResult overrides,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates,
        BattleTerrainGenerator terrainGenerator,
        BattleSimFormalCombatFixture fixture,
        long seed,
        bool traceAi,
        bool aiMutationGuardEnabled,
        bool validateSpawnReachability,
        bool validateBidirectionalSpawnReachability,
        AiProfileCapture aiProfiler = null
    )
    {
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        EncounterAnchorData encounterAnchor = null;
        AiTraceRecorder aiProfileRecorder = null;
        bool aiProfileRecorderEnded = false;
        try
        {
            bool useFormalTerrain = scenario != null && scenario.use_formal_terrain_generation;
            PrintProgress($"[Progress] run seed={seed} runtime setup start");
            runtime.setup(
                fixture,
                overrides.SkillDefs,
                enemyTemplates,
                overrides.EnemyAiBrains,
                null,
                default,
                fixture.GetItemDefsTyped(),
                useFormalTerrain ? null : terrainGenerator,
                default,
                new GDictionary()
            );
            PrintProgress($"[Progress] run seed={seed} runtime setup done");
            runtime.SetAiTraceEnabled(traceAi);
            runtime._ai_service.EnableMutationGuard = aiMutationGuardEnabled;
            runtime.SetAiScoreProfile(overrides.AiScoreProfile);
            runtime.SetFactionAiScoreProfiles(overrides.FactionAiScoreProfiles);

            encounterAnchor = new EncounterAnchorData
            {
                entity_id = scenario != null && scenario.scenario_id != "" ? scenario.scenario_id : "battle_sim",
                display_name = scenario != null && !string.IsNullOrEmpty(scenario.display_name) ? scenario.display_name : scenario?.scenario_id.ToString() ?? "battle_sim",
                faction_id = "hostile",
                world_coord = Vector2I.Zero,
                region_tag = "simulation",
            };

            GDictionary context = fixture.BuildRuntimeContext(runtime, scenario.BuildStartContext());
            context["validate_spawn_reachability"] = validateSpawnReachability;
            context["validate_bidirectional_spawn_reachability"] = validateBidirectionalSpawnReachability;
            PrintProgress($"[Progress] run seed={seed} start_battle start");
            state = runtime.StartBattle(encounterAnchor, seed, context);
            PrintProgress($"[Progress] run seed={seed} start_battle done phase={state?.phase}");
            BattleStartFailureSnapshot startFailure = runtime.GetLastStartFailureSnapshot();
            fixture.ApplyStartedBattleMetadata(state);

            PrintProgress($"[Progress] run seed={seed} execution_loop start");
            aiProfileRecorder = aiProfiler?.BeginRun();
            var loopResult = new BattleSimExecutionLoop().Run(runtime, state, scenario, MaxIdleLoops);
            PrintProgress($"[Progress] run seed={seed} execution_loop done");

            GDictionary rawMetrics = BattleMetricsProjection.Project(runtime.GetBattleMetricsTyped());
            GDictionary profileSummary = new();
            if (aiProfileRecorder != null && aiProfiler != null)
            {
                profileSummary = aiProfiler.EndRun(aiProfileRecorder, CountAiTurns(rawMetrics));
                aiProfileRecorderEnded = true;
            }
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
            if (profileSummary.Count > 0)
                result["ai_profile"] = profileSummary;
            if (startFailure != null && !startFailure.IsEmpty)
                result["start_failure"] = BattleSpawnReachabilityProjection.Project(startFailure);
            if (traceAi)
            {
                GArray rawTraces = (GArray)runtime.GetAiTurnTraces().Duplicate(true);
                result["ai_turn_traces"] = NormalizeValue(rawTraces) is GArray normalizedTraces
                    ? normalizedTraces
                    : new GArray();
            }
            return result;
        }
        finally
        {
            if (aiProfileRecorder != null && aiProfiler != null && !aiProfileRecorderEnded)
                aiProfiler.EndRun(aiProfileRecorder, 0);
            runtime.dispose();
            state?.Dispose();
        }
    }

    private static int CountAiTurns(GDictionary metrics)
    {
        int aiTurns = 0;
        GDictionary units = GetDict(metrics, "units");
        foreach (Variant unitKey in units.Keys)
        {
            GDictionary unit = GetDict(units, unitKey);
            if (GetString(unit, "control_mode") == "manual")
                continue;
            aiTurns += Math.Max(GetInt(unit, "turn_count"), 0);
        }
        return aiTurns;
    }

    private static BattleSimFormalRosterOptionsData BuildRosterOptionsFromEnvironment()
    {
        StringName mainCharacterMemberId = "";
        StringName leaderMemberId = "";
        int mainCharacterRerollCount = 0;
        long attributeRollSeed = 0;
        if (OS.HasEnvironment("MAIN_CHARACTER_MEMBER_ID"))
        {
            string memberId = OS.GetEnvironment("MAIN_CHARACTER_MEMBER_ID").StripEdges();
            if (!string.IsNullOrEmpty(memberId))
                mainCharacterMemberId = new StringName(memberId);
        }
        if (OS.HasEnvironment("LEADER_MEMBER_ID"))
        {
            string leaderId = OS.GetEnvironment("LEADER_MEMBER_ID").StripEdges();
            if (!string.IsNullOrEmpty(leaderId))
                leaderMemberId = new StringName(leaderId);
        }
        if (OS.HasEnvironment("MAIN_CHARACTER_REROLL_COUNT"))
            mainCharacterRerollCount = ReadIntEnvironment("MAIN_CHARACTER_REROLL_COUNT", 0);
        if (OS.HasEnvironment("ATTRIBUTE_ROLL_SEED"))
            attributeRollSeed = ReadLongEnvironment("ATTRIBUTE_ROLL_SEED", 0);
        return new BattleSimFormalRosterOptionsData
        {
            MainCharacterMemberId = mainCharacterMemberId,
            LeaderMemberId = leaderMemberId,
            MainCharacterRerollCount = mainCharacterRerollCount,
            AttributeRollSeed = attributeRollSeed,
        };
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

    private static BattleSimRunReport BuildTraceSummaryRun(long seed, GDictionary result, bool traceAi)
    {
        return new BattleSimRunReport
        {
            Seed = seed,
            BattleEnded = ReadExactBool(result, "battle_ended"),
            WinnerFactionId = GetString(result, "winner_faction_id"),
            Iterations = GetInt(result, "iterations"),
            TimelineSteps = GetInt(result, "timeline_steps"),
            Metrics = GetDict(result, "metrics"),
            AiTurnTraces = traceAi
                ? BuildTypedTraceSummaries(GetArray(result, "ai_turn_traces"))
                : System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
    }

    private static IReadOnlyList<BattleAiTurnTraceProjection> BuildTypedTraceSummaries(GArray traces)
    {
        var result = new List<BattleAiTurnTraceProjection>();
        foreach (Variant traceValue in traces ?? new GArray())
        {
            GDictionary trace = traceValue.VariantType == Variant.Type.Dictionary
                ? traceValue.AsGodotDictionary()
                : new GDictionary();
            if (trace.Count == 0)
                continue;
            result.Add(
                new BattleAiTurnTraceProjection
                {
                    TurnStartedTu = GetInt(trace, "turn_started_tu"),
                    UnitId = GetString(trace, "unit_id"),
                    UnitName = GetString(trace, "unit_name"),
                    FactionId = GetString(trace, "faction_id"),
                    BrainId = GetString(trace, "brain_id"),
                    StateId = GetString(trace, "state_id"),
                    ActionId = GetString(trace, "action_id"),
                    ReasonText = GetString(trace, "reason_text"),
                    ScoreInput = BuildTraceScoreInput(GetDict(trace, "score_input")),
                }
            );
        }
        return result;
    }

    private static BattleAiScoreInput BuildTraceScoreInput(GDictionary score)
    {
        return new BattleAiScoreInput
        {
            score_bucket_id = GetString(score, "score_bucket_id"),
            target_count = GetInt(score, "target_count"),
            total_score = GetInt(score, "total_score"),
        };
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
            ["scenario"] = BattleSimReportProjection.Project(scenario),
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

    private static BattleSimProfileDef LoadOverrideProfile()
    {
        if (!OS.HasEnvironment("AI_PROFILE_OVERRIDE_FILE"))
            return null;
        string path = OS.GetEnvironment("AI_PROFILE_OVERRIDE_FILE").StripEdges();
        if (string.IsNullOrEmpty(path))
            return null;
        var profile = ResourceLoader.Load<BattleSimProfileDef>(path);
        if (profile == null)
            GameLog.Error(
                $"AI_PROFILE_OVERRIDE_FILE could not be loaded: {path}",
                "bench.override.load_failed",
                "bench"
            );
        return profile;
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
            Json.Stringify(ToVariant(NormalizeValue(payload)), "\t")
        );
        return true;
    }

    private static object NormalizeValue(object rawValue)
    {
        if (rawValue is Variant rawVariant)
            return NormalizeVariant(rawVariant);
        if (rawValue is GDictionary rawDictionary)
        {
            var normalized = new GDictionary();
            foreach (var key in rawDictionary.Keys)
                normalized[key.ToString()] = ToVariant(NormalizeValue(rawDictionary[key]));
            return normalized;
        }
        if (rawValue is GArray rawArray)
        {
            var normalized = new GArray();
            foreach (var entry in rawArray)
                normalized.Add(ToVariant(NormalizeValue(entry)));
            return normalized;
        }
        if (rawValue is GodotObject obj)
            return NormalizeGodotObject(obj);
        return rawValue;
    }

    private static object NormalizeVariant(Variant value)
    {
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName().ToString();
        if (value.VariantType == Variant.Type.Vector2I)
        {
            Vector2I v = value.AsVector2I();
            return new GDictionary { ["x"] = v.X, ["y"] = v.Y };
        }
        if (value.VariantType == Variant.Type.Array)
        {
            var normalized = new GArray();
            foreach (var entry in value.AsGodotArray())
                normalized.Add(ToVariant(NormalizeValue(entry)));
            return normalized;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            var normalized = new GDictionary();
            GDictionary dictionary = value.AsGodotDictionary();
            foreach (var key in dictionary.Keys)
                normalized[key.ToString()] = ToVariant(NormalizeValue(dictionary[key]));
            return normalized;
        }
        if (value.VariantType == Variant.Type.Object)
            return NormalizeGodotObject(value.AsGodotObject());
        return value;
    }

    private static object NormalizeGodotObject(GodotObject obj)
    {
        if (obj is BattleSimScenarioDef scenarioDef)
            return NormalizeValue(BattleSimReportProjection.Project(scenarioDef));
        if (obj is BattleSimProfileDef profileDef)
            return NormalizeValue(BattleSimReportProjection.Project(profileDef));
        if (obj is BattleUnitState unitState)
            return NormalizeValue(BattleSimReportProjection.Project(unitState));
        return obj?.ToString() ?? "";
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
            GArray array => array,
            GDictionary dictionary => dictionary,
            GodotObject godotObject => godotObject,
            _ => default,
        };

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
        return TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary dictionary, object key)
    {
        return TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static int GetInt(GDictionary dictionary, object key, int fallback = 0)
    {
        return TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : fallback;
    }

    private static long GetLong(GDictionary dictionary, object key, long fallback = 0L)
    {
        return TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt64()
            : fallback;
    }

    private static string GetString(GDictionary dictionary, object key, string fallback = "")
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static bool ReadExactBool(GDictionary dictionary, object key, bool fallback = false)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static bool TryRead(GDictionary dictionary, object key, out Variant value)
    {
        value = default;
        if (dictionary == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        return false;
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

            if (ReadExactBool(result, "battle_ended"))
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
