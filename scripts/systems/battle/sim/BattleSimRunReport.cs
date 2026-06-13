using Godot;
using System.Collections.Generic;

public sealed class BattleSimRunReport
{
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

    public Godot.Collections.Dictionary Metrics { get; set; } = new();

    public IReadOnlyList<BattleAiTurnTraceProjection> AiTurnTraces { get; set; } =
        System.Array.Empty<BattleAiTurnTraceProjection>();

    public Godot.Collections.Array FinalUnits { get; set; } = new();

    internal Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            ["scenario_id"] = ScenarioId,
            ["profile_id"] = ProfileId,
            ["seed"] = Seed,
            ["battle_id"] = BattleId,
            ["battle_ended"] = BattleEnded,
            ["winner_faction_id"] = WinnerFactionId,
            ["final_tu"] = FinalTu,
            ["iterations"] = Iterations,
            ["idle_loops"] = IdleLoops,
            ["timeline_steps"] = TimelineSteps,
            ["ally_alive"] = AllyAlive,
            ["enemy_alive"] = EnemyAlive,
            ["metrics"] = Metrics?.Duplicate(true) ?? new Godot.Collections.Dictionary(),
            ["ai_turn_traces"] = ToGodotTraceArray(AiTurnTraces),
            ["final_units"] = FinalUnits?.Duplicate(true) ?? new Godot.Collections.Array(),
        };

    private static Godot.Collections.Array ToGodotTraceArray(
        IReadOnlyList<BattleAiTurnTraceProjection> traces
    )
    {
        var result = new Godot.Collections.Array();
        foreach (BattleAiTurnTraceProjection trace in traces ?? System.Array.Empty<BattleAiTurnTraceProjection>())
            result.Add(TraceDictionaryProjection.ToDictionary(trace?.ToTraceDictionary() ?? new Dictionary<string, object>()));
        return result;
    }
}
