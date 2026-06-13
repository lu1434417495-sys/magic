using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class ProgressionIdentityCatalogData
{
    public IReadOnlyDictionary<StringName, RaceDef> RaceDefs { get; }
    public IReadOnlyDictionary<StringName, SubraceDef> SubraceDefs { get; }
    public IReadOnlyDictionary<StringName, AgeProfileDef> AgeProfileDefs { get; }
    public IReadOnlyDictionary<StringName, BloodlineDef> BloodlineDefs { get; }
    public IReadOnlyDictionary<StringName, BloodlineStageDef> BloodlineStageDefs { get; }
    public IReadOnlyDictionary<StringName, AscensionDef> AscensionDefs { get; }
    public IReadOnlyDictionary<StringName, AscensionStageDef> AscensionStageDefs { get; }
    public IReadOnlyDictionary<StringName, StageAdvancementModifier> StageAdvancementDefs { get; }

    public ProgressionIdentityCatalogData()
        : this(
            new Dictionary<StringName, RaceDef>(),
            new Dictionary<StringName, SubraceDef>(),
            new Dictionary<StringName, AgeProfileDef>(),
            new Dictionary<StringName, BloodlineDef>(),
            new Dictionary<StringName, BloodlineStageDef>(),
            new Dictionary<StringName, AscensionDef>(),
            new Dictionary<StringName, AscensionStageDef>(),
            new Dictionary<StringName, StageAdvancementModifier>()
        ) { }

    public ProgressionIdentityCatalogData(
        IReadOnlyDictionary<StringName, RaceDef> raceDefs,
        IReadOnlyDictionary<StringName, SubraceDef> subraceDefs,
        IReadOnlyDictionary<StringName, AgeProfileDef> ageProfileDefs,
        IReadOnlyDictionary<StringName, BloodlineDef> bloodlineDefs,
        IReadOnlyDictionary<StringName, BloodlineStageDef> bloodlineStageDefs,
        IReadOnlyDictionary<StringName, AscensionDef> ascensionDefs,
        IReadOnlyDictionary<StringName, AscensionStageDef> ascensionStageDefs,
        IReadOnlyDictionary<StringName, StageAdvancementModifier> stageAdvancementDefs
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
