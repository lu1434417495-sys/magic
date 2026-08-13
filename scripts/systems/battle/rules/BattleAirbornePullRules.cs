using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleAirbornePullPlan
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    private BattleAirbornePullPlan(
        bool allowed,
        Vector2I sourceCoord,
        Vector2I destinationCoord,
        int distance,
        int maximumDistance,
        int targetBodySize,
        int maximumTargetBodySize,
        string message
    )
    {
        Allowed = allowed;
        SourceCoord = sourceCoord;
        DestinationCoord = destinationCoord;
        Distance = Math.Max(distance, 0);
        MaximumDistance = Math.Max(maximumDistance, 0);
        TargetBodySize = Math.Max(targetBodySize, 0);
        MaximumTargetBodySize = Math.Max(maximumTargetBodySize, 0);
        Message = message ?? "";
    }

    internal bool Allowed { get; }
    internal Vector2I SourceCoord { get; }
    internal Vector2I DestinationCoord { get; }
    internal int Distance { get; }
    internal int MaximumDistance { get; }
    internal int TargetBodySize { get; }
    internal int MaximumTargetBodySize { get; }
    internal string Message { get; }

    internal static BattleAirbornePullPlan AllowedResult(
        Vector2I sourceCoord,
        Vector2I destinationCoord,
        int maximumDistance,
        int targetBodySize,
        int maximumTargetBodySize
    ) =>
        new(
            true,
            sourceCoord,
            destinationCoord,
            ManhattanDistance(sourceCoord, destinationCoord),
            maximumDistance,
            targetBodySize,
            maximumTargetBodySize,
            ""
        );

    internal static BattleAirbornePullPlan Denied(string message) =>
        new(false, InvalidCoord, InvalidCoord, 0, 0, 0, 0, message);

    private static int ManhattanDistance(Vector2I left, Vector2I right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
}

