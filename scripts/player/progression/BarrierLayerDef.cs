using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BarrierLayerDef : Resource
{
    [Export]
    public StringName layer_id = "";

    [Export]
    public string display_name = "";

    [Export]
    public int order = 0;

    [Export]
    public Array<StringName> blocked_categories = new();

    internal Array<StringName> BlockedCategoriesProjectionBorrowed => blocked_categories;

    [Export]
    public Array<StringName> breaker_skill_ids = new();

    internal Array<StringName> BreakerSkillIdsProjectionBorrowed => breaker_skill_ids;

    [Export]
    public Array<BarrierOutcomeDef> passage_outcomes = new();

    internal Array<BarrierOutcomeDef> PassageOutcomesProjectionBorrowed => passage_outcomes;

    public Dictionary ToRuntimeDict(int defaultSaveDc = 0)
    {
        var outcomes = new Array();
        foreach (var outcome in passage_outcomes)
        {
            if (outcome == null)
                continue;
            outcomes.Add(outcome.ToRuntimeDict(defaultSaveDc));
        }
        return new Dictionary()
        {
            { "layer_id", (string)layer_id },
            { "display_name", display_name },
            { "order", order },
            { "broken", false },
            { "blocked_categories", _ToStringArray(blocked_categories) },
            { "breaker_skill_ids", _ToStringArray(breaker_skill_ids) },
            { "passage_outcomes", outcomes },
        };
    }

    private Array _ToStringArray(Array<StringName> values)
    {
        var result = new Array();
        foreach (var value in values)
        {
            var text = (string)value;
            if (!string.IsNullOrEmpty(text))
                result.Add(text);
        }
        return result;
    }
}
