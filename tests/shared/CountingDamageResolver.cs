using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class CountingDamageResolver : BattleDamageResolver
{
    public int resolve_effects_calls = 0;

    public override GDictionary resolve_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null
    )
    {
        resolve_effects_calls += 1;
        return base.resolve_effects(source_unit, target_unit, effect_defs, damage_context);
    }
}
