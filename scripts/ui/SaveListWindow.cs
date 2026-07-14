using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class SaveListWindow : SelectableListWindow
{
    [Signal]
    public delegate void save_load_requestedEventHandler(string save_id);

    [Signal]
    public delegate void closedEventHandler();

    protected override string _format_item_label(IReadOnlyDictionary<string, object> item)
    {
        return $"{DictString(item, "display_name", "")}  |  {DictString(item, "world_preset_name", "世界")}  |  {FormatUnixTime(DictInt(item, "updated_at_unix_time", 0))}";
    }

    protected override string _format_detail_text(IReadOnlyDictionary<string, object> item)
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

    protected override StringName _get_item_id(IReadOnlyDictionary<string, object> item)
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
        DateTime datetime = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
        return string.Format(
            "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}",
            datetime.Year,
            datetime.Month,
            datetime.Day,
            datetime.Hour,
            datetime.Minute,
            datetime.Second
        );
    }

    private static string FormatWorldSize(
        IReadOnlyDictionary<string, object> values,
        string key
    )
    {
        if (
            values == null
            || !values.TryGetValue(key, out object value)
            || value is not Vector2I worldSize
        )
            return "未知尺寸";
        return $"{worldSize.X} x {worldSize.Y}";
    }

    private static string DictString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string defaultValue
    )
    {
        if (values == null || !values.TryGetValue(key, out object value))
            return defaultValue;
        return value switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => defaultValue,
        };
    }

    private static int DictInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int defaultValue
    )
    {
        if (values == null || !values.TryGetValue(key, out object value))
            return defaultValue;
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue =>
                (int)longValue,
            _ => defaultValue,
        };
    }
}
