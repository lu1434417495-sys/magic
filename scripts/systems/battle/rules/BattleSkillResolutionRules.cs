using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GCastVariantArray = Godot.Collections.Array<CombatCastVariantDef>;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class BattleSkillResolutionRules : RefCounted
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName BlackContractPushSkillId = "black_contract_push";
    private static readonly StringName FatePreviewModeNone = "";
    private static readonly StringName FatePreviewModeStandard = "standard";
    private static readonly StringName FatePreviewModeForceHitNoCrit = "force_hit_no_crit";
    private static readonly StringName SaveDcModeCasterSpell = "caster_spell";

    private readonly record struct SkillResolutionPolicy(
        GStringNameArray TargetUnitIds,
        CombatCastVariantDef UnitCastVariant,
        CombatCastVariantDef GroundCastVariant,
        CombatCastVariantDef CommandCastVariant,
        CombatCastVariantDef UnitExecutionCastVariant,
        CombatCastVariantDef ExecutionCastVariant,
        bool RoutesToUnitTargeting,
        string OptionErrorMessage,
        GCombatEffectArray EffectDefs,
        bool UsesFateAttack,
        bool ForceHitNoCrit,
        StringName FatePreviewMode
    )
    {
        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["target_unit_ids"] = DuplicateStringNameArray(TargetUnitIds),
                ["unit_cast_variant"] = UnitCastVariant,
                ["ground_cast_variant"] = GroundCastVariant,
                ["command_cast_variant"] = CommandCastVariant,
                ["unit_execution_cast_variant"] = UnitExecutionCastVariant,
                ["execution_cast_variant"] = ExecutionCastVariant,
                ["routes_to_unit_targeting"] = RoutesToUnitTargeting,
                ["option_error_message"] = OptionErrorMessage,
                ["option_allowed"] = string.IsNullOrEmpty(OptionErrorMessage),
                ["effect_defs"] = DuplicateEffectArray(EffectDefs),
                ["uses_fate_attack"] = UsesFateAttack,
                ["force_hit_no_crit"] = ForceHitNoCrit,
                ["fate_preview_mode"] = FatePreviewMode,
            };
        }
    }

    public static StringName FATE_PREVIEW_MODE_FORCE_HIT_NO_CRIT() => FatePreviewModeForceHitNoCrit;

    public GDictionary build_skill_resolution_policy(
        SkillDef skill_def,
        BattleUnitState active_unit,
        StringName skill_variant_id = default,
        GStringNameArray target_unit_ids_option = null,
        BattleUnitState target_unit = null
    )
    {
        GStringNameArray targetUnitIds = normalize_target_unit_ids(target_unit_ids_option);
        bool routesToUnitTargeting = should_route_skill_command_to_unit_targeting(
            skill_def,
            targetUnitIds
        );
        string optionErrorMessage = get_skill_variant_command_error_message(
            skill_def,
            active_unit,
            skill_variant_id,
            routesToUnitTargeting
        );
        CombatCastVariantDef unitCastVariant = resolve_unit_cast_variant(
            skill_def,
            active_unit,
            skill_variant_id
        );
        CombatCastVariantDef groundCastVariant = resolve_ground_cast_variant(
            skill_def,
            active_unit,
            skill_variant_id
        );
        CombatCastVariantDef commandCastVariant = resolve_command_route_cast_variant(
            skill_def,
            active_unit,
            skill_variant_id,
            routesToUnitTargeting
        );
        CombatCastVariantDef unitExecutionCastVariant = routesToUnitTargeting
            ? commandCastVariant
            : unitCastVariant;
        CombatCastVariantDef executionCastVariant = routesToUnitTargeting
            ? unitExecutionCastVariant
            : commandCastVariant;

        GCombatEffectArray effectDefs = new();
        if (string.IsNullOrEmpty(optionErrorMessage))
        {
            effectDefs = routesToUnitTargeting
                ? collect_unit_skill_effect_defs(skill_def, unitExecutionCastVariant, active_unit)
                : collect_ground_unit_effect_defs(skill_def, groundCastVariant, active_unit);
        }

        bool usesFateAttack =
            routesToUnitTargeting
            && should_resolve_unit_skill_as_fate_attack(
                active_unit,
                target_unit,
                skill_def,
                effectDefs
            );
        bool forceHitNoCrit = usesFateAttack && is_force_hit_no_crit_skill(skill_def);
        StringName fatePreviewMode = FatePreviewModeNone;
        if (usesFateAttack)
        {
            fatePreviewMode = forceHitNoCrit
                ? FatePreviewModeForceHitNoCrit
                : FatePreviewModeStandard;
        }

        return new SkillResolutionPolicy(
            targetUnitIds,
            unitCastVariant,
            groundCastVariant,
            commandCastVariant,
            unitExecutionCastVariant,
            executionCastVariant,
            routesToUnitTargeting,
            optionErrorMessage,
            effectDefs,
            usesFateAttack,
            forceHitNoCrit,
            fatePreviewMode
        ).ToDictionary();
    }

    public GStringNameArray normalize_target_unit_ids(GStringNameArray target_unit_ids_option)
    {
        var targetUnitIds = new GStringNameArray();
        if (target_unit_ids_option == null)
        {
            return targetUnitIds;
        }
        var seenIds = new HashSet<StringName>();
        foreach (StringName targetUnitId in target_unit_ids_option)
        {
            if (IsEmpty(targetUnitId) || !seenIds.Add(targetUnitId))
            {
                continue;
            }
            targetUnitIds.Add(targetUnitId);
        }
        return targetUnitIds;
    }

    public bool should_route_skill_command_to_unit_targeting(
        SkillDef skill_def,
        GStringNameArray target_unit_ids
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return false;
        }
        if (target_unit_ids != null && target_unit_ids.Count > 0)
        {
            return true;
        }
        if (
            BattleTypedNames.ToTargetSelectionMode(
                combatProfile.target_selection_mode
            ) == BattleTargetSelectionMode.RandomChain
        )
        {
            return true;
        }
        return BattleTypedNames.ToTargetMode(combatProfile.target_mode)
            == BattleTargetMode.Unit;
    }

    public string get_skill_variant_command_error_message(
        SkillDef skill_def,
        BattleUnitState active_unit,
        StringName skill_variant_id = default,
        bool routes_to_unit_targeting = false
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return !IsEmpty(skill_variant_id) ? "技能形态无效或尚未解锁。" : "";
        }

        int skillLevel = GetUnitSkillLevel(active_unit, skill_def.skill_id);
        GCastVariantArray unlockedOptions = GetUnlockedCastVariants(combatProfile, skillLevel);
        var matchingModeOptions = new GCastVariantArray();
        StringName expectedTargetMode = get_command_route_cast_variant_target_mode(
            skill_def,
            routes_to_unit_targeting
        );
        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && get_cast_variant_target_mode(skill_def, castVariant) == expectedTargetMode
            )
            {
                matchingModeOptions.Add(castVariant);
            }
        }
        if (IsEmpty(skill_variant_id))
        {
            if (matchingModeOptions.Count > 1)
            {
                return "技能形态不明确。";
            }
            return matchingModeOptions.Count == 0 ? "技能形态无效或尚未解锁。" : "";
        }
        foreach (CombatCastVariantDef castVariant in matchingModeOptions)
        {
            if (castVariant != null && castVariant.variant_id == skill_variant_id)
            {
                return "";
            }
        }
        return "技能形态无效或尚未解锁。";
    }

    public bool should_resolve_unit_skill_as_fate_attack(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            active_unit == null
            || target_unit == null
            || skill_def == null
            || combatProfile == null
        )
        {
            return false;
        }
        if (active_unit.faction_id == target_unit.faction_id)
        {
            return false;
        }
        if (effect_defs == null || effect_defs.Count == 0)
        {
            return false;
        }
        foreach (CombatEffectDef effectDef in effect_defs)
        {
            if (
                effectDef == null
                || BattleTypedNames.ToEffectKind(effectDef.effect_type) != BattleEffectKind.Damage
            )
            {
                continue;
            }
            if (EffectHasSave(effectDef))
            {
                continue;
            }
            if (
                !is_unit_valid_for_effect(
                    active_unit,
                    target_unit,
                    resolve_effect_target_filter(skill_def, effectDef)
                )
            )
            {
                continue;
            }
            return true;
        }
        return false;
    }

    public bool is_force_hit_no_crit_skill(SkillDef skill_def)
    {
        return skill_def != null && skill_def.skill_id == BlackContractPushSkillId;
    }

    public CombatCastVariantDef resolve_ground_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        StringName skill_variant_id = default
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return
                BattleTypedNames.ToTargetMode(combatProfile.target_mode)
                    == BattleTargetMode.Ground
                && IsEmpty(skill_variant_id)
                ? BuildImplicitGroundCastVariant(skill_def)
                : null;
        }

        int skillLevel = GetUnitSkillLevel(active_unit, skill_def.skill_id);
        GCastVariantArray unlockedOptions = GetUnlockedCastVariants(combatProfile, skillLevel);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skill_variant_id))
        {
            var groundOptions = new GCastVariantArray();
            foreach (CombatCastVariantDef castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && BattleTypedNames.ToTargetMode(
                        get_cast_variant_target_mode(skill_def, castVariant)
                    ) == BattleTargetMode.Ground
                )
                {
                    groundOptions.Add(castVariant);
                }
            }
            return groundOptions.Count == 1 ? groundOptions[0] : null;
        }

        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.variant_id == skill_variant_id
                && BattleTypedNames.ToTargetMode(
                    get_cast_variant_target_mode(skill_def, castVariant)
                ) == BattleTargetMode.Ground
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    public CombatCastVariantDef resolve_unit_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        StringName skill_variant_id = default
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        if (combatProfile.cast_variants.Count == 0)
        {
            return null;
        }

        int skillLevel = GetUnitSkillLevel(active_unit, skill_def.skill_id);
        GCastVariantArray unlockedOptions = GetUnlockedCastVariants(combatProfile, skillLevel);
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skill_variant_id))
        {
            var unitOptions = new GCastVariantArray();
            foreach (CombatCastVariantDef castVariant in unlockedOptions)
            {
                if (
                    castVariant != null
                    && BattleTypedNames.ToTargetMode(
                        get_cast_variant_target_mode(skill_def, castVariant)
                    ) == BattleTargetMode.Unit
                )
                {
                    unitOptions.Add(castVariant);
                }
            }
            return unitOptions.Count == 1 ? unitOptions[0] : null;
        }

        foreach (CombatCastVariantDef castVariant in unlockedOptions)
        {
            if (
                castVariant != null
                && castVariant.variant_id == skill_variant_id
                && BattleTypedNames.ToTargetMode(
                    get_cast_variant_target_mode(skill_def, castVariant)
                ) == BattleTargetMode.Unit
            )
            {
                return castVariant;
            }
        }
        return null;
    }

    public CombatCastVariantDef resolve_command_route_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        StringName skill_variant_id = default,
        bool routes_to_unit_targeting = false
    )
    {
        StringName targetMode = get_command_route_cast_variant_target_mode(
            skill_def,
            routes_to_unit_targeting
        );
        if (BattleTypedNames.ToTargetMode(targetMode) == BattleTargetMode.Unit)
        {
            return resolve_unit_cast_variant(skill_def, active_unit, skill_variant_id);
        }
        if (BattleTypedNames.ToTargetMode(targetMode) == BattleTargetMode.Ground)
        {
            return resolve_ground_cast_variant(skill_def, active_unit, skill_variant_id);
        }
        return null;
    }

    public StringName get_command_route_cast_variant_target_mode(
        SkillDef skill_def,
        bool routes_to_unit_targeting = false
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return EmptyStringName;
        }
        return !routes_to_unit_targeting
            ? BattleTypedNames.TargetModeGround
            : combatProfile.target_mode;
    }

    public StringName get_cast_variant_target_mode(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (cast_variant == null)
        {
            return EmptyStringName;
        }
        StringName targetMode = cast_variant.target_mode;
        if (!IsEmpty(targetMode))
        {
            return targetMode;
        }
        return skill_def?.combat_profile != null ? skill_def.combat_profile.target_mode : EmptyStringName;
    }

    public GCombatEffectArray collect_unit_skill_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        return CollectEffectDefs(skill_def, cast_variant, active_unit);
    }

    public GCombatEffectArray collect_ground_unit_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        GCombatEffectArray effectDefs = new();
        foreach (CombatEffectDef effectDef in CollectEffectDefs(skill_def, cast_variant, active_unit))
        {
            if (is_unit_effect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public GCombatEffectArray collect_ground_terrain_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        GCombatEffectArray effectDefs = new();
        foreach (CombatEffectDef effectDef in CollectEffectDefs(skill_def, cast_variant, active_unit))
        {
            if (is_terrain_effect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public GCombatEffectArray collect_ground_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        return CollectEffectDefs(skill_def, cast_variant, active_unit);
    }

    public CombatEffectDef find_repeat_attack_effect(GCombatEffectArray effect_defs)
    {
        foreach (CombatEffectDef effectDef in effect_defs ?? new GCombatEffectArray())
        {
            if (
                effectDef != null
                && BattleTypedNames.ToEffectKind(effectDef.effect_type)
                    == BattleEffectKind.RepeatAttackUntilFail
            )
            {
                return effectDef;
            }
        }
        return null;
    }

    public bool is_unit_effect(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        return BattleTypedNames.IsUnitPayloadEffect(
            BattleTypedNames.ToEffectKind(effect_def.effect_type)
        );
    }

    public bool is_terrain_effect(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        return BattleTypedNames.IsGroundPayloadEffect(
            BattleTypedNames.ToEffectKind(effect_def.effect_type)
        );
    }

    public StringName resolve_effect_target_filter(SkillDef skill_def, CombatEffectDef effect_def)
    {
        return BattleTargetTeamRules.resolve_effect_target_filter(skill_def, effect_def);
    }

    public bool is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            source_unit,
            target_unit,
            target_team_filter
        );
    }

    private GCombatEffectArray CollectEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit
    )
    {
        var effectDefs = new GCombatEffectArray();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef?.skill_id ?? EmptyStringName);
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef != null && combatProfile != null)
        {
            AddUnlockedEffectDefs(
                effectDefs,
                combatProfile.effect_defs,
                skillLevel,
                activeUnit != null
            );
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefs(
                effectDefs,
                castVariant.effect_defs,
                skillLevel,
                activeUnit != null
            );
        }
        return effectDefs;
    }

    private static void AddUnlockedEffectDefs(
        GCombatEffectArray target,
        GCombatEffectArray source,
        int skillLevel,
        bool shouldFilter
    )
    {
        foreach (CombatEffectDef effectDef in source ?? new GCombatEffectArray())
        {
            if (IsEffectUnlockedForSkillLevel(effectDef, skillLevel, shouldFilter))
            {
                target.Add(effectDef);
            }
        }
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDef effectDef,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDef == null)
        {
            return false;
        }
        if (!shouldFilter)
        {
            return true;
        }
        int minLevel = Math.Max(effectDef.min_skill_level, 0);
        int maxLevel = effectDef.max_skill_level;
        return skillLevel >= minLevel && (maxLevel < 0 || skillLevel <= maxLevel);
    }

    private static int GetUnitSkillLevel(BattleUnitState activeUnit, StringName skillId)
    {
        if (activeUnit == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (activeUnit.known_skill_level_map.ContainsKey(skillId))
        {
            return Math.Max(activeUnit.known_skill_level_map[skillId].AsInt32(), 0);
        }
        string stringKey = skillId.ToString();
        return activeUnit.known_skill_level_map.ContainsKey(stringKey)
            ? Math.Max(activeUnit.known_skill_level_map[stringKey].AsInt32(), 0)
            : 0;
    }

    private static bool EffectHasSave(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        return effectDef.save_dc_mode == SaveDcModeCasterSpell || effectDef.save_dc > 0;
    }

    private static GCastVariantArray GetUnlockedCastVariants(
        CombatSkillDef combatProfile,
        int skillLevel
    )
    {
        return combatProfile?.get_unlocked_cast_variants(skillLevel) ?? new GCastVariantArray();
    }

    private static CombatCastVariantDef BuildImplicitGroundCastVariant(SkillDef skillDef)
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (combatProfile == null)
        {
            return null;
        }
        return new CombatCastVariantDef
        {
            variant_id = EmptyStringName,
            display_name = "",
            target_mode = BattleTypedNames.TargetModeGround,
            footprint_pattern = BattleTypedNames.AreaPatternSingle,
            required_coord_count = 1,
            effect_defs = DuplicateEffectArray(combatProfile.effect_defs),
        };
    }

    private static GStringNameArray DuplicateStringNameArray(GStringNameArray values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values ?? new GStringNameArray())
        {
            result.Add(value);
        }
        return result;
    }

    private static GCombatEffectArray DuplicateEffectArray(GCombatEffectArray values)
    {
        var result = new GCombatEffectArray();
        foreach (CombatEffectDef value in values ?? new GCombatEffectArray())
        {
            result.Add(value);
        }
        return result;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

}
