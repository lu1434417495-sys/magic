using Godot;
using Godot.Collections;

[GlobalClass]
public partial class SkillPassiveResolver : RefCounted
{
    private static readonly StringName VajraBodySkillId = "vajra_body";
    private static readonly StringName StatusVajraBody = "vajra_body";
    private const int VajraBodyNonCoreMaxLevel = 9;
    private static readonly StringName LastStandSkillId = "warrior_last_stand";
    private static readonly StringName StatusDeathWard = "death_ward";
    private const int LastStandNonCoreMaxLevel = 5;
    private static readonly StringName ShootingSpecializationSkillId = "archer_shooting_specialization";
    private static readonly StringName StatusShootingSpecialization = "archer_shooting_specialization";

    public static void ApplyToUnit(BattleUnitState unitState, PassiveSourceContext context, Dictionary skillDefs = null)
    {
        if (unitState == null)
            return;
        var progressionState = context?.unit_progress;
        SyncVajraBodyStatus(unitState, progressionState, skillDefs);
        SyncLastStandStatus(unitState, progressionState, skillDefs);
        SyncShootingSpecializationStatus(unitState, progressionState, skillDefs);
    }

    private static void SyncVajraBodyStatus(BattleUnitState unitState, GodotObject progressionState, Dictionary skillDefs)
    {
        var skillProgress = progressionState != null ? progressionState.Call("get_skill_progress", VajraBodySkillId) : default;
        if (skillProgress.VariantType == Variant.Type.Nil || !skillProgress.AsGodotObject().Get("is_learned").AsBool())
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
        var parameters = new Dictionary
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

    private static int ResolveVajraBodyEffectiveLevel(Variant skillProgress, GodotObject progressionState, Dictionary skillDefs)
    {
        var rawLevel = Mathf.Max(skillProgress.AsGodotObject().Get("skill_level").AsInt32(), 0);
        var skillDef = skillDefs != null && skillDefs.ContainsKey(VajraBodySkillId) ? skillDefs[VajraBodySkillId].AsGodotObject() as SkillDef : null;
        if (skillDef != null)
        {
            var effectiveMax = SkillEffectiveMaxLevelRules.get_effective_max_level(skillDef, skillProgress, progressionState);
            return Mathf.Clamp(rawLevel, 0, effectiveMax);
        }
        var fallbackMax = skillProgress.AsGodotObject().Get("is_level_trigger_locked").AsBool() ? 10 : VajraBodyNonCoreMaxLevel;
        return Mathf.Clamp(rawLevel, 0, fallbackMax);
    }

    private static void SyncShootingSpecializationStatus(BattleUnitState unitState, GodotObject progressionState, Dictionary skillDefs)
    {
        var skillProgress = progressionState != null ? progressionState.Call("get_skill_progress", ShootingSpecializationSkillId) : default;
        if (skillProgress.VariantType == Variant.Type.Nil || !skillProgress.AsGodotObject().Get("is_learned").AsBool() || !IsSkillPassiveActive(progressionState, skillProgress))
        {
            unitState.EraseStatusEffect(StatusShootingSpecialization);
            return;
        }
        var skillLevel = Mathf.Max(skillProgress.AsGodotObject().Get("skill_level").AsInt32(), 0);
        var statusId = StatusShootingSpecialization;
        var statusPower = 1;
        var statusParams = new Dictionary
        {
            ["source_skill_id"] = ShootingSpecializationSkillId.ToString(),
            ["skill_level"] = skillLevel,
            ["range_bonus"] = 1,
        };
        var skillDef = skillDefs != null && skillDefs.ContainsKey(ShootingSpecializationSkillId) ? skillDefs[ShootingSpecializationSkillId].AsGodotObject() as SkillDef : null;
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
                    if (effectDef.effect_type != "status" && effectDef.effect_type != "apply_status")
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

    private static bool IsSkillPassiveActive(GodotObject progressionState, Variant skillProgress)
    {
        if (skillProgress.VariantType == Variant.Type.Nil)
            return false;
        var skillProgressObj = skillProgress.AsGodotObject();
        if (skillProgressObj.Get("profession_granted_by").AsStringName() == "")
            return true;
        if (progressionState == null)
            return false;
        var professionProgress = progressionState.Call("get_profession_progress", skillProgressObj.Get("profession_granted_by").AsStringName()).AsGodotObject();
        if (professionProgress == null)
            return false;
        return professionProgress.Get("is_active").AsBool() && !professionProgress.Get("is_hidden").AsBool() && professionProgress.Get("rank").AsInt32() > 0;
    }

    private static void SyncLastStandStatus(BattleUnitState unitState, GodotObject progressionState, Dictionary skillDefs)
    {
        var skillProgress = progressionState != null ? progressionState.Call("get_skill_progress", LastStandSkillId) : default;
        if (skillProgress.VariantType == Variant.Type.Nil || !skillProgress.AsGodotObject().Get("is_learned").AsBool())
        {
            unitState.EraseStatusEffect(StatusDeathWard);
            return;
        }
        var maxStatusLevel = skillProgress.AsGodotObject().Get("is_core").AsBool() ? 7 : LastStandNonCoreMaxLevel;
        var skillLevel = Mathf.Clamp(skillProgress.AsGodotObject().Get("skill_level").AsInt32(), 0, maxStatusLevel);
        var skillDef = skillDefs != null && skillDefs.ContainsKey(LastStandSkillId) ? skillDefs[LastStandSkillId].AsGodotObject() as SkillDef : null;
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
                    if (effectDef.effect_type == "status" || effectDef.effect_type == "apply_status")
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
                        var configuredParams = new Dictionary();
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

        var parameters = new Dictionary
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
}
