using System.Collections.Generic;
using Godot;

public sealed class ProfessionRuleService
{
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();
    private readonly Dictionary<StringName, ProfessionDef> _professionDefs = new();

    public void Setup(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs
    )
    {
        _unit_progress = unitProgress;
        _skillDefs.Clear();
        _professionDefs.Clear();

        if (skillDefs != null)
        {
            foreach (KeyValuePair<StringName, SkillDef> pair in skillDefs)
            {
                if (pair.Key != "" && pair.Value != null)
                    _skillDefs[pair.Key] = pair.Value;
            }
        }

        if (professionDefs != null)
        {
            foreach (KeyValuePair<StringName, ProfessionDef> pair in professionDefs)
            {
                if (pair.Key != "" && pair.Value != null)
                    _professionDefs[pair.Key] = pair.Value;
            }
        }
    }

    public bool IsProfessionKnowledgeUnlocked(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return false;
        if (!professionDef.RequiresKnowledgeUnlock())
            return true;
        return _unit_progress != null
            && _unit_progress.HasKnowledge(professionDef.unlock_knowledge_id);
    }

    public bool CanUnlockProfession(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null || !IsProfessionKnowledgeUnlocked(professionId))
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress != null && professionProgress.rank > 0)
            return false;

        ProfessionPromotionRequirement unlockRequirement = professionDef.unlock_requirement;
        if (unlockRequirement == null)
            return true;

        if (
            !CanSatisfyRequiredSkillIdsForUnlock(professionId, unlockRequirement.required_skill_ids)
        )
            return false;
        if (!CanSatisfyTagRulesForUnlock(professionId, unlockRequirement.required_tag_rules))
            return false;
        if (!CanSatisfyProfessionGates(unlockRequirement.required_profession_ranks))
            return false;
        if (!CanSatisfyAttributeRules(unlockRequirement.required_attribute_rules))
            return false;
        if (!CanSatisfyReputationRules(unlockRequirement.required_reputation_rules))
            return false;

        return true;
    }

    public bool CanRankUpProfession(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null || !IsProfessionKnowledgeUnlocked(professionId))
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress == null || professionProgress.rank <= 0)
            return false;
        if (professionProgress.rank >= professionDef.max_rank)
            return false;

        int targetRank = professionProgress.rank + 1;
        ProfessionRankRequirement rankRequirement = professionDef.GetRankRequirement(targetRank);
        if (rankRequirement == null)
            return false;

        List<StringName> previewAssignedSkillIds = GetRankUpPreviewAssignedCoreSkillIds(
            professionId
        );
        if (
            !CanSatisfyTagRulesWithSkillIds(
                GetRankUpCandidateSkillIds(professionId, previewAssignedSkillIds),
                professionId,
                rankRequirement.required_tag_rules,
                false,
                previewAssignedSkillIds
            )
        )
        {
            return false;
        }

        if (!CanSatisfyProfessionGates(rankRequirement.required_profession_ranks))
            return false;
        if (!CanSatisfyAttributeRules(rankRequirement.required_attribute_rules))
            return false;
        if (!CanSatisfyReputationRules(rankRequirement.required_reputation_rules))
            return false;

        return true;
    }

    public bool CanSatisfyTagRules(
        StringName professionId,
        IEnumerable<TagRequirement> tagRules
    )
    {
        return CanSatisfyTagRulesWithSkillIds(
            GetRankUpCandidateSkillIds(professionId),
            professionId,
            tagRules,
            false
        );
    }

    public bool CanSatisfyProfessionGates(IEnumerable<ProfessionRankGate> gates)
    {
        if (gates == null)
            return true;

        foreach (ProfessionRankGate gate in gates)
        {
            if (gate == null)
                continue;

            UnitProfessionProgress professionProgress = GetProfessionProgress(gate.profession_id);
            if (professionProgress == null || professionProgress.rank < gate.min_rank)
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

    public bool CanSatisfyAttributeRules(IEnumerable<AttributeRequirement> rules)
    {
        UnitBaseAttributes unitBaseAttributes = _unit_progress?.unit_base_attributes;
        if (rules == null)
            return true;

        foreach (AttributeRequirement rule in rules)
        {
            if (rule == null)
                continue;
            if (unitBaseAttributes == null)
                return false;
            if (!rule.MatchesValue(unitBaseAttributes.GetAttributeValue(rule.attribute_id)))
                return false;
        }
        return true;
    }

    public bool CanSatisfyReputationRules(IEnumerable<ReputationRequirement> rules)
    {
        UnitReputationState reputationState = _unit_progress?.reputation_state;
        if (rules == null)
            return true;

        foreach (ReputationRequirement rule in rules)
        {
            if (rule == null)
                continue;
            if (reputationState == null)
                return false;
            if (!rule.MatchesValue(reputationState.GetReputationValue(rule.state_id)))
                return false;
        }
        return true;
    }

    public IReadOnlyList<StringName> GetEligibleSkillIds(
        StringName professionId,
        IEnumerable<TagRequirement> tagRules,
        bool allowUnassigned
    )
    {
        List<TagRequirement> normalizedTagRules = NormalizeTagRules(tagRules);
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
        TagRequirement tagRule,
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
        ProfessionDef professionDef = GetProfessionDef(professionId);
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
            ProfessionDef professionDef = GetProfessionDef(professionId);
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
        IEnumerable<TagRequirement> tagRules
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
        IEnumerable<TagRequirement> tagRules,
        bool allowUnassigned,
        IEnumerable<StringName> previewAssignedSkillIds = null
    )
    {
        List<TagRequirement> normalizedTagRules = NormalizeTagRules(tagRules);
        if (normalizedTagRules.Count == 0)
            return true;

        List<StringName> normalizedCandidateSkillIds = NormalizeSkillIds(candidateSkillIds);
        foreach (TagRequirement tagRule in normalizedTagRules)
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
            if (matchedCount < tagRule.count)
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

        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef == null)
            return false;
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDef,
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
        IEnumerable<TagRequirement> tagRules,
        bool allowUnassigned
    )
    {
        foreach (TagRequirement tagRule in NormalizeTagRules(tagRules))
        {
            if (MatchesTagRequirement(skillId, professionId, tagRule, allowUnassigned))
                return true;
        }
        return false;
    }

    private bool MatchesTagRequirement(
        StringName skillId,
        StringName professionId,
        TagRequirement tagRule,
        bool allowUnassigned,
        IEnumerable<StringName> previewAssignedSkillIds = null
    )
    {
        if (tagRule == null || tagRule.tag == "" || _unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef == null || !skillDef.HasTag(tagRule.tag))
            return false;
        if (!MatchesSkillState(skillProgress, skillDef, tagRule))
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
        SkillDef skillDef,
        TagRequirement tagRule
    )
    {
        return tagRule.SkillStateKind switch
        {
            TagRequirementSkillState.Learned => skillProgress.is_learned,
            TagRequirementSkillState.Core => skillProgress.is_core,
            TagRequirementSkillState.CoreMax =>
                skillProgress.is_core
                && SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                    skillDef,
                    skillProgress,
                    _unit_progress
                ),
            _ => false,
        };
    }

    private static bool MatchesOriginFilter(UnitSkillProgress skillProgress, TagRequirement tagRule)
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
        SkillDef skillDef = GetSkillDef(triggerSkillId);
        if (skillProgress == null || skillDef == null)
            return "";
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return "";
        if (skillProgress.is_level_trigger_locked)
            return "";
        if (_unit_progress.HasLockedLevelTriggerSkillId(triggerSkillId))
            return "";
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDef,
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

    private ProfessionGateCheckMode ResolveGateCheckMode(ProfessionRankGate gate)
    {
        if (gate.check_mode != "")
            return gate.CheckModeKind;

        ProfessionDef sourceProfessionDef = GetProfessionDef(gate.profession_id);
        if (sourceProfessionDef == null)
            return ProfessionGateCheckMode.Historical;
        if (
            sourceProfessionDef.DependencyVisibilityModeKind
            == ProfessionDependencyVisibilityMode.IgnoreWhenHidden
        )
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Historical;
    }

    private bool AreActiveConditionsSatisfied(ProfessionDef professionDef)
    {
        if (professionDef.active_conditions.Count == 0)
            return true;

        UnitBaseAttributes unitBaseAttributes = _unit_progress?.unit_base_attributes;
        UnitReputationState reputationState = _unit_progress?.reputation_state;

        foreach (ProfessionActiveCondition activeCondition in professionDef.active_conditions)
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
                        unitBaseAttributes.GetAttributeValue(activeCondition.attribute_id)
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
                        reputationState.GetReputationValue(activeCondition.state_id)
                    )
                )
                    return false;
            }
            else
            {
                GameLog.Warning(
                    $"Unsupported profession active condition type: {activeCondition.condition_type}.",
                    "progression.profession.unsupported_condition",
                    "progression"
                );
                return false;
            }
        }
        return true;
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        return _skillDefs.TryGetValue(skillId, out SkillDef skillDef) ? skillDef : null;
    }

    private ProfessionDef GetProfessionDef(StringName professionId)
    {
        return _professionDefs.TryGetValue(professionId, out ProfessionDef professionDef)
            ? professionDef
            : null;
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId)
    {
        return _unit_progress?.GetProfessionProgress(professionId);
    }


    private static List<TagRequirement> NormalizeTagRules(IEnumerable<TagRequirement> tagRules)
    {
        List<TagRequirement> normalizedRules = new();
        if (tagRules == null)
            return normalizedRules;

        foreach (TagRequirement tagRule in tagRules)
        {
            if (tagRule == null || tagRule.tag == "")
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
