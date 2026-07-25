using System.Collections.Generic;
using Godot;

internal static class EquipmentAbilityStatusDeclarationCatalog
{
    internal static EquipmentAbilityContentValidationContext ExpandWithEquipmentDeclarations(
        EquipmentAbilityContentValidationContext context,
        IReadOnlyList<EquipmentAbilityContentPackDef> packs
    )
    {
        var knownStatusIds = new HashSet<StringName>(context.KnownStatusIds);
        CollectEquipmentStatusDeclarations(knownStatusIds, packs);
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = context.KnownTraitIds,
            KnownSkillIds = context.KnownSkillIds,
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
        IReadOnlyList<EquipmentAbilityContentPackDef> packs
    )
    {
        if (packs == null)
            return;
        foreach (EquipmentAbilityContentPackDef pack in packs)
        {
            if (pack?.bindings == null)
                continue;
            foreach (EquipmentAbilityBindingDef binding in pack.bindings)
            {
                if (binding == null)
                    continue;
                foreach (EquipmentAbilityReactionDef reaction in binding.reactions)
                {
                    if (reaction == null)
                        continue;
                    CollectActionStatusDeclarations(result, reaction.actions);
                    foreach (
                        EquipmentOutcomeEntryDef entry in reaction.outcome_table?.entries
                            ?? new Godot.Collections.Array<EquipmentOutcomeEntryDef>()
                    )
                    {
                        CollectActionStatusDeclarations(result, entry?.actions);
                    }
                }
                foreach (EquipmentWorldEffectDef worldEffect in binding.world_effects)
                    CollectActionStatusDeclarations(result, worldEffect?.actions);
            }
        }
    }

    private static void CollectActionStatusDeclarations(
        HashSet<StringName> result,
        IEnumerable<EquipmentAbilityActionDef> actions
    )
    {
        if (actions == null)
            return;
        foreach (EquipmentAbilityActionDef action in actions)
        {
            switch (action?.payload)
            {
                case ApplyStatusActionPayloadDef applyStatus:
                    Add(result, applyStatus.status_id);
                    break;
                case ModifyActionPointsActionPayloadDef actionPoints
                    when actionPoints.mode == "set_next_turn_ap_to_zero":
                    Add(result, actionPoints.status_id);
                    break;
                case MarkTargetActionPayloadDef markTarget:
                    Add(result, markTarget.mirror_status_id);
                    break;
                case ScheduleAreaEffectActionPayloadDef areaEffect:
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
