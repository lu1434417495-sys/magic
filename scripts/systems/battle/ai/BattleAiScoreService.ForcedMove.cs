using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private void PopulateForcedMovePositionMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context
    )
    {
        BattleForcedMovePreviewData forcedMove = scoreInput?.preview?.ForcedMovePreviewTyped;
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        BattleGridService gridService = ContextGridService(context);
        if (
            forcedMove == null
            || state == null
            || actor == null
            || gridService == null
        )
        {
            return;
        }
        if (forcedMove.Mode == BattleTypedNames.ForcedMoveWindPush)
        {
            PopulateWindPushPositionMetrics(
                scoreInput,
                forcedMove,
                state,
                actor,
                gridService
            );
            return;
        }
        if (
            forcedMove.Mode != BattleTypedNames.ForcedMoveAirbornePull
            || !state.TryGetUnitTyped(forcedMove.TargetUnitId, out BattleUnitState targetUnit)
            || targetUnit == null
        )
        {
            return;
        }

        scoreInput.forced_move_distance = Math.Max(forcedMove.Distance, 0);
        int originEngagement = CountFriendlyMeleeEngagements(
            state,
            actor,
            targetUnit,
            forcedMove.SourceCoord,
            gridService
        );
        int destinationEngagement = CountFriendlyMeleeEngagements(
            state,
            actor,
            targetUnit,
            forcedMove.DestinationCoord,
            gridService
        );
        scoreInput.forced_move_engagement_delta = destinationEngagement - originEngagement;

        scoreInput.forced_move_landing_terrain_effect_delta = Math.Max(
            CountApplicableLandingTerrainEffects(
                state,
                targetUnit,
                forcedMove.DestinationCoord,
                gridService
            )
                - CountApplicableLandingTerrainEffects(
                    state,
                    targetUnit,
                    forcedMove.SourceCoord,
                    gridService
                ),
            0
        );

        BattleCellState sourceCell = gridService.GetCellState(state, forcedMove.SourceCoord);
        BattleCellState destinationCell = gridService.GetCellState(
            state,
            forcedMove.DestinationCoord
        );
        scoreInput.forced_move_height_delta =
            (sourceCell?.current_height ?? 0) - (destinationCell?.current_height ?? 0);

        int originActorDistance = DistanceBetweenProjectedUnits(
            actor,
            actor.GetAnchorCoord(),
            targetUnit,
            forcedMove.SourceCoord,
            gridService
        );
        int actorDistance = DistanceBetweenProjectedUnits(
            actor,
            actor.GetAnchorCoord(),
            targetUnit,
            forcedMove.DestinationCoord,
            gridService
        );
        int targetWeaponRange = Math.Max(BattleRangeService.GetWeaponAttackRange(targetUnit), 1);
        if (
            actorDistance <= targetWeaponRange
            && originActorDistance > targetWeaponRange
        )
        {
            int exposureBasis =
                Math.Max(_scoreProfile?.TargetCountWeight ?? 0, 0)
                + Math.Max(_scoreProfile?.StatusWeight ?? 0, 0);
            scoreInput.forced_move_caster_exposure_penalty =
                destinationEngagement > 0 ? Math.Max(exposureBasis / 2, 1) : exposureBasis;
        }

        scoreInput.forced_move_position_score =
            scoreInput.forced_move_distance
                * Math.Max((_scoreProfile?.StatusWeight ?? 0) / 2, 1)
            + scoreInput.forced_move_engagement_delta
                * Math.Max(_scoreProfile?.TargetCountWeight ?? 0, 0)
            + scoreInput.forced_move_landing_terrain_effect_delta
                * Math.Max(_scoreProfile?.TerrainWeight ?? 0, 0)
            + scoreInput.forced_move_height_delta
                * Math.Max(_scoreProfile?.HeightWeight ?? 0, 0)
            - scoreInput.forced_move_caster_exposure_penalty;
        scoreInput.hit_payoff_score += scoreInput.forced_move_position_score;
    }

    private void PopulateWindPushPositionMetrics(
        BattleAiScoreInput scoreInput,
        BattleForcedMovePreviewData forcedMove,
        BattleState state,
        BattleUnitState actor,
        BattleGridService gridService
    )
    {
        long distanceBasisPoints = 0;
        long engagementBasisPoints = 0;
        long terrainBasisPoints = 0;
        long heightBasisPoints = 0;
        long exposureBasisPoints = 0;
        foreach (
            BattleForcedMoveTargetPreviewData targetPreview in forcedMove.Targets
                ?? Array.Empty<BattleForcedMoveTargetPreviewData>()
        )
        {
            if (
                targetPreview == null
                || !targetPreview.CanMoveOnFailedSave
                || targetPreview.SaveFailureProbabilityBasisPoints <= 0
                || !state.TryGetUnitTyped(
                    targetPreview.TargetUnitId,
                    out BattleUnitState targetUnit
                )
                || targetUnit == null
            )
            {
                continue;
            }
            int probability = Math.Clamp(
                targetPreview.SaveFailureProbabilityBasisPoints,
                0,
                10000
            );
            int originEngagement = CountFriendlyMeleeEngagements(
                state,
                actor,
                targetUnit,
                targetPreview.SourceCoord,
                gridService
            );
            int destinationEngagement = CountFriendlyMeleeEngagements(
                state,
                actor,
                targetUnit,
                targetPreview.DestinationCoord,
                gridService
            );
            int terrainDelta = Math.Max(
                CountApplicableLandingTerrainEffects(
                    state,
                    targetUnit,
                    targetPreview.DestinationCoord,
                    gridService
                )
                    - CountApplicableLandingTerrainEffects(
                        state,
                        targetUnit,
                        targetPreview.SourceCoord,
                        gridService
                    ),
                0
            );
            BattleCellState sourceCell = gridService.GetCellState(
                state,
                targetPreview.SourceCoord
            );
            BattleCellState destinationCell = gridService.GetCellState(
                state,
                targetPreview.DestinationCoord
            );
            int heightDelta =
                (sourceCell?.current_height ?? 0) - (destinationCell?.current_height ?? 0);
            int originActorDistance = DistanceBetweenProjectedUnits(
                actor,
                actor.GetAnchorCoord(),
                targetUnit,
                targetPreview.SourceCoord,
                gridService
            );
            int destinationActorDistance = DistanceBetweenProjectedUnits(
                actor,
                actor.GetAnchorCoord(),
                targetUnit,
                targetPreview.DestinationCoord,
                gridService
            );
            int targetWeaponRange = Math.Max(
                BattleRangeService.GetWeaponAttackRange(targetUnit),
                1
            );
            int exposurePenalty = 0;
            if (
                destinationActorDistance <= targetWeaponRange
                && originActorDistance > targetWeaponRange
            )
            {
                int exposureBasis =
                    Math.Max(_scoreProfile?.TargetCountWeight ?? 0, 0)
                    + Math.Max(_scoreProfile?.StatusWeight ?? 0, 0);
                exposurePenalty = destinationEngagement > 0
                    ? Math.Max(exposureBasis / 2, 1)
                    : exposureBasis;
            }

            distanceBasisPoints += (long)targetPreview.Distance * probability;
            engagementBasisPoints += (long)(destinationEngagement - originEngagement) * probability;
            terrainBasisPoints += (long)terrainDelta * probability;
            heightBasisPoints += (long)heightDelta * probability;
            exposureBasisPoints += (long)exposurePenalty * probability;
        }

        scoreInput.forced_move_distance = RoundBasisPoints(distanceBasisPoints);
        scoreInput.forced_move_engagement_delta = RoundBasisPoints(engagementBasisPoints);
        scoreInput.forced_move_landing_terrain_effect_delta = RoundBasisPoints(
            terrainBasisPoints
        );
        scoreInput.forced_move_height_delta = RoundBasisPoints(heightBasisPoints);
        scoreInput.forced_move_caster_exposure_penalty = RoundBasisPoints(
            exposureBasisPoints
        );
        scoreInput.forced_move_position_score =
            scoreInput.forced_move_distance
                * Math.Max((_scoreProfile?.StatusWeight ?? 0) / 2, 1)
            + scoreInput.forced_move_engagement_delta
                * Math.Max(_scoreProfile?.TargetCountWeight ?? 0, 0)
            + scoreInput.forced_move_landing_terrain_effect_delta
                * Math.Max(_scoreProfile?.TerrainWeight ?? 0, 0)
            + scoreInput.forced_move_height_delta
                * Math.Max(_scoreProfile?.HeightWeight ?? 0, 0)
            - scoreInput.forced_move_caster_exposure_penalty;
        scoreInput.hit_payoff_score += scoreInput.forced_move_position_score;
    }

    private static int RoundBasisPoints(long value) =>
        (int)Math.Round(value / 10000.0, MidpointRounding.AwayFromZero);

    private static int CountFriendlyMeleeEngagements(
        BattleState state,
        BattleUnitState actor,
        BattleUnitState target,
        Vector2I targetAnchor,
        BattleGridService gridService
    )
    {
        int count = 0;
        foreach (BattleUnitState ally in state.GetUnitsTyped())
        {
            if (
                ally == null
                || ally == actor
                || !ally.IsAlive()
                || ally.faction_id != actor.faction_id
                || Math.Max(BattleRangeService.GetWeaponAttackRange(ally), 1) > 1
            )
            {
                continue;
            }
            if (
                DistanceBetweenProjectedUnits(
                    ally,
                    ally.GetAnchorCoord(),
                    target,
                    targetAnchor,
                    gridService
                ) <= 1
            )
            {
                count += 1;
            }
        }
        return count;
    }

    private static int CountApplicableLandingTerrainEffects(
        BattleState state,
        BattleUnitState target,
        Vector2I targetAnchor,
        BattleGridService gridService
    )
    {
        var seen = new HashSet<StringName>();
        foreach (Vector2I coord in gridService.GetUnitTargetCoords(target, targetAnchor))
        {
            BattleCellState cell = gridService.GetCellState(state, coord);
            foreach (
                BattleTerrainEffectState terrainEffect in cell?.timed_terrain_effects
                    ?? new List<BattleTerrainEffectState>()
            )
            {
                if (
                    terrainEffect == null
                    || terrainEffect.field_instance_id == ""
                    || !state.TryGetUnitTyped(
                        terrainEffect.source_unit_id,
                        out BattleUnitState terrainSource
                    )
                    || terrainSource == null
                    || !BattleTargetTeamRules.IsUnitValidForFilter(
                        terrainSource,
                        target,
                        terrainEffect.target_team_filter
                    )
                )
                {
                    continue;
                }
                seen.Add(terrainEffect.field_instance_id);
            }
        }
        return seen.Count;
    }

    private static int DistanceBetweenProjectedUnits(
        BattleUnitState first,
        Vector2I firstAnchor,
        BattleUnitState second,
        Vector2I secondAnchor,
        BattleGridService gridService
    )
    {
        int result = int.MaxValue;
        foreach (Vector2I firstCoord in gridService.GetUnitTargetCoords(first, firstAnchor))
        {
            foreach (Vector2I secondCoord in gridService.GetUnitTargetCoords(second, secondAnchor))
            {
                result = Math.Min(result, gridService.GetDistance(firstCoord, secondCoord));
            }
        }
        return result;
    }
}
