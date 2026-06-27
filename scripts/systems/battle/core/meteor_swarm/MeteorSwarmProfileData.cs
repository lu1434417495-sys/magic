using System;
using System.Collections.Generic;
using Godot;

public sealed class MeteorSwarmProfileData
{
    public StringName profile_id { get; set; } = "";
    public string profile_resource_path { get; set; } = "";
    public StringName coverage_shape_id { get; set; } = "square_7x7";
    public int radius { get; set; } = 3;
    public int profile_version { get; set; } = 1;
    public List<MeteorSwarmImpactComponentData> impact_components { get; } = new();
    public StringName concussed_status_id { get; set; } = "meteor_concussed";
    public List<MeteorSwarmTerrainProfileData> terrain_profiles { get; } = new();
    public int friendly_fire_soft_expected_hp_percent { get; set; } = 10;
    public int friendly_fire_hard_expected_hp_percent { get; set; } = 25;
    public int friendly_fire_hard_worst_case_hp_percent { get; set; } = 50;

    internal IEnumerable<MeteorSwarmTerrainProfileData> GetTerrainProfilesForRing(int ring)
    {
        foreach (MeteorSwarmTerrainProfileData terrainProfile in terrain_profiles)
        {
            if (terrainProfile == null)
            {
                continue;
            }
            if (ring >= terrainProfile.ring_min && ring <= terrainProfile.ring_max)
            {
                yield return terrainProfile;
            }
        }
    }

    internal static MeteorSwarmProfileData FromResource(
        StringName profileId,
        MeteorSwarmProfile profile
    )
    {
        if (profile == null)
        {
            return null;
        }
        var result = new MeteorSwarmProfileData
        {
            profile_id = profileId,
            profile_resource_path = profile.ResourcePath ?? "",
            coverage_shape_id = profile.coverage_shape_id,
            radius = profile.radius,
            profile_version = profile.profile_version,
            concussed_status_id = profile.concussed_status_id,
            friendly_fire_soft_expected_hp_percent =
                profile.friendly_fire_soft_expected_hp_percent,
            friendly_fire_hard_expected_hp_percent =
                profile.friendly_fire_hard_expected_hp_percent,
            friendly_fire_hard_worst_case_hp_percent =
                profile.friendly_fire_hard_worst_case_hp_percent,
        };
        foreach (MeteorSwarmImpactComponent component in profile.impact_components)
        {
            MeteorSwarmImpactComponentData projected =
                MeteorSwarmImpactComponentData.FromResource(component);
            if (projected != null)
            {
                result.impact_components.Add(projected);
            }
        }
        foreach (Variant terrainProfileValue in profile.terrain_profiles)
        {
            Godot.Collections.Dictionary terrainProfile =
                terrainProfileValue.AsGodotDictionary();
            MeteorSwarmTerrainProfileData projected =
                MeteorSwarmTerrainProfileData.FromDictionary(terrainProfile);
            if (projected != null)
            {
                result.terrain_profiles.Add(projected);
            }
        }
        return result;
    }
}

public sealed class MeteorSwarmImpactComponentData
{
    public StringName component_id { get; set; } = "";
    public StringName role_label { get; set; } = "";
    public StringName damage_tag { get; set; } = "";
    public int base_power { get; set; }
    public int dice_count { get; set; }
    public int dice_sides { get; set; }
    public double ring_weight { get; set; } = 1.0;
    public StringName save_profile_id { get; set; } = "";
    public bool can_crit { get; set; }
    public double mastery_weight { get; set; } = 1.0;
    public int ring_min { get; set; }
    public int ring_max { get; set; } = 3;
    private readonly Dictionary<string, double> _ringDamageScaleBp = new(StringComparer.Ordinal);

    internal static MeteorSwarmImpactComponentData FromResource(
        MeteorSwarmImpactComponent component
    )
    {
        if (component == null)
        {
            return null;
        }
        var result = new MeteorSwarmImpactComponentData
        {
            component_id = component.component_id,
            role_label = component.role_label,
            damage_tag = component.damage_tag,
            base_power = component.base_power,
            dice_count = component.dice_count,
            dice_sides = component.dice_sides,
            ring_weight = component.ring_weight,
            save_profile_id = component.save_profile_id,
            can_crit = component.can_crit,
            mastery_weight = component.mastery_weight,
            ring_min = component.ring_min,
            ring_max = component.ring_max,
        };
        foreach (Variant key in component.ring_damage_scale_bp.Keys)
        {
            if (!component.ring_damage_scale_bp.ContainsKey(key))
            {
                continue;
            }
            result._ringDamageScaleBp[key.ToString()] =
                component.ring_damage_scale_bp[key].AsDouble();
        }
        return result;
    }

