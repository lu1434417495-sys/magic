using System;
using System.Collections.Generic;
using Godot;

internal static class EquipmentAbilityStatusDeclarationCatalog
{
    internal static EquipmentAbilityContentValidationContext ExpandWithEquipmentDeclarations(
        EquipmentAbilityContentValidationContext context,
        IReadOnlyList<EquipmentAbilityContentPackImportModel> packs
    )
    {
        var knownStatusIds = new HashSet<StringName>(context.KnownStatusIds);
        CollectEquipmentStatusDeclarations(knownStatusIds, packs);
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = context.KnownTraitIds,
            KnownSkillIds = context.KnownSkillIds,
            WindupSkillIds = context.WindupSkillIds,
            KnownStatusIds = EquipmentAbilityReadOnlySet<StringName>.From(knownStatusIds),
        };
    }

    internal static IReadOnlySet<StringName> CollectExternalStatusDeclarations(
        IEnumerable<TraitDefinition> traitDefinitions,
        IEnumerable<SkillDefinition> skillDefinitions
    )
    {
        var result = new HashSet<StringName>(
            StatusContentRules.SystemDeclaredStatusIdsTyped()
        );
        if (traitDefinitions != null)
        {
            foreach (TraitDefinition trait in traitDefinitions)
            {
                if (trait == null)
                    continue;
                foreach (
                    TraitPassiveStatusEffectDefinition passiveStatus in trait.PassiveStatusEffects
                )
                {
                    Add(result, passiveStatus?.StatusId ?? "");
                }
            }
        }

        if (skillDefinitions != null)
        {
            foreach (SkillDefinition skill in skillDefinitions)
                CollectSkillStatusDeclarations(result, skill?.CombatProfile);
        }
        return EquipmentAbilityReadOnlySet<StringName>.From(result);
    }

    private static void CollectSkillStatusDeclarations(
        HashSet<StringName> result,
        CombatSkillDefinition combatProfile
    )
    {
        if (combatProfile == null)
            return;
        CollectSkillEffectStatusDeclarations(result, combatProfile.EffectDefinitions);
        CollectSkillEffectStatusDeclarations(result, combatProfile.PassiveEffectDefinitions);
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
            CollectSkillEffectStatusDeclarations(result, castVariant?.EffectDefinitions);
    }

    private static void CollectSkillEffectStatusDeclarations(
        HashSet<StringName> result,
        IEnumerable<CombatEffectDefinition> effects
    )
    {
        if (effects == null)
            return;
        foreach (CombatEffectDefinition effect in effects)
        {
            if (effect == null)
                continue;
            Add(result, effect.StatusId);
            Add(result, effect.SaveFailureStatusId);
            Add(result, effect.RepeatHitStatusId);
            Add(result, effect.TerminationStatusId);
        }
    }

    private static void CollectEquipmentStatusDeclarations(
        HashSet<StringName> result,
        IReadOnlyList<EquipmentAbilityContentPackImportModel> packs
    )
    {
        if (packs == null)
            return;
        foreach (EquipmentAbilityContentPackImportModel pack in packs)
        {
            if (pack?.bindings == null)
                continue;
            foreach (EquipmentAbilityBindingImportModel binding in pack.bindings)
            {
                if (binding == null)
                    continue;
                foreach (EquipmentAbilityReactionImportModel reaction in binding.reactions)
                {
                    if (reaction == null)
                        continue;
                    CollectActionStatusDeclarations(result, reaction.actions);
                    foreach (
                        EquipmentOutcomeEntryImportModel entry in reaction.outcome_table?.entries
                            ?? Array.Empty<EquipmentOutcomeEntryImportModel>()
                    )
                    {
                        CollectActionStatusDeclarations(result, entry?.actions);
                    }
                }
                foreach (EquipmentWorldEffectImportModel worldEffect in binding.world_effects)
                    CollectActionStatusDeclarations(result, worldEffect?.actions);
                foreach (
                    EquipmentFatalInterceptImportModel fatalIntercept
                    in binding.fatal_intercepts
                        ?? Array.Empty<EquipmentFatalInterceptImportModel>()
                )
                {
                    CollectActionStatusDeclarations(result, fatalIntercept?.success_actions);
                }
            }
        }
    }

    private static void CollectActionStatusDeclarations(
        HashSet<StringName> result,
        IEnumerable<EquipmentAbilityActionImportModel> actions
    )
    {
        if (actions == null)
            return;
        foreach (EquipmentAbilityActionImportModel action in actions)
        {
            switch (action?.payload)
            {
                case ApplyStatusActionPayloadImportModel applyStatus:
                    Add(result, applyStatus.status_id);
                    break;
                case ModifyActionPointsActionPayloadImportModel actionPoints
                    when actionPoints.mode == "set_next_turn_ap_to_zero":
                    Add(result, actionPoints.status_id);
                    break;
                case MarkTargetActionPayloadImportModel markTarget:
                    Add(result, markTarget.mirror_status_id);
                    break;
                case ScheduleAreaEffectActionPayloadImportModel areaEffect:
                    Add(result, areaEffect.contact_status_id);
                    break;
            }
        }
    }

    private static void Add(HashSet<StringName> result, StringName statusId)
    {
        if (statusId != "")
            result.Add(statusId);
    }
}
