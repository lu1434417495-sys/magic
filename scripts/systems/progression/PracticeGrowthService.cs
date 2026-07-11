using System.Collections.Generic;
using Godot;

internal enum PracticeTrackKind
{
    Unknown = 0,
    Meditation,
    Cultivation,
}

public sealed class PracticeGrowthService
{
    private static readonly StringName TrackMeditation = "meditation";
    private static readonly StringName TrackCultivation = "cultivation";
    private static readonly HashSet<StringName> PracticeTracks = new()
    {
        TrackMeditation,
        TrackCultivation,
    };

    private const int TierBasic = 0;
    private const int TierIntermediate = 1;
    private const int TierAdvanced = 2;
    private const int TierUltimate = 3;

    private static readonly StringName MpMaxAttr = "mp_max";
    private static readonly StringName AuraMaxAttr = "aura_max";

    private readonly Dictionary<StringName, SkillDefinition> _skillDefinitions = new();

    internal static StringName ToStringName(PracticeTrackKind kind)
    {
        return kind switch
        {
            PracticeTrackKind.Meditation => TrackMeditation,
            PracticeTrackKind.Cultivation => TrackCultivation,
            _ => "",
        };
    }

    internal static PracticeTrackKind ToTrackKind(StringName trackType)
    {
        if (trackType == TrackMeditation)
            return PracticeTrackKind.Meditation;
        if (trackType == TrackCultivation)
            return PracticeTrackKind.Cultivation;
        return PracticeTrackKind.Unknown;
    }

    public void Setup(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, ProfessionDefinition> _professionDefs
    )
    {
        _skillDefinitions.Clear();
        if (skillDefinitions == null)
            return;
        foreach (KeyValuePair<StringName, SkillDefinition> pair in skillDefinitions)
        {
            if (pair.Key != "" && pair.Value != null)
                _skillDefinitions[pair.Key] = pair.Value;
        }
    }

    public StringName GetTrackTypeForSkill(StringName skillId)
    {
        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        return skillDefinition != null ? GetExclusivePracticeTrack(skillDefinition) : "";
    }

    public int GetPracticeTier(StringName skillId)
    {
        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (skillDefinition == null)
            return -1;
        return ResolveTierValue(skillDefinition.PracticeTier);
    }

    public static int ResolveTierValue(StringName tierName)
    {
        return ResolveTierValue(SkillDefinition.ToPracticeTier(tierName));
    }

    public static StringName ResolveTierName(int tierValue)
    {
        return tierValue switch
        {
            TierBasic => SkillDefinition.ToPracticeTierName(SkillPracticeTierKind.Basic),
            TierIntermediate => SkillDefinition.ToPracticeTierName(SkillPracticeTierKind.Intermediate),
            TierAdvanced => SkillDefinition.ToPracticeTierName(SkillPracticeTierKind.Advanced),
            TierUltimate => SkillDefinition.ToPracticeTierName(SkillPracticeTierKind.Ultimate),
            _ => "",
        };
    }

    public StringName GetActivePracticeSkill(UnitProgress unitProgress, StringName trackType)
    {
        List<StringName> activeSkillIds = GetActivePracticeSkillIds(unitProgress, trackType);
        return activeSkillIds.Count == 1 ? activeSkillIds[0] : "";
    }

    public PracticeSkillLearnStatus CanLearnPracticeSkillTyped(
        StringName skillId,
        UnitProgress unitProgress
    )
    {
        StringName trackType = GetTrackTypeForSkill(skillId);
        if (trackType == "")
            return PracticeSkillLearnStatus.NonPractice();
        if (!HasValidPracticeTier(skillId))
            return PracticeSkillLearnStatus.Practice(trackType, false, false, "");

        List<StringName> existingSkillIds = GetActivePracticeSkillIds(unitProgress, trackType);
        if (existingSkillIds.Count > 1)
        {
            return PracticeSkillLearnStatus.Practice(
                trackType,
                false,
                false,
                "",
                "ambiguous_existing_practice_track"
            );
        }

        StringName existingSkillId = existingSkillIds.Count == 1 ? existingSkillIds[0] : "";
        if (existingSkillId == "")
            return PracticeSkillLearnStatus.Practice(trackType, true, false, "");
        if (existingSkillId == skillId)
            return PracticeSkillLearnStatus.Practice(trackType, false, false, existingSkillId);
        return PracticeSkillLearnStatus.Practice(trackType, false, true, existingSkillId);
    }

