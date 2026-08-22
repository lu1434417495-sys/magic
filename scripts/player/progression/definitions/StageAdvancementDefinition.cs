using System.Collections.Generic;
using Godot;

public sealed class StageAdvancementDefinition
{
    public StageAdvancementDefinition(
        StringName modifierId,
        string displayName,
        StringName targetAxis,
        int stageOffset,
        StringName maxStageId,
        IReadOnlyList<StringName> appliesToRaceIds,
        IReadOnlyList<StringName> appliesToSubraceIds,
        IReadOnlyList<StringName> appliesToBloodlineIds,
        IReadOnlyList<StringName> appliesToAscensionIds,
        bool grantsAttributes,
        bool grantsTraits,
        bool grantsBodySizeChange
    )
    {
        ModifierId = modifierId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "StageAdvancementDefinition.DisplayName"
        );
        TargetAxis = targetAxis;
        StageOffset = stageOffset;
        MaxStageId = maxStageId;
        AppliesToRaceIds = IdentityDefinitionProjection.FreezeList(
            appliesToRaceIds,
            "StageAdvancementDefinition.AppliesToRaceIds"
        );
        AppliesToSubraceIds = IdentityDefinitionProjection.FreezeList(
            appliesToSubraceIds,
            "StageAdvancementDefinition.AppliesToSubraceIds"
        );
        AppliesToBloodlineIds = IdentityDefinitionProjection.FreezeList(
            appliesToBloodlineIds,
            "StageAdvancementDefinition.AppliesToBloodlineIds"
        );
        AppliesToAscensionIds = IdentityDefinitionProjection.FreezeList(
            appliesToAscensionIds,
            "StageAdvancementDefinition.AppliesToAscensionIds"
        );
        GrantsAttributes = grantsAttributes;
        GrantsTraits = grantsTraits;
        GrantsBodySizeChange = grantsBodySizeChange;
    }

    public StringName ModifierId { get; }
    public string DisplayName { get; }
    public StringName TargetAxis { get; }
    public int StageOffset { get; }
    public StringName MaxStageId { get; }
    public IReadOnlyList<StringName> AppliesToRaceIds { get; }
    public IReadOnlyList<StringName> AppliesToSubraceIds { get; }
    public IReadOnlyList<StringName> AppliesToBloodlineIds { get; }
    public IReadOnlyList<StringName> AppliesToAscensionIds { get; }
    public bool GrantsAttributes { get; }
    public bool GrantsTraits { get; }
    public bool GrantsBodySizeChange { get; }
    internal StageAdvancementTargetAxis TargetAxisKind =>
        StageAdvancementContentRules.ToTargetAxis(TargetAxis);

}
