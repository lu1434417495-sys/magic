using System;
using System.Collections.Generic;
using Godot;

// Detached command facts. Only ordinary Move commands publish these paths;
// skill relocations keep their own presentation semantics.
internal sealed class BattleMovementPresentation
{
    internal BattleMovementPresentation(StringName unitId, IReadOnlyList<Vector2I> path)
    {
        UnitId = unitId;
        var copy = new Vector2I[path.Count];
        for (int index = 0; index < copy.Length; index++)
            copy[index] = path[index];
        Path = Array.AsReadOnly(copy);
    }

    internal StringName UnitId { get; }
    internal IReadOnlyList<Vector2I> Path { get; }
}
