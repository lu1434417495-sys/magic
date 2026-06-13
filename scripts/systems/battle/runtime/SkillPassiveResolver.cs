using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static class SkillPassiveResolver
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = null
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
        return progressionState?.GetSkillProgress(skillId);
    }

    private static void SyncVajraBodyStatus(
        BattleUnitState unitState,
        UnitProgress progressionState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
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

        var statusEntry = BuildPassiveSkillStatus(
            unitState,
            StatusVajraBody,
            passiveReduction,
            -1,
            VajraBodySkillId,
            skillLevel
        );
        statusEntry.control_save_bonus = controlSaveBonus;
        statusEntry.passive_reduction = passiveReduction;
        statusEntry.forced_move_immune = skillLevel >= 10;

        unitState.SetStatusEffect(statusEntry);
    }

    private static int ResolveVajraBodyEffectiveLevel(
        UnitSkillProgress skillProgress,
        UnitProgress progressionState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        var rawLevel = Mathf.Max(skillProgress.skill_level, 0);

        var skillDef = GetSkillDef(skillDefs, VajraBodySkillId);

        if (skillDef != null)
        {
            var effectiveMax = SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel(
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
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
        var statusRangeBonus = 1;
        GDictionary statusParams = null;

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

                    if (
                        effectDef.TriggerConditionKind
                        != CombatEffectTriggerCondition.BattleStart
                    )
                        continue;

                    if (
                        effectDef.EffectKind != BattleEffectKind.Status
                        && effectDef.EffectKind != BattleEffectKind.ApplyStatus
                    )
                        continue;

                    if (effectDef.status_id == "")
                        continue;

                    statusId = effectDef.status_id;

                    statusPower = effectDef.power;
                    statusRangeBonus = effectDef.range_bonus > 0 ? effectDef.range_bonus : 1;
                    statusParams = effectDef.@params;

                    break;
                }
            }
        }

        var statusEntry = BuildPassiveSkillStatus(
            unitState,
            statusId,
            statusPower,
            -1,
            ShootingSpecializationSkillId,
            skillLevel,
            statusParams
        );
        statusEntry.range_bonus = statusRangeBonus;

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

        var professionProgress = progressionState.GetProfessionProgress(
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
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
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

                    if (
                        effectDef.TriggerConditionKind
                        != CombatEffectTriggerCondition.BattleStart
                    )
                        continue;

                    var minLevel = Mathf.Max(effectDef.min_skill_level, 0);

                    var maxLevel = effectDef.max_skill_level;

                    if (skillLevel < minLevel)
                        continue;

                    if (maxLevel >= 0 && skillLevel > maxLevel)
                        continue;

                    if (
                        effectDef.EffectKind == BattleEffectKind.Status
                        || effectDef.EffectKind == BattleEffectKind.ApplyStatus
                    )
                    {
                        if (effectDef.status_id == "")
                            continue;

                        var configuredStatus = BuildPassiveSkillStatus(
                            unitState,
                            effectDef.status_id,
                            effectDef.power,
                            -1,
                            LastStandSkillId,
                            skillLevel,
                            effectDef.@params
                        );

                        unitState.SetStatusEffect(configuredStatus);

                        return;
                    }
                }
            }
        }

        var statusEntry = BuildPassiveSkillStatus(
            unitState,
            StatusDeathWard,
            skillLevel,
            -1,
            LastStandSkillId,
            skillLevel
        );

        unitState.SetStatusEffect(statusEntry);
    }

    private static BattleStatusEffectState BuildPassiveSkillStatus(
        BattleUnitState unitState,
        StringName statusId,
        int power,
        int durationTu,
        StringName sourceSkillId,
        int sourceSkillLevel,
        GDictionary statusParams = null
    )
    {
        return new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = unitState?.unit_id ?? new StringName(""),
            power = power,
            stacks = 1,
            duration = durationTu,
            @params = BattleStatusEffectState.CopyResidualParams(statusParams),
            source_skill_id = sourceSkillId,
            source_skill_level = sourceSkillLevel,
        };
    }

    private static SkillDef GetSkillDef(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        StringName skillId
    )
    {
        if (skillDefs == null)
            return null;

        return skillDefs.TryGetValue(skillId, out var skillDef) ? skillDef : null;
    }
}
