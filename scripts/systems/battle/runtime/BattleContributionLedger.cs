using System.Collections.Generic;

public sealed class BattleContributionLedger
{
    private readonly List<BattleContributionEvent> _events = new();

    public int Count => _events.Count;

    public void Clear()
    {
        _events.Clear();
    }

    public void Add(BattleContributionEvent contributionEvent)
    {
        if (contributionEvent == null)
        {
            return;
        }
        _events.Add(contributionEvent);
    }

    public Godot.Collections.Array ToDictionaryArray()
    {
        var result = new Godot.Collections.Array();
        foreach (BattleContributionEvent contributionEvent in _events)
        {
            result.Add(contributionEvent.ToDictionary());
        }
        return result;
    }

    public IReadOnlyList<BattleContributionEvent> Events => _events;
}
