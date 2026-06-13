using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitOneDamageResolver : BattleDamageResolver
{
    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    internal override GDictionary ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null
    )
    {
        return base.ResolveEffects(source_unit, target_unit, effect_defs, damage_context);
    }

    internal override GDictionary ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        return base.ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            attack_context
        );
    }

    public override int _roll_damage_die(int dice_sides)
    {
        return 1;
    }

    public int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
        {
            battle_state.NextAttackRollNonce();
        }
        return Math.Clamp(10, Math.Min(min_value, max_value), Math.Max(min_value, max_value));
    }
}
