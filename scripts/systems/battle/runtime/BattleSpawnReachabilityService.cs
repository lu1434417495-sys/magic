using System.Collections.Generic;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSpawnReachabilityService : RefCounted
{
    private const int DefaultMaxSearchNodes = 2048;

    private BattleTargetCollectionService _targetCollectionService = new();

    public Dictionary ValidateState(
        BattleState state,
        BattleGridService gridService,
        Dictionary skillDefs,
        Dictionary options = null
    )
    {
        options ??= new Dictionary();
        var result = new Dictionary
        {
            ["valid"] = true,
            ["invalid_enemy_unit_ids"] = new Godot.Collections.Array(),
            ["invalid_player_unit_ids"] = new Array(),
            ["details"] = new Array(),
        };

        if (state == null || gridService == null)
        {
            result["valid"] = false;
            result["details"]
                .AsGodotArray()
                .Add(new Dictionary { ["reason"] = "missing_state_or_grid" });
            return result;
        }

        var playerTargets = _CollectLivingUnits(
            state,
            (Godot.Collections.Array)state.ally_unit_ids
        );
        if (playerTargets.Count == 0)
        {
            result["valid"] = false;
            result["details"]
                .AsGodotArray()
                .Add(new Dictionary { ["reason"] = "no_living_player_targets" });
            return result;
        }

        foreach (var enemyUnitIdValue in state.enemy_unit_ids)
        {
            var enemyUnitId = new StringName(enemyUnitIdValue.ToString());
            var enemyUnit = state.units.ContainsKey(enemyUnitId)
                ? state.units[enemyUnitId].As<BattleUnitState>()
                : null;
            if (enemyUnit == null || !enemyUnit.is_alive)
                continue;
            var enemyResult = _ValidateAttackerUnit(
                state,
                gridService,
                skillDefs,
                enemyUnit,
                (Godot.Collections.Array)state.enemy_unit_ids,
                playerTargets,
                options
            );
            if (enemyResult.GetValueOrDefault("valid", false).AsBool())
                continue;
            result["valid"] = false;
            result["invalid_enemy_unit_ids"].AsGodotArray().Add(enemyUnit.unit_id);
            result["details"].AsGodotArray().Add(enemyResult);
        }

        if (!options.GetValueOrDefault("validate_player_to_enemy", false).AsBool())
            return result;

        var enemyTargets = _CollectLivingUnits(
            state,
            (Godot.Collections.Array)state.enemy_unit_ids
        );
        if (enemyTargets.Count == 0)
        {
            result["valid"] = false;
            result["details"]
                .AsGodotArray()
                .Add(new Dictionary { ["reason"] = "no_living_enemy_targets" });
            return result;
        }

        foreach (var playerUnitIdValue in state.ally_unit_ids)
        {
            var playerUnitId = new StringName(playerUnitIdValue.ToString());
            var playerUnit = state.units.ContainsKey(playerUnitId)
                ? state.units[playerUnitId].As<BattleUnitState>()
                : null;
            if (playerUnit == null || !playerUnit.is_alive)
                continue;
            var playerResult = _ValidateAttackerUnit(
                state,
                gridService,
                skillDefs,
                playerUnit,
                (Godot.Collections.Array)state.ally_unit_ids,
                enemyTargets,
                options
            );
            if (playerResult.GetValueOrDefault("valid", false).AsBool())
                continue;
            result["valid"] = false;
            result["invalid_player_unit_ids"].AsGodotArray().Add(playerUnit.unit_id);
            result["details"].AsGodotArray().Add(playerResult);
        }
        return result;
    }

    public Dictionary validate_state(
        BattleState state,
        BattleGridService grid_service,
        Dictionary skill_defs,
        Dictionary options = null
    )
    {
        return ValidateState(state, grid_service, skill_defs, options);
    }

    private Dictionary _ValidateAttackerUnit(
        BattleState state,
        BattleGridService gridService,
        Dictionary skillDefs,
        BattleUnitState attackerUnit,
        Array attackerUnitIds,
        Array targetUnits,
        Dictionary options
    )
    {
        var attackSkillIds = _CollectAttackSkillIds(attackerUnit, skillDefs, targetUnits);
        if (attackSkillIds.Count == 0)
        {
            return new Dictionary
            {
                ["valid"] = false,
                ["unit_id"] = attackerUnit.unit_id,
                ["faction_id"] = attackerUnit.faction_id,
                ["reason"] = "no_attack_skill",
            };
        }

        var occupantSnapshot = _SnapshotOccupants(state);
        _ClearNonblockingAttackerOccupants(state, attackerUnit, attackerUnitIds);
        var reachableAnchors = _CollectReachableAnchors(state, gridService, attackerUnit, options);
        var attackAnchor = new Vector2I(-1, -1);
        var attackTargetId = "";
        var attackSkillId = "";
        foreach (Vector2I anchorCoord in reachableAnchors)
        {
            var attackMatch = _FindAttackMatchFromAnchor(
                state,
                gridService,
                skillDefs,
                attackerUnit,
                anchorCoord,
                targetUnits,
                attackSkillIds
            );
            if (attackMatch.Count == 0)
                continue;
            attackAnchor = anchorCoord;
            attackTargetId = attackMatch.GetValueOrDefault("target_unit_id", "").AsStringName();
            attackSkillId = attackMatch.GetValueOrDefault("skill_id", "").AsStringName();
            break;
        }
        _RestoreOccupants(state, occupantSnapshot);

        if (attackAnchor == new Vector2I(-1, -1))
        {
            return new Dictionary
            {
                ["valid"] = false,
                ["unit_id"] = attackerUnit.unit_id,
                ["faction_id"] = attackerUnit.faction_id,
                ["reason"] = "no_reachable_attack_anchor",
                ["reachable_anchor_count"] = reachableAnchors.Count,
                ["attack_skill_ids"] = _StringNameArrayToStrings(attackSkillIds),
            };
        }
        return new Dictionary
        {
            ["valid"] = true,
            ["unit_id"] = attackerUnit.unit_id,
            ["faction_id"] = attackerUnit.faction_id,
            ["attack_anchor"] = attackAnchor,
            ["target_unit_id"] = attackTargetId,
            ["skill_id"] = attackSkillId,
            ["reachable_anchor_count"] = reachableAnchors.Count,
        };
    }

    private Array _CollectLivingUnits(BattleState state, Array unitIds)
    {
        var targets = new Array();
        if (state == null)
            return targets;
        foreach (var unitIdValue in unitIds)
        {
            var unitId = new StringName(unitIdValue.ToString());
            var unitState = state.units.ContainsKey(unitId)
                ? state.units[unitId].As<BattleUnitState>()
                : null;
            if (unitState == null || !unitState.is_alive)
                continue;
            targets.Add(unitState);
        }
        return targets;
    }

    private Array _CollectAttackSkillIds(
        BattleUnitState enemyUnit,
        Dictionary skillDefs,
        Array playerTargets
    )
    {
        var skillIds = new Array();
        if (enemyUnit == null)
            return skillIds;
        foreach (var skillIdValue in enemyUnit.known_active_skill_ids)
        {
            var skillId = new StringName(skillIdValue.ToString());
            if (!skillDefs.ContainsKey(skillId))
                continue;
            var skillDef = skillDefs[skillId].AsGodotObject() as SkillDef;
            if (skillDef == null || skillDef.combat_profile == null)
                continue;
            if (!_AttackerCanUseSkill(enemyUnit, skillDef))
                continue;
            if (!_SkillHasAttackableTarget(enemyUnit, skillDef, playerTargets))
                continue;
            skillIds.Add(skillId);
        }
        return skillIds;
    }

    private bool _SkillHasAttackableTarget(
        BattleUnitState enemyUnit,
        SkillDef skillDef,
        Array playerTargets
    )
    {
        if (enemyUnit == null || skillDef == null || skillDef.combat_profile == null)
            return false;
        if (!_AttackerCanUseSkill(enemyUnit, skillDef))
            return false;
        var targetMode = BattleTypedNames.ToTargetMode(skillDef.combat_profile.target_mode);
        if (targetMode != BattleTargetMode.Unit && targetMode != BattleTargetMode.Ground)
            return false;
        foreach (var targetUnitValue in playerTargets)
        {
            var targetUnit = targetUnitValue.As<BattleUnitState>();
            if (targetUnit == null)
                continue;
            var targetTeamFilter = skillDef.combat_profile.target_team_filter;
            if (_TargetFilterAllows(enemyUnit, targetUnit, targetTeamFilter))
                return true;
        }
        return false;
    }

    private Dictionary _SnapshotOccupants(BattleState state)
    {
        var snapshot = new Dictionary();
        if (state == null)
            return snapshot;
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
        {
            snapshot[cellEntry.Coord] = cellEntry.Cell.occupant_unit_id;
        }
        return snapshot;
    }

    private void _ClearNonblockingAttackerOccupants(
        BattleState state,
        BattleUnitState subjectUnit,
        Array attackerUnitIds
    )
    {
        if (state == null || subjectUnit == null)
            return;
        foreach (var unitIdValue in attackerUnitIds)
        {
            var unitId = new StringName(unitIdValue.ToString());
            if (unitId == subjectUnit.unit_id)
                continue;
            var sameSideUnit = state.units.ContainsKey(unitId)
                ? state.units[unitId].As<BattleUnitState>()
                : null;
            if (sameSideUnit == null)
                continue;
            sameSideUnit.refresh_footprint();
            foreach (Vector2I occupiedCoord in sameSideUnit.occupied_coords)
            {
                if (!state.cells.ContainsKey(occupiedCoord))
                    continue;
                var cell = state.cells[occupiedCoord].As<BattleCellState>();
                if (cell != null && cell.occupant_unit_id == sameSideUnit.unit_id)
                    cell.occupant_unit_id = "";
            }
        }
    }

    private void _RestoreOccupants(BattleState state, Dictionary snapshot)
    {
        if (state == null)
            return;
        foreach (var coordValue in snapshot.Keys)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
                continue;
            var coord = coordValue.AsVector2I();
            if (!state.cells.ContainsKey(coord))
                continue;
            var cell = state.cells[coord].As<BattleCellState>();
            if (cell != null)
                cell.occupant_unit_id = new StringName(
                    snapshot.GetValueOrDefault(coord, "").ToString()
                );
        }
    }

    private Array _CollectReachableAnchors(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState unitState,
        Dictionary options
    )
    {
        var anchors = new Array();
        if (state == null || gridService == null || unitState == null)
            return anchors;
        var maxSearchNodes = Mathf.Max(
            (int)options.GetValueOrDefault("max_search_nodes", DefaultMaxSearchNodes),
            1
        );
        var origin = unitState.coord;
        var frontier = new Array { origin };
        var seen = new Dictionary { [origin] = true };
        var frontierIndex = 0;
        while (frontierIndex < frontier.Count && seen.Count <= maxSearchNodes)
        {
            var current = frontier[frontierIndex].AsVector2I();
            frontierIndex++;
            anchors.Add(current);
            var neighbors = gridService.get_neighbors_4(state, current);
            foreach (Vector2I neighbor in neighbors)
            {
                if (seen.ContainsKey(neighbor))
                    continue;
                if (!gridService.can_unit_step_between_anchors(state, unitState, current, neighbor))
                    continue;
                seen[neighbor] = true;
                frontier.Add(neighbor);
            }
        }
        return anchors;
    }

    private Dictionary _FindAttackMatchFromAnchor(
        BattleState state,
        BattleGridService gridService,
        Dictionary skillDefs,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        Array playerTargets,
        Array attackSkillIds
    )
    {
        foreach (StringName skillId in attackSkillIds)
        {
            if (!skillDefs.ContainsKey(skillId))
                continue;
            var skillDef = skillDefs[skillId].AsGodotObject() as SkillDef;
            if (skillDef == null || skillDef.combat_profile == null)
                continue;
            foreach (var targetUnitValue in playerTargets)
            {
                var targetUnit = targetUnitValue.As<BattleUnitState>();
                if (targetUnit == null)
                    continue;
                var targetTeamFilter = skillDef.combat_profile.target_team_filter;
                if (!_TargetFilterAllows(enemyUnit, targetUnit, targetTeamFilter))
                    continue;
                if (
                    _CanSkillHitTargetFromAnchor(
                        state,
                        gridService,
                        enemyUnit,
                        anchorCoord,
                        targetUnit,
                        skillDef
                    )
                )
                {
                    return new Dictionary
                    {
                        ["skill_id"] = skillId,
                        ["target_unit_id"] = targetUnit.unit_id,
                    };
                }
            }
        }
        return new Dictionary();
    }

    private bool _CanSkillHitTargetFromAnchor(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (skillDef == null || skillDef.combat_profile == null)
            return false;
        if (!_AttackerCanUseSkill(enemyUnit, skillDef))
            return false;
        var targetMode = BattleTypedNames.ToTargetMode(skillDef.combat_profile.target_mode);
        switch (targetMode)
        {
            case BattleTargetMode.Unit:
                return _DistanceFromAnchorToUnit(gridService, enemyUnit, anchorCoord, targetUnit)
                    <= _GetEffectiveSkillRange(enemyUnit, skillDef);
            case BattleTargetMode.Ground:
                return _CanGroundSkillHitTarget(
                    state,
                    gridService,
                    enemyUnit,
                    anchorCoord,
                    targetUnit,
                    skillDef
                );
            default:
                return false;
        }
    }

    private bool _CanGroundSkillHitTarget(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        if (
            state == null
            || gridService == null
            || enemyUnit == null
            || targetUnit == null
            || skillDef == null
            || skillDef.combat_profile == null
        )
            return false;
        var skillRange = _GetEffectiveSkillRange(enemyUnit, skillDef);
        targetUnit.refresh_footprint();
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
        {
            Vector2I targetCoord = cellEntry.Coord;
            if (
                _DistanceFromAnchorToCoord(gridService, enemyUnit, anchorCoord, targetCoord)
                > skillRange
            )
                continue;
            var skillLevel = _GetUnitSkillLevel(enemyUnit, skillDef.skill_id);
            var combatProfile = skillDef.combat_profile;
            var collected = _targetCollectionService.collect_combat_profile_target_coords(
                state,
                gridService,
                anchorCoord,
                combatProfile,
                new Array { targetCoord },
                enemyUnit,
                new Array(),
                skillLevel
            );
            var effectCoords = new Array();
            var targetCoords = collected.ContainsKey("target_coords")
                ? collected["target_coords"].AsGodotArray()
                : new Array();
            foreach (var effectCoordValue in targetCoords)
            {
                if (effectCoordValue.VariantType == Variant.Type.Vector2I)
                    effectCoords.Add(effectCoordValue.AsVector2I());
            }
            foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
            {
                if (effectCoords.Contains(occupiedCoord))
                    return true;
            }
        }
        return false;
    }

    private int _DistanceFromAnchorToUnit(
        BattleGridService gridService,
        BattleUnitState sourceUnit,
        Vector2I sourceAnchor,
        BattleUnitState targetUnit
    )
    {
        if (gridService == null || sourceUnit == null || targetUnit == null)
            return 999999;
        targetUnit.refresh_footprint();
        var bestDistance = 999999;
        var sourceCoords = gridService.get_unit_target_coords(sourceUnit, sourceAnchor);
        foreach (Vector2I sourceCoord in sourceCoords)
        {
            foreach (Vector2I targetCoord in targetUnit.occupied_coords)
            {
                var distance = gridService.get_distance(sourceCoord, targetCoord);
                bestDistance = Mathf.Min(bestDistance, distance);
            }
        }
        return bestDistance;
    }

    private int _DistanceFromAnchorToCoord(
        BattleGridService gridService,
        BattleUnitState sourceUnit,
        Vector2I sourceAnchor,
        Vector2I targetCoord
    )
    {
        if (gridService == null || sourceUnit == null)
            return 999999;
        var bestDistance = 999999;
        var sourceCoords = gridService.get_unit_target_coords(sourceUnit, sourceAnchor);
        foreach (Vector2I sourceCoord in sourceCoords)
        {
            var distance = gridService.get_distance(sourceCoord, targetCoord);
            bestDistance = Mathf.Min(bestDistance, distance);
        }
        return bestDistance;
    }

    private bool _TargetFilterAllows(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    )
    {
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            sourceUnit,
            targetUnit,
            targetTeamFilter,
            default
        );
    }

    private int _GetEffectiveSkillRange(BattleUnitState unitState, SkillDef skillDef)
    {
        return BattleRangeService.get_effective_skill_range(unitState, skillDef);
    }

    private bool _AttackerCanUseSkill(BattleUnitState unitState, SkillDef skillDef)
    {
        if (unitState == null || skillDef == null || skillDef.combat_profile == null)
            return false;
        if (
            !BattleRangeService.unit_matches_required_weapon_families(
                unitState,
                skillDef.combat_profile.required_weapon_families
            )
        )
            return false;
        if (
            BattleRangeService.requires_current_melee_weapon(skillDef)
            && !BattleRangeService.unit_has_melee_weapon(unitState)
        )
            return false;
        return true;
    }

    private Array _StringNameArrayToStrings(Array values)
    {
        var results = new Array();
        foreach (var value in values)
        {
            results.Add(value.ToString());
        }
        return results;
    }

    private int _GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return 0;
        if (unitState.known_skill_level_map.ContainsKey(skillId))
            return (int)unitState.known_skill_level_map[skillId];
        foreach (var knownSkillId in unitState.known_active_skill_ids)
        {
            if (knownSkillId == skillId)
                return 1;
        }
        return 0;
    }

}
