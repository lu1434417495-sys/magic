using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleEquipmentAbilityActionPreviewResult
{
    internal StringName BindingId { get; init; } = "";
    internal StringName ActionId { get; init; } = "";
    internal StringName ActionKind { get; init; } = "";
    internal StringName TriggerSkillId { get; init; } = "";
    internal int TriggerProbabilityBasisPoints { get; init; }
    internal bool Guaranteed { get; init; }
    internal bool Applied { get; init; }
    internal bool Conditional { get; init; }
    internal bool Supported { get; init; } = true;
    internal string UnsupportedReason { get; init; } = "";
    internal IReadOnlyList<BattleDamagePreviewResult> DamagePreviews { get; init; } =
        Array.Empty<BattleDamagePreviewResult>();
}

internal sealed class BattleEquipmentAbilityCommandPreviewResult
{
    internal static readonly BattleEquipmentAbilityCommandPreviewResult None = new();

    internal bool Triggered { get; init; }
    internal StringName SourceUnitId { get; init; } = "";
    internal BattleUnitState SourceUnitAfter { get; init; }
    internal IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> Actions { get; init; } =
        Array.Empty<BattleEquipmentAbilityActionPreviewResult>();
}
