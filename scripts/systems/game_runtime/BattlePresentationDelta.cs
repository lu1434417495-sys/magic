using System;
using System.Collections.Generic;
using Godot;

[Flags]
internal enum BattlePresentationDirtyFlags
{
    None = 0,
    Log = 1 << 0,
    Hud = 1 << 1,
    Selection = 1 << 2,
    Units = 1 << 3,
    FullBoard = 1 << 4,
}

internal sealed class BattlePresentationDelta
{
    private static readonly IReadOnlyList<StringName> EmptyUnitIds = Array.Empty<StringName>();
    private static readonly IReadOnlyList<Vector2I> EmptyCoords = Array.Empty<Vector2I>();

    internal static BattlePresentationDelta None { get; } =
        new(BattlePresentationDirtyFlags.None, EmptyUnitIds, EmptyCoords);

    internal static BattlePresentationDelta Full { get; } =
        new(
            BattlePresentationDirtyFlags.Hud
                | BattlePresentationDirtyFlags.Selection
                | BattlePresentationDirtyFlags.Units
                | BattlePresentationDirtyFlags.FullBoard,
            EmptyUnitIds,
            EmptyCoords
        );

    internal static BattlePresentationDelta Overlay { get; } =
        new(
            BattlePresentationDirtyFlags.Hud | BattlePresentationDirtyFlags.Selection,
            EmptyUnitIds,
            EmptyCoords
        );

    internal BattlePresentationDelta(
        BattlePresentationDirtyFlags dirtyFlags,
        IReadOnlyList<StringName> changedUnitIds,
        IReadOnlyList<Vector2I> changedCoords
    )
    {
        DirtyFlags = dirtyFlags;
        ChangedUnitIds = changedUnitIds ?? EmptyUnitIds;
        ChangedCoords = changedCoords ?? EmptyCoords;
    }

    internal BattlePresentationDirtyFlags DirtyFlags { get; }
    internal IReadOnlyList<StringName> ChangedUnitIds { get; }
    internal IReadOnlyList<Vector2I> ChangedCoords { get; }
    internal bool HasChanges => DirtyFlags != BattlePresentationDirtyFlags.None;
    internal bool IsLogOnly => DirtyFlags == BattlePresentationDirtyFlags.Log;
    internal bool RequiresFullBoardRefresh =>
        (DirtyFlags & BattlePresentationDirtyFlags.FullBoard) != 0;
    internal bool RequiresPanelRefresh =>
        (DirtyFlags
            & (
                BattlePresentationDirtyFlags.Hud
                | BattlePresentationDirtyFlags.Selection
                | BattlePresentationDirtyFlags.Units
                | BattlePresentationDirtyFlags.FullBoard
            )) != 0;

    internal BattleRefreshMode ToLegacyRefreshMode()
    {
        if (RequiresFullBoardRefresh)
            return BattleRefreshMode.Full;
        return HasChanges ? BattleRefreshMode.Overlay : BattleRefreshMode.None;
    }
}
