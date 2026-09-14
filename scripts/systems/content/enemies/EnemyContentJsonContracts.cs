#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class EnemyContentJsonDomains
{
    internal const int SchemaVersion = 1;
    internal const string BrainDomainId = "enemy_ai_brains";
    internal const string TemplateDomainId = "enemy_templates";
    internal const string RosterDomainId = "encounter_rosters";
    internal const string BrainDirectory = "res://data/configs/json/enemies/brains";
    internal const string TemplateDirectory = "res://data/configs/json/enemies/templates";
    internal const string RosterDirectory = "res://data/configs/json/enemies/rosters";

    internal static ContentJsonSchemaDomainRegistration BrainSchemaRegistration { get; } =
        new(
            BrainDomainId,
            SchemaVersion,
            typeof(EnemyAiBrainJsonDocumentDto),
            "Magic enemy AI brain JSON authoring schema",
            "Strict enemy AI brain contract with closed 12-kind action payloads.",
            "res://data/schemas/content/enemy_ai_brains.schema.json",
            "/data/configs/json/enemies/brains/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration TemplateSchemaRegistration { get; } =
        new(
            TemplateDomainId,
            SchemaVersion,
            typeof(EnemyTemplateJsonDocumentDto),
            "Magic enemy template JSON authoring schema",
            "Strict enemy template contract using content IDs and battle sprite asset IDs.",
            "res://data/schemas/content/enemy_templates.schema.json",
            "/data/configs/json/enemies/templates/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration RosterSchemaRegistration { get; } =
        new(
            RosterDomainId,
            SchemaVersion,
            typeof(EncounterRosterJsonDocumentDto),
            "Magic encounter roster JSON authoring schema",
            "Strict staged encounter roster contract referencing enemy template IDs.",
            "res://data/schemas/content/encounter_rosters.schema.json",
            "/data/configs/json/enemies/rosters/**/*.json"
        );
}

internal static class EnemyContentJsonRules
{
    internal const string InvalidBrainDto = "enemy.brain.dto.invalid";
    internal const string InvalidTemplateDto = "enemy.template.dto.invalid";
    internal const string InvalidRosterDto = "enemy.roster.dto.invalid";
    internal const string UnknownActionKind = "enemy.brain.action.unknown_kind";
    internal const string InvalidActionPayload = "enemy.brain.action.invalid_payload";
}

internal abstract class EnemyJsonDocumentDto<TEntry>
{
    public abstract int Schema { get; init; }
    public abstract string Domain { get; init; }
    public abstract string Family { get; init; }
    public abstract IReadOnlyDictionary<string, TEntry> Templates { get; init; }
    public abstract IReadOnlyList<TEntry> Entries { get; init; }
}

[Description("Enemy AI brain document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyAiBrainJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(EnemyContentJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(EnemyContentJsonDomains.BrainDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, EnemyAiBrainJsonDto> Templates { get; init; } =
        EmptyMap<EnemyAiBrainJsonDto>.Value;

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "brain_id",
        "template"
    )]
    public IReadOnlyList<EnemyAiBrainJsonDto> Entries { get; init; } =
        Array.Empty<EnemyAiBrainJsonDto>();
}

[Description("Enemy template document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyTemplateJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(EnemyContentJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(EnemyContentJsonDomains.TemplateDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, EnemyTemplateJsonDto> Templates { get; init; } =
        EmptyMap<EnemyTemplateJsonDto>.Value;

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "template_id",
        "template"
    )]
    public IReadOnlyList<EnemyTemplateJsonDto> Entries { get; init; } =
        Array.Empty<EnemyTemplateJsonDto>();
}

[Description("Encounter roster document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EncounterRosterJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(EnemyContentJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(EnemyContentJsonDomains.RosterDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, EncounterRosterJsonDto> Templates { get; init; } =
        EmptyMap<EncounterRosterJsonDto>.Value;

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "profile_id",
        "template"
    )]
    public IReadOnlyList<EncounterRosterJsonDto> Entries { get; init; } =
        Array.Empty<EncounterRosterJsonDto>();
}

