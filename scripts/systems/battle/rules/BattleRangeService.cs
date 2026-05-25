using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleRangeService : RefCounted
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName StatusArcherRangeUp = "archer_range_up";
    private static readonly StringName StatusArcherShootingSpecialization = "archer_shooting_specialization";
    private static readonly StringName WeaponProfileKindEquipped = "equipped";
    private static readonly StringName WeaponFamilyBow = "bow";

    private sealed class UnitRangeInfo
    {
        public int WeaponAttackRange;
        public StringName WeaponProfileKind = EmptyStringName;
        public StringName WeaponPhysicalDamageTag = EmptyStringName;
        public StringName WeaponFamily = EmptyStringName;
        public readonly Dictionary<StringName, int> KnownSkillLevels = new();
        public readonly HashSet<StringName> KnownActiveSkillIds = new();
        public readonly Dictionary<StringName, StatusEffectData> StatusEffects = new();
    }

    private readonly record struct StatusEffectData(int Power, int RangeBonus);

    public static int get_weapon_attack_range(GodotObject unit_state)
    {
        return BuildUnitRangeInfo(unit_state).WeaponAttackRange;
    }

    public static bool unit_has_melee_weapon(GodotObject unit_state)
    {
        return UnitHasMeleeWeapon(BuildUnitRangeInfo(unit_state));
    }

    public static bool unit_matches_required_weapon_families(GodotObject unit_state, GArray required_weapon_families)
    {
        return UnitMatchesRequiredWeaponFamilies(BuildUnitRangeInfo(unit_state), required_weapon_families);
    }

    private static bool UnitHasMeleeWeapon(UnitRangeInfo unitInfo)
    {
        return unitInfo != null
            && unitInfo.WeaponProfileKind == WeaponProfileKindEquipped
            && unitInfo.WeaponAttackRange > 0
            && !IsEmpty(unitInfo.WeaponPhysicalDamageTag);
    }

    private static bool UnitMatchesRequiredWeaponFamilies(UnitRangeInfo unitInfo, GArray required_weapon_families)
    {
        if (required_weapon_families == null || required_weapon_families.Count == 0)
        {
            return true;
        }
        if (!UnitHasMeleeWeapon(unitInfo))
        {
            return false;
        }
        StringName currentFamily = unitInfo.WeaponFamily;
        if (IsEmpty(currentFamily))
        {
            return false;
        }
        foreach (Variant familyValue in required_weapon_families)
        {
            if (new StringName(familyValue.ToString()) == currentFamily)
            {
                return true;
            }
        }
        return false;
    }

    public static int get_effective_skill_range(GodotObject unit_state, GodotObject skill_def)
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unit_state);
        return GetEffectiveSkillRange(unitInfo, skill_def);
    }

    private static int GetEffectiveSkillRange(UnitRangeInfo unitInfo, GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return 0;
        }
        int skillRange = ResolveBaseSkillRange(unitInfo, skill_def);
        skillRange += GetRangeModifierBonus(unitInfo, skill_def);
        return Math.Max(skillRange, 0);
    }

    public static int get_effective_skill_threat_range(GodotObject unit_state, GodotObject skill_def)
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unit_state);
        int skillRange = GetEffectiveSkillRange(unitInfo, skill_def);
        skillRange += GetGroundEffectReachBonus(unitInfo, skill_def);
        return Math.Max(skillRange, 0);
    }

    public static int get_effective_skill_distance_contract_range(GodotObject unit_state, GodotObject skill_def)
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unit_state);
        int skillRange = GetEffectiveSkillRange(unitInfo, skill_def);
        skillRange += GetGroundEffectDistanceContractBonus(unitInfo, skill_def);
        return Math.Max(skillRange, 0);
    }

    public static bool requires_current_melee_weapon(GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return false;
        }
        if (GdInterop.GetArray(combatProfile, "required_weapon_families").Count > 0)
        {
            return true;
        }
        if (EffectListRequiresWeapon(GdInterop.GetArray(combatProfile, "effect_defs")))
        {
            return true;
        }
        foreach (Variant variantValue in GdInterop.GetArray(combatProfile, "cast_variants"))
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null && EffectListRequiresWeapon(GdInterop.GetArray(castVariant, "effect_defs")))
            {
                return true;
            }
        }
        return false;
    }

    public static bool is_weapon_range_skill(GodotObject skill_def)
    {
        return SkillHasTag(skill_def, "melee") || SkillHasTag(skill_def, "bow") || SkillHasTag(skill_def, "weapon");
    }

    public static int resolve_base_skill_range(GodotObject unit_state, GodotObject skill_def)
    {
        return ResolveBaseSkillRange(BuildUnitRangeInfo(unit_state), skill_def);
    }

    private static int ResolveBaseSkillRange(UnitRangeInfo unitInfo, GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, GdInterop.GetStringName(skill_def, "skill_id"));
        int configuredRange = Math.Max(combatProfile.Call("get_effective_range_value", skillLevel).AsInt32(), 0);
        if (is_ground_relocation_skill(skill_def))
        {
            return configuredRange;
        }
        if (requires_current_melee_weapon(skill_def))
        {
            return unitInfo.WeaponAttackRange;
        }
        if (is_weapon_range_skill(skill_def))
        {
            int weaponRange = unitInfo.WeaponAttackRange;
            if (weaponRange > 0)
            {
                return weaponRange;
            }
            if (SkillHasTag(skill_def, "melee"))
            {
                return 1;
            }
        }
        return configuredRange;
    }

    public static bool is_ground_jump_skill(GodotObject skill_def)
    {
        return is_ground_relocation_skill(skill_def);
    }

    public static bool is_ground_relocation_skill(GodotObject skill_def)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null || BattleTypedNames.ToTargetMode(GdInterop.GetStringName(combatProfile, "target_mode")) != BattleTargetMode.Ground)
        {
            return false;
        }
        if (EffectListHasGroundRelocation(GdInterop.GetArray(combatProfile, "effect_defs")))
        {
            return true;
        }
        foreach (Variant variantValue in GdInterop.GetArray(combatProfile, "cast_variants"))
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null && EffectListHasGroundRelocation(GdInterop.GetArray(castVariant, "effect_defs")))
            {
                return true;
            }
        }
        return false;
    }

    public static bool effect_uses_weapon_physical_damage_tag(GodotObject effect_def)
    {
        return effect_def != null && GdInterop.GetBool(GdInterop.GetDictionary(effect_def, "params"), "use_weapon_physical_damage_tag");
    }

    public static bool effect_requires_weapon(GodotObject effect_def)
    {
        return EffectRequiresWeapon(effect_def);
    }

    private static int GetRangeModifierBonus(UnitRangeInfo unitInfo, GodotObject skillDef)
    {
        int bonus = HasStatusEffect(unitInfo, StatusArcherRangeUp) ? 1 : 0;
        if (TryGetStatusEffectData(unitInfo, StatusArcherShootingSpecialization, out StatusEffectData shootingStatus)
            && UnitMatchesRequiredWeaponFamilies(unitInfo, new GArray { WeaponFamilyBow })
            && (requires_current_melee_weapon(skillDef) || is_weapon_range_skill(skillDef)))
        {
            bonus += Math.Max(shootingStatus.RangeBonus, 0);
        }
        return bonus;
    }

    private static int GetGroundEffectReachBonus(UnitRangeInfo unitInfo, GodotObject skillDef)
    {
        GodotObject combatProfile = GdInterop.GetObject(skillDef, "combat_profile");
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        if (BattleTypedNames.ToTargetMode(GdInterop.GetStringName(combatProfile, "target_mode")) != BattleTargetMode.Ground || is_ground_relocation_skill(skillDef))
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, GdInterop.GetStringName(skillDef, "skill_id"));
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(combatProfile.Call("get_effective_area_pattern", skillLevel).AsStringName());
        int areaValue = combatProfile.Call("get_effective_area_value", skillLevel).AsInt32();
        return BattleTypedNames.GetAreaPatternThreatReachBonus(areaPattern, areaValue);
    }

    private static int GetGroundEffectDistanceContractBonus(UnitRangeInfo unitInfo, GodotObject skillDef)
    {
        GodotObject combatProfile = GdInterop.GetObject(skillDef, "combat_profile");
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        if (BattleTypedNames.ToTargetMode(GdInterop.GetStringName(combatProfile, "target_mode")) != BattleTargetMode.Ground || is_ground_relocation_skill(skillDef))
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, GdInterop.GetStringName(skillDef, "skill_id"));
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(combatProfile.Call("get_effective_area_pattern", skillLevel).AsStringName());
        int areaValue = combatProfile.Call("get_effective_area_value", skillLevel).AsInt32();
        return BattleTypedNames.GetAreaPatternDistanceContractBonus(areaPattern, areaValue);
    }

    private static int GetUnitSkillLevel(UnitRangeInfo unitInfo, StringName skillId)
    {
        if (unitInfo == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (unitInfo.KnownSkillLevels.TryGetValue(skillId, out int skillLevel))
        {
            return skillLevel;
        }
        return unitInfo.KnownActiveSkillIds.Contains(skillId) ? 1 : 0;
    }

    private static bool EffectListRequiresWeapon(GArray effectDefs)
    {
        foreach (Variant effectValue in effectDefs)
        {
            if (EffectRequiresWeapon(effectValue.AsGodotObject()))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EffectRequiresWeapon(GodotObject effectDef)
    {
        return effectDef != null && GdInterop.GetBool(GdInterop.GetDictionary(effectDef, "params"), "requires_weapon");
    }

    private static bool EffectListHasGroundRelocation(GArray effectDefs)
    {
        foreach (Variant effectValue in effectDefs)
        {
            if (IsGroundRelocationEffect(effectValue.AsGodotObject()))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroundRelocationEffect(GodotObject effectDef)
    {
        if (effectDef == null || BattleTypedNames.ToEffectKind(GdInterop.GetStringName(effectDef, "effect_type")) != BattleEffectKind.ForcedMove)
        {
            return false;
        }
        BattleForcedMoveMode mode = BattleTypedNames.ToForcedMoveMode(GdInterop.GetStringName(effectDef, "forced_move_mode"));
        return mode is BattleForcedMoveMode.Jump or BattleForcedMoveMode.Blink;
    }

    private static bool SkillHasTag(GodotObject skillDef, StringName expectedTag)
    {
        if (skillDef == null || IsEmpty(expectedTag))
        {
            return false;
        }
        foreach (Variant tag in GdInterop.GetArray(skillDef, "tags"))
        {
            if (new StringName(tag.ToString()) == expectedTag)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasStatusEffect(UnitRangeInfo unitInfo, StringName statusId)
    {
        return TryGetStatusEffectData(unitInfo, statusId, out _);
    }

    private static bool TryGetStatusEffectData(UnitRangeInfo unitInfo, StringName statusId, out StatusEffectData statusData)
    {
        statusData = default;
        if (unitInfo == null || IsEmpty(statusId))
        {
            return false;
        }
        return unitInfo.StatusEffects.TryGetValue(statusId, out statusData);
    }

    private static UnitRangeInfo BuildUnitRangeInfo(GodotObject unitState)
    {
        var info = new UnitRangeInfo();
        if (unitState == null)
        {
            return info;
        }
        info.WeaponAttackRange = Math.Max(unitState.Call("get_weapon_attack_range").AsInt32(), 0);
        info.WeaponProfileKind = GdInterop.GetStringName(unitState, "weapon_profile_kind");
        info.WeaponPhysicalDamageTag = GdInterop.GetStringName(unitState, "weapon_physical_damage_tag");
        info.WeaponFamily = GdInterop.GetStringName(unitState, "weapon_family");

        foreach ((StringName skillId, int skillLevel) in BuildStringNameIntMap(GdInterop.GetDictionary(unitState, "known_skill_level_map")))
        {
            info.KnownSkillLevels[skillId] = skillLevel;
        }
        foreach (StringName skillId in BuildStringNameSet(GdInterop.GetArray(unitState, "known_active_skill_ids")))
        {
            info.KnownActiveSkillIds.Add(skillId);
        }
        GDictionary statusEffects = GdInterop.GetDictionary(unitState, "status_effects");
        foreach (Variant statusKeyValue in statusEffects.Keys)
        {
            StringName statusKey = ToStringName(statusKeyValue);
            if (IsEmpty(statusKey))
            {
                continue;
            }
            Variant effectValue = statusEffects[statusKeyValue];
            if (effectValue.VariantType == Variant.Type.Nil)
            {
                continue;
            }

            if (effectValue.VariantType == Variant.Type.Dictionary)
            {
                GDictionary effectData = effectValue.AsGodotDictionary();
                if (IsEmpty(GdInterop.GetStringName(effectData, "status_id")))
                {
                    continue;
                }
                Variant rawParams = GdInterop.TryGet(effectData, "params", out Variant paramsValue) ? paramsValue : default;
                GDictionary statusParams = rawParams.VariantType == Variant.Type.Dictionary ? rawParams.AsGodotDictionary() : new GDictionary();
                int power = GdInterop.GetInt(effectData, "power");
                info.StatusEffects[statusKey] = BuildStatusEffectData(power, statusParams);
                continue;
            }

            GodotObject effectState = effectValue.AsGodotObject();
            if (effectState == null || IsEmpty(GdInterop.GetStringName(effectState, "status_id")))
            {
                continue;
            }
            int effectPower = GdInterop.GetInt(effectState, "power");
            info.StatusEffects[statusKey] = BuildStatusEffectData(effectPower, GdInterop.GetDictionary(effectState, "params"));
        }
        return info;
    }

    private static StatusEffectData BuildStatusEffectData(int power, GDictionary parameters)
    {
        int rangeBonus = GdInterop.TryGet(parameters, "range_bonus", out Variant rawRangeBonus)
            ? rawRangeBonus.AsInt32()
            : power;
        return new StatusEffectData(power, rangeBonus);
    }

    private static Dictionary<StringName, int> BuildStringNameIntMap(GDictionary rawMap)
    {
        var result = new Dictionary<StringName, int>();
        if (rawMap == null || rawMap.Count == 0)
        {
            return result;
        }
        foreach (Variant keyValue in rawMap.Keys)
        {
            StringName key = ToStringName(keyValue);
            if (!IsEmpty(key))
            {
                result[key] = rawMap[keyValue].AsInt32();
            }
        }
        return result;
    }

    private static HashSet<StringName> BuildStringNameSet(GArray rawValues)
    {
        var result = new HashSet<StringName>();
        foreach (Variant value in rawValues)
        {
            StringName item = ToStringName(value);
            if (!IsEmpty(item))
            {
                result.Add(item);
            }
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
