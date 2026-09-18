using System.Collections;
using System.Collections.Generic;
using Godot;

public sealed partial class BattleBoardController
{
    private sealed record UnitSpriteHitMask(Vector2I Size, Vector2I Margin, BitArray Pixels);
    private readonly Dictionary<Texture2D, UnitSpriteHitMask> _unitSpriteHitMasks =
        new(GodotWrapperReferenceComparer.Instance);

    internal Vector2I PickHoveredUnit(Vector2 boardPosition)
    {
        Vector2I result = new(-1, -1);
        if (_snapshot == null || _input_layer == null) return result;
        int bestDepth = int.MinValue;
        Vector2 global = ((Node2D)_input_layer.GetParent()).ToGlobal(boardPosition);
        foreach ((StringName id, Node2D token) in _unitNodesById)
        {
            BattleBoardUnitSnapshot unit = _snapshot.GetUnit(id);
            if (unit == null || !unit.IsAlive || !token.Visible || token.ZIndex < bestDepth) continue;
            Sprite2D sprite = token.GetNodeOrNull<Sprite2D>("UnitSprite");
            bool hit;
            if (sprite != null && _unitSpriteHitMasks.TryGetValue(sprite.Texture, out var mask))
            {
                Vector2 pixel = sprite.ToLocal(global) + sprite.Texture.GetSize() * 0.5f - mask.Margin;
                int x = Mathf.FloorToInt(pixel.X), y = Mathf.FloorToInt(pixel.Y);
                hit = x >= 0 && y >= 0 && x < mask.Size.X && y < mask.Size.Y
                    && mask.Pixels[y * mask.Size.X + x];
            }
            else
            {
                Label glyph = token.GetNodeOrNull<Label>("UnitGlyphLabel");
                hit = glyph?.Visible == true && new Rect2(glyph.Position, glyph.Size).HasPoint(token.ToLocal(global));
            }
            if (!hit) continue;
            // Visible terrain in front of this unit still occludes its body.
            bool occluded = false;
            foreach (Node2D node in _terrainArtNodes)
            {
                if (node.ZIndex <= token.ZIndex || node is not BattleTerrainPaintLayer layer) continue;
                string name = layer.Name.ToString();
                if (!name.StartsWith("PaintTop") && !name.StartsWith("PaintFace")) continue;
                foreach (BattleTerrainPaintLayer.Patch patch in layer.Patches)
                {
                    if (!Geometry2D.IsPointInPolygon(boardPosition, patch.Points)) continue;
                    occluded = true;
                    break;
                }
                if (occluded) break;
            }
            if (occluded) continue;
            bestDepth = token.ZIndex;
            result = unit.AnchorCoord;
        }
        return result;
    }
}
