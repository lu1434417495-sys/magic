using System;
using System.Collections.Generic;
using Godot;

public sealed class ProgressionService
{
    private static readonly StringName HpMaxAttributeId = "hp_max";
    private const int LockHitBonusDefault = 1;
    private static readonly StringName PracticeTrackMeditation = "meditation";
    private static readonly StringName PracticeTrackCultivation = "cultivation";
    private static readonly IReadOnlyList<StringName> PracticeTracks = Array.AsReadOnly(
        new[] { PracticeTrackMeditation, PracticeTrackCultivation }
    );
    private UnitProgress _unit_progress;
    private readonly Dictionary<StringName, SkillDefinition> _skill_definitions = new();
    private readonly Dictionary<StringName, ProfessionDefinition> _profession_defs = new();
    private ProfessionRuleService _rule_service;
    private ProfessionAssignmentService _assignment_service;
    private SkillMergeService _skill_merge_service;

    public void SetupDefinitions(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefs
    )
    {
        SetupDefinitions(unitProgress, skillDefinitions, professionDefs, null, null, null);
    }

    public void SetupDefinitions(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefs,
        ProfessionRuleService ruleService,
        ProfessionAssignmentService assignmentService,
        SkillMergeService skillMergeService
    )
    {
        SetupInternal(
            unitProgress,
            skillDefinitions,
            professionDefs,
            ruleService,
            assignmentService,
            skillMergeService
        );
    }

    private void SetupInternal(
        UnitProgress unitProgress,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefs,
        ProfessionRuleService ruleService,
        ProfessionAssignmentService assignmentService,
        SkillMergeService skillMergeService)
    {
        _unit_progress = unitProgress;
        _skill_definitions.Clear();
        _profession_defs.Clear();
        CopyCatalog(skillDefinitions, _skill_definitions);
        CopyCatalog(professionDefs, _profession_defs);

        _assignment_service = assignmentService ?? new ProfessionAssignmentService();
        _assignment_service.Setup(_unit_progress, _skill_definitions, _profession_defs);

        _rule_service = ruleService ?? new ProfessionRuleService();
        _rule_service.Setup(_unit_progress, _skill_definitions, _profession_defs);

        _skill_merge_service = skillMergeService ?? new SkillMergeService();
        _skill_merge_service.Setup(_unit_progress, _skill_definitions, _assignment_service);

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

        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (skillDefinition.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            return LearnCompositeUpgrade(skillDefinition);

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
        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (_unit_progress == null || skillDefinition == null)
            return false;
        if (HasInvalidPracticeConfiguration(skillDefinition))
            return false;
        if (IsSkillRelearnBlocked(skillId))
            return false;
        if (IsManualSkillLearnSourceBlocked(skillDefinition.LearnSourceKind))
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillProgress != null && skillProgress.is_learned)
            return false;
        if (!CanLearnSkillRequirements(skillDefinition.LearnRequirements))
            return false;
        if (!CanSatisfyKnowledgeRequirements(skillDefinition.KnowledgeRequirements))
            return false;
        if (!CanSatisfySkillLevelRequirements(skillDefinition.SkillLevelRequirements))
            return false;
        if (!CanSatisfyAttributeRequirements(skillDefinition.AttributeRequirements))
            return false;
        if (!CanSatisfyAchievementRequirements(skillDefinition.AchievementRequirements))
            return false;
        if (skillDefinition.UnlockModeKind == SkillUnlockMode.CompositeUpgrade)
            return CanLearnCompositeUpgrade(skillDefinition);
        return true;
    }

