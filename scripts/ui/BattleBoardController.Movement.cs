using System;
using System.Collections.Generic;
using Godot;

public sealed partial class BattleBoardController
{
    private const double MovementSecondsPerCell = 0.14;
    private sealed record MovementTrack(
        Node2D Token, IReadOnlyList<Vector2I> Path, Vector2[] Positions, int[] Depths);

    private readonly Queue<MovementTrack> _movementTracks = new();
    private int _movementStep = 1;
    private double _movementElapsed;

    internal bool IsMovementPlaying => _movementTracks.Count > 0;

    internal bool PlayMovements(IReadOnlyList<BattleMovementPresentation> movements)
    {
        CancelMovement();
        if (_snapshot == null || movements == null)
            return false;
        foreach (BattleMovementPresentation movement in movements)
        {
            BattleBoardUnitSnapshot unit = _snapshot.GetUnit(movement.UnitId);
            if (unit == null || movement.Path.Count < 2
                || !_unitNodesById.TryGetValue(movement.UnitId, out Node2D token))
                continue;
            var positions = new Vector2[movement.Path.Count];
            var depths = new int[movement.Path.Count];
            for (int index = 0; index < movement.Path.Count; index++)
            {
                // Translate the complete footprint, including large enemies. Heights
                // come from the board generation on which this command was issued.
                Vector2I offset = movement.Path[index] - unit.AnchorCoord;
                Vector2 sum = Vector2.Zero;
                int count = 0;
                int depth = int.MinValue;
                foreach (Vector2I occupied in unit.OccupiedCoords)
                {
                    Vector2I coord = occupied + offset;
                    BattleBoardCellSnapshot cell = _snapshot.GetCell(coord);
                    if (cell == null)
                        continue;
                    sum += _get_cell_anchor_position(coord, cell.Height);
                    depth = Math.Max(depth, _get_cell_render_depth(coord, cell.Height));
                    count++;
                }
                positions[index] = count > 0 ? sum / count + _get_unit_anchor_bias() : token.Position;
                depths[index] = count > 0 ? depth : token.ZIndex;
            }
            _movementTracks.Enqueue(new(token, movement.Path, positions, depths));
        }
        if (!IsMovementPlaying)
            return false;
        BeginMovementTrack();
        return true;
    }

    private void BeginMovementTrack()
    {
        _movementStep = 1;
        _movementElapsed = 0;
        MovementTrack track = _movementTracks.Peek();
        SetMovementPose(track, 0, track.Positions[0]);
    }

    internal void AdvanceMovement(double delta)
    {
        if (!IsMovementPlaying)
            return;
        MovementTrack track = _movementTracks.Peek();
        // Never skip intermediate cells after a slow frame or a window stall.
        _movementElapsed += Math.Clamp(delta, 0, 0.05);
        float progress = (float)Math.Min(_movementElapsed / MovementSecondsPerCell, 1);
        Vector2 position = track.Positions[_movementStep - 1].Lerp(
            track.Positions[_movementStep], progress);
        SetMovementPose(track, progress < 0.5f ? _movementStep - 1 : _movementStep, position);
        if (progress < 1)
            return;
        _movementElapsed = 0;
        _movementStep++;
        if (_movementStep < track.Path.Count)
            return;
        _movementTracks.Dequeue();
        if (IsMovementPlaying)
            BeginMovementTrack();
    }

    private static void SetMovementPose(MovementTrack track, int index, Vector2 position)
    {
        track.Token.Position = position;
        track.Token.ZIndex = track.Depths[index];
        track.Token.SetMeta("board_coord", track.Path[index]);
        track.Token.SetMeta("sort_anchor_y", position.Y);
        track.Token.SetMeta("sort_depth", track.Depths[index]);
    }

    internal void CancelMovement()
    {
        _movementTracks.Clear();
        _movementStep = 1;
        _movementElapsed = 0;
    }
}
