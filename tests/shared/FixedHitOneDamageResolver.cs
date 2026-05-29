using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitOneDamageResolver : BattleDamageResolver
{
    public new void set_skill_defs(GDictionary skill_defs) => base.set_skill_defs(skill_defs);

    public new void set_hit_resolver(GodotObject hit_resolver) =>
        base.set_hit_resolver(hit_resolver);

    public new BattleFateEventBus get_fate_event_bus() => base.get_fate_event_bus();

    public new GArray get_and_clear_last_stand_mastery_records() =>
        base.get_and_clear_last_stand_mastery_records();

    public override GDictionary resolve_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null
    )
    {
        return base.resolve_effects(source_unit, target_unit, effect_defs, damage_context);
    }

    public override GDictionary resolve_attack_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary attack_check,
        GDictionary attack_context = null
    )
    {
        return base.resolve_attack_effects(
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
            battle_state.attack_roll_nonce = Math.Max((int)battle_state.attack_roll_nonce, 0) + 1;
        }
        return Math.Clamp(10, Math.Min(min_value, max_value), Math.Max(min_value, max_value));
    }
}
