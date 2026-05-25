using Godot;

[GlobalClass]
public partial class BattleFateAttackRules : RefCounted
{
    private const int NATURAL_HIT_ROLL = 20;
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang";

    public bool does_attack_roll_hit(int hitRoll, Godot.Collections.Dictionary attackCheck)
    {
        bool naturalOneAutoMiss = attackCheck.ContainsKey("natural_one_auto_miss")
            ? (bool)attackCheck["natural_one_auto_miss"]
            : true;
        if (naturalOneAutoMiss && hitRoll <= 1)
            return false;
        bool naturalTwentyAutoHit = attackCheck.ContainsKey("natural_twenty_auto_hit")
            ? (bool)attackCheck["natural_twenty_auto_hit"]
            : true;
        if (naturalTwentyAutoHit && hitRoll >= NATURAL_HIT_ROLL)
            return true;
        int requiredRoll = attackCheck.ContainsKey("required_roll")
            ? attackCheck["required_roll"].AsInt32()
            : 21;
        return hitRoll >= requiredRoll;
    }

    public bool does_gate_die_crit(int critGateRoll, int critGateDie, bool critLocked)
    {
        return !critLocked && critGateDie > NATURAL_HIT_ROLL && critGateRoll == critGateDie;
    }

    public bool is_high_threat_crit_roll(int hitRoll, bool critLocked, int critGateDie, int critThreshold)
    {
        return !critLocked && critGateDie == NATURAL_HIT_ROLL && hitRoll >= critThreshold;
    }

    public bool is_attack_crit_locked(BattleUnitState unitState)
    {
        return unitState != null && (
            unitState.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)
            || unitState.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG)
            || _unit_has_status_bool_param(unitState, "lock_crit")
        );
    }

    private bool _unit_has_status_bool_param(BattleUnitState unitState, StringName paramKey)
    {
        if (unitState == null || paramKey == "")
            return false;
        foreach (var statusIdVariant in unitState.status_effects.Keys)
        {
            var statusId = ProgressionDataUtils.to_string_name(statusIdVariant);
            var statusEntry = unitState.get_status_effect(statusId);
            if (statusEntry == null || statusEntry.@params == null)
                continue;
            if ((bool)_get_status_param_string_key(statusEntry.@params, paramKey, false))
                return true;
        }
        return false;
    }

    private Variant _get_status_param_string_key(Godot.Collections.Dictionary @params, StringName paramKey, Variant fallback)
    {
        if (@params == null || paramKey == "")
            return fallback;
        if (@params.ContainsKey(paramKey))
            return @params[paramKey];
        string paramName = (string)paramKey;
        if (@params.ContainsKey(paramName))
            return @params[paramName];
        foreach (var keyVariant in @params.Keys)
        {
            if (ProgressionDataUtils.to_string_name(keyVariant) == paramKey)
                return @params[keyVariant];
        }
        return fallback;
    }
}
