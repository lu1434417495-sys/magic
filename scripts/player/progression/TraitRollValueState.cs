using Godot;

public partial class TraitRollValueState : RefCounted
{
    public StringName key = "";
    public StringName value_type = "int";
    public int int_value;
    public StringName string_name_value = "";
    public bool bool_value;

    internal TraitRollValueType ValueTypeKind => TraitContentRules.ToRollValueType(value_type);

    public static TraitRollValueState CreateInt(StringName key, int value)
    {
        return new TraitRollValueState
        {
            key = ProgressionDataUtils.to_string_name(key),
            value_type = TraitContentRules.ToStringName(TraitRollValueType.Int),
            int_value = value,
        };
    }

    public static TraitRollValueState CreateStringName(StringName key, StringName value)
    {
        return new TraitRollValueState
        {
            key = ProgressionDataUtils.to_string_name(key),
            value_type = TraitContentRules.ToStringName(TraitRollValueType.StringName),
            string_name_value = ProgressionDataUtils.to_string_name(value),
        };
    }

    public static TraitRollValueState CreateBool(StringName key, bool value)
    {
        return new TraitRollValueState
        {
            key = ProgressionDataUtils.to_string_name(key),
            value_type = TraitContentRules.ToStringName(TraitRollValueType.Bool),
            bool_value = value,
        };
    }

    internal static TraitRollValueState FromVariant(StringName key, Variant value)
    {
        StringName normalizedKey = ProgressionDataUtils.to_string_name(key);
        if (normalizedKey == "")
            return null;

        return value.VariantType switch
        {
            Variant.Type.Int => CreateInt(normalizedKey, value.AsInt32()),
            Variant.Type.String or Variant.Type.StringName
                => CreateStringName(normalizedKey, ProgressionDataUtils.to_string_name(value)),
            Variant.Type.Bool => CreateBool(normalizedKey, value.AsBool()),
            _ => null,
        };
    }

    internal Variant ToVariant()
    {
        return ValueTypeKind switch
        {
            TraitRollValueType.Int => Variant.From(int_value),
            TraitRollValueType.StringName => Variant.From(string_name_value),
            TraitRollValueType.Bool => Variant.From(bool_value),
            _ => default,
        };
    }

    internal TraitRollValueState DuplicateState()
    {
        return new TraitRollValueState
        {
            key = key,
            value_type = value_type,
            int_value = int_value,
            string_name_value = string_name_value,
            bool_value = bool_value,
        };
    }
}
