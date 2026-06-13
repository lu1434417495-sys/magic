using System.Collections.Generic;
using Godot;

using GDictionary = Godot.Collections.Dictionary;

internal sealed class MeteorSwarmPreviewFacts : BattleSpecialProfilePreviewFacts
{
    public int impact_count = 0;
    public int expected_target_count = 0;
    public int expected_terrain_effect_count = 0;
    public int friendly_fire_risk_percent = 0;
    public List<MeteorSwarmComponentFact> component_preview = new();
    public List<MeteorSwarmNumericSummary> target_numeric_summaries = new();
    public List<MeteorSwarmNumericSummary> friendly_fire_numeric_summaries = new();

    internal override GDictionary ToDict()
    {
        var payload = base.ToDict();
        payload["impact_count"] = impact_count;
        payload["expected_target_count"] = expected_target_count;
        payload["expected_terrain_effect_count"] = expected_terrain_effect_count;
        payload["friendly_fire_risk_percent"] = friendly_fire_risk_percent;
        payload["component_preview"] = ToDictionaryArray(component_preview);
        payload["target_numeric_summary"] = MeteorSwarmNumericSummary.ToDictionaryArray(
            target_numeric_summaries
        );
        return payload;
    }

    internal override Dictionary<string, object> ToTraceDictionary()
    {
        Dictionary<string, object> result = base.ToTraceDictionary();
        result["impact_count"] = impact_count;
        result["expected_target_count"] = expected_target_count;
        result["expected_terrain_effect_count"] = expected_terrain_effect_count;
        result["friendly_fire_risk_percent"] = friendly_fire_risk_percent;
        result["component_preview"] = ToTraceComponentFactList(component_preview);
        result["target_numeric_summary"] = ToTraceNumericSummaryList(target_numeric_summaries);
        return result;
    }

    internal IReadOnlyList<MeteorSwarmNumericSummary> GetTargetNumericSummariesTyped() =>
        target_numeric_summaries;

    internal IReadOnlyList<MeteorSwarmNumericSummary> GetFriendlyFireNumericSummariesTyped() =>
        friendly_fire_numeric_summaries;

    internal override int GetExpectedTerrainEffectCount() => expected_terrain_effect_count;

    private static Godot.Collections.Array<Godot.Collections.Dictionary> ToDictionaryArray(
        IEnumerable<MeteorSwarmComponentFact> values
    )
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (values == null)
        {
            return result;
        }
        foreach (MeteorSwarmComponentFact value in values)
        {
            if (value != null)
            {
                result.Add(value.ToDictionary());
            }
        }
        return result;
    }

    private static List<object> ToTraceComponentFactList(
        IEnumerable<MeteorSwarmComponentFact> values
    )
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (MeteorSwarmComponentFact value in values)
        {
            if (value != null)
            {
                result.Add(value.ToTraceDictionary());
            }
        }
        return result;
    }
}
