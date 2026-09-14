using System.Collections.Generic;
using Godot;

/// <see cref="BattleAiDecisionBindingService"/> 需要的运行时能力面。
///
/// 与 <see cref="IBattleTimelineRuntimePort"/> 等同一套模式。这里**一个访问器都没有**，
/// 收敛比也是目前最高的：原先 17 个字段 + 9 个 hub 方法，压到 13 个行为。
///
/// 关键在于几个"组装 + 交付"的整体动作被整段挪进了 bridge：
/// <see cref="BindAiHelperServicesForDecision"/> 与 <see cref="PrepareAiContextForDecision"/>
/// 原先各自要在消费者侧从 hub 上抓十几个字段（四个内容索引、8 个 AI 回调、trace 开关、
/// score service）拼成上下文记录再交给 runtime services；现在消费者只交出"哪个单位、哪个上下文"。
/// 同理 <see cref="BuildUnitActionPlan"/> / <see cref="IsActionPlanStaleFor"/> 把四个索引
/// 和世界步数一并吃掉，消费者不再知道这些索引存在。
internal interface IBattleAiDecisionBindingRuntimePort
{
    BattleState GetBattleState();

    /// 对应原先的 `_ai_action_assembler == null` 前置判断。
    bool IsActionAssemblerReady();

    EnemyAiBrainDefinition GetEnemyAiBrain(StringName brainId);

    BattleAiRuntimeActionPlan BuildUnitActionPlan(
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain
    );

    bool IsActionPlanStaleFor(
        BattleAiRuntimeActionPlan actionPlan,
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain
    );

    void BindAiHelperServicesForDecision(BattleUnitState unitState, BattleAiContext aiContext);

    BattleAiContext PrepareAiContextForDecision(
        BattleUnitState activeUnit,
        BattleAiRuntimeActionPlan actionPlan
    );

    BattleAiScoreInput BuildSkillScoreInput(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata,
        BattleAiSkillCandidateScoreFacts? candidateScoreFacts
    );

    BattleAiScoreInput BuildActionScoreInput(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    );

    BattleAiScoreInput BuildQueryActionScoreInput(
        BattleAiQueryService service,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    );

    bool IsMovementBlocked(BattleUnitState unitState);

    int GetMoveCostForUnitTarget(BattleUnitState unitState, Vector2I toCoord);

    void ClearTurnAiOverride(BattleUnitState unitState);
}
