using System.Collections.Generic;
using Godot;

internal interface IBattleEquipmentAbilityReactionService
{
    List<BattleAttackRollModifierSpec> CollectAttackRollModifierCandidates(
        BattleAttackCheckPolicyContext context
    );

    EquipmentAttackDefenseAdjustment CollectAttackDefenseAdjustment(
        BattleAttackCheckPolicyContext context
    );

    BattleEquipmentAbilityCriticalHitOverrideResult ResolveCriticalHitOverride(
        BattleAttackCheckPolicyContext context
    );

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

    IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceOnHit(
        BattleEquipmentAbilityBonusDamageDiceContext context
    );

    StringName ResolveDamageRollModeOverride(
        BattleEquipmentAbilityDamageRollModeContext context
    );

    IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
        BattleEquipmentAbilityDamageReductionContext context
    );

    bool ResolveDamageApplied(BattleEquipmentAbilityDamageAppliedContext context);

    BattleState GetBattleState();
}
