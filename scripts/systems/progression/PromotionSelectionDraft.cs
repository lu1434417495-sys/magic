using System.Collections.Generic;
using Godot;

/// <summary>Domain-only partial selection used while finding a complete request.</summary>
internal sealed class PromotionSelectionDraft
{
    internal static PromotionSelectionDraft Empty { get; } = new();
    internal IReadOnlyList<StringName> AssignedCoreSkillIds { get; }
    internal IReadOnlyList<StringName> QualifierSkillIds { get; }
    internal bool HasAssignedCoreSkillIds => AssignedCoreSkillIds != null;
    internal bool HasQualifierSkillIds => QualifierSkillIds != null;

    internal PromotionSelectionDraft(IReadOnlyList<StringName> assigned = null, IReadOnlyList<StringName> qualifiers = null)
    {
        AssignedCoreSkillIds = assigned;
        QualifierSkillIds = qualifiers;
    }
}
