using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_performance_baseline : LifecycleTestSceneTree
{
    private const int ActionThreshold = 120;
    private const int TimelineTicksPerStep = 1;
    private const int TimelineTuPerTick = 5;
    private const int LegacyTargetTu = 200;
    private const int DefaultRepeatCount = 2;
    private const int DefaultMaxIterations = 100000;
    private const int MaxIdleLoops = 25;
    private const int FixtureVersion = 2;
    private const string BaselinePath = "res://tests/battle_runtime/benchmarks/baselines/ai_baseline.json";
    private const string MutationGuardAbortProcessSetting = "battle_ai/fail_loud_abort_process";

    private readonly TestHarness _test = new();
    private readonly Dictionary<string, ScenarioSpec> _scenarios =
        new()
        {
            ["small_4v8"] = new ScenarioSpec(new Vector2I(20, 14), 4, 8),
            ["medium_6v20"] = new ScenarioSpec(new Vector2I(20, 14), 6, 20),
            ["large_6v40"] = new ScenarioSpec(new Vector2I(20, 14), 6, 40),
        };

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunDeferred);
    }

    private void RunDeferred()
    {
        int exitCode = Run();
        RequestTestExit(_test.Finish("AI performance baseline", exitCode));
    }

    private int Run()
    {
        bool updateBaseline = ReadBoolEnvironment("UPDATE_BASELINE", false)
            || ReadBoolEnvironment("AI_BASELINE_UPDATE", false);
        double tolerancePct = ReadDoubleEnvironment(
            "AI_BASELINE_TOLERANCE_PCT",
            ReadDoubleEnvironment("BASELINE_TOLERANCE_PCT", AiBaselineDiff.DefaultTolerancePct)
        );
        int absoluteToleranceUsec = Math.Max(
            ReadIntEnvironment(
                "AI_BASELINE_ABSOLUTE_TOLERANCE_USEC",
                AiBaselineDiff.DefaultAbsoluteToleranceUsec
            ),
            0
        );
        int minMetricCallCount = Math.Max(
            ReadIntEnvironment(
                "AI_BASELINE_MIN_METRIC_CALL_COUNT",
                AiBaselineDiff.DefaultMinMetricCallCount
            ),
            0
        );
        int minPercentileCallCount = Math.Max(
            ReadIntEnvironment(
                "AI_BASELINE_MIN_PERCENTILE_CALL_COUNT",
                AiBaselineDiff.DefaultMinPercentileCallCount
            ),
            0
        );
        int repeatCount = Math.Max(
            ReadIntEnvironment(
                "AI_BASELINE_REPEAT_COUNT",
                ReadIntEnvironment("BASELINE_REPEAT_COUNT", DefaultRepeatCount)
            ),
            2
        );
        int maxIterations = ReadIntEnvironment("AI_BASELINE_MAX_ITERATIONS", DefaultMaxIterations);
        if (maxIterations <= 0)
            maxIterations = int.MaxValue;
        bool requireCompleted = ReadBoolEnvironment("AI_BASELINE_REQUIRE_COMPLETED", true);
        bool aiMutationGuardEnabled = ReadBoolEnvironment(
            "AI_BASELINE_MUTATION_GUARD_ENABLED",
            false
        );
        bool aiMutationGuardAbortProcess = ReadBoolEnvironment(
            "AI_BASELINE_MUTATION_GUARD_ABORT_PROCESS",
            ReadBoolEnvironment("AI_MUTATION_GUARD_ABORT_PROCESS", false)
        );
        ProjectSettings.SetSetting(
            MutationGuardAbortProcessSetting,
            aiMutationGuardAbortProcess
        );
        string outputPath = ResolveOutputPath(updateBaseline);
        List<string> scenarioIds = ResolveScenarioFilter();
        List<string> compareMetrics = ResolveCompareMetrics();

        ConsoleProcessOutput.WriteStandard(
            $"[AiBaseline] config update={updateBaseline} repeat={repeatCount} measured={repeatCount - 1} completion=battle_ended max_iterations={maxIterations} require_completed={requireCompleted} ai_mutation_guard={aiMutationGuardEnabled} ai_mutation_guard_abort_process={aiMutationGuardAbortProcess} tolerance_pct={tolerancePct:F1} absolute_tolerance_usec={absoluteToleranceUsec} min_metric_call_count={minMetricCallCount} min_percentile_call_count={minPercentileCallCount} compare_metrics={string.Join(",", compareMetrics)} output={outputPath}"
        );

        var scenariosDoc = new GDictionary();
        foreach (string scenarioId in scenarioIds)
        {
            if (!_scenarios.TryGetValue(scenarioId, out ScenarioSpec spec))
            {
                ConsoleProcessOutput.WriteStandard($"[AiBaseline] WARN scenario '{scenarioId}' is not defined, skipping.");
                continue;
            }

            ConsoleProcessOutput.WriteStandard(
                $"[AiBaseline] scenario={scenarioId} starting map={spec.MapSize.X}x{spec.MapSize.Y} units={spec.AllyCount}v{spec.EnemyCount}"
            );
            GDictionary scenarioResult = RunScenario(
                scenarioId,
                spec,
                repeatCount,
                maxIterations,
                requireCompleted,
                aiMutationGuardEnabled
            );
            scenariosDoc[scenarioId] = scenarioResult;
            ConsoleProcessOutput.WriteStandard(FormatScenarioSummary(scenarioId, scenarioResult));
            if (_test.Failures.Count > 0)
                break;
        }

        if (_test.Failures.Count > 0)
            return 0;

        GDictionary currentDoc = AiBaselineDiff.BuildBaselineDoc(
            scenariosDoc,
            AiProfileCapture.ResolveGitCommit()
        );
        currentDoc["completion_policy"] = "battle_ended";
        currentDoc["legacy_target_tu"] = LegacyTargetTu;
        currentDoc["max_iterations"] = maxIterations;
        currentDoc["repeat_total"] = repeatCount;
        currentDoc["repeat_measured"] = repeatCount - 1;
        currentDoc["baseline_aggregation_policy"] =
            "The first run is warmup-only. Profiler samples from every measured run are merged before avg/p50/p95 are calculated.";
        currentDoc["fixture_version"] = FixtureVersion;
        currentDoc["fixture_contract"] =
            "Manual allies wait. Melee AI uses a steel_longsword-equivalent equipped weapon; ranged AI uses an ash_longbow-equivalent equipped weapon. warrior_heavy_strike and archer_pinning_shot must pass their runtime weapon/cast preflight before measurement.";
        currentDoc["ai_mutation_guard_enabled"] = aiMutationGuardEnabled;
        currentDoc["ai_mutation_guard_abort_process"] = aiMutationGuardAbortProcess;
        var compareMetricsArray = new GArray();
        foreach (string metric in compareMetrics)
            compareMetricsArray.Add(metric);
        currentDoc["baseline_compare_metrics"] = compareMetricsArray;
        currentDoc["baseline_absolute_tolerance_usec"] = absoluteToleranceUsec;
        currentDoc["baseline_min_metric_call_count"] = minMetricCallCount;
        currentDoc["baseline_min_percentile_call_count"] = minPercentileCallCount;
        currentDoc["measurement_note"] =
            "Full-battle AI performance baseline. Runs stop only when battle_ended is reached; max_iterations is a safety guard, not the measurement target. Mutation guard is disabled by default so timings measure AI runtime rather than guard snapshot overhead; set AI_BASELINE_MUTATION_GUARD_ENABLED=1 for guard-on diagnostics. Regression checks compare the configured profiler metrics with percentage and optional absolute-usec tolerance.";

        if (updateBaseline)
        {
            if (!AiBaselineDiff.WriteBaseline(outputPath, currentDoc))
            {
                ConsoleProcessOutput.WriteFailure($"[AiBaseline] failed to write baseline at {outputPath}");
                return 1;
            }
            ConsoleProcessOutput.WriteStandard($"[AiBaseline] wrote baseline {outputPath}");
            return 0;
        }

        GDictionary baseline = AiBaselineDiff.ReadBaseline(BaselinePath);
        if (baseline.Count > 0)
        {
            GArray diffs = AiBaselineDiff.Compare(
                baseline,
                currentDoc,
                tolerancePct,
                compareMetrics,
                absoluteToleranceUsec,
                minMetricCallCount,
                minPercentileCallCount
            );
            string report = AiBaselineDiff.FormatDiffReport(
                diffs,
                tolerancePct,
                absoluteToleranceUsec,
                minMetricCallCount,
                minPercentileCallCount
            );
            currentDoc["baseline_diff"] = diffs;
            currentDoc["baseline_diff_report"] = report;
            currentDoc["baseline_regressions"] = AiBaselineDiff.CountRegressions(diffs);
            ConsoleProcessOutput.WriteStandard(report);
        }
        else
        {
            currentDoc["baseline_diff_report"] = $"[AiBaseline] no baseline found at {BaselinePath}.";
            currentDoc["baseline_regressions"] = 0;
        }

        if (!AiBaselineDiff.WriteBaseline(outputPath, currentDoc))
        {
            ConsoleProcessOutput.WriteFailure($"[AiBaseline] failed to write snapshot at {outputPath}");
            return 1;
        }
        ConsoleProcessOutput.WriteStandard($"[AiBaseline] wrote snapshot {outputPath}");
        return AiBaselineDiff.CountRegressions(
            currentDoc.ContainsKey("baseline_diff") ? currentDoc["baseline_diff"].AsGodotArray() : new GArray()
        ) > 0
            ? 1
            : 0;
    }

    private GDictionary RunScenario(
        string scenarioId,
        ScenarioSpec spec,
        int repeatCount,
        int maxIterations,
        bool requireCompleted,
        bool aiMutationGuardEnabled
    )
    {
        var perRunChoose = new GArray();
        var perRunBindAiHelpers = new GArray();
        var perRunSkill = new GArray();
        var perRunAction = new GArray();
        var perRunAssemble = new GArray();
        var perRunMovementDistanceBand = new GArray();
        var perRunMovementSnapshotRebuild = new GArray();
        var perRunMeta = new GArray();

        for (int runIndex = 0; runIndex < repeatCount; runIndex++)
        {
            bool measured = runIndex > 0;
            RunResult runResult = RunPass(
                scenarioId,
                spec,
                maxIterations,
                measured,
                aiMutationGuardEnabled
            );
            string phase = measured ? "measured" : "warmup";
            ConsoleProcessOutput.WriteStandard(
                $"[AiBaseline]   run {runIndex + 1}/{repeatCount} ({phase}): ai_turns={runResult.AiTurns} manual_turns={runResult.ManualTurns} final_tu={runResult.FinalTu} iterations={runResult.Iterations} ended={runResult.BattleEnded} termination={runResult.TerminationReason} winner={runResult.WinnerFactionId} movement_rebuilds={runResult.MovementCacheDiagnostics.SnapshotRebuildCount} path_cache={runResult.MovementCacheDiagnostics.PathTargetCacheHitCount}/{runResult.MovementCacheDiagnostics.PathTargetCacheMissCount} elapsed={runResult.ElapsedSeconds:F2}s"
            );

            if (runResult.TerminationReason == "fixture_contract_invalid")
                break;

            if (requireCompleted && !runResult.BattleEnded)
            {
                _test.Fail(
                    $"{scenarioId} {phase} run did not finish: termination={runResult.TerminationReason} max_iterations={maxIterations} final_tu={runResult.FinalTu} iterations={runResult.Iterations} movement_rebuilds={runResult.MovementCacheDiagnostics.SnapshotRebuildCount} path_cache_hits={runResult.MovementCacheDiagnostics.PathTargetCacheHitCount} path_cache_misses={runResult.MovementCacheDiagnostics.PathTargetCacheMissCount}."
                );
            }

            if (!measured)
                continue;

            perRunChoose.Add(runResult.ChooseStats);
            perRunBindAiHelpers.Add(runResult.BindAiHelpersStats);
            perRunSkill.Add(runResult.SkillScoreStats);
            perRunAction.Add(runResult.ActionScoreStats);
            perRunAssemble.Add(runResult.AssemblerStats);
            perRunMovementDistanceBand.Add(runResult.MovementDistanceBandStats);
            perRunMovementSnapshotRebuild.Add(runResult.MovementSnapshotRebuildStats);
            perRunMeta.Add(
                new GDictionary
                {
                    ["ai_turns"] = runResult.AiTurns,
                    ["manual_turns"] = runResult.ManualTurns,
                    ["iterations"] = runResult.Iterations,
                    ["timeline_steps"] = runResult.TimelineSteps,
                    ["final_tu"] = runResult.FinalTu,
                    ["battle_ended"] = runResult.BattleEnded,
                    ["termination_reason"] = runResult.TerminationReason,
                    ["winner"] = runResult.WinnerFactionId,
                    ["elapsed_seconds"] = runResult.ElapsedSeconds,
                    ["trace_balanced"] = runResult.TraceBalanced,
                    ["trace_truncated"] = runResult.TraceTruncated,
                    ["movement_snapshot_rebuild_count"] =
                        runResult.MovementCacheDiagnostics.SnapshotRebuildCount,
                    ["movement_path_cache_hit_count"] =
                        runResult.MovementCacheDiagnostics.PathTargetCacheHitCount,
                    ["movement_path_cache_miss_count"] =
                        runResult.MovementCacheDiagnostics.PathTargetCacheMissCount,
                    ["movement_path_cache_entry_count"] =
                        runResult.MovementCacheDiagnostics.PathTargetCacheEntryCount,
                    ["hotspots"] = runResult.Hotspots,
                }
            );
        }

        GDictionary mergedChoose = AiBaselineDiff.MergeRuns(perRunChoose);
        GDictionary mergedBindAiHelpers = AiBaselineDiff.MergeRuns(perRunBindAiHelpers);
        GDictionary mergedSkill = AiBaselineDiff.MergeRuns(perRunSkill);
        GDictionary mergedAction = AiBaselineDiff.MergeRuns(perRunAction);
        GDictionary mergedAssemble = AiBaselineDiff.MergeRuns(perRunAssemble);
        GDictionary mergedMovementDistanceBand =
            AiBaselineDiff.MergeRuns(perRunMovementDistanceBand);
        GDictionary mergedMovementSnapshotRebuild =
            AiBaselineDiff.MergeRuns(perRunMovementSnapshotRebuild);

        GDictionary summaryChoose = AiBaselineDiff.SummarizeStats(mergedChoose);
        GDictionary summaryBindAiHelpers = AiBaselineDiff.SummarizeStats(mergedBindAiHelpers);
        GDictionary summarySkill = AiBaselineDiff.SummarizeStats(mergedSkill);
        GDictionary summaryAction = AiBaselineDiff.SummarizeStats(mergedAction);
        GDictionary summaryAssemble = AiBaselineDiff.SummarizeStats(mergedAssemble);
        GDictionary summaryMovementDistanceBand =
            AiBaselineDiff.SummarizeStats(mergedMovementDistanceBand);
        GDictionary summaryMovementSnapshotRebuild =
            AiBaselineDiff.SummarizeStats(mergedMovementSnapshotRebuild);

        long chooseInclusiveUsec = DictLong(mergedChoose, "total_usec");
        long skillTotalUsec = DictLong(mergedSkill, "total_usec");
        long actionTotalUsec = DictLong(mergedAction, "total_usec");
        summaryChoose["total_inclusive_usec"] = chooseInclusiveUsec;
        summaryChoose["total_self_usec"] = Math.Max(
            chooseInclusiveUsec - skillTotalUsec - actionTotalUsec,
            0L
        );

        int aiTurnsTotal = 0;
        var aiTurnsPerRun = new GArray();
        foreach (Variant metaValue in perRunMeta)
        {
            if (metaValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary meta = metaValue.AsGodotDictionary();
            int aiTurns = DictInt(meta, "ai_turns");
            aiTurnsTotal += aiTurns;
            aiTurnsPerRun.Add(aiTurns);
        }

        return new GDictionary
        {
            ["completion_policy"] = "battle_ended",
            ["legacy_target_tu"] = LegacyTargetTu,
            ["max_iterations"] = maxIterations,
            ["ai_mutation_guard_enabled"] = aiMutationGuardEnabled,
            ["repeat_measured"] = perRunMeta.Count,
            ["ai_turns_total"] = aiTurnsTotal,
            ["ai_turns_per_run"] = aiTurnsPerRun,
            ["runs_meta"] = perRunMeta,
            ["layers"] = new GDictionary
            {
                ["choose_command"] = summaryChoose,
                ["bind_ai_helpers"] = summaryBindAiHelpers,
                ["build_skill_score_input"] = summarySkill,
                ["build_action_score_input"] = summaryAction,
                ["build_unit_action_plan"] = summaryAssemble,
                ["movement_distance_band_path_targets"] = summaryMovementDistanceBand,
                ["movement_query_setup_rebuild_snapshot"] = summaryMovementSnapshotRebuild,
            },
        };
    }

    private RunResult RunPass(
        string scenarioId,
        ScenarioSpec spec,
        int maxIterations,
        bool measured,
        bool aiMutationGuardEnabled
    )
    {
        ulong startMsec = Time.GetTicksMsec();
        AiTraceRecorder recorder = null;
        BattleState state = null;
        GameSession gameSession = null;
        BattleRuntimeModule runtime = null;

        try
        {
            gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            runtime = new BattleRuntimeModule();
            runtime.setup(
                skill_definitions: catalog.GetSkillDefinitionsTyped(),
                enemy_templates: catalog.GetEnemyTemplateDefinitions(),
                enemy_ai_brains: catalog.GetEnemyAiBrainDefinitions(),
                item_defs: catalog.GetItemDefsTyped(),
                skill_catalog: catalog.GetSkillCatalogTyped(),
                battle_special_profile_view: catalog.GetBattleSpecialProfileView()
            );
            runtime._ai_service.MutationGuardMode = aiMutationGuardEnabled
                ? BattleAiMutationGuardMode.FullSnapshotDiagnostic
                : BattleAiMutationGuardMode.Disabled;

            state = BuildFlatState(spec.MapSize, scenarioId);
            if (!PopulateUnits(runtime, state, spec))
            {
                return new RunResult
                {
                    TerminationReason = "fixture_contract_invalid",
                    ElapsedSeconds = (Time.GetTicksMsec() - startMsec) / 1000.0,
                };
            }
            runtime.SetupStateForTests(state);
            runtime._initialize_unit_trait_hooks();
            runtime._timeline_driver.InitializeUnitActionThresholds();
            runtime._battle_rating_system.InitializeBattleRatingStats();
            runtime._fate_runtime.BeginBattle(runtime.calamity_by_member_id);
            runtime._initialize_battle_metrics();
            state.PhaseKind = BattlePhaseKind.TimelineRunning;
            state.active_unit_id = "";
            state.ModalStateKind = BattleModalStateKind.None;
            state.attack_roll_nonce = 0;

            if (measured)
            {
                recorder = new AiTraceRecorder();
                recorder.SetEventCaptureEnabled(false);
                recorder.SetSampleCaptureEnabled(true);
                AiTraceRecorder.SetInstance(recorder);
            }

            runtime._build_ai_action_plans();

            var executionLoop = new BattleSimExecutionLoop();
            int iterations = 0;
            int idleLoops = 0;
            int timelineSteps = 0;
            int aiTurns = 0;
            int manualTurns = 0;

            while (iterations < maxIterations)
            {
                if (state.PhaseKind == BattlePhaseKind.BattleEnded)
                    break;

                iterations++;
                int previousTu = state.timeline.current_tu;
                BattlePhaseKind previousPhase = state.PhaseKind;
                StringName previousActiveUnitId = state.active_unit_id;
                int previousLogCount = state.log_entries.Count;

                if (state.PhaseKind == BattlePhaseKind.UnitActing)
                {
                    BattleUnitState activeUnit = GetUnit(state, state.active_unit_id);
                    if (activeUnit == null || !activeUnit.IsAlive())
                    {
                        _test.Fail($"{scenarioId}: invalid active unit {state.active_unit_id}");
                        break;
                    }
                    if (activeUnit.ControlModeKind == BattleUnitControlMode.Manual)
                        manualTurns++;
                    else
                        aiTurns++;
                }

                executionLoop.AdvanceStep(runtime, state, "wait", TimelineTicksPerStep);
                if (state.timeline.current_tu != previousTu)
                    timelineSteps++;

                bool madeProgress =
                    state.timeline.current_tu != previousTu
                    || state.PhaseKind != previousPhase
                    || state.active_unit_id != previousActiveUnitId
                    || state.log_entries.Count != previousLogCount;
                if (madeProgress)
                {
                    idleLoops = 0;
                }
                else
                {
                    idleLoops++;
                    if (idleLoops >= MaxIdleLoops)
                    {
                        _test.Fail(
                            $"{scenarioId}: stalled at TU={state.timeline.current_tu} phase={state.phase}"
                        );
                        break;
                    }
                }
            }

            AiTraceRecorder.SetInstance(null);
            using GodotProjectionLease<GDictionary> statsLease = recorder?.GetFuncStatsLease();
            GDictionary stats = statsLease?.Value;
            bool battleEnded = state.PhaseKind == BattlePhaseKind.BattleEnded;
            string terminationReason = battleEnded
                ? "battle_ended"
                : idleLoops >= MaxIdleLoops
                    ? "idle_stall"
                    : "iteration_budget";
            BattleMovementQueryService.CacheDiagnostics movementCacheDiagnostics =
                runtime.GetAiMovementQueryCacheDiagnostics();
            return new RunResult
            {
                ChooseStats = ExtractStats(stats, "advance:choose_command"),
                BindAiHelpersStats = ExtractStats(stats, "advance:bind_ai_helpers"),
                SkillScoreStats = ExtractStats(stats, "build_skill_score_input"),
                ActionScoreStats = ExtractStats(stats, "build_action_score_input"),
                AssemblerStats = ExtractStats(stats, "build_unit_action_plan"),
                MovementDistanceBandStats = ExtractStats(
                    stats,
                    "movement:distance_band_path_targets"
                ),
                MovementSnapshotRebuildStats = ExtractStats(
                    stats,
                    "movement_query_setup:rebuild_snapshot"
                ),
                Hotspots = BuildHotspotSummary(stats),
                AiTurns = aiTurns,
                ManualTurns = manualTurns,
                Iterations = iterations,
                TimelineSteps = timelineSteps,
                FinalTu = state.timeline.current_tu,
                BattleEnded = battleEnded,
                TerminationReason = terminationReason,
                WinnerFactionId = state.winner_faction_id.ToString(),
                MovementCacheDiagnostics = movementCacheDiagnostics,
                TraceBalanced = recorder == null || recorder.AssertBalanced(),
                TraceTruncated = recorder != null && recorder.IsTruncated(),
                ElapsedSeconds = (Time.GetTicksMsec() - startMsec) / 1000.0,
            };
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
            BattleTestFixture.DisposeBattleFixture(runtime, state);
            gameSession?.Dispose();
        }
    }

    private static BattleState BuildFlatState(Vector2I mapSize, string scenarioId)
    {
        var state = new BattleState
        {
            battle_id = new StringName($"ai_baseline_{scenarioId}"),
            phase = "timeline_running",
            map_size = mapSize,
            timeline = new BattleTimelineState
            {
                tu_per_tick = TimelineTuPerTick,
                frozen = false,
            },
        };
        state.InitializeObjective(BattleEliminationObjectiveDefinition.Instance);

        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(coord, cell);
            }
        }

        state.RebuildCellColumns();
        return state;
    }

    private bool PopulateUnits(BattleRuntimeModule runtime, BattleState state, ScenarioSpec spec)
    {
        int failureCountBeforeSetup = _test.Failures.Count;
        bool fixtureContractsValid = true;
        for (int index = 0; index < spec.AllyCount; index++)
        {
            int allyY = 3 + (index % 6);
            Vector2I coord = new(1 + (index / 6), allyY);
            AddUnitToState(
                runtime,
                state,
                BuildManualUnit(
                    new StringName($"ai_baseline_ally_{index + 1:00}"),
                    $"Ally {index + 1:00}",
                    coord
                ),
                isEnemy: false
            );
        }

        List<Vector2I> enemyPositions = new();
        int exMin = 10;
        int exMax = Math.Min(exMin + 8, state.map_size.X - 1);
        int eyMin = 3;
        int eyMax = Math.Min(eyMin + 6, state.map_size.Y - 1);
        for (int y = eyMin; y <= eyMax; y++)
        {
            for (int x = exMin; x <= exMax; x++)
                enemyPositions.Add(new Vector2I(x, y));
        }
        enemyPositions.Sort((left, right) =>
            left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X)
        );

        for (int index = 0; index < spec.EnemyCount; index++)
        {
            Vector2I coord = enemyPositions[index];
            bool isMelee = index % 2 == 0;
            BattleUnitState enemy;
            StringName finisherSkillId;
            if (isMelee)
            {
                enemy = BuildAiUnit(
                    new StringName($"ai_baseline_enemy_{index + 1:00}"),
                    $"Melee {index + 1:00}",
                    coord,
                    "melee_aggressor",
                    new[] { new StringName("charge"), new StringName("warrior_heavy_strike") }
                );
                ApplyBaselineMeleeWeapon(enemy);
                finisherSkillId = "warrior_heavy_strike";
            }
            else
            {
                enemy = BuildAiUnit(
                    new StringName($"ai_baseline_enemy_{index + 1:00}"),
                    $"Suppressor {index + 1:00}",
                    coord,
                    "ranged_suppressor",
                    new[]
                    {
                        new StringName("archer_suppressive_fire"),
                        new StringName("archer_pinning_shot"),
                    }
                );
                ApplyBaselineRangedWeapon(enemy);
                finisherSkillId = "archer_pinning_shot";
            }
            if (index < 2)
            {
                fixtureContractsValid &= ValidateFinisherContract(
                    runtime,
                    enemy,
                    finisherSkillId,
                    isMelee ? new StringName("sword") : new StringName("bow")
                );
            }
            AddUnitToState(runtime, state, enemy, isEnemy: true);
        }
        return fixtureContractsValid && _test.Failures.Count == failureCountBeforeSetup;
    }

    private static BattleUnitState BuildManualUnit(StringName unitId, string displayName, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "player",
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 260,
            mp: 120,
            stamina: 120,
            aura: 120,
            ap: 2,
            isAlive: true
        );
        unit.SetActionThresholdTyped(ActionThreshold);
        unit.SetAnchorCoord(coord);
        SetCoreAttributes(unit, hpMax: 260, attackBonus: 12, armorClass: 20);
        return unit;
    }

    private static BattleUnitState BuildAiUnit(
        StringName unitId,
        string displayName,
        Vector2I coord,
        StringName brainId,
        IReadOnlyList<StringName> skillIds
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "enemy",
            control_mode = "ai",
            ai_brain_id = brainId,
            ai_state_id = "",
        }.WithCombatResourcesForTest(
            hp: 180,
            mp: 120,
            stamina: 120,
            aura: 120,
            ap: 2,
            isAlive: true
        );
        unit.SetActionThresholdTyped(ActionThreshold);
        unit.SetAnchorCoord(coord);
        SetCoreAttributes(unit, hpMax: 180, attackBonus: 16, armorClass: 18);
        unit.SetKnownActiveSkillIds(Array.Empty<StringName>());
        foreach (StringName skillId in skillIds ?? Array.Empty<StringName>())
        {
            unit.AddKnownActiveSkill(skillId);
            unit.SetKnownSkillLevelTyped(skillId, 1);
        }
        return unit;
    }

    private static void ApplyBaselineMeleeWeapon(BattleUnitState unit)
    {
        unit?.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(
                    BattleWeaponProfileKind.Equipped
                ),
                weapon_item_id = "steel_longsword",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = BattleUnitState.ToStringName(
                    BattleWeaponGripKind.OneHanded
                ),
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 10 },
                weapon_is_versatile = true,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static void ApplyBaselineRangedWeapon(BattleUnitState unit)
    {
        unit?.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(
                    BattleWeaponProfileKind.Equipped
                ),
                weapon_item_id = "ash_longbow",
                weapon_profile_type_id = "longbow",
                weapon_range_type = "ranged",
                weapon_family = "bow",
                weapon_current_grip = BattleUnitState.ToStringName(
                    BattleWeaponGripKind.TwoHanded
                ),
                weapon_attack_range = 4,
                weapon_two_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private bool ValidateFinisherContract(
        BattleRuntimeModule runtime,
        BattleUnitState unit,
        StringName skillId,
        StringName expectedWeaponFamily
    )
    {
        SkillDefinition skillDefinition = runtime?.GetSkillDefinitionTyped(skillId);
        if (skillDefinition?.CombatProfile == null)
        {
            _test.Fail($"AI baseline finisher {skillId} is missing its runtime definition.");
            return false;
        }

        bool valid = true;
        BattleWeaponProjectionValues weaponProjection =
            unit.GetWeaponProjectionReadViewTyped().Values;
        if (weaponProjection.Family != expectedWeaponFamily)
        {
            _test.Fail(
                $"AI baseline finisher {skillId} expects weapon family {expectedWeaponFamily}, but {unit.unit_id} has {weaponProjection.Family}."
            );
            valid = false;
        }
        WeaponDice activeWeaponDice = unit.GetActiveWeaponDiceTyped();
        if (activeWeaponDice == null || activeWeaponDice.IsEmpty())
        {
            _test.Fail(
                $"AI baseline finisher {skillId} has no active weapon dice on {unit.unit_id}."
            );
            valid = false;
        }
        if (
            BattleRangeService.RequiresCurrentMeleeWeapon(skillDefinition)
            && !BattleRangeService.UnitHasMeleeWeapon(unit)
        )
        {
            _test.Fail(
                $"AI baseline finisher {skillId} requires an equipped weapon, but {unit.unit_id} has no valid weapon projection."
            );
            valid = false;
        }
        if (
            !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                unit,
                skillDefinition.CombatProfile.RequiredWeaponFamilies
            )
        )
        {
            _test.Fail(
                $"AI baseline finisher {skillId} rejects weapon family {weaponProjection.Family} on {unit.unit_id}."
            );
            valid = false;
        }

        BattleAiSkillAffordanceRecord affordance = new BattleAiSkillAffordanceClassifier()
            .ClassifySkill(skillDefinition, skill_level: 1);
        if (
            !affordance.is_generatable
            || affordance.target_mode != BattleTargetMode.Unit
            || affordance.selection_mode != BattleTargetSelectionMode.SingleUnit
            || !affordance.affordances.Contains(new StringName("unit_hostile.damage"))
            || !affordance.action_families.Contains(new StringName("use_unit_skill"))
        )
        {
            _test.Fail(
                $"AI baseline finisher {skillId} must remain a generatable single-target hostile damage skill."
            );
            valid = false;
        }

        BattleSkillCastBlockReasonKind blockReason = runtime._get_skill_cast_block_reason(
            unit,
            skillDefinition
        );
        if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
        {
            _test.Fail(
                $"AI baseline finisher {skillId} is blocked for {unit.unit_id}: {BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)}."
            );
            valid = false;
        }
        return valid;
    }

    private static void SetCoreAttributes(BattleUnitState unit, int hpMax, int attackBonus, int armorClass)
    {
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), attackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), armorClass);
    }

    private void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        if (isEnemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);

        if (!runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true))
            _test.Fail($"AI baseline unit {unit.unit_id} could not be placed.");
    }

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        return state?.GetUnit(unitId);
    }

    private static GDictionary ExtractStats(GDictionary recorderStats, StringName name)
    {
        if (
            recorderStats == null
            || !recorderStats.ContainsKey(name)
            || recorderStats[name].VariantType != Variant.Type.Dictionary
        )
        {
            return EmptyStats();
        }

        GDictionary row = recorderStats[name].AsGodotDictionary();
        return new GDictionary
        {
            ["call_count"] = DictLong(row, "ncalls"),
            ["total_usec"] = DictLong(row, "total_usec"),
            ["max_usec"] = DictLong(row, "max_usec"),
            ["samples"] =
                row.ContainsKey("samples") && row["samples"].VariantType == Variant.Type.PackedInt64Array
                    ? row["samples"].AsInt64Array()
                    : Array.Empty<long>(),
        };
    }

    private static GDictionary EmptyStats() =>
        new()
        {
            ["call_count"] = 0,
            ["total_usec"] = 0L,
            ["max_usec"] = 0L,
            ["samples"] = Array.Empty<long>(),
        };

    private static GArray BuildHotspotSummary(GDictionary recorderStats, int maxEntries = 20)
    {
        var entries = new List<GDictionary>();
        if (recorderStats == null)
            return new GArray();

        foreach (Variant key in recorderStats.Keys)
        {
            StringName traceName = ProgressionDataUtils.to_string_name(key);
            if (traceName == "")
                continue;
            GDictionary summary = AiBaselineDiff.SummarizeStats(ExtractStats(recorderStats, traceName));
            summary["name"] = traceName.ToString();
            entries.Add(summary);
        }
        entries.Sort(
            (left, right) =>
                DictLong(right, "total_usec").CompareTo(DictLong(left, "total_usec"))
        );

        var result = new GArray();
        int count = Math.Min(Math.Max(maxEntries, 0), entries.Count);
        for (int index = 0; index < count; index++)
        {
            result.Add(entries[index]);
        }
        return result;
    }

    private static string FormatScenarioSummary(string scenarioId, GDictionary scenario)
    {
        GDictionary layers = DictDict(scenario, "layers");
        GDictionary choose = DictDict(layers, "choose_command");
        GDictionary bindAiHelpers = DictDict(layers, "bind_ai_helpers");
        GDictionary skill = DictDict(layers, "build_skill_score_input");
        GDictionary action = DictDict(layers, "build_action_score_input");
        GDictionary assemble = DictDict(layers, "build_unit_action_plan");
        GDictionary movementDistanceBand = DictDict(layers, "movement_distance_band_path_targets");
        GDictionary movementSnapshotRebuild = DictDict(
            layers,
            "movement_query_setup_rebuild_snapshot"
        );
        return "[AiBaseline] "
            + $"{scenarioId} measured={DictInt(scenario, "repeat_measured")} ai_turns={DictInt(scenario, "ai_turns_total")} "
            + $"choose avg={DictInt(choose, "avg_usec")}us p50={DictInt(choose, "p50_usec")}us p95={DictInt(choose, "p95_usec")}us max={DictInt(choose, "max_usec")}us "
            + $"bind avg={DictInt(bindAiHelpers, "avg_usec")}us p95={DictInt(bindAiHelpers, "p95_usec")}us "
            + $"skill avg={DictInt(skill, "avg_usec")}us p95={DictInt(skill, "p95_usec")}us "
            + $"action avg={DictInt(action, "avg_usec")}us p95={DictInt(action, "p95_usec")}us "
            + $"assemble avg={DictInt(assemble, "avg_usec")}us "
            + $"move_band avg={DictInt(movementDistanceBand, "avg_usec")}us p95={DictInt(movementDistanceBand, "p95_usec")}us "
            + $"rebuild calls={DictInt(movementSnapshotRebuild, "call_count")}";
    }

    private static List<string> ResolveScenarioFilter()
    {
        string raw = ReadStringEnvironment(
            "AI_BASELINE_SCENARIOS",
            ReadStringEnvironment("BASELINE_SCENARIOS", "")
        );
        if (string.IsNullOrEmpty(raw))
            return new List<string> { "small_4v8", "medium_6v20", "large_6v40" };

        var ids = new List<string>();
        foreach (string token in raw.Split(','))
        {
            string id = token.Trim();
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }
        return ids;
    }

    private static List<string> ResolveCompareMetrics()
    {
        string raw = ReadStringEnvironment("AI_BASELINE_COMPARE_METRICS", "");
        if (string.IsNullOrEmpty(raw))
            return new List<string> { "avg_usec", "p50_usec", "p95_usec" };

        var metrics = new List<string>();
        foreach (string token in raw.Split(','))
        {
            string metric = token.Trim();
            if (!string.IsNullOrEmpty(metric))
                metrics.Add(metric);
        }
        return metrics.Count > 0
            ? metrics
            : new List<string> { "avg_usec", "p50_usec", "p95_usec" };
    }

    private static string ResolveOutputPath(bool updateBaseline)
    {
        if (updateBaseline)
            return ProjectSettings.GlobalizePath(BaselinePath);

        string raw = ReadStringEnvironment(
            "AI_BASELINE_OUTPUT_FILE",
            ReadStringEnvironment("OUTPUT_FILE", "")
        );
        if (!string.IsNullOrEmpty(raw))
            return raw;
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return ProjectSettings.GlobalizePath($"res://tmp/ai_baseline_snapshot_{timestamp}.json");
    }

    private static string ReadStringEnvironment(string name, string fallback)
    {
        string raw = OS.GetEnvironment(name).StripEdges();
        return string.IsNullOrEmpty(raw) ? fallback : raw;
    }

    private static int ReadIntEnvironment(string name, int fallback)
    {
        string raw = OS.GetEnvironment(name).StripEdges();
        return int.TryParse(raw, out int value) ? value : fallback;
    }

    private static double ReadDoubleEnvironment(string name, double fallback)
    {
        string raw = OS.GetEnvironment(name).StripEdges();
        return double.TryParse(raw, out double value) ? value : fallback;
    }

    private static bool ReadBoolEnvironment(string name, bool fallback)
    {
        string raw = OS.GetEnvironment(name).StripEdges().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw))
            return fallback;
        return raw == "1" || raw == "true" || raw == "yes" || raw == "on";
    }

    private static GDictionary DictDict(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Dictionary
            ? dict[key].AsGodotDictionary()
            : new GDictionary();

    private static int DictInt(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt32() : 0;

    private static long DictLong(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt64() : 0L;

    private sealed record ScenarioSpec(Vector2I MapSize, int AllyCount, int EnemyCount);

    private sealed class RunResult
    {
        public GDictionary ChooseStats { get; init; } = new();
        public GDictionary BindAiHelpersStats { get; init; } = new();
        public GDictionary SkillScoreStats { get; init; } = new();
        public GDictionary ActionScoreStats { get; init; } = new();
        public GDictionary AssemblerStats { get; init; } = new();
        public GDictionary MovementDistanceBandStats { get; init; } = new();
        public GDictionary MovementSnapshotRebuildStats { get; init; } = new();
        public GArray Hotspots { get; init; } = new();
        public int AiTurns { get; init; }
        public int ManualTurns { get; init; }
        public int Iterations { get; init; }
        public int TimelineSteps { get; init; }
        public int FinalTu { get; init; }
        public bool BattleEnded { get; init; }
        public string TerminationReason { get; init; } = "invalid_runtime";
        public string WinnerFactionId { get; init; } = "";
        public BattleMovementQueryService.CacheDiagnostics MovementCacheDiagnostics { get; init; }
        public bool TraceBalanced { get; init; }
        public bool TraceTruncated { get; init; }
        public double ElapsedSeconds { get; init; }
    }
}
