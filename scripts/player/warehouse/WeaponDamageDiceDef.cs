using Godot;

[GlobalClass]
public partial class WeaponDamageDiceDef : Resource
{
    [Export]
    public int dice_count = 1;

    [Export]
    public int dice_sides = 6;

    [Export]
    public int flat_bonus = 0;

    internal WeaponDamageDiceDefinition ToDefinition() =>
        WeaponDamageDiceDefinition.FromResource(this);
}
