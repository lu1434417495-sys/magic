using System.Collections.Generic;
using Godot;

internal enum BattleControlZoneOccupancyKind
{
    Neutral = 0,
    Player,
    Hostile,
    Contested,
}

internal static class BattleControlObjectiveRules
{
    internal static BattleControlZoneOccupancyKind ResolveOccupancy(
        BattleState state,
        BattleControlZoneRuntimeState zone
    )
    {
        if (state == null || zone == null)
            return BattleControlZoneOccupancyKind.Neutral;

        bool hasPlayer = HasLivingUnitFullyInside(
            state,
            state.GetAllyUnitIdsTyped(),
            zone
        );
        bool hasHostile = HasLivingUnitFullyInside(
            state,
            state.GetEnemyUnitIdsTyped(),
            zone
        );
        if (hasPlayer && hasHostile)
            return BattleControlZoneOccupancyKind.Contested;
        if (hasPlayer)
            return BattleControlZoneOccupancyKind.Player;
        if (hasHostile)
            return BattleControlZoneOccupancyKind.Hostile;
        return BattleControlZoneOccupancyKind.Neutral;
    }

    internal static bool IsUnitFullyInside(
        BattleUnitState unit,
        BattleControlZoneRuntimeState zone
    )
    {
        BattleUnitGeometryReadView geometry =
            unit?.GetGeometryReadViewTyped()
            ?? BattleUnitGeometryReadView.MissingOwner;
        if (
            unit?.IsAlive() != true
            || zone == null
            || !geometry.OwnerPresent
            || !geometry.OccupiedCoords.IsPresent
            || geometry.OccupiedCoords.Count == 0
        )
        {
            return false;
        }
        foreach (Vector2I coord in geometry.OccupiedCoords)
        {
            if (!zone.ContainsCoord(coord))
                return false;
        }
        return true;
    }

    private static bool HasLivingUnitFullyInside(
        BattleState state,
        IEnumerable<StringName> unitIds,
        BattleControlZoneRuntimeState zone
    )
    {
        foreach (StringName unitId in unitIds)
        {
            if (IsUnitFullyInside(state.GetUnit(unitId), zone))
                return true;
        }
        return false;
    }
}
