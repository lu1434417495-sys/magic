using Godot;

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

    [Export]
    public StringName origin_filter = OriginFilterAny;

    [Export]
    public StringName selection_role = SelectionRoleAssignedCore;

    public static StringName SKILL_STATE_LEARNED() => SkillStateLearned;

    public static StringName SKILL_STATE_CORE() => SkillStateCore;

    public static StringName SKILL_STATE_CORE_MAX() => SkillStateCoreMax;

    public static StringName ORIGIN_FILTER_ANY() => OriginFilterAny;

    public static StringName ORIGIN_FILTER_UNMERGED_ONLY() => OriginFilterUnmergedOnly;

    public static StringName ORIGIN_FILTER_MERGED_ONLY() => OriginFilterMergedOnly;

    public static StringName SELECTION_ROLE_ASSIGNED_CORE() => SelectionRoleAssignedCore;

    public static StringName SELECTION_ROLE_QUALIFIER() => SelectionRoleQualifier;

    public StringName get_normalized_skill_state()
    {
        return
            skill_state == SkillStateLearned
            || skill_state == SkillStateCore
            || skill_state == SkillStateCoreMax
            ? skill_state
            : SkillStateCoreMax;
    }

    public StringName get_normalized_origin_filter()
    {
        return
            origin_filter == OriginFilterAny
            || origin_filter == OriginFilterUnmergedOnly
            || origin_filter == OriginFilterMergedOnly
            ? origin_filter
            : OriginFilterAny;
    }

    public StringName get_normalized_selection_role()
    {
        return
            selection_role == SelectionRoleQualifier || selection_role == SelectionRoleAssignedCore
            ? selection_role
            : SelectionRoleAssignedCore;
    }
}
