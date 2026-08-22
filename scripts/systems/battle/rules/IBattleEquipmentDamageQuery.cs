using System.Collections.Generic;
using Godot;

internal interface IBattleEquipmentDamageQuery
{
    IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceOnHit(
        BattleEquipmentAbilityBonusDamageDiceContext context
    );

    StringName ResolveDamageRollModeOverride(
        BattleEquipmentAbilityDamageRollModeContext context
    );

    IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
        BattleEquipmentAbilityDamageReductionContext context
    );

    IReadOnlyList<BattleEquipmentAbilityMitigationAuraResult> CollectMitigationAuras(
        BattleEquipmentAbilityMitigationAuraContext context
    );

    IReadOnlyList<BattleEquipmentAbilityMitigationTierResult> CollectMitigationTiers(
        BattleEquipmentAbilityMitigationTierContext context
    );

    IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceForEffect(
        BattleEquipmentAbilityDirectDamageContext context
    );
}
