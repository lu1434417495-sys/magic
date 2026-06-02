using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleState : RefCounted
{
    private const int MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE = 2;

    private const int LowHpAttackDisadvantagePercent = 30;

    private const int LogEntryLimit = 10000;

    private const int LogTextByteLimit = 10 * 1024 * 1024;

    private static readonly Godot.Collections.Dictionary StrongAttackDisadvantageStatusIds = new()
    {
        { "blind", true },
        { "blinded", true },
        { "fear", true },
        { "feared", true },
        { "frozen", true },
        { "heavy_fatigue", true },
        { "petrified", true },
        { "shocked", true },
        { "staggered", true },
        { "stunned", true },
        { "terrified", true },
        { "exhausted", true },
    };

    internal readonly struct BattleCellEntry
    {
        public BattleCellEntry(Vector2I coord, BattleCellState cell)
        {
            Coord = coord;
            Cell = cell;
        }

        public Vector2I Coord { get; }
        public BattleCellState Cell { get; }
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
    }

    public static int LOW_HP_ATTACK_DISADVANTAGE_PERCENT() => LowHpAttackDisadvantagePercent;

    public static int LOG_ENTRY_LIMIT() => LogEntryLimit;

    public static int LOG_TEXT_BYTE_LIMIT() => LogTextByteLimit;

    public static Godot.Collections.Dictionary STRONG_ATTACK_DISADVANTAGE_STATUS_IDS() =>
        StrongAttackDisadvantageStatusIds.Duplicate();

    public StringName battle_id = "";

    public int seed;

    public int attack_roll_nonce;

    public StringName phase = "timeline_running";

    public Vector2I map_size = Vector2I.Zero;

    public Vector2I world_coord = Vector2I.Zero;

    public StringName encounter_anchor_id = "";

    public StringName terrain_profile_id = "default";

    public Godot.Collections.Array<StringName> attack_disadvantage_tags = new();

    public Godot.Collections.Dictionary cells = new();

    public Godot.Collections.Dictionary cell_columns = new();

    public Godot.Collections.Dictionary units = new();

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

    public Godot.Collections.Dictionary runtime_edge_faces = new();

    public bool runtime_edges_dirty = true;

    public Godot.Collections.Dictionary layered_barrier_fields = new();

    private int _log_text_byte_size;

    public void reset_log_entries(Godot.Collections.Array<string> entries)
    {
        log_entries.Clear();
        _log_text_byte_size = 0;
        foreach (string e in entries)
            append_log_entry(e);
    }

    public void clear_log_entries()
    {
        log_entries.Clear();
        _log_text_byte_size = 0;
    }

    public void append_log_entry(string entry)
    {
        var ne = entry.StripEdges();
        if (ne.Length == 0)
            return;
        log_entries.Add(ne);
        _log_text_byte_size += _estimate_log_text_bytes(ne);
        _trim_log_entries();
    }

    public int get_log_text_byte_size() => _log_text_byte_size;

    public int next_attack_roll_nonce()
    {
        attack_roll_nonce = Mathf.Max(attack_roll_nonce, 0) + 1;
        return attack_roll_nonce;
    }

    public string get_log_budget_summary_text() =>
        $"{log_entries.Count} 条 / {_log_text_byte_size / (1024.0 * 1024.0):F2} MiB";

    public bool is_attack_disadvantage(BattleUnitState attacker, BattleUnitState defender = null)
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

        var tauntEntry = attacker.get_status_effect("taunted");

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

    public bool is_empty() =>
        battle_id == ""
        && cells.Count == 0
        && units.Count == 0
        && ally_unit_ids.Count == 0
        && enemy_unit_ids.Count == 0;

    public WarehouseState get_party_backpack_view()
    {
        if (party_backpack_view == null)
            party_backpack_view = new WarehouseState();
        return party_backpack_view;
    }

    public void set_party_backpack_view(WarehouseState backpackState)
    {
        party_backpack_view = backpackState?.duplicate_state() ?? new WarehouseState();
    }

    public EquipmentState get_unit_equipment_view(StringName unitId)
    {
        TryGetUnitTyped(unitId, out BattleUnitState us);
        return us?.get_equipment_view();
    }

    public bool set_unit_equipment_view(StringName unitId, EquipmentState es)
    {
        TryGetUnitTyped(unitId, out BattleUnitState us);
        if (us == null)
            return false;
        us.set_equipment_view(es);
        return true;
    }

    public void mark_runtime_edges_dirty() => runtime_edges_dirty = true;

    public void clear_runtime_edge_faces()
    {
        runtime_edge_faces.Clear();
        runtime_edges_dirty = true;
    }

    public void normalize_unit_id_arrays()
    {
        ally_unit_ids = _normalize_string_name_array(ally_unit_ids);
        enemy_unit_ids = _normalize_string_name_array(enemy_unit_ids);
    }

    public Godot.Collections.Array<StringName> get_ally_unit_ids_typed() =>
        _normalize_string_name_array(ally_unit_ids);

    public Godot.Collections.Array<StringName> get_enemy_unit_ids_typed() =>
        _normalize_string_name_array(enemy_unit_ids);

    internal List<StringName> GetUnitIdsTyped(bool sorted = false)
    {
        var results = new List<StringName>();
        if (units == null)
        {
            return results;
        }

        foreach (var unitIdValue in units.Keys)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(unitIdValue);
            if (unitId.ToString().Length > 0)
            {
                results.Add(unitId);
            }
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
        if (units == null)
        {
            return results;
        }

        foreach (var unitValue in units.Values)
        {
            BattleUnitState unitState = unitValue.As<BattleUnitState>();
            if (unitState != null)
            {
                results.Add(unitState);
            }
        }
        return results;
    }

    internal List<BattleCellEntry> GetCellEntriesTyped()
    {
        var results = new List<BattleCellEntry>();
        if (cells == null)
        {
            return results;
        }

        foreach (var coordValue in cells.Keys)
        {
            Vector2I coord = coordValue.AsVector2I();
            BattleCellState cellState = cells[coord].As<BattleCellState>();
            if (cellState != null)
            {
                results.Add(new BattleCellEntry(coord, cellState));
            }
        }
        return results;
    }

    internal bool TryGetCellTyped(Vector2I coord, out BattleCellState cellState)
    {
        cellState = null;
        if (cells == null || !cells.ContainsKey(coord))
        {
            return false;
        }

        cellState = cells[coord].As<BattleCellState>();
        return cellState != null;
    }

    internal List<BattleUnitEntry> GetUnitEntriesTyped()
    {
        var results = new List<BattleUnitEntry>();
        if (units == null)
        {
            return results;
        }

        foreach (var unitIdValue in units.Keys)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(unitIdValue);
            BattleUnitState unitState = unitId != "" ? units[unitId].As<BattleUnitState>() : null;
            if (unitState == null)
            {
                continue;
            }
            results.Add(new BattleUnitEntry(unitId, unitState));
        }
        return results;
    }

    internal bool TryGetUnitTyped(StringName unitId, out BattleUnitState unitState)
    {
        unitState = null;
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        if (
            units == null
            || normalized.ToString().Length == 0
            || !units.ContainsKey(normalized)
        )
        {
            return false;
        }

        unitState = units[normalized].As<BattleUnitState>();
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

        attacker.refresh_footprint();

        var adjacentEnemyIds = new Godot.Collections.Dictionary();

        foreach (BattleUnitState c in GetUnitsTyped())
        {
            if (!_is_enemy_unit(attacker, c))
                continue;
            c.refresh_footprint();
            if (_are_units_adjacent(attacker, c))
                adjacentEnemyIds[c.unit_id] = true;
        }

        return adjacentEnemyIds.Count;
    }

    private static bool _is_enemy_unit(BattleUnitState a, BattleUnitState c)
    {
        if (a == null || c == null || c == a || c.unit_id == a.unit_id || !c.is_alive)
            return false;
        return a.faction_id != c.faction_id;
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

    private bool _is_low_hp_hardship(BattleUnitState attacker)
    {
        if (attacker?.attribute_snapshot == null)
            return false;

        int maxHp = Mathf.Max(attacker.attribute_snapshot.get_value("hp_max"), 0);

        if (maxHp <= 0)
            return false;

        return attacker.current_hp * 100 <= maxHp * LowHpAttackDisadvantagePercent;
    }

    private static bool _has_strong_attack_debuff(BattleUnitState attacker)
    {
        if (attacker == null)
            return false;
        foreach (var statusIdV in StrongAttackDisadvantageStatusIds.Keys)
            if (attacker.has_status_effect(new StringName(statusIdV.AsString())))
                return true;
        return false;
    }
}
