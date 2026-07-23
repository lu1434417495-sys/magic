using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of GameRuntimeFacade — battle start / resolution / writeback / loot finalize.
// Pure physical split: same class, no behavior change. See GameRuntimeFacade.cs.
public sealed partial class GameRuntimeFacade
{
    private enum PendingBattleStartAttemptKind
    {
        None = 0,
        Pending,
        Started,
        Failed,
    }

    internal void PrepareBattleStart(EncounterAnchorData encounter_anchor)
    {
        if (encounter_anchor == null)
            return;
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        fateRuntime?.ClearMisfortuneExaltedReadyFlags();
        _active_battle_encounter_id = encounter_anchor.entity_id;
        _active_battle_encounter_name = encounter_anchor.display_name;
        _last_battle_loot_snapshot.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _pending_promotion_prompt.Clear();
        _battle_selection.ClearBattleSkillSelection(false);
        _character_management.SetPartyState(_party_state);
    }

    internal StringName BeginBattleStart(
        EncounterAnchorData encounter_anchor,
        int seed,
        GDictionary context
    )
    {
        if (encounter_anchor == null || _battle_runtime == null)
        {
            HandleBattleStartFailure();
            return "failed";
        }
        if (ResolveBattleObjectiveDefinition(encounter_anchor) == null)
        {
            UpdateStatusInternal("战斗加载失败：遭遇缺少正式胜利目标。");
            HandleBattleStartFailure();
            return "failed";
        }
        _pending_battle_generation_request.Set(encounter_anchor, seed, context);
        _pending_battle_start_prompt.Clear();
        _active_modal_kind = RuntimeModalKind.BattleLoading;
        string encounterName = _resolve_battle_encounter_display_name(encounter_anchor);
        UpdateStatusInternal($"遭遇 {encounterName}，战斗地图生成中。");
        _log_runtime_event(
            GameLogLevel.Info,
            "battle",
            "battle.start_loading",
            $"遭遇 {encounterName}，战斗地图生成中。",
            Json.Stringify(new GDictionary
            {
                ["encounter_id"] = encounter_anchor.entity_id.ToString(),
                ["encounter_name"] = encounterName,
                ["runtime"] = _build_runtime_log_state(),
            })
        );
        PendingBattleStartAttemptKind attemptKind =
            _try_complete_pending_battle_start();
        if (attemptKind == PendingBattleStartAttemptKind.Started)
            return "started";
        if (attemptKind == PendingBattleStartAttemptKind.Failed)
        {
            HandleBattleStartFailure();
            return "failed";
        }
        return "pending";
    }

    internal void HandleBattleStartFailure()
    {
        string failedEncounterId = _active_battle_encounter_id.ToString();
        string failedEncounterName = _active_battle_encounter_name;
        _active_battle_encounter_id = "";
        _active_battle_encounter_name = "";
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _active_modal_kind = RuntimeModalKind.None;
        ClearRuntimeBattleStateReference();
        _release_battle_save_lock();
        _battle_auto_tick_remainder_msec = 0;
        _battle_selected_coord = new Vector2I(-1, -1);
        UpdateStatusInternal("遭遇战生成失败。");
        _log_runtime_event(
            GameLogLevel.Error,
            "battle",
            "battle.start_failed",
            "遭遇战生成失败。",
            Json.Stringify(new GDictionary
            {
                ["encounter_id"] = failedEncounterId,
                ["encounter_name"] = failedEncounterName,
                ["runtime"] = _build_runtime_log_state(),
            })
        );
    }

    private void HandleDeferredBattleStartFailure()
    {
        HandleBattleStartFailure();
        if (_game_session == null || !_game_session.HasActiveWorld())
            return;
        int flushError = _flush_game_state_with_world_sync();
        UpdateStatusInternal(
            flushError == (int)Error.Ok
                ? "遭遇战未能开始，已保留玩家当前位置与世界时间。"
                : "遭遇战未能开始，且玩家位置或世界时间持久化失败。"
        );
    }

