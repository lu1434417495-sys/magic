using Godot;

/// <summary>Draw-only ground marker and medallion for a detached board unit.</summary>
public partial class BattleUnitTokenDecoration : Node2D
{
    private static readonly Vector2[] Shield =
    {
        new(-34, -30), new(0, -40), new(34, -30),
        new(32, 16), new(0, 34), new(-32, 16), new(-34, -30),
    };

    public Color FactionColor { get; init; }
    public Vector2 GroundAnchor { get; init; }
    public bool IsActive { get; init; }
    public bool ShowMedallion { get; init; }

    public override void _Draw()
    {
        DrawSetTransform(GroundAnchor + new Vector2(0, 3), 0, new Vector2(1, 0.3f));
        DrawCircle(Vector2.Zero, 62, new Color(0.04f, 0.035f, 0.03f, 0.12f), antialiased: true);
        DrawCircle(Vector2.Zero, 53, new Color(0.04f, 0.035f, 0.03f, 0.28f), antialiased: true);
        DrawArc(Vector2.Zero, 54, 0, Mathf.Tau, 64,
            IsActive ? new Color(1, 0.87f, 0.52f) : FactionColor, IsActive ? 4 : 2.5f, true);
        DrawSetTransform(Vector2.Zero);
        if (!ShowMedallion)
            return;

        DrawLine(new Vector2(0, 24), GroundAnchor, new Color(0.13f, 0.14f, 0.16f), 8, true);
        DrawColoredPolygon(Shield, new Color(0.08f, 0.10f, 0.13f));
        DrawPolyline(Shield, FactionColor, 3, true);
        DrawSetTransform(new Vector2(0, -3), 0, new Vector2(0.84f, 0.83f));
        DrawPolyline(Shield, new Color(FactionColor, 0.32f), 1.5f, true);
        DrawSetTransform(Vector2.Zero);
        if (IsActive)
        {
            DrawColoredPolygon(new[]
            {
                new Vector2(-7, -49), new Vector2(7, -49), new Vector2(0, -42),
            }, new Color(1, 0.87f, 0.52f));
        }
    }
}
