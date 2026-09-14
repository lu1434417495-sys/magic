using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleSequentialLineHitPlan
{
    internal BattleSequentialLineHitPlan(
        bool allowed,
        Vector2I direction,
        IReadOnlyList<Vector2I> pathCoords,
        IReadOnlyList<BattleUnitState> targets,
        string message,
        string stopMessage
    )
    {
        Allowed = allowed;
        Direction = direction;
        PathCoords = new ReadOnlyCollection<Vector2I>(
            new List<Vector2I>(pathCoords ?? Array.Empty<Vector2I>())
        );
        Targets = new ReadOnlyCollection<BattleUnitState>(
            new List<BattleUnitState>(targets ?? Array.Empty<BattleUnitState>())
        );
        Message = message ?? "";
        StopMessage = stopMessage ?? "";
    }

    internal bool Allowed { get; }
    internal Vector2I Direction { get; }
    internal IReadOnlyList<Vector2I> PathCoords { get; }
    internal IReadOnlyList<BattleUnitState> Targets { get; }
    internal string Message { get; }
    internal string StopMessage { get; }

    internal static BattleSequentialLineHitPlan Denied(
        string message,
        IReadOnlyList<Vector2I> pathCoords = null
    ) =>
        new(
            false,
            Vector2I.Zero,
            pathCoords,
            Array.Empty<BattleUnitState>(),
            message,
            ""
        );
}

internal static class BattleSequentialLineHitRules
{
    internal static bool IsSequentialLineHitSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile?.SequentialLineHit != null;

