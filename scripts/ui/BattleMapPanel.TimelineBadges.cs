using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleMapPanel
{
    private void _rebuild_fate_badges(IReadOnlyList<BattleHudFateBadgeSnapshot> badges)
    {
        _clear_container(fate_badge_row);
        fate_badge_row.Visible = badges.Count > 0;
        if (badges.Count == 0)
            return;
        foreach (BattleHudFateBadgeSnapshot badge in badges)
        {
            fate_badge_row.AddChild(_create_fate_badge(badge));
        }
    }

    // 状态徽章行挂在 UnitCard 的 InfoColumn（姓名 / 角色行之下），随焦点单位快照
    // 重建。节点在代码里构建，与指令区同一约定：面板保持纯渲染层。

    private void _create_status_badge_row()
    {
        if (unit_role_label?.GetParent() is not VBoxContainer infoColumn)
            return;
        _status_badge_row = new HFlowContainer
        {
            Name = "StatusBadgeRow",
            Visible = false,
        };
        _status_badge_row.AddThemeConstantOverride("h_separation", 6);
        _status_badge_row.AddThemeConstantOverride("v_separation", 4);
        infoColumn.AddChild(_status_badge_row);
        infoColumn.MoveChild(_status_badge_row, unit_role_label.GetIndex() + 1);
    }

    private void _rebuild_status_badges(IReadOnlyList<BattleHudStatusEffectSnapshot> statuses)
    {
        if (_status_badge_row == null)
            return;
        _clear_container(_status_badge_row);
        int badgeCount = statuses?.Count ?? 0;
        _status_badge_row.Visible = badgeCount > 0;
        if (badgeCount == 0)
            return;
        foreach (BattleHudStatusEffectSnapshot status in statuses)
        {
            if (status == null)
                continue;
            _status_badge_row.AddChild(
                _create_fate_badge(
                    new BattleHudFateBadgeSnapshot(
                        FormatStatusBadgeText(status),
                        new StringName(status.IsDebuff ? "danger" : "calm"),
                        status.TooltipText
                    )
                )
            );
        }
    }

    internal static string FormatStatusBadgeText(BattleHudStatusEffectSnapshot status)
    {
        string text = string.IsNullOrEmpty(status.Label) ? status.StatusId : status.Label;
        if (status.Stacks > 1)
            text += $"×{status.Stacks}";
        if (status.RemainingTu >= 0)
            text += $" {status.RemainingTu}TU";
        return text;
    }

    private void _rebuild_timeline_row(IReadOnlyList<BattleHudQueueEntrySnapshot> entries)
    {
        _clear_container(timeline_row);
        timeline_row.Visible = entries.Count > 0;
        if (entries.Count == 0)
            return;
        foreach (BattleHudQueueEntrySnapshot entry in entries)
        {
            timeline_row.AddChild(
                entry.IsOverflow
                    ? _create_timeline_overflow(entry)
                    : _create_timeline_entry(entry)
            );
        }
    }

    private Control _create_timeline_entry(BattleHudQueueEntrySnapshot entry)
    {
        bool isActive = entry.IsActive;
        bool isReady = entry.IsReady;
        bool isEnemy = entry.IsEnemy;
        float hpRatio = Mathf.Clamp(entry.HpRatio, 0.0f, 1.0f);
        Color ringColor = isEnemy
            ? BattleUiTheme.TIMELINE_ENEMY_RING()
            : BattleUiTheme.TIMELINE_ALLY_RING();
        if (isActive)
            ringColor = BattleUiTheme.TIMELINE_ACTIVE_RING();

        var stack = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        stack.AddThemeConstantOverride("separation", 2);
        stack.TooltipText = BuildTimelineTooltip(
            string.IsNullOrEmpty(entry.Name) ? "?" : entry.Name,
            entry.HpText,
            entry.ApText
        );
        if (!isReady && !isActive)
            stack.Modulate = new Color(1.0f, 1.0f, 1.0f, BattleUiTheme.TIMELINE_INACTIVE_ALPHA());

        var portrait = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                BattleUiTheme.TIMELINE_ENTRY_SIZE(),
                BattleUiTheme.TIMELINE_ENTRY_SIZE()
            ),
        };
        portrait.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                ringColor,
                BattleUiTheme.PANEL_RADIUS_TINY(),
                isActive ? 2 : 1,
                new Color(0, 0, 0, 0)
            )
        );
        stack.AddChild(portrait);

        var glyphLabel = new Label
        {
            Text = string.IsNullOrEmpty(entry.Glyph) ? "?" : entry.Glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_LABEL());
        glyphLabel.AddThemeColorOverride(
            "font_color",
            isActive ? BattleUiTheme.TEXT_ACCENT() : BattleUiTheme.TEXT_PRIMARY()
        );
        portrait.AddChild(glyphLabel);

        var hpBand = new Control
        {
            CustomMinimumSize = new Vector2(
                BattleUiTheme.TIMELINE_ENTRY_SIZE(),
                BattleUiTheme.TIMELINE_HP_BAND_HEIGHT()
            ),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        stack.AddChild(hpBand);

        var hpBg = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0.0f,
            AnchorTop = 0.0f,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = BattleUiTheme.PANEL_BG_DEEP(),
        };
        hpBand.AddChild(hpBg);

        var hpFill = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0.0f,
            AnchorTop = 0.0f,
            AnchorRight = hpRatio,
            AnchorBottom = 1.0f,
            Color = !isEnemy ? BattleUiTheme.RESOURCE_HP() : BattleUiTheme.TIMELINE_ENEMY_RING(),
        };
        hpBand.AddChild(hpFill);
        return stack;
    }

    internal static string BuildTimelineTooltipForTest(string name, int hp, int ap) =>
        BuildTimelineTooltip(name, hp.ToString(), ap.ToString());

    private static string BuildTimelineTooltip(string name, string hpText, string apText) =>
        $"{name}\n{hpText}\n{apText}";

    private Control _create_timeline_overflow(BattleHudQueueEntrySnapshot entry)
    {
        var chip = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, BattleUiTheme.TIMELINE_ENTRY_SIZE()),
        };
        StyleBoxFlat chipStyle = _build_panel_style(
            BattleUiTheme.CHIP_BG(),
            BattleUiTheme.PANEL_EDGE_SOFT(),
            BattleUiTheme.PANEL_RADIUS_SMALL(),
            BattleUiTheme.PANEL_BORDER(),
            new Color(0, 0, 0, 0)
        );
        chipStyle.ContentMarginLeft = 6;
        chipStyle.ContentMarginRight = 6;
        chip.AddThemeStyleboxOverride("panel", chipStyle);

        var label = new Label
        {
            Text = string.IsNullOrEmpty(entry?.OverflowText) ? "+" : entry.OverflowText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        chip.AddChild(label);
        return chip;
    }

    private Control _create_fate_badge(BattleHudFateBadgeSnapshot badge)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = badge?.TooltipText ?? "",
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            _build_fate_badge_style(badge?.Tone ?? new StringName("gate"))
        );

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        panel.AddChild(margin);

        var label = new Label { Text = badge?.Text ?? "" };
        label.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_LABEL());
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        margin.AddChild(label);
        return panel;
    }

    private StyleBoxFlat _build_fate_badge_style(StringName tone)
    {
        Color accent = BattleUiTheme.FateColor(tone);
        Color tintedBg = BattleUiTheme.CHIP_BG().Lerp(accent, 0.18f);
        return _build_button_style(tintedBg, accent, 999, 1);
    }
}