internal static class EmptyMap<T>
{
    internal static IReadOnlyDictionary<string, T> Value { get; } =
        new ReadOnlyDictionary<string, T>(new Dictionary<string, T>());
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyAiBrainJsonDto
{
    [JsonPropertyName("brain_id"), JsonRequired]
    public string BrainId { get; init; } = "";

    [JsonPropertyName("default_state_id"), JsonRequired]
    public string DefaultStateId { get; init; } = "";

    [JsonPropertyName("score_profile"), JsonRequired]
    public BattleAiScoreProfileJsonDto? ScoreProfile { get; init; }

    [JsonPropertyName("states"), JsonRequired]
    public IReadOnlyList<EnemyAiStateJsonDto> States { get; init; } =
        Array.Empty<EnemyAiStateJsonDto>();

    [JsonPropertyName("transition_rules"), JsonRequired]
    public IReadOnlyList<EnemyAiTransitionRuleJsonDto> TransitionRules { get; init; } =
        Array.Empty<EnemyAiTransitionRuleJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyAiStateJsonDto
{
    [JsonPropertyName("state_id"), JsonRequired]
    public string StateId { get; init; } = "";

    [JsonPropertyName("actions"), JsonRequired]
    public IReadOnlyList<EnemyAiActionJsonDto> Actions { get; init; } =
        Array.Empty<EnemyAiActionJsonDto>();

    [JsonPropertyName("generation_slots"), JsonRequired]
    public IReadOnlyList<EnemyAiGenerationSlotJsonDto> GenerationSlots { get; init; } =
        Array.Empty<EnemyAiGenerationSlotJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(EnemyAiActionClosedKindSchemaSpec))]
internal sealed class EnemyAiActionJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class EnemyAiActionClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch("move_to_advantage_position", typeof(MoveToAdvantagePositionActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("move_to_multi_unit_skill_position", typeof(MoveToMultiUnitSkillPositionActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("move_to_range", typeof(MoveToRangeActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("retreat", typeof(RetreatActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_charge", typeof(UseChargeActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_charge_path_aoe", typeof(UseChargePathAoeActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_ground_reposition_skill", typeof(UseGroundRepositionSkillActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_ground_skill", typeof(UseGroundSkillActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_multi_unit_skill", typeof(UseMultiUnitSkillActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_random_chain_skill", typeof(UseRandomChainSkillActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("use_unit_skill", typeof(UseUnitSkillActionPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("wait", typeof(WaitActionPayloadJsonDto)),
            }
        );
}

internal abstract class EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("action_id"), JsonRequired]
    public string ActionId { get; init; } = "";

    [JsonPropertyName("score_bucket_id"), JsonRequired]
    public string ScoreBucketId { get; init; } = "";

    [JsonPropertyName("action_intent"), JsonRequired]
    public string ActionIntent { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MoveToAdvantagePositionActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("range_skill_ids"), JsonRequired] public IReadOnlyList<string> RangeSkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("minimum_safe_distance"), JsonRequired] public int MinimumSafeDistance { get; init; }
    [JsonPropertyName("safe_distance_margin"), JsonRequired] public int SafeDistanceMargin { get; init; }
    [JsonPropertyName("min_survival_margin_gain_to_escape"), JsonRequired] public int MinSurvivalMarginGainToEscape { get; init; }
    [JsonPropertyName("min_distance_progress_when_beyond_band"), JsonRequired] public int MinDistanceProgressWhenBeyondBand { get; init; }
    [JsonPropertyName("positioning_mode"), JsonRequired] public string PositioningMode { get; init; } = "";
    [JsonPropertyName("high_ground_weight"), JsonRequired] public int HighGroundWeight { get; init; }
    [JsonPropertyName("safety_weight"), JsonRequired] public int SafetyWeight { get; init; }
    [JsonPropertyName("distance_band_weight"), JsonRequired] public int DistanceBandWeight { get; init; }
    [JsonPropertyName("candidate_limit"), JsonRequired] public int CandidateLimit { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal class UseMultiUnitSkillActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("distance_reference"), JsonRequired] public string DistanceReference { get; init; } = "";
    [JsonPropertyName("candidate_pool_limit"), JsonRequired] public int CandidatePoolLimit { get; init; }
    [JsonPropertyName("candidate_group_limit"), JsonRequired] public int CandidateGroupLimit { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MoveToMultiUnitSkillPositionActionPayloadJsonDto : UseMultiUnitSkillActionPayloadJsonDto
{
    [JsonPropertyName("target_count_weight"), JsonRequired] public int TargetCountWeight { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MoveToRangeActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("ai_evaluation_mode"), JsonRequired] public string AiEvaluationMode { get; init; } = "";
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("range_skill_ids"), JsonRequired] public IReadOnlyList<string> RangeSkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("screening_mode"), JsonRequired] public string ScreeningMode { get; init; } = "";
    [JsonPropertyName("enable_aoe_setup_positioning"), JsonRequired] public bool EnableAoeSetupPositioning { get; init; }
    [JsonPropertyName("aoe_setup_min_target_count"), JsonRequired] public int AoeSetupMinTargetCount { get; init; }
    [JsonPropertyName("aoe_setup_target_count_weight"), JsonRequired] public int AoeSetupTargetCountWeight { get; init; }
    [JsonPropertyName("aoe_setup_improvement_weight"), JsonRequired] public int AoeSetupImprovementWeight { get; init; }
    [JsonPropertyName("aoe_setup_friendly_fire_penalty"), JsonRequired] public int AoeSetupFriendlyFirePenalty { get; init; }
    [JsonPropertyName("screening_min_hp_basis_points"), JsonRequired] public int ScreeningMinHpBasisPoints { get; init; }
    [JsonPropertyName("screening_ally_min_attack_range"), JsonRequired] public int ScreeningAllyMinAttackRange { get; init; }
    [JsonPropertyName("screening_enemy_max_contact_range"), JsonRequired] public int ScreeningEnemyMaxContactRange { get; init; }
    [JsonPropertyName("screening_threat_distance_buffer"), JsonRequired] public int ScreeningThreatDistanceBuffer { get; init; }
    [JsonPropertyName("screening_path_bonus"), JsonRequired] public int ScreeningPathBonus { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RetreatActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("minimum_safe_distance"), JsonRequired] public int MinimumSafeDistance { get; init; }
    [JsonPropertyName("use_dynamic_threat_safe_distance"), JsonRequired] public bool UseDynamicThreatSafeDistance { get; init; }
    [JsonPropertyName("safe_distance_margin"), JsonRequired] public int SafeDistanceMargin { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class UseChargeActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_id"), JsonRequired] public string SkillId { get; init; } = "";
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("minimum_charge_move_distance"), JsonRequired] public int MinimumChargeMoveDistance { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class UseChargePathAoeActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("minimum_hit_count"), JsonRequired] public int MinimumHitCount { get; init; }
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class UseGroundRepositionSkillActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("positioning_mode"), JsonRequired] public string PositioningMode { get; init; } = "";
    [JsonPropertyName("high_ground_weight"), JsonRequired] public int HighGroundWeight { get; init; }
    [JsonPropertyName("minimum_safe_distance"), JsonRequired] public int MinimumSafeDistance { get; init; }
    [JsonPropertyName("safe_distance_margin"), JsonRequired] public int SafeDistanceMargin { get; init; }
    [JsonPropertyName("desired_max_distance_bonus"), JsonRequired] public int DesiredMaxDistanceBonus { get; init; }
    [JsonPropertyName("action_base_score"), JsonRequired] public int ActionBaseScore { get; init; }
    [JsonPropertyName("min_survival_margin_gain_to_escape"), JsonRequired] public int MinSurvivalMarginGainToEscape { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class UseGroundSkillActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("minimum_hit_count"), JsonRequired] public int MinimumHitCount { get; init; }
    [JsonPropertyName("allow_empty_ground_control"), JsonRequired] public bool AllowEmptyGroundControl { get; init; }
    [JsonPropertyName("allow_ground_control_supplement_partial_hits"), JsonRequired] public bool AllowGroundControlSupplementPartialHits { get; init; }
    [JsonPropertyName("minimum_ground_control_score"), JsonRequired] public int MinimumGroundControlScore { get; init; }
    [JsonPropertyName("minimum_ally_threat_hit_count"), JsonRequired] public int MinimumAllyThreatHitCount { get; init; }
    [JsonPropertyName("maximum_friendly_fire_target_count"), JsonRequired] public int MaximumFriendlyFireTargetCount { get; init; }
    [JsonPropertyName("allow_friendly_lethal"), JsonRequired] public bool AllowFriendlyLethal { get; init; }
    [JsonPropertyName("threat_minimum_safe_distance"), JsonRequired] public int ThreatMinimumSafeDistance { get; init; }
    [JsonPropertyName("threat_safe_distance_margin"), JsonRequired] public int ThreatSafeDistanceMargin { get; init; }
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("distance_reference"), JsonRequired] public string DistanceReference { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class UseRandomChainSkillActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("distance_reference"), JsonRequired] public string DistanceReference { get; init; } = "";
    [JsonPropertyName("minimum_candidate_count"), JsonRequired] public int MinimumCandidateCount { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class UseUnitSkillActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("minimum_effective_target_count"), JsonRequired] public int MinimumEffectiveTargetCount { get; init; }
    [JsonPropertyName("maximum_friendly_fire_target_count"), JsonRequired] public int MaximumFriendlyFireTargetCount { get; init; }
    [JsonPropertyName("allow_friendly_lethal"), JsonRequired] public bool AllowFriendlyLethal { get; init; }
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("distance_reference"), JsonRequired] public string DistanceReference { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WaitActionPayloadJsonDto : EnemyAiActionPayloadJsonDto
{
    [JsonPropertyName("active_rest_action_base_score"), JsonRequired] public int ActiveRestActionBaseScore { get; init; }
    [JsonPropertyName("active_rest_min_stamina_residue"), JsonRequired] public int ActiveRestMinStaminaResidue { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyAiGenerationSlotJsonDto
{
    [JsonPropertyName("slot_id"), JsonRequired] public string SlotId { get; init; } = "";
    [JsonPropertyName("slot_role"), JsonRequired] public string SlotRole { get; init; } = "";
    [JsonPropertyName("order"), JsonRequired] public int Order { get; init; }
    [JsonPropertyName("allowed_affordances"), JsonRequired] public IReadOnlyList<string> AllowedAffordances { get; init; } = Array.Empty<string>();
    [JsonPropertyName("action_families"), JsonRequired] public IReadOnlyList<string> ActionFamilies { get; init; } = Array.Empty<string>();
    [JsonPropertyName("style_template_action_id"), JsonRequired] public string StyleTemplateActionId { get; init; } = "";
    [JsonPropertyName("score_bucket_id"), JsonRequired] public string ScoreBucketId { get; init; } = "";
    [JsonPropertyName("target_selector"), JsonRequired] public string TargetSelector { get; init; } = "";
    [JsonPropertyName("desired_min_distance"), JsonRequired] public int DesiredMinDistance { get; init; }
    [JsonPropertyName("desired_max_distance"), JsonRequired] public int DesiredMaxDistance { get; init; }
    [JsonPropertyName("distance_reference"), JsonRequired] public string DistanceReference { get; init; } = "";
    [JsonPropertyName("suppression_policy"), JsonRequired] public string SuppressionPolicy { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyAiTransitionRuleJsonDto
{
    [JsonPropertyName("rule_id"), JsonRequired] public string RuleId { get; init; } = "";
    [JsonPropertyName("order"), JsonRequired] public int Order { get; init; }
    [JsonPropertyName("from_state_ids"), JsonRequired] public IReadOnlyList<string> FromStateIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("target_state_id"), JsonRequired] public string TargetStateId { get; init; } = "";
    [JsonPropertyName("conditions"), JsonRequired] public IReadOnlyList<EnemyAiTransitionConditionJsonDto> Conditions { get; init; } = Array.Empty<EnemyAiTransitionConditionJsonDto>();
    [JsonPropertyName("designer_note"), JsonRequired] public string DesignerNote { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyAiTransitionConditionJsonDto
{
    [JsonPropertyName("predicate"), JsonRequired] public string Predicate { get; init; } = "";
    [JsonPropertyName("basis_points"), JsonRequired] public int BasisPoints { get; init; }
    [JsonPropertyName("max_distance"), JsonRequired] public int MaxDistance { get; init; }
    [JsonPropertyName("state_ids"), JsonRequired] public IReadOnlyList<string> StateIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("affordances"), JsonRequired] public IReadOnlyList<string> Affordances { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyTemplateJsonDto
{
    [JsonPropertyName("template_id"), JsonRequired] public string TemplateId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("battle_sprite_asset_id"), JsonRequired] public string BattleSpriteAssetId { get; init; } = "";
    [JsonPropertyName("brain_id"), JsonRequired] public string BrainId { get; init; } = "";
    [JsonPropertyName("initial_state_id"), JsonRequired] public string InitialStateId { get; init; } = "";
    [JsonPropertyName("enemy_count"), JsonRequired] public int EnemyCount { get; init; }
    [JsonPropertyName("body_size"), JsonRequired] public int BodySize { get; init; }
    [JsonPropertyName("creature_level"), JsonRequired] public int CreatureLevel { get; init; }
    [JsonPropertyName("hit_die_sides"), JsonRequired] public int HitDieSides { get; init; }
    [JsonPropertyName("cognition_kind"), JsonRequired] public string CognitionKind { get; init; } = "";
    [JsonPropertyName("tags"), JsonRequired] public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_advantage_tags"), JsonRequired] public IReadOnlyList<string> SaveAdvantageTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_disadvantage_tags"), JsonRequired] public IReadOnlyList<string> SaveDisadvantageTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("save_immunity_tags"), JsonRequired] public IReadOnlyList<string> SaveImmunityTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("damage_resistances"), JsonRequired] public IReadOnlyDictionary<string, string> DamageResistances { get; init; } = EmptyMap<string>.Value;
    [JsonPropertyName("attack_equipment_item_id"), JsonRequired] public string AttackEquipmentItemId { get; init; } = "";
    [JsonPropertyName("battle_equipment_entries"), JsonRequired] public IReadOnlyList<EnemyBattleEquipmentJsonDto> BattleEquipmentEntries { get; init; } = Array.Empty<EnemyBattleEquipmentJsonDto>();
    [JsonPropertyName("natural_weapon_damage_tag"), JsonRequired] public string NaturalWeaponDamageTag { get; init; } = "";
    [JsonPropertyName("natural_weapon_attack_range"), JsonRequired] public int NaturalWeaponAttackRange { get; init; }
    [JsonPropertyName("base_attribute_overrides"), JsonRequired] public IReadOnlyDictionary<string, int> BaseAttributeOverrides { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("skill_level_map"), JsonRequired] public IReadOnlyDictionary<string, int> SkillLevelMap { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("generated_core_skill_count"), JsonRequired] public int GeneratedCoreSkillCount { get; init; }
    [JsonPropertyName("attribute_overrides"), JsonRequired] public IReadOnlyDictionary<string, int> AttributeOverrides { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("target_rank"), JsonRequired] public string TargetRank { get; init; } = "";
    [JsonPropertyName("drop_entries"), JsonRequired] public IReadOnlyList<EnemyDropEntryJsonDto> DropEntries { get; init; } = Array.Empty<EnemyDropEntryJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyBattleEquipmentJsonDto
{
    [JsonPropertyName("slot_id"), JsonRequired] public string SlotId { get; init; } = "";
    [JsonPropertyName("item_id"), JsonRequired] public string ItemId { get; init; } = "";
    [JsonPropertyName("rarity"), JsonRequired] public int Rarity { get; init; }
    [JsonPropertyName("current_durability"), JsonRequired] public int CurrentDurability { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EnemyDropEntryJsonDto
{
    [JsonPropertyName("drop_entry_id"), JsonRequired] public string DropEntryId { get; init; } = "";
    [JsonPropertyName("drop_type"), JsonRequired] public string DropType { get; init; } = "";
    [JsonPropertyName("item_id"), JsonRequired] public string ItemId { get; init; } = "";
    [JsonPropertyName("quantity"), JsonRequired] public int Quantity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EncounterRosterJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired] public string ProfileId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("initial_stage"), JsonRequired] public int InitialStage { get; init; }
    [JsonPropertyName("growth_step_interval"), JsonRequired] public int GrowthStepInterval { get; init; }
    [JsonPropertyName("stages"), JsonRequired] public IReadOnlyList<EncounterRosterStageJsonDto> Stages { get; init; } = Array.Empty<EncounterRosterStageJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EncounterRosterStageJsonDto
{
    [JsonPropertyName("stage"), JsonRequired] public int Stage { get; init; }
    [JsonPropertyName("unit_entries"), JsonRequired] public IReadOnlyList<EncounterRosterUnitJsonDto> UnitEntries { get; init; } = Array.Empty<EncounterRosterUnitJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EncounterRosterUnitJsonDto
{
    [JsonPropertyName("template_id"), JsonRequired] public string TemplateId { get; init; } = "";
    [JsonPropertyName("actor_id"), JsonRequired] public string ActorId { get; init; } = "";
    [JsonPropertyName("count"), JsonRequired] public int Count { get; init; }
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleAiScoreProfileJsonDto
{
    [JsonPropertyName("damage_weight"), JsonRequired] public int DamageWeight { get; init; }
    [JsonPropertyName("heal_weight"), JsonRequired] public int HealWeight { get; init; }
    [JsonPropertyName("status_weight"), JsonRequired] public int StatusWeight { get; init; }
    [JsonPropertyName("terrain_weight"), JsonRequired] public int TerrainWeight { get; init; }
    [JsonPropertyName("height_weight"), JsonRequired] public int HeightWeight { get; init; }
    [JsonPropertyName("lethal_target_weight"), JsonRequired] public int LethalTargetWeight { get; init; }
    [JsonPropertyName("lethal_threat_target_weight"), JsonRequired] public int LethalThreatTargetWeight { get; init; }
    [JsonPropertyName("target_count_weight"), JsonRequired] public int TargetCountWeight { get; init; }
    [JsonPropertyName("friendly_fire_damage_weight"), JsonRequired] public int FriendlyFireDamageWeight { get; init; }
    [JsonPropertyName("friendly_fire_target_weight"), JsonRequired] public int FriendlyFireTargetWeight { get; init; }
    [JsonPropertyName("friendly_control_target_weight"), JsonRequired] public int FriendlyControlTargetWeight { get; init; }
    [JsonPropertyName("friendly_lethal_target_weight"), JsonRequired] public int FriendlyLethalTargetWeight { get; init; }
    [JsonPropertyName("ap_cost_weight"), JsonRequired] public int ApCostWeight { get; init; }
    [JsonPropertyName("mp_cost_weight"), JsonRequired] public int MpCostWeight { get; init; }
    [JsonPropertyName("stamina_cost_weight"), JsonRequired] public int StaminaCostWeight { get; init; }
    [JsonPropertyName("aura_cost_weight"), JsonRequired] public int AuraCostWeight { get; init; }
    [JsonPropertyName("cooldown_weight"), JsonRequired] public int CooldownWeight { get; init; }
    [JsonPropertyName("delayed_resolution_cost_per_5_tu"), JsonRequired] public int DelayedResolutionCostPer5Tu { get; init; }
    [JsonPropertyName("movement_cost_weight"), JsonRequired] public int MovementCostWeight { get; init; }
    [JsonPropertyName("mp_reserve_floor_bp"), JsonRequired] public int MpReserveFloorBp { get; init; }
    [JsonPropertyName("mp_reserve_pressure_weight"), JsonRequired] public int MpReservePressureWeight { get; init; }
    [JsonPropertyName("mp_reserve_breach_penalty"), JsonRequired] public int MpReserveBreachPenalty { get; init; }
    [JsonPropertyName("stamina_reserve_floor_bp"), JsonRequired] public int StaminaReserveFloorBp { get; init; }
    [JsonPropertyName("stamina_reserve_pressure_weight"), JsonRequired] public int StaminaReservePressureWeight { get; init; }
    [JsonPropertyName("stamina_reserve_breach_penalty"), JsonRequired] public int StaminaReserveBreachPenalty { get; init; }
    [JsonPropertyName("aura_reserve_floor_bp"), JsonRequired] public int AuraReserveFloorBp { get; init; }
    [JsonPropertyName("aura_reserve_pressure_weight"), JsonRequired] public int AuraReservePressureWeight { get; init; }
    [JsonPropertyName("aura_reserve_breach_penalty"), JsonRequired] public int AuraReserveBreachPenalty { get; init; }
    [JsonPropertyName("resource_conservation_weight"), JsonRequired] public int ResourceConservationWeight { get; init; }
    [JsonPropertyName("position_base_score"), JsonRequired] public int PositionBaseScore { get; init; }
    [JsonPropertyName("position_distance_step"), JsonRequired] public int PositionDistanceStep { get; init; }
    [JsonPropertyName("position_undershoot_penalty"), JsonRequired] public int PositionUndershootPenalty { get; init; }
    [JsonPropertyName("position_overshoot_penalty"), JsonRequired] public int PositionOvershootPenalty { get; init; }
    [JsonPropertyName("survival_margin_gain_weight"), JsonRequired] public int SurvivalMarginGainWeight { get; init; }
    [JsonPropertyName("post_action_threat_damage_weight"), JsonRequired] public int PostActionThreatDamageWeight { get; init; }
    [JsonPropertyName("post_action_threat_count_weight"), JsonRequired] public int PostActionThreatCountWeight { get; init; }
    [JsonPropertyName("lethal_survival_risk_penalty"), JsonRequired] public int LethalSurvivalRiskPenalty { get; init; }
    [JsonPropertyName("incoming_threat_relief_weight"), JsonRequired] public int IncomingThreatReliefWeight { get; init; }
    [JsonPropertyName("low_hp_urgency_threshold_bp"), JsonRequired] public int LowHpUrgencyThresholdBp { get; init; }
    [JsonPropertyName("low_hp_urgency_weight"), JsonRequired] public int LowHpUrgencyWeight { get; init; }
    [JsonPropertyName("execute_target_hp_threshold_bp"), JsonRequired] public int ExecuteTargetHpThresholdBp { get; init; }
    [JsonPropertyName("execute_bonus_weight"), JsonRequired] public int ExecuteBonusWeight { get; init; }
    [JsonPropertyName("overkill_damage_penalty_weight"), JsonRequired] public int OverkillDamagePenaltyWeight { get; init; }
    [JsonPropertyName("role_threat_min_effective_range"), JsonRequired] public int RoleThreatMinEffectiveRange { get; init; }
    [JsonPropertyName("role_threat_distance_window"), JsonRequired] public int RoleThreatDistanceWindow { get; init; }
    [JsonPropertyName("role_threat_max_approach_distance"), JsonRequired] public int RoleThreatMaxApproachDistance { get; init; }
    [JsonPropertyName("role_threat_max_contact_range"), JsonRequired] public int RoleThreatMaxContactRange { get; init; }
    [JsonPropertyName("role_threat_in_range_score_step"), JsonRequired] public int RoleThreatInRangeScoreStep { get; init; }
    [JsonPropertyName("enemy_target_count_weight"), JsonRequired] public int EnemyTargetCountWeight { get; init; }
    [JsonPropertyName("chain_enemy_target_weight"), JsonRequired] public int ChainEnemyTargetWeight { get; init; }
    [JsonPropertyName("focus_fire_wounded_target_weight"), JsonRequired] public int FocusFireWoundedTargetWeight { get; init; }
    [JsonPropertyName("hit_rate_reliability_weight"), JsonRequired] public int HitRateReliabilityWeight { get; init; }
    [JsonPropertyName("save_reliable_damage_weight"), JsonRequired] public int SaveReliableDamageWeight { get; init; }
    [JsonPropertyName("shield_absorbed_weight"), JsonRequired] public int ShieldAbsorbedWeight { get; init; }
    [JsonPropertyName("control_weight"), JsonRequired] public int ControlWeight { get; init; }
    [JsonPropertyName("ground_control_weight"), JsonRequired] public int GroundControlWeight { get; init; }
    [JsonPropertyName("status_redundancy_penalty"), JsonRequired] public int StatusRedundancyPenalty { get; init; }
    [JsonPropertyName("position_objective_weight"), JsonRequired] public int PositionObjectiveWeight { get; init; }
    [JsonPropertyName("safe_distance_adherence_weight"), JsonRequired] public int SafeDistanceAdherenceWeight { get; init; }
    [JsonPropertyName("threat_healer_bias_basis_points"), JsonRequired] public int ThreatHealerBiasBasisPoints { get; init; }
    [JsonPropertyName("threat_control_bias_basis_points"), JsonRequired] public int ThreatControlBiasBasisPoints { get; init; }
    [JsonPropertyName("threat_ranged_bias_basis_points"), JsonRequired] public int ThreatRangedBiasBasisPoints { get; init; }
    [JsonPropertyName("threat_range_step_bias_basis_points"), JsonRequired] public int ThreatRangeStepBiasBasisPoints { get; init; }
    [JsonPropertyName("threat_multiplier_cap_basis_points"), JsonRequired] public int ThreatMultiplierCapBasisPoints { get; init; }
    [JsonPropertyName("meteor_high_priority_threat_multiplier_bp"), JsonRequired] public int MeteorHighPriorityThreatMultiplierBp { get; init; }
    [JsonPropertyName("meteor_high_priority_damage_hp_percent"), JsonRequired] public int MeteorHighPriorityDamageHpPercent { get; init; }
    [JsonPropertyName("meteor_high_priority_target_priority_score"), JsonRequired] public int MeteorHighPriorityTargetPriorityScore { get; init; }
    [JsonPropertyName("meteor_top_threat_rank"), JsonRequired] public int MeteorTopThreatRank { get; init; }
    [JsonPropertyName("meteor_friendly_fire_profile"), JsonRequired] public string MeteorFriendlyFireProfile { get; init; } = "";
    [JsonPropertyName("meteor_friendly_fire_soft_expected_hp_percent"), JsonRequired] public int MeteorFriendlyFireSoftExpectedHpPercent { get; init; }
    [JsonPropertyName("meteor_friendly_fire_hard_expected_hp_percent"), JsonRequired] public int MeteorFriendlyFireHardExpectedHpPercent { get; init; }
    [JsonPropertyName("meteor_friendly_fire_hard_worst_case_hp_percent"), JsonRequired] public int MeteorFriendlyFireHardWorstCaseHpPercent { get; init; }
    [JsonPropertyName("action_base_scores"), JsonRequired] public IReadOnlyDictionary<string, int> ActionBaseScores { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("default_bucket_priority"), JsonRequired] public int DefaultBucketPriority { get; init; }
    [JsonPropertyName("bucket_priorities"), JsonRequired] public IReadOnlyDictionary<string, int> BucketPriorities { get; init; } = EmptyMap<int>.Value;
}

internal sealed record EnemyAiActionImportModel(
    string Kind,
    EnemyAiActionPayloadJsonDto Payload
);

internal sealed record EnemyAiStateImportModel(
    string StateId,
    IReadOnlyList<EnemyAiActionImportModel> Actions,
    IReadOnlyList<EnemyAiGenerationSlotJsonDto> GenerationSlots
);

internal sealed record EnemyAiBrainImportModel(
    string BrainId,
    string DefaultStateId,
    BattleAiScoreProfileJsonDto? ScoreProfile,
    IReadOnlyList<EnemyAiStateImportModel> States,
    IReadOnlyList<EnemyAiTransitionRuleJsonDto> TransitionRules
);

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(EnemyAiBrainJsonDto))]
[JsonSerializable(typeof(EnemyTemplateJsonDto))]
[JsonSerializable(typeof(EncounterRosterJsonDto))]
[JsonSerializable(typeof(MoveToAdvantagePositionActionPayloadJsonDto))]
[JsonSerializable(typeof(MoveToMultiUnitSkillPositionActionPayloadJsonDto))]
[JsonSerializable(typeof(MoveToRangeActionPayloadJsonDto))]
[JsonSerializable(typeof(RetreatActionPayloadJsonDto))]
[JsonSerializable(typeof(UseChargeActionPayloadJsonDto))]
[JsonSerializable(typeof(UseChargePathAoeActionPayloadJsonDto))]
[JsonSerializable(typeof(UseGroundRepositionSkillActionPayloadJsonDto))]
[JsonSerializable(typeof(UseGroundSkillActionPayloadJsonDto))]
[JsonSerializable(typeof(UseMultiUnitSkillActionPayloadJsonDto))]
[JsonSerializable(typeof(UseRandomChainSkillActionPayloadJsonDto))]
[JsonSerializable(typeof(UseUnitSkillActionPayloadJsonDto))]
[JsonSerializable(typeof(WaitActionPayloadJsonDto))]
internal partial class EnemyContentJsonSerializerContext : JsonSerializerContext { }
