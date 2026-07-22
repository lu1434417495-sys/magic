using Godot;
using Godot.Collections;

[GlobalClass]
public partial class SubraceDef : Resource
{
    [Export]
    public StringName subrace_id = "";

    [Export]
    public StringName parent_race_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public StringName body_size_category_override = "";

    [Export]
    public int speed_bonus = 0;

    [Export]
    public Array<AttributeModifier> attribute_modifiers = new();

    [Export]
    public Array<StringName> trait_ids = new();

    [Export]
    public Array<RacialGrantedSkill> racial_granted_skills = new();

    [Export]
    public Array<StringName> proficiency_tags = new();

    [Export]
    public Array<StringName> vision_tags = new();

    [Export]
    public Array<StringName> save_advantage_tags = new();

    [Export]
    public Array<StringName> save_disadvantage_tags = new();

    [Export]
    public Array<StringName> save_immunity_tags = new();

    [Export]
    public Dictionary damage_resistances = new();

    [Export]
    public Array<StringName> dialogue_tags = new();

    [Export]
    public Array<string> racial_trait_summary = new();

    internal Array<AttributeModifier> AttributeModifiersBorrowed => attribute_modifiers;
    internal Array<StringName> TraitIdsBorrowed => trait_ids;
    internal Array<RacialGrantedSkill> RacialGrantedSkillsBorrowed => racial_granted_skills;
    internal Array<StringName> ProficiencyTagsBorrowed => proficiency_tags;
    internal Array<StringName> VisionTagsBorrowed => vision_tags;
    internal Array<StringName> SaveAdvantageTagsBorrowed => save_advantage_tags;
    internal Array<StringName> SaveDisadvantageTagsBorrowed => save_disadvantage_tags;
    internal Array<StringName> SaveImmunityTagsBorrowed => save_immunity_tags;
    internal Dictionary DamageResistancesBorrowed => damage_resistances;
    internal Array<StringName> DialogueTagsBorrowed => dialogue_tags;
    internal Array<string> RacialTraitSummaryBorrowed => racial_trait_summary;
}
