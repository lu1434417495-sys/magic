using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗时间轴状态数据。
// 翻译自 battle_timeline_state.gd（2026-05-24，数据层 C# 迁移）。
[GlobalClass]
public partial class BattleTimelineState : RefCounted
{
    private const int _TU_GRANULARITY = 5;

    private static readonly string[] SchemaFields =
    {
        "current_tu",
        "tu_per_tick",
        "frozen",
        "ready_unit_ids",
    };

    public static int TU_GRANULARITY() => _TU_GRANULARITY;

    public int current_tu { get; set; }
    public int tu_per_tick { get; set; } = _TU_GRANULARITY;
    public bool frozen { get; set; }
    public Godot.Collections.Array<StringName> ready_unit_ids { get; set; } = new();

    public void clear()
    {
        current_tu = 0;
        tu_per_tick = _TU_GRANULARITY;
        frozen = false;
        ready_unit_ids.Clear();
    }

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["current_tu"] = current_tu,
            ["tu_per_tick"] = tu_per_tick,
            ["frozen"] = frozen,
            ["ready_unit_ids"] = StringNameArrayToStrings(ready_unit_ids),
        };
    }

    public static BattleTimelineState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        GDictionary payload = data.AsGodotDictionary();
        if (!HasExactSchemaFields(payload))
        {
            return null;
        }
        if (Get(payload, "current_tu").VariantType != Variant.Type.Int || Get(payload, "current_tu").AsInt32() < 0)
        {
            return null;
        }
        if (Get(payload, "tu_per_tick").VariantType != Variant.Type.Int || Get(payload, "tu_per_tick").AsInt32() <= 0)
        {
            return null;
        }
        if (Get(payload, "frozen").VariantType != Variant.Type.Bool)
        {
            return null;
        }
        if (Get(payload, "ready_unit_ids").VariantType != Variant.Type.Array)
        {
            return null;
        }

        Godot.Collections.Array<StringName> parsedReadyUnitIds = StringsToStringNameArray(Get(payload, "ready_unit_ids"));
        if (parsedReadyUnitIds == null)
        {
            return null;
        }

        return new BattleTimelineState
        {
            current_tu = Get(payload, "current_tu").AsInt32(),
            tu_per_tick = Get(payload, "tu_per_tick").AsInt32(),
            frozen = Get(payload, "frozen").AsBool(),
            ready_unit_ids = parsedReadyUnitIds,
        };
    }

    private static Godot.Collections.Array<string> StringNameArrayToStrings(Godot.Collections.Array<StringName> values)
    {
        var results = new Godot.Collections.Array<string>();
        foreach (StringName value in values ?? new Godot.Collections.Array<StringName>())
        {
            results.Add(value.ToString());
        }
        return results;
    }

    private static bool HasExactSchemaFields(GDictionary data)
    {
        if (data.Count != SchemaFields.Length)
        {
            return false;
        }
        foreach (string fieldName in SchemaFields)
        {
            if (!data.ContainsKey(fieldName))
            {
                return false;
            }
        }
        return true;
    }

    private static Godot.Collections.Array<StringName> StringsToStringNameArray(Variant values)
    {
        var results = new Godot.Collections.Array<StringName>();
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        foreach (Variant value in values.AsGodotArray())
        {
            if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            {
                return null;
            }
            string idText = value.AsString();
            if (string.IsNullOrEmpty(idText))
            {
                return null;
            }
            results.Add(new StringName(idText));
        }
        return results;
    }

    private static Variant Get(GDictionary payload, string key)
    {
        return payload.ContainsKey(key) ? payload[key] : default;
    }
}
