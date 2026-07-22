using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleMapPanel
{
    private void _apply_progress_bar_skin(ProgressBar progress_bar, Color fill_color)
    {
        progress_bar.ShowPercentage = false;
        progress_bar.AddThemeStyleboxOverride(
            "background",
            _build_button_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                4,
                1
            )
        );
        progress_bar.AddThemeStyleboxOverride(
            "fill",
            _build_button_style(fill_color.Darkened(0.05f), fill_color.Lightened(0.08f), 4, 0)
        );
    }

    private static void _style_header_label(Label label, int font_size, Color font_color)
    {
        label.AddThemeFontSizeOverride("font_size", font_size);
        label.AddThemeColorOverride("font_color", font_color);
    }

    private static void _style_stat_label(Label label)
    {
        label.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_BODY());
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
    }

    private static StyleBoxFlat _build_panel_style(
        Color background_color,
        Color border_color,
        int radius = 8,
        int border_width = 1,
        Color? shadow_color = null,
        int content_margin = 0
    )
    {
        Color shadowColor = shadow_color ?? new Color(0.0f, 0.0f, 0.0f, 0.0f);
        var style = new StyleBoxFlat
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
            ShadowColor = shadowColor,
            ShadowSize = shadowColor.A == 0.0f ? 0 : 8,
        };
        if (content_margin > 0)
        {
            style.ContentMarginLeft = content_margin;
            style.ContentMarginTop = content_margin;
            style.ContentMarginRight = content_margin;
            style.ContentMarginBottom = content_margin;
        }
        return style;
    }
}
