using Godot;

internal static class AttributeModifierContentRules
{
    private static readonly StringName ModeFlat = "flat";
    private static readonly StringName ModePercent = "percent";

    internal static AttributeModifierMode ToMode(StringName value)
    {
        if (value == ModeFlat)
            return AttributeModifierMode.Flat;
        if (value == ModePercent)
            return AttributeModifierMode.Percent;
        return AttributeModifierMode.Unknown;
    }

    internal static StringName ToStringName(AttributeModifierMode mode) =>
        mode switch
        {
            AttributeModifierMode.Flat => ModeFlat,
            AttributeModifierMode.Percent => ModePercent,
            _ => "",
        };
}
