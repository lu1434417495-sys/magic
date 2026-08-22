#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal static class ProfessionIdentityDefinitionProjector
{
    internal static ProfessionDefinition Project(ProfessionImportModel import)
    {
        ProfessionJsonDto value = import.Content;
        return new ProfessionDefinition(
            Name(value.ProfessionId),
            value.DisplayName,
            value.Description,
            value.MaxRank,
            value.HitDieSides,
            Name(value.BabProgression),
            value.IsInitialProfession,
            Name(value.UnlockKnowledgeId),
            value.UnlockRequirement == null ? null : Project(value.UnlockRequirement),
            value.RankRequirements.Select(Project).ToArray(),
            value.GrantedSkills.Select(Project).ToArray(),
            value.AttributeModifiers.Select(Project).ToArray(),
            value.ActiveConditions.Select(Project).ToArray(),
            Name(value.ReactivationMode),
            Name(value.DependencyVisibilityMode)
        );
    }

    internal static RaceDefinition Project(RaceImportModel import)
    {
        RaceJsonDto value = import.Content;
        return new RaceDefinition(
            Name(value.RaceId), value.DisplayName, value.Description,
            Name(value.AgeProfileId), Name(value.DefaultSubraceId), Names(value.SubraceIds),
            Name(value.BodySizeCategory), value.BaseSpeed,
            value.AttributeModifiers.Select(Project).ToArray(), Names(value.TraitIds),
            value.RacialGrantedSkills.Select(Project).ToArray(), Names(value.ProficiencyTags),
            Names(value.VisionTags), Names(value.SaveAdvantageTags),
            Names(value.SaveDisadvantageTags), Names(value.SaveImmunityTags),
            NameMap(value.DamageResistances), Names(value.DialogueTags), value.RacialTraitSummary
        );
    }

    internal static SubraceDefinition Project(SubraceImportModel import)
    {
        SubraceJsonDto value = import.Content;
        return new SubraceDefinition(
            Name(value.SubraceId), Name(value.ParentRaceId), value.DisplayName, value.Description,
            Name(value.BodySizeCategoryOverride), value.SpeedBonus,
            value.AttributeModifiers.Select(Project).ToArray(), Names(value.TraitIds),
            value.RacialGrantedSkills.Select(Project).ToArray(), Names(value.ProficiencyTags),
            Names(value.VisionTags), Names(value.SaveAdvantageTags),
            Names(value.SaveDisadvantageTags), Names(value.SaveImmunityTags),
            NameMap(value.DamageResistances), Names(value.DialogueTags), value.RacialTraitSummary
        );
    }

    internal static FaithDeityDefinition Project(FaithImportModel import)
    {
        FaithDeityJsonDto value = import.Content;
        return new FaithDeityDefinition(
            Name(value.DeityId), value.DisplayName, Name(value.FacilityId),
            value.ServiceTypeLabel, Names(value.PowerDomainTags), Name(value.RankProgressStatId),
            value.RankDefs.Select(Project).ToArray()
        );
    }

    internal static AgeProfileDefinition Project(AgeProfileImportModel import)
    {
        AgeProfileJsonDto value = import.Content;
        return new AgeProfileDefinition(
            Name(value.ProfileId), Name(value.RaceId), value.ChildAge, value.TeenAge,
            value.YoungAdultAge, value.AdultAge, value.MiddleAge, value.OldAge,
            value.VenerableAge, value.MaxNaturalAge, value.StageRules.Select(Project).ToArray(),
            Names(value.CreationStageIds), NameIntMap(value.DefaultAgeByStage)
        );
    }

    internal static BloodlineDefinition ProjectBloodline(BloodlineImportModel import)
    {
        BloodlineJsonDto value = import.Bloodline
            ?? throw new InvalidOperationException("Bloodline import does not contain a bloodline payload.");
        return new BloodlineDefinition(
            Name(value.BloodlineId), value.DisplayName, value.Description, Names(value.StageIds),
            Names(value.TraitIds), value.RacialGrantedSkills.Select(Project).ToArray(),
            value.AttributeModifiers.Select(Project).ToArray(), value.TraitSummary
        );
    }

    internal static BloodlineStageDefinition ProjectBloodlineStage(BloodlineImportModel import)
    {
        BloodlineStageJsonDto value = import.Stage
            ?? throw new InvalidOperationException("Bloodline import does not contain a stage payload.");
        return new BloodlineStageDefinition(
            Name(value.StageId), Name(value.BloodlineId), value.DisplayName, value.Description,
            value.AttributeModifiers.Select(Project).ToArray(), Names(value.TraitIds),
            value.RacialGrantedSkills.Select(Project).ToArray(), value.TraitSummary
        );
    }

    internal static AscensionDefinition ProjectAscension(AscensionImportModel import)
    {
        AscensionJsonDto value = import.Ascension
            ?? throw new InvalidOperationException("Ascension import does not contain an ascension payload.");
        return new AscensionDefinition(
            Name(value.AscensionId), value.DisplayName, value.Description, Names(value.StageIds),
            Names(value.TraitIds), value.RacialGrantedSkills.Select(Project).ToArray(),
            Names(value.AllowedRaceIds), Names(value.AllowedSubraceIds),
            Names(value.AllowedBloodlineIds), value.TraitSummary, value.ReplacesAgeGrowth,
            value.SuppressesOriginalRaceTraits
        );
    }

    internal static AscensionStageDefinition ProjectAscensionStage(AscensionImportModel import)
    {
        AscensionStageJsonDto value = import.Stage
            ?? throw new InvalidOperationException("Ascension import does not contain a stage payload.");
        return new AscensionStageDefinition(
            Name(value.StageId), Name(value.AscensionId), value.DisplayName, value.Description,
            value.AttributeModifiers.Select(Project).ToArray(), Names(value.TraitIds),
            value.RacialGrantedSkills.Select(Project).ToArray(),
            Name(value.BodySizeCategoryOverride), value.TraitSummary
        );
    }

    internal static StageAdvancementDefinition Project(StageAdvancementImportModel import)
    {
        StageAdvancementJsonDto value = import.Content;
        return new StageAdvancementDefinition(
            Name(value.ModifierId), value.DisplayName, Name(value.TargetAxis), value.StageOffset,
            Name(value.MaxStageId), Names(value.AppliesToRaceIds), Names(value.AppliesToSubraceIds),
            Names(value.AppliesToBloodlineIds), Names(value.AppliesToAscensionIds),
            value.GrantsAttributes, value.GrantsTraits, value.GrantsBodySizeChange
        );
    }

    private static ProfessionPromotionRequirementDefinition Project(
        ProfessionPromotionRequirementJsonDto value
    ) =>
        new(
            Names(value.RequiredSkillIds), value.RequiredTagRules.Select(Project).ToArray(),
            value.RequiredProfessionRanks.Select(Project).ToArray(),
            value.RequiredAttributeRules.Select(Project).ToArray(),
            value.RequiredReputationRules.Select(Project).ToArray(),
            value.AssignedCoreMustBeSubsetOfQualifiers
        );

    private static ProfessionRankRequirementDefinition Project(
        ProfessionRankRequirementJsonDto value
    ) =>
        new(
            value.TargetRank, value.RequiredTagRules.Select(Project).ToArray(),
            value.RequiredProfessionRanks.Select(Project).ToArray(),
            value.RequiredAttributeRules.Select(Project).ToArray(),
            value.RequiredReputationRules.Select(Project).ToArray()
        );

    private static TagRequirementDefinition Project(TagRequirementJsonDto value) =>
        new(Name(value.Tag), value.Count, Name(value.SkillState), Name(value.OriginFilter), Name(value.SelectionRole));
    private static ProfessionRankGateDefinition Project(ProfessionRankGateJsonDto value) =>
        new(Name(value.ProfessionId), value.MinRank, Name(value.CheckMode));
    private static AttributeRequirementDefinition Project(AttributeRequirementJsonDto value) =>
        new(Name(value.AttributeId), value.MinValue, value.MaxValue);
    private static ReputationRequirementDefinition Project(ReputationRequirementJsonDto value) =>
        new(Name(value.StateId), value.MinValue, value.MaxValue);
    private static ProfessionGrantedSkillDefinition Project(ProfessionGrantedSkillJsonDto value) =>
        new(Name(value.SkillId), value.UnlockRank, Name(value.SkillType));
    private static ProfessionActiveConditionDefinition Project(ProfessionActiveConditionJsonDto value) =>
        new(Name(value.ConditionType), Name(value.AttributeId), Name(value.StateId), value.MinValue, value.MaxValue);
    private static AttributeModifierDefinition Project(IdentityAttributeModifierJsonDto value) =>
        new(Name(value.AttributeId), Name(value.Mode), value.Value, value.ValuePerRank, Name(value.SourceType), Name(value.SourceId));
    private static RacialGrantedSkillDefinition Project(RacialGrantedSkillJsonDto value) =>
        new(Name(value.SkillId), value.MinimumSkillLevel, Name(value.ChargeKind), value.Charges);
    private static FaithRankDefinition Project(FaithRankJsonDto value) =>
        new(
            value.RankIndex, value.RankName, value.RequiredGold, value.RequiredLevel,
            Name(value.RequiredCustomStatId), value.RequiredCustomStatMinValue,
            Name(value.RequiredAchievementId),
            value.RewardEntries.Select(reward => new FaithRankRewardEntryDefinition(
                Name(reward.EntryType), Name(reward.TargetId), reward.Amount,
                reward.TargetLabel, reward.ReasonText
            )).ToArray()
        );
    private static AgeStageRuleDefinition Project(AgeStageRuleJsonDto value) =>
        new(
            Name(value.StageId), value.DisplayName, value.Description,
            value.AttributeModifiers.Select(Project).ToArray(), Names(value.TraitIds),
            value.TraitSummary, value.SelectableInCreation, value.ReachableByAging
        );

    private static StringName Name(string value) => new(value ?? "");
    private static IReadOnlyList<StringName> Names(IEnumerable<string> values) =>
        Array.AsReadOnly(values.Select(Name).ToArray());
    private static IReadOnlyDictionary<StringName, StringName> NameMap(
        IReadOnlyDictionary<string, string> values
    ) =>
        new ReadOnlyDictionary<StringName, StringName>(
            values.ToDictionary(pair => Name(pair.Key), pair => Name(pair.Value))
        );
    private static IReadOnlyDictionary<StringName, int> NameIntMap(
        IReadOnlyDictionary<string, int> values
    ) =>
        new ReadOnlyDictionary<StringName, int>(
            values.ToDictionary(pair => Name(pair.Key), pair => pair.Value)
        );
}
