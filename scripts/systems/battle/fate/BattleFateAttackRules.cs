using Godot;

[GlobalClass]
public partial class BattleFateAttackRules : RefCounted
{
    private const int NATURAL_HIT_ROLL = 20;

    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";

    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang";

    public bool does_attack_roll_hit(int hitRoll, AttackCheckInput attackCheck)
    {
        if (attackCheck.NaturalOneAutoMiss && hitRoll <= 1)
            return false;

        if (attackCheck.NaturalTwentyAutoHit && hitRoll >= NATURAL_HIT_ROLL)
            return true;

        return hitRoll >= attackCheck.RequiredRoll;
    }

    public bool does_gate_die_crit(int critGateRoll, int critGateDie, bool critLocked)
    {
        return !critLocked && critGateDie > NATURAL_HIT_ROLL && critGateRoll == critGateDie;
    }

    public bool is_high_threat_crit_roll(
        int hitRoll,
        bool critLocked,
        int critGateDie,
        int critThreshold
    )
    {
        return !critLocked && critGateDie == NATURAL_HIT_ROLL && hitRoll >= critThreshold;
    }

    public bool is_attack_crit_locked(BattleUnitState unitState)
    {
        return unitState != null
            && (
                unitState.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)
                || unitState.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG)
                || _unit_has_status_bool_param(unitState, "lock_crit")
            );
    }

    private bool _unit_has_status_bool_param(BattleUnitState unitState, StringName paramKey)
    {
        if (unitState == null || paramKey == "")
            return false;

        foreach (var statusIdValue in unitState.status_effects.Keys)
        {
            var statusId = ProgressionDataUtils.to_string_name(statusIdValue);

            var statusEntry = unitState.get_status_effect(statusId);

            if (statusEntry == null || statusEntry.@params == null)
                continue;

            if (TryGetStatusParamBool(statusEntry.@params, paramKey, out bool boolValue) && boolValue)
                return true;
        }

        return false;
    }

    private static bool TryGetStatusParamBool(
        Godot.Collections.Dictionary @params,
        StringName paramKey,
        out bool value
    )
    {
        value = false;
        if (@params == null || paramKey == "")
            return false;

        if (@params.ContainsKey(paramKey))
            return TryReadExactBool(@params[paramKey], out value);

        string paramName = (string)paramKey;

        if (@params.ContainsKey(paramName))
            return TryReadExactBool(@params[paramName], out value);

        foreach (var keyValue in @params.Keys)
        {
            if (ProgressionDataUtils.to_string_name(keyValue) == paramKey)
                return TryReadExactBool(@params[keyValue], out value);
        }

        return false;
    }

    private static bool TryReadExactBool(Variant rawValue, out bool value)
    {
        value = false;
        if (rawValue.VariantType != Variant.Type.Bool)
            return false;
        value = rawValue.AsBool();
        return true;
    }
}
