using System.Collections.Generic;
using Godot;

public sealed class AgeProfileDefinition
{
    public AgeProfileDefinition(
        StringName profileId,
        StringName raceId,
        int childAge,
        int teenAge,
        int youngAdultAge,
        int adultAge,
        int middleAge,
        int oldAge,
        int venerableAge,
        int maxNaturalAge,
        IReadOnlyList<AgeStageRuleDefinition> stageRules,
        IReadOnlyList<StringName> creationStageIds,
        IReadOnlyDictionary<StringName, int> defaultAgeByStage
    )
    {
        ProfileId = profileId;
        RaceId = raceId;
        ChildAge = childAge;
        TeenAge = teenAge;
        YoungAdultAge = youngAdultAge;
        AdultAge = adultAge;
        MiddleAge = middleAge;
        OldAge = oldAge;
        VenerableAge = venerableAge;
        MaxNaturalAge = maxNaturalAge;
        StageRules = IdentityDefinitionProjection.FreezeList(
            stageRules,
            "AgeProfileDefinition.StageRules"
        );
        CreationStageIds = IdentityDefinitionProjection.FreezeList(
            creationStageIds,
            "AgeProfileDefinition.CreationStageIds"
        );
        DefaultAgeByStage = IdentityDefinitionProjection.FreezeStringNameIntMap(
            defaultAgeByStage,
            "AgeProfileDefinition.DefaultAgeByStage"
        );
    }

    public StringName ProfileId { get; }
    public StringName RaceId { get; }
    public int ChildAge { get; }
    public int TeenAge { get; }
    public int YoungAdultAge { get; }
    public int AdultAge { get; }
    public int MiddleAge { get; }
    public int OldAge { get; }
    public int VenerableAge { get; }
    public int MaxNaturalAge { get; }
    public IReadOnlyList<AgeStageRuleDefinition> StageRules { get; }
    public IReadOnlyList<StringName> CreationStageIds { get; }
    public IReadOnlyDictionary<StringName, int> DefaultAgeByStage { get; }

}
