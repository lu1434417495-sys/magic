using System;

public partial class PartyState
{
    public PartyState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class QuestState
{
    public QuestState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}
