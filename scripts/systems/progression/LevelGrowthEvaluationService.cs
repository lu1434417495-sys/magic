using System.Collections.Generic;
using Godot;

public sealed class LevelGrowthEvaluationService
{
    private const int LOCK_HIT_BONUS_DEFAULT = 1;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();

    public void Setup(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        _skillDefs.Clear();
        if (skillDefs == null)
            return;
        foreach (KeyValuePair<StringName, SkillDef> pair in skillDefs)
        {
            if (pair.Key != "" && pair.Value != null)
                _skillDefs[pair.Key] = pair.Value;
        }
    }

    public LevelGrowthTriggerResult SetActiveTriggerCoreSkillTyped(
        PartyMemberState memberState,
        StringName skillId
    )
    {
        if (memberState == null || memberState.progression == null)
            return LevelGrowthTriggerResult.Fail("invalid_member_state");
        var unitProgress = memberState.progression;
        var skillProgress = unitProgress.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return LevelGrowthTriggerResult.Fail("skill_not_learned");
        if (!skillProgress.is_core)
            return LevelGrowthTriggerResult.Fail("skill_not_core");
        if (skillProgress.is_level_trigger_locked)
            return LevelGrowthTriggerResult.Fail("skill_already_locked");
        if (unitProgress.HasLockedLevelTriggerSkillId(skillId))
            return LevelGrowthTriggerResult.Fail("skill_already_locked");

        var previousActive = unitProgress.active_level_trigger_core_skill_id;
        if (previousActive != "" && previousActive != skillId)
        {
            var prevSkillProgress = unitProgress.GetSkillProgress(previousActive);
            if (prevSkillProgress != null)
                prevSkillProgress.is_level_trigger_active = false;
        }

        unitProgress.active_level_trigger_core_skill_id = skillId;
        skillProgress.is_level_trigger_active = true;
        return LevelGrowthTriggerResult.SetSuccess(skillId, previousActive);
    }

    public LevelGrowthTriggerResult ClearActiveTriggerCoreSkillTyped(
        PartyMemberState memberState
    )
    {
        if (memberState == null || memberState.progression == null)
            return LevelGrowthTriggerResult.Fail("invalid_member_state");
        var unitProgress = memberState.progression;
        var currentActive = unitProgress.active_level_trigger_core_skill_id;
        if (currentActive != "")
        {
            var skillProgress = unitProgress.GetSkillProgress(currentActive);
            if (skillProgress != null)
                skillProgress.is_level_trigger_active = false;
        }
        unitProgress.active_level_trigger_core_skill_id = "";
        return LevelGrowthTriggerResult.ClearSuccess();
    }

    public bool HasActiveTriggerCoreSkill(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        return memberState.progression.active_level_trigger_core_skill_id != "";
    }

    public bool IsActiveTriggerReadyForLevelUp(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        var unitProgress = memberState.progression;
        var triggerSkillId = unitProgress.active_level_trigger_core_skill_id;
        if (triggerSkillId == "")
            return false;
        var skillProgress = unitProgress.GetSkillProgress(triggerSkillId);
        SkillDef skillDef = GetSkillDef(triggerSkillId);
        if (skillProgress == null || skillDef == null)
            return false;
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return false;
        if (skillProgress.is_level_trigger_locked)
            return false;
        return SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
            skillDef,
            skillProgress,
            unitProgress
        );
    }

    public LevelGrowthTriggerResult ApplyLevelUpTyped(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return LevelGrowthTriggerResult.Fail("invalid_member_state");
        var unitProgress = memberState.progression;

        var triggerSkillId = unitProgress.active_level_trigger_core_skill_id;
        if (triggerSkillId == "")
            return LevelGrowthTriggerResult.Fail("no_active_trigger_core_skill");

        var skillProgress = unitProgress.GetSkillProgress(triggerSkillId);
        if (skillProgress == null)
            return LevelGrowthTriggerResult.Fail("trigger_skill_not_found");
        if (skillProgress.is_level_trigger_locked)
            return LevelGrowthTriggerResult.Fail("trigger_skill_already_locked");
        if (!IsActiveTriggerReadyForLevelUp(memberState))
            return LevelGrowthTriggerResult.Fail("trigger_skill_not_ready");

        skillProgress.is_level_trigger_active = false;
        skillProgress.is_level_trigger_locked = true;
        skillProgress.bonus_to_hit_from_lock = LOCK_HIT_BONUS_DEFAULT;
        unitProgress.active_level_trigger_core_skill_id = "";
        if (!unitProgress.HasLockedLevelTriggerSkillId(triggerSkillId))
            unitProgress.AddLockedLevelTriggerSkillId(triggerSkillId);

        return LevelGrowthTriggerResult.LevelUpSuccess(triggerSkillId);
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        if (skillId == "" || !_skillDefs.TryGetValue(skillId, out SkillDef skillDef))
            return null;
        return skillDef;
    }
}
