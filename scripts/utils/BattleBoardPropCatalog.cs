using Godot;

[GlobalClass]
public partial class BattleBoardPropCatalog : RefCounted
{
    public static readonly StringName PROP_SPIKE_BARRICADE = "spike_barricade";
    public static readonly StringName PROP_OBJECTIVE_MARKER = "objective_marker";
    public static readonly StringName PROP_TENT = "tent";
    public static readonly StringName PROP_TORCH = "torch";

    public static bool is_supported(StringName propId)
    {
        return propId == PROP_SPIKE_BARRICADE
            || propId == PROP_OBJECTIVE_MARKER
            || propId == PROP_TENT
            || propId == PROP_TORCH;
    }

    public static bool requires_interaction_shape(StringName propId)
    {
        return propId == PROP_OBJECTIVE_MARKER;
    }

    public static int get_sort_priority(StringName propId)
    {
        if (propId == PROP_SPIKE_BARRICADE) return 0;
        if (propId == PROP_TORCH) return 1;
        if (propId == PROP_TENT) return 2;
        if (propId == PROP_OBJECTIVE_MARKER) return 3;
        return 0;
    }
}
