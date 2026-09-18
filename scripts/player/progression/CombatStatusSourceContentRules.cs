using Godot;

public enum BattleStatusStackingScope
{
    Aggregate = 0,
    SourceDefinition,
}

internal static class CombatStatusSourceContentRules
{
    internal static readonly StringName BurningStatusId = "burning";
    internal static BattleStatusStackingScope GetStackingScope(StringName statusId) =>
        statusId == BurningStatusId ? BattleStatusStackingScope.SourceDefinition : BattleStatusStackingScope.Aggregate;
}
