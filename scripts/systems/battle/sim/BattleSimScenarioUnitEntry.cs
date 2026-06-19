using System;
using Godot;

public sealed class BattleSimScenarioUnitEntry
{
    private BattleSimScenarioUnitEntry(
        BattleSimUnitSpec spec,
        BattleUnitState unitState,
        Vector2I coord
    )
    {
        Spec = spec;
        UnitState = unitState;
        Coord = coord;
    }

    public BattleSimUnitSpec Spec { get; }

    public BattleUnitState UnitState { get; }

    public Vector2I Coord { get; }

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
                spec,
                spec.ToBattleUnitState(defaultFaction, defaultControlMode),
                spec.coord
            );
        }
        if (rawObject is BattleUnitState unitState)
        {
            return new BattleSimScenarioUnitEntry(null, unitState, unitState.coord);
        }
        throw new InvalidOperationException(
            $"{sourceLabel} 必须是 BattleSimUnitSpec 或 BattleUnitState。"
        );
    }
}
