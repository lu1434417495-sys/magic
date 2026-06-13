using System.Collections.Generic;
using Godot;

public static class BattleStatusModifierRules
{
    private const string HealMultiplierPercentLabel = "heal_multiplier_percent";
    private const string ShieldGainMultiplierPercentLabel = "shield_gain_multiplier_percent";
    private const int DefaultMultiplierPercent = 100;

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
        return ResolveMinHealMultiplierPercent(unitState);
    }

    public static int ResolveShieldGainMultiplierPercent(BattleUnitState unitState)
    {
        return ResolveMinShieldGainMultiplierPercent(unitState);
    }

    private static int ResolveMinHealMultiplierPercent(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return DefaultMultiplierPercent;
        }

        int result = DefaultMultiplierPercent;
        foreach (StatusModifierEntry entry in BuildStatusModifierEntries(unitState))
        {
            if (!entry.HealMultiplierPercent.HasValue)
            {
                continue;
            }
            result = Mathf.Min(
                result,
                ClampMultiplierPercent(
                    entry.StatusId,
                    entry.HealMultiplierPercent.Value,
                    HealMultiplierPercentLabel
                )
            );
        }
        return result;
    }

    private static int ResolveMinShieldGainMultiplierPercent(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return DefaultMultiplierPercent;
        }

        int result = DefaultMultiplierPercent;
        foreach (StatusModifierEntry entry in BuildStatusModifierEntries(unitState))
        {
            if (!entry.ShieldGainMultiplierPercent.HasValue)
            {
                continue;
            }
            result = Mathf.Min(
                result,
                ClampMultiplierPercent(
                    entry.StatusId,
                    entry.ShieldGainMultiplierPercent.Value,
                    ShieldGainMultiplierPercentLabel
                )
            );
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
            if (statusEntry == null || statusEntry.IsEmpty())
            {
                continue;
            }
            entries.Add(
                new StatusModifierEntry(
                    statusEntry.status_id,
                    GetOptionalHealMultiplier(statusEntry),
                    GetOptionalShieldGainMultiplier(statusEntry)
                )
            );
        }
        return entries;
    }

    private static int? GetOptionalHealMultiplier(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
        {
            return null;
        }
        return statusEntry.TryGetHealMultiplierPercentTyped(out int value) ? value : null;
    }

    private static int? GetOptionalShieldGainMultiplier(BattleStatusEffectState statusEntry)
    {
        if (statusEntry == null)
        {
            return null;
        }
        return statusEntry.TryGetShieldGainMultiplierPercentTyped(out int value) ? value : null;
    }

    private static int ClampMultiplierPercent(
        StringName statusId,
        int rawInt,
        string fieldLabel
    )
    {
        if (rawInt > DefaultMultiplierPercent)
        {
            string statusLabel = IsEmpty(statusId) ? "<unknown>" : statusId.ToString();
            GameLog.Warning(
                $"BattleStatusModifierRules: status {statusLabel} declares {fieldLabel}={rawInt} (> {DefaultMultiplierPercent}); clamped — these multipliers only express debuffs.",
                "battle.status.multiplier_clamped",
                "battle"
            );
        }
        return Mathf.Clamp(rawInt, 0, DefaultMultiplierPercent);
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
