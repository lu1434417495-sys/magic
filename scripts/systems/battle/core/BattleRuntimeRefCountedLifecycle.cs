using System;

public partial class BattleState
{
    public BattleState()
    {
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(this, GetType().Name);
    }
}
