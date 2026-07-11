using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public sealed class MeteorSwarmProfileData
{
    private MeteorSwarmProfileData(
        StringName profileId,
        string profileResourcePath,
        StringName coverageShapeId,
        int radius,
        int profileVersion,
        IReadOnlyList<MeteorSwarmImpactComponentData> impactComponents,
        StringName concussedStatusId,
        IReadOnlyList<MeteorSwarmTerrainProfileData> terrainProfiles,
        int friendlyFireSoftExpectedHpPercent,
        int friendlyFireHardExpectedHpPercent,
        int friendlyFireHardWorstCaseHpPercent
    )
    {
        profile_id = profileId;
        profile_resource_path = profileResourcePath
            ?? throw new ArgumentNullException(nameof(profileResourcePath));
        coverage_shape_id = coverageShapeId;
        this.radius = radius;
        profile_version = profileVersion;
        impact_components = FreezeImpactComponents(impactComponents);
        concussed_status_id = concussedStatusId;
        terrain_profiles = FreezeTerrainProfiles(terrainProfiles);
        friendly_fire_soft_expected_hp_percent = friendlyFireSoftExpectedHpPercent;
        friendly_fire_hard_expected_hp_percent = friendlyFireHardExpectedHpPercent;
        friendly_fire_hard_worst_case_hp_percent = friendlyFireHardWorstCaseHpPercent;
    }

    public StringName profile_id { get; }
    public string profile_resource_path { get; }
    public StringName coverage_shape_id { get; }
    public int radius { get; }
    public int profile_version { get; }
    public IReadOnlyList<MeteorSwarmImpactComponentData> impact_components { get; }
    public StringName concussed_status_id { get; }
    public IReadOnlyList<MeteorSwarmTerrainProfileData> terrain_profiles { get; }
    public int friendly_fire_soft_expected_hp_percent { get; }
    public int friendly_fire_hard_expected_hp_percent { get; }
    public int friendly_fire_hard_worst_case_hp_percent { get; }

    internal IReadOnlyList<MeteorSwarmTerrainProfileData> GetTerrainProfilesForRing(int ring)
    {
        var result = new List<MeteorSwarmTerrainProfileData>();
        foreach (MeteorSwarmTerrainProfileData terrainProfile in terrain_profiles)
        {
            if (ring >= terrainProfile.ring_min && ring <= terrainProfile.ring_max)
            {
                result.Add(MeteorSwarmTerrainProfileData.CopyOf(terrainProfile));
            }
        }
        return new ReadOnlyCollection<MeteorSwarmTerrainProfileData>(result);
    }

    internal static MeteorSwarmProfileData FromResource(
        StringName profileId,
        MeteorSwarmProfile profile
    )
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profileId == null || string.IsNullOrEmpty(profileId.ToString()))
        {
            throw new ArgumentException("Meteor swarm profile id must not be empty.", nameof(profileId));
        }

        string resourcePath = profile.ResourcePath;
        if (resourcePath == null)
        {
            throw new InvalidDataException("Meteor swarm profile ResourcePath must not be null.");
        }
        string ownerPath = resourcePath.Length > 0 ? resourcePath : profileId.ToString();

        var impactComponents = new List<MeteorSwarmImpactComponentData>();
        if (profile.impact_components == null)
        {
            throw Invalid(ownerPath + ".impact_components", "collection is null");
        }
        for (int index = 0; index < profile.impact_components.Count; index++)
        {
            MeteorSwarmImpactComponent component = profile.impact_components[index];
            if (component == null)
            {
                throw Invalid(
                    $"{ownerPath}.impact_components[{index}]",
                    "resource is null"
                );
            }
            impactComponents.Add(
                MeteorSwarmImpactComponentData.FromResource(
                    component,
                    $"{ownerPath}.impact_components[{index}]"
                )
            );
        }

        var terrainProfiles = new List<MeteorSwarmTerrainProfileData>();
        if (profile.terrain_profiles == null)
        {
            throw Invalid(ownerPath + ".terrain_profiles", "collection is null");
        }
        for (int index = 0; index < profile.terrain_profiles.Count; index++)
        {
            Variant terrainProfileValue = profile.terrain_profiles[index];
            if (terrainProfileValue.VariantType != Variant.Type.Dictionary)
            {
                throw Invalid(
                    $"{ownerPath}.terrain_profiles[{index}]",
                    $"expected Dictionary, got {terrainProfileValue.VariantType}"
                );
            }
            Godot.Collections.Dictionary terrainProfile =
                terrainProfileValue.AsGodotDictionary();
            terrainProfiles.Add(
                MeteorSwarmTerrainProfileData.FromDictionary(
                    terrainProfile,
                    $"{ownerPath}.terrain_profiles[{index}]"
                )
            );
        }

        return new MeteorSwarmProfileData(
            profileId,
            resourcePath,
            profile.coverage_shape_id,
            profile.radius,
            profile.profile_version,
            impactComponents,
            profile.concussed_status_id,
            terrainProfiles,
            profile.friendly_fire_soft_expected_hp_percent,
            profile.friendly_fire_hard_expected_hp_percent,
            profile.friendly_fire_hard_worst_case_hp_percent
        );
    }

    internal static MeteorSwarmProfileData CopyOf(MeteorSwarmProfileData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new MeteorSwarmProfileData(
            source.profile_id,
            source.profile_resource_path,
            source.coverage_shape_id,
            source.radius,
            source.profile_version,
            source.impact_components,
            source.concussed_status_id,
            source.terrain_profiles,
            source.friendly_fire_soft_expected_hp_percent,
            source.friendly_fire_hard_expected_hp_percent,
            source.friendly_fire_hard_worst_case_hp_percent
        );
    }

    private static IReadOnlyList<MeteorSwarmImpactComponentData> FreezeImpactComponents(
        IReadOnlyList<MeteorSwarmImpactComponentData> values
    )
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new List<MeteorSwarmImpactComponentData>(values.Count);
        foreach (MeteorSwarmImpactComponentData value in values)
        {
            if (value == null)
                throw new ArgumentException("Impact component list must not contain null.", nameof(values));
            result.Add(MeteorSwarmImpactComponentData.CopyOf(value));
        }
        return new ReadOnlyCollection<MeteorSwarmImpactComponentData>(result);
    }

    private static IReadOnlyList<MeteorSwarmTerrainProfileData> FreezeTerrainProfiles(
        IReadOnlyList<MeteorSwarmTerrainProfileData> values
    )
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new List<MeteorSwarmTerrainProfileData>(values.Count);
        foreach (MeteorSwarmTerrainProfileData value in values)
        {
            if (value == null)
                throw new ArgumentException("Terrain profile list must not contain null.", nameof(values));
            result.Add(MeteorSwarmTerrainProfileData.CopyOf(value));
        }
        return new ReadOnlyCollection<MeteorSwarmTerrainProfileData>(result);
    }

    private static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid authored meteor swarm content at '{path}': {message}.");
}

