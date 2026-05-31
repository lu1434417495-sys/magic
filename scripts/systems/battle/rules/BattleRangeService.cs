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
    private static readonly StringName StatusArcherShootingSpecialization =
        "archer_shooting_specialization";
    private static readonly StringName WeaponProfileKindEquipped = "equipped";
    private static readonly StringName WeaponFamilyBow = "bow";

    private sealed class UnitRangeInfo
    {
        public BattleUnitState UnitState;
        public int WeaponAttackRange;
        public StringName WeaponProfileKind = EmptyStringName;
        public StringName WeaponPhysicalDamageTag = EmptyStringName;
        public StringName WeaponFamily = EmptyStringName;
        public readonly Dictionary<StringName, StatusEffectData> StatusEffects = new();
    }

    private readonly record struct StatusEffectData(int Power, int RangeBonus);

    public static int get_weapon_attack_range(BattleUnitState unit_state)
    {
        return BuildUnitRangeInfo(unit_state).WeaponAttackRange;
    }

    public static bool unit_has_melee_weapon(BattleUnitState unit_state)
    {
        return UnitHasMeleeWeapon(BuildUnitRangeInfo(unit_state));
    }

    public static bool unit_matches_required_weapon_families(
        BattleUnitState unit_state,
        Godot.Collections.Array<StringName> required_weapon_families
    )
    {
        return UnitMatchesRequiredWeaponFamilies(
            BuildUnitRangeInfo(unit_state),
            required_weapon_families
        );
    }

    private static bool UnitHasMeleeWeapon(UnitRangeInfo unitInfo)
    {
        return unitInfo != null
            && unitInfo.WeaponProfileKind == WeaponProfileKindEquipped
            && unitInfo.WeaponAttackRange > 0
            && !IsEmpty(unitInfo.WeaponPhysicalDamageTag);
    }

    private static bool UnitMatchesRequiredWeaponFamilies(
        UnitRangeInfo unitInfo,
        Godot.Collections.Array<StringName> required_weapon_families
    )
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
        foreach (StringName familyValue in required_weapon_families)
        {
            if (familyValue == currentFamily)
            {
                return true;
            }
        }
        return false;
    }

    public static int get_effective_skill_range(BattleUnitState unit_state, SkillDef skill_def)
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unit_state);
        return GetEffectiveSkillRange(unitInfo, skill_def);
    }

    private static int GetEffectiveSkillRange(UnitRangeInfo unitInfo, SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return 0;
        }
        int skillRange = ResolveBaseSkillRange(unitInfo, skill_def);
        skillRange += GetRangeModifierBonus(unitInfo, skill_def);
        return Math.Max(skillRange, 0);
    }

    public static int get_effective_skill_threat_range(
        BattleUnitState unit_state,
        SkillDef skill_def
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unit_state);
        int skillRange = GetEffectiveSkillRange(unitInfo, skill_def);
        skillRange += GetGroundEffectReachBonus(unitInfo, skill_def);
        return Math.Max(skillRange, 0);
    }

    public static int get_effective_skill_distance_contract_range(
        BattleUnitState unit_state,
        SkillDef skill_def
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unit_state);
        int skillRange = GetEffectiveSkillRange(unitInfo, skill_def);
        skillRange += GetGroundEffectDistanceContractBonus(unitInfo, skill_def);
        return Math.Max(skillRange, 0);
    }

    public static bool requires_current_melee_weapon(SkillDef skill_def)
    {
        CombatSkillDef typedCombatProfile = skill_def?.combat_profile;
        if (skill_def == null || typedCombatProfile == null)
        {
            return false;
        }
        if (typedCombatProfile.required_weapon_families.Count > 0)
        {
            return true;
        }
        if (EffectListRequiresWeapon(typedCombatProfile.effect_defs))
        {
            return true;
        }
        foreach (CombatCastVariantDef castVariant in typedCombatProfile.cast_variants)
        {
            if (castVariant != null && EffectListRequiresWeapon(castVariant.effect_defs))
            {
                return true;
            }
        }
        return false;
    }

    public static bool is_weapon_range_skill(SkillDef skill_def)
    {
        return SkillHasTag(skill_def, "melee")
            || SkillHasTag(skill_def, "bow")
            || SkillHasTag(skill_def, "weapon");
    }

    public static int resolve_base_skill_range(BattleUnitState unit_state, SkillDef skill_def)
    {
        return ResolveBaseSkillRange(BuildUnitRangeInfo(unit_state), skill_def);
    }

    private static int ResolveBaseSkillRange(UnitRangeInfo unitInfo, SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skill_def.skill_id);
        int configuredRange = Math.Max(combatProfile.get_effective_range_value(skillLevel), 0);
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

    public static bool is_ground_jump_skill(SkillDef skill_def)
    {
        return is_ground_relocation_skill(skill_def);
    }

    public static bool is_ground_relocation_skill(SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            skill_def == null
            || combatProfile == null
            || BattleTypedNames.ToTargetMode(combatProfile.target_mode) != BattleTargetMode.Ground
        )
        {
            return false;
        }
        if (EffectListHasGroundRelocation(combatProfile.effect_defs))
        {
            return true;
        }
        foreach (var castVariant in combatProfile.cast_variants)
        {
            if (
                castVariant != null
                && EffectListHasGroundRelocation(castVariant.effect_defs)
            )
            {
                return true;
            }
        }
        return false;
    }

    public static bool effect_uses_weapon_physical_damage_tag(CombatEffectDef effect_def)
    {
        return effect_def?.use_weapon_physical_damage_tag ?? false;
    }

    public static bool effect_requires_weapon(CombatEffectDef effect_def)
    {
        return EffectRequiresWeapon(effect_def);
    }

    private static int GetRangeModifierBonus(UnitRangeInfo unitInfo, SkillDef skillDef)
    {
        int bonus = HasStatusEffect(unitInfo, StatusArcherRangeUp) ? 1 : 0;
        if (
            TryGetStatusEffectData(
                unitInfo,
                StatusArcherShootingSpecialization,
                out StatusEffectData shootingStatus
            )
            && UnitMatchesRequiredWeaponFamilies(
                unitInfo,
                new Godot.Collections.Array<StringName> { WeaponFamilyBow }
            )
            && (requires_current_melee_weapon(skillDef) || is_weapon_range_skill(skillDef))
        )
        {
            bonus += Math.Max(shootingStatus.RangeBonus, 0);
        }
        return bonus;
    }

    private static int GetGroundEffectReachBonus(UnitRangeInfo unitInfo, SkillDef skillDef)
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        if (
            BattleTypedNames.ToTargetMode(combatProfile.target_mode) != BattleTargetMode.Ground
            || is_ground_relocation_skill(skillDef)
        )
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDef.skill_id);
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(
            combatProfile.get_effective_area_pattern(skillLevel)
        );
        int areaValue = combatProfile.get_effective_area_value(skillLevel);
        return BattleTypedNames.GetAreaPatternThreatReachBonus(areaPattern, areaValue);
    }

    private static int GetGroundEffectDistanceContractBonus(
        UnitRangeInfo unitInfo,
        SkillDef skillDef
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        if (
            BattleTypedNames.ToTargetMode(combatProfile.target_mode) != BattleTargetMode.Ground
            || is_ground_relocation_skill(skillDef)
        )
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDef.skill_id);
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(
            combatProfile.get_effective_area_pattern(skillLevel)
        );
        int areaValue = combatProfile.get_effective_area_value(skillLevel);
        return BattleTypedNames.GetAreaPatternDistanceContractBonus(areaPattern, areaValue);
    }

    private static int GetUnitSkillLevel(UnitRangeInfo unitInfo, StringName skillId)
    {
        if (unitInfo == null || IsEmpty(skillId))
        {
            return 0;
        }
        BattleUnitState unitState = unitInfo.UnitState;
        if (unitState == null)
        {
            return 0;
        }
        int skillLevel = ReadInt(unitState.known_skill_level_map, skillId);
        if (skillLevel > 0)
        {
            return skillLevel;
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private static bool EffectRequiresWeapon(CombatEffectDef effectDef)
    {
        return effectDef?.requires_weapon ?? false;
    }

    private static bool EffectListRequiresWeapon(
        Godot.Collections.Array<CombatEffectDef> effectDefs
    )
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? new Godot.Collections.Array<CombatEffectDef>())
        {
            if (EffectRequiresWeapon(effectDef))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EffectListHasGroundRelocation(
        Godot.Collections.Array<CombatEffectDef> effectDefs
    )
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? new Godot.Collections.Array<CombatEffectDef>())
        {
            if (IsGroundRelocationEffect(effectDef))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroundRelocationEffect(CombatEffectDef effectDef)
    {
        if (
            effectDef == null
            || BattleTypedNames.ToEffectKind(effectDef.effect_type) != BattleEffectKind.ForcedMove
        )
        {
            return false;
        }
        BattleForcedMoveMode mode = BattleTypedNames.ToForcedMoveMode(effectDef.forced_move_mode);
        return mode is BattleForcedMoveMode.Jump or BattleForcedMoveMode.Blink;
    }

    private static bool SkillHasTag(SkillDef skillDef, StringName expectedTag)
    {
        if (skillDef == null || IsEmpty(expectedTag))
        {
            return false;
        }
        foreach (StringName tag in skillDef.tags)
        {
            if (tag == expectedTag)
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

    private static bool TryGetStatusEffectData(
        UnitRangeInfo unitInfo,
        StringName statusId,
        out StatusEffectData statusData
    )
    {
        statusData = default;
        if (unitInfo == null || IsEmpty(statusId))
        {
            return false;
        }
        return unitInfo.StatusEffects.TryGetValue(statusId, out statusData);
    }

    private static UnitRangeInfo BuildUnitRangeInfo(BattleUnitState unitState)
    {
        var info = new UnitRangeInfo();
        if (unitState == null)
        {
            return info;
        }
        info.UnitState = unitState;
        info.WeaponAttackRange = Math.Max(unitState.get_weapon_attack_range(), 0);
        info.WeaponProfileKind = unitState.weapon_profile_kind;
        info.WeaponPhysicalDamageTag = unitState.weapon_physical_damage_tag;
        info.WeaponFamily = unitState.weapon_family;

        AddStatusEffectData(info, unitState, StatusArcherRangeUp);
        AddStatusEffectData(info, unitState, StatusArcherShootingSpecialization);
        return info;
    }

    private static void AddStatusEffectData(
        UnitRangeInfo info,
        BattleUnitState unitState,
        StringName statusId
    )
    {
        BattleStatusEffectState effectState = unitState.get_status_effect(statusId);
        if (effectState == null || effectState.is_empty())
        {
            return;
        }
        info.StatusEffects[statusId] = BuildStatusEffectData(effectState.power, effectState.@params);
    }

    private static StatusEffectData BuildStatusEffectData(int power, GDictionary parameters)
    {
        int rangeBonus = ReadInt(parameters, "range_bonus", power);
        return new StatusEffectData(power, rangeBonus);
    }

    private static Dictionary<StringName, int> BuildStringNameIntMap(GDictionary rawMap)
    {
        var result = new Dictionary<StringName, int>();
        if (rawMap == null || rawMap.Count == 0)
        {
            return result;
        }
        foreach (object keyValue in rawMap.Keys)
        {
            StringName key = ToStringName(keyValue);
            if (!IsEmpty(key))
            {
                result[key] = ReadInt(rawMap, keyValue);
            }
        }
        return result;
    }

    private static HashSet<StringName> BuildStringNameSet(GArray rawValues)
    {
        var result = new HashSet<StringName>();
        foreach (object value in rawValues)
        {
            StringName item = ToStringName(value);
            if (!IsEmpty(item))
            {
                result.Add(item);
            }
        }
        return result;
    }

    private static StringName ToStringName(object rawValue)
    {
        if (rawValue is not Variant value)
        {
            return new StringName(rawValue?.ToString() ?? "");
        }
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(value.ToString()),
        };
    }

    private static IEnumerable<T> Objects<T>(GArray values)
        where T : GodotObject
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (TryAsObject(rawValue, out T value))
            {
                yield return value;
            }
        }
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, object key, out object value)
    {
        if (data == null || key == null)
        {
            value = null;
            return false;
        }
        if (key is Variant variantKey)
        {
            if (data.ContainsKey(variantKey))
            {
                value = data[variantKey];
                return true;
            }
        }
        else if (key is StringName stringNameKey && data.ContainsKey(stringNameKey))
        {
            value = data[stringNameKey];
            return true;
        }
        else if (key is string stringKey && data.ContainsKey(stringKey))
        {
            value = data[stringKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool IsNil(object rawValue)
    {
        return rawValue == null
            || rawValue is Variant variant && variant.VariantType == Variant.Type.Nil;
    }

    private static int ReadInt(GDictionary data, object key, int fallback = 0)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static Variant ReadValue(GDictionary data, object key)
    {
        if (data == null || key == null)
            return default;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            StringName stringNameKey => stringNameKey,
            string stringKey => stringKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return default;
        if (data.ContainsKey(variantKey))
            return data[variantKey];
        if (variantKey.VariantType == Variant.Type.String)
        {
            var stringNameKey = new StringName(variantKey.AsString());
            if (data.ContainsKey(stringNameKey))
                return data[stringNameKey];
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (data.ContainsKey(stringKey))
                return data[stringKey];
        }
        return default;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
