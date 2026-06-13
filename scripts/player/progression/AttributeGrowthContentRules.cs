using Godot;

internal enum AttributeGrowthTierKind
{
    Unknown = 0,
    Basic,
    Intermediate,
    Advanced,
    Ultimate,
}

public static class AttributeGrowthContentRules
{
    public static int GetTierBudget(StringName growthTier)
    {
        return ToGrowthTierKind(growthTier) switch
        {
            AttributeGrowthTierKind.Basic => 60,
            AttributeGrowthTierKind.Intermediate => 120,
            AttributeGrowthTierKind.Advanced => 180,
            AttributeGrowthTierKind.Ultimate => 240,
            _ => 0,
        };
    }

    public static bool IsValidGrowthTier(StringName growthTier)
    {
        return ToGrowthTierKind(growthTier) != AttributeGrowthTierKind.Unknown;
    }

    public static bool IsValidAttributeId(StringName attributeId)
    {
        return UnitBaseAttributes.IsBaseAttributeId(attributeId);
    }

    internal static AttributeGrowthTierKind ToGrowthTierKind(StringName growthTier)
    {
        return growthTier.ToString() switch
        {
            "basic" => AttributeGrowthTierKind.Basic,
            "intermediate" => AttributeGrowthTierKind.Intermediate,
            "advanced" => AttributeGrowthTierKind.Advanced,
            "ultimate" => AttributeGrowthTierKind.Ultimate,
            _ => AttributeGrowthTierKind.Unknown,
        };
    }
}
