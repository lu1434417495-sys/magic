using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public sealed class RaceDefinition
{
    public RaceDefinition(
        StringName raceId,
        string displayName,
        string description,
        StringName ageProfileId,
        StringName defaultSubraceId,
        IReadOnlyList<StringName> subraceIds,
        StringName bodySizeCategory,
        int baseSpeed,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<RacialGrantedSkillDefinition> racialGrantedSkills,
        IReadOnlyList<StringName> proficiencyTags,
        IReadOnlyList<StringName> visionTags,
        IReadOnlyList<StringName> saveAdvantageTags,
        IReadOnlyList<StringName> saveDisadvantageTags,
        IReadOnlyList<StringName> saveImmunityTags,
        IReadOnlyDictionary<StringName, StringName> damageResistances,
        IReadOnlyList<StringName> dialogueTags,
        IReadOnlyList<string> racialTraitSummary
    )
    {
        RaceId = raceId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "RaceDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "RaceDefinition.Description"
        );
        AgeProfileId = ageProfileId;
        DefaultSubraceId = defaultSubraceId;
        SubraceIds = IdentityDefinitionProjection.FreezeList(
            subraceIds,
            "RaceDefinition.SubraceIds"
        );
        BodySizeCategory = bodySizeCategory;
        BaseSpeed = baseSpeed;
        AttributeModifiers = IdentityDefinitionProjection.FreezeList(
            attributeModifiers,
            "RaceDefinition.AttributeModifiers"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "RaceDefinition.TraitIds"
        );
        RacialGrantedSkills = IdentityDefinitionProjection.FreezeList(
            racialGrantedSkills,
            "RaceDefinition.RacialGrantedSkills"
        );
        ProficiencyTags = IdentityDefinitionProjection.FreezeList(
            proficiencyTags,
            "RaceDefinition.ProficiencyTags"
        );
        VisionTags = IdentityDefinitionProjection.FreezeList(
            visionTags,
            "RaceDefinition.VisionTags"
        );
        SaveAdvantageTags = IdentityDefinitionProjection.FreezeList(
            saveAdvantageTags,
            "RaceDefinition.SaveAdvantageTags"
        );
        SaveDisadvantageTags = IdentityDefinitionProjection.FreezeList(
            saveDisadvantageTags,
            "RaceDefinition.SaveDisadvantageTags"
        );
        SaveImmunityTags = IdentityDefinitionProjection.FreezeList(
            saveImmunityTags,
            "RaceDefinition.SaveImmunityTags"
        );
        DamageResistances = IdentityDefinitionProjection.FreezeStringNameMap(
            damageResistances,
            "RaceDefinition.DamageResistances"
        );
        DialogueTags = IdentityDefinitionProjection.FreezeList(
            dialogueTags,
            "RaceDefinition.DialogueTags"
        );
        RacialTraitSummary = IdentityDefinitionProjection.FreezeList(
            racialTraitSummary,
            "RaceDefinition.RacialTraitSummary"
        );
    }

    public StringName RaceId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public StringName AgeProfileId { get; }
    public StringName DefaultSubraceId { get; }
    public IReadOnlyList<StringName> SubraceIds { get; }
    public StringName BodySizeCategory { get; }
    public int BaseSpeed { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<RacialGrantedSkillDefinition> RacialGrantedSkills { get; }
    public IReadOnlyList<StringName> ProficiencyTags { get; }
    public IReadOnlyList<StringName> VisionTags { get; }
    public IReadOnlyList<StringName> SaveAdvantageTags { get; }
    public IReadOnlyList<StringName> SaveDisadvantageTags { get; }
    public IReadOnlyList<StringName> SaveImmunityTags { get; }
    public IReadOnlyDictionary<StringName, StringName> DamageResistances { get; }
    public IReadOnlyList<StringName> DialogueTags { get; }
    public IReadOnlyList<string> RacialTraitSummary { get; }

}

internal static class IdentityDefinitionProjection
{
    internal static void RequireResource(object source, string path, string expectedType)
    {
        if (source == null)
        {
            throw new InvalidDataException(
                $"Content resource at '{NormalizePath(path)}' must be a non-null {expectedType}."
            );
        }
    }

    internal static string CopyString(string value, string path) =>
        value
        ?? throw new InvalidDataException(
            $"Content string at '{NormalizePath(path)}' must not be null."
        );

    internal static IReadOnlyList<T> FreezeList<T>(IReadOnlyList<T> source, string path)
    {
        if (source == null)
            throw MissingCollection(path);
        if (source.Count == 0)
            return System.Array.Empty<T>();
        var result = new List<T>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            T value = source[index];
            if (ReferenceEquals(value, null))
            {
                throw new InvalidDataException(
                    $"Content value at '{NormalizePath(path)}[{index}]' must not be null."
                );
            }
            result.Add(value);
        }
        return new ReadOnlyCollection<T>(result);
    }

    internal static IReadOnlyDictionary<StringName, StringName> FreezeStringNameMap(
        IReadOnlyDictionary<StringName, StringName> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        if (source.Count == 0)
            return new ReadOnlyDictionary<StringName, StringName>(
                new Dictionary<StringName, StringName>()
            );
        var result = new Dictionary<StringName, StringName>(source.Count);
        foreach ((StringName key, StringName value) in source)
        {
            if (key == "")
                throw new InvalidDataException($"Content map at '{NormalizePath(path)}' has an empty key.");
            if (result.ContainsKey(key))
                throw DuplicateKey(path, key);
            result.Add(key, value);
        }
        return new ReadOnlyDictionary<StringName, StringName>(result);
    }

    internal static IReadOnlyDictionary<StringName, int> FreezeStringNameIntMap(
        IReadOnlyDictionary<StringName, int> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        if (source.Count == 0)
            return new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
        var result = new Dictionary<StringName, int>(source.Count);
        foreach ((StringName key, int value) in source)
        {
            if (key == "")
                throw new InvalidDataException($"Content map at '{NormalizePath(path)}' has an empty key.");
            if (result.ContainsKey(key))
                throw DuplicateKey(path, key);
            result.Add(key, value);
        }
        return new ReadOnlyDictionary<StringName, int>(result);
    }

    private static InvalidDataException MissingCollection(string path) =>
        new($"Content collection at '{NormalizePath(path)}' must not be null.");

    private static InvalidDataException DuplicateKey(string path, StringName key) =>
        new(
            $"Content map at '{NormalizePath(path)}' contains duplicate normalized key '{key}'."
        );

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path) ? "$" : path;
}