    public int CalculateReplacementLevel(
        StringName oldSkillId,
        StringName newSkillId,
        UnitProgress unitProgress
    )
    {
        int oldTier = GetPracticeTier(oldSkillId);
        int newTier = GetPracticeTier(newSkillId);
        if (oldTier < 0 || newTier < 0)
            return -1;

        UnitSkillProgress oldSkillProgress = unitProgress?.GetSkillProgress(oldSkillId);
        int oldLevel = oldSkillProgress?.skill_level ?? 0;
        SkillDefinition newSkillDefinition = GetSkillDefinition(newSkillId);
        if (newSkillDefinition == null)
            return 0;

        int rawNewLevel = oldLevel + (oldTier - newTier);
        int maxLevel =
            newSkillDefinition.MaxLevel >= 0
                ? Mathf.Max(newSkillDefinition.MaxLevel, 0)
                : 999;
        if (newSkillDefinition.DynamicMaxLevelStatId != "")
        {
            int absoluteMax = SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(
                newSkillDefinition,
                unitProgress
            );
            if (absoluteMax > 0)
                maxLevel = Mathf.Min(maxLevel, absoluteMax);
        }
        return Mathf.Clamp(rawNewLevel, 0, maxLevel);
    }

    public bool ApplyReplacement(StringName newSkillId, UnitProgress unitProgress)
    {
        return ApplyReplacement(newSkillId, unitProgress, false);
    }

    public bool ApplyReplacement(
        StringName newSkillId,
        UnitProgress unitProgress,
        bool formalLearningVerified
    )
    {
        if (!formalLearningVerified)
            return false;

        StringName trackType = GetTrackTypeForSkill(newSkillId);
        if (trackType == "")
            return false;

        PracticeSkillLearnStatus learnResult = CanLearnPracticeSkillTyped(
            newSkillId,
            unitProgress
        );
        if (!learnResult.NeedsReplacement)
            return false;

        StringName oldSkillId = learnResult.ExistingSkillId;
        if (oldSkillId == "")
            return false;

        int predictedLevel = CalculateReplacementLevel(oldSkillId, newSkillId, unitProgress);
        if (predictedLevel < 0)
            return false;

        ClearReplacedSkillReferences(unitProgress, oldSkillId);
        unitProgress.RemoveSkillProgress(oldSkillId);

        UnitSkillProgress newSkillProgress = new()
        {
            skill_id = newSkillId,
            is_learned = true,
            skill_level = predictedLevel,
        };
        unitProgress.SetSkillProgress(newSkillProgress);
        return true;
    }

    public PracticeSkillLearnStatus GetSkillLearnedStatusTyped(
        StringName skillId,
        UnitProgress unitProgress
    )
    {
        StringName trackType = GetTrackTypeForSkill(skillId);
        if (trackType == "")
            return PracticeSkillLearnStatus.NonPractice();

        PracticeSkillLearnStatus result = CanLearnPracticeSkillTyped(skillId, unitProgress);
        return result.NeedsReplacement
            ? result.WithPredictedLevel(
                CalculateReplacementLevel(result.ExistingSkillId, skillId, unitProgress)
            )
            : result;
    }

