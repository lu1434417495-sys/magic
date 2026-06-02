using Godot;

public sealed class WorldMapOccupantState
{
    public readonly string OccupantId;
    public readonly string FootprintRootId;

    public WorldMapOccupantState(string occupantId, string footprintRootId = "")
    {
        OccupantId = occupantId ?? "";
        FootprintRootId = string.IsNullOrEmpty(footprintRootId) ? OccupantId : footprintRootId;
    }
}
