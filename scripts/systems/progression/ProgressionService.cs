using Godot;

using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class ProgressionService : RefCounted
{
    public const string SELECTION_KEY_QUALIFIER_SKILL_IDS = "selected_qualifier_skill_ids";
    public const string SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS = "selected_assigned_core_skill_ids";
    public const string SELECTION_KEY_HP_ROLL_OVERRIDE = "hp_roll_override";
    private const string SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID = "_required_trigger_skill_id";
    private static readonly StringName HpMaxAttributeId = "hp_max";
    private const int LockHitBonusDefault = 1;
    private static readonly StringName PracticeTrackMeditation = "meditation";
    private static readonly StringName PracticeTrackCultivation = "cultivation";
    private static readonly GStringNameArray PracticeTracks = new() { PracticeTrackMeditation, PracticeTrackCultivation };
    private static readonly GDictionary ValidPracticeTiers = new()
    {
        ["basic"] = true,
        ["intermediate"] = true,
        ["advanced"] = true,
        ["ultimate"] = true,
    };
    private static readonly GDictionary ManualLearnBlockedSources = new()
    {
        ["profession"] = true,
        ["race"] = true,
        ["subrace"] = true,
        ["ascension"] = true,
        ["bloodline"] = true,
    };
    private static readonly GDictionary RacialGrantSources = new()
    {
        ["race"] = true,
        ["subrace"] = true,
        ["ascension"] = true,
        ["bloodline"] = true,
    };

    private UnitProgress _unit_progress;
    private GDictionary _skill_defs = new();
    private GDictionary _profession_defs = new();
    private ProfessionRuleService _rule_service;
    private ProfessionAssignmentService _assignment_service;
    private SkillMergeService _skill_merge_service;

    public static string SELECTION_KEY_QUALIFIER_SKILL_IDS_ID() => SELECTION_KEY_QUALIFIER_SKILL_IDS;
    public static string SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS_ID() => SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS;
    public static string SELECTION_KEY_HP_ROLL_OVERRIDE_ID() => SELECTION_KEY_HP_ROLL_OVERRIDE;

    public void setup(UnitProgress unitProgress, GDictionary skillDefs, GDictionary professionDefs)
    {
        SetupInternal(unitProgress, skillDefs, professionDefs, null, null, null);
    }

    public void setup(
        UnitProgress unitProgress,
        GDictionary skillDefs,
        GDictionary professionDefs,
        ProfessionRuleService ruleService,
        ProfessionAssignmentService assignmentService,
        SkillMergeService skillMergeService)
    {
        SetupInternal(unitProgress, skillDefs, professionDefs, ruleService, assignmentService, skillMergeService);
    }

    private void SetupInternal(
        UnitProgress unitProgress,
        GDictionary skillDefs,
        GDictionary professionDefs,
        ProfessionRuleService ruleService,
        ProfessionAssignmentService assignmentService,
        SkillMergeService skillMergeService)
    {
        _unit_progress = unitProgress;
        _skill_defs = IndexSkillDefs(skillDefs);
        _profession_defs = IndexProfessionDefs(professionDefs);

        _assignment_service = assignmentService ?? new ProfessionAssignmentService();
        _assignment_service.setup(_unit_progress, _skill_defs, _profession_defs);

        _rule_service = ruleService ?? new ProfessionRuleService();
        _rule_service.setup(_unit_progress, _skill_defs, _profession_defs);

        _skill_merge_service = skillMergeService ?? new SkillMergeService();
        _skill_merge_service.setup(_unit_progress, _skill_defs, _assignment_service);

        refresh_runtime_state();
    }

    public void refresh_runtime_state()
    {
        if (_unit_progress == null)
            return;

        _unit_progress.sync_active_core_skill_ids();
        _unit_progress.sync_default_combat_resource_unlocks();
        NormalizeSkillLevelsToEffectiveMax();
        recalculate_character_level();
        _rule_service?.refresh_all_profession_states();
        SyncCombatResourceUnlocksFromLearnedSkills();
        RefreshCachedPendingProfessionChoices();
    }

    public bool learn_knowledge(StringName knowledgeId)
    {
        if (_unit_progress == null)
            return false;
        if (!_unit_progress.learn_knowledge(knowledgeId))
            return false;
        refresh_runtime_state();
        return true;
    }

    public bool learn_skill(StringName skillId)
    {
        if (!can_learn_skill(skillId))
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef.unlock_mode == "composite_upgrade")
            return LearnCompositeUpgrade(skillDef);

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress == null)
            skillProgress = new UnitSkillProgress { skill_id = skillId };

        skillProgress.is_learned = true;
        _unit_progress.set_skill_progress(skillProgress);
        refresh_runtime_state();
        return true;
    }

    public bool can_learn_skill(StringName skillId)
    {
        SkillDef skillDef = GetSkillDef(skillId);
        if (_unit_progress == null || skillDef == null)
            return false;
        if (HasInvalidPracticeConfiguration(skillDef))
            return false;
        if (is_skill_relearn_blocked(skillId))
            return false;
        if (IsManualSkillLearnSourceBlocked(skillDef.learn_source))
            return false;

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress != null && skillProgress.is_learned)
            return false;
        if (!CanLearnSkillRequirements(skillDef.learn_requirements))
            return false;
        if (!CanSatisfyKnowledgeRequirements(skillDef.knowledge_requirements))
            return false;
        if (!CanSatisfySkillLevelRequirements(skillDef.skill_level_requirements))
            return false;
        if (!CanSatisfyAttributeRequirements(skillDef.attribute_requirements))
            return false;
        if (!CanSatisfyAchievementRequirements(skillDef.achievement_requirements))
            return false;
        if (skillDef.unlock_mode == "composite_upgrade")
            return CanLearnCompositeUpgrade(skillDef);
        return true;
    }

    public bool grant_racial_skill(RacialGrantedSkill grant, StringName sourceType, StringName sourceId)
    {
        if (_unit_progress == null || grant == null)
            return false;
        if (!IsRacialGrantSourceType(sourceType))
            return false;
        if (sourceId == "" || grant.skill_id == "")
            return false;

        int minimumSkillLevel = grant.minimum_skill_level;
        if (minimumSkillLevel < 0)
            return false;

        SkillDef skillDef = GetSkillDef(grant.skill_id);
        if (skillDef == null || skillDef.learn_source != sourceType)
            return false;
        if (minimumSkillLevel > skillDef.max_level)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(grant.skill_id);
        if (skillProgress != null && skillProgress.is_learned)
            return false;
        if (skillProgress == null)
            skillProgress = new UnitSkillProgress { skill_id = grant.skill_id };

        skillProgress.is_learned = true;
        skillProgress.skill_level = minimumSkillLevel;
        skillProgress.granted_source_type = sourceType;
        skillProgress.granted_source_id = sourceId;

        _unit_progress.set_skill_progress(skillProgress);
        refresh_runtime_state();
        return true;
    }

    public bool grant_skill_mastery(StringName skillId, int amount, StringName sourceType)
    {
        if (_unit_progress == null || amount <= 0)
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillDef == null || skillProgress == null || !skillProgress.is_learned)
            return false;
        if (skillDef.mastery_sources.Count > 0 && !skillDef.mastery_sources.Contains(sourceType))
            return false;

        int effectiveMaxLevel = GetEffectiveSkillMaxLevel(skillDef, skillProgress);
        if (effectiveMaxLevel <= 0)
        {
            skillProgress.skill_level = 0;
            skillProgress.current_mastery = 0;
            _unit_progress.set_skill_progress(skillProgress);
            refresh_runtime_state();
            return false;
        }

        skillProgress.total_mastery_earned += amount;
        if (sourceType == "training")
            skillProgress.mastery_from_training += amount;
        else if (sourceType == "battle")
            skillProgress.mastery_from_battle += amount;

        if (skillProgress.skill_level >= effectiveMaxLevel)
        {
            skillProgress.skill_level = effectiveMaxLevel;
            skillProgress.current_mastery = 0;
            _unit_progress.set_skill_progress(skillProgress);
            refresh_runtime_state();
            return true;
        }

        skillProgress.current_mastery += amount;
        while (skillProgress.skill_level < effectiveMaxLevel)
        {
            int masteryRequired = skillDef.get_mastery_required_for_level(skillProgress.skill_level);
            if (masteryRequired <= 0 || skillProgress.current_mastery < masteryRequired)
                break;

            skillProgress.current_mastery -= masteryRequired;
            skillProgress.skill_level += 1;
        }

        if (skillProgress.skill_level >= effectiveMaxLevel)
        {
            skillProgress.skill_level = effectiveMaxLevel;
            skillProgress.current_mastery = 0;
        }

        _unit_progress.set_skill_progress(skillProgress);
        refresh_runtime_state();
        return true;
    }

    public bool set_skill_core(StringName skillId, bool enabled)
    {
        if (_unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;

        if (enabled)
        {
            skillProgress.is_core = true;
            _unit_progress.set_skill_progress(skillProgress);
            refresh_runtime_state();
            return true;
        }

        StringName previousProfessionId = skillProgress.assigned_profession_id;
        ClearLevelTriggerStateForSkill(skillId);
        skillProgress.is_core = false;
        skillProgress.clear_profession_assignment();
        _unit_progress.set_skill_progress(skillProgress);

        if (previousProfessionId != "")
        {
            UnitProfessionProgress professionProgress = _unit_progress.get_profession_progress(previousProfessionId);
            professionProgress?.remove_core_skill(skillId);
        }

        refresh_runtime_state();
        return true;
    }

    public int recalculate_character_level()
    {
        if (_unit_progress == null)
            return 0;

        int rankTotal = 0;
        foreach (var professionValue in _unit_progress.professions.Values)
        {
            UnitProfessionProgress professionProgress = professionValue.AsGodotObject() as UnitProfessionProgress;
            if (professionProgress != null)
                rankTotal += professionProgress.rank;
        }

        _unit_progress.character_level = rankTotal;
        return rankTotal;
    }

    public bool can_promote_profession(StringName professionId)
    {
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress == null || professionProgress.rank <= 0)
            return _rule_service != null && _rule_service.can_unlock_profession(professionId);
        return _rule_service != null && _rule_service.can_rank_up_profession(professionId);
    }

    public bool promote_profession(StringName professionId, GDictionary selection = null)
    {
        if (_unit_progress == null || _rule_service == null || _assignment_service == null)
            return false;
        if (!can_promote_profession(professionId))
            return false;

        StringName triggerSkillId = GetReadyActiveLevelTriggerSkillId();
        if (triggerSkillId == "")
            return false;

        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return false;

        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        bool isUnlock = professionProgress == null || professionProgress.rank <= 0;
        int currentRank = professionProgress?.rank ?? 0;
        int targetRank = isUnlock ? 1 : currentRank + 1;
        GDictionary promotionSelection = ResolvePromotionSelection(
            professionId,
            targetRank,
            isUnlock,
            WithRequiredTriggerSkill(selection ?? new GDictionary(), triggerSkillId)
        );
        if (promotionSelection.Count == 0)
            return false;
        if (!SelectionIncludesSkill(promotionSelection, triggerSkillId))
            return false;

        GStringNameArray consumedSkillIds = GetSelectionSkillIds(promotionSelection, SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS);
        GStringNameArray qualifierSkillIds = GetSelectionSkillIds(promotionSelection, SELECTION_KEY_QUALIFIER_SKILL_IDS);
        bool createdProfessionProgress = false;
        GDictionary previousProfessionCoreSkillIds = SnapshotProfessionCoreSkillIds();
        GDictionary previousSkillAssignments = SnapshotSkillAssignmentIds(consumedSkillIds);

        if (professionProgress == null)
        {
            professionProgress = new UnitProfessionProgress { profession_id = professionId };
            _unit_progress.set_profession_progress(professionProgress);
            createdProfessionProgress = true;
        }

        foreach (StringName skillId in consumedSkillIds)
        {
            if (!_assignment_service.can_assign_core_skill_to_profession(skillId, professionId))
            {
                RollbackPromotionAssignmentState(
                    professionId,
                    createdProfessionProgress,
                    previousProfessionCoreSkillIds,
                    previousSkillAssignments
                );
                return false;
            }
        }
        foreach (StringName skillId in consumedSkillIds)
        {
            if (!_assignment_service.assign_core_skill_to_profession(skillId, professionId))
            {
                RollbackPromotionAssignmentState(
                    professionId,
                    createdProfessionProgress,
                    previousProfessionCoreSkillIds,
                    previousSkillAssignments
                );
                return false;
            }
        }

        professionProgress.rank = targetRank;
        ProfessionPromotionRecord promotionRecord = new()
        {
            new_rank = targetRank,
            consumed_skill_ids = new GStringNameArray(consumedSkillIds),
            qualifier_skill_ids = new GStringNameArray(qualifierSkillIds),
            snapshot_unit_base_attributes = GetUnitBaseAttributesSnapshot(),
            timestamp = (int)Time.GetUnixTimeFromSystem(),
        };
        professionProgress.add_promotion_record(promotionRecord);

        ApplyProfessionHitPointGain(professionDef, selection ?? new GDictionary());
        GrantProfessionSkills(professionDef, professionProgress, targetRank);
        LockReadyActiveLevelTriggerSkill(triggerSkillId);
        _unit_progress.set_profession_progress(professionProgress);
        refresh_runtime_state();
        return true;
    }

    public static int calculate_profession_hit_point_gain(int hitDieRoll, int constitutionValue)
    {
        return Mathf.Max(1, Mathf.Max(hitDieRoll, 1) + calculate_constitution_modifier(constitutionValue) * 2);
    }

    public static int calculate_constitution_modifier(int constitutionValue)
    {
        return AttributeSnapshot.calculate_score_modifier(constitutionValue);
    }

    public Godot.Collections.Array<PendingProfessionChoice> get_profession_upgrade_candidates()
    {
        return BuildPendingProfessionChoices();
    }

    public bool is_skill_relearn_blocked(StringName skillId)
    {
        return _unit_progress != null && _unit_progress.is_skill_relearn_blocked(skillId);
    }

    private bool LearnCompositeUpgrade(SkillDef skillDef)
    {
        if (_unit_progress == null || skillDef == null || skillDef.skill_id == "")
            return false;
        UnitSkillProgress existingProgress = _unit_progress.get_skill_progress(skillDef.skill_id);
        if (existingProgress != null && existingProgress.is_learned)
            return false;

        if (_skill_merge_service != null && skillDef.upgrade_source_skill_ids.Count > 0)
        {
            if (!_skill_merge_service.apply_composite_upgrade_result(
                skillDef.skill_id,
                skillDef.upgrade_source_skill_ids,
                skillDef.retain_source_skills_on_unlock,
                skillDef.core_skill_transition_mode
            ))
            {
                return false;
            }
        }
        else
        {
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillDef.skill_id);
            if (skillProgress == null)
                skillProgress = new UnitSkillProgress { skill_id = skillDef.skill_id };
            skillProgress.is_learned = true;
            skillProgress.merged_from_skill_ids = new GStringNameArray(skillDef.upgrade_source_skill_ids);
            _unit_progress.set_skill_progress(skillProgress);
        }

        refresh_runtime_state();
        return true;
    }

    private void ApplyProfessionHitPointGain(ProfessionDef professionDef, GDictionary selection)
    {
        if (_unit_progress?.unit_base_attributes == null || professionDef == null)
            return;

        int hitDieSides = Mathf.Max(professionDef.hit_die_sides, 1);
        int hitDieRoll = RollProfessionHitDie(hitDieSides, selection);
        int constitutionValue = _unit_progress.unit_base_attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION());
        int hpGain = calculate_profession_hit_point_gain(hitDieRoll, constitutionValue);
        int currentHpMax = _unit_progress.unit_base_attributes.get_attribute_value(HpMaxAttributeId);
        _unit_progress.unit_base_attributes.set_attribute_value(HpMaxAttributeId, currentHpMax + hpGain);
    }

    private static int RollProfessionHitDie(int hitDieSides, GDictionary selection)
    {
        int normalizedSides = Mathf.Max(hitDieSides, 1);
        if (selection != null && GdInterop.TryGet(selection, SELECTION_KEY_HP_ROLL_OVERRIDE, out Variant overrideValue)
            && overrideValue.VariantType == Variant.Type.Int)
        {
            return Mathf.Clamp(overrideValue.AsInt32(), 1, normalizedSides);
        }
        return TrueRandomSeedService.randi_range(1, normalizedSides);
    }

    private static bool IsManualSkillLearnSourceBlocked(StringName learnSource)
    {
        return ManualLearnBlockedSources.ContainsKey(learnSource);
    }

    private static bool HasInvalidPracticeConfiguration(SkillDef skillDef)
    {
        if (skillDef == null)
            return false;

        int practiceTrackCount = 0;
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDef.tags.Contains(trackType))
                practiceTrackCount += 1;
        }
        if (practiceTrackCount == 0)
            return skillDef.practice_tier != "";
        if (practiceTrackCount != 1)
            return true;
        if (skillDef.tags.Count != 1)
            return true;
        return !ValidPracticeTiers.ContainsKey(skillDef.practice_tier);
    }

    private static StringName GetExclusivePracticeTrack(SkillDef skillDef)
    {
        if (skillDef == null || HasInvalidPracticeConfiguration(skillDef))
            return "";
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDef.tags.Contains(trackType))
                return trackType;
        }
        return "";
    }

    private static bool IsRacialGrantSourceType(StringName sourceType)
    {
        return RacialGrantSources.ContainsKey(sourceType);
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        return _skill_defs.ContainsKey(skillId) ? _skill_defs[skillId].AsGodotObject() as SkillDef : null;
    }

    private ProfessionDef GetProfessionDef(StringName professionId)
    {
        return _profession_defs.ContainsKey(professionId) ? _profession_defs[professionId].AsGodotObject() as ProfessionDef : null;
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId)
    {
        return _unit_progress?.get_profession_progress(professionId);
    }

    private void NormalizeSkillLevelsToEffectiveMax()
    {
        if (_unit_progress == null)
            return;

        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_unit_progress.skills))
        {
            StringName skillId = new(skillKey);
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
            SkillDef skillDef = GetSkillDef(skillId);
            if (skillProgress == null || skillDef == null)
                continue;

            int effectiveMaxLevel = GetEffectiveSkillMaxLevel(skillDef, skillProgress);
            if (skillProgress.skill_level <= effectiveMaxLevel)
                continue;

            skillProgress.skill_level = effectiveMaxLevel;
            skillProgress.current_mastery = 0;
            _unit_progress.set_skill_progress(skillProgress);
        }
    }

    private int GetEffectiveSkillMaxLevel(SkillDef skillDef, UnitSkillProgress skillProgress)
    {
        return SkillEffectiveMaxLevelRules.get_effective_max_level(skillDef, skillProgress, _unit_progress);
    }

    private bool CanLearnSkillRequirements(Godot.Collections.Array<StringName> requirements)
    {
        if (_unit_progress == null)
            return false;

        foreach (StringName requiredSkillId in requirements)
        {
            UnitSkillProgress requiredSkillProgress = _unit_progress.get_skill_progress(requiredSkillId);
            if (requiredSkillProgress == null || !requiredSkillProgress.is_learned)
                return false;
        }
        return true;
    }

    private bool CanLearnCompositeUpgrade(SkillDef skillDef)
    {
        if (_unit_progress == null || skillDef == null)
            return false;
        if (!CanLearnSkillRequirements(skillDef.learn_requirements))
            return false;
        if (!CanSatisfyKnowledgeRequirements(skillDef.knowledge_requirements))
            return false;
        if (!CanSatisfySkillLevelRequirements(skillDef.skill_level_requirements))
            return false;
        if (!CanSatisfyAttributeRequirements(skillDef.attribute_requirements))
            return false;
        if (!CanSatisfyAchievementRequirements(skillDef.achievement_requirements))
            return false;
        return true;
    }

    private bool CanSatisfyKnowledgeRequirements(Godot.Collections.Array<StringName> requiredKnowledgeIds)
    {
        if (_unit_progress == null)
            return false;
        foreach (StringName knowledgeId in requiredKnowledgeIds)
        {
            if (!_unit_progress.has_knowledge(knowledgeId))
                return false;
        }
        return true;
    }

    private bool CanSatisfySkillLevelRequirements(GDictionary requiredSkillLevelMap)
    {
        if (_unit_progress == null)
            return false;

        foreach (var requiredSkillKey in requiredSkillLevelMap.Keys)
        {
            StringName requiredSkillId = ProgressionDataUtils.to_string_name(requiredSkillKey);
            int requiredLevel = requiredSkillLevelMap[requiredSkillKey].AsInt32();
            if (requiredSkillId == "" || requiredLevel <= 0)
                return false;

            UnitSkillProgress requiredSkillProgress = _unit_progress.get_skill_progress(requiredSkillId);
            if (requiredSkillProgress == null || !requiredSkillProgress.is_learned)
                return false;
            if (requiredSkillProgress.skill_level < requiredLevel)
                return false;
        }
        return true;
    }

    private bool CanSatisfyAttributeRequirements(GDictionary requiredAttributeMap)
    {
        if (_unit_progress?.unit_base_attributes == null)
            return false;

        foreach (var attributeKeyValue in requiredAttributeMap.Keys)
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(attributeKeyValue);
            int requiredValue = requiredAttributeMap[attributeKeyValue].AsInt32();
            if (attributeId == "" || requiredValue <= 0)
                return false;
            if (_unit_progress.unit_base_attributes.get_attribute_value(attributeId) < requiredValue)
                return false;
        }
        return true;
    }

    private bool CanSatisfyAchievementRequirements(Godot.Collections.Array<StringName> requiredAchievementIds)
    {
        if (_unit_progress == null)
            return false;
        foreach (StringName achievementId in requiredAchievementIds)
        {
            AchievementProgressState progressState = _unit_progress.get_achievement_progress_state(achievementId);
            if (progressState == null || !progressState.is_unlocked)
                return false;
        }
        return true;
    }

    private GDictionary ResolvePromotionSelection(StringName professionId, int targetRank, bool isUnlock, GDictionary selection)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return new GDictionary();

        Godot.Collections.Array<TagRequirement> tagRules = GetTagRulesForTarget(professionDef, targetRank, isUnlock);
        Godot.Collections.Array<TagRequirement> qualifierRules = GetTagRulesForRole(tagRules, TagRequirement.SELECTION_ROLE_QUALIFIER());
        Godot.Collections.Array<TagRequirement> assignedCoreRules = GetTagRulesForRole(tagRules, TagRequirement.SELECTION_ROLE_ASSIGNED_CORE());
        bool allowUnassigned = isUnlock;
        GStringNameArray requiredSkillIds = GetRequiredSkillIdsForTarget(professionDef, isUnlock);
        StringName requiredTriggerSkillId = GetRequiredTriggerSkillId(selection);
        GStringNameArray previewAssignedSkillIds = GetPreviewAssignedCoreSkillIdsForSelection(professionId, isUnlock, requiredTriggerSkillId);
        bool triggerAsQualifier = false;

        if (requiredTriggerSkillId != "")
        {
            if (CanIncludeSkillInSelection(requiredTriggerSkillId, professionId, assignedCoreRules, allowUnassigned, previewAssignedSkillIds))
            {
                if (!requiredSkillIds.Contains(requiredTriggerSkillId))
                    requiredSkillIds.Add(requiredTriggerSkillId);
            }
            else if (isUnlock && CanIncludeSkillInSelection(requiredTriggerSkillId, professionId, qualifierRules, allowUnassigned, previewAssignedSkillIds))
            {
                triggerAsQualifier = true;
            }
            else
            {
                return new GDictionary();
            }
        }

        bool hasExplicitAssignedCoreSelection = selection.ContainsKey(SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS);
        GStringNameArray assignedCoreSkillIds = new();
        if (hasExplicitAssignedCoreSelection)
            assignedCoreSkillIds = GetSelectionSkillIds(
                selection,
                SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS
            );
        if (hasExplicitAssignedCoreSelection)
        {
            if (!ValidateExplicitSelection(
                assignedCoreSkillIds,
                professionId,
                assignedCoreRules,
                allowUnassigned,
                requiredSkillIds,
                previewAssignedSkillIds
            ))
            {
                return new GDictionary();
            }
        }
        else
        {
            assignedCoreSkillIds = SelectSkillIdsForTagRules(
                professionId,
                assignedCoreRules,
                allowUnassigned,
                requiredSkillIds,
                previewAssignedSkillIds
            );
            if (assignedCoreSkillIds.Count == 0 && (assignedCoreRules.Count > 0 || requiredSkillIds.Count > 0))
                return new GDictionary();
        }

        bool hasExplicitQualifierSelection = selection.ContainsKey(SELECTION_KEY_QUALIFIER_SKILL_IDS);
        GStringNameArray qualifierSkillIds = new();
        if (hasExplicitQualifierSelection)
            qualifierSkillIds = GetSelectionSkillIds(selection, SELECTION_KEY_QUALIFIER_SKILL_IDS);

        GStringNameArray qualifierLockedSkillIds = new();
        if (AssignedCoreMustBeSubsetOfQualifiers(professionDef, isUnlock))
            qualifierLockedSkillIds = new GStringNameArray(assignedCoreSkillIds);
        if (triggerAsQualifier && !qualifierLockedSkillIds.Contains(requiredTriggerSkillId))
            qualifierLockedSkillIds.Add(requiredTriggerSkillId);

        if (hasExplicitQualifierSelection)
        {
            if (!ValidateExplicitSelection(
                qualifierSkillIds,
                professionId,
                qualifierRules,
                allowUnassigned,
                qualifierLockedSkillIds,
                previewAssignedSkillIds
            ))
            {
                return new GDictionary();
            }
        }
        else
        {
            qualifierSkillIds = SelectSkillIdsForTagRules(
                professionId,
                qualifierRules,
                allowUnassigned,
                qualifierLockedSkillIds,
                previewAssignedSkillIds
            );
            if (qualifierSkillIds.Count == 0 && qualifierRules.Count > 0)
                return new GDictionary();
        }

        if (AssignedCoreMustBeSubsetOfQualifiers(professionDef, isUnlock))
        {
            foreach (StringName skillId in assignedCoreSkillIds)
            {
                if (!qualifierSkillIds.Contains(skillId))
                    return new GDictionary();
            }
        }

        return new GDictionary
        {
            [SELECTION_KEY_QUALIFIER_SKILL_IDS] = qualifierSkillIds,
            [SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS] = assignedCoreSkillIds,
            ["trigger_skill_ids"] = MergeUniqueSkillIds(qualifierSkillIds, assignedCoreSkillIds),
        };
    }

    private bool ValidateExplicitSelection(
        GStringNameArray selectedSkillIds,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray requiredSkillIds,
        GStringNameArray previewAssignedSkillIds)
    {
        if (!SelectionContainsRequiredSkillIds(selectedSkillIds, requiredSkillIds))
            return false;

        foreach (StringName skillId in selectedSkillIds)
        {
            if (requiredSkillIds.Contains(skillId))
            {
                if (!IsRequiredSkillIdSelectable(skillId, professionId, allowUnassigned, previewAssignedSkillIds))
                    return false;
                continue;
            }
            if (!MatchesAnyTagRule(skillId, professionId, tagRules, allowUnassigned, previewAssignedSkillIds))
                return false;
        }

        return AreTagRulesSatisfied(selectedSkillIds, professionId, tagRules, allowUnassigned, previewAssignedSkillIds);
    }

    private static bool SelectionContainsRequiredSkillIds(GStringNameArray selectedSkillIds, GStringNameArray requiredSkillIds)
    {
        foreach (StringName requiredSkillId in requiredSkillIds)
        {
            if (!selectedSkillIds.Contains(requiredSkillId))
                return false;
        }
        return true;
    }

    private bool IsRequiredSkillIdSelectable(
        StringName skillId,
        StringName professionId,
        bool allowUnassigned,
        GStringNameArray previewAssignedSkillIds)
    {
        if (_unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        SkillDef skillDef = GetSkillDef(skillId);
        if (skillProgress == null || skillDef == null)
            return false;
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return false;
        if (!SkillEffectiveMaxLevelRules.is_at_effective_max_level(skillDef, skillProgress, _unit_progress))
            return false;
        if (professionId != "" && skillProgress.assigned_profession_id == professionId)
            return true;
        if (skillProgress.assigned_profession_id != "")
            return false;
        return allowUnassigned || previewAssignedSkillIds.Contains(skillId);
    }

    private GStringNameArray SelectSkillIdsForTagRules(
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray lockedSkillIds,
        GStringNameArray previewAssignedSkillIds)
    {
        GStringNameArray selectedSkillIds = new();
        GStringNameArray normalizedLockedSkillIds = NormalizeSkillIdSelection(lockedSkillIds);

        foreach (StringName skillId in normalizedLockedSkillIds)
        {
            if (!CanIncludeSkillInSelection(skillId, professionId, tagRules, allowUnassigned, previewAssignedSkillIds))
                return new GStringNameArray();
            selectedSkillIds.Add(skillId);
        }

        if (tagRules.Count == 0)
            return selectedSkillIds;

        GStringNameArray candidateSkillIds = GetRoleCandidateSkillIds(professionId, tagRules, allowUnassigned, previewAssignedSkillIds);
        while (true)
        {
            GDictionary deficits = CalculateTagRuleDeficits(selectedSkillIds, professionId, tagRules, allowUnassigned, previewAssignedSkillIds);
            if (deficits.Count == 0)
                return PruneSelection(selectedSkillIds, professionId, tagRules, allowUnassigned, normalizedLockedSkillIds, previewAssignedSkillIds);

            StringName bestSkillId = "";
            int bestScore = 0;
            foreach (StringName skillId in candidateSkillIds)
            {
                if (selectedSkillIds.Contains(skillId))
                    continue;

                int score = ScoreSkillAgainstDeficits(skillId, professionId, tagRules, allowUnassigned, deficits, previewAssignedSkillIds);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSkillId = skillId;
                }
            }

            if (bestScore <= 0 || bestSkillId == "")
                return new GStringNameArray();

            selectedSkillIds.Add(bestSkillId);
        }
    }

    private GStringNameArray GetRoleCandidateSkillIds(
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray previewAssignedSkillIds)
    {
        if (_rule_service == null || tagRules.Count == 0)
            return new GStringNameArray();

        GStringNameArray candidateSkillIds = _rule_service.get_eligible_skill_ids(professionId, tagRules, allowUnassigned);
        foreach (StringName skillId in previewAssignedSkillIds)
        {
            if (skillId == "" || candidateSkillIds.Contains(skillId))
                continue;
            if (MatchesAnyTagRule(skillId, professionId, tagRules, allowUnassigned, previewAssignedSkillIds))
                candidateSkillIds.Add(skillId);
        }
        return candidateSkillIds;
    }

    private bool CanIncludeSkillInSelection(
        StringName skillId,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray previewAssignedSkillIds)
    {
        if (tagRules.Count == 0)
            return IsRequiredSkillIdSelectable(skillId, professionId, allowUnassigned, previewAssignedSkillIds);
        return MatchesAnyTagRule(skillId, professionId, tagRules, allowUnassigned, previewAssignedSkillIds);
    }

    private GDictionary CalculateTagRuleDeficits(
        GStringNameArray selectedSkillIds,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray previewAssignedSkillIds)
    {
        GDictionary deficits = new();
        for (int index = 0; index < tagRules.Count; index++)
        {
            TagRequirement tagRule = tagRules[index];
            if (tagRule == null || tagRule.tag == "")
                continue;

            int matchedCount = 0;
            foreach (StringName skillId in selectedSkillIds)
            {
                if (_rule_service != null && _rule_service.skill_matches_tag_requirement(
                    skillId,
                    professionId,
                    tagRule,
                    allowUnassigned,
                    previewAssignedSkillIds
                ))
                {
                    matchedCount += 1;
                }
            }

            int remaining = tagRule.count - matchedCount;
            if (remaining > 0)
                deficits[index] = remaining;
        }
        return deficits;
    }

    private int ScoreSkillAgainstDeficits(
        StringName skillId,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GDictionary deficits,
        GStringNameArray previewAssignedSkillIds)
    {
        if (_rule_service == null)
            return 0;

        int score = 0;
        foreach (var rawIndex in deficits.Keys)
        {
            TagRequirement tagRule = tagRules[rawIndex.AsInt32()];
            if (tagRule == null)
                continue;
            if (_rule_service.skill_matches_tag_requirement(skillId, professionId, tagRule, allowUnassigned, previewAssignedSkillIds))
                score += 1;
        }
        return score;
    }

    private GStringNameArray PruneSelection(
        GStringNameArray selectedSkillIds,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray lockedSkillIds,
        GStringNameArray previewAssignedSkillIds)
    {
        GStringNameArray prunedSelection = new(selectedSkillIds);
        GStringNameArray normalizedLockedSkillIds = NormalizeSkillIdSelection(lockedSkillIds);

        for (int index = prunedSelection.Count - 1; index >= 0; index--)
        {
            StringName skillId = prunedSelection[index];
            if (normalizedLockedSkillIds.Contains(skillId))
                continue;

            GStringNameArray trialSelection = new(prunedSelection);
            trialSelection.RemoveAt(index);
            if (AreTagRulesSatisfied(trialSelection, professionId, tagRules, allowUnassigned, previewAssignedSkillIds))
                prunedSelection = trialSelection;
        }
        return prunedSelection;
    }

    private bool AreTagRulesSatisfied(
        GStringNameArray selectedSkillIds,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray previewAssignedSkillIds)
    {
        return CalculateTagRuleDeficits(selectedSkillIds, professionId, tagRules, allowUnassigned, previewAssignedSkillIds).Count == 0;
    }

    private bool MatchesAnyTagRule(
        StringName skillId,
        StringName professionId,
        Godot.Collections.Array<TagRequirement> tagRules,
        bool allowUnassigned,
        GStringNameArray previewAssignedSkillIds)
    {
        if (_rule_service == null)
            return false;

        foreach (TagRequirement tagRule in tagRules)
        {
            if (_rule_service.skill_matches_tag_requirement(skillId, professionId, tagRule, allowUnassigned, previewAssignedSkillIds))
                return true;
        }
        return false;
    }

    private static GStringNameArray NormalizeSkillIdSelection(GArray values)
    {
        GStringNameArray normalizedSkillIds = new();
        GDictionary seenSkillIds = new();
        if (values == null)
            return normalizedSkillIds;
        foreach (var value in values)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(value);
            if (skillId == "" || seenSkillIds.ContainsKey(skillId))
                continue;
            seenSkillIds[skillId] = true;
            normalizedSkillIds.Add(skillId);
        }
        return normalizedSkillIds;
    }

    private static GStringNameArray NormalizeSkillIdSelection(GStringNameArray values)
    {
        GStringNameArray normalizedSkillIds = new();
        GDictionary seenSkillIds = new();
        if (values == null)
            return normalizedSkillIds;
        foreach (StringName skillId in values)
        {
            if (skillId == "" || seenSkillIds.ContainsKey(skillId))
                continue;
            seenSkillIds[skillId] = true;
            normalizedSkillIds.Add(skillId);
        }
        return normalizedSkillIds;
    }

    private static Godot.Collections.Array<TagRequirement> GetTagRulesForTarget(ProfessionDef professionDef, int targetRank, bool isUnlock)
    {
        Godot.Collections.Array<TagRequirement> emptyRules = new();
        if (professionDef == null)
            return emptyRules;
        if (isUnlock)
            return professionDef.unlock_requirement != null ? professionDef.unlock_requirement.required_tag_rules : emptyRules;

        ProfessionRankRequirement rankRequirement = professionDef.get_rank_requirement(targetRank);
        return rankRequirement != null ? rankRequirement.required_tag_rules : emptyRules;
    }

    private static GStringNameArray GetRequiredSkillIdsForTarget(ProfessionDef professionDef, bool isUnlock)
    {
        if (!isUnlock || professionDef == null || professionDef.unlock_requirement == null)
            return new GStringNameArray();
        return new GStringNameArray(professionDef.unlock_requirement.required_skill_ids);
    }

    private static bool AssignedCoreMustBeSubsetOfQualifiers(ProfessionDef professionDef, bool isUnlock)
    {
        return isUnlock
            && professionDef != null
            && professionDef.unlock_requirement != null
            && professionDef.unlock_requirement.assigned_core_must_be_subset_of_qualifiers;
    }

    private static Godot.Collections.Array<TagRequirement> GetTagRulesForRole(
        Godot.Collections.Array<TagRequirement> tagRules,
        StringName selectionRole)
    {
        Godot.Collections.Array<TagRequirement> roleRules = new();
        foreach (TagRequirement tagRule in tagRules)
        {
            if (tagRule == null)
                continue;
            if (tagRule.get_normalized_selection_role() == selectionRole)
                roleRules.Add(tagRule);
        }
        return roleRules;
    }

    private static GStringNameArray GetSelectionSkillIds(GDictionary selection, string key)
    {
        if (selection == null || !selection.ContainsKey(key))
            return new GStringNameArray();
        var values = selection[key];
        return values.VariantType == Variant.Type.Array
            ? NormalizeSkillIdSelection(values.AsGodotArray())
            : new GStringNameArray();
    }

    private GDictionary SnapshotSkillAssignmentIds(GStringNameArray skillIds)
    {
        GDictionary snapshots = new();
        if (_unit_progress == null)
            return snapshots;

        foreach (StringName skillId in skillIds)
        {
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
            if (skillProgress != null)
                snapshots[skillId] = skillProgress.assigned_profession_id;
        }
        return snapshots;
    }

    private GDictionary SnapshotProfessionCoreSkillIds()
    {
        GDictionary snapshots = new();
        if (_unit_progress == null)
            return snapshots;

        foreach (var professionKey in _unit_progress.professions.Keys)
        {
            StringName professionId = ProgressionDataUtils.to_string_name(professionKey);
            UnitProfessionProgress professionProgress = _unit_progress.get_profession_progress(professionId);
            if (professionProgress != null)
                snapshots[professionId] = new GStringNameArray(professionProgress.core_skill_ids);
        }
        return snapshots;
    }

    private void RollbackPromotionAssignmentState(
        StringName professionId,
        bool createdProfessionProgress,
        GDictionary previousProfessionCoreSkillIds,
        GDictionary previousSkillAssignments)
    {
        if (_unit_progress == null)
            return;

        foreach (var professionKey in previousProfessionCoreSkillIds.Keys)
        {
            StringName snapshotProfessionId = ProgressionDataUtils.to_string_name(professionKey);
            UnitProfessionProgress professionProgress = _unit_progress.get_profession_progress(snapshotProfessionId);
            if (professionProgress == null)
                continue;
            var coreSkillIdsValue = previousProfessionCoreSkillIds[professionKey];
            professionProgress.core_skill_ids =
                coreSkillIdsValue.VariantType == Variant.Type.Array
                    ? NormalizeSkillIdSelection(coreSkillIdsValue.AsGodotArray())
                    : new GStringNameArray();
        }

        if (createdProfessionProgress)
            _unit_progress.professions.Remove(professionId);

        foreach (var skillKey in previousSkillAssignments.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(skillKey);
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
            if (skillProgress == null)
                continue;
            skillProgress.assigned_profession_id = ProgressionDataUtils.to_string_name(previousSkillAssignments[skillKey]);
            _unit_progress.set_skill_progress(skillProgress);
        }

        _unit_progress.sync_active_core_skill_ids();
    }

    private static GStringNameArray MergeUniqueSkillIds(GStringNameArray firstSkillIds, GStringNameArray secondSkillIds)
    {
        GStringNameArray mergedSkillIds = new();
        GDictionary seenSkillIds = new();

        foreach (StringName skillId in firstSkillIds)
        {
            if (skillId == "" || seenSkillIds.ContainsKey(skillId))
                continue;
            seenSkillIds[skillId] = true;
            mergedSkillIds.Add(skillId);
        }

        foreach (StringName skillId in secondSkillIds)
        {
            if (skillId == "" || seenSkillIds.ContainsKey(skillId))
                continue;
            seenSkillIds[skillId] = true;
            mergedSkillIds.Add(skillId);
        }
        return mergedSkillIds;
    }

    private static void AppendMissingSkillIds(GStringNameArray targetSkillIds, GStringNameArray sourceSkillIds)
    {
        foreach (StringName skillId in sourceSkillIds)
        {
            if (skillId == "" || targetSkillIds.Contains(skillId))
                continue;
            targetSkillIds.Add(skillId);
        }
    }

    private Godot.Collections.Array<PendingProfessionChoice> BuildPendingProfessionChoices()
    {
        Godot.Collections.Array<PendingProfessionChoice> results = new();
        if (_unit_progress == null)
            return results;

        StringName triggerSkillId = GetReadyActiveLevelTriggerSkillId();
        if (triggerSkillId == "")
            return results;

        foreach (StringName professionId in GetSortedProfessionIds())
        {
            if (!can_promote_profession(professionId))
                continue;

            UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
            bool isUnlock = professionProgress == null || professionProgress.rank <= 0;
            int targetRank = isUnlock ? 1 : professionProgress.rank + 1;
            PendingProfessionChoice choice = BuildPendingProfessionChoice(professionId, targetRank, isUnlock, triggerSkillId);
            if (choice != null)
                results.Add(choice);
        }
        return results;
    }

    private PendingProfessionChoice BuildPendingProfessionChoice(
        StringName professionId,
        int targetRank,
        bool isUnlock,
        StringName triggerSkillId)
    {
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return null;

        Godot.Collections.Array<TagRequirement> tagRules = GetTagRulesForTarget(professionDef, targetRank, isUnlock);
        Godot.Collections.Array<TagRequirement> qualifierRules = GetTagRulesForRole(tagRules, TagRequirement.SELECTION_ROLE_QUALIFIER());
        Godot.Collections.Array<TagRequirement> assignedCoreRules = GetTagRulesForRole(tagRules, TagRequirement.SELECTION_ROLE_ASSIGNED_CORE());
        bool allowUnassigned = isUnlock;
        GStringNameArray previewAssignedSkillIds = GetPreviewAssignedCoreSkillIdsForSelection(professionId, isUnlock, triggerSkillId);

        PendingProfessionChoice choice = new();
        choice.candidate_profession_ids.Add(professionId);
        choice.set_target_rank(professionId, targetRank);
        choice.qualifier_skill_pool_ids = GetRoleCandidateSkillIds(professionId, qualifierRules, allowUnassigned, previewAssignedSkillIds);
        choice.assignable_skill_candidate_ids = GetRoleCandidateSkillIds(professionId, assignedCoreRules, allowUnassigned, previewAssignedSkillIds);

        foreach (StringName requiredSkillId in GetRequiredSkillIdsForTarget(professionDef, isUnlock))
        {
            if (!choice.assignable_skill_candidate_ids.Contains(requiredSkillId))
                choice.assignable_skill_candidate_ids.Add(requiredSkillId);
        }

        GDictionary defaultSelection = ResolvePromotionSelection(
            professionId,
            targetRank,
            isUnlock,
            WithRequiredTriggerSkill(new GDictionary(), triggerSkillId)
        );
        if (triggerSkillId != "" && defaultSelection.Count == 0)
            return null;
        if (defaultSelection.Count > 0)
        {
            GStringNameArray defaultQualifierSkillIds = GetSelectionSkillIds(defaultSelection, SELECTION_KEY_QUALIFIER_SKILL_IDS);
            GStringNameArray defaultAssignedCoreSkillIds = GetSelectionSkillIds(defaultSelection, SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS);
            AppendMissingSkillIds(choice.qualifier_skill_pool_ids, defaultQualifierSkillIds);
            AppendMissingSkillIds(choice.assignable_skill_candidate_ids, defaultAssignedCoreSkillIds);
            choice.trigger_skill_ids = GetSelectionSkillIds(defaultSelection, "trigger_skill_ids");
            choice.required_qualifier_count = defaultQualifierSkillIds.Count;
            choice.required_assigned_core_count = defaultAssignedCoreSkillIds.Count;
        }

        return choice;
    }

    private GStringNameArray GetPreviewAssignedCoreSkillIdsForSelection(
        StringName professionId,
        bool isUnlock,
        StringName requiredTriggerSkillId)
    {
        GStringNameArray previewSkillIds = new();
        if (isUnlock || requiredTriggerSkillId == "")
            return previewSkillIds;
        if (requiredTriggerSkillId != GetReadyActiveLevelTriggerSkillId())
            return previewSkillIds;
        if (_assignment_service == null || !_assignment_service.can_assign_core_skill_to_profession(requiredTriggerSkillId, professionId))
            return previewSkillIds;

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(requiredTriggerSkillId);
        if (skillProgress == null || skillProgress.assigned_profession_id != "")
            return previewSkillIds;

        previewSkillIds.Add(requiredTriggerSkillId);
        return previewSkillIds;
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
        if (!SkillEffectiveMaxLevelRules.is_at_effective_max_level(skillDef, skillProgress, _unit_progress))
            return "";
        return triggerSkillId;
    }

    private static GDictionary WithRequiredTriggerSkill(GDictionary selection, StringName triggerSkillId)
    {
        GDictionary resolvedSelection = selection != null ? selection.Duplicate(true) : new GDictionary();
        if (triggerSkillId != "")
            resolvedSelection[SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID] = triggerSkillId;
        return resolvedSelection;
    }

    private static StringName GetRequiredTriggerSkillId(GDictionary selection)
    {
        if (selection == null)
            return "";
        return ProgressionDataUtils.to_string_name(selection.ContainsKey(SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID)
            ? selection[SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID]
            : Variant.From(""));
    }

    private static bool SelectionIncludesSkill(GDictionary selection, StringName skillId)
    {
        if (skillId == "")
            return false;
        return GetSelectionSkillIds(selection, "trigger_skill_ids").Contains(skillId)
            || GetSelectionSkillIds(selection, SELECTION_KEY_QUALIFIER_SKILL_IDS).Contains(skillId)
            || GetSelectionSkillIds(selection, SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS).Contains(skillId);
    }

    private bool LockReadyActiveLevelTriggerSkill(StringName skillId)
    {
        if (_unit_progress == null || skillId == "")
            return false;

        UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
        if (skillProgress == null)
            return false;

        skillProgress.is_level_trigger_active = false;
        skillProgress.is_level_trigger_locked = true;
        skillProgress.bonus_to_hit_from_lock = LockHitBonusDefault;
        _unit_progress.active_level_trigger_core_skill_id = "";
        if (!_unit_progress.locked_level_trigger_skill_ids.Contains(skillId))
            _unit_progress.locked_level_trigger_skill_ids.Add(skillId);
        _unit_progress.set_skill_progress(skillProgress);
        return true;
    }

    private void ClearLevelTriggerStateForSkill(StringName skillId)
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

    private void RefreshCachedPendingProfessionChoices()
    {
        if (_unit_progress != null)
            _unit_progress.pending_profession_choices = BuildPendingProfessionChoices();
    }

    private void GrantProfessionSkills(ProfessionDef professionDef, UnitProfessionProgress professionProgress, int targetRank)
    {
        if (professionDef == null || professionProgress == null)
            return;

        foreach (ProfessionGrantedSkill grantedSkill in professionDef.get_granted_skills_for_rank(targetRank))
        {
            if (grantedSkill == null || grantedSkill.skill_id == "")
                continue;

            professionProgress.add_granted_skill(grantedSkill.skill_id);
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(grantedSkill.skill_id);
            bool wasAlreadyLearned = skillProgress != null && skillProgress.is_learned;
            if (skillProgress == null)
                skillProgress = new UnitSkillProgress { skill_id = grantedSkill.skill_id };

            skillProgress.is_learned = true;
            if (skillProgress.profession_granted_by == "")
                skillProgress.profession_granted_by = professionDef.profession_id;
            if (!wasAlreadyLearned)
            {
                skillProgress.granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PROFESSION();
                skillProgress.granted_source_id = professionDef.profession_id;
            }

            _unit_progress.set_skill_progress(skillProgress);
        }
    }

    private void SyncCombatResourceUnlocksFromLearnedSkills()
    {
        if (_unit_progress == null)
            return;

        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_unit_progress.skills))
        {
            StringName skillId = ProgressionDataUtils.to_string_name(skillKey);
            UnitSkillProgress skillProgress = _unit_progress.get_skill_progress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;

            SkillDef skillDef = GetSkillDef(skillId);
            StringName practiceTrack = GetExclusivePracticeTrack(skillDef);
            if (practiceTrack == PracticeTrackMeditation)
                _unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_MP());
            else if (practiceTrack == PracticeTrackCultivation)
                _unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_AURA());

            UnlockCombatResourcesForSkill(skillDef, Mathf.Max(skillProgress.skill_level, 1));
        }
    }

    private void UnlockCombatResourcesForSkill(SkillDef skillDef, int skillLevel)
    {
        if (_unit_progress == null || skillDef?.combat_profile == null)
            return;

        GDictionary costs = skillDef.combat_profile.get_effective_resource_costs(skillLevel);
        if (GdInterop.GetInt(costs, "mp_cost") > 0)
            _unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_MP());
        if (GdInterop.GetInt(costs, "aura_cost") > 0)
            _unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_AURA());
    }

    private GDictionary GetUnitBaseAttributesSnapshot()
    {
        if (_unit_progress?.unit_base_attributes == null)
            return new GDictionary();
        return _unit_progress.unit_base_attributes.to_dict();
    }

    private GStringNameArray GetSortedProfessionIds()
    {
        GStringNameArray sortedIds = new();
        foreach (string professionIdString in ProgressionDataUtils.sorted_string_keys(_profession_defs))
            sortedIds.Add(new StringName(professionIdString));
        return sortedIds;
    }

    private static GDictionary IndexSkillDefs(GDictionary skillDefs)
    {
        GDictionary indexedDefs = new();
        if (skillDefs == null)
            return indexedDefs;

        foreach (var key in skillDefs.Keys)
        {
            if (skillDefs[key].AsGodotObject() is not SkillDef skillDef)
                continue;
            StringName indexedId =
                skillDef.skill_id != "" ? skillDef.skill_id : ProgressionDataUtils.to_string_name(key);
            indexedDefs[indexedId] = skillDef;
        }
        return indexedDefs;
    }

    private static GDictionary IndexProfessionDefs(GDictionary professionDefs)
    {
        GDictionary indexedDefs = new();
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
            indexedDefs[indexedId] = professionDef;
        }
        return indexedDefs;
    }
}
