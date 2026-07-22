using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleMapPanel
{
    private const string SKILL_ICON_DIR = "res://assets/main/battle/skills/";
    private const string SKILL_ICON_FALLBACK_KEY = "warrior_whirlwind_slash";
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
        if (slots.Count == 0)
        {
            for (int index = 0; index < 20; index++)
                skill_grid.AddChild(
                    _create_skill_slot(new BattleHudSkillSlotSnapshot(index, true))
                );
            return;
        }
        foreach (BattleHudSkillSlotSnapshot slot in slots)
        {
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
        float available = skill_grid.Size.X;
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
            TooltipText = _build_skill_slot_tooltip(slot),
        };
        panel.AddThemeStyleboxOverride("panel", _build_skill_slot_style(slot));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 0);
        margin.AddChild(layout);

        var hotkeyRow = new HBoxContainer();
        layout.AddChild(hotkeyRow);

        var hotkeyLabel = new Label
        {
            Text = slot?.Hotkey ?? "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        hotkeyLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
        hotkeyLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_MUTED());
        hotkeyRow.AddChild(hotkeyLabel);

        int cdValue = slot?.Cooldown ?? 0;
        var cdLabel = new Label
        {
            Text = cdValue > 0 ? $"CD {cdValue}" : "",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        cdLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
        cdLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_ACCENT());
        hotkeyRow.AddChild(cdLabel);

        Control glyphNode = _create_skill_glyph_node(slot, isEmpty, isDisabled);
        layout.AddChild(glyphNode);

        if (!isEmpty)
        {
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
            panel.AddChild(glowBand);
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
            clickTarget.TooltipText = slot.DisplayName;
            clickTarget.skill_display_name = slot.DisplayName;
            clickTarget.skill_description = slot.Description;
            clickTarget.skill_footer_text = slot.FooterText;
            clickTarget.skill_disabled_reason = slot.DisabledReason;
            clickTarget.skill_cooldown = slot.Cooldown;
            clickTarget.skill_accent_color = slot.AccentColor;
        }
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

    private string _build_skill_slot_tooltip(BattleHudSkillSlotSnapshot slot)
    {
        if (slot == null || slot.IsEmpty)
            return "";
        var lines = new List<string> { slot.DisplayName };
        string disabledReason = slot.DisabledReason;
        if (!string.IsNullOrEmpty(disabledReason))
        {
            lines.Add($"不可用：{disabledReason}");
        }
        else
        {
            string footerText = slot.FooterText;
            if (!string.IsNullOrEmpty(footerText) && footerText != "READY")
                lines.Add($"信息：{footerText}");
        }
        return string.Join("\n", lines);
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
        Texture2D texture = _resolve_skill_icon(iconKey)
            ?? _resolve_skill_icon(SKILL_ICON_FALLBACK_KEY);
        if (texture != null)
        {
            var icon = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
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
        string path = $"{SKILL_ICON_DIR}{icon_key}.png";
        Texture2D texture = null;
        if (ResourceLoader.Exists(path, "Texture2D"))
            texture = EngineAssetAccess.ResolveBorrowed<Texture2D>(this, path);
        _skill_icon_cache[icon_key] = texture;
        return texture;
    }

    private ShaderMaterial _get_skill_icon_grayscale_material()
    {
        if (_skill_icon_grayscale_material?.Shader != null)
            return _skill_icon_grayscale_material;
        if (ResourceLoader.Exists(SKILL_ICON_GRAYSCALE_SHADER, "Shader")
            && EngineAssetAccess.ResolveBorrowed<Shader>(
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
