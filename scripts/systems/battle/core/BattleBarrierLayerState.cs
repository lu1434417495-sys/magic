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

        layer.layer_id = ProgressionDataUtils.to_string_name(GetValue(source, "layer_id", ""));
        layer.display_name = GetValue(source, "display_name", "").AsString();
        layer.order = GetValue(source, "order", 0).AsInt32();
        layer.broken = GetValue(source, "broken", false).AsBool();
        layer.blocked_categories = ReadStringNameArray(GetArray(source, "blocked_categories"));
        layer.breaker_skill_ids = ReadStringNameArray(GetArray(source, "breaker_skill_ids"));
        layer.passage_outcomes = GetArray(source, "passage_outcomes").Duplicate(true);
        if (layer.passage_outcomes.Count == 0 && source.ContainsKey("passage"))
        {
            layer.passage_outcomes.Add(GetValue(source, "passage", new GDictionary()));
        }
        if (source.ContainsKey("save_roll_override"))
        {
            layer.has_save_roll_override = true;
            layer.save_roll_override = GetValue(source, "save_roll_override", 0).AsInt32();
        }
        return layer;
    }

    public List<BattleBarrierOutcomeState> GetPassageOutcomesTyped()
    {
        var result = new List<BattleBarrierOutcomeState>();
        foreach (var outcomeValue in passage_outcomes ?? new GArray())
        {
            if (outcomeValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
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

    private static Variant GetValue(GDictionary source, object key, object fallback)
    {
        if (source == null)
        {
            return GdInterop.GetValueOrDefault(null, "", fallback);
        }
        return source.GetValueOrDefault(key, GdInterop.GetValueOrDefault(null, "", fallback));
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        var value = GetValue(source, key, new GArray());
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
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
