using Godot;

[GlobalClass]
public partial class AttributeRequirement : Resource
{
    [Export]
    public StringName attribute_id = "";

    [Export]
    public int min_value = 0;

    [Export]
    public int max_value = 0;

    public bool MatchesValue(int value) =>
        ProgressionDataUtils.MatchesValueRange(value, min_value, max_value);
}
