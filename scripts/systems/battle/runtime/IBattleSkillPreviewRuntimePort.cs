using System.Collections.Generic;
using Godot;

/// Runtime queries consumed by preview. Execution orchestration is not a preview dependency.
internal interface IBattleSkillPreviewRuntimePort
{
    // Borrowed only for rules that have not yet adopted BattleStateReadView.
    BattleState GetStateForReadOnlyRules();
    BattleAttackCheckPolicyService GetAttackCheckPolicyService();
    BattleRepeatAttackResolver GetRepeatAttackResolver();
    bool IsMovementBlocked(BattleUnitState sourceUnit);
    string GetSkillCommandBlockReason(
        BattleUnitReadView unit,
        SkillDefinition skill,
        CombatCastVariantDefinition variant
    );
    string GetTargetSlotCostBlockReason(
        BattleUnitReadView unit,
        SkillDefinition skill,
        int targetSlotCount
    );
    CombatSkillResourceCosts GetEffectiveSkillResourceCosts(
        BattleUnitReadView unit,
        SkillDefinition skill,
        int targetSlotCount = 1
    );
    BattlePreparedChainDamage BuildPreparedChainPreviewPlan(
        BattleUnitReadView source,
        BattleUnitReadView target,
        SkillDefinition skill,
        IReadOnlyList<CombatEffectDefinition> effects,
        bool backlashTriggered
    );

    BattleGridService GetGridService();

    BattleLayeredBarrierService GetLayeredBarrierService();

    BattleSkillResolutionRules GetSkillResolutionRules();

    BattleChargeResolver GetChargeResolver();

    SkillDefinition GetSkillDefinition(StringName skillId);

    IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefIndex();

    BattleSpecialProfileGateResult PreviewSpecialProfileSkill(
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitReadView activeUnit
    );

    /// 返回 false 表示禁咒结算器尚未接入（原先是 `_meteor_swarm_resolver == null` 分支）。
    bool PopulateMeteorSwarmPreview(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        BattlePreview preview
    );

    BattleSourceRetreatPlan BuildSourceRetreatPlan(
        BattleUnitReadView sourceUnit,
        Vector2I targetCoord,
        Vector2I direction,
        int distance
    );

    BattleApproachAttackPlan BuildApproachAttackPlan(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition
    );

    BattleTargetCollectionResult CollectCombatProfileTargetCoords(
        BattleState state,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitReadView sourceUnit,
        IEnumerable<BattleUnitReadView> targetUnits,
        int skillLevel
    );

    BattleGroundSkillValidationResult ValidateGroundSkillCommandResult(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    );

    IReadOnlyList<Vector2I> BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    );

    IReadOnlyList<CombatEffectDefinition> CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    );

    IReadOnlyList<CombatEffectDefinition> CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    );

    IReadOnlyList<StringName> CollectGroundPreviewUnitIds(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    );

}
