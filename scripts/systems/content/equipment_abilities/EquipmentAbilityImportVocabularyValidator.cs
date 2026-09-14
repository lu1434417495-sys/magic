#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

internal static class EquipmentAbilityImportVocabularyValidator
{
    internal const string UnknownTrigger = "equipment_ability.vocabulary.trigger.unknown";
    internal const string UnknownTiming = "equipment_ability.vocabulary.timing.unknown";
    internal const string UnknownGroupMode = "equipment_ability.vocabulary.condition_group_mode.unknown";
    internal const string UnknownFactQueryKind = "equipment_ability.vocabulary.fact_query_kind.unknown";
    internal const string UnknownFactId = "equipment_ability.vocabulary.fact_id.unknown";
    internal const string UnknownFactSubject = "equipment_ability.vocabulary.fact_subject.unknown";
    internal const string UnknownFactAggregation = "equipment_ability.vocabulary.fact_aggregation.unknown";
    internal const string UnknownFactValueKind = "equipment_ability.vocabulary.fact_value_kind.unknown";

    internal static void Validate(
        JsonContentEntryContext context,
        EquipmentAbilityContentPackImportModel import,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(diagnostics);
        Visit(context, import, context.JsonPointer, diagnostics);
    }

    private static void Visit(
        JsonContentEntryContext context,
        object value,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        Type valueType = value.GetType();
        if (value is string || valueType.IsPrimitive || valueType.IsEnum || value is decimal)
            return;
        switch (value)
        {
            case EquipmentAbilityReactionImportModel reaction:
                ValidateClosed(
                    EquipmentAbilityClosedVocabulary.IsKnownTrigger(reaction.trigger),
                    UnknownTrigger,
                    "Reaction trigger is not registered.",
                    reaction.trigger,
                    context,
                    $"{pointer}/trigger",
                    diagnostics
                );
                ValidateClosed(
                    EquipmentAbilityClosedVocabulary.IsKnownTiming(reaction.timing),
                    UnknownTiming,
                    "Reaction timing is not registered.",
                    reaction.timing,
                    context,
                    $"{pointer}/timing",
                    diagnostics
                );
                break;
            case EquipmentWorldEffectImportModel worldEffect:
                ValidateClosed(
                    EquipmentAbilityClosedVocabulary.IsKnownTrigger(worldEffect.trigger),
                    UnknownTrigger,
                    "World-effect trigger is not registered.",
                    worldEffect.trigger,
                    context,
                    $"{pointer}/trigger",
                    diagnostics
                );
                ValidateClosed(
                    EquipmentAbilityClosedVocabulary.IsKnownTiming(worldEffect.timing),
                    UnknownTiming,
                    "World-effect timing is not registered.",
                    worldEffect.timing,
                    context,
                    $"{pointer}/timing",
                    diagnostics
                );
                break;
            case EquipmentAbilityConditionGroupImportModel group:
                ValidateClosed(
                    EquipmentAbilityClosedVocabulary.IsKnownConditionGroupMode(group.mode),
                    UnknownGroupMode,
                    "Condition-group mode is not registered.",
                    group.mode,
                    context,
                    $"{pointer}/mode",
                    diagnostics
                );
                break;
            case EquipmentAbilityFactQueryImportModel query:
                ValidateFactQuery(context, query, pointer, diagnostics);
                break;
        }

        foreach (
            PropertyInfo property in value.GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            object? child = property.GetValue(value);
            if (child == null || child is string)
                continue;
            string childPointer = $"{pointer}/{property.Name}";
            if (child is IEnumerable sequence)
            {
                int index = 0;
                foreach (object? element in sequence)
                {
                    if (element != null)
                        Visit(context, element, $"{childPointer}/{index}", diagnostics);
                    index += 1;
                }
                continue;
            }
            if (child.GetType().Name.EndsWith("ImportModel", StringComparison.Ordinal))
                Visit(context, child, childPointer, diagnostics);
        }
    }

    private static void ValidateFactQuery(
        JsonContentEntryContext context,
        EquipmentAbilityFactQueryImportModel query,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ValidateClosed(EquipmentAbilityClosedVocabulary.IsKnownFactQueryKind(query.query_kind), UnknownFactQueryKind, "Fact query kind is not registered.", query.query_kind, context, $"{pointer}/query_kind", diagnostics);
        if (query.query_kind == "fact")
            ValidateClosed(EquipmentAbilityClosedVocabulary.IsKnownFactId(query.fact_id), UnknownFactId, "Fact id is not registered.", query.fact_id, context, $"{pointer}/fact_id", diagnostics);
        ValidateClosed(EquipmentAbilityClosedVocabulary.IsKnownFactSubject(query.subject), UnknownFactSubject, "Fact subject is not registered.", query.subject, context, $"{pointer}/subject", diagnostics);
        ValidateClosed(EquipmentAbilityClosedVocabulary.IsKnownFactAggregation(query.aggregation), UnknownFactAggregation, "Fact aggregation is not registered.", query.aggregation, context, $"{pointer}/aggregation", diagnostics);
        ValidateClosed(EquipmentAbilityClosedVocabulary.IsKnownFactValueKind(query.value_kind), UnknownFactValueKind, "Fact value kind is not registered.", query.value_kind, context, $"{pointer}/value_kind", diagnostics);
    }

    private static void ValidateClosed(
        bool valid,
        string ruleId,
        string message,
        string actual,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (!valid)
            Add(ruleId, message, actual, context, pointer, diagnostics);
    }

    private static void Add(
        string ruleId,
        string message,
        string actual,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    ) => diagnostics.Add(new ContentJsonDiagnostic(ruleId, message, context.SourceLabel, pointer, Actual: actual ?? ""));
}
