using System;
using System.Collections.Generic;
using Godot;

internal interface IBattleCounterattackWeaponAttackDefinitionProvider
{
    bool TryResolve(
        BattleState state,
        BattleUnitReadView sourceUnit,
        BattleCounterattackCapability capability,
        out BattleImmediateWeaponAttackDefinition definition
    );
}

internal sealed class
    BattleRuntimeCounterattackWeaponAttackDefinitionProvider
    : BattleRuntimeModuleBorrower,
        IBattleCounterattackWeaponAttackDefinitionProvider
{
    public bool TryResolve(
        BattleState state,
        BattleUnitReadView sourceUnit,
        BattleCounterattackCapability capability,
        out BattleImmediateWeaponAttackDefinition definition
    )
    {
        definition = null;
        BattleRuntimeModule runtime = _runtime;
        if (
            runtime == null
            || state == null
            || !ReferenceEquals(state, runtime.GetState())
            || !sourceUnit.IsValid
            || capability.WeaponActionDefinitionId == new StringName("")
        )
        {
            return false;
        }

        StringName definitionId = capability.WeaponActionDefinitionId;
        SkillDefinition skillDefinition =
            runtime.GetSkillDefinitionTyped(definitionId);
        if (skillDefinition?.CombatProfile == null)
            return false;

        int skillLevel;
        if (sourceUnit.HasKnownSkillLevel(definitionId))
        {
            skillLevel = Math.Max(
                sourceUnit.GetKnownSkillLevel(definitionId),
                0
            );
        }
        else if (sourceUnit.KnowsActiveSkill(definitionId))
        {
            skillLevel = 1;
        }
        else
        {
            return false;
        }

        BattleSkillCastBlockReasonKind weaponRequirementBlockReason =
            BattleSkillWeaponRequirementRules.GetBlockReason(
                sourceUnit,
                skillDefinition,
                runtime.GetItemDefIndexTyped()
            );
        if (
            BattleSkillCastBlockReasonKinds.IsBlocked(
                weaponRequirementBlockReason
            )
        )
        {
            return false;
        }

        SkillEffectiveCombatDefinition effective =
            SkillEffectiveCombatDefinition.BuildUncached(
                skillDefinition,
                skillLevel
            );
        IReadOnlyList<CombatEffectDefinition> effects =
            effective.CombatProfile?.EffectDefinitions
            ?? Array.Empty<CombatEffectDefinition>();
        int staminaCost = effective.ResourceCosts.StaminaCost;
        if (
            effects.Count == 0
            || !BattleAttackDeliveryRules.IncludesWeaponDamage(effects)
            || staminaCost < 0
        )
        {
            return false;
        }

        definition = new BattleImmediateWeaponAttackDefinition(
            skillDefinition,
            effects,
            staminaCost
        );
        return true;
    }
}
