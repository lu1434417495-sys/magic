using Godot;
using System.Collections.Generic;

public partial class FixedFailedSaveDamageResolver : FixedRollDamageResolver
{
    public FixedFailedSaveDamageResolver() { }

    public FixedFailedSaveDamageResolver(
        Godot.Collections.Array damageRolls,
        Godot.Collections.Array attackRolls
    )
        : base(damageRolls, attackRolls) { }

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
            (damage_context ?? DamageResolutionContext.Empty()).WithSaveRollOverrides(new[] { 1 })
        );
    }
}
