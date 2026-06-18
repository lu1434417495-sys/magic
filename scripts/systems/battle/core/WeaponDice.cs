using Godot;

public sealed class WeaponDice
{
    public int dice_count { get; set; }
    public int dice_sides { get; set; }
    public int flat_bonus { get; set; }

    public WeaponDice() { }

    public WeaponDice DuplicateState()
    {
        return FromValues(dice_count, dice_sides, flat_bonus);
    }

    public bool IsEmpty()
    {
        return dice_count <= 0 || dice_sides <= 0;
    }

    internal static WeaponDice FromDictionary(Godot.Collections.Dictionary data)
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

    internal static WeaponDice FromResource(WeaponDamageDiceDef diceResource)
    {
        if (diceResource == null)
        {
            return new WeaponDice();
        }
        return FromValues(
            diceResource.GetDiceCount(),
            diceResource.GetDiceSides(),
            diceResource.flat_bonus
        );
    }

    private static int GetInt(Godot.Collections.Dictionary values, string key)
    {
        return values.ContainsKey(key) ? values[key].AsInt32() : 0;
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
