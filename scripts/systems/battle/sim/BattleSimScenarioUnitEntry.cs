using System;
using Godot;

public sealed class BattleSimScenarioUnitEntry
{
    internal BattleSimScenarioUnitEntry(
        BattleSimUnitDefinition unitDefinition
    )
    {
        UnitDefinition = unitDefinition
            ?? throw new ArgumentNullException(nameof(unitDefinition));
    }

    internal BattleSimUnitDefinition UnitDefinition { get; }

    public Vector2I Coord => UnitDefinition.Coord;

    internal BattleSimScenarioUnitEntry DeepClone(string sourceLabel) =>
        new(UnitDefinition.DeepClone(sourceLabel));

    internal static BattleSimScenarioUnitEntry FromProjectedState(
        BattleUnitState unitState,
        string sourceLabel
    ) =>
        new(
            BattleSimUnitDefinition.FromProjectedState(
                unitState,
                sourceLabel
            )
        );

}
