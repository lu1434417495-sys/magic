using System.Collections.Generic;
using Godot;

public static class PassiveStatusOrchestrator
{
    public static void ApplyToUnit(
        BattleUnitState unitState,
        PassiveSourceContext context = null,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
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
            skillDefinitions
        );
    }

    private static void _clear_identity_projection(BattleUnitState unitState)
    {
        unitState.vision_tags = new StringNameList();
        unitState.proficiency_tags = new StringNameList();
        unitState.save_advantage_tags = new StringNameList();
        unitState.save_disadvantage_tags = new StringNameList();
        unitState.save_immunity_tags = new StringNameList();
        unitState.damage_resistances = new BattleStringNameMap();
        unitState.save_bonus_by_ability = new BattleStringNameIntMap();
    }

    private static bool _suppresses_original_race_traits(PassiveSourceContext context)
    {
        return context?.ascension_def != null
            && context.ascension_def.SuppressesOriginalRaceTraits;
    }
}
