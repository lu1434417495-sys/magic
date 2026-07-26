using Godot;

public sealed class WorldMapCellData
{
    public Vector2I coord;
    public Vector2I chunk_coord;
    public string occupant_id = "";
    public string footprint_root_id = "";

    public WorldMapCellData() { }

    public WorldMapCellData(Vector2I cellCoord = default, Vector2I cellChunkCoord = default)
    {
        coord = cellCoord;
        chunk_coord = cellChunkCoord;
    }
}
