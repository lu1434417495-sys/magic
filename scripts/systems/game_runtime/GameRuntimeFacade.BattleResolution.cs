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
            return "failed";
        _pending_battle_generation_request.Set(encounter_anchor, seed, context);
        _pending_battle_start_prompt.Clear();
        _active_modal_kind = RuntimeModalKind.BattleLoading;
        string encounterName = _resolve_battle_encounter_display_name(encounter_anchor);
        UpdateStatusInternal($"遭遇 {encounterName}，战斗地图生成中。");
        _log_runtime_event(
            "info",
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
        return _try_complete_pending_battle_start() ? "started" : "pending";
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
        _battle_auto_tick_remainder_msec = 0;
        _battle_selected_coord = new Vector2I(-1, -1);
        UpdateStatusInternal("遭遇战生成失败。");
        _log_runtime_event(
            "error",
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
            "info",
            "battle",
            "battle.start_prepared",
            "战斗地图已载入，请确认开始战斗。",
            Json.Stringify(new GDictionary { ["runtime"] = _build_runtime_log_state() })
        );
    }

    private bool _try_complete_pending_battle_start()
    {
        if (_pending_battle_generation_request.IsEmpty || _battle_runtime == null)
            return false;
        var encounterAnchor = _pending_battle_generation_request.EncounterAnchor;
        if (encounterAnchor == null)
            return false;
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
                contextLease.Value
            );
        }
        if (runtimeState == null || runtimeState.IsEmpty())
            return false;
        _pending_battle_generation_request.Clear();
        if (_battle_session_facade != null)
            _battle_session_facade.RefreshBattleRuntimeState();
        else
            _battle_state = runtimeState;
        PresentBattleStartConfirmation();
        return true;
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
            GDictionary writebackPayload = GameRuntimeBattleWritebackProjection.Project(
                writebackResult
            );
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
                    "warn",
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
                "warn",
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
                    BuildDefaultBattleQuestProgressEventsTyped(winnerFactionId)
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
                    "warn",
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
            _resolve_world_encounter_after_battle(winnerFactionId);
            worldPersistError = _game_session.SetWorldData(
                _world_map_data_context.root_world_data
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
                    "warn",
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
                    "warn",
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
                DictString(
                    GetGameOverContext(),
                    "description",
                    "主角已阵亡，本次旅程结束。"
                )
            );
            _log_runtime_event(
                "info",
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
            persistedOk ? "info" : "warn",
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

    internal GStringNameArray HandleFortunaChapterCompleted(GDictionary payload)
    {
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        if (fateRuntime == null)
            return new StringNameList().ToGodotArray();
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

    internal GStringNameArray HandleMisfortuneForgeResult(
        StringName member_id,
        SettlementServiceResult result)
    {
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        if (
            fateRuntime == null
            || member_id == ""
            || result == null
        )
            return new StringNameList().ToGodotArray();
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
