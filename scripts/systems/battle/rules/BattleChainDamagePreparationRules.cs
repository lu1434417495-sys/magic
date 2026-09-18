using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;
using static BattleSkillTargetPlanRules;

internal static class BattleChainDamagePreparationRules
{
    internal static BattlePreparedChainDamage BuildPreparedPlan(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState primaryTarget,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        bool backlashTriggered
    )
    {
        if (sourceUnit == null || primaryTarget == null || skillDefinition == null)
            return BattlePreparedChainDamage.Empty;
        CombatEffectDefinition chainEffect = FindChainEffect(effectDefinitions);
        if (chainEffect?.ChainDamage == null)
            return BattlePreparedChainDamage.Empty;
        IReadOnlyList<CombatEffectDefinition> targetEffects =
            BuildChainTargetEffectDefinitions(effectDefinitions, chainEffect);
        if (targetEffects.Count == 0)
            return BattlePreparedChainDamage.Empty;

        StringName targetFilter = BattleSkillTargetPlanRules.ResolveEffectTargetFilter(
            skillDefinition,
            chainEffect
        );
        if (BattleSkillTargetPlanRules.StringNameIsEmpty(targetFilter))
            return BattlePreparedChainDamage.Empty;
        if (state == null)
            return BattlePreparedChainDamage.Empty;
        BattleChainDamagePlan plan = BattleChainDamageRules.BuildPlan(
            state.AsReadView(),
            primaryTarget,
            chainEffect.ChainDamage,
            backlashTriggered,
            candidate =>
                BattleSkillTargetPlanRules._is_unit_valid_for_effect(
                    sourceUnit,
                    candidate.UnsafeUnitForReadOnlyRules,
                    targetFilter
                )
        );
        return new BattlePreparedChainDamage(chainEffect, targetEffects, plan);
    }

    internal static CombatEffectDefinition FindChainEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.ChainDamage
                && effectDefinition.ChainDamage != null
            )
                return effectDefinition;
        }
        return null;
    }

    internal static IReadOnlyList<CombatEffectDefinition> BuildChainTargetEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        CombatEffectDefinition chainEffect
    )
    {
        var chainTargetEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition == chainEffect
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
                continue;
            chainTargetEffects.Add(effectDefinition);
        }
        return chainTargetEffects;
    }
}
