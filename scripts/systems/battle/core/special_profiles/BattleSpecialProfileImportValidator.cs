#nullable enable

using System;
using System.Collections.Generic;

internal static class BattleSpecialProfileImportValidator
{
    private static readonly HashSet<string> AllowedMeteorSaveProfileIds = new(
        new[] { "", "meteor_dex_half" },
        StringComparer.Ordinal
    );

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateManifest(
        JsonContentEntryContext context,
        BattleSpecialProfileManifestImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.ProfileId);
        if (import.SchemaVersion != BattleSpecialProfileJsonDomains.SchemaVersion)
            Unsupported(diagnostics, context, "schema_version", "/schema_version");
        if (import.OwningSkillIds.Count == 0)
            Required(diagnostics, context, "owning_skill_ids must be non-empty.", "/owning_skill_ids");
        AddUniqueIds(diagnostics, context, import.OwningSkillIds, "/owning_skill_ids", "owning skill id");
        RequireText(diagnostics, context, import.RuntimeResolverId, "/runtime_resolver_id", "runtime_resolver_id");
        if (!string.Equals(import.RuntimeReadPolicy, "forbidden", StringComparison.Ordinal))
            Unsupported(diagnostics, context, "runtime_read_policy", "/runtime_read_policy");
        if (string.Equals(import.ProfileId, "meteor_swarm", StringComparison.Ordinal)
            && !string.Equals(import.RuntimeResolverId, "meteor_swarm", StringComparison.Ordinal))
        {
            Unsupported(diagnostics, context, "meteor_swarm runtime_resolver_id", "/runtime_resolver_id");
        }
        RequireText(diagnostics, context, import.DisplayName, "/presentation_metadata/display_name", "display_name");
        RequireText(diagnostics, context, import.CoverageShapeId, "/presentation_metadata/coverage_shape_id", "coverage_shape_id");
        if (import.Radius <= 0)
            OutOfRange(diagnostics, context, "presentation radius must be positive.", "/presentation_metadata/radius");
        for (int index = 0; index < import.DeferredCapabilities.Count; index += 1)
        {
            BattleSpecialProfileDeferredCapabilityImportModel capability = import.DeferredCapabilities[index];
            RequireText(diagnostics, context, capability.CapabilityId, $"/deferred_capabilities/{index}/capability_id", "capability_id");
            RequireText(diagnostics, context, capability.Status, $"/deferred_capabilities/{index}/status", "status");
        }
        ValidateOptionalIsoDate(diagnostics, context, import.SunsetWarningDate, "/sunset_warning_date");
        ValidateOptionalIsoDate(diagnostics, context, import.SunsetHardBlockDate, "/sunset_hard_block_date");
        return diagnostics;
    }

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateProfile(
        JsonContentEntryContext context,
        BattleSpecialProfileImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.ProfileId);
        if (import.Kind != BattleSpecialProfileImportKind.MeteorSwarm)
        {
            Unsupported(diagnostics, context, "special profile kind", "/profile/kind");
            return diagnostics;
        }
        if (!string.Equals(import.ProfileId, "meteor_swarm", StringComparison.Ordinal))
            Unsupported(diagnostics, context, "meteor_swarm profile_id", "/profile_id");

        ValidateMeteorSwarm(diagnostics, context, import.MeteorSwarm);
        return diagnostics;
    }

    private static void ValidateMeteorSwarm(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        MeteorSwarmProfileImportModel profile
    )
    {
        if (!string.Equals(profile.CoverageShapeId, "square_7x7", StringComparison.Ordinal))
            Unsupported(diagnostics, context, "coverage_shape_id", "/profile/payload/coverage_shape_id");
        if (profile.Radius != 3)
            OutOfRange(diagnostics, context, "meteor swarm radius must be 3.", "/profile/payload/radius");
        if (profile.ProfileVersion != 1)
            Unsupported(diagnostics, context, "profile_version", "/profile/payload/profile_version");
        RequireText(diagnostics, context, profile.ConcussedStatusId, "/profile/payload/concussed_status_id", "concussed_status_id");
        if (profile.ImpactComponents.Count == 0)
            Required(diagnostics, context, "impact_components must be non-empty.", "/profile/payload/impact_components");
        if (profile.TerrainProfiles.Count == 0)
            Required(diagnostics, context, "terrain_profiles must be non-empty.", "/profile/payload/terrain_profiles");
        if (profile.FriendlyFireSoftExpectedHpPercent < 0)
            OutOfRange(diagnostics, context, "soft friendly-fire threshold must be >= 0.", "/profile/payload/friendly_fire_soft_expected_hp_percent");
        if (profile.FriendlyFireHardExpectedHpPercent < profile.FriendlyFireSoftExpectedHpPercent)
            OutOfRange(diagnostics, context, "hard expected threshold must be >= soft threshold.", "/profile/payload/friendly_fire_hard_expected_hp_percent");
        if (profile.FriendlyFireHardWorstCaseHpPercent < profile.FriendlyFireHardExpectedHpPercent)
            OutOfRange(diagnostics, context, "hard worst-case threshold must be >= hard expected threshold.", "/profile/payload/friendly_fire_hard_worst_case_hp_percent");

        var componentIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < profile.ImpactComponents.Count; index += 1)
        {
            MeteorSwarmImpactComponentImportModel component = profile.ImpactComponents[index];
            string pointer = $"/profile/payload/impact_components/{index}";
            RequireText(diagnostics, context, component.ComponentId, $"{pointer}/component_id", "component_id");
            if (!string.IsNullOrWhiteSpace(component.ComponentId) && !componentIds.Add(component.ComponentId))
                Duplicate(diagnostics, context, $"Duplicate component_id '{component.ComponentId}'.", $"{pointer}/component_id");
            RequireText(diagnostics, context, component.RoleLabel, $"{pointer}/role_label", "role_label");
            RequireText(diagnostics, context, component.DamageTag, $"{pointer}/damage_tag", "damage_tag");
            if (component.BasePower < 0 || component.DiceCount < 0 || component.DiceSides < 0)
                OutOfRange(diagnostics, context, "damage values must be >= 0.", pointer);
            if (component.BasePower <= 0 && component.DiceCount <= 0)
                Required(diagnostics, context, "component must declare dice or base_power.", pointer);
            if (component.DiceCount > 0 && component.DiceSides <= 0)
                OutOfRange(diagnostics, context, "dice_sides must be positive when dice_count is positive.", $"{pointer}/dice_sides");
            if (component.RingMin < 0 || component.RingMax < component.RingMin || component.RingMax > profile.Radius)
                OutOfRange(diagnostics, context, "component ring range is outside radius.", pointer);
            if (component.RingWeight < 0 || component.MasteryWeight < 0)
                OutOfRange(diagnostics, context, "component weights must be >= 0.", pointer);
            if (!AllowedMeteorSaveProfileIds.Contains(component.SaveProfileId))
                Unsupported(diagnostics, context, "save_profile_id", $"{pointer}/save_profile_id");
            foreach ((string ring, double basisPoints) in component.RingDamageScaleBasisPoints)
            {
                if (!int.TryParse(ring, out int ringValue) || ringValue < component.RingMin || ringValue > component.RingMax || basisPoints < 0)
                    OutOfRange(diagnostics, context, "ring_damage_scale_bp keys must be within the component ring and values must be >= 0.", $"{pointer}/ring_damage_scale_bp");
            }
        }

        for (int index = 0; index < profile.TerrainProfiles.Count; index += 1)
        {
            MeteorSwarmTerrainProfileImportModel terrain = profile.TerrainProfiles[index];
            string pointer = $"/profile/payload/terrain_profiles/{index}";
            RequireText(diagnostics, context, terrain.TerrainProfileId, $"{pointer}/terrain_profile_id", "terrain_profile_id");
            if (terrain.RingMin < 0 || terrain.RingMax < terrain.RingMin || terrain.RingMax > profile.Radius)
                OutOfRange(diagnostics, context, "terrain ring range is outside radius.", pointer);
            if (terrain.LifetimePolicy is not "battle" and not "timed")
                Unsupported(diagnostics, context, "lifetime_policy", $"{pointer}/lifetime_policy");
            if (terrain.DurationTu < 0 || terrain.TickIntervalTu < 0)
                OutOfRange(diagnostics, context, "terrain duration values must be >= 0.", pointer);
            RequireText(diagnostics, context, terrain.RenderOverlayId, $"{pointer}/render_overlay_id", "render_overlay_id");
            if (terrain.AccuracyModifierSpec is not null)
                ValidateAccuracyModifier(diagnostics, context, terrain.AccuracyModifierSpec, $"{pointer}/accuracy_modifier_spec");
        }
    }

    private static void ValidateAccuracyModifier(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        BattleAttackRollModifierImportModel spec,
        string pointer
    )
    {
        RequireText(diagnostics, context, spec.SourceDomain, $"{pointer}/source_domain", "source_domain");
        RequireText(diagnostics, context, spec.Label, $"{pointer}/label", "label");
        RequireText(diagnostics, context, spec.StackKey, $"{pointer}/stack_key", "stack_key");
        if (spec.StackMode is not "add" and not "exclusive" and not "max" and not "min")
            Unsupported(diagnostics, context, "stack_mode", $"{pointer}/stack_mode");
        if (spec.EndpointMode is not "either" and not "attacker" and not "target" and not "both")
            Unsupported(diagnostics, context, "endpoint_mode", $"{pointer}/endpoint_mode");
        if (spec.TargetTeamFilter is not "any" and not "ally" and not "enemy" and not "self")
            Unsupported(diagnostics, context, "target_team_filter", $"{pointer}/target_team_filter");
        if (spec.FootprintMode != "any_cell")
            Unsupported(diagnostics, context, "footprint_mode", $"{pointer}/footprint_mode");
        if (spec.AppliesTo is not "attack_roll" and not "attack_advantage")
            Unsupported(diagnostics, context, "applies_to", $"{pointer}/applies_to");
    }

    private static void ValidateOptionalIsoDate(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string value,
        string pointer
    )
    {
        if (value.Length > 0 && !DateOnly.TryParseExact(value, "yyyy-MM-dd", out _))
            Unsupported(diagnostics, context, "date", pointer);
    }

    private static void RequireIdentity(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(diagnostics, context, BattleSpecialProfileJsonRules.IdRequired, "profile_id is required.", "/profile_id");
        else if (!string.Equals(value, context.EntryId, StringComparison.Ordinal))
            Add(diagnostics, context, BattleSpecialProfileJsonRules.IdMismatch, "profile_id must match the envelope entry id.", "/profile_id");
    }

    private static void AddUniqueIds(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, IReadOnlyList<string> values, string pointer, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                Required(diagnostics, context, $"{label} must be non-empty.", pointer);
            else if (!seen.Add(value))
                Duplicate(diagnostics, context, $"Duplicate {label} '{value}'.", pointer);
        }
    }

    private static void RequireText(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string value, string pointer, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            Required(diagnostics, context, $"{label} is required.", pointer);
    }

    private static void Required(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) => Add(diagnostics, context, BattleSpecialProfileJsonRules.ValueRequired, message, pointer);
    private static void Unsupported(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string label, string pointer) => Add(diagnostics, context, BattleSpecialProfileJsonRules.ValueUnsupported, $"{label} is unsupported.", pointer);
    private static void OutOfRange(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) => Add(diagnostics, context, BattleSpecialProfileJsonRules.ValueOutOfRange, message, pointer);
    private static void Duplicate(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) => Add(diagnostics, context, BattleSpecialProfileJsonRules.DuplicateId, message, pointer);

    private static void Add(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string ruleId, string message, string pointer) =>
        diagnostics.Add(new ContentJsonDiagnostic(ruleId, message, context.SourceLabel, $"{context.JsonPointer}{pointer}"));
}
