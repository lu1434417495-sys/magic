using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleMapPanel
{
    private const string SKILL_ICON_GRAYSCALE_SHADER =
        "res://assets/shaders/skill_icon_grayscale.gdshader";
    private static readonly Color SKILL_ICON_DISABLED_MODULATE = new(0.62f, 0.62f, 0.62f, 0.85f);

    private readonly List<TextureRect> _skill_icon_nodes = new();
    private string _last_skill_grid_signature = "";

    private void _rebuild_skill_grid(IReadOnlyList<BattleHudSkillSlotSnapshot> slots)
    {
        // Rebuilding 20 slot nodes (panels + margins + glyphs + labels + styleboxes)
        // on every battle snapshot apply is expensive. The slot set/state usually
        // doesn't change between ticks, so skip the teardown+recreate when the
        // render-affecting data is identical to the last build.
        string signature = _build_skill_grid_signature(slots);
        if (skill_grid.GetChildCount() > 0 && signature == _last_skill_grid_signature)
            return;
        _last_skill_grid_signature = signature;

        ClearSkillIconPresentationBindings();
        _clear_container(skill_grid);
        foreach (BattleHudSkillSlotSnapshot slot in slots)
        {
            // Keep command indices stable without presenting unused placeholders.
            if (!slot.IsEmpty)
                skill_grid.AddChild(_create_skill_slot(slot));
        }
        _update_skill_grid_columns();
    }

    private static string _build_skill_grid_signature(
        IReadOnlyList<BattleHudSkillSlotSnapshot> slots
    )
    {
        var builder = new System.Text.StringBuilder();
        foreach (BattleHudSkillSlotSnapshot slot in slots)
        {
            builder.Append(slot.Index).Append('|');
            if (slot.IsEmpty)
            {
                builder.Append("e;");
                continue;
            }
            builder
                .Append(slot.SkillEntryId).Append('|')
                .Append(slot.DisplayName).Append('|')
                .Append(slot.Description).Append('|')
                .Append(slot.DisabledReason).Append('|')
                .Append(slot.SkillLevel).Append('|')
                .Append(slot.Hotkey).Append('|')
                .Append(slot.IsBattleOnly).Append('|')
                .Append(slot.Tooltip).Append('|')
                .Append(slot.AccentColor).Append('|')
                .Append(slot.ShortName).Append('|')
                .Append(slot.FooterText).Append('|')
                .Append(slot.IconKey).Append('|')
                .Append(slot.IsSelected ? '1' : '0')
                .Append(slot.IsDisabled ? '1' : '0')
                .Append(slot.Cooldown)
                .Append(';');
        }
        return builder.ToString();
    }

    private void _update_skill_grid_columns()
    {
        if (skill_grid == null)
            return;
        int slotCount = skill_grid.GetChildCount();
        if (slotCount == 0)
            return;
        float available = (skill_grid.GetParent() as Control)?.Size.X ?? skill_grid.Size.X;
        if (available <= 0.0f)
            return;
        int hSeparation = skill_grid.GetThemeConstant("h_separation");
        float cellStride = BattleUiTheme.SKILL_SLOT_SIZE() + hSeparation;
        int columns = Mathf.FloorToInt((available + hSeparation) / cellStride);
        columns = Mathf.Clamp(columns, 1, slotCount);
        if (skill_grid.Columns != columns)
            skill_grid.Columns = columns;
    }

    private Control _create_skill_slot(BattleHudSkillSlotSnapshot slot)
    {
        bool isEmpty = slot?.IsEmpty != false;
        bool isDisabled = slot?.IsDisabled == true;

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                BattleUiTheme.SKILL_SLOT_SIZE(),
                BattleUiTheme.SKILL_SLOT_SIZE()
            ),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        panel.AddThemeStyleboxOverride("panel", _build_skill_slot_style(slot));

        // The artwork fills the slot inside its border. Hotkeys remain in the
        // tooltip; status indicators overlay the image instead of shrinking it.
        Control glyphNode = _create_skill_glyph_node(slot, isEmpty, isDisabled);
        panel.AddChild(glyphNode);

        if (!isEmpty)
        {
            // A PanelContainer stretches direct children; anchor the band inside
            // a plain Control so it cannot cover the glyph.
            var overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
            panel.AddChild(overlay);
            int cdValue = slot.Cooldown;
            if (cdValue > 0)
            {
                var cdLabel = new Label
                {
                    Name = "CooldownLabel",
                    Text = $"CD {cdValue}",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                overlay.AddChild(cdLabel);
                cdLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                cdLabel.OffsetLeft = 3;
                cdLabel.OffsetTop = 2;
                cdLabel.OffsetRight = -3;
                cdLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
                cdLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_ACCENT());
                cdLabel.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.025f, 0.04f, 0.95f));
                cdLabel.AddThemeConstantOverride("outline_size", 5);
            }
            var glowBand = new ColorRect
            {
                Name = "FateGlow",
                MouseFilter = MouseFilterEnum.Ignore,
                LayoutMode = 1,
                AnchorLeft = 0.0f,
                AnchorRight = 1.0f,
                AnchorTop = 1.0f,
                AnchorBottom = 1.0f,
                OffsetTop = -BattleUiTheme.SKILL_GLOW_BAND_HEIGHT(),
                OffsetLeft = 0.0f,
                OffsetRight = 0.0f,
                OffsetBottom = 0.0f,
            };
            Color accentColor = slot?.AccentColor ?? BattleUiTheme.FATE_GATE();
            if (isDisabled)
                accentColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.32f);
            glowBand.Color = accentColor;
            overlay.AddChild(glowBand);
        }

        var clickTarget = new BattleSkillSlotButton
        {
            Flat = true,
            FocusMode = FocusModeEnum.None,
            LayoutMode = 1,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            Disabled = isEmpty || isDisabled,
            Text = "",
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        if (!isEmpty)
        {
            clickTarget.SetSnapshot(slot);
        }
        // Keep the artwork visible under the button's hover and press feedback.
        clickTarget.AddThemeStyleboxOverride("hover", _build_panel_style(
            new Color(1, 1, 1, 0.12f), Colors.Transparent, 0, 0));
        clickTarget.AddThemeStyleboxOverride("pressed", _build_panel_style(
            new Color(0, 0, 0, 0.22f), Colors.Transparent, 0, 0));
        int slotIndex = slot?.Index ?? -1;
        clickTarget.Pressed += () => _on_skill_slot_pressed(slotIndex);
        panel.AddChild(clickTarget);
        return panel;
    }

    public void _on_skill_slot_pressed(int index)
    {
        if (index < 0)
            return;
        EmitSignal(SignalName.battle_skill_slot_selected, index);
    }

    private Control _create_skill_glyph_node(
        BattleHudSkillSlotSnapshot slot,
        bool is_empty,
        bool is_disabled
    )
    {
        if (is_empty)
        {
            return new Control
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        }

        string iconKey = slot?.IconKey ?? "";
        Texture2D texture = _resolve_skill_icon(iconKey);
        if (texture != null)
        {
            var icon = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            if (is_disabled)
            {
                icon.Modulate = SKILL_ICON_DISABLED_MODULATE;
                icon.Material = _get_skill_icon_grayscale_material();
            }
            _skill_icon_nodes.Add(icon);
            return icon;
        }

        var glyphLabel = new Label
        {
            Text = !string.IsNullOrEmpty(slot?.ShortName) ? slot.ShortName : "--",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_TITLE());
        glyphLabel.AddThemeColorOverride(
            "font_color",
            is_disabled ? BattleUiTheme.TEXT_MUTED() : BattleUiTheme.TEXT_PRIMARY()
        );
        return glyphLabel;
    }

    private void ClearSkillIconPresentationBindings()
    {
        foreach (TextureRect icon in _skill_icon_nodes)
        {
            if (!GodotObject.IsInstanceValid(icon))
                continue;
            icon.Material = null;
            icon.Texture = null;
        }
        _skill_icon_nodes.Clear();
    }

    private Texture2D _resolve_skill_icon(string icon_key)
    {
        if (string.IsNullOrEmpty(icon_key))
            return null;
        if (_skill_icon_cache.TryGetValue(icon_key, out Texture2D cachedTexture))
            return cachedTexture;
        Texture2D texture = EngineAssetAccess.ResolveContentAssetBorrowed<Texture2D>(
            this,
            icon_key
        );
        _skill_icon_cache[icon_key] = texture;
        return texture;
    }

    private ShaderMaterial _get_skill_icon_grayscale_material()
    {
        if (_skill_icon_grayscale_material?.Shader != null)
            return _skill_icon_grayscale_material;
        if (ResourceLoader.Exists(SKILL_ICON_GRAYSCALE_SHADER, "Shader")
            && EngineAssetAccess.ResolveCodeAssetBorrowed<Shader>(
                this,
                SKILL_ICON_GRAYSCALE_SHADER
            ) is Shader shader)
        {
            _skill_icon_grayscale_material = EnsurePresentationLease().Value;
            _skill_icon_grayscale_material.Shader = shader;
        }
        return _skill_icon_grayscale_material?.Shader != null
            ? _skill_icon_grayscale_material
            : null;
    }

    private GodotProjectionLease<ShaderMaterial> EnsurePresentationLease()
    {
        if (_presentationLease != null)
            return _presentationLease;
        var material = new ShaderMaterial();
        try
        {
            _presentationLease = GodotProjectionLease<ShaderMaterial>.CreateOwnedRoot(
                material,
                "battle-map-panel-presentation",
                LifetimeDomain.SceneTree,
                "BattleMapPanel.skill_icon_grayscale_material"
            );
            _skill_icon_grayscale_material = material;
            return _presentationLease;
        }
        catch
        {
            if (GodotObject.IsInstanceValid(material))
                material.Dispose();
            throw;
        }
    }

    internal ShaderMaterial ResolveSkillIconGrayscaleMaterialForTest() =>
        _get_skill_icon_grayscale_material();

    internal Texture2D ResolveSkillIconForTest(string iconKey) =>
        _resolve_skill_icon(iconKey);

    internal bool HasPresentationLeaseForTest() =>
        _presentationLease != null;

    private StyleBoxFlat _build_skill_slot_style(BattleHudSkillSlotSnapshot slot)
    {
        int radius = BattleUiTheme.PANEL_RADIUS_TINY();
        if (slot == null || slot.IsEmpty)
        {
            Color edge = BattleUiTheme.PANEL_EDGE_SOFT();
            Color emptyEdge = new(edge.R, edge.G, edge.B, 0.4f);
            return _build_panel_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                emptyEdge,
                radius,
                1,
                new Color(0, 0, 0, 0)
            );
        }
        if (slot.IsSelected)
            return _build_panel_style(
                BattleUiTheme.PANEL_BG_ALT(),
                BattleUiTheme.TEXT_ACCENT(),
                radius,
                2,
                new Color(0, 0, 0, 0)
            );
        if (slot.IsDisabled)
        {
            Color bg = BattleUiTheme.PANEL_BG_DEEP();
            Color dimBg = new(bg.R, bg.G, bg.B, 0.78f);
            return _build_panel_style(
                dimBg,
                BattleUiTheme.PANEL_EDGE_SOFT(),
                radius,
                1,
                new Color(0, 0, 0, 0)
            );
        }
        return _build_panel_style(
            BattleUiTheme.PANEL_BG_DEEP(),
            BattleUiTheme.PANEL_EDGE_GLOW(),
            radius,
            2,
            new Color(0, 0, 0, 0)
        );
    }
}
