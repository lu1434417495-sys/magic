using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public sealed class GameRuntimeCommandLogger
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;
    private CommandLogScope _previousCommandLogScope = CommandLogScope.Empty();
    private CommandLogScope _activeCommandLogScope = CommandLogScope.Empty();

    private sealed class CommandLogScope
    {
        public string EventId { get; }
        public string Domain { get; }
        private readonly System.Collections.Generic.Dictionary<string, object> _commandArgs =
            new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, object> _beforeState =
            new(StringComparer.Ordinal);
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
            foreach (
                KeyValuePair<string, object> entry in RuntimePlainPayload.NormalizeDictionary(
                    commandArgs,
                    "GameRuntimeCommandLogger.CommandArgs"
                )
            )
            {
                if (!string.IsNullOrEmpty(entry.Key))
                    _commandArgs[entry.Key] = entry.Value;
            }
            foreach (
                KeyValuePair<string, object> entry in RuntimePlainPayload.NormalizeDictionary(
                    beforeState,
                    "GameRuntimeCommandLogger.BeforeState"
                )
            )
            {
                if (!string.IsNullOrEmpty(entry.Key))
                    _beforeState[entry.Key] = entry.Value;
            }
            Logged = logged;
        }

        public static CommandLogScope Empty() =>
            new("", "", new Dictionary(), new Dictionary(), false);

        public static CommandLogScope Create(
            string eventId,
            string domain,
            Dictionary commandArgs,
            Dictionary beforeState
        ) => new(eventId, domain, commandArgs, beforeState, false);

        public CommandLogScope Clone() =>
            new(
                EventId,
                Domain,
                RuntimePlainPayload.CloneDictionary(_commandArgs),
                RuntimePlainPayload.CloneDictionary(_beforeState),
                Logged
            );

        private CommandLogScope(
            string eventId,
            string domain,
            IReadOnlyDictionary<string, object> commandArgs,
            IReadOnlyDictionary<string, object> beforeState,
            bool logged
        )
        {
            EventId = eventId ?? "";
            Domain = domain ?? "";
            foreach (KeyValuePair<string, object> entry in RuntimePlainPayload.CloneDictionary(commandArgs))
                _commandArgs[entry.Key] = entry.Value;
            foreach (KeyValuePair<string, object> entry in RuntimePlainPayload.CloneDictionary(beforeState))
                _beforeState[entry.Key] = entry.Value;
            Logged = logged;
        }

        internal System.Collections.Generic.Dictionary<string, object> BuildContextPlain() =>
            new(StringComparer.Ordinal)
            {
                ["command_args"] = RuntimePlainPayload.CloneDictionary(_commandArgs),
                ["before"] = RuntimePlainPayload.CloneDictionary(_beforeState),
            };

        public void MarkLogged()
        {
            Logged = true;
        }

        public void ClearPayloads()
        {
            _commandArgs.Clear();
            _beforeState.Clear();
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
        _previousCommandLogScope?.ClearPayloads();
        _activeCommandLogScope?.ClearPayloads();
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
        GameLogLevel level,
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

    internal static Dictionary NormalizeReportEntryForLog(
        IReadOnlyDictionary<string, object> reportEntry
    ) =>
        NormalizeLogValue(reportEntry) as Dictionary ?? new Dictionary();

    private void BeginLoggedCommandInternal(string eventId, string domain, Dictionary context)
    {
        var previousScope = _activeCommandLogScope;
        _previousCommandLogScope = previousScope.Clone();
        previousScope.ClearPayloads();
        var commandArgs = NormalizeLogValue(context) as Dictionary ?? new Dictionary();
        var beforeState = BuildRuntimeLogStateInternal();

        if (domain == "battle")
        {
            _runtime._pending_command_battle_batches.Clear();
        }

        _activeCommandLogScope = CommandLogScope.Create(eventId, domain, commandArgs, beforeState);
    }

    private Dictionary FinishLoggedCommandInternal(Dictionary result)
    {
        var resolvedResult = result ?? new Dictionary();
        var currentScope = _activeCommandLogScope;
        if (!currentScope.Logged)
            LogCommandResultInternal(currentScope, resolvedResult);

        _activeCommandLogScope = _previousCommandLogScope ?? CommandLogScope.Empty();

        if (currentScope.Domain == "battle")
            _runtime._pending_command_battle_batches.Clear();

        _previousCommandLogScope = CommandLogScope.Empty();
        currentScope.ClearPayloads();
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
        var logContext = scope.BuildContextPlain();
        using Dictionary afterState = BuildRuntimeLogStateInternal();
        logContext["runtime"] = RuntimePlainPayload.NormalizeDictionary(
            afterState,
            "GameRuntimeCommandLogger.LogCommandResult.runtime"
        );
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
            var batchSnapshots = new System.Collections.Generic.List<object>();
            foreach (IReadOnlyDictionary<string, object> batch in pendingBatches)
                batchSnapshots.Add(RuntimePlainPayload.CloneDictionary(batch));
            logContext["battle_batches"] = batchSnapshots;
            logContext["battle_batch"] = RuntimePlainPayload.CloneDictionary(
                pendingBatches[pendingBatches.Count - 1]
            );
            logContext["battle_changed_units"] = CollectCommandBattleChangedUnits(pendingBatches);
        }

        GameLogLevel eventLevel = ok ? GameLogLevel.Info : GameLogLevel.Warning;
        var eventDomain = string.IsNullOrEmpty(scopeDomain) ? "runtime" : scopeDomain;
        var eventId = string.IsNullOrEmpty(scope.EventId) ? "runtime.command" : scope.EventId;
        var eventMessage = !string.IsNullOrEmpty(message)
            ? message
            : (ok ? "命令成功。" : "命令失败。");

        using GodotProjectionLease<Dictionary> logContextLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                logContext,
                "GameRuntimeCommandLogger.LogCommandResult",
                LifetimeDomain.Request,
                "GameRuntimeCommandLogger.LogCommandResult"
            );
        LogRuntimeEventInternal(
            eventLevel,
            eventDomain,
            eventId,
            eventMessage,
            Json.Stringify(logContextLease.Value)
        );
        scope.MarkLogged();
    }

    private Dictionary BuildRuntimeLogStateInternal()
    {
        var gameSession = _runtime._game_session;
        var worldMapContext = _runtime._world_map_data_context;

        var context = new Dictionary
        {
            ["save_id"] = gameSession != null ? gameSession.GetActiveSaveId() : "",
            ["map_id"] = worldMapContext != null ? worldMapContext.active_map_id : "",
            ["map_display_name"] =
                worldMapContext != null ? worldMapContext.active_map_display_name : "",
            ["player_coord"] = _runtime._player_coord.ToString(),
            ["selected_coord"] = _runtime._selected_coord.ToString(),
            ["active_modal_id"] = _runtime.GetActiveModalId(),
            ["battle_active"] = _runtime.IsBattleActive(),
        };

        if (_runtime.IsBattleActive())
            context["battle"] = BuildBattleLogStateInternal();

        return context;
    }

    private void LogRuntimeEventInternal(
        GameLogLevel level,
        string domain,
        string eventId,
        string message,
        string context
    )
    {
        var gameSession = _runtime._game_session;
        if (gameSession == null)
            return;
        gameSession.RecordLogEvent(level, domain, eventId, message, context);
    }

    private void LogBattleBatchEntriesInternal(BattleEventBatch batch)
    {
        if (batch == null)
            return;
        if (batch.LogLinesTyped.Count == 0)
            return;

        using Dictionary baseContext = BuildBattleBatchLogContextInternal(batch);
        using Dictionary runtimeContext = BuildRuntimeLogStateInternal();
        System.Collections.Generic.Dictionary<string, object> baseContextPlain =
            RuntimePlainPayload.NormalizeDictionary(
            baseContext,
            "GameRuntimeCommandLogger.LogBattleBatch.base"
        );
        baseContextPlain["runtime"] = RuntimePlainPayload.NormalizeDictionary(
            runtimeContext,
            "GameRuntimeCommandLogger.LogBattleBatch.runtime"
        );
        using GodotProjectionLease<Dictionary> contextLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                baseContextPlain,
                "GameRuntimeCommandLogger.LogBattleBatch",
                LifetimeDomain.Request,
                "GameRuntimeCommandLogger.LogBattleBatch"
            );
        string contextStr = Json.Stringify(contextLease.Value);
        foreach (string logLine in batch.LogLinesTyped)
        {
            LogRuntimeEventInternal(
                GameLogLevel.Info,
                "battle",
                "battle.log",
                logLine,
                contextStr
            );
        }
    }

    private Dictionary BuildBattleLogStateInternal()
    {
        if (!_runtime.IsBattleActive())
            return new Dictionary();

        var battleState = _runtime._battle_state;
        if (battleState == null)
            return new Dictionary();

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

        return new Dictionary
        {
            ["encounter_id"] = _runtime._active_battle_encounter_id.ToString(),
            ["encounter_name"] = _runtime._active_battle_encounter_name,
            ["battle_id"] = battleState.battle_id.ToString(),
            ["seed"] = battleState.seed,
            ["terrain_profile_id"] = battleState.terrain_profile_id.ToString(),
            ["map_size"] = battleState.map_size,
            ["phase"] = battleState.phase.ToString(),
            ["modal_state"] = battleState.modal_state.ToString(),
            ["objective_mode"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.ObjectiveRuntimeState?.Mode ?? BattleObjectiveMode.Unknown
            ),
            ["outcome"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.FinalDecision?.Outcome ?? BattleOutcomeKind.Unknown
            ),
            ["end_reason"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.FinalDecision?.EndReason ?? BattleEndReasonKind.None
            ),
            ["decision_tu"] = battleState.FinalDecision?.DecisionTu ?? -1,
            ["winner_faction_id"] = battleState.winner_faction_id.ToString(),
            ["active_unit_id"] = battleState.active_unit_id.ToString(),
            ["active_unit_name"] = _runtime._get_battle_active_unit_name(),
            ["selected_coord"] = _runtime._battle_selected_coord.ToString(),
            ["selected_skill_entry_id"] = _runtime._selected_battle_skill_entry_id.ToString(),
            ["selected_skill_id"] = _runtime._selected_battle_skill_id.ToString(),
            ["selected_skill_variant_id"] = _runtime._selected_battle_skill_variant_id.ToString(),
            ["selected_target_coord_count"] =
                _runtime.GetBattleSelectionTargetCoordsStateTyped().Count,
            ["selected_target_unit_count"] =
                _runtime.GetBattleSelectionTargetUnitIdsStateTyped().Count,
            ["terrain_counts"] = _runtime._count_battle_terrain_types(),
            ["ally_alive_count"] = allyAliveCount,
            ["hostile_alive_count"] = hostileAliveCount,
            ["units"] = BuildBattleUnitLogEntries(),
        };
    }

    private Dictionary BuildBattleBatchLogContextInternal(BattleEventBatch batch)
    {
        if (batch == null)
            return new Dictionary();
        return new Dictionary
        {
            ["phase_changed"] = batch.phase_changed,
            ["battle_ended"] = batch.battle_ended,
            ["modal_requested"] = batch.modal_requested,
            ["changed_unit_count"] = batch.ChangedUnitIdsTyped.Count,
            ["changed_coord_count"] = batch.ChangedCoordsTyped.Count,
            ["changed_coords"] = NormalizeVector2IArray(batch.ChangedCoordsTyped),
            ["changed_unit_ids"] = NormalizeStringNameArray(batch.ChangedUnitIdsTyped),
            ["changed_units"] = BuildBattleUnitLogEntries(ToValueArray(batch.ChangedUnitIdsTyped)),
            ["report_entry_count"] = batch.ReportEntriesTyped.Count,
            ["report_entries"] = NormalizeLogArray(batch.ReportEntriesTyped),
        };
    }

    private System.Collections.Generic.List<object> CollectCommandBattleChangedUnits(
        IEnumerable<IReadOnlyDictionary<string, object>> batchContexts
    )
    {
        var mergedByUnitId =
            new System.Collections.Generic.Dictionary<
                string,
                System.Collections.Generic.Dictionary<string, object>
            >(StringComparer.Ordinal);
        var orderedUnitIds = new System.Collections.Generic.List<string>();
        foreach (
            IReadOnlyDictionary<string, object> batchContext in
            batchContexts ?? System.Array.Empty<IReadOnlyDictionary<string, object>>()
        )
        {
            if (batchContext == null)
                continue;
            using GodotProjectionLease<Dictionary> batchLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    batchContext,
                    "GameRuntimeCommandLogger.CollectChangedUnits.batch",
                    LifetimeDomain.Request,
                    "GameRuntimeCommandLogger.CollectChangedUnits.batch"
                );
            using Godot.Collections.Array changedUnits = DictionaryArray(
                batchLease.Value,
                "changed_units"
            );
            if (changedUnits.Count == 0)
                continue;
            foreach (var changedUnitValue in changedUnits)
            {
                if (changedUnitValue.VariantType != Variant.Type.Dictionary)
                    continue;
                using var changedUnit = changedUnitValue.AsGodotDictionary();
                var unitId = DictionaryString(changedUnit, "unit_id").StripEdges();
                if (string.IsNullOrEmpty(unitId))
                    continue;
                if (!mergedByUnitId.ContainsKey(unitId))
                    orderedUnitIds.Add(unitId);
                mergedByUnitId[unitId] = RuntimePlainPayload.NormalizeDictionary(
                    changedUnit,
                    "GameRuntimeCommandLogger.CollectChangedUnits.merge"
                );
            }
        }

        var result = new System.Collections.Generic.List<object>();
        foreach (var unitId in orderedUnitIds)
        {
            if (mergedByUnitId.TryGetValue(unitId, out var changedUnit))
                result.Add(RuntimePlainPayload.CloneDictionary(changedUnit));
        }
        return result;
    }

    private Godot.Collections.Array<Dictionary> BuildBattleUnitLogEntries(
        Godot.Collections.Array unitIds = null
    )
    {
        var result = new Array<Dictionary>();
        var battleState = _runtime._battle_state;
        if (battleState == null)
            return result;

        var normalizedIds = new Array<StringName>();
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
            );
        }
        return result;
    }

    private static Godot.Collections.Array ToValueArray(IEnumerable<StringName> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private static Godot.Collections.Array NormalizeStringNameArray(IEnumerable<StringName> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static Godot.Collections.Array NormalizeVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (Vector2I coord in values)
            result.Add(new Dictionary { ["x"] = coord.X, ["y"] = coord.Y });
        return result;
    }

    private static Godot.Collections.Array NormalizeLogArray(
        System.Collections.IEnumerable values
    )
    {
        var normalizedArray = new Godot.Collections.Array();
        if (values == null)
            return normalizedArray;
        foreach (var entry in values)
            normalizedArray.Add(ToVariant(NormalizeLogValue(entry)));
        return normalizedArray;
    }

    private static object NormalizeLogValue(object rawValue)
    {
        if (rawValue is StringName stringName)
            return stringName.ToString();
        if (rawValue is Vector2I plainCoord)
            return new Dictionary { ["x"] = plainCoord.X, ["y"] = plainCoord.Y };
        if (rawValue is Vector2 plainFloatCoord)
            return new Dictionary { ["x"] = plainFloatCoord.X, ["y"] = plainFloatCoord.Y };
        if (rawValue is IReadOnlyDictionary<string, object> plainDictionary)
        {
            var normalizedDictionary = new Dictionary();
            foreach (KeyValuePair<string, object> entry in plainDictionary)
                normalizedDictionary[entry.Key] = ToVariant(NormalizeLogValue(entry.Value));
            return normalizedDictionary;
        }
        if (rawValue is Dictionary rawDictionary)
        {
            var normalizedDictionary = new Dictionary();
            foreach (var key in rawDictionary.Keys)
                normalizedDictionary[key.ToString()] = ToVariant(NormalizeLogValue(rawDictionary[key]));
            return normalizedDictionary;
        }
        if (rawValue is Godot.Collections.Array rawArray)
        {
            var normalizedArray = new Godot.Collections.Array();
            foreach (var entry in rawArray)
                normalizedArray.Add(ToVariant(NormalizeLogValue(entry)));
            return normalizedArray;
        }
        if (rawValue is System.Collections.IEnumerable plainValues && rawValue is not string)
        {
            var normalizedArray = new Godot.Collections.Array();
            foreach (object entry in plainValues)
                normalizedArray.Add(ToVariant(NormalizeLogValue(entry)));
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
                return new Dictionary { ["x"] = coord.X, ["y"] = coord.Y };
            case Variant.Type.Vector2:
                var floatCoord = value.AsVector2();
                return new Dictionary { ["x"] = floatCoord.X, ["y"] = floatCoord.Y };
            case Variant.Type.Dictionary:
                var dict = value.AsGodotDictionary();
                var normalizedDict = new Dictionary();
                foreach (var key in dict.Keys)
                    normalizedDict[key.ToString()] = ToVariant(NormalizeLogValue(dict[key]));
                return normalizedDict;
            case Variant.Type.Array:
                var array = value.AsGodotArray();
                var normalizedArray = new Godot.Collections.Array();
                foreach (var entry in array)
                    normalizedArray.Add(ToVariant(NormalizeLogValue(entry)));
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

    private static Godot.Collections.Array DictionaryArray(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Godot.Collections.Array();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
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
