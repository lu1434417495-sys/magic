using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class CountingDamageResolver : BattleDamageResolver
{
    internal int ResolveEffectsCalls = 0;

    internal override AttackEffectResolutionResult ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        System.Collections.Generic.IEnumerable<CombatEffectDefinition> effect_definitions,
        DamageResolutionContext damage_context
    )
    {
        ResolveEffectsCalls += 1;
        return base.ResolveEffects(source_unit, target_unit, effect_definitions, damage_context);
    }
}
