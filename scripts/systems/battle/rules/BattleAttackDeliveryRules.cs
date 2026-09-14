using System.Collections.Generic;

internal static class BattleAttackDeliveryRules
{
    internal static bool IncludesWeaponDamage(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    ) =>
        BattleUnitSkillDefinitionExecutionRules
            .IncludesWeaponDamage(effectDefinitions);

    internal static BattleAttackDeliveryKind Resolve(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleUnitWeaponProjectionReadView weaponProjection
    )
    {
        if (!IncludesWeaponDamage(effectDefinitions))
            return BattleAttackDeliveryKind.NonWeapon;
        if (
            !weaponProjection.OwnerPresent
            || weaponProjection.Values.AttackRange <= 0
        )
        {
            return BattleAttackDeliveryKind.Unknown;
        }
        return BattleWeaponRangeTypeNames.Parse(
            weaponProjection.Values.RangeType
        ) switch
        {
            BattleWeaponRangeTypeKind.Melee =>
                BattleAttackDeliveryKind.MeleeWeapon,
            BattleWeaponRangeTypeKind.Ranged =>
                BattleAttackDeliveryKind.RangedWeapon,
            _ => BattleAttackDeliveryKind.Unknown,
        };
    }
}
