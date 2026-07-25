using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class PendingBattleTerrainGenerator : BattleTerrainGenerator
{
    private const int PendingFailCalls = 8;

    private int _generateCallCount;

    internal override bool EmptyGenerationIsPending => true;

    internal override BattleTerrainLayout GenerateTyped(
        EncounterAnchorData encounterAnchor,
        long seed,
        GDictionary context
    )
    {
        _generateCallCount++;
        if (_generateCallCount <= PendingFailCalls)
            return new BattleTerrainLayout();

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
        return new BattleTerrainLayout(
            mapSize,
            cells,
            new[] { new Vector2I(0, 0) },
            new[] { new Vector2I(2, 1) },
            new Vector2I(0, 0),
            new Vector2I(2, 1),
            "default"
        );
    }
}
