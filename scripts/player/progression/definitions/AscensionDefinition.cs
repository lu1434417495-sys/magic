using System.Collections.Generic;
using Godot;

public sealed class AscensionDefinition
{
    public AscensionDefinition(
        StringName ascensionId,
        string displayName,
        string description,
        IReadOnlyList<StringName> stageIds,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<RacialGrantedSkillDefinition> racialGrantedSkills,
        IReadOnlyList<StringName> allowedRaceIds,
        IReadOnlyList<StringName> allowedSubraceIds,
        IReadOnlyList<StringName> allowedBloodlineIds,
        IReadOnlyList<string> traitSummary,
        bool replacesAgeGrowth,
        bool suppressesOriginalRaceTraits
    )
    {
        AscensionId = ascensionId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "AscensionDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "AscensionDefinition.Description"
        );
        StageIds = IdentityDefinitionProjection.FreezeList(
            stageIds,
            "AscensionDefinition.StageIds"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "AscensionDefinition.TraitIds"
        );
        RacialGrantedSkills = IdentityDefinitionProjection.FreezeList(
            racialGrantedSkills,
            "AscensionDefinition.RacialGrantedSkills"
        );
        AllowedRaceIds = IdentityDefinitionProjection.FreezeList(
            allowedRaceIds,
            "AscensionDefinition.AllowedRaceIds"
        );
        AllowedSubraceIds = IdentityDefinitionProjection.FreezeList(
            allowedSubraceIds,
            "AscensionDefinition.AllowedSubraceIds"
        );
        AllowedBloodlineIds = IdentityDefinitionProjection.FreezeList(
            allowedBloodlineIds,
            "AscensionDefinition.AllowedBloodlineIds"
        );
        TraitSummary = IdentityDefinitionProjection.FreezeList(
            traitSummary,
            "AscensionDefinition.TraitSummary"
        );
        ReplacesAgeGrowth = replacesAgeGrowth;
        SuppressesOriginalRaceTraits = suppressesOriginalRaceTraits;
    }

    public StringName AscensionId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> StageIds { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<RacialGrantedSkillDefinition> RacialGrantedSkills { get; }
    public IReadOnlyList<StringName> AllowedRaceIds { get; }
    public IReadOnlyList<StringName> AllowedSubraceIds { get; }
    public IReadOnlyList<StringName> AllowedBloodlineIds { get; }
    public IReadOnlyList<string> TraitSummary { get; }
    public bool ReplacesAgeGrowth { get; }
    public bool SuppressesOriginalRaceTraits { get; }

    internal static AscensionDefinition FromResource(AscensionDef source, string path)
    {
        IdentityDefinitionProjection.RequireResource(source, path, nameof(AscensionDef));
        return new AscensionDefinition(
            source.ascension_id,
            IdentityDefinitionProjection.CopyString(source.display_name, $"{path}.display_name"),
            IdentityDefinitionProjection.CopyString(source.description, $"{path}.description"),
            IdentityDefinitionProjection.CopyStringNames(
                source.StageIdsBorrowed,
                $"{path}.stage_ids"
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
                source.AllowedRaceIdsBorrowed,
                $"{path}.allowed_race_ids"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.AllowedSubraceIdsBorrowed,
                $"{path}.allowed_subrace_ids"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.AllowedBloodlineIdsBorrowed,
                $"{path}.allowed_bloodline_ids"
            ),
            IdentityDefinitionProjection.CopyStrings(
                source.TraitSummaryBorrowed,
                $"{path}.trait_summary"
            ),
            source.replaces_age_growth,
            source.suppresses_original_race_traits
        );
    }
}
