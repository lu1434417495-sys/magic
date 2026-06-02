using Godot;

[GlobalClass]
public partial class ItemDef : Resource
{
    private static readonly StringName ItemCategoryMisc = "misc";
    private static readonly StringName ItemCategoryEquipment = "equipment";
    private static readonly StringName ItemCategorySkillBook = "skill_book";
    private static readonly StringName EquipmentTypeWeapon = "weapon";
    private static readonly StringName EquipmentTypeArmor = "armor";
    private static readonly StringName EquipmentTypeAccessory = "accessory";
    private static readonly StringName DamageTagPhysicalSlash = "physical_slash";
    private static readonly StringName DamageTagPhysicalPierce = "physical_pierce";
    private static readonly StringName DamageTagPhysicalBlunt = "physical_blunt";
    private const int PriceBasisPointsDenominator = 10000;

    public static StringName ITEM_CATEGORY_MISC() => ItemCategoryMisc;

    public static StringName ITEM_CATEGORY_EQUIPMENT() => ItemCategoryEquipment;

    public static StringName ITEM_CATEGORY_SKILL_BOOK() => ItemCategorySkillBook;

    public static StringName EQUIPMENT_TYPE_WEAPON() => EquipmentTypeWeapon;

    public static StringName EQUIPMENT_TYPE_ARMOR() => EquipmentTypeArmor;

    public static StringName EQUIPMENT_TYPE_ACCESSORY() => EquipmentTypeAccessory;

    public static StringName DAMAGE_TAG_PHYSICAL_SLASH() => DamageTagPhysicalSlash;

    public static StringName DAMAGE_TAG_PHYSICAL_PIERCE() => DamageTagPhysicalPierce;

    public static StringName DAMAGE_TAG_PHYSICAL_BLUNT() => DamageTagPhysicalBlunt;

    public static Godot.Collections.Array<StringName> get_valid_item_categories()
    {
        return new Godot.Collections.Array<StringName>
        {
            ItemCategoryMisc,
            ItemCategoryEquipment,
            ItemCategorySkillBook,
        };
    }

    [Export]
    public StringName item_id = "";

    [Export]
    public StringName base_item_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public string icon = "";

    [Export]
    public bool is_stackable = true;

    [Export(PropertyHint.Range, "0,999999,1")]
    public int base_price;

    [Export(PropertyHint.Range, "0,999999,1")]
    public int buy_price;

    [Export(PropertyHint.Range, "0,999999,1")]
    public int sell_price;

    [Export]
    public bool sellable = true;

    [Export(PropertyHint.Range, "1,9999,1")]
    public int max_stack = 99;

    [Export]
    public StringName item_category = "";

    [Export]
    public Godot.Collections.Array<StringName> tags = new();

    [Export]
    public Godot.Collections.Array<StringName> crafting_groups = new();

    [Export]
    public Godot.Collections.Array<StringName> quest_groups = new();

    [Export]
    public Godot.Collections.Array<string> equipment_slot_ids = new();

    [Export]
    public Godot.Collections.Array<AttributeModifier> attribute_modifiers = new();

    [Export]
    public StringName granted_skill_id = "";

    [Export]
    public Godot.Collections.Array<string> occupied_slot_ids = new();

    [Export]
    public Resource equip_requirement;

    [Export]
    public StringName equipment_type_id = "";

    [Export]
    public Resource weapon_profile;

    [Export(PropertyHint.Range, "-1,20,1")]
    public int max_dex_bonus = -1;

    public int get_effective_max_stack()
    {
        return is_stackable ? Mathf.Max(max_stack, 1) : 1;
    }

    public int get_base_price()
    {
        return Mathf.Max(base_price, 0);
    }

    public int get_buy_price()
    {
        return get_buy_price(PriceBasisPointsDenominator);
    }

    public int get_buy_price(int price_basis_points)
    {
        return ApplyPriceBasisPoints(Mathf.Max(buy_price, 0), price_basis_points);
    }

    public int get_sell_price()
    {
        return get_sell_price(PriceBasisPointsDenominator);
    }

    public int get_sell_price(int price_basis_points)
    {
        if (!sellable)
            return 0;
        return ApplyPriceBasisPoints(Mathf.Max(sell_price, 0), price_basis_points);
    }

    public Godot.Collections.Array<StringName> get_tags() => NormalizeStringNameList(tags);

