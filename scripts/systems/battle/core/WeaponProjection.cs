using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WeaponProjection : RefCounted
{
    public StringName weapon_profile_kind { get; set; } = "";
    public StringName weapon_item_id { get; set; } = "";
    public StringName weapon_profile_type_id { get; set; } = "";
    public StringName weapon_family { get; set; } = "";
    public StringName weapon_current_grip { get; set; } = "";
    public int weapon_attack_range { get; set; }
    public WeaponDice weapon_one_handed_dice { get; set; } = new();
    public WeaponDice weapon_two_handed_dice { get; set; } = new();
    public bool weapon_is_versatile { get; set; }
    public bool weapon_uses_two_hands { get; set; }
    public StringName weapon_physical_damage_tag { get; set; } = "";

    public WeaponProjection()
    {
    }

    public WeaponProjection duplicate_state()
    {
        return new WeaponProjection
        {
            weapon_profile_kind = weapon_profile_kind,
            weapon_item_id = weapon_item_id,
            weapon_profile_type_id = weapon_profile_type_id,
            weapon_family = weapon_family,
            weapon_current_grip = weapon_current_grip,
            weapon_attack_range = weapon_attack_range,
            weapon_one_handed_dice = weapon_one_handed_dice?.duplicate_state() ?? new WeaponDice(),
            weapon_two_handed_dice = weapon_two_handed_dice?.duplicate_state() ?? new WeaponDice(),
            weapon_is_versatile = weapon_is_versatile,
            weapon_uses_two_hands = weapon_uses_two_hands,
            weapon_physical_damage_tag = weapon_physical_damage_tag,
        };
    }

    public bool is_empty()
    {
        return weapon_profile_kind == "";
    }

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["weapon_profile_kind"] = weapon_profile_kind.ToString(),
            ["weapon_item_id"] = weapon_item_id.ToString(),
            ["weapon_profile_type_id"] = weapon_profile_type_id.ToString(),
            ["weapon_family"] = weapon_family.ToString(),
            ["weapon_current_grip"] = weapon_current_grip.ToString(),
            ["weapon_attack_range"] = weapon_attack_range,
            ["weapon_one_handed_dice"] = (weapon_one_handed_dice ?? new WeaponDice()).to_dict(),
            ["weapon_two_handed_dice"] = (weapon_two_handed_dice ?? new WeaponDice()).to_dict(),
            ["weapon_is_versatile"] = weapon_is_versatile,
            ["weapon_uses_two_hands"] = weapon_uses_two_hands,
            ["weapon_physical_damage_tag"] = weapon_physical_damage_tag.ToString(),
        };
    }
}
