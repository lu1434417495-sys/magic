using Godot;

[GlobalClass]
public partial class SkillMergeService : RefCounted
{
    private UnitProgress _unit_progress;
    private Godot.Collections.Dictionary _skill_defs = new();
    private ProfessionAssignmentService _assignment_service;

    public void setup(
        UnitProgress unitProgress,
        Godot.Collections.Dictionary skillDefs,
        ProfessionAssignmentService assignmentService = null
    )
    {
        _unit_progress = unitProgress;
        _skill_defs = _index_skill_defs(skillDefs);
        _assignment_service = assignmentService;
    }

    public bool merge_skills(
        Godot.Collections.Array<StringName> sourceSkillIds,
        StringName resultSkillId,
        bool keepCore,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null || resultSkillId == "")
            return false;
        if (_unit_progress.is_skill_relearn_blocked(resultSkillId))
            return false;
        var nss = _normalize_source_skill_ids(sourceSkillIds, resultSkillId);
        if (nss.Count == 0 || !_all_source_skills_exist(nss))
            return false;
        var rtp = targetProfessionId;
        if (keepCore && rtp == "")
            rtp = _infer_target_profession_id_from_sources(nss);
        if (keepCore && rtp == "")
            return false;
        if (keepCore && _get_profession_progress(rtp) == null)
            return false;
        var rp = _get_or_create_result_skill_progress(resultSkillId, nss);
        if (rp == null)
            return false;
        detach_merged_source_skills(nss);
        rp.is_learned = true;
        rp.is_core = keepCore;
        rp.merged_from_skill_ids = new Godot.Collections.Array<StringName>(nss);
        if (keepCore)
            rp.assigned_profession_id = rtp;
        else
            rp.clear_profession_assignment();
        _unit_progress.remember_merge_sources(resultSkillId, nss);
        _unit_progress.set_skill_progress(rp);
        return attach_merged_result_skill(resultSkillId, keepCore, rtp);
    }

    public bool apply_composite_upgrade_result(
        StringName resultSkillId,
        Godot.Collections.Array<StringName> sourceSkillIds,
        bool retainSourceSkills,
        StringName coreTransitionMode,
        StringName targetProfessionId = default
    )
    {
        if (_unit_progress == null || resultSkillId == "")
            return false;
        if (_unit_progress.is_skill_relearn_blocked(resultSkillId))
            return false;
        var nss = _normalize_source_skill_ids(sourceSkillIds, resultSkillId);
        if (nss.Count == 0 || !_all_source_skills_exist(nss))
            return false;
        if (!retainSourceSkills)
            return merge_skills(
                nss,
                resultSkillId,
                coreTransitionMode == "replace_sources_with_result",
                targetProfessionId
            );
        var rp = _get_or_create_result_skill_progress(resultSkillId, nss);
        if (rp == null)
            return false;
        rp.is_learned = true;
        rp.merged_from_skill_ids = new Godot.Collections.Array<StringName>(nss);
        _unit_progress.remember_merge_sources(resultSkillId, nss);
        _unit_progress.set_skill_progress(rp);
        var rtp2 = targetProfessionId;
        if (coreTransitionMode == "replace_sources_with_result" && rtp2 == "")
            rtp2 = _infer_target_profession_id_from_sources(nss);
        if (coreTransitionMode == "replace_sources_with_result" && rtp2 != "")
        {
            if (!_replace_source_cores_with_result(nss, resultSkillId, rtp2))
            {
                _clear_level_trigger_references(resultSkillId);
                rp.is_core = false;
                rp.clear_profession_assignment();
            }
            else
            {
                rp.is_core = true;
                rp.assigned_profession_id = rtp2;
            }
        }
        else if (coreTransitionMode == "replace_sources_with_result")
        {
            _clear_level_trigger_references(resultSkillId);
            rp.is_core = false;
            rp.clear_profession_assignment();
        }
        _unit_progress.sync_active_core_skill_ids();
        return true;
    }

    public void detach_merged_source_skills(Godot.Collections.Array<StringName> sourceSkillIds)
    {
        if (_unit_progress == null)
            return;
        var nss = _normalize_source_skill_ids(sourceSkillIds);
        foreach (var sid in nss)
        {
            var sp =
                _unit_progress.get_skill_progress(sid);
            if (sp == null)
                continue;
            if (sp.merged_from_skill_ids.Count > 0)
                _unit_progress.remember_merge_sources(sid, sp.merged_from_skill_ids);
            if (sp.assigned_profession_id != "")
                _remove_source_skill_from_profession(sid, sp.assigned_profession_id);
            else
                _remove_source_skill_from_all_professions(sid);
            _clear_level_trigger_references(sid);
            sp.clear_profession_assignment();
            _unit_progress.block_skill_relearn(sid);
            _unit_progress.remove_skill_progress(sid);
        }
        _unit_progress.sync_active_core_skill_ids();
    }

    public bool attach_merged_result_skill(
        StringName resultSkillId,
        bool keepCore,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null)
            return false;
        var rsp = _unit_progress.get_skill_progress(resultSkillId);
        if (rsp == null)
        {
            rsp = new UnitSkillProgress { skill_id = resultSkillId, is_learned = true };
            _unit_progress.set_skill_progress(rsp);
        }
        if (!keepCore)
        {
            _clear_level_trigger_references(resultSkillId);
            _remove_source_skill_from_all_professions(resultSkillId);
            rsp.is_core = false;
            rsp.clear_profession_assignment();
            _unit_progress.set_skill_progress(rsp);
            _unit_progress.sync_active_core_skill_ids();
            return true;
        }
        if (targetProfessionId == "")
            return false;
        var pp = _get_profession_progress(targetProfessionId);
        if (pp == null)
            return false;
        _remove_source_skill_from_all_professions(resultSkillId, targetProfessionId);
        rsp.is_learned = true;
        rsp.is_core = true;
        rsp.assigned_profession_id = targetProfessionId;
        pp.add_core_skill(resultSkillId);
        _unit_progress.set_skill_progress(rsp);
        _unit_progress.sync_active_core_skill_ids();
        return true;
    }

    public Godot.Collections.Array<StringName> get_merged_source_skill_ids(StringName skillId) =>
        _unit_progress?.get_merged_source_skill_ids(skillId)
        ?? new Godot.Collections.Array<StringName>();

    public Godot.Collections.Array<StringName> get_merged_source_skill_ids_recursive(
        StringName skillId
    ) =>
        _unit_progress?.get_merged_source_skill_ids_recursive(skillId)
        ?? new Godot.Collections.Array<StringName>();

    private Godot.Collections.Dictionary _index_skill_defs(Godot.Collections.Dictionary skillDefs)
    {
        var r = new Godot.Collections.Dictionary();
        if (skillDefs == null)
            return r;

        foreach (var k in skillDefs.Keys)
        {
            var sd = skillDefs[k].AsGodotObject() as SkillDef;
            if (sd != null)
            {
                var idxId = sd.skill_id != "" ? sd.skill_id : ProgressionDataUtils.to_string_name(k);
                r[idxId] = sd;
            }
        }
        return r;
    }

    private static Godot.Collections.Array<StringName> _normalize_source_skill_ids(
        Godot.Collections.Array<StringName> sourceSkillIds,
        StringName excludeSkillId = default
    )
    {
        var r = new Godot.Collections.Array<StringName>();
        var s = new Godot.Collections.Dictionary();
        foreach (var sid in sourceSkillIds)
        {
            if (sid == "" || (excludeSkillId != "" && sid == excludeSkillId) || s.ContainsKey(sid))
                continue;
            s[sid] = true;
            r.Add(sid);
        }
        return r;
    }

    private bool _all_source_skills_exist(Godot.Collections.Array<StringName> sids)
    {
        foreach (var sid in sids)
            if (_unit_progress.get_skill_progress(sid) == null)
                return false;
        return true;
    }

    private StringName _infer_target_profession_id_from_sources(
        Godot.Collections.Array<StringName> sids
    )
    {
        StringName inferred = new StringName("");
        foreach (var sid in sids)
        {
            var sp =
                _unit_progress.get_skill_progress(sid);
            if (sp == null || !sp.is_core || sp.assigned_profession_id == "")
                continue;
            if (inferred == "")
                inferred = sp.assigned_profession_id;
            else if (inferred != sp.assigned_profession_id)
                return new StringName("");
        }
        return inferred;
    }

    private UnitSkillProgress _get_or_create_result_skill_progress(
        StringName rsid,
        Godot.Collections.Array<StringName> sids
    )
    {
        var existing =
            _unit_progress.get_skill_progress(rsid);
        if (existing != null)
            return existing;
        var rp = new UnitSkillProgress { skill_id = rsid, is_learned = true };
        int sml = 0,
            stm = 0,
            strm = 0,
            sbm = 0,
            scm = 0;
        StringName gbp = new StringName("");
        bool gpc = false;
        foreach (var sid in sids)
        {
            var sp =
                _unit_progress.get_skill_progress(sid);
            if (sp == null)
                continue;
            sml = Mathf.Max(sml, sp.skill_level);
            stm += sp.total_mastery_earned;
            strm += sp.mastery_from_training;
            sbm += sp.mastery_from_battle;
            scm = Mathf.Max(scm, sp.current_mastery);
            if (sp.profession_granted_by == "")
                continue;
            if (gbp == "")
                gbp = sp.profession_granted_by;
            else if (gbp != sp.profession_granted_by)
                gpc = true;
        }
        var rsd = _skill_defs.ContainsKey(rsid)
            ? _skill_defs[rsid].AsGodotObject() as SkillDef
            : null;
        if (rsd != null)
            sml = Mathf.Min(
                sml,
                SkillEffectiveMaxLevelRules.get_effective_max_level(
                    rsd,
                    rp,
                    _unit_progress
                )
            );
        rp.skill_level = sml;
        rp.current_mastery = scm;
        rp.total_mastery_earned = stm;
        rp.mastery_from_training = strm;
        rp.mastery_from_battle = sbm;
        if (!gpc)
        {
            rp.profession_granted_by = gbp;
            if (gbp != "")
            {
                rp.granted_source_type = "profession";
                rp.granted_source_id = gbp;
            }
        }
        return rp;
    }

    private void _clear_level_trigger_references(StringName sid)
    {
        if (_unit_progress == null || sid == "")
            return;
        if (_unit_progress.active_level_trigger_core_skill_id == sid)
            _unit_progress.active_level_trigger_core_skill_id = new StringName("");
        _unit_progress.locked_level_trigger_skill_ids.Remove(sid);
        var sp = _unit_progress.get_skill_progress(sid);
        if (sp == null)
            return;
        sp.is_level_trigger_active = false;
        sp.is_level_trigger_locked = false;
        _unit_progress.set_skill_progress(sp);
    }

    private void _remove_source_skill_from_profession(StringName sid, StringName pid)
    {
        if (_assignment_service != null)
        {
            _assignment_service.remove_core_skill_from_profession(sid, pid);
            return;
        }
        var pp = _get_profession_progress(pid);
        pp?.remove_core_skill(sid);
    }

    private void _remove_source_skill_from_all_professions(
        StringName sid,
        StringName exceptPid = default
    )
    {
        if (_unit_progress == null)
            return;
        var profs = _unit_progress.professions;
        foreach (var pk in profs.Keys)
        {
            var pid = ProgressionDataUtils.to_string_name(pk);
            if (exceptPid != "" && pid == exceptPid)
                continue;
            var pp = _get_profession_progress(pid);
            pp?.remove_core_skill(sid);
        }
    }

    private UnitProfessionProgress _get_profession_progress(StringName pid) =>
        _unit_progress?.get_profession_progress(pid);

    private bool _replace_source_cores_with_result(
        Godot.Collections.Array<StringName> sids,
        StringName rsid,
        StringName tpid
    )
    {
        if (_unit_progress == null || tpid == "")
            return false;
        var pp = _get_profession_progress(tpid);
        if (pp == null)
            return false;
        foreach (var sid in sids)
        {
            var sp =
                _unit_progress.get_skill_progress(sid);
            if (sp == null || sp.assigned_profession_id != tpid)
                continue;
            _clear_level_trigger_references(sid);
            sp.is_core = false;
            sp.clear_profession_assignment();
            pp.remove_core_skill(sid);
        }
        _remove_source_skill_from_all_professions(rsid, tpid);
        var rsp =
            _unit_progress.get_skill_progress(rsid);
        if (rsp == null)
        {
            rsp = new UnitSkillProgress { skill_id = rsid, is_learned = true };
        }
        rsp.is_core = true;
        rsp.assigned_profession_id = tpid;
        pp.add_core_skill(rsid);
        _unit_progress.set_skill_progress(rsp);
        return true;
    }
}
