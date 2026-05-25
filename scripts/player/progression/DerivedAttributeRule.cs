using Godot;

[GlobalClass]
public partial class DerivedAttributeRule : RefCounted
{
    public StringName target_attribute_id = "";
    public int base_value;
    public Godot.Collections.Dictionary coefficients = new();
    public int divisor = 1;
    public int min_value;
    public int max_value;
    public int source_offset;

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
        divisor = Mathf.Max(p_divisor, 1);
        min_value = p_min_value;
        max_value = p_max_value;
        source_offset = p_source_offset;
    }

    public int evaluate(Godot.Collections.Dictionary source_values)
    {
        int scaled_total = 0;
        foreach (var key in coefficients.Keys)
        {
            int sourceValue = source_values.ContainsKey(key) ? source_values[key].AsInt32() : 0;
            scaled_total += coefficients[key].AsInt32() * (sourceValue - source_offset);
        }

        int result = base_value + Mathf.FloorToInt((float)scaled_total / divisor);
        if (max_value > min_value)
            return Mathf.Clamp(result, min_value, max_value);
        return Mathf.Max(result, min_value);
    }
}
