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
        StageAdvancementModifier.ToTargetAxis(TargetAxis);

    internal static StageAdvancementDefinition FromResource(
        StageAdvancementModifier source,
        string path
    )
    {
        IdentityDefinitionProjection.RequireResource(
            source,
            path,
            nameof(StageAdvancementModifier)
        );
        return new StageAdvancementDefinition(
            source.modifier_id,
            IdentityDefinitionProjection.CopyString(source.display_name, $"{path}.display_name"),
            source.target_axis,
            source.stage_offset,
            source.max_stage_id,
            IdentityDefinitionProjection.CopyStringNames(
                source.AppliesToRaceIdsBorrowed,
                $"{path}.applies_to_race_ids"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.AppliesToSubraceIdsBorrowed,
                $"{path}.applies_to_subrace_ids"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.AppliesToBloodlineIdsBorrowed,
                $"{path}.applies_to_bloodline_ids"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.AppliesToAscensionIdsBorrowed,
                $"{path}.applies_to_ascension_ids"
            ),
            source.grants_attributes,
            source.grants_traits,
            source.grants_body_size_change
        );
    }
}
