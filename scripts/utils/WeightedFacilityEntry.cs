using Godot;

[GlobalClass]
public partial class WeightedFacilityEntry : Resource
{
    [Export]
    public string facility_id { get; set; } = "";

    [Export]
    public int weight { get; set; } = 1;

    public string GetFacilityTemplateId()
    {
        return (facility_id ?? string.Empty).Trim();
    }
}
