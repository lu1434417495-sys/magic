using Godot;

[GlobalClass]
public partial class WeaponDamageDiceDef : Resource
{
    private const int DiceCountMax = 99;
    private const int DiceSidesMax = 999;
    private const int FlatBonusMin = -999;
    private const int FlatBonusMax = 999;

    [Export]
    public int dice_count = 1;

    [Export]
    public int dice_sides = 6;

    [Export]
    public int flat_bonus = 0;

    internal static Godot.Collections.Array<string> ValidateDice(
        string label,
        WeaponDamageDiceDef dice
    )
    {
        var errors = new Godot.Collections.Array<string>();
        if (dice == null)
        {
            return errors;
        }

        int dc = dice.GetDiceCount();
        if (dc < 1 || dc > DiceCountMax)
        {
            errors.Add($"{label}.dice_count must be 1..{DiceCountMax}, got {dc}.");
        }

        int ds = dice.GetDiceSides();
        if (ds < 1 || ds > DiceSidesMax)
        {
            errors.Add($"{label}.dice_sides must be 1..{DiceSidesMax}, got {ds}.");
        }

        if (dice.flat_bonus < FlatBonusMin || dice.flat_bonus > FlatBonusMax)
        {
            errors.Add(
                $"{label}.flat_bonus must be {FlatBonusMin}..{FlatBonusMax}, got {dice.flat_bonus}."
            );
        }
        return errors;
    }

    public WeaponDamageDiceDef DuplicateDice()
    {
        return new WeaponDamageDiceDef
        {
            dice_count = GetDiceCount(),
            dice_sides = GetDiceSides(),
            flat_bonus = flat_bonus,
        };
    }

    public int GetDiceCount()
    {
        return Mathf.Max(dice_count, 1);
    }

    public int GetDiceSides()
    {
        return Mathf.Max(dice_sides, 1);
    }

    public string ToRollLabel()
    {
        var label = $"{GetDiceCount()}D{GetDiceSides()}";
        if (flat_bonus > 0)
        {
            label += $"+{flat_bonus}";
        }
        else if (flat_bonus < 0)
        {
            label += $"{flat_bonus}";
        }
        return label;
    }
}