public sealed class MeteorSwarmImpactComponentData
{
    private readonly IReadOnlyDictionary<string, double> _ringDamageScaleBp;

    private MeteorSwarmImpactComponentData(
        StringName componentId,
        StringName roleLabel,
        StringName damageTag,
        int basePower,
        int diceCount,
        int diceSides,
        double ringWeight,
        StringName saveProfileId,
        bool canCrit,
        double masteryWeight,
        int ringMin,
        int ringMax,
        IReadOnlyDictionary<string, double> ringDamageScaleBp
    )
    {
        component_id = componentId;
        role_label = roleLabel;
        damage_tag = damageTag;
        base_power = basePower;
        dice_count = diceCount;
        dice_sides = diceSides;
        ring_weight = ringWeight;
        save_profile_id = saveProfileId;
        can_crit = canCrit;
        mastery_weight = masteryWeight;
        ring_min = ringMin;
        ring_max = ringMax;
        ArgumentNullException.ThrowIfNull(ringDamageScaleBp);
        var copiedScales = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach ((string distance, double scale) in ringDamageScaleBp)
            copiedScales[distance] = scale;
        _ringDamageScaleBp = new ReadOnlyDictionary<string, double>(copiedScales);
    }

    public StringName component_id { get; }
    public StringName role_label { get; }
    public StringName damage_tag { get; }
    public int base_power { get; }
    public int dice_count { get; }
    public int dice_sides { get; }
    public double ring_weight { get; }
    public StringName save_profile_id { get; }
    public bool can_crit { get; }
    public double mastery_weight { get; }
    public int ring_min { get; }
    public int ring_max { get; }

    internal static MeteorSwarmImpactComponentData FromResource(
        MeteorSwarmImpactComponent component,
        string ownerPath
    )
    {
        ArgumentNullException.ThrowIfNull(component);
        if (ownerPath == null)
            throw new ArgumentNullException(nameof(ownerPath));

        var ringDamageScaleBp = new Dictionary<string, double>(StringComparer.Ordinal);
        if (component.ring_damage_scale_bp == null)
        {
            throw new InvalidDataException(
                $"Invalid authored meteor swarm content at '{ownerPath}.ring_damage_scale_bp': collection is null."
            );
        }
        foreach (Variant key in component.ring_damage_scale_bp.Keys)
        {
            ringDamageScaleBp[key.ToString()] = component.ring_damage_scale_bp[key].AsDouble();
        }

        return new MeteorSwarmImpactComponentData(
            component.component_id,
            component.role_label,
            component.damage_tag,
            component.base_power,
            component.dice_count,
            component.dice_sides,
            component.ring_weight,
            component.save_profile_id,
            component.can_crit,
            component.mastery_weight,
            component.ring_min,
            component.ring_max,
            ringDamageScaleBp
        );
    }

