using Godot;

public static class SkillEffectiveMaxLevelRules
{
    private const string PROFESSION_RANK_STAT_PREFIX = "profession_rank:";

    public static int GetEffectiveMaxLevel(
        SkillDef skillDef,
        UnitSkillProgress skillProgress,
        UnitProgress unitProgress
    )
    {
        if (skillDef == null)
            return 0;

        int absoluteMax = GetEffectiveAbsoluteMaxLevel(skillDef, unitProgress);

        int configuredNonCoreMax = skillDef.non_core_max_level;

        if (
            configuredNonCoreMax > 0
            && (
                skillProgress == null
                || !skillProgress.is_level_trigger_locked
            )
        )
            return Mathf.Min(absoluteMax, configuredNonCoreMax);

        return absoluteMax;
    }

    public static int GetEffectiveAbsoluteMaxLevel(
        SkillDef skillDef,
        UnitProgress unitProgress
    )
    {
        if (skillDef == null)
            return 0;

        if (_uses_dynamic_max_level(skillDef))
        {
            int statValue = _get_dynamic_max_level_stat_value(
                skillDef.dynamic_max_level_stat_id,
                unitProgress
            );

            int baseLevel = Mathf.Max(skillDef.dynamic_max_level_base, 0);

            int levelPerStat = skillDef.dynamic_max_level_per_stat;

            int dynamicLevel;

            if (levelPerStat > 0)
                dynamicLevel = baseLevel + statValue * levelPerStat;
            else if (levelPerStat < 0)
                dynamicLevel = Mathf.Max(baseLevel, statValue / Mathf.Abs(levelPerStat));
            else
                dynamicLevel = baseLevel;

            if (skillDef.max_level >= 0)
                return Mathf.Max(dynamicLevel, skillDef.max_level);

            return dynamicLevel;
        }

        if (skillDef.max_level < 0)
            return 0;

        return Mathf.Max(skillDef.max_level, 0);
    }

    public static bool IsAtEffectiveMaxLevel(
        SkillDef skillDef,
        UnitSkillProgress skillProgress,
        UnitProgress unitProgress
    )
    {
        if (skillDef == null || skillProgress == null)
            return false;

        return skillProgress.skill_level
            >= GetEffectiveMaxLevel(skillDef, skillProgress, unitProgress);
    }

    private static bool _uses_dynamic_max_level(SkillDef skillDef)
    {
        return skillDef != null && skillDef.dynamic_max_level_stat_id != "";
    }

    private static int _get_dynamic_max_level_stat_value(
        StringName statId,
        UnitProgress unitProgress
    )
    {
        if (unitProgress == null)
            return 0;

        if (statId == "character_level")
            return Mathf.Max(unitProgress.character_level, 0);

        string statKey = (string)statId;

        if (statKey.StartsWith(PROFESSION_RANK_STAT_PREFIX))
        {
            var professionId = new StringName(
                statKey.Substring(PROFESSION_RANK_STAT_PREFIX.Length)
            );

            if (professionId == "")
                return 0;

            var professionProgress = unitProgress.GetProfessionProgress(professionId);

            if (professionProgress == null)
                return 0;

            return Mathf.Max(professionProgress.rank, 0);
        }

        var unitBaseAttributes = unitProgress.unit_base_attributes;

        if (unitBaseAttributes == null)
            return 0;

        return Mathf.Max(unitBaseAttributes.GetAttributeValue(statId), 0);
    }
}
