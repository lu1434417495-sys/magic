using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleLineThroughAttackPlan
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    internal BattleLineThroughAttackPlan(
        bool allowed,
        Vector2I direction,
        IReadOnlyList<Vector2I> anchorPath,
        IReadOnlyList<BattleUnitState> intermediateTargets,
        BattleUnitState primaryTarget,
        string message
    )
    {
        Allowed = allowed;
        Direction = direction;
        AnchorPath = new ReadOnlyCollection<Vector2I>(
            new List<Vector2I>(anchorPath ?? Array.Empty<Vector2I>())
        );
        IntermediateTargets = new ReadOnlyCollection<BattleUnitState>(
            new List<BattleUnitState>(intermediateTargets ?? Array.Empty<BattleUnitState>())
        );
        PrimaryTarget = primaryTarget;
        Message = message ?? "";
    }

    internal bool Allowed { get; }
    internal Vector2I Direction { get; }
    internal IReadOnlyList<Vector2I> AnchorPath { get; }
    internal IReadOnlyList<BattleUnitState> IntermediateTargets { get; }
    internal BattleUnitState PrimaryTarget { get; }
    internal string Message { get; }
    internal Vector2I Origin => AnchorPath.Count > 0 ? AnchorPath[0] : InvalidCoord;
    internal Vector2I Destination => AnchorPath.Count > 0 ? AnchorPath[^1] : InvalidCoord;
    internal int TravelDistance => Math.Max(AnchorPath.Count - 1, 0);

    internal static BattleLineThroughAttackPlan Denied(string message) =>
        new(
            false,
            Vector2I.Zero,
            Array.Empty<Vector2I>(),
            Array.Empty<BattleUnitState>(),
            null,
            message
        );
}

internal static class BattleLineThroughAttackRules
{
    internal static bool IsLineThroughAttackSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile?.LineThroughAttack != null;

