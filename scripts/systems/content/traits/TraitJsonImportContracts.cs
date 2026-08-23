#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

[Description("One immutable attribute modifier authored by a trait.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitAttributeModifierJsonDto
{
    [JsonPropertyName("attribute_id")]
    [JsonRequired]
    public string AttributeId { get; init; } = null!;

    [JsonPropertyName("mode")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitAttributeModifierModeSchemaValues))]
    public string Mode { get; init; } = null!;

    [JsonPropertyName("value")]
    [JsonRequired]
    public int Value { get; init; }

    [JsonPropertyName("value_per_rank")]
    [JsonRequired]
    public int ValuePerRank { get; init; }

    [JsonPropertyName("source_type")]
    [JsonRequired]
    public string SourceType { get; init; } = null!;

    [JsonPropertyName("source_id")]
    [JsonRequired]
    public string SourceId { get; init; } = null!;
}

[Description("One typed damage resistance projection.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitDamageResistanceEntryJsonDto
{
    [JsonPropertyName("damage_tag")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitDamageTagSchemaValues))]
    public string DamageTag { get; init; } = null!;

    [JsonPropertyName("mitigation_tier")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitMitigationTierSchemaValues))]
    public string MitigationTier { get; init; } = null!;
}

[Description("One base-attribute saving throw bonus.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitSaveBonusEntryJsonDto
{
    [JsonPropertyName("save_ability")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitSaveAbilitySchemaValues))]
    public string SaveAbility { get; init; } = null!;

    [JsonPropertyName("bonus")]
    [JsonRequired]
    public int Bonus { get; init; }
}

[Description("One typed saving throw bonus keyed by a canonical save tag.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitSaveTagBonusEntryJsonDto
{
    [JsonPropertyName("save_tag")]
    [JsonRequired]
    public string SaveTag { get; init; } = null!;

    [JsonPropertyName("bonus")]
    [JsonRequired]
    public int Bonus { get; init; }

    [JsonPropertyName("stack_mode")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitSaveTagBonusStackModeSchemaValues))]
    public string StackMode { get; init; } = null!;
}

[Description("One passive status declaration projected by a trait.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitPassiveStatusEffectJsonDto
{
    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = null!;

    [JsonPropertyName("power")]
    [JsonRequired]
    public int Power { get; init; }

    [JsonPropertyName("stacks")]
    [JsonRequired]
    public int Stacks { get; init; }

    [JsonPropertyName("display_label")]
    [JsonRequired]
    public string DisplayLabel { get; init; } = null!;

    [JsonPropertyName("undispellable")]
    [JsonRequired]
    public bool Undispellable { get; init; }

    [JsonPropertyName("counts_as_debuff_override")]
    [JsonRequired]
    public bool CountsAsDebuffOverride { get; init; }

    [JsonPropertyName("counts_as_debuff")]
    [JsonRequired]
    public bool CountsAsDebuff { get; init; }

    [JsonPropertyName("save_immunity_tags")]
    [JsonRequired]
    public IReadOnlyList<string> SaveImmunityTags { get; init; } = null!;
}

[Description("One typed per-instance trait roll value.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitRollValueSchemaEntryJsonDto
{
    [JsonPropertyName("key")]
    [JsonRequired]
    public string Key { get; init; } = null!;

    [JsonPropertyName("value_type")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitRollValueTypeSchemaValues))]
    public string ValueType { get; init; } = null!;

    [JsonPropertyName("min_value")]
    [JsonRequired]
    public int MinValue { get; init; }

    [JsonPropertyName("max_value")]
    [JsonRequired]
    public int MaxValue { get; init; }

    [JsonPropertyName("allowed_values")]
    [JsonRequired]
    public IReadOnlyList<string> AllowedValues { get; init; } = null!;
}

[Description("Strict expanded trait entry. Save-tag fields contain bare tags only.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitJsonDto
{
    [JsonPropertyName("trait_id")]
    [JsonRequired]
    public string TraitId { get; init; } = null!;

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = null!;

    [JsonPropertyName("description")]
    [JsonRequired]
    public string Description { get; init; } = null!;

    [JsonPropertyName("categories")]
    [JsonRequired]
    public IReadOnlyList<string> Categories { get; init; } = null!;

    [JsonPropertyName("allowed_source_kinds")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitSourceKindSchemaValues))]
    public IReadOnlyList<string> AllowedSourceKinds { get; init; } = null!;

    [JsonPropertyName("effect_type")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitEffectTypeSchemaValues))]
    public string EffectType { get; init; } = null!;

    [JsonPropertyName("trigger_type")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitTriggerTypeSchemaValues))]
    public string TriggerType { get; init; } = null!;

    [JsonPropertyName("stack_policy")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitStackPolicySchemaValues))]
    public string StackPolicy { get; init; } = null!;

    [JsonPropertyName("charge_scope")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitChargeScopeSchemaValues))]
    public string ChargeScope { get; init; } = null!;

    [JsonPropertyName("charge_reset_timing")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(TraitChargeResetTimingSchemaValues))]
    public string ChargeResetTiming { get; init; } = null!;

    [JsonPropertyName("highest_roll_compare_key")]
    [JsonRequired]
    public string HighestRollCompareKey { get; init; } = null!;

    [JsonPropertyName("vision_range")]
    [JsonRequired]
    public int VisionRange { get; init; }

    [JsonPropertyName("proficiency_choice_count")]
    [JsonRequired]
    public int ProficiencyChoiceCount { get; init; }

    [JsonPropertyName("attribute_modifiers")]
    [JsonRequired]
    public IReadOnlyList<TraitAttributeModifierJsonDto> AttributeModifiers { get; init; } = null!;

    [JsonPropertyName("save_advantage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> SaveAdvantageTags { get; init; } = null!;

    [JsonPropertyName("save_disadvantage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> SaveDisadvantageTags { get; init; } = null!;

    [JsonPropertyName("save_immunity_tags")]
    [JsonRequired]
    public IReadOnlyList<string> SaveImmunityTags { get; init; } = null!;

    [JsonPropertyName("damage_resistance_entries")]
    [JsonRequired]
    public IReadOnlyList<TraitDamageResistanceEntryJsonDto> DamageResistanceEntries { get; init; } = null!;

    [JsonPropertyName("save_bonus_entries")]
    [JsonRequired]
    public IReadOnlyList<TraitSaveBonusEntryJsonDto> SaveBonusEntries { get; init; } = null!;

    [JsonPropertyName("save_tag_bonus_entries")]
    public IReadOnlyList<TraitSaveTagBonusEntryJsonDto> SaveTagBonusEntries { get; init; } = null!;

    [JsonPropertyName("passive_status_effects")]
    [JsonRequired]
    public IReadOnlyList<TraitPassiveStatusEffectJsonDto> PassiveStatusEffects { get; init; } = null!;

    [JsonPropertyName("roll_value_schema")]
    [JsonRequired]
    public IReadOnlyList<TraitRollValueSchemaEntryJsonDto> RollValueSchema { get; init; } = null!;
}

[Description("Trait authoring document with file-local templates.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TraitJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(TraitContentJsonAuthoringDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(TraitContentJsonAuthoringDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, TraitJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, TraitJsonDto>(new Dictionary<string, TraitJsonDto>());

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        TraitContentJsonAuthoringDomain.EntryIdPropertyName,
        "template"
    )]
    public IReadOnlyList<TraitJsonDto> Entries { get; init; } = Array.Empty<TraitJsonDto>();
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(TraitJsonDto))]
[JsonSerializable(typeof(TraitAttributeModifierJsonDto))]
[JsonSerializable(typeof(TraitDamageResistanceEntryJsonDto))]
[JsonSerializable(typeof(TraitSaveBonusEntryJsonDto))]
[JsonSerializable(typeof(TraitSaveTagBonusEntryJsonDto))]
[JsonSerializable(typeof(TraitPassiveStatusEffectJsonDto))]
[JsonSerializable(typeof(TraitRollValueSchemaEntryJsonDto))]
internal partial class TraitJsonImportSerializerContext : JsonSerializerContext { }

internal sealed class TraitSaveTagBonusStackModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "add", "highest" });
}

