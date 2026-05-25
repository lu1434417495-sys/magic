using Godot;

[GlobalClass]
public partial class WorldPreset : RefCounted
{
    public StringName preset_id { get; set; } = "";
    public string display_name { get; set; } = "";
    public string size_label { get; set; } = "";
    public string generation_config_path { get; set; } = "";

    public WorldPreset(StringName presetId, string displayName, string sizeLabel, string generationConfigPath)
    {
        preset_id = presetId;
        display_name = displayName;
        size_label = sizeLabel;
        generation_config_path = generationConfigPath;
    }
}
