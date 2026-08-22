using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattlePositionSwapPlan
{
    internal bool Allowed { get; init; }
    internal string Message { get; init; } = "";
    internal Vector2I SourceFrom { get; init; } = new(-1, -1);
    internal Vector2I SourceTo { get; init; } = new(-1, -1);
    internal Vector2I TargetFrom { get; init; } = new(-1, -1);
    internal Vector2I TargetTo { get; init; } = new(-1, -1);
    internal bool RequiresEnemySave { get; init; }

    internal static BattlePositionSwapPlan Denied(string message) =>
        new() { Message = message ?? "换位目标无效。" };
}

internal static class BattlePositionSwapRules
{
    internal static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.PositionSwap)
                return effectDefinition;
        }
        return null;
    }

    internal static BattlePositionSwapPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    ) => BuildPlan(
        state,
        gridService,
        barrierService,
        (BattleUnitReadView)sourceUnit,
        (BattleUnitReadView)targetUnit
    );

    internal static BattlePositionSwapPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit
    )
    {
        if (
            state == null
            || gridService == null
            || !sourceUnit.IsValid
            || !targetUnit.IsValid
            || !sourceUnit.IsAlive
            || !targetUnit.IsAlive
        )
            return BattlePositionSwapPlan.Denied("换位目标无效或已经倒下。");
        if (sourceUnit.UnitId == targetUnit.UnitId)
            return BattlePositionSwapPlan.Denied("不能与自己交换位置。");
        if (
            sourceUnit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS)
            || targetUnit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS)
        )
            return BattlePositionSwapPlan.Denied("时间静滞中的单位不能交换位置。");

        bool hostile = sourceUnit.FactionId != targetUnit.FactionId;
        if (hostile && BlocksForcedMovement(targetUnit))
            return BattlePositionSwapPlan.Denied("目标免疫敌方强制位移，不能被换位。");

        Vector2I sourceFrom = sourceUnit.Coord;
        Vector2I targetFrom = targetUnit.Coord;
        if (
            !gridService.CanFitFootprintIgnoringOccupants(
                state,
                targetFrom,
                sourceUnit.FootprintSize,
                sourceUnit.UnsafeUnitForReadOnlyRules
            )
            || !gridService.CanFitFootprintIgnoringOccupants(
                state,
                sourceFrom,
                targetUnit.FootprintSize,
                targetUnit.UnsafeUnitForReadOnlyRules
            )
        )
            return BattlePositionSwapPlan.Denied("至少一方无法合法占据对方原位置。");

        IReadOnlyList<Vector2I> sourceDestination = sourceUnit.GetTargetCoords(targetFrom);
        IReadOnlyList<Vector2I> targetDestination = targetUnit.GetTargetCoords(sourceFrom);
        var sourceDestinationSet = new HashSet<Vector2I>(sourceDestination);
        foreach (Vector2I coord in targetDestination)
        {
            if (sourceDestinationSet.Contains(coord))
                return BattlePositionSwapPlan.Denied("交换后的单位占位会发生重叠。");
        }
        if (
            HasThirdPartyOccupant(state, gridService, sourceDestination, sourceUnit, targetUnit)
            || HasThirdPartyOccupant(state, gridService, targetDestination, sourceUnit, targetUnit)
        )
            return BattlePositionSwapPlan.Denied("对方原位置没有足够的完整落脚空间。");

        if (
            barrierService?.HasUnitBoundaryBarrier(
                sourceUnit.UnsafeUnitForReadOnlyRules,
                sourceFrom,
                targetFrom
            ) == true
            || barrierService?.HasUnitBoundaryBarrier(
                targetUnit.UnsafeUnitForReadOnlyRules,
                targetFrom,
                sourceFrom
            ) == true
        )
            return BattlePositionSwapPlan.Denied("墙体或屏障阻止了位置交换。");

        return new BattlePositionSwapPlan
        {
            Allowed = true,
            SourceFrom = sourceFrom,
            SourceTo = targetFrom,
            TargetFrom = targetFrom,
            TargetTo = sourceFrom,
            RequiresEnemySave = hostile,
        };
    }

    internal static bool Commit(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattlePositionSwapPlan plan
    )
    {
        if (
            state == null
            || gridService == null
            || sourceUnit == null
            || targetUnit == null
            || plan?.Allowed != true
            || sourceUnit.GetAnchorCoord() != plan.SourceFrom
            || targetUnit.GetAnchorCoord() != plan.TargetFrom
        )
            return false;

        var sourcePreviousCoords = new List<Vector2I>(
            sourceUnit.GetOccupiedCoordsReadViewTyped()
        );
        var targetPreviousCoords = new List<Vector2I>(
            targetUnit.GetOccupiedCoordsReadViewTyped()
        );
        gridService.ClearUnitOccupancy(state, sourceUnit);
        gridService.ClearUnitOccupancy(state, targetUnit);
        if (!gridService.PlaceUnit(state, sourceUnit, plan.SourceTo, ignore_height: true))
        {
            gridService.SetOccupantsTyped(state, sourcePreviousCoords, sourceUnit.unit_id);
            gridService.SetOccupantsTyped(state, targetPreviousCoords, targetUnit.unit_id);
            return false;
        }
        if (gridService.PlaceUnit(state, targetUnit, plan.TargetTo, ignore_height: true))
            return true;

        gridService.ClearUnitOccupancy(state, sourceUnit);
        sourceUnit.SetAnchorCoord(plan.SourceFrom);
        targetUnit.SetAnchorCoord(plan.TargetFrom);
        gridService.SetOccupantsTyped(state, sourcePreviousCoords, sourceUnit.unit_id);
        gridService.SetOccupantsTyped(state, targetPreviousCoords, targetUnit.unit_id);
        state.MarkMovementGeometryChanged();
        return false;
    }

    private static bool BlocksForcedMovement(BattleUnitReadView targetUnit)
    {
        foreach (BattleStatusReadView status in targetUnit.StatusEffects())
        {
            if (status.Stacks > 0 && status.ForcedMoveImmune)
                return true;
        }
        return false;
    }

    private static bool HasThirdPartyOccupant(
        BattleState state,
        BattleGridService gridService,
        IReadOnlyList<Vector2I> coords,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit
    )
    {
        foreach (Vector2I coord in coords)
        {
            BattleUnitState occupant = gridService.GetUnitAtCoord(state, coord);
            if (
                occupant != null
                && occupant.unit_id != sourceUnit.UnitId
                && occupant.unit_id != targetUnit.UnitId
            )
                return true;
        }
        return false;
    }

}
