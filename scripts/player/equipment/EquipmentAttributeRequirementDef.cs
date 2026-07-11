using Godot;

[GlobalClass]
public partial class EquipmentAttributeRequirementDef : Resource
{
    [Export]
    public StringName attribute_id = "";

    [Export(PropertyHint.Range, "0,999,1")]
    public int min_value;

    internal EquipmentAttributeRequirementDefinition ToDefinition() =>
        EquipmentAttributeRequirementDefinition.FromResource(this);
}
