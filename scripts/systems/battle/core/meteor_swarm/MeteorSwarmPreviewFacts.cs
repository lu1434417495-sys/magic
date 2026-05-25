using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MeteorSwarmPreviewFacts : BattleSpecialProfilePreviewFacts
{
    public int impact_count = 0;
    public int expected_target_count = 0;
    public int expected_terrain_effect_count = 0;
    public int friendly_fire_risk_percent = 0;
    public Array<Dictionary> component_preview = new();
    public Array<Dictionary> target_numeric_summary = new();

    public new Dictionary ToDict()
    {
        var payload = base.ToDict();
        payload["impact_count"] = impact_count;
        payload["expected_target_count"] = expected_target_count;
        payload["expected_terrain_effect_count"] = expected_terrain_effect_count;
        payload["friendly_fire_risk_percent"] = friendly_fire_risk_percent;
        payload["component_preview"] = component_preview.Duplicate(true);
        payload["target_numeric_summary"] = target_numeric_summary.Duplicate(true);
        return payload;
    }
}
