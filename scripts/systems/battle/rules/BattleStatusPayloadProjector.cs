using System;
using System.Collections.Generic;
using Godot;

internal static class BattleStatusPayloadProjector
{
    internal static void Apply(
        BattleStatusEffectState status,
        ICombatEffectPayloadDefinition payload
    )
    {
        if (status == null || payload is not StatusEffectPayloadDefinition statusPayload)
            return;

        if (status.source_skill_id == "" && statusPayload.SourceSkillId != "")
            status.source_skill_id = statusPayload.SourceSkillId;
        status.save_bonus_by_tag = CopyMap(statusPayload.SaveBonusByTag);
        status.source_bound_attack_roll_penalty =
            statusPayload.SourceBoundAttackRollPenalty;
        status.source_bound_attack_roll_penalty_min_stacks = Math.Max(
            statusPayload.SourceBoundAttackRollPenaltyMinStacks,
            1
        );
        status.source_bound_incoming_attack_roll_bonus_per_stack =
            statusPayload.SourceBoundIncomingAttackRollBonusPerStack;
        status.source_bound_incoming_attack_roll_bonus_min_stacks = Math.Max(
            statusPayload.SourceBoundIncomingAttackRollBonusMinStacks,
            1
        );
        status.SetParamsTyped(ProjectResidualState(statusPayload));
    }

    private static IReadOnlyDictionary<string, object> ProjectResidualState(
        StatusEffectPayloadDefinition payload
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (payload.BreaksBarrierLayer != "")
            result["breaks_barrier_layer"] = payload.BreaksBarrierLayer;
        return result;
    }

    private static Dictionary<StringName, int> CopyMap(
        IReadOnlyDictionary<StringName, int> values
    )
    {
        return values == null
            ? new Dictionary<StringName, int>()
            : new Dictionary<StringName, int>(values);
    }
}
