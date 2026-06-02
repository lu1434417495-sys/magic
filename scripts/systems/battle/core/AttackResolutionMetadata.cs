using System.Collections.Generic;
using Godot;

public sealed class AttackResolutionMetadata
{
    public StringName AttackResolution = new("");
    public bool AttackSuccess;
    public bool CriticalHit;
    public bool CriticalFail;
    public bool OrdinaryMiss;
    public bool IsDisadvantage;
    public int HiddenLuckAtBirth;
    public int FaithLuckBonus;
    public int EffectiveLuck;
    public bool CritLocked;
    public int CritGateDie;
    public int CritGateRoll;
    public int HitRoll;
    public int FumbleLowEnd;
    public int CritThreshold;
    public int RequiredRoll = 21;
    public int DisplayRequiredRoll;
    public int HitRatePercent;
    public int SuccessRatePercent;
    public bool ReverseFateDowngraded;
    public bool SecondaryHitSuccess;
    public StringName SkillId = new("");
    public readonly List<AttackTraitTriggerResult> TraitTriggerResults = new();
}
