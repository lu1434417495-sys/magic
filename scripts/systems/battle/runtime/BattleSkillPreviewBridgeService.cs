using System.Collections.Generic;
using Godot;

/// 把 <see cref="IBattleSkillPreviewRuntimePort"/> 实现到 <see cref="BattleRuntimeModule"/> 上。
///
/// 作为 <see cref="BattleRuntimeModuleBorrower"/>，它经基类的 <c>_runtime</c> 访问 hub；
/// 穿透 hub 的行为被限制在本文件内，<see cref="BattleSkillPreviewService"/> 自身不再引用
/// BattleRuntimeModule。与 <see cref="BattleTimelineBridgeService"/> 同一套模式。
///
/// 全部成员为显式接口实现：本类不提供 hub 之外的额外能力面。
internal sealed class BattleSkillPreviewBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleSkillPreviewRuntimePort
{
    BattleState IBattleSkillPreviewRuntimePort.GetStateForReadOnlyRules() => _runtime?._state;
    BattleAttackCheckPolicyService IBattleSkillPreviewRuntimePort.GetAttackCheckPolicyService() =>
        _runtime?.GetAttackCheckPolicyService();

    BattleRepeatAttackResolver IBattleSkillPreviewRuntimePort.GetRepeatAttackResolver() =>
        _runtime?._repeat_attack_resolver;

    bool IBattleSkillPreviewRuntimePort.IsMovementBlocked(BattleUnitState sourceUnit) =>
        _runtime?._movement_service?.IsMovementBlocked(sourceUnit) == true;

    string IBattleSkillPreviewRuntimePort.GetSkillCommandBlockReason(
        BattleUnitReadView unit,
        SkillDefinition skill,
        CombatCastVariantDefinition variant
    ) => _runtime?._get_skill_command_block_reason(unit, skill, variant) ?? "";

    string IBattleSkillPreviewRuntimePort.GetTargetSlotCostBlockReason(
        BattleUnitReadView unit,
        SkillDefinition skill,
        int targetSlotCount
    ) => _runtime?._get_target_slot_cost_block_reason(unit, skill, targetSlotCount) ?? "";

    CombatSkillResourceCosts IBattleSkillPreviewRuntimePort.GetEffectiveSkillResourceCosts(
        BattleUnitReadView unit,
        SkillDefinition skill,
        int targetSlotCount
    ) => _runtime?._skill_turn_resolver?.GetEffectiveSkillResourceCosts(unit, skill, targetSlotCount)
        ?? CombatSkillResourceCosts.Zero;

    BattlePreparedChainDamage IBattleSkillPreviewRuntimePort.BuildPreparedChainPreviewPlan(
        BattleUnitReadView source,
        BattleUnitReadView target,
        SkillDefinition skill,
        IReadOnlyList<CombatEffectDefinition> effects,
        bool backlashTriggered
    ) => BattleChainDamagePreparationRules.BuildPreparedPlan(
        _runtime?._state,
        source.UnsafeUnitForReadOnlyRules,
        target.UnsafeUnitForReadOnlyRules,
        skill,
        effects,
        backlashTriggered
    );

    BattleGridService IBattleSkillPreviewRuntimePort.GetGridService() =>
        _runtime?.GetGridService();

    BattleLayeredBarrierService IBattleSkillPreviewRuntimePort.GetLayeredBarrierService() =>
        _runtime?._layered_barrier_service;

    BattleSkillResolutionRules IBattleSkillPreviewRuntimePort.GetSkillResolutionRules() =>
        _runtime?._skill_resolution_rules;

    BattleChargeResolver IBattleSkillPreviewRuntimePort.GetChargeResolver() =>
        _runtime?._charge_resolver;

    SkillDefinition IBattleSkillPreviewRuntimePort.GetSkillDefinition(StringName skillId) =>
        _runtime?.GetSkillDefinitionTyped(skillId);

