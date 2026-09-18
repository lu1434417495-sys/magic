using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleSkillExecutionOrchestrator — skill-definition predicate gates (repeat/chain/applicability checks).
// Pure physical split: same class, no behavior change. See BattleSkillExecutionOrchestrator.cs.
internal sealed partial class BattleSkillExecutionOrchestrator
{

    private static bool HasRepeatAttackEffectDefinition(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) => BattleUnitSkillDefinitionExecutionRules.HasRepeatAttackEffect(effectDefinitions);

    private static bool CanApplyUnitSkillResultFromDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) => BattleUnitSkillDefinitionExecutionRules.CanApplyUnitSkillResult(effectDefinitions);

    private static bool CanApplyUnitSkillOrRepeatResultFromDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        return BattleUnitSkillDefinitionExecutionRules.CanApplyUnitSkillOrRepeatResult(
            effectDefinitions
        );
    }

    private static bool CanHandleUnitSkillCommandFromDefinitions(
        SkillDefinition skillDefinition,
        BattleSkillResolutionPolicy policy
    )
    {
        return skillDefinition?.CombatProfile != null
            && skillDefinition.CombatProfile.SpecialResolutionProfileId != new StringName("meteor_swarm")
            && policy?.RoutesToUnitTargeting == true
            && CanApplyUnitSkillOrRepeatResultFromDefinitions(policy.EffectDefinitions);
    }

    private bool CanApplyPendingUnitCastFromDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    )
    {
        return skillDefinition?.CombatProfile != null
            && skillDefinition.CombatProfile.SpecialResolutionProfileId != new StringName("meteor_swarm")
            && !IsRandomChainSkill(skillDefinition)
            && CanApplyUnitSkillOrRepeatResultFromDefinitions(
                CollectUnitSkillEffectDefinitions(skillDefinition, castVariantDefinition, activeUnit)
            );
    }

    private static bool IsRandomChainSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition?.CombatProfile?.TargetSelectionModeKind
            == BattleTargetSelectionMode.RandomChain;
    }

}
