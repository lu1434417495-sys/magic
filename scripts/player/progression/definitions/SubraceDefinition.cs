using System.Collections.Generic;
using Godot;

public sealed class SubraceDefinition
{
    public SubraceDefinition(
        StringName subraceId,
        StringName parentRaceId,
        string displayName,
        string description,
        StringName bodySizeCategoryOverride,
        int speedBonus,
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
        SubraceId = subraceId;
        ParentRaceId = parentRaceId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "SubraceDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "SubraceDefinition.Description"
        );
        BodySizeCategoryOverride = bodySizeCategoryOverride;
        SpeedBonus = speedBonus;
        AttributeModifiers = IdentityDefinitionProjection.FreezeList(
            attributeModifiers,
            "SubraceDefinition.AttributeModifiers"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "SubraceDefinition.TraitIds"
        );
        RacialGrantedSkills = IdentityDefinitionProjection.FreezeList(
            racialGrantedSkills,
            "SubraceDefinition.RacialGrantedSkills"
        );
        ProficiencyTags = IdentityDefinitionProjection.FreezeList(
            proficiencyTags,
            "SubraceDefinition.ProficiencyTags"
        );
        VisionTags = IdentityDefinitionProjection.FreezeList(
            visionTags,
            "SubraceDefinition.VisionTags"
        );
        SaveAdvantageTags = IdentityDefinitionProjection.FreezeList(
            saveAdvantageTags,
            "SubraceDefinition.SaveAdvantageTags"
        );
        SaveDisadvantageTags = IdentityDefinitionProjection.FreezeList(
            saveDisadvantageTags,
            "SubraceDefinition.SaveDisadvantageTags"
        );
        SaveImmunityTags = IdentityDefinitionProjection.FreezeList(
            saveImmunityTags,
            "SubraceDefinition.SaveImmunityTags"
        );
        DamageResistances = IdentityDefinitionProjection.FreezeStringNameMap(
            damageResistances,
            "SubraceDefinition.DamageResistances"
        );
        DialogueTags = IdentityDefinitionProjection.FreezeList(
            dialogueTags,
            "SubraceDefinition.DialogueTags"
        );
        RacialTraitSummary = IdentityDefinitionProjection.FreezeList(
            racialTraitSummary,
            "SubraceDefinition.RacialTraitSummary"
        );
    }

    public StringName SubraceId { get; }
    public StringName ParentRaceId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public StringName BodySizeCategoryOverride { get; }
    public int SpeedBonus { get; }
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
