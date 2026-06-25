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
        if (
            skillCatalog != null
            && skill_def != null
            && skill_def.skill_id != ""
            && skillCatalog.TryGetSkillDefinition(
                skill_def.skill_id,
                out SkillDefinition catalogSkillDefinition
            )
        )
        {
            return ClassifySkill(catalogSkillDefinition, skill_level, skillCatalog);
        }
        return ClassifySkill(SkillDefinition.FromResource(skill_def), skill_level, skillCatalog);
    }

    internal BattleAiSkillAffordanceRecord ClassifySkill(
        SkillDefinition skillDefinition,
        int skill_level = 1,
        ISkillCatalog skillCatalog = null
    )
    {
        BattleAiSkillAffordanceRecord record =
            BattleAiSkillAffordanceRecord.Empty(
                skillDefinition != null ? skillDefinition.SkillId : ""
            );
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            skillDefinition == null
            || combatProfile == null
            || skillDefinition.SkillTypeKind != SkillTypeKind.Active
        )
        {
            record.skip_reason = "passive_or_no_combat";
            return record;
        }

        record.target_mode = combatProfile.TargetModeKind;
        record.target_filter = combatProfile.TargetFilterKind;
        record.selection_mode = combatProfile.TargetSelectionModeKind;
        record.team_intent = ResolveTeamIntent(skillDefinition, combatProfile, skill_level);

        ClassifyOptions(record, combatProfile, combatProfile.GetUnlockedCastVariants(skill_level));
        ClassifySelectionMode(record, combatProfile);
        ClassifyEffectsAndTargetMode(record, skillDefinition, combatProfile, skill_level);

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
        CombatSkillDefinition combatProfile,
        IReadOnlyList<CombatCastVariantDefinition> unlockedCastVariants
    )
    {
        if (Normalize(combatProfile.SpecialResolutionProfileId) == MeteorSwarmProfileId)
        {
            record.AddAffordance(new StringName("special_ground"));
            record.AddAffordance(new StringName("ground_hostile.aoe"));
            record.AddActionFamily(new StringName("use_ground_skill"));
        }

        foreach (CombatCastVariantDefinition option in unlockedCastVariants)
        {
            if (option == null)
            {
                continue;
            }
            if (option.VariantId != "")
            {
                record.AddVariantId(option.VariantId);
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
        CombatSkillDefinition combatProfile
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
        SkillDefinition skillDef,
        CombatSkillDefinition combatProfile,
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

        foreach (CombatEffectDefinition effectDef in CollectEffectDefs(combatProfile, skillLevel))
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
            if (IsExecuteEffect(effectDef))
            {
                record.AddEffectRole(new StringName("execute"));
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
        SkillDefinition skillDef,
        CombatSkillDefinition combatProfile,
        int skillLevel = -1
    )
    {
        if (skillDef == null || combatProfile == null)
        {
            return "";
        }
        StringName filter = Normalize(combatProfile.TargetTeamFilter);
        if (BattleTargetTeamRules.IsBeneficialFilter(filter))
        {
            return "support";
        }
        if (BattleTargetTeamRules.IsEnemyFilter(filter))
        {
            return "hostile";
        }
        foreach (CombatEffectDefinition effectDef in CollectEffectDefs(combatProfile, skillLevel))
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName effectFilter = Normalize(effectDef.EffectTargetTeamFilter);
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

    private static List<CombatEffectDefinition> CollectEffectDefs(
        CombatSkillDefinition combatProfile,
        int skillLevel = -1
    )
    {
        var results = new List<CombatEffectDefinition>();
        if (combatProfile == null)
        {
            return results;
        }
        foreach (CombatEffectDefinition effectDef in combatProfile.EffectDefinitions)
        {
            if (effectDef != null && IsEffectUnlockedForLevel(effectDef, skillLevel))
            {
                results.Add(effectDef);
            }
        }
        foreach (CombatCastVariantDefinition option in combatProfile.CastVariants)
        {
            if (option == null)
            {
                continue;
            }
            if (skillLevel >= 0 && option.MinSkillLevel > skillLevel)
            {
                continue;
            }
            foreach (CombatEffectDefinition effectDef in option.EffectDefinitions)
            {
                if (effectDef != null && IsEffectUnlockedForLevel(effectDef, skillLevel))
                {
                    results.Add(effectDef);
                }
            }
        }
        return results;
    }

    private static bool IsEffectUnlockedForLevel(
        CombatEffectDefinition effectDef,
        int skillLevel
    )
    {
        if (effectDef == null)
        {
            return false;
        }
        if (skillLevel < 0)
        {
            return true;
        }
        int minLevel = Mathf.Max(effectDef.MinSkillLevel, 0);
        int maxLevel = effectDef.MaxSkillLevel;
        if (skillLevel < minLevel)
        {
            return false;
        }
        return maxLevel < 0 || skillLevel <= maxLevel;
    }

    private static bool IsDamageEffect(CombatEffectDefinition effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        return effectKind == BattleEffectKind.Damage
            || effectKind == BattleEffectKind.ChainDamage
            || effectKind == BattleEffectKind.PathStepAoe
            || effectKind == BattleEffectKind.Execute
            || effectKind == BattleEffectKind.GradedSaveExecute;
    }

    private static bool IsHealEffect(CombatEffectDefinition effectDef)
    {
        return effectDef != null && effectDef.EffectKind == BattleEffectKind.Heal;
    }

    private static bool IsControlEffect(CombatEffectDefinition effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (
            effectKind == BattleEffectKind.Status
            || effectKind == BattleEffectKind.ApplyStatus
            || effectKind == BattleEffectKind.GradedSaveExecute
            || effectKind == BattleEffectKind.ForcedMove
            || effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.HeightDelta
        )
        {
            return true;
        }
        return effectDef.StatusId != "" || effectDef.SaveFailureStatusId != "";
    }

    private static bool IsExecuteEffect(CombatEffectDefinition effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        return effectKind == BattleEffectKind.Execute
            || effectKind == BattleEffectKind.GradedSaveExecute;
    }

    private static bool IsGroundControlEffect(CombatEffectDefinition effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        return effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.HeightDelta
            || effectKind == BattleEffectKind.PathStepAoe
            || effectDef.TerrainEffectId != ""
            || effectDef.HeightDelta != 0;
    }

    private static bool OptionHasEffect(
        CombatCastVariantDefinition castVariant,
        BattleEffectKind effectKind
    )
    {
        if (castVariant == null)
        {
            return false;
        }
        foreach (CombatEffectDefinition effectDef in castVariant.EffectDefinitions)
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
