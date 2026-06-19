using Godot;

internal static class TraitTestData
{
    internal static Godot.Collections.Array<TraitRollValueState> RollValues(
        params TraitRollValueState[] values)
    {
        Godot.Collections.Array<TraitRollValueState> result = new();
        foreach (TraitRollValueState value in values ?? System.Array.Empty<TraitRollValueState>())
            if (value != null)
                result.Add(value);
        return result;
    }

    internal static TraitRollValueState IntRoll(StringName key, int value) =>
        TraitRollValueState.CreateInt(key, value);

    internal static TraitRollValueState StringNameRoll(StringName key, StringName value) =>
        TraitRollValueState.CreateStringName(key, value);

    internal static TraitRollValueState BoolRoll(StringName key, bool value) =>
        TraitRollValueState.CreateBool(key, value);

    internal static Godot.Collections.Array<BattleEffectiveTraitInstanceState> EffectiveTraits(
        params BattleEffectiveTraitInstanceState[] values)
    {
        Godot.Collections.Array<BattleEffectiveTraitInstanceState> result = new();
        foreach (
            BattleEffectiveTraitInstanceState value in values
                ?? System.Array.Empty<BattleEffectiveTraitInstanceState>()
        )
        {
            if (value != null)
                result.Add(value);
        }
        return result;
    }

    internal static BattleEffectiveTraitInstanceState EffectiveTrait(
        StringName traitId,
        StringName effectiveInstanceKey,
        StringName triggerType,
        StringName chargeScope,
        StringName chargeResetTiming,
        StringName effectType = default,
        StringName sourceType = default,
        StringName sourceId = default,
        Godot.Collections.Array<TraitRollValueState> rollValues = null
    )
    {
        StringName effect = effectType == default || effectType == "" ? traitId : effectType;
        StringName sourceTypeValue =
            sourceType == default || sourceType == ""
                ? TraitContentRules.ToStringName(TraitSourceKind.Identity)
                : sourceType;
        StringName sourceIdValue = sourceId == default || sourceId == "" ? "test_identity" : sourceId;
        return new BattleEffectiveTraitInstanceState
        {
            trait_id = traitId,
            effective_instance_key = effectiveInstanceKey,
            source_type = sourceTypeValue,
            source_id = sourceIdValue,
            effect_type = effect,
            trigger_type = triggerType,
            charge_scope = chargeScope,
            charge_reset_timing = chargeResetTiming,
            rank = 1,
            stacks = 1,
            roll_values = rollValues ?? new Godot.Collections.Array<TraitRollValueState>(),
        };
    }
}
