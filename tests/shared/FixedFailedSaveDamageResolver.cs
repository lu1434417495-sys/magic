using Godot;
using Godot.Collections;

public partial class FixedFailedSaveDamageResolver : FixedRollDamageResolver
{
    public FixedFailedSaveDamageResolver() { }

    public FixedFailedSaveDamageResolver(Array damageRolls, Array attackRolls)
        : base(damageRolls, attackRolls) { }

    public new void set_skill_defs(Dictionary skill_defs) => base.set_skill_defs(skill_defs);

    public new void set_hit_resolver(GodotObject hit_resolver) =>
        base.set_hit_resolver(hit_resolver);

    public new BattleFateEventBus get_fate_event_bus() => base.get_fate_event_bus();

    public new Array get_and_clear_last_stand_mastery_records() =>
        base.get_and_clear_last_stand_mastery_records();

    public override Dictionary resolve_attack_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        Dictionary attack_check,
        Dictionary attack_context = null
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

    public override Dictionary resolve_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        Dictionary damage_context = null
    )
    {
        Dictionary fixedContext = damage_context?.Duplicate(true) ?? new Dictionary();
        fixedContext["save_roll_override"] = 1;
        return base.resolve_effects(source_unit, target_unit, effect_defs, fixedContext);
    }
}
