using Godot;
using System.Collections.Generic;

public sealed class BattleSimRunReport
{
    private Godot.Collections.Dictionary _metrics = new();
    private Godot.Collections.Array _finalUnits = new();

    public string ScenarioId { get; set; } = "";

    public string ProfileId { get; set; } = "";

    public long Seed { get; set; }

    public string BattleId { get; set; } = "";

    public bool BattleEnded { get; set; }

    public string WinnerFactionId { get; set; } = "";

    public int FinalTu { get; set; }

    public int Iterations { get; set; }

    public int IdleLoops { get; set; }

    public int TimelineSteps { get; set; }

    public int AllyAlive { get; set; }

    public int EnemyAlive { get; set; }

    public Godot.Collections.Dictionary Metrics
    {
        get => (Godot.Collections.Dictionary)_metrics.Duplicate(true);
        set => _metrics = value?.Duplicate(true) ?? new Godot.Collections.Dictionary();
    }

    public IReadOnlyList<BattleAiTurnTraceProjection> AiTurnTraces { get; set; } =
        System.Array.Empty<BattleAiTurnTraceProjection>();

    public Godot.Collections.Array FinalUnits
    {
        get => (Godot.Collections.Array)_finalUnits.Duplicate(true);
        set => _finalUnits = value?.Duplicate(true) ?? new Godot.Collections.Array();
    }

}
