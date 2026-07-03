using Godot;

internal static class SkillContentRules
{
    private static readonly StringName UnlockModeStandard = "standard";
    private static readonly StringName UnlockModeCompositeUpgrade = "composite_upgrade";
    private static readonly StringName CoreTransitionInherit = "inherit";
    private static readonly StringName CoreTransitionReplaceSources = "replace_sources_with_result";
    private static readonly StringName SkillTypeActive = "active";
    private static readonly StringName SkillTypePassive = "passive";
    private static readonly StringName LearnSourceBook = "book";
    private static readonly StringName LearnSourceInnate = "innate";
    private static readonly StringName LearnSourcePlayer = "player";
    private static readonly StringName LearnSourceProfession = "profession";
    private static readonly StringName LearnSourceRace = "race";
    private static readonly StringName LearnSourceSubrace = "subrace";
    private static readonly StringName LearnSourceAscension = "ascension";
    private static readonly StringName LearnSourceBloodline = "bloodline";
    private static readonly StringName PracticeTierBasic = "basic";
    private static readonly StringName PracticeTierIntermediate = "intermediate";
    private static readonly StringName PracticeTierAdvanced = "advanced";
    private static readonly StringName PracticeTierUltimate = "ultimate";

    internal static SkillTypeKind ToSkillType(StringName value)
    {
        if (value == SkillTypeActive)
            return SkillTypeKind.Active;
        if (value == SkillTypePassive)
            return SkillTypeKind.Passive;
        return SkillTypeKind.Unknown;
    }

    internal static SkillLearnSourceKind ToLearnSource(StringName value)
    {
        if (value == LearnSourceBook)
            return SkillLearnSourceKind.Book;
        if (value == LearnSourceInnate)
            return SkillLearnSourceKind.Innate;
        if (value == LearnSourcePlayer)
            return SkillLearnSourceKind.Player;
        if (value == LearnSourceProfession)
            return SkillLearnSourceKind.Profession;
        if (value == LearnSourceRace)
            return SkillLearnSourceKind.Race;
        if (value == LearnSourceSubrace)
            return SkillLearnSourceKind.Subrace;
        if (value == LearnSourceAscension)
            return SkillLearnSourceKind.Ascension;
        if (value == LearnSourceBloodline)
            return SkillLearnSourceKind.Bloodline;
        return SkillLearnSourceKind.Unknown;
    }

    internal static StringName ToStringName(SkillLearnSourceKind value)
    {
        return value switch
        {
            SkillLearnSourceKind.Book => LearnSourceBook,
            SkillLearnSourceKind.Innate => LearnSourceInnate,
            SkillLearnSourceKind.Player => LearnSourcePlayer,
            SkillLearnSourceKind.Profession => LearnSourceProfession,
            SkillLearnSourceKind.Race => LearnSourceRace,
            SkillLearnSourceKind.Subrace => LearnSourceSubrace,
            SkillLearnSourceKind.Ascension => LearnSourceAscension,
            SkillLearnSourceKind.Bloodline => LearnSourceBloodline,
            _ => "",
        };
    }

    internal static SkillPracticeTierKind ToPracticeTier(StringName value)
    {
        if (value == "")
            return SkillPracticeTierKind.None;
        if (value == PracticeTierBasic)
            return SkillPracticeTierKind.Basic;
        if (value == PracticeTierIntermediate)
            return SkillPracticeTierKind.Intermediate;
        if (value == PracticeTierAdvanced)
            return SkillPracticeTierKind.Advanced;
        if (value == PracticeTierUltimate)
            return SkillPracticeTierKind.Ultimate;
        return SkillPracticeTierKind.Unknown;
    }

    internal static StringName ToStringName(SkillPracticeTierKind value)
    {
        return value switch
        {
            SkillPracticeTierKind.Basic => PracticeTierBasic,
            SkillPracticeTierKind.Intermediate => PracticeTierIntermediate,
            SkillPracticeTierKind.Advanced => PracticeTierAdvanced,
            SkillPracticeTierKind.Ultimate => PracticeTierUltimate,
            _ => "",
        };
    }

