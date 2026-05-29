using Godot;

[GlobalClass]
public partial class BattleBoardPropCatalog : RefCounted
{
    private static readonly StringName PropSpikeBarricade = "spike_barricade";
    private static readonly StringName PropObjectiveMarker = "objective_marker";
    private static readonly StringName PropTent = "tent";
    private static readonly StringName PropTorch = "torch";

    public static StringName PROP_SPIKE_BARRICADE() => PropSpikeBarricade;

    public static StringName PROP_OBJECTIVE_MARKER() => PropObjectiveMarker;

    public static StringName PROP_TENT() => PropTent;

    public static StringName PROP_TORCH() => PropTorch;

    public bool IsSupported(StringName propId)
    {
        return propId == PropSpikeBarricade
            || propId == PropObjectiveMarker
            || propId == PropTent
            || propId == PropTorch;
    }

    public bool RequiresInteractionShape(StringName propId)
    {
        return propId == PropObjectiveMarker;
    }

    public int GetSortPriority(StringName propId)
    {
        if (propId == PropSpikeBarricade)
            return 0;
        if (propId == PropTorch)
            return 1;
        if (propId == PropTent)
            return 2;
        if (propId == PropObjectiveMarker)
            return 3;
        return 0;
    }

    public bool is_supported(StringName propId) => IsSupported(propId);

    public bool requires_interaction_shape(StringName propId) => RequiresInteractionShape(propId);

    public int get_sort_priority(StringName propId) => GetSortPriority(propId);
}
