using System.Collections.Generic;
using Godot;

// OptionButton 的统一填充 / 按 id 选中工具：id 存进 item metadata，
// 取代各窗口手写的"平行 id 列表 + AddItem + 循环找下标"三件套。
public static class UiOptionButtonUtils
{
    public static void Populate(
        OptionButton button,
        IEnumerable<(StringName Id, string Label)> options,
        StringName selectedId
    )
    {
        if (button == null)
            return;
        button.Clear();
        if (options != null)
        {
            foreach ((StringName id, string label) in options)
            {
                button.AddItem(label ?? "");
                button.SetItemMetadata(button.ItemCount - 1, Variant.From(id ?? new StringName("")));
            }
        }
        SelectById(button, selectedId);
    }

    // 单个固定选项：填充 + 选中 + 不可交互语义交由调用方（Disabled）自行决定。
    public static void SetSingle(OptionButton button, string label, StringName id = default)
    {
        if (button == null)
            return;
        button.Clear();
        button.AddItem(label ?? "");
        button.SetItemMetadata(0, Variant.From(id ?? new StringName("")));
        button.Select(0);
    }

    public static bool SelectById(OptionButton button, StringName id)
    {
        int index = FindIndexById(button, id);
        if (index < 0)
            return false;
        button.Select(index);
        return true;
    }

    public static int FindIndexById(OptionButton button, StringName id)
    {
        if (button == null || id == null)
            return -1;
        for (int index = 0; index < button.ItemCount; index++)
        {
            if (GetIdAt(button, index) == id)
                return index;
        }
        return -1;
    }

    public static StringName GetIdAt(OptionButton button, int index)
    {
        if (button == null || index < 0 || index >= button.ItemCount)
            return "";
        Variant metadata = button.GetItemMetadata(index);
        return metadata.VariantType switch
        {
            Variant.Type.StringName => metadata.AsStringName(),
            Variant.Type.String => new StringName(metadata.AsString()),
            _ => new StringName(""),
        };
    }

    public static StringName GetSelectedId(OptionButton button)
    {
        return button != null ? GetIdAt(button, button.Selected) : new StringName("");
    }
}
