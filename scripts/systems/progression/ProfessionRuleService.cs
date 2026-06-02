using System.Collections.Generic;
using Godot;

public sealed class ProfessionRuleService
{
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDef> _skillDefs = new();
    private readonly Dictionary<StringName, ProfessionDef> _professionDefs = new();

    public void setup(
        UnitProgress unitProgress,
        Godot.Collections.Dictionary skillDefs,
        Godot.Collections.Dictionary professionDefs
    )
    {
        setup(unitProgress, IndexSkillDefs(skillDefs), IndexProfessionDefs(professionDefs));
    }

    public void setup(
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

    public bool is_profession_knowledge_unlocked(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return false;
        if (!professionDef.requires_knowledge_unlock())
            return true;
        return _unit_progress != null
            && _unit_progress.has_knowledge(professionDef.unlock_knowledge_id);
    }

    public bool can_unlock_profession(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null || !is_profession_knowledge_unlocked(professionId))
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
        if (!can_satisfy_profession_gates(unlockRequirement.required_profession_ranks))
            return false;
        if (!can_satisfy_attribute_rules(unlockRequirement.required_attribute_rules))
            return false;
        if (!can_satisfy_reputation_rules(unlockRequirement.required_reputation_rules))
            return false;

        return true;
    }

    public bool can_rank_up_profession(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null || !is_profession_knowledge_unlocked(professionId))
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress == null || professionProgress.rank <= 0)
            return false;
        if (professionProgress.rank >= professionDef.max_rank)
            return false;

        int targetRank = professionProgress.rank + 1;
        ProfessionRankRequirement rankRequirement = professionDef.get_rank_requirement(targetRank);
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

        if (!can_satisfy_profession_gates(rankRequirement.required_profession_ranks))
            return false;
        if (!can_satisfy_attribute_rules(rankRequirement.required_attribute_rules))
            return false;
        if (!can_satisfy_reputation_rules(rankRequirement.required_reputation_rules))
            return false;

        return true;
    }

    public bool can_satisfy_tag_rules(
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

    public bool can_satisfy_profession_gates(IEnumerable<ProfessionRankGate> gates)
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

            StringName checkMode = ResolveGateCheckMode(gate);
            if (
                checkMode == "active_only"
                && (!professionProgress.is_active || professionProgress.is_hidden)
            )
                return false;
        }
        return true;
    }

    public bool can_satisfy_attribute_rules(IEnumerable<AttributeRequirement> rules)
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
            if (!rule.matches_value(unitBaseAttributes.get_attribute_value(rule.attribute_id)))
                return false;
        }
        return true;
    }

    public bool can_satisfy_reputation_rules(IEnumerable<ReputationRequirement> rules)
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
            if (!rule.matches_value(reputationState.get_reputation_value(rule.state_id)))
                return false;
        }
        return true;
    }

    public IReadOnlyList<StringName> get_eligible_skill_ids(
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

    public bool skill_matches_tag_requirement(
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

    public bool evaluate_profession_active_state(StringName professionId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionDef == null || professionProgress == null)
            return false;
        if (professionProgress.rank <= 0)
            return false;
        return AreActiveConditionsSatisfied(professionDef);
    }

    public void refresh_all_profession_states()
    {
        if (_unit_progress == null)
            return;

        foreach (var professionKey in _unit_progress.professions.Keys)
        {
            StringName professionId = ProgressionDataUtils.to_string_name(professionKey);
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

                if (professionDef.reactivation_mode == "auto")
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

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(triggerSkillId);
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

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress == null || !skillProgress.is_learned || !skillProgress.is_core)
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef == null)
            return false;
        if (
            !SkillEffectiveMaxLevelRules.is_at_effective_max_level(
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

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef == null || !skillDef.tags.Contains(tagRule.tag))
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
        StringName skillState = tagRule.get_normalized_skill_state();
        if (skillState == TagRequirement.SKILL_STATE_LEARNED())
            return skillProgress.is_learned;
        if (skillState == TagRequirement.SKILL_STATE_CORE())
            return skillProgress.is_core;
        if (skillState == TagRequirement.SKILL_STATE_CORE_MAX())
            return skillProgress.is_core
                && SkillEffectiveMaxLevelRules.is_at_effective_max_level(
                    skillDef,
                    skillProgress,
                    _unit_progress
                );
        return false;
    }

    private static bool MatchesOriginFilter(UnitSkillProgress skillProgress, TagRequirement tagRule)
    {
        StringName originFilter = tagRule.get_normalized_origin_filter();
        if (originFilter == TagRequirement.ORIGIN_FILTER_ANY())
            return true;
        if (originFilter == TagRequirement.ORIGIN_FILTER_UNMERGED_ONLY())
            return skillProgress.merged_from_skill_ids.Count == 0;
        if (originFilter == TagRequirement.ORIGIN_FILTER_MERGED_ONLY())
            return skillProgress.merged_from_skill_ids.Count > 0;
        return false;
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

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(triggerSkillId);
        SkillDef skillDef = GetSkillDef(triggerSkillId);
        if (skillProgress == null || skillDef == null)
            return "";
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return "";
        if (skillProgress.is_level_trigger_locked)
            return "";
        if (_unit_progress.locked_level_trigger_skill_ids.Contains(triggerSkillId))
            return "";
        if (
            !SkillEffectiveMaxLevelRules.is_at_effective_max_level(
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

        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_unit_progress.skills))
        {
            StringName skillId = new(skillKey);
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
            if (skillProgress != null && skillProgress.is_learned)
                learnedSkillIds.Add(skillId);
        }
        return learnedSkillIds;
    }

    private StringName ResolveGateCheckMode(ProfessionRankGate gate)
    {
        if (gate.check_mode != "")
            return gate.check_mode;

        ProfessionDef sourceProfessionDef = GetProfessionDef(gate.profession_id);
        if (sourceProfessionDef == null)
            return "historical";
        if (sourceProfessionDef.dependency_visibility_mode == "ignore_when_hidden")
            return "active_only";
        return "historical";
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

            if (activeCondition.condition_type == "attribute_range")
            {
                if (unitBaseAttributes == null)
                    return false;
                if (
                    !activeCondition.matches_value(
                        unitBaseAttributes.get_attribute_value(activeCondition.attribute_id)
                    )
                )
                    return false;
            }
            else if (activeCondition.condition_type == "reputation_range")
            {
                if (reputationState == null)
                    return false;
                if (
                    !activeCondition.matches_value(
                        reputationState.get_reputation_value(activeCondition.state_id)
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
        return _unit_progress?.get_profession_progress(professionId);
    }

    private static Dictionary<StringName, SkillDef> IndexSkillDefs(
        Godot.Collections.Dictionary skillDefs
    )
    {
        Dictionary<StringName, SkillDef> indexedDefs = new();
        if (skillDefs == null)
            return indexedDefs;

        foreach (var key in skillDefs.Keys)
        {
            if (skillDefs[key].AsGodotObject() is not SkillDef skillDef)
                continue;
            StringName indexedId =
                skillDef.skill_id != "" ? skillDef.skill_id : ProgressionDataUtils.to_string_name(key);
            if (indexedId != "")
                indexedDefs[indexedId] = skillDef;
        }
        return indexedDefs;
    }

    private static Dictionary<StringName, ProfessionDef> IndexProfessionDefs(
        Godot.Collections.Dictionary professionDefs
    )
    {
        Dictionary<StringName, ProfessionDef> indexedDefs = new();
        if (professionDefs == null)
            return indexedDefs;

        foreach (var key in professionDefs.Keys)
        {
            if (professionDefs[key].AsGodotObject() is not ProfessionDef professionDef)
                continue;
            StringName indexedId =
                professionDef.profession_id != ""
                    ? professionDef.profession_id
                    : ProgressionDataUtils.to_string_name(key);
            if (indexedId != "")
                indexedDefs[indexedId] = professionDef;
        }
        return indexedDefs;
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
