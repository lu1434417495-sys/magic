using System.Collections.Generic;
using Godot;

internal interface IBattleTerrainEffectRuntime
{
    BattleState GetState();

    BattleGridService GetGridService();

    BattleDamageResolver GetDamageResolver();

    void AppendChangedCoord(BattleEventBatch batch, Vector2I coord);

    void AppendChangedUnitId(BattleEventBatch batch, StringName unitId);

    void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unitState);

    void AppendBatchLog(BattleEventBatch batch, string message);

    void ClearDefeatedUnit(BattleUnitState unitState, BattleEventBatch batch = null);

    void AppendResultSourceStatusEffects(
        BattleEventBatch batch,
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result
    );

    void RecordEnemyDefeatedAchievement(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    );

    void RecordBattleContributionResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        bool causedDefeat,
        StringName originKind,
        StringName skillId
    );

    void MarkAppliedStatusesForTurnTiming(
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> statusEffectIds
    );
}
