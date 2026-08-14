using System.Collections.Generic;
using Godot;

/// 把 <see cref="IBattleCommandPreviewRuntimePort"/> 实现到 <see cref="BattleRuntimeModule"/> 上。
///
/// 作为 <see cref="BattleRuntimeModuleBorrower"/>，它经基类的 <c>_runtime</c> 访问 hub；
/// 穿透 hub 的行为被限制在本文件内，<see cref="BattleCommandPreviewService"/> 自身不再引用
/// BattleRuntimeModule。与 <see cref="BattleTimelineBridgeService"/> 同一套模式。
///
/// 全部成员为显式接口实现：本类不提供 hub 之外的额外能力面。
internal sealed class BattleCommandPreviewBridgeService
    : BattleRuntimeModuleBorrower,
        IBattleCommandPreviewRuntimePort
{
    void IBattleCommandPreviewRuntimePort.EnsureSidecarsReady() =>
        _runtime?._ensure_sidecars_ready();

    BattleState IBattleCommandPreviewRuntimePort.GetBattleState() => _runtime?._state;

    void IBattleCommandPreviewRuntimePort.PreviewCancelCast(
        BattleCommand command,
        BattlePreview preview
    ) => _runtime?._casting_time_service?.PreviewCancelCast(command, preview);

    void IBattleCommandPreviewRuntimePort.PreviewObjectiveInteraction(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    ) => _runtime?.PreviewObjectiveInteraction(activeUnit, command, preview);

    bool IBattleCommandPreviewRuntimePort.IsMovementBlocked(BattleUnitReadView activeUnit) =>
        _runtime?._movement_service?.IsMovementBlocked(activeUnit) == true;

    BattleMovePathResult IBattleCommandPreviewRuntimePort.ResolveMovePathResult(
        BattleUnitReadView activeUnit,
        Vector2I targetCoord
    ) => _runtime?._movement_service?.ResolveMovePathResultTyped(activeUnit, targetCoord);

    IReadOnlyList<Vector2I> IBattleCommandPreviewRuntimePort.GetUnitFootprintCoords(
        BattleUnitReadView activeUnit,
        Vector2I anchorCoord
    ) =>
        _runtime?._grid_service?.GetUnitTargetCoords(activeUnit, anchorCoord)
        ?? (IReadOnlyList<Vector2I>)System.Array.Empty<Vector2I>();

    void IBattleCommandPreviewRuntimePort.PreviewSkillCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    ) => _runtime?._preview_skill_command(activeUnit, command, preview);

    bool IBattleCommandPreviewRuntimePort.ResolveEquipmentGrantedSkillUsed(
        BattleEquipmentAbilityGrantedSkillUsedContext context
    ) => _runtime?._equipment_ability_runtime_service?.ResolveGrantedSkillUsed(context) == true;

    BattleSkillAccessResult IBattleCommandPreviewRuntimePort.ValidateSkillCommandEntryAccess(
        BattleCommand command,
        BattleSkillAvailabilityConsumer consumer
    )
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return BattleSkillAccessResult.Deny("runtime_unbound", "技能或目标无效。");
        }
        BattleSkillAvailabilityService service = new(
            runtime._skillCatalog,
            runtime._skillDefinitionIndex,
            runtime._equipmentAbilityBindingIndex,
            runtime._itemDefIndex
        );
        return service.ValidateSkillCommandEntryAccess(
            runtime._state,
            command,
            consumer,
            runtime.GetBattleWorldStep()
        );
    }

    void IBattleCommandPreviewRuntimePort.PreviewChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    ) => _runtime?._change_equipment_resolver?.PreviewCommand(activeUnit, command, preview);
}
