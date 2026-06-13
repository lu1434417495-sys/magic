using System.Collections.Generic;
using Godot;

public static class PassiveStatusOrchestrator
{
    public static void ApplyToUnit(
        BattleUnitState unitState,
        PassiveSourceContext context = null,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = null
    )
    {
        if (unitState == null)
            return;
        var resolvedContext = context ?? new PassiveSourceContext();
        _clear_identity_projection(unitState);
        if (!_suppresses_original_race_traits(resolvedContext))
            RaceTraitResolver.ApplyToUnit(unitState, resolvedContext);
        AscensionTraitResolver.ApplyToUnit(unitState, resolvedContext);
        SkillPassiveResolver.ApplyToUnit(
            unitState,
            resolvedContext,
            skillDefs
        );
    }

    private static void _clear_identity_projection(BattleUnitState unitState)
    {
        unitState.vision_tags = new Godot.Collections.Array<StringName>();
        unitState.proficiency_tags = new Godot.Collections.Array<StringName>();
        unitState.save_advantage_tags = new Godot.Collections.Array<StringName>();
        unitState.damage_resistances = new Godot.Collections.Dictionary();
        unitState.race_trait_ids = new Godot.Collections.Array<StringName>();
        unitState.subrace_trait_ids = new Godot.Collections.Array<StringName>();
        unitState.ascension_trait_ids = new Godot.Collections.Array<StringName>();
        unitState.bloodline_trait_ids = new Godot.Collections.Array<StringName>();
    }

    private static bool _suppresses_original_race_traits(PassiveSourceContext context)
    {
        return context?.ascension_def != null
            && context.ascension_def.suppresses_original_race_traits;
    }
}
