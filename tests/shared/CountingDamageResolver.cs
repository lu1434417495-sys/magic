using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class CountingDamageResolver : BattleDamageResolver
{
    internal int ResolveEffectsCalls = 0;

    internal override GDictionary ResolveEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null
    )
    {
        ResolveEffectsCalls += 1;
        return base.ResolveEffects(source_unit, target_unit, effect_defs, damage_context);
    }
}
