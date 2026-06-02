using System.Collections.Generic;
using Godot;

public sealed class BattleAiService
{
    private readonly Dictionary<StringName, EnemyAiBrainDef> _enemyAiBrains = new();
    private readonly BattleAiScoreService _scoreService = new();
    private readonly BattleAiStateResolver _stateResolver = new();
    private readonly BattleAiDecisionEngine _decisionEngine = new();

    public bool EnableMutationGuard { get; set; } = true;

    public void Setup(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains = null,
        BattleDamageResolver damageResolver = null
    )
    {
        _enemyAiBrains.Clear();
        if (enemyAiBrains != null)
        {
            foreach (KeyValuePair<StringName, EnemyAiBrainDef> entry in enemyAiBrains)
            {
                if (IsEmpty(entry.Key) || entry.Value == null)
                {
                    continue;
                }
                _enemyAiBrains[entry.Key] = entry.Value;
            }
        }
        _scoreService.Setup(damageResolver);
    }

    public void SetScoreProfile(BattleAiScoreProfile profile)
    {
        _scoreService.SetProfile(profile ?? new BattleAiScoreProfile());
    }

    public BattleAiScoreProfile GetScoreProfile()
    {
        return _scoreService.GetProfile();
    }

    internal BattleAiScoreService GetScoreService()
    {
        return _scoreService;
    }

    public BattleAiDecision ChooseCommand(BattleAiContext context)
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

        if (!EnableMutationGuard)
        {
            AiTraceRecorder.enter("choose:impl");
            BattleAiDecision decisionNoGuard = ChooseCommandImpl(context);
            AiTraceRecorder.exit("choose:impl");
            return decisionNoGuard;
        }

        BattleAiMutationGuard mutationGuard = new();
        AiTraceRecorder.enter("choose:mutation_guard_capture");
        mutationGuard.capture(context);
        AiTraceRecorder.exit("choose:mutation_guard_capture");

        AiTraceRecorder.enter("choose:impl");
        BattleAiDecision decision = ChooseCommandImpl(context);
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

    private BattleAiDecision ChooseCommandImpl(BattleAiContext context)
    {
        context.skill_score_input_callback ??=
            (aiContext, skillDef, command, preview, effectDefs, metadata) =>
                _scoreService.BuildSkillScoreInput(
                    aiContext,
                    skillDef,
                    command,
                    preview,
                    effectDefs ?? new Godot.Collections.Array(),
                    metadata ?? new Godot.Collections.Dictionary()
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
                _scoreService.BuildActionScoreInput(
                    aiContext,
                    actionKind,
                    actionLabel,
                    scoreBucketId,
                    command,
                    preview,
                    metadata ?? new Godot.Collections.Dictionary()
                );

        BattleAiDecision decision = _decisionEngine.ChooseCommandImpl(
            context,
            _enemyAiBrains,
            _stateResolver,
            BuildWaitDecision,
            _scoreService
        );
        return decision;
    }

    private static BattleAiDecision BuildWaitDecision(
        BattleAiContext context,
        StringName brainId,
        StringName stateId,
        StringName actionId,
        string reasonText
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
            brain_id = brainId,
            state_id = stateId,
            action_id = actionId,
            reason_text = reasonText,
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
