using Godot;

internal enum BarrierAnchorMode
{
    Unknown = 0,
    Fixed,
}

internal enum BarrierOutcomeKind
{
    None = 0,
    Unknown,
    Damage,
    PoisonDeath,
    Status,
    Banish,
}

internal static class BarrierKinds
{
    internal static BarrierAnchorMode ToAnchorMode(StringName value) =>
        value == "fixed" ? BarrierAnchorMode.Fixed : BarrierAnchorMode.Unknown;

    internal static StringName ToStringName(BarrierAnchorMode value) =>
        value == BarrierAnchorMode.Fixed ? new StringName("fixed") : new StringName("");

    internal static BarrierOutcomeKind ToOutcomeKind(StringName value) =>
        value.ToString() switch
        {
            "" => BarrierOutcomeKind.None,
            "damage" => BarrierOutcomeKind.Damage,
            "poison_death" => BarrierOutcomeKind.PoisonDeath,
            "status" => BarrierOutcomeKind.Status,
            "banish" => BarrierOutcomeKind.Banish,
            _ => BarrierOutcomeKind.Unknown,
        };

    internal static StringName ToStringName(BarrierOutcomeKind value) =>
        new(
            value switch
            {
                BarrierOutcomeKind.Damage => "damage",
                BarrierOutcomeKind.PoisonDeath => "poison_death",
                BarrierOutcomeKind.Status => "status",
                BarrierOutcomeKind.Banish => "banish",
                _ => "",
            }
        );
}
