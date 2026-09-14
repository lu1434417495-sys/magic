#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class ProfessionIdentityJsonDomains
{
    internal const int SchemaVersion = 1;
    internal const string ProfessionDomain = "professions";
    internal const string RaceDomain = "races";
    internal const string SubraceDomain = "subraces";
    internal const string FaithDomain = "faith";
    internal const string AgeProfileDomain = "age_profiles";
    internal const string BloodlineDomain = "bloodlines";
    internal const string AscensionDomain = "ascensions";
    internal const string StageAdvancementDomain = "stage_advancements";

    internal const string ProfessionDirectory = "res://data/configs/json/professions";
    internal const string RaceDirectory = "res://data/configs/json/races";
    internal const string SubraceDirectory = "res://data/configs/json/subraces";
    internal const string FaithDirectory = "res://data/configs/json/faith";
    internal const string AgeProfileDirectory = "res://data/configs/json/age_profiles";
    internal const string BloodlineDirectory = "res://data/configs/json/bloodlines";
    internal const string AscensionDirectory = "res://data/configs/json/ascensions";
    internal const string StageAdvancementDirectory = "res://data/configs/json/stage_advancements";

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(
            new[]
            {
                Registration(ProfessionDomain, typeof(ProfessionJsonDocumentDto), "professions.schema.json", "profession_id"),
                Registration(RaceDomain, typeof(RaceJsonDocumentDto), "races.schema.json", "race_id"),
                Registration(SubraceDomain, typeof(SubraceJsonDocumentDto), "subraces.schema.json", "subrace_id"),
                Registration(FaithDomain, typeof(FaithJsonDocumentDto), "faith.schema.json", "deity_id"),
                Registration(AgeProfileDomain, typeof(AgeProfileJsonDocumentDto), "age_profiles.schema.json", "profile_id"),
                Registration(BloodlineDomain, typeof(BloodlineJsonDocumentDto), "bloodlines.schema.json", "entry_id"),
                Registration(AscensionDomain, typeof(AscensionJsonDocumentDto), "ascensions.schema.json", "entry_id"),
                Registration(StageAdvancementDomain, typeof(StageAdvancementJsonDocumentDto), "stage_advancements.schema.json", "modifier_id"),
            }
        );

    private static ContentJsonSchemaDomainRegistration Registration(
        string domain,
        Type documentType,
        string schemaFile,
        string entryId
    ) =>
        new(
            domain,
            SchemaVersion,
            documentType,
            $"Magic {domain} JSON authoring schema",
            $"Strict {domain} contract. Cross-domain relations are content IDs.",
            $"res://data/schemas/content/{schemaFile}",
            $"/data/configs/json/{domain}/**/*.json"
        );
}

internal static class ProfessionIdentityEmptyMap<T>
{
    internal static IReadOnlyDictionary<string, T> Value { get; } =
        new Dictionary<string, T>(StringComparer.Ordinal);
}

