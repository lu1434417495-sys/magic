using System.Collections.Generic;
using Godot;

/// <summary>
/// 屏障边界只读判定。只读取调用方传入的 BattleState / BattleGridService，
/// 预览、执行与 AI 候选共用同一份判定，不经 BattleRuntimeModule 取当前战场。
/// </summary>
internal static class BattleBarrierBoundaryRules
{
    internal static bool HasUnitBoundaryBarrier(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState unitState,
        Vector2I fromCoord,
        Vector2I toCoord
    )
    {
        if (state == null || gridService == null || unitState == null)
            return false;
        foreach (StringName barrierKey in state.LayeredBarrierStore.SortedKeys())
        {
            if (!TryReadBarrier(state, barrierKey, out BattleBarrierInstanceState barrier))
                continue;
            if (IsBarrierCreator(unitState, barrier))
                continue;
            BattleBarrierFootprintTransition transition =
                BattleBarrierGeometryService.ClassifyFootprintTransition(
                    gridService.GetFootprintCoords(fromCoord, unitState.GetFootprintSize()),
                    gridService.GetFootprintCoords(toCoord, unitState.GetFootprintSize()),
                    GetBarrierCoords(state, gridService, barrier)
                );
            if (transition.CrossesBoundary)
                return true;
        }
        return false;
    }

    internal static bool HasActiveBarrierBoundaryBetween(
        BattleState state,
        BattleGridService gridService,
        Vector2I fromCoord,
        Vector2I toCoord
    )
    {
        if (state == null || gridService == null)
            return false;
        foreach (StringName barrierKey in state.LayeredBarrierStore.SortedKeys())
        {
            if (
                !TryReadBarrier(state, barrierKey, out BattleBarrierInstanceState barrier)
                || GetActiveLayer(barrier) == null
            )
            {
                continue;
            }
            BattleBarrierFootprintTransition transition =
                BattleBarrierGeometryService.ClassifyFootprintTransition(
                    new[] { fromCoord },
                    new[] { toCoord },
                    GetBarrierCoords(state, gridService, barrier)
                );
            if (transition.CrossesBoundary)
                return true;
        }
        return false;
    }

    internal static BattleBarrierLayerState GetActiveLayer(BattleBarrierInstanceState barrier)
    {
        foreach (
            BattleBarrierLayerState layer in barrier?.GetLayersTyped()
                ?? new List<BattleBarrierLayerState>()
        )
        {
            if (layer != null && !layer.Broken)
                return layer;
        }
        return null;
    }

    internal static List<Vector2I> GetBarrierCoords(
        BattleState state,
        BattleGridService gridService,
        BattleBarrierInstanceState barrier
    )
    {
        var coords = new List<Vector2I>();
        if (state == null || gridService == null || barrier == null || barrier.IsEmpty)
            return coords;
        int radius = Mathf.Max(barrier.RadiusCells, 0);
        foreach (
            Vector2I coord in gridService.GetAreaCoords(
                state,
                barrier.AnchorCoord,
                barrier.AreaPattern,
                radius,
                Vector2I.Zero
            )
        )
        {
            coords.Add(coord);
        }
        return coords;
    }

    internal static bool IsBarrierCreator(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier
    ) =>
        unitState != null
        && barrier != null
        && unitState.unit_id == barrier.SourceUnitId;

    private static bool TryReadBarrier(
        BattleState state,
        StringName barrierKey,
        out BattleBarrierInstanceState barrier
    )
    {
        barrier = null;
        if (barrierKey == "")
            return false;
        return state.TryGetLayeredBarrierField(barrierKey, out barrier);
    }
}
