using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedSuccessOneDamageResolver : FixedHitOneDamageResolver
{
    public new void set_skill_defs(GDictionary skill_defs) => base.set_skill_defs(skill_defs);

    public new void set_hit_resolver(GodotObject hit_resolver) =>
        base.set_hit_resolver(hit_resolver);

    public new BattleFateEventBus get_fate_event_bus() => base.get_fate_event_bus();

    public new Godot.Collections.Array get_and_clear_last_stand_mastery_records() =>
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
        GDictionary fixedContext = attack_context?.Duplicate(true) ?? new GDictionary();
        fixedContext["force_hit_no_crit"] = true;
        return base.resolve_attack_effects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            fixedContext
        );
    }
}
