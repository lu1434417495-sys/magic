using System.Collections.Generic;
using Godot;

/// <summary>Retained terrain drawing commands; rebuilt only with a terrain snapshot.</summary>
public sealed partial class BattleTerrainPaintLayer : Node2D
{
    internal readonly record struct Patch(Vector2[] Points, Vector2[] Uvs, Color Tint);
    internal readonly record struct Stroke(Vector2[] Points, Color Tint, float Width);
    internal readonly List<Patch> Patches = new();
    internal readonly List<Stroke> Strokes = new();
    internal Texture2D Texture;

    public override void _Draw()
    {
        foreach (Patch patch in Patches)
            DrawPolygon(patch.Points, new[] { patch.Tint }, patch.Uvs, Texture);
        foreach (Stroke stroke in Strokes)
            DrawPolyline(stroke.Points, stroke.Tint, stroke.Width, true);
    }
}
