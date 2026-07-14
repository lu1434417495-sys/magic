using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class PendingBattleTerrainGenerator : BattleTerrainGenerator
{
    private const int PendingFailCalls = 8;

    private int _generateCallCount;

    internal override GodotProjectionLease<GDictionary> GenerateLease(
        EncounterAnchorData encounterAnchor,
        long seed,
        GDictionary context,
        LifetimeDomain domain = LifetimeDomain.Battle
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "pending-battle-terrain",
                domain,
                "PendingBattleTerrainGenerator.GenerateLease"
            );
        _generateCallCount++;
        if (_generateCallCount <= PendingFailCalls)
            return lease;

        Vector2I mapSize = new(3, 2);
        var cells = new Dictionary<Vector2I, BattleCellState>();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = "land",
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                cells[cell.coord] = cell;
            }
        }
        var cellColumns = BattleCellState.BuildColumnsFromSurfaceCells(cells);

        Godot.Collections.Array allySpawns = lease.Own(
            new Godot.Collections.Array { new Vector2I(0, 0) },
            "PendingBattleTerrainGenerator.GenerateLease.ally_spawns"
        );
        Godot.Collections.Array enemySpawns = lease.Own(
            new Godot.Collections.Array { new Vector2I(2, 1) },
            "PendingBattleTerrainGenerator.GenerateLease.enemy_spawns"
        );
        root["map_size"] = mapSize;
        root["cells"] = BattleCellState.ProjectCellsToPayload(lease, cells);
        root["cell_columns"] = BattleCellState.ProjectColumnsToPayload(lease, cellColumns);
        root["ally_spawns"] = allySpawns;
        root["enemy_spawns"] = enemySpawns;
        root["terrain_profile_id"] = new StringName("default");
        return lease;
    }
}