    internal static BattleLineThroughAttackPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        bool movementBlocked
    )
    {
        CombatLineThroughAttackDefinition profile =
            skillDefinition?.CombatProfile?.LineThroughAttack;
        if (
            state == null
            || gridService == null
            || sourceUnit == null
            || targetUnit == null
            || profile == null
            || !sourceUnit.IsAlive()
            || !targetUnit.IsAlive()
        )
        {
            return BattleLineThroughAttackPlan.Denied("穿身攻击的施术者或终点目标无效。");
        }
        if (sourceUnit.faction_id == targetUnit.faction_id)
            return BattleLineThroughAttackPlan.Denied("穿身攻击必须选择敌方终点目标。");
        if (movementBlocked)
            return BattleLineThroughAttackPlan.Denied("当前被限制移动，无法使用穿身攻击。");
        if (
            !BattleRangeService.UnitMatchesRequiredWeaponFamilies(sourceUnit, skillDefinition)
            || !BattleRangeService.UnitMatchesRequiredWeaponTypeIds(sourceUnit, skillDefinition)
        )
        {
            return BattleLineThroughAttackPlan.Denied("当前装备武器不满足穿身攻击要求。");
        }
        int weaponRange = BattleRangeService.GetWeaponAttackRange(sourceUnit);
        if (weaponRange <= 0 || weaponRange > profile.MaximumWeaponRange)
        {
            return BattleLineThroughAttackPlan.Denied(
                $"穿身攻击需要攻击距离不超过 {profile.MaximumWeaponRange} 格的已装备近战武器。"
            );
        }

        Vector2I sourceAnchor = sourceUnit.GetAnchorCoord();
        Vector2I targetAnchor = targetUnit.GetAnchorCoord();
        if (!TryResolveDirection(sourceAnchor, targetAnchor, out Vector2I direction))
            return BattleLineThroughAttackPlan.Denied("终点目标必须与施术者处于同一行或同一列。");

        int skillLevel = sourceUnit.GetKnownSkillLevelTyped(skillDefinition.SkillId);
        int effectiveRange = skillDefinition.CombatProfile.GetEffectiveRangeValue(skillLevel);
        int targetDistance = gridService.GetDistanceBetweenUnits(sourceUnit, targetUnit);
        if (effectiveRange <= 0 || targetDistance <= 0 || targetDistance > effectiveRange)
            return BattleLineThroughAttackPlan.Denied($"终点目标超出 {effectiveRange} 格穿身距离。");

        Vector2I footprint = sourceUnit.GetFootprintSize();
        var path = new List<Vector2I> { sourceAnchor };
        var intermediateTargets = new List<BattleUnitState>();
        var seenIntermediateIds = new HashSet<StringName>();
        bool crossedPrimaryTarget = false;
        int maximumScanSteps = Math.Max(
            Math.Abs(targetAnchor.X - sourceAnchor.X)
                + Math.Abs(targetAnchor.Y - sourceAnchor.Y)
                + targetUnit.GetFootprintSize().X
                + targetUnit.GetFootprintSize().Y
                + footprint.X
                + footprint.Y
                + 2,
            effectiveRange + 4
        );
        Vector2I currentAnchor = sourceAnchor;
        for (int step = 1; step <= maximumScanSteps; step++)
        {
            Vector2I nextAnchor = currentAnchor + direction;
            if (
                !CanTraverseAnchorStep(
                    state,
                    gridService,
                    barrierService,
                    sourceUnit,
                    currentAnchor,
                    nextAnchor
                )
            )
            {
                return BattleLineThroughAttackPlan.Denied("穿身路径被墙体、地形或屏障阻挡。");
            }

            IReadOnlyList<Vector2I> nextCoords = gridService.GetFootprintCoords(
                nextAnchor,
                footprint
            );
            bool overlapsPrimary = false;
            foreach (Vector2I coord in nextCoords)
            {
                BattleUnitState occupant = gridService.GetUnitAtCoord(state, coord);
                if (occupant == null || occupant.unit_id == sourceUnit.unit_id)
                    continue;
                if (occupant.unit_id == targetUnit.unit_id)
                {
                    overlapsPrimary = true;
                    crossedPrimaryTarget = true;
                    continue;
                }
                if (!occupant.IsAlive())
                    continue;
                if (crossedPrimaryTarget)
                    return BattleLineThroughAttackPlan.Denied("终点目标身后的落点被其他单位占据。");
                if (occupant.faction_id == sourceUnit.faction_id)
                    return BattleLineThroughAttackPlan.Denied("穿身路径被友方单位阻挡。");
                if (seenIntermediateIds.Add(occupant.unit_id))
                    intermediateTargets.Add(occupant);
            }

            path.Add(nextAnchor);
            currentAnchor = nextAnchor;
            if (!crossedPrimaryTarget || overlapsPrimary)
                continue;
            if (
                !gridService.CanPlaceFootprint(
                    state,
                    nextAnchor,
                    footprint,
                    sourceUnit.unit_id,
                    sourceUnit
                )
            )
            {
                return BattleLineThroughAttackPlan.Denied("终点目标身后没有合法落点。");
            }
            return new BattleLineThroughAttackPlan(
                true,
                direction,
                path,
                intermediateTargets,
                targetUnit,
                ""
            );
        }

        return BattleLineThroughAttackPlan.Denied("无法在终点目标身后找到合法落点。");
    }

    internal static bool CanCommitLanding(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleLineThroughAttackPlan plan
    )
    {
        if (
            state == null
            || gridService == null
            || sourceUnit?.IsAlive() != true
            || plan?.Allowed != true
            || sourceUnit.GetAnchorCoord() != plan.Origin
        )
        {
            return false;
        }
        for (int index = 1; index < plan.AnchorPath.Count; index++)
        {
            if (
                !CanTraverseAnchorStep(
                    state,
                    gridService,
                    barrierService,
                    sourceUnit,
                    plan.AnchorPath[index - 1],
                    plan.AnchorPath[index]
                )
            )
            {
                return false;
            }
        }
        return gridService.CanPlaceFootprint(
            state,
            plan.Destination,
            sourceUnit.GetFootprintSize(),
            sourceUnit.unit_id,
            sourceUnit
        );
    }

    private static bool CanTraverseAnchorStep(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        Vector2I fromAnchor,
        Vector2I toAnchor
    )
    {
        if (
            !gridService.CanFitFootprintIgnoringOccupants(
                state,
                toAnchor,
                sourceUnit.GetFootprintSize(),
                sourceUnit
            )
            || barrierService?.HasUnitBoundaryBarrier(sourceUnit, fromAnchor, toAnchor) == true
        )
        {
            return false;
        }

        Vector2I delta = toAnchor - fromAnchor;
        foreach (
            Vector2I fromCoord in gridService.GetFootprintCoords(
                fromAnchor,
                sourceUnit.GetFootprintSize()
            )
        )
        {
            Vector2I toCoord = fromCoord + delta;
            BattleCellState fromCell = state.GetCell(fromCoord);
            BattleCellState toCell = state.GetCell(toCoord);
            if (
                fromCell == null
                || toCell == null
                || Math.Abs(fromCell.current_height - toCell.current_height) > 1
                || !gridService.CanCrossEdgeBetween(state, fromCoord, toCoord)
            )
            {
                return false;
            }
        }
        return true;
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
