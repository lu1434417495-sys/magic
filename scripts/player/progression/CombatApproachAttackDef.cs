using Godot;

[GlobalClass]
public partial class CombatApproachAttackDef : Resource
{
    [Export]
    public int maximum_path_height_delta_from_origin { get; set; }
}
