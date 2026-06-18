using System.Collections.Generic;
using Godot;

public sealed class MeteorSwarmTerrainSummaryFact
{
    public StringName coverage_shape_id { get; set; } = "";
    public int radius { get; set; } = 0;
    public int affected_coord_count { get; set; } = 0;
    public int terrain_effect_count { get; set; } = 0;
    public int crater_count { get; set; } = 0;
    public int rubble_count { get; set; } = 0;
    public int dust_count { get; set; } = 0;

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["coverage_shape_id"] = coverage_shape_id.ToString(),
            ["radius"] = radius,
            ["affected_coord_count"] = affected_coord_count,
            ["terrain_effect_count"] = terrain_effect_count,
            ["crater_count"] = crater_count,
            ["rubble_count"] = rubble_count,
            ["dust_count"] = dust_count,
        };
    }
}