internal static class BattleAirbornePullRules
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    internal static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition
            in effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.ForcedMove
                && effectDefinition.ForcedMoveModeKind == BattleForcedMoveMode.AirbornePull
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    internal static bool HasEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) => FindEffect(effectDefinitions) != null;

    internal static BattleAirbornePullPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        Vector2I destinationCoord
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
            return BattleAirbornePullPlan.Denied("牵引单位或目标无效。");
        }
        return BuildPlanCore(
            sourceUnit.GetAnchorCoord(),
            sourceUnit.unit_id,
            sourceUnit.GetOccupiedCoordsReadViewTyped(),
            targetUnit.GetAnchorCoord(),
            targetUnit.GetBodySize(),
            targetUnit.GetFootprintSize(),
            effectDefinition,
            destinationCoord,
            targetUnit.HasStatusEffect,
            statusId => targetUnit.GetStatusEffect(statusId),
            () => BattleTemporalStatusService.HasTimeStasis(targetUnit),
            () => BlocksEnemyForcedMove(sourceUnit, targetUnit),
            coord => gridService.CanPlaceUnit(state, targetUnit, coord, ignore_height: true),
            (fromCoord, toCoord) =>
                barrierService?.HasUnitBoundaryBarrier(targetUnit, fromCoord, toCoord) == true
        );
    }

    internal static BattleAirbornePullPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        CombatEffectDefinition effectDefinition,
        Vector2I destinationCoord
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
            return BattleAirbornePullPlan.Denied("牵引单位或目标无效。");
        }
        return BuildPlanCore(
            sourceUnit.Coord,
            sourceUnit.UnitId,
            sourceUnit.GetOccupiedCoords(),
            targetUnit.Coord,
            targetUnit.BodySize,
            targetUnit.FootprintSize,
            effectDefinition,
            destinationCoord,
            targetUnit.HasStatusEffect,
            statusId => targetUnit.GetStatus(statusId),
            () => targetUnit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS),
            () => BlocksEnemyForcedMove(sourceUnit, targetUnit),
            coord => gridService.CanPlaceUnit(state, targetUnit, coord, ignore_height: true),
            (fromCoord, toCoord) =>
                barrierService?.HasUnitBoundaryBarrier(
                    targetUnit.UnsafeUnitForReadOnlyRules,
                    fromCoord,
                    toCoord
                ) == true
        );
    }

    internal static IReadOnlyList<Vector2I> CollectLegalDestinations(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (targetUnit == null || effectDefinition == null)
            return Array.Empty<Vector2I>();
        var result = new List<Vector2I>();
        int maximumDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0);
        Vector2I origin = targetUnit.GetAnchorCoord();
        for (int deltaY = -maximumDistance; deltaY <= maximumDistance; deltaY++)
        {
            for (int deltaX = -maximumDistance; deltaX <= maximumDistance; deltaX++)
            {
                int distance = Math.Abs(deltaX) + Math.Abs(deltaY);
                if (distance < 1 || distance > maximumDistance)
                    continue;
                Vector2I candidate = origin + new Vector2I(deltaX, deltaY);
                if (
                    BuildPlan(
                        state,
                        gridService,
                        barrierService,
                        sourceUnit,
                        targetUnit,
                        effectDefinition,
                        candidate
                    ).Allowed
                )
                {
                    result.Add(candidate);
                }
            }
        }
        result.Sort(
            (left, right) =>
                left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X)
        );
        return new ReadOnlyCollection<Vector2I>(result);
    }

    private static BattleAirbornePullPlan BuildPlanCore<TStatus>(
        Vector2I sourceCoord,
        StringName sourceUnitId,
        IReadOnlyList<Vector2I> sourceOccupiedCoords,
        Vector2I targetCoord,
        int targetBodySize,
        Vector2I targetFootprintSize,
        CombatEffectDefinition effectDefinition,
        Vector2I destinationCoord,
        Func<StringName, bool> hasStatus,
        Func<StringName, TStatus> getStatus,
        Func<bool> hasTimeStasis,
        Func<bool> blocksEnemyForcedMove,
        Func<Vector2I, bool> canPlace,
        Func<Vector2I, Vector2I, bool> hasBoundaryBarrier
    )
    {
        if (
            effectDefinition?.EffectKind != BattleEffectKind.ForcedMove
            || effectDefinition.ForcedMoveModeKind != BattleForcedMoveMode.AirbornePull
        )
        {
            return BattleAirbornePullPlan.Denied("当前效果不是空中牵引。");
        }
        int maximumDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0);
        if (maximumDistance <= 0)
            return BattleAirbornePullPlan.Denied("空中牵引距离必须大于0。");
        int maximumBodySize = Math.Max(effectDefinition.ForcedMoveMaxTargetBodySize, 0);
        if (targetBodySize < 1 || maximumBodySize < 1 || targetBodySize > maximumBodySize)
        {
            return BattleAirbornePullPlan.Denied(
                $"目标体型 {targetBodySize} 超出本等级允许的体型上限 {maximumBodySize}。"
            );
        }
        StringName requiredStatusId = ProgressionDataUtils.to_string_name(
            effectDefinition.RequiredTargetStatusId
        );
        if (requiredStatusId == "" || hasStatus?.Invoke(requiredStatusId) != true)
            return BattleAirbornePullPlan.Denied("目标必须处于感电状态。");
        if (
            !TargetStatusRequirementPasses(
                effectDefinition,
                requiredStatusId,
                getStatus,
                sourceUnitId
            )
        )
        {
            return BattleAirbornePullPlan.Denied("目标的感电层数或来源不满足牵引条件。");
        }
        if (hasTimeStasis?.Invoke() == true)
            return BattleAirbornePullPlan.Denied("目标处于时间静滞，无法被牵引。");
        if (blocksEnemyForcedMove?.Invoke() == true)
            return BattleAirbornePullPlan.Denied("目标免疫敌方强制位移。");
        if (destinationCoord == InvalidCoord || destinationCoord == targetCoord)
            return BattleAirbornePullPlan.Denied("必须选择一个不同于目标当前位置的落点。");
        int displacementDistance = ManhattanDistance(targetCoord, destinationCoord);
        if (displacementDistance < 1 || displacementDistance > maximumDistance)
        {
            return BattleAirbornePullPlan.Denied(
                $"落点超出最大牵引距离 {maximumDistance}。"
            );
        }
        int currentCasterDistance = DistanceBetweenFootprints(
            sourceOccupiedCoords,
            BuildFootprint(targetCoord, targetFootprintSize)
        );
        int destinationCasterDistance = DistanceBetweenFootprints(
            sourceOccupiedCoords,
            BuildFootprint(destinationCoord, targetFootprintSize)
        );
        if (destinationCasterDistance >= currentCasterDistance)
            return BattleAirbornePullPlan.Denied("牵引落点必须比目标当前位置更接近施法者。");
        if (canPlace?.Invoke(destinationCoord) != true)
            return BattleAirbornePullPlan.Denied("牵引落点无法容纳目标完整体型或已被占用。");
        if (hasBoundaryBarrier?.Invoke(targetCoord, destinationCoord) == true)
            return BattleAirbornePullPlan.Denied("目标与落点之间存在不可穿越的魔法边界。");

        return BattleAirbornePullPlan.AllowedResult(
            targetCoord,
            destinationCoord,
            maximumDistance,
            targetBodySize,
            maximumBodySize
        );
    }

    private static bool TargetStatusRequirementPasses<TStatus>(
        CombatEffectDefinition effectDefinition,
        StringName requiredStatusId,
        Func<StringName, TStatus> getStatus,
        StringName sourceUnitId
    )
    {
        object status = getStatus != null ? getStatus(requiredStatusId) : default(TStatus);
        int stacks = status switch
        {
            BattleStatusEffectState mutableStatus => mutableStatus.stacks,
            BattleStatusReadView readStatus => readStatus.Stacks,
            _ => 0,
        };
        if (
            Math.Max(stacks, 0)
            < Math.Max(effectDefinition.RequiredTargetStatusMinStacks, 1)
        )
        {
            return false;
        }
        StringName sourceSelector = ProgressionDataUtils.to_string_name(
            effectDefinition.RequiredTargetStatusSourceSelector
        );
        if (sourceSelector == "")
            return true;
        if (
            sourceSelector != "source"
            && sourceSelector != "attacker"
            && sourceSelector != "owner"
            && sourceSelector != "caster"
        )
        {
            return false;
        }
        StringName statusSourceUnitId = status switch
        {
            BattleStatusEffectState mutableStatus => mutableStatus.source_unit_id,
            BattleStatusReadView readStatus => readStatus.SourceUnitId,
            _ => new StringName(""),
        };
        return sourceUnitId != ""
            && ProgressionDataUtils.to_string_name(statusSourceUnitId) == sourceUnitId;
    }

    private static bool BlocksEnemyForcedMove(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (
            sourceUnit == null
            || targetUnit == null
            || sourceUnit.unit_id == targetUnit.unit_id
            || sourceUnit.faction_id == targetUnit.faction_id
        )
        {
            return false;
        }
        foreach (BattleStatusEffectState status in targetUnit.GetStatusEffectsTyped())
        {
            if (status?.stacks > 0 && status.forced_move_immune)
                return true;
        }
        return false;
    }

    private static bool BlocksEnemyForcedMove(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit
    )
    {
        if (
            !sourceUnit.IsValid
            || !targetUnit.IsValid
            || sourceUnit.UnitId == targetUnit.UnitId
            || sourceUnit.FactionId == targetUnit.FactionId
        )
        {
            return false;
        }
        foreach (BattleStatusReadView status in targetUnit.StatusEffects())
        {
            if (status.IsValid && status.Stacks > 0 && status.ForcedMoveImmune)
                return true;
        }
        return false;
    }

    private static List<Vector2I> BuildFootprint(Vector2I anchor, Vector2I footprintSize)
    {
        var result = new List<Vector2I>();
        int width = Math.Max(footprintSize.X, 1);
        int height = Math.Max(footprintSize.Y, 1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                result.Add(anchor + new Vector2I(x, y));
        }
        return result;
    }

    private static int DistanceBetweenFootprints(
        IReadOnlyList<Vector2I> leftCoords,
        IReadOnlyList<Vector2I> rightCoords
    )
    {
        int best = int.MaxValue;
        foreach (Vector2I left in leftCoords ?? Array.Empty<Vector2I>())
        {
            foreach (Vector2I right in rightCoords ?? Array.Empty<Vector2I>())
                best = Math.Min(best, ManhattanDistance(left, right));
        }
        return best == int.MaxValue ? int.MaxValue / 2 : best;
    }

    private static int ManhattanDistance(Vector2I left, Vector2I right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
}
