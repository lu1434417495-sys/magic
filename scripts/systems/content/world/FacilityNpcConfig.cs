using Godot;

[GlobalClass]
public partial class FacilityNpcConfig : Resource
{
    [Export]
    public string npc_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public string service_type { get; set; } = "";

    [Export]
    public string interaction_script_id { get; set; } = "";

    [Export]
    public string local_slot_id { get; set; } = "";

    internal FacilityNpcDefinition ToDefinition(string path) =>
        FacilityNpcDefinition.FromResource(this, path);

    public string GetTemplateId()
    {
        return (npc_id ?? string.Empty).Trim();
    }
}
