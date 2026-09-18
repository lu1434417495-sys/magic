using Godot;

internal interface IBattleIncomingAttackDisadvantageSink
{
    BattleIncomingAttackMasteryReward CaptureIncomingAttackDisadvantage(
        BattleUnitState attacker,
        BattleUnitState defender,
        StringName contributingStatusId
    );
    void CommitIncomingAttackDisadvantage(BattleIncomingAttackMasteryReward reward, BattleEventBatch batch);
}

internal readonly record struct BattleIncomingAttackMasteryReward(
    BattleUnitState Owner, BattleSkillMasteryGrant Grant);
