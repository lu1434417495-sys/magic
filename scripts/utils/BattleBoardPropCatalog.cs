using Godot;

internal enum BattleBoardPropKind
{
    Unknown = 0,
    SpikeBarricade,
    ObjectiveMarker,
    Tent,
    Torch,
}

public static class BattleBoardPropCatalog
{
    private static readonly StringName PropSpikeBarricade = "spike_barricade";
    private static readonly StringName PropObjectiveMarker = "objective_marker";
    private static readonly StringName PropTent = "tent";
    private static readonly StringName PropTorch = "torch";

    internal static StringName ToStringName(BattleBoardPropKind kind)
    {
        return kind switch
        {
            BattleBoardPropKind.SpikeBarricade => PropSpikeBarricade,
            BattleBoardPropKind.ObjectiveMarker => PropObjectiveMarker,
            BattleBoardPropKind.Tent => PropTent,
            BattleBoardPropKind.Torch => PropTorch,
            _ => new StringName(""),
        };
    }

    internal static BattleBoardPropKind ToPropKind(StringName propId)
    {
        if (propId == PropSpikeBarricade)
            return BattleBoardPropKind.SpikeBarricade;
        if (propId == PropObjectiveMarker)
            return BattleBoardPropKind.ObjectiveMarker;
        if (propId == PropTent)
            return BattleBoardPropKind.Tent;
        if (propId == PropTorch)
            return BattleBoardPropKind.Torch;
        return BattleBoardPropKind.Unknown;
    }

    public static bool IsSupported(StringName propId)
    {
        return ToPropKind(propId) != BattleBoardPropKind.Unknown;
    }

    public static bool RequiresInteractionShape(StringName propId)
    {
        return ToPropKind(propId) == BattleBoardPropKind.ObjectiveMarker;
    }

    public static int GetSortPriority(StringName propId)
    {
        return ToPropKind(propId) switch
        {
            BattleBoardPropKind.SpikeBarricade => 0,
            BattleBoardPropKind.Torch => 1,
            BattleBoardPropKind.Tent => 2,
            BattleBoardPropKind.ObjectiveMarker => 3,
            _ => 0,
        };
    }
}
