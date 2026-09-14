using Godot;

// Presentation is confined to the panel rect. The owning modal retains all input and state.
public partial class ChronicleWindowDecoration : Control
{
    public Texture2D Artwork { get; set; }
    private TextureRect _vignette;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        if (Artwork != null)
        {
            _vignette = new TextureRect
            {
                Name = "HeaderVignette",
                Texture = Artwork,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                Material = GD.Load<ShaderMaterial>("res://scenes/ui/styles/chronicle_header_material.tres"),
            };
            AddChild(_vignette);
        }
        Resized += UpdateDecoration;
        UpdateDecoration();
    }

    private void UpdateDecoration()
    {
        if (_vignette != null)
        {
            float width = Mathf.Min(320, Size.X * 0.42f);
            _vignette.Position = new Vector2(Size.X - width - 10, 3);
            _vignette.Size = new Vector2(width, 96);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color gold = new(0.65f, 0.53f, 0.34f, 0.75f);
        Color muted = new(0.65f, 0.53f, 0.34f, 0.25f);
        foreach (Vector2 corner in new[] { new Vector2(8, 8), new Vector2(Size.X - 8, 8), new Vector2(8, Size.Y - 8), Size - new Vector2(8, 8) })
        {
            float dx = corner.X < Size.X / 2 ? 1 : -1;
            float dy = corner.Y < Size.Y / 2 ? 1 : -1;
            DrawLine(corner, corner + new Vector2(dx * 22, 0), gold, 1, true);
            DrawLine(corner, corner + new Vector2(0, dy * 22), gold, 1, true);
            Vector2 inset = corner + new Vector2(dx * 4, dy * 4);
            DrawLine(inset, inset + new Vector2(dx * 10, 0), muted, 1, true);
            DrawLine(inset, inset + new Vector2(0, dy * 10), muted, 1, true);
        }
        float mid = Size.X / 2;
        DrawLine(new Vector2(mid - 56, 8), new Vector2(mid - 7, 8), muted, 1, true);
        DrawLine(new Vector2(mid + 7, 8), new Vector2(mid + 56, 8), muted, 1, true);
        DrawPolyline(new[] { new Vector2(mid, 4), new Vector2(mid + 4, 8), new Vector2(mid, 12), new Vector2(mid - 4, 8), new Vector2(mid, 4) }, gold, 1, true);
    }

    public static ChronicleWindowDecoration Attach(PanelContainer panel, Texture2D artwork)
    {
        var decoration = new ChronicleWindowDecoration { Name = "WindowDecoration", Artwork = artwork, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(decoration);
        panel.MoveChild(decoration, 0);
        return decoration;
    }
}
