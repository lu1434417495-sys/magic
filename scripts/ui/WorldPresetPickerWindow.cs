using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldPresetPickerWindow : SelectableListWindow
{
    [Signal]
    public delegate void preset_confirmedEventHandler(StringName preset_id);

    [Signal]
    public delegate void cancelledEventHandler();

    protected override string _format_item_label(GDictionary item)
    {
        return $"{DictString(item, "display_name", "未命名世界")}  |  {DictString(item, "size_label", "")}";
    }

    protected override string _format_detail_text(GDictionary item)
    {
        string presetName = DictString(item, "display_name", "世界");
        string sizeLabel = DictString(item, "size_label", "未知尺寸");
        return string.Join(
            "\n",
            new[]
            {
                $"世界类型：{presetName}",
                $"地图尺寸：{sizeLabel}",
                "会创建一个全新的唯一存档。",
            }
        );
    }

    protected override string _format_empty_detail() => "当前没有可用的世界预设。";

    protected override StringName _get_item_id(GDictionary item)
    {
        return new StringName(DictString(item, "preset_id", ""));
    }

    protected override void _emit_confirmed_for_id(StringName item_id)
    {
        EmitSignal("preset_confirmed", item_id);
    }

    protected override void _emit_cancelled()
    {
        EmitSignal("cancelled");
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsString();
    }
}
