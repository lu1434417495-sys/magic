using Godot;

public sealed class CharacterAttributeChangeFact
{
    public StringName AttributeId { get; }
    public string AttributeLabel { get; }
    public int Delta { get; }
    public string ReasonText { get; }
    public int? ProgressDelta { get; }
    public int? ProgressBefore { get; }
    public int? ProgressAfter { get; }
    public int? AttributeBefore { get; }
    public int? AttributeAfter { get; }

    public CharacterAttributeChangeFact(
        StringName attributeId,
        string attributeLabel,
        int delta,
        string reasonText,
        int? progressDelta = null,
        int? progressBefore = null,
        int? progressAfter = null,
        int? attributeBefore = null,
        int? attributeAfter = null
    )
    {
        AttributeId = attributeId;
        AttributeLabel = attributeLabel ?? "";
        Delta = delta;
        ReasonText = reasonText ?? "";
        ProgressDelta = progressDelta;
        ProgressBefore = progressBefore;
        ProgressAfter = progressAfter;
        AttributeBefore = attributeBefore;
        AttributeAfter = attributeAfter;
    }

    public static CharacterAttributeChangeFact PermanentDelta(
        StringName attributeId,
        string attributeLabel,
        int delta,
        string reasonText
    ) => new(attributeId, attributeLabel, delta, reasonText);

    public static CharacterAttributeChangeFact GrowthResult(
        string attributeLabel,
        AttributeGrowthResult growthResult
    )
    {
        if (growthResult == null)
            return null;
        return new CharacterAttributeChangeFact(
            growthResult.AttributeId,
            attributeLabel,
            growthResult.AttributeDelta,
            growthResult.ReasonText,
            growthResult.ProgressDelta,
            growthResult.ProgressBefore,
            growthResult.ProgressAfter,
            growthResult.AttributeBefore,
            growthResult.AttributeAfter
        );
    }

}
