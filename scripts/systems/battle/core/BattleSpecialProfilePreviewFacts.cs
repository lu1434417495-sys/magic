using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public class BattleSpecialProfilePreviewFacts
{
    public StringName profile_id = "";
    public StringName skill_id = "";
    public StringName preview_fact_id = "";
    public string nominal_plan_signature = "";
    public string final_plan_signature = "";
    public Vector2I resolved_anchor_coord = new(-1, -1);
    public List<StringName> target_unit_ids = new();
    public List<Vector2I> target_coords = new();
    public MeteorSwarmTerrainSummaryFact terrain_summary = new();
    public List<MeteorSwarmNumericSummary> friendly_fire_numeric_summary = new();
    public List<BattleAttackRollModifierSpec> attack_roll_modifier_breakdown = new();

    internal virtual Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["profile_id"] = profile_id.ToString(),
            ["skill_id"] = skill_id.ToString(),
            ["preview_fact_id"] = preview_fact_id.ToString(),
            ["nominal_plan_signature"] = nominal_plan_signature ?? "",
            ["final_plan_signature"] = final_plan_signature ?? "",
            ["resolved_anchor_coord"] = resolved_anchor_coord,
            ["target_unit_ids"] = ToTraceStringNameList(target_unit_ids),
            ["target_coords"] = ToTraceVector2IList(target_coords),
            ["terrain_summary"] =
                terrain_summary?.ToTraceDictionary()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            ["friendly_fire_numeric_summary"] = ToTraceNumericSummaryList(
                friendly_fire_numeric_summary
            ),
            ["attack_roll_modifier_breakdown"] = ToTraceAttackRollModifierBreakdownList(
                attack_roll_modifier_breakdown
            ),
        };
    }

    internal IReadOnlyList<MeteorSwarmNumericSummary> GetFriendlyFireNumericSummary() =>
        friendly_fire_numeric_summary;

    internal IReadOnlyList<BattleAttackRollModifierSpec> GetAttackRollModifierBreakdown() =>
        attack_roll_modifier_breakdown;

    internal virtual int GetExpectedTerrainEffectCount() => 0;

    protected static List<StringName> ToTraceStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            if (value != "")
            {
                result.Add(value);
            }
        }
        return result;
    }

    protected static List<Vector2I> ToTraceVector2IList(IEnumerable<Vector2I> values)
    {
        var result = new List<Vector2I>();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    protected static List<object> ToTraceNumericSummaryList(
        IEnumerable<MeteorSwarmNumericSummary> values
    )
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (MeteorSwarmNumericSummary value in values)
        {
            if (value != null)
            {
                result.Add(value.ToTraceDictionary());
            }
        }
        return result;
    }

    protected static List<object> ToTraceAttackRollModifierBreakdownList(
        IEnumerable<BattleAttackRollModifierSpec> values
    )
    {
        var result = new List<object>();
        foreach (
            BattleAttackRollModifierSpec value
            in values ?? System.Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (value != null)
            {
                result.Add(value.ToTraceDictionary());
            }
        }
        return result;
    }
}
