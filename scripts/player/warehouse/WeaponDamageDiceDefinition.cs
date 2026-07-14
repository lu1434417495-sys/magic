using System;
using System.Collections.Generic;

public sealed class WeaponDamageDiceDefinition
{
    internal const int DiceCountMax = 99;
    internal const int DiceSidesMax = 999;
    internal const int FlatBonusMin = -999;
    internal const int FlatBonusMax = 999;

    public WeaponDamageDiceDefinition(int diceCount, int diceSides, int flatBonus)
    {
        DiceCount = diceCount;
        DiceSides = diceSides;
        FlatBonus = flatBonus;
    }

    public int DiceCount { get; }
    public int DiceSides { get; }
    public int FlatBonus { get; }

    public int GetDiceCount() => Math.Max(DiceCount, 1);

    public int GetDiceSides() => Math.Max(DiceSides, 1);

    public string ToRollLabel()
    {
        string label = $"{GetDiceCount()}D{GetDiceSides()}";
        if (FlatBonus > 0)
            label += $"+{FlatBonus}";
        else if (FlatBonus < 0)
            label += FlatBonus.ToString();
        return label;
    }

    internal static IReadOnlyList<string> ValidateDice(
        string label,
        WeaponDamageDiceDefinition dice
    )
    {
        var errors = new List<string>();
        if (dice == null)
            return errors;

        int diceCount = dice.DiceCount;
        if (diceCount < 1 || diceCount > DiceCountMax)
            errors.Add($"{label}.dice_count must be 1..{DiceCountMax}, got {diceCount}.");

        int diceSides = dice.DiceSides;
        if (diceSides < 1 || diceSides > DiceSidesMax)
            errors.Add($"{label}.dice_sides must be 1..{DiceSidesMax}, got {diceSides}.");

        if (dice.FlatBonus < FlatBonusMin || dice.FlatBonus > FlatBonusMax)
        {
            errors.Add(
                $"{label}.flat_bonus must be {FlatBonusMin}..{FlatBonusMax}, got {dice.FlatBonus}."
            );
        }
        return errors;
    }

    internal static WeaponDamageDiceDefinition FromResource(WeaponDamageDiceDef source) =>
        source == null
            ? null
            : new WeaponDamageDiceDefinition(
                source.dice_count,
                source.dice_sides,
                source.flat_bonus
            );

    internal static WeaponDamageDiceDefinition CopyOf(WeaponDamageDiceDefinition source) =>
        source == null
            ? null
            : new WeaponDamageDiceDefinition(source.DiceCount, source.DiceSides, source.FlatBonus);
}
