using Godot;
using System.Collections.Generic;

public sealed class BattleMovePathResult
{
    public bool Allowed { get; init; }
    public int Cost { get; init; }
    public IReadOnlyList<Vector2I> Path { get; init; } = System.Array.Empty<Vector2I>();
    public string Message { get; init; } = "";

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["allowed"] = Allowed,
            ["cost"] = Cost,
            ["path"] = ToVector2IArray(Path),
            ["message"] = Message,
        };
    }

    internal Godot.Collections.Array<Vector2I> ToPathArray() => ToVector2IArray(Path);

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IEnumerable<Vector2I> source
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
        if (source == null)
        {
            return result;
        }
        foreach (Vector2I coord in source)
        {
            result.Add(coord);
        }
        return result;
    }
}

public sealed class BattleMovePathTreeResult
{
    public Dictionary<Vector2I, int> Costs { get; } = new();
    public Dictionary<Vector2I, Vector2I> Previous { get; } = new();
    public Dictionary<Vector2I, int> Steps { get; } = new();

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["costs"] = ToDictionary(Costs),
            ["previous"] = ToDictionary(Previous),
            ["steps"] = ToDictionary(Steps),
        };
    }

    private static Godot.Collections.Dictionary ToDictionary(Dictionary<Vector2I, int> source)
    {
        var result = new Godot.Collections.Dictionary();
        foreach ((Vector2I key, int value) in source)
        {
            result[key] = value;
        }
        return result;
    }

    private static Godot.Collections.Dictionary ToDictionary(Dictionary<Vector2I, Vector2I> source)
    {
        var result = new Godot.Collections.Dictionary();
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
    public List<Vector2I> ExecutedPath { get; } = new();

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["executed"] = Executed,
            ["reached_target"] = ReachedTarget,
            ["stopped_by_barrier"] = StoppedByBarrier,
            ["executed_path"] = ToVector2IArray(ExecutedPath),
        };
    }

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IEnumerable<Vector2I> source
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
        if (source == null)
        {
            return result;
        }
        foreach (Vector2I coord in source)
        {
            result.Add(coord);
        }
        return result;
    }
}
