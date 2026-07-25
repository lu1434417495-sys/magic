using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleAiLayeredBarrierProjection
{
    public StringName profile_id { get; init; } = "";
    public Vector2I anchor_coord { get; init; } = new(-1, -1);
    public int radius_cells { get; init; }
    public StringName area_pattern { get; init; } = "";
    public int projected_layer_count { get; init; }
    public int projected_duration_tu { get; init; }
    public int replacement_threshold_tu { get; init; }
    public int same_profile_barrier_count { get; init; }
    public int same_anchor_barrier_count { get; init; }
    public int strongest_same_anchor_active_layer_count { get; init; }
    public int strongest_same_anchor_broken_layer_count { get; init; }
    public int strongest_same_anchor_remaining_tu { get; init; }
    public int protected_ally_count { get; init; }
    public int enemy_inside_count { get; init; }
    public int nearby_outside_enemy_count { get; init; }
    public int nearest_enemy_distance { get; init; } = -1;
    public bool redundant_same_anchor { get; init; }
    public int utility_control_count { get; init; }
    public string reason { get; init; } = "";

    internal static BattleAiLayeredBarrierProjection Build(
        IBattleAiScoreContext context,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        StringName profileId = effectDefinition?.GetStringNameParamTyped("profile_id", "")
            ?? new StringName("");
        Vector2I anchorCoord =
            targetUnit?.GetAnchorCoord()
            ?? sourceUnit?.GetAnchorCoord()
            ?? new Vector2I(-1, -1);
        if (
            context == null
            || sourceUnit == null
            || effectDefinition == null
            || profileId == ""
            || context.barrier_profile_definitions == null
            || !context.barrier_profile_definitions.TryGetValue(
                profileId,
                out BarrierProfileDefinition profile
            )
            || profile == null
        )
        {
            return new BattleAiLayeredBarrierProjection
            {
                profile_id = profileId,
                anchor_coord = anchorCoord,
                reason = "missing_profile",
            };
        }

        int radius = effectDefinition.GetIntParamTyped("radius_cells", 0);
        if (radius <= 0)
            radius = profile.RadiusCells;
        radius = Math.Max(radius, 1);
        StringName areaPattern = effectDefinition.GetStringNameParamTyped("area_pattern", "");
        if (areaPattern == "")
            areaPattern = profile.AreaPattern;
        int projectedDuration = effectDefinition.DurationTu > 0
            ? effectDefinition.DurationTu
            : profile.DurationTu;
        projectedDuration = Math.Max(projectedDuration, 1);
        int replacementThreshold = Math.Max(projectedDuration / 4, 1);

        BattleState state = context.state;
        BattleGridService grid = context.grid_service;
        var areaCoords = new HashSet<Vector2I>();
        if (state != null && grid != null)
        {
            foreach (
                Vector2I coord in grid.GetAreaCoords(
                    state,
                    anchorCoord,
                    areaPattern,
                    radius,
                    Vector2I.Zero
                )
            )
            {
                areaCoords.Add(coord);
            }
        }
        if (areaCoords.Count == 0)
            areaCoords.Add(anchorCoord);

        int protectedAllyCount = 0;
        int enemyInsideCount = 0;
        int nearbyOutsideEnemyCount = 0;
        int nearestEnemyDistance = int.MaxValue;
        foreach (BattleUnitState unit in state?.Units() ?? Array.Empty<BattleUnitState>())
        {
            if (unit == null || !unit.IsAlive())
                continue;
            bool inside = false;
            foreach (Vector2I occupiedCoord in unit.GetOccupiedCoordsTyped())
            {
                if (areaCoords.Contains(occupiedCoord))
                {
                    inside = true;
                    break;
                }
            }
            if (unit.faction_id == sourceUnit.faction_id)
            {
                if (inside)
                    protectedAllyCount += 1;
                continue;
            }

            int distance = grid != null
                ? grid.GetDistanceFromUnitToCoord(unit, anchorCoord)
                : BattleGridDistanceService.GetDistance(
                    unit.GetAnchorCoord(),
                    anchorCoord
                );
            nearestEnemyDistance = Math.Min(nearestEnemyDistance, Math.Max(distance, 0));
            if (inside)
            {
                enemyInsideCount += 1;
            }
            else if (distance <= radius + 2)
            {
                nearbyOutsideEnemyCount += 1;
            }
        }

        int sameProfileCount = 0;
        int sameAnchorCount = 0;
        int strongestActiveLayers = 0;
        int strongestBrokenLayers = 0;
        int strongestRemainingTu = 0;
        bool redundantSameAnchor = false;
        foreach (
            BattleBarrierInstanceState barrier in state?.LayeredBarrierStore.ValuesSorted()
                ?? Array.Empty<BattleBarrierInstanceState>()
        )
        {
            if (
                barrier == null
                || barrier.IsEmpty
                || barrier.RemainingTu <= 0
                || barrier.ProfileId != profileId
            )
            {
                continue;
            }
            sameProfileCount += 1;
            if (barrier.AnchorCoord != anchorCoord)
                continue;
            sameAnchorCount += 1;

            int activeLayers = 0;
            int brokenLayers = 0;
            foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
            {
                if (layer == null)
                    continue;
                if (layer.Broken)
                    brokenLayers += 1;
                else
                    activeLayers += 1;
            }
            if (
                activeLayers > strongestActiveLayers
                || (
                    activeLayers == strongestActiveLayers
                    && barrier.RemainingTu > strongestRemainingTu
                )
            )
            {
                strongestActiveLayers = activeLayers;
                strongestBrokenLayers = brokenLayers;
                strongestRemainingTu = barrier.RemainingTu;
            }
            if (
                activeLayers >= profile.Layers.Count
                && barrier.RemainingTu >= replacementThreshold
            )
            {
                redundantSameAnchor = true;
            }
        }

        bool positionRelevant = nearbyOutsideEnemyCount > 0;
        int utilityControlCount = !redundantSameAnchor && positionRelevant ? 1 : 0;
        string reason = redundantSameAnchor
            ? "redundant_same_anchor"
            : !positionRelevant
                ? "no_nearby_outside_enemy"
                : "tactical_boundary";
        return new BattleAiLayeredBarrierProjection
        {
            profile_id = profileId,
            anchor_coord = anchorCoord,
            radius_cells = radius,
            area_pattern = areaPattern,
            projected_layer_count = profile.Layers.Count,
            projected_duration_tu = projectedDuration,
            replacement_threshold_tu = replacementThreshold,
            same_profile_barrier_count = sameProfileCount,
            same_anchor_barrier_count = sameAnchorCount,
            strongest_same_anchor_active_layer_count = strongestActiveLayers,
            strongest_same_anchor_broken_layer_count = strongestBrokenLayers,
            strongest_same_anchor_remaining_tu = strongestRemainingTu,
            protected_ally_count = protectedAllyCount,
            enemy_inside_count = enemyInsideCount,
            nearby_outside_enemy_count = nearbyOutsideEnemyCount,
            nearest_enemy_distance = nearestEnemyDistance == int.MaxValue
                ? -1
                : nearestEnemyDistance,
            redundant_same_anchor = redundantSameAnchor,
            utility_control_count = utilityControlCount,
            reason = reason,
        };
    }

    internal BattleAiLayeredBarrierProjection Clone() =>
        new()
        {
            profile_id = profile_id,
            anchor_coord = anchor_coord,
            radius_cells = radius_cells,
            area_pattern = area_pattern,
            projected_layer_count = projected_layer_count,
            projected_duration_tu = projected_duration_tu,
            replacement_threshold_tu = replacement_threshold_tu,
            same_profile_barrier_count = same_profile_barrier_count,
            same_anchor_barrier_count = same_anchor_barrier_count,
            strongest_same_anchor_active_layer_count = strongest_same_anchor_active_layer_count,
            strongest_same_anchor_broken_layer_count = strongest_same_anchor_broken_layer_count,
            strongest_same_anchor_remaining_tu = strongest_same_anchor_remaining_tu,
            protected_ally_count = protected_ally_count,
            enemy_inside_count = enemy_inside_count,
            nearby_outside_enemy_count = nearby_outside_enemy_count,
            nearest_enemy_distance = nearest_enemy_distance,
            redundant_same_anchor = redundant_same_anchor,
            utility_control_count = utility_control_count,
            reason = reason ?? "",
        };

    internal Dictionary<string, object> ToTraceDictionary() =>
        new(StringComparer.Ordinal)
        {
            ["profile_id"] = profile_id.ToString(),
            ["anchor_coord"] = anchor_coord,
            ["radius_cells"] = radius_cells,
            ["area_pattern"] = area_pattern.ToString(),
            ["projected_layer_count"] = projected_layer_count,
            ["projected_duration_tu"] = projected_duration_tu,
            ["replacement_threshold_tu"] = replacement_threshold_tu,
            ["same_profile_barrier_count"] = same_profile_barrier_count,
            ["same_anchor_barrier_count"] = same_anchor_barrier_count,
            ["strongest_same_anchor_active_layer_count"] =
                strongest_same_anchor_active_layer_count,
            ["strongest_same_anchor_broken_layer_count"] =
                strongest_same_anchor_broken_layer_count,
            ["strongest_same_anchor_remaining_tu"] = strongest_same_anchor_remaining_tu,
            ["protected_ally_count"] = protected_ally_count,
            ["enemy_inside_count"] = enemy_inside_count,
            ["nearby_outside_enemy_count"] = nearby_outside_enemy_count,
            ["nearest_enemy_distance"] = nearest_enemy_distance,
            ["redundant_same_anchor"] = redundant_same_anchor,
            ["utility_control_count"] = utility_control_count,
            ["reason"] = reason ?? "",
        };
}
