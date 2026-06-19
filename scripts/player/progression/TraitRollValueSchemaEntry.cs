using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class TraitRollValueSchemaEntry : Resource
{
    [Export]
    public StringName key { get; set; } = "";

    [Export]
    public StringName value_type { get; set; } = "int";

    [Export]
    public int min_value { get; set; }

    [Export]
    public int max_value { get; set; }

    [Export]
    public Godot.Collections.Array<StringName> allowed_values { get; set; } = new();

    internal TraitRollValueType ValueTypeKind =>
        TraitContentRules.ToRollValueType(value_type);

    internal void AppendSchemaErrors(List<string> errors, string ownerLabel)
    {
        if (errors == null)
            return;

        if (key == "")
            errors.Add($"{ownerLabel}: roll_value_schema entry missing key.");

        switch (ValueTypeKind)
        {
            case TraitRollValueType.Int:
                if (min_value > max_value)
                    errors.Add(
                        $"{ownerLabel}.{key}: min_value {min_value} > max_value {max_value}."
                    );
                break;
            case TraitRollValueType.StringName:
                if (allowed_values.Count == 0)
                    errors.Add(
                        $"{ownerLabel}.{key}: string_name roll needs non-empty allowed_values."
                    );
                break;
            case TraitRollValueType.Bool:
                break;
            default:
                errors.Add($"{ownerLabel}.{key}: unsupported value_type {value_type}.");
                break;
        }
    }
}
