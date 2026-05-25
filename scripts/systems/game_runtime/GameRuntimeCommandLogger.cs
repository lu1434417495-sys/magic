using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeCommandLogger : RefCounted
{
    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public new void Dispose()
    {
        _runtime = null;
    }

    public Dictionary ExecuteLoggedCommand(string eventId, string domain, Dictionary context, Callable action)
    {
        return ExecuteLoggedCommandInternal(eventId, domain, context, action);
    }

    public void LogActiveCommandScopeResult(Dictionary result)
    {
        LogActiveCommandScopeResultInternal(result);
    }

    public Dictionary BuildRuntimeLogState()
    {
        return BuildRuntimeLogStateInternal();
    }

    public void LogRuntimeEvent(string level, string domain, string eventId, string message, Dictionary context = null)
    {
        LogRuntimeEventInternal(level, domain, eventId, message, context ?? new Dictionary());
    }

    public void LogBattleBatchEntries(GodotObject batch)
    {
        LogBattleBatchEntriesInternal(batch);
    }

    public Dictionary BuildBattleLogState()
    {
        return BuildBattleLogStateInternal();
    }

    public Dictionary BuildBattleBatchLogContext(GodotObject batch)
    {
        return BuildBattleBatchLogContextInternal(batch);
    }

    public Variant NormalizeLogVariant(Variant value)
    {
        return NormalizeLogVariantInternal(value);
    }

    private Dictionary ExecuteLoggedCommandInternal(string eventId, string domain, Dictionary context, Callable action)
    {
        var activeScope = _runtime.Get("_active_command_log_scope").AsGodotDictionary().Duplicate(true);
        var commandArgs = NormalizeLogVariantInternal(context).AsGodotDictionary();
        var beforeState = BuildRuntimeLogStateInternal();

        if (domain == "battle")
        {
            var pendingBatches = _runtime.Get("_pending_command_battle_batches").AsGodotArray();
            pendingBatches.Clear();
        }

        _runtime.Set("_active_command_log_scope", new Dictionary
        {
            ["event_id"] = eventId,
            ["domain"] = domain,
            ["context"] = new Dictionary
            {
                ["command_args"] = commandArgs,
                ["before"] = beforeState,
            },
            ["logged"] = false,
        });

        var resultVariant = action.Call();
        var result = resultVariant.VariantType == Variant.Type.Dictionary ? resultVariant.AsGodotDictionary() : new Dictionary();

        var currentScope = _runtime.Get("_active_command_log_scope").AsGodotDictionary();
        if (!DictionaryGet(currentScope, "logged", false).AsBool())
            LogCommandResultInternal(currentScope, result);

        _runtime.Set("_active_command_log_scope", activeScope);

        if (domain == "battle")
        {
            var pendingBatches = _runtime.Get("_pending_command_battle_batches").AsGodotArray();
            pendingBatches.Clear();
        }

        return result;
    }

    private void LogActiveCommandScopeResultInternal(Dictionary result)
    {
        var scope = _runtime.Get("_active_command_log_scope").AsGodotDictionary();
        if (scope.Count == 0)
            return;
        if (DictionaryGet(scope, "logged", false).AsBool())
            return;
        LogCommandResultInternal(scope, result);
    }

    private void LogCommandResultInternal(Dictionary scope, Dictionary result)
    {
        if (scope.Count == 0)
            return;

        var resolvedResult = result ?? new Dictionary();
        var ok = DictionaryGet(resolvedResult, "ok", false).AsBool();
        var message = DictionaryGet(resolvedResult, "message", _runtime.Get("_current_status_message").AsString()).AsString();
        var logContext = (DictionaryGet(scope, "context", new Dictionary()).AsGodotDictionary()).Duplicate(true);
        var afterState = BuildRuntimeLogStateInternal();
        logContext["runtime"] = afterState;
        logContext["ok"] = ok;
        if (!string.IsNullOrEmpty(message))
            logContext["result_message"] = message;

        var battleRefreshMode = DictionaryGet(resolvedResult, "battle_refresh_mode", "").AsString();
        if (!string.IsNullOrEmpty(battleRefreshMode))
            logContext["battle_refresh_mode"] = battleRefreshMode;

        var scopeDomain = DictionaryGet(scope, "domain", "").AsString();
        var pendingBatches = _runtime.Get("_pending_command_battle_batches").AsGodotArray();
        if (scopeDomain == "battle" && pendingBatches.Count > 0)
        {
            logContext["battle_batches"] = pendingBatches.Duplicate(true);
            var lastBatch = pendingBatches[-1].AsGodotDictionary().Duplicate(true);
            logContext["battle_batch"] = lastBatch;
            logContext["battle_changed_units"] = CollectCommandBattleChangedUnits(pendingBatches);
        }

        var eventLevel = ok ? "info" : "warn";
        var eventDomain = string.IsNullOrEmpty(scopeDomain) ? "runtime" : scopeDomain;
        var eventId = DictionaryGet(scope, "event_id", "runtime.command").AsString();
        var eventMessage = !string.IsNullOrEmpty(message) ? message : (ok ? "命令成功。" : "命令失败。");

        LogRuntimeEventInternal(eventLevel, eventDomain, eventId, eventMessage, logContext);
        scope["logged"] = true;
        _runtime.Set("_active_command_log_scope", scope);
    }

    private Dictionary BuildRuntimeLogStateInternal()
    {
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        var worldMapContext = _runtime.Get("_world_map_data_context").AsGodotObject();

        var context = new Dictionary
        {
            ["save_id"] = gameSession != null && gameSession.HasMethod("get_active_save_id") ? gameSession.Call("get_active_save_id").AsString() : "",
            ["map_id"] = worldMapContext != null ? worldMapContext.Get("active_map_id").AsString() : "",
            ["map_display_name"] = worldMapContext != null ? worldMapContext.Get("active_map_display_name").AsString() : "",
            ["player_coord"] = _runtime.Get("_player_coord").AsVector2I(),
            ["selected_coord"] = _runtime.Get("_selected_coord").AsVector2I(),
            ["active_modal_id"] = _runtime.Get("_active_modal_id").AsString(),
            ["battle_active"] = _runtime.Call("_is_battle_active").AsBool(),
        };

        if (_runtime.Call("_is_battle_active").AsBool())
            context["battle"] = BuildBattleLogStateInternal();

        return context;
    }

    private void LogRuntimeEventInternal(string level, string domain, string eventId, string message, Dictionary context)
    {
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        if (gameSession == null)
            return;
        if (!gameSession.HasMethod("log_event"))
            return;
        gameSession.Call("log_event", level, domain, eventId, message, context);
    }

    private void LogBattleBatchEntriesInternal(GodotObject batch)
    {
        if (batch == null)
            return;
        var logLines = batch.Get("log_lines").AsGodotArray();
        if (logLines.Count == 0)
            return;

        var baseContext = BuildBattleBatchLogContextInternal(batch);
        baseContext["runtime"] = BuildRuntimeLogStateInternal();
        foreach (var logLine in logLines)
        {
            LogRuntimeEventInternal("info", "battle", "battle.log", logLine.AsString(), baseContext);
        }
    }

    private Dictionary BuildBattleLogStateInternal()
    {
        if (!_runtime.Call("_is_battle_active").AsBool())
            return new Dictionary();

        var battleState = _runtime.Get("_battle_state").As<BattleState>();
        if (battleState == null)
            return new Dictionary();

        var allyAliveCount = 0;
        var hostileAliveCount = 0;
        foreach (var unitVariant in battleState.units.Values)
        {
            var unitState = unitVariant.As<BattleUnitState>();
            if (unitState == null || !unitState.is_alive)
                continue;
            if (unitState.faction_id.ToString() == _runtime.Get("_player_faction_id").AsString())
                allyAliveCount++;
            else
                hostileAliveCount++;
        }

        return new Dictionary
        {
            ["encounter_id"] = _runtime.Get("_active_battle_encounter_id").AsString(),
            ["encounter_name"] = _runtime.Get("_active_battle_encounter_name").AsString(),
            ["battle_id"] = battleState.battle_id.ToString(),
            ["seed"] = battleState.seed,
            ["terrain_profile_id"] = battleState.terrain_profile_id.ToString(),
            ["map_size"] = battleState.map_size,
            ["phase"] = battleState.phase.ToString(),
            ["modal_state"] = battleState.modal_state.ToString(),
            ["winner_faction_id"] = battleState.winner_faction_id.ToString(),
            ["active_unit_id"] = battleState.active_unit_id.ToString(),
            ["active_unit_name"] = _runtime.Call("_get_battle_active_unit_name").AsString(),
            ["selected_coord"] = _runtime.Get("_battle_selected_coord").AsVector2I(),
            ["selected_skill_id"] = _runtime.Get("_selected_battle_skill_id").AsString(),
            ["selected_skill_variant_id"] = _runtime.Get("_selected_battle_skill_variant_id").AsString(),
            ["selected_target_coord_count"] = _runtime.Get("_queued_battle_skill_target_coords").AsGodotArray().Count,
            ["selected_target_unit_count"] = _runtime.Get("_queued_battle_skill_target_unit_ids").AsGodotArray().Count,
            ["terrain_counts"] = _runtime.Call("_count_battle_terrain_types"),
            ["ally_alive_count"] = allyAliveCount,
            ["hostile_alive_count"] = hostileAliveCount,
            ["units"] = BuildBattleUnitLogEntries(),
        };
    }

    private Dictionary BuildBattleBatchLogContextInternal(GodotObject batch)
    {
        if (batch == null)
            return new Dictionary();
        return new Dictionary
        {
            ["phase_changed"] = batch.Get("phase_changed").AsBool(),
            ["battle_ended"] = batch.Get("battle_ended").AsBool(),
            ["modal_requested"] = batch.Get("modal_requested").AsBool(),
            ["changed_unit_count"] = batch.Get("changed_unit_ids").AsGodotArray().Count,
            ["changed_coord_count"] = batch.Get("changed_coords").AsGodotArray().Count,
            ["changed_coords"] = NormalizeLogVariantInternal(batch.Get("changed_coords")),
            ["changed_unit_ids"] = NormalizeLogVariantInternal(batch.Get("changed_unit_ids")),
            ["changed_units"] = BuildBattleUnitLogEntries(batch.Get("changed_unit_ids").AsGodotArray()),
            ["report_entry_count"] = batch.Get("report_entries").AsGodotArray().Count,
            ["report_entries"] = NormalizeLogVariantInternal(batch.Get("report_entries")),
        };
    }

    private Godot.Collections.Array<Dictionary> CollectCommandBattleChangedUnits(Godot.Collections.Array batchContexts)
    {
        var mergedByUnitId = new Dictionary();
        var orderedUnitIds = new Array<string>();
        foreach (var batchContextVariant in batchContexts)
        {
            var batchContext = batchContextVariant.AsGodotDictionary();
            if (batchContext == null)
                continue;
            var changedUnitsVariant = DictionaryGet(batchContext, "changed_units", default(Variant));
            if (changedUnitsVariant.VariantType != Variant.Type.Array)
                continue;
            var changedUnits = changedUnitsVariant.AsGodotArray();
            foreach (var changedUnitVariant in changedUnits)
            {
                if (changedUnitVariant.VariantType != Variant.Type.Dictionary)
                    continue;
                var changedUnit = changedUnitVariant.AsGodotDictionary();
                var unitId = DictionaryGet(changedUnit, "unit_id", "").AsString().StripEdges();
                if (string.IsNullOrEmpty(unitId))
                    continue;
                if (!mergedByUnitId.ContainsKey(unitId))
                    orderedUnitIds.Add(unitId);
                mergedByUnitId[unitId] = changedUnit.Duplicate(true);
            }
        }

        var result = new Array<Dictionary>();
        foreach (var unitId in orderedUnitIds)
        {
            if (mergedByUnitId.ContainsKey(unitId))
                result.Add(mergedByUnitId[unitId].AsGodotDictionary().Duplicate(true));
        }
        return result;
    }

    private Godot.Collections.Array<Dictionary> BuildBattleUnitLogEntries(Godot.Collections.Array unitIds = null)
    {
        var result = new Array<Dictionary>();
        var battleState = _runtime.Get("_battle_state").As<BattleState>();
        if (battleState == null)
            return result;

        var normalizedIds = new Array<StringName>();
        if (unitIds == null || unitIds.Count == 0)
        {
            foreach (var unitKey in ProgressionDataUtils.sorted_string_keys(battleState.units))
                normalizedIds.Add((StringName)unitKey);
        }
        else
        {
            foreach (var unitIdVariant in unitIds)
            {
                var normalizedUnitId = ProgressionDataUtils.to_string_name(unitIdVariant);
                if (normalizedUnitId == "")
                    continue;
                if (normalizedIds.Contains(normalizedUnitId))
                    continue;
                normalizedIds.Add(normalizedUnitId);
            }
        }

        foreach (var unitId in normalizedIds)
        {
            var unitState = battleState.units[unitId].As<BattleUnitState>();
            if (unitState == null)
                continue;
            result.Add(new Dictionary
            {
                ["unit_id"] = unitState.unit_id.ToString(),
                ["display_name"] = !string.IsNullOrEmpty(unitState.display_name) ? unitState.display_name : unitState.unit_id.ToString(),
                ["faction_id"] = unitState.faction_id.ToString(),
                ["control_mode"] = unitState.control_mode.ToString(),
                ["is_alive"] = unitState.is_alive,
                ["coord"] = unitState.coord,
                ["current_hp"] = (int)unitState.current_hp,
                ["current_mp"] = (int)unitState.current_mp,
                ["current_stamina"] = (int)unitState.current_stamina,
                ["current_aura"] = (int)unitState.current_aura,
                ["current_ap"] = (int)unitState.current_ap,
                ["current_move_points"] = (int)unitState.current_move_points,
            });
        }
        return result;
    }

    private Variant NormalizeLogVariantInternal(Variant value)
    {
        switch (value.VariantType)
        {
            case Variant.Type.StringName:
                return value.AsStringName().ToString();
            case Variant.Type.Vector2I:
                var coord = value.AsVector2I();
                return new Dictionary { ["x"] = coord.X, ["y"] = coord.Y };
            case Variant.Type.Vector2:
                var floatCoord = value.AsVector2();
                return new Dictionary { ["x"] = floatCoord.X, ["y"] = floatCoord.Y };
            case Variant.Type.Dictionary:
                var dict = value.AsGodotDictionary();
                var normalizedDict = new Dictionary();
                foreach (var key in dict.Keys)
                    normalizedDict[key.AsString()] = NormalizeLogVariantInternal(dict[key]);
                return normalizedDict;
            case Variant.Type.Array:
                var array = value.AsGodotArray();
                var normalizedArray = new Godot.Collections.Array();
                foreach (var entry in array)
                    normalizedArray.Add(NormalizeLogVariantInternal(entry));
                return normalizedArray;
            case Variant.Type.Object:
                var obj = value.AsGodotObject();
                if (obj == null)
                    return default(Variant);
                if (obj.HasMethod("to_dict"))
                    return NormalizeLogVariantInternal(obj.Call("to_dict"));
                return obj.ToString();
            default:
                return value;
        }
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }
}