    internal static BattleSequentialLineHitPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState primaryTarget,
        SkillDefinition skillDefinition
    )
    {
        CombatSkillDefinition combat = skillDefinition?.CombatProfile;
        CombatSequentialLineHitDefinition profile = combat?.SequentialLineHit;
        if (
            state == null
            || gridService == null
            || sourceUnit?.IsAlive() != true
            || primaryTarget?.IsAlive() != true
            || combat == null
            || profile == null
        )
        {
            return BattleSequentialLineHitPlan.Denied("连续直线攻击的施术者或目标无效。");
        }
        if (
            !BattleTargetTeamRules.IsUnitValidForFilter(
                sourceUnit,
                primaryTarget,
                combat.TargetTeamFilter
            )
        )
        {
            return BattleSequentialLineHitPlan.Denied("连续直线攻击必须选择敌方单位。");
        }

        Vector2I sourceCoord = sourceUnit.GetAnchorCoord();
        Vector2I targetCoord = primaryTarget.GetAnchorCoord();
        if (!TryResolveDirection(sourceCoord, targetCoord, out Vector2I direction))
        {
            return BattleSequentialLineHitPlan.Denied(
                "目标必须与施术者处于同一行或同一列。"
            );
        }

        int skillLevel = sourceUnit.GetKnownSkillLevelTyped(skillDefinition.SkillId);
        int maximumPrimaryDistance = combat.GetEffectiveRangeValue(skillLevel);
        int minimumPrimaryDistance = profile.GetMinimumPrimaryDistance(skillLevel);
        int primaryDistance = Math.Abs(targetCoord.X - sourceCoord.X)
            + Math.Abs(targetCoord.Y - sourceCoord.Y);
        if (
            primaryDistance < minimumPrimaryDistance
            || primaryDistance > maximumPrimaryDistance
        )
        {
            return BattleSequentialLineHitPlan.Denied(
                $"首个目标必须位于 {minimumPrimaryDistance}–{maximumPrimaryDistance} 格。"
            );
        }

        var pathCoords = new List<Vector2I>();
        var targets = new List<BattleUnitState>();
        var seenUnitIds = new HashSet<StringName> { sourceUnit.unit_id };
        Vector2I currentCoord = sourceCoord;
        bool foundPrimary = false;
        for (int step = 1; step <= primaryDistance; step++)
        {
            Vector2I nextCoord = currentCoord + direction;
            if (!CanProjectileCross(state, gridService, barrierService, currentCoord, nextCoord))
            {
                return BattleSequentialLineHitPlan.Denied(
                    "通往首个目标的直线路径被战场边界或屏障截断。",
                    pathCoords
                );
            }
            pathCoords.Add(nextCoord);
            currentCoord = nextCoord;
            BattleUnitState occupant = gridService.GetUnitAtCoord(state, currentCoord);
            if (
                occupant == null
                || !occupant.IsAlive()
                || !seenUnitIds.Add(occupant.unit_id)
            )
            {
                continue;
            }
            if (occupant.unit_id != primaryTarget.unit_id)
            {
                return BattleSequentialLineHitPlan.Denied(
                    "所选目标不是该方向上的首个存活单位。",
                    pathCoords
                );
            }
            foundPrimary = true;
            targets.Add(primaryTarget);
            currentCoord = AdvanceThroughFootprint(
                state,
                gridService,
                barrierService,
                primaryTarget,
                currentCoord,
                direction,
                pathCoords
            );
            break;
        }
        if (!foundPrimary)
        {
            return BattleSequentialLineHitPlan.Denied(
                "所选目标不在可贯穿的正交射线上。",
                pathCoords
            );
        }

        int continuationRange = profile.GetContinuationRange(skillLevel);
        int maximumTargetCount = Math.Max(combat.GetEffectiveMaxTargetCount(skillLevel), 1);
        string stopMessage = "已达到本级最大目标数。";
        while (targets.Count < maximumTargetCount)
        {
            bool foundNextTarget = false;
            stopMessage = $"命中后续行 {continuationRange} 格内没有下一个敌人。";
            for (int distance = 1; distance <= continuationRange; distance++)
            {
                Vector2I nextCoord = currentCoord + direction;
                if (!CanProjectileCross(state, gridService, barrierService, currentCoord, nextCoord))
                {
                    stopMessage = "后续路径被战场边界或屏障截断。";
                    return Allowed(direction, pathCoords, targets, stopMessage);
                }
                pathCoords.Add(nextCoord);
                currentCoord = nextCoord;
                BattleUnitState occupant = gridService.GetUnitAtCoord(state, currentCoord);
                if (
                    occupant == null
                    || !occupant.IsAlive()
                    || !seenUnitIds.Add(occupant.unit_id)
                )
                {
                    continue;
                }
                if (
                    !BattleTargetTeamRules.IsUnitValidForFilter(
                        sourceUnit,
                        occupant,
                        combat.TargetTeamFilter
                    )
                )
                {
                    stopMessage = "后续路径被友方或非敌对单位阻挡。";
                    return Allowed(direction, pathCoords, targets, stopMessage);
                }
                targets.Add(occupant);
                currentCoord = AdvanceThroughFootprint(
                    state,
                    gridService,
                    barrierService,
                    occupant,
                    currentCoord,
                    direction,
                    pathCoords
                );
                foundNextTarget = true;
                break;
            }
            if (!foundNextTarget)
                break;
        }
        return Allowed(direction, pathCoords, targets, stopMessage);
    }

    private static BattleSequentialLineHitPlan Allowed(
        Vector2I direction,
        IReadOnlyList<Vector2I> pathCoords,
        IReadOnlyList<BattleUnitState> targets,
        string stopMessage
    ) => new(true, direction, pathCoords, targets, "", stopMessage);

    private static Vector2I AdvanceThroughFootprint(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState hitUnit,
        Vector2I currentCoord,
        Vector2I direction,
        List<Vector2I> pathCoords
    )
    {
        while (true)
        {
            Vector2I nextCoord = currentCoord + direction;
            if (!gridService.IsInside(state, nextCoord))
                return currentCoord;
            BattleUnitState nextOccupant = gridService.GetUnitAtCoord(state, nextCoord);
            if (nextOccupant?.unit_id != hitUnit.unit_id)
                return currentCoord;
            if (!CanProjectileCross(state, gridService, barrierService, currentCoord, nextCoord))
                return currentCoord;
            pathCoords.Add(nextCoord);
            currentCoord = nextCoord;
        }
    }

    private static bool CanProjectileCross(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        Vector2I fromCoord,
        Vector2I toCoord
    )
    {
        if (!gridService.IsInside(state, toCoord))
            return false;
        return barrierService?.HasActiveBarrierBoundaryBetween(fromCoord, toCoord) != true;
    }

    private static bool TryResolveDirection(
        Vector2I sourceCoord,
        Vector2I targetCoord,
        out Vector2I direction
    )
    {
        direction = Vector2I.Zero;
        Vector2I delta = targetCoord - sourceCoord;
        if (delta == Vector2I.Zero || (delta.X != 0 && delta.Y != 0))
            return false;
        direction =
            delta.X == 0
                ? new Vector2I(0, Math.Sign(delta.Y))
                : new Vector2I(Math.Sign(delta.X), 0);
        return true;
    }
}
