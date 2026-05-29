using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class PracticeGrowthService : RefCounted
{
    public static readonly StringName TRACK_MEDITATION = "meditation";
    public static readonly StringName TRACK_CULTIVATION = "cultivation";
    private static readonly GStringNameArray PracticeTracks = new()
    {
        TRACK_MEDITATION,
        TRACK_CULTIVATION,
    };

    private const int TierBasic = 0;
    private const int TierIntermediate = 1;
    private const int TierAdvanced = 2;
    private const int TierUltimate = 3;

    private static readonly GDictionary TierNameToValue = new()
    {
        ["basic"] = TierBasic,
        ["intermediate"] = TierIntermediate,
        ["advanced"] = TierAdvanced,
        ["ultimate"] = TierUltimate,
    };
    private static readonly GDictionary TierValueToName = new()
    {
        [TierBasic] = "basic",
        [TierIntermediate] = "intermediate",
        [TierAdvanced] = "advanced",
        [TierUltimate] = "ultimate",
    };

    private static readonly StringName MpMaxAttr = "mp_max";
    private static readonly StringName AuraMaxAttr = "aura_max";

    private GDictionary _skill_defs = new();
    private GDictionary _profession_defs = new();

    public void setup(GDictionary skillDefs, GDictionary professionDefs)
    {
        _skill_defs = skillDefs ?? new GDictionary();
        _profession_defs = professionDefs ?? new GDictionary();
    }

    public StringName get_track_type_for_skill(StringName skillId)
    {
        SkillDef skillDef = GetSkillDef(skillId);
        return skillDef != null ? GetExclusivePracticeTrack(skillDef) : "";
    }

    public int get_practice_tier(StringName skillId)
    {
        SkillDef skillDef = GetSkillDef(skillId);
        if (skillDef == null)
            return -1;
        return TierNameToValue.ContainsKey(skillDef.practice_tier)
            ? TierNameToValue[skillDef.practice_tier].AsInt32()
            : -1;
    }

    public static int resolve_tier_value(StringName tierName)
    {
        return TierNameToValue.ContainsKey(tierName) ? TierNameToValue[tierName].AsInt32() : -1;
    }

    public static StringName resolve_tier_name(int tierValue)
    {
        return TierValueToName.ContainsKey(tierValue)
            ? TierValueToName[tierValue].AsStringName()
            : "";
    }

    public StringName get_active_practice_skill(UnitProgress unitProgress, StringName trackType)
    {
        GStringNameArray activeSkillIds = GetActivePracticeSkillIds(unitProgress, trackType);
        return activeSkillIds.Count == 1 ? activeSkillIds[0] : "";
    }

    public GDictionary can_learn_practice_skill(StringName skillId, UnitProgress unitProgress)
    {
        StringName trackType = get_track_type_for_skill(skillId);
        if (trackType == "")
            return PracticeLearnResult(false, false, "");
        if (!HasValidPracticeTier(skillId))
            return PracticeLearnResult(false, false, "");

        GStringNameArray existingSkillIds = GetActivePracticeSkillIds(unitProgress, trackType);
        if (existingSkillIds.Count > 1)
        {
            GDictionary result = PracticeLearnResult(false, false, "");
            result["error_code"] = "ambiguous_existing_practice_track";
            return result;
        }

        StringName existingSkillId = existingSkillIds.Count == 1 ? existingSkillIds[0] : "";
        if (existingSkillId == "")
            return PracticeLearnResult(true, false, "");
        if (existingSkillId == skillId)
            return PracticeLearnResult(false, false, existingSkillId);
        return PracticeLearnResult(false, true, existingSkillId);
    }

    public int calculate_replacement_level(
        StringName oldSkillId,
        StringName newSkillId,
        UnitProgress unitProgress
    )
    {
        int oldTier = get_practice_tier(oldSkillId);
        int newTier = get_practice_tier(newSkillId);
        if (oldTier < 0 || newTier < 0)
            return -1;

        UnitSkillProgress oldSkillProgress = unitProgress?.get_skill_progress(oldSkillId);
        int oldLevel = oldSkillProgress?.skill_level ?? 0;
        SkillDef newSkillDef = GetSkillDef(newSkillId);
        if (newSkillDef == null)
            return 0;

        int rawNewLevel = oldLevel + (oldTier - newTier);
        int maxLevel = newSkillDef.max_level >= 0 ? Mathf.Max(newSkillDef.max_level, 0) : 999;
        if (newSkillDef.dynamic_max_level_stat_id != "")
        {
            int absoluteMax = SkillEffectiveMaxLevelRules.get_effective_absolute_max_level(
                newSkillDef,
                unitProgress
            );
            if (absoluteMax > 0)
                maxLevel = Mathf.Min(maxLevel, absoluteMax);
        }
        return Mathf.Clamp(rawNewLevel, 0, maxLevel);
    }

    public bool apply_replacement(StringName newSkillId, UnitProgress unitProgress)
    {
        return apply_replacement(newSkillId, unitProgress, false);
    }

    public bool apply_replacement(
        StringName newSkillId,
        UnitProgress unitProgress,
        bool formalLearningVerified
    )
    {
        if (!formalLearningVerified)
            return false;

        StringName trackType = get_track_type_for_skill(newSkillId);
        if (trackType == "")
            return false;

        GDictionary learnResult = can_learn_practice_skill(newSkillId, unitProgress);
        if (!GdInterop.GetBool(learnResult, "needs_replacement"))
            return false;

        StringName oldSkillId = GdInterop.GetStringName(learnResult, "existing_skill_id");
        if (oldSkillId == "")
            return false;

        int predictedLevel = calculate_replacement_level(oldSkillId, newSkillId, unitProgress);
        if (predictedLevel < 0)
            return false;

        ClearReplacedSkillReferences(unitProgress, oldSkillId);
        unitProgress.remove_skill_progress(oldSkillId);

        UnitSkillProgress newSkillProgress = new()
        {
            skill_id = newSkillId,
            is_learned = true,
            skill_level = predictedLevel,
        };
        unitProgress.set_skill_progress(newSkillProgress);
        return true;
    }

    public GDictionary get_skill_learned_status(StringName skillId, UnitProgress unitProgress)
    {
        StringName trackType = get_track_type_for_skill(skillId);
        if (trackType == "")
        {
            return new GDictionary
            {
                ["is_practice_skill"] = false,
                ["track_type"] = "",
                ["is_learned_direct"] = false,
                ["needs_replacement"] = false,
                ["existing_skill_id"] = "",
                ["predicted_level"] = 0,
            };
        }

        GDictionary result = can_learn_practice_skill(skillId, unitProgress);
        result["is_practice_skill"] = true;
        result["track_type"] = trackType;
        if (GdInterop.GetBool(result, "needs_replacement"))
        {
            result["predicted_level"] = calculate_replacement_level(
                GdInterop.GetStringName(result, "existing_skill_id"),
                skillId,
                unitProgress
            );
        }
        return result;
    }

    public void inject_first_unlock_starting_values(
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

        StringName existingSkillId = get_active_practice_skill(unitProgress, trackType);
        if (existingSkillId == "")
            return;
        if (GetSkillDef(existingSkillId) == null)
            return;

        int growth = CalculateDailyUpperLimitGrowth(unitProgress, existingSkillId, trackType);
        if (trackType == TRACK_MEDITATION)
        {
            baseAttrs.set_attribute_value(MpMaxAttr, growth);
            memberState.current_mp = growth;
        }
        else if (trackType == TRACK_CULTIVATION)
        {
            baseAttrs.set_attribute_value(AuraMaxAttr, growth);
            memberState.current_aura = growth;
        }
    }

    public void apply_daily_growth_to_member(PartyMemberState memberState, int daysElapsed)
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
            StringName skillId = get_active_practice_skill(unitProgress, trackType);
            if (skillId == "")
                continue;

            int singleDayGrowth = CalculateDailyUpperLimitGrowth(unitProgress, skillId, trackType);
            int singleDayRecovery = CalculateDailyRecovery(unitProgress, skillId, trackType);

            if (trackType == TRACK_MEDITATION)
            {
                int currentMax = baseAttrs.get_attribute_value(MpMaxAttr);
                baseAttrs.set_attribute_value(
                    MpMaxAttr,
                    currentMax + singleDayGrowth * daysElapsed
                );
                memberState.current_mp = Mathf.Min(
                    memberState.current_mp + singleDayRecovery * daysElapsed,
                    baseAttrs.get_attribute_value(MpMaxAttr)
                );
            }
            else if (trackType == TRACK_CULTIVATION)
            {
                int currentMax = baseAttrs.get_attribute_value(AuraMaxAttr);
                baseAttrs.set_attribute_value(
                    AuraMaxAttr,
                    currentMax + singleDayGrowth * daysElapsed
                );
                memberState.current_aura = Mathf.Min(
                    memberState.current_aura + singleDayRecovery * daysElapsed,
                    baseAttrs.get_attribute_value(AuraMaxAttr)
                );
            }
        }
    }

    public static string get_track_display_name(StringName trackType)
    {
        return trackType == TRACK_MEDITATION ? "冥想" : "修炼";
    }

    public static string get_tier_display_name(int tierValue)
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

    private SkillDef GetSkillDef(StringName skillId)
    {
        return _skill_defs.ContainsKey(skillId)
            ? _skill_defs[skillId].AsGodotObject() as SkillDef
            : null;
    }

    private static StringName GetExclusivePracticeTrack(SkillDef skillDef)
    {
        if (skillDef == null)
            return "";

        StringName matchedTrack = "";
        int matchedCount = 0;
        foreach (StringName trackType in PracticeTracks)
        {
            if (skillDef.tags.Contains(trackType))
            {
                matchedTrack = trackType;
                matchedCount += 1;
            }
        }
        if (matchedCount != 1 || skillDef.tags.Count != 1)
            return "";
        return matchedTrack;
    }

    private bool HasValidPracticeTier(StringName skillId)
    {
        SkillDef skillDef = GetSkillDef(skillId);
        return skillDef != null && TierNameToValue.ContainsKey(skillDef.practice_tier);
    }

    private GStringNameArray GetActivePracticeSkillIds(
        UnitProgress unitProgress,
        StringName trackType
    )
    {
        GStringNameArray activeSkillIds = new();
        if (unitProgress == null || !PracticeTracks.Contains(trackType))
            return activeSkillIds;

        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(unitProgress.skills))
        {
            StringName skillId = new(skillKey);
            UnitSkillProgress skillProgress = unitProgress.get_skill_progress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            if (get_track_type_for_skill(skillId) == trackType)
                activeSkillIds.Add(skillId);
        }
        return activeSkillIds;
    }

    private static GDictionary PracticeLearnResult(
        bool canLearn,
        bool needsReplacement,
        StringName existingSkillId
    )
    {
        return new GDictionary
        {
            ["can_learn"] = canLearn,
            ["needs_replacement"] = needsReplacement,
            ["existing_skill_id"] = existingSkillId,
        };
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
        unitProgress.locked_level_trigger_skill_ids.Remove(oldSkillId);

        UnitSkillProgress oldSkillProgress = unitProgress.get_skill_progress(oldSkillId);
        if (oldSkillProgress != null)
        {
            oldSkillProgress.is_level_trigger_active = false;
            oldSkillProgress.is_level_trigger_locked = false;
            unitProgress.set_skill_progress(oldSkillProgress);
        }

        foreach (
            string professionKey in ProgressionDataUtils.sorted_string_keys(
                unitProgress.professions
            )
        )
        {
            StringName professionId = ProgressionDataUtils.to_string_name(professionKey);
            UnitProfessionProgress professionProgress = unitProgress.get_profession_progress(
                professionId
            );
            if (professionProgress == null)
                continue;
            if (professionProgress.core_skill_ids.Contains(oldSkillId))
            {
                professionProgress.remove_core_skill(oldSkillId);
                unitProgress.set_profession_progress(professionProgress);
            }
        }
    }

    private int CalculateDailyUpperLimitGrowth(
        UnitProgress unitProgress,
        StringName skillId,
        StringName trackType
    )
    {
        UnitSkillProgress skillProgress = unitProgress?.get_skill_progress(skillId);
        if (skillProgress == null)
            return 0;

        UnitBaseAttributes baseAttrs = unitProgress.unit_base_attributes;
        if (baseAttrs == null)
            return 0;

        int skillLevel = skillProgress.skill_level;
        int professionBonus = GetProfessionWhitelistBonus(unitProgress, trackType);
        int knowledgeBonus = GetKnowledgeWhitelistBonus(unitProgress, trackType);

        if (trackType == TRACK_MEDITATION)
        {
            int intelligence = baseAttrs.get_attribute_value(UnitBaseAttributes.INTELLIGENCE());
            int willpower = baseAttrs.get_attribute_value(UnitBaseAttributes.WILLPOWER());
            return skillLevel + (intelligence + willpower) / 4 + professionBonus + knowledgeBonus;
        }
        if (trackType == TRACK_CULTIVATION)
        {
            int strength = baseAttrs.get_attribute_value(UnitBaseAttributes.STRENGTH());
            int willpower = baseAttrs.get_attribute_value(UnitBaseAttributes.WILLPOWER());
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
        UnitSkillProgress skillProgress = unitProgress?.get_skill_progress(skillId);
        if (skillProgress == null)
            return 0;

        UnitBaseAttributes baseAttrs = unitProgress.unit_base_attributes;
        if (baseAttrs == null)
            return 0;

        int skillLevel = skillProgress.skill_level;
        int willpower = baseAttrs.get_attribute_value(UnitBaseAttributes.WILLPOWER());
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
