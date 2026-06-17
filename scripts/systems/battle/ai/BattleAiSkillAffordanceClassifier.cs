using System.Collections.Generic;
using Godot;

internal sealed class BattleAiSkillAffordanceClassifier
{
    private static readonly StringName MeteorSwarmProfileId = "meteor_swarm";

    internal BattleAiSkillAffordanceRecord ClassifySkill(
        SkillDef skill_def,
        int skill_level = 1,
        ISkillCatalog skillCatalog = null
    )
    {
        BattleAiSkillAffordanceRecord record =
            BattleAiSkillAffordanceRecord.Empty(skill_def != null ? skill_def.skill_id : "");
        CombatSkillDef combatProfile = skill_def?.combat_profile as CombatSkillDef;
        if (
            skill_def == null
            || combatProfile == null
            || skill_def.SkillTypeKind != SkillTypeKind.Active
        )
        {
            record.skip_reason = "passive_or_no_combat";
            return record;
        }

        record.target_mode = combatProfile.TargetModeKind;
        record.target_filter = combatProfile.TargetFilterKind;
        record.selection_mode = combatProfile.TargetSelectionModeKind;
        record.team_intent = ResolveTeamIntent(skill_def, combatProfile, skill_level);

        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(skillCatalog, skill_def, skill_level);

        ClassifyOptions(record, combatProfile, effectiveProfile);
        ClassifySelectionMode(record, combatProfile);
        ClassifyEffectsAndTargetMode(record, skill_def, combatProfile, skill_level);

        if (record.affordances.Count > 0 && record.action_families.Count > 0)
        {
            record.is_generatable = true;
            record.skip_reason = "";
        }
        else
        {
            record.is_generatable = false;
            record.skip_reason = "unsupported_or_special";
        }
        record.requires_positioning_action = RequiresPositioningAction(record);
        return record;
    }

    private static void ClassifyOptions(
        BattleAiSkillAffordanceRecord record,
        CombatSkillDef combatProfile,
        SkillEffectiveCombatProfile effectiveProfile
    )
    {
        if (Normalize(combatProfile.special_resolution_profile_id) == MeteorSwarmProfileId)
        {
            record.AddAffordance(new StringName("special_ground"));
            record.AddAffordance(new StringName("ground_hostile.aoe"));
            record.AddActionFamily(new StringName("use_ground_skill"));
        }

        foreach (CombatCastVariantDef option in effectiveProfile.UnlockedCastVariants)
        {
            if (option == null)
            {
                continue;
            }
            if (option.variant_id != "")
            {
                record.AddVariantId(option.variant_id);
            }
            bool hasCharge = OptionHasEffect(option, BattleEffectKind.Charge);
            bool hasPathAoe = OptionHasEffect(option, BattleEffectKind.PathStepAoe);
            if (hasCharge && hasPathAoe)
            {
                record.AddEffectRole(new StringName("charge"));
                record.AddEffectRole(BattleTypedNames.EffectPathStepAoe);
                record.AddAffordance(new StringName("charge_path_aoe"));
                record.AddActionFamily(new StringName("use_charge_path_aoe"));
            }
            else if (hasCharge)
            {
                record.AddEffectRole(new StringName("charge"));
                record.AddAffordance(new StringName("charge_engage"));
                record.AddActionFamily(new StringName("use_charge"));
            }
        }
    }

    private static void ClassifySelectionMode(
        BattleAiSkillAffordanceRecord record,
        CombatSkillDef combatProfile
    )
    {
        BattleTargetSelectionMode selectionMode = combatProfile.TargetSelectionModeKind;
        if (selectionMode == BattleTargetSelectionMode.RandomChain)
        {
            record.AddAffordance(new StringName("random_chain"));
            record.AddActionFamily(new StringName("use_random_chain_skill"));
            record.AddActionFamily(new StringName("move_to_range"));
        }
        else if (selectionMode == BattleTargetSelectionMode.MultiUnit)
        {
            record.AddAffordance(new StringName("multi_unit"));
            record.AddActionFamily(new StringName("use_multi_unit_skill"));
            record.AddActionFamily(new StringName("move_to_multi_unit_skill_position"));
        }
    }

