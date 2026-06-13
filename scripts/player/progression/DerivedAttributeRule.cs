using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class DerivedAttributeRule : RefCounted
{
    public StringName target_attribute_id { get; private set; } = "";
    public int base_value { get; private set; }
    public Godot.Collections.Dictionary coefficients { get; private set; } = new();
    public int divisor { get; private set; } = 1;
    public int min_value { get; private set; }
    public int max_value { get; private set; }
    public int source_offset { get; private set; }

    private Dictionary<StringName, int> _cachedCoeffs = new();

    public DerivedAttributeRule() { }

    public DerivedAttributeRule(
        StringName p_target_attribute_id = default,
        int p_base_value = 0,
        Godot.Collections.Dictionary p_coefficients = null,
        int p_divisor = 1,
        int p_min_value = 0,
        int p_max_value = 0,
        int p_source_offset = 0
    )
    {
        target_attribute_id = p_target_attribute_id;
        base_value = p_base_value;
        coefficients = p_coefficients?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        divisor = p_divisor > 0 ? p_divisor : 1;
        min_value = p_min_value;
        max_value = p_max_value;
        source_offset = p_source_offset;
        _RebuildCache();
    }

    public void RefreshCache()
    {
        _RebuildCache();
    }

    private void _RebuildCache()
    {
        _cachedCoeffs.Clear();
        foreach (var key in coefficients.Keys)
        {
            if (
                key.VariantType != Variant.Type.String
                && key.VariantType != Variant.Type.StringName
            )
                continue;
            var sn = new StringName(key.ToString());
            if (coefficients[key].VariantType == Variant.Type.Int)
                _cachedCoeffs[sn] = coefficients[key].AsInt32();
        }
    }

    public int evaluate(Dictionary<StringName, int> source_values)
    {
        int scaled_total = 0;
        foreach (var pair in _cachedCoeffs)
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
