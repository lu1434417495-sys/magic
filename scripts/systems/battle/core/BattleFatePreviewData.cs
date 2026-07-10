using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleFatePreviewData
{
    public bool UsesFateAttack { get; init; }
    public bool ForceHitNoCrit { get; init; }
    public bool ForceCriticalOnHit { get; init; }
    public bool IsDisadvantage { get; init; }
    public int EffectiveLuck { get; init; }
    public int CritGateDie { get; init; }
    public int FumbleLowEnd { get; init; }
    public int CritThreshold { get; init; }
    public bool CritLocked { get; init; }

    public bool MercyActive => IsDisadvantage && FumbleLowEnd > 1;

    public static BattleFatePreviewData FromAttackCheck(AttackCheckInput attackCheck)
    {
        if (attackCheck.Invalid)
            return null;
        if (
            attackCheck.CritGateDie <= 0
            && attackCheck.FumbleLowEnd <= 0
            && attackCheck.CritThreshold <= 0
            && !attackCheck.ForceHitNoCrit
            && !attackCheck.ForceCriticalOnHit
        )
            return null;

        return new BattleFatePreviewData
        {
            UsesFateAttack = true,
            ForceHitNoCrit = attackCheck.ForceHitNoCrit,
            ForceCriticalOnHit =
                attackCheck.ForceCriticalOnHit
                && !attackCheck.CritLocked
                && !attackCheck.ForceHitNoCrit,
            IsDisadvantage = attackCheck.IsDisadvantage,
            EffectiveLuck = attackCheck.EffectiveLuck,
            CritGateDie = attackCheck.CritGateDie,
            FumbleLowEnd = attackCheck.FumbleLowEnd,
            CritThreshold = attackCheck.CritThreshold,
            CritLocked = attackCheck.CritLocked,
        };
    }

    public static BattleFatePreviewData ForceHitNoCritPreview() =>
        new()
        {
            UsesFateAttack = true,
            ForceHitNoCrit = true,
            CritLocked = true,
        };

    public GDictionary ToDictionary() =>
        new()
        {
            ["uses_fate_attack"] = UsesFateAttack,
            ["force_hit_no_crit"] = ForceHitNoCrit,
            ["force_critical_on_hit"] = ForceCriticalOnHit,
            ["is_disadvantage"] = IsDisadvantage,
            ["effective_luck"] = EffectiveLuck,
            ["crit_gate_die"] = CritGateDie,
            ["fumble_low_end"] = FumbleLowEnd,
            ["crit_threshold"] = CritThreshold,
            ["crit_locked"] = CritLocked,
            ["mercy_active"] = MercyActive,
        };

    public static BattleFatePreviewData FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
            return null;
        return new BattleFatePreviewData
        {
            UsesFateAttack = ReadBool(source, "uses_fate_attack"),
            ForceHitNoCrit = ReadBool(source, "force_hit_no_crit"),
            ForceCriticalOnHit = ReadBool(source, "force_critical_on_hit"),
            IsDisadvantage = ReadBool(source, "is_disadvantage"),
            EffectiveLuck = ReadInt(source, "effective_luck"),
            CritGateDie = ReadInt(source, "crit_gate_die"),
            FumbleLowEnd = ReadInt(source, "fumble_low_end"),
            CritThreshold = ReadInt(source, "crit_threshold"),
            CritLocked = ReadBool(source, "crit_locked"),
        };
    }

    private static bool ReadBool(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return false;
        return source[key].AsBool();
    }

    private static int ReadInt(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return 0;
        return source[key].AsInt32();
    }
}
