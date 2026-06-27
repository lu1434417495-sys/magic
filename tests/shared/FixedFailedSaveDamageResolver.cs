using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class FixedFailedSaveDamageResolver : FixedRollDamageResolver
{
    public FixedFailedSaveDamageResolver() { }

    public FixedFailedSaveDamageResolver(Array damageRolls, Array attackRolls)
        : base(damageRolls, attackRolls) { }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    internal override AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        DamageResolutionContext damage_context
    )
    {
        Dictionary fixedContext = damage_context?.RawContext?.Duplicate(true) ?? new Dictionary();
        fixedContext["save_roll_override"] = 1;
        return base.ResolveEffects(
            source_unit,
            target_unit,
            effect_definitions,
            DamageResolutionContext.FromDictionary(fixedContext)
        );
    }
}
