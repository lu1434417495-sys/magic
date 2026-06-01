using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleBarrierLayerState : RefCounted
{
    public StringName layer_id { get; set; } = "";
    public string display_name { get; set; } = "";
    public int order { get; set; }
    public bool broken { get; set; }
    public Godot.Collections.Array<StringName> blocked_categories { get; set; } = new();
    public Godot.Collections.Array<StringName> breaker_skill_ids { get; set; } = new();
    public GArray passage_outcomes { get; set; } = new();
    public bool has_save_roll_override { get; set; }
    public int save_roll_override { get; set; }

    public static BattleBarrierLayerState from_runtime_dict(GDictionary source)
    {
        var layer = new BattleBarrierLayerState();
        if (source == null || source.Count == 0)
        {
            return layer;
        }

        layer.layer_id = ReadStringName(source, "layer_id");
        layer.display_name = ReadString(source, "display_name");
        layer.order = ReadInt(source, "order");
        layer.broken = ReadBool(source, "broken");
        layer.blocked_categories = ReadStringNameArray(GetArray(source, "blocked_categories"));
        layer.breaker_skill_ids = ReadStringNameArray(GetArray(source, "breaker_skill_ids"));
        layer.passage_outcomes = GetArray(source, "passage_outcomes").Duplicate(true);
        if (source.ContainsKey("save_roll_override"))
        {
            layer.has_save_roll_override = true;
            layer.save_roll_override = ReadInt(source, "save_roll_override");
        }
        return layer;
    }

    public List<BattleBarrierOutcomeState> GetPassageOutcomesTyped()
    {
        var result = new List<BattleBarrierOutcomeState>();
        foreach (var outcomeValue in passage_outcomes ?? new GArray())
        {
            BattleBarrierOutcomeState outcome = BattleBarrierOutcomeState.from_runtime_dict(
                outcomeValue.AsGodotDictionary()
            );
            if (outcome != null && !outcome.IsEmpty)
            {
                result.Add(outcome);
            }
        }
        return result;
    }

    public void SetPassageOutcomesTyped(IReadOnlyList<BattleBarrierOutcomeState> outcomes)
    {
        passage_outcomes = new GArray();
        if (outcomes == null)
        {
            return;
        }
        foreach (BattleBarrierOutcomeState outcome in outcomes)
        {
            if (outcome != null && !outcome.IsEmpty)
            {
                passage_outcomes.Add(outcome.to_runtime_dict());
            }
        }
    }

    public GDictionary to_runtime_dict()
    {
        var result = new GDictionary
        {
            ["layer_id"] = layer_id.ToString(),
            ["display_name"] = display_name,
            ["order"] = order,
            ["broken"] = broken,
            ["blocked_categories"] = StringArray(blocked_categories),
            ["breaker_skill_ids"] = StringArray(breaker_skill_ids),
            ["passage_outcomes"] = passage_outcomes.Duplicate(true),
        };
        if (has_save_roll_override)
        {
            result["save_roll_override"] = save_roll_override;
        }
        return result;
    }

    private static GArray StringArray(Godot.Collections.Array<StringName> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            string text = value.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                result.Add(text);
            }
        }
        return result;
    }

    private static bool HasKey(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        return source.ContainsKey(key) || source.ContainsKey(new StringName(key));
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].ToString();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].ToString() : fallback;
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        string text = ReadString(source, key);
        return string.IsNullOrEmpty(text) ? new StringName("") : new StringName(text);
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsInt32();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsBool();
        }
        StringName stringNameKey = new(key);
        return source.ContainsKey(stringNameKey) ? source[stringNameKey].AsBool() : fallback;
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        if (!HasKey(source, key))
        {
            return new GArray();
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsGodotArray();
        }
        return source[new StringName(key)].AsGodotArray();
    }

    private static Godot.Collections.Array<StringName> ReadStringNameArray(GArray values)
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (var rawValue in values)
        {
            StringName value = ProgressionDataUtils.to_string_name(rawValue);
            if (value != "")
            {
                result.Add(value);
            }
        }
        return result;
    }
}
