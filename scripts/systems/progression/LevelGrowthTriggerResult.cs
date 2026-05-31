using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class LevelGrowthTriggerResult
{
    public readonly bool Ok;
    public readonly string Error;
    public readonly StringName SkillId;
    public readonly StringName PreviousActive;
    public readonly StringName TriggerCoreSkillId;
    private readonly bool _includeSkillId;
    private readonly bool _includePreviousActive;
    private readonly bool _includeTriggerCoreSkillId;

    private LevelGrowthTriggerResult(
        bool ok,
        string error,
        StringName skillId,
        StringName previousActive,
        StringName triggerCoreSkillId,
        bool includeSkillId,
        bool includePreviousActive,
        bool includeTriggerCoreSkillId
    )
    {
        Ok = ok;
        Error = error ?? "";
        SkillId = skillId;
        PreviousActive = previousActive;
        TriggerCoreSkillId = triggerCoreSkillId;
        _includeSkillId = includeSkillId;
        _includePreviousActive = includePreviousActive;
        _includeTriggerCoreSkillId = includeTriggerCoreSkillId;
    }

    public static LevelGrowthTriggerResult Fail(string error) =>
        new(false, error, "", "", "", false, false, false);

    public static LevelGrowthTriggerResult SetSuccess(
        StringName skillId,
        StringName previousActive
    ) =>
        new(true, "", skillId, previousActive, "", true, true, false);

    public static LevelGrowthTriggerResult ClearSuccess() =>
        new(true, "", "", "", "", false, false, false);

    public static LevelGrowthTriggerResult LevelUpSuccess(StringName triggerCoreSkillId) =>
        new(true, "", "", "", triggerCoreSkillId, false, false, true);

    public GDictionary ToDictionary()
    {
        var result = new GDictionary { ["ok"] = Ok };
        if (!Ok)
            result["error"] = Error;
        if (_includeSkillId)
            result["skill_id"] = SkillId;
        if (_includePreviousActive)
            result["previous_active"] = PreviousActive;
        if (_includeTriggerCoreSkillId)
            result["trigger_core_skill_id"] = TriggerCoreSkillId;
        return result;
    }
}
