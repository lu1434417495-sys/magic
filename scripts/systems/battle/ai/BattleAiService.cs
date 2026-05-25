using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiService : RefCounted
{
    private static readonly GDScript BattleAiScoreServiceScript = GD.Load<GDScript>("res://scripts/systems/battle/ai/battle_ai_score_service.gd");

    private GDictionary _enemy_ai_brains = new();
    private GodotObject _score_service = BattleAiScoreServiceScript.New().AsGodotObject();
    private BattleAiStateResolver _state_resolver = new();
    private BattleAiDecisionEngine _decision_engine = new();

    public bool enable_mutation_guard { get; set; } = true;

    public void setup(GDictionary enemy_ai_brains = null, GodotObject damage_resolver = null)
    {
        _enemy_ai_brains = enemy_ai_brains ?? new GDictionary();
        _score_service.Call("setup", damage_resolver);
    }

    public void set_score_profile(BattleAiScoreProfile profile)
    {
        _score_service.Call("set_profile", profile ?? new BattleAiScoreProfile());
    }

    public BattleAiScoreProfile get_score_profile()
    {
        return _score_service.Call("get_profile").AsGodotObject() as BattleAiScoreProfile;
    }

    public GodotObject get_score_service()
    {
        return _score_service;
    }

    public BattleAiDecision choose_command(GodotObject context)
    {
        if (context == null
            || GetObject(context, "state") == null
            || GetObject(context, "unit_state") == null
            || GetObject(context, "grid_service") == null)
        {
            return null;
        }

        if (context is BattleAiContext typedContext)
        {
            typedContext.mutation_guard_violations.Clear();
        }

        if (!enable_mutation_guard)
        {
            AiTraceRecorder.enter("choose:impl");
            BattleAiDecision decisionNoGuard = _choose_command_impl(context);
            AiTraceRecorder.exit("choose:impl");
            return decisionNoGuard;
        }

        BattleAiMutationGuard mutationGuard = new();
        AiTraceRecorder.enter("choose:mutation_guard_capture");
        mutationGuard.capture(context);
        AiTraceRecorder.exit("choose:mutation_guard_capture");

        AiTraceRecorder.enter("choose:impl");
        BattleAiDecision decision = _choose_command_impl(context);
        AiTraceRecorder.exit("choose:impl");

        AiTraceRecorder.enter("choose:mutation_guard_validate");
        GArray violations = mutationGuard.validate_and_restore(context);
        AiTraceRecorder.exit("choose:mutation_guard_validate");
        if (violations.Count == 0)
        {
            return decision;
        }

        if (context is BattleAiContext aiContext)
        {
            aiContext.mutation_guard_violations = violations.Duplicate();
        }
        foreach (Variant violation in violations)
        {
            GD.PushError($"AI mutation guard blocked decision: {violation}");
        }

        GodotObject unitState = GetObject(context, "unit_state");
        string unitLabel = unitState != null ? GetString(unitState, "display_name") : "unknown";
        string crashMessage = $"AI mutation guard blocked {unitLabel} 的决策；越权写入：{string.Join("; ", ToStringArray(violations))}";
        GD.PushError(crashMessage);
        return null;
    }

    public BattleAiDecision _choose_command_impl(GodotObject context)
    {
        if (context is BattleAiContext typedContext)
        {
            if (!IsCallableValid(typedContext.skill_score_input_callback))
            {
                typedContext.skill_score_input_callback = new Callable(this, "build_skill_score_input");
            }
            if (!IsCallableValid(typedContext.action_score_input_callback))
            {
                typedContext.action_score_input_callback = new Callable(this, "build_action_score_input");
            }
        }

        GodotObject decision = _decision_engine.choose_command_impl(
            context,
            _enemy_ai_brains,
            _state_resolver,
            new Callable(this, "_build_wait_decision"),
            _score_service,
            new Callable(this, "_trace_enter"),
            new Callable(this, "_trace_exit"),
            AiTraceRecorder.has_instance());
        return decision as BattleAiDecision;
    }

    public void _trace_enter(StringName name)
    {
        AiTraceRecorder.enter(name);
    }

    public void _trace_exit(StringName name)
    {
        AiTraceRecorder.exit(name);
    }

    public GodotObject build_skill_score_input(
        GodotObject context,
        SkillDef skill_def,
        GodotObject command,
        GodotObject preview,
        GArray effect_defs = null,
        GDictionary metadata = null)
    {
        return _score_service.Call(
            "build_skill_score_input",
            context,
            skill_def,
            command,
            preview,
            effect_defs ?? new GArray(),
            metadata ?? new GDictionary()).AsGodotObject();
    }

    public GodotObject build_action_score_input(
        GodotObject context,
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        GodotObject command,
        GodotObject preview,
        GDictionary metadata = null)
    {
        return _score_service.Call(
            "build_action_score_input",
            context,
            action_kind,
            action_label,
            score_bucket_id,
            command,
            preview,
            metadata ?? new GDictionary()).AsGodotObject();
    }

    public bool _is_better_score_input(GodotObject candidate, GodotObject best_candidate)
    {
        return _decision_engine.is_better_score_input(candidate, best_candidate);
    }

    public BattleAiDecision _build_wait_decision(
        GodotObject context,
        StringName brain_id,
        StringName state_id,
        StringName action_id,
        string reason_text)
    {
        GodotObject unitState = GetObject(context, "unit_state");
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = GetStringName(unitState, "unit_id"),
        };
        return new BattleAiDecision
        {
            command = command,
            brain_id = brain_id,
            state_id = state_id,
            action_id = action_id,
            reason_text = reason_text,
        };
    }

    private static bool IsCallableValid(Callable callable)
    {
        return !callable.Equals(default(Callable)) && !string.IsNullOrEmpty(callable.Method.ToString());
    }

    private static string[] ToStringArray(GArray values)
    {
        var result = new string[values.Count];
        for (int i = 0; i < values.Count; i += 1)
        {
            result[i] = values[i].ToString();
        }
        return result;
    }
}
