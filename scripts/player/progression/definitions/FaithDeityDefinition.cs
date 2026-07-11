using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public sealed class FaithDeityDefinition
{
    public FaithDeityDefinition(
        StringName deityId,
        string displayName,
        StringName facilityId,
        string serviceTypeLabel,
        IReadOnlyList<StringName> powerDomainTags,
        StringName rankProgressStatId,
        IReadOnlyList<FaithRankDefinition> rankDefinitions
    )
    {
        DeityId = deityId;
        DisplayName = displayName
            ?? throw new InvalidDataException("FaithDeityDefinition.DisplayName must not be null.");
        FacilityId = facilityId;
        ServiceTypeLabel = serviceTypeLabel
            ?? throw new InvalidDataException(
                "FaithDeityDefinition.ServiceTypeLabel must not be null."
            );
        PowerDomainTags = ProgressionDefinitionProjection.FreezeValues(
            powerDomainTags,
            "FaithDeityDefinition.PowerDomainTags"
        );
        RankProgressStatId = rankProgressStatId;
        RankDefinitions = ProgressionDefinitionProjection.FreezeValues(
            rankDefinitions,
            "FaithDeityDefinition.RankDefinitions"
        );
    }

    public StringName DeityId { get; }
    public string DisplayName { get; }
    public StringName FacilityId { get; }
    public string ServiceTypeLabel { get; }
    public IReadOnlyList<StringName> PowerDomainTags { get; }
    public StringName RankProgressStatId { get; }
    public IReadOnlyList<FaithRankDefinition> RankDefinitions { get; }

    public FaithRankDefinition GetRankDefinition(int rankIndex)
    {
        foreach (FaithRankDefinition definition in RankDefinitions)
        {
            if (definition.RankIndex == rankIndex)
                return definition;
        }
        return null;
    }

    public int GetMaxRank()
    {
        int maximum = 0;
        foreach (FaithRankDefinition definition in RankDefinitions)
            maximum = Math.Max(maximum, definition.RankIndex);
        return maximum;
    }

    internal static FaithDeityDefinition FromResource(FaithDeityDef source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        IReadOnlyList<FaithRankDefinition> ranks =
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RankDefsProjectionBorrowed,
                path + ".rank_defs",
                FaithRankDefinition.FromResource
            );

        return new FaithDeityDefinition(
            source.deity_id,
            source.display_name,
            source.facility_id,
            source.service_type_label,
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.PowerDomainTagsProjectionBorrowed,
                path + ".power_domain_tags"
            ),
            source.rank_progress_stat_id,
            ranks
        );
    }
}
