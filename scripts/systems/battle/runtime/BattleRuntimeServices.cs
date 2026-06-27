using System;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleAiDecisionContextSetup
{
    internal readonly BattleState State;
    internal readonly BattleUnitState UnitState;
    internal readonly BattleGridService GridService;
    internal readonly BattleAiRuntimeActionPlan ActionPlan;
    internal readonly IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
    internal readonly bool TraceEnabled;
    internal readonly ISkillCatalog SkillCatalog;
    internal readonly Func<BattleUnitState, Vector2I, int> MoveCostCallback;
    internal readonly Func<BattleCommand, BattlePreview> PreviewCommandCallback;
    internal readonly Func<
        BattleAiContext,
        SkillDefinition,
        BattleCommand,
        BattlePreview,
        IReadOnlyList<CombatEffectDefinition>,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > SkillScoreInputCallback;
    internal readonly Func<
        BattleAiContext,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > ActionScoreInputCallback;
    internal readonly Func<BattleUnitState, SkillDefinition, BattleSkillCastBlockReasonKind>
        SkillCastBlockReasonCallback;

    internal BattleAiDecisionContextSetup(
        BattleState state,
        BattleUnitState unitState,
        BattleGridService gridService,
        BattleAiRuntimeActionPlan actionPlan,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        bool traceEnabled,
        ISkillCatalog skillCatalog,
        Func<BattleUnitState, Vector2I, int> moveCostCallback,
        Func<BattleCommand, BattlePreview> previewCommandCallback,
        Func<
            BattleAiContext,
            SkillDefinition,
            BattleCommand,
            BattlePreview,
            IReadOnlyList<CombatEffectDefinition>,
            IReadOnlyDictionary<string, object>,
            BattleAiScoreInput
        > skillScoreInputCallback,
        Func<
            BattleAiContext,
            StringName,
            string,
            StringName,
            BattleCommand,
            BattlePreview,
            IReadOnlyDictionary<string, object>,
            BattleAiScoreInput
        > actionScoreInputCallback,
        Func<BattleUnitState, SkillDefinition, BattleSkillCastBlockReasonKind>
            skillCastBlockReasonCallback
    )
    {
        State = state;
        UnitState = unitState;
        GridService = gridService;
        ActionPlan = actionPlan;
        SkillDefinitions = skillDefinitions;
        TraceEnabled = traceEnabled;
        SkillCatalog = skillCatalog;
        MoveCostCallback = moveCostCallback;
        PreviewCommandCallback = previewCommandCallback;
        SkillScoreInputCallback = skillScoreInputCallback;
        ActionScoreInputCallback = actionScoreInputCallback;
        SkillCastBlockReasonCallback = skillCastBlockReasonCallback;
    }
}

internal readonly struct BattleAiHelperBindingContext
{
    internal readonly BattleState State;
    internal readonly BattleGridService GridService;
    internal readonly BattleUnitState UnitState;
    internal readonly IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
    internal readonly ISkillCatalog SkillCatalog;
    internal readonly BattleAiScoreService ScoreService;
    internal readonly Func<StringName, Vector2I, Vector2I, int> MoveQueryCostCallback;
    internal readonly Func<
        BattleAiQueryService,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > QueryActionScoreInputCallback;
    internal readonly Func<StringName, bool> MovementBlockedCallback;
    internal readonly Func<BattleUnitState, Vector2I, int> MoveCostCallback;
    internal readonly Func<BattleCommand, BattlePreview> PreviewCommandCallback;
    internal readonly Func<
        BattleAiContext,
        SkillDefinition,
        BattleCommand,
        BattlePreview,
        IReadOnlyList<CombatEffectDefinition>,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > SkillScoreInputCallback;
    internal readonly Func<
        BattleAiContext,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > ActionScoreInputCallback;
    internal readonly Func<BattleUnitState, SkillDefinition, BattleSkillCastBlockReasonKind>
        SkillCastBlockReasonCallback;

    internal BattleAiHelperBindingContext(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        ISkillCatalog skillCatalog,
        BattleAiScoreService scoreService,
        Func<StringName, Vector2I, Vector2I, int> moveQueryCostCallback,
        Func<
            BattleAiQueryService,
            StringName,
            string,
            StringName,
            BattleCommand,
            BattlePreview,
            IReadOnlyDictionary<string, object>,
            BattleAiScoreInput
        > queryActionScoreInputCallback,
        Func<StringName, bool> movementBlockedCallback,
        Func<BattleUnitState, Vector2I, int> moveCostCallback,
        Func<BattleCommand, BattlePreview> previewCommandCallback,
        Func<
            BattleAiContext,
            SkillDefinition,
            BattleCommand,
            BattlePreview,
            IReadOnlyList<CombatEffectDefinition>,
            IReadOnlyDictionary<string, object>,
            BattleAiScoreInput
        > skillScoreInputCallback,
        Func<
            BattleAiContext,
            StringName,
            string,
            StringName,
            BattleCommand,
            BattlePreview,
            IReadOnlyDictionary<string, object>,
            BattleAiScoreInput
        > actionScoreInputCallback,
        Func<BattleUnitState, SkillDefinition, BattleSkillCastBlockReasonKind>
            skillCastBlockReasonCallback
    )
    {
        State = state;
        GridService = gridService;
        UnitState = unitState;
        SkillDefinitions = skillDefinitions;
        SkillCatalog = skillCatalog;
        ScoreService = scoreService;
        MoveQueryCostCallback = moveQueryCostCallback;
        QueryActionScoreInputCallback = queryActionScoreInputCallback;
        MovementBlockedCallback = movementBlockedCallback;
        MoveCostCallback = moveCostCallback;
        PreviewCommandCallback = previewCommandCallback;
        SkillScoreInputCallback = skillScoreInputCallback;
        ActionScoreInputCallback = actionScoreInputCallback;
        SkillCastBlockReasonCallback = skillCastBlockReasonCallback;
    }
}

