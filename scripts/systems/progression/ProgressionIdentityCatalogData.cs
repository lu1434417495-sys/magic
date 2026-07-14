using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class ProgressionIdentityCatalogData
{
    public IReadOnlyDictionary<StringName, RaceDefinition> RaceDefs { get; }
    public IReadOnlyDictionary<StringName, SubraceDefinition> SubraceDefs { get; }
    public IReadOnlyDictionary<StringName, AgeProfileDefinition> AgeProfileDefs { get; }
    public IReadOnlyDictionary<StringName, BloodlineDefinition> BloodlineDefs { get; }
    public IReadOnlyDictionary<StringName, BloodlineStageDefinition> BloodlineStageDefs { get; }
    public IReadOnlyDictionary<StringName, AscensionDefinition> AscensionDefs { get; }
    public IReadOnlyDictionary<StringName, AscensionStageDefinition> AscensionStageDefs { get; }
    public IReadOnlyDictionary<StringName, StageAdvancementDefinition> StageAdvancementDefs { get; }

    public ProgressionIdentityCatalogData()
        : this(
            new Dictionary<StringName, RaceDefinition>(),
            new Dictionary<StringName, SubraceDefinition>(),
            new Dictionary<StringName, AgeProfileDefinition>(),
            new Dictionary<StringName, BloodlineDefinition>(),
            new Dictionary<StringName, BloodlineStageDefinition>(),
            new Dictionary<StringName, AscensionDefinition>(),
            new Dictionary<StringName, AscensionStageDefinition>(),
            new Dictionary<StringName, StageAdvancementDefinition>()
        ) { }

    public ProgressionIdentityCatalogData(
        IReadOnlyDictionary<StringName, RaceDefinition> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDefinition> subraceDefs,
        IReadOnlyDictionary<StringName, AgeProfileDefinition> ageProfileDefs,
        IReadOnlyDictionary<StringName, BloodlineDefinition> bloodlineDefs,
        IReadOnlyDictionary<StringName, BloodlineStageDefinition> bloodlineStageDefs,
        IReadOnlyDictionary<StringName, AscensionDefinition> ascensionDefs,
        IReadOnlyDictionary<StringName, AscensionStageDefinition> ascensionStageDefs,
        IReadOnlyDictionary<StringName, StageAdvancementDefinition> stageAdvancementDefs
    )
    {
        RaceDefs = Clone(raceDefs);
        SubraceDefs = Clone(subraceDefs);
        AgeProfileDefs = Clone(ageProfileDefs);
        BloodlineDefs = Clone(bloodlineDefs);
        BloodlineStageDefs = Clone(bloodlineStageDefs);
        AscensionDefs = Clone(ascensionDefs);
        AscensionStageDefs = Clone(ascensionStageDefs);
        StageAdvancementDefs = Clone(stageAdvancementDefs);
    }

    private static IReadOnlyDictionary<StringName, T> Clone<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        Dictionary<StringName, T> copy =
            source != null ? new Dictionary<StringName, T>(source) : new Dictionary<StringName, T>();
        return new ReadOnlyDictionary<StringName, T>(copy);
    }
}
