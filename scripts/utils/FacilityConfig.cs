using Godot;

[GlobalClass]
public partial class FacilityConfig : Resource
{
    [Export]
    public string facility_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public string category { get; set; } = "";

    [Export]
    public int min_settlement_tier { get; set; } = 0;

    [Export]
    public Godot.Collections.Array<string> allowed_slot_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> bound_service_npcs { get; set; } = new();

    [Export]
    public string interaction_type { get; set; } = "";

    public string GetTemplateId()
    {
        return (facility_id ?? string.Empty).Trim();
    }

    public string GetPrimaryServiceName()
    {
        if (bound_service_npcs == null || bound_service_npcs.Count == 0)
        {
            return (interaction_type ?? string.Empty).Capitalize();
        }

        var firstNpc = bound_service_npcs[0] as FacilityNpcConfig;
        return firstNpc == null
            ? string.Empty
            : (firstNpc.service_type ?? string.Empty).Capitalize();
    }
}
