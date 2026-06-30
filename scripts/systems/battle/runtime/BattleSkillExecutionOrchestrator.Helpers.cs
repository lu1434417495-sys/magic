using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleSkillExecutionOrchestrator — static coord/array/dictionary conversion + read helpers.
// Pure physical split: same class, no behavior change. See BattleSkillExecutionOrchestrator.cs.
internal sealed partial class BattleSkillExecutionOrchestrator
{

    private static List<Vector2I> SortCoordsTyped(IEnumerable<Vector2I> coords)
    {
        var result = new List<Vector2I>(coords ?? Array.Empty<Vector2I>());
        result.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return result;
    }

    private static GArray ToUntypedArray(IReadOnlyList<BattleUnitState> src)
    {
        var result = new GArray();
        if (src == null)
        {
            return result;
        }
        foreach (BattleUnitState unit in src)
        {
            if (unit != null)
            {
                result.Add(unit.ToDictionary());
            }
        }
        return result;
    }


    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> src)
        => new StringNameList(src).ToGodotArray();

    private static List<StringName> ToStringNameList(GStringNameArray src)
    {
        var result = new List<StringName>();
        if (src == null)
        {
            return result;
        }
        foreach (StringName id in src)
        {
            result.Add(id);
        }
        return result;
    }

    private static List<Vector2I> ToVector2IList(GVector2IArray src)
    {
        var result = new List<Vector2I>();
        if (src == null)
        {
            return result;
        }
        foreach (Vector2I coord in src)
        {
            result.Add(coord);
        }
        return result;
    }

    private static List<BattleUnitState> ToUnitList(GArray src)
    {
        var result = new List<BattleUnitState>();
        if (src == null)
        {
            return result;
        }
        foreach (var value in src)
        {
            if (BattleUnitState.TryReadUnitPayload(value, out BattleUnitState unit) && unit != null)
            {
                result.Add(unit);
            }
        }
        return result;
    }

    private BattleState RtState()
    {
        return Runtime?._state;
    }

    private static GArray DictArray(GDictionary source, object key)
    {
        if (!HasDictionaryKey(source, key))
        {
            return new GArray();
        }
        return ReadDictionaryArrayValue(source, key);
    }

    private static int DictInt(GDictionary source, object key, int fallback = 0)
    {
        if (!HasDictionaryKey(source, key))
        {
            return fallback;
        }
        return ReadDictionaryIntValue(source, key);
    }

    private static string DictString(GDictionary source, object key, string fallback = "")
    {
        if (!HasDictionaryKey(source, key))
        {
            return fallback;
        }
        StringName parsed = ReadDictionaryStringNameValue(source, key);
        return StringNameIsEmpty(parsed) ? fallback : parsed.ToString();
    }

    private static StringName DictStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!HasDictionaryKey(source, key))
        {
            return fallback;
        }
        StringName parsed = ReadDictionaryStringNameValue(source, key);
        return StringNameIsEmpty(parsed) ? fallback : parsed;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (var value in values)
        {
            yield return value.AsGodotDictionary();
        }
    }

    private static bool HasDictionaryKey(GDictionary dictionary, object key)
    {
        if (dictionary == null || key == null)
        {
            return false;
        }
        if (key is string stringKey)
        {
            return dictionary.ContainsKey(stringKey);
        }
        if (key is StringName stringNameKey)
        {
            return dictionary.ContainsKey(stringNameKey);
        }
        if (key is int intKey)
        {
            return dictionary.ContainsKey(intKey);
        }
        if (key is long longKey)
        {
            return dictionary.ContainsKey(longKey);
        }
        return false;
    }

    private static GArray ReadDictionaryArrayValue(GDictionary dictionary, object key)
    {
        if (key is string stringKey)
        {
            return dictionary[stringKey].AsGodotArray();
        }
        if (key is StringName stringNameKey)
        {
            return dictionary[stringNameKey].AsGodotArray();
        }
        if (key is int intKey)
        {
            return dictionary[intKey].AsGodotArray();
        }
        return dictionary[(long)key].AsGodotArray();
    }

    private static int ReadDictionaryIntValue(GDictionary dictionary, object key)
    {
        if (key is string stringKey)
        {
            return dictionary[stringKey].AsInt32();
        }
        if (key is StringName stringNameKey)
        {
            return dictionary[stringNameKey].AsInt32();
        }
        if (key is int intKey)
        {
            return dictionary[intKey].AsInt32();
        }
        return dictionary[(long)key].AsInt32();
    }

    private static StringName ReadDictionaryStringNameValue(GDictionary dictionary, object key)
    {
        if (key is string stringKey)
        {
            return ProgressionDataUtils.to_string_name(dictionary[stringKey]);
        }
        if (key is StringName stringNameKey)
        {
            return ProgressionDataUtils.to_string_name(dictionary[stringNameKey]);
        }
        if (key is int intKey)
        {
            return ProgressionDataUtils.to_string_name(dictionary[intKey]);
        }
        return ProgressionDataUtils.to_string_name(dictionary[(long)key]);
    }

    private static bool StringNameIsEmpty(StringName value)
    {
        return value == null || value.ToString().Length == 0;
    }
}
