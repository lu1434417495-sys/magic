using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSimExecutionLoop : RefCounted
{
    private const int DefaultMaxIdleLoops = 25;
    private const int DefaultTimelineTicksPerStep = 1;
    private static readonly StringName DefaultManualPolicy = "wait";

    public Dictionary run(GodotObject runtime, GodotObject state, GodotObject scenario_def, Dictionary options)
    {
        return Run(runtime, state, scenario_def, options);
    }

    public void advance_step(GodotObject runtime, GodotObject state, StringName manual_policy, int timeline_ticks_per_step)
    {
        AdvanceStep(runtime, state, manual_policy, timeline_ticks_per_step);
    }

    public bool has_ready_units(GodotObject state)
    {
        return HasReadyUnits(state);
    }

    public string build_progress_signature(GodotObject state)
    {
        return BuildProgressSignature(state);
    }

    public Dictionary Run(GodotObject runtime, GodotObject state, GodotObject scenarioDef, Dictionary options)
    {
        int iterations = 0;
        int idleLoops = 0;
        int timelineSteps = 0;
        bool stalled = false;
        int maxIterations = ResolveMaxIterations(scenarioDef, options);
        int maxIdleLoops = ResolveMaxIdleLoops(options);
        StringName manualPolicy = ResolveManualPolicy(scenarioDef, options);
        int timelineTicksPerStep = ResolveTimelineTicksPerStep(scenarioDef, options);
        int progressIterationInterval = Mathf.Max(DictionaryGet(options, "progress_iteration_interval", 0).AsInt32(), 0);
        Callable progressCallback = DictionaryGet(options, "progress_callback", new Callable()).AsCallable();
        Dictionary progressContext = DictionaryGet(options, "progress_context", new Dictionary()).VariantType == Variant.Type.Dictionary
            ? DictionaryGet(options, "progress_context", new Dictionary()).AsGodotDictionary()
            : new Dictionary();

        while (state != null && state.Get("phase").AsStringName() != "battle_ended" && iterations < maxIterations)
        {
            iterations++;
            var timeline = state.Get("timeline").AsGodotObject();
            int previousTu = timeline != null ? timeline.Get("current_tu").AsInt32() : 0;
            string previousSignature = BuildProgressSignature(state);
            AdvanceStep(runtime, state, manualPolicy, timelineTicksPerStep);
            timeline = state != null ? state.Get("timeline").AsGodotObject() : null;
            int nextTu = state != null && timeline != null ? timeline.Get("current_tu").AsInt32() : previousTu;
            if (nextTu != previousTu)
                timelineSteps++;

            if (progressIterationInterval > 0 && !progressCallback.Equals(default(Callable)) && iterations % progressIterationInterval == 0)
            {
                progressCallback.Call(new Dictionary
                {
                    ["iterations"] = iterations,
                    ["idle_loops"] = idleLoops,
                    ["timeline_steps"] = timelineSteps,
                    ["state"] = state,
                    ["context"] = progressContext,
                });
            }

            string nextSignature = BuildProgressSignature(state);
            if (previousSignature == nextSignature)
            {
                idleLoops++;
                if (idleLoops >= maxIdleLoops)
                {
                    stalled = true;
                    break;
                }
            }
            else
            {
                idleLoops = 0;
            }
        }

        return new Dictionary
        {
            ["iterations"] = iterations,
            ["idle_loops"] = idleLoops,
            ["timeline_steps"] = timelineSteps,
            ["stalled"] = stalled,
        };
    }

    public void AdvanceStep(GodotObject runtime, GodotObject state, StringName manualPolicy, int timelineTicksPerStep)
    {
        if (runtime == null || state == null)
            return;

        var phase = state.Get("phase").AsStringName();
        if (phase == "unit_acting")
        {
            var units = state.Get("units").AsGodotDictionary();
            var activeUnitId = state.Get("active_unit_id").AsStringName();
            var activeUnit = DictionaryGet(units, activeUnitId, default).AsGodotObject();
            if (activeUnit != null && activeUnit.Get("is_alive").AsBool() && activeUnit.Get("control_mode").AsStringName() == "manual")
            {
                IssueManualPolicy(runtime, manualPolicy, activeUnitId);
            }
            else
            {
                runtime.Call("advance", 0);
            }
            return;
        }

        if (HasReadyUnits(state))
        {
            runtime.Call("advance", 0);
            return;
        }

        runtime.Call("advance", Mathf.Max(timelineTicksPerStep, DefaultTimelineTicksPerStep));
    }

    public bool HasReadyUnits(GodotObject state)
    {
        if (state == null)
            return false;
        var timeline = state.Get("timeline").AsGodotObject();
        if (timeline == null)
            return false;
        var readyUnitIds = timeline.Get("ready_unit_ids").AsGodotArray();
        return readyUnitIds.Count > 0;
    }

    public string BuildProgressSignature(GodotObject state)
    {
        if (state == null)
            return "";

        var units = state.Get("units").AsGodotDictionary();
        var timeline = state.Get("timeline").AsGodotObject();
        var unitParts = new System.Collections.Generic.List<string>();

        foreach (var unitIdStr in ProgressionDataUtils.sorted_string_keys(units))
        {
            var unitState = DictionaryGet(units, (StringName)unitIdStr, default).AsGodotObject();
            if (unitState == null)
                continue;
            var coord = unitState.Get("coord").AsVector2I();
            unitParts.Add(string.Format("{0}:{1},{2}:{3}:{4}:{5}:{6}:{7}",
                unitIdStr,
                coord.X, coord.Y,
                unitState.Get("is_alive").AsBool() ? 1 : 0,
                unitState.Get("current_hp").AsInt32(),
                unitState.Get("current_ap").AsInt32(),
                unitState.Get("current_stamina").AsInt32(),
                unitState.Get("current_move_points").AsInt32()));
        }

        return string.Format("{0}|{1}|{2}|{3}|{4}",
            state.Get("phase").AsStringName().ToString(),
            state.Get("active_unit_id").AsStringName().ToString(),
            state.Get("winner_faction_id").AsStringName().ToString(),
            timeline != null ? timeline.Get("current_tu").AsInt32() : 0,
            string.Join(";", unitParts));
    }

    private void IssueManualPolicy(GodotObject runtime, StringName manualPolicy, StringName unitId)
    {
        var command = new BattleCommand();
        command.unit_id = unitId;
        command.command_type = BattleCommand.TYPE_WAIT();
        switch (manualPolicy.ToString())
        {
            case "wait":
            default:
                runtime.Call("issue_command", command);
                break;
        }
    }

    private int ResolveMaxIterations(GodotObject scenarioDef, Dictionary options)
    {
        if (options.ContainsKey("max_iterations"))
            return Mathf.Max(DictionaryGet(options, "max_iterations", 0).AsInt32(), 0);
        return scenarioDef != null ? Mathf.Max(scenarioDef.Get("max_iterations").AsInt32(), 0) : 0;
    }

    private int ResolveMaxIdleLoops(Dictionary options)
    {
        return Mathf.Max(DictionaryGet(options, "max_idle_loops", DefaultMaxIdleLoops).AsInt32(), 1);
    }

    private StringName ResolveManualPolicy(GodotObject scenarioDef, Dictionary options)
    {
        if (options.ContainsKey("manual_policy"))
            return ProgressionDataUtils.to_string_name(DictionaryGet(options, "manual_policy", DefaultManualPolicy));
        return scenarioDef != null ? ProgressionDataUtils.to_string_name(scenarioDef.Get("manual_policy")) : DefaultManualPolicy;
    }

    private int ResolveTimelineTicksPerStep(GodotObject scenarioDef, Dictionary options)
    {
        int value = DefaultTimelineTicksPerStep;
        if (options.ContainsKey("timeline_ticks_per_step"))
            value = DictionaryGet(options, "timeline_ticks_per_step", DefaultTimelineTicksPerStep).AsInt32();
        else if (scenarioDef != null)
            value = scenarioDef.Get("timeline_ticks_per_step").AsInt32();
        return Mathf.Max(value, DefaultTimelineTicksPerStep);
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary != null && dictionary.ContainsKey(key))
            return dictionary[key];
        return fallback;
    }
}
