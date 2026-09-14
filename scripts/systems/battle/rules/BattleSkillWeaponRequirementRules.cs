using System.Collections.Generic;
using Godot;

internal static class BattleSkillWeaponRequirementRules
{
    internal static BattleSkillCastBlockReasonKind GetBlockReason(
        BattleUnitReadView unitView,
        SkillDefinition skillDefinition,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        CombatSkillDefinition combatProfile =
            skillDefinition?.CombatProfile;
        if (
            !unitView.IsValid
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return BattleSkillCastBlockReasonKind.InvalidSkillOrTarget;
        }

        if (
            combatProfile.RequiredWeaponFamilies.Count > 0
            && !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                unitView,
                skillDefinition
            )
        )
        {
            return BattleSkillCastBlockReasonKind
                .RequiredWeaponFamilyMissing;
        }
        if (
            combatProfile.RequiredWeaponTypeIds.Count > 0
            && !BattleRangeService.UnitMatchesRequiredWeaponTypeIds(
                unitView,
                skillDefinition
            )
        )
        {
            return BattleSkillCastBlockReasonKind
                .RequiredWeaponTypeMissing;
        }
        if (
            combatProfile.RequiresHeavyWeapon
            && (
                unitView.WeaponProfileKind != new StringName("equipped")
                || unitView.WeaponRangeType != new StringName("melee")
                || !unitView.WeaponIsHeavy
            )
        )
        {
            return BattleSkillCastBlockReasonKind.HeavyWeaponRequired;
        }
        if (
            combatProfile.RequiresEquippedShield
            && !BattleEquipmentRequirementRules.UnitHasEquippedShield(
                unitView,
                itemDefinitions
            )
        )
        {
            return BattleSkillCastBlockReasonKind.ShieldRequired;
        }
        if (
            BattleRangeService.RequiresCurrentWeapon(skillDefinition)
            && !BattleRangeService.UnitHasAllowedWeaponForSkill(unitView, skillDefinition)
        )
        {
            return BattleSkillCastBlockReasonKind.MeleeWeaponRequired;
        }
        if (
            BattleRangeService.RequiresCurrentMeleeWeapon(
                skillDefinition
            )
            && !BattleRangeService.UnitHasAllowedMeleeWeaponForSkill(unitView, skillDefinition)
        )
        {
            return BattleSkillCastBlockReasonKind.MeleeWeaponRequired;
        }
        if (
            combatProfile.ExcludedWeaponFamilies.Count > 0
            && Contains(
                combatProfile.ExcludedWeaponFamilies,
                unitView.WeaponFamily
            )
        )
        {
            return BattleSkillCastBlockReasonKind.ExcludedWeaponFamily;
        }
        if (
            combatProfile.ExcludedWeaponTypeIds.Count > 0
            && Contains(
                combatProfile.ExcludedWeaponTypeIds,
                unitView.WeaponProfileTypeId
            )
        )
        {
            return BattleSkillCastBlockReasonKind.ExcludedWeaponType;
        }
        return BattleSkillCastBlockReasonKind.None;
    }

    private static bool Contains(
        IReadOnlyList<StringName> values,
        StringName expected
    )
    {
        foreach (StringName value in values)
        {
            if (value == expected)
            {
                return true;
            }
        }
        return false;
    }
}
