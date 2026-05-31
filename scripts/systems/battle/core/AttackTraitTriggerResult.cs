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
    public readonly int ExtraWeaponDiceCount;
    public readonly int ExtraWeaponDiceSides;
    public readonly int ClampToHp;
    public readonly int ProjectedHp;
    public readonly int HpDamage;

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
        int chargesRemaining = 0,
        int extraWeaponDiceCount = 0,
        int extraWeaponDiceSides = 0,
        int clampToHp = 0,
        int projectedHp = 0,
        int hpDamage = 0
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
        ExtraWeaponDiceCount = extraWeaponDiceCount;
        ExtraWeaponDiceSides = extraWeaponDiceSides;
        ClampToHp = clampToHp;
        ProjectedHp = projectedHp;
        HpDamage = hpDamage;
    }
}
