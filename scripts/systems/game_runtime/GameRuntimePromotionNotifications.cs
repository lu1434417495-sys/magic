using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Session-only presentation state. Eligibility and consumed growth remain domain facts.</summary>
internal sealed class GameRuntimePromotionNotifications
{
    internal readonly record struct Opportunity(
        StringName MemberId, StringName ProfessionId, int TargetRank, StringName SkillId);

    private HashSet<Opportunity> _available = new();
    private readonly HashSet<Opportunity> _presented = new();
    private List<StringName> _members = new();
    private List<StringName> _unpresentedMembers = new();
    private long _revision = -1;
    private bool _dirty = true;

    internal int MemberCount => _members.Count;
    internal string Hint { get; private set; } = "";
    internal IReadOnlyList<StringName> UnpresentedMembers => _unpresentedMembers;

    internal void Invalidate() => _dirty = true;
    internal bool NeedsRefresh(long revision) => _dirty || _revision != revision;

    internal bool Refresh(long revision, IEnumerable<Opportunity> opportunities,
        IReadOnlyDictionary<StringName, string> memberNames)
    {
        var available = new HashSet<Opportunity>(opportunities);
        bool changed = !_available.SetEquals(available);
        _available = available;
        _presented.IntersectWith(_available);
        _members = _available.Select(offer => offer.MemberId).Distinct()
            .OrderBy(id => (string)id, System.StringComparer.Ordinal).ToList();
        RefreshUnpresentedMembers();
        Hint = _members.Count == 0 ? "" :
            $"可晋升：{string.Join("、", _members.Select(id => memberNames[id]))}。按 G 或点击晋升提示打开。";
        _revision = revision;
        _dirty = false;
        return changed;
    }

    internal void MarkPresented(GameRuntimePromotionPromptContext prompt)
    {
        foreach (var choice in prompt.Choices)
            _presented.Add(new Opportunity(prompt.MemberId, choice.ProfessionId,
                choice.Selection.TargetRank, choice.Selection.GrowthTriggerSkillId));
        RefreshUnpresentedMembers();
    }

    private void RefreshUnpresentedMembers() => _unpresentedMembers = _members.Where(
        member => _available.Any(offer => offer.MemberId == member && !_presented.Contains(offer))).ToList();

    internal void Clear()
    {
        _available.Clear();
        _presented.Clear();
        _members.Clear();
        _unpresentedMembers.Clear();
        Hint = "";
        _revision = -1;
        _dirty = true;
    }
}
