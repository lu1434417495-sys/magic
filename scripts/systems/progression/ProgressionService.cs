using System;
using System.Collections.Generic;
using Godot;

using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public sealed class ProgressionService
{
    private static readonly StringName HpMaxAttributeId = "hp_max";
    private const int LockHitBonusDefault = 1;
    private static readonly StringName PracticeTrackMeditation = "meditation";
    private static readonly StringName PracticeTrackCultivation = "cultivation";
    private static readonly GStringNameArray PracticeTracks = new() { PracticeTrackMeditation, PracticeTrackCultivation };
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDef> _skill_defs = new();
    private readonly Dictionary<StringName, ProfessionDef> _profession_defs = new();
    private ProfessionRuleService _rule_service;
    private ProfessionAssignmentService _assignment_service;
    private SkillMergeService _skill_merge_service;

    public void Setup(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs
    )
    {
        Setup(unitProgress, skillDefs, professionDefs, null, null, null);
    }

    public void Setup(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        ProfessionRuleService ruleService,
        ProfessionAssignmentService assignmentService,
        SkillMergeService skillMergeService
    )
    {
        SetupInternal(
            unitProgress,
            skillDefs,
            professionDefs,
            ruleService,
            assignmentService,
            skillMergeService
        );
    }

    private void SetupInternal(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ProfessionDef> professionDefs,
        ProfessionRuleService ruleService,
        ProfessionAssignmentService assignmentService,
        SkillMergeService skillMergeService)
    {
        _unit_progress = unitProgress;
        _skill_defs.Clear();
        _profession_defs.Clear();
        CopyCatalog(skillDefs, _skill_defs);
        CopyCatalog(professionDefs, _profession_defs);

        _assignment_service = assignmentService ?? new ProfessionAssignmentService();
        _assignment_service.Setup(_unit_progress, _skill_defs, _profession_defs);

        _rule_service = ruleService ?? new ProfessionRuleService();
        _rule_service.Setup(_unit_progress, _skill_defs, _profession_defs);

        _skill_merge_service = skillMergeService ?? new SkillMergeService();
        _skill_merge_service.Setup(_unit_progress, _skill_defs, _assignment_service);

        RefreshRuntimeState();
    }

    public void RefreshRuntimeState()
    {
        if (_unit_progress == null)
            return;

        _unit_progress.SyncActiveCoreSkillIds();
        _unit_progress.SyncDefaultCombatResourceUnlocks();
        NormalizeSkillLevelsToEffectiveMax();
        RecalculateCharacterLevel();
        _rule_service?.RefreshAllProfessionStates();
        SyncCombatResourceUnlocksFromLearnedSkills();
        RefreshCachedPendingProfessionChoices();
    }

    public bool LearnKnowledge(StringName knowledgeId)
    {
        if (_unit_progress == null)
            return false;
        if (!_unit_progress.LearnKnowledge(knowledgeId))
            return false;
        RefreshRuntimeState();
        return true;
    }

    public bool LearnSkill(StringName skillId)
    {
        if (!CanLearnSkill(skillId))
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            return LearnCompositeUpgrade(skillDef);

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null)
            skillProgress = new UnitSkillProgress { skill_id = skillId };

        skillProgress.is_learned = true;
        _unit_progress.SetSkillProgress(skillProgress);
        RefreshRuntimeState();
        return true;
    }

    public bool CanLearnSkill(StringName skillId)
    {
        SkillDef skillDef = GetSkillDef(skillId);
        if (_unit_progress == null || skillDef == null)
            return false;
        if (HasInvalidPracticeConfiguration(skillDef))
            return false;
        if (IsSkillRelearnBlocked(skillId))
            return false;
        if (IsManualSkillLearnSourceBlocked(skillDef.LearnSourceKind))
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress != null && skillProgress.is_learned)
            return false;
        if (!CanLearnSkillRequirements(skillDef.LearnRequirementsTyped))
            return false;
        if (!CanSatisfyKnowledgeRequirements(skillDef.KnowledgeRequirementsTyped))
            return false;
        if (!CanSatisfySkillLevelRequirements(skillDef.SkillLevelRequirementEntriesTyped))
            return false;
        if (!CanSatisfyAttributeRequirements(skillDef.AttributeRequirementEntriesTyped))
            return false;
        if (!CanSatisfyAchievementRequirements(skillDef.AchievementRequirementsTyped))
            return false;
        if (skillDef.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            return CanLearnCompositeUpgrade(skillDef);
        return true;
    }

    public bool GrantRacialSkill(
        RacialGrantedSkill grant,
        StringName sourceType,
        StringName sourceId
    )
    {
        if (_unit_progress == null || grant == null)
            return false;
        SkillLearnSourceKind sourceKind = SkillDef.ToLearnSource(sourceType);
        if (!IsRacialGrantSourceType(sourceKind))
            return false;
        if (sourceId == "" || grant.skill_id == "")
            return false;

        int minimumSkillLevel = grant.minimum_skill_level;
        if (minimumSkillLevel < 0)
            return false;

        SkillDef skillDef = GetSkillDef(grant.skill_id);
        if (skillDef == null || skillDef.LearnSourceKind != sourceKind)
            return false;
        if (minimumSkillLevel > skillDef.max_level)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(grant.skill_id);
        if (skillProgress != null && skillProgress.is_learned)
            return false;
        if (skillProgress == null)
            skillProgress = new UnitSkillProgress { skill_id = grant.skill_id };

        skillProgress.is_learned = true;
        skillProgress.skill_level = minimumSkillLevel;
        skillProgress.granted_source_type = sourceType;
        skillProgress.granted_source_id = sourceId;

        _unit_progress.SetSkillProgress(skillProgress);
        RefreshRuntimeState();
        return true;
    }

    public bool GrantSkillMastery(StringName skillId, int amount, StringName sourceType)
    {
        if (_unit_progress == null || amount <= 0)
            return false;

        SkillDef skillDef = GetSkillDef(skillId);
        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillDef == null || skillProgress == null || !skillProgress.is_learned)
            return false;
        if (
            skillDef.MasterySourcesTyped.Count > 0
            && !HasStringName(skillDef.MasterySourcesTyped, sourceType)
        )
            return false;

        int effectiveMaxLevel = GetEffectiveSkillMaxLevel(skillDef, skillProgress);
        if (effectiveMaxLevel <= 0)
        {
            skillProgress.skill_level = 0;
            skillProgress.current_mastery = 0;
            _unit_progress.SetSkillProgress(skillProgress);
            RefreshRuntimeState();
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
            _unit_progress.SetSkillProgress(skillProgress);
            RefreshRuntimeState();
            return true;
        }

        skillProgress.current_mastery += amount;
        while (skillProgress.skill_level < effectiveMaxLevel)
        {
            int masteryRequired = skillDef.GetMasteryRequiredForLevel(skillProgress.skill_level);
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

        _unit_progress.SetSkillProgress(skillProgress);
        RefreshRuntimeState();
        return true;
    }

    public bool SetSkillCore(StringName skillId, bool enabled)
    {
        if (_unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;

        if (enabled)
        {
            skillProgress.is_core = true;
            _unit_progress.SetSkillProgress(skillProgress);
            RefreshRuntimeState();
            return true;
        }

        StringName previousProfessionId = skillProgress.assigned_profession_id;
        ClearLevelTriggerStateForSkill(skillId);
        skillProgress.is_core = false;
        skillProgress.ClearProfessionAssignment();
        _unit_progress.SetSkillProgress(skillProgress);

        if (previousProfessionId != "")
        {
            UnitProfessionProgress professionProgress = _unit_progress.GetProfessionProgress(previousProfessionId);
            professionProgress?.RemoveCoreSkill(skillId);
        }

        RefreshRuntimeState();
        return true;
    }

    public int RecalculateCharacterLevel()
    {
        if (_unit_progress == null)
            return 0;

        int rankTotal = 0;
        foreach (UnitProfessionProgress professionProgress in _unit_progress.ProfessionsTyped.Values)
            if (professionProgress != null)
                rankTotal += professionProgress.rank;

        _unit_progress.character_level = rankTotal;
        return rankTotal;
    }

    public bool CanPromoteProfession(StringName professionId)
    {
        UnitProfessionProgress professionProgress = GetProfessionProgress(professionId);
        if (professionProgress == null || professionProgress.rank <= 0)
            return _rule_service != null && _rule_service.CanUnlockProfession(professionId);
        return _rule_service != null && _rule_service.CanRankUpProfession(professionId);
    }

    public bool PromoteProfession(StringName professionId, PromotionSelectionData selection = null)
    {
        if (_unit_progress == null || _rule_service == null || _assignment_service == null)
            return false;
        if (!CanPromoteProfession(professionId))
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
        PromotionSelectionData promotionSelection = ResolvePromotionSelection(
            professionId,
            targetRank,
            isUnlock,
            selection,
            triggerSkillId
        );
        if (promotionSelection == null)
            return false;
        if (!promotionSelection.IncludesSkill(triggerSkillId))
            return false;

        IReadOnlyList<StringName> consumedSkillIds = promotionSelection.AssignedCoreSkillIds;
        IReadOnlyList<StringName> qualifierSkillIds = promotionSelection.QualifierSkillIds;
        bool createdProfessionProgress = false;
        GDictionary previousProfessionCoreSkillIds = SnapshotProfessionCoreSkillIds();
        GDictionary previousSkillAssignments = SnapshotSkillAssignmentIds(consumedSkillIds);

        if (professionProgress == null)
        {
            professionProgress = new UnitProfessionProgress { profession_id = professionId };
            _unit_progress.SetProfessionProgress(professionProgress);
            createdProfessionProgress = true;
        }

        foreach (StringName skillId in consumedSkillIds)
        {
            if (!_assignment_service.CanAssignCoreSkillToProfession(skillId, professionId))
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
            if (!_assignment_service.AssignCoreSkillToProfession(skillId, professionId))
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
            consumed_skill_ids = PromotionSelectionData.BuildStringNameArray(consumedSkillIds),
            qualifier_skill_ids = PromotionSelectionData.BuildStringNameArray(qualifierSkillIds),
            snapshot_unit_base_attributes = GetUnitBaseAttributesSnapshotTyped(),
            timestamp = (int)Time.GetUnixTimeFromSystem(),
        };
        professionProgress.AddPromotionRecord(promotionRecord);

        ApplyProfessionHitPointGain(professionDef);
        GrantProfessionSkills(professionDef, professionProgress, targetRank);
        LockReadyActiveLevelTriggerSkill(triggerSkillId);
        _unit_progress.SetProfessionProgress(professionProgress);
        RefreshRuntimeState();
        return true;
    }

    public static int CalculateProfessionHitPointGain(int hitDieRoll, int constitutionValue)
    {
        return Mathf.Max(
            1,
            Mathf.Max(hitDieRoll, 1) + CalculateConstitutionModifier(constitutionValue) * 2
        );
    }

    public static int CalculateConstitutionModifier(int constitutionValue)
    {
        return AttributeSnapshot.CalculateScoreModifier(constitutionValue);
    }

    public IReadOnlyList<PendingProfessionChoice> GetProfessionUpgradeCandidates()
    {
        var projected = new List<PendingProfessionChoice>();
        foreach (PendingProfessionChoice choice in BuildPendingProfessionChoices())
            if (choice != null)
                projected.Add(choice.DuplicateState());
        return projected;
    }

    public bool IsSkillRelearnBlocked(StringName skillId)
    {
        return _unit_progress != null && _unit_progress.IsSkillRelearnBlocked(skillId);
    }

    private bool LearnCompositeUpgrade(SkillDef skillDef)
    {
        if (_unit_progress == null || skillDef == null || skillDef.skill_id == "")
            return false;
        UnitSkillProgress existingProgress = _unit_progress.GetSkillProgress(skillDef.skill_id);
        if (existingProgress != null && existingProgress.is_learned)
            return false;

        if (_skill_merge_service != null && skillDef.UpgradeSourceSkillIdsTyped.Count > 0)
        {
            if (!_skill_merge_service.ApplyCompositeUpgradeResult(
                skillDef.skill_id,
                skillDef.UpgradeSourceSkillIdsTyped,
                skillDef.retain_source_skills_on_unlock,
                skillDef.CoreSkillTransitionModeKind
            ))
            {
                return false;
            }
        }
        else
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillDef.skill_id);
            if (skillProgress == null)
                skillProgress = new UnitSkillProgress { skill_id = skillDef.skill_id };
            skillProgress.is_learned = true;
            skillProgress.merged_from_skill_ids = new GStringNameArray(
                skillDef.UpgradeSourceSkillIdsTyped
            );
            _unit_progress.SetSkillProgress(skillProgress);
        }

        RefreshRuntimeState();
        return true;
    }

    private void ApplyProfessionHitPointGain(ProfessionDef professionDef)
    {
        if (_unit_progress?.unit_base_attributes == null || professionDef == null)
            return;

        int hitDieSides = Mathf.Max(professionDef.hit_die_sides, 1);
        int hitDieRoll = RollProfessionHitDie(hitDieSides);
        int constitutionValue = _unit_progress.unit_base_attributes.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int hpGain = CalculateProfessionHitPointGain(hitDieRoll, constitutionValue);
        int currentHpMax = _unit_progress.unit_base_attributes.GetAttributeValue(HpMaxAttributeId);
        _unit_progress.unit_base_attributes.SetAttributeValue(HpMaxAttributeId, currentHpMax + hpGain);
    }

    private static int RollProfessionHitDie(int hitDieSides)
    {
        int normalizedSides = Mathf.Max(hitDieSides, 1);
        return TrueRandomSeedService.RandiRange(1, normalizedSides);
    }

    private static bool IsManualSkillLearnSourceBlocked(SkillLearnSourceKind learnSource)
    {
        return learnSource
            is SkillLearnSourceKind.Profession
                or SkillLearnSourceKind.Race
                or SkillLearnSourceKind.Subrace
                or SkillLearnSourceKind.Ascension
                or SkillLearnSourceKind.Bloodline;
    }

    private static bool HasInvalidPracticeConfiguration(SkillDef skillDef)
    {
        if (skillDef == null)
            return false;

        int practiceTrackCount = 0;
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDef.HasTag(trackType))
                practiceTrackCount += 1;
        }
        if (practiceTrackCount == 0)
            return skillDef.PracticeTierKind != SkillPracticeTierKind.None;
        if (practiceTrackCount != 1)
            return true;
        if (skillDef.TagsTyped.Count != 1)
            return true;
        return skillDef.PracticeTierKind is SkillPracticeTierKind.None or SkillPracticeTierKind.Unknown;
    }

    private static StringName GetExclusivePracticeTrack(SkillDef skillDef)
    {
        if (skillDef == null || HasInvalidPracticeConfiguration(skillDef))
            return "";
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDef.HasTag(trackType))
                return trackType;
        }
        return "";
    }

    private static bool IsRacialGrantSourceType(SkillLearnSourceKind sourceType)
    {
        return sourceType
            is SkillLearnSourceKind.Race
                or SkillLearnSourceKind.Subrace
                or SkillLearnSourceKind.Ascension
                or SkillLearnSourceKind.Bloodline;
    }

    private SkillDef GetSkillDef(StringName skillId)
    {
        return _skill_defs.TryGetValue(skillId, out SkillDef skillDef) ? skillDef : null;
    }

    private ProfessionDef GetProfessionDef(StringName professionId)
    {
        return _profession_defs.TryGetValue(professionId, out ProfessionDef professionDef)
            ? professionDef
            : null;
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId)
    {
        return _unit_progress?.GetProfessionProgress(professionId);
    }

    private void NormalizeSkillLevelsToEffectiveMax()
    {
        if (_unit_progress == null)
            return;

        foreach (StringName skillId in _unit_progress.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
            SkillDef skillDef = GetSkillDef(skillId);
            if (skillProgress == null || skillDef == null)
                continue;

            int effectiveMaxLevel = GetEffectiveSkillMaxLevel(skillDef, skillProgress);
            if (skillProgress.skill_level <= effectiveMaxLevel)
                continue;

            skillProgress.skill_level = effectiveMaxLevel;
            skillProgress.current_mastery = 0;
            _unit_progress.SetSkillProgress(skillProgress);
        }
    }

    private int GetEffectiveSkillMaxLevel(SkillDef skillDef, UnitSkillProgress skillProgress)
    {
        return SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(
            skillDef,
            skillProgress,
            _unit_progress
        );
    }

    private bool CanLearnSkillRequirements(IReadOnlyList<StringName> requirements)
    {
        if (_unit_progress == null)
            return false;

        foreach (StringName requiredSkillId in requirements)
        {
            UnitSkillProgress requiredSkillProgress = _unit_progress.GetSkillProgress(requiredSkillId);
            if (requiredSkillProgress == null || !requiredSkillProgress.is_learned)
                return false;
        }
        return true;
    }

    private bool CanLearnCompositeUpgrade(SkillDef skillDef)
    {
        if (_unit_progress == null || skillDef == null)
            return false;
        if (!CanLearnSkillRequirements(skillDef.LearnRequirementsTyped))
            return false;
        if (!CanSatisfyKnowledgeRequirements(skillDef.KnowledgeRequirementsTyped))
            return false;
        if (!CanSatisfySkillLevelRequirements(skillDef.SkillLevelRequirementEntriesTyped))
            return false;
        if (!CanSatisfyAttributeRequirements(skillDef.AttributeRequirementEntriesTyped))
            return false;
        if (!CanSatisfyAchievementRequirements(skillDef.AchievementRequirementsTyped))
            return false;
        return true;
    }

    private bool CanSatisfyKnowledgeRequirements(IReadOnlyList<StringName> requiredKnowledgeIds)
    {
        if (_unit_progress == null)
            return false;
        foreach (StringName knowledgeId in requiredKnowledgeIds)
        {
            if (!_unit_progress.HasKnowledge(knowledgeId))
                return false;
        }
        return true;
    }

    private bool CanSatisfySkillLevelRequirements(
        IReadOnlyList<SkillDef.IntRequirementEntryData> requiredSkillLevelEntries
    )
    {
        if (_unit_progress == null)
            return false;

        foreach (SkillDef.IntRequirementEntryData entry in requiredSkillLevelEntries)
        {
            StringName requiredSkillId = entry.RequirementId;
            int requiredLevel = entry.Amount;
            if (requiredSkillId == "" || requiredLevel <= 0)
                return false;

            UnitSkillProgress requiredSkillProgress = _unit_progress.GetSkillProgress(requiredSkillId);
            if (requiredSkillProgress == null || !requiredSkillProgress.is_learned)
                return false;
            if (requiredSkillProgress.skill_level < requiredLevel)
                return false;
        }
        return true;
    }

    private bool CanSatisfyAttributeRequirements(
        IReadOnlyList<SkillDef.IntRequirementEntryData> requiredAttributeEntries
    )
    {
        if (_unit_progress?.unit_base_attributes == null)
            return false;

        foreach (SkillDef.IntRequirementEntryData entry in requiredAttributeEntries)
        {
            StringName attributeId = entry.RequirementId;
            int requiredValue = entry.Amount;
            if (attributeId == "" || requiredValue <= 0)
                return false;
            if (_unit_progress.unit_base_attributes.GetAttributeValue(attributeId) < requiredValue)
                return false;
        }
        return true;
    }

    private bool CanSatisfyAchievementRequirements(
        IReadOnlyList<StringName> requiredAchievementIds
    )
    {
        if (_unit_progress == null)
            return false;
        foreach (StringName achievementId in requiredAchievementIds)
        {
            AchievementProgressState progressState = _unit_progress.GetAchievementProgressState(achievementId);
            if (progressState == null || !progressState.is_unlocked)
                return false;
        }
        return true;
    }

    private PromotionSelectionData ResolvePromotionSelection(
        StringName professionId,
        int targetRank,
        bool isUnlock,
        PromotionSelectionData selection,
        StringName requiredTriggerSkillId
    )
    {
        selection ??= PromotionSelectionData.Empty;
        ProfessionDef professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return null;

        Godot.Collections.Array<TagRequirement> tagRules = GetTagRulesForTarget(professionDef, targetRank, isUnlock);
        Godot.Collections.Array<TagRequirement> qualifierRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.Qualifier);
        Godot.Collections.Array<TagRequirement> assignedCoreRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.AssignedCore);
        bool allowUnassigned = isUnlock;
        GStringNameArray requiredSkillIds = GetRequiredSkillIdsForTarget(professionDef, isUnlock);
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
                return null;
            }
        }

        bool hasExplicitAssignedCoreSelection = selection.HasAssignedCoreSkillIds;
        GStringNameArray assignedCoreSkillIds = new();
        if (hasExplicitAssignedCoreSelection)
            assignedCoreSkillIds = PromotionSelectionData.BuildStringNameArray(
                selection.AssignedCoreSkillIds
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
                return null;
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
                return null;
        }

        bool hasExplicitQualifierSelection = selection.HasQualifierSkillIds;
        GStringNameArray qualifierSkillIds = new();
        if (hasExplicitQualifierSelection)
            qualifierSkillIds = PromotionSelectionData.BuildStringNameArray(
                selection.QualifierSkillIds
            );

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
                return null;
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
                return null;
        }

        if (AssignedCoreMustBeSubsetOfQualifiers(professionDef, isUnlock))
        {
            foreach (StringName skillId in assignedCoreSkillIds)
            {
                if (!qualifierSkillIds.Contains(skillId))
                    return null;
            }
        }

        return new PromotionSelectionData(
            assignedCoreSkillIds,
            qualifierSkillIds,
            MergeUniqueSkillIds(qualifierSkillIds, assignedCoreSkillIds),
            hasAssignedCoreSkillIds: true,
            hasQualifierSkillIds: true,
            hasTriggerSkillIds: true
        );
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

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        SkillDef skillDef = GetSkillDef(skillId);
        if (skillProgress == null || skillDef == null)
            return false;
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return false;
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDef,
                skillProgress,
                _unit_progress
            )
        )
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

        GStringNameArray candidateSkillIds = new();
        foreach (
            StringName skillId in _rule_service.GetEligibleSkillIds(
                professionId,
                tagRules,
                allowUnassigned
            )
        )
        {
            if (skillId != "" && !candidateSkillIds.Contains(skillId))
                candidateSkillIds.Add(skillId);
        }
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
                if (_rule_service != null && _rule_service.SkillMatchesTagRequirement(
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
            if (
                _rule_service.SkillMatchesTagRequirement(
                    skillId,
                    professionId,
                    tagRule,
                    allowUnassigned,
                    previewAssignedSkillIds
                )
            )
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
            if (
                _rule_service.SkillMatchesTagRequirement(
                    skillId,
                    professionId,
                    tagRule,
                    allowUnassigned,
                    previewAssignedSkillIds
                )
            )
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

        ProfessionRankRequirement rankRequirement = professionDef.GetRankRequirement(targetRank);
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
        TagRequirementSelectionRole selectionRole)
    {
        Godot.Collections.Array<TagRequirement> roleRules = new();
        foreach (TagRequirement tagRule in tagRules)
        {
            if (tagRule == null)
                continue;
            if (tagRule.SelectionRoleKind == selectionRole)
                roleRules.Add(tagRule);
        }
        return roleRules;
    }

    private GDictionary SnapshotSkillAssignmentIds(IEnumerable<StringName> skillIds)
    {
        GDictionary snapshots = new();
        if (_unit_progress == null || skillIds == null)
            return snapshots;

        foreach (StringName skillId in skillIds)
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
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

        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress professionProgress = _unit_progress.GetProfessionProgress(professionId);
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
            UnitProfessionProgress professionProgress = _unit_progress.GetProfessionProgress(snapshotProfessionId);
            if (professionProgress == null)
                continue;
            var coreSkillIdsValue = previousProfessionCoreSkillIds[professionKey];
            professionProgress.core_skill_ids =
                coreSkillIdsValue.VariantType == Variant.Type.Array
                    ? NormalizeSkillIdSelection(coreSkillIdsValue.AsGodotArray())
                    : new GStringNameArray();
        }

        if (createdProfessionProgress)
            _unit_progress.RemoveProfessionProgress(professionId);

        foreach (var skillKey in previousSkillAssignments.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(skillKey);
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
            if (skillProgress == null)
                continue;
            skillProgress.assigned_profession_id = ProgressionDataUtils.to_string_name(previousSkillAssignments[skillKey]);
            _unit_progress.SetSkillProgress(skillProgress);
        }

        _unit_progress.SyncActiveCoreSkillIds();
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

    private List<PendingProfessionChoice> BuildPendingProfessionChoices()
    {
        List<PendingProfessionChoice> results = new();
        if (_unit_progress == null)
            return results;

        StringName triggerSkillId = GetReadyActiveLevelTriggerSkillId();
        if (triggerSkillId == "")
            return results;

        foreach (StringName professionId in GetSortedProfessionIds())
        {
            if (!CanPromoteProfession(professionId))
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
        Godot.Collections.Array<TagRequirement> qualifierRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.Qualifier);
        Godot.Collections.Array<TagRequirement> assignedCoreRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.AssignedCore);
        bool allowUnassigned = isUnlock;
        GStringNameArray previewAssignedSkillIds = GetPreviewAssignedCoreSkillIdsForSelection(professionId, isUnlock, triggerSkillId);

        PendingProfessionChoice choice = new();
        choice.AddCandidateProfessionId(professionId);
        choice.SetTargetRank(professionId, targetRank);
        choice.SetQualifierSkillPoolIds(
            GetRoleCandidateSkillIds(professionId, qualifierRules, allowUnassigned, previewAssignedSkillIds)
        );
        choice.SetAssignableSkillCandidateIds(
            GetRoleCandidateSkillIds(professionId, assignedCoreRules, allowUnassigned, previewAssignedSkillIds)
        );

        foreach (StringName requiredSkillId in GetRequiredSkillIdsForTarget(professionDef, isUnlock))
        {
            if (HasStringName(choice.AssignableSkillCandidateIdsTyped, requiredSkillId))
                continue;
            choice.AddAssignableSkillCandidateId(requiredSkillId);
        }

        PromotionSelectionData defaultSelection = ResolvePromotionSelection(
            professionId,
            targetRank,
            isUnlock,
            PromotionSelectionData.Empty,
            triggerSkillId
        );
        if (triggerSkillId != "" && defaultSelection == null)
            return null;
        if (defaultSelection != null)
        {
            IReadOnlyList<StringName> defaultQualifierSkillIds =
                defaultSelection.QualifierSkillIds;
            IReadOnlyList<StringName> defaultAssignedCoreSkillIds =
                defaultSelection.AssignedCoreSkillIds;
            foreach (StringName skillId in defaultQualifierSkillIds)
                choice.AddQualifierSkillPoolId(skillId);
            foreach (StringName skillId in defaultAssignedCoreSkillIds)
                choice.AddAssignableSkillCandidateId(skillId);
            choice.SetTriggerSkillIds(defaultSelection.TriggerSkillIds);
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
        if (
            _assignment_service == null
            || !_assignment_service.CanAssignCoreSkillToProfession(
                requiredTriggerSkillId,
                professionId
            )
        )
            return previewSkillIds;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(requiredTriggerSkillId);
        if (skillProgress == null || skillProgress.assigned_profession_id != "")
            return previewSkillIds;

        previewSkillIds.Add(requiredTriggerSkillId);
        return previewSkillIds;
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        foreach (StringName value in values)
            if (value == target)
                return true;
        return false;
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

    private bool LockReadyActiveLevelTriggerSkill(StringName skillId)
    {
        if (_unit_progress == null || skillId == "")
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress == null)
            return false;

        skillProgress.is_level_trigger_active = false;
        skillProgress.is_level_trigger_locked = true;
        skillProgress.bonus_to_hit_from_lock = LockHitBonusDefault;
        _unit_progress.active_level_trigger_core_skill_id = "";
        if (!_unit_progress.HasLockedLevelTriggerSkillId(skillId))
            _unit_progress.AddLockedLevelTriggerSkillId(skillId);
        _unit_progress.SetSkillProgress(skillProgress);
        return true;
    }

    private void ClearLevelTriggerStateForSkill(StringName skillId)
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

    private void RefreshCachedPendingProfessionChoices()
    {
        if (_unit_progress != null)
            _unit_progress.SetPendingProfessionChoices(BuildPendingProfessionChoices());
    }

    private void GrantProfessionSkills(ProfessionDef professionDef, UnitProfessionProgress professionProgress, int targetRank)
    {
        if (professionDef == null || professionProgress == null)
            return;

        foreach (ProfessionGrantedSkill grantedSkill in professionDef.GetGrantedSkillsForRank(targetRank))
        {
            if (grantedSkill == null || grantedSkill.skill_id == "")
                continue;

            professionProgress.AddGrantedSkill(grantedSkill.skill_id);
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(grantedSkill.skill_id);
            bool wasAlreadyLearned = skillProgress != null && skillProgress.is_learned;
            if (skillProgress == null)
                skillProgress = new UnitSkillProgress { skill_id = grantedSkill.skill_id };

            skillProgress.is_learned = true;
            if (skillProgress.profession_granted_by == "")
                skillProgress.profession_granted_by = professionDef.profession_id;
            if (!wasAlreadyLearned)
            {
                skillProgress.granted_source_type = UnitSkillProgress.ToStringName(
                    UnitSkillGrantSourceType.Profession
                );
                skillProgress.granted_source_id = professionDef.profession_id;
            }

            _unit_progress.SetSkillProgress(skillProgress);
        }
    }

    private void SyncCombatResourceUnlocksFromLearnedSkills()
    {
        if (_unit_progress == null)
            return;

        foreach (StringName skillId in _unit_progress.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;

            SkillDef skillDef = GetSkillDef(skillId);
            StringName practiceTrack = GetExclusivePracticeTrack(skillDef);
            if (practiceTrack == PracticeTrackMeditation)
                _unit_progress.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
            else if (practiceTrack == PracticeTrackCultivation)
                _unit_progress.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));

            UnlockCombatResourcesForSkill(skillDef, Mathf.Max(skillProgress.skill_level, 1));
        }
    }

    private void UnlockCombatResourcesForSkill(SkillDef skillDef, int skillLevel)
    {
        if (_unit_progress == null || skillDef?.combat_profile == null)
            return;

        CombatSkillResourceCosts costs = skillDef.combat_profile.GetEffectiveResourceCostValues(
            skillLevel
        );
        if (costs.MpCost > 0)
            _unit_progress.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        if (costs.AuraCost > 0)
            _unit_progress.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
    }

    private UnitBaseAttributes GetUnitBaseAttributesSnapshotTyped()
    {
        if (_unit_progress?.unit_base_attributes == null)
            return new UnitBaseAttributes();
        return _unit_progress.unit_base_attributes.DuplicateState();
    }

    private GStringNameArray GetSortedProfessionIds()
    {
        GStringNameArray sortedIds = new();
        foreach (StringName professionId in SortedKeys(_profession_defs))
            sortedIds.Add(professionId);
        return sortedIds;
    }

    private static void CopyCatalog<T>(
        IReadOnlyDictionary<StringName, T> source,
        Dictionary<StringName, T> target
    )
        where T : class
    {
        if (source == null)
            return;
        foreach (KeyValuePair<StringName, T> pair in source)
        {
            if (pair.Key != "" && pair.Value != null)
                target[pair.Key] = pair.Value;
        }
    }

    private static List<StringName> SortedKeys<T>(IReadOnlyDictionary<StringName, T> source)
    {
        List<StringName> keys = new(source?.Keys ?? Array.Empty<StringName>());
        keys.Sort((a, b) => string.CompareOrdinal((string)a, (string)b));
        return keys;
    }

}
