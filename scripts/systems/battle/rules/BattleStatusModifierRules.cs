using System.Collections.Generic;
using Godot;

public static class BattleStatusModifierRules
{
    public const string HealMultiplierPercentParam = "heal_multiplier_percent";
    public const string ShieldGainMultiplierPercentParam = "shield_gain_multiplier_percent";
    public const int DefaultMultiplierPercent = 100;

    private readonly record struct StatusModifierEntry(
        StringName StatusId,
        int? HealMultiplierPercent,
        int? ShieldGainMultiplierPercent
    );

    public static int ApplyHealMultiplier(BattleUnitState unitState, int amount)
    {
        return ApplyMultiplier(amount, ResolveHealMultiplierPercent(unitState));
    }

    public static int ApplyShieldGainMultiplier(BattleUnitState unitState, int amount)
    {
        return ApplyMultiplier(amount, ResolveShieldGainMultiplierPercent(unitState));
    }

    public static int ResolveHealMultiplierPercent(BattleUnitState unitState)
    {
        return ResolveMinMultiplierPercent(unitState, HealMultiplierPercentParam);
    }

    public static int ResolveShieldGainMultiplierPercent(BattleUnitState unitState)
    {
        return ResolveMinMultiplierPercent(unitState, ShieldGainMultiplierPercentParam);
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
                paramKey == HealMultiplierPercentParam
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

        foreach (BattleStatusEffectState statusEntry in unitState.GetStatusEffectsTyped())
        {
            if (statusEntry == null || statusEntry.is_empty())
            {
                continue;
            }
            entries.Add(
                new StatusModifierEntry(
                    statusEntry.status_id,
                    GetOptionalInt(statusEntry, HealMultiplierPercentParam),
                    GetOptionalInt(statusEntry, ShieldGainMultiplierPercentParam)
                )
            );
        }
        return entries;
    }

    private static int? GetOptionalInt(BattleStatusEffectState statusEntry, string key)
    {
        if (statusEntry == null)
        {
            return null;
        }
        return statusEntry.TryGetIntParam(key, out int value) ? value : null;
    }

    private static int ApplyMultiplier(int amount, int multiplierPercent)
    {
        if (amount <= 0)
        {
            return 0;
        }
        int normalizedPercent = Mathf.Clamp(multiplierPercent, 0, DefaultMultiplierPercent);
        int scaled = Mathf.RoundToInt((float)amount * normalizedPercent / 100.0f);
        if (scaled <= 0 && normalizedPercent > 0)
        {
            return 1;
        }
        return Mathf.Max(scaled, 0);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }
}
