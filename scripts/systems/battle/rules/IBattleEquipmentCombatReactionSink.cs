using System.Collections.Generic;
using Godot;

internal interface IBattleEquipmentCombatReactionSink
{
    bool ResolveAttackCheck(BattleEquipmentAbilityAttackCheckContext context);

    BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
        BattleEquipmentAbilityAfterHitContext context
    );

    BattleEquipmentAbilityAfterHitResult ResolveHitReceived(
        BattleEquipmentAbilityAfterHitContext context
    );

    IReadOnlyList<StringName> RefreshEquipmentProjectionAfterDurabilityDestruction(
        BattleUnitState targetUnit,
        BattleEventBatch batch = null
    );

    bool ResolveDamageApplied(BattleEquipmentAbilityDamageAppliedContext context);
}
