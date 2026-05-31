using Godot;

[GlobalClass]
public partial class LevelGrowthEvaluationService : RefCounted
{
    private const int LOCK_HIT_BONUS_DEFAULT = 1;
    private Godot.Collections.Dictionary _skill_defs = new();

    public void setup(Godot.Collections.Dictionary skillDefs)
    {
        _skill_defs = skillDefs;
    }

    public Godot.Collections.Dictionary set_active_trigger_core_skill(
        PartyMemberState memberState,
        StringName skillId
    ) => set_active_trigger_core_skill_typed(memberState, skillId).ToDictionary();

    public LevelGrowthTriggerResult set_active_trigger_core_skill_typed(
        PartyMemberState memberState,
        StringName skillId
    )
    {
        if (memberState == null || memberState.progression == null)
            return LevelGrowthTriggerResult.Fail("invalid_member_state");
        var unitProgress = memberState.progression;
        var skillProgress = unitProgress.get_skill_progress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return LevelGrowthTriggerResult.Fail("skill_not_learned");
        if (!skillProgress.is_core)
            return LevelGrowthTriggerResult.Fail("skill_not_core");
        if (skillProgress.is_level_trigger_locked)
            return LevelGrowthTriggerResult.Fail("skill_already_locked");
        var lockedIds = unitProgress.locked_level_trigger_skill_ids;
        if (lockedIds.Contains(skillId))
            return LevelGrowthTriggerResult.Fail("skill_already_locked");

        var previousActive = unitProgress.active_level_trigger_core_skill_id;
        if (previousActive != "" && previousActive != skillId)
        {
            var prevSkillProgress = unitProgress.get_skill_progress(previousActive);
            if (prevSkillProgress != null)
                prevSkillProgress.is_level_trigger_active = false;
        }

        unitProgress.active_level_trigger_core_skill_id = skillId;
        skillProgress.is_level_trigger_active = true;
        return LevelGrowthTriggerResult.SetSuccess(skillId, previousActive);
    }

    public Godot.Collections.Dictionary clear_active_trigger_core_skill(PartyMemberState memberState) =>
        clear_active_trigger_core_skill_typed(memberState).ToDictionary();

    public LevelGrowthTriggerResult clear_active_trigger_core_skill_typed(
        PartyMemberState memberState
    )
    {
        if (memberState == null || memberState.progression == null)
            return LevelGrowthTriggerResult.Fail("invalid_member_state");
        var unitProgress = memberState.progression;
        var currentActive = unitProgress.active_level_trigger_core_skill_id;
        if (currentActive != "")
        {
            var skillProgress = unitProgress.get_skill_progress(currentActive);
            if (skillProgress != null)
                skillProgress.is_level_trigger_active = false;
        }
        unitProgress.active_level_trigger_core_skill_id = "";
        return LevelGrowthTriggerResult.ClearSuccess();
    }

    public bool has_active_trigger_core_skill(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        return memberState.progression.active_level_trigger_core_skill_id != "";
    }

    public Godot.Collections.Dictionary get_trigger_skill_growth_progress(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return new Godot.Collections.Dictionary();
        var unitProgress = memberState.progression;
        var triggerSkillId = unitProgress.active_level_trigger_core_skill_id;
        if (triggerSkillId == "")
            return new Godot.Collections.Dictionary();
        SkillDef skillDef = GetSkillDef(triggerSkillId);
        if (skillDef == null)
            return new Godot.Collections.Dictionary();
        return skillDef.attribute_growth_progress.Duplicate();
    }

    public bool is_active_trigger_ready_for_level_up(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return false;
        var unitProgress = memberState.progression;
        var triggerSkillId = unitProgress.active_level_trigger_core_skill_id;
        if (triggerSkillId == "")
            return false;
        var skillProgress = unitProgress.get_skill_progress(triggerSkillId);
        SkillDef skillDef = GetSkillDef(triggerSkillId);
        if (skillProgress == null || skillDef == null)
            return false;
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return false;
        if (skillProgress.is_level_trigger_locked)
            return false;
        return SkillEffectiveMaxLevelRules.is_at_effective_max_level(
            skillDef,
            skillProgress,
            unitProgress
        );
    }

    public Godot.Collections.Dictionary apply_level_up(PartyMemberState memberState)
    {
        return apply_level_up_typed(memberState).ToDictionary();
    }

    public LevelGrowthTriggerResult apply_level_up_typed(PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
            return LevelGrowthTriggerResult.Fail("invalid_member_state");
        var unitProgress = memberState.progression;

        var triggerSkillId = unitProgress.active_level_trigger_core_skill_id;
        if (triggerSkillId == "")
            return LevelGrowthTriggerResult.Fail("no_active_trigger_core_skill");

        var skillProgress = unitProgress.get_skill_progress(triggerSkillId);
        if (skillProgress == null)
            return LevelGrowthTriggerResult.Fail("trigger_skill_not_found");
        if (skillProgress.is_level_trigger_locked)
            return LevelGrowthTriggerResult.Fail("trigger_skill_already_locked");
        if (!is_active_trigger_ready_for_level_up(memberState))
            return LevelGrowthTriggerResult.Fail("trigger_skill_not_ready");

        skillProgress.is_level_trigger_active = false;
        skillProgress.is_level_trigger_locked = true;
        skillProgress.bonus_to_hit_from_lock = LOCK_HIT_BONUS_DEFAULT;
        unitProgress.active_level_trigger_core_skill_id = "";
        var lockedIds = unitProgress.locked_level_trigger_skill_ids;
        if (!lockedIds.Contains(triggerSkillId))
            lockedIds.Add(triggerSkillId);

        return LevelGrowthTriggerResult.LevelUpSuccess(triggerSkillId);
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        if (skillId == "" || !_skill_defs.ContainsKey(skillId))
            return null;
        return _skill_defs[skillId].AsGodotObject() as SkillDef;
    }
}
