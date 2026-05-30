using Godot;

public readonly struct AttackTraitTriggerResult
{
    public readonly bool Triggered;
    public readonly StringName Event;
    public readonly StringName TraitId;
    public readonly StringName EffectType;
    public readonly int OriginalRoll;
    public readonly bool RerollDie;
    public readonly int RerolledRoll;
    public readonly int DieSize;
    public readonly StringName ChargeKey;
    public readonly int ChargesRemaining;

    public AttackTraitTriggerResult(
        bool triggered = false,
        StringName @event = null,
        StringName traitId = null,
        StringName effectType = null,
        int originalRoll = 0,
        bool rerollDie = false,
        int rerolledRoll = 0,
        int dieSize = 0,
        StringName chargeKey = null,
        int chargesRemaining = 0
    )
    {
        Triggered = triggered;
        Event = @event ?? new StringName("");
        TraitId = traitId ?? new StringName("");
        EffectType = effectType ?? new StringName("");
        OriginalRoll = originalRoll;
        RerollDie = rerollDie;
        RerolledRoll = rerolledRoll;
        DieSize = dieSize;
        ChargeKey = chargeKey ?? new StringName("");
        ChargesRemaining = chargesRemaining;
    }
}
