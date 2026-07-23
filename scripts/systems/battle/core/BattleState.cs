using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleState
{
    private const int MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE = 2;

    internal const int LowHpAttackDisadvantagePercent = 30;

    internal const int LogEntryLimit = 10000;

    internal const int LogTextByteLimit = 10 * 1024 * 1024;

    private static readonly StringName[] StrongAttackDisadvantageStatusIdOrder =
    {
        "blind",
        "blinded",
        "fear",
        "feared",
        "frightened",
        "frozen",
        "heavy_fatigue",
        "petrified",
        "shocked",
        "staggered",
        "stunned",
        "terrified",
        "exhausted",
    };
    private static readonly HashSet<StringName> StrongAttackDisadvantageStatusIds =
        new(StrongAttackDisadvantageStatusIdOrder);

    internal readonly struct BattleCellEntry
    {
        public BattleCellEntry(Vector2I coord, BattleCellState cell)
        {
            Coord = coord;
            Cell = cell;
        }

        public Vector2I Coord { get; }
        public BattleCellState Cell { get; }

        public void Deconstruct(out Vector2I coord, out BattleCellState cell)
        {
            coord = Coord;
            cell = Cell;
        }
    }

    internal readonly struct BattleUnitEntry
    {
        public BattleUnitEntry(StringName unitId, BattleUnitState unit)
        {
            UnitId = unitId;
            Unit = unit;
        }

        public StringName UnitId { get; }
        public BattleUnitState Unit { get; }

        public void Deconstruct(out StringName unitId, out BattleUnitState unit)
        {
            unitId = UnitId;
            unit = Unit;
        }
    }

    public static bool IsStrongAttackDisadvantageStatusId(StringName statusId) =>
        StrongAttackDisadvantageStatusIds.Contains(statusId);

    internal static IReadOnlyList<StringName> StrongAttackDisadvantageStatusIdsTyped() =>
        new List<StringName>(StrongAttackDisadvantageStatusIdOrder);

    public StringName battle_id = "";

    public long seed;

    public int attack_roll_nonce;

    public StringName phase = "timeline_running";

    public Vector2I map_size = Vector2I.Zero;

    public Vector2I world_coord = Vector2I.Zero;

    public StringName encounter_anchor_id = "";

    public StringName terrain_profile_id = "default";

    private BattleEnvironmentSnapshot _environmentSnapshot = BattleEnvironmentSnapshot.Empty();

    public StringNameList attack_disadvantage_tags = new();

    private readonly Dictionary<Vector2I, List<BattleCellState>> _cellColumns = new();

    public StringNameList ally_unit_ids = new();

    public StringNameList enemy_unit_ids = new();

    public BattleTimelineState timeline = new BattleTimelineState();

    public StringName active_unit_id = "";

    private BattleObjectiveRuntimeState _objectiveRuntimeState;

    private BattleFinalDecision _finalDecision;

    public StringName winner_faction_id => _finalDecision?.WinnerFactionId ?? "";

    public StringList log_entries = new();

    private readonly List<IReadOnlyDictionary<string, object>> _reportEntries = new();
    public ReadOnlyCollection<IReadOnlyDictionary<string, object>> report_entries =>
        BuildReportEntrySnapshots();

    public WarehouseState party_backpack_view = new WarehouseState();

    private readonly List<Dictionary<string, object>> _promotionQueue = new();

    internal IReadOnlyList<IReadOnlyDictionary<string, object>> PromotionQueueSnapshots =>
        BuildPromotionQueueSnapshots();

    public StringName modal_state = "";

    private readonly Dictionary<Vector3I, BattleEdgeFaceState> _runtimeEdgeFaces = new();

    public bool runtime_edges_dirty = true;

    private readonly BattleBarrierStore _layeredBarrierStore = new();
    private readonly List<BattleEquipmentTargetMarkState> _equipmentTargetMarks = new();
    private readonly List<BattleTemporaryEdgeFeatureState> _temporaryEdgeFeatures = new();

    private readonly Dictionary<Vector2I, BattleCellState> _cellsByCoord = new();
    private readonly Dictionary<StringName, BattleUnitState> _unitsById = new();
    private int _log_text_byte_size;
    private long _movement_geometry_revision;
    private ulong _next_cast_sequence = 1;
    private int _next_temporary_edge_feature_sequence = 1;

    internal IReadOnlyDictionary<Vector2I, BattleCellState> CellIndex => _cellsByCoord;
    internal IReadOnlyDictionary<StringName, BattleUnitState> UnitIndex => _unitsById;
    internal int CellCount => _cellsByCoord.Count;
    internal int UnitCount => _unitsById.Count;
    internal int CellColumnCount => _cellColumns.Count;
    internal int RuntimeEdgeFaceCount => _runtimeEdgeFaces.Count;
    internal int LayeredBarrierFieldCount => _layeredBarrierStore.Count;
    internal int EquipmentTargetMarkCount => _equipmentTargetMarks.Count;
    internal int TemporaryEdgeFeatureCount => _temporaryEdgeFeatures.Count;
    internal int ReportEntryCount => _reportEntries.Count;
    internal IReadOnlyList<IReadOnlyDictionary<string, object>> ReportEntriesTyped =>
        BuildReportEntrySnapshots();
    internal IReadOnlyList<IReadOnlyDictionary<string, object>> PromotionQueueTyped =>
        BuildPromotionQueueSnapshots();
    internal BattleBarrierStore LayeredBarrierStore => _layeredBarrierStore;
    internal long MovementGeometryRevision => _movement_geometry_revision;

    internal long CaptureMovementGeometryRevisionForMutationSnapshot() =>
        _movement_geometry_revision;

    internal void RestoreMovementGeometryRevisionForMutationSnapshot(long revision) =>
        _movement_geometry_revision = revision;
    internal BattleObjectiveRuntimeState ObjectiveRuntimeState => _objectiveRuntimeState;
    internal BattleFinalDecision FinalDecision => _finalDecision;

    internal bool InitializeObjective(BattleObjectiveDefinition objectiveDefinition)
    {
        BattleObjectiveRuntimeStateFactory.TryCreate(
            this,
            objectiveDefinition,
            out _objectiveRuntimeState
        );
        _finalDecision = null;
        return _objectiveRuntimeState != null;
    }

    internal bool TryLatchFinalDecision(BattleFinalDecision decision)
    {
        if (decision == null || _finalDecision != null)
            return false;
        if (
            _objectiveRuntimeState == null
            || _objectiveRuntimeState.Mode != decision.ObjectiveMode
        )
            throw new InvalidOperationException(
                "Battle final decision does not match the active objective."
            );
        _finalDecision = decision;
        return true;
    }

    internal void RestoreObjectiveState(
        BattleObjectiveRuntimeState objectiveRuntimeState,
        BattleFinalDecision finalDecision
    )
    {
        if (
            finalDecision != null
            && (
                objectiveRuntimeState == null
                || objectiveRuntimeState.Mode != finalDecision.ObjectiveMode
            )
        )
        {
            throw new InvalidOperationException(
                "Battle objective snapshot contains a final decision that does not match its runtime objective."
            );
        }
        _objectiveRuntimeState = objectiveRuntimeState?.DuplicateState();
        _finalDecision = finalDecision?.DuplicateState();
    }

    internal void MarkMovementGeometryChanged()
    {
        unchecked
        {
            _movement_geometry_revision += 1;
        }
    }

    internal BattlePhaseKind PhaseKind
    {
        get => BattleTypedNames.ToPhaseKind(phase);
        set => phase = BattleTypedNames.ToStringName(value);
    }

    internal BattleModalStateKind ModalStateKind
    {
        get => BattleTypedNames.ToModalStateKind(modal_state);
        set => modal_state = BattleTypedNames.ToStringName(value);
    }

    public BattleEnvironmentSnapshot GetEnvironmentSnapshot() =>
        _environmentSnapshot ?? BattleEnvironmentSnapshot.Empty();

    internal void ReplaceEnvironmentSnapshot(BattleEnvironmentSnapshot snapshot)
    {
        _environmentSnapshot = snapshot?.DuplicateState() ?? BattleEnvironmentSnapshot.Empty();
    }

    public void ResetLogEntries(IEnumerable<string> entries)
    {
        log_entries.Clear();
        _log_text_byte_size = 0;
        foreach (string e in entries)
            AppendLogEntry(e);
    }

    public void ClearLogEntries()
    {
        log_entries.Clear();
        _log_text_byte_size = 0;
    }

    public void AppendLogEntry(string entry)
    {
        var ne = entry.StripEdges();
        if (ne.Length == 0)
            return;
        log_entries.Add(ne);
        _log_text_byte_size += _estimate_log_text_bytes(ne);
        _trim_log_entries();
    }

    internal void SetReportEntries(
        IEnumerable<IReadOnlyDictionary<string, object>> values
    )
    {
        _reportEntries.Clear();
        if (values == null)
            return;
        foreach (IReadOnlyDictionary<string, object> value in values)
            AddReportEntry(value);
    }

    internal void AddReportEntry(IReadOnlyDictionary<string, object> reportEntry)
    {
        if (reportEntry == null || reportEntry.Count == 0)
            return;
        _reportEntries.Add(
            new ReadOnlyDictionary<string, object>(
                RuntimePlainPayload.CloneDictionary(reportEntry)
            )
        );
    }

    private ReadOnlyCollection<IReadOnlyDictionary<string, object>> BuildReportEntrySnapshots()
    {
        var result = new List<IReadOnlyDictionary<string, object>>(_reportEntries.Count);
        foreach (IReadOnlyDictionary<string, object> entry in _reportEntries)
        {
            result.Add(
                new ReadOnlyDictionary<string, object>(
                    RuntimePlainPayload.CloneDictionary(entry)
                )
            );
        }
        return result.AsReadOnly();
    }

    private ReadOnlyCollection<IReadOnlyDictionary<string, object>> BuildPromotionQueueSnapshots()
    {
        var result = new List<IReadOnlyDictionary<string, object>>(_promotionQueue.Count);
        foreach (IReadOnlyDictionary<string, object> entry in _promotionQueue)
        {
            result.Add(
                new ReadOnlyDictionary<string, object>(
                    RuntimePlainPayload.CloneDictionary(entry)
                )
            );
        }
        return result.AsReadOnly();
    }

    internal void SetPromotionQueue(System.Collections.IEnumerable values)
    {
        SetPlainPayloadEntries(_promotionQueue, values, "BattleState.promotion_queue");
    }

    public int GetLogTextByteSize() => _log_text_byte_size;

    public int NextAttackRollNonce()
    {
        attack_roll_nonce = Mathf.Max(attack_roll_nonce, 0) + 1;
        return attack_roll_nonce;
    }

    internal ulong AllocateCastSequence()
    {
        ulong result = _next_cast_sequence;
        unchecked
        {
            _next_cast_sequence++;
        }
        if (_next_cast_sequence == 0)
            _next_cast_sequence = 1;
        return result;
    }

    public string GetLogBudgetSummaryText() =>
        $"{log_entries.Count} 条 / {_log_text_byte_size / (1024.0 * 1024.0):F2} MiB";

    public bool IsAttackDisadvantage(BattleUnitState attacker, BattleUnitState defender = null)
    {
        if (attacker == null || !attacker.is_alive)
            return false;

        if (defender == attacker)
            return false;

        if (attack_disadvantage_tags.Count > 0)
            return true;

        if (_count_adjacent_enemy_units(attacker) >= MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE)
            return true;

        if (_is_low_hp_hardship(attacker))
            return true;

        var tauntEntry = attacker.GetStatusEffect("taunted");

        if (tauntEntry != null)
        {
            var sourceId = ProgressionDataUtils.to_string_name(tauntEntry.source_unit_id);
            TryGetUnitTyped(sourceId, out BattleUnitState sourceUnit);
            if (
                _is_enemy_unit(attacker, sourceUnit)
                && defender != null
                && defender.unit_id != sourceId
            )
                return true;
        }

        return _has_strong_attack_debuff(attacker);
    }

    internal bool IsAttackDisadvantage(
        BattleUnitReadView attacker,
        BattleUnitReadView defender = default
    )
    {
        if (!attacker.IsValid || !attacker.IsAlive)
            return false;

        if (defender.IsValid && defender.UnitId == attacker.UnitId)
            return false;

        if (attack_disadvantage_tags.Count > 0)
            return true;

        if (_count_adjacent_enemy_units(attacker) >= MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE)
            return true;

        if (_is_low_hp_hardship(attacker))
            return true;

        StringName tauntSourceId = attacker.GetStatusSourceUnitId("taunted");
        if (tauntSourceId != "")
        {
            BattleUnitReadView sourceUnit = new(GetUnit(tauntSourceId));
            if (
                _is_enemy_unit(attacker, sourceUnit)
                && defender.IsValid
                && defender.UnitId != tauntSourceId
            )
                return true;
        }

        return _has_strong_attack_debuff(attacker);
    }

    public bool IsEmpty() =>
        battle_id == ""
        && CellCount == 0
        && UnitCount == 0
        && ally_unit_ids.Count == 0
        && enemy_unit_ids.Count == 0;

    public WarehouseState GetPartyBackpackView()
    {
        if (party_backpack_view == null)
            party_backpack_view = new WarehouseState();
        return party_backpack_view;
    }

    public void SetPartyBackpackView(WarehouseState backpackState)
    {
        party_backpack_view = backpackState?.DuplicateState() ?? new WarehouseState();
    }

    public EquipmentState GetUnitEquipmentView(StringName unitId)
    {
        BattleUnitState us = GetUnit(unitId);
        return us?.GetEquipmentView();
    }

    public bool SetUnitEquipmentView(StringName unitId, EquipmentState es)
    {
        BattleUnitState us = GetUnit(unitId);
        if (us == null)
            return false;
        us.SetEquipmentView(es);
        return true;
    }

    public void MarkRuntimeEdgesDirty()
    {
        if (!runtime_edges_dirty)
        {
            runtime_edges_dirty = true;
            MarkMovementGeometryChanged();
        }
    }

    internal void MarkTemporaryEdgeGeometryChanged()
    {
        runtime_edges_dirty = true;
        MarkMovementGeometryChanged();
    }

    public void NormalizeUnitIdArrays()
    {
        StringNameList normalizedAllyUnitIds = _normalize_string_name_array(ally_unit_ids);
        StringNameList normalizedEnemyUnitIds = _normalize_string_name_array(enemy_unit_ids);
        bool changed = false;
        if (!_string_name_lists_equal(ally_unit_ids, normalizedAllyUnitIds))
        {
            ally_unit_ids = normalizedAllyUnitIds;
            changed = true;
        }
        if (!_string_name_lists_equal(enemy_unit_ids, normalizedEnemyUnitIds))
        {
            enemy_unit_ids = normalizedEnemyUnitIds;
            changed = true;
        }
        if (changed)
            MarkMovementGeometryChanged();
    }

    public List<StringName> GetAllyUnitIdsTyped() =>
        new(_normalize_string_name_array(ally_unit_ids));

    public List<StringName> GetEnemyUnitIdsTyped() =>
        new(_normalize_string_name_array(enemy_unit_ids));

    internal BattleStateReadView AsReadView() => new(this);

    internal bool ContainsCell(Vector2I coord) => _cellsByCoord.ContainsKey(coord);

    internal bool ContainsUnit(StringName unitId) =>
        NormalizeUnitId(unitId) is StringName normalized
        && normalized != ""
        && _unitsById.ContainsKey(normalized);

    internal BattleCellState GetCell(Vector2I coord) =>
        _cellsByCoord.TryGetValue(coord, out BattleCellState cellState) ? cellState : null;

    internal BattleUnitState GetUnit(StringName unitId)
    {
        StringName normalized = NormalizeUnitId(unitId);
        return normalized != "" && _unitsById.TryGetValue(normalized, out BattleUnitState unitState)
            ? unitState
            : null;
    }

    internal BattleUnitState GetAliveUnit(StringName unitId)
    {
        BattleUnitState unitState = GetUnit(unitId);
        return unitState != null && unitState.is_alive ? unitState : null;
    }

    internal BattleUnitReadView GetUnitView(StringName unitId) => new(GetUnit(unitId));

    internal BattleCellReadView GetCellView(Vector2I coord) => new(GetCell(coord));

    internal IEnumerable<BattleCellState> Cells()
    {
        foreach (BattleCellState cellState in _cellsByCoord.Values)
        {
            if (cellState != null)
                yield return cellState;
        }
    }

    internal IEnumerable<BattleUnitState> Units()
    {
        foreach (BattleUnitState unitState in _unitsById.Values)
        {
            if (unitState != null)
                yield return unitState;
        }
    }

    internal IEnumerable<BattleUnitState> AliveUnits()
    {
        foreach (BattleUnitState unitState in Units())
        {
            if (unitState.is_alive)
                yield return unitState;
        }
    }

    internal List<BattleCellEntry> CellEntries(bool sorted = false)
    {
        var results = new List<BattleCellEntry>();
        foreach ((Vector2I coord, BattleCellState cellState) in _cellsByCoord)
        {
            if (cellState != null)
                results.Add(new BattleCellEntry(coord, cellState));
        }
        if (sorted)
            results.Sort(CompareCellEntries);
        return results;
    }

    internal List<BattleUnitEntry> UnitEntries(bool sorted = false)
    {
        var results = new List<BattleUnitEntry>();
        foreach ((StringName unitId, BattleUnitState unitState) in _unitsById)
        {
            if (unitId != "" && unitState != null)
                results.Add(new BattleUnitEntry(unitId, unitState));
        }
        if (sorted)
            results.Sort(CompareUnitEntries);
        return results;
    }

    internal void SetCell(BattleCellState cellState)
    {
        if (cellState == null)
            return;
        _cellsByCoord[cellState.coord] = cellState;
        MarkMovementGeometryChanged();
    }

    internal void SetCell(Vector2I coord, BattleCellState cellState)
    {
        if (cellState == null)
            return;
        cellState.SetCoord(coord);
        SetCell(cellState);
    }

    internal bool RemoveCell(Vector2I coord)
    {
        bool removed = _cellsByCoord.Remove(coord);
        if (removed)
            MarkMovementGeometryChanged();
        return removed;
    }

    internal void ClearCells()
    {
        if (_cellsByCoord.Count == 0)
            return;
        _cellsByCoord.Clear();
        MarkMovementGeometryChanged();
    }

    internal void SetUnit(BattleUnitState unitState)
    {
        if (unitState == null)
            return;
        StringName unitId = NormalizeUnitId(unitState.unit_id);
        if (unitId == "")
            return;
        unitState.unit_id = unitId;
        unitState.NormalizeBodySizeProjectionForOwnerWrite();
        _unitsById[unitId] = unitState;
        MarkMovementGeometryChanged();
    }

    internal bool RemoveUnit(StringName unitId)
    {
        StringName normalized = NormalizeUnitId(unitId);
        if (normalized == "")
            return false;
        bool removed = _unitsById.Remove(normalized);
        if (removed)
            MarkMovementGeometryChanged();
        return removed;
    }

    internal void ClearUnits()
    {
        if (_unitsById.Count == 0)
            return;
        _unitsById.Clear();
        MarkMovementGeometryChanged();
    }

    internal void ClearBattleTopology()
    {
        bool changed =
            _cellsByCoord.Count > 0 || _unitsById.Count > 0 || _temporaryEdgeFeatures.Count > 0;
        DisposeStoredCellColumns();
        DisposeStoredRuntimeEdgeFaces();
        _cellsByCoord.Clear();
        _unitsById.Clear();
        _cellColumns.Clear();
        _runtimeEdgeFaces.Clear();
        _temporaryEdgeFeatures.Clear();
        if (changed)
            MarkMovementGeometryChanged();
    }

    internal void SetCells(IEnumerable<BattleCellState> cellStates, bool rebuildColumns = true)
    {
        _cellsByCoord.Clear();
        if (cellStates != null)
        {
            foreach (BattleCellState cellState in cellStates)
            {
                if (cellState != null)
                    _cellsByCoord[cellState.coord] = cellState;
            }
        }
        if (rebuildColumns)
            RebuildCellColumns();
        MarkMovementGeometryChanged();
    }

    internal void SetCells(IReadOnlyDictionary<Vector2I, BattleCellState> cellStates, bool rebuildColumns = true)
    {
        _cellsByCoord.Clear();
        if (cellStates != null)
        {
            foreach ((Vector2I coord, BattleCellState cellState) in cellStates)
            {
                if (cellState == null)
                    continue;
                cellState.SetCoord(coord);
                _cellsByCoord[coord] = cellState;
            }
        }
        if (rebuildColumns)
            RebuildCellColumns();
        MarkMovementGeometryChanged();
    }

    internal void ReplaceCellsForMutationSnapshotExact(
        IReadOnlyDictionary<Vector2I, BattleCellState> cellStates
    )
    {
        _cellsByCoord.Clear();
        if (cellStates != null)
        {
            foreach (KeyValuePair<Vector2I, BattleCellState> entry in cellStates)
            {
                _cellsByCoord[entry.Key] = entry.Value;
            }
        }
        MarkMovementGeometryChanged();
    }

    internal void SetCellsFromDictionary(
        Godot.Collections.Dictionary cellStates,
        bool duplicateCells = false,
        bool rebuildColumns = true
    )
    {
        _cellsByCoord.Clear();
        if (cellStates != null)
        {
            foreach (Variant rawKey in cellStates.Keys)
            {
                if (rawKey.VariantType != Variant.Type.Vector2I)
                    continue;
                Vector2I coord = rawKey.AsVector2I();
                if (!BattleCellState.TryReadCellPayload(cellStates[rawKey], out BattleCellState cellState))
                    continue;
                if (cellState == null)
                    continue;
                BattleCellState ownedCell = duplicateCells ? cellState.DuplicateCell() : cellState;
                ownedCell.SetCoord(coord);
                _cellsByCoord[coord] = ownedCell;
            }
        }
        if (rebuildColumns)
            RebuildCellColumns();
        MarkMovementGeometryChanged();
    }

    internal void SetUnitsFromDictionary(
        Godot.Collections.Dictionary unitStates,
        bool duplicateUnits = false
    )
    {
        _unitsById.Clear();
        if (unitStates != null)
        {
            foreach (Variant rawKey in unitStates.Keys)
            {
                StringName unitId = NormalizeUnitId(rawKey);
                if (unitId == "")
                    continue;
                if (!BattleUnitState.TryReadUnitPayload(unitStates[rawKey], out BattleUnitState unitState)
                    || unitState == null)
                    continue;
                BattleUnitState ownedUnit = duplicateUnits ? unitState.clone() : unitState;
                ownedUnit.unit_id = NormalizeUnitId(ownedUnit.unit_id) != ""
                    ? NormalizeUnitId(ownedUnit.unit_id)
                    : unitId;
                ownedUnit.NormalizeBodySizeProjectionForOwnerWrite();
                _unitsById[unitId] = ownedUnit;
            }
        }
        MarkMovementGeometryChanged();
    }

    internal void SetUnits(IEnumerable<BattleUnitState> unitStates)
    {
        _unitsById.Clear();
        if (unitStates != null)
        {
            foreach (BattleUnitState unitState in unitStates)
            {
                if (unitState == null)
                    continue;
                StringName unitId = NormalizeUnitId(unitState.unit_id);
                if (unitId == "")
                    continue;
                unitState.unit_id = unitId;
                unitState.NormalizeBodySizeProjectionForOwnerWrite();
                _unitsById[unitId] = unitState;
            }
        }
        MarkMovementGeometryChanged();
    }

    internal void ReplaceUnitsForMutationSnapshotExact(
        IEnumerable<KeyValuePair<StringName, BattleUnitState>> unitStates
    )
    {
        _unitsById.Clear();
        if (unitStates != null)
        {
            foreach (KeyValuePair<StringName, BattleUnitState> entry in unitStates)
            {
                _unitsById[entry.Key] = entry.Value;
            }
        }
        MarkMovementGeometryChanged();
    }

    internal void RebuildCellColumns()
    {
        ReplaceCellColumns(BattleCellState.BuildColumnsFromSurfaceCells(_cellsByCoord));
    }

    internal IReadOnlyDictionary<Vector2I, List<BattleCellState>> ProjectCellColumnsTyped() =>
        _cellColumns;

    internal void ReplaceCellColumns(
        IReadOnlyDictionary<Vector2I, List<BattleCellState>> columns
    )
    {
        DisposeStoredCellColumns();
        if (columns == null)
            return;
        foreach ((Vector2I coord, List<BattleCellState> column) in columns)
            _cellColumns[coord] = DuplicateCellColumn(column);
    }

    internal void ReplaceCellColumnsForMutationSnapshotExact(
        IReadOnlyDictionary<Vector2I, List<BattleCellState>> columns
    )
    {
        DisposeStoredCellColumns();
        if (columns == null)
        {
            return;
        }
        foreach (KeyValuePair<Vector2I, List<BattleCellState>> entry in columns)
        {
            _cellColumns[entry.Key] = entry.Value;
        }
    }

    internal void ReplaceCellColumnsFromPayload(Godot.Collections.Dictionary payload)
    {
        DisposeStoredCellColumns();
        if (payload == null)
            return;
        foreach (Variant key in payload.Keys)
        {
            if (key.VariantType != Variant.Type.Vector2I)
                continue;
            List<BattleCellState> column = BattleCellState.ParseColumnPayload(payload[key]);
            if (column != null)
                _cellColumns[key.AsVector2I()] = column;
        }
    }

    internal void PutCellColumn(Vector2I coord, List<BattleCellState> column)
    {
        DisposeStoredCellColumn(coord);
        if (column != null)
            _cellColumns[coord] = DuplicateCellColumn(column);
    }

    internal void RemoveCellColumnPayload(Vector2I coord)
    {
        DisposeStoredCellColumn(coord);
        _cellColumns.Remove(coord);
    }

    internal IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> ProjectRuntimeEdgeFaces() =>
        _runtimeEdgeFaces;

    internal void ReplaceRuntimeEdgeFaces(
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces
    )
    {
        _runtimeEdgeFaces.Clear();
        if (edgeFaces == null)
        {
            return;
        }
        foreach ((Vector3I key, BattleEdgeFaceState edgeFace) in edgeFaces)
        {
            if (edgeFace != null)
            {
                _runtimeEdgeFaces[key] = edgeFace;
            }
        }
    }

    internal void ClearRuntimeEdgeFaces()
    {
        _runtimeEdgeFaces.Clear();
    }

    internal Godot.Collections.Dictionary ProjectLayeredBarrierFields() =>
        _layeredBarrierStore.ProjectPayload();

    internal void ReplaceLayeredBarrierFieldsPayload(Godot.Collections.Dictionary payload) =>
        _layeredBarrierStore.ReplaceFromPayload(payload ?? new Godot.Collections.Dictionary());

    internal void ReplaceLayeredBarrierFieldsTyped(
        IEnumerable<KeyValuePair<StringName, BattleBarrierInstanceState>> barriers
    ) => _layeredBarrierStore.ReplaceWith(barriers);

    internal void ReplaceLayeredBarrierFieldsForMutationSnapshotExact(
        IEnumerable<KeyValuePair<StringName, BattleBarrierInstanceState>> barriers
    ) => _layeredBarrierStore.ReplaceWithForMutationSnapshotExact(barriers);

    internal void PutLayeredBarrierField(StringName key, BattleBarrierInstanceState barrier)
    {
        if (key == "")
            return;
        _layeredBarrierStore.Put(key, barrier);
    }

    internal void PutLayeredBarrierFieldPayload(StringName key, Godot.Collections.Dictionary payload)
    {
        if (key == "")
            return;
        _layeredBarrierStore.PutFromPayload(key, payload ?? new Godot.Collections.Dictionary());
    }

    internal void RemoveLayeredBarrierFieldPayload(StringName key)
    {
        if (key == "")
            return;
        _layeredBarrierStore.Remove(key);
    }

    internal bool TryGetLayeredBarrierField(StringName key, out BattleBarrierInstanceState barrier)
    {
        barrier = null;
        if (key == "")
            return false;
        return _layeredBarrierStore.TryGet(key, out barrier);
    }

    internal bool TryGetLayeredBarrierFieldPayload(
        StringName key,
        out Godot.Collections.Dictionary payload
    )
    {
        payload = new Godot.Collections.Dictionary();
        if (key == "")
            return false;
        if (!_layeredBarrierStore.TryGet(key, out BattleBarrierInstanceState barrier))
            return false;
        payload = barrier.ToRuntimeDict();
        return true;
    }

    internal List<StringName> GetUnitIdsTyped(bool sorted = false)
    {
        var results = new List<StringName>();
        foreach (StringName unitId in _unitsById.Keys)
        {
            if (unitId != "")
                results.Add(unitId);
        }

        if (sorted)
        {
            results.Sort(
                (left, right) => string.CompareOrdinal(left.ToString(), right.ToString())
            );
        }
        return results;
    }

    internal List<BattleUnitState> GetUnitsTyped()
    {
        var results = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in Units())
            results.Add(unitState);
        return results;
    }

    internal IReadOnlyList<BattleEquipmentTargetMarkState> GetEquipmentTargetMarksTyped()
    {
        var result = new List<BattleEquipmentTargetMarkState>();
        foreach (BattleEquipmentTargetMarkState mark in _equipmentTargetMarks)
        {
            if (mark?.IsValid == true)
                result.Add(mark.DuplicateState());
        }
        return result;
    }

    internal List<BattleEquipmentTargetMarkState>
        CaptureEquipmentTargetMarksForMutationSnapshotExact()
    {
        var result = new List<BattleEquipmentTargetMarkState>();
        foreach (BattleEquipmentTargetMarkState mark in _equipmentTargetMarks)
        {
            result.Add(mark?.DuplicateState());
        }
        return result;
    }

    internal void ReplaceEquipmentTargetMarksForMutationSnapshotExact(
        IEnumerable<BattleEquipmentTargetMarkState> marks
    )
    {
        _equipmentTargetMarks.Clear();
        if (marks == null)
        {
            return;
        }
        foreach (BattleEquipmentTargetMarkState mark in marks)
        {
            _equipmentTargetMarks.Add(mark?.DuplicateState());
        }
    }

    internal void ReplaceEquipmentTargetMarksTyped(
        IEnumerable<BattleEquipmentTargetMarkState> marks
    )
    {
        _equipmentTargetMarks.Clear();
        foreach (
            BattleEquipmentTargetMarkState mark in
            marks ?? Array.Empty<BattleEquipmentTargetMarkState>()
        )
        {
            if (mark?.IsValid == true)
                _equipmentTargetMarks.Add(mark.DuplicateState());
        }
    }

    internal ulong CaptureNextCastSequence() => _next_cast_sequence;

    internal void RestoreNextCastSequence(ulong nextCastSequence)
    {
        _next_cast_sequence = nextCastSequence;
    }

    internal int CaptureNextTemporaryEdgeFeatureSequence() =>
        _next_temporary_edge_feature_sequence;

    internal void RestoreNextTemporaryEdgeFeatureSequence(int nextSequence)
    {
        _next_temporary_edge_feature_sequence = nextSequence;
    }

    internal bool SetEquipmentTargetMark(
        BattleEquipmentTargetMarkState mark,
        bool uniquePerSource,
        out BattleEquipmentTargetMarkState replaced
    )
    {
        replaced = null;
        if (mark?.IsValid != true)
            return false;
        for (int index = _equipmentTargetMarks.Count - 1; index >= 0; index--)
        {
            BattleEquipmentTargetMarkState existing = _equipmentTargetMarks[index];
            if (existing?.IsValid != true)
            {
                _equipmentTargetMarks.RemoveAt(index);
                continue;
            }
            if (!uniquePerSource || !existing.IsSameSource(mark))
                continue;
            replaced = existing.DuplicateState();
            _equipmentTargetMarks.RemoveAt(index);
        }
        _equipmentTargetMarks.Add(mark.DuplicateState());
        return true;
    }

    internal bool RemoveEquipmentTargetMark(
        StringName sourceUnitId,
        StringName sourceEquipmentInstanceId,
        StringName bindingId,
        StringName stateKey
    )
    {
        bool removed = false;
        for (int index = _equipmentTargetMarks.Count - 1; index >= 0; index--)
        {
            BattleEquipmentTargetMarkState existing = _equipmentTargetMarks[index];
            if (existing?.IsValid != true)
            {
                _equipmentTargetMarks.RemoveAt(index);
                removed = true;
                continue;
            }
            if (
                existing.SourceUnitId == sourceUnitId
                && existing.SourceEquipmentInstanceId == sourceEquipmentInstanceId
                && existing.BindingId == bindingId
                && existing.StateKey == stateKey
            )
            {
                _equipmentTargetMarks.RemoveAt(index);
                removed = true;
            }
        }
        return removed;
    }

    internal bool TryGetEquipmentTargetMark(
        StringName sourceUnitId,
        StringName sourceEquipmentInstanceId,
        StringName bindingId,
        StringName stateKey,
        out BattleEquipmentTargetMarkState mark
    )
    {
        mark = null;
        foreach (BattleEquipmentTargetMarkState existing in _equipmentTargetMarks)
        {
            if (
                existing?.IsValid == true
                && existing.SourceUnitId == sourceUnitId
                && existing.SourceEquipmentInstanceId == sourceEquipmentInstanceId
                && existing.BindingId == bindingId
                && existing.StateKey == stateKey
            )
            {
                mark = existing.DuplicateState();
                return true;
            }
        }
        return false;
    }

    internal IReadOnlyList<BattleTemporaryEdgeFeatureState> GetTemporaryEdgeFeaturesTyped()
    {
        var result = new List<BattleTemporaryEdgeFeatureState>();
        foreach (BattleTemporaryEdgeFeatureState feature in _temporaryEdgeFeatures)
            if (feature?.IsValid == true)
                result.Add(feature.DuplicateState());
        return result;
    }

    internal IReadOnlyList<BattleTemporaryEdgeFeatureState> GetTemporaryEdgeFeaturesForProjection()
    {
        var result = new List<BattleTemporaryEdgeFeatureState>();
        int currentTu = Math.Max(timeline?.current_tu ?? 0, 0);
        foreach (BattleTemporaryEdgeFeatureState feature in _temporaryEdgeFeatures)
        {
            if (feature?.IsValid != true || feature.IsExpired(currentTu))
                continue;
            if (feature.SourceUnitId != "")
            {
                BattleUnitState sourceUnit = GetUnit(feature.SourceUnitId);
                if (sourceUnit == null || !sourceUnit.is_alive)
                    continue;
            }
            result.Add(feature.DuplicateState());
        }
        return result;
    }

    internal bool PutTemporaryEdgeFeature(
        BattleTemporaryEdgeFeatureState feature,
        bool refreshExisting,
        int maxActiveEdges
    )
    {
        if (feature?.IsValid != true)
            return false;

        bool changed = RemoveInvalidTemporaryEdgeFeatures();
        if (refreshExisting)
        {
            for (int index = _temporaryEdgeFeatures.Count - 1; index >= 0; index--)
            {
                BattleTemporaryEdgeFeatureState existing = _temporaryEdgeFeatures[index];
                if (
                    existing?.IsValid == true
                    && existing.SameSource(feature)
                    && existing.SameEdge(feature)
                )
                {
                    _temporaryEdgeFeatures.RemoveAt(index);
                    changed = true;
                }
            }
        }

        BattleTemporaryEdgeFeatureState stored = CopyTemporaryEdgeFeatureWithSequence(
            feature,
            _next_temporary_edge_feature_sequence++
        );
        _temporaryEdgeFeatures.Add(stored);
        changed = true;
        changed |= EnforceTemporaryEdgeFeatureLimit(stored, Math.Max(maxActiveEdges, 0));
        if (changed)
            MarkTemporaryEdgeGeometryChanged();
        return true;
    }

    internal int RemoveExpiredTemporaryEdgeFeatures()
    {
        int currentTu = Math.Max(timeline?.current_tu ?? 0, 0);
        int removed = 0;
        for (int index = _temporaryEdgeFeatures.Count - 1; index >= 0; index--)
        {
            BattleTemporaryEdgeFeatureState feature = _temporaryEdgeFeatures[index];
            bool remove =
                feature?.IsValid != true
                || feature.IsExpired(currentTu)
                || (
                    feature.SourceUnitId != ""
                    && (GetUnit(feature.SourceUnitId)?.is_alive != true)
                );
            if (!remove)
                continue;
            _temporaryEdgeFeatures.RemoveAt(index);
            removed++;
        }
        if (removed > 0)
            MarkTemporaryEdgeGeometryChanged();
        return removed;
    }

    internal void ReplaceTemporaryEdgeFeaturesTyped(
        IEnumerable<BattleTemporaryEdgeFeatureState> features
    )
    {
        _temporaryEdgeFeatures.Clear();
        int maxSequence = 0;
        if (features != null)
        {
            foreach (BattleTemporaryEdgeFeatureState feature in features)
            {
                if (feature?.IsValid != true)
                    continue;
                BattleTemporaryEdgeFeatureState copy = feature.DuplicateState();
                _temporaryEdgeFeatures.Add(copy);
                if (copy.Sequence > maxSequence)
                    maxSequence = copy.Sequence;
            }
        }
        _next_temporary_edge_feature_sequence = Math.Max(maxSequence + 1, 1);
        MarkTemporaryEdgeGeometryChanged();
    }

    internal List<BattleTemporaryEdgeFeatureState>
        CaptureTemporaryEdgeFeaturesForMutationSnapshotExact()
    {
        var result = new List<BattleTemporaryEdgeFeatureState>();
        foreach (BattleTemporaryEdgeFeatureState feature in _temporaryEdgeFeatures)
        {
            result.Add(feature?.DuplicateState());
        }
        return result;
    }

    internal void ReplaceTemporaryEdgeFeaturesForMutationSnapshotExact(
        IEnumerable<BattleTemporaryEdgeFeatureState> features
    )
    {
        _temporaryEdgeFeatures.Clear();
        if (features == null)
        {
            return;
        }
        foreach (BattleTemporaryEdgeFeatureState feature in features)
        {
            _temporaryEdgeFeatures.Add(feature?.DuplicateState());
        }
    }

    internal List<BattleCellEntry> GetCellEntriesTyped() => CellEntries();

    internal bool TryGetCellTyped(Vector2I coord, out BattleCellState cellState)
    {
        cellState = GetCell(coord);
        return cellState != null;
    }

    internal List<BattleUnitEntry> GetUnitEntriesTyped() => UnitEntries();

    internal bool TryGetUnitTyped(StringName unitId, out BattleUnitState unitState)
    {
        unitState = GetUnit(unitId);
        return unitState != null;
    }

    private void DisposeStoredCellColumns()
    {
        if (_cellColumns.Count == 0)
            return;
        foreach (List<BattleCellState> column in _cellColumns.Values)
            DisposeCellColumn(column);
        _cellColumns.Clear();
    }

    private void DisposeStoredCellColumn(Vector2I coord)
    {
        if (!_cellColumns.TryGetValue(coord, out List<BattleCellState> column))
            return;
        DisposeCellColumn(column);
    }

    private void DisposeStoredRuntimeEdgeFaces()
    {
        _runtimeEdgeFaces.Clear();
    }

    private bool RemoveInvalidTemporaryEdgeFeatures()
    {
        bool removed = false;
        for (int index = _temporaryEdgeFeatures.Count - 1; index >= 0; index--)
        {
            if (_temporaryEdgeFeatures[index]?.IsValid == true)
                continue;
            _temporaryEdgeFeatures.RemoveAt(index);
            removed = true;
        }
        return removed;
    }

    private bool EnforceTemporaryEdgeFeatureLimit(
        BattleTemporaryEdgeFeatureState sourceFeature,
        int maxActiveEdges
    )
    {
        if (sourceFeature == null || maxActiveEdges <= 0)
            return false;

        bool changed = false;
        while (CountTemporaryEdgeFeaturesForSource(sourceFeature) > maxActiveEdges)
        {
            int oldestIndex = FindOldestTemporaryEdgeFeatureIndex(sourceFeature);
            if (oldestIndex < 0)
                break;
            _temporaryEdgeFeatures.RemoveAt(oldestIndex);
            changed = true;
        }
        return changed;
    }

    private int CountTemporaryEdgeFeaturesForSource(BattleTemporaryEdgeFeatureState sourceFeature)
    {
        int count = 0;
        foreach (BattleTemporaryEdgeFeatureState feature in _temporaryEdgeFeatures)
            if (feature?.IsValid == true && feature.SameSource(sourceFeature))
                count++;
        return count;
    }

    private int FindOldestTemporaryEdgeFeatureIndex(BattleTemporaryEdgeFeatureState sourceFeature)
    {
        int oldestIndex = -1;
        BattleTemporaryEdgeFeatureState oldest = null;
        for (int index = 0; index < _temporaryEdgeFeatures.Count; index++)
        {
            BattleTemporaryEdgeFeatureState feature = _temporaryEdgeFeatures[index];
            if (feature?.IsValid != true || !feature.SameSource(sourceFeature))
                continue;
            if (
                oldest == null
                || feature.CreatedAtTu < oldest.CreatedAtTu
                || (
                    feature.CreatedAtTu == oldest.CreatedAtTu
                    && feature.Sequence < oldest.Sequence
                )
            )
            {
                oldest = feature;
                oldestIndex = index;
            }
        }
        return oldestIndex;
    }

    private static BattleTemporaryEdgeFeatureState CopyTemporaryEdgeFeatureWithSequence(
        BattleTemporaryEdgeFeatureState feature,
        int sequence
    )
    {
        return new BattleTemporaryEdgeFeatureState
        {
            OriginCoord = feature.OriginCoord,
            Direction = feature.Direction,
            SourceUnitId = feature.SourceUnitId,
            SourceEquipmentInstanceId = feature.SourceEquipmentInstanceId,
            BindingId = feature.BindingId,
            ActionId = feature.ActionId,
            CreatedAtTu = feature.CreatedAtTu,
            ExpiresAtTu = feature.ExpiresAtTu,
            Sequence = sequence,
            Feature = feature.Feature?.DuplicateFeature(),
        };
    }

    private static void DisposeCellColumn(List<BattleCellState> column)
    {
        if (column == null)
            return;
        foreach (BattleCellState cell in column)
            BattleCellState.DisposeRuntimeGraph(cell);
        column.Clear();
    }

    private static List<BattleCellState> DuplicateCellColumn(IEnumerable<BattleCellState> column)
    {
        List<BattleCellState> result = new();
        if (column == null)
            return result;
        foreach (BattleCellState cell in column)
            if (cell != null)
                result.Add(cell.DuplicateCell());
        return result;
    }

    private static bool TryAsGodotObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variantValue)
        {
            if (variantValue.VariantType == Variant.Type.Object)
            {
                value = variantValue.AsGodotObject() as T;
                return value != null;
            }
            value = null;
            return false;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static void SetPlainPayloadEntries(
        List<Dictionary<string, object>> target,
        System.Collections.IEnumerable values,
        string ownerPath
    )
    {
        target.Clear();
        if (values == null)
            return;
        int index = 0;
        foreach (object value in values)
        {
            if (TryAsDictionary(value, out GDictionary payload))
            {
                target.Add(
                    RuntimePlainPayload.NormalizeDictionary(
                        payload,
                        $"{ownerPath}[{index}]"
                    )
                );
            }
            index++;
        }
    }

    private static bool TryAsDictionary(object value, out GDictionary dictionary)
    {
        if (value is GDictionary dictionaryValue)
        {
            dictionary = dictionaryValue;
            return true;
        }
        if (value is Variant variantValue && variantValue.VariantType == Variant.Type.Dictionary)
        {
            dictionary = variantValue.AsGodotDictionary();
            return true;
        }
        dictionary = null;
        return false;
    }

    private void _trim_log_entries()
    {
        while (log_entries.Count > LogEntryLimit || _log_text_byte_size > LogTextByteLimit)
        {
            if (log_entries.Count == 0)
            {
                _log_text_byte_size = 0;
                return;
            }
            string removed = log_entries[0];
            log_entries.RemoveAt(0);
            _log_text_byte_size = Mathf.Max(
                _log_text_byte_size - _estimate_log_text_bytes(removed),
                0
            );
        }
    }

    private static int _estimate_log_text_bytes(string entry) =>
        System.Text.Encoding.UTF8.GetByteCount(entry) + 1;

    private static StringNameList _normalize_string_name_array(IEnumerable<StringName> values)
    {
        var results = new StringNameList();

        if (values == null)
            return results;

        foreach (StringName value in values)
        {
            StringName id = ProgressionDataUtils.to_string_name(value);

            if (id.ToString().Length > 0)
                results.Add(id);
        }

        return results;
    }

    private static bool _string_name_lists_equal(
        IReadOnlyList<StringName> left,
        IReadOnlyList<StringName> right
    )
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.Count != right.Count)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index])
                return false;
        }
        return true;
    }

    private int _count_adjacent_enemy_units(BattleUnitState attacker)
    {
        if (attacker == null)
            return 0;

        var adjacentEnemyIds = new HashSet<StringName>();

        foreach (BattleUnitState c in GetUnitsTyped())
        {
            if (!_is_enemy_unit(attacker, c))
                continue;
            if (_are_units_adjacent(attacker, c))
                adjacentEnemyIds.Add(c.unit_id);
        }

        return adjacentEnemyIds.Count;
    }

    private int _count_adjacent_enemy_units(BattleUnitReadView attacker)
    {
        if (!attacker.IsValid)
            return 0;

        var adjacentEnemyIds = new HashSet<StringName>();

        foreach (BattleUnitState candidateState in GetUnitsTyped())
        {
            BattleUnitReadView candidate = new(candidateState);
            if (!_is_enemy_unit(attacker, candidate))
                continue;
            if (_are_units_adjacent(attacker, candidate))
                adjacentEnemyIds.Add(candidate.UnitId);
        }

        return adjacentEnemyIds.Count;
    }

    private static bool _is_enemy_unit(BattleUnitState a, BattleUnitState c)
    {
        if (a == null || c == null || c == a || c.unit_id == a.unit_id || !c.is_alive)
            return false;
        return a.faction_id != c.faction_id;
    }

    private static bool _is_enemy_unit(BattleUnitReadView a, BattleUnitReadView c)
    {
        if (
            !a.IsValid
            || !c.IsValid
            || c.UnitId == a.UnitId
            || !c.IsAlive
        )
            return false;
        return a.FactionId != c.FactionId;
    }

    private static bool _are_units_adjacent(BattleUnitState a, BattleUnitState b)
    {
        if (a == null || b == null)
            return false;
        foreach (var ac in a.occupied_coords)
        foreach (var bc in b.occupied_coords)
            if (Mathf.Abs(ac.X - bc.X) + Mathf.Abs(ac.Y - bc.Y) == 1)
                return true;
        return false;
    }

    private static bool _are_units_adjacent(BattleUnitReadView a, BattleUnitReadView b)
    {
        if (!a.IsValid || !b.IsValid)
            return false;
        foreach (Vector2I ac in a.GetOccupiedCoords())
        foreach (Vector2I bc in b.GetOccupiedCoords())
            if (Mathf.Abs(ac.X - bc.X) + Mathf.Abs(ac.Y - bc.Y) == 1)
                return true;
        return false;
    }

    private bool _is_low_hp_hardship(BattleUnitState attacker)
    {
        if (attacker?.attribute_snapshot == null)
            return false;

        int maxHp = Mathf.Max(attacker.attribute_snapshot.GetValue("hp_max"), 0);

        if (maxHp <= 0)
            return false;

        return attacker.current_hp * 100 <= maxHp * LowHpAttackDisadvantagePercent;
    }

    private bool _is_low_hp_hardship(BattleUnitReadView attacker)
    {
        if (!attacker.IsValid)
            return false;

        int maxHp = Mathf.Max(attacker.GetAttributeValue("hp_max"), 0);
        if (maxHp <= 0)
            return false;

        return attacker.CurrentHp * 100 <= maxHp * LowHpAttackDisadvantagePercent;
    }

    private static bool _has_strong_attack_debuff(BattleUnitState attacker)
    {
        if (attacker == null)
            return false;
        foreach (StringName statusId in StrongAttackDisadvantageStatusIdOrder)
            if (attacker.HasStatusEffect(statusId))
                return true;
        return false;
    }

    private static bool _has_strong_attack_debuff(BattleUnitReadView attacker)
    {
        if (!attacker.IsValid)
            return false;
        foreach (StringName statusId in StrongAttackDisadvantageStatusIdOrder)
            if (attacker.HasStatusEffect(statusId))
                return true;
        return false;
    }

    private static StringName NormalizeUnitId(object unitId) =>
        ProgressionDataUtils.to_string_name(unitId);

    private static int CompareCellEntries(BattleCellEntry left, BattleCellEntry right)
    {
        int yCompare = left.Coord.Y.CompareTo(right.Coord.Y);
        return yCompare != 0 ? yCompare : left.Coord.X.CompareTo(right.Coord.X);
    }

    private static int CompareUnitEntries(BattleUnitEntry left, BattleUnitEntry right) =>
        string.CompareOrdinal(left.UnitId.ToString(), right.UnitId.ToString());
}
