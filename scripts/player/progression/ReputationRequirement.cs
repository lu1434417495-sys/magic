using Godot;

[GlobalClass]
public partial class ReputationRequirement : Resource
{
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
