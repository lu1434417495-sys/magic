using System;
using Godot;

public sealed record BarrierOutcomeDefinition(
    StringName OutcomeType,
    int Amount,
    StringName DamageTag,
    bool HalfOnSuccess,
    int SuccessAmount,
    StringName SuccessDamageTag,
    int FatalDamage,
    StringName StatusId,
    StringName SaveAbility,
    StringName SaveTag,
    int SaveDc
)
{
    internal BarrierOutcomeKind OutcomeKind => BarrierKinds.ToOutcomeKind(OutcomeType);

    public int ResolveSaveDc(int defaultSaveDc) =>
        SaveDc > 0 ? SaveDc : Math.Max(defaultSaveDc, 0);

    public int ResolveFatalDamage() => Math.Max(FatalDamage, 1);

}
