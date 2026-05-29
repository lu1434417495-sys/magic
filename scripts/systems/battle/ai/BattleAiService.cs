using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;

[GlobalClass]
public partial class BattleAiService : RefCounted
{
    private GDictionary _enemy_ai_brains = new();
    private BattleAiScoreService _score_service = new();
    private BattleAiStateResolver _state_resolver = new();
    private BattleAiDecisionEngine _decision_engine = new();

    public bool enable_mutation_guard { get; set; } = true;

    public void setup(GDictionary enemy_ai_brains = null, BattleDamageResolver damage_resolver = null)
    {
        _enemy_ai_brains = enemy_ai_brains ?? new GDictionary();
        _score_service.setup(damage_resolver);
    }

    public void set_score_profile(BattleAiScoreProfile profile)
    {
        _score_service.set_profile(profile ?? new BattleAiScoreProfile());
    }

    public BattleAiScoreProfile get_score_profile()
    {
        return _score_service.get_profile();
    }

    public BattleAiScoreService get_score_service()
    {
        return _score_service;
    }

    public BattleAiDecision choose_command(BattleAiContext context)
    {
        if (
            context == null
            || context.state == null
            || context.unit_state == null
            || context.grid_service == null
        )
        {
            return null;
        }

        context.mutation_guard_violations.Clear();

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
        List<string> violations = mutationGuard.ValidateAndRestoreTyped(context);
        AiTraceRecorder.exit("choose:mutation_guard_validate");
        if (violations.Count == 0)
        {
            return decision;
        }

        if (context is BattleAiContext aiContext)
        {
            aiContext.mutation_guard_violations = BattleAiMutationGuard.ToViolationArray(violations);
        }
        foreach (string violation in violations)
        {
            GameLog.Error($"AI mutation guard blocked decision: {violation}.", "ai.mutation_guard.blocked", "ai");
        }

        BattleUnitState unitState = context.unit_state;
        string unitLabel = unitState != null ? unitState.display_name : "unknown";
        string crashMessage =
            $"AI mutation guard blocked {unitLabel} 的决策；越权写入：{string.Join("; ", violations)}";
        GameLog.Error(crashMessage, "ai.decision.crash", "ai");
        return null;
    }

    public BattleAiDecision _choose_command_impl(BattleAiContext context)
    {
        context.skill_score_input_callback ??=
            (aiContext, skillDef, command, preview, effectDefs, metadata) =>
                _score_service.build_skill_score_input(
                    aiContext,
                    skillDef,
                    command,
                    preview,
                    effectDefs ?? new GArray(),
                    metadata ?? new GDictionary()
                );
        context.action_score_input_callback ??=
            (
                aiContext,
                actionKind,
                actionLabel,
                scoreBucketId,
                command,
                preview,
                metadata
            ) =>
                _score_service.build_action_score_input(
                    aiContext,
                    actionKind,
                    actionLabel,
                    scoreBucketId,
                    command,
                    preview,
                    metadata ?? new GDictionary()
                );

        BattleAiDecision decision = _decision_engine.choose_command_impl(
            context,
            _enemy_ai_brains,
            _state_resolver,
            _build_wait_decision,
            _score_service
        );
        return decision;
    }

    public void _trace_enter(StringName name)
    {
        AiTraceRecorder.enter(name);
    }

    public void _trace_exit(StringName name)
    {
        AiTraceRecorder.exit(name);
    }

    public bool _is_better_score_input(BattleAiScoreInput candidate, BattleAiScoreInput best_candidate)
    {
        return _decision_engine.is_better_score_input(candidate, best_candidate);
    }

    public BattleAiDecision _build_wait_decision(
        BattleAiContext context,
        StringName brain_id,
        StringName state_id,
        StringName action_id,
        string reason_text
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = context?.unit_state?.unit_id ?? new StringName(""),
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

}
