using System.Collections.Generic;
using Godot;

/// 把 <see cref="IBattleAiDecisionBindingRuntimePort"/> 实现到 <see cref="BattleRuntimeModule"/> 上。
///
/// 作为 <see cref="BattleRuntimeModuleBorrower"/>，它经基类的 <c>_runtime</c> 访问 hub；
/// 穿透 hub 的行为被限制在本文件内，<see cref="BattleAiDecisionBindingService"/> 自身不再引用
/// BattleRuntimeModule。与 <see cref="BattleTimelineBridgeService"/> 同一套模式。
///
/// 本文件承担了原先散落在消费者里的上下文组装：AI 决策要用的四个内容索引、8 个回调、
/// trace 开关、score service 全部在这里取，消费者只交出单位与上下文。
///
/// 全部成员为显式接口实现：本类不提供 hub 之外的额外能力面。
internal sealed class BattleAiDecisionBindingBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleAiDecisionBindingRuntimePort
{
    BattleState IBattleAiDecisionBindingRuntimePort.GetBattleState() => _runtime?._state;

    bool IBattleAiDecisionBindingRuntimePort.IsActionAssemblerReady() =>
        _runtime?._ai_action_assembler != null;

    EnemyAiBrainDefinition IBattleAiDecisionBindingRuntimePort.GetEnemyAiBrain(
        StringName brainId
    ) => _runtime?.GetEnemyAiBrainTyped(brainId);

    BattleAiRuntimeActionPlan IBattleAiDecisionBindingRuntimePort.BuildUnitActionPlan(
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain
    )
    {
        BattleRuntimeModule runtime = _runtime;
        return runtime?._ai_action_assembler?.BuildUnitActionPlan(
            unitState,
            brain,
            runtime.GetSkillDefinitionIndexTyped(),
            runtime._skillCatalog,
            runtime.GetEquipmentAbilityBindingIndexTyped(),
            runtime.GetItemDefIndexTyped(),
            runtime._state,
            runtime.GetBattleWorldStep()
        );
    }

    bool IBattleAiDecisionBindingRuntimePort.IsActionPlanStaleFor(
        BattleAiRuntimeActionPlan actionPlan,
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain
    )
    {
        BattleRuntimeModule runtime = _runtime;
        if (actionPlan == null || runtime == null)
        {
            return true;
        }
        return actionPlan.IsStaleFor(
            unitState,
            brain,
            runtime._skillCatalog,
            runtime.GetSkillDefinitionIndexTyped(),
            runtime.GetEquipmentAbilityBindingIndexTyped(),
            runtime.GetItemDefIndexTyped(),
            runtime._state,
            runtime.GetBattleWorldStep()
        );
    }

    void IBattleAiDecisionBindingRuntimePort.BindAiHelperServicesForDecision(
        BattleUnitState unitState,
        BattleAiContext aiContext
    )
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime?._state == null || runtime._grid_service == null)
        {
            return;
        }
        runtime._runtime_services.BindAiHelperServicesForDecision(
            new BattleAiHelperBindingContext(
                runtime._state,
                runtime._grid_service,
                unitState,
                runtime.GetSkillDefinitionIndexTyped(),
                runtime.GetBarrierProfileIndexTyped(),
                runtime.GetEquipmentAbilityBindingIndexTyped(),
                runtime.GetItemDefIndexTyped(),
                runtime.GetBasicAttackSkillId(),
                runtime._skillCatalog,
                runtime._ai_service.GetScoreService(),
                runtime._ai_move_query_cost_callback,
                runtime._ai_query_action_score_input_callback,
                runtime._ai_movement_blocked_callback,
                runtime._ai_move_cost_callback,
                runtime._ai_preview_command_callback,
                runtime._ai_skill_score_input_callback,
                runtime._ai_action_score_input_callback,
                runtime._ai_skill_cast_block_reason_callback
            ),
            aiContext
        );
    }

    BattleAiContext IBattleAiDecisionBindingRuntimePort.PrepareAiContextForDecision(
        BattleUnitState activeUnit,
        BattleAiRuntimeActionPlan actionPlan
    )
    {
        BattleRuntimeModule runtime = _runtime;
        return runtime?._runtime_services.PrepareAiContextForDecision(
            new BattleAiDecisionContextSetup(
                runtime._state,
                activeUnit,
                runtime._grid_service,
                actionPlan,
                runtime.GetSkillDefinitionIndexTyped(),
                runtime.GetBarrierProfileIndexTyped(),
                runtime.GetEquipmentAbilityBindingIndexTyped(),
                runtime.GetItemDefIndexTyped(),
                runtime.GetBasicAttackSkillId(),
                runtime._ai_trace_enabled,
                runtime._skillCatalog,
                runtime._ai_move_cost_callback,
                runtime._ai_preview_command_callback,
                runtime._ai_skill_score_input_callback,
                runtime._ai_action_score_input_callback,
                runtime._ai_skill_cast_block_reason_callback
            )
        );
    }

    BattleAiScoreInput IBattleAiDecisionBindingRuntimePort.BuildSkillScoreInput(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata,
        BattleAiSkillCandidateScoreFacts? candidateScoreFacts
    ) =>
        _runtime
            ?._ai_service
            ?.GetScoreService()
            ?.BuildSkillScoreInput(
                context,
                skillDefinition,
                command,
                preview,
                effectDefinitions,
                metadata,
                candidateScoreFacts
            );

    BattleAiScoreInput IBattleAiDecisionBindingRuntimePort.BuildActionScoreInput(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    ) =>
        _runtime
            ?._ai_service
            ?.GetScoreService()
            ?.BuildActionScoreInput(
                context,
                actionKind,
                actionLabel,
                scoreBucketId,
                command,
                preview,
                metadata
            );

    BattleAiScoreInput IBattleAiDecisionBindingRuntimePort.BuildQueryActionScoreInput(
        BattleAiQueryService service,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    ) =>
        _runtime?._runtime_services.BuildActionScoreInput(
            service,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );

    bool IBattleAiDecisionBindingRuntimePort.IsMovementBlocked(BattleUnitState unitState) =>
        _runtime?._is_movement_blocked(unitState) == true;

    int IBattleAiDecisionBindingRuntimePort.GetMoveCostForUnitTarget(
        BattleUnitState unitState,
        Vector2I toCoord
    ) =>
        _runtime
            ?._movementCommandService
            ?._get_move_cost_for_unit_target(unitState, toCoord) ?? 1;

    void IBattleAiDecisionBindingRuntimePort.ClearTurnAiOverride(BattleUnitState unitState) =>
        _runtime?._skill_turn_resolver?.ClearTurnAiOverride(unitState);
}
