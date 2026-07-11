using System;
using Godot;

public sealed class TagRequirementDefinition
{
    private static readonly StringName SkillStateLearned = "learned";
    private static readonly StringName SkillStateCore = "core";
    private static readonly StringName SkillStateCoreMax = "core_max";
    private static readonly StringName OriginFilterAny = "any";
    private static readonly StringName OriginFilterUnmergedOnly = "unmerged_only";
    private static readonly StringName OriginFilterMergedOnly = "merged_only";
    private static readonly StringName SelectionRoleAssignedCore = "assigned_core";
    private static readonly StringName SelectionRoleQualifier = "qualifier";

    public TagRequirementDefinition(
        StringName tag,
        int count,
        StringName skillState,
        StringName originFilter,
        StringName selectionRole
    )
    {
        Tag = tag;
        Count = count;
        SkillState = skillState;
        OriginFilter = originFilter;
        SelectionRole = selectionRole;
    }

    public StringName Tag { get; }
    public int Count { get; }
    public StringName SkillState { get; }
    public StringName OriginFilter { get; }
    public StringName SelectionRole { get; }

    internal TagRequirementSkillState SkillStateKind => ToSkillState(SkillState);
    internal TagRequirementOriginFilter OriginFilterKind => ToOriginFilter(OriginFilter);
    internal TagRequirementSelectionRole SelectionRoleKind => ToSelectionRole(SelectionRole);

    internal static TagRequirementDefinition FromResource(TagRequirement source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        ProgressionDefinitionProjection.RequireKnown(
            source.SkillStateKind != TagRequirementSkillState.Unknown,
            $"{path}.skill_state",
            source.skill_state
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.OriginFilterKind != TagRequirementOriginFilter.Unknown,
            $"{path}.origin_filter",
            source.origin_filter
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.SelectionRoleKind != TagRequirementSelectionRole.Unknown,
            $"{path}.selection_role",
            source.selection_role
        );
        return new TagRequirementDefinition(
            source.tag,
            source.count,
            source.skill_state,
            source.origin_filter,
            source.selection_role
        );
    }

    private static TagRequirementSkillState ToSkillState(StringName value)
    {
        if (value == SkillStateLearned)
            return TagRequirementSkillState.Learned;
        if (value == SkillStateCore)
            return TagRequirementSkillState.Core;
        if (value == SkillStateCoreMax)
            return TagRequirementSkillState.CoreMax;
        return TagRequirementSkillState.Unknown;
    }

    private static TagRequirementOriginFilter ToOriginFilter(StringName value)
    {
        if (value == OriginFilterAny)
            return TagRequirementOriginFilter.Any;
        if (value == OriginFilterUnmergedOnly)
            return TagRequirementOriginFilter.UnmergedOnly;
        if (value == OriginFilterMergedOnly)
            return TagRequirementOriginFilter.MergedOnly;
        return TagRequirementOriginFilter.Unknown;
    }

    private static TagRequirementSelectionRole ToSelectionRole(StringName value)
    {
        if (value == SelectionRoleAssignedCore)
            return TagRequirementSelectionRole.AssignedCore;
        if (value == SelectionRoleQualifier)
            return TagRequirementSelectionRole.Qualifier;
        return TagRequirementSelectionRole.Unknown;
    }
}
