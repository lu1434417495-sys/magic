using Godot;

public sealed class AttributeGrowthService
{
    private const int ATTRIBUTE_PROGRESS_THRESHOLD = 100;

    private const int BASE_ATTRIBUTE_PROGRESS_CONVERSION_CAP = 20;

    private UnitProgress _unit_progress;

    public void Setup(UnitProgress unitProgress)
    {
        _unit_progress = unitProgress;
    }

    public static int GetTierBudget(StringName growthTier) =>
        AttributeGrowthContentRules.GetTierBudget(growthTier);

    public static bool IsValidGrowthTier(StringName growthTier) =>
        AttributeGrowthContentRules.IsValidGrowthTier(growthTier);

    public static bool IsValidAttributeId(StringName attributeId) =>
        AttributeGrowthContentRules.IsValidAttributeId(attributeId);

    public AttributeGrowthResult ApplyAttributeProgressTyped(
        StringName attributeId,
        int amount,
        string reasonText = ""
    )
    {
        if (
            _unit_progress == null
            || _unit_progress.unit_base_attributes == null
        )
            return AttributeGrowthResult.NotApplied(attributeId, reasonText);

        if (!IsValidAttributeId(attributeId) || amount <= 0)
            return AttributeGrowthResult.NotApplied(attributeId, reasonText);

        var baseAttrs = _unit_progress.unit_base_attributes;

        int beforeProgress = _unit_progress.TryGetAttributeGrowthProgressAmount(
            attributeId,
            out int existingProgress
        )
            ? existingProgress
            : 0;

        int beforeAttribute = baseAttrs.GetAttributeValue(attributeId);

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

        _unit_progress.SetAttributeGrowthProgressAmount(attributeId, nextProgress);

        baseAttrs.SetAttributeValue(attributeId, nextAttribute);

        return AttributeGrowthResult.AppliedResult(
            attributeId,
            amount,
            beforeProgress,
            nextProgress,
            beforeAttribute,
            nextAttribute,
            reasonText
        );
    }
}
