using System.Collections.Generic;
using Godot.Collections;
using GArray = Godot.Collections.Array;

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

    public GArray ToGodotArray()
    {
        var result = new GArray();
        foreach (BattleContributionEvent contributionEvent in _events)
        {
            result.Add(contributionEvent.to_dictionary());
        }
        return result;
    }

    public IEnumerable<BattleContributionEvent> Events => _events;
}
