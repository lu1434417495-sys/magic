using System;
using Godot;

public partial class BattleEffectiveTraitInstanceState : RefCounted
{
    private static readonly string[] RequiredFields =
    {
        "trait_id",
        "effective_instance_key",
        "source_type",
        "source_id",
        "effect_type",
        "trigger_type",
        "charge_scope",
        "charge_reset_timing",
        "rank",
        "stacks",
        "roll_values",
    };

    public StringName trait_id = "";
    public StringName effective_instance_key = "";
    public StringName source_type = "";
    public StringName source_id = "";
    public StringName effect_type = "";
    public StringName trigger_type = "";
    public StringName charge_scope = "none";
    public StringName charge_reset_timing = "none";
    public int rank = 1;
    public int stacks = 1;
    public Godot.Collections.Array<TraitRollValueState> roll_values = new();

    internal TraitEffectKind EffectKind => TraitContentRules.ToEffectKind(effect_type);
    internal TraitTriggerKind TriggerKind => TraitTriggerContentRules.ToTriggerKind(trigger_type);
    internal TraitChargeScopeKind ChargeScopeKind =>
        TraitContentRules.ToChargeScopeKind(charge_scope);
    internal TraitChargeResetTimingKind ChargeResetTimingKind =>
        TraitContentRules.ToChargeResetTimingKind(charge_reset_timing);

    internal BattleEffectiveTraitInstanceState DuplicateState()
    {
        return new BattleEffectiveTraitInstanceState
        {
            trait_id = trait_id,
            effective_instance_key = effective_instance_key,
            source_type = source_type,
            source_id = source_id,
            effect_type = effect_type,
            trigger_type = trigger_type,
            charge_scope = charge_scope,
            charge_reset_timing = charge_reset_timing,
            rank = rank,
            stacks = stacks,
            roll_values = TraitInstanceState.NormalizeRollValues(roll_values),
        };
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "trait_id", trait_id.ToString() },
            { "effective_instance_key", effective_instance_key.ToString() },
            { "source_type", source_type.ToString() },
            { "source_id", source_id.ToString() },
            { "effect_type", effect_type.ToString() },
            { "trigger_type", trigger_type.ToString() },
            { "charge_scope", charge_scope.ToString() },
            { "charge_reset_timing", charge_reset_timing.ToString() },
            { "rank", Math.Max(rank, 1) },
            { "stacks", Math.Max(stacks, 1) },
            { "roll_values", TraitInstanceState.RollValuesToDictionary(roll_values) },
        };
    }

    internal static BattleEffectiveTraitInstanceState FromDictionary(
        Godot.Collections.Dictionary payload)
    {
        if (!IsValidPayload(payload))
            return null;

        Godot.Collections.Array<TraitRollValueState> parsedRollValues =
            TraitInstanceState.RollValuesFromDictionary(
                payload["roll_values"].AsGodotDictionary()
            );
        if (parsedRollValues == null)
            return null;

        return new BattleEffectiveTraitInstanceState
        {
            trait_id = ToStringName(payload["trait_id"]),
            effective_instance_key = ToStringName(payload["effective_instance_key"]),
            source_type = ToStringName(payload["source_type"]),
            source_id = ToStringName(payload["source_id"]),
            effect_type = ToStringName(payload["effect_type"]),
            trigger_type = ToStringName(payload["trigger_type"]),
            charge_scope = ToStringName(payload["charge_scope"]),
            charge_reset_timing = ToStringName(payload["charge_reset_timing"]),
            rank = payload["rank"].AsInt32(),
            stacks = payload["stacks"].AsInt32(),
            roll_values = parsedRollValues,
        };
    }

    private static bool IsValidPayload(Godot.Collections.Dictionary payload)
    {
        if (payload == null || payload.Count != RequiredFields.Length)
            return false;
        foreach (string fieldName in RequiredFields)
            if (!payload.ContainsKey(fieldName))
                return false;
        foreach (Variant rawKey in payload.Keys)
            if (rawKey.VariantType != Variant.Type.String || !ContainsField(rawKey.AsString()))
                return false;

        if (
            !IsStringNameField(payload, "trait_id")
            || !IsStringNameField(payload, "effective_instance_key")
            || !IsStringNameField(payload, "source_type")
            || !IsStringNameField(payload, "source_id")
            || !IsStringNameField(payload, "effect_type")
            || !IsStringNameField(payload, "trigger_type")
            || !IsStringNameField(payload, "charge_scope")
            || !IsStringNameField(payload, "charge_reset_timing")
        )
            return false;

        if (IsEmpty(ToStringName(payload["trait_id"])))
            return false;
        if (IsEmpty(ToStringName(payload["effective_instance_key"])))
            return false;
        if (!TraitContentRules.IsValidSourceType(ToStringName(payload["source_type"])))
            return false;
        if (!TraitContentRules.IsValidEffectType(ToStringName(payload["effect_type"])))
            return false;
        if (
            TraitTriggerContentRules.ToTriggerKind(ToStringName(payload["trigger_type"]))
            == TraitTriggerKind.Unknown
        )
            return false;
        if (!TraitContentRules.IsValidChargeScope(ToStringName(payload["charge_scope"])))
            return false;
        if (
            !TraitContentRules.IsValidChargeResetTiming(
                ToStringName(payload["charge_reset_timing"])
            )
        )
            return false;
        if (payload["rank"].VariantType != Variant.Type.Int || payload["rank"].AsInt32() < 1)
            return false;
        if (payload["stacks"].VariantType != Variant.Type.Int || payload["stacks"].AsInt32() < 1)
            return false;
        return payload["roll_values"].VariantType == Variant.Type.Dictionary;
    }

    private static bool ContainsField(string value)
    {
        foreach (string fieldName in RequiredFields)
            if (fieldName == value)
                return true;
        return false;
    }

    private static bool IsStringNameField(Godot.Collections.Dictionary data, string key)
    {
        Variant.Type type = data[key].VariantType;
        return type == Variant.Type.String || type == Variant.Type.StringName;
    }

    private static StringName ToStringName(Variant value)
    {
        return ProgressionDataUtils.to_string_name(value);
    }

    private static bool IsEmpty(StringName value) => value == default || value == "";
}