internal sealed class TraitAttributeModifierModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "flat", "percent" });
}

internal sealed class TraitSourceKindSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "identity", "character", "equipment_fixed", "equipment_roll", "gear_set_threshold" }
    );
}

internal sealed class TraitTriggerTypeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "passive", "on_natural_one", "on_crit", "on_fatal_damage", "on_battle_start", "on_turn_start" }
    );
}

internal sealed class TraitStackPolicySchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "unique_by_trait", "highest_roll", "additive", "stack_by_instance" }
    );
}

internal sealed class TraitChargeScopeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "none", "per_turn", "per_battle" });
}

internal sealed class TraitChargeResetTimingSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "none", "battle_start", "turn_start" });
}

internal sealed class TraitRollValueTypeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "int", "string_name", "bool" });
}

internal sealed class TraitDamageTagSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "acid", "fire", "force", "freeze", "lightning", "magic", "negative_energy", "physical_blunt", "physical_pierce", "physical_slash", "poison", "psychic", "radiant", "thunder" }
    );
}

internal sealed class TraitMitigationTierSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "double", "half", "immune", "normal" });
}

internal sealed class TraitSaveAbilitySchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "strength", "agility", "constitution", "perception", "intelligence", "willpower" }
    );
}

internal sealed class TraitEffectTypeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[]
        {
            "attribute_modifier", "darkvision", "superior_darkvision", "fey_ancestry", "brave",
            "halfling_luck", "savage_attacks", "relentless_endurance", "gnome_cunning",
            "dwarven_resilience", "duergar_resilience", "human_versatility", "small_body",
            "fleet_of_foot", "dragon_breath", "racial_spell_grant", "damage_resistance",
            "save_advantage", "civil_militia", "keen_senses", "trance", "elven_weapon_training",
            "drow_weapon_training", "dwarven_combat_training", "shield_dwarf_armor_training",
            "dwarven_toughness", "menacing", "halfling_nimbleness", "naturally_stealthy",
            "mask_of_the_wild", "stonecunning", "forest_gnome_magic", "deep_gnome_camouflage",
            "artificers_lore", "duergar_magic", "githyanki_martial_prodigy", "astral_knowledge",
            "githyanki_psionics", "infernal_legacy", "asmodeus_legacy", "mephistopheles_legacy",
            "zariel_legacy", "drow_magic", "draconic_ancestry", "equipment_ability",
        }
    );
}
