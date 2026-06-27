using System.Collections.Generic;
using Godot;

internal readonly struct BattleSpawnReachabilityOptions
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
}

internal sealed class BattleSpawnReachabilityResult
{
    private readonly List<BattleSpawnReachabilityUnitResult> _details = new();
    private readonly List<StringName> _invalidEnemyUnitIds = new();
    private readonly List<StringName> _invalidPlayerUnitIds = new();

    public bool Valid { get; private set; } = true;
    public IReadOnlyList<BattleSpawnReachabilityUnitResult> Details => _details;
    public IReadOnlyList<StringName> InvalidEnemyUnitIds => _invalidEnemyUnitIds;
    public IReadOnlyList<StringName> InvalidPlayerUnitIds => _invalidPlayerUnitIds;

    internal static BattleSpawnReachabilityResult Invalid(string reason)
    {
        var result = new BattleSpawnReachabilityResult();
        result.Valid = false;
        result._details.Add(BattleSpawnReachabilityUnitResult.Invalid(reason));
        return result;
    }

    internal static BattleSpawnReachabilityResult FromProjectionPayload(
        bool valid,
        IEnumerable<StringName> invalidEnemyUnitIds,
        IEnumerable<StringName> invalidPlayerUnitIds,
        IEnumerable<BattleSpawnReachabilityUnitResult> details
    )
    {
        var result = new BattleSpawnReachabilityResult { Valid = valid };
        if (invalidEnemyUnitIds != null)
            result._invalidEnemyUnitIds.AddRange(invalidEnemyUnitIds);
        if (invalidPlayerUnitIds != null)
            result._invalidPlayerUnitIds.AddRange(invalidPlayerUnitIds);
        if (details != null)
            result._details.AddRange(details);
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
}

internal sealed class BattleSpawnReachabilityUnitResult
{
    public bool Valid { get; init; }
    public StringName UnitId { get; init; } = "";
    public StringName FactionId { get; init; } = "";
    public string Reason { get; init; } = "";
    public Vector2I AttackAnchor { get; init; } = new(-1, -1);
    public StringName TargetUnitId { get; init; } = "";
    public StringName SkillId { get; init; } = "";
    public int ReachableAnchorCount { get; init; } = -1;
    public IReadOnlyList<StringName> AttackSkillIds { get; init; } =
        System.Array.Empty<StringName>();

    internal static BattleSpawnReachabilityUnitResult Invalid(string reason) =>
        new() { Valid = false, Reason = reason };
}

internal sealed class BattleSpawnReachabilityService
{
    private BattleTargetCollectionService _targetCollectionService = new();

    internal BattleSpawnReachabilityResult ValidateStateTyped(
        BattleState state,
        BattleGridService gridService,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        BattleSpawnReachabilityOptions options = default
    )
    {
        if (state == null || gridService == null)
            return BattleSpawnReachabilityResult.Invalid("missing_state_or_grid");

        skillDefinitions ??= EmptySkillDefinitions;
        var result = new BattleSpawnReachabilityResult();
        List<StringName> enemyUnitIds = ReadStringNameList(state.enemy_unit_ids);
        List<StringName> allyUnitIds = ReadStringNameList(state.ally_unit_ids);
        var playerTargets = _CollectLivingUnits(state, allyUnitIds);
        if (playerTargets.Count == 0)
            return BattleSpawnReachabilityResult.Invalid("no_living_player_targets");

        var enemyOccupantSnapshot = _ClearUnitOccupants(state, enemyUnitIds);
        foreach (StringName enemyUnitId in enemyUnitIds)
        {
            BattleUnitState enemyUnit = TryGetUnit(state, enemyUnitId);
            if (enemyUnit == null || !enemyUnit.is_alive)
                continue;
            var enemyResult = _ValidateAttackerUnit(
                state,
                gridService,
                skillDefinitions,
                enemyUnit,
                playerTargets,
                options
            );
            if (enemyResult.Valid)
                continue;
            result.AddInvalidEnemy(enemyUnit.unit_id, enemyResult);
        }
        _RestoreOccupants(state, enemyOccupantSnapshot);

        if (!options.ValidatePlayerToEnemy)
            return result;

        var enemyTargets = _CollectLivingUnits(state, enemyUnitIds);
        if (enemyTargets.Count == 0)
            return BattleSpawnReachabilityResult.Invalid("no_living_enemy_targets");

        var allyOccupantSnapshot = _ClearUnitOccupants(state, allyUnitIds);
        foreach (StringName playerUnitId in allyUnitIds)
        {
            BattleUnitState playerUnit = TryGetUnit(state, playerUnitId);
            if (playerUnit == null || !playerUnit.is_alive)
                continue;
            var playerResult = _ValidateAttackerUnit(
                state,
                gridService,
                skillDefinitions,
                playerUnit,
                enemyTargets,
                options
            );
            if (playerResult.Valid)
                continue;
            result.AddInvalidPlayer(playerUnit.unit_id, playerResult);
        }
        _RestoreOccupants(state, allyOccupantSnapshot);
        return result;
    }

