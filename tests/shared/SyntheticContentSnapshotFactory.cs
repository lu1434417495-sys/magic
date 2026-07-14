using System;
using System.Collections.Generic;
using Godot;

internal sealed class SyntheticContentSnapshotSeed
{
    internal long Epoch { get; set; } = 1;
    internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; set; }
    internal IReadOnlyDictionary<StringName, TraitDefinition> Traits { get; set; }
    internal IReadOnlyDictionary<StringName, ProfessionDefinition> Professions { get; set; }
    internal IReadOnlyDictionary<StringName, AchievementDefinition> Achievements { get; set; }
    internal IReadOnlyDictionary<StringName, QuestDefinition> Quests { get; set; }
    internal IReadOnlyDictionary<StringName, RaceDefinition> Races { get; set; }
    internal IReadOnlyDictionary<StringName, SubraceDefinition> Subraces { get; set; }
    internal IReadOnlyDictionary<StringName, AgeProfileDefinition> AgeProfiles { get; set; }
    internal IReadOnlyDictionary<StringName, BloodlineDefinition> Bloodlines { get; set; }
    internal IReadOnlyDictionary<StringName, BloodlineStageDefinition> BloodlineStages { get; set; }
    internal IReadOnlyDictionary<StringName, AscensionDefinition> Ascensions { get; set; }
    internal IReadOnlyDictionary<StringName, AscensionStageDefinition> AscensionStages { get; set; }
    internal IReadOnlyDictionary<StringName, StageAdvancementDefinition> StageAdvancements { get; set; }
    internal IReadOnlyDictionary<StringName, FaithDeityDefinition> FaithDeities { get; set; }
    internal IReadOnlyDictionary<StringName, BarrierProfileDefinition> BarrierProfiles { get; set; }
    internal IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> ContingencyTemplates { get; set; }
    internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; set; }
    internal IReadOnlyDictionary<StringName, RecipeDefinition> Recipes { get; set; }
    internal IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> EquipmentAbilityPacks { get; set; }
    internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentAbilityBindings { get; set; }
    internal IReadOnlyDictionary<string, WorldGenerationDefinition> WorldGenerations { get; set; }
    internal IBattleSpecialProfileView BattleSpecialProfiles { get; set; }
    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EnemyTemplates { get; set; }
    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyBrains { get; set; }
    internal IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> EncounterRosters { get; set; }
    internal IReadOnlyDictionary<StringName, BattleSimProfileDefinition> BattleSimProfiles { get; set; }
}

internal static class SyntheticContentSnapshotFactory
{
    internal static ContentSnapshot CreateEmpty(long epoch = 1) =>
        Create(new SyntheticContentSnapshotSeed { Epoch = epoch });

    internal static ContentSnapshot Create(SyntheticContentSnapshotSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new ContentSnapshot(
            seed.Epoch,
            OrEmpty(seed.Skills),
            OrEmpty(seed.Traits),
            OrEmpty(seed.Professions),
            OrEmpty(seed.Achievements),
            OrEmpty(seed.Quests),
            OrEmpty(seed.Races),
            OrEmpty(seed.Subraces),
            OrEmpty(seed.AgeProfiles),
            OrEmpty(seed.Bloodlines),
            OrEmpty(seed.BloodlineStages),
            OrEmpty(seed.Ascensions),
            OrEmpty(seed.AscensionStages),
            OrEmpty(seed.StageAdvancements),
            OrEmpty(seed.FaithDeities),
            OrEmpty(seed.BarrierProfiles),
            OrEmpty(seed.ContingencyTemplates),
            OrEmpty(seed.Items),
            OrEmpty(seed.Recipes),
            OrEmpty(seed.EquipmentAbilityPacks),
            OrEmpty(seed.EquipmentAbilityBindings),
            seed.WorldGenerations ?? new Dictionary<string, WorldGenerationDefinition>(StringComparer.Ordinal),
            seed.BattleSpecialProfiles ?? BattleSpecialProfileRuntimeView.Empty,
            OrEmpty(seed.EnemyTemplates),
            OrEmpty(seed.EnemyBrains),
            OrEmpty(seed.EncounterRosters),
            OrEmpty(seed.BattleSimProfiles)
        );
    }

    internal static SyntheticContentSnapshotSeed CreateSeed(ContentSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new SyntheticContentSnapshotSeed
        {
            Epoch = source.Epoch,
            Skills = source.Skills,
            Traits = source.Traits,
            Professions = source.Professions,
            Achievements = source.Achievements,
            Quests = source.Quests,
            Races = source.Races,
            Subraces = source.Subraces,
            AgeProfiles = source.AgeProfiles,
            Bloodlines = source.Bloodlines,
            BloodlineStages = source.BloodlineStages,
            Ascensions = source.Ascensions,
            AscensionStages = source.AscensionStages,
            StageAdvancements = source.StageAdvancements,
            FaithDeities = source.FaithDeities,
            BarrierProfiles = source.BarrierProfiles,
            ContingencyTemplates = source.ContingencyTemplates,
            Items = source.Items,
            Recipes = source.Recipes,
            EquipmentAbilityPacks = source.EquipmentAbilityPacks,
            EquipmentAbilityBindings = source.EquipmentAbilityBindings,
            WorldGenerations = source.WorldGenerations,
            BattleSpecialProfiles = source.BattleSpecialProfiles,
            EnemyTemplates = source.EnemyTemplates,
            EnemyBrains = source.EnemyBrains,
            EncounterRosters = source.EncounterRosters,
            BattleSimProfiles = source.BattleSimProfiles,
        };
    }

    private static IReadOnlyDictionary<StringName, T> OrEmpty<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class => source ?? new Dictionary<StringName, T>();

}
