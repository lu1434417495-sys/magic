using System.Collections.Generic;
using Godot;

public sealed class ProfessionRuleService
{
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDefinition> _skillDefinitions = new();
    private readonly Dictionary<StringName, ProfessionDefinition> _professionDefs = new();

    public void Setup(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefs
    )
    {
        _unit_progress = unitProgress;
        _skillDefinitions.Clear();
        _professionDefs.Clear();

        if (skillDefinitions != null)
        {
            foreach (KeyValuePair<StringName, SkillDefinition> pair in skillDefinitions)
            {
                if (pair.Key != "" && pair.Value != null)
                    _skillDefinitions[pair.Key] = pair.Value;
            }
        }

        if (professionDefs != null)
        {
            foreach (KeyValuePair<StringName, ProfessionDefinition> pair in professionDefs)
            {
                if (pair.Key != "" && pair.Value != null)
                    _professionDefs[pair.Key] = pair.Value;
            }
        }
    }

    public bool IsProfessionKnowledgeUnlocked(StringName professionId)
    {
        ProfessionDefinition professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return false;
        if (!professionDef.RequiresKnowledgeUnlock())
            return true;
        return _unit_progress != null
            && _unit_progress.HasKnowledge(professionDef.UnlockKnowledgeId);
    }

    public bool CanUnlockProfession(StringName professionId)
    {
        ProfessionDefinition professionDef = GetProfessionDef(professionId);
        if (professionDef == null || !IsProfessionKnowledgeUnlocked(professionId))
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress != null && professionProgress.rank > 0)
            return false;

        ProfessionPromotionRequirementDefinition unlockRequirement = professionDef.UnlockRequirement;
        if (unlockRequirement == null)
            return true;

        if (
            !CanSatisfyRequiredSkillIdsForUnlock(professionId, unlockRequirement.RequiredSkillIds)
        )
            return false;
        if (!CanSatisfyTagRulesForUnlock(professionId, unlockRequirement.RequiredTagRules))
            return false;
        if (!CanSatisfyProfessionGates(unlockRequirement.RequiredProfessionRanks))
            return false;
        if (!CanSatisfyAttributeRules(unlockRequirement.RequiredAttributeRules))
            return false;
        if (!CanSatisfyReputationRules(unlockRequirement.RequiredReputationRules))
            return false;

