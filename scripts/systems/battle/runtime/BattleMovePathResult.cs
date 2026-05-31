using Godot;
using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class BattleMovePathResult
{
    public bool Allowed { get; init; }
    public int Cost { get; init; }
    public GVector2IArray Path { get; init; } = new();
    public string Message { get; init; } = "";

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["allowed"] = Allowed,
            ["cost"] = Cost,
            ["path"] = Path ?? new GVector2IArray(),
            ["message"] = Message,
        };
    }
}

public sealed class BattleMovePathTreeResult
{
    public Dictionary<Vector2I, int> Costs { get; } = new();
    public Dictionary<Vector2I, Vector2I> Previous { get; } = new();
    public Dictionary<Vector2I, int> Steps { get; } = new();

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["costs"] = ToGodotDictionary(Costs),
            ["previous"] = ToGodotDictionary(Previous),
            ["steps"] = ToGodotDictionary(Steps),
        };
    }

    private static GDictionary ToGodotDictionary(Dictionary<Vector2I, int> source)
    {
        var result = new GDictionary();
        foreach ((Vector2I key, int value) in source)
        {
            result[key] = value;
        }
        return result;
    }

    private static GDictionary ToGodotDictionary(Dictionary<Vector2I, Vector2I> source)
    {
        var result = new GDictionary();
        foreach ((Vector2I key, Vector2I value) in source)
        {
            result[key] = value;
        }
        return result;
    }
}

public sealed class BattleValidatedMoveExecutionResult
{
    public bool Executed { get; set; }
    public bool ReachedTarget { get; set; }
    public bool StoppedByBarrier { get; set; }
    public GVector2IArray ExecutedPath { get; } = new();

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["executed"] = Executed,
            ["reached_target"] = ReachedTarget,
            ["stopped_by_barrier"] = StoppedByBarrier,
            ["executed_path"] = ExecutedPath,
        };
    }
}
