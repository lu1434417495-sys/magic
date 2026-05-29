using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleMetricsCollector : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName TypeMove = "move";
    private static readonly StringName TypeWait = "wait";

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public void _initialize_battle_metrics()
    {
        GodotObject runtime = _runtime;
        GodotObject state = GdInterop.GetObject(runtime, "_state");
        var metrics = new GDictionary
        {
            ["battle_id"] =
                state != null ? GdInterop.GetStringName(state, "battle_id").ToString() : "",
            ["seed"] = state != null ? GdInterop.GetInt(state, "seed") : 0,
            ["units"] = new GDictionary(),
            ["factions"] = new GDictionary(),
        };
        runtime.Set("_battle_metrics", metrics);
        if (state == null)
        {
            return;
        }

        GDictionary stateUnits = GdInterop.GetDictionary(state, "units");
        GDictionary units = GdInterop.GetDictionary(metrics, "units");
        foreach (var unitValue in stateUnits.Values)
        {
            BattleUnitState unitState =
                unitValue.VariantType == Variant.Type.Nil
                    ? null
                    : unitValue.AsGodotObject() as BattleUnitState;
            if (unitState == null)
            {
                continue;
            }
            GDictionary unitEntry = _build_unit_metric_entry(unitState);
            units[unitState.unit_id.ToString()] = unitEntry;
            GDictionary factionEntry = _ensure_faction_metric_entry(unitState.faction_id);
            factionEntry["unit_count"] = GdInterop.GetInt(factionEntry, "unit_count", 0) + 1;
        }
    }

    public GDictionary _build_unit_metric_entry(BattleUnitState unit_state)
    {
        return new GDictionary
        {
            ["unit_id"] = unit_state.unit_id.ToString(),
            ["display_name"] = unit_state.display_name,
            ["faction_id"] = unit_state.faction_id.ToString(),
            ["control_mode"] = unit_state.control_mode.ToString(),
            ["source_member_id"] = unit_state.source_member_id.ToString(),
            ["turn_count"] = 0,
            ["action_counts"] = new GDictionary
            {
                ["move"] = 0,
                ["skill"] = 0,
                ["wait"] = 0,
            },
            ["skill_attempt_counts"] = new GDictionary(),
            ["skill_success_counts"] = new GDictionary(),
            ["successful_skill_count"] = 0,
            ["total_damage_done"] = 0,
            ["total_healing_done"] = 0,
            ["total_damage_taken"] = 0,
            ["total_healing_received"] = 0,
            ["kill_count"] = 0,
            ["death_count"] = 0,
        };
    }

    public GDictionary _ensure_unit_metric_entry(BattleUnitState unit_state)
    {
        GDictionary battleMetrics = BattleMetrics();
        if (battleMetrics.Count == 0 || unit_state == null)
        {
            return new GDictionary();
        }

        GDictionary units = GdInterop.GetDictionary(battleMetrics, "units");
        string unitKey = unit_state.unit_id.ToString();
        if (!units.ContainsKey(unitKey))
        {
            units[unitKey] = _build_unit_metric_entry(unit_state);
            battleMetrics["units"] = units;
        }
        return GdInterop.GetDictionary(units, unitKey);
    }

    public GDictionary _ensure_faction_metric_entry(StringName faction_id)
    {
        GDictionary battleMetrics = BattleMetrics();
        if (battleMetrics.Count == 0)
        {
            return new GDictionary();
        }

        GDictionary factions = GdInterop.GetDictionary(battleMetrics, "factions");
        string factionKey = (faction_id ?? Empty).ToString();
        if (!factions.ContainsKey(factionKey))
        {
            factions[factionKey] = new GDictionary
            {
                ["faction_id"] = factionKey,
                ["unit_count"] = 0,
                ["turn_count"] = 0,
                ["action_counts"] = new GDictionary
                {
                    ["move"] = 0,
                    ["skill"] = 0,
                    ["wait"] = 0,
                },
                ["skill_attempt_counts"] = new GDictionary(),
                ["skill_success_counts"] = new GDictionary(),
                ["successful_skill_count"] = 0,
                ["total_damage_done"] = 0,
                ["total_healing_done"] = 0,
                ["total_damage_taken"] = 0,
                ["total_healing_received"] = 0,
                ["kill_count"] = 0,
                ["death_count"] = 0,
            };
            battleMetrics["factions"] = factions;
        }
        return GdInterop.GetDictionary(factions, factionKey);
    }

    public void _record_turn_started(BattleUnitState unit_state)
    {
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        unitEntry["turn_count"] = GdInterop.GetInt(unitEntry, "turn_count", 0) + 1;
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        factionEntry["turn_count"] = GdInterop.GetInt(factionEntry, "turn_count", 0) + 1;
    }

    public void _record_action_issued(
        BattleUnitState unit_state,
        StringName command_type,
        int ap_cost = 0
    )
    {
        if (unit_state != null)
        {
            if (command_type == TypeMove)
            {
                unit_state.has_moved_this_turn = true;
            }
            else if (command_type != TypeWait && ap_cost > 0)
            {
                unit_state.has_taken_action_this_turn = true;
                unit_state.is_resting = false;
            }
        }

        string commandKey = (command_type ?? Empty).ToString();
        if (string.IsNullOrEmpty(commandKey))
        {
            return;
        }
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        _increment_metric_count(GdInterop.GetDictionary(unitEntry, "action_counts"), commandKey, 1);
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        _increment_metric_count(
            GdInterop.GetDictionary(factionEntry, "action_counts"),
            commandKey,
            1
        );
    }

    public void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)
    {
        string skillKey = (skill_id ?? Empty).ToString();
        if (string.IsNullOrEmpty(skillKey))
        {
            return;
        }
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        _increment_metric_count(
            GdInterop.GetDictionary(unitEntry, "skill_attempt_counts"),
            skillKey,
            1
        );
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        _increment_metric_count(
            GdInterop.GetDictionary(factionEntry, "skill_attempt_counts"),
            skillKey,
            1
        );
    }

    public void _record_skill_success(BattleUnitState unit_state, StringName skill_id)
    {
        string skillKey = (skill_id ?? Empty).ToString();
        if (string.IsNullOrEmpty(skillKey))
        {
            return;
        }
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        _increment_metric_count(
            GdInterop.GetDictionary(unitEntry, "skill_success_counts"),
            skillKey,
            1
        );
        unitEntry["successful_skill_count"] =
            GdInterop.GetInt(unitEntry, "successful_skill_count", 0) + 1;
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        _increment_metric_count(
            GdInterop.GetDictionary(factionEntry, "skill_success_counts"),
            skillKey,
            1
        );
        factionEntry["successful_skill_count"] =
            GdInterop.GetInt(factionEntry, "successful_skill_count", 0) + 1;
    }

    public void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        if (source_unit == null || target_unit == null)
        {
            return;
        }
        GDictionary sourceEntry = _ensure_unit_metric_entry(source_unit);
        GDictionary targetEntry = _ensure_unit_metric_entry(target_unit);
        GDictionary sourceFactionEntry = _ensure_faction_metric_entry(source_unit.faction_id);
        GDictionary targetFactionEntry = _ensure_faction_metric_entry(target_unit.faction_id);
        if (damage > 0)
        {
            sourceEntry["total_damage_done"] =
                GdInterop.GetInt(sourceEntry, "total_damage_done", 0) + damage;
            targetEntry["total_damage_taken"] =
                GdInterop.GetInt(targetEntry, "total_damage_taken", 0) + damage;
            sourceFactionEntry["total_damage_done"] =
                GdInterop.GetInt(sourceFactionEntry, "total_damage_done", 0) + damage;
            targetFactionEntry["total_damage_taken"] =
                GdInterop.GetInt(targetFactionEntry, "total_damage_taken", 0) + damage;
        }
        if (healing > 0)
        {
            sourceEntry["total_healing_done"] =
                GdInterop.GetInt(sourceEntry, "total_healing_done", 0) + healing;
            targetEntry["total_healing_received"] =
                GdInterop.GetInt(targetEntry, "total_healing_received", 0) + healing;
            sourceFactionEntry["total_healing_done"] =
                GdInterop.GetInt(sourceFactionEntry, "total_healing_done", 0) + healing;
            targetFactionEntry["total_healing_received"] =
                GdInterop.GetInt(targetFactionEntry, "total_healing_received", 0) + healing;
        }
        if (kill_count > 0)
        {
            sourceEntry["kill_count"] = GdInterop.GetInt(sourceEntry, "kill_count", 0) + kill_count;
            sourceFactionEntry["kill_count"] =
                GdInterop.GetInt(sourceFactionEntry, "kill_count", 0) + kill_count;
        }
    }

    public void _record_unit_defeated(BattleUnitState unit_state)
    {
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        unitEntry["death_count"] = GdInterop.GetInt(unitEntry, "death_count", 0) + 1;
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        factionEntry["death_count"] = GdInterop.GetInt(factionEntry, "death_count", 0) + 1;
    }

    public void _increment_metric_count(GDictionary metric_map, string key, int delta)
    {
        metric_map[key] = GdInterop.GetInt(metric_map, key, 0) + delta;
    }

    private GDictionary BattleMetrics()
    {
        return GdInterop.GetDictionary(_runtime, "_battle_metrics");
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GodotObject target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
