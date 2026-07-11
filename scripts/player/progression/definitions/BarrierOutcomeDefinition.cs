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
    internal BarrierOutcomeKind OutcomeKind => BarrierOutcomeDef.ToOutcomeKind(OutcomeType);

    public int ResolveSaveDc(int defaultSaveDc) =>
        SaveDc > 0 ? SaveDc : Math.Max(defaultSaveDc, 0);

    public int ResolveFatalDamage() => Math.Max(FatalDamage, 1);

    internal static BarrierOutcomeDefinition FromResource(
        BarrierOutcomeDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new BarrierOutcomeDefinition(
            source.outcome_type,
            source.amount,
            source.damage_tag,
            source.half_on_success,
            source.success_amount,
            source.success_damage_tag,
            source.fatal_damage,
            source.status_id,
            source.save_ability,
            source.save_tag,
            source.save_dc
        );
    }
}
