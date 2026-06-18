using Godot;
using Godot.Collections;

public partial class FixedFailedSaveDamageResolver : FixedRollDamageResolver
{
    public FixedFailedSaveDamageResolver() { }

    public FixedFailedSaveDamageResolver(Array damageRolls, Array attackRolls)
        : base(damageRolls, attackRolls) { }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    internal override AttackEffectResolutionResult ResolveAttackEffects(
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

    internal override AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        Dictionary damage_context = null
    )
    {
        Dictionary fixedContext = damage_context?.Duplicate(true) ?? new Dictionary();
        fixedContext["save_roll_override"] = 1;
        return base.ResolveEffects(source_unit, target_unit, effect_defs, fixedContext);
    }
}
