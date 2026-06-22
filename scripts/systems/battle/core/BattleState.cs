using System.Collections.Generic;
using Godot;

public partial class BattleState : RefCounted
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

    public Godot.Collections.Array<StringName> attack_disadvantage_tags = new();

    private readonly RuntimePayloadStore _cellColumns = new();

    public Godot.Collections.Array<StringName> ally_unit_ids = new();

    public Godot.Collections.Array<StringName> enemy_unit_ids = new();

    public BattleTimelineState timeline = new BattleTimelineState();

    public StringName active_unit_id = "";

    public StringName winner_faction_id = "";

    public Godot.Collections.Array<string> log_entries = new();

    public Godot.Collections.Array<Godot.Collections.Dictionary> report_entries = new();

    public WarehouseState party_backpack_view = new WarehouseState();

    public Godot.Collections.Array<Godot.Collections.Dictionary> promotion_queue = new();

    public StringName modal_state = "";

    private readonly RuntimePayloadStore _runtimeEdgeFaces = new();

    public bool runtime_edges_dirty = true;

    private readonly BattleBarrierStore _layeredBarrierStore = new();

    private readonly Dictionary<Vector2I, BattleCellState> _cellsByCoord = new();
    private readonly Dictionary<StringName, BattleUnitState> _unitsById = new();
    private int _log_text_byte_size;
    private long _movement_geometry_revision;
    private ulong _next_cast_sequence = 1;

    internal IReadOnlyDictionary<Vector2I, BattleCellState> CellIndex => _cellsByCoord;
    internal IReadOnlyDictionary<StringName, BattleUnitState> UnitIndex => _unitsById;
    internal int CellCount => _cellsByCoord.Count;
    internal int UnitCount => _unitsById.Count;
    internal int CellColumnCount => _cellColumns.Count;
    internal int RuntimeEdgeFaceCount => _runtimeEdgeFaces.Count;
    internal int LayeredBarrierFieldCount => _layeredBarrierStore.Count;
    internal BattleBarrierStore LayeredBarrierStore => _layeredBarrierStore;
    internal long MovementGeometryRevision => _movement_geometry_revision;

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

    public void ResetLogEntries(Godot.Collections.Array<string> entries)
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

    public void NormalizeUnitIdArrays()
    {
        ally_unit_ids = _normalize_string_name_array(ally_unit_ids);
        enemy_unit_ids = _normalize_string_name_array(enemy_unit_ids);
        MarkMovementGeometryChanged();
    }

    public List<StringName> GetAllyUnitIdsTyped() =>
        new(_normalize_string_name_array(ally_unit_ids));

    public List<StringName> GetEnemyUnitIdsTyped() =>
        new(_normalize_string_name_array(enemy_unit_ids));

    internal BattleStateReadView AsReadView() => new(this);

    internal Godot.Collections.Dictionary ProjectCells()
    {
        var result = new Godot.Collections.Dictionary();
        foreach ((Vector2I coord, BattleCellState cell) in _cellsByCoord)
        {
            if (cell != null)
                result[coord] = cell;
        }
        return result;
    }

    internal Godot.Collections.Dictionary ProjectUnits()
    {
        var result = new Godot.Collections.Dictionary();
        foreach ((StringName unitId, BattleUnitState unit) in _unitsById)
        {
            if (unitId != "" && unit != null)
                result[unitId] = unit;
        }
        return result;
    }

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
        bool changed = _cellsByCoord.Count > 0 || _unitsById.Count > 0;
        _cellsByCoord.Clear();
        _unitsById.Clear();
        _cellColumns.Clear();
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
                BattleCellState cellState = cellStates[rawKey].As<BattleCellState>();
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
                BattleUnitState unitState = unitStates[rawKey].As<BattleUnitState>();
                if (unitState == null)
                    continue;
                BattleUnitState ownedUnit = duplicateUnits ? unitState.clone() : unitState;
                ownedUnit.unit_id = NormalizeUnitId(ownedUnit.unit_id) != ""
                    ? NormalizeUnitId(ownedUnit.unit_id)
                    : unitId;
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
                _unitsById[unitId] = unitState;
            }
        }
        MarkMovementGeometryChanged();
    }

    internal void RebuildCellColumns()
    {
        ReplaceCellColumnsPayload(BattleCellState.BuildColumnsFromSurfaceCells(_cellsByCoord));
    }

    internal Godot.Collections.Dictionary ProjectCellColumns() => _cellColumns.ProjectPayload();

    internal void ReplaceCellColumnsPayload(Godot.Collections.Dictionary payload) =>
        _cellColumns.ReplaceWithPayload(payload ?? new Godot.Collections.Dictionary());

    internal void PutCellColumnPayload(Vector2I coord, Variant columnPayload) =>
        _cellColumns.PutPayloadValue(coord, columnPayload);

    internal void RemoveCellColumnPayload(Vector2I coord) =>
        _cellColumns.RemovePayloadValue(coord);

    internal Godot.Collections.Dictionary ProjectRuntimeEdgeFaces() =>
        _runtimeEdgeFaces.ProjectPayload();

    internal void ReplaceRuntimeEdgeFacesPayload(Godot.Collections.Dictionary payload) =>
        _runtimeEdgeFaces.ReplaceWithPayload(payload ?? new Godot.Collections.Dictionary());

    internal void ClearRuntimeEdgeFaces() => _runtimeEdgeFaces.Clear();

    internal Godot.Collections.Dictionary ProjectLayeredBarrierFields() =>
        _layeredBarrierStore.ProjectPayload();

    internal void ReplaceLayeredBarrierFieldsPayload(Godot.Collections.Dictionary payload) =>
        _layeredBarrierStore.ReplaceFromPayload(payload ?? new Godot.Collections.Dictionary());

    internal void ReplaceLayeredBarrierFieldsTyped(
        IEnumerable<KeyValuePair<StringName, BattleBarrierInstanceState>> barriers
    ) => _layeredBarrierStore.ReplaceWith(barriers);

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

    private static Godot.Collections.Array<StringName> _normalize_string_name_array(
        Godot.Collections.Array<StringName> values
    )
    {
        var results = new Godot.Collections.Array<StringName>();

        foreach (StringName value in values)
        {
            StringName id = ProgressionDataUtils.to_string_name(value);

            if (id.ToString().Length > 0)
                results.Add(id);
        }

        return results;
    }

    private int _count_adjacent_enemy_units(BattleUnitState attacker)
    {
        if (attacker == null)
            return 0;

        attacker.RefreshFootprint();

        var adjacentEnemyIds = new Godot.Collections.Dictionary();

        foreach (BattleUnitState c in GetUnitsTyped())
        {
            if (!_is_enemy_unit(attacker, c))
                continue;
            c.RefreshFootprint();
            if (_are_units_adjacent(attacker, c))
                adjacentEnemyIds[c.unit_id] = true;
        }

        return adjacentEnemyIds.Count;
    }

    private int _count_adjacent_enemy_units(BattleUnitReadView attacker)
    {
        if (!attacker.IsValid)
            return 0;

        var adjacentEnemyIds = new Godot.Collections.Dictionary();

        foreach (BattleUnitState candidateState in GetUnitsTyped())
        {
            BattleUnitReadView candidate = new(candidateState);
            if (!_is_enemy_unit(attacker, candidate))
                continue;
            if (_are_units_adjacent(attacker, candidate))
                adjacentEnemyIds[candidate.UnitId] = true;
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
