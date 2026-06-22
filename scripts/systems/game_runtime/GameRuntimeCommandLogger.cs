using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public sealed class GameRuntimeCommandLogger
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;
    private CommandLogScope _previousCommandLogScope = CommandLogScope.Empty();
    private CommandLogScope _activeCommandLogScope = CommandLogScope.Empty();

    private sealed class CommandLogScope : IDisposable
    {
        private readonly GodotProjectionPayloadOwner _payloads = new();

        public string EventId { get; }
        public string Domain { get; }
        internal Dictionary CommandArgs { get; }
        internal Dictionary BeforeState { get; }
        public bool Logged { get; private set; }

        public bool IsEmpty => string.IsNullOrEmpty(EventId) && string.IsNullOrEmpty(Domain);

        private CommandLogScope(
            string eventId,
            string domain,
            Dictionary commandArgs,
            Dictionary beforeState,
            bool logged
        )
        {
            EventId = eventId ?? "";
            Domain = domain ?? "";
            CommandArgs =
                NormalizeLogValue(commandArgs, _payloads) as Dictionary
                ?? _payloads.Dictionary();
            BeforeState =
                NormalizeLogValue(beforeState, _payloads) as Dictionary
                ?? _payloads.Dictionary();
            Logged = logged;
        }

        public static CommandLogScope Empty() =>
            new("", "", null, null, false);

        public static CommandLogScope Create(
            string eventId,
            string domain,
            Dictionary commandArgs,
            Dictionary beforeState
        ) => new(eventId, domain, commandArgs, beforeState, false);

        public CommandLogScope Clone() =>
            new(EventId, Domain, CommandArgs, BeforeState, Logged);

        internal Dictionary BuildContext(GodotProjectionPayloadOwner payloads) =>
            payloads.Track(
                new Dictionary
                {
                    ["command_args"] = ToVariant(NormalizeLogValue(CommandArgs, payloads)),
                    ["before"] = ToVariant(NormalizeLogValue(BeforeState, payloads)),
                }
            );

        public void MarkLogged()
        {
            Logged = true;
        }

        public void Dispose()
        {
            _payloads.Dispose();
        }
    }

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    internal void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
        _previousCommandLogScope?.Dispose();
        _activeCommandLogScope?.Dispose();
        _previousCommandLogScope = CommandLogScope.Empty();
        _activeCommandLogScope = CommandLogScope.Empty();
    }

    internal void BeginLoggedCommand(string eventId, string domain, Dictionary context)
    {
        BeginLoggedCommandInternal(eventId, domain, context);
    }

    internal Dictionary FinishLoggedCommand(Dictionary result)
    {
        return FinishLoggedCommandInternal(result);
    }

    internal void LogActiveCommandScopeResult(Dictionary result)
    {
        LogActiveCommandScopeResultInternal(result);
    }

    internal Dictionary BuildRuntimeLogState()
    {
        return BuildRuntimeLogStateInternal();
    }

    internal void LogRuntimeEvent(
        string level,
        string domain,
        string eventId,
        string message,
        string context = ""
    )
    {
        LogRuntimeEventInternal(level, domain, eventId, message, context);
    }

    internal void LogBattleBatchEntries(BattleEventBatch batch)
    {
        LogBattleBatchEntriesInternal(batch);
    }

    internal Dictionary BuildBattleLogState()
    {
        return BuildBattleLogStateInternal();
    }

    internal Dictionary BuildBattleBatchLogContext(BattleEventBatch batch)
    {
        return BuildBattleBatchLogContextInternal(batch);
    }

    private void BeginLoggedCommandInternal(string eventId, string domain, Dictionary context)
    {
        _previousCommandLogScope?.Dispose();
        _previousCommandLogScope = _activeCommandLogScope.Clone();
        using var transientPayloads = new GodotProjectionPayloadOwner();
        var commandArgs =
            NormalizeLogValue(context, transientPayloads) as Dictionary
            ?? transientPayloads.Dictionary();
        var beforeState = BuildRuntimeLogStateInternal(transientPayloads);

        if (domain == "battle")
        {
            _runtime._pending_command_battle_batches.Clear();
        }

        _activeCommandLogScope?.Dispose();
        _activeCommandLogScope = CommandLogScope.Create(eventId, domain, commandArgs, beforeState);
    }

    private Dictionary FinishLoggedCommandInternal(Dictionary result)
    {
        var resolvedResult = result ?? new Dictionary();
        var currentScope = _activeCommandLogScope;
        if (!currentScope.Logged)
            LogCommandResultInternal(currentScope, resolvedResult);

        currentScope?.Dispose();
        _activeCommandLogScope = _previousCommandLogScope ?? CommandLogScope.Empty();

        if (currentScope.Domain == "battle")
            _runtime._pending_command_battle_batches.Clear();

        _previousCommandLogScope = null;
        _previousCommandLogScope = CommandLogScope.Empty();
        return resolvedResult;
    }

    private void LogActiveCommandScopeResultInternal(Dictionary result)
    {
        var scope = _activeCommandLogScope;
        if (scope == null || scope.IsEmpty)
            return;
        if (scope.Logged)
            return;
        LogCommandResultInternal(scope, result);
    }

    private void LogCommandResultInternal(CommandLogScope scope, Dictionary result)
    {
        if (scope == null || scope.IsEmpty)
            return;

        var resolvedResult = result ?? new Dictionary();
        var ok = DictionaryBool(resolvedResult, "ok", false);
        var message = DictionaryString(
            resolvedResult,
            "message",
            _runtime._current_status_message
        );
        using var logPayloads = new GodotProjectionPayloadOwner();
        var logContext = scope.BuildContext(logPayloads);
        var afterState = BuildRuntimeLogStateInternal(logPayloads);
        logContext["runtime"] = afterState;
        logContext["ok"] = ok;
        if (!string.IsNullOrEmpty(message))
            logContext["result_message"] = message;

        var battleRefreshMode = DictionaryString(resolvedResult, "battle_refresh_mode");
        if (!string.IsNullOrEmpty(battleRefreshMode))
            logContext["battle_refresh_mode"] = battleRefreshMode;

        var scopeDomain = scope.Domain;
        var pendingBatches = _runtime._pending_command_battle_batches;
        if (scopeDomain == "battle" && pendingBatches.Count > 0)
        {
            logContext["battle_batches"] = logPayloads.Track(pendingBatches.Duplicate(true));
            var lastBatch = logPayloads.Track(
                pendingBatches[pendingBatches.Count - 1]
                    .AsGodotDictionary()
                    .Duplicate(true)
            );
            logContext["battle_batch"] = lastBatch;
            logContext["battle_changed_units"] = CollectCommandBattleChangedUnits(
                pendingBatches,
                logPayloads
            );
        }

        var eventLevel = ok ? "info" : "warn";
        var eventDomain = string.IsNullOrEmpty(scopeDomain) ? "runtime" : scopeDomain;
        var eventId = string.IsNullOrEmpty(scope.EventId) ? "runtime.command" : scope.EventId;
        var eventMessage = !string.IsNullOrEmpty(message)
            ? message
            : (ok ? "命令成功。" : "命令失败。");

        LogRuntimeEventInternal(
            eventLevel,
            eventDomain,
            eventId,
            eventMessage,
            Json.Stringify(logContext)
        );
        scope.MarkLogged();
    }

    private Dictionary BuildRuntimeLogStateInternal(GodotProjectionPayloadOwner payloads = null)
    {
        var gameSession = _runtime._game_session;
        var worldMapContext = _runtime._world_map_data_context;

        var context = NewDictionary(
            payloads,
            new Dictionary
            {
                ["save_id"] = gameSession != null ? gameSession.GetActiveSaveId() : "",
                ["map_id"] = worldMapContext != null ? worldMapContext.active_map_id : "",
                ["map_display_name"] =
                    worldMapContext != null ? worldMapContext.active_map_display_name : "",
                ["player_coord"] = _runtime._player_coord.ToString(),
                ["selected_coord"] = _runtime._selected_coord.ToString(),
                ["active_modal_id"] = _runtime.GetActiveModalId(),
                ["battle_active"] = _runtime.IsBattleActive(),
            }
        );

        if (_runtime.IsBattleActive())
            context["battle"] = BuildBattleLogStateInternal(payloads);

        return context;
    }

    private static Dictionary NewDictionary(GodotProjectionPayloadOwner payloads, Dictionary dictionary)
    {
        return payloads?.Track(dictionary) ?? dictionary;
    }

    private static Godot.Collections.Array NewArray(
        GodotProjectionPayloadOwner payloads,
        Godot.Collections.Array array
    )
    {
        return payloads?.Track(array) ?? array;
    }

    private void LogRuntimeEventInternal(
        string level,
        string domain,
        string eventId,
        string message,
        string context
    )
    {
        var gameSession = _runtime._game_session;
        if (gameSession == null)
            return;
        Dictionary entry = gameSession.LogEvent(level, domain, eventId, message, context);
        entry?.Dispose();
    }

    private void LogBattleBatchEntriesInternal(BattleEventBatch batch)
    {
        if (batch == null)
            return;
        if (batch.LogLinesTyped.Count == 0)
            return;

        using var logPayloads = new GodotProjectionPayloadOwner();
        var baseContext = BuildBattleBatchLogContextInternal(batch, logPayloads);
        baseContext["runtime"] = BuildRuntimeLogStateInternal(logPayloads);
        string contextStr = Json.Stringify(baseContext);
        foreach (string logLine in batch.LogLinesTyped)
        {
            LogRuntimeEventInternal(
                "info",
                "battle",
                "battle.log",
                logLine,
                contextStr
            );
        }
    }

    private Dictionary BuildBattleLogStateInternal(GodotProjectionPayloadOwner payloads = null)
    {
        if (!_runtime.IsBattleActive())
            return NewDictionary(payloads, new Dictionary());

        var battleState = _runtime._battle_state;
        if (battleState == null)
            return NewDictionary(payloads, new Dictionary());

        var allyAliveCount = 0;
        var hostileAliveCount = 0;
        foreach (BattleUnitState unitState in battleState.Units())
        {
            if (unitState == null || !unitState.is_alive)
                continue;
            if (unitState.faction_id.ToString() == _runtime._player_faction_id)
                allyAliveCount++;
            else
                hostileAliveCount++;
        }

        return NewDictionary(
            payloads,
            new Dictionary
            {
                ["encounter_id"] = _runtime._active_battle_encounter_id.ToString(),
                ["encounter_name"] = _runtime._active_battle_encounter_name,
                ["battle_id"] = battleState.battle_id.ToString(),
                ["seed"] = battleState.seed,
                ["terrain_profile_id"] = battleState.terrain_profile_id.ToString(),
                ["map_size"] = battleState.map_size,
                ["phase"] = battleState.phase.ToString(),
                ["modal_state"] = battleState.modal_state.ToString(),
                ["winner_faction_id"] = battleState.winner_faction_id.ToString(),
                ["active_unit_id"] = battleState.active_unit_id.ToString(),
                ["active_unit_name"] = _runtime._get_battle_active_unit_name(),
                ["selected_coord"] = _runtime._battle_selected_coord.ToString(),
                ["selected_skill_id"] = _runtime._selected_battle_skill_id.ToString(),
                ["selected_skill_variant_id"] =
                    _runtime._selected_battle_skill_variant_id.ToString(),
                ["selected_target_coord_count"] = _runtime._queued_battle_skill_target_coords.Count,
                ["selected_target_unit_count"] = _runtime._queued_battle_skill_target_unit_ids.Count,
                ["terrain_counts"] = _runtime._count_battle_terrain_types(),
                ["ally_alive_count"] = allyAliveCount,
                ["hostile_alive_count"] = hostileAliveCount,
                ["units"] = BuildBattleUnitLogEntries(null, payloads),
            }
        );
    }

    private Dictionary BuildBattleBatchLogContextInternal(
        BattleEventBatch batch,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        if (batch == null)
            return NewDictionary(payloads, new Dictionary());
        var changedUnitIds = ToValueArray(batch.ChangedUnitIdsTyped, payloads);
        return NewDictionary(
            payloads,
            new Dictionary
            {
                ["phase_changed"] = batch.phase_changed,
                ["battle_ended"] = batch.battle_ended,
                ["modal_requested"] = batch.modal_requested,
                ["changed_unit_count"] = batch.ChangedUnitIdsTyped.Count,
                ["changed_coord_count"] = batch.ChangedCoordsTyped.Count,
                ["changed_coords"] = NormalizeVector2IArray(batch.ChangedCoordsTyped, payloads),
                ["changed_unit_ids"] = NormalizeStringNameArray(batch.ChangedUnitIdsTyped, payloads),
                ["changed_units"] = BuildBattleUnitLogEntries(changedUnitIds, payloads),
                ["report_entry_count"] = batch.ReportEntriesTyped.Count,
                ["report_entries"] = NormalizeLogArray(batch.ReportEntriesTyped, payloads),
            }
        );
    }

    private Godot.Collections.Array CollectCommandBattleChangedUnits(
        Godot.Collections.Array batchContexts,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        var mergedByUnitId = NewDictionary(payloads, new Dictionary());
        var orderedUnitIds = new List<string>();
        foreach (var batchContextValue in batchContexts)
        {
            var batchContext = batchContextValue.AsGodotDictionary();
            if (batchContext == null)
                continue;
            var changedUnits = DictionaryArray(batchContext, "changed_units", payloads);
            if (changedUnits.Count == 0)
                continue;
            foreach (var changedUnitValue in changedUnits)
            {
                if (changedUnitValue.VariantType != Variant.Type.Dictionary)
                    continue;
                var changedUnit = changedUnitValue.AsGodotDictionary();
                var unitId = DictionaryString(changedUnit, "unit_id").StripEdges();
                if (string.IsNullOrEmpty(unitId))
                    continue;
                if (!mergedByUnitId.ContainsKey(unitId))
                    orderedUnitIds.Add(unitId);
                mergedByUnitId[unitId] = TrackPayload(payloads, changedUnit.Duplicate(true));
            }
        }

        var result = NewArray(payloads, new Godot.Collections.Array());
        foreach (var unitId in orderedUnitIds)
        {
            if (mergedByUnitId.ContainsKey(unitId))
                result.Add(
                    TrackPayload(
                        payloads,
                        mergedByUnitId[unitId].AsGodotDictionary().Duplicate(true)
                    )
                );
        }
        return result;
    }

    private Godot.Collections.Array BuildBattleUnitLogEntries(
        Godot.Collections.Array unitIds = null,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        var result = NewArray(payloads, new Godot.Collections.Array());
        var battleState = _runtime._battle_state;
        if (battleState == null)
            return result;

        var normalizedIds = new List<StringName>();
        if (unitIds == null || unitIds.Count == 0)
        {
            foreach ((StringName unitId, BattleUnitState _) in battleState.UnitEntries(sorted: true))
                normalizedIds.Add(unitId);
        }
        else
        {
            foreach (var unitIdValue in unitIds)
            {
                var normalizedUnitId = ProgressionDataUtils.to_string_name(unitIdValue);
                if (normalizedUnitId == "")
                    continue;
                if (normalizedIds.Contains(normalizedUnitId))
                    continue;
                normalizedIds.Add(normalizedUnitId);
            }
        }

        foreach (var unitId in normalizedIds)
        {
            var unitState = battleState.GetUnit(unitId);
            if (unitState == null)
                continue;
            result.Add(
                NewDictionary(
                    payloads,
                    new Dictionary
                    {
                        ["unit_id"] = unitState.unit_id.ToString(),
                        ["display_name"] = !string.IsNullOrEmpty(unitState.display_name)
                            ? unitState.display_name
                            : unitState.unit_id.ToString(),
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
                    }
                )
            );
        }
        return result;
    }

    private static T TrackPayload<T>(GodotProjectionPayloadOwner payloads, T value)
    {
        return payloads != null ? payloads.Track(value) : value;
    }

    private static Godot.Collections.Array ToValueArray(
        IEnumerable<StringName> values,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        var result = NewArray(payloads, new Godot.Collections.Array());
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private static Godot.Collections.Array NormalizeStringNameArray(
        IEnumerable<StringName> values,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        var result = NewArray(payloads, new Godot.Collections.Array());
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static Godot.Collections.Array NormalizeVector2IArray(
        IEnumerable<Vector2I> values,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        var result = NewArray(payloads, new Godot.Collections.Array());
        if (values == null)
            return result;
        foreach (Vector2I coord in values)
            result.Add(NewDictionary(payloads, new Dictionary { ["x"] = coord.X, ["y"] = coord.Y }));
        return result;
    }

    private static Godot.Collections.Array NormalizeLogArray(
        System.Collections.IEnumerable values,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        var normalizedArray = NewArray(payloads, new Godot.Collections.Array());
        if (values == null)
            return normalizedArray;
        foreach (var entry in values)
            normalizedArray.Add(ToVariant(NormalizeLogValue(entry, payloads)));
        return normalizedArray;
    }

    private static object NormalizeLogValue(
        object rawValue,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        if (rawValue is Dictionary rawDictionary)
        {
            var normalizedDictionary = NewDictionary(payloads, new Dictionary());
            foreach (var key in rawDictionary.Keys)
                normalizedDictionary[key.ToString()] = ToVariant(
                    NormalizeLogValue(rawDictionary[key], payloads)
                );
            return normalizedDictionary;
        }
        if (rawValue is Godot.Collections.Array rawArray)
        {
            var normalizedArray = NewArray(payloads, new Godot.Collections.Array());
            foreach (var entry in rawArray)
                normalizedArray.Add(ToVariant(NormalizeLogValue(entry, payloads)));
            return normalizedArray;
        }
        if (rawValue is not Variant value)
            return rawValue;

        switch (value.VariantType)
        {
            case Variant.Type.Nil:
                return null;
            case Variant.Type.Bool:
                return value.AsBool();
            case Variant.Type.Int:
                return value.AsInt64();
            case Variant.Type.Float:
                return value.AsDouble();
            case Variant.Type.String:
                return value.AsString();
            case Variant.Type.StringName:
                return value.AsStringName().ToString();
            case Variant.Type.Vector2I:
                var coord = value.AsVector2I();
                return NewDictionary(payloads, new Dictionary { ["x"] = coord.X, ["y"] = coord.Y });
            case Variant.Type.Vector2:
                var floatCoord = value.AsVector2();
                return NewDictionary(
                    payloads,
                    new Dictionary { ["x"] = floatCoord.X, ["y"] = floatCoord.Y }
                );
            case Variant.Type.Dictionary:
                var dict = value.AsGodotDictionary();
                var normalizedDict = NewDictionary(payloads, new Dictionary());
                foreach (var key in dict.Keys)
                    normalizedDict[key.ToString()] = ToVariant(
                        NormalizeLogValue(dict[key], payloads)
                    );
                return normalizedDict;
            case Variant.Type.Array:
                var array = value.AsGodotArray();
                var normalizedArray = NewArray(payloads, new Godot.Collections.Array());
                foreach (var entry in array)
                    normalizedArray.Add(ToVariant(NormalizeLogValue(entry, payloads)));
                return normalizedArray;
            case Variant.Type.Object:
                var obj = value.AsGodotObject();
                if (obj == null)
                    return null;
                return obj.ToString();
            default:
                return value;
        }
    }

    private static Variant ToVariant(object value)
    {
        return value switch
        {
            Variant variant => variant,
            string text => text,
            StringName stringName => stringName,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            Vector2I coord => coord,
            Vector2 coord => coord,
            Dictionary dictionary => dictionary,
            Godot.Collections.Array array => array,
            GodotObject godotObject => godotObject,
            _ => default,
        };
    }

    private static bool DictionaryBool(Dictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType != Variant.Type.Nil ? value.AsString() : fallback;
    }

    private static Dictionary DictionaryDictionary(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Dictionary();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Dictionary();
    }

    private static Godot.Collections.Array DictionaryArray(
        Dictionary dictionary,
        string key,
        GodotProjectionPayloadOwner payloads = null
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return NewArray(payloads, new Godot.Collections.Array());
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Array
            ? NewArray(payloads, value.AsGodotArray())
            : NewArray(payloads, new Godot.Collections.Array());
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GameRuntimeFacade target)
        )
            return null;
        return target;
    }
}
