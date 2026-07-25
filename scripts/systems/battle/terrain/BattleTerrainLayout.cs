using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleTerrainLayout : IDisposable
{
    private Dictionary<Vector2I, BattleCellState> _cells;
    private List<Vector2I> _allySpawns;
    private List<Vector2I> _enemySpawns;
    private bool _cellsTaken;
    private bool _disposed;

    internal BattleTerrainLayout()
        : this(
            Vector2I.Zero,
            new Dictionary<Vector2I, BattleCellState>(),
            Array.Empty<Vector2I>(),
            Array.Empty<Vector2I>(),
            Vector2I.Zero,
            Vector2I.Zero,
            ""
        )
    {
    }

    internal BattleTerrainLayout(
        Vector2I mapSize,
        Dictionary<Vector2I, BattleCellState> cells,
        IEnumerable<Vector2I> allySpawns,
        IEnumerable<Vector2I> enemySpawns,
        Vector2I playerCoord,
        Vector2I enemyCoord,
        StringName terrainProfileId
    )
    {
        MapSize = mapSize;
        _cells = cells ?? new Dictionary<Vector2I, BattleCellState>();
        foreach ((Vector2I coord, BattleCellState cell) in _cells)
            cell?.SetCoord(coord);
        _allySpawns = new List<Vector2I>(allySpawns ?? Array.Empty<Vector2I>());
        _enemySpawns = new List<Vector2I>(enemySpawns ?? Array.Empty<Vector2I>());
        PlayerCoord = playerCoord;
        EnemyCoord = enemyCoord;
        TerrainProfileId = terrainProfileId;
    }

    internal Vector2I MapSize { get; }
    internal IReadOnlyDictionary<Vector2I, BattleCellState> Cells => _cells;
    internal IReadOnlyList<Vector2I> AllySpawns => _allySpawns;
    internal IReadOnlyList<Vector2I> EnemySpawns => _enemySpawns;
    internal Vector2I PlayerCoord { get; }
    internal Vector2I EnemyCoord { get; }
    internal StringName TerrainProfileId { get; }
    internal bool IsEmpty => _cells.Count == 0;

    internal Dictionary<Vector2I, BattleCellState> TakeCells()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cellsTaken)
            throw new InvalidOperationException(
                "Battle terrain cells have already been transferred."
            );
        _cellsTaken = true;
        Dictionary<Vector2I, BattleCellState> cells = _cells;
        _cells = new Dictionary<Vector2I, BattleCellState>();
        return cells;
    }

    internal void OverrideAllySpawns(IEnumerable<Vector2I> coords)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _allySpawns = new List<Vector2I>(coords ?? Array.Empty<Vector2I>());
    }

    internal void OverrideEnemySpawns(IEnumerable<Vector2I> coords)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _enemySpawns = new List<Vector2I>(coords ?? Array.Empty<Vector2I>());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        var disposedCells = new HashSet<BattleCellState>();
        foreach (BattleCellState cell in _cells.Values)
        {
            if (cell != null && disposedCells.Add(cell))
                BattleCellState.DisposeRuntimeGraph(cell);
        }
        _cells.Clear();
        _allySpawns.Clear();
        _enemySpawns.Clear();
    }
}
