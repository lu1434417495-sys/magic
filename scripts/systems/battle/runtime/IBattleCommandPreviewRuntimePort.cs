using System.Collections.Generic;
using Godot;

/// <see cref="BattleCommandPreviewService"/> 需要的运行时能力面。
///
/// 与 <see cref="IBattleTimelineRuntimePort"/> / <see cref="IBattleChargeRuntimePort"/> 同一套模式。
/// 这里全部是行为，没有服务访问器：预览路径对每个下层协作者都只有一两处调用，
/// 包成行为反而更省（见 <see cref="IBattleChargeRuntimePort"/> 里的分界线说明）。
///
/// <see cref="ValidateSkillCommandEntryAccess"/> 尤其值得留意：它在 hub 侧一次性吃掉了
/// 四个内容索引（skill catalog / skill definition / equipment ability binding / item def）
/// 加 <c>_state</c> 加世界步数，消费者因此完全不必知道这些索引的存在。
internal interface IBattleCommandPreviewRuntimePort
{
    void EnsureSidecarsReady();

    BattleState GetBattleState();

    void PreviewCancelCast(BattleCommand command, BattlePreview preview);

    void PreviewObjectiveInteraction(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    );

    bool IsMovementBlocked(BattleUnitReadView activeUnit);

    BattleMovePathResult ResolveMovePathResult(BattleUnitReadView activeUnit, Vector2I targetCoord);

    IReadOnlyList<Vector2I> GetUnitFootprintCoords(
        BattleUnitReadView activeUnit,
        Vector2I anchorCoord
    );

    void PreviewSkillCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    );

    bool ResolveEquipmentGrantedSkillUsed(BattleEquipmentAbilityGrantedSkillUsedContext context);

    BattleSkillAccessResult ValidateSkillCommandEntryAccess(
        BattleCommand command,
        BattleSkillAvailabilityConsumer consumer
    );

    void PreviewChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    );
}
