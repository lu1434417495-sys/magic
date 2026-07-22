using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleMapPanel
{
    private const int AP_DOT_MAX_PIPS = 8;
    private static readonly Vector2 AP_DOT_SIZE = new(10, 10);

    private void _create_command_dock()
    {
        if (mode_chip?.GetParent() is HBoxContainer rightCell)
        {
            resolve_button = _create_dock_button("ResolveBattleButton", "结算 ⏎");
            resolve_button.Pressed += _on_resolve_pressed;
            rightCell.AddChild(resolve_button);
            rightCell.MoveChild(resolve_button, 0);
        }

        if (skill_grid?.GetParent() is not VBoxContainer skillLayout)
            return;

        var dockRow = new HBoxContainer { Name = "CommandDock" };
        dockRow.AddThemeConstantOverride("separation", 8);

        clear_skill_button = _create_dock_button("ClearSkillButton", "取消 Esc");
        clear_skill_button.Pressed += _on_clear_skill_pressed;
        prev_variant_button = _create_dock_button("PrevVariantButton", "◀ Q");
        prev_variant_button.Pressed += _on_prev_variant_pressed;
        variant_name_label = new Label
        {
            Name = "VariantNameLabel",
            VerticalAlignment = VerticalAlignment.Center,
        };
        next_variant_button = _create_dock_button("NextVariantButton", "E ▶");
        next_variant_button.Pressed += _on_next_variant_pressed;
        command_summary_label = new Label
        {
            Name = "CommandSummaryLabel",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        dockRow.AddChild(clear_skill_button);
        dockRow.AddChild(prev_variant_button);
        dockRow.AddChild(variant_name_label);
        dockRow.AddChild(next_variant_button);
        dockRow.AddChild(command_summary_label);
        skillLayout.AddChild(dockRow);
        skillLayout.MoveChild(dockRow, 1);

        hint_label = new Label
        {
            Name = "HintLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint_label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        barrier_status_label = new Label
        {
            Name = "BarrierStatusLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
        };
        barrier_status_label.AddThemeFontSizeOverride("font_size", 11);
        barrier_status_label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_ACCENT());
        log_label = new Label
        {
            Name = "LogLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        log_label.AddThemeFontSizeOverride("font_size", 11);
        log_label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        skillLayout.AddChild(barrier_status_label);
        skillLayout.AddChild(hint_label);
        skillLayout.AddChild(log_label);
    }

    // 视口控制（C5）：缩放指示 chip + 重置视角按钮，挂在 TopBar 右格。摄像机操作是
    // 纯本地视口行为，不走命令通道，也不受"技能选择拉慢"约束。

    private void _create_viewport_controls()
    {
        if (mode_chip?.GetParent() is not HBoxContainer rightCell)
            return;
        _zoom_chip = new PanelContainer { Name = "ZoomChip" };
        _zoom_value_label = new Label
        {
            Name = "ZoomValueLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            TooltipText = "滚轮缩放 · 中键拖拽平移",
        };
        _zoom_chip.AddChild(_zoom_value_label);
        _zoom_chip.MouseFilter = MouseFilterEnum.Stop;
        _zoom_chip.TooltipText = "滚轮缩放 · 中键拖拽平移";
        _reset_view_button = new Button
        {
            Name = "ResetViewButton",
            Text = "重置视角",
            FocusMode = FocusModeEnum.None,
            TooltipText = "恢复默认缩放并回到当前行动单位",
        };
        _reset_view_button.Pressed += _on_reset_view_pressed;
        rightCell.AddChild(_zoom_chip);
        rightCell.AddChild(_reset_view_button);
        rightCell.MoveChild(_zoom_chip, 0);
        rightCell.MoveChild(_reset_view_button, 1);
    }

    private void _on_reset_view_pressed()
    {
        if (_battle_board == null)
            return;
        _battle_board.ResetViewportCamera();
        _request_map_viewport_update();
    }

    private void _update_zoom_chip()
    {
        if (_zoom_value_label == null)
            return;
        _zoom_value_label.Text = _battle_board != null
            ? $"×{_battle_board.GetCameraZoom():0.0}"
            : "×-.-";
    }

    private static Button _create_dock_button(string name, string text)
    {
        return new Button
        {
            Name = name,
            Text = text,
            FocusMode = FocusModeEnum.None,
            Disabled = true,
        };
    }

    private void _on_resolve_pressed() => EmitSignal(SignalName.battle_resolve_pressed);

    private void _on_clear_skill_pressed() => EmitSignal(SignalName.battle_clear_skill_pressed);

    private void _on_prev_variant_pressed() =>
        EmitSignal(SignalName.battle_cycle_variant_pressed, -1);

    private void _on_next_variant_pressed() =>
        EmitSignal(SignalName.battle_cycle_variant_pressed, 1);

    private void _apply_command_dock(BattleHudSnapshot snapshot)
    {
        BattleHudCommandDockSnapshot dock = snapshot?.CommandDock
            ?? BattleHudCommandDockSnapshot.Empty;
        if (resolve_button != null)
        {
            resolve_button.Disabled = !dock.ResolveEnabled;
            // A1 residual: light up the resolve button when a multi-target cast has
            // reached its minimum and is ready to confirm.
            _set_resolve_highlight(snapshot?.SelectedSkillConfirmReady == true);
        }
        if (clear_skill_button != null)
            clear_skill_button.Disabled = !dock.ClearSkillEnabled;
        if (prev_variant_button != null)
            prev_variant_button.Disabled = !dock.PrevVariantEnabled;
        if (next_variant_button != null)
            next_variant_button.Disabled = !dock.NextVariantEnabled;
        if (variant_name_label != null)
            variant_name_label.Text = snapshot?.SelectedSkillVariantName ?? "";
        if (command_summary_label != null)
            command_summary_label.Text = _build_command_summary(snapshot);
        if (hint_label != null)
            hint_label.Text = snapshot?.HintText ?? "";
        if (barrier_status_label != null)
        {
            barrier_status_label.Text = snapshot?.BarrierSummaryText ?? "";
            barrier_status_label.Visible = !string.IsNullOrEmpty(barrier_status_label.Text);
        }
        if (log_label != null)
            log_label.Text = _join_recent_log_lines(snapshot);
    }

    private void _set_resolve_highlight(bool highlighted)
    {
        if (resolve_button == null)
            return;
        if (highlighted)
            resolve_button.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_ACCENT());
        else
            resolve_button.RemoveThemeColorOverride("font_color");
    }

    private static string _build_command_summary(BattleHudSnapshot snapshot)
    {
        if (snapshot?.SelectedSkillTargetSelectionMode != "multi_unit")
            return "";
        int count = snapshot.SelectedSkillTargetCount;
        int maxCount = Mathf.Max(snapshot.SelectedSkillTargetMaxCount, 1);
        return $"已选 {count}/{maxCount} 目标";
    }

    private static string _join_recent_log_lines(BattleHudSnapshot snapshot)
    {
        var parts = new List<string>();
        foreach (string line in snapshot?.RecentBattleLogLines ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(line))
                parts.Add(line);
        }
        return string.Join("\n", parts);
    }

    private void _refresh_focus_unit_card(BattleHudFocusUnitSnapshot focusUnit)
    {
        Color edgeColor = focusUnit?.EdgeColor ?? BattleUiTheme.PANEL_EDGE();
        Color primaryColor = focusUnit?.PrimaryColor ?? BattleUiTheme.PANEL_BG_ALT();
        portrait_frame.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                primaryColor.Darkened(0.18f),
                edgeColor,
                BattleUiTheme.PANEL_RADIUS_SMALL(),
                BattleUiTheme.PANEL_BORDER()
            )
        );
        portrait_glyph_label.Text = focusUnit?.Glyph ?? "?";
        unit_name_label.Text = focusUnit?.Name ?? "待命";
        unit_role_label.Text = focusUnit?.RoleText ?? "未选中单位";
        BattleHudResourceInfoSnapshot resourceInfo = focusUnit?.ResourceInfo;

        _set_progress_bar_values(
            hp_bar,
            hp_value_label,
            focusUnit?.HpCurrent ?? 0,
            focusUnit?.HpMax ?? 1,
            "HP",
            BattleUiTheme.RESOURCE_HP()
        );
        _set_progress_bar_values(
            stamina_bar,
            stamina_value_label,
            focusUnit?.StaminaCurrent ?? resourceInfo?.Stamina?.Current ?? 0,
            focusUnit?.StaminaMax ?? resourceInfo?.Stamina?.Max ?? 1,
            "体力",
            BattleUiTheme.RESOURCE_STAMINA()
        );
        _set_progress_bar_values(
            mp_bar,
            mp_value_label,
            focusUnit?.MpCurrent ?? 0,
            focusUnit?.MpMax ?? 1,
            "MP",
            BattleUiTheme.RESOURCE_MP()
        );
        _set_progress_bar_values(
            aura_bar,
            aura_value_label,
            focusUnit?.AuraCurrent ?? resourceInfo?.Aura?.Current ?? 0,
            focusUnit?.AuraMax ?? resourceInfo?.Aura?.Max ?? 1,
            "斗气",
            BattleUiTheme.RESOURCE_AURA()
        );
        _rebuild_ap_dots(
            focusUnit?.MoveCurrent ?? 0,
            focusUnit?.MoveMax ?? 2
        );
        _rebuild_status_badges(focusUnit?.StatusEffects);
        _set_resource_row_visible(mp_bar, mp_value_label, resourceInfo?.Mp?.Visible ?? true);
        _set_resource_row_visible(
            aura_bar,
            aura_value_label,
            resourceInfo?.Aura?.Visible ?? true
        );
    }

    private void _apply_chip_skin(PanelContainer panel, Color edge)
    {
        if (panel == null)
            return;
        StyleBoxFlat chipStyle = _build_panel_style(
            BattleUiTheme.CHIP_BG(),
            edge,
            BattleUiTheme.PANEL_RADIUS_SMALL(),
            BattleUiTheme.PANEL_BORDER(),
            new Color(0, 0, 0, 0)
        );
        chipStyle.ContentMarginLeft = 10;
        chipStyle.ContentMarginRight = 10;
        chipStyle.ContentMarginTop = 4;
        chipStyle.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", chipStyle);
    }

    private static void _set_resource_row_visible(ProgressBar bar, Label label, bool is_visible)
    {
        if (bar != null)
            bar.Visible = is_visible;
        if (label != null)
            label.Visible = is_visible;
    }

    private static void _style_ap_value_label(Label label)
    {
        if (label == null)
            return;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
    }

    private void _style_ap_prefix_label()
    {
        var prefix = ap_dot_container.GetParent().GetNodeOrNull<Label>("ApPrefixLabel");
        if (prefix == null)
            return;
        prefix.AddThemeFontSizeOverride("font_size", 11);
        prefix.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
    }

    private void _rebuild_ap_dots(int current, int max_value)
    {
        if (ap_dot_container == null)
            return;
        int safeMax = Mathf.Max(max_value, 0);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        _clear_container(ap_dot_container);
        if (safeMax <= AP_DOT_MAX_PIPS)
        {
            for (int index = 0; index < safeMax; index++)
                ap_dot_container.AddChild(_create_ap_dot(index < safeCurrent));
            ap_value_label.Visible = false;
            ap_value_label.Text = "";
        }
        else
        {
            int visiblePips = Mathf.Min(safeCurrent, AP_DOT_MAX_PIPS);
            for (int index = 0; index < visiblePips; index++)
                ap_dot_container.AddChild(_create_ap_dot(true));
            ap_value_label.Visible = true;
            ap_value_label.Text = $"{safeCurrent}/{safeMax}";
        }
    }

    private Control _create_ap_dot(bool is_filled)
    {
        return new ColorRect
        {
            CustomMinimumSize = AP_DOT_SIZE,
            Color = is_filled ? BattleUiTheme.AP_DOT_FILL() : BattleUiTheme.AP_DOT_EMPTY(),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
    }

    private void _apply_button_skin(Button button, bool is_compact, bool is_primary = false)
    {
        int radius = is_compact
            ? BattleUiTheme.PANEL_RADIUS_SMALL()
            : BattleUiTheme.PANEL_RADIUS_MEDIUM();
        Color primaryEdge = BattleUiTheme.PANEL_EDGE_GLOW();
        Color normalBg = !is_primary ? BattleUiTheme.CHIP_BG() : BattleUiTheme.PANEL_BG_ALT();
        Color normalEdge = !is_primary ? BattleUiTheme.PANEL_EDGE_SOFT() : primaryEdge;
        button.AddThemeFontSizeOverride(
            "font_size",
            is_compact ? BattleUiTheme.FONT_BODY() : BattleUiTheme.FONT_HEADING()
        );
        button.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        button.AddThemeColorOverride("font_focus_color", BattleUiTheme.TEXT_PRIMARY());
        button.AddThemeStyleboxOverride(
            "normal",
            _build_button_style(normalBg, normalEdge, radius, BattleUiTheme.PANEL_BORDER())
        );
        button.AddThemeStyleboxOverride(
            "hover",
            _build_button_style(
                normalBg.Lightened(0.06f),
                normalEdge.Lightened(0.18f),
                radius,
                BattleUiTheme.PANEL_BORDER()
            )
        );
        button.AddThemeStyleboxOverride(
            "pressed",
            _build_button_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                normalEdge.Darkened(0.12f),
                radius,
                BattleUiTheme.PANEL_BORDER()
            )
        );
        button.AddThemeStyleboxOverride(
            "disabled",
            _build_button_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                radius,
                BattleUiTheme.PANEL_BORDER()
            )
        );
    }

    private static StyleBoxFlat _build_button_style(
        Color background_color,
        Color border_color,
        int radius = 14,
        int border_width = 2
    )
    {
        return new StyleBoxFlat
        {
            BgColor = background_color,
            BorderWidthLeft = border_width,
            BorderWidthTop = border_width,
            BorderWidthRight = border_width,
            BorderWidthBottom = border_width,
            BorderColor = border_color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
        };
    }
}