    public void InjectFirstUnlockStartingValues(
        PartyMemberState memberState,
        StringName trackType
    )
    {
        if (memberState?.progression == null)
            return;

        UnitProgress unitProgress = memberState.progression as UnitProgress;
        if (unitProgress == null)
            return;
        UnitBaseAttributes baseAttrs = unitProgress.unit_base_attributes;
        if (baseAttrs == null)
            return;

        StringName existingSkillId = GetActivePracticeSkill(unitProgress, trackType);
        if (existingSkillId == "")
            return;
        if (GetSkillDefinition(existingSkillId) == null)
            return;

        int growth = CalculateDailyUpperLimitGrowth(unitProgress, existingSkillId, trackType);
        if (ToTrackKind(trackType) == PracticeTrackKind.Meditation)
        {
            baseAttrs.SetAttributeValue(MpMaxAttr, growth);
            memberState.SetCurrentMp(growth);
        }
        else if (ToTrackKind(trackType) == PracticeTrackKind.Cultivation)
        {
            baseAttrs.SetAttributeValue(AuraMaxAttr, growth);
            memberState.SetCurrentAura(growth);
        }
    }

    public void ApplyDailyGrowthToMember(PartyMemberState memberState, int daysElapsed)
    {
        if (memberState?.progression == null || daysElapsed <= 0)
            return;

        UnitProgress unitProgress = memberState.progression as UnitProgress;
        if (unitProgress == null)
            return;
        UnitBaseAttributes baseAttrs = unitProgress.unit_base_attributes;
        if (baseAttrs == null)
            return;

        foreach (StringName trackType in PracticeTracks)
        {
            StringName skillId = GetActivePracticeSkill(unitProgress, trackType);
            if (skillId == "")
                continue;

            int singleDayGrowth = CalculateDailyUpperLimitGrowth(unitProgress, skillId, trackType);
            int singleDayRecovery = CalculateDailyRecovery(unitProgress, skillId, trackType);

            if (ToTrackKind(trackType) == PracticeTrackKind.Meditation)
            {
                int currentMax = baseAttrs.GetAttributeValue(MpMaxAttr);
                baseAttrs.SetAttributeValue(
                    MpMaxAttr,
                    currentMax + singleDayGrowth * daysElapsed
                );
                memberState.SetCurrentMp(Mathf.Min(
                    memberState.current_mp + singleDayRecovery * daysElapsed,
                    baseAttrs.GetAttributeValue(MpMaxAttr)
                ));
            }
            else if (ToTrackKind(trackType) == PracticeTrackKind.Cultivation)
            {
                int currentMax = baseAttrs.GetAttributeValue(AuraMaxAttr);
                baseAttrs.SetAttributeValue(
                    AuraMaxAttr,
                    currentMax + singleDayGrowth * daysElapsed
                );
                memberState.SetCurrentAura(Mathf.Min(
                    memberState.current_aura + singleDayRecovery * daysElapsed,
                    baseAttrs.GetAttributeValue(AuraMaxAttr)
                ));
            }
        }
    }

    public static string GetTrackDisplayName(StringName trackType)
    {
        return ToTrackKind(trackType) == PracticeTrackKind.Meditation ? "冥想" : "修炼";
    }

    public static string GetTierDisplayName(int tierValue)
    {
        return tierValue switch
        {
            TierBasic => "基础",
            TierIntermediate => "进阶",
            TierAdvanced => "高阶",
            TierUltimate => "终极",
            _ => "",
        };
    }

