using Godot;

public sealed class BattleSimExecutionLoop
{
    private const int DefaultMaxIdleLoops = 25;
    private const int DefaultTimelineTicksPerStep = 1;

    private static readonly StringName DefaultManualPolicy = "wait";

    public BattleSimExecutionLoopResult Run(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleSimScenarioDef scenarioDef,
        int maxIdleLoops = DefaultMaxIdleLoops
    )
    {
        int maxIterations = Mathf.Max(scenarioDef?.max_iterations ?? 0, 0);
        StringName manualPolicy = scenarioDef?.manual_policy ?? DefaultManualPolicy;
        int timelineTicksPerStep = Mathf.Max(
            scenarioDef?.timeline_ticks_per_step ?? DefaultTimelineTicksPerStep,
            DefaultTimelineTicksPerStep
        );
        return Run(
            runtime,
            state,
            maxIterations,
            maxIdleLoops,
            manualPolicy,
            timelineTicksPerStep
        );
    }

    public BattleSimExecutionLoopResult Run(
        BattleRuntimeModule runtime,
        BattleState state,
        int maxIterations,
        int maxIdleLoops = DefaultMaxIdleLoops,
        StringName manualPolicy = default,
        int timelineTicksPerStep = DefaultTimelineTicksPerStep
    )
    {
        int iterations = 0;
        int idleLoops = 0;
        int timelineSteps = 0;
        bool stalled = false;
        maxIterations = Mathf.Max(maxIterations, 0);
        maxIdleLoops = Mathf.Max(maxIdleLoops, 1);
        timelineTicksPerStep = Mathf.Max(timelineTicksPerStep, DefaultTimelineTicksPerStep);
        if (manualPolicy == default || string.IsNullOrEmpty(manualPolicy.ToString()))
            manualPolicy = DefaultManualPolicy;
        bool traceLoop = OS.HasEnvironment("SIM_LOOP_TRACE")
            && OS.GetEnvironment("SIM_LOOP_TRACE").StripEdges() == "1";

        while (
            runtime != null
            && state != null
            && state.PhaseKind != BattlePhaseKind.BattleEnded
            && iterations < maxIterations
        )
        {
            iterations++;

            int previousTu = state.timeline?.current_tu ?? 0;
            BattlePhaseKind previousPhase = state.PhaseKind;
            StringName previousActiveUnitId = state.active_unit_id;

            if (traceLoop && (iterations <= 50 || iterations % 100 == 0))
            {
                GameLog.Debug(
                    $"[LoopTrace] before iteration={iterations} phase={state.phase} active={state.active_unit_id} tu={previousTu} ready={state.timeline?.ready_unit_ids.Count ?? 0}",
                    "battlesim.loop.trace_before",
                    "battlesim"
                );
            }
            AiTraceRecorder recorder = null;
            if (traceLoop)
            {
                recorder = new AiTraceRecorder();
                recorder.SetEventCaptureEnabled(false);
                AiTraceRecorder.SetInstance(recorder);
            }
            ulong advanceStartMsec = traceLoop ? Time.GetTicksMsec() : 0;
            BattleEventBatch batch = AdvanceStep(runtime, state, manualPolicy, timelineTicksPerStep);
            if (traceLoop && (iterations <= 50 || iterations % 100 == 0))
            {
                ulong advanceElapsedMsec = Time.GetTicksMsec() - advanceStartMsec;
                GameLog.Debug(
                    $"[LoopTrace] after iteration={iterations} phase={state.phase} active={state.active_unit_id} tu={state.timeline?.current_tu ?? previousTu} ready={state.timeline?.ready_unit_ids.Count ?? 0} batch_null={batch == null} advance_ms={advanceElapsedMsec}",
                    "battlesim.loop.trace_after",
                    "battlesim"
                );
                if (advanceElapsedMsec >= 250 && recorder != null)
                {
                    PrintTraceStats(recorder, iterations);
                }
            }
            if (traceLoop)
            {
                AiTraceRecorder.SetInstance(null);
            }

            int nextTu = state.timeline?.current_tu ?? previousTu;
            if (nextTu != previousTu)
                timelineSteps++;

            if (HasProgressed(state, batch, previousTu, previousPhase, previousActiveUnitId))
            {
                idleLoops = 0;
                continue;
            }

            idleLoops++;
            if (idleLoops >= maxIdleLoops)
            {
                stalled = true;
                break;
            }
        }

        return new BattleSimExecutionLoopResult
        {
            iterations = iterations,
            idle_loops = idleLoops,
            timeline_steps = timelineSteps,
            stalled = stalled,
        };
    }

