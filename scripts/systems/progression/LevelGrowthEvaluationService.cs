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

    public Godot.Collections.Dictionary set_active_trigger_core_skill(GodotObject memberState, StringName skillId)
    {
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null)
            return _fail("invalid_member_state");
        var unitProgress = memberState.Get("progression").AsGodotObject();
        var skillProgress = unitProgress.Call("get_skill_progress", skillId).AsGodotObject();
        if (skillProgress == null || !(bool)skillProgress.Get("is_learned"))
            return _fail("skill_not_learned");
        if (!(bool)skillProgress.Get("is_core"))
            return _fail("skill_not_core");
        if ((bool)skillProgress.Get("is_level_trigger_locked"))
            return _fail("skill_already_locked");
        var lockedIds = unitProgress.Get("locked_level_trigger_skill_ids").AsGodotArray();
        if (lockedIds.Contains(skillId))
            return _fail("skill_already_locked");

        var previousActive = unitProgress.Get("active_level_trigger_core_skill_id").AsStringName();
        if (previousActive != "" && previousActive != skillId)
        {
            var prevSkillProgress = unitProgress.Call("get_skill_progress", previousActive).AsGodotObject();
            if (prevSkillProgress != null)
                prevSkillProgress.Set("is_level_trigger_active", false);
        }

        unitProgress.Set("active_level_trigger_core_skill_id", skillId);
        skillProgress.Set("is_level_trigger_active", true);
        return new Godot.Collections.Dictionary { { "ok", true }, { "skill_id", skillId }, { "previous_active", previousActive } };
    }

    public Godot.Collections.Dictionary clear_active_trigger_core_skill(GodotObject memberState)
    {
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null)
            return _fail("invalid_member_state");
        var unitProgress = memberState.Get("progression").AsGodotObject();
        var currentActive = unitProgress.Get("active_level_trigger_core_skill_id").AsStringName();
        if (currentActive != "")
        {
            var skillProgress = unitProgress.Call("get_skill_progress", currentActive).AsGodotObject();
            if (skillProgress != null)
                skillProgress.Set("is_level_trigger_active", false);
        }
        unitProgress.Set("active_level_trigger_core_skill_id", "");
        return new Godot.Collections.Dictionary { { "ok", true } };
    }

    public bool has_active_trigger_core_skill(GodotObject memberState)
    {
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null)
            return false;
        return memberState.Get("progression").AsGodotObject().Get("active_level_trigger_core_skill_id").AsStringName() != "";
    }

    public Godot.Collections.Dictionary get_trigger_skill_growth_progress(GodotObject memberState)
    {
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null)
            return new Godot.Collections.Dictionary();
        var unitProgress = memberState.Get("progression").AsGodotObject();
        var triggerSkillId = unitProgress.Get("active_level_trigger_core_skill_id").AsStringName();
        if (triggerSkillId == "")
            return new Godot.Collections.Dictionary();
        var skillDef = _skill_defs.ContainsKey(triggerSkillId) ? _skill_defs[triggerSkillId].AsGodotObject() : null;
        if (skillDef == null)
            return new Godot.Collections.Dictionary();
        return skillDef.Get("attribute_growth_progress").AsGodotDictionary().Duplicate();
    }

    public bool is_active_trigger_ready_for_level_up(GodotObject memberState)
    {
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null)
            return false;
        var unitProgress = memberState.Get("progression").AsGodotObject();
        var triggerSkillId = unitProgress.Get("active_level_trigger_core_skill_id").AsStringName();
        if (triggerSkillId == "")
            return false;
        var skillProgress = unitProgress.Call("get_skill_progress", triggerSkillId).AsGodotObject();
        var skillDef = _skill_defs.ContainsKey(triggerSkillId) ? _skill_defs[triggerSkillId].AsGodotObject() : null;
        if (skillProgress == null || skillDef == null)
            return false;
        if (!(bool)skillProgress.Get("is_learned") || !(bool)skillProgress.Get("is_core"))
            return false;
        if ((bool)skillProgress.Get("is_level_trigger_locked"))
            return false;
        return SkillEffectiveMaxLevelRules.is_at_effective_max_level(skillDef, Variant.From(skillProgress), unitProgress);
    }

    public Godot.Collections.Dictionary apply_level_up(GodotObject memberState)
    {
        if (memberState == null || memberState.Get("progression").AsGodotObject() == null)
            return _fail("invalid_member_state");
        var unitProgress = memberState.Get("progression").AsGodotObject();

        var triggerSkillId = unitProgress.Get("active_level_trigger_core_skill_id").AsStringName();
        if (triggerSkillId == "")
            return _fail("no_active_trigger_core_skill");

        var skillProgress = unitProgress.Call("get_skill_progress", triggerSkillId).AsGodotObject();
        if (skillProgress == null)
            return _fail("trigger_skill_not_found");
        if ((bool)skillProgress.Get("is_level_trigger_locked"))
            return _fail("trigger_skill_already_locked");
        if (!is_active_trigger_ready_for_level_up(memberState))
            return _fail("trigger_skill_not_ready");

        skillProgress.Set("is_level_trigger_active", false);
        skillProgress.Set("is_level_trigger_locked", true);
        skillProgress.Set("bonus_to_hit_from_lock", LOCK_HIT_BONUS_DEFAULT);
        unitProgress.Set("active_level_trigger_core_skill_id", "");
        var lockedIds = unitProgress.Get("locked_level_trigger_skill_ids").AsGodotArray();
        if (!lockedIds.Contains(triggerSkillId))
            lockedIds.Add(triggerSkillId);

        return new Godot.Collections.Dictionary { { "ok", true }, { "trigger_core_skill_id", triggerSkillId } };
    }

    private static Godot.Collections.Dictionary _fail(string reason)
    {
        return new Godot.Collections.Dictionary { { "ok", false }, { "error", reason } };
    }
}
