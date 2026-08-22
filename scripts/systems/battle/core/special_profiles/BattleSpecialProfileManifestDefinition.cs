#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleSpecialProfileManifestDefinition
{
    internal BattleSpecialProfileManifestDefinition(
        StringName profileId,
        int schemaVersion,
        IReadOnlyList<StringName> owningSkillIds,
        StringName runtimeResolverId,
        StringName runtimeReadPolicy,
        string displayName,
        StringName coverageShapeId,
        int radius,
        IReadOnlyList<BattleSpecialProfileDeferredCapabilityDefinition> deferredCapabilities,
        string sunsetWarningDate,
        string sunsetHardBlockDate
    )
    {
        ProfileId = profileId;
        SchemaVersion = schemaVersion;
        OwningSkillIds = new ReadOnlyCollection<StringName>(new List<StringName>(owningSkillIds));
        RuntimeResolverId = runtimeResolverId;
        RuntimeReadPolicy = runtimeReadPolicy;
        DisplayName = displayName ?? "";
        CoverageShapeId = coverageShapeId;
        Radius = radius;
        DeferredCapabilities = new ReadOnlyCollection<BattleSpecialProfileDeferredCapabilityDefinition>(
            new List<BattleSpecialProfileDeferredCapabilityDefinition>(deferredCapabilities)
        );
        SunsetWarningDate = sunsetWarningDate ?? "";
        SunsetHardBlockDate = sunsetHardBlockDate ?? "";
    }

    internal StringName ProfileId { get; }
    internal int SchemaVersion { get; }
    internal IReadOnlyList<StringName> OwningSkillIds { get; }
    internal StringName RuntimeResolverId { get; }
    internal StringName RuntimeReadPolicy { get; }
    internal string DisplayName { get; }
    internal StringName CoverageShapeId { get; }
    internal int Radius { get; }
    internal IReadOnlyList<BattleSpecialProfileDeferredCapabilityDefinition> DeferredCapabilities { get; }
    internal string SunsetWarningDate { get; }
    internal string SunsetHardBlockDate { get; }
}

internal sealed record BattleSpecialProfileDeferredCapabilityDefinition(
    StringName CapabilityId,
    StringName Status
);
