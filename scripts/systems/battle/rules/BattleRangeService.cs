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

    public static int GetEffectiveSkillRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition
    )
    {
        return GetEffectiveSkillRange(unitState, skillDefinition, null);
    }

    internal static int GetEffectiveSkillRange(
        BattleUnitReadView unitView,
        SkillDefinition skillDefinition
    )
    {
        return GetEffectiveSkillRange(unitView, skillDefinition, null);
    }

    public static int GetEffectiveSkillRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitState);
        return GetEffectiveSkillRange(unitInfo, skillDefinition, skillCatalog);
    }

    internal static int GetEffectiveSkillRange(
        BattleUnitReadView unitView,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitView);
        return GetEffectiveSkillRange(unitInfo, skillDefinition, skillCatalog);
    }

    private static int GetEffectiveSkillRange(
        UnitRangeInfo unitInfo,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return 0;
        }
        int skillRange = ResolveBaseSkillRange(unitInfo, skillDefinition, skillCatalog);
        skillRange += GetRangeModifierBonus(unitInfo, skillDefinition);
        return Math.Max(skillRange, 0);
    }

    public static int GetEffectiveSkillThreatRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition
    )
    {
        return GetEffectiveSkillThreatRange(unitState, skillDefinition, null);
    }

    public static int GetEffectiveSkillThreatRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitState);
        int skillRange = GetEffectiveSkillRange(unitInfo, skillDefinition, skillCatalog);
        skillRange += GetGroundEffectReachBonus(unitInfo, skillDefinition, skillCatalog);
        return Math.Max(skillRange, 0);
    }

    public static int GetEffectiveSkillDistanceContractRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition
    )
    {
        return GetEffectiveSkillDistanceContractRange(unitState, skillDefinition, null);
    }

    public static int GetEffectiveSkillDistanceContractRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        UnitRangeInfo unitInfo = BuildUnitRangeInfo(unitState);
        int skillRange = GetEffectiveSkillRange(unitInfo, skillDefinition, skillCatalog);
        skillRange += GetGroundEffectDistanceContractBonus(
            unitInfo,
            skillDefinition,
            skillCatalog
        );
        return Math.Max(skillRange, 0);
    }

    public static bool RequiresCurrentMeleeWeapon(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return false;
        }
        if (combatProfile.RequiredWeaponFamilies.Count > 0)
        {
            return true;
        }
        if (EffectListRequiresWeapon(combatProfile.EffectDefinitions))
        {
            return true;
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant != null && EffectListRequiresWeapon(castVariant.EffectDefinitions))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsWeaponRangeSkill(SkillDefinition skillDefinition)
    {
        return SkillHasTag(skillDefinition, "melee")
            || SkillHasTag(skillDefinition, "bow")
            || SkillHasTag(skillDefinition, "weapon");
    }

    public static int ResolveBaseSkillRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition
    )
    {
        return ResolveBaseSkillRange(unitState, skillDefinition, null);
    }

    public static int ResolveBaseSkillRange(
        BattleUnitState unitState,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        return ResolveBaseSkillRange(
            BuildUnitRangeInfo(unitState),
            skillDefinition,
            skillCatalog
        );
    }

    private static int ResolveBaseSkillRange(
        UnitRangeInfo unitInfo,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDefinition.SkillId);
        SkillEffectiveCombatDefinition effectiveDefinition = ResolveEffectiveDefinition(
            skillCatalog,
            skillDefinition,
            skillLevel
        );
        int configuredRange = Math.Max(effectiveDefinition.RangeValue, 0);
        if (IsGroundRelocationSkill(skillDefinition))
        {
            return configuredRange;
        }
        if (RequiresCurrentMeleeWeapon(skillDefinition))
        {
            return unitInfo.WeaponAttackRange;
        }
        if (IsWeaponRangeSkill(skillDefinition))
        {
            int weaponRange = unitInfo.WeaponAttackRange;
            if (weaponRange > 0)
            {
                return weaponRange;
            }
            if (SkillHasTag(skillDefinition, "melee"))
            {
                return 1;
            }
        }
        return configuredRange;
    }

    public static bool IsGroundJumpSkill(SkillDefinition skillDefinition)
    {
        return IsGroundRelocationSkill(skillDefinition);
    }

    public static bool IsGroundRelocationSkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            skillDefinition == null
            || combatProfile == null
            || combatProfile.TargetModeKind != BattleTargetMode.Ground
        )
        {
            return false;
        }
        if (EffectListHasGroundRelocation(combatProfile.EffectDefinitions))
        {
            return true;
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant != null && EffectListHasGroundRelocation(castVariant.EffectDefinitions))
            {
                return true;
            }
        }
        return false;
    }

    private static int GetRangeModifierBonus(
        UnitRangeInfo unitInfo,
        SkillDefinition skillDefinition
    )
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
            && (RequiresCurrentMeleeWeapon(skillDefinition) || IsWeaponRangeSkill(skillDefinition))
        )
        {
            bonus += Math.Max(shootingStatus.RangeBonus, 0);
        }
        return bonus;
    }

    private static int GetGroundEffectReachBonus(
        UnitRangeInfo unitInfo,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return 0;
        }
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Ground
            || IsGroundRelocationSkill(skillDefinition)
        )
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDefinition.SkillId);
        SkillEffectiveCombatDefinition effectiveDefinition = ResolveEffectiveDefinition(
            skillCatalog,
            skillDefinition,
            skillLevel
        );
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(
            effectiveDefinition.AreaPattern
        );
        int areaValue = effectiveDefinition.AreaValue;
        return BattleTypedNames.GetAreaPatternThreatReachBonus(areaPattern, areaValue);
    }

    private static int GetGroundEffectDistanceContractBonus(
        UnitRangeInfo unitInfo,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return 0;
        }
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Ground
            || IsGroundRelocationSkill(skillDefinition)
        )
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unitInfo, skillDefinition.SkillId);
        SkillEffectiveCombatDefinition effectiveDefinition = ResolveEffectiveDefinition(
            skillCatalog,
            skillDefinition,
            skillLevel
        );
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(
            effectiveDefinition.AreaPattern
        );
        int areaValue = effectiveDefinition.AreaValue;
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

    private static bool EffectListRequiresWeapon(
        IEnumerable<CombatEffectDefinition> effectDefs
    )
    {
        foreach (
            CombatEffectDefinition effectDef in effectDefs
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDef?.RequiresWeapon ?? false)
            {
                return true;
            }
        }
        return false;
    }

    private static bool EffectListHasGroundRelocation(
        IEnumerable<CombatEffectDefinition> effectDefs
    )
    {
        foreach (
            CombatEffectDefinition effectDef in effectDefs
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (IsGroundRelocationEffect(effectDef))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroundRelocationEffect(CombatEffectDefinition effectDef)
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

    private static bool SkillHasTag(SkillDefinition skillDefinition, StringName expectedTag)
    {
        if (skillDefinition == null || IsEmpty(expectedTag))
        {
            return false;
        }
        foreach (StringName tag in skillDefinition.Tags)
        {
            if (tag == expectedTag)
            {
                return true;
            }
        }
        return false;
    }

    private static SkillEffectiveCombatDefinition ResolveEffectiveDefinition(
        ISkillCatalog skillCatalog,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        if (
            skillCatalog != null
            && skillDefinition != null
            && !IsEmpty(skillDefinition.SkillId)
        )
        {
            return skillCatalog.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel);
        }
        return SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
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
