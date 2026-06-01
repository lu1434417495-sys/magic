using System.Collections.Generic;
using Godot;

public sealed class SkillMergeService
{
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();
    private ProfessionAssignmentService _assignment_service;

    public void setup(
        UnitProgress unitProgress,
        Godot.Collections.Dictionary skillDefs,
        ProfessionAssignmentService assignmentService = null
    )
    {
        _unit_progress = unitProgress;
        _skillDefs.Clear();
        foreach (KeyValuePair<StringName, SkillDef> pair in IndexSkillDefs(skillDefs))
            _skillDefs[pair.Key] = pair.Value;
        _assignment_service = assignmentService;
    }

    public bool merge_skills(
        IEnumerable<StringName> sourceSkillIds,
        StringName resultSkillId,
        bool keepCore,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null || resultSkillId == "")
            return false;
        if (_unit_progress.is_skill_relearn_blocked(resultSkillId))
            return false;
        List<StringName> normalizedSourceIds = NormalizeSourceSkillIds(
            sourceSkillIds,
            resultSkillId
        );
        if (normalizedSourceIds.Count == 0 || !AllSourceSkillsExist(normalizedSourceIds))
            return false;
        var resolvedTargetProfessionId = targetProfessionId;
        if (keepCore && resolvedTargetProfessionId == "")
            resolvedTargetProfessionId = InferTargetProfessionIdFromSources(normalizedSourceIds);
        if (keepCore && resolvedTargetProfessionId == "")
            return false;
        if (keepCore && GetProfessionProgress(resolvedTargetProfessionId) == null)
            return false;
        UnitSkillProgress resultProgress = GetOrCreateResultSkillProgress(
            resultSkillId,
            normalizedSourceIds
        );
        if (resultProgress == null)
            return false;
        detach_merged_source_skills(normalizedSourceIds);
        resultProgress.is_learned = true;
        resultProgress.is_core = keepCore;
        resultProgress.merged_from_skill_ids = ToStringNameArray(normalizedSourceIds);
        if (keepCore)
            resultProgress.assigned_profession_id = resolvedTargetProfessionId;
        else
            resultProgress.clear_profession_assignment();
        _unit_progress.remember_merge_sources(resultSkillId, ToStringNameArray(normalizedSourceIds));
        _unit_progress.set_skill_progress(resultProgress);
        return attach_merged_result_skill(resultSkillId, keepCore, resolvedTargetProfessionId);
    }

    public bool apply_composite_upgrade_result(
        StringName resultSkillId,
        IEnumerable<StringName> sourceSkillIds,
        bool retainSourceSkills,
        StringName coreTransitionMode,
        StringName targetProfessionId = default
    )
    {
        if (_unit_progress == null || resultSkillId == "")
            return false;
        if (_unit_progress.is_skill_relearn_blocked(resultSkillId))
            return false;
        List<StringName> normalizedSourceIds = NormalizeSourceSkillIds(
            sourceSkillIds,
            resultSkillId
        );
        if (normalizedSourceIds.Count == 0 || !AllSourceSkillsExist(normalizedSourceIds))
            return false;
        if (!retainSourceSkills)
        {
            return merge_skills(
                normalizedSourceIds,
                resultSkillId,
                coreTransitionMode == "replace_sources_with_result",
                targetProfessionId
            );
        }

        UnitSkillProgress resultProgress = GetOrCreateResultSkillProgress(
            resultSkillId,
            normalizedSourceIds
        );
        if (resultProgress == null)
            return false;
        resultProgress.is_learned = true;
        resultProgress.merged_from_skill_ids = ToStringNameArray(normalizedSourceIds);
        _unit_progress.remember_merge_sources(resultSkillId, ToStringNameArray(normalizedSourceIds));
        _unit_progress.set_skill_progress(resultProgress);
        var resolvedTargetProfessionId = targetProfessionId;
        if (coreTransitionMode == "replace_sources_with_result" && resolvedTargetProfessionId == "")
            resolvedTargetProfessionId = InferTargetProfessionIdFromSources(normalizedSourceIds);
        if (coreTransitionMode == "replace_sources_with_result" && resolvedTargetProfessionId != "")
        {
            if (
                !ReplaceSourceCoresWithResult(
                    normalizedSourceIds,
                    resultSkillId,
                    resolvedTargetProfessionId
                )
            )
            {
                ClearLevelTriggerReferences(resultSkillId);
                resultProgress.is_core = false;
                resultProgress.clear_profession_assignment();
            }
            else
            {
                resultProgress.is_core = true;
                resultProgress.assigned_profession_id = resolvedTargetProfessionId;
            }
        }
        else if (coreTransitionMode == "replace_sources_with_result")
        {
            ClearLevelTriggerReferences(resultSkillId);
            resultProgress.is_core = false;
            resultProgress.clear_profession_assignment();
        }
        _unit_progress.sync_active_core_skill_ids();
        return true;
    }

    public void detach_merged_source_skills(IEnumerable<StringName> sourceSkillIds)
    {
        if (_unit_progress == null)
            return;
        List<StringName> normalizedSourceIds = NormalizeSourceSkillIds(sourceSkillIds);
        foreach (var sourceSkillId in normalizedSourceIds)
        {
            UnitSkillProgress sourceProgress =
                _unit_progress.get_skill_progress(sourceSkillId);
            if (sourceProgress == null)
                continue;
            if (sourceProgress.merged_from_skill_ids.Count > 0)
                _unit_progress.remember_merge_sources(
                    sourceSkillId,
                    sourceProgress.merged_from_skill_ids
                );
            if (sourceProgress.assigned_profession_id != "")
                RemoveSourceSkillFromProfession(
                    sourceSkillId,
                    sourceProgress.assigned_profession_id
                );
            else
                RemoveSourceSkillFromAllProfessions(sourceSkillId);
            ClearLevelTriggerReferences(sourceSkillId);
            sourceProgress.clear_profession_assignment();
            _unit_progress.block_skill_relearn(sourceSkillId);
            _unit_progress.remove_skill_progress(sourceSkillId);
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
        UnitSkillProgress resultProgress = _unit_progress.get_skill_progress(resultSkillId);
        if (resultProgress == null)
        {
            resultProgress = new UnitSkillProgress
            {
                skill_id = resultSkillId,
                is_learned = true,
            };
            _unit_progress.set_skill_progress(resultProgress);
        }
        if (!keepCore)
        {
            ClearLevelTriggerReferences(resultSkillId);
            RemoveSourceSkillFromAllProfessions(resultSkillId);
            resultProgress.is_core = false;
            resultProgress.clear_profession_assignment();
            _unit_progress.set_skill_progress(resultProgress);
            _unit_progress.sync_active_core_skill_ids();
            return true;
        }
        if (targetProfessionId == "")
            return false;
        UnitProfessionProgress professionProgress = GetProfessionProgress(targetProfessionId);
        if (professionProgress == null)
            return false;
        RemoveSourceSkillFromAllProfessions(resultSkillId, targetProfessionId);
        resultProgress.is_learned = true;
        resultProgress.is_core = true;
        resultProgress.assigned_profession_id = targetProfessionId;
        professionProgress.add_core_skill(resultSkillId);
        _unit_progress.set_skill_progress(resultProgress);
        _unit_progress.sync_active_core_skill_ids();
        return true;
    }

    private static Dictionary<StringName, SkillDef> IndexSkillDefs(
        Godot.Collections.Dictionary skillDefs
    )
    {
        var result = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
            return result;

        foreach (Variant rawKey in skillDefs.Keys)
        {
            Variant rawDef = skillDefs[rawKey];
            if (rawDef.VariantType != Variant.Type.Object)
                continue;
            var skillDef = rawDef.AsGodotObject() as SkillDef;
            if (skillDef == null)
                continue;
            var skillId = skillDef.skill_id;
            if (skillId == "" && TryReadStringName(rawKey, out StringName keyId))
                skillId = keyId;
            if (skillId != "")
                result[skillId] = skillDef;
        }
        return result;
    }

    private static List<StringName> NormalizeSourceSkillIds(
        IEnumerable<StringName> sourceSkillIds,
        StringName excludeSkillId = default
    )
    {
        var result = new List<StringName>();
        var seen = new HashSet<StringName>();
        if (sourceSkillIds == null)
            return result;
        foreach (var sourceSkillId in sourceSkillIds)
        {
            if (
                sourceSkillId == ""
                || (excludeSkillId != "" && sourceSkillId == excludeSkillId)
                || !seen.Add(sourceSkillId)
            )
                continue;
            result.Add(sourceSkillId);
        }
        return result;
    }

    private bool AllSourceSkillsExist(IEnumerable<StringName> sourceSkillIds)
    {
        foreach (var sourceSkillId in sourceSkillIds)
        {
            if (_unit_progress.get_skill_progress(sourceSkillId) == null)
                return false;
        }
        return true;
    }

    private StringName InferTargetProfessionIdFromSources(IEnumerable<StringName> sourceSkillIds)
    {
        StringName inferredProfessionId = "";
        foreach (var sourceSkillId in sourceSkillIds)
        {
            UnitSkillProgress sourceProgress =
                _unit_progress.get_skill_progress(sourceSkillId);
            if (
                sourceProgress == null
                || !sourceProgress.is_core
                || sourceProgress.assigned_profession_id == ""
            )
                continue;
            if (inferredProfessionId == "")
                inferredProfessionId = sourceProgress.assigned_profession_id;
            else if (inferredProfessionId != sourceProgress.assigned_profession_id)
                return "";
        }
        return inferredProfessionId;
    }

    private UnitSkillProgress GetOrCreateResultSkillProgress(
        StringName resultSkillId,
        IEnumerable<StringName> sourceSkillIds
    )
    {
        UnitSkillProgress existingProgress =
            _unit_progress.get_skill_progress(resultSkillId);
        if (existingProgress != null)
            return existingProgress;
        var resultProgress = new UnitSkillProgress
        {
            skill_id = resultSkillId,
            is_learned = true,
        };
        int maxSkillLevel = 0;
        int totalMastery = 0;
        int trainingMastery = 0;
        int battleMastery = 0;
        int currentMastery = 0;
        StringName grantedByProfessionId = "";
        bool hasProfessionGrantConflict = false;
        foreach (var sourceSkillId in sourceSkillIds)
        {
            UnitSkillProgress sourceProgress =
                _unit_progress.get_skill_progress(sourceSkillId);
            if (sourceProgress == null)
                continue;
            maxSkillLevel = Mathf.Max(maxSkillLevel, sourceProgress.skill_level);
            totalMastery += sourceProgress.total_mastery_earned;
            trainingMastery += sourceProgress.mastery_from_training;
            battleMastery += sourceProgress.mastery_from_battle;
            currentMastery = Mathf.Max(currentMastery, sourceProgress.current_mastery);
            if (sourceProgress.profession_granted_by == "")
                continue;
            if (grantedByProfessionId == "")
                grantedByProfessionId = sourceProgress.profession_granted_by;
            else if (grantedByProfessionId != sourceProgress.profession_granted_by)
                hasProfessionGrantConflict = true;
        }
        SkillDef resultSkillDef = _skillDefs.TryGetValue(resultSkillId, out SkillDef foundSkillDef)
            ? foundSkillDef
            : null;
        if (resultSkillDef != null)
        {
            maxSkillLevel = Mathf.Min(
                maxSkillLevel,
                SkillEffectiveMaxLevelRules.get_effective_max_level(
                    resultSkillDef,
                    resultProgress,
                    _unit_progress
                )
            );
        }
        resultProgress.skill_level = maxSkillLevel;
        resultProgress.current_mastery = currentMastery;
        resultProgress.total_mastery_earned = totalMastery;
        resultProgress.mastery_from_training = trainingMastery;
        resultProgress.mastery_from_battle = battleMastery;
        if (!hasProfessionGrantConflict)
        {
            resultProgress.profession_granted_by = grantedByProfessionId;
            if (grantedByProfessionId != "")
            {
                resultProgress.granted_source_type = "profession";
                resultProgress.granted_source_id = grantedByProfessionId;
            }
        }
        return resultProgress;
    }

    private void ClearLevelTriggerReferences(StringName skillId)
    {
        if (_unit_progress == null || skillId == "")
            return;
        if (_unit_progress.active_level_trigger_core_skill_id == skillId)
            _unit_progress.active_level_trigger_core_skill_id = "";
        _unit_progress.locked_level_trigger_skill_ids.Remove(skillId);
        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress == null)
            return;
        skillProgress.is_level_trigger_active = false;
        skillProgress.is_level_trigger_locked = false;
        _unit_progress.set_skill_progress(skillProgress);
    }

    private void RemoveSourceSkillFromProfession(StringName skillId, StringName professionId)
    {
        if (_assignment_service != null)
        {
            _assignment_service.remove_core_skill_from_profession(skillId, professionId);
            return;
        }
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        professionProgress?.remove_core_skill(skillId);
    }

    private void RemoveSourceSkillFromAllProfessions(
        StringName skillId,
        StringName exceptProfessionId = default
    )
    {
        if (_unit_progress == null)
            return;
        var professions = _unit_progress.professions;
        foreach (Variant rawProfessionId in professions.Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(rawProfessionId);
            if (exceptProfessionId != "" && professionId == exceptProfessionId)
                continue;
            UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
            professionProgress?.remove_core_skill(skillId);
        }
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId) =>
        _unit_progress?.get_profession_progress(professionId);

    private bool ReplaceSourceCoresWithResult(
        IEnumerable<StringName> sourceSkillIds,
        StringName resultSkillId,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null || targetProfessionId == "")
            return false;
        UnitProfessionProgress professionProgress = GetProfessionProgress(targetProfessionId);
        if (professionProgress == null)
            return false;
        foreach (var sourceSkillId in sourceSkillIds)
        {
            UnitSkillProgress sourceProgress =
                _unit_progress.get_skill_progress(sourceSkillId);
            if (sourceProgress == null || sourceProgress.assigned_profession_id != targetProfessionId)
                continue;
            ClearLevelTriggerReferences(sourceSkillId);
            sourceProgress.is_core = false;
            sourceProgress.clear_profession_assignment();
            professionProgress.remove_core_skill(sourceSkillId);
        }
        RemoveSourceSkillFromAllProfessions(resultSkillId, targetProfessionId);
        UnitSkillProgress resultProgress =
            _unit_progress.get_skill_progress(resultSkillId);
        if (resultProgress == null)
        {
            resultProgress = new UnitSkillProgress
            {
                skill_id = resultSkillId,
                is_learned = true,
            };
        }
        resultProgress.is_core = true;
        resultProgress.assigned_profession_id = targetProfessionId;
        professionProgress.add_core_skill(resultSkillId);
        _unit_progress.set_skill_progress(resultProgress);
        return true;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
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
