using Godot;

public sealed class AttributeGrowthResult
{
    public readonly StringName AttributeId;
    public readonly int ProgressDelta;
    public readonly int ProgressBefore;
    public readonly int ProgressAfter;
    public readonly int AttributeBefore;
    public readonly int AttributeAfter;
    public readonly int AttributeDelta;
    public readonly string ReasonText;
    public readonly bool Applied;

    private AttributeGrowthResult(
        StringName attributeId,
        int progressDelta,
        int progressBefore,
        int progressAfter,
        int attributeBefore,
        int attributeAfter,
        int attributeDelta,
        string reasonText,
        bool applied
    )
    {
        AttributeId = attributeId;
        ProgressDelta = progressDelta;
        ProgressBefore = progressBefore;
        ProgressAfter = progressAfter;
        AttributeBefore = attributeBefore;
        AttributeAfter = attributeAfter;
        AttributeDelta = attributeDelta;
        ReasonText = reasonText ?? "";
        Applied = applied;
    }

    public static AttributeGrowthResult NotApplied(
        StringName attributeId,
        string reasonText = ""
    ) =>
        new(
            attributeId,
            0,
            0,
            0,
            0,
            0,
            0,
            reasonText,
            false
        );

    public static AttributeGrowthResult AppliedResult(
        StringName attributeId,
        int progressDelta,
        int progressBefore,
        int progressAfter,
        int attributeBefore,
        int attributeAfter,
        string reasonText = ""
    ) =>
        new(
            attributeId,
            progressDelta,
            progressBefore,
            progressAfter,
            attributeBefore,
            attributeAfter,
            attributeAfter - attributeBefore,
            reasonText,
            true
        );
}
