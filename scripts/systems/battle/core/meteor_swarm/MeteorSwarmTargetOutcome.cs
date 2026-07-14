using System.Collections.Generic;
using Godot;

internal sealed class MeteorSwarmTargetOutcome
{
    public StringName target_unit_id { get; set; } = "";
    public Vector2I target_coord { get; set; } = new(-1, -1);
    public StringName target_faction_id { get; set; } = "";
    public int distance_from_anchor { get; set; } = 0;
    public List<MeteorSwarmImpactComponentData> components { get; set; } = new();
    public List<DamageEventResult> damage_events { get; set; } = new();
    public List<StringName> status_effect_ids { get; set; } = new();
    public List<StringName> terrain_effect_ids { get; set; } = new();
    public List<BattleAttackRollModifierSpec> attack_roll_modifier_breakdown { get; set; } =
        new();
    public List<MeteorSwarmComponentFact> report_component_breakdown { get; set; } = new();
    public int total_damage { get; set; } = 0;
    public int total_healing { get; set; } = 0;
    public bool defeated { get; set; } = false;

    internal void AddComponent(MeteorSwarmImpactComponentData component)
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

}
