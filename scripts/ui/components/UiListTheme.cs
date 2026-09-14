using Godot;

// ItemList 的统一皮肤：悬停 / 选中 / 光标样式与存档列表（SelectableListWindow）一致。
// 从 SelectableListWindow 提出为公共工具，供商店 / 队伍 / 仓库 / 触发条件 / 战中背包
// 等所有弹窗列表复用，避免各窗口停留在 Godot 默认样式。
public static class UiListTheme
{
    public static void Apply(ItemList list)
    {
        if (list == null)
            return;

        list.Theme = GD.Load<Theme>("res://scenes/ui/styles/chronicle_theme.tres");

        var selectedStyle = list.Theme.GetStylebox("selected", "ItemList");
        list.AddThemeStyleboxOverride("selected", selectedStyle);
        list.AddThemeStyleboxOverride("selected_focus", selectedStyle);
        list.AddThemeStyleboxOverride("hovered_selected", selectedStyle);
        list.AddThemeStyleboxOverride("hovered_selected_focus", selectedStyle);

        var hoverStyle = list.Theme.GetStylebox("hovered", "ItemList");
        list.AddThemeStyleboxOverride("hovered", hoverStyle);

        var cursorStyle = list.Theme.GetStylebox("cursor", "ItemList");
        list.AddThemeStyleboxOverride("cursor", cursorStyle);
        list.AddThemeStyleboxOverride("cursor_unfocused", cursorStyle);

        list.AddThemeColorOverride("font_hovered_color", new Color(0.94f, 0.89f, 0.78f));
        list.AddThemeColorOverride("font_hovered_selected_color", new Color(0.86f, 0.73f, 0.49f));
    }

}
