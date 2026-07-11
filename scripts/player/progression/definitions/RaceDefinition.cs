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
    public IReadOnlyDictionary<StringName, StringName> DamageResistances { get; }
    public IReadOnlyList<StringName> DialogueTags { get; }
    public IReadOnlyList<string> RacialTraitSummary { get; }

    internal static RaceDefinition FromResource(RaceDef source, string path)
    {
        IdentityDefinitionProjection.RequireResource(source, path, nameof(RaceDef));
        return new RaceDefinition(
            source.race_id,
            IdentityDefinitionProjection.CopyString(source.display_name, $"{path}.display_name"),
            IdentityDefinitionProjection.CopyString(source.description, $"{path}.description"),
            source.age_profile_id,
            source.default_subrace_id,
            IdentityDefinitionProjection.CopyStringNames(
                source.SubraceIdsBorrowed,
                $"{path}.subrace_ids"
            ),
            source.body_size_category,
            source.base_speed,
            IdentityDefinitionProjection.CopyAttributeModifiers(
                source.AttributeModifiersBorrowed,
                $"{path}.attribute_modifiers"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.TraitIdsBorrowed,
                $"{path}.trait_ids"
            ),
            IdentityDefinitionProjection.CopyRacialGrantedSkills(
                source.RacialGrantedSkillsBorrowed,
                $"{path}.racial_granted_skills"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.ProficiencyTagsBorrowed,
                $"{path}.proficiency_tags"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.VisionTagsBorrowed,
                $"{path}.vision_tags"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.SaveAdvantageTagsBorrowed,
                $"{path}.save_advantage_tags"
            ),
            IdentityDefinitionProjection.CopyDamageResistances(
                source.DamageResistancesBorrowed,
                $"{path}.damage_resistances"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.DialogueTagsBorrowed,
                $"{path}.dialogue_tags"
            ),
            IdentityDefinitionProjection.CopyStrings(
                source.RacialTraitSummaryBorrowed,
                $"{path}.racial_trait_summary"
            )
        );
    }
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

    internal static IReadOnlyList<StringName> CopyStringNames(
        Godot.Collections.Array<StringName> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        if (source.Count == 0)
            return System.Array.Empty<StringName>();
        var result = new List<StringName>(source.Count);
        for (int index = 0; index < source.Count; index++)
            result.Add(source[index]);
        return new ReadOnlyCollection<StringName>(result);
    }

    internal static IReadOnlyList<string> CopyStrings(
        Godot.Collections.Array<string> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        if (source.Count == 0)
            return System.Array.Empty<string>();
        var result = new List<string>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            string value = source[index];
            if (value == null)
            {
                throw new InvalidDataException(
                    $"Content string at '{NormalizePath(path)}[{index}]' must not be null."
                );
            }
            result.Add(value);
        }
        return new ReadOnlyCollection<string>(result);
    }

    internal static IReadOnlyList<AttributeModifierDefinition> CopyAttributeModifiers(
        Godot.Collections.Array<Resource> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        var result = new List<AttributeModifierDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] is not AttributeModifier modifier)
                throw InvalidNestedType(path, index, nameof(AttributeModifier));
            result.Add(AttributeModifierDefinition.FromResource(modifier));
        }
        return result.Count == 0
            ? System.Array.Empty<AttributeModifierDefinition>()
            : new ReadOnlyCollection<AttributeModifierDefinition>(result);
    }

    internal static IReadOnlyList<AttributeModifierDefinition> CopyAttributeModifiers(
        Godot.Collections.Array<AttributeModifier> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        var result = new List<AttributeModifierDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            AttributeModifier modifier = source[index];
            if (modifier == null)
                throw InvalidNestedType(path, index, nameof(AttributeModifier));
            result.Add(AttributeModifierDefinition.FromResource(modifier));
        }
        return result.Count == 0
            ? System.Array.Empty<AttributeModifierDefinition>()
            : new ReadOnlyCollection<AttributeModifierDefinition>(result);
    }

    internal static IReadOnlyList<RacialGrantedSkillDefinition> CopyRacialGrantedSkills(
        Godot.Collections.Array<RacialGrantedSkill> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        var result = new List<RacialGrantedSkillDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            RacialGrantedSkill value = source[index];
            if (value == null)
                throw InvalidNestedType(path, index, nameof(RacialGrantedSkill));
            result.Add(RacialGrantedSkillDefinition.FromResource(value, $"{path}[{index}]"));
        }
        return result.Count == 0
            ? System.Array.Empty<RacialGrantedSkillDefinition>()
            : new ReadOnlyCollection<RacialGrantedSkillDefinition>(result);
    }

    internal static IReadOnlyList<AgeStageRuleDefinition> CopyAgeStageRules(
        Godot.Collections.Array<AgeStageRule> source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        var result = new List<AgeStageRuleDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            AgeStageRule value = source[index];
            if (value == null)
                throw InvalidNestedType(path, index, nameof(AgeStageRule));
            result.Add(AgeStageRuleDefinition.FromResource(value, $"{path}[{index}]"));
        }
        return result.Count == 0
            ? System.Array.Empty<AgeStageRuleDefinition>()
            : new ReadOnlyCollection<AgeStageRuleDefinition>(result);
    }

    internal static IReadOnlyDictionary<StringName, StringName> CopyDamageResistances(
        Godot.Collections.Dictionary source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        var result = new Dictionary<StringName, StringName>(source.Count);
        int index = 0;
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                throw new InvalidDataException(
                    $"Content map key at '{NormalizePath(path)}[key:{index}]' must be StringName, got {rawKey.VariantType}."
                );
            }
            StringName key = rawKey.AsStringName();
            if (key == "")
                throw new InvalidDataException($"Content map at '{NormalizePath(path)}' has an empty key.");
            if (result.ContainsKey(key))
                throw DuplicateKey(path, key);
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.StringName)
            {
                throw new InvalidDataException(
                    $"Content value at '{NormalizePath(path)}.{key}' must be StringName, got {rawValue.VariantType}."
                );
            }
            result.Add(key, rawValue.AsStringName());
            index++;
        }
        return new ReadOnlyDictionary<StringName, StringName>(result);
    }

    internal static IReadOnlyDictionary<StringName, int> CopyStringNameIntMap(
        Godot.Collections.Dictionary source,
        string path
    )
    {
        if (source == null)
            throw MissingCollection(path);
        var result = new Dictionary<StringName, int>(source.Count);
        int index = 0;
        foreach (Variant rawKey in source.Keys)
        {
            StringName key = rawKey.VariantType switch
            {
                Variant.Type.String => new StringName(rawKey.AsString()),
                Variant.Type.StringName => rawKey.AsStringName(),
                _ => throw new InvalidDataException(
                    $"Content map key at '{NormalizePath(path)}[key:{index}]' must be String or StringName, got {rawKey.VariantType}."
                ),
            };
            if (key == "")
                throw new InvalidDataException($"Content map at '{NormalizePath(path)}' has an empty key.");
            if (result.ContainsKey(key))
                throw DuplicateKey(path, key);
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Int)
            {
                throw new InvalidDataException(
                    $"Content value at '{NormalizePath(path)}.{key}' must be Int, got {rawValue.VariantType}."
                );
            }
            result.Add(key, rawValue.AsInt32());
            index++;
        }
        return new ReadOnlyDictionary<StringName, int>(result);
    }

    private static InvalidDataException MissingCollection(string path) =>
        new($"Content collection at '{NormalizePath(path)}' must not be null.");

    private static InvalidDataException InvalidNestedType(
        string path,
        int index,
        string expectedType
    ) =>
        new(
            $"Content value at '{NormalizePath(path)}[{index}]' must be a non-null {expectedType}."
        );

    private static InvalidDataException DuplicateKey(string path, StringName key) =>
        new(
            $"Content map at '{NormalizePath(path)}' contains duplicate normalized key '{key}'."
        );

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path) ? "$" : path;
}
