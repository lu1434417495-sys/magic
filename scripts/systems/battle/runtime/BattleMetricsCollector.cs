using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleMetricsCollector : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName TypeMove = "move";
    private static readonly StringName TypeWait = "wait";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    public void setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public void _initialize_battle_metrics()
    {
        BattleRuntimeModule runtime = _runtime;
        BattleState state = runtime?._state;
        var metrics = new GDictionary
        {
            ["battle_id"] =
                state != null ? (state.battle_id ?? Empty).ToString() : "",
            ["seed"] = state?.seed ?? 0,
            ["units"] = new GDictionary(),
            ["factions"] = new GDictionary(),
        };
        if (runtime != null)
        {
            runtime._battle_metrics = metrics;
        }
        if (state == null)
        {
            return;
        }

        GDictionary units = EnsureDict(metrics, "units");
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            if (unitState == null)
            {
                continue;
            }
            GDictionary unitEntry = _build_unit_metric_entry(unitState);
            units[unitState.unit_id.ToString()] = unitEntry;
            GDictionary factionEntry = _ensure_faction_metric_entry(unitState.faction_id);
            factionEntry["unit_count"] = GetInt(factionEntry, "unit_count") + 1;
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

        GDictionary units = EnsureDict(battleMetrics, "units");
        string unitKey = unit_state.unit_id.ToString();
        if (!units.ContainsKey(unitKey))
        {
            units[unitKey] = _build_unit_metric_entry(unit_state);
        }
        return GetDict(units, unitKey);
    }

    public GDictionary _ensure_faction_metric_entry(StringName faction_id)
    {
        GDictionary battleMetrics = BattleMetrics();
        if (battleMetrics.Count == 0)
        {
            return new GDictionary();
        }

        GDictionary factions = EnsureDict(battleMetrics, "factions");
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
        }
        return GetDict(factions, factionKey);
    }

    public void _record_turn_started(BattleUnitState unit_state)
    {
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        unitEntry["turn_count"] = GetInt(unitEntry, "turn_count") + 1;
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        factionEntry["turn_count"] = GetInt(factionEntry, "turn_count") + 1;
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
        _increment_metric_count(EnsureDict(unitEntry, "action_counts"), commandKey, 1);
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        _increment_metric_count(
            EnsureDict(factionEntry, "action_counts"),
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
            EnsureDict(unitEntry, "skill_attempt_counts"),
            skillKey,
            1
        );
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        _increment_metric_count(
            EnsureDict(factionEntry, "skill_attempt_counts"),
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
            EnsureDict(unitEntry, "skill_success_counts"),
            skillKey,
            1
        );
        unitEntry["successful_skill_count"] =
            GetInt(unitEntry, "successful_skill_count") + 1;
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        _increment_metric_count(
            EnsureDict(factionEntry, "skill_success_counts"),
            skillKey,
            1
        );
        factionEntry["successful_skill_count"] =
            GetInt(factionEntry, "successful_skill_count") + 1;
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
                GetInt(sourceEntry, "total_damage_done") + damage;
            targetEntry["total_damage_taken"] =
                GetInt(targetEntry, "total_damage_taken") + damage;
            sourceFactionEntry["total_damage_done"] =
                GetInt(sourceFactionEntry, "total_damage_done") + damage;
            targetFactionEntry["total_damage_taken"] =
                GetInt(targetFactionEntry, "total_damage_taken") + damage;
        }
        if (healing > 0)
        {
            sourceEntry["total_healing_done"] =
                GetInt(sourceEntry, "total_healing_done") + healing;
            targetEntry["total_healing_received"] =
                GetInt(targetEntry, "total_healing_received") + healing;
            sourceFactionEntry["total_healing_done"] =
                GetInt(sourceFactionEntry, "total_healing_done") + healing;
            targetFactionEntry["total_healing_received"] =
                GetInt(targetFactionEntry, "total_healing_received") + healing;
        }
        if (kill_count > 0)
        {
            sourceEntry["kill_count"] = GetInt(sourceEntry, "kill_count") + kill_count;
            sourceFactionEntry["kill_count"] =
                GetInt(sourceFactionEntry, "kill_count") + kill_count;
        }
    }

    public void _record_unit_defeated(BattleUnitState unit_state)
    {
        GDictionary unitEntry = _ensure_unit_metric_entry(unit_state);
        if (unitEntry.Count == 0)
        {
            return;
        }
        unitEntry["death_count"] = GetInt(unitEntry, "death_count") + 1;
        GDictionary factionEntry = _ensure_faction_metric_entry(unit_state.faction_id);
        factionEntry["death_count"] = GetInt(factionEntry, "death_count") + 1;
    }

    public void _increment_metric_count(GDictionary metric_map, string key, int delta)
    {
        if (metric_map == null || string.IsNullOrEmpty(key))
        {
            return;
        }
        metric_map[key] = GetInt(metric_map, key) + delta;
    }

    private GDictionary BattleMetrics()
    {
        return _runtime?._battle_metrics ?? new GDictionary();
    }

    private static GDictionary GetDict(GDictionary source, object key)
    {
        return TryGetValue(source, key, out Variant value)
            && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GDictionary EnsureDict(GDictionary source, object key)
    {
        if (source == null)
        {
            return new GDictionary();
        }
        if (TryGetValue(source, key, out Variant value) && value.VariantType == Variant.Type.Dictionary)
        {
            return value.AsGodotDictionary();
        }
        var created = new GDictionary();
        source[ToVariantKey(key)] = created;
        return created;
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.Bool => value.AsBool() ? 1 : 0,
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed)
                ? parsed
                : fallback,
            Variant.Type.StringName
                => int.TryParse(value.AsStringName().ToString(), out int parsed)
                    ? parsed
                    : fallback,
            _ => fallback,
        };
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : GodotObject
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
