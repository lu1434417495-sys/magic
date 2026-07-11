using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_ai_performance_baseline : LifecycleTestSceneTree
{
    private const int ActionThreshold = 120;
    private const int TimelineTicksPerStep = 1;
    private const int TimelineTuPerTick = 5;
    private const int LegacyTargetTu = 200;
    private const int DefaultRepeatCount = 2;
    private const int DefaultMaxIterations = 100000;
    private const int MaxIdleLoops = 25;
    private const string BaselinePath = "res://tests/battle_runtime/benchmarks/baselines/ai_baseline.json";
    private const string MutationGuardAbortProcessSetting = "battle_ai/fail_loud_abort_process";

    private readonly TestHarness _test = new();
    private readonly List<GameSession> _sessionKeepAlive = new();
    private readonly List<BattleRuntimeModule> _runtimeKeepAlive = new();
    private readonly Dictionary<string, ScenarioSpec> _scenarios =
        new()
        {
            ["small_4v8"] = new ScenarioSpec(new Vector2I(20, 14), 4, 8),
            ["medium_6v20"] = new ScenarioSpec(new Vector2I(20, 14), 6, 20),
            ["large_6v40"] = new ScenarioSpec(new Vector2I(20, 14), 6, 40),
        };

    public override void _Initialize()
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

        GD.Print(
            $"[AiBaseline] config update={updateBaseline} repeat={repeatCount} measured={repeatCount - 1} completion=battle_ended max_iterations={maxIterations} require_completed={requireCompleted} ai_mutation_guard={aiMutationGuardEnabled} ai_mutation_guard_abort_process={aiMutationGuardAbortProcess} tolerance_pct={tolerancePct:F1} absolute_tolerance_usec={absoluteToleranceUsec} min_metric_call_count={minMetricCallCount} min_percentile_call_count={minPercentileCallCount} compare_metrics={string.Join(",", compareMetrics)} output={outputPath}"
        );

        var scenariosDoc = new GDictionary();
        foreach (string scenarioId in scenarioIds)
        {
            if (!_scenarios.TryGetValue(scenarioId, out ScenarioSpec spec))
            {
                GD.Print($"[AiBaseline] WARN scenario '{scenarioId}' is not defined, skipping.");
                continue;
            }

            GD.Print(
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
            GD.Print(FormatScenarioSummary(scenarioId, scenarioResult));
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
                GD.PushError($"[AiBaseline] failed to write baseline at {outputPath}");
                return 1;
            }
            GD.Print($"[AiBaseline] wrote baseline {outputPath}");
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
            GD.Print(report);
        }
        else
        {
            currentDoc["baseline_diff_report"] = $"[AiBaseline] no baseline found at {BaselinePath}.";
            currentDoc["baseline_regressions"] = 0;
        }

        if (!AiBaselineDiff.WriteBaseline(outputPath, currentDoc))
        {
            GD.PushError($"[AiBaseline] failed to write snapshot at {outputPath}");
            return 1;
        }
        GD.Print($"[AiBaseline] wrote snapshot {outputPath}");
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
        var perRunSkill = new GArray();
        var perRunAction = new GArray();
        var perRunAssemble = new GArray();
        var perRunMovementDistanceBand = new GArray();
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
            GD.Print(
                $"[AiBaseline]   run {runIndex + 1}/{repeatCount} ({phase}): ai_turns={runResult.AiTurns} manual_turns={runResult.ManualTurns} final_tu={runResult.FinalTu} iterations={runResult.Iterations} ended={runResult.BattleEnded} winner={runResult.WinnerFactionId} elapsed={runResult.ElapsedSeconds:F2}s"
            );

            if (requireCompleted && !runResult.BattleEnded)
            {
                _test.Fail(
                    $"{scenarioId} {phase} run did not finish before max_iterations={maxIterations} (final_tu={runResult.FinalTu}, iterations={runResult.Iterations})."
                );
            }

            if (!measured)
                continue;

            perRunChoose.Add(runResult.ChooseStats);
            perRunSkill.Add(runResult.SkillScoreStats);
            perRunAction.Add(runResult.ActionScoreStats);
            perRunAssemble.Add(runResult.AssemblerStats);
            perRunMovementDistanceBand.Add(runResult.MovementDistanceBandStats);
            perRunMeta.Add(
                new GDictionary
                {
                    ["ai_turns"] = runResult.AiTurns,
                    ["manual_turns"] = runResult.ManualTurns,
                    ["iterations"] = runResult.Iterations,
                    ["timeline_steps"] = runResult.TimelineSteps,
                    ["final_tu"] = runResult.FinalTu,
                    ["battle_ended"] = runResult.BattleEnded,
                    ["winner"] = runResult.WinnerFactionId,
                    ["elapsed_seconds"] = runResult.ElapsedSeconds,
                    ["trace_balanced"] = runResult.TraceBalanced,
                    ["trace_truncated"] = runResult.TraceTruncated,
                    ["hotspots"] = runResult.Hotspots,
                }
            );
        }

        GDictionary mergedChoose = AiBaselineDiff.MergeRuns(perRunChoose);
        GDictionary mergedSkill = AiBaselineDiff.MergeRuns(perRunSkill);
        GDictionary mergedAction = AiBaselineDiff.MergeRuns(perRunAction);
        GDictionary mergedAssemble = AiBaselineDiff.MergeRuns(perRunAssemble);
        GDictionary mergedMovementDistanceBand =
            AiBaselineDiff.MergeRuns(perRunMovementDistanceBand);

        GDictionary summaryChoose = AiBaselineDiff.SummarizeStats(mergedChoose);
        GDictionary summarySkill = AiBaselineDiff.SummarizeStats(mergedSkill);
        GDictionary summaryAction = AiBaselineDiff.SummarizeStats(mergedAction);
        GDictionary summaryAssemble = AiBaselineDiff.SummarizeStats(mergedAssemble);
        GDictionary summaryMovementDistanceBand =
            AiBaselineDiff.SummarizeStats(mergedMovementDistanceBand);

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
                ["build_skill_score_input"] = summarySkill,
                ["build_action_score_input"] = summaryAction,
                ["build_unit_action_plan"] = summaryAssemble,
                ["movement_distance_band_path_targets"] = summaryMovementDistanceBand,
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

        try
        {
            GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
            _sessionKeepAlive.Add(gameSession);
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            BattleRuntimeModule runtime = new();
            _runtimeKeepAlive.Add(runtime);
            runtime.setup(
                skill_definitions: catalog.GetSkillDefinitionsTyped(),
                enemy_templates: catalog.GetEnemyTemplatesTyped(),
                enemy_ai_brains: catalog.GetEnemyAiBrainsTyped(),
                item_defs: catalog.GetItemDefsTyped(),
                skill_catalog: catalog.GetSkillCatalogTyped(),
                battle_special_profile_view: catalog.GetBattleSpecialProfileView()
            );
            runtime._ai_service.EnableMutationGuard = aiMutationGuardEnabled;

            state = BuildFlatState(spec.MapSize, scenarioId);
            PopulateUnits(runtime, state, spec);
            runtime.SetupStateForTests(state);
            runtime._initialize_unit_trait_hooks();
            runtime._initialize_unit_action_thresholds();
            runtime._battle_rating_system.InitializeBattleRatingStats();
            runtime._fate_runtime.BeginBattle(runtime.calamity_by_member_id);
            runtime._initialize_battle_metrics();
            state.PhaseKind = BattlePhaseKind.TimelineRunning;
            state.active_unit_id = "";
            state.winner_faction_id = "";
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
                    if (activeUnit == null || !activeUnit.is_alive)
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
            GDictionary stats = recorder?.GetFuncStats() ?? new GDictionary();
            return new RunResult
            {
                ChooseStats = ExtractStats(stats, "advance:choose_command"),
                SkillScoreStats = ExtractStats(stats, "build_skill_score_input"),
                ActionScoreStats = ExtractStats(stats, "build_action_score_input"),
                AssemblerStats = ExtractStats(stats, "build_unit_action_plan"),
                MovementDistanceBandStats = ExtractStats(
                    stats,
                    "movement:distance_band_path_targets"
                ),
                Hotspots = BuildHotspotSummary(stats),
                AiTurns = aiTurns,
                ManualTurns = manualTurns,
                Iterations = iterations,
                TimelineSteps = timelineSteps,
                FinalTu = state.timeline.current_tu,
                BattleEnded = state.PhaseKind == BattlePhaseKind.BattleEnded,
                WinnerFactionId = state.winner_faction_id.ToString(),
                TraceBalanced = recorder == null || recorder.AssertBalanced(),
                TraceTruncated = recorder != null && recorder.IsTruncated(),
                ElapsedSeconds = (Time.GetTicksMsec() - startMsec) / 1000.0,
            };
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
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

    private void PopulateUnits(BattleRuntimeModule runtime, BattleState state, ScenarioSpec spec)
    {
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
            BattleUnitState enemy =
                index % 2 == 0
                    ? BuildAiUnit(
                        new StringName($"ai_baseline_enemy_{index + 1:00}"),
                        $"Wolf {index + 1:00}",
                        coord,
                        "melee_aggressor",
                        new[] { new StringName("charge"), new StringName("warrior_heavy_strike") }
                    )
                    : BuildAiUnit(
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
            AddUnitToState(runtime, state, enemy, isEnemy: true);
        }
    }

    private static BattleUnitState BuildManualUnit(StringName unitId, string displayName, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "player",
            control_mode = "manual",
            current_hp = 260,
            current_mp = 120,
            current_stamina = 120,
            current_aura = 120,
            current_ap = 2,
            action_threshold = ActionThreshold,
            is_alive = true,
        };
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
            current_hp = 180,
            current_mp = 120,
            current_stamina = 120,
            current_aura = 120,
            current_ap = 2,
            action_threshold = ActionThreshold,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        SetCoreAttributes(unit, hpMax: 180, attackBonus: 16, armorClass: 18);
        unit.known_active_skill_ids = new GStringNameArray();
        foreach (StringName skillId in skillIds ?? Array.Empty<StringName>())
        {
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = 1;
        }
        return unit;
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

        if (!runtime._grid_service.PlaceUnit(state, unit, unit.coord, true))
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
        GDictionary skill = DictDict(layers, "build_skill_score_input");
        GDictionary action = DictDict(layers, "build_action_score_input");
        GDictionary assemble = DictDict(layers, "build_unit_action_plan");
        GDictionary movementDistanceBand = DictDict(layers, "movement_distance_band_path_targets");
        return "[AiBaseline] "
            + $"{scenarioId} measured={DictInt(scenario, "repeat_measured")} ai_turns={DictInt(scenario, "ai_turns_total")} "
            + $"choose avg={DictInt(choose, "avg_usec")}us p50={DictInt(choose, "p50_usec")}us p95={DictInt(choose, "p95_usec")}us max={DictInt(choose, "max_usec")}us "
            + $"skill avg={DictInt(skill, "avg_usec")}us p95={DictInt(skill, "p95_usec")}us "
            + $"action avg={DictInt(action, "avg_usec")}us p95={DictInt(action, "p95_usec")}us "
            + $"assemble avg={DictInt(assemble, "avg_usec")}us "
            + $"move_band avg={DictInt(movementDistanceBand, "avg_usec")}us p95={DictInt(movementDistanceBand, "p95_usec")}us";
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
        public GDictionary SkillScoreStats { get; init; } = new();
        public GDictionary ActionScoreStats { get; init; } = new();
        public GDictionary AssemblerStats { get; init; } = new();
        public GDictionary MovementDistanceBandStats { get; init; } = new();
        public GArray Hotspots { get; init; } = new();
        public int AiTurns { get; init; }
        public int ManualTurns { get; init; }
        public int Iterations { get; init; }
        public int TimelineSteps { get; init; }
        public int FinalTu { get; init; }
        public bool BattleEnded { get; init; }
        public string WinnerFactionId { get; init; } = "";
        public bool TraceBalanced { get; init; }
        public bool TraceTruncated { get; init; }
        public double ElapsedSeconds { get; init; }
    }
}
