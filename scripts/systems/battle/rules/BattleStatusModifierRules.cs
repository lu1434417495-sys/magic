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

    public static int apply_heal_multiplier(GodotObject unit_state, int amount)
    {
        return ApplyMultiplier(amount, resolve_heal_multiplier_percent(unit_state));
    }

    public static int apply_shield_gain_multiplier(GodotObject unit_state, int amount)
    {
        return ApplyMultiplier(amount, resolve_shield_gain_multiplier_percent(unit_state));
    }

    public static int resolve_heal_multiplier_percent(GodotObject unit_state)
    {
        return ResolveMinMultiplierPercent(unit_state, ParamHealMultiplierPercent);
    }

    public static int resolve_shield_gain_multiplier_percent(GodotObject unit_state)
    {
        return ResolveMinMultiplierPercent(unit_state, ParamShieldGainMultiplierPercent);
    }

    private static int ResolveMinMultiplierPercent(GodotObject unitState, string paramKey)
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
                string statusLabel = GdInterop.IsEmpty(entry.StatusId)
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

    private static List<StatusModifierEntry> BuildStatusModifierEntries(GodotObject unitState)
    {
        var entries = new List<StatusModifierEntry>();
        GDictionary statusEffects = GdInterop.GetDictionary(unitState, "status_effects");
        foreach (var statusValue in statusEffects.Values)
        {
            GodotObject statusEntry = statusValue.AsGodotObject();
            if (statusEntry == null)
            {
                continue;
            }
            GDictionary parameters = GdInterop.GetDictionary(statusEntry, "params");
            entries.Add(
                new StatusModifierEntry(
                    GdInterop.GetStringName(statusEntry, "status_id"),
                    GetOptionalInt(parameters, ParamHealMultiplierPercent),
                    GetOptionalInt(parameters, ParamShieldGainMultiplierPercent)
                )
            );
        }
        return entries;
    }

    private static int? GetOptionalInt(GDictionary parameters, string key)
    {
        if (
            !GdInterop.TryGet(parameters, key, out Variant value)
            || value.VariantType != Variant.Type.Int
        )
        {
            return null;
        }
        return value.AsInt32();
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
}
