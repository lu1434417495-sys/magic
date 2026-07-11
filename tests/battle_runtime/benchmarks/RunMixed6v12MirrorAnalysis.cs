using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class RunMixed6v12MirrorAnalysis : LifecycleTestSceneTree
{
    private const int MaxIdleLoops = 25;
    private const int DefaultSimulationTimeoutSeconds = 30 * 60;
    private const string ScenarioPath = "res://data/configs/battle_sim/scenarios/mixed_6v12_mirror_simulation.tres";

    private readonly TestHarness _test = new();
    private bool _progressEnabled = true;

    public override void _Initialize()
    {
        int exitCode = Run();
        RequestTestExit(_test.Finish("Mixed 6v12 mirror analysis", exitCode));
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

        var contentLoader = new TestContentResourceLoader();
        var contentProvider = new BattleSimContentProvider(contentLoader);
        var overrideApplier = new BattleSimOverrideApplier();
        var terrainGenerator = new BattleTerrainGenerator();
        var progressionRegistry = new ProgressionContentRegistry(contentLoader);
        var itemRegistry = new ItemContentRegistry(contentLoader);

        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            contentProvider.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates =
            contentProvider.GetEnemyTemplatesTyped();
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains =
            contentProvider.GetEnemyAiBrainsTyped();
        if (skillDefinitions.Count == 0 || enemyAiBrains.Count == 0)
        {
            GameLog.Error($"Battle sim content provider returned empty content: skills={skillDefinitions.Count}, brains={enemyAiBrains.Count}.", "bench.content.empty", "bench");
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
            skillDefinitions,
            enemyAiBrains,
            baseline
        );
        BattleSimFormalRosterOptionsData rosterOptions = BuildRosterOptionsFromEnvironment();

        var rng = new RuntimeRandom(Math.Max(startSeed, 1L));
        var accum = new BatchAccumulator();
        var perUnitSummary = new Dictionary<string, PerUnitAggregate>(StringComparer.Ordinal);
        var runDetails = new List<object>();

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
            MixedSimulationRunResult result;
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

                MergePerUnitSummary(perUnitSummary, result.Metrics.Units);
                runDetails.Add(BuildRunDetail(runIndex, seed, result, traceAi));
                accum.AbsorbRun(result, fixture);
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
                $"[Progress] run {runIndex + 1}/{requestedRunCount} done winner={result.WinnerFactionId} ended={result.BattleEnded} iterations={result.Iterations} timeline_steps={result.TimelineSteps} run_elapsed={runElapsed:F1}s batch_elapsed={elapsed:F1}s rate={(runIndex + 1) / Math.Max(elapsed, 0.001):F2} runs/s"
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
            Dictionary<string, object> plainProfileReport =
                TraceDictionaryProjection.FromDictionary(profileReport);
            report["ai_profile"] = plainProfileReport;
            PrintProgress(
                $"[Progress] wrote AI profile {GetPlainString(plainProfileReport, "hotspots_path")}"
            );
        }
        traceSummaryReport.ProfileEntries[0].Summary = new BattleSimProfileSummary
        {
            AverageIterations = completedRunCount > 0 ? (float)(accum.TotalIterations / n) : 0.0f,
        };

        if (string.IsNullOrEmpty(outputPath))
        {
            using GodotProjectionLease<GDictionary> reportLease =
                TraceDictionaryProjection.BuildJsonSafeLease(
                    report,
                    "mixed-mirror-analysis-report",
                    LifetimeDomain.Request,
                    "RunMixed6v12MirrorAnalysis.stdout"
                );
            GameLog.Info(Json.Stringify(reportLease.Value, "\t"), "bench.report", "bench");
        }
        else if (!WritePlainJsonFile(outputPath, report))
        {
            GameLog.Error($"[ERROR] Failed to write: {outputPath}.", "bench.output_write_failed", "bench");
        }
        else
        {
            PrintProgress($"[Progress] wrote report {outputPath}");
        }

        if (traceAi)
        {
            string traceSummaryPath = GetPlainString(report, "trace_summary_file");
            var traceSummaryBuilder = new BattleSimTraceSummaryBuilder();
            using GodotProjectionLease<GDictionary> compactReportLease =
                traceSummaryBuilder.BuildFileLease(traceSummaryReport, outputPath);
            if (!WriteLeasedJsonFile(traceSummaryPath, compactReportLease.Value))
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
            overrides.SkillDefinitions
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

    private MixedSimulationRunResult RunSingleSimulation(
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
        AiProfileCapture aiProfiler
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
                overrides.SkillDefinitions,
                enemyTemplates,
                overrides.EnemyAiBrains,
                null,
                default,
                fixture.GetItemDefsTyped(),
                useFormalTerrain ? null : terrainGenerator,
                default
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

            BattleMetricsState metricsState = runtime.GetBattleMetricsTyped();
            BattleSimMetricsSnapshot metricsSnapshot =
                BattleSimMetricsSnapshot.Capture(metricsState);
            var profileSummary = new Dictionary<string, object>(StringComparer.Ordinal);
            if (aiProfileRecorder != null && aiProfiler != null)
            {
                GDictionary rawProfileSummary = aiProfiler.EndRun(
                    aiProfileRecorder,
                    CountAiTurns(metricsSnapshot)
                );
                profileSummary = TraceDictionaryProjection.FromDictionary(rawProfileSummary);
                aiProfileRecorderEnded = true;
            }
            var result = new MixedSimulationRunResult
            {
                BattleEnded = state != null && state.phase == "battle_ended",
                WinnerFactionId = state != null ? state.winner_faction_id.ToString() : "",
                Iterations = loopResult.iterations,
                TimelineSteps = loopResult.timeline_steps,
                Metrics = metricsSnapshot,
                AiProfile = profileSummary,
            };
            if (startFailure != null && !startFailure.IsEmpty)
                result.StartFailure = BuildStartFailurePlain(startFailure);
            if (traceAi)
                result.AiTurnTraces = new List<BattleAiTurnTraceProjection>(
                    runtime.GetAiTurnTracesTyped()
                );
            return result;
        }
        finally
        {
            if (aiProfileRecorder != null && aiProfiler != null && !aiProfileRecorderEnded)
                aiProfiler.EndRun(aiProfileRecorder, 0);
            runtime.dispose();
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private static int CountAiTurns(BattleSimMetricsSnapshot metrics)
    {
        int aiTurns = 0;
        if (metrics == null)
            return aiTurns;
        foreach (BattleSimUnitMetricsSnapshot unit in metrics.Units.Values)
        {
            if (unit == null || unit.ControlMode == "manual")
                continue;
            aiTurns += Math.Max(unit.TurnCount, 0);
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

    private static Dictionary<string, object> BuildRunDetail(
        int runIndex,
        long seed,
        MixedSimulationRunResult result,
        bool traceAi
    )
    {
        var runFactions = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry
            in result.Metrics.Factions
        )
        {
            BattleSimUnitMetricsSnapshot faction = entry.Value;
            if (faction == null)
                continue;
            runFactions[entry.Key] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["total_damage_done"] = faction.TotalDamageDone,
                ["total_damage_taken"] = faction.TotalDamageTaken,
                ["kill_count"] = faction.KillCount,
                ["death_count"] = faction.DeathCount,
                ["turn_count"] = faction.TurnCount,
            };
        }

        var runUnits = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry
            in result.Metrics.Units
        )
        {
            BattleSimUnitMetricsSnapshot unit = entry.Value;
            if (unit == null)
                continue;
            runUnits[entry.Key] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["display_name"] = unit.DisplayName,
                ["faction_id"] = unit.FactionId,
                ["turn_count"] = unit.TurnCount,
                ["total_damage_done"] = unit.TotalDamageDone,
                ["total_damage_taken"] = unit.TotalDamageTaken,
                ["kill_count"] = unit.KillCount,
                ["death_count"] = unit.DeathCount,
                ["skill_attempts"] = unit.SkillAttemptCounts,
                ["skill_successes"] = unit.SkillSuccessCounts,
            };
        }

        var detail = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["run_index"] = runIndex,
            ["seed"] = seed,
            ["winner_faction_id"] = result.WinnerFactionId,
            ["iterations"] = result.Iterations,
            ["timeline_steps"] = result.TimelineSteps,
            ["factions"] = runFactions,
            ["units"] = runUnits,
        };
        if (result.StartFailure.Count > 0)
            detail["start_failure"] = result.StartFailure;
        if (traceAi)
        {
            var traces = new List<object>();
            foreach (BattleAiTurnTraceProjection trace in result.AiTurnTraces)
                traces.Add(BattleAiTurnTracePayloadProjection.BuildPlain(trace));
            detail["ai_turn_traces"] = traces;
        }
        return detail;
    }

    private static BattleSimRunReport BuildTraceSummaryRun(
        long seed,
        MixedSimulationRunResult result,
        bool traceAi
    )
    {
        return new BattleSimRunReport
        {
            Seed = seed,
            BattleEnded = result.BattleEnded,
            WinnerFactionId = result.WinnerFactionId,
            Iterations = result.Iterations,
            TimelineSteps = result.TimelineSteps,
            MetricsSnapshot = result.Metrics,
            AiTurnTraces = traceAi
                ? BuildLegacyTraceSummaryViews(result.AiTurnTraces)
                : System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
    }

    internal static IReadOnlyList<BattleAiTurnTraceProjection> BuildLegacyTraceSummaryViews(
        IReadOnlyList<BattleAiTurnTraceProjection> traces
    )
    {
        var result = new List<BattleAiTurnTraceProjection>();
        foreach (
            BattleAiTurnTraceProjection trace
            in traces ?? System.Array.Empty<BattleAiTurnTraceProjection>()
        )
        {
            if (trace == null)
                continue;
            BattleAiScoreInput sourceScore = trace.ScoreInput;
            result.Add(
                new BattleAiTurnTraceProjection
                {
                    TurnStartedTu = trace.TurnStartedTu,
                    UnitId = trace.UnitId,
                    UnitName = trace.UnitName,
                    FactionId = trace.FactionId,
                    BrainId = trace.BrainId,
                    StateId = trace.StateId,
                    ActionId = trace.ActionId,
                    ReasonText = trace.ReasonText,
                    ScoreInput = new BattleAiScoreInput
                    {
                        score_bucket_id = sourceScore?.score_bucket_id ?? "",
                        target_count = sourceScore?.target_count ?? 0,
                        total_score = sourceScore?.total_score ?? 0,
                    },
                }
            );
        }
        return result;
    }

    private static void MergePerUnitSummary(
        Dictionary<string, PerUnitAggregate> perUnitSummary,
        IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> units
    )
    {
        if (units == null)
            return;
        foreach (KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry in units)
        {
            BattleSimUnitMetricsSnapshot unit = entry.Value;
            if (unit == null)
                continue;
            if (!perUnitSummary.TryGetValue(entry.Key, out PerUnitAggregate summary))
            {
                summary = new PerUnitAggregate(unit.DisplayName, unit.FactionId);
                perUnitSummary[entry.Key] = summary;
            }
            summary.Absorb(unit);
        }
    }

    private static Dictionary<string, object> BuildReport(
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
        IReadOnlyDictionary<string, PerUnitAggregate> perUnitSummary,
        IReadOnlyList<object> runDetails,
        double n
    )
    {
        var perUnitReport = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, PerUnitAggregate> entry in perUnitSummary)
            perUnitReport[entry.Key] = entry.Value.BuildPlain();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["scenario"] = ProjectScenarioPlain(scenario),
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
            ["win_rate"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["player"] = accum.TotalWinsPlayer,
                ["hostile"] = accum.TotalWinsHostile,
                ["draw"] = accum.TotalDraws,
            },
            ["global"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["charge"] = BuildSkillReport(accum.TotalChargeAttempts, accum.TotalChargeSuccesses, accum.TotalChargeMastery, n),
                ["warrior_heavy_strike"] = BuildSkillReport(accum.TotalHeavyAttempts, accum.TotalHeavySuccesses, accum.TotalHeavyMastery, n),
                ["archer_aimed_shot"] = BuildSkillReport(accum.TotalAimedAttempts, accum.TotalAimedSuccesses, accum.TotalAimedMastery, n),
                ["archer_multishot"] = BuildSkillReport(accum.TotalMultishotAttempts, accum.TotalMultishotSuccesses, accum.TotalMultishotMastery, n),
                ["basic_attack"] = BuildSkillReport(accum.TotalBasicAttempts, accum.TotalBasicSuccesses, accum.TotalBasicMastery, n),
            },
            ["player"] = BuildFactionReport(accum.PlayerDamageDone, accum.PlayerDamageTaken, accum.PlayerChargeAttempts, accum.PlayerChargeSuccesses, accum.PlayerHeavyAttempts, accum.PlayerHeavySuccesses, accum.PlayerAimedAttempts, accum.PlayerAimedSuccesses, accum.PlayerMultishotAttempts, accum.PlayerMultishotSuccesses, accum.PlayerBasicAttempts, accum.PlayerBasicSuccesses, n),
            ["hostile"] = BuildFactionReport(accum.HostileDamageDone, accum.HostileDamageTaken, accum.HostileChargeAttempts, accum.HostileChargeSuccesses, accum.HostileHeavyAttempts, accum.HostileHeavySuccesses, accum.HostileAimedAttempts, accum.HostileAimedSuccesses, accum.HostileMultishotAttempts, accum.HostileMultishotSuccesses, accum.HostileBasicAttempts, accum.HostileBasicSuccesses, n),
            ["per_unit_summary"] = perUnitReport,
            ["runs"] = runDetails,
        };
    }

    private static Dictionary<string, object> BuildFactionReport(
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
        return new Dictionary<string, object>(StringComparer.Ordinal)
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

    private static Dictionary<string, object> BuildSkillReport(
        int attempts,
        int successes,
        int mastery,
        double runCount
    )
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
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

    private static bool WritePlainJsonFile(
        string path,
        IReadOnlyDictionary<string, object> payload
    )
    {
        using GodotProjectionLease<GDictionary> lease =
            TraceDictionaryProjection.BuildJsonSafeLease(
                payload,
                "mixed-mirror-analysis-file-payload",
                LifetimeDomain.Request,
                $"RunMixed6v12MirrorAnalysis.write:{path}"
            );
        return WriteLeasedJsonFile(path, lease.Value);
    }

    private static bool WriteLeasedJsonFile(string path, GDictionary payload)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string absolutePath = path.StartsWith("res://") || path.StartsWith("user://")
            ? ProjectSettings.GlobalizePath(path)
            : path;
        string directory = absolutePath.GetBaseDir();
        if (!string.IsNullOrEmpty(directory))
            DirAccess.MakeDirRecursiveAbsolute(directory);
        using NativeLeaseScope fileScope = new(
            "mixed-mirror-analysis-json-file",
            LifetimeDomain.Request
        );
        FileAccess openedFile = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Write);
        if (openedFile == null)
            return false;
        try
        {
            FileAccess file = fileScope.Own(openedFile, $"open:{absolutePath}");
            file.StoreString(Json.Stringify(payload, "\t"));
            return true;
        }
        finally
        {
            openedFile.Close();
        }
    }

    private static Dictionary<string, object> ProjectScenarioPlain(
        BattleSimScenarioDef scenario
    ) => BattleSimFilePayloadProjection.BuildScenarioFacts(scenario);

    private static Dictionary<string, object> BuildStartFailurePlain(
        BattleStartFailureSnapshot snapshot
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (snapshot == null)
            return result;
        if (!string.IsNullOrEmpty(snapshot.Reason))
            result["reason"] = snapshot.Reason;
        if (snapshot.AllyUnitCount >= 0)
            result["ally_unit_count"] = snapshot.AllyUnitCount;
        if (snapshot.EnemyUnitCount >= 0)
            result["enemy_unit_count"] = snapshot.EnemyUnitCount;
        if (snapshot.PlacementAttempt >= 0)
            result["placement_attempt"] = snapshot.PlacementAttempt;
        if (snapshot.TerrainSeed != 0)
            result["terrain_seed"] = snapshot.TerrainSeed;
        if (snapshot.AllySpawnCount >= 0)
            result["ally_spawn_count"] = snapshot.AllySpawnCount;
        if (snapshot.EnemySpawnCount >= 0)
            result["enemy_spawn_count"] = snapshot.EnemySpawnCount;
        if (snapshot.PlacementAttempts >= 0)
            result["placement_attempts"] = snapshot.PlacementAttempts;

        if (snapshot.ReachabilityResult != null)
            result["reachability"] = BuildReachabilityPlain(snapshot.ReachabilityResult);
        return result;
    }

    private static Dictionary<string, object> BuildReachabilityPlain(
        BattleSpawnReachabilityResult reachability
    )
    {
        var details = new List<object>();
        foreach (BattleSpawnReachabilityUnitResult detail in reachability.Details)
        {
            var item = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["valid"] = detail.Valid,
            };
            if (detail.UnitId != (StringName)"")
                item["unit_id"] = detail.UnitId;
            if (detail.FactionId != (StringName)"")
                item["faction_id"] = detail.FactionId;
            if (!string.IsNullOrEmpty(detail.Reason))
                item["reason"] = detail.Reason;
            if (detail.AttackAnchor != new Vector2I(-1, -1))
                item["attack_anchor"] = detail.AttackAnchor;
            if (detail.TargetUnitId != (StringName)"")
                item["target_unit_id"] = detail.TargetUnitId;
            if (detail.SkillId != (StringName)"")
                item["skill_id"] = detail.SkillId;
            if (detail.ReachableAnchorCount >= 0)
                item["reachable_anchor_count"] = detail.ReachableAnchorCount;
            if (detail.AttackSkillIds.Count > 0)
                item["attack_skill_ids"] = new List<StringName>(detail.AttackSkillIds);
            details.Add(item);
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["valid"] = reachability.Valid,
            ["invalid_enemy_unit_ids"] = new List<StringName>(
                reachability.InvalidEnemyUnitIds
            ),
            ["invalid_player_unit_ids"] = new List<StringName>(
                reachability.InvalidPlayerUnitIds
            ),
            ["details"] = details,
        };
    }

    private static string GetPlainString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    ) =>
        source != null
        && source.TryGetValue(key, out object value)
        && value is string text
            ? text
            : fallback;

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

    private static List<long> ReadLongListEnvironment(string name)
    {
        var values = new List<long>();
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

    private sealed class MixedSimulationRunResult
    {
        public bool BattleEnded { get; init; }
        public string WinnerFactionId { get; init; } = "";
        public int Iterations { get; init; }
        public int TimelineSteps { get; init; }
        public BattleSimMetricsSnapshot Metrics { get; init; } =
            BattleSimMetricsSnapshot.Empty();
        public Dictionary<string, object> AiProfile { get; init; } =
            new(StringComparer.Ordinal);
        public Dictionary<string, object> StartFailure { get; set; } =
            new(StringComparer.Ordinal);
        public IReadOnlyList<BattleAiTurnTraceProjection> AiTurnTraces { get; set; } =
            System.Array.Empty<BattleAiTurnTraceProjection>();
    }

    private sealed class PerUnitAggregate
    {
        private readonly Dictionary<string, int> _skillAttempts =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _skillSuccesses =
            new(StringComparer.Ordinal);

        internal PerUnitAggregate(string displayName, string factionId)
        {
            DisplayName = displayName ?? "";
            FactionId = factionId ?? "";
        }

        private string DisplayName { get; }
        private string FactionId { get; }
        private int Runs { get; set; }
        private int TurnCount { get; set; }
        private int TotalDamageDone { get; set; }
        private int TotalDamageTaken { get; set; }
        private int TotalHealingDone { get; set; }
        private int TotalHealingReceived { get; set; }
        private int KillCount { get; set; }
        private int DeathCount { get; set; }

        internal void Absorb(BattleSimUnitMetricsSnapshot unit)
        {
            Runs++;
            TurnCount += unit.TurnCount;
            TotalDamageDone += unit.TotalDamageDone;
            TotalDamageTaken += unit.TotalDamageTaken;
            TotalHealingDone += unit.TotalHealingDone;
            TotalHealingReceived += unit.TotalHealingReceived;
            KillCount += unit.KillCount;
            DeathCount += unit.DeathCount;
            MergeCounts(_skillAttempts, unit.SkillAttemptCounts);
            MergeCounts(_skillSuccesses, unit.SkillSuccessCounts);
        }

        internal Dictionary<string, object> BuildPlain() =>
            new(StringComparer.Ordinal)
            {
                ["display_name"] = DisplayName,
                ["faction_id"] = FactionId,
                ["runs"] = Runs,
                ["turn_count"] = TurnCount,
                ["total_damage_done"] = TotalDamageDone,
                ["total_damage_taken"] = TotalDamageTaken,
                ["total_healing_done"] = TotalHealingDone,
                ["total_healing_received"] = TotalHealingReceived,
                ["kill_count"] = KillCount,
                ["death_count"] = DeathCount,
                ["skill_attempts"] = _skillAttempts,
                ["skill_successes"] = _skillSuccesses,
            };

        private static void MergeCounts(
            Dictionary<string, int> target,
            IReadOnlyDictionary<string, int> source
        )
        {
            if (source == null)
                return;
            foreach (KeyValuePair<string, int> entry in source)
            {
                target[entry.Key] =
                    (target.TryGetValue(entry.Key, out int current) ? current : 0)
                    + entry.Value;
            }
        }
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

        public void AbsorbRun(
            MixedSimulationRunResult result,
            BattleSimFormalCombatFixture fixture
        )
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

            foreach (
                KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry
                in result.Metrics.Factions
            )
            {
                BattleSimUnitMetricsSnapshot faction = entry.Value;
                if (faction == null)
                    continue;
                int facChargeA = ReadCount(faction.SkillAttemptCounts, "charge");
                int facChargeS = ReadCount(faction.SkillSuccessCounts, "charge");
                int facHeavyA = ReadCount(
                    faction.SkillAttemptCounts,
                    "warrior_heavy_strike"
                );
                int facHeavyS = ReadCount(
                    faction.SkillSuccessCounts,
                    "warrior_heavy_strike"
                );
                int facAimedA = ReadCount(
                    faction.SkillAttemptCounts,
                    "archer_aimed_shot"
                );
                int facAimedS = ReadCount(
                    faction.SkillSuccessCounts,
                    "archer_aimed_shot"
                );
                int facMultiA = ReadCount(
                    faction.SkillAttemptCounts,
                    "archer_multishot"
                );
                int facMultiS = ReadCount(
                    faction.SkillSuccessCounts,
                    "archer_multishot"
                );
                int facBasicA = ReadCount(faction.SkillAttemptCounts, "basic_attack");
                int facBasicS = ReadCount(faction.SkillSuccessCounts, "basic_attack");
                int facDamageDone = faction.TotalDamageDone;
                int facDamageTaken = faction.TotalDamageTaken;

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

                if (entry.Key == "player")
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

            if (result.BattleEnded)
            {
                EndedCount++;
                string winner = result.WinnerFactionId;
                if (winner == "player")
                    TotalWinsPlayer++;
                else if (winner == "hostile")
                    TotalWinsHostile++;
                else
                    TotalDraws++;
            }
            TotalIterations += result.Iterations;
            TotalTimelineSteps += result.TimelineSteps;
        }

        private static int ReadCount(
            IReadOnlyDictionary<string, int> counts,
            string key
        ) =>
            counts != null && counts.TryGetValue(key, out int value) ? value : 0;
    }
}
