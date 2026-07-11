using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleRuntimeModule — static Godot dictionary/array conversion helpers.
// Pure physical split: same class, no behavior change. See BattleRuntimeModule.cs.
public sealed partial class BattleRuntimeModule
{

    private static GDictionary GetDict(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return new GDictionary();
        if (dict.ContainsKey(key))
            return dict[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static GArray GetArray(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return new GArray();
        if (dict.ContainsKey(key))
            return dict[key].AsGodotArray();
        return new GArray();
    }

    private static string GetString(GDictionary dict, string key, string fallback = "")
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        string text = "";
        if (dict.ContainsKey(key))
            text = dict[key].ToString();
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    private static StringName GetStringName(
        GDictionary dict,
        string key,
        StringName fallback = default
    )
    {
        string text = GetString(dict, key);
        StringName parsed = !string.IsNullOrEmpty(text) ? new StringName(text) : "";
        return IsEmpty(parsed) ? fallback : parsed;
    }

    private static int GetInt(GDictionary dict, string key, int fallback = 0)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dict.ContainsKey(key))
            return dict[key].AsInt32();
        return fallback;
    }

    private static Vector2I GetVector2I(GDictionary dict, string key, Vector2I fallback)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dict.ContainsKey(key))
            return dict[key].AsVector2I();
        return fallback;
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

    private static GVector2IArray ToVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GVector2IArray();
        foreach (Vector2I value in values ?? Array.Empty<Vector2I>())
            result.Add(value);
        return result;
    }

    private static GBattleUnitArray ToBattleUnitArray(GArray values)
    {
        var result = new GBattleUnitArray();
        if (values == null)
            return result;
        foreach (var value in values)
        {
            if (BattleUnitState.TryReadUnitPayload(value, out BattleUnitState unitState)
                && unitState != null)
                result.Add(unitState);
        }
        return result;
    }

    private static GBattleUnitArray ToBattleUnitArray(IEnumerable<BattleUnitState> values)
    {
        var result = new GBattleUnitArray();
        if (values == null)
            return result;
        foreach (BattleUnitState unitState in values)
        {
            if (unitState != null)
                result.Add(unitState);
        }
        return result;
    }

    private static GStringNameArray ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new GStringNameArray();
        foreach (StringName value in values ?? Array.Empty<StringName>())
            result.Add(value);
        return result;
    }

    private static List<Vector2I> ToVector2IList(GArray values)
    {
        var result = new List<Vector2I>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value.AsVector2I());
        return result;
    }

    private static List<Vector2I> ToVector2IList(GVector2IArray values)
    {
        var result = new List<Vector2I>();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntypedArray(GStringNameArray values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

}