    private BattleSpawnReachabilityUnitResult _ValidateAttackerUnit(
        BattleState state,
        BattleGridService gridService,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        BattleUnitState attackerUnit,
        IReadOnlyList<BattleUnitState> targetUnits,
        BattleSpawnReachabilityOptions options
    )
    {
        var attackSkills = _CollectAttackSkills(attackerUnit, skillDefinitions, targetUnits);
        if (attackSkills.Count == 0)
        {
            return new BattleSpawnReachabilityUnitResult
            {
                Valid = false,
                UnitId = attackerUnit.unit_id,
                FactionId = attackerUnit.faction_id,
                Reason = "no_attack_skill",
            };
        }

        var reachableAttackAnchor = _FindReachableAttackAnchor(
            state,
            gridService,
            attackerUnit,
            targetUnits,
            attackSkills,
            options
        );

        if (!reachableAttackAnchor.Found)
        {
            return new BattleSpawnReachabilityUnitResult
            {
                Valid = false,
                UnitId = attackerUnit.unit_id,
                FactionId = attackerUnit.faction_id,
                Reason = "no_reachable_attack_anchor",
                ReachableAnchorCount = reachableAttackAnchor.ReachableAnchorCount,
                AttackSkillIds = ToAttackSkillIds(attackSkills),
            };
        }
        return new BattleSpawnReachabilityUnitResult
        {
            Valid = true,
            UnitId = attackerUnit.unit_id,
            FactionId = attackerUnit.faction_id,
            AttackAnchor = reachableAttackAnchor.AttackAnchor,
            TargetUnitId = reachableAttackAnchor.TargetUnitId,
            SkillId = reachableAttackAnchor.SkillId,
            ReachableAnchorCount = reachableAttackAnchor.ReachableAnchorCount,
        };
    }

    private List<BattleUnitState> _CollectLivingUnits(
        BattleState state,
        IReadOnlyList<StringName> unitIds
    )
    {
        var targets = new List<BattleUnitState>();
        if (state == null)
            return targets;
        foreach (StringName unitId in unitIds)
        {
            BattleUnitState unitState = TryGetUnit(state, unitId);
            if (unitState == null || !unitState.is_alive)
                continue;
            targets.Add(unitState);
        }
        return targets;
    }

