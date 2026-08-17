using Godot;

[GlobalClass]
public partial class EngineShaderAssetEntryDef : Resource
{
    [Export]
    public StringName asset_id { get; set; } = "";

    [Export]
    public Shader shader { get; set; }
}
