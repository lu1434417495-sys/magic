using Godot;

internal enum ProfessionReactivationMode
{
    Unknown = 0,
    Auto,
    Manual,
}

internal enum ProfessionDependencyVisibilityMode
{
    Unknown = 0,
    CountWhenHidden,
    IgnoreWhenHidden,
}

internal enum ProfessionBaseAttackProgression
{
    Unknown = 0,
    Full,
    ThreeQuarter,
    Half,
}

[GlobalClass]
public partial class ProfessionDef : Resource
{
    private static readonly StringName ReactivationAuto = "auto";
    private static readonly StringName ReactivationManual = "manual";
    private static readonly StringName DependencyCountWhenHidden = "count_when_hidden";
    private static readonly StringName DependencyIgnoreWhenHidden = "ignore_when_hidden";
    private static readonly StringName BabProgressionFull = "full";
    private static readonly StringName BabProgressionThreeQuarter = "three_quarter";
    private static readonly StringName BabProgressionHalf = "half";

    [Export]
    public StringName profession_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public int max_rank = 1;

    [Export]
    public int hit_die_sides = 8;

    [Export]
    public StringName bab_progression = "half";

    internal ProfessionBaseAttackProgression BabProgressionKind
    {
        get => ToBabProgression(bab_progression);
        set => bab_progression = ToStringName(value);
    }

    [Export]
    public bool is_initial_profession;

    [Export]
    public StringName unlock_knowledge_id = "";

    [Export]
    public ProfessionPromotionRequirement unlock_requirement;

    [Export]
    public Godot.Collections.Array<ProfessionRankRequirement> rank_requirements = new();

    [Export]
    public Godot.Collections.Array<ProfessionGrantedSkill> granted_skills = new();

    [Export]
    public Godot.Collections.Array<AttributeModifier> attribute_modifiers = new();

    [Export]
    public Godot.Collections.Array<ProfessionActiveCondition> active_conditions = new();

    [Export]
    public StringName reactivation_mode = "auto";

    internal ProfessionReactivationMode ReactivationModeKind =>
        ToReactivationMode(reactivation_mode);

    [Export]
    public StringName dependency_visibility_mode = "count_when_hidden";

    internal ProfessionDependencyVisibilityMode DependencyVisibilityModeKind =>
        ToDependencyVisibilityMode(dependency_visibility_mode);

    public bool RequiresKnowledgeUnlock() => !is_initial_profession;

    internal static ProfessionReactivationMode ToReactivationMode(StringName value)
    {
        if (value == ReactivationAuto)
            return ProfessionReactivationMode.Auto;
        if (value == ReactivationManual)
            return ProfessionReactivationMode.Manual;
        return ProfessionReactivationMode.Unknown;
    }

    internal static ProfessionDependencyVisibilityMode ToDependencyVisibilityMode(
        StringName value
    )
    {
        if (value == DependencyCountWhenHidden)
            return ProfessionDependencyVisibilityMode.CountWhenHidden;
        if (value == DependencyIgnoreWhenHidden)
            return ProfessionDependencyVisibilityMode.IgnoreWhenHidden;
        return ProfessionDependencyVisibilityMode.Unknown;
    }

    internal static ProfessionBaseAttackProgression ToBabProgression(StringName value)
    {
        if (value == BabProgressionFull)
            return ProfessionBaseAttackProgression.Full;
        if (value == BabProgressionThreeQuarter)
            return ProfessionBaseAttackProgression.ThreeQuarter;
        if (value == BabProgressionHalf)
            return ProfessionBaseAttackProgression.Half;
        return ProfessionBaseAttackProgression.Unknown;
    }

    internal static StringName ToStringName(ProfessionBaseAttackProgression value)
    {
        return value switch
        {
            ProfessionBaseAttackProgression.Full => BabProgressionFull,
            ProfessionBaseAttackProgression.ThreeQuarter => BabProgressionThreeQuarter,
            ProfessionBaseAttackProgression.Half => BabProgressionHalf,
            _ => "",
        };
    }

    public ProfessionRankRequirement GetRankRequirement(int target_rank)
    {
        foreach (var r in rank_requirements)
        {
            if (r != null && r.target_rank == target_rank)
                return r;
        }
        return null;
    }

    public Godot.Collections.Array<ProfessionGrantedSkill> GetGrantedSkillsForRank(
        int target_rank
    )
    {
        var result = new Godot.Collections.Array<ProfessionGrantedSkill>();
        foreach (var gs in granted_skills)
        {
            if (gs != null && gs.unlock_rank == target_rank)
                result.Add(gs);
        }
        return result;
    }
}
