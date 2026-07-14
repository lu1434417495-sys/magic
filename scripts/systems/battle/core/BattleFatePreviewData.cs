using Godot;

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

}
