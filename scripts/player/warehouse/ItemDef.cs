using Godot;
using System.Collections.Generic;

internal enum ItemCategoryKind
{
    Unknown = 0,
    Misc,
    Equipment,
    SkillBook,
}

internal enum ItemEquipmentTypeKind
{
    Unknown = 0,
    Weapon,
    Armor,
    Accessory,
}

internal enum WeaponPhysicalDamageTagKind
{
    Unknown = 0,
    Slash,
    Pierce,
    Blunt,
}

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

    internal ItemCategoryKind CategoryKind
    {
        get => ToItemCategoryKind(item_category);
        set => item_category = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<StringName> tags = new();

    [Export]
    public Godot.Collections.Array<StringName> crafting_groups = new();

    [Export]
    public Godot.Collections.Array<StringName> quest_groups = new();

    [Export]
    public Godot.Collections.Array<StringName> trait_ids { get; set; } = new();

    [Export]
    public Godot.Collections.Array<TraitRollGroupDef> trait_roll_groups { get; set; } = new();

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

    internal ItemEquipmentTypeKind EquipmentTypeKind
    {
        get => ToEquipmentTypeKind(equipment_type_id);
        set => equipment_type_id = ToStringName(value);
    }

    [Export]
    public Resource weapon_profile;

    [Export(PropertyHint.Range, "-1,20,1")]
    public int max_dex_bonus = -1;

    public int GetEffectiveMaxStack()
    {
        return is_stackable ? Mathf.Max(max_stack, 1) : 1;
    }

    public int GetBasePrice()
    {
        return Mathf.Max(base_price, 0);
    }

    public int GetBuyPrice()
    {
        return GetBuyPrice(PriceBasisPointsDenominator);
    }

    public int GetBuyPrice(int price_basis_points)
    {
        return ApplyPriceBasisPoints(Mathf.Max(buy_price, 0), price_basis_points);
    }

    public int GetSellPrice()
    {
        return GetSellPrice(PriceBasisPointsDenominator);
    }

    public int GetSellPrice(int price_basis_points)
    {
        if (!sellable)
            return 0;
        return ApplyPriceBasisPoints(Mathf.Max(sell_price, 0), price_basis_points);
    }

    public List<StringName> GetTagsTyped() => NormalizeStringNameList(tags);

    public List<StringName> GetCraftingGroupsTyped() =>
        NormalizeStringNameList(crafting_groups);

    public List<StringName> GetQuestGroupsTyped() =>
        NormalizeStringNameList(quest_groups);

    public List<StringName> GetTraitIdsTyped() => NormalizeStringNameList(trait_ids);

    public List<TraitRollGroupDef> GetTraitRollGroupsTyped()
    {
        List<TraitRollGroupDef> result = new();
        foreach (TraitRollGroupDef group in trait_roll_groups)
        {
            if (group != null)
                result.Add(group);
        }
        return result;
    }

    public StringName GetItemCategoryNormalized()
    {
        return ToStringName(CategoryKind);
    }

    public bool HasEquipmentCategory()
    {
        return CategoryKind == ItemCategoryKind.Equipment;
    }

    public List<StringName> GetEquipmentSlotIdsTyped()
    {
        return new List<StringName>(EquipmentRules.NormalizeSlotIdsTyped(equipment_slot_ids));
    }

    public bool IsEquipment()
    {
        return HasEquipmentCategory() && GetEquipmentSlotIdsTyped().Count > 0;
    }

    public StringName GetEquipmentTypeIdNormalized()
    {
        return ToStringName(EquipmentTypeKind);
    }

    public bool HasValidEquipmentType()
    {
        return GetEquipmentTypeIdNormalized() != "";
    }

    public bool IsWeapon()
    {
        return HasEquipmentCategory() && EquipmentTypeKind == ItemEquipmentTypeKind.Weapon;
    }

    public int GetWeaponAttackRange()
    {
        var profile = _get_weapon_profile_resource();
        if (!IsWeapon() || profile == null)
            return 0;
        return Mathf.Max(profile.attack_range, 0);
    }

    public StringName GetWeaponRangeType()
    {
        var profile = _get_weapon_profile_resource();
        if (!IsWeapon() || profile == null)
            return "";
        return ProgressionDataUtils.to_string_name(profile.range_type);
    }

    public StringName GetWeaponPhysicalDamageTag()
    {
        var profile = _get_weapon_profile_resource();
        if (!IsWeapon() || profile == null)
            return "";
        var normalized = ProgressionDataUtils.to_string_name(profile.damage_tag);
        return ToStringName(ToWeaponPhysicalDamageTagKind(normalized));
    }

    internal WeaponPhysicalDamageTagKind GetWeaponPhysicalDamageTagKind()
    {
        var profile = _get_weapon_profile_resource();
        if (!IsWeapon() || profile == null)
            return WeaponPhysicalDamageTagKind.Unknown;
        return ToWeaponPhysicalDamageTagKind(ProgressionDataUtils.to_string_name(profile.damage_tag));
    }

    public bool IsArmor()
    {
        return HasEquipmentCategory() && EquipmentTypeKind == ItemEquipmentTypeKind.Armor;
    }

    public int GetMaxDexBonus()
    {
        return Mathf.Max(max_dex_bonus, -1);
    }

    public bool IsAccessory()
    {
        return HasEquipmentCategory()
            && EquipmentTypeKind == ItemEquipmentTypeKind.Accessory;
    }

    public bool IsSkillBook()
    {
        return CategoryKind == ItemCategoryKind.SkillBook && granted_skill_id != "";
    }

    public List<AttributeModifier> GetAttributeModifiersTyped()
    {
        var result = new List<AttributeModifier>();
        foreach (var modifier in attribute_modifiers)
            result.Add(modifier);
        return result;
    }

    public List<StringName> GetFinalOccupiedSlotIdsTyped(StringName entry_slot_id)
    {
        if (occupied_slot_ids.Count > 0)
            return new List<StringName>(EquipmentRules.NormalizeSlotIdsTyped(occupied_slot_ids));
        var normalized = ProgressionDataUtils.to_string_name(entry_slot_id);
        if (EquipmentRules.IsValidSlot(normalized))
            return new List<StringName> { normalized };
        return new List<StringName>();
    }

    private static List<StringName> NormalizeStringNameList(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new List<StringName>();
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

    internal static ItemCategoryKind ToItemCategoryKind(StringName value)
    {
        if (value == "" || value == ItemCategoryMisc)
            return ItemCategoryKind.Misc;
        if (value == ItemCategoryEquipment)
            return ItemCategoryKind.Equipment;
        if (value == ItemCategorySkillBook)
            return ItemCategoryKind.SkillBook;
        return ItemCategoryKind.Unknown;
    }

    internal static ItemEquipmentTypeKind ToEquipmentTypeKind(StringName value)
    {
        if (value == EquipmentTypeWeapon)
            return ItemEquipmentTypeKind.Weapon;
        if (value == EquipmentTypeArmor)
            return ItemEquipmentTypeKind.Armor;
        if (value == EquipmentTypeAccessory)
            return ItemEquipmentTypeKind.Accessory;
        return ItemEquipmentTypeKind.Unknown;
    }

    internal static WeaponPhysicalDamageTagKind ToWeaponPhysicalDamageTagKind(StringName value)
    {
        if (value == DamageTagPhysicalSlash)
            return WeaponPhysicalDamageTagKind.Slash;
        if (value == DamageTagPhysicalPierce)
            return WeaponPhysicalDamageTagKind.Pierce;
        if (value == DamageTagPhysicalBlunt)
            return WeaponPhysicalDamageTagKind.Blunt;
        return WeaponPhysicalDamageTagKind.Unknown;
    }

    internal static StringName ToStringName(ItemCategoryKind kind)
    {
        return kind switch
        {
            ItemCategoryKind.Misc => ItemCategoryMisc,
            ItemCategoryKind.Equipment => ItemCategoryEquipment,
            ItemCategoryKind.SkillBook => ItemCategorySkillBook,
            _ => "",
        };
    }

    internal static StringName ToStringName(ItemEquipmentTypeKind kind)
    {
        return kind switch
        {
            ItemEquipmentTypeKind.Weapon => EquipmentTypeWeapon,
            ItemEquipmentTypeKind.Armor => EquipmentTypeArmor,
            ItemEquipmentTypeKind.Accessory => EquipmentTypeAccessory,
            _ => "",
        };
    }

    internal static StringName ToStringName(WeaponPhysicalDamageTagKind kind)
    {
        return kind switch
        {
            WeaponPhysicalDamageTagKind.Slash => DamageTagPhysicalSlash,
            WeaponPhysicalDamageTagKind.Pierce => DamageTagPhysicalPierce,
            WeaponPhysicalDamageTagKind.Blunt => DamageTagPhysicalBlunt,
            _ => "",
        };
    }

    private static int ApplyPriceBasisPoints(int price, int priceBasisPoints)
    {
        int normalizedPrice = Mathf.Max(price, 0);
        int normalizedBasisPoints = Mathf.Max(priceBasisPoints, 0);
        return (normalizedPrice * normalizedBasisPoints + PriceBasisPointsDenominator / 2)
            / PriceBasisPointsDenominator;
    }
}
