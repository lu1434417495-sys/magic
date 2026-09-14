using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitMaxDamageResolver : BattleDamageResolver
{
    public FixedHitMaxDamageResolver()
    {
        SetHitResolver(new FixedHitResolver());
    }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    public override int _roll_damage_die(int dice_sides)
    {
        return Math.Max(dice_sides, 1);
    }

}
