using Godot;

[GlobalClass]
public partial class MountedSubmapConfig : Resource
{
    [Export]
    public StringName submap_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.File, "*.tres")]
    public string generation_config_path = "";

    [Export]
    public string return_hint_text = "点击任意地点返回原位置。";
}