    internal static SkillUnlockMode ToUnlockMode(StringName value)
    {
        if (value == UnlockModeStandard)
            return SkillUnlockMode.Standard;
        if (value == UnlockModeCompositeUpgrade)
            return SkillUnlockMode.CompositeUpgrade;
        return SkillUnlockMode.Unknown;
    }

    internal static CoreSkillTransitionMode ToCoreSkillTransitionMode(StringName value)
    {
        if (value == CoreTransitionInherit)
            return CoreSkillTransitionMode.Inherit;
        if (value == CoreTransitionReplaceSources)
            return CoreSkillTransitionMode.ReplaceSourcesWithResult;
        return CoreSkillTransitionMode.Unknown;
    }
}

internal static class CombatSkillContentRules
{
    private static readonly StringName SpellFateControlRoll = "control_roll";
    private static readonly StringName BacklashGroundAnchorDrift = "ground_anchor_drift";
    private static readonly StringName AttackResolutionModeAuto = "auto";
    private static readonly StringName AttackResolutionModeDirectEffect = "direct_effect";
    private static readonly StringName AttackResolutionModeFateAttack = "fate_attack";
    private static readonly StringName AttackResolutionModeForceHitNoCrit = "force_hit_no_crit";
    private static readonly StringName AreaOriginTarget = "target";
    private static readonly StringName AreaOriginCaster = "caster";
    private static readonly StringName AreaOriginAnchorCoord = "anchor_coord";
    private static readonly StringName AreaDirectionTargetVector = "target_vector";
    private static readonly StringName AreaDirectionCasterFacing = "caster_facing";

    internal static CombatSpellFateMode ToSpellFateMode(StringName value)
    {
        if (value == "")
            return CombatSpellFateMode.None;
        if (value == SpellFateControlRoll)
            return CombatSpellFateMode.ControlRoll;
        return CombatSpellFateMode.Unknown;
    }

    internal static CombatSkillBacklashMode ToBacklashMode(StringName value)
    {
        if (value == "")
            return CombatSkillBacklashMode.None;
        if (value == BacklashGroundAnchorDrift)
            return CombatSkillBacklashMode.GroundAnchorDrift;
        return CombatSkillBacklashMode.Unknown;
    }

    internal static CombatSkillAttackResolutionMode ToAttackResolutionMode(StringName value)
    {
        if (value == "" || value == AttackResolutionModeAuto)
            return CombatSkillAttackResolutionMode.Auto;
        if (value == AttackResolutionModeDirectEffect)
            return CombatSkillAttackResolutionMode.DirectEffect;
        if (value == AttackResolutionModeFateAttack)
            return CombatSkillAttackResolutionMode.FateAttack;
        if (value == AttackResolutionModeForceHitNoCrit)
            return CombatSkillAttackResolutionMode.ForceHitNoCrit;
        return CombatSkillAttackResolutionMode.Unknown;
    }

    internal static CombatAreaOriginMode ToAreaOriginMode(StringName value)
    {
        if (value == AreaOriginTarget)
            return CombatAreaOriginMode.Target;
        if (value == AreaOriginCaster)
            return CombatAreaOriginMode.Caster;
        if (value == AreaOriginAnchorCoord)
            return CombatAreaOriginMode.AnchorCoord;
        return CombatAreaOriginMode.Unknown;
    }

    internal static CombatAreaDirectionMode ToAreaDirectionMode(StringName value)
    {
        if (value == AreaDirectionTargetVector)
            return CombatAreaDirectionMode.TargetVector;
        if (value == AreaDirectionCasterFacing)
            return CombatAreaDirectionMode.CasterFacing;
        return CombatAreaDirectionMode.Unknown;
    }
}
