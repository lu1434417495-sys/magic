using System.Collections.Generic;
using Godot;

public sealed class DerivedAttributeRule
{
    public StringName target_attribute_id { get; private set; } = "";
    public int base_value { get; private set; }
    public int divisor { get; private set; } = 1;
    public int min_value { get; private set; }
    public int max_value { get; private set; }
    public int source_offset { get; private set; }

    private readonly Dictionary<StringName, int> _coefficients = new();

    public IReadOnlyDictionary<StringName, int> coefficients => _coefficients;

    public DerivedAttributeRule() { }

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
        divisor = p_divisor > 0 ? p_divisor : 1;
        min_value = p_min_value;
        max_value = p_max_value;
        source_offset = p_source_offset;
        SetCoefficients(p_coefficients);
    }

    public void RefreshCache()
    {
        // Kept for callers that rebuild rule data before evaluation; plain C# rules cache eagerly.
    }

    private void SetCoefficients(IReadOnlyDictionary<StringName, int> source)
    {
        _coefficients.Clear();
        if (source == null)
            return;
        foreach ((StringName attributeId, int coefficient) in source)
            _coefficients[attributeId] = coefficient;
    }

    public int evaluate(Dictionary<StringName, int> source_values)
    {
        int scaled_total = 0;
        foreach (var pair in _coefficients)
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
