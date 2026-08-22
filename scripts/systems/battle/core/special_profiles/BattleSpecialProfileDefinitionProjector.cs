#nullable enable

using System;
using System.Linq;
using Godot;

internal static class BattleSpecialProfileDefinitionProjector
{
    internal static BattleSpecialProfileManifestDefinition ProjectManifest(
        BattleSpecialProfileManifestImportModel import
    )
    {
        ArgumentNullException.ThrowIfNull(import);
        return new BattleSpecialProfileManifestDefinition(
            new StringName(import.ProfileId),
            import.SchemaVersion,
            import.OwningSkillIds.Select(value => new StringName(value)).ToArray(),
            new StringName(import.RuntimeResolverId),
            new StringName(import.RuntimeReadPolicy),
            import.DisplayName,
            new StringName(import.CoverageShapeId),
            import.Radius,
            import.DeferredCapabilities
                .Select(value => new BattleSpecialProfileDeferredCapabilityDefinition(
                    new StringName(value.CapabilityId),
                    new StringName(value.Status)
                ))
                .ToArray(),
            import.SunsetWarningDate,
            import.SunsetHardBlockDate
        );
    }

    internal static MeteorSwarmProfileData ProjectMeteorSwarm(
        BattleSpecialProfileImportModel import
    )
    {
        ArgumentNullException.ThrowIfNull(import);
        if (import.Kind != BattleSpecialProfileImportKind.MeteorSwarm)
            throw new InvalidOperationException($"Unsupported special profile kind {import.Kind}.");
        MeteorSwarmProfileImportModel profile = import.MeteorSwarm;
        return MeteorSwarmProfileData.Create(
            new StringName(import.ProfileId),
            new StringName(profile.CoverageShapeId),
            profile.Radius,
            profile.ProfileVersion,
            profile.ImpactComponents.Select(ProjectImpactComponent).ToArray(),
            new StringName(profile.ConcussedStatusId),
            profile.TerrainProfiles.Select(ProjectTerrainProfile).ToArray(),
            profile.FriendlyFireSoftExpectedHpPercent,
            profile.FriendlyFireHardExpectedHpPercent,
            profile.FriendlyFireHardWorstCaseHpPercent
        );
    }

    private static MeteorSwarmImpactComponentData ProjectImpactComponent(
        MeteorSwarmImpactComponentImportModel component
    ) =>
        MeteorSwarmImpactComponentData.Create(
            new StringName(component.ComponentId),
            new StringName(component.RoleLabel),
            new StringName(component.DamageTag),
            component.BasePower,
            component.DiceCount,
            component.DiceSides,
            component.RingWeight,
            new StringName(component.SaveProfileId),
            component.CanCrit,
            component.MasteryWeight,
            component.RingMin,
            component.RingMax,
            component.RingDamageScaleBasisPoints
        );

    private static MeteorSwarmTerrainProfileData ProjectTerrainProfile(
        MeteorSwarmTerrainProfileImportModel terrain
    ) =>
        MeteorSwarmTerrainProfileData.Create(
            new StringName(terrain.TerrainProfileId),
            terrain.RingMin,
            terrain.RingMax,
            new StringName(terrain.TickEffectType),
            new StringName(terrain.LifetimePolicy),
            terrain.MoveCostDelta,
            new StringName(terrain.MoveCostStackKey),
            new StringName(terrain.MoveCostStackMode),
            new StringName(terrain.RenderOverlayId),
            terrain.OverlayPriority,
            terrain.DurationTu,
            terrain.TickIntervalTu,
            ProjectAccuracyModifier(terrain.AccuracyModifierSpec)
        );

    private static BattleAttackRollModifierSpec? ProjectAccuracyModifier(
        BattleAttackRollModifierImportModel? value
    ) =>
        value is null
            ? null
            : new BattleAttackRollModifierSpec
            {
                source_domain = new StringName(value.SourceDomain),
                label = value.Label,
                modifier_delta = value.ModifierDelta,
                stack_key = new StringName(value.StackKey),
                stack_mode = new StringName(value.StackMode),
                roll_kind_filter = new StringName(value.RollKindFilter),
                endpoint_mode = new StringName(value.EndpointMode),
                distance_min_exclusive = value.DistanceMinExclusive,
                distance_max_inclusive = value.DistanceMaxInclusive,
                target_team_filter = new StringName(value.TargetTeamFilter),
                footprint_mode = new StringName(value.FootprintMode),
                applies_to = new StringName(value.AppliesTo),
            };
}
