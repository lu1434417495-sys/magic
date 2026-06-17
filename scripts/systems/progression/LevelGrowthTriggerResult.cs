using Godot;

public sealed class LevelGrowthTriggerResult
{
    public readonly bool Ok;
    public readonly string Error;
    public readonly StringName SkillId;
    public readonly StringName PreviousActive;
    public readonly StringName TriggerCoreSkillId;

    private LevelGrowthTriggerResult(
        bool ok,
        string error,
        StringName skillId,
        StringName previousActive,
        StringName triggerCoreSkillId
    )
    {
        Ok = ok;
        Error = error ?? "";
        SkillId = skillId;
        PreviousActive = previousActive;
        TriggerCoreSkillId = triggerCoreSkillId;
    }

    public static LevelGrowthTriggerResult Fail(string error) =>
        new(false, error, "", "", "");

    public static LevelGrowthTriggerResult SetSuccess(
        StringName skillId,
        StringName previousActive
    ) =>
        new(true, "", skillId, previousActive, "");

    public static LevelGrowthTriggerResult ClearSuccess() =>
        new(true, "", "", "", "");

    public static LevelGrowthTriggerResult LevelUpSuccess(StringName triggerCoreSkillId) =>
        new(true, "", "", "", triggerCoreSkillId);
}
