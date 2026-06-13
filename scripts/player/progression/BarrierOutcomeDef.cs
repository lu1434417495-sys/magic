using Godot;
using Godot.Collections;

internal enum BarrierOutcomeKind
{
    None = 0,
    Unknown,
    Damage,
    PoisonDeath,
    Status,
    Banish,
}

[GlobalClass]
public partial class BarrierOutcomeDef : Resource
{
    private static readonly StringName OutcomeDamage = "damage";
    private static readonly StringName OutcomePoisonDeath = "poison_death";
    private static readonly StringName OutcomeStatus = "status";
    private static readonly StringName OutcomeBanish = "banish";

    [Export]
    public StringName outcome_type = "";
    internal BarrierOutcomeKind OutcomeKind
    {
        get => ToOutcomeKind(outcome_type);
        set => outcome_type = ToStringName(value);
    }

    [Export]
    public int amount = 0;

    [Export]
    public StringName damage_tag = "";

    [Export]
    public bool half_on_success = false;

    [Export]
    public int success_amount = 0;

    [Export]
    public StringName success_damage_tag = "";

    [Export]
    public int fatal_damage = 99999;

    [Export]
    public StringName status_id = "";

    [Export]
    public StringName save_ability = "";

    [Export]
    public StringName save_tag = "";

    [Export]
    public int save_dc = 0;

    public Dictionary ToRuntimeDict(int defaultSaveDc = 0)
    {
        int resolvedSaveDc = save_dc;
        if (resolvedSaveDc <= 0)
            resolvedSaveDc = Mathf.Max(defaultSaveDc, 0);
        return new Dictionary()
        {
            { "outcome_type", (string)outcome_type },
            { "amount", amount },
            { "damage_tag", (string)damage_tag },
            { "half_on_success", half_on_success },
            { "success_amount", success_amount },
            { "success_damage_tag", (string)success_damage_tag },
            { "fatal_damage", Mathf.Max(fatal_damage, 1) },
            { "status_id", (string)status_id },
            { "save_ability", (string)save_ability },
            { "save_tag", (string)save_tag },
            { "save_dc", resolvedSaveDc },
        };
    }

    internal static BarrierOutcomeKind ToOutcomeKind(StringName value)
    {
        if (value == "")
            return BarrierOutcomeKind.None;
        if (value == OutcomeDamage)
            return BarrierOutcomeKind.Damage;
        if (value == OutcomePoisonDeath)
            return BarrierOutcomeKind.PoisonDeath;
        if (value == OutcomeStatus)
            return BarrierOutcomeKind.Status;
        if (value == OutcomeBanish)
            return BarrierOutcomeKind.Banish;
        return BarrierOutcomeKind.Unknown;
    }

    internal static StringName ToStringName(BarrierOutcomeKind value)
    {
        return value switch
        {
            BarrierOutcomeKind.Damage => OutcomeDamage,
            BarrierOutcomeKind.PoisonDeath => OutcomePoisonDeath,
            BarrierOutcomeKind.Status => OutcomeStatus,
            BarrierOutcomeKind.Banish => OutcomeBanish,
            _ => "",
        };
    }
}
