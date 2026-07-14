using System;
using Godot;

internal static class BattlePresentationDeltaFactory
{
    internal static BattlePresentationDelta Create(BattleEventBatch batch)
    {
        if (batch == null)
            return BattlePresentationDelta.None;

        BattleChangeFlags changeFlags = batch.ChangeFlags;
        BattlePresentationDirtyFlags dirtyFlags = BattlePresentationDirtyFlags.None;
        if ((changeFlags & BattleChangeFlags.Log) != 0)
            dirtyFlags |= BattlePresentationDirtyFlags.Log;
        if (
            (changeFlags
                & (
                    BattleChangeFlags.UnitState
                    | BattleChangeFlags.Timeline
                )) != 0
        )
        {
            dirtyFlags |= BattlePresentationDirtyFlags.Hud | BattlePresentationDirtyFlags.Units;
        }
        if ((changeFlags & BattleChangeFlags.Phase) != 0)
        {
            dirtyFlags |=
                BattlePresentationDirtyFlags.Hud
                | BattlePresentationDirtyFlags.Selection
                | BattlePresentationDirtyFlags.Units
                | BattlePresentationDirtyFlags.FullBoard;
        }
        if (
            (changeFlags
                & (
                    BattleChangeFlags.AffectedCoords
                    | BattleChangeFlags.UnitPlacement
                    | BattleChangeFlags.CellState
                    | BattleChangeFlags.PropState
                    | BattleChangeFlags.BattleLifecycle
                    | BattleChangeFlags.Modal
                    | BattleChangeFlags.Progression
                )) != 0
        )
        {
            dirtyFlags |=
                BattlePresentationDirtyFlags.Hud
                | BattlePresentationDirtyFlags.Selection
                | BattlePresentationDirtyFlags.Units
                | BattlePresentationDirtyFlags.FullBoard;
        }

        if (dirtyFlags == BattlePresentationDirtyFlags.None)
            return BattlePresentationDelta.None;
        return new BattlePresentationDelta(
            dirtyFlags,
            CopyUnitIds(batch.ChangedUnitIdsTyped),
            CopyCoords(batch.ChangedCoordsTyped)
        );
    }

    internal static BattlePresentationDelta FromRefreshMode(BattleRefreshMode refreshMode) =>
        refreshMode switch
        {
            BattleRefreshMode.Full => BattlePresentationDelta.Full,
            BattleRefreshMode.Overlay => BattlePresentationDelta.Overlay,
            _ => BattlePresentationDelta.None,
        };

    private static StringName[] CopyUnitIds(System.Collections.Generic.IReadOnlyList<StringName> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<StringName>();
        var result = new StringName[source.Count];
        for (int index = 0; index < source.Count; index++)
            result[index] = source[index];
        return result;
    }

    private static Vector2I[] CopyCoords(System.Collections.Generic.IReadOnlyList<Vector2I> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<Vector2I>();
        var result = new Vector2I[source.Count];
        for (int index = 0; index < source.Count; index++)
            result[index] = source[index];
        return result;
    }
}
