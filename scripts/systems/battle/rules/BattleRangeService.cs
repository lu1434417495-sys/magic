using System;
using System.Collections.Generic;
using Godot;

public static class BattleRangeService
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
        public BattleUnitReadView UnitView;
        public bool HasUnitView;
        public int WeaponAttackRange;
        public StringName WeaponProfileKind = EmptyStringName;
        public StringName WeaponPhysicalDamageTag = EmptyStringName;
        public StringName WeaponFamily = EmptyStringName;
        public readonly Dictionary<StringName, StatusEffectData> StatusEffects = new();
    }

    private readonly record struct StatusEffectData(int Power, int RangeBonus);

    public static int GetWeaponAttackRange(BattleUnitState unitState)
    {
        return BuildUnitRangeInfo(unitState).WeaponAttackRange;
    }

    internal static int GetWeaponAttackRange(BattleUnitReadView unitView)
    {
        return BuildUnitRangeInfo(unitView).WeaponAttackRange;
    }

    public static bool UnitHasMeleeWeapon(BattleUnitState unitState)
    {
        return UnitHasMeleeWeapon(BuildUnitRangeInfo(unitState));
    }

    public static bool UnitMatchesRequiredWeaponFamilies(
        BattleUnitState unitState,
        IEnumerable<StringName> requiredWeaponFamilies
    )
    {
        return UnitMatchesRequiredWeaponFamilies(
            BuildUnitRangeInfo(unitState),
            requiredWeaponFamilies
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
        IEnumerable<StringName> requiredWeaponFamilies
    )
    {
        bool hasRequiredFamily = false;
        if (requiredWeaponFamilies == null)
        {
            return true;
        }
        foreach (StringName familyValue in requiredWeaponFamilies)
        {
            if (IsEmpty(familyValue))
            {
                continue;
            }
            hasRequiredFamily = true;
            if (!UnitHasMeleeWeapon(unitInfo))
            {
                return false;
            }
            StringName currentFamily = unitInfo.WeaponFamily;
            if (IsEmpty(currentFamily))
            {
                return false;
            }
            if (familyValue == currentFamily)
            {
                return true;
            }
        }
        return !hasRequiredFamily;
    }

    public static int GetEffectiveSkillRange(BattleUnitState unitState, SkillDef skillDef)
    {
        return GetEffectiveSkillRange(unitState, skillDef, null);
    }

    internal static int GetEffectiveSkillRange(BattleUnitReadView unitView, SkillDef skillDef)
    {
        return GetEffectiveSkillRange(unitView, skillDef, null);
    }

    public static int GetEffectiveSkillRange(
        BattleUnitState unitState,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitState);
        return GetEffectiveSkillRange(unitInfo, skillDef, skillCatalog);
    }

    internal static int GetEffectiveSkillRange(
        BattleUnitReadView unitView,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitView);
        return GetEffectiveSkillRange(unitInfo, skillDef, skillCatalog);
    }

    private static int GetEffectiveSkillRange(
        UnitRangeInfo unitInfo,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        int skillRange = ResolveBaseSkillRange(unitInfo, skillDef, skillCatalog);
        skillRange += GetRangeModifierBonus(unitInfo, skillDef);
        return Math.Max(skillRange, 0);
    }

    public static int GetEffectiveSkillThreatRange(
        BattleUnitState unitState,
        SkillDef skillDef
    )
    {
        return GetEffectiveSkillThreatRange(unitState, skillDef, null);
    }

    public static int GetEffectiveSkillThreatRange(
        BattleUnitState unitState,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitState);
        int skillRange = GetEffectiveSkillRange(unitInfo, skillDef, skillCatalog);
        skillRange += GetGroundEffectReachBonus(unitInfo, skillDef, skillCatalog);
        return Math.Max(skillRange, 0);
    }

    public static int GetEffectiveSkillDistanceContractRange(
        BattleUnitState unitState,
        SkillDef skillDef
    )
    {
        return GetEffectiveSkillDistanceContractRange(unitState, skillDef, null);
    }

    public static int GetEffectiveSkillDistanceContractRange(
        BattleUnitState unitState,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitState);
        int skillRange = GetEffectiveSkillRange(unitInfo, skillDef, skillCatalog);
        skillRange += GetGroundEffectDistanceContractBonus(unitInfo, skillDef, skillCatalog);
        return Math.Max(skillRange, 0);
    }

    public static bool RequiresCurrentMeleeWeapon(SkillDef skillDef)
    {
        CombatSkillDef typedCombatProfile = skillDef?.combat_profile;
        if (skillDef == null || typedCombatProfile == null)
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

    public static bool IsWeaponRangeSkill(SkillDef skillDef)
    {
        return SkillHasTag(skillDef, "melee")
            || SkillHasTag(skillDef, "bow")
            || SkillHasTag(skillDef, "weapon");
    }

    public static int ResolveBaseSkillRange(BattleUnitState unitState, SkillDef skillDef)
    {
        return ResolveBaseSkillRange(unitState, skillDef, null);
    }

    public static int ResolveBaseSkillRange(
        BattleUnitState unitState,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        return ResolveBaseSkillRange(BuildUnitRangeInfo(unitState), skillDef, skillCatalog);
    }

    private static int ResolveBaseSkillRange(
        UnitRangeInfo unitInfo,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDef.skill_id);
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(skillCatalog, skillDef, skillLevel);
        int configuredRange = Math.Max(effectiveProfile.RangeValue, 0);
        if (IsGroundRelocationSkill(skillDef))
        {
            return configuredRange;
        }
        if (RequiresCurrentMeleeWeapon(skillDef))
        {
            return unitInfo.WeaponAttackRange;
        }
        if (IsWeaponRangeSkill(skillDef))
        {
            int weaponRange = unitInfo.WeaponAttackRange;
            if (weaponRange > 0)
            {
                return weaponRange;
            }
            if (SkillHasTag(skillDef, "melee"))
            {
                return 1;
            }
        }
        return configuredRange;
    }

    public static bool IsGroundJumpSkill(SkillDef skillDef)
    {
        return IsGroundRelocationSkill(skillDef);
    }

    public static bool IsGroundRelocationSkill(SkillDef skillDef)
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (
            skillDef == null
            || combatProfile == null
            || combatProfile.TargetModeKind != BattleTargetMode.Ground
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

    public static bool EffectUsesWeaponPhysicalDamageTag(CombatEffectDef effectDef)
    {
        return effectDef?.use_weapon_physical_damage_tag ?? false;
    }

    public static bool EffectRequiresWeapon(CombatEffectDef effectDef)
    {
        return EffectRequiresWeaponInternal(effectDef);
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
                new[] { WeaponFamilyBow }
            )
            && (RequiresCurrentMeleeWeapon(skillDef) || IsWeaponRangeSkill(skillDef))
        )
        {
            bonus += Math.Max(shootingStatus.RangeBonus, 0);
        }
        return bonus;
    }

    private static int GetGroundEffectReachBonus(
        UnitRangeInfo unitInfo,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Ground
            || IsGroundRelocationSkill(skillDef)
        )
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDef.skill_id);
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(skillCatalog, skillDef, skillLevel);
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(
            effectiveProfile.AreaPattern
        );
        int areaValue = effectiveProfile.AreaValue;
        return BattleTypedNames.GetAreaPatternThreatReachBonus(areaPattern, areaValue);
    }

    private static int GetGroundEffectDistanceContractBonus(
        UnitRangeInfo unitInfo,
        SkillDef skillDef,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Ground
            || IsGroundRelocationSkill(skillDef)
        )
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDef.skill_id);
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(skillCatalog, skillDef, skillLevel);
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(
            effectiveProfile.AreaPattern
        );
        int areaValue = effectiveProfile.AreaValue;
        return BattleTypedNames.GetAreaPatternDistanceContractBonus(areaPattern, areaValue);
    }

    private static int GetUnitSkillLevel(UnitRangeInfo unitInfo, StringName skillId)
    {
        if (unitInfo == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (unitInfo.HasUnitView)
        {
            int readViewSkillLevel = unitInfo.UnitView.GetKnownSkillLevel(skillId);
            if (readViewSkillLevel > 0)
            {
                return readViewSkillLevel;
            }
            return unitInfo.UnitView.KnowsActiveSkill(skillId) ? 1 : 0;
        }
        BattleUnitState unitState = unitInfo.UnitState;
        if (unitState == null)
        {
            return 0;
        }
        int skillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        if (skillLevel > 0)
        {
            return skillLevel;
        }
        return unitState.known_active_skill_ids.Contains(skillId) ? 1 : 0;
    }

    private static bool EffectRequiresWeaponInternal(CombatEffectDef effectDef)
    {
        return effectDef?.requires_weapon ?? false;
    }

    private static bool EffectListRequiresWeapon(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            if (EffectRequiresWeaponInternal(effectDef))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EffectListHasGroundRelocation(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
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
            || effectDef.EffectKind != BattleEffectKind.ForcedMove
        )
        {
            return false;
        }
        BattleForcedMoveMode mode = effectDef.ForcedMoveModeKind;
        return mode is BattleForcedMoveMode.Jump or BattleForcedMoveMode.Blink;
    }

    private static bool SkillHasTag(SkillDef skillDef, StringName expectedTag)
    {
        if (skillDef == null || IsEmpty(expectedTag))
        {
            return false;
        }
        foreach (StringName tag in skillDef.TagsTyped)
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
        info.WeaponAttackRange = Math.Max(unitState.GetWeaponAttackRange(), 0);
        info.WeaponProfileKind = unitState.weapon_profile_kind;
        info.WeaponPhysicalDamageTag = unitState.weapon_physical_damage_tag;
        info.WeaponFamily = unitState.weapon_family;

        AddStatusEffectData(info, unitState, StatusArcherRangeUp);
        AddStatusEffectData(info, unitState, StatusArcherShootingSpecialization);
        return info;
    }

    private static UnitRangeInfo BuildUnitRangeInfo(BattleUnitReadView unitView)
    {
        var info = new UnitRangeInfo();
        if (!unitView.IsValid)
        {
            return info;
        }
        info.UnitView = unitView;
        info.HasUnitView = true;
        info.WeaponAttackRange = Math.Max(unitView.WeaponAttackRange, 0);
        info.WeaponProfileKind = unitView.WeaponProfileKind;
        info.WeaponPhysicalDamageTag = unitView.WeaponPhysicalDamageTag;
        info.WeaponFamily = unitView.WeaponFamily;

        AddStatusEffectData(info, unitView, StatusArcherRangeUp);
        AddStatusEffectData(info, unitView, StatusArcherShootingSpecialization);
        return info;
    }

    private static void AddStatusEffectData(
        UnitRangeInfo info,
        BattleUnitState unitState,
        StringName statusId
    )
    {
        BattleStatusEffectState effectState = unitState.GetStatusEffect(statusId);
        if (effectState == null || effectState.IsEmpty())
        {
            return;
        }
        info.StatusEffects[statusId] = BuildStatusEffectData(effectState.power, effectState.range_bonus);
    }

    private static void AddStatusEffectData(
        UnitRangeInfo info,
        BattleUnitReadView unitView,
        StringName statusId
    )
    {
        if (!unitView.HasStatusEffect(statusId))
        {
            return;
        }
        info.StatusEffects[statusId] = BuildStatusEffectData(
            unitView.GetStatusPower(statusId),
            unitView.GetStatusRangeBonus(statusId)
        );
    }

    private static StatusEffectData BuildStatusEffectData(int power, int rangeBonus)
    {
        return new StatusEffectData(power, rangeBonus > 0 ? rangeBonus : power);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