    private static void ClassifyEffectsAndTargetMode(
        BattleAiSkillAffordanceRecord record,
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        int skillLevel
    )
    {
        BattleTargetMode targetMode = combatProfile.TargetModeKind;
        StringName teamIntent = ProgressionDataUtils.to_string_name(record.team_intent);
        bool hasDamage = false;
        bool hasHeal = false;
        bool hasControl = false;
        bool hasGroundControl = false;
        bool hasReposition = false;

        foreach (CombatEffectDef effectDef in CollectEffectDefs(combatProfile, skillLevel))
        {
            if (effectDef == null)
            {
                continue;
            }
            BattleEffectKind effectKind = effectDef.EffectKind;
            if (IsDamageEffect(effectDef))
            {
                hasDamage = true;
                record.AddEffectRole(new StringName("damage"));
            }
            if (IsHealEffect(effectDef))
            {
                hasHeal = true;
                record.AddEffectRole(new StringName("heal"));
            }
            if (IsControlEffect(effectDef))
            {
                hasControl = true;
                record.AddEffectRole(new StringName("control"));
            }
            if (IsGroundControlEffect(effectDef))
            {
                hasGroundControl = true;
                record.AddEffectRole(new StringName("ground_control"));
            }
            if (effectKind == BattleEffectKind.ForcedMove)
            {
                hasReposition = true;
                record.AddEffectRole(new StringName("forced_move"));
            }
        }

        if (targetMode == BattleTargetMode.Ground)
        {
            if (hasDamage && teamIntent != "support")
            {
                record.AddAffordance(new StringName("ground_hostile.aoe"));
            }
            if (hasGroundControl || hasControl)
            {
                record.AddAffordance(new StringName("ground_control"));
                record.AddAffordance(new StringName("terrain_control"));
            }
            if (!record.HasActionFamily("use_charge_path_aoe"))
            {
                record.AddActionFamily(new StringName("use_ground_skill"));
            }
            return;
        }

        if (targetMode == BattleTargetMode.Unit)
        {
            if (teamIntent == "support")
            {
                if (hasHeal)
                {
                    record.AddAffordance(new StringName("ally_heal"));
                }
                else if (hasControl || hasReposition)
                {
                    record.AddAffordance(new StringName("self_or_ally_buff"));
                }
            }
            else if (hasDamage)
            {
                record.AddAffordance(new StringName("unit_hostile.damage"));
            }
            else if (hasControl || hasReposition)
            {
                record.AddAffordance(new StringName("unit_hostile.control"));
                if (hasReposition)
                {
                    record.AddAffordance(new StringName("displacement_control"));
                }
            }
            if (
                !record.HasAnyActionFamily(
                    new[]
                    {
                        new StringName("use_charge"),
                        new StringName("use_charge_path_aoe"),
                        new StringName("use_random_chain_skill"),
                        new StringName("use_multi_unit_skill"),
                    }
                )
            )
            {
                record.AddActionFamily(new StringName("use_unit_skill"));
            }
        }
    }

    private static StringName ResolveTeamIntent(
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        int skillLevel = -1
    )
    {
        if (skillDef == null || combatProfile == null)
        {
            return "";
        }
        StringName filter = Normalize(combatProfile.target_team_filter);
        if (BattleTargetTeamRules.IsBeneficialFilter(filter))
        {
            return "support";
        }
        if (BattleTargetTeamRules.IsEnemyFilter(filter))
        {
            return "hostile";
        }
        foreach (CombatEffectDef effectDef in CollectEffectDefs(combatProfile, skillLevel))
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName effectFilter = Normalize(effectDef.effect_target_team_filter);
            if (BattleTargetTeamRules.IsEnemyFilter(effectFilter))
            {
                return "hostile";
            }
            if (BattleTargetTeamRules.IsBeneficialFilter(effectFilter))
            {
                return "support";
            }
        }
        return "neutral";
    }

    private static List<CombatEffectDef> CollectEffectDefs(
        CombatSkillDef combatProfile,
        int skillLevel = -1
    )
    {
        var results = new List<CombatEffectDef>();
        if (combatProfile == null)
        {
            return results;
        }
        foreach (CombatEffectDef effectDef in combatProfile.effect_defs)
        {
            if (effectDef != null && IsEffectUnlockedForLevel(effectDef, skillLevel))
            {
                results.Add(effectDef);
            }
        }
        foreach (CombatCastVariantDef option in combatProfile.cast_variants)
        {
            if (option == null)
            {
                continue;
            }
            if (skillLevel >= 0 && option.min_skill_level > skillLevel)
            {
                continue;
            }
            foreach (CombatEffectDef effectDef in option.effect_defs)
            {
                if (effectDef != null && IsEffectUnlockedForLevel(effectDef, skillLevel))
                {
                    results.Add(effectDef);
                }
            }
        }
        return results;
    }

    private static bool IsEffectUnlockedForLevel(CombatEffectDef effectDef, int skillLevel)
    {
        if (effectDef == null)
        {
            return false;
        }
        if (skillLevel < 0)
        {
            return true;
        }
        int minLevel = Mathf.Max(effectDef.min_skill_level, 0);
        int maxLevel = effectDef.max_skill_level;
        if (skillLevel < minLevel)
        {
            return false;
        }
        return maxLevel < 0 || skillLevel <= maxLevel;
    }

    private static bool IsDamageEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        return effectKind == BattleEffectKind.Damage
            || effectKind == BattleEffectKind.ChainDamage
            || effectKind == BattleEffectKind.PathStepAoe
            || effectKind == BattleEffectKind.Execute;
    }

    private static bool IsHealEffect(CombatEffectDef effectDef)
    {
        return effectDef != null && effectDef.EffectKind == BattleEffectKind.Heal;
    }

    private static bool IsControlEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (
            effectKind == BattleEffectKind.Status
            || effectKind == BattleEffectKind.ApplyStatus
            || effectKind == BattleEffectKind.ForcedMove
            || effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.HeightDelta
        )
        {
            return true;
        }
        return effectDef.status_id != "" || effectDef.save_failure_status_id != "";
    }

    private static bool IsGroundControlEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        return effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.HeightDelta
            || effectKind == BattleEffectKind.PathStepAoe
            || effectDef.terrain_effect_id != ""
            || effectDef.height_delta != 0;
    }

    private static bool OptionHasEffect(CombatCastVariantDef castVariant, BattleEffectKind effectKind)
    {
        if (castVariant == null)
        {
            return false;
        }
        foreach (CombatEffectDef effectDef in castVariant.effect_defs)
        {
            if (effectDef != null && effectDef.EffectKind == effectKind)
            {
                return true;
            }
        }
        return false;
    }

    private static bool RequiresPositioningAction(BattleAiSkillAffordanceRecord record)
    {
        return record.HasActionFamily("move_to_range")
            || record.HasActionFamily("move_to_multi_unit_skill_position");
    }

    private static StringName Normalize(StringName value)
    {
        return ProgressionDataUtils.to_string_name(value);
    }
}
