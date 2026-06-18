internal static class WeaponDiceProjection
{
    internal static Godot.Collections.Dictionary Project(WeaponDice dice)
    {
        dice ??= new WeaponDice();
        if (dice.IsEmpty())
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["dice_count"] = dice.dice_count,
            ["dice_sides"] = dice.dice_sides,
            ["flat_bonus"] = dice.flat_bonus,
        };
    }
}
