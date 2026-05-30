using Godot;

[GlobalClass]
public partial class SkillEffectiveMaxLevelRules : RefCounted
{
    private const string PROFESSION_RANK_STAT_PREFIX = "profession_rank:";

    public static int get_effective_max_level(
        GodotObject skillDef,
        UnitSkillProgress skillProgress,
        GodotObject unitProgress
    )
    {
        if (skillDef == null)
            return 0;

        int absoluteMax = get_effective_absolute_max_level(skillDef, unitProgress);

        int configuredNonCoreMax = skillDef.Get("non_core_max_level").AsInt32();

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

    public static int get_effective_absolute_max_level(
        GodotObject skillDef,
        GodotObject unitProgress
    )
    {
        if (skillDef == null)
            return 0;

        if (_uses_dynamic_max_level(skillDef))
        {
            int statValue = _get_dynamic_max_level_stat_value(
                skillDef.Get("dynamic_max_level_stat_id").AsStringName(),
                unitProgress
            );

            int baseLevel = Mathf.Max(skillDef.Get("dynamic_max_level_base").AsInt32(), 0);

            int levelPerStat = skillDef.Get("dynamic_max_level_per_stat").AsInt32();

            int dynamicLevel;

            if (levelPerStat > 0)
                dynamicLevel = baseLevel + statValue * levelPerStat;
            else if (levelPerStat < 0)
                dynamicLevel = Mathf.Max(baseLevel, statValue / Mathf.Abs(levelPerStat));
            else
                dynamicLevel = baseLevel;

            if (skillDef.Get("max_level").AsInt32() >= 0)
                return Mathf.Max(dynamicLevel, skillDef.Get("max_level").AsInt32());

            return dynamicLevel;
        }

        if (skillDef.Get("max_level").AsInt32() < 0)
            return 0;

        return Mathf.Max(skillDef.Get("max_level").AsInt32(), 0);
    }

    public static bool is_at_effective_max_level(
        GodotObject skillDef,
        UnitSkillProgress skillProgress,
        GodotObject unitProgress
    )
    {
        if (skillDef == null || skillProgress == null)
            return false;

        return skillProgress.skill_level
            >= get_effective_max_level(skillDef, skillProgress, unitProgress);
    }

    private static bool _uses_dynamic_max_level(GodotObject skillDef)
    {
        return skillDef != null && skillDef.Get("dynamic_max_level_stat_id").AsStringName() != "";
    }

    private static int _get_dynamic_max_level_stat_value(
        StringName statId,
        GodotObject unitProgress
    )
    {
        if (unitProgress == null)
            return 0;

        if (statId == "character_level")
            return Mathf.Max(unitProgress.Get("character_level").AsInt32(), 0);

        string statKey = (string)statId;

        if (statKey.StartsWith(PROFESSION_RANK_STAT_PREFIX))
        {
            var professionId = new StringName(
                statKey.Substring(PROFESSION_RANK_STAT_PREFIX.Length)
            );

            if (professionId == "")
                return 0;

            var professionProgress = (unitProgress as UnitProgress)?.get_profession_progress(
                professionId
            );

            if (professionProgress == null)
                return 0;

            return Mathf.Max(professionProgress.rank, 0);
        }

        var unitBaseAttributes = (unitProgress as UnitProgress)?.unit_base_attributes;

        if (unitBaseAttributes == null)
            return 0;

        return Mathf.Max(unitBaseAttributes.get_attribute_value(statId), 0);
    }
}
