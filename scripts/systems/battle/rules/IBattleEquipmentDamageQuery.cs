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
}