    private SkillDefinition GetSkillDefinition(StringName skillId)
    {
        return _skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private static StringName GetExclusivePracticeTrack(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null)
            return "";

        StringName matchedTrack = "";
        int matchedCount = 0;
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDefinition.HasTag(trackType))
            {
                matchedTrack = trackType;
                matchedCount += 1;
            }
        }
        if (matchedCount != 1 || skillDefinition.Tags.Count != 1)
            return "";
        return matchedTrack;
    }

    private bool HasValidPracticeTier(StringName skillId)
    {
        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        return skillDefinition != null && ResolveTierValue(skillDefinition.PracticeTier) >= 0;
    }

    private static int ResolveTierValue(SkillPracticeTierKind tierKind)
    {
        return tierKind switch
        {
            SkillPracticeTierKind.Basic => TierBasic,
            SkillPracticeTierKind.Intermediate => TierIntermediate,
            SkillPracticeTierKind.Advanced => TierAdvanced,
            SkillPracticeTierKind.Ultimate => TierUltimate,
            _ => -1,
        };
    }

    private List<StringName> GetActivePracticeSkillIds(
        UnitProgress unitProgress,
        StringName trackType
    )
    {
        List<StringName> activeSkillIds = new();
        if (unitProgress == null || !PracticeTracks.Contains(trackType))
            return activeSkillIds;

        foreach (StringName skillId in unitProgress.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress skillProgress = unitProgress.GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            if (GetTrackTypeForSkill(skillId) == trackType)
                activeSkillIds.Add(skillId);
        }
        return activeSkillIds;
    }

    private static void ClearReplacedSkillReferences(
        UnitProgress unitProgress,
        StringName oldSkillId
    )
    {
        if (unitProgress == null || oldSkillId == "")
            return;

        if (unitProgress.active_level_trigger_core_skill_id == oldSkillId)
            unitProgress.active_level_trigger_core_skill_id = "";
        unitProgress.RemoveLockedLevelTriggerSkillId(oldSkillId);

        UnitSkillProgress oldSkillProgress = unitProgress.GetSkillProgress(oldSkillId);
        if (oldSkillProgress != null)
        {
            oldSkillProgress.is_level_trigger_active = false;
            oldSkillProgress.is_level_trigger_locked = false;
            unitProgress.SetSkillProgress(oldSkillProgress);
        }

        foreach (StringName professionId in unitProgress.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress professionProgress = unitProgress.GetProfessionProgress(
                professionId
            );
            if (professionProgress == null)
                continue;
            if (professionProgress.core_skill_ids.Contains(oldSkillId))
            {
                professionProgress.RemoveCoreSkill(oldSkillId);
                unitProgress.SetProfessionProgress(professionProgress);
            }
        }
    }

    private int CalculateDailyUpperLimitGrowth(
        UnitProgress unitProgress,
        StringName skillId,
        StringName trackType
    )
    {
        UnitSkillProgress skillProgress = unitProgress?.GetSkillProgress(skillId);
        if (skillProgress == null)
            return 0;

        UnitBaseAttributes baseAttrs = unitProgress.unit_base_attributes;
        if (baseAttrs == null)
            return 0;

        int skillLevel = skillProgress.skill_level;
        int professionBonus = GetProfessionWhitelistBonus(unitProgress, trackType);
        int knowledgeBonus = GetKnowledgeWhitelistBonus(unitProgress, trackType);

        if (ToTrackKind(trackType) == PracticeTrackKind.Meditation)
        {
            int intelligence = baseAttrs.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence));
            int willpower = baseAttrs.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower));
            return skillLevel + (intelligence + willpower) / 4 + professionBonus + knowledgeBonus;
        }
        if (ToTrackKind(trackType) == PracticeTrackKind.Cultivation)
        {
            int strength = baseAttrs.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength));
            int willpower = baseAttrs.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower));
            return skillLevel + (strength + willpower) / 4 + professionBonus + knowledgeBonus;
        }
        return 0;
    }

    private int CalculateDailyRecovery(
        UnitProgress unitProgress,
        StringName skillId,
        StringName trackType
    )
    {
        UnitSkillProgress skillProgress = unitProgress?.GetSkillProgress(skillId);
        if (skillProgress == null)
            return 0;

        UnitBaseAttributes baseAttrs = unitProgress.unit_base_attributes;
        if (baseAttrs == null)
            return 0;

        int skillLevel = skillProgress.skill_level;
        int willpower = baseAttrs.GetAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower));
        int professionBonus = GetProfessionWhitelistBonus(unitProgress, trackType);
        int knowledgeBonus = GetKnowledgeWhitelistBonus(unitProgress, trackType);

        return Mathf.Max(
            skillLevel / 2 + willpower / 5 + professionBonus / 2 + knowledgeBonus / 2,
            1
        );
    }

    private static int GetProfessionWhitelistBonus(UnitProgress unitProgress, StringName trackType)
    {
        return 0;
    }

    private static int GetKnowledgeWhitelistBonus(UnitProgress unitProgress, StringName trackType)
    {
        return 0;
    }
}
