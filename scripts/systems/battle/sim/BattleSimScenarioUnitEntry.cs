using System;
using Godot;

public sealed class BattleSimScenarioUnitEntry
{
    private BattleSimScenarioUnitEntry(
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

    internal static BattleSimScenarioUnitEntry FromVariant(
        Variant value,
        string sourceLabel,
        StringName defaultFaction,
        StringName defaultControlMode
    )
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return null;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            BattleUnitState unitState = BattleUnitState.FromDictionary(value.AsGodotDictionary());
            if (unitState != null)
            {
                return new BattleSimScenarioUnitEntry(
                    BattleSimUnitDefinition.FromProjectedState(unitState, sourceLabel)
                );
            }
        }
        if (value.VariantType != Variant.Type.Object)
        {
            throw new InvalidOperationException(
                $"{sourceLabel} 必须是 BattleSimUnitSpec 或 BattleUnitState。"
            );
        }
        GodotObject rawObject = value.AsGodotObject();
        if (rawObject is BattleSimUnitSpec spec)
        {
            return new BattleSimScenarioUnitEntry(
                spec.ToDefinition(defaultFaction, defaultControlMode)
            );
        }
        throw new InvalidOperationException(
            $"{sourceLabel} 必须是 BattleSimUnitSpec 或 BattleUnitState。"
        );
    }
}
