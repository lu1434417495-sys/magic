using System.Collections.Generic;
using Godot;

public sealed class SkillMergeService
{
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();
    private ProfessionAssignmentService _assignment_service;

    public void Setup(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        ProfessionAssignmentService assignmentService = null
    )
    {
        _unit_progress = unitProgress;
        _skillDefs.Clear();
        if (skillDefs != null)
        {
            foreach (KeyValuePair<StringName, SkillDef> pair in skillDefs)
            {
                if (pair.Key != "" && pair.Value != null)
                    _skillDefs[pair.Key] = pair.Value;
            }
        }
        _assignment_service = assignmentService;
    }

    public bool MergeSkills(
        IEnumerable<StringName> sourceSkillIds,
        StringName resultSkillId,
        bool keepCore,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null || resultSkillId == "")
            return false;
        if (_unit_progress.IsSkillRelearnBlocked(resultSkillId))
            return false;
        List<StringName> normalizedSourceIds = NormalizeSourceSkillIds(
            sourceSkillIds,
            resultSkillId
        );
        if (normalizedSourceIds.Count == 0 || !AllSourceSkillsExist(normalizedSourceIds))
            return false;
        var resolvedTargetProfessionId = targetProfessionId;
        if (keepCore && IsEmpty(resolvedTargetProfessionId))
            resolvedTargetProfessionId = InferTargetProfessionIdFromSources(normalizedSourceIds);
        if (keepCore && IsEmpty(resolvedTargetProfessionId))
            return false;
        if (keepCore && GetProfessionProgress(resolvedTargetProfessionId) == null)
            return false;
        UnitSkillProgress resultProgress = GetOrCreateResultSkillProgress(
            resultSkillId,
            normalizedSourceIds
        );
        if (resultProgress == null)
            return false;
        DetachMergedSourceSkills(normalizedSourceIds);
        resultProgress.is_learned = true;
        resultProgress.is_core = keepCore;
        resultProgress.merged_from_skill_ids = ToStringNameArray(normalizedSourceIds);
        if (keepCore)
            resultProgress.assigned_profession_id = resolvedTargetProfessionId;
        else
            resultProgress.ClearProfessionAssignment();
        _unit_progress.RememberMergeSources(resultSkillId, ToStringNameArray(normalizedSourceIds));
        _unit_progress.SetSkillProgress(resultProgress);
        return AttachMergedResultSkill(resultSkillId, keepCore, resolvedTargetProfessionId);
    }

    internal bool ApplyCompositeUpgradeResult(
        StringName resultSkillId,
        IEnumerable<StringName> sourceSkillIds,
        bool retainSourceSkills,
        CoreSkillTransitionMode coreTransitionMode,
        StringName targetProfessionId = default
    )
    {
        if (_unit_progress == null || resultSkillId == "")
            return false;
        if (_unit_progress.IsSkillRelearnBlocked(resultSkillId))
            return false;
        List<StringName> normalizedSourceIds = NormalizeSourceSkillIds(
            sourceSkillIds,
            resultSkillId
        );
        if (normalizedSourceIds.Count == 0 || !AllSourceSkillsExist(normalizedSourceIds))
            return false;
        if (!retainSourceSkills)
        {
            return MergeSkills(
                normalizedSourceIds,
                resultSkillId,
                coreTransitionMode == CoreSkillTransitionMode.ReplaceSourcesWithResult,
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
        _unit_progress.RememberMergeSources(resultSkillId, ToStringNameArray(normalizedSourceIds));
        _unit_progress.SetSkillProgress(resultProgress);
        var resolvedTargetProfessionId = targetProfessionId;
        if (
            coreTransitionMode == CoreSkillTransitionMode.ReplaceSourcesWithResult
            && resolvedTargetProfessionId == ""
        )
            resolvedTargetProfessionId = InferTargetProfessionIdFromSources(normalizedSourceIds);
        if (
            coreTransitionMode == CoreSkillTransitionMode.ReplaceSourcesWithResult
            && !IsEmpty(resolvedTargetProfessionId)
        )
        {
            if (
                !ReplaceSourceCoresWithResult(
                    normalizedSourceIds,
                    resultSkillId,
                    resolvedTargetProfessionId
                )
            )
            {
                ClearCompositeTriggerReferences(normalizedSourceIds, resultSkillId);
                resultProgress.is_core = false;
                resultProgress.ClearProfessionAssignment();
            }
            else
            {
                resultProgress.is_core = true;
                resultProgress.assigned_profession_id = resolvedTargetProfessionId;
            }
        }
        else if (coreTransitionMode == CoreSkillTransitionMode.ReplaceSourcesWithResult)
        {
            ClearCompositeTriggerReferences(normalizedSourceIds, resultSkillId);
            resultProgress.is_core = false;
            resultProgress.ClearProfessionAssignment();
        }
        _unit_progress.SyncActiveCoreSkillIds();
        return true;
    }

    public void DetachMergedSourceSkills(IEnumerable<StringName> sourceSkillIds)
    {
        if (_unit_progress == null)
            return;
        List<StringName> normalizedSourceIds = NormalizeSourceSkillIds(sourceSkillIds);
        foreach (var sourceSkillId in normalizedSourceIds)
        {
            UnitSkillProgress sourceProgress =
                _unit_progress.GetSkillProgress(sourceSkillId);
            if (sourceProgress == null)
                continue;
            if (sourceProgress.merged_from_skill_ids.Count > 0)
                _unit_progress.RememberMergeSources(
                    sourceSkillId,
                    sourceProgress.merged_from_skill_ids
                );
            if (!IsEmpty(sourceProgress.assigned_profession_id))
                RemoveSourceSkillFromProfession(
                    sourceSkillId,
                    sourceProgress.assigned_profession_id
                );
            else
                RemoveSourceSkillFromAllProfessions(sourceSkillId);
            ClearLevelTriggerReferences(sourceSkillId);
            sourceProgress.ClearProfessionAssignment();
            _unit_progress.BlockSkillRelearn(sourceSkillId);
            _unit_progress.RemoveSkillProgress(sourceSkillId);
        }
        _unit_progress.SyncActiveCoreSkillIds();
    }

    public bool AttachMergedResultSkill(
        StringName resultSkillId,
        bool keepCore,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null)
            return false;
        UnitSkillProgress resultProgress = _unit_progress.GetSkillProgress(resultSkillId);
        if (resultProgress == null)
        {
            resultProgress = new UnitSkillProgress
            {
                skill_id = resultSkillId,
                is_learned = true,
            };
            _unit_progress.SetSkillProgress(resultProgress);
        }
        if (!keepCore)
        {
            ClearLevelTriggerReferences(resultSkillId);
            RemoveSourceSkillFromAllProfessions(resultSkillId);
            resultProgress.is_core = false;
            resultProgress.ClearProfessionAssignment();
            _unit_progress.SetSkillProgress(resultProgress);
            _unit_progress.SyncActiveCoreSkillIds();
            return true;
        }
        if (IsEmpty(targetProfessionId))
            return false;
        UnitProfessionProgress professionProgress = GetProfessionProgress(targetProfessionId);
        if (professionProgress == null)
            return false;
        RemoveSourceSkillFromAllProfessions(resultSkillId, targetProfessionId);
        resultProgress.is_learned = true;
        resultProgress.is_core = true;
        resultProgress.assigned_profession_id = targetProfessionId;
        professionProgress.AddCoreSkill(resultSkillId);
        _unit_progress.SetSkillProgress(resultProgress);
        _unit_progress.SyncActiveCoreSkillIds();
        return true;
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
            if (_unit_progress.GetSkillProgress(sourceSkillId) == null)
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
                _unit_progress.GetSkillProgress(sourceSkillId);
            if (
                sourceProgress == null
                || !sourceProgress.is_core
                || IsEmpty(sourceProgress.assigned_profession_id)
            )
                continue;
            if (IsEmpty(inferredProfessionId))
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
            _unit_progress.GetSkillProgress(resultSkillId);
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
                _unit_progress.GetSkillProgress(sourceSkillId);
            if (sourceProgress == null)
                continue;
            maxSkillLevel = Mathf.Max(maxSkillLevel, sourceProgress.skill_level);
            totalMastery += sourceProgress.total_mastery_earned;
            trainingMastery += sourceProgress.mastery_from_training;
            battleMastery += sourceProgress.mastery_from_battle;
            currentMastery = Mathf.Max(currentMastery, sourceProgress.current_mastery);
            if (IsEmpty(sourceProgress.profession_granted_by))
                continue;
            if (IsEmpty(grantedByProfessionId))
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
                SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(
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
            if (!IsEmpty(grantedByProfessionId))
            {
                resultProgress.granted_source_type = UnitSkillProgress.ToStringName(
                    UnitSkillGrantSourceType.Profession
                );
                resultProgress.granted_source_id = grantedByProfessionId;
            }
        }
        return resultProgress;
    }

    // 复合升级降级为非核心时，被消耗的 source 触发锁也必须清理，
    // 与 ReplaceSourceCoresWithResult 成功路径保持同一套语义。
    private void ClearCompositeTriggerReferences(
        IEnumerable<StringName> sourceSkillIds,
        StringName resultSkillId
    )
    {
        foreach (var sourceSkillId in sourceSkillIds)
            ClearLevelTriggerReferences(sourceSkillId);
        ClearLevelTriggerReferences(resultSkillId);
    }

    private void ClearLevelTriggerReferences(StringName skillId)
    {
        if (_unit_progress == null || skillId == "")
            return;
        if (_unit_progress.active_level_trigger_core_skill_id == skillId)
            _unit_progress.active_level_trigger_core_skill_id = "";
        _unit_progress.RemoveLockedLevelTriggerSkillId(skillId);
        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null)
            return;
        skillProgress.is_level_trigger_active = false;
        skillProgress.is_level_trigger_locked = false;
        _unit_progress.SetSkillProgress(skillProgress);
    }

    private void RemoveSourceSkillFromProfession(StringName skillId, StringName professionId)
    {
        if (IsEmpty(professionId))
            return;
        if (_assignment_service != null)
        {
            _assignment_service.RemoveCoreSkillFromProfession(skillId, professionId);
            return;
        }
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        professionProgress?.RemoveCoreSkill(skillId);
    }

    private void RemoveSourceSkillFromAllProfessions(
        StringName skillId,
        StringName exceptProfessionId = default
    )
    {
        if (_unit_progress == null)
            return;
        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            if (!IsEmpty(exceptProfessionId) && professionId == exceptProfessionId)
                continue;
            UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
            professionProgress?.RemoveCoreSkill(skillId);
        }
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId) =>
        IsEmpty(professionId) ? null : _unit_progress?.GetProfessionProgress(professionId);

    private bool ReplaceSourceCoresWithResult(
        IEnumerable<StringName> sourceSkillIds,
        StringName resultSkillId,
        StringName targetProfessionId
    )
    {
        if (_unit_progress == null || IsEmpty(targetProfessionId))
            return false;
        UnitProfessionProgress professionProgress = GetProfessionProgress(targetProfessionId);
        if (professionProgress == null)
            return false;
        foreach (var sourceSkillId in sourceSkillIds)
        {
            UnitSkillProgress sourceProgress =
                _unit_progress.GetSkillProgress(sourceSkillId);
            if (sourceProgress == null || sourceProgress.assigned_profession_id != targetProfessionId)
                continue;
            ClearLevelTriggerReferences(sourceSkillId);
            sourceProgress.is_core = false;
            sourceProgress.ClearProfessionAssignment();
            professionProgress.RemoveCoreSkill(sourceSkillId);
        }
        RemoveSourceSkillFromAllProfessions(resultSkillId, targetProfessionId);
        UnitSkillProgress resultProgress =
            _unit_progress.GetSkillProgress(resultSkillId);
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
        professionProgress.AddCoreSkill(resultSkillId);
        _unit_progress.SetSkillProgress(resultProgress);
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

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }
}
