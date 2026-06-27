using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitOneDamageResolver : BattleDamageResolver
{
    public FixedHitOneDamageResolver()
    {
        SetHitResolver(new FixedHitResolver());
    }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    internal override AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        DamageResolutionContext damage_context
    )
    {
        return base.ResolveEffects(
            source_unit,
            target_unit,
            effect_definitions,
            damage_context
        );
    }

    internal override AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        return base.ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_definitions,
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
