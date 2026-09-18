using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
    : IBattleIncomingAttackDisadvantageSink
{
    BattleIncomingAttackMasteryReward IBattleIncomingAttackDisadvantageSink.CaptureIncomingAttackDisadvantage(
        BattleUnitState attacker,
        BattleUnitState defender,
        StringName contributingStatusId
    )
    {
        BattleStatusEffectState status = defender?.GetStatusEffect(contributingStatusId);
        BattleUnitState owner = status == null ? null : RtState()?.GetUnit(status.source_unit_id);
        BattleSkillMasteryGrant grant = Runtime?._skill_mastery_service
            .BuildIncomingAttackDisadvantageGrant(owner, attacker, status,
                Runtime.GetSkillDefinitionIndexTyped());
        return new BattleIncomingAttackMasteryReward(owner, grant);
    }

    void IBattleIncomingAttackDisadvantageSink.CommitIncomingAttackDisadvantage(
        BattleIncomingAttackMasteryReward reward, BattleEventBatch batch)
    {
        if (reward.Grant != null)
            Runtime?.ApplySkillMasteryGrantTyped(reward.Owner, reward.Grant, batch);
    }
}
