using Godot;
using System.Collections.Generic;

public partial class CharacterCreationWindow
{
    private PanelContainer _creationPanel;

    private void _configure_responsive_phases()
    {
        _creationPanel = GetNode<PanelContainer>("CenterContainer/Panel");
        foreach (Control phase in new[] { name_phase, attribute_phase, race_phase, age_phase, identity_options_phase })
        {
            var children = new List<Node>();
            foreach (Node child in phase.GetChildren())
                children.Add(child);
            // The final button row remains visible even when the body scrolls.
            var scroll = new ScrollContainer
            {
                Name = "BodyScroll",
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            var body = new VBoxContainer { Name = "Body", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 12);
            phase.AddChild(scroll);
            phase.MoveChild(scroll, 0);
            scroll.AddChild(body);
            for (int i = 0; i < children.Count - 1; i++)
                children[i].Reparent(body, false);
            phase.SizeFlagsVertical = SizeFlags.ExpandFill;
            phase.VisibilityChanged += _update_creation_layout;
        }
        Resized += _update_creation_layout;
        _update_creation_layout();
    }

    private void _update_creation_layout()
    {
        if (_creationPanel == null || !IsInsideTree())
            return;
        var frame = GetNode<CenterContainer>("CenterContainer");
        bool compact = Size.X < 1500;
        frame.AnchorLeft = compact ? 0.40f : 0.47f;
        frame.AnchorRight = 0.95f;
        frame.AnchorTop = 0.04f;
        frame.AnchorBottom = 0.96f;
        if (shade?.Material is ShaderMaterial veil)
            veil.SetShaderParameter("ui_start", frame.AnchorLeft);
        Vector2 available = new(Size.X * (frame.AnchorRight - frame.AnchorLeft), Size.Y * 0.92f);
        Vector2 preferred = name_phase.Visible ? new Vector2(760, 440)
            : attribute_phase.Visible ? new Vector2(900, 760)
            : age_phase.Visible ? new Vector2(860, 540)
            : identity_options_phase.Visible ? new Vector2(900, 710)
            : new Vector2(940, 920);
        _creationPanel.CustomMinimumSize = new Vector2(
            Mathf.Min(preferred.X, Mathf.Max(available.X, 320)),
            Mathf.Min(preferred.Y, Mathf.Max(available.Y, 320))
        );
        _creationPanel.ResetSize();
    }
}
