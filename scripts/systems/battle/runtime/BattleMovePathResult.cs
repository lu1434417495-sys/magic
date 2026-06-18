using Godot;
using System.Collections.Generic;

internal sealed class BattleMovePathResult
{
    public bool Allowed { get; init; }
    public int Cost { get; init; }
    public IReadOnlyList<Vector2I> Path { get; init; } = System.Array.Empty<Vector2I>();
    public string Message { get; init; } = "";

    internal Godot.Collections.Array<Vector2I> ToPathArray() =>
        BattleMovePathResultProjection.ToVector2IArray(Path);
}

internal sealed class BattleMovePathTreeResult
{
    public Dictionary<Vector2I, int> Costs { get; } = new();
    public Dictionary<Vector2I, Vector2I> Previous { get; } = new();
    public Dictionary<Vector2I, int> Steps { get; } = new();
}

internal sealed class BattleValidatedMoveExecutionResult
{
    public bool Executed { get; set; }
    public bool ReachedTarget { get; set; }
    public bool StoppedByBarrier { get; set; }
    public List<Vector2I> ExecutedPath { get; } = new();
}
