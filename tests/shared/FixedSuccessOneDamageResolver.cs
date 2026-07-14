using Godot;
using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedSuccessOneDamageResolver : FixedHitOneDamageResolver
{
    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    internal override AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        AttackContext fixedContext = attack_context ?? new AttackContext();
        fixedContext.ForceHitNoCrit = true;
        return base.ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_definitions,
            attack_check,
            fixedContext
        );
    }
}
