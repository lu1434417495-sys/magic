using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class BattleWindPushRules
{
    internal static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        return (effectDefinitions ?? Array.Empty<CombatEffectDefinition>()).FirstOrDefault(
            effect => effect?.EffectKind == BattleEffectKind.ForcedMove
                && effect.ForcedMoveModeKind == BattleForcedMoveMode.WindPush
        );
    }

    internal static bool IsPureWindPushEffectSet(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        if (effectDefinitions == null || effectDefinitions.Count == 0)
            return false;
        foreach (CombatEffectDefinition effect in effectDefinitions)
        {
            if (
                effect == null
                || effect.EffectKind != BattleEffectKind.ForcedMove
                || effect.ForcedMoveModeKind != BattleForcedMoveMode.WindPush
            )
            {
                return false;
            }
        }
        return true;
    }

    internal static BattleForcedMovePreviewData BuildPreview(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        IReadOnlyList<StringName> targetUnitIds,
        Vector2I rawDirection,
        StringName skillId
    )
    {
        Vector2I direction = NormalizeAxisDirection(rawDirection);
        if (
            state == null
            || gridService == null
            || sourceUnit == null
            || effectDefinition == null
            || direction == Vector2I.Zero
        )
        {
            return null;
        }

        BattleDetachedPreviewState detached = BattleDetachedPreviewState.Create(state);
        BattleState previewState = detached.State;
        var orderedTargetIds = new List<StringName>();
        var seenTargetIds = new HashSet<StringName>();
        foreach (StringName targetUnitId in targetUnitIds ?? Array.Empty<StringName>())
        {
            if (
                targetUnitId != ""
                && seenTargetIds.Add(targetUnitId)
                && state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit)
                && targetUnit != null
                && targetUnit.IsAlive()
            )
            {
                orderedTargetIds.Add(targetUnitId);
            }
        }
        orderedTargetIds.Sort(
            (leftId, rightId) =>
            {
                BattleUnitState left = state.GetUnit(leftId);
                BattleUnitState right = state.GetUnit(rightId);
                int leftProjection = Dot(left?.GetAnchorCoord() ?? Vector2I.Zero, direction);
                int rightProjection = Dot(right?.GetAnchorCoord() ?? Vector2I.Zero, direction);
                int projectionComparison = rightProjection.CompareTo(leftProjection);
                return projectionComparison != 0
                    ? projectionComparison
                    : string.CompareOrdinal(leftId.ToString(), rightId.ToString());
            }
        );

        var targetPreviews = new List<BattleForcedMoveTargetPreviewData>();
        foreach (StringName targetUnitId in orderedTargetIds)
        {
            BattleUnitState targetUnit = state.GetUnit(targetUnitId);
            BattleUnitState previewTarget = detached.GetUnit(targetUnitId);
            if (targetUnit == null || previewTarget == null)
                continue;

            int maximumBodySize = Math.Max(
                effectDefinition.ForcedMoveMaxTargetBodySize,
                0
            );
            string blockReason = ResolveEligibilityBlockReason(
                sourceUnit,
                targetUnit,
                maximumBodySize
            );
            BattleSaveProbabilityResult saveProbability =
                BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    BattleSaveContext.ForSkill(skillId)
                );
            int failureProbabilityBasisPoints = saveProbability.HasSave
                ? saveProbability.FailureProbabilityBasisPoints
                : 10000;
            if (failureProbabilityBasisPoints <= 0 && string.IsNullOrEmpty(blockReason))
                blockReason = saveProbability.Immune ? "免疫本次力量豁免效果" : "豁免不会失败";

            Vector2I sourceCoord = previewTarget.GetAnchorCoord();
            int movedDistance = 0;
            if (string.IsNullOrEmpty(blockReason))
            {
                int maximumDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0);
                for (int step = 0; step < maximumDistance; step++)
                {
                    Vector2I fromCoord = previewTarget.GetAnchorCoord();
                    Vector2I toCoord = fromCoord + direction;
                    if (
                        !gridService.CanTraverse(
                            previewState,
                            fromCoord,
                            toCoord,
                            previewTarget
                        )
                        || barrierService?.HasUnitBoundaryBarrier(
                            previewTarget,
                            fromCoord,
                            toCoord
                        ) == true
                        || !gridService.MoveUnit(previewState, previewTarget, toCoord)
                    )
                    {
                        break;
                    }
                    movedDistance++;
                }
                if (movedDistance <= 0)
                    blockReason = "风向路径立即受阻";
            }

            targetPreviews.Add(
                new BattleForcedMoveTargetPreviewData
                {
                    TargetUnitId = targetUnitId,
                    TargetDisplayName = targetUnit.display_name ?? targetUnitId.ToString(),
                    SourceCoord = sourceCoord,
                    DestinationCoord = previewTarget.GetAnchorCoord(),
                    Distance = movedDistance,
                    MaximumDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0),
                    TargetBodySize = targetUnit.GetBodySize(),
                    MaximumTargetBodySize = maximumBodySize,
                    SaveDc = saveProbability.Dc,
                    SaveAbility = saveProbability.Ability,
                    SaveTag = saveProbability.SaveTag,
                    SaveSuccessProbabilityBasisPoints = saveProbability.HasSave
                        ? saveProbability.SuccessProbabilityBasisPoints
                        : 0,
                    SaveFailureProbabilityBasisPoints = failureProbabilityBasisPoints,
                    CanMoveOnFailedSave = movedDistance > 0,
                    BlockReason = blockReason,
                }
            );
        }

        int movableTargetCount = targetPreviews.Count(target =>
            target.CanMoveOnFailedSave && target.SaveFailureProbabilityBasisPoints > 0
        );
        int saveDc = targetPreviews.Select(target => target.SaveDc).FirstOrDefault(value => value > 0);
        string summary = movableTargetCount > 0
            ? $"强风预览：{movableTargetCount} 名目标在力量豁免失败时可沿风向移动，DC {saveDc}；位移逐格受单位、地形与屏障阻挡，并逐格触发地形接触。"
            : "强风预览：当前没有可实际推动的目标；仍可空放，但会照常消耗资源并进入冷却。";
        return new BattleForcedMovePreviewData
        {
            Mode = BattleTypedNames.ToStringName(BattleForcedMoveMode.WindPush),
            SourceCoord = sourceUnit.GetAnchorCoord(),
            DestinationCoord = sourceUnit.GetAnchorCoord() + direction,
            MaximumDistance = Math.Max(effectDefinition.ForcedMoveDistance, 0),
            MaximumTargetBodySize = Math.Max(
                effectDefinition.ForcedMoveMaxTargetBodySize,
                0
            ),
            AppliesLandingContact = true,
            AppliesContactPerEnteredCell = true,
            Targets = targetPreviews.AsReadOnly(),
            SummaryText = summary,
        };
    }

    private static string ResolveEligibilityBlockReason(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int maximumBodySize
    )
    {
        if (maximumBodySize <= 0)
            return "未配置可推动体型上限";
        if (targetUnit.GetBodySize() > maximumBodySize)
            return $"体型 {targetUnit.GetBodySize()} 超过上限 {maximumBodySize}";
        if (BattleTemporalStatusService.HasTimeStasis(targetUnit))
            return "处于时间静滞";
        if (BlocksEnemyForcedMove(sourceUnit, targetUnit))
            return "免疫敌方强制位移";
        return "";
    }

    private static bool BlocksEnemyForcedMove(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (
            sourceUnit == null
            || targetUnit == null
            || sourceUnit.unit_id == targetUnit.unit_id
            || sourceUnit.faction_id == targetUnit.faction_id
        )
        {
            return false;
        }
        foreach (BattleStatusEffectState status in targetUnit.GetStatusEffectsTyped())
        {
            if (
                status != null
                && status.stacks > 0
                && status.forced_move_immune
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int Dot(Vector2I coord, Vector2I direction) =>
        coord.X * direction.X + coord.Y * direction.Y;

    private static Vector2I NormalizeAxisDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
            return Vector2I.Zero;
        int absX = Math.Abs(direction.X);
        int absY = Math.Abs(direction.Y);
        if (absX >= absY && absX > 0)
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        return absY > 0 ? new Vector2I(0, direction.Y > 0 ? 1 : -1) : Vector2I.Zero;
    }
}
