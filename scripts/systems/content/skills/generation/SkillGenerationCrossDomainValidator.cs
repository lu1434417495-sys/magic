#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal static class SkillGenerationCrossDomainRules
{
    internal const string ExistingSkillId = "skill.generation.cross_domain.existing_skill_id";
    internal const string MissingSkillReference =
        "skill.generation.cross_domain.missing_skill_reference";
    internal const string MissingAchievementReference =
        "skill.generation.cross_domain.missing_achievement_reference";
    internal const string MissingBarrierProfile =
        "skill.generation.cross_domain.missing_barrier_profile";
    internal const string MissingSpecialProfile =
        "skill.generation.cross_domain.missing_special_profile";
    internal const string InvalidReactionSkill =
        "skill.generation.cross_domain.invalid_reaction_skill";
    internal const string MissingTextureAsset =
        "skill.generation.cross_domain.missing_texture_asset";
}

internal static class SkillGenerationCrossDomainValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> Validate(
        IReadOnlyList<ContentImportEntry<SkillImportModel>> entries,
        IReadOnlyDictionary<StringName, SkillDefinition> candidateSkills,
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills,
        ContentSnapshot processSnapshot,
        IReadOnlySet<StringName> textureAssetIds
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(candidateSkills);
        ArgumentNullException.ThrowIfNull(combinedSkills);
        ArgumentNullException.ThrowIfNull(processSnapshot);
        ArgumentNullException.ThrowIfNull(textureAssetIds);

        var diagnostics = new List<ContentJsonDiagnostic>();
        foreach (ContentImportEntry<SkillImportModel> entry in entries)
        {
            SkillImportModel import = entry.Import;
            JsonContentEntryContext context = entry.Context;
            var skillId = new StringName(import.SkillId.Value);
            if (processSnapshot.Skills.ContainsKey(skillId))
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.ExistingSkillId,
                    context,
                    "/skill_id",
                    $"Generated skill ID '{skillId}' already exists in the process snapshot."
                ));
            }

            string iconIdValue = import.IconId.Value ?? "";
            if (
                iconIdValue.Length > 0
                && !textureAssetIds.Contains(new StringName(iconIdValue))
            )
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.MissingTextureAsset,
                    context,
                    "/icon_id",
                    $"Texture asset ID '{iconIdValue}' is not present in the supplied typed asset-ID catalog."
                ));
            }

            AppendSkillReferences(
                diagnostics,
                context,
                import.LearnRequirements,
                "/learn_requirements",
                combinedSkills
            );
            AppendSkillReferences(
                diagnostics,
                context,
                import.SkillLevelRequirements.Keys,
                "/skill_level_requirements",
                combinedSkills
            );
            AppendSkillReferences(
                diagnostics,
                context,
                import.UpgradeSourceSkillIds,
                "/upgrade_source_skill_ids",
                combinedSkills
            );
            AppendAchievementReferences(
                diagnostics,
                context,
                import.AchievementRequirements,
                processSnapshot.Achievements
            );
            AppendCombatReferences(
                diagnostics,
                context,
                import.CombatProfile,
                combinedSkills,
                processSnapshot
            );
        }
        return diagnostics.AsReadOnly();
    }

    private static void AppendSkillReferences(
        ICollection<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        IEnumerable<SkillImportIdentifier> references,
        string fieldPointer,
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills
    )
    {
        int index = 0;
        foreach (SkillImportIdentifier reference in references)
        {
            var referencedSkillId = new StringName(reference.Value);
            if (!combinedSkills.ContainsKey(referencedSkillId))
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.MissingSkillReference,
                    context,
                    $"{fieldPointer}/{index}",
                    $"Skill reference '{referencedSkillId}' does not exist in the combined skill catalog."
                ));
            }
            index += 1;
        }
    }

    private static void AppendAchievementReferences(
        ICollection<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        IReadOnlyList<SkillImportIdentifier> references,
        IReadOnlyDictionary<StringName, AchievementDefinition> achievements
    )
    {
        for (int index = 0; index < references.Count; index += 1)
        {
            var achievementId = new StringName(references[index].Value);
            if (!achievements.ContainsKey(achievementId))
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.MissingAchievementReference,
                    context,
                    $"/achievement_requirements/{index}",
                    $"Achievement reference '{achievementId}' does not exist in the process snapshot."
                ));
            }
        }
    }

    private static void AppendCombatReferences(
        ICollection<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        CombatSkillImportModel? combat,
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills,
        ContentSnapshot processSnapshot
    )
    {
        if (combat == null)
            return;

        string specialProfileId = combat.SpecialResolutionProfileId.Value;
        if (specialProfileId.Length > 0)
        {
            var profileId = new StringName(specialProfileId);
            if (!processSnapshot.BattleSpecialProfiles.TryGetMeteorSwarmProfile(
                    profileId,
                    out _
                ))
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.MissingSpecialProfile,
                    context,
                    "/combat_profile/special_resolution_profile_id",
                    $"Battle special profile '{profileId}' does not exist in the process snapshot."
                ));
            }
        }

        CombatSpellReactionImportModel? reaction = combat.SpellReactionProfile;
        if (reaction != null)
        {
            var reactionSkillId = new StringName(reaction.ReactionSkillId.Value);
            if (
                !combinedSkills.TryGetValue(
                    reactionSkillId,
                    out SkillDefinition? reactionSkill
                )
                || !IsValidReactionSkill(reactionSkill)
            )
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.InvalidReactionSkill,
                    context,
                    "/combat_profile/spell_reaction_profile/reaction_skill_id",
                    $"Reaction skill '{reactionSkillId}' must exist and be an active unit skill with weapon-dice damage."
                ));
            }
        }

        AppendBarrierProfiles(
            diagnostics,
            context,
            combat.EffectDefs,
            "/combat_profile/effect_defs",
            processSnapshot.BarrierProfiles
        );
        AppendBarrierProfiles(
            diagnostics,
            context,
            combat.PassiveEffectDefs,
            "/combat_profile/passive_effect_defs",
            processSnapshot.BarrierProfiles
        );
        for (int variantIndex = 0; variantIndex < combat.CastVariants.Count; variantIndex += 1)
        {
            AppendBarrierProfiles(
                diagnostics,
                context,
                combat.CastVariants[variantIndex].EffectDefs,
                $"/combat_profile/cast_variants/{variantIndex}/effect_defs",
                processSnapshot.BarrierProfiles
            );
        }
    }

    private static bool IsValidReactionSkill(SkillDefinition? skill) =>
        skill?.SkillTypeKind == SkillTypeKind.Active
        && skill.CombatProfile?.TargetModeKind == BattleTargetMode.Unit
        && HasWeaponDiceDamage(skill.CombatProfile.EffectDefinitions);

    private static bool HasWeaponDiceDamage(
        IReadOnlyList<CombatEffectDefinition> effects
    )
    {
        foreach (CombatEffectDefinition effect in effects)
        {
            if (effect?.EffectKind == BattleEffectKind.Damage && effect.AddWeaponDice)
                return true;
        }
        return false;
    }

    private static void AppendBarrierProfiles(
        ICollection<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        IReadOnlyList<CombatEffectImportModel> effects,
        string effectsPointer,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profiles
    )
    {
        for (int effectIndex = 0; effectIndex < effects.Count; effectIndex += 1)
        {
            if (effects[effectIndex].Payload is not LayeredBarrierEffectPayloadImportModel payload)
                continue;
            var profileId = new StringName(payload.ProfileId.Value);
            if (!profiles.ContainsKey(profileId))
            {
                diagnostics.Add(Diagnostic(
                    SkillGenerationCrossDomainRules.MissingBarrierProfile,
                    context,
                    $"{effectsPointer}/{effectIndex}/payload/profile_id",
                    $"Barrier profile '{profileId}' does not exist in the process snapshot."
                ));
            }
        }
    }

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        JsonContentEntryContext context,
        string relativePointer,
        string message
    ) => new(
        ruleId,
        message,
        context.SourceLabel,
        context.JsonPointer + relativePointer
    );
}
