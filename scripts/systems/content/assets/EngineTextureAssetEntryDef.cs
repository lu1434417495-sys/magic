using Godot;

[GlobalClass]
public partial class EngineTextureAssetEntryDef : Resource
{
    [Export]
    public StringName asset_id { get; set; } = "";

    [Export]
    public Texture2D texture { get; set; }
}
