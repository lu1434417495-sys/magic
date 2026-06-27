using Godot;
using System.Collections.Generic;

public sealed class BattleSimRunReport
{
    private BattleSimMetricsSnapshot _metricsSnapshot = BattleSimMetricsSnapshot.Empty();
    private List<object> _finalUnits = new();

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

    public BattleSimMetricsSnapshot MetricsSnapshot
    {
        get => _metricsSnapshot;
        set => _metricsSnapshot = value ?? BattleSimMetricsSnapshot.Empty();
    }

    public Godot.Collections.Dictionary Metrics
    {
        get => _metricsSnapshot.ToDictionary();
        set => _metricsSnapshot = BattleSimMetricsSnapshot.FromDictionary(value);
    }

    public IReadOnlyList<BattleAiTurnTraceProjection> AiTurnTraces { get; set; } =
        System.Array.Empty<BattleAiTurnTraceProjection>();

    public Godot.Collections.Array FinalUnits
    {
        get => RuntimePlainPayload.ProjectArray(
            _finalUnits,
            "BattleSimRunReport.FinalUnits.get"
        );
        set =>
            _finalUnits = RuntimePlainPayload.NormalizeArray(
                value,
                "BattleSimRunReport.FinalUnits.set"
            );
    }

}
