using Godot;

[GlobalClass]
public partial class TraitDef : Resource
{
    [Export]
    public StringName trait_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> categories { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> allowed_source_kinds { get; set; } = new();

    [Export]
    public StringName effect_type { get; set; } = "";

    [Export]
    public StringName trigger_type { get; set; } = "passive";

    [Export]
    public StringName stack_policy { get; set; } = "unique_by_trait";

    [Export]
    public StringName charge_scope { get; set; } = "none";

    [Export]
    public StringName charge_reset_timing { get; set; } = "none";

    [Export]
    public StringName highest_roll_compare_key { get; set; } = "";

    [Export(PropertyHint.Range, "0,999,1")]
    public int vision_range { get; set; }

    [Export(PropertyHint.Range, "0,99,1")]
    public int proficiency_choice_count { get; set; }

    [Export]
    public Godot.Collections.Array<AttributeModifier> attribute_modifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> save_advantage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<TraitDamageResistanceEntryDef> damage_resistance_entries { get; set; } = new();

    [Export]
    public Godot.Collections.Array<TraitSaveBonusEntryDef> save_bonus_entries { get; set; } = new();

    [Export]
    public Godot.Collections.Array<TraitPassiveStatusEffectDef> passive_status_effects { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<TraitRollValueSchemaEntry> roll_value_schema { get; set; } =
        new();

    internal TraitEffectKind EffectKind => TraitContentRules.ToEffectKind(effect_type);

    internal TraitTriggerKind TriggerKind =>
        TraitTriggerContentRules.ToTriggerKind(trigger_type);

    internal TraitStackPolicyKind StackPolicyKind =>
        TraitContentRules.ToStackPolicyKind(stack_policy);

    internal TraitChargeScopeKind ChargeScopeKind =>
        TraitContentRules.ToChargeScopeKind(charge_scope);

    internal TraitChargeResetTimingKind ChargeResetTimingKind =>
        TraitContentRules.ToChargeResetTimingKind(charge_reset_timing);

    internal StringName GetHighestRollCompareKey()
    {
        StringName configured = ProgressionDataUtils.to_string_name(highest_roll_compare_key);
        if (configured != "")
            return configured;

        foreach (TraitRollValueSchemaEntry entry in roll_value_schema)
        {
            if (entry != null && entry.ValueTypeKind == TraitRollValueType.Int && entry.key != "")
                return entry.key;
        }
        return "";
    }
}
