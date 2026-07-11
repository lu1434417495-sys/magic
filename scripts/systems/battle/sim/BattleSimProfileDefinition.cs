using System;
using System.Collections.Generic;
using Godot;

internal sealed record BattleSimProfileDefinition
{
    internal BattleSimProfileDefinition(
        StringName profileId,
        string displayName,
        string description,
        BattleAiScoreProfileDefinition aiScoreProfile,
        IReadOnlyList<BattleSimOverridePatchDefinition> overridePatches
    )
    {
        ProfileId = profileId;
        DisplayName = displayName ?? "";
        Description = description ?? "";
        AiScoreProfile = aiScoreProfile ?? BattleAiScoreProfileDefinition.Default;
        OverridePatches = EnemyDefinitionCollections.FreezeList(overridePatches);
    }

    internal StringName ProfileId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal BattleAiScoreProfileDefinition AiScoreProfile { get; }
    internal IReadOnlyList<BattleSimOverridePatchDefinition> OverridePatches { get; }
}
