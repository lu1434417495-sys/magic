using System.Collections.Generic;
using Godot;

/// <see cref="BattleTimelineDriver"/> 对 runtime 的全部依赖面。
///
/// 该 driver 过去持 <c>WeakReference&lt;BattleRuntimeModule&gt;</c> 并穿透 hub 取兄弟服务
/// （<c>_casting_time_service</c> / <c>_skill_turn_resolver</c> / <c>_terrain_effect_system</c> …），
/// 依赖既不可声明也无法隔离单测。本接口把那 19 个穿透点收敛成一份显式契约：
/// 只暴露**行为**，不暴露服务对象，因此 driver 不再知道哪个服务负责哪件事。
///
/// 实现方为 <see cref="BattleTimelineBridgeService"/>（一个 BattleRuntimeModuleBorrower），
/// 与 <see cref="IBattleContingencyRuntimePort"/> / <see cref="BattleContingencyBridgeService"/>
/// 是同一套模式。
internal interface IBattleTimelineRuntimePort
{
    BattleState GetBattleState();

    // --- 回合与状态推进 ---
    void AdvanceUnitTurnTimers(BattleUnitState unitState, BattleEventBatch batch);
    BattleStatusTickResult ApplyTurnStartStatuses(BattleUnitState unitState, BattleEventBatch batch);
    BattleStatusTickResult ApplyUnitStatusPeriodicTicks(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch
    );
    bool AdvanceUnitStatusDurations(
        BattleUnitState unitState,
        int elapsedTu,
        BattleEventBatch batch
    );
    bool AdvanceTimeStasisFrozenTimers(
        BattleUnitState unitState,
        int tuDelta,
        BattleEventBatch batch
    );
    bool IsTurnAiOverrideActive(BattleUnitState unitState);
    BattleTurnControlStatusResult ResolveTurnControlStatus(
        BattleUnitState unitState,
        BattleEventBatch batch
    );

    // --- 引导施法 ---
    void ReconcilePendingCasts(BattleEventBatch batch);
    void AdvancePendingCasts(
        int elapsedTu,
        BattleEventBatch batch,
        ISet<StringName> stasisFrozenUnitIds
    );
    void CompleteReadyPendingCasts(BattleEventBatch batch);

    // --- 场上持续效果 ---
    void ProcessDueDelayedAreaEffects(BattleEventBatch batch);
    void ProcessTimedTerrainEffects(BattleEventBatch batch);
    void AdvanceBarrierDurations(int tuDelta, BattleEventBatch batch);

    // --- trait 钩子（只用到这两个，故不暴露 hooks 对象本身）---
    void DispatchTraitBattleStart(BattleUnitState unitState);
    TraitDispatchResult DispatchTraitTurnStart(BattleUnitState unitState);

    // --- 目标（objective）事务 ---
    void BeginObjectiveMutation();
    void EndObjectiveMutation(BattleEventBatch batch, bool mutationCompleted);
    void AdvanceControlObjectiveProgress(int tuDelta, BattleEventBatch batch);

    // --- 单位生命周期 ---
    void RecordTurnStarted(BattleUnitState unitState, BattleEventBatch batch);
    int GetUnitStaminaMax(BattleUnitState unitState);
    void AppendChangedUnitId(BattleEventBatch batch, StringName unitId);
    void CollectDefeatedUnitLoot(
        BattleUnitState unitState,
        BattleUnitState killerUnit,
        BattleEventBatch batch
    );
    void HandleUnitDefeatedByRuntimeEffect(
        BattleUnitState unitState,
        BattleUnitState sourceUnit,
        BattleEventBatch batch,
        string logLine,
        BattleDefeatHandlingOptions options
    );
    void PrepareAiTurn(BattleUnitState unitState);
    void CleanupAiTurn(BattleUnitState unitState);

    // --- 跨域联动 ---
    void HandleLowHpTurnEndMisfortune(BattleUnitState unitState);
    bool ResolveEquipmentAbilityTurnEnd(BattleEquipmentAbilityTurnEndContext context);
}
