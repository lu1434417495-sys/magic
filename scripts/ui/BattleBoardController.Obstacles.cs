using Godot;

public sealed partial class BattleBoardController
{
    private void DrawBlockedBoundary(BattleBoardEdgeSnapshot edge, Vector2 axisX, Vector2 axisY)
    {
        DrawBlockedBoundarySide(edge.OriginCoord, edge.Direction, axisX, axisY);
        DrawBlockedBoundarySide(edge.NeighborCoord, -edge.Direction, axisX, axisY);
    }

    private void DrawBlockedBoundarySide(Vector2I coord, Vector2I direction, Vector2 axisX, Vector2 axisY)
    {
        BattleBoardCellSnapshot cell = _snapshot.GetCell(coord);
        if (cell == null) return;
        Vector2 across = direction.X != 0 ? axisX * direction.X : axisY * direction.Y;
        Vector2 along = direction.X != 0 ? axisY : axisX;
        Vector2 anchor = _get_cell_anchor_position(coord, cell.Height);
        // Inset onto each adjacent surface: both an ascent and a descent remain
        // readable even when the actual cliff face points away from the camera.
        Vector2 center = anchor + across * 0.43f;
        Vector2 a = center - along * 0.44f;
        Vector2 b = center + along * 0.44f;
        var border = AddTerrainPaintLayer($"BlockedEdge_{coord.X}_{coord.Y}_{direction.X}_{direction.Y}",
            GetCellDrawDepth(coord) + 5, null, null);
        border.Strokes.Add(new(new[] { a, b }, new Color(0.19f, 0.13f, 0.10f, 0.95f), 11f));
        border.Strokes.Add(new(new[] { a, b }, new Color(0.90f, 0.39f, 0.17f), 5f));
        foreach (float offset in new[] { 0.22f, 0.5f, 0.78f })
        {
            Vector2 point = a.Lerp(b, offset);
            border.Strokes.Add(new(new[] { point - across * 0.035f, point + across * 0.035f },
                new Color(1f, 0.79f, 0.46f), 3.5f));
        }
    }

    private void DrawRaisedWall(BattleBoardEdgeSnapshot edge, Vector2 axisX, Vector2 axisY)
    {
        bool right = edge.Direction == Vector2I.Right;
        BattleBoardCellSnapshot neighbor = _snapshot.GetCell(edge.NeighborCoord);
        int baseHeight = Mathf.Max(edge.FromHeight, neighbor?.Height ?? edge.FromHeight);
        Vector2 anchor = _get_cell_anchor_position(edge.OriginCoord, baseHeight);
        Vector2 across = right ? axisX : axisY;
        Vector2 along = right ? axisY : -axisX;
        Vector2 middle = anchor + across * 0.5f;
        Vector2 thickness = across * 0.065f;
        Vector2 a = middle - along * 0.5f + thickness;
        Vector2 b = middle + along * 0.5f + thickness;
        Vector2 rise = new(0, edge.FeatureLayers * _get_visual_height_step());
        Vector2 topA = a - rise;
        Vector2 topB = b - rise;
        Vector2 rearA = topA - thickness * 2;
        Vector2 rearB = topB - thickness * 2;
        // A wall stands on the higher adjoining surface, in front of the near
        // cell's grid, while that cell's tree/unit can still stand in front of it.
        int depth = GetCellDrawDepth(edge.NeighborCoord) + 5;
        string suffix = $"{edge.OriginCoord.X}_{edge.OriginCoord.Y}_{right}";
        var face = AddTerrainPaintLayer($"RaisedWall_{suffix}", depth, _paintedRock, _paintedCliffMaterial);
        face.Patches.Add(new(new[] { topA, topB, b, a },
            new[] { new Vector2(0, 0), new Vector2(0.6f, 0),
                new Vector2(0.6f, edge.FeatureLayers * 0.35f), new Vector2(0, edge.FeatureLayers * 0.35f) },
            right ? new Color(0.64f, 0.70f, 0.75f) : new Color(0.83f, 0.85f, 0.84f)));
        var side = AddTerrainPaintLayer($"WallEnd_{suffix}", depth, _paintedRock, _paintedCliffMaterial);
        side.Patches.Add(new(new[] { rearB, topB, b, b - thickness * 2 },
            new[] { Vector2.Zero, new Vector2(0.1f, 0),
                new Vector2(0.1f, 0.35f), new Vector2(0, 0.35f) }, new Color(0.51f, 0.55f, 0.58f)));
        var cap = AddTerrainPaintLayer($"WallCap_{suffix}", depth, _paintedGround, null);
        cap.Patches.Add(new(new[] { rearA, rearB, topB, topA },
            new[] { Vector2.Zero, new Vector2(0.6f, 0),
                new Vector2(0.6f, 0.12f), new Vector2(0, 0.12f) }, new Color(0.87f, 0.89f, 0.85f)));
        var seams = AddTerrainPaintLayer($"WallMasonry_{suffix}", depth, null, null);
        Color mortar = new(0.20f, 0.19f, 0.16f, 0.80f);
        seams.Strokes.Add(new(new[] { rearA, rearB, topB, b, a, topA, rearA }, mortar, 3f));
        int courses = edge.FeatureLayers * 3;
        for (int row = 0; row < courses; row++)
        {
            Vector2 down = rise * (row / (float)courses);
            seams.Strokes.Add(new(new[] { topA + down, topB + down }, mortar, 2.5f));
            for (int block = 0; block < 4; block++)
            {
                float fraction = (block + (row % 2 == 0 ? 0.5f : 1f)) / 4f;
                Vector2 start = topA.Lerp(topB, fraction) + down;
                seams.Strokes.Add(new(new[] { start, start + rise / courses }, mortar, 2f));
            }
        }
    }
}
