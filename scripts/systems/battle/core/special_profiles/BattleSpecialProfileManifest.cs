using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSpecialProfileManifest : Resource
{
    [Export] public StringName profile_id = "";
    [Export] public int schema_version = 1;
    [Export] public Array<StringName> owning_skill_ids = new();
    [Export] public StringName runtime_resolver_id = "";
    [Export] public Resource profile_resource = null;
    [Export] public StringName runtime_read_policy = "forbidden";
    [Export] public Dictionary presentation_metadata = new();
    [Export] public Array<string> required_regression_tests = new();
    [Export] public Array<Dictionary> deferred_capabilities = new();
    [Export] public string sunset_warning_date = "";
    [Export] public string sunset_hard_block_date = "";
}
