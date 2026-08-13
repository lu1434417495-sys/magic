using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleApproachAttackPlan
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    internal BattleApproachAttackPlan(
        bool allowed,
        int maximumAdvanceDistance,
        int weaponRange,
        int originHeight,
        IReadOnlyList<Vector2I> path,
        string message
    )
    {
        Allowed = allowed;
        MaximumAdvanceDistance = Math.Max(maximumAdvanceDistance, 0);
        WeaponRange = Math.Max(weaponRange, 0);
        OriginHeight = originHeight;
        Path = new ReadOnlyCollection<Vector2I>(
            new List<Vector2I>(path ?? Array.Empty<Vector2I>())
        );
        Message = message ?? "";
    }

    internal bool Allowed { get; }
    internal int MaximumAdvanceDistance { get; }
    internal int WeaponRange { get; }
    internal int OriginHeight { get; }
    internal IReadOnlyList<Vector2I> Path { get; }
    internal string Message { get; }
    internal int AdvanceDistance => Math.Max(Path.Count - 1, 0);
    internal Vector2I FinalCoord => Path.Count > 0 ? Path[^1] : InvalidCoord;

    internal static BattleApproachAttackPlan Denied(string message) =>
        new(false, 0, 0, 0, Array.Empty<Vector2I>(), message);
}

internal static class BattleApproachAttackRules
{
    internal static bool IsApproachAttackSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile?.ApproachAttack != null;

