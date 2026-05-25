using System;
using System.Collections.Generic;
using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleSkillResolutionRules : RefCounted
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName BlackContractPushSkillId = "black_contract_push";
    private static readonly StringName FatePreviewModeNone = "";
    private static readonly StringName FatePreviewModeStandard = "standard";
    private static readonly StringName FatePreviewModeForceHitNoCrit = "force_hit_no_crit";
    private static readonly StringName SaveDcModeCasterSpell = "caster_spell";

    private Resource _combatCastVariantScript;

    private readonly record struct SkillResolutionPolicy(
        GArray TargetUnitIds,
        GodotObject UnitCastVariant,
        GodotObject GroundCastVariant,
        GodotObject CommandCastVariant,
        GodotObject UnitExecutionCastVariant,
        GodotObject ExecutionCastVariant,
        bool RoutesToUnitTargeting,
        string VariantErrorMessage,
        GArray EffectDefs,
        bool UsesFateAttack,
        bool ForceHitNoCrit,
        StringName FatePreviewMode
    )
    {
        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["target_unit_ids"] = DuplicateArray(TargetUnitIds),
                ["unit_cast_variant"] = UnitCastVariant,
                ["ground_cast_variant"] = GroundCastVariant,
                ["command_cast_variant"] = CommandCastVariant,
                ["unit_execution_cast_variant"] = UnitExecutionCastVariant,
                ["execution_cast_variant"] = ExecutionCastVariant,
                ["routes_to_unit_targeting"] = RoutesToUnitTargeting,
                ["variant_error_message"] = VariantErrorMessage,
                ["variant_allowed"] = string.IsNullOrEmpty(VariantErrorMessage),
                ["effect_defs"] = DuplicateArray(EffectDefs),
                ["uses_fate_attack"] = UsesFateAttack,
                ["force_hit_no_crit"] = ForceHitNoCrit,
                ["fate_preview_mode"] = FatePreviewMode,
            };
        }
    }

    public static StringName FATE_PREVIEW_MODE_FORCE_HIT_NO_CRIT() => FatePreviewModeForceHitNoCrit;

    public GDictionary build_skill_resolution_policy(
        GodotObject skill_def,
        GodotObject active_unit,
        StringName skill_variant_id = default,
        Variant target_unit_ids_variant = default,
        GodotObject target_unit = null)
    {
        GArray targetUnitIds = normalize_target_unit_ids(target_unit_ids_variant);
        bool routesToUnitTargeting = should_route_skill_command_to_unit_targeting(skill_def, targetUnitIds);
        string variantErrorMessage = get_skill_variant_command_error_message(
            skill_def,
            active_unit,
            skill_variant_id,
            routesToUnitTargeting);
        GodotObject unitCastVariant = resolve_unit_cast_variant(skill_def, active_unit, skill_variant_id);
        GodotObject groundCastVariant = resolve_ground_cast_variant(skill_def, active_unit, skill_variant_id);
        GodotObject commandCastVariant = resolve_command_route_cast_variant(
            skill_def,
            active_unit,
            skill_variant_id,
            routesToUnitTargeting);
        GodotObject unitExecutionCastVariant = routesToUnitTargeting ? commandCastVariant : unitCastVariant;
        GodotObject executionCastVariant = routesToUnitTargeting ? unitExecutionCastVariant : commandCastVariant;

        GArray effectDefs = new();
        if (string.IsNullOrEmpty(variantErrorMessage))
        {
            effectDefs = routesToUnitTargeting
                ? collect_unit_skill_effect_defs(skill_def, unitExecutionCastVariant, active_unit)
                : collect_ground_unit_effect_defs(skill_def, groundCastVariant, active_unit);
        }

        bool usesFateAttack = routesToUnitTargeting
            && should_resolve_unit_skill_as_fate_attack(active_unit as BattleUnitState, target_unit as BattleUnitState, skill_def, effectDefs);
        bool forceHitNoCrit = usesFateAttack && is_force_hit_no_crit_skill(skill_def);
        StringName fatePreviewMode = FatePreviewModeNone;
        if (usesFateAttack)
        {
            fatePreviewMode = forceHitNoCrit ? FatePreviewModeForceHitNoCrit : FatePreviewModeStandard;
        }

        return new SkillResolutionPolicy(
            targetUnitIds,
            unitCastVariant,
            groundCastVariant,
            commandCastVariant,
            unitExecutionCastVariant,
            executionCastVariant,
            routesToUnitTargeting,
            variantErrorMessage,
            effectDefs,
            usesFateAttack,
            forceHitNoCrit,
            fatePreviewMode
        ).ToDictionary();
    }

    public GArray normalize_target_unit_ids(Variant target_unit_ids_variant)
    {
        var targetUnitIds = new GArray();
        if (target_unit_ids_variant.VariantType != Variant.Type.Array)
        {
            return targetUnitIds;
        }
        var seenIds = new HashSet<StringName>();
        foreach (Variant targetUnitIdValue in target_unit_ids_variant.AsGodotArray())
        {
            StringName targetUnitId = ToStringName(targetUnitIdValue);
            if (IsEmpty(targetUnitId) || !seenIds.Add(targetUnitId))
            {
                continue;
            }
            targetUnitIds.Add(targetUnitId);
        }
        return targetUnitIds;
    }

    public bool should_route_skill_command_to_unit_targeting(GodotObject skill_def, GArray target_unit_ids)
    {
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return false;
        }
        if (target_unit_ids != null && target_unit_ids.Count > 0)
        {
            return true;
        }
        if (BattleTypedNames.ToTargetSelectionMode(GetStringName(combatProfile, "target_selection_mode")) == BattleTargetSelectionMode.RandomChain)
        {
            return true;
        }
        return BattleTypedNames.ToTargetMode(GetStringName(combatProfile, "target_mode")) == BattleTargetMode.Unit;
    }

    public string get_skill_variant_command_error_message(
        GodotObject skill_def,
        GodotObject active_unit,
        StringName skill_variant_id = default,
        bool routes_to_unit_targeting = false)
    {
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }
        GArray rawVariants = GetArray(combatProfile, "cast_variants");
        if (rawVariants.Count == 0)
        {
            return !IsEmpty(skill_variant_id) ? "技能形态无效或尚未解锁。" : "";
        }

        int skillLevel = GetUnitSkillLevel(active_unit, GetStringName(skill_def, "skill_id"));
        GArray unlockedVariants = GetUnlockedCastVariants(combatProfile, skillLevel);
        var matchingModeVariants = new GArray();
        StringName expectedTargetMode = get_command_route_cast_variant_target_mode(skill_def, routes_to_unit_targeting);
        foreach (Variant variantValue in unlockedVariants)
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null && get_cast_variant_target_mode(skill_def, castVariant) == expectedTargetMode)
            {
                matchingModeVariants.Add(castVariant);
            }
        }
        if (IsEmpty(skill_variant_id))
        {
            if (matchingModeVariants.Count > 1)
            {
                return "技能形态不明确。";
            }
            return matchingModeVariants.Count == 0 ? "技能形态无效或尚未解锁。" : "";
        }
        foreach (Variant variantValue in matchingModeVariants)
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null && GetStringName(castVariant, "variant_id") == skill_variant_id)
            {
                return "";
            }
        }
        return "技能形态无效或尚未解锁。";
    }

    public bool should_resolve_unit_skill_as_fate_attack(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        GodotObject skill_def,
        GArray effect_defs)
    {
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        if (active_unit == null || target_unit == null || skill_def == null || combatProfile == null)
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
        foreach (Variant effectValue in effect_defs)
        {
            GodotObject effectDef = effectValue.AsGodotObject();
            if (effectDef == null || BattleTypedNames.ToEffectKind(GetStringName(effectDef, "effect_type")) != BattleEffectKind.Damage)
            {
                continue;
            }
            if (EffectHasSave(effectDef))
            {
                continue;
            }
            if (!is_unit_valid_for_effect(active_unit, target_unit, resolve_effect_target_filter(skill_def, effectDef)))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    public bool is_force_hit_no_crit_skill(GodotObject skill_def)
    {
        return skill_def != null && GetStringName(skill_def, "skill_id") == BlackContractPushSkillId;
    }

    public GodotObject resolve_ground_cast_variant(
        GodotObject skill_def,
        GodotObject active_unit,
        StringName skill_variant_id = default)
    {
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        GArray rawVariants = GetArray(combatProfile, "cast_variants");
        if (rawVariants.Count == 0)
        {
            return BattleTypedNames.ToTargetMode(GetStringName(combatProfile, "target_mode")) == BattleTargetMode.Ground && IsEmpty(skill_variant_id)
                ? BuildImplicitGroundCastVariant(skill_def)
                : null;
        }

        int skillLevel = GetUnitSkillLevel(active_unit, GetStringName(skill_def, "skill_id"));
        GArray unlockedVariants = GetUnlockedCastVariants(combatProfile, skillLevel);
        if (unlockedVariants.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skill_variant_id))
        {
            var groundVariants = new GArray();
            foreach (Variant variantValue in unlockedVariants)
            {
                GodotObject castVariant = variantValue.AsGodotObject();
                if (castVariant != null && BattleTypedNames.ToTargetMode(get_cast_variant_target_mode(skill_def, castVariant)) == BattleTargetMode.Ground)
                {
                    groundVariants.Add(castVariant);
                }
            }
            return groundVariants.Count == 1 ? groundVariants[0].AsGodotObject() : null;
        }

        foreach (Variant variantValue in unlockedVariants)
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null
                && GetStringName(castVariant, "variant_id") == skill_variant_id
                && BattleTypedNames.ToTargetMode(get_cast_variant_target_mode(skill_def, castVariant)) == BattleTargetMode.Ground)
            {
                return castVariant;
            }
        }
        return null;
    }

    public GodotObject resolve_unit_cast_variant(
        GodotObject skill_def,
        GodotObject active_unit,
        StringName skill_variant_id = default)
    {
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return null;
        }
        if (GetArray(combatProfile, "cast_variants").Count == 0)
        {
            return null;
        }

        int skillLevel = GetUnitSkillLevel(active_unit, GetStringName(skill_def, "skill_id"));
        GArray unlockedVariants = GetUnlockedCastVariants(combatProfile, skillLevel);
        if (unlockedVariants.Count == 0)
        {
            return null;
        }
        if (IsEmpty(skill_variant_id))
        {
            var unitVariants = new GArray();
            foreach (Variant variantValue in unlockedVariants)
            {
                GodotObject castVariant = variantValue.AsGodotObject();
                if (castVariant != null && BattleTypedNames.ToTargetMode(get_cast_variant_target_mode(skill_def, castVariant)) == BattleTargetMode.Unit)
                {
                    unitVariants.Add(castVariant);
                }
            }
            return unitVariants.Count == 1 ? unitVariants[0].AsGodotObject() : null;
        }

        foreach (Variant variantValue in unlockedVariants)
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null
                && GetStringName(castVariant, "variant_id") == skill_variant_id
                && BattleTypedNames.ToTargetMode(get_cast_variant_target_mode(skill_def, castVariant)) == BattleTargetMode.Unit)
            {
                return castVariant;
            }
        }
        return null;
    }

    public GodotObject resolve_command_route_cast_variant(
        GodotObject skill_def,
        GodotObject active_unit,
        StringName skill_variant_id = default,
        bool routes_to_unit_targeting = false)
    {
        StringName targetMode = get_command_route_cast_variant_target_mode(skill_def, routes_to_unit_targeting);
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

    public StringName get_command_route_cast_variant_target_mode(GodotObject skill_def, bool routes_to_unit_targeting = false)
    {
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return EmptyStringName;
        }
        return !routes_to_unit_targeting ? BattleTypedNames.TargetModeGround : GetStringName(combatProfile, "target_mode");
    }

    public StringName get_cast_variant_target_mode(GodotObject skill_def, GodotObject cast_variant)
    {
        if (cast_variant == null)
        {
            return EmptyStringName;
        }
        StringName targetMode = GetStringName(cast_variant, "target_mode");
        if (!IsEmpty(targetMode))
        {
            return targetMode;
        }
        GodotObject combatProfile = GetObject(skill_def, "combat_profile");
        return combatProfile != null ? GetStringName(combatProfile, "target_mode") : EmptyStringName;
    }

    public GArray collect_unit_skill_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit = null)
    {
        return CollectEffectDefs(skill_def, cast_variant, active_unit);
    }

    public GArray collect_ground_unit_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit = null)
    {
        GArray effectDefs = new();
        foreach (Variant effectValue in CollectEffectDefs(skill_def, cast_variant, active_unit))
        {
            GodotObject effectDef = effectValue.AsGodotObject();
            if (is_unit_effect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public GArray collect_ground_terrain_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit = null)
    {
        GArray effectDefs = new();
        foreach (Variant effectValue in CollectEffectDefs(skill_def, cast_variant, active_unit))
        {
            GodotObject effectDef = effectValue.AsGodotObject();
            if (is_terrain_effect(effectDef))
            {
                effectDefs.Add(effectDef);
            }
        }
        return effectDefs;
    }

    public GArray collect_ground_effect_defs(GodotObject skill_def, GodotObject cast_variant, GodotObject active_unit = null)
    {
        return CollectEffectDefs(skill_def, cast_variant, active_unit);
    }

    public GodotObject find_repeat_attack_effect(GArray effect_defs)
    {
        foreach (Variant effectValue in effect_defs ?? new GArray())
        {
            GodotObject effectDef = effectValue.AsGodotObject();
            if (effectDef != null && BattleTypedNames.ToEffectKind(GetStringName(effectDef, "effect_type")) == BattleEffectKind.RepeatAttackUntilFail)
            {
                return effectDef;
            }
        }
        return null;
    }

    public bool is_unit_effect(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        return BattleTypedNames.IsUnitPayloadEffect(BattleTypedNames.ToEffectKind(GetStringName(effect_def, "effect_type")));
    }

    public bool is_terrain_effect(GodotObject effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        return BattleTypedNames.IsGroundPayloadEffect(BattleTypedNames.ToEffectKind(GetStringName(effect_def, "effect_type")));
    }

    public StringName resolve_effect_target_filter(GodotObject skill_def, GodotObject effect_def)
    {
        return BattleTargetTeamRules.resolve_effect_target_filter(skill_def, effect_def);
    }

    public bool is_unit_valid_for_effect(BattleUnitState source_unit, BattleUnitState target_unit, StringName target_team_filter)
    {
        return BattleTargetTeamRules.is_unit_valid_for_filter(source_unit, target_unit, target_team_filter);
    }

    private GArray CollectEffectDefs(GodotObject skillDef, GodotObject castVariant, GodotObject activeUnit)
    {
        var effectDefs = new GArray();
        int skillLevel = GetUnitSkillLevel(activeUnit, skillDef != null ? GetStringName(skillDef, "skill_id") : EmptyStringName);
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (skillDef != null && combatProfile != null)
        {
            AddUnlockedEffectDefs(effectDefs, GetArray(combatProfile, "effect_defs"), skillLevel, activeUnit != null);
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefs(effectDefs, GetArray(castVariant, "effect_defs"), skillLevel, activeUnit != null);
        }
        return effectDefs;
    }

    private static void AddUnlockedEffectDefs(GArray target, GArray source, int skillLevel, bool shouldFilter)
    {
        foreach (Variant effectValue in source)
        {
            GodotObject effectDef = effectValue.AsGodotObject();
            if (IsEffectUnlockedForSkillLevel(effectDef, skillLevel, shouldFilter))
            {
                target.Add(effectDef);
            }
        }
    }

    private static bool IsEffectUnlockedForSkillLevel(GodotObject effectDef, int skillLevel, bool shouldFilter)
    {
        if (effectDef == null)
        {
            return false;
        }
        if (!shouldFilter)
        {
            return true;
        }
        int minLevel = Math.Max(GetInt(effectDef, "min_skill_level"), 0);
        int maxLevel = GetInt(effectDef, "max_skill_level");
        return skillLevel >= minLevel && (maxLevel < 0 || skillLevel <= maxLevel);
    }

    private static int GetUnitSkillLevel(GodotObject activeUnit, StringName skillId)
    {
        if (activeUnit == null || IsEmpty(skillId))
        {
            return 0;
        }
        return Math.Max(GetInt(GetDictionary(activeUnit, "known_skill_level_map"), skillId, 0), 0);
    }

    private static bool EffectHasSave(GodotObject effectDef)
    {
        if (effectDef == null)
        {
            return false;
        }
        return GetStringName(effectDef, "save_dc_mode") == SaveDcModeCasterSpell || GetInt(effectDef, "save_dc") > 0;
    }

    private static GArray GetUnlockedCastVariants(GodotObject combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return new GArray();
        }
        Variant unlockedVariants = combatProfile.Call("get_unlocked_cast_variants", skillLevel);
        return unlockedVariants.VariantType == Variant.Type.Array ? unlockedVariants.AsGodotArray() : new GArray();
    }

    private GodotObject BuildImplicitGroundCastVariant(GodotObject skillDef)
    {
        _combatCastVariantScript ??= ResourceLoader.Load<Resource>("res://scripts/player/progression/combat_cast_variant_def.gd");
        GodotObject castVariant = _combatCastVariantScript?.Call("new").AsGodotObject();
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (castVariant == null || combatProfile == null)
        {
            return null;
        }
        castVariant.Set("variant_id", EmptyStringName);
        castVariant.Set("display_name", "");
        castVariant.Set("target_mode", BattleTypedNames.TargetModeGround);
        castVariant.Set("footprint_pattern", BattleTypedNames.AreaPatternSingle);
        castVariant.Set("required_coord_count", 1);
        castVariant.Set("effect_defs", DuplicateArray(GetArray(combatProfile, "effect_defs")));
        return castVariant;
    }

    private static GArray DuplicateArray(GArray values)
    {
        var result = new GArray();
        foreach (Variant value in values ?? new GArray())
        {
            result.Add(value);
        }
        return result;
    }

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(value.ToString()),
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

}
