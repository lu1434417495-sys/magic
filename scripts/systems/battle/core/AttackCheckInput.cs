using Godot;

public readonly struct AttackCheckInput
{
    public readonly int AttackerBaseAttackBonus;
    public readonly int AttackerAttackBonus;
    public readonly int AttackerBab;
    public readonly int TargetArmorClass;
    public readonly int SkillAttackBonus;
    public readonly int LockedSkillHitBonus;
    public readonly int SituationalAttackBonus;
    public readonly int SituationalAttackPenalty;

    public readonly int RequiredRoll;
    public readonly int DisplayRequiredRoll;
    public readonly int HitRatePercent;
    public readonly int SuccessRatePercent;
    public readonly int BaseHitRatePercent;

    public readonly bool NaturalOneAutoMiss;
    public readonly bool NaturalTwentyAutoHit;

    public readonly int CritThreshold;
    public readonly int FumbleLowEnd;
    public readonly bool CritLocked;
    public readonly int CritGateDie;
    public readonly bool ForceHitNoCrit;

    public readonly StringName SkillId;
    public readonly int FollowUpAttackPenalty;
    public readonly bool ExponentialPenalty;
    public readonly bool IsDisadvantage;

    public readonly bool Invalid;
    public readonly StringName ErrorId;
    public readonly string ErrorMessage;
    public readonly string PreviewText;

    public AttackCheckInput(
        int attackerBaseAttackBonus = 0,
        int attackerAttackBonus = 0,
        int attackerBab = 0,
        int targetArmorClass = 0,
        int skillAttackBonus = 0,
        int lockedSkillHitBonus = 0,
        int situationalAttackBonus = 0,
        int situationalAttackPenalty = 0,
        int requiredRoll = 21,
        int displayRequiredRoll = 0,
        int hitRatePercent = 0,
        int successRatePercent = 0,
        int baseHitRatePercent = 0,
        bool naturalOneAutoMiss = true,
        bool naturalTwentyAutoHit = true,
        int critThreshold = 0,
        int fumbleLowEnd = 0,
        bool critLocked = false,
        int critGateDie = 0,
        bool forceHitNoCrit = false,
        StringName skillId = null,
        int followUpAttackPenalty = 0,
        bool exponentialPenalty = false,
        bool isDisadvantage = false,
        bool invalid = false,
        StringName errorId = null,
        string errorMessage = "",
        string previewText = ""
    )
    {
        AttackerBaseAttackBonus = attackerBaseAttackBonus;
        AttackerAttackBonus = attackerAttackBonus;
        AttackerBab = attackerBab;
        TargetArmorClass = targetArmorClass;
        SkillAttackBonus = skillAttackBonus;
        LockedSkillHitBonus = lockedSkillHitBonus;
        SituationalAttackBonus = situationalAttackBonus;
        SituationalAttackPenalty = situationalAttackPenalty;
        RequiredRoll = requiredRoll;
        DisplayRequiredRoll = displayRequiredRoll;
        HitRatePercent = hitRatePercent;
        SuccessRatePercent = successRatePercent;
        BaseHitRatePercent = baseHitRatePercent;
        NaturalOneAutoMiss = naturalOneAutoMiss;
        NaturalTwentyAutoHit = naturalTwentyAutoHit;
        CritThreshold = critThreshold;
        FumbleLowEnd = fumbleLowEnd;
        CritLocked = critLocked;
        CritGateDie = critGateDie;
        ForceHitNoCrit = forceHitNoCrit;
        SkillId = skillId ?? new StringName("");
        FollowUpAttackPenalty = followUpAttackPenalty;
        ExponentialPenalty = exponentialPenalty;
        IsDisadvantage = isDisadvantage;
        Invalid = invalid;
        ErrorId = errorId ?? new StringName("");
        ErrorMessage = errorMessage ?? "";
        PreviewText = previewText ?? "";
    }

}
