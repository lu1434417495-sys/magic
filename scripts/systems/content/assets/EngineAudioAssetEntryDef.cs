using Godot;

[GlobalClass]
public partial class EngineAudioAssetEntryDef : Resource
{
    [Export]
    public StringName asset_id { get; set; } = "";

    [Export]
    public AudioStream audio { get; set; }
}
