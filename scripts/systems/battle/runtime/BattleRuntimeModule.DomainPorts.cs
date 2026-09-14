using System.Collections.Generic;
using Godot;

public sealed partial class BattleRuntimeModule
    : IBattleTerrainEffectRuntime,
        IMisfortuneGuidanceBattleQuery
{
    void IBattleTerrainEffectRuntime.AppendChangedCoord(
        BattleEventBatch batch,
        Vector2I coord
    ) => AppendChangedCoord(batch, coord);

    void IBattleTerrainEffectRuntime.AppendChangedUnitId(
        BattleEventBatch batch,
        StringName unitId
    ) => AppendChangedUnitId(batch, unitId);

    void IBattleTerrainEffectRuntime.AppendChangedUnitCoords(
        BattleEventBatch batch,
        BattleUnitState unitState
    ) => AppendChangedUnitCoords(batch, unitState);

    void IBattleTerrainEffectRuntime.AppendBatchLog(
        BattleEventBatch batch,
        string message
    ) => AppendBatchLog(batch, message);

    void IBattleTerrainEffectRuntime.ClearDefeatedUnit(
        BattleUnitState unitState,
        BattleEventBatch batch
    ) => ClearDefeatedUnit(unitState, batch);

    void IBattleTerrainEffectRuntime.AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    ) => AppendResultSourceStatusEffects(batch, sourceUnit, result);

    void IBattleTerrainEffectRuntime.RecordEnemyDefeatedAchievement(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    ) => RecordEnemyDefeatedAchievement(activeUnit, targetUnit);

    void IBattleTerrainEffectRuntime.MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    ) => MarkAppliedStatusesForTurnTiming(targetUnit, statusEffectIds);

    void IBattleTerrainEffectRuntime.GrantTerrainEffectiveTriggerMastery(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName skillId,
        BattleEventBatch batch
    ) => GrantTerrainEffectiveTriggerMastery(sourceUnit, targetUnit, skillId, batch);

    IReadOnlyDictionary<StringName, int>
        IMisfortuneGuidanceBattleQuery.GetCalamityByMemberIdSnapshot() =>
            GetCalamityByMemberIdSnapshot();

    bool IMisfortuneGuidanceBattleQuery.HasMisfortuneReason(
        StringName memberId,
        StringName reasonId
    ) => HasMisfortuneReason(memberId, reasonId);
}
