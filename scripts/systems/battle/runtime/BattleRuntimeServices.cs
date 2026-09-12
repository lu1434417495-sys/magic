using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Godot;

internal readonly struct BattleAiDecisionContextSetup
{
    internal readonly BattleState State;
    internal readonly BattleUnitState UnitState;
    internal readonly BattleGridService GridService;
    internal readonly BattleAiRuntimeActionPlan ActionPlan;
    internal readonly IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions;
    internal readonly IReadOnlyDictionary<StringName, BarrierProfileDefinition> BarrierProfileDefinitions;
    internal readonly IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        EquipmentAbilityBindings;
    internal readonly IReadOnlyDictionary<StringName, ItemDefinition> ItemDefinitions;
    internal readonly StringName BasicAttackSkillId;
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
        BattleAiSkillCandidateScoreFacts?,
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
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfileDefinitions,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        StringName basicAttackSkillId,
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
            BattleAiSkillCandidateScoreFacts?,
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
        BarrierProfileDefinitions = barrierProfileDefinitions;
        EquipmentAbilityBindings = equipmentAbilityBindings;
        ItemDefinitions = itemDefinitions;
        BasicAttackSkillId = basicAttackSkillId;
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
    internal readonly IReadOnlyDictionary<StringName, BarrierProfileDefinition> BarrierProfileDefinitions;
    internal readonly IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        EquipmentAbilityBindings;
    internal readonly IReadOnlyDictionary<StringName, ItemDefinition> ItemDefinitions;
    internal readonly StringName BasicAttackSkillId;
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
        BattleAiSkillCandidateScoreFacts?,
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
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfileDefinitions,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        StringName basicAttackSkillId,
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
            BattleAiSkillCandidateScoreFacts?,
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
        BarrierProfileDefinitions = barrierProfileDefinitions;
        EquipmentAbilityBindings = equipmentAbilityBindings;
        ItemDefinitions = itemDefinitions;
        BasicAttackSkillId = basicAttackSkillId;
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
    private bool _runtimeSidecarsBound;
    private bool _aiHelperBindingsActive;
    private long _battleEpoch = long.MinValue;

    internal bool HasAiRuntimeBindings =>
        _aiHelperBindingsActive || AiDecisionContext.HasRuntimeBindings;

    internal bool HasRuntimeSidecarBindings => _runtimeSidecarsBound;

    internal void BeginBattle(long battleEpoch)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_battleEpoch == battleEpoch)
        {
            return;
        }

        ClearRuntimeBindings();
        AiMovementQuery.BeginBattle(battleEpoch);
        _battleEpoch = battleEpoch;
    }

    internal void EndBattle()
    {
        Exception accumulatedFailure = null;
        RunTeardownStep(ref accumulatedFailure, ClearRuntimeBindings);
        RunTeardownStep(ref accumulatedFailure, AiMovementQuery.EndBattle);
        _battleEpoch = long.MinValue;
        Rethrow(accumulatedFailure);
    }

    internal void SetupRuntimeSidecars(
        BattleRuntimeModule runtime,
        IBattleContingencyRuntimePort contingencyRuntimePort
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // 就地绑定 bridge（经 IsBoundTo 幂等）：本方法可能早于 _moduleBorrowers.Setup 跑到，
        // 不保证的话地面效果服务族会拿到未绑定的端口而静默 no-op。
        runtime._moduleBorrowers.GroundEffectBridge.Setup(runtime);
        GroundEffects.Setup(runtime._moduleBorrowers.GroundEffectBridge);
        SpecialSkills.Setup(runtime);
        Movement.Setup(runtime);
        Contingencies.Setup(contingencyRuntimePort);
        _runtimeSidecarsBound = runtime != null;
    }

    internal BattleAiContext PrepareAiContextForDecision(BattleAiDecisionContextSetup context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AiDecisionContext.ResetForDecision(
            context.State,
            context.UnitState,
            context.GridService,
            context.ActionPlan,
            context.SkillDefinitions,
            context.TraceEnabled,
            context.SkillCatalog,
            context.BarrierProfileDefinitions,
            context.EquipmentAbilityBindings,
            context.ItemDefinitions,
            context.BasicAttackSkillId
        );
        BindContextCallbacks(AiDecisionContext, context);
        return AiDecisionContext;
    }

    internal void BindAiHelperServicesForDecision(
        BattleAiHelperBindingContext context,
        BattleAiContext aiContext
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (
            context.UnitState == null
            || aiContext == null
            || context.State == null
            || context.GridService == null
        )
        {
            return;
        }
        if (_battleEpoch == long.MinValue)
        {
            throw new InvalidOperationException(
                "AI helper services cannot bind before the battle cache epoch is initialized."
            );
        }

        aiContext.SetBasicAttackSkillId(context.BasicAttackSkillId);
        BindContextCallbacks(aiContext, context);

        using (new BattleAiTraceSpan("bind_ai_helpers:movement_query_setup"))
        {
            AiMovementQuery.Setup(
                _battleEpoch,
                context.State,
                context.GridService,
                context.MoveQueryCostCallback
            );
        }

        using (new BattleAiTraceSpan("bind_ai_helpers:score_adapter_setup"))
        {
            AiScoreContextAdapter.Setup(
                context.ScoreService,
                context.State,
                context.UnitState,
                context.GridService,
                context.SkillCatalog,
                context.SkillDefinitions,
                context.BarrierProfileDefinitions,
                context.SkillCastBlockReasonCallback,
                context.EquipmentAbilityBindings,
                context.ItemDefinitions,
                context.BasicAttackSkillId
            );
        }

        using (new BattleAiTraceSpan("bind_ai_helpers:query_setup"))
        {
            AiQuery.Setup(
                context.State,
                context.GridService,
                context.UnitState.unit_id,
                context.SkillDefinitions,
                context.QueryActionScoreInputCallback,
                AiMovementQuery,
                context.MovementBlockedCallback,
                context.SkillCatalog,
                context.EquipmentAbilityBindings,
                context.ItemDefinitions
            );
        }

        using (new BattleAiTraceSpan("bind_ai_helpers:candidate_setup"))
        {
            AiCandidateEvaluation.Setup(context.ScoreService);
        }

        aiContext.ai_query_service = AiQuery;
        aiContext.candidate_evaluator = AiCandidateEvaluation;
        _aiHelperBindingsActive = true;
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
        // Clear in reverse borrower order. Movement query topology/path caches are plain
        // battle-lifetime values; only its decision-scoped state/grid/callback bindings end here.
        _aiHelperBindingsActive = false;
        Exception accumulatedFailure = null;
        RunTeardownStep(ref accumulatedFailure, AiDecisionContext.ClearRuntimeBindings);
        RunTeardownStep(ref accumulatedFailure, AiQuery.ClearRuntimeBindings);
        RunTeardownStep(ref accumulatedFailure, AiScoreContextAdapter.ClearRuntimeBindings);
        RunTeardownStep(ref accumulatedFailure, AiMovementQuery.ClearRuntimeBindings);
        Rethrow(accumulatedFailure);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _runtimeSidecarsBound = false;
        Exception accumulatedFailure = null;
        RunTeardownStep(ref accumulatedFailure, EndBattle);
        RunTeardownStep(ref accumulatedFailure, Contingencies.Dispose);
        RunTeardownStep(ref accumulatedFailure, GroundEffects.Dispose);
        RunTeardownStep(ref accumulatedFailure, SpecialSkills.Dispose);
        RunTeardownStep(ref accumulatedFailure, Movement.Dispose);
        RunTeardownStep(ref accumulatedFailure, AiMovementQuery.Dispose);
        Rethrow(accumulatedFailure);
    }

    private static void RunTeardownStep(ref Exception accumulatedFailure, Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            accumulatedFailure ??= exception;
        }
    }

    private static void Rethrow(Exception failure)
    {
        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
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
