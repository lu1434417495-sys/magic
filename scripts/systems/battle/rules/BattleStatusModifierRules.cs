using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleStatusModifierRules : RefCounted
{
    private const string ParamHealMultiplierPercent = "heal_multiplier_percent";
    private const string ParamShieldGainMultiplierPercent = "shield_gain_multiplier_percent";
    private const int DefaultMultiplierPercent = 100;

    private readonly record struct StatusModifierEntry(
        StringName StatusId,
        int? HealMultiplierPercent,
        int? ShieldGainMultiplierPercent
    );

    public static string PARAM_HEAL_MULTIPLIER_PERCENT() => ParamHealMultiplierPercent;

    public static string PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT() => ParamShieldGainMultiplierPercent;

    public static int DEFAULT_MULTIPLIER_PERCENT() => DefaultMultiplierPercent;

    public static int apply_heal_multiplier(BattleUnitState unit_state, int amount)
    {
        return ApplyMultiplier(amount, resolve_heal_multiplier_percent(unit_state));
    }

    public static int apply_shield_gain_multiplier(BattleUnitState unit_state, int amount)
    {
        return ApplyMultiplier(amount, resolve_shield_gain_multiplier_percent(unit_state));
    }

    public static int resolve_heal_multiplier_percent(BattleUnitState unit_state)
    {
        return ResolveMinMultiplierPercent(unit_state, ParamHealMultiplierPercent);
    }

    public static int resolve_shield_gain_multiplier_percent(BattleUnitState unit_state)
    {
        return ResolveMinMultiplierPercent(unit_state, ParamShieldGainMultiplierPercent);
    }

    private static int ResolveMinMultiplierPercent(BattleUnitState unitState, string paramKey)
    {
        if (unitState == null)
        {
            return DefaultMultiplierPercent;
        }

        int result = DefaultMultiplierPercent;
        foreach (StatusModifierEntry entry in BuildStatusModifierEntries(unitState))
        {
            int? multiplier =
                paramKey == ParamHealMultiplierPercent
                    ? entry.HealMultiplierPercent
                    : entry.ShieldGainMultiplierPercent;
            if (!multiplier.HasValue)
            {
                continue;
            }
            int rawInt = multiplier.Value;
            if (rawInt > DefaultMultiplierPercent)
            {
                string statusLabel = IsEmpty(entry.StatusId)
                    ? "<unknown>"
                    : entry.StatusId.ToString();
                GameLog.Warning(
                    $"BattleStatusModifierRules: status {statusLabel} declares {paramKey}={rawInt} (> {DefaultMultiplierPercent}); clamped — these multipliers only express debuffs.",
                    "battle.status.multiplier_clamped",
                    "battle"
                );
            }
            result = Mathf.Min(result, Mathf.Clamp(rawInt, 0, DefaultMultiplierPercent));
        }
        return result;
    }

    private static List<StatusModifierEntry> BuildStatusModifierEntries(BattleUnitState unitState)
    {
        var entries = new List<StatusModifierEntry>();
        if (unitState == null)
        {
            return entries;
        }

        foreach (var statusValue in unitState.status_effects.Values)
        {
            BattleStatusEffectState statusEntry = statusValue.As<BattleStatusEffectState>();
            if (statusEntry == null || statusEntry.is_empty())
            {
                continue;
            }
            entries.Add(
                new StatusModifierEntry(
                    statusEntry.status_id,
                    GetOptionalInt(statusEntry.@params, ParamHealMultiplierPercent),
                    GetOptionalInt(statusEntry.@params, ParamShieldGainMultiplierPercent)
                )
            );
        }
        return entries;
    }

    private static int? GetOptionalInt(GDictionary parameters, string key)
    {
        if (parameters == null)
        {
            return null;
        }
        if (parameters.ContainsKey(key))
        {
            return parameters[key].AsInt32();
        }
        var stringNameKey = new StringName(key);
        if (!parameters.ContainsKey(stringNameKey))
        {
            return null;
        }
        return parameters[stringNameKey].AsInt32();
    }

    private static int ApplyMultiplier(int amount, int multiplierPercent)
    {
        if (amount <= 0)
        {
            return 0;
        }
        int normalizedPercent = Mathf.Clamp(multiplierPercent, 0, DefaultMultiplierPercent);
        return Mathf.Max(Mathf.RoundToInt((float)amount * normalizedPercent / 100.0f), 0);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }
}
