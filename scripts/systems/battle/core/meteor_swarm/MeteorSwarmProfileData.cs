#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class MeteorSwarmProfileData
{
    private MeteorSwarmProfileData(
        StringName profileId,
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
        if (profileId == "")
            throw new ArgumentException("Meteor swarm profile id must not be empty.", nameof(profileId));
        profile_id = profileId;
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
                result.Add(MeteorSwarmTerrainProfileData.CopyOf(terrainProfile));
        }
        return new ReadOnlyCollection<MeteorSwarmTerrainProfileData>(result);
    }

    internal static MeteorSwarmProfileData Create(
        StringName profileId,
        StringName coverageShapeId,
        int radius,
        int profileVersion,
        IReadOnlyList<MeteorSwarmImpactComponentData> impactComponents,
        StringName concussedStatusId,
        IReadOnlyList<MeteorSwarmTerrainProfileData> terrainProfiles,
        int friendlyFireSoftExpectedHpPercent,
        int friendlyFireHardExpectedHpPercent,
        int friendlyFireHardWorstCaseHpPercent
    ) =>
        new(
            profileId,
            coverageShapeId,
            radius,
            profileVersion,
            impactComponents,
            concussedStatusId,
            terrainProfiles,
            friendlyFireSoftExpectedHpPercent,
            friendlyFireHardExpectedHpPercent,
            friendlyFireHardWorstCaseHpPercent
        );

    internal static MeteorSwarmProfileData CopyOf(MeteorSwarmProfileData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create(
            source.profile_id,
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
            if (value is null)
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
            if (value is null)
                throw new ArgumentException("Terrain profile list must not contain null.", nameof(values));
            result.Add(MeteorSwarmTerrainProfileData.CopyOf(value));
        }
        return new ReadOnlyCollection<MeteorSwarmTerrainProfileData>(result);
    }
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
        _ringDamageScaleBp = new ReadOnlyDictionary<string, double>(
            new Dictionary<string, double>(ringDamageScaleBp, StringComparer.Ordinal)
        );
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

    internal static MeteorSwarmImpactComponentData Create(
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
    ) =>
        new(
            componentId,
            roleLabel,
            damageTag,
            basePower,
            diceCount,
            diceSides,
            ringWeight,
            saveProfileId,
            canCrit,
            masteryWeight,
            ringMin,
            ringMax,
            ringDamageScaleBp
        );

    internal static MeteorSwarmImpactComponentData CopyOf(MeteorSwarmImpactComponentData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create(
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

    public bool AppliesToDistance(int distance_from_anchor, bool center_direct = false) =>
        component_id == (StringName)"center_direct"
            ? center_direct
            : distance_from_anchor >= ring_min && distance_from_anchor <= ring_max;

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
        return Math.Max((int)Math.Round((base_power + diceAverage) * GetDamageScale(distance_from_anchor)), 0);
    }

    public int GetWorstCaseBaseDamage(int distance_from_anchor)
    {
        int diceWorst = Math.Max(dice_count, 0) * Math.Max(dice_sides, 0);
        return Math.Max((int)Math.Round((base_power + diceWorst) * GetDamageScale(distance_from_anchor)), 0);
    }
}

public sealed class MeteorSwarmTerrainProfileData
{
    private readonly BattleAttackRollModifierSpec? _accuracyModifierSpec;

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
        BattleAttackRollModifierSpec? accuracyModifierSpec
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

    internal static MeteorSwarmTerrainProfileData Create(
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
        BattleAttackRollModifierSpec? accuracyModifierSpec
    ) =>
        new(
            terrainProfileId,
            ringMin,
            ringMax,
            tickEffectType,
            lifetimePolicy,
            moveCostDelta,
            moveCostStackKey,
            moveCostStackMode,
            renderOverlayId,
            overlayPriority,
            durationTu,
            tickIntervalTu,
            accuracyModifierSpec
        );

    internal static MeteorSwarmTerrainProfileData CopyOf(MeteorSwarmTerrainProfileData source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create(
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

    internal BattleAttackRollModifierSpec? CloneAccuracyModifierSpec() =>
        _accuracyModifierSpec?.Clone();
}
