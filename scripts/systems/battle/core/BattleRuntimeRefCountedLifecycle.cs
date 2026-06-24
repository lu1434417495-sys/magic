using System;

public partial class BattleCellState
{
    public BattleCellState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class BattleEdgeFaceState
{
    public BattleEdgeFaceState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class BattleEffectiveTraitInstanceState
{
    public BattleEffectiveTraitInstanceState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class BattleState
{
    public BattleState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class BattleStatusEffectState
{
    public BattleStatusEffectState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class BattleTerrainEffectState
{
    public BattleTerrainEffectState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}
