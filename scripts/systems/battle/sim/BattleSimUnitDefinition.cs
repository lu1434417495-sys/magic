using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Immutable, plain runtime definition projected from one authored simulation unit entry.
/// The stored snapshot contains no Resource or Godot collection wrapper and creates a fresh
/// mutable BattleUnitState for every simulation run.
/// </summary>
internal sealed class BattleSimUnitDefinition
{
    private readonly IReadOnlyDictionary<string, object> _unitSnapshot;

    private BattleSimUnitDefinition(
        IReadOnlyDictionary<string, object> unitSnapshot,
        Vector2I coord,
        string sourceLabel
    )
    {
        ArgumentNullException.ThrowIfNull(unitSnapshot);
        _unitSnapshot = ContentValueNormalizer.NormalizeDictionary(
            RuntimePlainPayload.CloneDictionary(unitSnapshot),
            string.IsNullOrWhiteSpace(sourceLabel) ? "battle_sim_unit" : sourceLabel
        );
        Coord = coord;
    }

    internal Vector2I Coord { get; }

    internal IReadOnlyDictionary<string, object> UnitSnapshot => _unitSnapshot;

    internal static BattleSimUnitDefinition FromProjectedState(
        BattleUnitState unitState,
        string sourceLabel
    )
    {
        ArgumentNullException.ThrowIfNull(unitState);
        string label = string.IsNullOrWhiteSpace(sourceLabel)
            ? "battle_sim_unit"
            : sourceLabel;
        return new BattleSimUnitDefinition(
            unitState.BuildSnapshotPlain(),
            unitState.coord,
            label
        );
    }

    internal BattleSimUnitDefinition DeepClone(string sourceLabel) =>
        new(_unitSnapshot, Coord, sourceLabel);

    internal BattleUnitState CreateRuntimeState()
    {
        using GodotProjectionLease<Godot.Collections.Dictionary> lease =
            RuntimePlainPayload.ProjectDictionaryLease(
                _unitSnapshot,
                "battle-sim-unit-definition",
                LifetimeDomain.Request,
                "BattleSimUnitDefinition.CreateRuntimeState"
            );
        BattleUnitState state = BattleUnitState.FromDictionary(lease.Value);
        if (state == null)
        {
            throw new InvalidOperationException(
                "BattleSimUnitDefinition could not reconstruct its projected unit state."
            );
        }
        return state;
    }
}