[Description("Profession JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.ProfessionDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))] public IReadOnlyDictionary<string, ProfessionJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<ProfessionJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "profession_id", "template")] public IReadOnlyList<ProfessionJsonDto> Entries { get; init; } = Array.Empty<ProfessionJsonDto>();
}

[Description("Race JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RaceJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.RaceDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))] public IReadOnlyDictionary<string, RaceJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<RaceJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "race_id", "template")] public IReadOnlyList<RaceJsonDto> Entries { get; init; } = Array.Empty<RaceJsonDto>();
}

[Description("Subrace JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SubraceJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.SubraceDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))] public IReadOnlyDictionary<string, SubraceJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<SubraceJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "subrace_id", "template")] public IReadOnlyList<SubraceJsonDto> Entries { get; init; } = Array.Empty<SubraceJsonDto>();
}

[Description("Faith JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class FaithJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.FaithDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))] public IReadOnlyDictionary<string, FaithDeityJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<FaithDeityJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "deity_id", "template")] public IReadOnlyList<FaithDeityJsonDto> Entries { get; init; } = Array.Empty<FaithDeityJsonDto>();
}

[Description("Age profile JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AgeProfileJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.AgeProfileDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))] public IReadOnlyDictionary<string, AgeProfileJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<AgeProfileJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "profile_id", "template")] public IReadOnlyList<AgeProfileJsonDto> Entries { get; init; } = Array.Empty<AgeProfileJsonDto>();
}

[Description("Bloodline and bloodline-stage JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BloodlineJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.BloodlineDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired] public IReadOnlyDictionary<string, BloodlineEntryJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<BloodlineEntryJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "entry_id", "template")] public IReadOnlyList<BloodlineEntryJsonDto> Entries { get; init; } = Array.Empty<BloodlineEntryJsonDto>();
}

[Description("Ascension and ascension-stage JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AscensionJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.AscensionDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired] public IReadOnlyDictionary<string, AscensionEntryJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<AscensionEntryJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "entry_id", "template")] public IReadOnlyList<AscensionEntryJsonDto> Entries { get; init; } = Array.Empty<AscensionEntryJsonDto>();
}

[Description("Stage advancement JSON document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class StageAdvancementJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(1)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(ProfessionIdentityJsonDomains.StageAdvancementDomain)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))] public IReadOnlyDictionary<string, StageAdvancementJsonDto> Templates { get; init; } = ProfessionIdentityEmptyMap<StageAdvancementJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "modifier_id", "template")] public IReadOnlyList<StageAdvancementJsonDto> Entries { get; init; } = Array.Empty<StageAdvancementJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class IdentityAttributeModifierJsonDto
{
    [JsonPropertyName("attribute_id"), JsonRequired] public string AttributeId { get; init; } = "";
    [JsonPropertyName("mode"), JsonRequired] public string Mode { get; init; } = "";
    [JsonPropertyName("value"), JsonRequired] public int Value { get; init; }
    [JsonPropertyName("value_per_rank"), JsonRequired] public int ValuePerRank { get; init; }
    [JsonPropertyName("source_type"), JsonRequired] public string SourceType { get; init; } = "";
    [JsonPropertyName("source_id"), JsonRequired] public string SourceId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RacialGrantedSkillJsonDto
{
    [JsonPropertyName("skill_id"), JsonRequired] public string SkillId { get; init; } = "";
    [JsonPropertyName("minimum_skill_level"), JsonRequired] public int MinimumSkillLevel { get; init; }
    [JsonPropertyName("charge_kind"), JsonRequired] public string ChargeKind { get; init; } = "";
    [JsonPropertyName("charges"), JsonRequired] public int Charges { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TagRequirementJsonDto
{
    [JsonPropertyName("tag"), JsonRequired] public string Tag { get; init; } = "";
    [JsonPropertyName("count"), JsonRequired] public int Count { get; init; }
    [JsonPropertyName("skill_state"), JsonRequired] public string SkillState { get; init; } = "";
    [JsonPropertyName("origin_filter"), JsonRequired] public string OriginFilter { get; init; } = "";
    [JsonPropertyName("selection_role"), JsonRequired] public string SelectionRole { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionRankGateJsonDto
{
    [JsonPropertyName("profession_id"), JsonRequired] public string ProfessionId { get; init; } = "";
    [JsonPropertyName("min_rank"), JsonRequired] public int MinRank { get; init; }
    [JsonPropertyName("check_mode"), JsonRequired] public string CheckMode { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AttributeRequirementJsonDto
{
    [JsonPropertyName("attribute_id"), JsonRequired] public string AttributeId { get; init; } = "";
    [JsonPropertyName("min_value"), JsonRequired] public int MinValue { get; init; }
    [JsonPropertyName("max_value"), JsonRequired] public int MaxValue { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ReputationRequirementJsonDto
{
    [JsonPropertyName("state_id"), JsonRequired] public string StateId { get; init; } = "";
    [JsonPropertyName("min_value"), JsonRequired] public int MinValue { get; init; }
    [JsonPropertyName("max_value"), JsonRequired] public int MaxValue { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionPromotionRequirementJsonDto
{
    [JsonPropertyName("required_skill_ids"), JsonRequired] public IReadOnlyList<string> RequiredSkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("required_tag_rules"), JsonRequired] public IReadOnlyList<TagRequirementJsonDto> RequiredTagRules { get; init; } = Array.Empty<TagRequirementJsonDto>();
    [JsonPropertyName("required_profession_ranks"), JsonRequired] public IReadOnlyList<ProfessionRankGateJsonDto> RequiredProfessionRanks { get; init; } = Array.Empty<ProfessionRankGateJsonDto>();
    [JsonPropertyName("required_attribute_rules"), JsonRequired] public IReadOnlyList<AttributeRequirementJsonDto> RequiredAttributeRules { get; init; } = Array.Empty<AttributeRequirementJsonDto>();
    [JsonPropertyName("required_reputation_rules"), JsonRequired] public IReadOnlyList<ReputationRequirementJsonDto> RequiredReputationRules { get; init; } = Array.Empty<ReputationRequirementJsonDto>();
    [JsonPropertyName("assigned_core_must_be_subset_of_qualifiers"), JsonRequired] public bool AssignedCoreMustBeSubsetOfQualifiers { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionRankRequirementJsonDto
{
    [JsonPropertyName("target_rank"), JsonRequired] public int TargetRank { get; init; }
    [JsonPropertyName("required_tag_rules"), JsonRequired] public IReadOnlyList<TagRequirementJsonDto> RequiredTagRules { get; init; } = Array.Empty<TagRequirementJsonDto>();
    [JsonPropertyName("required_profession_ranks"), JsonRequired] public IReadOnlyList<ProfessionRankGateJsonDto> RequiredProfessionRanks { get; init; } = Array.Empty<ProfessionRankGateJsonDto>();
    [JsonPropertyName("required_attribute_rules"), JsonRequired] public IReadOnlyList<AttributeRequirementJsonDto> RequiredAttributeRules { get; init; } = Array.Empty<AttributeRequirementJsonDto>();
    [JsonPropertyName("required_reputation_rules"), JsonRequired] public IReadOnlyList<ReputationRequirementJsonDto> RequiredReputationRules { get; init; } = Array.Empty<ReputationRequirementJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionGrantedSkillJsonDto
{
    [JsonPropertyName("skill_id"), JsonRequired] public string SkillId { get; init; } = "";
    [JsonPropertyName("unlock_rank"), JsonRequired] public int UnlockRank { get; init; }
    [JsonPropertyName("skill_type"), JsonRequired] public string SkillType { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionActiveConditionJsonDto
{
    [JsonPropertyName("condition_type"), JsonRequired] public string ConditionType { get; init; } = "";
    [JsonPropertyName("attribute_id"), JsonRequired] public string AttributeId { get; init; } = "";
    [JsonPropertyName("state_id"), JsonRequired] public string StateId { get; init; } = "";
    [JsonPropertyName("min_value"), JsonRequired] public int MinValue { get; init; }
    [JsonPropertyName("max_value"), JsonRequired] public int MaxValue { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ProfessionJsonDto
{
    [JsonPropertyName("profession_id"), JsonRequired] public string ProfessionId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("max_rank"), JsonRequired] public int MaxRank { get; init; }
    [JsonPropertyName("hit_die_sides"), JsonRequired] public int HitDieSides { get; init; }
    [JsonPropertyName("bab_progression"), JsonRequired] public string BabProgression { get; init; } = "";
    [JsonPropertyName("is_initial_profession"), JsonRequired] public bool IsInitialProfession { get; init; }
    [JsonPropertyName("unlock_knowledge_id"), JsonRequired] public string UnlockKnowledgeId { get; init; } = "";
    [JsonPropertyName("unlock_requirement"), JsonRequired] public ProfessionPromotionRequirementJsonDto? UnlockRequirement { get; init; }
    [JsonPropertyName("rank_requirements"), JsonRequired] public IReadOnlyList<ProfessionRankRequirementJsonDto> RankRequirements { get; init; } = Array.Empty<ProfessionRankRequirementJsonDto>();
    [JsonPropertyName("granted_skills"), JsonRequired] public IReadOnlyList<ProfessionGrantedSkillJsonDto> GrantedSkills { get; init; } = Array.Empty<ProfessionGrantedSkillJsonDto>();
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("active_conditions"), JsonRequired] public IReadOnlyList<ProfessionActiveConditionJsonDto> ActiveConditions { get; init; } = Array.Empty<ProfessionActiveConditionJsonDto>();
    [JsonPropertyName("reactivation_mode"), JsonRequired] public string ReactivationMode { get; init; } = "";
    [JsonPropertyName("dependency_visibility_mode"), JsonRequired] public string DependencyVisibilityMode { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RaceJsonDto
{
    [JsonPropertyName("race_id"), JsonRequired] public string RaceId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("age_profile_id"), JsonRequired] public string AgeProfileId { get; init; } = "";
    [JsonPropertyName("default_subrace_id"), JsonRequired] public string DefaultSubraceId { get; init; } = "";
    [JsonPropertyName("subrace_ids"), JsonRequired] public IReadOnlyList<string> SubraceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("body_size_category"), JsonRequired] public string BodySizeCategory { get; init; } = "";
    [JsonPropertyName("base_speed"), JsonRequired] public int BaseSpeed { get; init; }
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_granted_skills"), JsonRequired] public IReadOnlyList<RacialGrantedSkillJsonDto> RacialGrantedSkills { get; init; } = Array.Empty<RacialGrantedSkillJsonDto>();
    [JsonPropertyName("proficiency_tags"), JsonRequired] public IReadOnlyList<string> ProficiencyTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("vision_tags"), JsonRequired] public IReadOnlyList<string> VisionTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_advantage_tags"), JsonRequired] public IReadOnlyList<string> SaveAdvantageTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_disadvantage_tags"), JsonRequired] public IReadOnlyList<string> SaveDisadvantageTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_immunity_tags"), JsonRequired] public IReadOnlyList<string> SaveImmunityTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("damage_resistances"), JsonRequired] public IReadOnlyDictionary<string, string> DamageResistances { get; init; } = ProfessionIdentityEmptyMap<string>.Value;
    [JsonPropertyName("dialogue_tags"), JsonRequired] public IReadOnlyList<string> DialogueTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_trait_summary"), JsonRequired] public IReadOnlyList<string> RacialTraitSummary { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SubraceJsonDto
{
    [JsonPropertyName("subrace_id"), JsonRequired] public string SubraceId { get; init; } = "";
    [JsonPropertyName("parent_race_id"), JsonRequired] public string ParentRaceId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("body_size_category_override"), JsonRequired] public string BodySizeCategoryOverride { get; init; } = "";
    [JsonPropertyName("speed_bonus"), JsonRequired] public int SpeedBonus { get; init; }
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_granted_skills"), JsonRequired] public IReadOnlyList<RacialGrantedSkillJsonDto> RacialGrantedSkills { get; init; } = Array.Empty<RacialGrantedSkillJsonDto>();
    [JsonPropertyName("proficiency_tags"), JsonRequired] public IReadOnlyList<string> ProficiencyTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("vision_tags"), JsonRequired] public IReadOnlyList<string> VisionTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_advantage_tags"), JsonRequired] public IReadOnlyList<string> SaveAdvantageTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_disadvantage_tags"), JsonRequired] public IReadOnlyList<string> SaveDisadvantageTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_immunity_tags"), JsonRequired] public IReadOnlyList<string> SaveImmunityTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("damage_resistances"), JsonRequired] public IReadOnlyDictionary<string, string> DamageResistances { get; init; } = ProfessionIdentityEmptyMap<string>.Value;
    [JsonPropertyName("dialogue_tags"), JsonRequired] public IReadOnlyList<string> DialogueTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_trait_summary"), JsonRequired] public IReadOnlyList<string> RacialTraitSummary { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class FaithRankRewardJsonDto
{
    [JsonPropertyName("entry_type"), JsonRequired] public string EntryType { get; init; } = "";
    [JsonPropertyName("target_id"), JsonRequired] public string TargetId { get; init; } = "";
    [JsonPropertyName("amount"), JsonRequired] public int Amount { get; init; }
    [JsonPropertyName("target_label"), JsonRequired] public string TargetLabel { get; init; } = "";
    [JsonPropertyName("reason_text"), JsonRequired] public string ReasonText { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class FaithRankJsonDto
{
    [JsonPropertyName("rank_index"), JsonRequired] public int RankIndex { get; init; }
    [JsonPropertyName("rank_name"), JsonRequired] public string RankName { get; init; } = "";
    [JsonPropertyName("required_gold"), JsonRequired] public int RequiredGold { get; init; }
    [JsonPropertyName("required_level"), JsonRequired] public int RequiredLevel { get; init; }
    [JsonPropertyName("required_custom_stat_id"), JsonRequired] public string RequiredCustomStatId { get; init; } = "";
    [JsonPropertyName("required_custom_stat_min_value"), JsonRequired] public int RequiredCustomStatMinValue { get; init; }
    [JsonPropertyName("required_achievement_id"), JsonRequired] public string RequiredAchievementId { get; init; } = "";
    [JsonPropertyName("reward_entries"), JsonRequired] public IReadOnlyList<FaithRankRewardJsonDto> RewardEntries { get; init; } = Array.Empty<FaithRankRewardJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class FaithDeityJsonDto
{
    [JsonPropertyName("deity_id"), JsonRequired] public string DeityId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("facility_id"), JsonRequired] public string FacilityId { get; init; } = "";
    [JsonPropertyName("service_type_label"), JsonRequired] public string ServiceTypeLabel { get; init; } = "";
    [JsonPropertyName("power_domain_tags"), JsonRequired] public IReadOnlyList<string> PowerDomainTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("rank_progress_stat_id"), JsonRequired] public string RankProgressStatId { get; init; } = "";
    [JsonPropertyName("rank_defs"), JsonRequired] public IReadOnlyList<FaithRankJsonDto> RankDefs { get; init; } = Array.Empty<FaithRankJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AgeStageRuleJsonDto
{
    [JsonPropertyName("stage_id"), JsonRequired] public string StageId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("trait_summary"), JsonRequired] public IReadOnlyList<string> TraitSummary { get; init; } = Array.Empty<string>();
    [JsonPropertyName("selectable_in_creation"), JsonRequired] public bool SelectableInCreation { get; init; }
    [JsonPropertyName("reachable_by_aging"), JsonRequired] public bool ReachableByAging { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AgeProfileJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired] public string ProfileId { get; init; } = "";
    [JsonPropertyName("race_id"), JsonRequired] public string RaceId { get; init; } = "";
    [JsonPropertyName("child_age"), JsonRequired] public int ChildAge { get; init; }
    [JsonPropertyName("teen_age"), JsonRequired] public int TeenAge { get; init; }
    [JsonPropertyName("young_adult_age"), JsonRequired] public int YoungAdultAge { get; init; }
    [JsonPropertyName("adult_age"), JsonRequired] public int AdultAge { get; init; }
    [JsonPropertyName("middle_age"), JsonRequired] public int MiddleAge { get; init; }
    [JsonPropertyName("old_age"), JsonRequired] public int OldAge { get; init; }
    [JsonPropertyName("venerable_age"), JsonRequired] public int VenerableAge { get; init; }
    [JsonPropertyName("max_natural_age"), JsonRequired] public int MaxNaturalAge { get; init; }
    [JsonPropertyName("stage_rules"), JsonRequired] public IReadOnlyList<AgeStageRuleJsonDto> StageRules { get; init; } = Array.Empty<AgeStageRuleJsonDto>();
    [JsonPropertyName("creation_stage_ids"), JsonRequired] public IReadOnlyList<string> CreationStageIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("default_age_by_stage"), JsonRequired] public IReadOnlyDictionary<string, int> DefaultAgeByStage { get; init; } = ProfessionIdentityEmptyMap<int>.Value;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(BloodlineEntryClosedKindSchemaSpec))]
internal sealed class BloodlineEntryJsonDto
{
    [JsonPropertyName("entry_id"), JsonRequired] public string EntryId { get; init; } = "";
    [JsonPropertyName("kind"), JsonRequired] public string Kind { get; init; } = "";
    [JsonPropertyName("payload"), JsonRequired] public object Payload { get; init; } = null!;
}

internal sealed class BloodlineEntryClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } = Array.AsReadOnly(new[]
    {
        new ContentJsonSchemaClosedKindBranch("bloodline", typeof(BloodlineJsonDto)),
        new ContentJsonSchemaClosedKindBranch("stage", typeof(BloodlineStageJsonDto)),
    });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BloodlineJsonDto
{
    [JsonPropertyName("bloodline_id"), JsonRequired] public string BloodlineId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("stage_ids"), JsonRequired] public IReadOnlyList<string> StageIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_granted_skills"), JsonRequired] public IReadOnlyList<RacialGrantedSkillJsonDto> RacialGrantedSkills { get; init; } = Array.Empty<RacialGrantedSkillJsonDto>();
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("trait_summary"), JsonRequired] public IReadOnlyList<string> TraitSummary { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BloodlineStageJsonDto
{
    [JsonPropertyName("stage_id"), JsonRequired] public string StageId { get; init; } = "";
    [JsonPropertyName("bloodline_id"), JsonRequired] public string BloodlineId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_granted_skills"), JsonRequired] public IReadOnlyList<RacialGrantedSkillJsonDto> RacialGrantedSkills { get; init; } = Array.Empty<RacialGrantedSkillJsonDto>();
    [JsonPropertyName("trait_summary"), JsonRequired] public IReadOnlyList<string> TraitSummary { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(AscensionEntryClosedKindSchemaSpec))]
internal sealed class AscensionEntryJsonDto
{
    [JsonPropertyName("entry_id"), JsonRequired] public string EntryId { get; init; } = "";
    [JsonPropertyName("kind"), JsonRequired] public string Kind { get; init; } = "";
    [JsonPropertyName("payload"), JsonRequired] public object Payload { get; init; } = null!;
}

internal sealed class AscensionEntryClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } = Array.AsReadOnly(new[]
    {
        new ContentJsonSchemaClosedKindBranch("ascension", typeof(AscensionJsonDto)),
        new ContentJsonSchemaClosedKindBranch("stage", typeof(AscensionStageJsonDto)),
    });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AscensionJsonDto
{
    [JsonPropertyName("ascension_id"), JsonRequired] public string AscensionId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("stage_ids"), JsonRequired] public IReadOnlyList<string> StageIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_granted_skills"), JsonRequired] public IReadOnlyList<RacialGrantedSkillJsonDto> RacialGrantedSkills { get; init; } = Array.Empty<RacialGrantedSkillJsonDto>();
    [JsonPropertyName("allowed_race_ids"), JsonRequired] public IReadOnlyList<string> AllowedRaceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("allowed_subrace_ids"), JsonRequired] public IReadOnlyList<string> AllowedSubraceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("allowed_bloodline_ids"), JsonRequired] public IReadOnlyList<string> AllowedBloodlineIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("trait_summary"), JsonRequired] public IReadOnlyList<string> TraitSummary { get; init; } = Array.Empty<string>();
    [JsonPropertyName("replaces_age_growth"), JsonRequired] public bool ReplacesAgeGrowth { get; init; }
    [JsonPropertyName("suppresses_original_race_traits"), JsonRequired] public bool SuppressesOriginalRaceTraits { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AscensionStageJsonDto
{
    [JsonPropertyName("stage_id"), JsonRequired] public string StageId { get; init; } = "";
    [JsonPropertyName("ascension_id"), JsonRequired] public string AscensionId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("attribute_modifiers"), JsonRequired] public IReadOnlyList<IdentityAttributeModifierJsonDto> AttributeModifiers { get; init; } = Array.Empty<IdentityAttributeModifierJsonDto>();
    [JsonPropertyName("trait_ids"), JsonRequired] public IReadOnlyList<string> TraitIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("racial_granted_skills"), JsonRequired] public IReadOnlyList<RacialGrantedSkillJsonDto> RacialGrantedSkills { get; init; } = Array.Empty<RacialGrantedSkillJsonDto>();
    [JsonPropertyName("body_size_category_override"), JsonRequired] public string BodySizeCategoryOverride { get; init; } = "";
    [JsonPropertyName("trait_summary"), JsonRequired] public IReadOnlyList<string> TraitSummary { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class StageAdvancementJsonDto
{
    [JsonPropertyName("modifier_id"), JsonRequired] public string ModifierId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("target_axis"), JsonRequired] public string TargetAxis { get; init; } = "";
    [JsonPropertyName("stage_offset"), JsonRequired] public int StageOffset { get; init; }
    [JsonPropertyName("max_stage_id"), JsonRequired] public string MaxStageId { get; init; } = "";
    [JsonPropertyName("applies_to_race_ids"), JsonRequired] public IReadOnlyList<string> AppliesToRaceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("applies_to_subrace_ids"), JsonRequired] public IReadOnlyList<string> AppliesToSubraceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("applies_to_bloodline_ids"), JsonRequired] public IReadOnlyList<string> AppliesToBloodlineIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("applies_to_ascension_ids"), JsonRequired] public IReadOnlyList<string> AppliesToAscensionIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("grants_attributes"), JsonRequired] public bool GrantsAttributes { get; init; }
    [JsonPropertyName("grants_traits"), JsonRequired] public bool GrantsTraits { get; init; }
    [JsonPropertyName("grants_body_size_change"), JsonRequired] public bool GrantsBodySizeChange { get; init; }
}

[JsonSerializable(typeof(ProfessionJsonDto))]
[JsonSerializable(typeof(RaceJsonDto))]
[JsonSerializable(typeof(SubraceJsonDto))]
[JsonSerializable(typeof(FaithDeityJsonDto))]
[JsonSerializable(typeof(AgeProfileJsonDto))]
[JsonSerializable(typeof(BloodlineEntryJsonDto))]
[JsonSerializable(typeof(BloodlineJsonDto))]
[JsonSerializable(typeof(BloodlineStageJsonDto))]
[JsonSerializable(typeof(AscensionEntryJsonDto))]
[JsonSerializable(typeof(AscensionJsonDto))]
[JsonSerializable(typeof(AscensionStageJsonDto))]
[JsonSerializable(typeof(StageAdvancementJsonDto))]
internal partial class ProfessionIdentityJsonSerializerContext : JsonSerializerContext { }
