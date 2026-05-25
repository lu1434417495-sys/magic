using Godot;

[GlobalClass]
public partial class BattleState : RefCounted
{
    private static readonly Script AttributeServiceScript = GD.Load<Script>("res://scripts/systems/attributes/attribute_service.gd");
    private const int MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE = 2;
    private const int LowHpAttackDisadvantagePercent = 30;
    private const int LogEntryLimit = 10000;
    private const int LogTextByteLimit = 10 * 1024 * 1024;
    private static readonly Godot.Collections.Dictionary StrongAttackDisadvantageStatusIds = new() { {"blind",true},{"blinded",true},{"fear",true},{"feared",true},{"frozen",true},{"heavy_fatigue",true},{"petrified",true},{"shocked",true},{"staggered",true},{"stunned",true},{"terrified",true},{"exhausted",true} };

    public static int LOW_HP_ATTACK_DISADVANTAGE_PERCENT() => LowHpAttackDisadvantagePercent;
    public static int LOG_ENTRY_LIMIT() => LogEntryLimit;
    public static int LOG_TEXT_BYTE_LIMIT() => LogTextByteLimit;
    public static Godot.Collections.Dictionary STRONG_ATTACK_DISADVANTAGE_STATUS_IDS() => StrongAttackDisadvantageStatusIds.Duplicate();

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

    public void reset_log_entries(Godot.Collections.Array<string> entries) { log_entries.Clear(); _log_text_byte_size = 0; foreach (string e in entries) append_log_entry(e); }
    public void clear_log_entries() { log_entries.Clear(); _log_text_byte_size = 0; }
    public void append_log_entry(string entry) { var ne = entry.StripEdges(); if (ne.Length == 0) return; log_entries.Add(ne); _log_text_byte_size += _estimate_log_text_bytes(ne); _trim_log_entries(); }
    public int get_log_text_byte_size() => _log_text_byte_size;
    public string get_log_budget_summary_text() => $"{log_entries.Count} 条 / {_log_text_byte_size / (1024.0 * 1024.0):F2} MiB";

    public bool is_attack_disadvantage(BattleUnitState attacker, BattleUnitState defender = null)
    {
        if (attacker == null || !attacker.is_alive) return false;
        if (defender == attacker) return false;
        if (attack_disadvantage_tags.Count > 0) return true;
        if (_count_adjacent_enemy_units(attacker) >= MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE) return true;
        if (_is_low_hp_hardship(attacker)) return true;
        var tauntEntry = attacker.get_status_effect("taunted");
        if (tauntEntry != null) { var sourceId = ProgressionDataUtils.to_string_name(tauntEntry.source_unit_id); var sourceUnit = units.ContainsKey(sourceId) ? units[sourceId].AsGodotObject() as BattleUnitState : null; if (_is_enemy_unit(attacker, sourceUnit) && defender != null && defender.unit_id != sourceId) return true; }
        return _has_strong_attack_debuff(attacker);
    }

    public bool is_empty() => battle_id == "" && cells.Count == 0 && units.Count == 0 && ally_unit_ids.Count == 0 && enemy_unit_ids.Count == 0;
    public WarehouseState get_party_backpack_view() { if (party_backpack_view == null) party_backpack_view = new WarehouseState(); return party_backpack_view; }
    public void set_party_backpack_view(GodotObject backpackState) { party_backpack_view = (backpackState != null && backpackState.HasMethod("duplicate_state")) ? (WarehouseState)backpackState.Call("duplicate_state") : new WarehouseState(); }
    public GodotObject get_unit_equipment_view(StringName unitId) { var us = units.ContainsKey(unitId) ? units[unitId].AsGodotObject() as BattleUnitState : null; return us?.Call("get_equipment_view").AsGodotObject(); }
    public bool set_unit_equipment_view(StringName unitId, GodotObject es) { var us = units.ContainsKey(unitId) ? units[unitId].AsGodotObject() as BattleUnitState : null; if (us == null) return false; us.Call("set_equipment_view", es); return true; }
    public void mark_runtime_edges_dirty() => runtime_edges_dirty = true;
    public void clear_runtime_edge_faces() { runtime_edge_faces.Clear(); runtime_edges_dirty = true; }
    public void normalize_unit_id_arrays() { ally_unit_ids = _normalize_string_name_array(ally_unit_ids); enemy_unit_ids = _normalize_string_name_array(enemy_unit_ids); }
    public Godot.Collections.Array<StringName> get_ally_unit_ids_typed() => _normalize_string_name_array(ally_unit_ids);
    public Godot.Collections.Array<StringName> get_enemy_unit_ids_typed() => _normalize_string_name_array(enemy_unit_ids);

    private void _trim_log_entries() { while (log_entries.Count > LogEntryLimit || _log_text_byte_size > LogTextByteLimit) { if (log_entries.Count == 0) { _log_text_byte_size = 0; return; } string removed = log_entries[0]; log_entries.RemoveAt(0); _log_text_byte_size = Mathf.Max(_log_text_byte_size - _estimate_log_text_bytes(removed), 0); } }
    private static int _estimate_log_text_bytes(string entry) => System.Text.Encoding.UTF8.GetByteCount(entry) + 1;
    private static Godot.Collections.Array<StringName> _normalize_string_name_array(Godot.Collections.Array<StringName> values)
    {
        var results = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values)
        {
            StringName id = ProgressionDataUtils.to_string_name(value);
            if (id.ToString().Length > 0) results.Add(id);
        }
        return results;
    }

    private int _count_adjacent_enemy_units(BattleUnitState attacker)
    {
        if (attacker == null) return 0;
        attacker.refresh_footprint();
        var adjacentEnemyIds = new Godot.Collections.Dictionary();
        foreach (var uv in units.Values) { var c = uv.AsGodotObject() as BattleUnitState; if (!_is_enemy_unit(attacker, c)) continue; c.refresh_footprint(); if (_are_units_adjacent(attacker, c)) adjacentEnemyIds[c.unit_id] = true; }
        return adjacentEnemyIds.Count;
    }
    private static bool _is_enemy_unit(BattleUnitState a, BattleUnitState c) { if (a == null || c == null || c == a || c.unit_id == a.unit_id || !c.is_alive) return false; return a.faction_id != c.faction_id; }
    private static bool _are_units_adjacent(BattleUnitState a, BattleUnitState b) { if (a == null || b == null) return false; foreach (var ac in a.occupied_coords) foreach (var bc in b.occupied_coords) if (Mathf.Abs(ac.X - bc.X) + Mathf.Abs(ac.Y - bc.Y) == 1) return true; return false; }

    private bool _is_low_hp_hardship(BattleUnitState attacker)
    {
        if (attacker?.attribute_snapshot == null) return false;
        int maxHp = Mathf.Max(attacker.attribute_snapshot.Call("get_value", (StringName)"hp_max").AsInt32(), 0);
        if (maxHp <= 0) return false;
        return attacker.current_hp * 100 <= maxHp * LowHpAttackDisadvantagePercent;
    }
    private static bool _has_strong_attack_debuff(BattleUnitState attacker) { if (attacker == null) return false; foreach (var statusIdV in StrongAttackDisadvantageStatusIds.Keys) if (attacker.has_status_effect(new StringName(statusIdV.AsString()))) return true; return false; }
}
