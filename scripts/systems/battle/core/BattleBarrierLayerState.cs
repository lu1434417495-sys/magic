using Godot;
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

    public GDictionary to_runtime_dict()
    {
        return new GDictionary
        {
            ["layer_id"] = layer_id.ToString(),
            ["display_name"] = display_name,
            ["order"] = order,
            ["broken"] = broken,
            ["blocked_categories"] = StringArray(blocked_categories),
            ["breaker_skill_ids"] = StringArray(breaker_skill_ids),
            ["passage_outcomes"] = passage_outcomes.Duplicate(true),
        };
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
}
