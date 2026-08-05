using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class WeaponProjection
{
    public StringName weapon_profile_kind { get; set; } = "";
    public StringName weapon_item_id { get; set; } = "";
    public StringName weapon_instance_id { get; set; } = "";
    public StringName weapon_profile_type_id { get; set; } = "";
    public StringName weapon_range_type { get; set; } = "";
    public StringName weapon_family { get; set; } = "";
    public StringName weapon_current_grip { get; set; } = "";
    public int weapon_attack_range { get; set; }
    public WeaponDice weapon_one_handed_dice { get; set; } = new();
    public WeaponDice weapon_two_handed_dice { get; set; } = new();
    public bool weapon_is_versatile { get; set; }
    public bool weapon_uses_two_hands { get; set; }
    public bool weapon_is_heavy { get; set; }
    public StringName weapon_physical_damage_tag { get; set; } = "";

    public WeaponProjection() { }

    internal static WeaponProjection FromDictionary(GDictionary projection)
    {
        if (projection == null || projection.Count == 0)
        {
            return new WeaponProjection();
        }

        StringName currentGrip = ReadStringName(projection, "weapon_current_grip");
        bool hasWeaponUsesTwoHands = projection.ContainsKey("weapon_uses_two_hands");
        bool usesTwoHands = hasWeaponUsesTwoHands
            ? ReadBool(projection, "weapon_uses_two_hands")
            : currentGrip == new StringName("two_handed");
        WeaponDice oneHandedDice = WeaponDice.FromDictionary(
            ReadDictionary(projection, "weapon_one_handed_dice")
        );
        if (!usesTwoHands && hasWeaponUsesTwoHands && currentGrip == new StringName("two_handed"))
        {
            currentGrip = oneHandedDice != null && !oneHandedDice.IsEmpty()
                ? new StringName("one_handed")
                : new StringName("");
        }

        return new WeaponProjection
        {
            weapon_profile_kind = ReadStringName(projection, "weapon_profile_kind"),
            weapon_item_id = ReadStringName(projection, "weapon_item_id"),
            weapon_instance_id = ReadStringName(projection, "weapon_instance_id"),
            weapon_profile_type_id = ReadStringName(projection, "weapon_profile_type_id"),
            weapon_range_type = ReadStringName(projection, "weapon_range_type"),
            weapon_family = ReadStringName(projection, "weapon_family"),
            weapon_current_grip = currentGrip,
            weapon_attack_range = Mathf.Max(ReadInt(projection, "weapon_attack_range"), 0),
            weapon_one_handed_dice = oneHandedDice,
            weapon_two_handed_dice = WeaponDice.FromDictionary(
                ReadDictionary(projection, "weapon_two_handed_dice")
            ),
            weapon_is_versatile = ReadBool(projection, "weapon_is_versatile"),
            weapon_uses_two_hands = usesTwoHands,
            weapon_is_heavy = ReadBool(projection, "weapon_is_heavy"),
            weapon_physical_damage_tag = ReadStringName(
                projection,
                "weapon_physical_damage_tag"
            ),
        };
    }

    public WeaponProjection DuplicateState()
    {
        return new WeaponProjection
        {
            weapon_profile_kind = weapon_profile_kind,
            weapon_item_id = weapon_item_id,
            weapon_instance_id = weapon_instance_id,
            weapon_profile_type_id = weapon_profile_type_id,
            weapon_range_type = weapon_range_type,
            weapon_family = weapon_family,
            weapon_current_grip = weapon_current_grip,
            weapon_attack_range = weapon_attack_range,
            weapon_one_handed_dice = weapon_one_handed_dice?.DuplicateState() ?? new WeaponDice(),
            weapon_two_handed_dice = weapon_two_handed_dice?.DuplicateState() ?? new WeaponDice(),
            weapon_is_versatile = weapon_is_versatile,
            weapon_uses_two_hands = weapon_uses_two_hands,
            weapon_is_heavy = weapon_is_heavy,
            weapon_physical_damage_tag = weapon_physical_damage_tag,
        };
    }

    public bool IsEmpty()
    {
        return weapon_profile_kind == "";
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["weapon_profile_kind"] = weapon_profile_kind.ToString(),
            ["weapon_item_id"] = weapon_item_id.ToString(),
            ["weapon_instance_id"] = weapon_instance_id.ToString(),
            ["weapon_profile_type_id"] = weapon_profile_type_id.ToString(),
            ["weapon_range_type"] = weapon_range_type.ToString(),
            ["weapon_family"] = weapon_family.ToString(),
            ["weapon_current_grip"] = weapon_current_grip.ToString(),
            ["weapon_attack_range"] = weapon_attack_range,
            ["weapon_one_handed_dice"] = WeaponDiceProjection.Project(weapon_one_handed_dice),
            ["weapon_two_handed_dice"] = WeaponDiceProjection.Project(weapon_two_handed_dice),
            ["weapon_is_versatile"] = weapon_is_versatile,
            ["weapon_uses_two_hands"] = weapon_uses_two_hands,
            ["weapon_is_heavy"] = weapon_is_heavy,
            ["weapon_physical_damage_tag"] = weapon_physical_damage_tag.ToString(),
        };
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return new StringName("");
        }
        return new StringName(source[key].AsString());
    }

    private static int ReadInt(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;
    }

    private static bool ReadBool(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) && source[key].AsBool();
    }

    private static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }
}
