using Godot;

[GlobalClass]
public partial class WorldMapSettlementBundle : Resource
{
    [Export]
    public Godot.Collections.Array<Resource> settlement_library { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> facility_library { get; set; } = new();
}
