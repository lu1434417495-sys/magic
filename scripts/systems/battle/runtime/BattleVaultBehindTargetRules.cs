using Godot;

internal readonly record struct BattleVaultBehindTargetPlan(
    bool Allowed,
    Vector2I Destination,
    string Message
)
{
    internal static BattleVaultBehindTargetPlan Denied(string message) =>
        new(false, new Vector2I(-1, -1), message);
}

internal static class BattleVaultBehindTargetRules
{
    internal static BattleVaultBehindTargetPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (
            state == null
            || gridService == null
            || sourceUnit == null
            || targetUnit == null
        )
            return BattleVaultBehindTargetPlan.Denied("借势越肩的目标无效。");

        return BuildPlanCore(
            state,
            gridService,
            barrierService,
            sourceUnit,
            sourceUnit.GetAnchorCoord(),
            sourceUnit.GetFootprintSize(),
            sourceUnit.unit_id,
            targetUnit.GetAnchorCoord()
        );
    }

    internal static BattleVaultBehindTargetPlan BuildPlan(
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
        )
            return BattleVaultBehindTargetPlan.Denied("借势越肩的目标无效。");

        return BuildPlanCore(
            state,
            gridService,
            barrierService,
            sourceUnit.UnsafeUnitForReadOnlyRules,
            sourceUnit.Coord,
            sourceUnit.FootprintSize,
            sourceUnit.UnitId,
            targetUnit.Coord
        );
    }

    private static BattleVaultBehindTargetPlan BuildPlanCore(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        Vector2I sourceCoord,
        Vector2I sourceFootprint,
        StringName sourceUnitId,
        Vector2I targetCoord
    )
    {
        Vector2I direction = targetCoord - sourceCoord;
        if (gridService.GetDistance(sourceCoord, targetCoord) != 1)
            return BattleVaultBehindTargetPlan.Denied("借势越肩只能对相邻目标使用。");

        Vector2I destination = targetCoord + direction;
        if (
            !gridService.CanPlaceFootprint(
                state,
                destination,
                sourceFootprint,
                sourceUnitId,
                sourceUnit
            )
        )
            return BattleVaultBehindTargetPlan.Denied("目标身后没有可供落脚的位置。");

        if (
            !gridService.CanCrossEdgeBetween(state, sourceCoord, targetCoord)
            || !gridService.CanCrossEdgeBetween(state, targetCoord, destination)
        )
            return BattleVaultBehindTargetPlan.Denied("墙体或高差阻挡了越肩路径。");

        if (
            barrierService != null
            && (
                barrierService.HasUnitBoundaryBarrier(sourceUnit, sourceCoord, targetCoord)
                || barrierService.HasUnitBoundaryBarrier(sourceUnit, targetCoord, destination)
            )
        )
            return BattleVaultBehindTargetPlan.Denied("屏障阻挡了越肩路径。");

        return new BattleVaultBehindTargetPlan(true, destination, "");
    }
}
