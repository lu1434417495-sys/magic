using System.Collections.Generic;
using Godot;

internal static class BattleMovePathResultProjection
{
    internal static Godot.Collections.Dictionary Project(BattleMovePathResult result)
    {
        if (result == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["allowed"] = result.Allowed,
            ["cost"] = result.Cost,
            ["path"] = ToVector2IArray(result.Path),
            ["message"] = result.Message,
        };
    }

    internal static Godot.Collections.Dictionary ProjectTree(BattleMovePathTreeResult result)
    {
        if (result == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["costs"] = ToDictionary(result.Costs),
            ["previous"] = ToDictionary(result.Previous),
            ["steps"] = ToDictionary(result.Steps),
        };
    }

    internal static Godot.Collections.Dictionary ProjectExecution(
        BattleValidatedMoveExecutionResult result
    )
    {
        if (result == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["executed"] = result.Executed,
            ["reached_target"] = result.ReachedTarget,
            ["stopped_by_barrier"] = result.StoppedByBarrier,
            ["executed_path"] = ToVector2IArray(result.ExecutedPath),
        };
    }

    internal static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IEnumerable<Vector2I> source
    ) => new Vector2IList(source).ToGodotArray();

    private static Godot.Collections.Dictionary ToDictionary(Dictionary<Vector2I, int> source)
    {
        var result = new Godot.Collections.Dictionary();
        if (source == null)
            return result;
        foreach ((Vector2I key, int value) in source)
        {
            result[key] = value;
        }
        return result;
    }

    private static Godot.Collections.Dictionary ToDictionary(Dictionary<Vector2I, Vector2I> source)
    {
        var result = new Godot.Collections.Dictionary();
        if (source == null)
            return result;
        foreach ((Vector2I key, Vector2I value) in source)
        {
            result[key] = value;
        }
        return result;
    }
}