        return true;
    }

    public bool CanRankUpProfession(StringName professionId)
    {
        ProfessionDefinition professionDef = GetProfessionDef(professionId);
        if (professionDef == null || !IsProfessionKnowledgeUnlocked(professionId))
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress == null || professionProgress.rank <= 0)
            return false;
        if (professionProgress.rank >= professionDef.MaxRank)
            return false;

        int targetRank = professionProgress.rank + 1;
        ProfessionRankRequirementDefinition rankRequirement = professionDef.GetRankRequirement(targetRank);
        if (rankRequirement == null)
            return false;

        List<StringName> previewAssignedSkillIds = GetRankUpPreviewAssignedCoreSkillIds(
            professionId
        );
        if (
            !CanSatisfyTagRulesWithSkillIds(
                GetRankUpCandidateSkillIds(professionId, previewAssignedSkillIds),
                professionId,
                rankRequirement.RequiredTagRules,
                false,
                previewAssignedSkillIds
            )
        )
        {
            return false;
        }

        if (!CanSatisfyProfessionGates(rankRequirement.RequiredProfessionRanks))
            return false;
        if (!CanSatisfyAttributeRules(rankRequirement.RequiredAttributeRules))
            return false;
        if (!CanSatisfyReputationRules(rankRequirement.RequiredReputationRules))
            return false;

        return true;
    }

    public bool CanSatisfyTagRules(
        StringName professionId,
        IEnumerable<TagRequirementDefinition> tagRules
    )
    {
        return CanSatisfyTagRulesWithSkillIds(
            GetRankUpCandidateSkillIds(professionId),
            professionId,
            tagRules,
            false
        );
    }

    public bool CanSatisfyProfessionGates(IEnumerable<ProfessionRankGateDefinition> gates)
    {
        if (gates == null)
            return true;

        foreach (ProfessionRankGateDefinition gate in gates)
        {
            if (gate == null)
                continue;

            UnitProfessionProgress professionProgress = GetProfessionProgress(gate.ProfessionId);
            if (professionProgress == null || professionProgress.rank < gate.MinRank)
                return false;

            ProfessionGateCheckMode checkMode = ResolveGateCheckMode(gate);
            if (
                checkMode == ProfessionGateCheckMode.ActiveOnly
                && (!professionProgress.is_active || professionProgress.is_hidden)
            )
                return false;
        }
        return true;
    }

    public bool CanSatisfyAttributeRules(IEnumerable<AttributeRequirementDefinition> rules)
    {
        UnitBaseAttributes unitBaseAttributes = _unit_progress?.unit_base_attributes;
        if (rules == null)
            return true;

        foreach (AttributeRequirementDefinition rule in rules)
        {
            if (rule == null)
                continue;
            if (unitBaseAttributes == null)
                return false;
            if (!rule.MatchesValue(unitBaseAttributes.GetAttributeValue(rule.AttributeId)))
                return false;
        }
        return true;
    }

    public bool CanSatisfyReputationRules(IEnumerable<ReputationRequirementDefinition> rules)
    {
        UnitReputationState reputationState = _unit_progress?.reputation_state;
        if (rules == null)
            return true;

        foreach (ReputationRequirementDefinition rule in rules)
        {
            if (rule == null)
                continue;
            if (reputationState == null)
                return false;
            if (!rule.MatchesValue(reputationState.GetReputationValue(rule.StateId)))
                return false;
        }
        return true;
    }

    public IReadOnlyList<StringName> GetEligibleSkillIds(
        StringName professionId,
        IEnumerable<TagRequirementDefinition> tagRules,
        bool allowUnassigned
    )
    {
        List<TagRequirementDefinition> normalizedTagRules = NormalizeTagRules(tagRules);
        List<StringName> eligibleSkillIds = new();
        if (_unit_progress == null || normalizedTagRules.Count == 0)
            return eligibleSkillIds;

        foreach (StringName skillId in GetAllLearnedSkillIds())
        {
            if (MatchesAnyTagRule(skillId, professionId, normalizedTagRules, allowUnassigned))
                eligibleSkillIds.Add(skillId);
        }
        return eligibleSkillIds;
    }

    public bool SkillMatchesTagRequirement(
        StringName skillId,
        StringName professionId,
        TagRequirementDefinition tagRule,
        bool allowUnassigned,
        IEnumerable<StringName> previewAssignedSkillIds = null
    )
    {
        return MatchesTagRequirement(
            skillId,
            professionId,
            tagRule,
            allowUnassigned,
            previewAssignedSkillIds
        );
    }

    public bool EvaluateProfessionActiveState(StringName professionId)
    {
        ProfessionDefinition professionDef = GetProfessionDef(professionId);
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionDef == null || professionProgress == null)
            return false;
        if (professionProgress.rank <= 0)
            return false;
        return AreActiveConditionsSatisfied(professionDef);
    }

    public void RefreshAllProfessionStates()
    {
        if (_unit_progress == null)
            return;

        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
            ProfessionDefinition professionDef = GetProfessionDef(professionId);
            if (professionProgress == null || professionDef == null)
                continue;

            if (professionProgress.rank <= 0)
            {
                professionProgress.is_active = false;
                professionProgress.is_hidden = false;
                professionProgress.inactive_reason = "";
                continue;
            }

            bool conditionsSatisfied = AreActiveConditionsSatisfied(professionDef);
            if (conditionsSatisfied)
            {
                if (professionProgress.is_active)
                {
                    professionProgress.is_hidden = false;
                    professionProgress.inactive_reason = "";
                    continue;
                }

                if (professionDef.ReactivationModeKind == ProfessionReactivationMode.Auto)
                {
                    professionProgress.is_active = true;
                    professionProgress.is_hidden = false;
                    professionProgress.inactive_reason = "";
                }
                else
                {
                    professionProgress.is_hidden = true;
                    professionProgress.inactive_reason = "manual_reactivation_required";
                }
            }
            else
            {
                professionProgress.is_active = false;
                professionProgress.is_hidden = true;
                professionProgress.inactive_reason = "active_conditions_not_met";
            }
        }
    }

    private bool CanSatisfyRequiredSkillIdsForUnlock(
        StringName professionId,
        IEnumerable<StringName> requiredSkillIds
    )
    {
        if (requiredSkillIds == null)
            return true;

        foreach (StringName requiredSkillId in requiredSkillIds)
        {
            if (!IsSkillEligibleForUnlock(requiredSkillId, professionId))
                return false;
        }
        return true;
    }

    private bool CanSatisfyTagRulesForUnlock(
        StringName professionId,
        IEnumerable<TagRequirementDefinition> tagRules
    )
    {
        return CanSatisfyTagRulesWithSkillIds(
            GetUnlockCandidateSkillIds(professionId),
            professionId,
            tagRules,
            true
        );
    }

    private bool CanSatisfyTagRulesWithSkillIds(
        IEnumerable<StringName> candidateSkillIds,
        StringName professionId,
        IEnumerable<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IEnumerable<StringName> previewAssignedSkillIds = null
    )
    {
        List<TagRequirementDefinition> normalizedTagRules = NormalizeTagRules(tagRules);
        if (normalizedTagRules.Count == 0)
            return true;

        List<StringName> normalizedCandidateSkillIds = NormalizeSkillIds(candidateSkillIds);
        foreach (TagRequirementDefinition tagRule in normalizedTagRules)
        {
            int matchedCount = 0;
            foreach (StringName skillId in normalizedCandidateSkillIds)
            {
                if (
                    MatchesTagRequirement(
                        skillId,
                        professionId,
                        tagRule,
                        allowUnassigned,
                        previewAssignedSkillIds
                    )
                )
                    matchedCount += 1;
            }
            if (matchedCount < tagRule.Count)
                return false;
        }
        return true;
    }

    private List<StringName> GetUnlockCandidateSkillIds(StringName professionId)
    {
        return GetAllLearnedSkillIds();
    }

    private List<StringName> GetRankUpCandidateSkillIds(
        StringName professionId,
        IEnumerable<StringName> previewAssignedSkillIds = null
    )
    {
        List<StringName> candidateSkillIds = new();
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress == null)
            return candidateSkillIds;

        foreach (StringName skillId in professionProgress.core_skill_ids)
            if (skillId != "" && !candidateSkillIds.Contains(skillId))
                candidateSkillIds.Add(skillId);

        if (previewAssignedSkillIds != null)
        {
            foreach (StringName skillId in previewAssignedSkillIds)
            {
                if (skillId != "" && !candidateSkillIds.Contains(skillId))
                    candidateSkillIds.Add(skillId);
            }
        }
        return candidateSkillIds;
    }

    private List<StringName> GetRankUpPreviewAssignedCoreSkillIds(StringName professionId)
    {
        List<StringName> previewSkillIds = new();
        StringName triggerSkillId = GetReadyActiveLevelTriggerSkillId();
        if (triggerSkillId == "")
            return previewSkillIds;
        if (!IsSkillEligibleForProfession(triggerSkillId, professionId, true))
            return previewSkillIds;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(triggerSkillId);
        if (skillProgress == null || skillProgress.assigned_profession_id != "")
            return previewSkillIds;

        previewSkillIds.Add(triggerSkillId);
        return previewSkillIds;
    }

    private bool IsSkillEligibleForUnlock(StringName skillId, StringName professionId)
    {
        return IsSkillEligibleForProfession(skillId, professionId, true);
    }

    private bool IsSkillEligibleForProfession(
        StringName skillId,
        StringName professionId,
        bool allowUnassigned
    )
    {
        if (_unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned || !skillProgress.is_core)
            return false;

        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (skillDefinition == null)
            return false;
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDefinition,
                skillProgress,
                _unit_progress
            )
        )
            return false;

        if (skillProgress.assigned_profession_id == "")
            return allowUnassigned;
        return skillProgress.assigned_profession_id == professionId;
    }

    private bool MatchesAnyTagRule(
        StringName skillId,
        StringName professionId,
        IEnumerable<TagRequirementDefinition> tagRules,
        bool allowUnassigned
    )
    {
        foreach (TagRequirementDefinition tagRule in NormalizeTagRules(tagRules))
        {
            if (MatchesTagRequirement(skillId, professionId, tagRule, allowUnassigned))
                return true;
        }
        return false;
    }

    private bool MatchesTagRequirement(
        StringName skillId,
        StringName professionId,
        TagRequirementDefinition tagRule,
        bool allowUnassigned,
        IEnumerable<StringName> previewAssignedSkillIds = null
    )
    {
        if (tagRule == null || tagRule.Tag == "" || _unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;

        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (skillDefinition == null || !skillDefinition.HasTag(tagRule.Tag))
            return false;
        if (!MatchesSkillState(skillProgress, skillDefinition, tagRule))
            return false;
        if (!MatchesOriginFilter(skillProgress, tagRule))
            return false;
        return MatchesAssignment(
            skillProgress,
            professionId,
            allowUnassigned,
            previewAssignedSkillIds
        );
    }

    private bool MatchesSkillState(
        UnitSkillProgress skillProgress,
        SkillDefinition skillDefinition,
        TagRequirementDefinition tagRule
    )
    {
        return tagRule.SkillStateKind switch
        {
            TagRequirementSkillState.Learned => skillProgress.is_learned,
            TagRequirementSkillState.Core => skillProgress.is_core,
            TagRequirementSkillState.CoreMax =>
                skillProgress.is_core
                && SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                    skillDefinition,
                    skillProgress,
                    _unit_progress
                ),
            _ => false,
        };
    }

    private static bool MatchesOriginFilter(UnitSkillProgress skillProgress, TagRequirementDefinition tagRule)
    {
        return tagRule.OriginFilterKind switch
        {
            TagRequirementOriginFilter.Any => true,
            TagRequirementOriginFilter.UnmergedOnly =>
                skillProgress.merged_from_skill_ids.Count == 0,
            TagRequirementOriginFilter.MergedOnly =>
                skillProgress.merged_from_skill_ids.Count > 0,
            _ => false,
        };
    }

    private static bool MatchesAssignment(
        UnitSkillProgress skillProgress,
        StringName professionId,
        bool allowUnassigned,
        IEnumerable<StringName> previewAssignedSkillIds
    )
    {
        if (professionId != "" && skillProgress.assigned_profession_id == professionId)
            return true;
        if (skillProgress.assigned_profession_id != "")
            return false;
        return allowUnassigned || ContainsSkillId(previewAssignedSkillIds, skillProgress.skill_id);
    }

    private StringName GetReadyActiveLevelTriggerSkillId()
    {
        if (_unit_progress == null)
            return "";

        StringName triggerSkillId = _unit_progress.active_level_trigger_core_skill_id;
        if (triggerSkillId == "")
            return "";

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(triggerSkillId);
        SkillDefinition skillDefinition = GetSkillDefinition(triggerSkillId);
        if (skillProgress == null || skillDefinition == null)
            return "";
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return "";
        if (skillProgress.is_level_trigger_locked)
            return "";
        if (_unit_progress.HasLockedLevelTriggerSkillId(triggerSkillId))
            return "";
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDefinition,
                skillProgress,
                _unit_progress
            )
        )
            return "";
        return triggerSkillId;
    }

    private List<StringName> GetAllLearnedSkillIds()
    {
        List<StringName> learnedSkillIds = new();
        if (_unit_progress == null)
            return learnedSkillIds;

        foreach (StringName skillId in _unit_progress.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
            if (skillProgress != null && skillProgress.is_learned)
                learnedSkillIds.Add(skillId);
        }
        return learnedSkillIds;
    }

    private ProfessionGateCheckMode ResolveGateCheckMode(ProfessionRankGateDefinition gate)
    {
        if (gate.CheckMode != "")
            return gate.CheckModeKind;

        ProfessionDefinition sourceProfessionDef = GetProfessionDef(gate.ProfessionId);
        if (sourceProfessionDef == null)
            return ProfessionGateCheckMode.Historical;
        if (
            sourceProfessionDef.DependencyVisibilityModeKind
            == ProfessionDependencyVisibilityMode.IgnoreWhenHidden
        )
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Historical;
    }

    private bool AreActiveConditionsSatisfied(ProfessionDefinition professionDef)
    {
        if (professionDef.ActiveConditions.Count == 0)
            return true;

        UnitBaseAttributes unitBaseAttributes = _unit_progress?.unit_base_attributes;
        UnitReputationState reputationState = _unit_progress?.reputation_state;

        foreach (ProfessionActiveConditionDefinition activeCondition in professionDef.ActiveConditions)
        {
            if (activeCondition == null)
                continue;

            if (
                activeCondition.ConditionKind
                == ProfessionActiveConditionKind.AttributeRange
            )
            {
                if (unitBaseAttributes == null)
                    return false;
                if (
                    !activeCondition.MatchesValue(
                        unitBaseAttributes.GetAttributeValue(activeCondition.AttributeId)
                    )
                )
                    return false;
            }
            else if (
                activeCondition.ConditionKind
                == ProfessionActiveConditionKind.ReputationRange
            )
            {
                if (reputationState == null)
                    return false;
                if (
                    !activeCondition.MatchesValue(
                        reputationState.GetReputationValue(activeCondition.StateId)
                    )
                )
                    return false;
            }
            else
            {
                GameLog.Warning(
                    $"Unsupported profession active condition type: {activeCondition.ConditionType}.",
                    "progression.profession.unsupported_condition",
                    "progression"
                );
                return false;
            }
        }
        return true;
    }

    private SkillDefinition GetSkillDefinition(StringName skillId)
    {
        return _skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private ProfessionDefinition GetProfessionDef(StringName professionId)
    {
        return _professionDefs.TryGetValue(professionId, out ProfessionDefinition professionDef)
            ? professionDef
            : null;
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId)
    {
        return _unit_progress?.GetProfessionProgress(professionId);
    }


    private static List<TagRequirementDefinition> NormalizeTagRules(IEnumerable<TagRequirementDefinition> tagRules)
    {
        List<TagRequirementDefinition> normalizedRules = new();
        if (tagRules == null)
            return normalizedRules;

        foreach (TagRequirementDefinition tagRule in tagRules)
        {
            if (tagRule == null || tagRule.Tag == "")
                continue;
            normalizedRules.Add(tagRule);
        }
        return normalizedRules;
    }

    private static List<StringName> NormalizeSkillIds(IEnumerable<StringName> skillIds)
    {
        List<StringName> normalizedSkillIds = new();
        HashSet<StringName> seenSkillIds = new();
        if (skillIds == null)
            return normalizedSkillIds;

        foreach (StringName skillId in skillIds)
        {
            if (skillId == "" || !seenSkillIds.Add(skillId))
                continue;
            normalizedSkillIds.Add(skillId);
        }
        return normalizedSkillIds;
    }

    private static bool ContainsSkillId(IEnumerable<StringName> skillIds, StringName targetSkillId)
    {
        if (targetSkillId == "" || skillIds == null)
            return false;

        foreach (StringName skillId in skillIds)
        {
            if (skillId == targetSkillId)
                return true;
        }
        return false;
    }
}