    public bool GrantRacialSkill(
        RacialGrantedSkillDefinition grant,
        StringName sourceType,
        StringName sourceId
    )
    {
        if (_unit_progress == null || grant == null)
            return false;
        SkillLearnSourceKind sourceKind = SkillDefinition.ToLearnSource(sourceType);
        if (!IsRacialGrantSourceType(sourceKind))
            return false;
        if (sourceId == "" || grant.SkillId == "")
            return false;

        int minimumSkillLevel = grant.MinimumSkillLevel;
        if (minimumSkillLevel < 0)
            return false;

        SkillDefinition skillDefinition = GetSkillDefinition(grant.SkillId);
        if (skillDefinition == null || skillDefinition.LearnSourceKind != sourceKind)
            return false;
        if (minimumSkillLevel > skillDefinition.MaxLevel)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(grant.SkillId);
        if (skillProgress != null && skillProgress.is_learned)
            return false;
        if (skillProgress == null)
            skillProgress = new UnitSkillProgress { skill_id = grant.SkillId };

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

        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        if (skillDefinition == null || skillProgress == null || !skillProgress.is_learned)
            return false;
        if (
            skillDefinition.MasterySources.Count > 0
            && !HasStringName(skillDefinition.MasterySources, sourceType)
        )
            return false;

        int effectiveMaxLevel = GetEffectiveSkillMaxLevel(skillDefinition, skillProgress);
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
            int masteryRequired = skillDefinition.GetMasteryRequiredForLevel(skillProgress.skill_level);
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

        ProfessionDefinition professionDef = GetProfessionDef(professionId);
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
        Dictionary<StringName, List<StringName>> previousProfessionCoreSkillIds =
            SnapshotProfessionCoreSkillIds();
        Dictionary<StringName, StringName> previousSkillAssignments =
            SnapshotSkillAssignmentIds(consumedSkillIds);

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
            consumed_skill_ids = new StringNameList(consumedSkillIds),
            qualifier_skill_ids = new StringNameList(qualifierSkillIds),
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

    private bool LearnCompositeUpgrade(SkillDefinition skillDefinition)
    {
        if (_unit_progress == null || skillDefinition == null || skillDefinition.SkillId == "")
            return false;
        UnitSkillProgress existingProgress = _unit_progress.GetSkillProgress(skillDefinition.SkillId);
        if (existingProgress != null && existingProgress.is_learned)
            return false;

        if (_skill_merge_service != null && skillDefinition.UpgradeSourceSkillIds.Count > 0)
        {
            if (!_skill_merge_service.ApplyCompositeUpgradeResult(
                skillDefinition.SkillId,
                skillDefinition.UpgradeSourceSkillIds,
                skillDefinition.RetainSourceSkillsOnUnlock,
                skillDefinition.CoreSkillTransitionModeKind
            ))
            {
                return false;
            }
        }
        else
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillDefinition.SkillId);
            if (skillProgress == null)
                skillProgress = new UnitSkillProgress { skill_id = skillDefinition.SkillId };
            skillProgress.is_learned = true;
            skillProgress.merged_from_skill_ids = new StringNameList(
                skillDefinition.UpgradeSourceSkillIds
            );
            _unit_progress.SetSkillProgress(skillProgress);
        }

