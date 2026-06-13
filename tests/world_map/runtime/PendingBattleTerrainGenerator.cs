using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class PendingBattleTerrainGenerator : BattleTerrainGenerator
{
    private const int PendingFailCalls = 8;

    private int _generateCallCount;

    public override GDictionary GenerateTyped(
        EncounterAnchorData encounterAnchor,
        long seed,
        GDictionary context
    )
    {
        _generateCallCount++;
        if (_generateCallCount <= PendingFailCalls)
            return new GDictionary();

        Vector2I mapSize = new(3, 2);
        GDictionary cells = new();
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

        return new GDictionary
        {
            ["map_size"] = mapSize,
            ["cells"] = cells,
            ["cell_columns"] = BattleCellState.BuildColumnsFromSurfaceCells(cells),
            ["ally_spawns"] = new Godot.Collections.Array<Vector2I> { new(0, 0) },
            ["enemy_spawns"] = new Godot.Collections.Array<Vector2I> { new(2, 1) },
            ["terrain_profile_id"] = new StringName("default"),
        };
    }
}
