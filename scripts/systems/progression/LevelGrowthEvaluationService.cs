using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class LevelGrowthEvaluationService
{
    private const int LOCK_HIT_BONUS_DEFAULT = 1;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();

    public void setup(GDictionary skillDefs)
    {
        _skillDefs.Clear();
        if (skillDefs == null)
            return;
        foreach (Variant rawKey in skillDefs.Keys)
        {
            if (!TryReadStringName(rawKey, out StringName skillId) || skillId == "")
                continue;
            Variant rawDef = skillDefs[rawKey];
            if (rawDef.VariantType == Variant.Type.Object && rawDef.AsGodotObject() is SkillDef skillDef)
                _skillDefs[skillId] = skillDef;
        }
    }

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
        if (skillId == "" || !_skillDefs.TryGetValue(skillId, out SkillDef skillDef))
            return null;
        return skillDef;
    }

    private static bool TryReadStringName(Variant value, out StringName result)
    {
        if (value.VariantType == Variant.Type.StringName)
        {
            result = value.AsStringName();
            return true;
        }
        if (value.VariantType == Variant.Type.String)
        {
            result = new StringName(value.AsString());
            return true;
        }
        result = "";
        return false;
    }
}
