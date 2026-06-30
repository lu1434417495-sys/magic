using System;
using System.Collections.Generic;
using Godot;

public sealed class EquipmentAbilityEquipmentTargetRef
{
    public StringName UnitId { get; init; } = "";
    public StringName EntrySlotId { get; init; } = "";
    public StringName SlotId { get; init; } = "";
    public StringName ItemId { get; init; } = "";
    public StringName EquipmentInstanceId { get; init; } = "";
    public StringName EquipmentTypeId { get; init; } = "";
    public IReadOnlyList<StringName> OccupiedSlotIds { get; init; } = Array.Empty<StringName>();
    public IReadOnlyList<StringName> ItemTags { get; init; } = Array.Empty<StringName>();
    public int CurrentDurability { get; init; }
}

internal sealed class EquipmentDurabilityCommitRequest
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState TargetUnit { get; init; }
    public EquipmentAbilityEquipmentTargetRef TargetEquipment { get; init; }
    public CombatEffectDefinition EffectDefinition { get; init; }
    public DamageResolutionContext DamageContext { get; init; }
    public int TotalDamage { get; init; }
    public int TotalShieldAbsorbed { get; init; }
    public StringName SourceKey { get; init; } = "";
    public StringName ActionId { get; init; } = "";
}

internal sealed class EquipmentDurabilityCommitResult
{
    public bool Resolved { get; init; }
    public StringName TargetUnitId { get; init; } = "";
    public StringName EntrySlotId { get; init; } = "";
    public StringName SlotId { get; init; } = "";
    public StringName ItemId { get; init; } = "";
    public StringName EquipmentInstanceId { get; init; } = "";
    public int Rarity { get; init; }
    public int DurabilityBefore { get; init; }
    public int DurabilityAfter { get; init; }
    public int DurabilityLoss { get; init; }
    public bool Destroyed { get; init; }
    public bool HasSave { get; init; }
    public SaveResolutionResult SaveResult { get; init; }
    public StringName NoOpReason { get; init; } = "";

    public static EquipmentDurabilityCommitResult NoOp(StringName reason) =>
        new()
        {
            Resolved = false,
            DurabilityLoss = 0,
            Destroyed = false,
            HasSave = false,
            SaveResult = new SaveResolutionResult(),
            NoOpReason = reason,
        };
}
