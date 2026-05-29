using Godot;

[GlobalClass]
public partial class BattleSaveContentRules : RefCounted
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

    private static readonly Godot.Collections.Dictionary VALID_SAVE_TAGS = new()
    {
        { SAVE_TAG_SLEEP, true },
        { SAVE_TAG_PARALYSIS, true },
        { SAVE_TAG_CHARM, true },
        { SAVE_TAG_POISON, true },
        { SAVE_TAG_DRAGON_BREATH, true },
        { SAVE_TAG_FIREBALL, true },
        { SAVE_TAG_CHAIN_LIGHTNING, true },
        { SAVE_TAG_EQUIPMENT_DISJUNCTION, true },
        { SAVE_TAG_MAGIC, true },
        { SAVE_TAG_ILLUSION, true },
        { SAVE_TAG_FRIGHTENED, true },
        { SAVE_TAG_EXECUTE, true },
        { SAVE_TAG_STRENGTH, true },
        { SAVE_TAG_AGILITY, true },
        { SAVE_TAG_CONSTITUTION, true },
        { SAVE_TAG_PERCEPTION, true },
        { SAVE_TAG_INTELLIGENCE, true },
        { SAVE_TAG_WILLPOWER, true },
    };

    private static readonly Godot.Collections.Dictionary VALID_SAVE_ABILITIES = new()
    {
        { UnitBaseAttributes.STRENGTH(), true },
        { UnitBaseAttributes.AGILITY(), true },
        { UnitBaseAttributes.CONSTITUTION(), true },
        { UnitBaseAttributes.PERCEPTION(), true },
        { UnitBaseAttributes.INTELLIGENCE(), true },
        { UnitBaseAttributes.WILLPOWER(), true },
    };

    private static readonly Godot.Collections.Dictionary CONTROL_SAVE_TAGS = new()
    {
        { SAVE_TAG_SLEEP, true },
        { SAVE_TAG_PARALYSIS, true },
        { SAVE_TAG_CHARM, true },
        { SAVE_TAG_ILLUSION, true },
        { SAVE_TAG_FRIGHTENED, true },
    };

    private static readonly Godot.Collections.Dictionary VALID_SAVE_DC_MODES = new()
    {
        { SAVE_DC_MODE_STATIC, true },
        { SAVE_DC_MODE_CASTER_SPELL, true },
    };
}
