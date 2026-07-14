using Godot;

[GlobalClass]
public partial class FacilitySlotConfig : Resource
{
    [Export]
    public string slot_id { get; set; } = "";

    [Export]
    public Vector2I local_coord { get; set; } = Vector2I.Zero;

    [Export]
    public string slot_tag { get; set; } = "";

    [Export]
    public bool required { get; set; } = false;

    internal FacilitySlotDefinition ToDefinition(string path) =>
        FacilitySlotDefinition.FromResource(this, path);
}
