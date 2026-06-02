using System;
using Godot;

[GlobalClass]
public partial class BattleBoardProp : Node2D
{
    private static readonly StringName PropSpikeBarricade = "spike_barricade";
    private static readonly StringName PropObjectiveMarker = "objective_marker";
    private static readonly StringName PropTent = "tent";
    private static readonly StringName PropTorch = "torch";

    [Export]
    public StringName prop_id { get; set; } = PropTent;

    private int _optionSeed;
    private bool _needsInteractionShape;
    private Area2D _interactionArea;
    private CollisionShape2D _interactionShape;

    public override void _Ready()
    {
        _interactionArea = GetNode<Area2D>("%InteractionArea");
        _interactionShape = GetNode<CollisionShape2D>("%InteractionShape");
        ApplyInteractionState();
        QueueRedraw();
    }

    public void configure(StringName new_prop_id)
    {
        configure(new_prop_id, 0, false);
    }

    public void configure(StringName new_prop_id, int option_seed)
    {
        configure(new_prop_id, option_seed, false);
    }

    public void configure(StringName new_prop_id, int option_seed, bool needs_interaction_shape)
    {
        prop_id = new_prop_id;
        _optionSeed = option_seed;
        _needsInteractionShape = needs_interaction_shape;
        ApplyInteractionState();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (prop_id == PropSpikeBarricade)
            DrawSpikeBarricade();
        else if (prop_id == PropObjectiveMarker)
            DrawObjectiveMarker();
        else if (prop_id == PropTorch)
            DrawTorch();
        else
            DrawTent();
    }

    private void ApplyInteractionState()
    {
        if (_interactionArea == null || _interactionShape == null)
            return;
        _interactionArea.Monitoring = _needsInteractionShape;
        _interactionArea.Monitorable = _needsInteractionShape;
        _interactionShape.Disabled = !_needsInteractionShape;
    }

    private void DrawSpikeBarricade()
    {
        var woodDark = new Color(0.22f, 0.11f, 0.06f, 0.96f);
        var woodMid = new Color(0.46f, 0.26f, 0.15f, 0.98f);
        const float braceY = -6.0f;
        Vector2[] points =
        {
            new(-16.0f, 0.0f),
            new(-8.0f, -24.0f),
            new(0.0f, -4.0f),
            new(9.0f, -27.0f),
            new(17.0f, -2.0f),
        };
        for (int index = 0; index < points.Length; index++)
        {
            Vector2 tip = points[index];
            float sway = SignedOffset(index + 1, 2.0f);
            DrawLine(
                new Vector2(tip.X + sway, 0.0f),
                new Vector2(tip.X, tip.Y),
                woodMid,
                4.0f,
                true
            );
            DrawCircle(new Vector2(tip.X, tip.Y), 2.6f, woodDark);
        }
        DrawLine(
            new Vector2(-18.0f, braceY),
            new Vector2(18.0f, braceY - 2.0f),
            woodDark,
            3.0f,
            true
        );
    }

    private void DrawObjectiveMarker()
    {
        var poleDark = new Color(0.22f, 0.12f, 0.08f, 0.98f);
        var poleLight = new Color(0.58f, 0.38f, 0.22f, 0.98f);
        var clothMain = new Color(0.9f, 0.72f, 0.3f, 0.96f);
        var clothShadow = new Color(0.7f, 0.44f, 0.16f, 0.96f);
        DrawLine(new Vector2(0.0f, 0.0f), new Vector2(0.0f, -34.0f), poleLight, 5.0f, true);
        DrawLine(new Vector2(0.0f, 0.0f), new Vector2(0.0f, -34.0f), poleDark, 1.2f, true);
        DrawPolygon(
            new[]
            {
                new Vector2(0.0f, -32.0f),
                new Vector2(18.0f, -28.0f + SignedOffset(3, 1.5f)),
                new Vector2(11.0f, -14.0f),
                new Vector2(0.0f, -18.0f),
            },
            new[] { clothMain, clothMain, clothShadow, clothShadow }
        );
        DrawCircle(new Vector2(0.0f, -36.0f), 4.0f, new Color(0.96f, 0.88f, 0.58f, 0.98f));
    }

    private void DrawTent()
    {
        var canvasMain = new Color(0.72f, 0.58f, 0.38f, 0.96f);
        var canvasShadow = new Color(0.46f, 0.32f, 0.18f, 0.98f);
        var trim = new Color(0.28f, 0.17f, 0.1f, 0.96f);
        float width = 15.0f + Ratio(5) * 5.0f;
        DrawPolygon(
            new[]
            {
                new Vector2(-width, 0.0f),
                new Vector2(0.0f, -22.0f),
                new Vector2(width, 0.0f),
            },
            new[] { canvasShadow, canvasMain, canvasShadow }
        );
        DrawLine(new Vector2(0.0f, -22.0f), new Vector2(0.0f, 0.0f), trim, 2.0f, true);
        DrawLine(
            new Vector2(-width - 2.0f, 0.0f),
            new Vector2(width + 2.0f, 0.0f),
            trim,
            3.0f,
            true
        );
    }

    private void DrawTorch()
    {
        var pole = new Color(0.34f, 0.18f, 0.1f, 0.98f);
        var ember = new Color(0.98f, 0.48f, 0.12f, 0.92f);
        var flame = new Color(1.0f, 0.78f, 0.32f, 0.84f);
        DrawLine(new Vector2(0.0f, 0.0f), new Vector2(0.0f, -22.0f), pole, 4.0f, true);
        DrawCircle(new Vector2(SignedOffset(7, 1.5f), -25.0f), 5.0f + Ratio(8) * 2.0f, ember);
        DrawCircle(new Vector2(SignedOffset(9, 1.0f), -28.0f), 3.6f + Ratio(10) * 1.5f, flame);
    }

    private float SignedOffset(int salt, float magnitude)
    {
        return StableHash(salt) % 2 == 0 ? magnitude : -magnitude;
    }

    private float Ratio(int salt)
    {
        return (StableHash(salt) % 1000) / 1000.0f;
    }

    private long StableHash(int salt)
    {
        long hashValue = (long)_optionSeed * 1103515245L + salt * 12345L;
        return Math.Abs(hashValue);
    }
}
