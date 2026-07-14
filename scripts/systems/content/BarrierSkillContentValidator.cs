using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class BarrierSkillContentValidator
{
    internal static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfiles
    )
    {
        skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();
        barrierProfiles ??= new Dictionary<StringName, BarrierProfileDefinition>();

        var errors = new List<string>();
        foreach (
            (StringName skillId, SkillDefinition skillDefinition) in skillDefinitions.OrderBy(
                entry => entry.Key.ToString(),
                StringComparer.Ordinal
            )
        )
        {
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile == null)
                continue;

            AppendLayeredBarrierReferenceErrors(
                errors,
                skillId,
                "combat_profile.effect_defs",
                combatProfile.EffectDefinitions,
                barrierProfiles
            );
            AppendLayeredBarrierReferenceErrors(
                errors,
                skillId,
                "combat_profile.passive_effect_defs",
                combatProfile.PassiveEffectDefinitions,
                barrierProfiles
            );
            for (int variantIndex = 0; variantIndex < combatProfile.CastVariants.Count; variantIndex++)
            {
                CombatCastVariantDefinition variant = combatProfile.CastVariants[variantIndex];
                if (variant == null)
                    continue;
                AppendLayeredBarrierReferenceErrors(
                    errors,
                    skillId,
                    $"combat_profile.cast_variants[{variantIndex}].effect_defs",
                    variant.EffectDefinitions,
                    barrierProfiles
                );
            }
        }

        foreach (
            (StringName profileId, BarrierProfileDefinition profile) in barrierProfiles.OrderBy(
                entry => entry.Key.ToString(),
                StringComparer.Ordinal
            )
        )
        {
            if (profile == null)
                continue;
            for (int layerIndex = 0; layerIndex < profile.Layers.Count; layerIndex++)
            {
                BarrierLayerDefinition layer = profile.Layers[layerIndex];
                if (layer == null)
                    continue;
                for (int breakerIndex = 0; breakerIndex < layer.BreakerSkillIds.Count; breakerIndex++)
                {
                    StringName breakerSkillId = layer.BreakerSkillIds[breakerIndex];
                    if (breakerSkillId == "")
                    {
                        errors.Add(
                            $"Barrier profile {profileId}.layers[{layerIndex}].breaker_skill_ids[{breakerIndex}] must be non-empty."
                        );
                    }
                    else if (!skillDefinitions.ContainsKey(breakerSkillId))
                    {
                        errors.Add(
                            $"Barrier profile {profileId}.layers[{layerIndex}].breaker_skill_ids[{breakerIndex}] references missing skill {breakerSkillId}."
                        );
                    }
                }
            }
        }

        return errors;
    }

    private static void AppendLayeredBarrierReferenceErrors(
        ICollection<string> errors,
        StringName skillId,
        string effectsLabel,
        IReadOnlyList<CombatEffectDefinition> effects,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfiles
    )
    {
        for (int effectIndex = 0; effectIndex < (effects?.Count ?? 0); effectIndex++)
        {
            CombatEffectDefinition effect = effects[effectIndex];
            if (effect?.EffectKind != BattleEffectKind.LayeredBarrier)
                continue;

            StringName profileId = effect.GetStringNameParamTyped("profile_id", "");
            string effectLabel = $"Skill {skillId} {effectsLabel}[{effectIndex}] layered_barrier";
            if (profileId == "")
            {
                errors.Add($"{effectLabel} must declare params.profile_id.");
            }
            else if (!barrierProfiles.ContainsKey(profileId))
            {
                errors.Add($"{effectLabel} references missing barrier profile {profileId}.");
            }
        }
    }
}
