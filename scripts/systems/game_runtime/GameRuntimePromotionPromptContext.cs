using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class GameRuntimePromotionChoiceContext
{
    private readonly IReadOnlyList<StringName> _grantedSkillIds;
    private readonly PromotionCommitRequest _selection;

    public StringName ProfessionId { get; }
    public string DisplayName { get; }
    public string Summary { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> GrantedSkillIds => _grantedSkillIds;
    public string SelectionHint { get; }
    public PromotionCommitRequest Selection => _selection;

    public GameRuntimePromotionChoiceContext(
        StringName professionId,
        string displayName,
        string summary,
        string description,
        IEnumerable<StringName> grantedSkillIds,
        string selectionHint,
        PromotionCommitRequest selection
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
        _selection = selection;
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

    internal bool ContainsSelection(PromotionCommitRequest selection) =>
        _selection.SelectionEquals(selection);

    internal GameRuntimePromotionChoiceContext WithPromptId(string promptId) => new(
        ProfessionId, DisplayName, Summary, Description, GrantedSkillIds, SelectionHint, _selection.WithPromptId(promptId));

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
        MemberId = memberId ?? "";
        MemberName = memberName ?? "";
        string promptId = Guid.NewGuid().ToString("N");
        _choices = Array.AsReadOnly(
            choices != null
                ? new List<GameRuntimePromotionChoiceContext>(choices)
                    .FindAll(choice => choice?.Selection?.IsWellFormed == true)
                    .Select(choice => choice.WithPromptId(promptId)).ToArray()
                : Array.Empty<GameRuntimePromotionChoiceContext>()
        );
    }

    public bool TryGetChoice(StringName professionId, out GameRuntimePromotionChoiceContext choice,
        StringName triggerSkillId = default)
    {
        choice = null;
        triggerSkillId ??= "";
        foreach (GameRuntimePromotionChoiceContext candidate in _choices)
        {
            if (candidate.ProfessionId != professionId
                || (triggerSkillId != "" && candidate.Selection.GrowthTriggerSkillId != triggerSkillId)) continue;
            if (choice != null) { choice = null; return false; }
            choice = candidate;
        }
        return choice != null;
    }

    public bool ContainsChoice(StringName memberId, StringName professionId, PromotionCommitRequest selection) =>
        MemberId == memberId && selection != null
        && _choices.Any(choice => choice.ProfessionId == professionId && choice.ContainsSelection(selection));

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
