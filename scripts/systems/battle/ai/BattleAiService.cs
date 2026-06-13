using System.Collections.Generic;
using Godot;

internal sealed class BattleAiService
{
    private readonly Dictionary<StringName, EnemyAiBrainDef> _enemyAiBrains = new();
    private readonly BattleAiScoreService _scoreService = new();
    private readonly BattleAiStateResolver _stateResolver = new();
    private readonly BattleAiDecisionEngine _decisionEngine = new();

    internal bool EnableMutationGuard { get; set; } = true;

    internal void Setup(
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

    internal void SetScoreProfile(BattleAiScoreProfile profile)
    {
        _scoreService.SetProfile(profile ?? new BattleAiScoreProfile());
    }

    internal BattleAiScoreProfile GetScoreProfile()
    {
        return _scoreService.GetProfile();
    }

    internal BattleAiScoreService GetScoreService()
    {
        return _scoreService;
    }

    internal BattleAiDecision ChooseCommand(BattleAiContext context)
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

        _scoreService.BeginDecisionScope(
            context.state,
            context.unit_state,
            ((IBattleAiScoreContext)context).skill_defs
        );
        try
        {
            context.ClearMutationGuardViolations();

            if (!EnableMutationGuard)
            {
                AiTraceRecorder.Enter("choose:impl");
                BattleAiDecision decisionNoGuard = ChooseCommandImpl(context);
                AiTraceRecorder.Exit("choose:impl");
                return decisionNoGuard;
            }

            BattleAiMutationGuard mutationGuard = new();
            AiTraceRecorder.Enter("choose:mutation_guard_capture");
            mutationGuard.Capture(context);
            AiTraceRecorder.Exit("choose:mutation_guard_capture");

            AiTraceRecorder.Enter("choose:impl");
            BattleAiDecision decision = ChooseCommandImpl(
                context,
                BuildActionMutationCheckpoint()
            );
            AiTraceRecorder.Exit("choose:impl");

            AiTraceRecorder.Enter("choose:mutation_guard_validate");
            BattleAiMutationViolationReport report =
                mutationGuard.ValidateAndRestoreReportTyped(
                    context,
                    "decision",
                    callSite: "BattleAiService.ChooseCommandImpl"
                );
            AiTraceRecorder.Exit("choose:mutation_guard_validate");
            if (report == null)
            {
                return decision;
            }

            AbortMutationViolation(context, report);
            return null;
        }
        finally
        {
            _scoreService.EndDecisionScope();
        }
    }

    private BattleAiDecision ChooseCommandImpl(
        BattleAiContext context,
        BattleAiActionMutationCheckpoint mutationCheckpoint = null
    )
    {
        context.skill_score_input_callback ??=
            (aiContext, skillDef, command, preview, effectDefs, metadata) =>
                _scoreService.BuildSkillScoreInput(
                    aiContext,
                    skillDef,
                    command,
                    preview,
                    effectDefs ?? System.Array.Empty<CombatEffectDef>(),
                    metadata
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
                    metadata
                );

        BattleAiDecision decision = _decisionEngine.ChooseCommandImpl(
            context,
            _enemyAiBrains,
            _stateResolver,
            BuildWaitDecision,
            _scoreService,
            mutationCheckpoint
        );
        return decision;
    }

    private BattleAiActionMutationCheckpoint BuildActionMutationCheckpoint()
    {
        BattleAiMutationGuard actionMutationGuard = null;
        return (context, action, actionIndex, stage) =>
        {
            if (stage == "before_action")
            {
                actionMutationGuard = new BattleAiMutationGuard();
                actionMutationGuard.Capture(context);
                return;
            }
            if (stage != "after_action" || actionMutationGuard == null)
            {
                return;
            }

            BattleAiMutationViolationReport report =
                actionMutationGuard.ValidateAndRestoreReportTyped(
                    context,
                    "action",
                    action,
                    actionIndex,
                    BattleAiMutationViolationReport.BuildActionCallSite(action, actionIndex)
                );
            actionMutationGuard = null;
            if (report != null)
            {
                AbortMutationViolation(context, report);
            }
        };
    }

    private static void AbortMutationViolation(
        BattleAiContext context,
        BattleAiMutationViolationReport report
    )
    {
        if (report == null)
        {
            return;
        }

        context?.SetMutationGuardViolations(report.Violations);
        BattleAiFailurePolicy.ReportMutationViolation(report.Message, report.ToMetadata());
        throw new BattleAiMutationViolationException(report);
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
            CommandKind = BattleCommandKind.Wait,
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
