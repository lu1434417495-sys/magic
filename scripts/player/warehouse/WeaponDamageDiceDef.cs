using Godot;

[GlobalClass]
public partial class WeaponDamageDiceDef : Resource
{
    public const int DiceCountMax = 99;
    public const int DiceSidesMax = 999;
    public const int FlatBonusMin = -999;
    public const int FlatBonusMax = 999;

    [Export]
    public int dice_count = 1;

    [Export]
    public int dice_sides = 6;

    [Export]
    public int flat_bonus = 0;

    public static Godot.Collections.Array<string> validate_dice(
        string label,
        WeaponDamageDiceDef dice
    )
    {
        var errors = new Godot.Collections.Array<string>();
        if (dice == null)
        {
            return errors;
        }

        int dc = dice.get_dice_count();
        if (dc < 1 || dc > DiceCountMax)
        {
            errors.Add($"{label}.dice_count must be 1..{DiceCountMax}, got {dc}.");
        }

        int ds = dice.get_dice_sides();
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

    public WeaponDamageDiceDef duplicate_dice()
    {
        return new WeaponDamageDiceDef
        {
            dice_count = get_dice_count(),
            dice_sides = get_dice_sides(),
            flat_bonus = flat_bonus,
        };
    }

    public int get_dice_count()
    {
        return Mathf.Max(dice_count, 1);
    }

    public int get_dice_sides()
    {
        return Mathf.Max(dice_sides, 1);
    }

    public string to_roll_label()
    {
        var label = $"{get_dice_count()}D{get_dice_sides()}";
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

    public int GetDiceCount() => get_dice_count();

    public int GetDiceSides() => get_dice_sides();

    public string ToRollLabel() => to_roll_label();
}
