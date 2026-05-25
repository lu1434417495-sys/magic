using Godot;

[GlobalClass]
public partial class ProfessionActiveCondition : Resource
{
    [Export] public StringName condition_type = "attribute_range";
    [Export] public StringName attribute_id = "";
    [Export] public StringName state_id = "";
    [Export] public int min_value = 0;
    [Export] public int max_value = 0;

    public bool MatchesValue(int value)
    {
        if (value < min_value)
            return false;
        return max_value <= 0 || value <= max_value;
    }
}
