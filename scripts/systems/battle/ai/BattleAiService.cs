using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiService : IDisposable
{
    private readonly Dictionary<StringName, EnemyAiBrainDefinition> _enemyAiBrains = new();
    private readonly BattleAiScoreService _scoreService = new();
    private readonly BattleAiStateResolver _stateResolver = new();
    private readonly BattleAiDecisionEngine _decisionEngine = new();
    private bool _disposed;

    internal BattleAiMutationGuardMode MutationGuardMode { get; set; } =
        BattleAiMutationGuardMode.Disabled;

    internal void Setup(
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains = null,
        BattleDamageResolver damageResolver = null
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _enemyAiBrains.Clear();
        Dictionary<StringName, BattleAiScoreProfileDefinition> brainProfiles = new();
        if (enemyAiBrains != null)
        {
            foreach (KeyValuePair<StringName, EnemyAiBrainDefinition> entry in enemyAiBrains)
            {
                if (IsEmpty(entry.Key) || entry.Value == null)
                {
                    continue;
                }
                _enemyAiBrains[entry.Key] = entry.Value;
                if (entry.Value.ScoreProfile != null)
                {
                    brainProfiles[entry.Key] = entry.Value.ScoreProfile;
                }
            }
        }
        _scoreService.Setup(damageResolver);
        _scoreService.SetBrainProfiles(brainProfiles);
    }

    internal void SetScoreProfile(BattleAiScoreProfileDefinition profile)
    {
        _scoreService.SetProfile(profile);
    }

    internal void SetFactionScoreProfiles(
        IReadOnlyDictionary<StringName, BattleAiScoreProfileDefinition> profiles
    )
    {
        _scoreService.SetFactionProfiles(profiles);
    }

    internal BattleAiScoreProfileDefinition GetScoreProfile()
    {
        return _scoreService.GetProfile();
    }

    internal BattleAiScoreService GetScoreService()
    {
        return _scoreService;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _enemyAiBrains.Clear();
        _scoreService.Dispose();
    }

    internal BattleAiDecisionResult ChooseCommand(BattleAiContext context, bool captureTrace)
    {
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (
                context == null
                || context.state == null
                || context.unit_state == null
                || context.grid_service == null
            )
            {
                return null;
            }

            _scoreService.BeginDecisionScope(context.state, context.unit_state);
            context.active_score_profile = _scoreService.GetProfile();
            context.ClearMutationGuardViolations();

            if (MutationGuardMode == BattleAiMutationGuardMode.Disabled)
            {
                BattleAiDecision decisionNoGuard;
                using (new BattleAiTraceSpan("choose:impl"))
                    decisionNoGuard = ChooseCommandImpl(context);
                return BattleAiDecisionResult.Capture(context, decisionNoGuard, captureTrace);
            }

            if (MutationGuardMode != BattleAiMutationGuardMode.FullSnapshotDiagnostic)
            {
                throw new InvalidOperationException(
                    $"Unsupported AI mutation guard mode: {MutationGuardMode}."
                );
            }

            BattleAiMutationGuard mutationGuard = new();
            using (new BattleAiTraceSpan("choose:mutation_guard_capture"))
                mutationGuard.Capture(context);

            BattleAiDecision decision;
            try
            {
                using (new BattleAiTraceSpan("choose:impl"))
                    decision = ChooseCommandImpl(context);
            }
            catch (Exception evaluationException)
            {
                BattleAiMutationViolationReport exceptionReport;
                using (new BattleAiTraceSpan("choose:mutation_guard_validate_exception"))
                    exceptionReport = mutationGuard.ValidateReportTyped(
                        context,
                        "decision_exception",
                        callSite: "BattleAiService.ChooseCommandImpl"
                    );
                if (exceptionReport == null)
                {
                    throw;
                }

                RecordMutationViolation(context, exceptionReport);
                throw new BattleAiMutationViolationException(
                    exceptionReport,
                    evaluationException
                );
            }

            BattleAiMutationViolationReport report;
            using (new BattleAiTraceSpan("choose:mutation_guard_validate"))
                report = mutationGuard.ValidateReportTyped(
                    context,
                    "decision",
                    callSite: "BattleAiService.ChooseCommandImpl"
                );
            if (report == null)
            {
                return BattleAiDecisionResult.Capture(context, decision, captureTrace);
            }

            decision?.ClearOwnedRuntimeReferences();
            RecordMutationViolation(context, report);
            throw new BattleAiMutationViolationException(report);
        }
        finally
        {
            try
            {
                context?.ClearRuntimeBindings();
            }
            finally
            {
                _scoreService.EndDecisionScope();
            }
        }
    }

    private BattleAiDecision ChooseCommandImpl(BattleAiContext context)
    {
        context.skill_score_input_callback ??=
            (aiContext, skillDefinition, command, preview, effectDefs, metadata) =>
                _scoreService.BuildSkillScoreInput(
                    aiContext,
                    skillDefinition,
                    command,
                    preview,
                    effectDefs ?? System.Array.Empty<CombatEffectDefinition>(),
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
            _scoreService
        );
        return decision;
    }

    private static void RecordMutationViolation(
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
