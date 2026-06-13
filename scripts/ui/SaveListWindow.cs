using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SaveListWindow : SelectableListWindow
{
    [Signal]
    public delegate void save_load_requestedEventHandler(string save_id);

    [Signal]
    public delegate void closedEventHandler();

    protected override string _format_item_label(GDictionary item)
    {
        return $"{DictString(item, "display_name", "")}  |  {DictString(item, "world_preset_name", "世界")}  |  {FormatUnixTime(DictInt(item, "updated_at_unix_time", 0))}";
    }

    protected override string _format_detail_text(GDictionary item)
    {
        string sizeLabel = FormatWorldSize(item, "world_size_cells");
        return string.Join(
            "\n",
            new[]
            {
                $"存档名：{DictString(item, "display_name", "")}",
                $"世界类型：{DictString(item, "world_preset_name", "世界")}",
                $"地图尺寸：{sizeLabel}",
                $"创建时间：{FormatUnixTime(DictInt(item, "created_at_unix_time", 0))}",
                $"最近保存：{FormatUnixTime(DictInt(item, "updated_at_unix_time", 0))}",
            }
        );
    }

    protected override string _format_empty_detail() => "当前没有可读取的存档。";

    protected override StringName _get_item_id(GDictionary item)
    {
        return new StringName(DictString(item, "save_id", ""));
    }

    protected override void _emit_confirmed_for_id(StringName item_id)
    {
        EmitSignal("save_load_requested", item_id.ToString());
    }

    protected override void _emit_cancelled()
    {
        EmitSignal("closed");
    }

    private static string FormatUnixTime(int unixTime)
    {
        if (unixTime <= 0)
            return "未知";
        GDictionary datetime = Time.GetDatetimeDictFromUnixTime(unixTime);
        return string.Format(
            "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}",
            DictInt(datetime, "year", 1970),
            DictInt(datetime, "month", 1),
            DictInt(datetime, "day", 1),
            DictInt(datetime, "hour", 0),
            DictInt(datetime, "minute", 0),
            DictInt(datetime, "second", 0)
        );
    }

    private static string FormatWorldSize(GDictionary dict, string key)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Vector2I)
            return "未知尺寸";
        Vector2I worldSize = value.AsVector2I();
        return $"{worldSize.X} x {worldSize.Y}";
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (!TryRead(dict, key, out Variant value))
            return defaultValue;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => defaultValue,
        };
    }

    private static int DictInt(GDictionary dict, string key, int defaultValue)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Int)
            return defaultValue;
        return value.AsInt32();
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        value = default;
        if (dict == null || !dict.ContainsKey(key))
            return false;
        value = dict[key];
        return value.VariantType != Variant.Type.Nil;
    }
}
