using Godot;

[GlobalClass]
public partial class ProfessionRankGate : Resource
{
    [Export] public StringName profession_id = "";
    [Export] public int min_rank = 1;
    [Export] public StringName check_mode = "historical";
}
