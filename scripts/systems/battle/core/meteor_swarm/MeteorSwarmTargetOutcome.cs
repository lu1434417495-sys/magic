using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class MeteorSwarmTargetOutcome
{
    public StringName target_unit_id { get; set; } = "";
    public Vector2I target_coord { get; set; } = new(-1, -1);
    public StringName target_faction_id { get; set; } = "";
    public int distance_from_anchor { get; set; } = 0;
    public List<MeteorSwarmImpactComponent> components { get; set; } = new();
    public List<DamageEventResult> damage_events { get; set; } = new();
    public List<StringName> status_effect_ids { get; set; } = new();
    public List<StringName> terrain_effect_ids { get; set; } = new();
    public List<BattleAttackRollModifierSpec> attack_roll_modifier_breakdown { get; set; } =
        new();
    public List<MeteorSwarmComponentFact> report_component_breakdown { get; set; } = new();
    public int total_damage { get; set; } = 0;
    public int total_healing { get; set; } = 0;
    public bool defeated { get; set; } = false;

    internal void AddComponent(MeteorSwarmImpactComponent component)
    {
        if (component != null)
            components.Add(component);
    }

    internal void AddStatusEffectId(StringName status_id)
    {
        if (status_id == (StringName)"" || status_effect_ids.Contains(status_id))
            return;
        status_effect_ids.Add(status_id);
    }

    internal GDictionary ToSummaryDictionary()
    {
        var statusEffectIds = new GStringNameArray();
        foreach (StringName statusId in status_effect_ids)
            statusEffectIds.Add(statusId);
        var componentBreakdown = new GDictArray();
        foreach (MeteorSwarmComponentFact component in report_component_breakdown)
        {
            if (component != null)
                componentBreakdown.Add(component.ToDictionary());
        }
        return new GDictionary
        {
            ["target_unit_id"] = target_unit_id.ToString(),
            ["target_coord"] = target_coord,
            ["target_faction_id"] = target_faction_id.ToString(),
            ["distance_from_anchor"] = distance_from_anchor,
            ["total_damage"] = total_damage,
            ["total_healing"] = total_healing,
            ["defeated"] = defeated,
            ["status_effect_ids"] = statusEffectIds,
            ["component_breakdown"] = componentBreakdown,
        };
    }
}
