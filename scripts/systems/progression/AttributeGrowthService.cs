using Godot;

[GlobalClass]
public partial class AttributeGrowthService : RefCounted
{
    private const int ATTRIBUTE_PROGRESS_THRESHOLD = 100;

    private const int BASE_ATTRIBUTE_PROGRESS_CONVERSION_CAP = 20;

    private UnitProgress _unit_progress;

    public void setup(UnitProgress unitProgress)
    {
        _unit_progress = unitProgress;
    }

    public static int get_tier_budget(StringName growthTier) =>
        AttributeGrowthContentRules.get_tier_budget(growthTier);

    public static bool is_valid_growth_tier(StringName growthTier) =>
        AttributeGrowthContentRules.is_valid_growth_tier(growthTier);

    public static bool is_valid_attribute_id(StringName attributeId) =>
        AttributeGrowthContentRules.is_valid_attribute_id(attributeId);

    public Godot.Collections.Dictionary apply_attribute_progress(
        StringName attributeId,
        int amount,
        string reasonText = ""
    )
    {
        var result = new Godot.Collections.Dictionary
        {
            { "attribute_id", attributeId },
            { "progress_delta", 0 },
            { "progress_before", 0 },
            { "progress_after", 0 },
            { "attribute_before", 0 },
            { "attribute_after", 0 },
            { "attribute_delta", 0 },
            { "reason_text", reasonText },
            { "applied", false },
        };

        if (
            _unit_progress == null
            || _unit_progress.unit_base_attributes == null
        )
            return result;

        if (!is_valid_attribute_id(attributeId) || amount <= 0)
            return result;

        var growthProgress = _unit_progress.attribute_growth_progress;

        var baseAttrs = _unit_progress.unit_base_attributes;

        int beforeProgress = growthProgress.ContainsKey(attributeId)
            ? growthProgress[attributeId].AsInt32()
            : 0;

        int beforeAttribute = baseAttrs.get_attribute_value(attributeId);

        int nextProgress = beforeProgress + amount;

        int nextAttribute = beforeAttribute;

        while (
            nextAttribute < BASE_ATTRIBUTE_PROGRESS_CONVERSION_CAP
            && nextProgress >= ATTRIBUTE_PROGRESS_THRESHOLD
        )
        {
            nextAttribute += 1;

            nextProgress -= ATTRIBUTE_PROGRESS_THRESHOLD;
        }

        growthProgress[attributeId] = nextProgress;

        baseAttrs.set_attribute_value(attributeId, nextAttribute);

        result["progress_delta"] = amount;

        result["progress_before"] = beforeProgress;

        result["progress_after"] = nextProgress;

        result["attribute_before"] = beforeAttribute;

        result["attribute_after"] = nextAttribute;

        result["attribute_delta"] = nextAttribute - beforeAttribute;

        result["applied"] = true;

        return result;
    }
}
