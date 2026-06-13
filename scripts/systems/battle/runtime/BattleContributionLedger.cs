using System.Collections.Generic;

internal sealed class BattleContributionLedger
{
    private readonly List<BattleContributionEvent> _events = new();

    public int Count => _events.Count;

    internal void Clear()
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

    public IReadOnlyList<BattleContributionEvent> Events => _events;
}
