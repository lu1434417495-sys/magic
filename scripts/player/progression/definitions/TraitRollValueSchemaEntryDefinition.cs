using System;
using System.Collections.Generic;
using Godot;

public sealed class TraitRollValueSchemaEntryDefinition
{
    public TraitRollValueSchemaEntryDefinition(
        StringName key,
        StringName valueType,
        int minValue,
        int maxValue,
        IReadOnlyList<StringName> allowedValues
    )
    {
        Key = key;
        ValueType = valueType;
        MinValue = minValue;
        MaxValue = maxValue;
        AllowedValues = ProgressionDefinitionProjection.FreezeValues(
            allowedValues,
            "TraitRollValueSchemaEntryDefinition.AllowedValues"
        );
    }

    public StringName Key { get; }
    public StringName ValueType { get; }
    public int MinValue { get; }
    public int MaxValue { get; }
    public IReadOnlyList<StringName> AllowedValues { get; }
    internal TraitRollValueType ValueTypeKind => TraitContentRules.ToRollValueType(ValueType);

    internal void AppendSchemaErrors(List<string> errors, string ownerLabel)
    {
        if (errors == null)
            return;

        if (Key == "")
            errors.Add($"{ownerLabel}: roll_value_schema entry missing key.");

        switch (ValueTypeKind)
        {
            case TraitRollValueType.Int:
                if (MinValue > MaxValue)
                    errors.Add($"{ownerLabel}.{Key}: min_value {MinValue} > max_value {MaxValue}.");
                break;
            case TraitRollValueType.StringName:
                if (AllowedValues.Count == 0)
                    errors.Add(
                        $"{ownerLabel}.{Key}: string_name roll needs non-empty allowed_values."
                    );
                break;
            case TraitRollValueType.Bool:
                break;
            default:
                errors.Add($"{ownerLabel}.{Key}: unsupported value_type {ValueType}.");
                break;
        }
    }

    internal static TraitRollValueSchemaEntryDefinition FromResource(
        TraitRollValueSchemaEntry source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ProgressionDefinitionProjection.RequireKnown(
            source.ValueTypeKind != TraitRollValueType.Unknown,
            $"{path}.value_type",
            source.value_type
        );
        return new TraitRollValueSchemaEntryDefinition(
            source.key,
            source.value_type,
            source.min_value,
            source.max_value,
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.AllowedValuesProjectionBorrowed,
                $"{path}.allowed_values"
            )
        );
    }
}
