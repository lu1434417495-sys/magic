using System;
using System.Collections.Generic;
using Godot;

internal static class TestProgressionDefinitionProjection
{
    internal static ProfessionDefinition Profession(ProfessionDef source) =>
        ProfessionDefinition.FromResource(source);

    internal static AchievementDefinition Achievement(AchievementDef source) =>
        AchievementDefinition.FromSeed(source, Path("achievement", source?.achievement_id ?? ""));

    internal static QuestDefinition Quest(QuestDef source) =>
        QuestDefinition.FromResource(source, Path("quest", source?.quest_id ?? ""));

    internal static TraitDefinition Trait(TraitDef source) =>
        TraitDefinition.FromResource(source);

    internal static RaceDefinition Race(RaceDef source) =>
        RaceDefinition.FromResource(source, Path("race", source?.race_id ?? ""));

    internal static SubraceDefinition Subrace(SubraceDef source) =>
        SubraceDefinition.FromResource(source, Path("subrace", source?.subrace_id ?? ""));

    internal static AgeProfileDefinition AgeProfile(AgeProfileDef source) =>
        AgeProfileDefinition.FromResource(source, Path("age_profile", source?.profile_id ?? ""));

    internal static AgeStageRuleDefinition AgeStageRule(AgeStageRule source) =>
        AgeStageRuleDefinition.FromResource(source, Path("age_stage", source?.stage_id ?? ""));

    internal static BloodlineDefinition Bloodline(BloodlineDef source) =>
        BloodlineDefinition.FromResource(source, Path("bloodline", source?.bloodline_id ?? ""));

    internal static BloodlineStageDefinition BloodlineStage(BloodlineStageDef source) =>
        BloodlineStageDefinition.FromResource(
            source,
            Path("bloodline_stage", source?.stage_id ?? "")
        );

    internal static AscensionDefinition Ascension(AscensionDef source) =>
        AscensionDefinition.FromResource(source, Path("ascension", source?.ascension_id ?? ""));

    internal static AscensionStageDefinition AscensionStage(AscensionStageDef source) =>
        AscensionStageDefinition.FromResource(
            source,
            Path("ascension_stage", source?.stage_id ?? "")
        );

    internal static StageAdvancementDefinition StageAdvancement(
        StageAdvancementModifier source
    ) =>
        StageAdvancementDefinition.FromResource(
            source,
            Path("stage_advancement", source?.modifier_id ?? "")
        );

    internal static TagRequirementDefinition TagRequirement(TagRequirement source) =>
        TagRequirementDefinition.FromResource(source, "test.tag_requirement");

    internal static Dictionary<StringName, ProfessionDefinition> Professions(
        IReadOnlyDictionary<StringName, ProfessionDef> source
    ) => Project(source, Profession);

    internal static Dictionary<StringName, AchievementDefinition> Achievements(
        IReadOnlyDictionary<StringName, AchievementDef> source
    ) => Project(source, Achievement);

    internal static Dictionary<StringName, QuestDefinition> Quests(
        IReadOnlyDictionary<StringName, QuestDef> source
    ) => Project(source, Quest);

    internal static Dictionary<StringName, TraitDefinition> Traits(
        IReadOnlyDictionary<StringName, TraitDef> source
    ) => Project(source, Trait);

    internal static Dictionary<StringName, RaceDefinition> Races(
        IReadOnlyDictionary<StringName, RaceDef> source
    ) => Project(source, Race);

    internal static Dictionary<StringName, SubraceDefinition> Subraces(
        IReadOnlyDictionary<StringName, SubraceDef> source
    ) => Project(source, Subrace);

    internal static Dictionary<StringName, AgeProfileDefinition> AgeProfiles(
        IReadOnlyDictionary<StringName, AgeProfileDef> source
    ) => Project(source, AgeProfile);

    internal static Dictionary<StringName, BloodlineDefinition> Bloodlines(
        IReadOnlyDictionary<StringName, BloodlineDef> source
    ) => Project(source, Bloodline);

    internal static Dictionary<StringName, BloodlineStageDefinition> BloodlineStages(
        IReadOnlyDictionary<StringName, BloodlineStageDef> source
    ) => Project(source, BloodlineStage);

    internal static Dictionary<StringName, AscensionDefinition> Ascensions(
        IReadOnlyDictionary<StringName, AscensionDef> source
    ) => Project(source, Ascension);

    internal static Dictionary<StringName, AscensionStageDefinition> AscensionStages(
        IReadOnlyDictionary<StringName, AscensionStageDef> source
    ) => Project(source, AscensionStage);

    internal static Dictionary<StringName, StageAdvancementDefinition> StageAdvancements(
        IReadOnlyDictionary<StringName, StageAdvancementModifier> source
    ) => Project(source, StageAdvancement);

    private static Dictionary<StringName, TDefinition> Project<TSource, TDefinition>(
        IReadOnlyDictionary<StringName, TSource> source,
        Func<TSource, TDefinition> projector
    )
        where TSource : class
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(projector);
        var result = new Dictionary<StringName, TDefinition>(source.Count);
        foreach ((StringName key, TSource value) in source)
        {
            if (key == "")
                throw new ArgumentException("Test authored fixture contains an empty key.", nameof(source));
            result.Add(key, projector(value));
        }
        return result;
    }

    private static string Path(string kind, StringName id) =>
        $"test.{kind}.{(id == "" ? "<missing>" : id.ToString())}";
}
