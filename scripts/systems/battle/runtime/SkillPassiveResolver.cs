using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SkillPassiveResolver : RefCounted
{
    private static readonly StringName VajraBodySkillId = "vajra_body";

    private static readonly StringName StatusVajraBody = "vajra_body";

    private const int VajraBodyNonCoreMaxLevel = 9;

    private static readonly StringName LastStandSkillId = "warrior_last_stand";

    private static readonly StringName StatusDeathWard = "death_ward";

    private const int LastStandNonCoreMaxLevel = 5;

    private static readonly StringName ShootingSpecializationSkillId =
        "archer_shooting_specialization";

    private static readonly StringName StatusShootingSpecialization =
        "archer_shooting_specialization";

    public static void ApplyToUnit(
        BattleUnitState unitState,
        PassiveSourceContext context,
        GDictionary skillDefs = null
    )
    {
        if (unitState == null)
            return;

        var progressionState = context?.unit_progress;

        SyncVajraBodyStatus(unitState, progressionState, skillDefs);

        SyncLastStandStatus(unitState, progressionState, skillDefs);

        SyncShootingSpecializationStatus(unitState, progressionState, skillDefs);
    }

    private static UnitSkillProgress GetSkillProgress(
        UnitProgress progressionState,
        StringName skillId
    )
    {
        return progressionState?.get_skill_progress(skillId);
    }

    private static void SyncVajraBodyStatus(
        BattleUnitState unitState,
        UnitProgress progressionState,
        GDictionary skillDefs
    )
    {
        var skillProgress = GetSkillProgress(progressionState, VajraBodySkillId);

        if (skillProgress == null || !skillProgress.is_learned)
        {
            unitState.EraseStatusEffect(StatusVajraBody);

            return;
        }

        var skillLevel = ResolveVajraBodyEffectiveLevel(skillProgress, progressionState, skillDefs);

        var passiveReduction = Mathf.FloorToInt((float)(skillLevel + 1) / 2.0f) + 1;

        var controlSaveBonus = 0;

        if (skillLevel >= 9)
            controlSaveBonus = 2;
        else if (skillLevel >= 7)
            controlSaveBonus = 1;

        var parameters = new GDictionary
        {
            ["source_skill_id"] = VajraBodySkillId.ToString(),

            ["skill_level"] = skillLevel,

            ["passive_reduction"] = passiveReduction,
        };

        if (controlSaveBonus > 0)
            parameters["control_save_bonus"] = controlSaveBonus;

        if (skillLevel >= 10)
            parameters["forced_move_immune"] = true;

        var statusEntry = new BattleStatusEffectState
        {
            status_id = StatusVajraBody,

            source_unit_id = unitState.unit_id,

            power = passiveReduction,

            stacks = 1,

            duration = -1,

            @params = parameters,
        };

        unitState.SetStatusEffect(statusEntry);
    }

    private static int ResolveVajraBodyEffectiveLevel(
        UnitSkillProgress skillProgress,
        UnitProgress progressionState,
        GDictionary skillDefs
    )
    {
        var rawLevel = Mathf.Max(skillProgress.skill_level, 0);

        var skillDef = GetSkillDef(skillDefs, VajraBodySkillId);

        if (skillDef != null)
        {
            var effectiveMax = SkillEffectiveMaxLevelRules.get_effective_max_level(
                skillDef,
                skillProgress,
                progressionState
            );

            return Mathf.Clamp(rawLevel, 0, effectiveMax);
        }

        var fallbackMax = skillProgress.is_level_trigger_locked ? 10 : VajraBodyNonCoreMaxLevel;

        return Mathf.Clamp(rawLevel, 0, fallbackMax);
    }

    private static void SyncShootingSpecializationStatus(
        BattleUnitState unitState,
        UnitProgress progressionState,
        GDictionary skillDefs
    )
    {
        var skillProgress = GetSkillProgress(progressionState, ShootingSpecializationSkillId);

        if (
            skillProgress == null
            || !skillProgress.is_learned
            || !IsSkillPassiveActive(progressionState, skillProgress)
        )
        {
            unitState.EraseStatusEffect(StatusShootingSpecialization);

            return;
        }

        var skillLevel = Mathf.Max(skillProgress.skill_level, 0);

        var statusId = StatusShootingSpecialization;

        var statusPower = 1;

        var statusParams = new GDictionary
        {
            ["source_skill_id"] = ShootingSpecializationSkillId.ToString(),

            ["skill_level"] = skillLevel,

            ["range_bonus"] = 1,
        };

        var skillDef = GetSkillDef(skillDefs, ShootingSpecializationSkillId);

        if (skillDef != null && skillDef.combat_profile != null)
        {
            var combatProfile = skillDef.combat_profile as CombatSkillDef;

            if (combatProfile != null)
            {
                foreach (var effectDef in combatProfile.passive_effect_defs)
                {
                    if (effectDef == null)
                        continue;

                    if (effectDef.trigger_condition != "battle_start")
                        continue;

                    if (
                        effectDef.effect_type != "status"
                        && effectDef.effect_type != "apply_status"
                    )
                        continue;

                    if (effectDef.status_id == "")
                        continue;

                    statusId = effectDef.status_id;

                    statusPower = effectDef.power;

                    if (effectDef.@params != null)
                        statusParams = effectDef.@params.Duplicate(true);

                    break;
                }
            }
        }

        statusParams["source_skill_id"] = ShootingSpecializationSkillId.ToString();

        statusParams["skill_level"] = skillLevel;

        if (!statusParams.ContainsKey("range_bonus"))
            statusParams["range_bonus"] = 1;

        var statusEntry = new BattleStatusEffectState
        {
            status_id = statusId,

            source_unit_id = unitState.unit_id,

            power = statusPower,

            stacks = 1,

            duration = -1,

            @params = statusParams,
        };

        unitState.SetStatusEffect(statusEntry);
    }

    private static bool IsSkillPassiveActive(
        UnitProgress progressionState,
        UnitSkillProgress skillProgress
    )
    {
        if (skillProgress == null)
            return false;

        if (skillProgress.profession_granted_by == "")
            return true;

        if (progressionState == null)
            return false;

        var professionProgress = progressionState.get_profession_progress(
            skillProgress.profession_granted_by
        );

        if (professionProgress == null)
            return false;

        return professionProgress.is_active
            && !professionProgress.is_hidden
            && professionProgress.rank > 0;
    }

    private static void SyncLastStandStatus(
        BattleUnitState unitState,
        UnitProgress progressionState,
        GDictionary skillDefs
    )
    {
        if (unitState.death_ward_consumed_this_battle)
            return;

        var skillProgress = GetSkillProgress(progressionState, LastStandSkillId);

        if (skillProgress == null || !skillProgress.is_learned)
        {
            unitState.EraseStatusEffect(StatusDeathWard);

            return;
        }

        var maxStatusLevel = skillProgress.is_core ? 7 : LastStandNonCoreMaxLevel;

        var skillLevel = Mathf.Clamp(
            skillProgress.skill_level,
            0,
            maxStatusLevel
        );

        var skillDef = GetSkillDef(skillDefs, LastStandSkillId);

        if (skillDef != null && skillDef.combat_profile != null)
        {
            var combatProfile = skillDef.combat_profile as CombatSkillDef;

            if (combatProfile != null)
            {
                foreach (var effectDef in combatProfile.passive_effect_defs)
                {
                    if (effectDef == null)
                        continue;

                    if (effectDef.trigger_condition != "battle_start")
                        continue;

                    var minLevel = Mathf.Max(effectDef.min_skill_level, 0);

                    var maxLevel = effectDef.max_skill_level;

                    if (skillLevel < minLevel)
                        continue;

                    if (maxLevel >= 0 && skillLevel > maxLevel)
                        continue;

                    if (
                        effectDef.effect_type == "status"
                        || effectDef.effect_type == "apply_status"
                    )
                    {
                        if (effectDef.status_id == "")
                            continue;

                        var configuredStatus = new BattleStatusEffectState
                        {
                            status_id = effectDef.status_id,

                            source_unit_id = unitState.unit_id,

                            power = effectDef.power,

                            stacks = 1,

                            duration = -1,
                        };

                        var configuredParams = new GDictionary();

                        if (effectDef.@params != null)
                            configuredParams = effectDef.@params.Duplicate(true);

                        configuredParams["source_skill_id"] = LastStandSkillId.ToString();

                        configuredParams["skill_level"] = skillLevel;

                        configuredStatus.@params = configuredParams;

                        unitState.SetStatusEffect(configuredStatus);

                        return;
                    }
                }
            }
        }

        var parameters = new GDictionary
        {
            ["source_skill_id"] = LastStandSkillId.ToString(),

            ["skill_level"] = skillLevel,
        };

        var statusEntry = new BattleStatusEffectState
        {
            status_id = StatusDeathWard,

            source_unit_id = unitState.unit_id,

            power = skillLevel,

            stacks = 1,

            duration = -1,

            @params = parameters,
        };

        unitState.SetStatusEffect(statusEntry);
    }

    private static SkillDef GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
            return null;

        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }
}
