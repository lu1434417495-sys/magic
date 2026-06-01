using Godot;
using System.Collections.Generic;

public static class BattleSaveContentRules
{
    public static readonly StringName SAVE_TAG_SLEEP = "sleep";

    public static readonly StringName SAVE_TAG_PARALYSIS = "paralysis";

    public static readonly StringName SAVE_TAG_CHARM = "charm";

    public static readonly StringName SAVE_TAG_POISON = "poison";

    public static readonly StringName SAVE_TAG_DRAGON_BREATH = "dragon_breath";

    public static readonly StringName SAVE_TAG_FIREBALL = "fireball";

    public static readonly StringName SAVE_TAG_CHAIN_LIGHTNING = "chain_lightning";

    public static readonly StringName SAVE_TAG_EQUIPMENT_DISJUNCTION = "equipment_disjunction";

    public static readonly StringName SAVE_TAG_MAGIC = "magic";

    public static readonly StringName SAVE_TAG_ILLUSION = "illusion";

    public static readonly StringName SAVE_TAG_FRIGHTENED = "frightened";

    public static readonly StringName SAVE_TAG_EXECUTE = "execute";

    public static readonly StringName SAVE_TAG_STRENGTH = UnitBaseAttributes.STRENGTH();

    public static readonly StringName SAVE_TAG_AGILITY = UnitBaseAttributes.AGILITY();

    public static readonly StringName SAVE_TAG_CONSTITUTION = UnitBaseAttributes.CONSTITUTION();

    public static readonly StringName SAVE_TAG_PERCEPTION = UnitBaseAttributes.PERCEPTION();

    public static readonly StringName SAVE_TAG_INTELLIGENCE = UnitBaseAttributes.INTELLIGENCE();

    public static readonly StringName SAVE_TAG_WILLPOWER = UnitBaseAttributes.WILLPOWER();

    public static readonly StringName ADVANTAGE_STATE_NORMAL = "normal";

    public static readonly StringName ADVANTAGE_STATE_ADVANTAGE = "advantage";

    public static readonly StringName ADVANTAGE_STATE_DISADVANTAGE = "disadvantage";

    public static readonly StringName SAVE_DC_MODE_STATIC = "static";

    public static readonly StringName SAVE_DC_MODE_CASTER_SPELL = "caster_spell";

    private static readonly HashSet<StringName> VALID_SAVE_TAGS = new()
    {
        SAVE_TAG_SLEEP,
        SAVE_TAG_PARALYSIS,
        SAVE_TAG_CHARM,
        SAVE_TAG_POISON,
        SAVE_TAG_DRAGON_BREATH,
        SAVE_TAG_FIREBALL,
        SAVE_TAG_CHAIN_LIGHTNING,
        SAVE_TAG_EQUIPMENT_DISJUNCTION,
        SAVE_TAG_MAGIC,
        SAVE_TAG_ILLUSION,
        SAVE_TAG_FRIGHTENED,
        SAVE_TAG_EXECUTE,
        SAVE_TAG_STRENGTH,
        SAVE_TAG_AGILITY,
        SAVE_TAG_CONSTITUTION,
        SAVE_TAG_PERCEPTION,
        SAVE_TAG_INTELLIGENCE,
        SAVE_TAG_WILLPOWER,
    };

    private static readonly HashSet<StringName> VALID_SAVE_ABILITIES = new()
    {
        UnitBaseAttributes.STRENGTH(),
        UnitBaseAttributes.AGILITY(),
        UnitBaseAttributes.CONSTITUTION(),
        UnitBaseAttributes.PERCEPTION(),
        UnitBaseAttributes.INTELLIGENCE(),
        UnitBaseAttributes.WILLPOWER(),
    };

    private static readonly HashSet<StringName> CONTROL_SAVE_TAGS = new()
    {
        SAVE_TAG_SLEEP,
        SAVE_TAG_PARALYSIS,
        SAVE_TAG_CHARM,
        SAVE_TAG_ILLUSION,
        SAVE_TAG_FRIGHTENED,
    };

    private static readonly HashSet<StringName> VALID_SAVE_DC_MODES = new()
    {
        SAVE_DC_MODE_STATIC,
        SAVE_DC_MODE_CASTER_SPELL,
    };

    public static bool is_valid_save_tag(StringName value) => VALID_SAVE_TAGS.Contains(value);

    public static bool is_valid_save_ability(StringName value) =>
        VALID_SAVE_ABILITIES.Contains(value);

    public static bool is_control_save_tag(StringName value) => CONTROL_SAVE_TAGS.Contains(value);

    public static bool is_valid_save_dc_mode(StringName value) =>
        VALID_SAVE_DC_MODES.Contains(value);
}
