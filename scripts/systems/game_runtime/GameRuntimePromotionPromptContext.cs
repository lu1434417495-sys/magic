using System;
using System.Collections.Generic;
using Godot;

public sealed class GameRuntimePromotionChoiceContext
{
    private readonly IReadOnlyList<StringName> _grantedSkillIds;
    private readonly PromotionSelectionData _selection;

    public StringName ProfessionId { get; }
    public string DisplayName { get; }
    public string Summary { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> GrantedSkillIds => _grantedSkillIds;
    public string SelectionHint { get; }
    public PromotionSelectionData Selection => CloneSelection(_selection);

    public GameRuntimePromotionChoiceContext(
        StringName professionId,
        string displayName,
        string summary,
        string description,
        IEnumerable<StringName> grantedSkillIds,
        string selectionHint,
        PromotionSelectionData selection
    )
    {
        ProfessionId = professionId;
        DisplayName = displayName ?? "";
        Summary = summary ?? "";
        Description = description ?? "";
        _grantedSkillIds = Array.AsReadOnly(
            grantedSkillIds != null
                ? new List<StringName>(grantedSkillIds).ToArray()
                : Array.Empty<StringName>()
        );
        SelectionHint = selectionHint ?? "";
        _selection = CloneSelection(selection);
    }

    internal Dictionary<string, object> ToPlainSnapshot()
    {
        List<object> grantedSkillIds = new(_grantedSkillIds.Count);
        foreach (StringName skillId in _grantedSkillIds)
            grantedSkillIds.Add(skillId.ToString());
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["profession_id"] = ProfessionId.ToString(),
            ["display_name"] = DisplayName,
            ["summary"] = Summary,
            ["description"] = Description,
            ["granted_skill_ids"] = grantedSkillIds,
            ["selection_hint"] = SelectionHint,
            ["selection"] = _selection.ToPlainPayload(),
        };
    }

    internal bool ContainsSelection(PromotionSelectionData selection) =>
        _selection.SelectionEquals(selection);

    private static PromotionSelectionData CloneSelection(PromotionSelectionData selection)
    {
        selection ??= PromotionSelectionData.Empty;
        return new PromotionSelectionData(
            selection.AssignedCoreSkillIds,
            selection.QualifierSkillIds,
            selection.TriggerSkillIds,
            selection.HasAssignedCoreSkillIds,
            selection.HasQualifierSkillIds,
            selection.HasTriggerSkillIds
        );
    }
}

public sealed class GameRuntimePromotionPromptContext
{
    private readonly IReadOnlyList<GameRuntimePromotionChoiceContext> _choices;

    public static GameRuntimePromotionPromptContext Empty { get; } =
        new("", "", Array.Empty<GameRuntimePromotionChoiceContext>());

    public StringName MemberId { get; }
    public string MemberName { get; }
    public IReadOnlyList<GameRuntimePromotionChoiceContext> Choices => _choices;
    public bool IsEmpty => MemberId == "" || _choices.Count == 0;

    public GameRuntimePromotionPromptContext(
        StringName memberId,
        string memberName,
        IEnumerable<GameRuntimePromotionChoiceContext> choices
    )
    {
        MemberId = memberId;
        MemberName = memberName ?? "";
        _choices = Array.AsReadOnly(
            choices != null
                ? new List<GameRuntimePromotionChoiceContext>(choices)
                    .FindAll(choice => choice != null)
                    .ToArray()
                : Array.Empty<GameRuntimePromotionChoiceContext>()
        );
    }

    public bool TryGetChoice(
        StringName professionId,
        out GameRuntimePromotionChoiceContext choice
    )
    {
        foreach (GameRuntimePromotionChoiceContext candidate in _choices)
        {
            if (candidate.ProfessionId != professionId)
                continue;
            choice = candidate;
            return true;
        }
        choice = null;
        return false;
    }

    public bool ContainsChoice(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        return MemberId == memberId
            && TryGetChoice(professionId, out GameRuntimePromotionChoiceContext choice)
            && choice.ContainsSelection(selection);
    }

    public IReadOnlyDictionary<string, object> ToPlainSnapshot()
    {
        if (IsEmpty)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        List<object> choices = new(_choices.Count);
        foreach (GameRuntimePromotionChoiceContext choice in _choices)
            choices.Add(choice.ToPlainSnapshot());
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["member_id"] = MemberId.ToString(),
            ["member_name"] = MemberName,
            ["choices"] = choices,
        };
    }
}
