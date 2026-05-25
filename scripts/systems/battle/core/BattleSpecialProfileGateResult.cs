using Godot;

[GlobalClass]
public partial class BattleSpecialProfileGateResult : RefCounted
{
    public bool allowed;
    public StringName profile_id = "";
    public StringName skill_id = "";
    public StringName block_code = "";
    public string player_message = "";
    public Godot.Collections.Dictionary debug_details = new();
}
