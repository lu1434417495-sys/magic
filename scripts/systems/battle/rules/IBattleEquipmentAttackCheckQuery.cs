using System.Collections.Generic;

internal interface IBattleEquipmentAttackCheckQuery
{
    IReadOnlyList<BattleAttackRollModifierSpec> CollectAttackRollModifierCandidates(
        BattleAttackCheckPolicyContext context
    );

    EquipmentAttackDefenseAdjustment CollectAttackDefenseAdjustment(
        BattleAttackCheckPolicyContext context
    );

    BattleEquipmentAbilityCriticalHitOverrideResult ResolveCriticalHitOverride(
        BattleAttackCheckPolicyContext context
    );
}
