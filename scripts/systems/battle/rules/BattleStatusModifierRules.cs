using System;
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
                RequireMultiplierPercent(
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
                RequireMultiplierPercent(
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

    /// 两个授权源（SkillExecuteEffectValidator 与 EquipmentAbilityPayloadValidators）都已在
    /// 内容校验期把这两个百分比限死在 0..100。原先的"警告一句然后 clamp"意味着 JSON 写的数
    /// 和战斗里跑的数不一致却照常打完，所以这里改成越界即报缺陷。
    private static int RequireMultiplierPercent(
        StringName statusId,
        int rawInt,
        string fieldLabel
    )
    {
        if (rawInt < 0 || rawInt > DefaultMultiplierPercent)
        {
            string statusLabel = IsEmpty(statusId) ? "<unknown>" : statusId.ToString();
            throw new InvalidOperationException(
                $"Status {statusLabel} carries {fieldLabel}={rawInt}, outside the validated "
                    + $"0..{DefaultMultiplierPercent} range."
            );
        }
        return rawInt;
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