    internal static MeteorSwarmImpactComponentData CopyOf(
        MeteorSwarmImpactComponentData source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new MeteorSwarmImpactComponentData(
            source.component_id,
            source.role_label,
            source.damage_tag,
            source.base_power,
            source.dice_count,
            source.dice_sides,
            source.ring_weight,
            source.save_profile_id,
            source.can_crit,
            source.mastery_weight,
            source.ring_min,
            source.ring_max,
            source._ringDamageScaleBp
        );
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
    private readonly BattleAttackRollModifierSpec _accuracyModifierSpec;

    private MeteorSwarmTerrainProfileData(
        StringName terrainProfileId,
        int ringMin,
        int ringMax,
        StringName tickEffectType,
        StringName lifetimePolicy,
        int moveCostDelta,
        StringName moveCostStackKey,
        StringName moveCostStackMode,
        StringName renderOverlayId,
        int overlayPriority,
        int durationTu,
        int tickIntervalTu,
        BattleAttackRollModifierSpec accuracyModifierSpec
    )
    {
        terrain_profile_id = terrainProfileId;
        ring_min = ringMin;
        ring_max = ringMax;
        tick_effect_type = tickEffectType;
        lifetime_policy = lifetimePolicy;
        move_cost_delta = moveCostDelta;
        move_cost_stack_key = moveCostStackKey;
        move_cost_stack_mode = moveCostStackMode;
        render_overlay_id = renderOverlayId;
        overlay_priority = overlayPriority;
        duration_tu = durationTu;
        tick_interval_tu = tickIntervalTu;
        _accuracyModifierSpec = accuracyModifierSpec?.Clone();
    }

    public StringName terrain_profile_id { get; }
    public int ring_min { get; }
    public int ring_max { get; }
    public StringName tick_effect_type { get; }
    public StringName lifetime_policy { get; }
    public int move_cost_delta { get; }
    public StringName move_cost_stack_key { get; }
    public StringName move_cost_stack_mode { get; }
    public StringName render_overlay_id { get; }
    public int overlay_priority { get; }
    public int duration_tu { get; }
    public int tick_interval_tu { get; }

    internal static MeteorSwarmTerrainProfileData FromDictionary(
        Godot.Collections.Dictionary source,
        string ownerPath
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ownerPath == null)
            throw new ArgumentNullException(nameof(ownerPath));
        return new MeteorSwarmTerrainProfileData(
            ReadStringName(source, "terrain_profile_id"),
            ReadInt(source, "ring_min", 0),
            ReadInt(source, "ring_max", 0),
            ReadStringName(source, "tick_effect_type", "none"),
            ReadStringName(source, "lifetime_policy", "timed"),
            ReadInt(source, "move_cost_delta", 0),
            ReadStringName(source, "move_cost_stack_key", ""),
            ReadStringName(source, "move_cost_stack_mode", ""),
            ReadStringName(source, "render_overlay_id"),
            ReadInt(source, "overlay_priority", 0),
            ReadInt(source, "duration_tu", 0),
            ReadInt(source, "tick_interval_tu", 0),
            BuildAccuracyModifierSpec(source, ownerPath)
        );
    }

    internal static MeteorSwarmTerrainProfileData CopyOf(
        MeteorSwarmTerrainProfileData source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new MeteorSwarmTerrainProfileData(
            source.terrain_profile_id,
            source.ring_min,
            source.ring_max,
            source.tick_effect_type,
            source.lifetime_policy,
            source.move_cost_delta,
            source.move_cost_stack_key,
            source.move_cost_stack_mode,
            source.render_overlay_id,
            source.overlay_priority,
            source.duration_tu,
            source.tick_interval_tu,
            source._accuracyModifierSpec
        );
    }

    internal BattleAttackRollModifierSpec CloneAccuracyModifierSpec() =>
        _accuracyModifierSpec?.Clone();

    private static BattleAttackRollModifierSpec BuildAccuracyModifierSpec(
        Godot.Collections.Dictionary source,
        string ownerPath
    )
    {
        if (!source.ContainsKey("accuracy_modifier_spec"))
            return null;
        Variant value = source["accuracy_modifier_spec"];
        if (value.VariantType != Variant.Type.Dictionary)
        {
            throw new InvalidDataException(
                $"Invalid authored meteor swarm content at '{ownerPath}.accuracy_modifier_spec': expected Dictionary, got {value.VariantType}."
            );
        }
        Godot.Collections.Dictionary spec = value.AsGodotDictionary();
        return spec.Count == 0 ? null : BattleAttackRollModifierSpec.FromPartialDictionary(spec);
    }

    private static int ReadInt(
        Godot.Collections.Dictionary source,
        string key,
        int fallback
    )
    {
        if (string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(source[key]);
    }
}
