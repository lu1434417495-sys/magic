using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public readonly struct BattleSpawnReachabilityOptions
{
    private const int DefaultMaxSearchNodes = 2048;

    public readonly bool ValidatePlayerToEnemy;
    public readonly int MaxSearchNodes;

    public int EffectiveMaxSearchNodes =>
        MaxSearchNodes > 0 ? MaxSearchNodes : DefaultMaxSearchNodes;

    public BattleSpawnReachabilityOptions(
        bool validatePlayerToEnemy = false,
        int maxSearchNodes = DefaultMaxSearchNodes
    )
    {
        ValidatePlayerToEnemy = validatePlayerToEnemy;
        MaxSearchNodes = Mathf.Max(maxSearchNodes, 1);
    }

    public static BattleSpawnReachabilityOptions FromDictionary(GDictionary options)
    {
        options ??= new GDictionary();
        return new BattleSpawnReachabilityOptions(
            ReadBool(options, "validate_player_to_enemy"),
            ReadInt(options, "max_search_nodes", DefaultMaxSearchNodes)
        );
    }

    private static bool ReadBool(GDictionary options, string key, bool fallback = false)
    {
        if (!TryRead(options, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int ReadInt(GDictionary options, string key, int fallback = 0)
    {
        if (!TryRead(options, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool TryRead(GDictionary options, string key, out Variant value)
    {
        value = default;
        if (options == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (options.ContainsKey(key))
        {
            value = options[key];
            return true;
        }
        StringName stringNameKey = new(key);
        if (options.ContainsKey(stringNameKey))
        {
            value = options[stringNameKey];
            return true;
        }
        return false;
    }
}

public sealed class BattleSpawnReachabilityResult
{
    private readonly List<BattleSpawnReachabilityUnitResult> _details = new();
    private readonly List<StringName> _invalidEnemyUnitIds = new();
    private readonly List<StringName> _invalidPlayerUnitIds = new();

    public bool Valid { get; private set; } = true;

    public static BattleSpawnReachabilityResult Invalid(string reason)
    {
        var result = new BattleSpawnReachabilityResult();
        result.Valid = false;
        result._details.Add(BattleSpawnReachabilityUnitResult.Invalid(reason));
        return result;
    }

    internal void AddInvalidEnemy(
        StringName unitId,
        BattleSpawnReachabilityUnitResult detail
    )
    {
        Valid = false;
        _invalidEnemyUnitIds.Add(unitId);
        _details.Add(detail);
    }

    internal void AddInvalidPlayer(
        StringName unitId,
        BattleSpawnReachabilityUnitResult detail
    )
    {
        Valid = false;
        _invalidPlayerUnitIds.Add(unitId);
        _details.Add(detail);
    }

    public GDictionary ToDictionary()
    {
        var invalidEnemyUnitIds = new GStringNameArray();
        foreach (StringName unitId in _invalidEnemyUnitIds)
            invalidEnemyUnitIds.Add(unitId);

        var invalidPlayerUnitIds = new GStringNameArray();
        foreach (StringName unitId in _invalidPlayerUnitIds)
            invalidPlayerUnitIds.Add(unitId);

        var details = new GArray();
        foreach (BattleSpawnReachabilityUnitResult detail in _details)
            details.Add(detail.ToDictionary());

        return new GDictionary
        {
            ["valid"] = Valid,
            ["invalid_enemy_unit_ids"] = invalidEnemyUnitIds,
            ["invalid_player_unit_ids"] = invalidPlayerUnitIds,
            ["details"] = details,
        };
    }
}

internal sealed class BattleSpawnReachabilityUnitResult
{
    internal bool Valid { get; init; }
    internal StringName UnitId { get; init; } = "";
    internal StringName FactionId { get; init; } = "";
    internal string Reason { get; init; } = "";
    internal Vector2I AttackAnchor { get; init; } = new(-1, -1);
    internal StringName TargetUnitId { get; init; } = "";
    internal StringName SkillId { get; init; } = "";
    internal int ReachableAnchorCount { get; init; } = -1;
    internal IReadOnlyList<StringName> AttackSkillIds { get; init; } =
        System.Array.Empty<StringName>();

    internal static BattleSpawnReachabilityUnitResult Invalid(string reason) =>
        new() { Valid = false, Reason = reason };

    internal GDictionary ToDictionary()
    {
        var result = new GDictionary { ["valid"] = Valid };
        if (!IsEmpty(UnitId))
            result["unit_id"] = UnitId;
        if (!IsEmpty(FactionId))
            result["faction_id"] = FactionId;
        if (!string.IsNullOrEmpty(Reason))
            result["reason"] = Reason;
        if (AttackAnchor != new Vector2I(-1, -1))
            result["attack_anchor"] = AttackAnchor;
        if (!IsEmpty(TargetUnitId))
            result["target_unit_id"] = TargetUnitId;
        if (!IsEmpty(SkillId))
            result["skill_id"] = SkillId;
        if (ReachableAnchorCount >= 0)
            result["reachable_anchor_count"] = ReachableAnchorCount;
        if (AttackSkillIds.Count > 0)
            result["attack_skill_ids"] = ToStringArray(AttackSkillIds);
        return result;
    }

    private static GArray ToStringArray(IReadOnlyList<StringName> values)
    {
        var result = new GArray();
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";
}

[GlobalClass]
public partial class BattleSpawnReachabilityService : RefCounted
{
    private BattleTargetCollectionService _targetCollectionService = new();

    public GDictionary ValidateState(
        BattleState state,
        BattleGridService gridService,
        GDictionary skillDefs,
        GDictionary options = null
    )
    {
        return ValidateStateTyped(
                state,
                gridService,
                skillDefs,
                BattleSpawnReachabilityOptions.FromDictionary(options)
            )
            .ToDictionary();
    }

    public BattleSpawnReachabilityResult ValidateStateTyped(
        BattleState state,
        BattleGridService gridService,
        GDictionary skillDefs,
        BattleSpawnReachabilityOptions options = default
    )
    {
        if (state == null || gridService == null)
            return BattleSpawnReachabilityResult.Invalid("missing_state_or_grid");

        var result = new BattleSpawnReachabilityResult();
        var playerTargets = _CollectLivingUnits(state, (GArray)state.ally_unit_ids);
        if (playerTargets.Count == 0)
            return BattleSpawnReachabilityResult.Invalid("no_living_player_targets");

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
                (GArray)state.enemy_unit_ids,
                playerTargets,
                options
            );
            if (enemyResult.Valid)
                continue;
            result.AddInvalidEnemy(enemyUnit.unit_id, enemyResult);
        }

        if (!options.ValidatePlayerToEnemy)
            return result;

        var enemyTargets = _CollectLivingUnits(state, (GArray)state.enemy_unit_ids);
        if (enemyTargets.Count == 0)
            return BattleSpawnReachabilityResult.Invalid("no_living_enemy_targets");

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
                (GArray)state.ally_unit_ids,
                enemyTargets,
                options
            );
            if (playerResult.Valid)
                continue;
            result.AddInvalidPlayer(playerUnit.unit_id, playerResult);
        }
        return result;
    }

    public GDictionary validate_state(
        BattleState state,
        BattleGridService grid_service,
        GDictionary skill_defs,
        GDictionary options = null
    )
    {
        return ValidateState(state, grid_service, skill_defs, options);
    }

    private BattleSpawnReachabilityUnitResult _ValidateAttackerUnit(
        BattleState state,
        BattleGridService gridService,
        GDictionary skillDefs,
        BattleUnitState attackerUnit,
        GArray attackerUnitIds,
        GArray targetUnits,
        BattleSpawnReachabilityOptions options
    )
    {
        var attackSkillIds = _CollectAttackSkillIds(attackerUnit, skillDefs, targetUnits);
        if (attackSkillIds.Count == 0)
        {
            return new BattleSpawnReachabilityUnitResult
            {
                Valid = false,
                UnitId = attackerUnit.unit_id,
                FactionId = attackerUnit.faction_id,
                Reason = "no_attack_skill",
            };
        }

        var occupantSnapshot = _SnapshotOccupants(state);
        _ClearNonblockingAttackerOccupants(state, attackerUnit, attackerUnitIds);
        var reachableAnchors = _CollectReachableAnchors(
            state,
            gridService,
            attackerUnit,
            options
        );
        var attackAnchor = new Vector2I(-1, -1);
        StringName attackTargetId = "";
        StringName attackSkillId = "";
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
            if (!attackMatch.Found)
                continue;
            attackAnchor = anchorCoord;
            attackTargetId = attackMatch.TargetUnitId;
            attackSkillId = attackMatch.SkillId;
            break;
        }
        _RestoreOccupants(state, occupantSnapshot);

        if (attackAnchor == new Vector2I(-1, -1))
        {
            return new BattleSpawnReachabilityUnitResult
            {
                Valid = false,
                UnitId = attackerUnit.unit_id,
                FactionId = attackerUnit.faction_id,
                Reason = "no_reachable_attack_anchor",
                ReachableAnchorCount = reachableAnchors.Count,
                AttackSkillIds = attackSkillIds,
            };
        }
        return new BattleSpawnReachabilityUnitResult
        {
            Valid = true,
            UnitId = attackerUnit.unit_id,
            FactionId = attackerUnit.faction_id,
            AttackAnchor = attackAnchor,
            TargetUnitId = attackTargetId,
            SkillId = attackSkillId,
            ReachableAnchorCount = reachableAnchors.Count,
        };
    }

    private GArray _CollectLivingUnits(BattleState state, GArray unitIds)
    {
        var targets = new GArray();
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

    private List<StringName> _CollectAttackSkillIds(
        BattleUnitState enemyUnit,
        GDictionary skillDefs,
        GArray playerTargets
    )
    {
        var skillIds = new List<StringName>();
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
        GArray playerTargets
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

    private System.Collections.Generic.Dictionary<Vector2I, StringName> _SnapshotOccupants(
        BattleState state
    )
    {
        var snapshot = new System.Collections.Generic.Dictionary<Vector2I, StringName>();
        if (state == null)
            return snapshot;
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
            snapshot[cellEntry.Coord] = cellEntry.Cell.occupant_unit_id;
        return snapshot;
    }

    private void _ClearNonblockingAttackerOccupants(
        BattleState state,
        BattleUnitState subjectUnit,
        GArray attackerUnitIds
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

    private void _RestoreOccupants(
        BattleState state,
        System.Collections.Generic.Dictionary<Vector2I, StringName> snapshot
    )
    {
        if (state == null)
            return;
        foreach (var entry in snapshot)
        {
            if (!state.cells.ContainsKey(entry.Key))
                continue;
            var cell = state.cells[entry.Key].As<BattleCellState>();
            if (cell != null)
                cell.occupant_unit_id = entry.Value;
        }
    }

    private List<Vector2I> _CollectReachableAnchors(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState unitState,
        BattleSpawnReachabilityOptions options
    )
    {
        var anchors = new List<Vector2I>();
        if (state == null || gridService == null || unitState == null)
            return anchors;
        int maxSearchNodes = Mathf.Max(options.EffectiveMaxSearchNodes, 1);
        var origin = unitState.coord;
        var frontier = new List<Vector2I> { origin };
        var seen = new HashSet<Vector2I> { origin };
        int frontierIndex = 0;
        while (frontierIndex < frontier.Count && seen.Count <= maxSearchNodes)
        {
            Vector2I current = frontier[frontierIndex];
            frontierIndex++;
            anchors.Add(current);
            var neighbors = gridService.get_neighbors_4(state, current);
            foreach (Vector2I neighbor in neighbors)
            {
                if (seen.Contains(neighbor))
                    continue;
                if (!gridService.can_unit_step_between_anchors(state, unitState, current, neighbor))
                    continue;
                seen.Add(neighbor);
                frontier.Add(neighbor);
            }
        }
        return anchors;
    }

    private BattleSpawnReachabilityAttackMatch _FindAttackMatchFromAnchor(
        BattleState state,
        BattleGridService gridService,
        GDictionary skillDefs,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        GArray playerTargets,
        IReadOnlyList<StringName> attackSkillIds
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
                    return new BattleSpawnReachabilityAttackMatch(
                        true,
                        skillId,
                        targetUnit.unit_id
                    );
                }
            }
        }
        return default;
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
            BattleTargetCollectionResult collected =
                _targetCollectionService.CollectCombatProfileTargetCoords(
                    state,
                    gridService,
                    anchorCoord,
                    combatProfile,
                    new[] { targetCoord },
                    enemyUnit,
                    System.Array.Empty<BattleUnitState>(),
                    skillLevel
                );
            var effectCoords = new HashSet<Vector2I>(collected.TargetCoords);
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
        int bestDistance = 999999;
        var sourceCoords = gridService.get_unit_target_coords(sourceUnit, sourceAnchor);
        foreach (Vector2I sourceCoord in sourceCoords)
        {
            foreach (Vector2I targetCoord in targetUnit.occupied_coords)
            {
                int distance = gridService.get_distance(sourceCoord, targetCoord);
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
        int bestDistance = 999999;
        var sourceCoords = gridService.get_unit_target_coords(sourceUnit, sourceAnchor);
        foreach (Vector2I sourceCoord in sourceCoords)
        {
            int distance = gridService.get_distance(sourceCoord, targetCoord);
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

    private readonly struct BattleSpawnReachabilityAttackMatch
    {
        internal readonly bool Found;
        internal readonly StringName SkillId;
        internal readonly StringName TargetUnitId;

        internal BattleSpawnReachabilityAttackMatch(
            bool found,
            StringName skillId,
            StringName targetUnitId
        )
        {
            Found = found;
            SkillId = skillId;
            TargetUnitId = targetUnitId;
        }
    }
}