internal sealed class BattleRuntimeServices : IDisposable
{
    internal BattleMovementService Movement { get; } = new();
    internal BattleGroundEffectService GroundEffects { get; } = new();
    internal BattleSpecialSkillResolver SpecialSkills { get; } = new();
    internal BattleMovementQueryService AiMovementQuery { get; } = new();
    internal BattleAiScoreContextAdapter AiScoreContextAdapter { get; } = new();
    internal BattleAiQueryService AiQuery { get; } = new();
    internal BattleAiCandidateEvaluationService AiCandidateEvaluation { get; } = new();
    internal BattleAiContext AiDecisionContext { get; } = new();
    internal BattleContingencySystem Contingencies { get; } = new();

    private bool _disposed;

    internal void SetupRuntimeSidecars(BattleRuntimeModule runtime)
    {
        GroundEffects.Setup(runtime);
        SpecialSkills.Setup(runtime);
        Movement.Setup(runtime);
        Contingencies.Setup(runtime);
    }

    internal BattleAiContext PrepareAiContextForDecision(BattleAiDecisionContextSetup context)
    {
        AiDecisionContext.ResetForDecision(
            context.State,
            context.UnitState,
            context.GridService,
            context.ActionPlan,
            context.SkillDefinitions,
            context.TraceEnabled,
            context.SkillCatalog
        );
        BindContextCallbacks(AiDecisionContext, context);
        return AiDecisionContext;
    }

    internal void BindAiHelperServicesForDecision(
        BattleAiHelperBindingContext context,
        BattleAiContext aiContext
    )
    {
        if (
            context.UnitState == null
            || aiContext == null
            || context.State == null
            || context.GridService == null
        )
        {
            return;
        }

        BindContextCallbacks(aiContext, context);

        AiTraceRecorder.Enter("bind_ai_helpers:movement_query_setup");
        AiMovementQuery.Setup(
            context.State,
            context.GridService,
            context.MoveQueryCostCallback
        );
        AiTraceRecorder.Exit("bind_ai_helpers:movement_query_setup");

        AiTraceRecorder.Enter("bind_ai_helpers:score_adapter_setup");
        AiScoreContextAdapter.Setup(
            context.ScoreService,
            context.State,
            context.UnitState,
            context.GridService,
            context.SkillCatalog,
            context.SkillDefinitions
        );
        AiTraceRecorder.Exit("bind_ai_helpers:score_adapter_setup");

        AiTraceRecorder.Enter("bind_ai_helpers:query_setup");
        AiQuery.Setup(
            context.State,
            context.GridService,
            context.UnitState.unit_id,
            context.SkillDefinitions,
            context.QueryActionScoreInputCallback,
            AiMovementQuery,
            context.MovementBlockedCallback,
            context.SkillCatalog
        );
        AiTraceRecorder.Exit("bind_ai_helpers:query_setup");

        AiTraceRecorder.Enter("bind_ai_helpers:candidate_setup");
        AiCandidateEvaluation.Setup(context.ScoreService);
        AiTraceRecorder.Exit("bind_ai_helpers:candidate_setup");

        aiContext.ai_query_service = AiQuery;
        aiContext.candidate_evaluator = AiCandidateEvaluation;
    }

    internal BattleAiScoreInput BuildActionScoreInput(
        BattleAiQueryService service,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return AiScoreContextAdapter.BuildActionScoreInput(
            service,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    internal void ClearRuntimeBindings()
    {
        AiMovementQuery.ClearRuntimeBindings();
        AiScoreContextAdapter.ClearRuntimeBindings();
        AiQuery.ClearRuntimeBindings();
        AiDecisionContext.ClearRuntimeBindings();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        ClearRuntimeBindings();
        Contingencies.Dispose();
        GroundEffects.Dispose();
        SpecialSkills.Dispose();
        Movement.Dispose();
        AiMovementQuery.Dispose();
    }

    private static void BindContextCallbacks(
        BattleAiContext aiContext,
        BattleAiDecisionContextSetup context
    )
    {
        aiContext.move_cost_callback = context.MoveCostCallback;
        aiContext.preview_command_callback = context.PreviewCommandCallback;
        aiContext.skill_score_input_callback = context.SkillScoreInputCallback;
        aiContext.action_score_input_callback = context.ActionScoreInputCallback;
        aiContext.skill_cast_block_reason_callback = context.SkillCastBlockReasonCallback;
    }

    private static void BindContextCallbacks(
        BattleAiContext aiContext,
        BattleAiHelperBindingContext context
    )
    {
        aiContext.move_cost_callback = context.MoveCostCallback;
        aiContext.preview_command_callback = context.PreviewCommandCallback;
        aiContext.skill_score_input_callback = context.SkillScoreInputCallback;
        aiContext.action_score_input_callback = context.ActionScoreInputCallback;
        aiContext.skill_cast_block_reason_callback = context.SkillCastBlockReasonCallback;
    }
}
