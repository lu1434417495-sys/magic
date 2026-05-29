using Godot;
using Godot.Collections;

[GlobalClass]
public partial class AttributeGrowthContentRules : RefCounted
{
    public static readonly Dictionary<StringName, int> ValidGrowthTiers = new()
    {
        { "basic", 60 },
        { "intermediate", 120 },
        { "advanced", 180 },
        { "ultimate", 240 },
    };

    public static int GetTierBudget(StringName growthTier)
    {
        return (ValidGrowthTiers.ContainsKey(growthTier) ? ValidGrowthTiers[growthTier] : 0);
    }

    public static int get_tier_budget(StringName growth_tier) => GetTierBudget(growth_tier);

    public static bool IsValidGrowthTier(StringName growthTier)
    {
        return ValidGrowthTiers.ContainsKey(growthTier);
    }

    public static bool is_valid_growth_tier(StringName growth_tier) =>
        IsValidGrowthTier(growth_tier);

    public static bool IsValidAttributeId(StringName attributeId)
    {
        return UnitBaseAttributes.BASE_ATTRIBUTE_IDS().Contains(attributeId);
    }

    public static bool is_valid_attribute_id(StringName attribute_id) =>
        IsValidAttributeId(attribute_id);
}