    internal void PresentBattleStartConfirmation()
    {
        if (!IsBattleActive() || _battle_state == null)
            return;
        ReplacePlainPayload(
            _pending_battle_start_prompt,
            new GDictionary
            {
                ["title"] = "开始战斗",
                ["description"] = "是否开始战斗？确认后 TU 将按整数 tick 推进。",
                ["confirm_text"] = "开始战斗",
                ["cancel_visible"] = false,
                ["dismiss_on_shade"] = false,
            },
            "GameRuntimeFacade.pending_battle_start_prompt"
        );
        _active_modal_kind = RuntimeModalKind.BattleStartConfirm;
        _battle_state.ModalStateKind = BattleModalStateKind.StartConfirm;
        if (_battle_state.timeline != null)
        {
            _battle_state.timeline.frozen = true;
            _battle_state.timeline.tu_per_tick = 5;
        }
        _battle_auto_tick_remainder_msec = 0;
        UpdateStatusInternal("战斗地图已载入，请确认开始战斗。");
        _log_runtime_event(
            GameLogLevel.Info,
            "battle",
            "battle.start_prepared",
            "战斗地图已载入，请确认开始战斗。",
            Json.Stringify(new GDictionary { ["runtime"] = _build_runtime_log_state() })
        );
    }

    private PendingBattleStartAttemptKind _try_complete_pending_battle_start()
    {
        if (_pending_battle_generation_request.IsEmpty || _battle_runtime == null)
            return PendingBattleStartAttemptKind.None;
        var encounterAnchor = _pending_battle_generation_request.EncounterAnchor;
        if (encounterAnchor == null)
            return PendingBattleStartAttemptKind.Failed;
        BattleObjectiveDefinition objectiveDefinition =
            ResolveBattleObjectiveDefinition(encounterAnchor);
        if (objectiveDefinition == null)
        {
            UpdateStatusInternal("战斗加载失败：遭遇缺少正式胜利目标。");
            return PendingBattleStartAttemptKind.Failed;
        }
        int seed = _pending_battle_generation_request.Seed;
        Dictionary<string, object> context =
            _pending_battle_generation_request.CloneContextPlain();
        BattleState runtimeState;
        using (
            GodotProjectionLease<GDictionary> contextLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    context,
                    "pending-battle-start-context",
                    LifetimeDomain.Request,
                    "GameRuntimeFacade.TryCompletePendingBattleStart"
                )
        )
        {
            runtimeState = _battle_runtime.StartBattleBorrowingContext(
                encounterAnchor,
                seed,
                objectiveDefinition,
                contextLease.Value
            );
        }
        if (runtimeState == null || runtimeState.IsEmpty())
        {
            BattleStartFailureSnapshot failure =
                _battle_runtime.GetLastStartFailureSnapshot();
            return IsTerminalBattleStartFailure(failure)
                ? PendingBattleStartAttemptKind.Failed
                : PendingBattleStartAttemptKind.Pending;
        }
        _pending_battle_generation_request.Clear();
        if (_battle_session_facade != null)
            _battle_session_facade.RefreshBattleRuntimeState();
        else
            _battle_state = runtimeState;
        PresentBattleStartConfirmation();
        return PendingBattleStartAttemptKind.Started;
    }

    private static bool IsTerminalBattleStartFailure(
        BattleStartFailureSnapshot failure
    )
    {
        if (failure == null || failure.IsEmpty)
            return false;
        return failure.Reason switch
        {
            "missing_objective_definition" => true,
            "invalid_start_units" => true,
            "invalid_objective_binding" => true,
            "spawn_reachability" => true,
            "placement_exhausted" => true,
            _ => false,
        };
    }

    private string _resolve_battle_encounter_display_name(EncounterAnchorData encounter_anchor)
    {
        if (encounter_anchor == null)
            return "遭遇";
        return string.IsNullOrEmpty(encounter_anchor.display_name)
            ? "遭遇"
            : encounter_anchor.display_name;
    }

    internal bool FinalizeBattleResolution(BattleResolutionResult battle_resolution_result)
    {
        if (
            battle_resolution_result == null
            || !battle_resolution_result.IsTerminal
            || _game_session == null
            || _character_management == null
            || _battle_runtime == null
        )
        {
            UpdateStatusInternal("战斗结算失败：运行时状态不完整，已保留战斗上下文。");
            return false;
        }

        string battleName = string.IsNullOrEmpty(_active_battle_encounter_name)
            ? "遭遇"
            : _active_battle_encounter_name;
        string winnerFactionId = battle_resolution_result.winner_faction_id.ToString();
        var battleSummary = _build_battle_log_state();
        RuntimeTransaction rollbackTransaction = new RuntimeTransaction()
            .MarkPartyChanged()
            .MarkWorldChanged()
            .MarkPlayerCoordChanged();
        RuntimeTransactionRollbackState rollbackState =
            RuntimeTransactionRollbackState.Capture(this, rollbackTransaction);
        BattleFinalizationRollbackState battleRollbackState =
            BattleFinalizationRollbackState.Capture(_battle_runtime, battle_resolution_result);
        var guidanceUnlocks = new GStringNameArray();
        var misfortuneGuidanceUnlocks = new GStringNameArray();
        var lowLuckEventResult = new GDictionary();
        var writebackResult = CommitBattleLocalViewsToPartyStateTyped(
            _battle_state,
            _party_state
        );
        if (!writebackResult.Ok)
        {
            using GodotProjectionLease<GDictionary> writebackLease =
                GameRuntimeBattleWritebackProjection.ProjectLease(writebackResult);
            GDictionary writebackPayload = writebackLease.Value;
            _report_battle_local_writeback_inoption_failure(
                writebackPayload,
                battleSummary,
                winnerFactionId
            );
            RollbackBattleFinalization(rollbackTransaction, rollbackState, battleRollbackState, battle_resolution_result);
            UpdateStatusInternal("战斗结算失败：战斗内队伍状态回写失败，已保留战斗上下文。");
            return false;
        }

        List<PendingCharacterReward> resolvedPendingRewards = DuplicatePendingCharacterRewards(
            _battle_runtime.GetPendingPostBattleCharacterRewards()
        );
        var fateResolution = _battle_runtime.GetFateRuntime().HandleBattleResolution(
            _battle_state,
            battle_resolution_result,
            resolvedPendingRewards
        );
        if (fateResolution.Count > 0)
        {
            guidanceUnlocks = ProgressionDataUtils.to_string_name_array(
                DictArray(fateResolution, "fortuna_guidance_unlocks")
            );
            misfortuneGuidanceUnlocks = ProgressionDataUtils.to_string_name_array(
                DictArray(fateResolution, "misfortune_guidance_unlocks")
            );
            lowLuckEventResult = DictDictionary(fateResolution, "low_luck_event_result");
        }

        bool mainCharacterDead =
            IsMainCharacterDead() || IsMainCharacterDeadInBattleState();
        if (!mainCharacterDead)
        {
            resolvedPendingRewards = FilterBattlePendingCharacterRewardsForQueue(
                resolvedPendingRewards,
                battleSummary,
                winnerFactionId
            );
        }
        var questSummary = new QuestProgressApplyResultData();
        var lootCommitResult = GameRuntimeBattleLootCommitService.BattleLootCommitResult.Success();
        int partyPersistError = (int)Error.Ok;
        int worldPersistError = (int)Error.Ok;
        int flushError = (int)Error.Ok;
        bool saveSkipped = false;

        if (!mainCharacterDead)
        {
            lootCommitResult = CommitBattleLootToSharedWarehouseTyped(battle_resolution_result);
            if (!lootCommitResult.Ok)
            {
                UpdateStatusInternal(
                    BuildBattleResolutionStatusMessageTyped(
                        battleName,
                        winnerFactionId,
                        lootCommitResult,
                        false
                    )
                );
                _log_runtime_event(
                    GameLogLevel.Warning,
                    "battle",
                    "battle.resolve_failed.loot_commit",
                    _current_status_message,
                    Json.Stringify(new GDictionary
                    {
                        ["battle"] = battleSummary,
                        ["winner_faction_id"] = winnerFactionId,
                        ["loot_commit_error_code"] = lootCommitResult.ErrorCode,
                        ["loot_commit_blocked_item_id"] = lootCommitResult.BlockedItemId,
                    })
                );
                RollbackBattleFinalization(rollbackTransaction, rollbackState, battleRollbackState, battle_resolution_result);
                return false;
            }
        }

        BattleEndResult endBattleResult = _battle_runtime.EndBattle(
            new BattleEndOptions(commitProgression: true)
        );
        if (!endBattleResult.Ok)
        {
            UpdateStatusInternal(
                BuildBattleResolutionStatusMessageTyped(
                    battleName,
                    winnerFactionId,
                    lootCommitResult,
                    false
                )
            );
            _log_runtime_event(
                GameLogLevel.Warning,
                "battle",
                "battle.resolve_failed.writeback",
                _current_status_message,
                Json.Stringify(new GDictionary
                {
                    ["battle"] = battleSummary,
                    ["winner_faction_id"] = winnerFactionId,
                    ["error_code"] = endBattleResult.ErrorCode,
                    ["flush_error"] = endBattleResult.FlushError,
                    ["contingency_error_code"] =
                        endBattleResult.ContingencyConsumedResult?.ErrorCode ?? "",
                    ["contingency_member_id"] =
                        endBattleResult.ContingencyConsumedResult?.MemberId.ToString() ?? "",
                    ["resource_error_code"] =
                        endBattleResult.ResourceCommitResult?.ErrorCode ?? "",
                    ["resource_member_id"] =
                        endBattleResult.ResourceCommitResult?.MemberId.ToString() ?? "",
                })
            );
            RollbackBattleFinalization(rollbackTransaction, rollbackState, battleRollbackState, battle_resolution_result);
            return false;
        }
        _party_state = _character_management.GetPartyState();
        if (!mainCharacterDead)
        {
            _character_management.EnqueuePendingCharacterRewardsTyped(resolvedPendingRewards);
            questSummary = _character_management
                .ApplyQuestProgressEventsTyped(
                    BuildDefaultBattleQuestProgressEventsTyped(
                        battle_resolution_result.outcome
                    )
                );
            _party_state = _character_management.GetPartyState();
            partyPersistError = _game_session.SetPartyState(_party_state);
            if (partyPersistError != (int)Error.Ok)
            {
                UpdateStatusInternal(
                    BuildBattleResolutionStatusMessageTyped(
                        battleName,
                        winnerFactionId,
                        lootCommitResult,
                        false
                    )
                );
                _log_runtime_event(
                    GameLogLevel.Warning,
                    "battle",
                    "battle.resolve_failed.party_persist",
                    _current_status_message,
                    Json.Stringify(new GDictionary
                    {
                        ["battle"] = battleSummary,
                        ["winner_faction_id"] = winnerFactionId,
                        ["party_persist_error"] = partyPersistError,
                    })
                );
                RollbackBattleFinalization(rollbackTransaction, rollbackState, battleRollbackState, battle_resolution_result);
                return false;
            }
            _resolve_world_encounter_after_battle(battle_resolution_result);
            _materialize_active_world_state_to_root();
            worldPersistError = _game_session.SetWorldData(
                _world_map_data_context.RootRuntimeData
            );
            if (worldPersistError != (int)Error.Ok)
            {
                UpdateStatusInternal(
                    BuildBattleResolutionStatusMessageTyped(
                        battleName,
                        winnerFactionId,
                        lootCommitResult,
                        false
                    )
                );
                _log_runtime_event(
                    GameLogLevel.Warning,
                    "battle",
                    "battle.resolve_failed.world_persist",
                    _current_status_message,
                    Json.Stringify(new GDictionary
                    {
                        ["battle"] = battleSummary,
                        ["winner_faction_id"] = winnerFactionId,
                        ["world_persist_error"] = worldPersistError,
                    })
                );
                RollbackBattleFinalization(rollbackTransaction, rollbackState, battleRollbackState, battle_resolution_result);
                return false;
            }
        }
        else
        {
            saveSkipped = true;
        }

        if (saveSkipped)
        {
            _game_session.DiscardPendingSave();
            _game_session.SetBattleSaveLock(false);
        }
        else
        {
            _game_session.SetBattleSaveLock(false);
            flushError = _flush_game_state_with_world_sync();
            if (flushError != (int)Error.Ok)
            {
                UpdateStatusInternal(
                    BuildBattleResolutionStatusMessageTyped(
                        battleName,
                        winnerFactionId,
                        lootCommitResult,
                        false
                    )
                );
                _log_runtime_event(
                    GameLogLevel.Warning,
                    "battle",
                    "battle.resolve_failed.flush",
                    _current_status_message,
                    Json.Stringify(new GDictionary
                    {
                        ["battle"] = battleSummary,
                        ["winner_faction_id"] = winnerFactionId,
                        ["flush_error"] = flushError,
                    })
                );
                RollbackBattleFinalization(rollbackTransaction, rollbackState, battleRollbackState, battle_resolution_result);
                return false;
            }
        }

        ClearResolvedBattleRuntimeContext();
        if (mainCharacterDead)
        {
            _last_battle_loot_snapshot.Clear();
            ActivateGameOver(BuildMainCharacterGameOverContext());
        }
        else
        {
            ReplacePlainPayload(
                _last_battle_loot_snapshot,
                BuildLastBattleLootSnapshotTyped(
                    battleName,
                    winnerFactionId,
                    battle_resolution_result,
                    lootCommitResult
                ),
                "GameRuntimeFacade.last_battle_loot_snapshot"
            );
        }

        _RefreshFog();
        if (mainCharacterDead)
        {
            UpdateStatusInternal(
                _active_game_over_context?.Description
                ?? "主角已阵亡，本次旅程结束。"
            );
            _log_runtime_event(
                GameLogLevel.Info,
                "battle",
                "battle.game_over",
                _current_status_message,
                BuildBattleResolvedLogContext(
                    battleSummary,
                    winnerFactionId,
                    resolvedPendingRewards,
                    guidanceUnlocks,
                    misfortuneGuidanceUnlocks,
                    lowLuckEventResult,
                    questSummary,
                    battle_resolution_result,
                    lootCommitResult,
                    saveSkipped,
                    partyPersistError,
                    worldPersistError,
                    flushError
                )
            );
            return true;
        }

        bool persistedOk =
            partyPersistError == (int)Error.Ok
            && worldPersistError == (int)Error.Ok
            && flushError == (int)Error.Ok;
        UpdateStatusInternal(
            BuildBattleResolutionStatusMessageTyped(
                battleName,
                winnerFactionId,
                lootCommitResult,
                persistedOk
            )
        );
        _log_runtime_event(
            persistedOk ? GameLogLevel.Info : GameLogLevel.Warning,
            "battle",
            "battle.resolved",
            _current_status_message,
            BuildBattleResolvedLogContext(
                battleSummary,
                winnerFactionId,
                resolvedPendingRewards,
                guidanceUnlocks,
                misfortuneGuidanceUnlocks,
                lowLuckEventResult,
                questSummary,
                battle_resolution_result,
                lootCommitResult,
                saveSkipped,
                partyPersistError,
                worldPersistError,
                flushError
            )
        );
        PresentPendingRewardIfReady();
        return true;
    }

    private void _release_battle_save_lock()
    {
        _game_session?.SetBattleSaveLock(false);
    }

    private void RollbackBattleFinalization(
        RuntimeTransaction rollbackTransaction,
        RuntimeTransactionRollbackState rollbackState,
        BattleFinalizationRollbackState battleRollbackState,
        BattleResolutionResult battleResolutionResult
    )
    {
        battleRollbackState?.Restore(_battle_runtime, battleResolutionResult);
        rollbackTransaction?.Rollback(this, rollbackState);
        SyncPartyStateServices();
    }

    internal IReadOnlyList<StringName> HandleFortunaChapterCompleted(GDictionary payload)
    {
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        if (fateRuntime == null)
            return Array.Empty<StringName>();
        var unlockedIds = fateRuntime.HandleFortunaChapterCompleted(payload);
        if (_character_management != null)
            _party_state = _character_management.GetPartyState();
        if (_party_state != null)
            _clear_regular_battle_calamity_shard_flags();
        if (
            _game_session != null
            && _party_state != null
            && _game_session.HasActiveWorld()
        )
        {
            _game_session.SetPartyState(_party_state);
            _flush_game_state_with_world_sync();
        }
        return unlockedIds;
    }

    internal IReadOnlyList<StringName> HandleMisfortuneForgeResult(
        StringName member_id,
        SettlementServiceResult result)
    {
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        if (
            fateRuntime == null
            || member_id == ""
            || result == null
        )
            return Array.Empty<StringName>();
        var itemDefs =
            GetContentCatalogTyped() != null
                ? GetContentCatalogTyped().GetItemDefsTyped()
                : new Dictionary<StringName, ItemDefinition>();
        var unlockedIds = fateRuntime.HandleMisfortuneForgeResult(member_id, result, itemDefs);
        if (_character_management != null)
            _party_state = _character_management.GetPartyState();
        return unlockedIds;
    }

    internal GDictionary ResolveLowLuckSettlementEventRewards(GDictionary context)
    {
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        if (fateRuntime == null)
            return new GDictionary();
        var result = fateRuntime.ResolveLowLuckSettlementEventRewards(context);
        if (_character_management != null)
            _party_state = _character_management.GetPartyState();
        return result;
    }

    internal GameRuntimeBattleWritebackService.BattleLocalWritebackResult CommitBattleLocalViewsToPartyStateTyped(
        BattleState battleState,
        PartyState partyState
    )
    {
        BindRuntimeSidecarOwners();
        return _battle_writeback_service.CommitBattleLocalViewsToPartyStateTyped(
            battleState,
            partyState
        );
    }

    private void _report_battle_local_writeback_inoption_failure(
        GDictionary writeback_result,
        GDictionary battle_summary,
        string winner_faction_id
    )
    {
        BindRuntimeSidecarOwners();
        _battle_writeback_service.ReportInoptionFailure(
            writeback_result,
            battle_summary,
            winner_faction_id
        );
    }

    internal GameRuntimeBattleLootCommitService.BattleLootCommitResult CommitBattleLootToSharedWarehouseTyped(
        BattleResolutionResult battleResolutionResult
    )
    {
        BindRuntimeSidecarOwners();
        return _battle_loot_commit_service.CommitBattleLootToSharedWarehouseTyped(
            battleResolutionResult
        );
    }

    private void _clear_regular_battle_calamity_shard_flags()
    {
        BindRuntimeSidecarOwners();
        _battle_loot_commit_service.ClearRegularBattleCalamityShardFlags();
    }

    private string BuildBattleResolutionStatusMessageTyped(
        string battleName,
        string winnerFactionId,
        GameRuntimeBattleLootCommitService.BattleLootCommitResult lootCommitResult,
        bool persistedOk
    ) =>
        _battle_loot_commit_service.BuildBattleResolutionStatusMessageTyped(
            battleName,
            winnerFactionId,
            lootCommitResult,
            persistedOk
        );

    private GDictionary BuildLastBattleLootSnapshotTyped(
        string battleName,
        string winnerFactionId,
        BattleResolutionResult battleResolutionResult,
        GameRuntimeBattleLootCommitService.BattleLootCommitResult lootCommitResult
    ) =>
        _battle_loot_commit_service.BuildLastBattleLootSnapshotTyped(
            battleName,
            winnerFactionId,
            battleResolutionResult,
            lootCommitResult
        );

    private string _format_battle_drop_entries(GArray drop_entry_options) =>
        _battle_loot_commit_service.FormatBattleDropEntries(drop_entry_options);
}
