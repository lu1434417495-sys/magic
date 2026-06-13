using Godot;

internal enum TagRequirementSkillState
{
    Unknown = 0,
    Learned,
    Core,
    CoreMax,
}

internal enum TagRequirementOriginFilter
{
    Unknown = 0,
    Any,
    UnmergedOnly,
    MergedOnly,
}

internal enum TagRequirementSelectionRole
{
    Unknown = 0,
    AssignedCore,
    Qualifier,
}

[GlobalClass]
public partial class TagRequirement : Resource
{
    private static readonly StringName SkillStateLearned = "learned";
    private static readonly StringName SkillStateCore = "core";
    private static readonly StringName SkillStateCoreMax = "core_max";
    private static readonly StringName OriginFilterAny = "any";
    private static readonly StringName OriginFilterUnmergedOnly = "unmerged_only";
    private static readonly StringName OriginFilterMergedOnly = "merged_only";
    private static readonly StringName SelectionRoleAssignedCore = "assigned_core";
    private static readonly StringName SelectionRoleQualifier = "qualifier";

    [Export]
    public StringName tag = "";

    [Export]
    public int count = 1;

    [Export]
    public StringName skill_state = SkillStateCoreMax;
    internal TagRequirementSkillState SkillStateKind
    {
        get => ToSkillState(skill_state);
        set => skill_state = ToStringName(value);
    }

    [Export]
    public StringName origin_filter = OriginFilterAny;
    internal TagRequirementOriginFilter OriginFilterKind
    {
        get => ToOriginFilter(origin_filter);
        set => origin_filter = ToStringName(value);
    }

    [Export]
    public StringName selection_role = SelectionRoleAssignedCore;
    internal TagRequirementSelectionRole SelectionRoleKind
    {
        get => ToSelectionRole(selection_role);
        set => selection_role = ToStringName(value);
    }

    internal static TagRequirementSkillState ToSkillState(StringName value)
    {
        if (value == SkillStateLearned)
            return TagRequirementSkillState.Learned;
        if (value == SkillStateCore)
            return TagRequirementSkillState.Core;
        if (value == SkillStateCoreMax)
            return TagRequirementSkillState.CoreMax;
        return TagRequirementSkillState.Unknown;
    }

    internal static TagRequirementOriginFilter ToOriginFilter(StringName value)
    {
        if (value == OriginFilterAny)
            return TagRequirementOriginFilter.Any;
        if (value == OriginFilterUnmergedOnly)
            return TagRequirementOriginFilter.UnmergedOnly;
        if (value == OriginFilterMergedOnly)
            return TagRequirementOriginFilter.MergedOnly;
        return TagRequirementOriginFilter.Unknown;
    }

    internal static TagRequirementSelectionRole ToSelectionRole(StringName value)
    {
        if (value == SelectionRoleAssignedCore)
            return TagRequirementSelectionRole.AssignedCore;
        if (value == SelectionRoleQualifier)
            return TagRequirementSelectionRole.Qualifier;
        return TagRequirementSelectionRole.Unknown;
    }

    internal static StringName ToStringName(TagRequirementSkillState kind)
    {
        return kind switch
        {
            TagRequirementSkillState.Learned => SkillStateLearned,
            TagRequirementSkillState.Core => SkillStateCore,
            TagRequirementSkillState.CoreMax => SkillStateCoreMax,
            _ => "",
        };
    }

    internal static StringName ToStringName(TagRequirementOriginFilter kind)
    {
        return kind switch
        {
            TagRequirementOriginFilter.Any => OriginFilterAny,
            TagRequirementOriginFilter.UnmergedOnly => OriginFilterUnmergedOnly,
            TagRequirementOriginFilter.MergedOnly => OriginFilterMergedOnly,
            _ => "",
        };
    }

    internal static StringName ToStringName(TagRequirementSelectionRole kind)
    {
        return kind switch
        {
            TagRequirementSelectionRole.AssignedCore => SelectionRoleAssignedCore,
            TagRequirementSelectionRole.Qualifier => SelectionRoleQualifier,
            _ => "",
        };
    }
}
