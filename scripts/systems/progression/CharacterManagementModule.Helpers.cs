using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of CharacterManagementModule — static achievement-summary / comparison / projection helpers.
// Pure physical split: same class, no behavior change. See CharacterManagementModule.cs.
public sealed partial class CharacterManagementModule
{

    private static int CompareAchievementProgressEntry(
        AchievementProgressSummaryEntry a,
        AchievementProgressSummaryEntry b
    )
    {
        if (Mathf.IsEqualApprox(a.ProgressRatio, b.ProgressRatio))
        {
            if (a.CurrentValue == b.CurrentValue)
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            return b.CurrentValue.CompareTo(a.CurrentValue);
        }
        return b.ProgressRatio.CompareTo(a.ProgressRatio);
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        foreach (StringName value in values)
        {
            if (value == target)
                return true;
        }
        return false;
    }

    private static int GetIntParam(GDictionary dict, string key, int fallback = 0)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return (int)dict[key];
    }

    private static int GetIntParam(GDictionary dict, StringName key, int fallback = 0)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return (int)dict[key];
    }

    private static float GetFloatParam(GDictionary dict, string key, float fallback = 0.0f)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        return (float)dict[key];
    }

    private static bool DictionariesEqual(GDictionary left, GDictionary right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (var key in left.Keys)
        {
            if (!right.ContainsKey(key) || !left[key].Equals(right[key]))
                return false;
        }
        return true;
    }

    private static GArray ToUntyped(IEnumerable<StageAdvancementModifier> values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntyped(Godot.Collections.Array<GDictionary> values)
    {
        var result = new GArray();
        foreach (var value in values)
            result.Add(value);
        return result;
    }
}
