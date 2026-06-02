using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class PracticeSkillLearnStatus
{
    public readonly bool IsPracticeSkill;
    public readonly StringName TrackType;
    public readonly bool CanLearn;
    public readonly bool NeedsReplacement;
    public readonly StringName ExistingSkillId;
    public readonly int PredictedLevel;
    public readonly string ErrorCode;

    private PracticeSkillLearnStatus(
        bool isPracticeSkill,
        StringName trackType,
        bool canLearn,
        bool needsReplacement,
        StringName existingSkillId,
        int predictedLevel,
        string errorCode
    )
    {
        IsPracticeSkill = isPracticeSkill;
        TrackType = trackType;
        CanLearn = canLearn;
        NeedsReplacement = needsReplacement;
        ExistingSkillId = existingSkillId;
        PredictedLevel = predictedLevel;
        ErrorCode = errorCode ?? "";
    }

    public static PracticeSkillLearnStatus NonPractice() =>
        new(false, "", false, false, "", 0, "");

    public static PracticeSkillLearnStatus Practice(
        StringName trackType,
        bool canLearn,
        bool needsReplacement,
        StringName existingSkillId,
        string errorCode = ""
    ) =>
        new(true, trackType, canLearn, needsReplacement, existingSkillId, 0, errorCode);

    public PracticeSkillLearnStatus WithPredictedLevel(int predictedLevel) =>
        new(
            IsPracticeSkill,
            TrackType,
            CanLearn,
            NeedsReplacement,
            ExistingSkillId,
            predictedLevel,
            ErrorCode
        );

    public GDictionary ToCanLearnDictionary()
    {
        var result = new GDictionary
        {
            ["can_learn"] = CanLearn,
            ["needs_replacement"] = NeedsReplacement,
            ["existing_skill_id"] = ExistingSkillId,
        };
        if (!string.IsNullOrEmpty(ErrorCode))
            result["error_code"] = ErrorCode;
        return result;
    }

    public GDictionary ToLearnedStatusDictionary()
    {
        if (!IsPracticeSkill)
        {
            return new GDictionary
            {
                ["is_practice_skill"] = false,
                ["track_type"] = "",
                ["is_learned_direct"] = false,
                ["needs_replacement"] = false,
                ["existing_skill_id"] = "",
                ["predicted_level"] = 0,
            };
        }

        var result = ToCanLearnDictionary();
        result["is_practice_skill"] = true;
        result["track_type"] = TrackType;
        if (NeedsReplacement)
            result["predicted_level"] = PredictedLevel;
        return result;
    }
}