    internal static BattleApproachAttackPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        bool movementBlocked
    )
    {
        if (
            state == null
            || gridService == null
            || sourceUnit == null
            || targetUnit == null
            || !sourceUnit.IsAlive()
            || !targetUnit.IsAlive()
        )
        {
            return BattleApproachAttackPlan.Denied("踏步攻击的单位目标无效。");
        }
        if (
            !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                sourceUnit,
                skillDefinition
            )
            || !BattleRangeService.UnitMatchesRequiredWeaponTypeIds(
                sourceUnit,
                skillDefinition
            )
        )
        {
            return BattleApproachAttackPlan.Denied("当前装备武器不满足踏步攻击要求。");
        }

        return BuildPlanCore(
            sourceUnit.GetAnchorCoord(),
            targetUnit.GetAnchorCoord(),
            sourceUnit.GetOccupiedCoordsReadViewTyped(),
            targetUnit.GetOccupiedCoordsReadViewTyped(),
            sourceUnit.GetFootprintSize(),
            BattleRangeService.GetWeaponAttackRange(sourceUnit),
            ResolveMaximumAdvanceDistance(sourceUnit, skillDefinition),
            skillDefinition?.CombatProfile?.ApproachAttack,
            movementBlocked,
            coord =>
            {
                BattleCellState cell = state.GetCell(coord);
                return cell == null ? null : cell.current_height;
            },
            (fromCoord, toCoord) =>
                gridService.CanUnitStepBetweenAnchors(
                    state,
                    sourceUnit,
                    fromCoord,
                    toCoord
                ),
            (fromCoord, toCoord) =>
                barrierService?.HasUnitBoundaryBarrier(
                    sourceUnit,
                    fromCoord,
                    toCoord
                ) == true
        );
    }

    internal static BattleApproachAttackPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        bool movementBlocked
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
        {
            return BattleApproachAttackPlan.Denied("踏步攻击的单位目标无效。");
        }
        if (
            !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                sourceUnit,
                skillDefinition
            )
            || !BattleRangeService.UnitMatchesRequiredWeaponTypeIds(
                sourceUnit,
                skillDefinition
            )
        )
        {
            return BattleApproachAttackPlan.Denied("当前装备武器不满足踏步攻击要求。");
        }

        BattleStateReadView stateView = state.AsReadView();
        return BuildPlanCore(
            sourceUnit.Coord,
            targetUnit.Coord,
            sourceUnit.GetOccupiedCoords(),
            targetUnit.GetOccupiedCoords(),
            sourceUnit.FootprintSize,
            BattleRangeService.GetWeaponAttackRange(sourceUnit),
            ResolveMaximumAdvanceDistance(sourceUnit, skillDefinition),
            skillDefinition?.CombatProfile?.ApproachAttack,
            movementBlocked,
            coord =>
            {
                BattleCellReadView cell = stateView.GetCell(coord);
                return cell.IsValid ? cell.CurrentHeight : null;
            },
            (fromCoord, toCoord) =>
                gridService.CanUnitStepBetweenAnchors(
                    state,
                    sourceUnit,
                    fromCoord,
                    toCoord
                ),
            (fromCoord, toCoord) =>
                barrierService?.HasUnitBoundaryBarrier(
                    sourceUnit.UnsafeUnitForReadOnlyRules,
                    fromCoord,
                    toCoord
                ) == true
        );
    }

    internal static bool IsAttackReadyAfterAdvance(
        BattleGridService gridService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        if (
            gridService == null
            || sourceUnit == null
            || targetUnit == null
            || !sourceUnit.IsAlive()
            || !targetUnit.IsAlive()
            || !TryResolveDirection(
                sourceUnit.GetAnchorCoord(),
                targetUnit.GetAnchorCoord(),
                out _
            )
            || !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                sourceUnit,
                skillDefinition
            )
            || !BattleRangeService.UnitMatchesRequiredWeaponTypeIds(
                sourceUnit,
                skillDefinition
            )
        )
        {
            return false;
        }

        int weaponRange = BattleRangeService.GetWeaponAttackRange(sourceUnit);
        return weaponRange > 0
            && gridService.GetDistanceBetweenUnits(sourceUnit, targetUnit)
                <= weaponRange;
    }

    internal static bool IsAtPlanHeight(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleApproachAttackPlan plan,
        SkillDefinition skillDefinition
    )
    {
        CombatApproachAttackDefinition profile =
            skillDefinition?.CombatProfile?.ApproachAttack;
        return state != null
            && sourceUnit != null
            && plan != null
            && profile != null
            && AllFootprintCellsWithinOriginHeight(
                sourceUnit.GetOccupiedCoordsReadViewTyped(),
                plan.OriginHeight,
                profile.MaximumPathHeightDeltaFromOrigin,
                coord =>
                {
                    BattleCellState cell = state.GetCell(coord);
                    return cell == null ? null : cell.current_height;
                }
            );
    }

    private static BattleApproachAttackPlan BuildPlanCore(
        Vector2I sourceAnchor,
        Vector2I targetAnchor,
        IReadOnlyList<Vector2I> sourceCoords,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceFootprint,
        int weaponRange,
        int maximumAdvanceDistance,
        CombatApproachAttackDefinition profile,
        bool movementBlocked,
        Func<Vector2I, int?> getHeight,
        Func<Vector2I, Vector2I, bool> canStep,
        Func<Vector2I, Vector2I, bool> hasBoundaryBarrier
    )
    {
        if (profile == null)
            return BattleApproachAttackPlan.Denied("踏步攻击规则未绑定。");
        if (!TryResolveDirection(sourceAnchor, targetAnchor, out Vector2I direction))
            return BattleApproachAttackPlan.Denied("目标必须与施术者处于同一行或同一列。");
        if (movementBlocked)
            return BattleApproachAttackPlan.Denied("当前被限制移动，无法使用踏步攻击。");
        if (weaponRange <= 0 || maximumAdvanceDistance <= 0)
            return BattleApproachAttackPlan.Denied("当前武器或踏步距离不可用。");
        if (sourceCoords == null || sourceCoords.Count == 0)
            return BattleApproachAttackPlan.Denied("施术者占用格数据不可用。");

        int? originHeightValue = getHeight?.Invoke(sourceAnchor);
        if (!originHeightValue.HasValue)
            return BattleApproachAttackPlan.Denied("起始格高度数据不可用。");
        int originHeight = originHeightValue.Value;
        if (
            !AllFootprintCellsWithinOriginHeight(
                sourceCoords,
                originHeight,
                profile.MaximumPathHeightDeltaFromOrigin,
                getHeight
            )
        )
        {
            return BattleApproachAttackPlan.Denied("施术者起始占用格不处于同一允许高度。");
        }

        int currentDistance = GetMinimumDistance(sourceCoords, targetCoords);
        if (currentDistance <= weaponRange)
            return BattleApproachAttackPlan.Denied("目标已在当前武器射程内，无需踏步推进。");

        Vector2I footprint = new(
            Math.Max(sourceFootprint.X, 1),
            Math.Max(sourceFootprint.Y, 1)
        );
        var path = new List<Vector2I> { sourceAnchor };
        Vector2I currentAnchor = sourceAnchor;
        for (int step = 1; step <= maximumAdvanceDistance; step++)
        {
            Vector2I nextAnchor = currentAnchor + direction;
            if (
                canStep?.Invoke(currentAnchor, nextAnchor) != true
                || hasBoundaryBarrier?.Invoke(currentAnchor, nextAnchor) == true
            )
            {
                return BattleApproachAttackPlan.Denied("推进路径被单位、障碍或屏障阻挡。");
            }

            IReadOnlyList<Vector2I> nextCoords = BuildFootprintCoords(
                nextAnchor,
                footprint
            );
            if (
                !AllFootprintCellsWithinOriginHeight(
                    nextCoords,
                    originHeight,
                    profile.MaximumPathHeightDeltaFromOrigin,
                    getHeight
                )
            )
            {
                return BattleApproachAttackPlan.Denied("推进路径与起始格不处于同一允许高度。");
            }

            path.Add(nextAnchor);
            currentAnchor = nextAnchor;
            if (GetMinimumDistance(nextCoords, targetCoords) <= weaponRange)
            {
                return new BattleApproachAttackPlan(
                    true,
                    maximumAdvanceDistance,
                    weaponRange,
                    originHeight,
                    path,
                    ""
                );
            }
        }

        return BattleApproachAttackPlan.Denied("目标超出武器射程与最大推进距离之和。");
    }

    private static int ResolveMaximumAdvanceDistance(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition
    ) =>
        skillDefinition?.CombatProfile?.GetEffectiveRangeValue(
            sourceUnit?.GetKnownSkillLevelTyped(skillDefinition.SkillId) ?? 0
        ) ?? 0;

    private static int ResolveMaximumAdvanceDistance(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition
    ) =>
        skillDefinition?.CombatProfile?.GetEffectiveRangeValue(
            sourceUnit.IsValid
                ? sourceUnit.GetKnownSkillLevel(skillDefinition.SkillId)
                : 0
        ) ?? 0;

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

    private static IReadOnlyList<Vector2I> BuildFootprintCoords(
        Vector2I anchor,
        Vector2I footprint
    )
    {
        var result = new List<Vector2I>(footprint.X * footprint.Y);
        for (int y = 0; y < footprint.Y; y++)
        {
            for (int x = 0; x < footprint.X; x++)
                result.Add(anchor + new Vector2I(x, y));
        }
        return result;
    }

    private static bool AllFootprintCellsWithinOriginHeight(
        IReadOnlyList<Vector2I> coords,
        int originHeight,
        int maximumDelta,
        Func<Vector2I, int?> getHeight
    )
    {
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            int? height = getHeight?.Invoke(coord);
            if (
                !height.HasValue
                || Math.Abs(height.Value - originHeight) > maximumDelta
            )
            {
                return false;
            }
        }
        return true;
    }

    private static int GetMinimumDistance(
        IReadOnlyList<Vector2I> sourceCoords,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        int best = int.MaxValue;
        foreach (Vector2I sourceCoord in sourceCoords ?? Array.Empty<Vector2I>())
        {
            foreach (Vector2I targetCoord in targetCoords ?? Array.Empty<Vector2I>())
            {
                best = Math.Min(
                    best,
                    BattleGridDistanceService.GetDistance(sourceCoord, targetCoord)
                );
            }
        }
        return best;
    }
}
