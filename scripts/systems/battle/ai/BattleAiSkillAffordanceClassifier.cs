using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiSkillAffordanceClassifier : RefCounted
{
    private static readonly StringName PathStepAoeEffectType = "path_step_aoe";
    private static readonly StringName MeteorSwarmProfileId = "meteor_swarm";

    public GDictionary classify_skill(SkillDef skill_def, int skill_level = 1)
    {
        GDictionary record = EmptyRecord(skill_def);
        CombatSkillDef combatProfile = skill_def?.combat_profile as CombatSkillDef;
        if (skill_def == null || combatProfile == null || skill_def.skill_type != "active")
        {
            record["skip_reason"] = "passive_or_no_combat";
            return record;
        }

        record["target_mode"] = Normalize(combatProfile.target_mode);
        record["target_filter"] = Normalize(combatProfile.target_team_filter);
        record["selection_mode"] = Normalize(combatProfile.target_selection_mode);
        record["team_intent"] = ResolveTeamIntent(skill_def, combatProfile, skill_level);

        ClassifyVariants(record, combatProfile, skill_level);
        ClassifySelectionMode(record, combatProfile);
        ClassifyEffectsAndTargetMode(record, skill_def, combatProfile, skill_level);

        if (GetArray(record, "affordances").Count > 0 && GetArray(record, "action_families").Count > 0)
        {
            record["is_generatable"] = true;
            record["skip_reason"] = "";
        }
        else
        {
            record["is_generatable"] = false;
            record["skip_reason"] = "unsupported_or_special";
        }
        record["requires_positioning_action"] = RequiresPositioningAction(record);
        return record;
    }

    private static GDictionary EmptyRecord(SkillDef skillDef)
    {
        return new GDictionary
        {
            ["skill_id"] = skillDef != null ? skillDef.skill_id : new StringName(""),
            ["is_generatable"] = false,
            ["skip_reason"] = "",
            ["team_intent"] = new StringName(""),
            ["target_mode"] = new StringName(""),
            ["target_filter"] = new StringName(""),
            ["selection_mode"] = new StringName(""),
            ["effect_roles"] = new GArray(),
            ["affordances"] = new GArray(),
            ["action_families"] = new GArray(),
            ["requires_positioning_action"] = false,
            ["variant_ids"] = new GArray(),
            ["blocked_reason"] = "",
        };
    }

    private static void ClassifyVariants(GDictionary record, CombatSkillDef combatProfile, int skillLevel)
    {
        if (Normalize(combatProfile.special_resolution_profile_id) == MeteorSwarmProfileId)
        {
            AddUnique(GetArray(record, "affordances"), new StringName("special_ground"));
            AddUnique(GetArray(record, "affordances"), new StringName("ground_hostile.aoe"));
            AddUnique(GetArray(record, "action_families"), new StringName("use_ground_skill"));
        }

        foreach (CombatCastVariantDef variant in combatProfile.get_unlocked_cast_variants(skillLevel))
        {
            if (variant == null)
            {
                continue;
            }
            if (variant.variant_id != "")
            {
                AddUnique(GetArray(record, "variant_ids"), variant.variant_id);
            }
            bool hasCharge = VariantHasEffect(variant, "charge");
            bool hasPathAoe = VariantHasEffect(variant, PathStepAoeEffectType);
            if (hasCharge && hasPathAoe)
            {
                AddUnique(GetArray(record, "effect_roles"), new StringName("charge"));
                AddUnique(GetArray(record, "effect_roles"), PathStepAoeEffectType);
                AddUnique(GetArray(record, "affordances"), new StringName("charge_path_aoe"));
                AddUnique(GetArray(record, "action_families"), new StringName("use_charge_path_aoe"));
            }
            else if (hasCharge)
            {
                AddUnique(GetArray(record, "effect_roles"), new StringName("charge"));
                AddUnique(GetArray(record, "affordances"), new StringName("charge_engage"));
                AddUnique(GetArray(record, "action_families"), new StringName("use_charge"));
            }
        }
    }

    private static void ClassifySelectionMode(GDictionary record, CombatSkillDef combatProfile)
    {
        StringName selectionMode = Normalize(combatProfile.target_selection_mode);
        if (selectionMode == "random_chain")
        {
            AddUnique(GetArray(record, "affordances"), new StringName("random_chain"));
            AddUnique(GetArray(record, "action_families"), new StringName("use_random_chain_skill"));
            AddUnique(GetArray(record, "action_families"), new StringName("move_to_range"));
        }
        else if (selectionMode == "multi_unit")
        {
            AddUnique(GetArray(record, "affordances"), new StringName("multi_unit"));
            AddUnique(GetArray(record, "action_families"), new StringName("use_multi_unit_skill"));
            AddUnique(GetArray(record, "action_families"), new StringName("move_to_multi_unit_skill_position"));
        }
    }

    private static void ClassifyEffectsAndTargetMode(GDictionary record, SkillDef skillDef, CombatSkillDef combatProfile, int skillLevel)
    {
        StringName targetMode = Normalize(combatProfile.target_mode);
        StringName teamIntent = Normalize(record["team_intent"]);
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
            StringName effectType = Normalize(effectDef.effect_type);
            if (IsDamageEffect(effectDef))
            {
                hasDamage = true;
                AddUnique(GetArray(record, "effect_roles"), new StringName("damage"));
            }
            if (IsHealEffect(effectDef))
            {
                hasHeal = true;
                AddUnique(GetArray(record, "effect_roles"), new StringName("heal"));
            }
            if (IsControlEffect(effectDef))
            {
                hasControl = true;
                AddUnique(GetArray(record, "effect_roles"), new StringName("control"));
            }
            if (IsGroundControlEffect(effectDef))
            {
                hasGroundControl = true;
                AddUnique(GetArray(record, "effect_roles"), new StringName("ground_control"));
            }
            if (effectType == "forced_move")
            {
                hasReposition = true;
                AddUnique(GetArray(record, "effect_roles"), new StringName("forced_move"));
            }
        }

        if (targetMode == "ground")
        {
            if (hasDamage && teamIntent != "support")
            {
                AddUnique(GetArray(record, "affordances"), new StringName("ground_hostile.aoe"));
            }
            if (hasGroundControl || hasControl)
            {
                AddUnique(GetArray(record, "affordances"), new StringName("ground_control"));
                AddUnique(GetArray(record, "affordances"), new StringName("terrain_control"));
            }
            if (!HasFamily(record, "use_charge_path_aoe"))
            {
                AddUnique(GetArray(record, "action_families"), new StringName("use_ground_skill"));
            }
            return;
        }

        if (targetMode == "unit")
        {
            if (teamIntent == "support")
            {
                if (hasHeal)
                {
                    AddUnique(GetArray(record, "affordances"), new StringName("ally_heal"));
                }
                else if (hasControl || hasReposition)
                {
                    AddUnique(GetArray(record, "affordances"), new StringName("self_or_ally_buff"));
                }
            }
            else if (hasDamage)
            {
                AddUnique(GetArray(record, "affordances"), new StringName("unit_hostile.damage"));
            }
            else if (hasControl || hasReposition)
            {
                AddUnique(GetArray(record, "affordances"), new StringName("unit_hostile.control"));
                if (hasReposition)
                {
                    AddUnique(GetArray(record, "affordances"), new StringName("displacement_control"));
                }
            }
            if (!HasAnyFamily(record, new GArray { "use_charge", "use_charge_path_aoe", "use_random_chain_skill", "use_multi_unit_skill" }))
            {
                AddUnique(GetArray(record, "action_families"), new StringName("use_unit_skill"));
            }
        }
    }

    private static StringName ResolveTeamIntent(SkillDef skillDef, CombatSkillDef combatProfile, int skillLevel = -1)
    {
        if (skillDef == null || combatProfile == null)
        {
            return "";
        }
        StringName filter = Normalize(combatProfile.target_team_filter);
        if (BattleTargetTeamRules.is_beneficial_filter(filter))
        {
            return "support";
        }
        if (BattleTargetTeamRules.is_enemy_filter(filter))
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
            if (BattleTargetTeamRules.is_enemy_filter(effectFilter))
            {
                return "hostile";
            }
            if (BattleTargetTeamRules.is_beneficial_filter(effectFilter))
            {
                return "support";
            }
        }
        return "neutral";
    }

    private static GArray CollectEffectDefs(CombatSkillDef combatProfile, int skillLevel = -1)
    {
        var results = new GArray();
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
        foreach (CombatCastVariantDef variant in combatProfile.cast_variants)
        {
            if (variant == null)
            {
                continue;
            }
            if (skillLevel >= 0 && variant.min_skill_level > skillLevel)
            {
                continue;
            }
            foreach (Resource effectResource in variant.effect_defs)
            {
                var effectDef = effectResource as CombatEffectDef;
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
        StringName effectType = Normalize(effectDef.effect_type);
        return effectType == "damage" || effectType == "chain_damage" || effectType == PathStepAoeEffectType;
    }

    private static bool IsHealEffect(CombatEffectDef effectDef)
    {
        return effectDef != null && Normalize(effectDef.effect_type) == "heal";
    }

    private static bool IsControlEffect(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        StringName effectType = Normalize(effectDef.effect_type);
        if (effectType == "status" || effectType == "apply_status" || effectType == "forced_move" || effectType == "terrain" || effectType == "height_delta" || effectType == "barrier")
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
        StringName effectType = Normalize(effectDef.effect_type);
        return effectType == "terrain"
            || effectType == "height_delta"
            || effectType == PathStepAoeEffectType
            || effectDef.terrain_effect_id != ""
            || effectDef.height_delta != 0;
    }

    private static bool VariantHasEffect(CombatCastVariantDef castVariant, StringName effectType)
    {
        if (castVariant == null)
        {
            return false;
        }
        foreach (Resource effectResource in castVariant.effect_defs)
        {
            var effectDef = effectResource as CombatEffectDef;
            if (effectDef != null && Normalize(effectDef.effect_type) == effectType)
            {
                return true;
            }
        }
        return false;
    }

    private static bool RequiresPositioningAction(GDictionary record)
    {
        GArray families = GetArray(record, "action_families");
        return families.Contains(new StringName("move_to_range")) || families.Contains(new StringName("move_to_multi_unit_skill_position"));
    }

    private static bool HasFamily(GDictionary record, StringName family)
    {
        return GetArray(record, "action_families").Contains(family);
    }

    private static bool HasAnyFamily(GDictionary record, GArray families)
    {
        foreach (Variant familyValue in families)
        {
            if (HasFamily(record, Normalize(familyValue)))
            {
                return true;
            }
        }
        return false;
    }

    private static void AddUnique(GArray target, Variant value)
    {
        if (value.VariantType == Variant.Type.Nil || target.Contains(value))
        {
            return;
        }
        target.Add(value);
    }

    private static GArray GetArray(GDictionary record, Variant key)
    {
        return record.ContainsKey(key) && record[key].VariantType == Variant.Type.Array
            ? record[key].AsGodotArray()
            : new GArray();
    }

    private static StringName Normalize(Variant value)
    {
        return ProgressionDataUtils.to_string_name(value);
    }
}