    public bool AppliesToDistance(int distance_from_anchor, bool center_direct = false)
    {
        if (component_id == (StringName)"center_direct")
            return center_direct;
        return distance_from_anchor >= ring_min && distance_from_anchor <= ring_max;
    }

    public double GetDamageScale(int distance_from_anchor)
    {
        string key = distance_from_anchor.ToString();
        double fallback = Math.Round(ring_weight * 10000.0);
        double rawValue = _ringDamageScaleBp.TryGetValue(key, out double configured)
            ? configured
            : fallback;
        return Math.Max(rawValue / 10000.0, 0.0);
    }

    public int GetAverageBaseDamage(int distance_from_anchor)
    {
        double diceAverage = Math.Max(dice_count, 0) * (Math.Max(dice_sides, 0) + 1.0) / 2.0;
        return Math.Max(
            (int)Math.Round((base_power + diceAverage) * GetDamageScale(distance_from_anchor)),
            0
        );
    }

    public int GetWorstCaseBaseDamage(int distance_from_anchor)
    {
        int diceWorst = Math.Max(dice_count, 0) * Math.Max(dice_sides, 0);
        return Math.Max(
            (int)Math.Round((base_power + diceWorst) * GetDamageScale(distance_from_anchor)),
            0
        );
    }
}

public sealed class MeteorSwarmTerrainProfileData
{
    public StringName terrain_profile_id { get; set; } = "";
    public int ring_min { get; set; }
    public int ring_max { get; set; }
    public StringName tick_effect_type { get; set; } = "none";
    public StringName lifetime_policy { get; set; } = "timed";
    public int move_cost_delta { get; set; }
    public StringName move_cost_stack_key { get; set; } = "";
    public StringName move_cost_stack_mode { get; set; } = "";
    public StringName render_overlay_id { get; set; } = "";
    public int overlay_priority { get; set; }
    public int duration_tu { get; set; }
    public int tick_interval_tu { get; set; }
    public BattleAttackRollModifierSpec accuracy_modifier_spec { get; set; }

    internal static MeteorSwarmTerrainProfileData FromDictionary(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null)
        {
            return null;
        }
        return new MeteorSwarmTerrainProfileData
        {
            terrain_profile_id = ReadStringName(source, "terrain_profile_id"),
            ring_min = ReadInt(source, "ring_min", 0),
            ring_max = ReadInt(source, "ring_max", 0),
            tick_effect_type = ReadStringName(source, "tick_effect_type", "none"),
            lifetime_policy = ReadStringName(source, "lifetime_policy", "timed"),
            move_cost_delta = ReadInt(source, "move_cost_delta", 0),
            move_cost_stack_key = ReadStringName(source, "move_cost_stack_key", ""),
            move_cost_stack_mode = ReadStringName(source, "move_cost_stack_mode", ""),
            render_overlay_id = ReadStringName(source, "render_overlay_id"),
            overlay_priority = ReadInt(source, "overlay_priority", 0),
            duration_tu = ReadInt(source, "duration_tu", 0),
            tick_interval_tu = ReadInt(source, "tick_interval_tu", 0),
            accuracy_modifier_spec = BuildAccuracyModifierSpec(source),
        };
    }

    internal BattleAttackRollModifierSpec CloneAccuracyModifierSpec()
    {
        return accuracy_modifier_spec?.Clone();
    }

    private static BattleAttackRollModifierSpec BuildAccuracyModifierSpec(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null || !source.ContainsKey("accuracy_modifier_spec"))
        {
            return null;
        }
        Godot.Collections.Dictionary spec = source["accuracy_modifier_spec"].AsGodotDictionary();
        return spec == null || spec.Count == 0
            ? null
            : BattleAttackRollModifierSpec.FromPartialDictionary(spec);
    }

    private static int ReadInt(
        Godot.Collections.Dictionary source,
        string key,
        int fallback
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return fallback ?? new StringName("");
        }
        return ProgressionDataUtils.to_string_name(source[key]);
    }
}
