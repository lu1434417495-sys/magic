#nullable enable

using System.Collections.Generic;

internal static class TraitImportCanonicalJson
{
    private sealed record EmptyTemplates;
    private sealed record Document(string Family, IReadOnlyList<TraitImportModel> Entries)
    {
        internal EmptyTemplates Templates { get; } = new();
    }

    private static readonly ContentCanonicalJsonValueSchema<string> Text = ContentCanonicalJsonValue.Text;
    private static readonly ContentCanonicalJsonValueSchema<int> Int = ContentCanonicalJsonValue.Int32;
    private static readonly ContentCanonicalJsonValueSchema<bool> Bool = ContentCanonicalJsonValue.Boolean;
    private static readonly ContentCanonicalJsonValueSchema<IReadOnlyList<string>> Texts = ContentCanonicalJsonValue.Array(Text);

    private static readonly ContentCanonicalJsonObjectSchema<TraitAttributeModifierImportModel> Modifier = new(
        ContentCanonicalJsonProperty<TraitAttributeModifierImportModel>.Required("attribute_id", value => value.AttributeId, Text),
        ContentCanonicalJsonProperty<TraitAttributeModifierImportModel>.Required("mode", value => value.Mode, Text),
        ContentCanonicalJsonProperty<TraitAttributeModifierImportModel>.Required("value", value => value.Value, Int),
        ContentCanonicalJsonProperty<TraitAttributeModifierImportModel>.Required("value_per_rank", value => value.ValuePerRank, Int),
        ContentCanonicalJsonProperty<TraitAttributeModifierImportModel>.Required("source_type", value => value.SourceType, Text),
        ContentCanonicalJsonProperty<TraitAttributeModifierImportModel>.Required("source_id", value => value.SourceId, Text)
    );

    private static readonly ContentCanonicalJsonObjectSchema<TraitDamageResistanceEntryImportModel> Resistance = new(
        ContentCanonicalJsonProperty<TraitDamageResistanceEntryImportModel>.Required("damage_tag", value => value.DamageTag, Text),
        ContentCanonicalJsonProperty<TraitDamageResistanceEntryImportModel>.Required("mitigation_tier", value => value.MitigationTier, Text)
    );

    private static readonly ContentCanonicalJsonObjectSchema<TraitSaveBonusEntryImportModel> SaveBonus = new(
        ContentCanonicalJsonProperty<TraitSaveBonusEntryImportModel>.Required("save_ability", value => value.SaveAbility, Text),
        ContentCanonicalJsonProperty<TraitSaveBonusEntryImportModel>.Required("bonus", value => value.Bonus, Int)
    );

    private static readonly ContentCanonicalJsonObjectSchema<TraitPassiveStatusEffectImportModel> PassiveStatus = new(
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("status_id", value => value.StatusId, Text),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("power", value => value.Power, Int),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("stacks", value => value.Stacks, Int),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("display_label", value => value.DisplayLabel, Text),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("undispellable", value => value.Undispellable, Bool),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("counts_as_debuff_override", value => value.CountsAsDebuffOverride, Bool),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("counts_as_debuff", value => value.CountsAsDebuff, Bool),
        ContentCanonicalJsonProperty<TraitPassiveStatusEffectImportModel>.Required("save_immunity_tags", value => value.SaveImmunityTags, Texts)
    );

    private static readonly ContentCanonicalJsonObjectSchema<TraitRollValueSchemaEntryImportModel> RollSchema = new(
        ContentCanonicalJsonProperty<TraitRollValueSchemaEntryImportModel>.Required("key", value => value.Key, Text),
        ContentCanonicalJsonProperty<TraitRollValueSchemaEntryImportModel>.Required("value_type", value => value.ValueType, Text),
        ContentCanonicalJsonProperty<TraitRollValueSchemaEntryImportModel>.Required("min_value", value => value.MinValue, Int),
        ContentCanonicalJsonProperty<TraitRollValueSchemaEntryImportModel>.Required("max_value", value => value.MaxValue, Int),
        ContentCanonicalJsonProperty<TraitRollValueSchemaEntryImportModel>.Required("allowed_values", value => value.AllowedValues, Texts)
    );

    internal static readonly ContentCanonicalJsonObjectSchema<TraitImportModel> Entry = new(
        ContentCanonicalJsonProperty<TraitImportModel>.Required("trait_id", value => value.TraitId, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("display_name", value => value.DisplayName, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("description", value => value.Description, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("categories", value => value.Categories, Texts),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("allowed_source_kinds", value => value.AllowedSourceKinds, Texts),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("effect_type", value => value.EffectType, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("trigger_type", value => value.TriggerType, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("stack_policy", value => value.StackPolicy, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("charge_scope", value => value.ChargeScope, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("charge_reset_timing", value => value.ChargeResetTiming, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("highest_roll_compare_key", value => value.HighestRollCompareKey, Text),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("vision_range", value => value.VisionRange, Int),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("proficiency_choice_count", value => value.ProficiencyChoiceCount, Int),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("attribute_modifiers", value => value.AttributeModifiers, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Modifier))),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("save_advantage_tags", value => value.SaveAdvantageTags, Texts),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("save_disadvantage_tags", value => value.SaveDisadvantageTags, Texts),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("save_immunity_tags", value => value.SaveImmunityTags, Texts),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("damage_resistance_entries", value => value.DamageResistanceEntries, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Resistance))),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("save_bonus_entries", value => value.SaveBonusEntries, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(SaveBonus))),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("passive_status_effects", value => value.PassiveStatusEffects, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(PassiveStatus))),
        ContentCanonicalJsonProperty<TraitImportModel>.Required("roll_value_schema", value => value.RollValueSchema, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(RollSchema)))
    );

    private static readonly ContentCanonicalJsonObjectSchema<Document> DocumentSchema = new(
        ContentCanonicalJsonProperty<Document>.Required("schema", _ => TraitContentJsonAuthoringDomain.SchemaVersion, Int),
        ContentCanonicalJsonProperty<Document>.Required("domain", _ => TraitContentJsonAuthoringDomain.DomainId, Text),
        ContentCanonicalJsonProperty<Document>.Required("family", value => value.Family, Text),
        ContentCanonicalJsonProperty<Document>.Required("templates", value => value.Templates, ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<EmptyTemplates>())),
        ContentCanonicalJsonProperty<Document>.Required("entries", value => value.Entries, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Entry)))
    );

    internal static string WriteDocument(
        ContentCanonicalJsonWriter writer,
        string family,
        IReadOnlyList<TraitImportModel> entries
    ) => writer.Write(new Document(family, entries), DocumentSchema, indented: true)
        .Replace("\r\n", "\n", System.StringComparison.Ordinal)
        .Replace("\r", "\n", System.StringComparison.Ordinal)
        .TrimEnd('\n') + "\n";
}
