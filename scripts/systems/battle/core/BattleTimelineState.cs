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

    public BattleTimelineState duplicate_state()
    {
        return new BattleTimelineState
        {
            current_tu = current_tu,
            tu_per_tick = tu_per_tick,
            frozen = frozen,
            ready_unit_ids = new Godot.Collections.Array<StringName>(ready_unit_ids),
        };
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

    public static BattleTimelineState from_dict(GDictionary payload)
    {
        if (payload == null)
            return null;

        if (!HasExactSchemaFields(payload))
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "current_tu", out int currentTu) || currentTu < 0)
        {
            return null;
        }
        if (!TryGetStrictInt(payload, "tu_per_tick", out int tuPerTick) || tuPerTick <= 0)
        {
            return null;
        }
        if (!TryReadBoolField(payload, "frozen", out bool isFrozen))
        {
            return null;
        }
        if (!TryGetRawArray(payload, "ready_unit_ids", out Godot.Collections.Array readyUnitIds))
        {
            return null;
        }

        Godot.Collections.Array<StringName> parsedReadyUnitIds = StringsToStringNameArray(readyUnitIds);
        if (parsedReadyUnitIds == null)
        {
            return null;
        }

        return new BattleTimelineState
        {
            current_tu = currentTu,
            tu_per_tick = tuPerTick,
            frozen = isFrozen,
            ready_unit_ids = parsedReadyUnitIds,
        };
    }

    private static Godot.Collections.Array<string> StringNameArrayToStrings(
        Godot.Collections.Array<StringName> values
    )
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

    private static Godot.Collections.Array<StringName> StringsToStringNameArray(
        Godot.Collections.Array values
    )
    {
        var results = new Godot.Collections.Array<StringName>();
        foreach (var value in values)
        {
            string valueTypeName = value.VariantType.ToString();
            if (valueTypeName != "String" && valueTypeName != "StringName")
            {
                return null;
            }
            string idText = value.ToString();
            if (string.IsNullOrEmpty(idText))
            {
                return null;
            }
            results.Add(new StringName(idText));
        }
        return results;
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Int"))
        {
            value = data[key].AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryReadBoolField(GDictionary data, string key, out bool value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Bool"))
        {
            value = data[key].AsBool();
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetRawArray(
        GDictionary data,
        string key,
        out Godot.Collections.Array value
    )
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Array"))
        {
            value = data[key].AsGodotArray();
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool IsFieldType(GDictionary data, string key, string expectedTypeName)
    {
        return data[key].VariantType.ToString() == expectedTypeName;
    }

}
