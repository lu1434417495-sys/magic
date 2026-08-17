using Godot;

[GlobalClass]
public partial class EngineSceneAssetEntryDef : Resource
{
    [Export]
    public StringName asset_id { get; set; } = "";

    [Export]
    public PackedScene scene { get; set; }
}