        RefreshRuntimeState();
        return true;
    }

    private void ApplyProfessionHitPointGain(ProfessionDefinition professionDef)
    {
        if (_unit_progress?.unit_base_attributes == null || professionDef == null)
            return;

        int hitDieSides = Mathf.Max(professionDef.HitDieSides, 1);
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
            is SkillLearnSourceKind.Internal
                or SkillLearnSourceKind.Profession
                or SkillLearnSourceKind.Race
                or SkillLearnSourceKind.Subrace
                or SkillLearnSourceKind.Ascension
                or SkillLearnSourceKind.Bloodline;
    }

    private static bool HasInvalidPracticeConfiguration(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null)
            return false;

        int practiceTrackCount = 0;
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDefinition.HasTag(trackType))
                practiceTrackCount += 1;
        }
        if (practiceTrackCount == 0)
            return skillDefinition.PracticeTierKind != SkillPracticeTierKind.None;
        if (practiceTrackCount != 1)
            return true;
        if (skillDefinition.Tags.Count != 1)
            return true;
        return skillDefinition.PracticeTierKind
            is SkillPracticeTierKind.None
                or SkillPracticeTierKind.Unknown;
    }

    private static StringName GetExclusivePracticeTrack(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null || HasInvalidPracticeConfiguration(skillDefinition))
            return "";
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDefinition.HasTag(trackType))
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

    private SkillDefinition GetSkillDefinition(StringName skillId)
    {
        return _skill_definitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private ProfessionDefinition GetProfessionDef(StringName professionId)
    {
        return _profession_defs.TryGetValue(professionId, out ProfessionDefinition professionDef)
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
            SkillDefinition skillDefinition = GetSkillDefinition(skillId);
            if (skillProgress == null || skillDefinition == null)
                continue;

            int effectiveMaxLevel = GetEffectiveSkillMaxLevel(skillDefinition, skillProgress);
            if (skillProgress.skill_level <= effectiveMaxLevel)
                continue;

            skillProgress.skill_level = effectiveMaxLevel;
            skillProgress.current_mastery = 0;
            _unit_progress.SetSkillProgress(skillProgress);
        }
    }

    private int GetEffectiveSkillMaxLevel(SkillDefinition skillDefinition, UnitSkillProgress skillProgress)
    {
        return SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(
            skillDefinition,
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

    private bool CanLearnCompositeUpgrade(SkillDefinition skillDefinition)
    {
        if (_unit_progress == null || skillDefinition == null)
            return false;
        if (!CanLearnSkillRequirements(skillDefinition.LearnRequirements))
            return false;
        if (!CanSatisfyKnowledgeRequirements(skillDefinition.KnowledgeRequirements))
            return false;
        if (!CanSatisfySkillLevelRequirements(skillDefinition.SkillLevelRequirements))
            return false;
        if (!CanSatisfyAttributeRequirements(skillDefinition.AttributeRequirements))
            return false;
        if (!CanSatisfyAchievementRequirements(skillDefinition.AchievementRequirements))
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
        IReadOnlyDictionary<StringName, int> requiredSkillLevelEntries
    )
    {
        if (_unit_progress == null)
            return false;

        foreach (KeyValuePair<StringName, int> entry in requiredSkillLevelEntries)
        {
            StringName requiredSkillId = entry.Key;
            int requiredLevel = entry.Value;
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
        IReadOnlyDictionary<StringName, int> requiredAttributeEntries
    )
    {
        if (_unit_progress?.unit_base_attributes == null)
            return false;

        foreach (KeyValuePair<StringName, int> entry in requiredAttributeEntries)
        {
            StringName attributeId = entry.Key;
            int requiredValue = entry.Value;
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
        ProfessionDefinition professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return null;

        IReadOnlyList<TagRequirementDefinition> tagRules = GetTagRulesForTarget(professionDef, targetRank, isUnlock);
        IReadOnlyList<TagRequirementDefinition> qualifierRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.Qualifier);
        IReadOnlyList<TagRequirementDefinition> assignedCoreRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.AssignedCore);
        bool allowUnassigned = isUnlock;
        List<StringName> requiredSkillIds = GetRequiredSkillIdsForTarget(professionDef, isUnlock);
        List<StringName> previewAssignedSkillIds = GetPreviewAssignedCoreSkillIdsForSelection(
            professionId,
            isUnlock,
            requiredTriggerSkillId
        );
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
        List<StringName> assignedCoreSkillIds = new();
        if (hasExplicitAssignedCoreSelection)
            assignedCoreSkillIds = new List<StringName>(selection.AssignedCoreSkillIds);
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
        List<StringName> qualifierSkillIds = new();
        if (hasExplicitQualifierSelection)
            qualifierSkillIds = new List<StringName>(selection.QualifierSkillIds);

        List<StringName> qualifierLockedSkillIds = new();
        if (AssignedCoreMustBeSubsetOfQualifiers(professionDef, isUnlock))
            qualifierLockedSkillIds = new List<StringName>(assignedCoreSkillIds);
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
        IReadOnlyList<StringName> selectedSkillIds,
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> requiredSkillIds,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        if (!SelectionContainsRequiredSkillIds(selectedSkillIds, requiredSkillIds))
            return false;

        foreach (StringName skillId in selectedSkillIds)
        {
            if (HasStringName(requiredSkillIds, skillId))
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

    private static bool SelectionContainsRequiredSkillIds(
        IReadOnlyList<StringName> selectedSkillIds,
        IReadOnlyList<StringName> requiredSkillIds
    )
    {
        foreach (StringName requiredSkillId in requiredSkillIds)
        {
            if (!HasStringName(selectedSkillIds, requiredSkillId))
                return false;
        }
        return true;
    }

    private bool IsRequiredSkillIdSelectable(
        StringName skillId,
        StringName professionId,
        bool allowUnassigned,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        if (_unit_progress == null)
            return false;

        UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(skillId);
        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (skillProgress == null || skillDefinition == null)
            return false;
        if (!skillProgress.is_learned || !skillProgress.is_core)
            return false;
        if (
            !SkillEffectiveMaxLevelRules.IsAtEffectiveMaxLevel(
                skillDefinition,
                skillProgress,
                _unit_progress
            )
        )
            return false;
        if (professionId != "" && skillProgress.assigned_profession_id == professionId)
            return true;
        if (skillProgress.assigned_profession_id != "")
            return false;
        return allowUnassigned || HasStringName(previewAssignedSkillIds, skillId);
    }

    private List<StringName> SelectSkillIdsForTagRules(
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> lockedSkillIds,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        List<StringName> selectedSkillIds = new();
        List<StringName> normalizedLockedSkillIds = NormalizeSkillIdSelection(lockedSkillIds);

        foreach (StringName skillId in normalizedLockedSkillIds)
        {
            if (!CanIncludeSkillInSelection(skillId, professionId, tagRules, allowUnassigned, previewAssignedSkillIds))
                return new List<StringName>();
            selectedSkillIds.Add(skillId);
        }

        if (tagRules.Count == 0)
            return selectedSkillIds;

        List<StringName> candidateSkillIds = GetRoleCandidateSkillIds(
            professionId,
            tagRules,
            allowUnassigned,
            previewAssignedSkillIds
        );
        while (true)
        {
            Dictionary<int, int> deficits = CalculateTagRuleDeficits(
                selectedSkillIds,
                professionId,
                tagRules,
                allowUnassigned,
                previewAssignedSkillIds
            );
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
                return new List<StringName>();

            selectedSkillIds.Add(bestSkillId);
        }
    }

    private List<StringName> GetRoleCandidateSkillIds(
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        if (_rule_service == null || tagRules.Count == 0)
            return new List<StringName>();

        List<StringName> candidateSkillIds = new();
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
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        if (tagRules.Count == 0)
            return IsRequiredSkillIdSelectable(skillId, professionId, allowUnassigned, previewAssignedSkillIds);
        return MatchesAnyTagRule(skillId, professionId, tagRules, allowUnassigned, previewAssignedSkillIds);
    }

    private Dictionary<int, int> CalculateTagRuleDeficits(
        IReadOnlyList<StringName> selectedSkillIds,
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        Dictionary<int, int> deficits = new();
        for (int index = 0; index < tagRules.Count; index++)
        {
            TagRequirementDefinition tagRule = tagRules[index];
            if (tagRule == null || tagRule.Tag == "")
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

            int remaining = tagRule.Count - matchedCount;
            if (remaining > 0)
                deficits[index] = remaining;
        }
        return deficits;
    }

    private int ScoreSkillAgainstDeficits(
        StringName skillId,
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyDictionary<int, int> deficits,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        if (_rule_service == null)
            return 0;

        int score = 0;
        foreach (int ruleIndex in deficits.Keys)
        {
            TagRequirementDefinition tagRule = tagRules[ruleIndex];
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

    private List<StringName> PruneSelection(
        IReadOnlyList<StringName> selectedSkillIds,
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> lockedSkillIds,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        List<StringName> prunedSelection = new(selectedSkillIds);
        List<StringName> normalizedLockedSkillIds = NormalizeSkillIdSelection(lockedSkillIds);

        for (int index = prunedSelection.Count - 1; index >= 0; index--)
        {
            StringName skillId = prunedSelection[index];
            if (normalizedLockedSkillIds.Contains(skillId))
                continue;

            List<StringName> trialSelection = new(prunedSelection);
            trialSelection.RemoveAt(index);
            if (AreTagRulesSatisfied(trialSelection, professionId, tagRules, allowUnassigned, previewAssignedSkillIds))
                prunedSelection = trialSelection;
        }
        return prunedSelection;
    }

    private bool AreTagRulesSatisfied(
        IReadOnlyList<StringName> selectedSkillIds,
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        return CalculateTagRuleDeficits(selectedSkillIds, professionId, tagRules, allowUnassigned, previewAssignedSkillIds).Count == 0;
    }

    private bool MatchesAnyTagRule(
        StringName skillId,
        StringName professionId,
        IReadOnlyList<TagRequirementDefinition> tagRules,
        bool allowUnassigned,
        IReadOnlyList<StringName> previewAssignedSkillIds)
    {
        if (_rule_service == null)
            return false;

        foreach (TagRequirementDefinition tagRule in tagRules)
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

    private static List<StringName> NormalizeSkillIdSelection(IEnumerable<StringName> values)
    {
        List<StringName> normalizedSkillIds = new();
        HashSet<StringName> seenSkillIds = new();
        if (values == null)
            return normalizedSkillIds;
        foreach (StringName skillId in values)
        {
            if (skillId == "" || !seenSkillIds.Add(skillId))
                continue;
            normalizedSkillIds.Add(skillId);
        }
        return normalizedSkillIds;
    }

    private static IReadOnlyList<TagRequirementDefinition> GetTagRulesForTarget(ProfessionDefinition professionDef, int targetRank, bool isUnlock)
    {
        IReadOnlyList<TagRequirementDefinition> emptyRules = System.Array.Empty<TagRequirementDefinition>();
        if (professionDef == null)
            return emptyRules;
        if (isUnlock)
            return professionDef.UnlockRequirement != null ? professionDef.UnlockRequirement.RequiredTagRules : emptyRules;

        ProfessionRankRequirementDefinition rankRequirement = professionDef.GetRankRequirement(targetRank);
        return rankRequirement != null ? rankRequirement.RequiredTagRules : emptyRules;
    }

    private static List<StringName> GetRequiredSkillIdsForTarget(
        ProfessionDefinition professionDef,
        bool isUnlock
    )
    {
        if (!isUnlock || professionDef == null || professionDef.UnlockRequirement == null)
            return new List<StringName>();
        return new List<StringName>(professionDef.UnlockRequirement.RequiredSkillIds);
    }

    private static bool AssignedCoreMustBeSubsetOfQualifiers(ProfessionDefinition professionDef, bool isUnlock)
    {
        return isUnlock
            && professionDef != null
            && professionDef.UnlockRequirement != null
            && professionDef.UnlockRequirement.AssignedCoreMustBeSubsetOfQualifiers;
    }

    private static IReadOnlyList<TagRequirementDefinition> GetTagRulesForRole(
        IReadOnlyList<TagRequirementDefinition> tagRules,
        TagRequirementSelectionRole selectionRole)
    {
        List<TagRequirementDefinition> roleRules = new();
        foreach (TagRequirementDefinition tagRule in tagRules)
        {
            if (tagRule == null)
                continue;
            if (tagRule.SelectionRoleKind == selectionRole)
                roleRules.Add(tagRule);
        }
        return roleRules;
    }

    private Dictionary<StringName, StringName> SnapshotSkillAssignmentIds(
        IEnumerable<StringName> skillIds
    )
    {
        Dictionary<StringName, StringName> snapshots = new();
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

    private Dictionary<StringName, List<StringName>> SnapshotProfessionCoreSkillIds()
    {
        Dictionary<StringName, List<StringName>> snapshots = new();
        if (_unit_progress == null)
            return snapshots;

        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress professionProgress = _unit_progress.GetProfessionProgress(professionId);
            if (professionProgress != null)
                snapshots[professionId] = new List<StringName>(
                    professionProgress.core_skill_ids
                );
        }
        return snapshots;
    }

    private void RollbackPromotionAssignmentState(
        StringName professionId,
        bool createdProfessionProgress,
        IReadOnlyDictionary<StringName, List<StringName>> previousProfessionCoreSkillIds,
        IReadOnlyDictionary<StringName, StringName> previousSkillAssignments)
    {
        if (_unit_progress == null)
            return;

        foreach (
            KeyValuePair<StringName, List<StringName>> snapshot in previousProfessionCoreSkillIds
        )
        {
            UnitProfessionProgress professionProgress = _unit_progress.GetProfessionProgress(
                snapshot.Key
            );
            if (professionProgress == null)
                continue;
            professionProgress.core_skill_ids = new StringNameList(
                NormalizeSkillIdSelection(snapshot.Value)
            );
        }

        if (createdProfessionProgress)
            _unit_progress.RemoveProfessionProgress(professionId);

        foreach (KeyValuePair<StringName, StringName> snapshot in previousSkillAssignments)
        {
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(snapshot.Key);
            if (skillProgress == null)
                continue;
            skillProgress.assigned_profession_id = snapshot.Value;
            _unit_progress.SetSkillProgress(skillProgress);
        }

        _unit_progress.SyncActiveCoreSkillIds();
    }

    private static List<StringName> MergeUniqueSkillIds(
        IReadOnlyList<StringName> firstSkillIds,
        IReadOnlyList<StringName> secondSkillIds
    )
    {
        List<StringName> mergedSkillIds = new();
        HashSet<StringName> seenSkillIds = new();

        foreach (StringName skillId in firstSkillIds)
        {
            if (skillId == "" || !seenSkillIds.Add(skillId))
                continue;
            mergedSkillIds.Add(skillId);
        }

        foreach (StringName skillId in secondSkillIds)
        {
            if (skillId == "" || !seenSkillIds.Add(skillId))
                continue;
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
        ProfessionDefinition professionDef = GetProfessionDef(professionId);
        if (professionDef == null)
            return null;

        IReadOnlyList<TagRequirementDefinition> tagRules = GetTagRulesForTarget(professionDef, targetRank, isUnlock);
        IReadOnlyList<TagRequirementDefinition> qualifierRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.Qualifier);
        IReadOnlyList<TagRequirementDefinition> assignedCoreRules = GetTagRulesForRole(tagRules, TagRequirementSelectionRole.AssignedCore);
        bool allowUnassigned = isUnlock;
        List<StringName> previewAssignedSkillIds = GetPreviewAssignedCoreSkillIdsForSelection(
            professionId,
            isUnlock,
            triggerSkillId
        );

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

    private List<StringName> GetPreviewAssignedCoreSkillIdsForSelection(
        StringName professionId,
        bool isUnlock,
        StringName requiredTriggerSkillId)
    {
        List<StringName> previewSkillIds = new();
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

    private void GrantProfessionSkills(ProfessionDefinition professionDef, UnitProfessionProgress professionProgress, int targetRank)
    {
        if (professionDef == null || professionProgress == null)
            return;

        foreach (ProfessionGrantedSkillDefinition grantedSkill in professionDef.GetGrantedSkillsForRank(targetRank))
        {
            if (grantedSkill == null || grantedSkill.SkillId == "")
                continue;

            professionProgress.AddGrantedSkill(grantedSkill.SkillId);
            UnitSkillProgress skillProgress = _unit_progress.GetSkillProgress(grantedSkill.SkillId);
            bool wasAlreadyLearned = skillProgress != null && skillProgress.is_learned;
            if (skillProgress == null)
                skillProgress = new UnitSkillProgress { skill_id = grantedSkill.SkillId };

            skillProgress.is_learned = true;
            if (skillProgress.profession_granted_by == "")
                skillProgress.profession_granted_by = professionDef.ProfessionId;
            if (!wasAlreadyLearned)
            {
                skillProgress.granted_source_type = UnitSkillProgress.ToStringName(
                    UnitSkillGrantSourceType.Profession
                );
                skillProgress.granted_source_id = professionDef.ProfessionId;
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

            SkillDefinition skillDefinition = GetSkillDefinition(skillId);
            StringName practiceTrack = GetExclusivePracticeTrack(skillDefinition);
            if (practiceTrack == PracticeTrackMeditation)
                _unit_progress.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
            else if (practiceTrack == PracticeTrackCultivation)
                _unit_progress.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));

            UnlockCombatResourcesForSkill(skillDefinition, Mathf.Max(skillProgress.skill_level, 1));
        }
    }

    private void UnlockCombatResourcesForSkill(SkillDefinition skillDefinition, int skillLevel)
    {
        if (_unit_progress == null || skillDefinition?.CombatProfile == null)
            return;

        CombatSkillResourceCosts costs = skillDefinition.CombatProfile.GetEffectiveResourceCostValues(
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

    private List<StringName> GetSortedProfessionIds() => SortedKeys(_profession_defs);

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
