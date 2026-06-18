using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedSuccessOneDamageResolver : FixedHitOneDamageResolver
{
    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    internal override AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null
    )
    {
        return base.ResolveEffects(source_unit, target_unit, effect_defs, damage_context);
    }

    internal override AttackEffectResolutionResult ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        AttackContext fixedContext = attack_context ?? new AttackContext();
        fixedContext.ForceHitNoCrit = true;
        return base.ResolveAttackEffects(
            source_unit,
            target_unit,
            effect_defs,
            attack_check,
            fixedContext
        );
    }
}
