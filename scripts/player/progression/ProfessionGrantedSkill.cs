using Godot;

[GlobalClass]
public partial class ProfessionGrantedSkill : Resource
{
    [Export]
    public StringName skill_id = "";

    [Export]
    public int unlock_rank = 1;

    [Export]
    public StringName skill_type = "active";
}
