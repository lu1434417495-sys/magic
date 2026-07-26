using Godot;

[GlobalClass]
public partial class WorldMapSettlementBundle : Resource
{
    [Export]
    public Godot.Collections.Array<Resource> settlement_library { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> facility_library { get; set; } = new();

    internal Godot.Collections.Array<Resource> SettlementLibraryProjectionBorrowed =>
        settlement_library;
    internal Godot.Collections.Array<Resource> FacilityLibraryProjectionBorrowed =>
        facility_library;

    internal WorldMapSettlementBundleDefinition ToDefinition(string path) =>
        WorldMapSettlementBundleDefinition.FromResource(this, path);
}