    public BattleEventBatch AdvanceStep(
        BattleRuntimeModule runtime,
        BattleState state,
        StringName manualPolicy,
        int timelineTicksPerStep
    )
    {
        if (runtime == null || state == null)
            return null;

        if (state.PhaseKind == BattlePhaseKind.UnitActing)
        {
            BattleUnitState activeUnit = GetUnit(state, state.active_unit_id);
            if (
                activeUnit != null
                && activeUnit.is_alive
                && activeUnit.ControlModeKind == BattleUnitControlMode.Manual
            )
            {
                return IssueManualPolicy(runtime, manualPolicy, activeUnit.unit_id);
            }

            return runtime.advance(0);
        }

        if (HasReadyUnits(state))
            return runtime.advance(0);

        return runtime.advance(Mathf.Max(timelineTicksPerStep, DefaultTimelineTicksPerStep));
    }

    public bool HasReadyUnits(BattleState state)
    {
        return state?.timeline?.ready_unit_ids != null && state.timeline.ready_unit_ids.Count > 0;
    }

    private static bool HasProgressed(
        BattleState state,
        BattleEventBatch batch,
        int previousTu,
        BattlePhaseKind previousPhase,
        StringName previousActiveUnitId
    )
    {
        if (state == null)
            return false;
        if ((state.timeline?.current_tu ?? previousTu) != previousTu)
            return true;
        if (state.PhaseKind != previousPhase || state.active_unit_id != previousActiveUnitId)
            return true;
        if (batch == null)
            return false;
        return batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.ChangedUnitIdsTyped.Count > 0
            || batch.ChangedCoordsTyped.Count > 0
            || batch.LogLinesTyped.Count > 0
            || batch.ReportEntriesTyped.Count > 0
            || batch.ProgressionDeltasTyped.Count > 0;
    }

    private static BattleEventBatch IssueManualPolicy(
        BattleRuntimeModule runtime,
        StringName manualPolicy,
        StringName unitId
    )
    {
        var command = new BattleCommand
        {
            unit_id = unitId,
            CommandKind = BattleCommandKind.Wait,
        };

        switch (manualPolicy.ToString())
        {
            case "wait":
            default:
                return runtime.IssueCommand(command);
        }
    }

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        if (state?.units == null || !state.units.ContainsKey(unitId))
            return null;
        return state.units[unitId].AsGodotObject() as BattleUnitState;
    }

    private static void PrintTraceStats(AiTraceRecorder recorder, int iteration)
    {
        var stats = recorder?.GetFuncStats();
        if (stats == null || stats.Count == 0)
        {
            return;
        }
        var entries = new System.Collections.Generic.List<(string Name, long TotalUsec, long Calls)>();
        foreach (var key in stats.Keys)
        {
            var name = key.ToString();
            if (stats[key].VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            var row = stats[key].AsGodotDictionary();
            long totalUsec = ReadInt64(row, "total_usec", 0);
            long calls = ReadInt64(row, "ncalls", 0);
            entries.Add((name, totalUsec, calls));
        }
        entries.Sort((left, right) => right.TotalUsec.CompareTo(left.TotalUsec));
        int limit = Mathf.Min(entries.Count, 8);
        for (int index = 0; index < limit; index++)
        {
            var entry = entries[index];
                GameLog.Debug(
                    $"[LoopTraceStats] iteration={iteration} rank={index + 1} name={entry.Name} total_ms={entry.TotalUsec / 1000.0:F1} calls={entry.Calls}",
                    "battlesim.loop.trace_stats",
                    "battlesim"
                );
        }
    }

    private static long ReadInt64(
        Godot.Collections.Dictionary source,
        string key,
        long fallback = 0
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;

        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
    }
}

public sealed class BattleSimExecutionLoopResult
{
    public int iterations;
    public int idle_loops;
    public int timeline_steps;
    public bool stalled;
}
