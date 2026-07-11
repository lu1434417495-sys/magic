using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

/// <summary>
/// Process-scoped, immutable projection of authored non-AI content. Runtime owners may
/// borrow this graph, but they never receive the raw Resource roots used to build it.
/// </summary>
internal sealed class ContentSnapshot
{
    internal ContentSnapshot(
        long epoch,
        IReadOnlyDictionary<StringName, SkillDefinition> skills,
        IReadOnlyDictionary<StringName, TraitDefinition> traits,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professions,
        IReadOnlyDictionary<StringName, AchievementDefinition> achievements,
        IReadOnlyDictionary<StringName, QuestDefinition> quests,
        IReadOnlyDictionary<StringName, RaceDefinition> races,
        IReadOnlyDictionary<StringName, SubraceDefinition> subraces,
        IReadOnlyDictionary<StringName, AgeProfileDefinition> ageProfiles,
        IReadOnlyDictionary<StringName, BloodlineDefinition> bloodlines,
        IReadOnlyDictionary<StringName, BloodlineStageDefinition> bloodlineStages,
        IReadOnlyDictionary<StringName, AscensionDefinition> ascensions,
        IReadOnlyDictionary<StringName, AscensionStageDefinition> ascensionStages,
        IReadOnlyDictionary<StringName, StageAdvancementDefinition> stageAdvancements,
        IReadOnlyDictionary<StringName, FaithDeityDefinition> faithDeities,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierProfiles,
        IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> contingencyTemplates,
        IReadOnlyDictionary<StringName, ItemDefinition> items,
        IReadOnlyDictionary<StringName, RecipeDefinition> recipes,
        IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> equipmentAbilityPacks,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipmentAbilityBindings,
        IReadOnlyDictionary<string, WorldGenerationDefinition> worldGenerations,
        IBattleSpecialProfileView battleSpecialProfiles
    )
    {
        if (epoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(epoch), epoch, "Snapshot epoch must be positive.");

        Epoch = epoch;
        Skills = Freeze(skills);
        Traits = Freeze(traits);
        Professions = Freeze(professions);
        Achievements = Freeze(achievements);
        Quests = Freeze(quests);
        Races = Freeze(races);
        Subraces = Freeze(subraces);
        AgeProfiles = Freeze(ageProfiles);
        Bloodlines = Freeze(bloodlines);
        BloodlineStages = Freeze(bloodlineStages);
        Ascensions = Freeze(ascensions);
        AscensionStages = Freeze(ascensionStages);
        StageAdvancements = Freeze(stageAdvancements);
        IdentityCatalog = new ProgressionIdentityCatalogData(
            Races,
            Subraces,
            AgeProfiles,
            Bloodlines,
            BloodlineStages,
            Ascensions,
            AscensionStages,
            StageAdvancements
        );
        FaithDeities = Freeze(faithDeities);
        BarrierProfiles = Freeze(barrierProfiles);
        ContingencyTemplates = Freeze(contingencyTemplates);
        Items = Freeze(items);
        Recipes = Freeze(recipes);
        EquipmentAbilityPacks = Freeze(equipmentAbilityPacks);
        EquipmentAbilityBindings = Freeze(equipmentAbilityBindings);
        WorldGenerations = Freeze(worldGenerations, StringComparer.Ordinal);
        BattleSpecialProfiles = battleSpecialProfiles ?? BattleSpecialProfileRuntimeView.Empty;
    }

    internal long Epoch { get; }
    internal IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
    internal IReadOnlyDictionary<StringName, TraitDefinition> Traits { get; }
    internal IReadOnlyDictionary<StringName, ProfessionDefinition> Professions { get; }
    internal IReadOnlyDictionary<StringName, AchievementDefinition> Achievements { get; }
    internal IReadOnlyDictionary<StringName, QuestDefinition> Quests { get; }
    internal IReadOnlyDictionary<StringName, RaceDefinition> Races { get; }
    internal IReadOnlyDictionary<StringName, SubraceDefinition> Subraces { get; }
    internal IReadOnlyDictionary<StringName, AgeProfileDefinition> AgeProfiles { get; }
    internal IReadOnlyDictionary<StringName, BloodlineDefinition> Bloodlines { get; }
    internal IReadOnlyDictionary<StringName, BloodlineStageDefinition> BloodlineStages { get; }
    internal IReadOnlyDictionary<StringName, AscensionDefinition> Ascensions { get; }
    internal IReadOnlyDictionary<StringName, AscensionStageDefinition> AscensionStages { get; }
    internal IReadOnlyDictionary<StringName, StageAdvancementDefinition> StageAdvancements { get; }
    internal ProgressionIdentityCatalogData IdentityCatalog { get; }
    internal IReadOnlyDictionary<StringName, FaithDeityDefinition> FaithDeities { get; }
    internal IReadOnlyDictionary<StringName, BarrierProfileDefinition> BarrierProfiles { get; }
    internal IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> ContingencyTemplates { get; }
    internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
    internal IReadOnlyDictionary<StringName, RecipeDefinition> Recipes { get; }
    internal IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> EquipmentAbilityPacks { get; }
    internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> EquipmentAbilityBindings { get; }
    internal IReadOnlyDictionary<string, WorldGenerationDefinition> WorldGenerations { get; }
    internal IBattleSpecialProfileView BattleSpecialProfiles { get; }

    private static IReadOnlyDictionary<TKey, TValue> Freeze<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        IEqualityComparer<TKey> comparer = null
    )
        where TValue : class
    {
        var copy = comparer == null
            ? new Dictionary<TKey, TValue>()
            : new Dictionary<TKey, TValue>(comparer);
        if (source != null)
        {
            foreach ((TKey key, TValue value) in source)
            {
                if (key == null || value == null)
                    throw new ArgumentException("Snapshot dictionaries cannot contain null keys or values.", nameof(source));
                copy.Add(key, value);
            }
        }
        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }
}
