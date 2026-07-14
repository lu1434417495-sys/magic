using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class DerivedAttributeRule
{
    public StringName target_attribute_id { get; private set; } = "";
    public int base_value { get; private set; }
    public IReadOnlyDictionary<StringName, int> coefficients { get; private set; } =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
    public int divisor { get; private set; } = 1;
    public int min_value { get; private set; }
    public int max_value { get; private set; }
    public int source_offset { get; private set; }

    public DerivedAttributeRule(
        StringName p_target_attribute_id = default,
        int p_base_value = 0,
        IReadOnlyDictionary<StringName, int> p_coefficients = null,
        int p_divisor = 1,
        int p_min_value = 0,
        int p_max_value = 0,
        int p_source_offset = 0
    )
    {
        target_attribute_id = p_target_attribute_id;
        base_value = p_base_value;
        coefficients = new ReadOnlyDictionary<StringName, int>(
            p_coefficients != null
                ? new Dictionary<StringName, int>(p_coefficients)
                : new Dictionary<StringName, int>()
        );
        divisor = p_divisor > 0 ? p_divisor : 1;
        min_value = p_min_value;
        max_value = p_max_value;
        source_offset = p_source_offset;
    }

    public int evaluate(IReadOnlyDictionary<StringName, int> source_values)
    {
        int scaled_total = 0;
        foreach (var pair in coefficients)
        {
            int sourceValue = source_values.TryGetValue(pair.Key, out var v) ? v : 0;
            scaled_total += pair.Value * (sourceValue - source_offset);
        }

        int safeDivisor = divisor <= 0 ? 1 : divisor;
        int result = base_value + Mathf.FloorToInt((float)scaled_total / safeDivisor);
        if (max_value >= min_value && max_value != 0)
            return Mathf.Clamp(result, min_value, max_value);
        return Mathf.Max(result, min_value);
    }
}