    public Godot.Collections.Array<StringName> get_crafting_groups() =>
        NormalizeStringNameList(crafting_groups);

    public Godot.Collections.Array<StringName> get_quest_groups() =>
        NormalizeStringNameList(quest_groups);

    public StringName get_item_category_normalized()
    {
        return item_category == "" ? ItemCategoryMisc : item_category;
    }

    public bool has_equipment_category()
    {
        return get_item_category_normalized() == ItemCategoryEquipment;
    }

    public Godot.Collections.Array<StringName> get_equipment_slot_ids()
    {
        return EquipmentRules.normalize_slot_ids(equipment_slot_ids);
    }

    public bool is_equipment()
    {
        return has_equipment_category() && get_equipment_slot_ids().Count > 0;
    }

    public StringName get_equipment_type_id_normalized()
    {
        var normalized = ProgressionDataUtils.to_string_name(equipment_type_id);
        return get_valid_equipment_type_ids().Contains(normalized) ? normalized : "";
    }

    public bool has_valid_equipment_type()
    {
        return get_equipment_type_id_normalized() != "";
    }

    public bool is_weapon()
    {
        return has_equipment_category()
            && get_equipment_type_id_normalized() == EquipmentTypeWeapon;
    }

    public int get_weapon_attack_range()
    {
        var profile = _get_weapon_profile_resource();
        if (!is_weapon() || profile == null)
            return 0;
        return Mathf.Max(profile.attack_range, 0);
    }

    public StringName get_weapon_physical_damage_tag()
    {
        var profile = _get_weapon_profile_resource();
        if (!is_weapon() || profile == null)
            return "";
        var normalized = ProgressionDataUtils.to_string_name(profile.damage_tag);
        return get_valid_weapon_physical_damage_tags().Contains(normalized) ? normalized : "";
    }

    public bool is_armor()
    {
        return has_equipment_category() && get_equipment_type_id_normalized() == EquipmentTypeArmor;
    }

    public int get_max_dex_bonus()
    {
        return Mathf.Max(max_dex_bonus, -1);
    }

    public bool is_accessory()
    {
        return has_equipment_category()
            && get_equipment_type_id_normalized() == EquipmentTypeAccessory;
    }

    public bool is_skill_book()
    {
        return get_item_category_normalized() == ItemCategorySkillBook && granted_skill_id != "";
    }

    public Godot.Collections.Array<AttributeModifier> get_attribute_modifiers()
    {
        var result = new Godot.Collections.Array<AttributeModifier>();
        foreach (var modifier in attribute_modifiers)
            result.Add(modifier);
        return result;
    }

    public static Godot.Collections.Array<StringName> get_valid_equipment_type_ids()
    {
        return new Godot.Collections.Array<StringName>
        {
            EquipmentTypeWeapon,
            EquipmentTypeArmor,
            EquipmentTypeAccessory,
        };
    }

    public static Godot.Collections.Array<StringName> get_valid_weapon_physical_damage_tags()
    {
        return new Godot.Collections.Array<StringName>
        {
            DamageTagPhysicalSlash,
            DamageTagPhysicalPierce,
            DamageTagPhysicalBlunt,
        };
    }

    public Godot.Collections.Array<StringName> get_final_occupied_slot_ids(StringName entry_slot_id)
    {
        if (occupied_slot_ids.Count > 0)
            return EquipmentRules.normalize_slot_ids(occupied_slot_ids);
        var normalized = ProgressionDataUtils.to_string_name(entry_slot_id);
        if (EquipmentRules.is_valid_slot(normalized))
            return new Godot.Collections.Array<StringName> { normalized };
        return new Godot.Collections.Array<StringName>();
    }

    private static Godot.Collections.Array<StringName> NormalizeStringNameList(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var rawValue in values)
        {
            var normalized = ProgressionDataUtils.to_string_name(rawValue);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private WeaponProfileDef _get_weapon_profile_resource()
    {
        return weapon_profile as WeaponProfileDef;
    }

    private static int ApplyPriceBasisPoints(int price, int priceBasisPoints)
    {
        int normalizedPrice = Mathf.Max(price, 0);
        int normalizedBasisPoints = Mathf.Max(priceBasisPoints, 0);
        return (normalizedPrice * normalizedBasisPoints + PriceBasisPointsDenominator / 2)
            / PriceBasisPointsDenominator;
    }
}