    private List<BattleSpawnReachabilityAttackSkill> _CollectAttackSkills(
        BattleUnitState enemyUnit,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyList<BattleUnitState> playerTargets
    )
    {
        var attackSkills = new List<BattleSpawnReachabilityAttackSkill>();
        if (enemyUnit == null)
            return attackSkills;
        foreach (var skillIdValue in enemyUnit.known_active_skill_ids)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillIdValue);
            if (!skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition))
                continue;
            if (skillDefinition == null || skillDefinition.CombatProfile == null)
                continue;
            if (!_AttackerCanUseSkill(enemyUnit, skillDefinition))
                continue;
            CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
            BattleTargetMode targetMode = combatProfile.TargetModeKind;
            if (targetMode != BattleTargetMode.Unit && targetMode != BattleTargetMode.Ground)
                continue;
            StringName targetTeamFilter = combatProfile.TargetTeamFilter;
            if (!HasAttackableTargetForFilter(enemyUnit, playerTargets, targetTeamFilter))
                continue;
            int skillLevel = _GetUnitSkillLevel(enemyUnit, skillId);
            SkillEffectiveCombatDefinition effectiveProfile =
                SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
            BattleAreaPattern groundAreaPattern = BattleTypedNames.ToAreaPattern(
                effectiveProfile.AreaPattern
            );
            attackSkills.Add(
                new BattleSpawnReachabilityAttackSkill(
                    skillId,
                    skillDefinition,
                    combatProfile,
                    targetMode,
                    targetTeamFilter,
                    skillLevel,
                    _GetEffectiveSkillRange(enemyUnit, skillDefinition),
                    groundAreaPattern,
                    System.Math.Max(effectiveProfile.AreaValue, 0)
                )
            );
        }
        return attackSkills;
    }

    private static IReadOnlyList<StringName> ToAttackSkillIds(
        IReadOnlyList<BattleSpawnReachabilityAttackSkill> attackSkills
    )
    {
        var skillIds = new List<StringName>();
        foreach (BattleSpawnReachabilityAttackSkill attackSkill in attackSkills)
            skillIds.Add(attackSkill.SkillId);
        return skillIds;
    }

    private bool HasAttackableTargetForFilter(
        BattleUnitState enemyUnit,
        IReadOnlyList<BattleUnitState> playerTargets,
        StringName targetTeamFilter
    )
    {
        if (enemyUnit == null)
            return false;
        foreach (BattleUnitState targetUnit in playerTargets)
        {
            if (targetUnit == null)
                continue;
            if (_TargetFilterAllows(enemyUnit, targetUnit, targetTeamFilter))
                return true;
        }
        return false;
    }

    private System.Collections.Generic.Dictionary<Vector2I, StringName> _ClearUnitOccupants(
        BattleState state,
        IReadOnlyList<StringName> unitIds
    )
    {
        var snapshot = new System.Collections.Generic.Dictionary<Vector2I, StringName>();
        if (state == null)
            return snapshot;
        foreach (StringName unitId in unitIds)
        {
            BattleUnitState sameSideUnit = TryGetUnit(state, unitId);
            if (sameSideUnit == null)
                continue;
            sameSideUnit.RefreshFootprint();
            foreach (Vector2I occupiedCoord in sameSideUnit.occupied_coords)
            {
                var cell = state.GetCell(occupiedCoord);
                if (cell != null && cell.occupant_unit_id == sameSideUnit.unit_id)
                {
                    snapshot[occupiedCoord] = cell.occupant_unit_id;
                    cell.occupant_unit_id = "";
                }
            }
        }
        return snapshot;
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
            var cell = state.GetCell(entry.Key);
            if (cell != null)
                cell.occupant_unit_id = entry.Value;
        }
    }

    private BattleSpawnReachabilitySearchResult _FindReachableAttackAnchor(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState unitState,
        IReadOnlyList<BattleUnitState> targetUnits,
        IReadOnlyList<BattleSpawnReachabilityAttackSkill> attackSkills,
        BattleSpawnReachabilityOptions options
    )
    {
        if (state == null || gridService == null || unitState == null)
            return default;
        var attackTargets = BuildAttackTargets(
            state,
            gridService,
            unitState,
            targetUnits,
            attackSkills
        );
        if (attackTargets.Count == 0)
            return default;
        int maxSearchNodes = Mathf.Max(options.EffectiveMaxSearchNodes, 1);
        var origin = unitState.coord;
        var frontier = new List<Vector2I> { origin };
        var seen = new HashSet<Vector2I> { origin };
        int frontierIndex = 0;
        int reachableAnchorCount = 0;
        while (frontierIndex < frontier.Count && seen.Count <= maxSearchNodes)
        {
            Vector2I current = frontier[frontierIndex];
            frontierIndex++;
            reachableAnchorCount++;
            var attackMatch = _FindAttackMatchFromAnchor(
                state,
                gridService,
                unitState,
                current,
                attackTargets
            );
            if (attackMatch.Found)
            {
                return new BattleSpawnReachabilitySearchResult(
                    true,
                    current,
                    attackMatch.TargetUnitId,
                    attackMatch.SkillId,
                    reachableAnchorCount
                );
            }
            var neighbors = gridService.GetNeighbors4(state, current);
            foreach (Vector2I neighbor in neighbors)
            {
                if (seen.Contains(neighbor))
                    continue;
                if (!gridService.CanUnitStepBetweenAnchors(state, unitState, current, neighbor))
                    continue;
                seen.Add(neighbor);
                frontier.Add(neighbor);
            }
        }
        return new BattleSpawnReachabilitySearchResult(
            false,
            new Vector2I(-1, -1),
            "",
            "",
            reachableAnchorCount
        );
    }

    private BattleSpawnReachabilityAttackMatch _FindAttackMatchFromAnchor(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        IReadOnlyList<BattleSpawnReachabilityAttackTarget> attackTargets
    )
    {
        foreach (BattleSpawnReachabilityAttackTarget attackTarget in attackTargets)
        {
            if (
                _CanSkillHitTargetFromAnchor(
                    state,
                    gridService,
                    enemyUnit,
                    anchorCoord,
                    attackTarget
                )
            )
            {
                return new BattleSpawnReachabilityAttackMatch(
                    true,
                    attackTarget.AttackSkill.SkillId,
                    attackTarget.TargetUnit.unit_id
                );
            }
        }
        return default;
    }

    private List<BattleSpawnReachabilityAttackTarget> BuildAttackTargets(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState enemyUnit,
        IReadOnlyList<BattleUnitState> targetUnits,
        IReadOnlyList<BattleSpawnReachabilityAttackSkill> attackSkills
    )
    {
        var attackTargets = new List<BattleSpawnReachabilityAttackTarget>();
        foreach (BattleSpawnReachabilityAttackSkill attackSkill in attackSkills)
        {
            foreach (BattleUnitState targetUnit in targetUnits)
            {
                if (targetUnit == null)
                    continue;
                if (!_TargetFilterAllows(enemyUnit, targetUnit, attackSkill.TargetTeamFilter))
                    continue;
                IReadOnlyList<Vector2I> fastGroundTargetCoords = null;
                if (
                    attackSkill.TargetMode == BattleTargetMode.Ground
                    && IsSpawnReachabilityFastGroundPattern(attackSkill.GroundAreaPattern)
                    && attackSkill.GroundAreaPattern != BattleAreaPattern.Self
                )
                {
                    targetUnit.RefreshFootprint();
                    fastGroundTargetCoords = BuildGroundTargetCoordCandidates(
                        state,
                        gridService,
                        targetUnit,
                        attackSkill.GroundAreaPattern,
                        attackSkill.GroundAreaValue
                    );
                }
                attackTargets.Add(
                    new BattleSpawnReachabilityAttackTarget(
                        attackSkill,
                        targetUnit,
                        fastGroundTargetCoords
                    )
                );
            }
        }
        return attackTargets;
    }

    private bool _CanSkillHitTargetFromAnchor(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        BattleSpawnReachabilityAttackTarget attackTarget
    )
    {
        BattleSpawnReachabilityAttackSkill attackSkill = attackTarget.AttackSkill;
        if (attackSkill.SkillDefinition == null || attackSkill.CombatProfile == null)
            return false;
        switch (attackSkill.TargetMode)
        {
            case BattleTargetMode.Unit:
                return _DistanceFromAnchorToUnit(
                    gridService,
                    enemyUnit,
                    anchorCoord,
                    attackTarget.TargetUnit
                ) <= attackSkill.Range;
            case BattleTargetMode.Ground:
                return _CanGroundSkillHitTarget(
                    state,
                    gridService,
                    enemyUnit,
                    anchorCoord,
                    attackTarget
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
        BattleSpawnReachabilityAttackTarget attackTarget
    )
    {
        BattleSpawnReachabilityAttackSkill attackSkill = attackTarget.AttackSkill;
        BattleUnitState targetUnit = attackTarget.TargetUnit;
        if (
            state == null
            || gridService == null
            || enemyUnit == null
            || targetUnit == null
            || attackSkill.SkillDefinition == null
            || attackSkill.CombatProfile == null
        )
            return false;
        targetUnit.RefreshFootprint();
        if (
            _TryCanGroundSkillHitTargetFast(
                state,
                gridService,
                enemyUnit,
                anchorCoord,
                targetUnit,
                attackSkill.GroundAreaPattern,
                attackSkill.GroundAreaValue,
                attackSkill.Range,
                attackTarget.FastGroundTargetCoords,
                out bool fastResult
            )
        )
            return fastResult;
        foreach (BattleState.BattleCellEntry cellEntry in state.GetCellEntriesTyped())
        {
            Vector2I targetCoord = cellEntry.Coord;
            if (
                _DistanceFromAnchorToCoord(gridService, enemyUnit, anchorCoord, targetCoord)
                > attackSkill.Range
            )
                continue;
            BattleTargetCollectionResult collected =
                _targetCollectionService.CollectCombatProfileTargetCoords(
                    state,
                    gridService,
                    anchorCoord,
                    attackSkill.CombatProfile,
                    new[] { targetCoord },
                    enemyUnit,
                    System.Array.Empty<BattleUnitState>(),
                    attackSkill.SkillLevel
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

    private bool _TryCanGroundSkillHitTargetFast(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState enemyUnit,
        Vector2I anchorCoord,
        BattleUnitState targetUnit,
        BattleAreaPattern areaPattern,
        int areaValue,
        int skillRange,
        IReadOnlyList<Vector2I> fastGroundTargetCoords,
        out bool canHit
    )
    {
        canHit = false;
        if (areaPattern == BattleAreaPattern.Self)
        {
            foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
            {
                if (occupiedCoord == anchorCoord)
                {
                    canHit = true;
                    return true;
                }
            }
            return true;
        }

        if (!IsSpawnReachabilityFastGroundPattern(areaPattern))
            return false;

        foreach (Vector2I targetCoord in fastGroundTargetCoords ?? System.Array.Empty<Vector2I>())
        {
            if (
                _DistanceFromAnchorToCoord(gridService, enemyUnit, anchorCoord, targetCoord)
                > skillRange
            )
                continue;
            canHit = true;
            return true;
        }
        return true;
    }

    private static bool IsSpawnReachabilityFastGroundPattern(BattleAreaPattern areaPattern)
    {
        return areaPattern
            is BattleAreaPattern.Unknown
                or BattleAreaPattern.Single
                or BattleAreaPattern.Diamond
                or BattleAreaPattern.Square
                or BattleAreaPattern.Radius
                or BattleAreaPattern.Cross;
    }

    private static IReadOnlyList<Vector2I> BuildGroundTargetCoordCandidates(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState targetUnit,
        BattleAreaPattern areaPattern,
        int areaValue
    )
    {
        var candidates = new List<Vector2I>();
        var seen = new HashSet<Vector2I>();
        int radius = GetGroundTargetCandidateRadius(areaPattern, areaValue);
        foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
        {
            for (int y = occupiedCoord.Y - radius; y <= occupiedCoord.Y + radius; y++)
            {
                for (int x = occupiedCoord.X - radius; x <= occupiedCoord.X + radius; x++)
                {
                    var candidate = new Vector2I(x, y);
                    if (!gridService.IsInside(state, candidate))
                        continue;
                    if (!GroundAreaContainsCoord(candidate, areaPattern, areaValue, occupiedCoord))
                        continue;
                    if (seen.Add(candidate))
                        candidates.Add(candidate);
                }
            }
        }
        return candidates;
    }

    private static int GetGroundTargetCandidateRadius(
        BattleAreaPattern areaPattern,
        int areaValue
    )
    {
        if (areaValue <= 0)
            return 0;
        return areaPattern
            is BattleAreaPattern.Diamond
                or BattleAreaPattern.Square
                or BattleAreaPattern.Radius
                or BattleAreaPattern.Cross
            ? areaValue
            : 0;
    }

    private static bool GroundAreaContainsCoord(
        Vector2I center,
        BattleAreaPattern areaPattern,
        int areaValue,
        Vector2I coord
    )
    {
        int radius = System.Math.Max(areaValue, 0);
        if (
            areaPattern == BattleAreaPattern.Unknown
            || areaPattern == BattleAreaPattern.Single
            || radius <= 0
        )
            return center == coord;
        int dx = System.Math.Abs(coord.X - center.X);
        int dy = System.Math.Abs(coord.Y - center.Y);
        return areaPattern switch
        {
            BattleAreaPattern.Diamond => dx + dy <= radius,
            BattleAreaPattern.Square => System.Math.Max(dx, dy) <= radius,
            BattleAreaPattern.Radius => System.Math.Max(dx, dy) <= radius,
            BattleAreaPattern.Cross => (dx == 0 && dy <= radius)
                || (dy == 0 && dx <= radius),
            _ => center == coord,
        };
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
        targetUnit.RefreshFootprint();
        int bestDistance = 999999;
        var sourceCoords = gridService.GetUnitTargetCoords(sourceUnit, sourceAnchor);
        foreach (Vector2I sourceCoord in sourceCoords)
        {
            foreach (Vector2I targetCoord in targetUnit.occupied_coords)
            {
                int distance = gridService.GetDistance(sourceCoord, targetCoord);
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
        var sourceCoords = gridService.GetUnitTargetCoords(sourceUnit, sourceAnchor);
        foreach (Vector2I sourceCoord in sourceCoords)
        {
            int distance = gridService.GetDistance(sourceCoord, targetCoord);
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
        return BattleTargetTeamRules.IsUnitValidForFilter(
            sourceUnit,
            targetUnit,
            targetTeamFilter,
            default
        );
    }

    private int _GetEffectiveSkillRange(BattleUnitState unitState, SkillDefinition skillDefinition)
    {
        return BattleRangeService.GetEffectiveSkillRange(unitState, skillDefinition);
    }

    private bool _AttackerCanUseSkill(BattleUnitState unitState, SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (unitState == null || skillDefinition == null || combatProfile == null)
            return false;
        if (
            !BattleRangeService.UnitMatchesRequiredWeaponFamilies(
                unitState,
                combatProfile.RequiredWeaponFamilies
            )
        )
            return false;
        if (
            BattleRangeService.RequiresCurrentMeleeWeapon(skillDefinition)
            && !BattleRangeService.UnitHasMeleeWeapon(unitState)
        )
            return false;
        return true;
    }

    private int _GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return 0;
        if (unitState.HasKnownSkillLevelTyped(skillId))
            return unitState.GetKnownSkillLevelTyped(skillId);
        foreach (var knownSkillId in unitState.known_active_skill_ids)
        {
            if (knownSkillId == skillId)
                return 1;
        }
        return 0;
    }

    private static readonly IReadOnlyDictionary<StringName, SkillDefinition> EmptySkillDefinitions =
        new Dictionary<StringName, SkillDefinition>();

    private static List<StringName> ReadStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (StringName value in values)
        {
            StringName id = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(id))
                result.Add(id);
        }
        return result;
    }

    private static BattleUnitState TryGetUnit(BattleState state, StringName unitId)
    {
        return state != null && state.TryGetUnitTyped(unitId, out BattleUnitState unitState)
            ? unitState
            : null;
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";

    private readonly struct BattleSpawnReachabilityAttackSkill
    {
        internal readonly StringName SkillId;
        internal readonly SkillDefinition SkillDefinition;
        internal readonly CombatSkillDefinition CombatProfile;
        internal readonly BattleTargetMode TargetMode;
        internal readonly StringName TargetTeamFilter;
        internal readonly int SkillLevel;
        internal readonly int Range;
        internal readonly BattleAreaPattern GroundAreaPattern;
        internal readonly int GroundAreaValue;

        internal BattleSpawnReachabilityAttackSkill(
            StringName skillId,
            SkillDefinition skillDefinition,
            CombatSkillDefinition combatProfile,
            BattleTargetMode targetMode,
            StringName targetTeamFilter,
            int skillLevel,
            int range,
            BattleAreaPattern groundAreaPattern,
            int groundAreaValue
        )
        {
            SkillId = skillId;
            SkillDefinition = skillDefinition;
            CombatProfile = combatProfile;
            TargetMode = targetMode;
            TargetTeamFilter = targetTeamFilter;
            SkillLevel = skillLevel;
            Range = range;
            GroundAreaPattern = groundAreaPattern;
            GroundAreaValue = groundAreaValue;
        }
    }

    private readonly struct BattleSpawnReachabilityAttackTarget
    {
        internal readonly BattleSpawnReachabilityAttackSkill AttackSkill;
        internal readonly BattleUnitState TargetUnit;
        internal readonly IReadOnlyList<Vector2I> FastGroundTargetCoords;

        internal BattleSpawnReachabilityAttackTarget(
            BattleSpawnReachabilityAttackSkill attackSkill,
            BattleUnitState targetUnit,
            IReadOnlyList<Vector2I> fastGroundTargetCoords
        )
        {
            AttackSkill = attackSkill;
            TargetUnit = targetUnit;
            FastGroundTargetCoords = fastGroundTargetCoords;
        }
    }

    private readonly struct BattleSpawnReachabilitySearchResult
    {
        internal readonly bool Found;
        internal readonly Vector2I AttackAnchor;
        internal readonly StringName TargetUnitId;
        internal readonly StringName SkillId;
        internal readonly int ReachableAnchorCount;

        internal BattleSpawnReachabilitySearchResult(
            bool found,
            Vector2I attackAnchor,
            StringName targetUnitId,
            StringName skillId,
            int reachableAnchorCount
        )
        {
            Found = found;
            AttackAnchor = attackAnchor;
            TargetUnitId = targetUnitId;
            SkillId = skillId;
            ReachableAnchorCount = reachableAnchorCount;
        }
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
