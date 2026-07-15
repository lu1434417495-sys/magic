using Godot;

// ItemList 的统一皮肤：悬停 / 选中 / 光标样式与存档列表（SelectableListWindow）一致。
// 从 SelectableListWindow 提出为公共工具，供商店 / 队伍 / 仓库 / 触发条件 / 战中背包
// 等所有弹窗列表复用，避免各窗口停留在 Godot 默认样式。
public static class UiListTheme
{
    private static readonly Color ListItemHoverBg = new(0.40f, 0.50f, 0.66f, 0.18f);
    private static readonly Color ListItemSelectedBg = new(0.22f, 0.18f, 0.10f, 0.95f);
    private static readonly Color ListItemSelectedBorder = new(0.95f, 0.78f, 0.32f, 1.0f);
    private static readonly Color ListItemCursorBorder = new(0.40f, 0.50f, 0.66f, 0.55f);
    private static readonly Color ListFontNormal = new(0.85f, 0.92f, 1.0f, 0.92f);
    private static readonly Color ListFontHover = new(0.98f, 0.94f, 0.78f, 1.0f);
    private static readonly Color ListFontSelected = new(0.98f, 0.86f, 0.46f, 1.0f);

    private const int ListItemCornerRadius = 6;
    private const int ListItemPadX = 12;
    private const int ListItemPadY = 8;

    public static void Apply(ItemList list)
    {
        if (list == null)
            return;

        var transparentBg = new StyleBoxEmpty();
        list.AddThemeStyleboxOverride("panel", transparentBg);
        list.AddThemeStyleboxOverride("focus", transparentBg);

        var selectedStyle = MakeItemStyle(ListItemSelectedBg, ListItemSelectedBorder, 3);
        list.AddThemeStyleboxOverride("selected", selectedStyle);
        list.AddThemeStyleboxOverride("selected_focus", selectedStyle);
        list.AddThemeStyleboxOverride("hovered_selected", selectedStyle);
        list.AddThemeStyleboxOverride("hovered_selected_focus", selectedStyle);

        var hoverStyle = MakeItemStyle(ListItemHoverBg, new Color(0, 0, 0, 0), 0);
        list.AddThemeStyleboxOverride("hovered", hoverStyle);

        var cursorStyle = MakeItemStyle(new Color(0, 0, 0, 0), ListItemCursorBorder, 2);
        list.AddThemeStyleboxOverride("cursor", cursorStyle);
        list.AddThemeStyleboxOverride("cursor_unfocused", cursorStyle);

        list.AddThemeColorOverride("font_color", ListFontNormal);
        list.AddThemeColorOverride("font_hovered_color", ListFontHover);
        list.AddThemeColorOverride("font_selected_color", ListFontSelected);
        list.AddThemeColorOverride("font_hovered_selected_color", ListFontSelected);
        list.AddThemeConstantOverride("v_separation", 4);
    }

    private static StyleBoxFlat MakeItemStyle(Color bg, Color border, int borderWidthLeft)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = ListItemCornerRadius,
            CornerRadiusTopRight = ListItemCornerRadius,
            CornerRadiusBottomRight = ListItemCornerRadius,
            CornerRadiusBottomLeft = ListItemCornerRadius,
            ContentMarginLeft = ListItemPadX,
            ContentMarginRight = ListItemPadX - 2,
            ContentMarginTop = ListItemPadY,
            ContentMarginBottom = ListItemPadY,
        };
        if (borderWidthLeft > 0)
        {
            style.BorderColor = border;
            style.BorderWidthLeft = borderWidthLeft;
        }
        return style;
    }
}
