using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleSkillSlotButton : Button
{
    private const float TooltipMinWidth = 240.0f;
    private const float TooltipMaxWidth = 320.0f;
    private const int TooltipPadding = 14;
    private const int TooltipMouseGap = 18;
    private const int TooltipScreenMargin = 8;

    public string skill_display_name = "";
    public string skill_description = "";
    public string skill_footer_text = "";
    public string skill_disabled_reason = "";
    public int skill_cooldown;
    public Color skill_accent_color = BattleUiTheme.FATE_GATE();

    public override Control _MakeCustomTooltip(string _forText)
    {
        var root = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(TooltipMinWidth, 0),
        };
        root.AddThemeStyleboxOverride("panel", _build_tooltip_panel_style());

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 8);
        root.AddChild(layout);

        var accentStrip = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 3),
            Color = skill_accent_color,
        };
        layout.AddChild(accentStrip);

        var textPadding = new MarginContainer();
        textPadding.AddThemeConstantOverride("margin_left", TooltipPadding);
        textPadding.AddThemeConstantOverride("margin_right", TooltipPadding);
        textPadding.AddThemeConstantOverride("margin_top", 6);
        textPadding.AddThemeConstantOverride("margin_bottom", TooltipPadding);
        layout.AddChild(textPadding);

        var textColumn = new VBoxContainer();
        textColumn.AddThemeConstantOverride("separation", 6);
        textPadding.AddChild(textColumn);

        var titleLabel = MakeLabel(
            string.IsNullOrEmpty(skill_display_name) ? "未知技能" : skill_display_name,
            BattleUiTheme.TEXT_PRIMARY(),
            BattleUiTheme.FONT_TITLE()
        );
        textColumn.AddChild(titleLabel);

        if (!string.IsNullOrEmpty(skill_description))
        {
            var descriptionLabel = MakeLabel(
                skill_description,
                BattleUiTheme.TEXT_SECONDARY(),
                BattleUiTheme.FONT_BODY()
            );
            textColumn.AddChild(descriptionLabel);
        }

        var metaSegments = new List<string>();
        if (skill_cooldown > 0)
            metaSegments.Add($"冷却 {skill_cooldown}");
        if (!string.IsNullOrEmpty(skill_footer_text) && skill_footer_text != "READY")
            metaSegments.Add(skill_footer_text);
        if (metaSegments.Count > 0)
        {
            var metaLabel = MakeLabel(
                string.Join("  ·  ", metaSegments),
                BattleUiTheme.TEXT_MUTED(),
                BattleUiTheme.FONT_LABEL()
            );
            textColumn.AddChild(metaLabel);
        }

        if (!string.IsNullOrEmpty(skill_disabled_reason))
        {
            var disabledLabel = MakeLabel(
                $"不可用：{skill_disabled_reason}",
                BattleUiTheme.FATE_DANGER(),
                BattleUiTheme.FONT_LABEL()
            );
            textColumn.AddChild(disabledLabel);
        }

        root.CustomMinimumSize = new Vector2(TooltipMinWidth, 0);
        root.Size = new Vector2(TooltipMaxWidth, 0);

        root.Resized += () =>
        {
            if (!GodotObject.IsInstanceValid(root))
                return;
            if (root.GetViewport() is not Window windowNode)
                return;

            Vector2I mouseScreen = DisplayServer.MouseGetPosition();
            int tipW = Mathf.RoundToInt(root.Size.X);
            int tipH = Mathf.RoundToInt(root.Size.Y);
            Vector2I screenSize = DisplayServer.ScreenGetSize();
            int maxX = Mathf.Max(screenSize.X - tipW - TooltipScreenMargin, TooltipScreenMargin);
            int newX = Mathf.Clamp(mouseScreen.X - tipW / 2, TooltipScreenMargin, maxX);
            int newY = Mathf.Max(mouseScreen.Y - tipH - TooltipMouseGap, TooltipScreenMargin);
            windowNode.Position = new Vector2I(newX, newY);
        };

        return root;
    }

    private static Label MakeLabel(string text, Color color, int fontSize)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(TooltipMinWidth - TooltipPadding * 2, 0),
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat _build_tooltip_panel_style()
    {
        return new StyleBoxFlat
        {
            BgColor = BattleUiTheme.PANEL_BG_ALT(),
            BorderColor = BattleUiTheme.TEXT_ACCENT(),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = BattleUiTheme.PANEL_RADIUS_LARGE(),
            CornerRadiusTopRight = BattleUiTheme.PANEL_RADIUS_LARGE(),
            CornerRadiusBottomLeft = BattleUiTheme.PANEL_RADIUS_LARGE(),
            CornerRadiusBottomRight = BattleUiTheme.PANEL_RADIUS_LARGE(),
            ShadowColor = new Color(0, 0, 0, 0.7f),
            ShadowSize = 12,
        };
    }
}