    IReadOnlyDictionary<StringName, ItemDefinition> IBattleSkillPreviewRuntimePort.GetItemDefIndex() =>
        _runtime?.GetItemDefIndexTyped();

    BattleSpecialProfileGateResult IBattleSkillPreviewRuntimePort.PreviewSpecialProfileSkill(
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitReadView activeUnit
    )
    {
        BattleRuntimeModule runtime = _runtime;
        return runtime?._special_profile_gate?.PreviewSkill(
            skillDefinition,
            command,
            activeUnit,
            runtime._state
        );
    }

    bool IBattleSkillPreviewRuntimePort.PopulateMeteorSwarmPreview(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        BattlePreview preview
    )
    {
        BattleMeteorSwarmResolver resolver = _runtime?._meteor_swarm_resolver;
        if (resolver == null)
        {
            return false;
        }
        resolver.PopulatePreview(activeUnit, command, skillDefinition, preview);
        return true;
    }

    BattleSourceRetreatPlan IBattleSkillPreviewRuntimePort.BuildSourceRetreatPlan(
        BattleUnitReadView sourceUnit,
        Vector2I targetCoord,
        Vector2I direction,
        int distance
    ) =>
        _runtime
            ?._movement_service
            ?.BuildSourceRetreatPlan(sourceUnit, targetCoord, direction, distance);

    BattleApproachAttackPlan IBattleSkillPreviewRuntimePort.BuildApproachAttackPlan(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition
    ) =>
        _runtime
            ?._movement_service
            ?.BuildApproachAttackPlan(sourceUnit, targetUnit, skillDefinition);

    BattleTargetCollectionResult IBattleSkillPreviewRuntimePort.CollectCombatProfileTargetCoords(
        BattleState state,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitReadView sourceUnit,
        IEnumerable<BattleUnitReadView> targetUnits,
        int skillLevel
    )
    {
        BattleRuntimeModule runtime = _runtime;
        return runtime?._target_collection_service?.CollectCombatProfileTargetCoords(
            state,
            runtime.GetGridService(),
            sourceCoord,
            combatProfile,
            targetCoords,
            sourceUnit,
            targetUnits,
            skillLevel
        );
    }

    BattleGroundSkillValidationResult IBattleSkillPreviewRuntimePort.ValidateGroundSkillCommandResult(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    ) =>
        _runtime?.ValidateGroundSkillCommandResultTyped(
            activeUnit,
            skillDefinition,
            castVariantDefinition,
            command
        ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");

    IReadOnlyList<Vector2I> IBattleSkillPreviewRuntimePort.BuildGroundEffectCoords(
        SkillDefinition skillDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        Vector2I sourceCoord,
        BattleUnitReadView activeUnit,
        CombatCastVariantDefinition castVariantDefinition
    ) =>
        _runtime?.BuildGroundEffectCoordsTyped(
            skillDefinition,
            targetCoords,
            sourceCoord,
            activeUnit,
            castVariantDefinition
        );

    IReadOnlyList<CombatEffectDefinition> IBattleSkillPreviewRuntimePort.CollectGroundUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    ) =>
        _runtime?.CollectGroundUnitEffectDefinitionsTyped(
            skillDefinition,
            castVariantDefinition,
            activeUnit
        );

    IReadOnlyList<CombatEffectDefinition> IBattleSkillPreviewRuntimePort.CollectGroundTerrainEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitReadView activeUnit
    ) =>
        _runtime?.CollectGroundTerrainEffectDefinitionsTyped(
            skillDefinition,
            castVariantDefinition,
            activeUnit
        );

    IReadOnlyList<StringName> IBattleSkillPreviewRuntimePort.CollectGroundPreviewUnitIds(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    ) =>
        _runtime?.CollectGroundPreviewUnitIdsTyped(
            sourceUnit,
            skillDefinition,
            effectDefinitions,
            effectCoords
        );

}
