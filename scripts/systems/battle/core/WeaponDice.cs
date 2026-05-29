using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WeaponDice : RefCounted
{
    public int dice_count { get; set; }
    public int dice_sides { get; set; }
    public int flat_bonus { get; set; }

    public WeaponDice() { }

    public WeaponDice duplicate_state()
    {
        return FromValues(dice_count, dice_sides, flat_bonus);
    }

    public bool is_empty()
    {
        return dice_count <= 0 || dice_sides <= 0;
    }

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["dice_count"] = dice_count,
            ["dice_sides"] = dice_sides,
            ["flat_bonus"] = flat_bonus,
        };
    }

    public static WeaponDice from_dict(GDictionary data)
    {
        if (data == null)
        {
            return new WeaponDice();
        }
        int count = GetInt(data, "dice_count");
        int sides = GetInt(data, "dice_sides");
        int bonus = GetInt(data, "flat_bonus");
        if (count <= 0 || sides <= 0)
        {
            return new WeaponDice();
        }
        return FromValues(count, sides, bonus);
    }

    public static WeaponDice from_resource(GodotObject dice_resource)
    {
        if (dice_resource == null)
        {
            return new WeaponDice();
        }
        return from_dict(
            new GDictionary
            {
                ["dice_count"] = GetObjectInt(dice_resource, "dice_count"),
                ["dice_sides"] = GetObjectInt(dice_resource, "dice_sides"),
                ["flat_bonus"] = GetObjectInt(dice_resource, "flat_bonus"),
            }
        );
    }

    private static int GetInt(GDictionary values, string key)
    {
        return values.ContainsKey(key) ? values[key].AsInt32() : 0;
    }

    private static int GetObjectInt(GodotObject source, string property)
    {
        var value = source.Get(property);
        return value.VariantType == Variant.Type.Nil ? 0 : value.AsInt32();
    }

    private static WeaponDice FromValues(int count, int sides, int bonus)
    {
        return new WeaponDice
        {
            dice_count = count,
            dice_sides = sides,
            flat_bonus = bonus,
        };
    }
}
