using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Development-only headless bridge for automation and debugging.
// This is not a player-facing startup path or UI layer.
public sealed class HeadlessGameTestSession : IDisposable, IApplicationShutdownParticipant
{
    private const string ApplicationShutdownParticipantId = "headless-game-test-session";
    private const ApplicationShutdownParticipantStage ApplicationShutdownStage =
        ApplicationShutdownParticipantStage.Runtime;
    private const int ApplicationShutdownOrder = 0;
    internal readonly struct SessionCommandOutcome
    {
        public SessionCommandOutcome(
            bool ok,
            string message,
            GameRuntimeFacade.RuntimeCommandCode code = GameRuntimeFacade.RuntimeCommandCode.None
        )
        {
            Ok = ok;
            Message = message ?? "";
            Code =
                code != GameRuntimeFacade.RuntimeCommandCode.None
                    ? code
                    : ok
                        ? GameRuntimeFacade.RuntimeCommandCode.Ok
                        : GameRuntimeFacade.RuntimeCommandCode.Failed;
        }

        public bool Ok { get; }
        public string Message { get; }
        public GameRuntimeFacade.RuntimeCommandCode Code { get; }
    }

    private sealed class BattleEquipmentInstanceSelection
    {
        public bool Ok;
        public string Message = "";
        public StringName InstanceId = "";
        public StringName ItemId = "";
    }

    private sealed class ChangeEquipmentReportSummary
    {
        public bool Ok;
        public string Text = "";
    }

    private static readonly StringName EncounterKindSettlement = "settlement";
    private static readonly StringName HeadlessSettlementLootProfileId = "wolf_den";
    private static readonly StringName HeadlessSettlementLootEncounterId =
        "headless_settlement_wolf_den";
    private const string HeadlessSettlementLootDisplayName = "荒狼巢穴";

    private GameSession _gameSession;
    private GameRuntimeFacade _runtime;
    private bool _ownsGameSession;
    private bool _initialized;
    private bool _disposed;
    private ApplicationLifetimeCoordinator _applicationLifetimeCoordinator;
    private EncounterAnchorData _activeHeadlessEncounterAnchor;
    private string _lastBattleStartDiagnostic = "";

    string IApplicationShutdownParticipant.ShutdownParticipantId =>
        ApplicationShutdownParticipantId;

    ApplicationShutdownParticipantStage IApplicationShutdownParticipant.ShutdownStage =>
        ApplicationShutdownStage;

    int IApplicationShutdownParticipant.ShutdownOrder => ApplicationShutdownOrder;

    ValueTask IApplicationShutdownParticipant.CloseForApplicationShutdownAsync(
        ShutdownReport report
    )
    {
        Dispose(false);
        return ValueTask.CompletedTask;
    }

    public void initialize()
    {
        _initialized = true;
        EnsureGameSession();
    }

    internal void BindOwnedGameSessionForTests(GameSession gameSession)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(gameSession);
        if (
            _initialized
            || _gameSession != null
            || _runtime != null
            || _ownsGameSession
        )
        {
            throw new InvalidOperationException(
                "A test-owned GameSession must be bound before headless initialization."
            );
        }
        if (!GodotObject.IsInstanceValid(gameSession) || gameSession.IsClosed)
        {
            throw new ArgumentException(
                "The test-owned GameSession must be valid and open.",
                nameof(gameSession)
            );
        }

        _gameSession = gameSession;
        _ownsGameSession = true;
    }

    public GameSession GetGameSession()
    {
        return _gameSession;
    }

    public GameSession GetGameSessionTyped() => _gameSession;

    public GameRuntimeFacade GetRuntimeFacade()
    {
        return _runtime;
    }

    public GameRuntimeFacade GetRuntimeFacadeTyped() => _runtime;

    private bool HasWorldLoaded()
    {
        return _runtime != null;
    }

    internal IReadOnlyList<WorldPresetRegistry.WorldPresetInfo> ListPresetsTyped() =>
        WorldPresetRegistry.ListPresetsTyped();

    internal List<Dictionary<string, object>> ListSaveSlotsPlain()
    {
        EnsureGameSession();
        return _gameSession.ListSaveSlotsPlain();
    }

    internal SessionCommandOutcome CreateNewGameTyped(StringName preset_id)
    {
        EnsureGameSession();
        if (!WorldPresetRegistry.TryGetPresetTyped(preset_id, out var preset))
        {
            return new SessionCommandOutcome(
                false,
                $"未找到世界预设 {preset_id}。",
                GameRuntimeFacade.RuntimeCommandCode.NotFound
            );
        }

        UnloadWorldScene();
        int createError = _gameSession.CreateNewSave(
            preset.GenerationConfigPath,
            preset_id,
            string.IsNullOrEmpty(preset.DisplayName) ? "世界" : preset.DisplayName
        );
        if (createError != (int)Error.Ok)
        {
            return new SessionCommandOutcome(
                false,
                $"创建世界失败，错误码 {createError}。",
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        return EnsureWorldLoadedTyped();
    }

    internal SessionCommandOutcome LoadGameTyped(string save_id)
    {
        EnsureGameSession();
        if (string.IsNullOrEmpty(save_id))
        {
            return new SessionCommandOutcome(
                false,
                "存档 ID 不能为空。",
                GameRuntimeFacade.RuntimeCommandCode.InvalidArgument
            );
        }

        UnloadWorldScene();
        int loadError = _gameSession.LoadSave(save_id);
        if (loadError != (int)Error.Ok)
        {
            return new SessionCommandOutcome(
                false,
                $"加载存档失败，错误码 {loadError}。",
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        return EnsureWorldLoadedTyped();
    }

    internal SessionCommandOutcome EnsureWorldLoadedTyped()
    {
        EnsureGameSession();
        if (!_gameSession.HasActiveWorld())
        {
            return new SessionCommandOutcome(
                false,
                "当前没有已加载的世界。",
                GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
            );
        }

        if (HasWorldLoaded())
        {
            SettleFrames();
            return new SessionCommandOutcome(true, "世界地图已可用。");
        }

        _runtime = new GameRuntimeFacade();
        _runtime.Setup(_gameSession);
        SettleFrames();
        return new SessionCommandOutcome(true, "世界地图已载入。");
    }

    internal void SettleFrames()
    {
        SettleFrames(2);
    }

    internal void SettleFrames(int frame_count)
    {
        if (_runtime == null)
        {
            return;
        }

        int iterations = Mathf.Max(frame_count, 1);
        for (int index = 0; index < iterations; index++)
        {
            _runtime.advance(0.0f);
            TryCompleteHeadlessPendingBattleStart();
        }
    }

    private bool TryCompleteHeadlessPendingBattleStart()
    {
        if (_runtime == null || _runtime.IsBattleActive())
        {
            return true;
        }

        GameRuntimeFacade runtimeFacade = _runtime;
        if (runtimeFacade == null)
        {
            return false;
        }

        GameRuntimePendingBattleGenerationRequest pendingRequest =
            runtimeFacade.GetPendingBattleGenerationRequestState();
        if (pendingRequest == null || pendingRequest.IsEmpty)
        {
            return false;
        }

        EncounterAnchorData encounterAnchor = pendingRequest.EncounterAnchor;
        if (encounterAnchor == null)
        {
            return false;
        }

        BattleRuntimeModule battleRuntime = _runtime.GetBattleRuntime();
        if (battleRuntime == null)
        {
            return false;
        }
        SyncBattleRuntimeContentCatalogs(battleRuntime);
        BattleObjectiveDefinition objectiveDefinition =
            runtimeFacade.ResolveBattleObjectiveDefinition(encounterAnchor);
        if (objectiveDefinition == null)
        {
            return false;
        }

        int seed =
            pendingRequest.Seed != 0
                ? pendingRequest.Seed
                : TrueRandomSeedService.RandiRange(1, int.MaxValue - 1);
        Dictionary<string, object> typedContext = BuildBattleStartContextTyped(
            encounterAnchor,
            pendingRequest.CloneContextPlain()
        );
        BattleState runtimeState;
        using (
            GodotProjectionLease<GDictionary> contextLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    typedContext,
                    "headless-battle-start-context",
                    LifetimeDomain.Request,
                    "HeadlessGameTestSession.TryStartPendingBattle"
                )
        )
        {
            runtimeState = battleRuntime.StartBattleBorrowingContext(
                encounterAnchor,
                seed,
                objectiveDefinition,
                contextLease.Value
            );
        }
        BattleState storedState = battleRuntime.GetState();
        _lastBattleStartDiagnostic = BuildBattleStartDiagnostic(
            runtimeState,
            storedState,
            seed,
            typedContext
        );
        if (runtimeState == null || runtimeState.IsEmpty())
        {
            return false;
        }

        _activeHeadlessEncounterAnchor = encounterAnchor;
        runtimeFacade.ClearPendingBattleGenerationRequest();
        runtimeFacade.RefreshBattleRuntimeState();
        runtimeFacade.PresentBattleStartConfirmation();
        return true;
    }

    internal SessionCommandOutcome SetPartyStorageCapacityTyped(int capacity)
    {
        if (!HasWorldLoaded() || _runtime == null)
        {
            return new SessionCommandOutcome(false, "当前世界地图不可用。");
        }

        PartyState partyState = _runtime.GetPartyState();
        if (partyState == null)
        {
            return new SessionCommandOutcome(false, "当前不存在队伍数据。");
        }

        int resolvedCapacity = Mathf.Max(capacity, 0);
        bool firstMemberAssigned = false;
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            var unitProgress = memberState?.progression as UnitProgress;
            UnitBaseAttributes unitBaseAttributes = unitProgress?.unit_base_attributes;
            if (unitBaseAttributes == null)
            {
                continue;
            }

            unitBaseAttributes.custom_stats["storage_space"] = !firstMemberAssigned
                ? resolvedCapacity
                : 0;
            firstMemberAssigned = true;
        }

        SettleFrames(1);
        if (!firstMemberAssigned)
        {
            return new SessionCommandOutcome(false, "当前队伍没有可调整仓库容量的成员。");
        }
        return new SessionCommandOutcome(true, $"已将共享仓库总容量调整为 {resolvedCapacity}。");
    }

    internal SessionCommandOutcome StartBattleByKindTyped(StringName encounter_kind)
    {
        if (!HasWorldLoaded() || _runtime == null)
        {
            return new SessionCommandOutcome(false, "当前世界地图不可用。");
        }
        if (_runtime.IsBattleActive())
        {
            return new SessionCommandOutcome(false, "当前已有进行中的战斗。");
        }

        EncounterAnchorData encounterAnchor =
            FindNearestEncounterAnchor(encounter_kind)
            ?? BuildHeadlessEncounterAnchor(encounter_kind);
        if (encounterAnchor == null)
        {
            return new SessionCommandOutcome(false, $"未找到 encounter_kind={encounter_kind} 的遭遇。");
        }

        _activeHeadlessEncounterAnchor = encounterAnchor;
        _gameSession.SetBattleSaveLock(true);
        StartBattleDirect(encounterAnchor);
        if (!_runtime.IsBattleActive())
        {
            _activeHeadlessEncounterAnchor = null;
            _gameSession.SetBattleSaveLock(false);
            string statusText = _runtime.GetStatusText();
            return new SessionCommandOutcome(
                false,
                $"遭遇 {encounterAnchor.display_name} 未能开始战斗。status={statusText}; {_lastBattleStartDiagnostic}"
            );
        }
        return new SessionCommandOutcome(true, $"已进入遭遇 {encounterAnchor.display_name} 的战斗准备。");
    }

    private void StartBattleDirect(EncounterAnchorData encounterAnchor)
    {
        _lastBattleStartDiagnostic = "";
        if (_runtime == null || encounterAnchor == null)
        {
            _lastBattleStartDiagnostic = "runtime_or_anchor_missing";
            return;
        }

        BattleRuntimeModule battleRuntime = _runtime.GetBattleRuntime();
        if (battleRuntime == null)
        {
            _lastBattleStartDiagnostic = "battle_runtime_missing";
            return;
        }
        SyncBattleRuntimeContentCatalogs(battleRuntime);

        GameRuntimeFacade runtimeFacade = _runtime as GameRuntimeFacade;
        BattleObjectiveDefinition objectiveDefinition =
            runtimeFacade?.ResolveBattleObjectiveDefinition(encounterAnchor);
        if (objectiveDefinition == null)
        {
            _lastBattleStartDiagnostic = "objective_definition_missing";
            return;
        }

        _runtime.PrepareBattleStart(encounterAnchor);
        Dictionary<string, object> typedContext = BuildBattleStartContextTyped(encounterAnchor);
        int seed = TrueRandomSeedService.RandiRange(1, int.MaxValue - 1);
        BattleState runtimeState;
        using (
            GodotProjectionLease<GDictionary> contextLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    typedContext,
                    "headless-battle-start-context",
                    LifetimeDomain.Request,
                    "HeadlessGameTestSession.StartBattleDirect"
                )
        )
        {
            runtimeState = battleRuntime.StartBattleBorrowingContext(
                encounterAnchor,
                seed,
                objectiveDefinition,
                contextLease.Value
            );
        }
        BattleState storedState = battleRuntime.GetState();
        _lastBattleStartDiagnostic = BuildBattleStartDiagnostic(
            runtimeState,
            storedState,
            seed,
            typedContext
        );
        if (runtimeState == null || runtimeState.IsEmpty())
        {
            return;
        }

        runtimeFacade?.ClearPendingBattleGenerationRequest();
        runtimeFacade?.RefreshBattleRuntimeState();
        runtimeFacade?.PresentBattleStartConfirmation();
    }

    internal SessionCommandOutcome FinishActiveBattleTyped(StringName winner_faction_id)
    {
        if (!HasWorldLoaded() || _runtime == null)
        {
            return new SessionCommandOutcome(false, "当前世界地图不可用。");
        }
        if (!_runtime.IsBattleActive())
        {
            return new SessionCommandOutcome(false, "当前没有进行中的战斗。");
        }
        if (winner_faction_id != "player" && winner_faction_id != "hostile")
        {
            return new SessionCommandOutcome(false, "胜利方只能是 player 或 hostile。");
        }

        BattleState battleState = _runtime.GetBattleState();
        if (battleState == null || battleState.IsEmpty())
        {
            return new SessionCommandOutcome(false, "当前战斗状态不可用。");
        }

        GameRuntimeFacade facade = _runtime as GameRuntimeFacade;
        BattleRuntimeModule battleRuntime = facade?.GetBattleRuntime();
        if (facade == null || battleRuntime == null)
        {
            return new SessionCommandOutcome(false, "当前战斗运行时不可用。");
        }

        PrimeHeadlessBattleLootIfNeeded(winner_faction_id);
        using BattleEventBatch batch = new();
        battleRuntime.BeginObjectiveMutation();
        bool mutationCompleted = false;
        try
        {
            IReadOnlyList<StringName> defeatedUnitIds =
                winner_faction_id == "player"
                    ? battleState.GetEnemyUnitIdsTyped()
                    : battleState.GetAllyUnitIdsTyped();
            foreach (StringName unitId in defeatedUnitIds)
            {
                BattleUnitState unit = battleState.GetUnit(unitId);
                if (unit?.IsAlive() != true)
                    continue;
                unit.MarkDead();
                battleRuntime.HandleUnitDefeatedByRuntimeEffect(
                    unit,
                    null,
                    batch,
                    $"{unit.display_name} 被测试结算击倒。",
                    new BattleDefeatHandlingOptions(
                        collectLoot: false,
                        recordEnemyDefeatedAchievement: false
                    )
                );
            }
            mutationCompleted = true;
        }
        finally
        {
            battleRuntime.EndObjectiveMutation(batch, mutationCompleted);
        }
        facade.ApplyBattleBatch(batch);
        _activeHeadlessEncounterAnchor = null;
        SettleFrames(1);
        return new SessionCommandOutcome(
            true,
            "战斗已按正式目标结算。",
            GameRuntimeFacade.RuntimeCommandCode.Ok
        );
    }

    internal SessionCommandOutcome ChangeBattleEquipmentTyped(
        StringName operation,
        StringName slot_id,
        StringName item_id,
        StringName instance_id,
        StringName target_unit_id
    )
    {
        if (!HasWorldLoaded() || _runtime == null)
        {
            return new SessionCommandOutcome(false, "当前世界地图不可用。");
        }
        if (!_runtime.IsBattleActive())
        {
            return new SessionCommandOutcome(false, "当前没有进行中的战斗。");
        }

        BattleState battleState = _runtime.GetBattleState();
        if (battleState == null || battleState.IsEmpty())
        {
            return new SessionCommandOutcome(false, "当前战斗状态不可用。");
        }
        if (battleState.PhaseKind != BattlePhaseKind.UnitActing || battleState.active_unit_id == "")
        {
            return new SessionCommandOutcome(false, "当前没有可手动操作的行动单位。");
        }
        if (battleState.ModalStateKind != BattleModalStateKind.None)
        {
            return new SessionCommandOutcome(false, "当前战斗流程阻止换装。");
        }

        battleState.TryGetUnitTyped(battleState.active_unit_id, out BattleUnitState activeUnit);
        if (activeUnit == null || !activeUnit.IsAlive())
        {
            return new SessionCommandOutcome(false, "当前行动单位不可用。");
        }
        if (activeUnit.ControlModeKind != BattleUnitControlMode.Manual)
        {
            return new SessionCommandOutcome(false, "当前行动单位不是手动单位。");
        }

        var facade = _runtime as GameRuntimeFacade;
        if (facade == null)
        {
            return new SessionCommandOutcome(false, "当前战斗运行时不可用。");
        }

        var battleRuntime = facade.GetBattleRuntime();
        if (battleRuntime == null)
        {
            return new SessionCommandOutcome(false, "当前战斗运行时不可用。");
        }

        BattleEquipmentOperationKind operationKind =
            BattleTypedNames.ToEquipmentOperationKind(operation);
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.ChangeEquipment,
            unit_id = activeUnit.unit_id,
            target_unit_id = target_unit_id != "" ? target_unit_id : activeUnit.unit_id,
            EquipmentOperationKind = operationKind,
            equipment_slot_id = slot_id,
            equipment_item_id = item_id,
            equipment_instance_id = instance_id,
        };

        if (operationKind == BattleEquipmentOperationKind.Equip)
        {
            BattleEquipmentInstanceSelection resolvedInstance = ResolveBattleBackpackEquipmentInstance(
                battleState,
                item_id,
                instance_id
            );
            if (resolvedInstance == null || !resolvedInstance.Ok)
            {
                return new SessionCommandOutcome(
                    false,
                    resolvedInstance?.Message ?? "战斗背包状态不可用。"
                );
            }

            command.equipment_instance_id = resolvedInstance.InstanceId;
            command.equipment_item_id = resolvedInstance.ItemId;
            command.equipment_instance = BuildBattleCommandEquipmentInstance(
                command.equipment_item_id,
                command.equipment_instance_id
            );
        }
        else if (operationKind == BattleEquipmentOperationKind.Unequip)
        {
            if (command.equipment_instance_id != "")
            {
                command.equipment_instance = BuildBattleCommandEquipmentInstance(
                    command.equipment_item_id,
                    command.equipment_instance_id
                );
            }
        }
        else
        {
            return new SessionCommandOutcome(false, "战斗换装操作只能是 equip 或 unequip。");
        }

        var batch = battleRuntime.IssueCommand(command);
        if (batch != null)
        {
            facade.ApplyBattleBatch(batch);
        }
        SettleFrames(1);

        ChangeEquipmentReportSummary report = FindLastChangeEquipmentReport(
            batch != null
                ? batch.ReportEntriesTyped
                : Array.Empty<IReadOnlyDictionary<string, object>>()
        );
        if (report == null)
        {
            return new SessionCommandOutcome(false, "战斗换装命令未产生结果。");
        }
        return new SessionCommandOutcome(
            report.Ok,
            report.Text,
            report.Ok
                ? GameRuntimeFacade.RuntimeCommandCode.Ok
                : GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private static EquipmentInstanceState BuildBattleCommandEquipmentInstance(
        StringName itemId,
        StringName instanceId
    )
    {
        return new EquipmentInstanceState
        {
            item_id = ProgressionDataUtils.to_string_name(itemId),
            instance_id = ProgressionDataUtils.to_string_name(instanceId),
        };
    }

    internal GodotProjectionLease<GDictionary> BuildSnapshotLease()
    {
        return RuntimePlainPayload.ProjectDictionaryLease(
            BuildSnapshotPlain(),
            "headless-game-test-session-snapshot",
            LifetimeDomain.Request,
            "HeadlessGameTestSession.root"
        );
    }

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        var sessionSnapshot = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["active_save_id"] = _gameSession != null ? _gameSession.GetActiveSaveId() : "",
            ["generation_config_path"] =
                _gameSession != null ? _gameSession.GetGenerationConfigPath() : "",
            ["world_loaded"] = HasWorldLoaded(),
            ["presets"] = BuildPresetSnapshotsPlain(),
            ["save_slots"] =
                ClonePlainDictionaryList(
                    _gameSession != null
                        ? _gameSession.PeekSaveSlotsPlain()
                        : new List<Dictionary<string, object>>()
                ),
        };

        var snapshot = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["session"] = sessionSnapshot,
            ["validation"] =
                _gameSession != null
                    ? _gameSession.GetContentValidationSnapshot()
                    : new Dictionary<string, object>(StringComparer.Ordinal),
            ["status"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["view"] = "none",
                ["text"] = "",
            },
            ["modal"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = "" },
            ["logs"] =
                _gameSession != null
                    ? RuntimePlainPayload.CloneDictionary(
                        _gameSession.GetLogSnapshotPlain()
                    )
                    : new Dictionary<string, object>(StringComparer.Ordinal),
            ["world"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["submap"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["party"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["settlement"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["character_info"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["warehouse"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["battle"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["reward"] = new Dictionary<string, object>(StringComparer.Ordinal),
            ["promotion"] = new Dictionary<string, object>(StringComparer.Ordinal),
        };

        if (HasWorldLoaded())
        {
            Dictionary<string, object> worldSnapshot = RuntimePlainPayload.CloneDictionary(
                _runtime.BuildHeadlessSnapshotPlain()
            );
            foreach ((string key, object value) in worldSnapshot)
                snapshot[key] = value;
            AugmentPartyContingencySnapshotTyped(snapshot);
            AugmentBattleSnapshotTyped(snapshot);
        }
        return snapshot;
    }

    internal string BuildTextSnapshot()
    {
        return GameTextSnapshotRenderer.RenderFullSnapshot(BuildSnapshotPlain());
    }

    public void Dispose()
    {
        Dispose(false);
    }

    public void Dispose(bool clear_persisted_game)
    {
        if (_disposed)
        {
            UnregisterApplicationShutdownParticipant();
            return;
        }
        _disposed = true;
        UnregisterApplicationShutdownParticipant();
        UnloadWorldScene();
        if (_gameSession != null && GodotObject.IsInstanceValid(_gameSession))
        {
            if (clear_persisted_game)
            {
                _gameSession.ClearPersistedGame();
            }
            if (_ownsGameSession)
            {
                _gameSession.Dispose();
            }
        }
        _gameSession = null;
        _ownsGameSession = false;
        _activeHeadlessEncounterAnchor = null;
    }

    private void EnsureGameSession()
    {
        if (_gameSession != null && GodotObject.IsInstanceValid(_gameSession))
        {
            RegisterApplicationShutdownParticipantIfAvailable();
            return;
        }

        SceneTree sceneTree = GetSceneTree();
        if (sceneTree == null)
        {
            return;
        }

        _gameSession = sceneTree.Root.GetNodeOrNull<GameSession>("GameSession");
        if (_gameSession != null)
        {
            GC.KeepAlive(_gameSession);
            _ownsGameSession = false;
            RegisterApplicationShutdownParticipantIfAvailable();
            return;
        }

        throw new InvalidOperationException(
            "HeadlessGameTestSession requires the canonical GameSession or an explicit "
                + "BindOwnedGameSessionForTests call before initialization."
        );
    }

    private void RegisterApplicationShutdownParticipantIfAvailable()
    {
        if (_disposed)
            return;

        SceneTree sceneTree = GetSceneTree();
        ApplicationLifetimeCoordinator coordinator = sceneTree
            ?.Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        if (coordinator == null)
            return;
        if (ReferenceEquals(_applicationLifetimeCoordinator, coordinator))
            return;

        UnregisterApplicationShutdownParticipant();
        coordinator.RegisterParticipant(this);
        _applicationLifetimeCoordinator = coordinator;
    }

    private void UnregisterApplicationShutdownParticipant()
    {
        ApplicationLifetimeCoordinator coordinator = _applicationLifetimeCoordinator;
        _applicationLifetimeCoordinator = null;
        if (coordinator == null || !GodotObject.IsInstanceValid(coordinator))
            return;

        coordinator.UnregisterParticipant(this);
    }

    private void UnloadWorldScene()
    {
        if (!HasWorldLoaded())
        {
            AbortHeadlessBattleSaveIfLocked();
            _runtime = null;
            _activeHeadlessEncounterAnchor = null;
            return;
        }

        if (_runtime != null)
        {
            _runtime.Dispose();
        }
        AbortHeadlessBattleSaveIfLocked();
        _runtime = null;
        _activeHeadlessEncounterAnchor = null;
        SettleFrames();
    }

    private void AbortHeadlessBattleSaveIfLocked()
    {
        if (_gameSession == null || !GodotObject.IsInstanceValid(_gameSession))
        {
            return;
        }

        bool wasBattleLocked = _gameSession.IsBattleSaveLocked();
        if (wasBattleLocked)
        {
            _gameSession.DiscardPendingSave();
        }
        _gameSession.SetBattleSaveLock(false);
    }

    private static SceneTree GetSceneTree()
    {
        return Engine.GetMainLoop() as SceneTree;
    }

    private EncounterAnchorData FindNearestEncounterAnchor(StringName encounterKind)
    {
        if (_runtime == null)
        {
            return null;
        }

        Vector2I playerCoord = _runtime.GetPlayerCoord();
        EncounterAnchorData nearestEncounter = null;
        int nearestDistance = int.MaxValue;
        foreach (EncounterAnchorData encounterAnchor in GetWorldEncounterAnchorsTyped())
        {
            if (encounterAnchor == null || encounterAnchor.is_cleared)
            {
                continue;
            }
            if (encounterKind != "" && encounterAnchor.encounter_kind != encounterKind)
            {
                continue;
            }
            if (
                encounterKind == EncounterKindSettlement
                && !EncounterHasFormalLoot(encounterAnchor)
            )
            {
                continue;
            }

            Vector2I delta = encounterAnchor.world_coord - playerCoord;
            int distance = Math.Abs(delta.X) + Math.Abs(delta.Y);
            if (distance > nearestDistance)
            {
                continue;
            }
            if (
                distance == nearestDistance
                && nearestEncounter != null
                && string.CompareOrdinal(
                    encounterAnchor.entity_id.ToString(),
                    nearestEncounter.entity_id.ToString()
                ) >= 0
            )
            {
                continue;
            }
            nearestDistance = distance;
            nearestEncounter = encounterAnchor;
        }
        return nearestEncounter;
    }

    private IReadOnlyList<EncounterAnchorData> GetWorldEncounterAnchorsTyped()
    {
        return _runtime?.GetActiveWorldRuntimeData()?.EncounterAnchors
            ?? Array.Empty<EncounterAnchorData>();
    }

    private bool EncounterHasFormalLoot(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null || _gameSession == null)
        {
            return false;
        }

        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> wildEncounterRosters =
            _gameSession.GetEncounterRosterDefinitions();
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters =
            _gameSession.GetBattleEncounterDefinitions();
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates =
            _gameSession.GetEnemyTemplateDefinitions();
        using var builder = new EncounterRosterBuilder();
        builder.Setup(battleEncounters, wildEncounterRosters, enemyTemplates);
        IReadOnlyList<IReadOnlyDictionary<string, object>> lootEntries =
            builder.BuildLootEntriesPlain(
            encounterAnchor,
            enemyTemplates: enemyTemplates,
            itemDefs: _gameSession.GetItemDefsTyped()
        );
        return lootEntries.Count > 0;
    }

    private EncounterAnchorData BuildHeadlessEncounterAnchor(StringName encounterKind)
    {
        if (encounterKind != EncounterKindSettlement || _runtime == null)
        {
            return null;
        }

        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosters =
            _gameSession.GetEncounterRosterDefinitions();
        if (!rosters.ContainsKey(HeadlessSettlementLootProfileId))
        {
            return null;
        }

        var encounterAnchor = new EncounterAnchorData
        {
            entity_id = HeadlessSettlementLootEncounterId,
            display_name = HeadlessSettlementLootDisplayName,
            world_coord = _runtime.GetPlayerCoord(),
            faction_id = "hostile",
            encounter_kind = EncounterKindSettlement,
            encounter_profile_id = HeadlessSettlementLootProfileId,
        };
        return EncounterHasFormalLoot(encounterAnchor) ? encounterAnchor : null;
    }

    private static StringName ResolveBattleTerrainProfile(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
        {
            return "default";
        }

        string regionTag = encounterAnchor.region_tag.ToString().StripEdges().ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        return regionTag switch
        {
            "canyon" or "north_wilds" or "south_wilds" => "canyon",
            "narrow_assault" => "narrow_assault",
            "holdout_push" => "holdout_push",
            _ => "default",
        };
    }

    private static Dictionary<string, object> BuildBattleStartContextTyped(
        EncounterAnchorData encounterAnchor,
        IReadOnlyDictionary<string, object> baseContext = null
    )
    {
        Dictionary<string, object> context = RuntimePlainPayload.CloneDictionary(baseContext);
        if (!context.ContainsKey("world_coord"))
        {
            context["world_coord"] = encounterAnchor != null ? encounterAnchor.world_coord : default(Vector2I);
        }
        if (!context.ContainsKey("battle_terrain_profile"))
        {
            context["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor)
                .ToString();
        }
        context["validate_spawn_reachability"] = false;
        return context;
    }

    private static string BuildBattleStartDiagnostic(
        BattleState returnedState,
        BattleState storedState,
        int seed,
        IReadOnlyDictionary<string, object> context
    )
    {
        string returnedSummary =
            returnedState == null
                ? "returned=null"
                : $"returned_empty={returnedState.IsEmpty()},returned_units={returnedState.UnitCount},returned_cells={returnedState.CellCount},returned_terrain={returnedState.terrain_profile_id}";
        string storedSummary =
            storedState == null
                ? "stored=null"
                : $"stored_empty={storedState.IsEmpty()},stored_units={storedState.UnitCount},stored_cells={storedState.CellCount},stored_terrain={storedState.terrain_profile_id}";
        return $"seed={seed},terrain={ReadTypedString(context, "battle_terrain_profile")}; {returnedSummary}; {storedSummary}";
    }

    private void SyncBattleRuntimeContentCatalogs(BattleRuntimeModule battleRuntime)
    {
        if (
            battleRuntime == null
            || _gameSession == null
            || !GodotObject.IsInstanceValid(_gameSession)
        )
        {
            return;
        }

        GameContentCatalog contentCatalog = _gameSession.GetContentCatalogTyped();
        battleRuntime.SyncContentCatalogsTyped(
            contentCatalog.GetItemDefsTyped(),
            contentCatalog.GetSkillDefinitionsTyped(),
            contentCatalog.GetTraitDefsTyped(),
            contentCatalog.GetEquipmentAbilityBindingDefinitionsTyped(),
            contentCatalog.GetBarrierProfileDefinitionsTyped()
        );
    }

    private void PrimeHeadlessBattleLootIfNeeded(StringName winnerFactionId)
    {
        if (winnerFactionId != "player" || _runtime == null || _gameSession == null)
        {
            return;
        }
        if (_activeHeadlessEncounterAnchor == null)
        {
            return;
        }

        BattleRuntimeModule battleRuntime = _runtime.GetBattleRuntime();
        if (battleRuntime == null)
        {
            return;
        }

        if (battleRuntime._active_loot_entries.Count > 0)
        {
            return;
        }

        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> wildEncounterRosters =
            _gameSession.GetEncounterRosterDefinitions();
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters =
            _gameSession.GetBattleEncounterDefinitions();
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates =
            _gameSession.GetEnemyTemplateDefinitions();
        using var rosterBuilder = new EncounterRosterBuilder();
        rosterBuilder.Setup(battleEncounters, wildEncounterRosters, enemyTemplates);
        IReadOnlyList<IReadOnlyDictionary<string, object>> previewLootEntries =
            rosterBuilder.BuildLootEntriesPlain(
                _activeHeadlessEncounterAnchor,
                enemyTemplates: enemyTemplates,
                itemDefs: _gameSession.GetItemDefsTyped()
            );
        if (previewLootEntries.Count == 0)
        {
            return;
        }
        foreach (
            BattleLootEntry lootEntry in BattleLootEntryPayload.ParseEntriesPlain(
                previewLootEntries
            )
        )
        {
            battleRuntime._active_loot_entries.Add(lootEntry);
        }
    }

    private BattleEquipmentInstanceSelection ResolveBattleBackpackEquipmentInstance(
        BattleState battleState,
        StringName itemId,
        StringName instanceId
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        StringName normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        if (normalizedItemId == "" && normalizedInstanceId == "")
        {
            return new BattleEquipmentInstanceSelection
            {
                Ok = false,
                Message = "用法: battle equip <slot_id> <item_id> [instance_id=<instance_id>]",
            };
        }

        WarehouseState backpackView = battleState?.GetPartyBackpackView();
        if (backpackView == null)
        {
            return new BattleEquipmentInstanceSelection
            {
                Ok = false,
                Message = "战斗背包状态不可用。",
            };
        }

        var matchingInstances = new List<BattleEquipmentInstanceSelection>();
        foreach (EquipmentInstanceState instance in backpackView.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance == null)
            {
                continue;
            }

            StringName candidateInstanceId = ProgressionDataUtils.to_string_name(
                instance.instance_id
            );
            StringName candidateItemId = ProgressionDataUtils.to_string_name(instance.item_id);
            if (normalizedInstanceId != "" && candidateInstanceId != normalizedInstanceId)
            {
                continue;
            }
            if (normalizedItemId != "" && candidateItemId != normalizedItemId)
            {
                continue;
            }
            matchingInstances.Add(
                new BattleEquipmentInstanceSelection
                {
                    Ok = true,
                    InstanceId = candidateInstanceId,
                    ItemId = candidateItemId,
                }
            );
        }

        if (matchingInstances.Count == 1)
        {
            return matchingInstances[0];
        }
        if (normalizedInstanceId == "" && matchingInstances.Count > 1)
        {
            return new BattleEquipmentInstanceSelection
            {
                Ok = false,
                Message = $"战斗背包中有多个 {normalizedItemId} 装备实例，请指定 instance_id。",
            };
        }

        string label =
            normalizedInstanceId != ""
                ? normalizedInstanceId.ToString()
                : normalizedItemId.ToString();
        return new BattleEquipmentInstanceSelection
        {
            Ok = false,
            Message = $"战斗背包中找不到装备 {label}。",
        };
    }

    private static ChangeEquipmentReportSummary FindLastChangeEquipmentReport(
        IReadOnlyList<IReadOnlyDictionary<string, object>> reportEntries
    )
    {
        if (reportEntries == null)
        {
            return null;
        }
        for (int index = reportEntries.Count - 1; index >= 0; index--)
        {
            IReadOnlyDictionary<string, object> report = reportEntries[index];
            if (report == null || report.Count == 0)
            {
                continue;
            }

            Dictionary<string, object> typedReport = RuntimePlainPayload.CloneDictionary(report);
            string reportType = ReadTypedString(typedReport, "type");
            if (reportType == "change_equipment")
            {
                return new ChangeEquipmentReportSummary
                {
                    Ok = ReadTypedBool(typedReport, "ok", false),
                    Text = ReadTypedString(typedReport, "text"),
                };
            }
        }
        return null;
    }

    private void AugmentPartyContingencySnapshotTyped(Dictionary<string, object> snapshot)
    {
        Dictionary<string, object> partySnapshot = ReadTypedDictionary(snapshot, "party");
        Dictionary<string, object> statusByMember = ReadTypedDictionary(
            partySnapshot,
            "contingency_status_by_member"
        );
        if (statusByMember.Count == 0 || _runtime == null)
        {
            return;
        }

        foreach (object statusValue in statusByMember.Values)
        {
            if (statusValue is not Dictionary<string, object> memberStatus)
            {
                continue;
            }
            StringName memberId = ReadTypedStringName(memberStatus, "member_id");
            AttributeSnapshot attributeSnapshot = _runtime.GetMemberAttributeSnapshot(memberId);
            PartyMemberState memberState = _runtime.GetPartyState()?.GetMemberState(memberId);
            int effectiveMpMax = Mathf.Max(
                attributeSnapshot?.GetValue(AttributeService.MP_MAX)
                    ?? memberState?.current_mp
                    ?? 0,
                0
            );
            foreach (object setupValue in ReadTypedArray(memberStatus, "setups"))
            {
                if (setupValue is Dictionary<string, object> setupSnapshot)
                    setupSnapshot["effective_mp_max"] = effectiveMpMax;
            }
        }
    }

    private void AugmentBattleSnapshotTyped(Dictionary<string, object> snapshot)
    {
        Dictionary<string, object> battleSnapshot = ReadTypedDictionary(snapshot, "battle");
        if (!ReadTypedBool(battleSnapshot, "active", false))
        {
            return;
        }

        BattleState battleState = _runtime != null ? _runtime.GetBattleState() : null;
        if (battleState == null || battleState.IsEmpty())
        {
            return;
        }

        battleSnapshot["party_backpack"] = BuildBattleBackpackSnapshotTyped(
            battleState.GetPartyBackpackView()
        );
        Dictionary<string, object> contingencySnapshot = ReadTypedDictionary(
            battleSnapshot,
            "contingency"
        );
        IReadOnlyList<object> units = ReadTypedArray(battleSnapshot, "units");
        foreach (object unitSnapshotValue in units)
        {
            if (unitSnapshotValue is not Dictionary<string, object> unitSnapshot)
            {
                continue;
            }

            StringName unitId = ReadTypedStringName(unitSnapshot, "unit_id");
            battleState.TryGetUnitTyped(unitId, out BattleUnitState unitState);
            if (unitState == null)
            {
                continue;
            }

            List<object> equipmentEntries = BuildBattleEquipmentEntriesTyped(
                unitState.GetEquipmentView()
            );
            unitSnapshot["hp_max"] = GetBattleUnitHpMax(unitState);
            unitSnapshot["mp_max"] = GetBattleUnitAttributeValue(
                unitState,
                AttributeService.MP_MAX
            );
            unitSnapshot["reserved_mp_max"] = GetBattleUnitAttributeValue(
                unitState,
                AttributeService.RESERVED_MP_MAX
            );
            unitSnapshot["contingency_state"] = ResolveBattleUnitContingencyState(
                contingencySnapshot,
                unitState.unit_id
            );
            unitSnapshot["contingency_suppressed"] = ResolveBattleUnitContingencySuppressed(
                contingencySnapshot,
                unitState.unit_id
            );
            unitSnapshot["contingency_release_queue_count"] = CountQueuedContingencyContextsForOwner(
                contingencySnapshot,
                unitState.unit_id
            );
            unitSnapshot["consumed_contingency_setup_ids"] = StringNameArrayToStringList(
                unitState.GetConsumedContingencySetupIdsTyped()
            );
            unitSnapshot["equipment"] = equipmentEntries;
            unitSnapshot["equipment_count"] = equipmentEntries.Count;
        }
    }

    private static Dictionary<string, object> BuildBattleBackpackSnapshotTyped(
        WarehouseState backpackView
    )
    {
        var stackEntries = new List<object>();
        var equipmentEntries = new List<object>();
        if (backpackView != null)
        {
            foreach (WarehouseStackState stack in backpackView.GetNonEmptyStacksTyped())
            {
                if (stack == null)
                {
                    continue;
                }
                stackEntries.Add(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["item_id"] = stack.item_id.ToString(),
                        ["quantity"] = stack.quantity,
                    }
                );
            }

            foreach (EquipmentInstanceState instance in backpackView.GetNonEmptyEquipmentInstancesTyped())
            {
                if (instance == null)
                {
                    continue;
                }
                equipmentEntries.Add(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["instance_id"] = instance.instance_id.ToString(),
                        ["item_id"] = instance.item_id.ToString(),
                    }
                );
            }
        }

        equipmentEntries = SortEquipmentEntriesByInstanceIdTyped(equipmentEntries);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["stack_count"] = stackEntries.Count,
            ["equipment_instance_count"] = equipmentEntries.Count,
            ["used_slots"] = stackEntries.Count + equipmentEntries.Count,
            ["stacks"] = stackEntries,
            ["equipment_instances"] = equipmentEntries,
        };
    }

    private static List<object> BuildBattleEquipmentEntriesTyped(EquipmentState equipmentView)
    {
        var entries = new List<object>();
        if (equipmentView == null)
        {
            return entries;
        }

        foreach (StringName entrySlotId in equipmentView.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipmentView.GetEntry(entrySlotId);
            if (entry == null)
            {
                continue;
            }

            entries.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["slot_id"] = entrySlotId.ToString(),
                    ["item_id"] = entry.item_id.ToString(),
                    ["instance_id"] = entry.instance_id.ToString(),
                    ["occupied_slot_ids"] = StringNameArrayToStringList(entry.occupied_slot_ids),
                }
            );
        }
        return entries;
    }

    private static int GetBattleUnitHpMax(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
        {
            return 0;
        }
        return Mathf.Max(
            unitState.attribute_snapshot.GetValue(new StringName("hp_max")),
            1
        );
    }

    private static int GetBattleUnitAttributeValue(
        BattleUnitState unitState,
        StringName attributeId
    )
    {
        if (unitState?.attribute_snapshot == null)
        {
            return 0;
        }
        return Mathf.Max(unitState.attribute_snapshot.GetValue(attributeId), 0);
    }

    private static string ResolveBattleUnitContingencyState(
        Dictionary<string, object> contingencySnapshot,
        StringName unitId
    )
    {
        bool hasInstance = false;
        foreach (Dictionary<string, object> instance in ContingencySnapshotDictionaries(
            contingencySnapshot,
            "instances"
        ))
        {
            if (ReadTypedString(instance, "owner_unit_id") != unitId.ToString())
            {
                continue;
            }
            hasInstance = true;
            if (!ReadTypedBool(instance, "consumed", false))
            {
                return "armed";
            }
        }
        return hasInstance ? "consumed" : "none";
    }

    private static bool ResolveBattleUnitContingencySuppressed(
        Dictionary<string, object> contingencySnapshot,
        StringName unitId
    )
    {
        foreach (Dictionary<string, object> instance in ContingencySnapshotDictionaries(
            contingencySnapshot,
            "instances"
        ))
        {
            if (
                ReadTypedString(instance, "owner_unit_id") == unitId.ToString()
                && ReadTypedBool(instance, "suppressed", false)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int CountQueuedContingencyContextsForOwner(
        Dictionary<string, object> contingencySnapshot,
        StringName unitId
    )
    {
        int count = 0;
        foreach (Dictionary<string, object> context in ContingencySnapshotDictionaries(
            contingencySnapshot,
            "queued_release_contexts"
        ))
        {
            if (ReadTypedString(context, "owner_unit_id") == unitId.ToString())
            {
                count += 1;
            }
        }
        return count;
    }

    private static IEnumerable<Dictionary<string, object>> ContingencySnapshotDictionaries(
        Dictionary<string, object> contingencySnapshot,
        string key
    )
    {
        foreach (object value in ReadTypedArray(contingencySnapshot, key))
            if (value is Dictionary<string, object> dictionary)
                yield return dictionary;
    }

    private static List<object> StringNameArrayToStringList(
        IEnumerable<StringName> values
    )
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (var value in values)
        {
            result.Add(value.ToString());
        }
        return result;
    }

    private static List<object> SortEquipmentEntriesByInstanceIdTyped(List<object> entries)
    {
        var sorted = new List<Dictionary<string, object>>();
        foreach (object entryValue in entries)
        {
            if (entryValue is Dictionary<string, object> entry)
            {
                sorted.Add(entry);
            }
        }
        sorted.Sort(
            (left, right) =>
                string.CompareOrdinal(
                    ReadTypedString(left, "instance_id"),
                    ReadTypedString(right, "instance_id")
                )
        );

        var result = new List<object>();
        foreach (Dictionary<string, object> entry in sorted)
            result.Add(entry);
        return result;
    }

    private static List<object> BuildPresetSnapshotsPlain()
    {
        var result = new List<object>();
        foreach (WorldPresetRegistry.WorldPresetInfo preset in WorldPresetRegistry.ListPresetsTyped())
        {
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["preset_id"] = preset.PresetId,
                    ["display_name"] = preset.DisplayName,
                    ["size_label"] = preset.SizeLabel,
                    ["generation_config_path"] = preset.GenerationConfigPath,
                }
            );
        }
        return result;
    }

    private static List<object> ClonePlainDictionaryList(
        IEnumerable<Dictionary<string, object>> source
    )
    {
        var result = new List<object>();
        if (source == null)
            return result;
        foreach (Dictionary<string, object> entry in source)
            result.Add(RuntimePlainPayload.CloneDictionary(entry));
        return result;
    }

    private static Dictionary<string, object> ReadTypedDictionary(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is Dictionary<string, object> dictionary
            ? dictionary
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<object> ReadTypedArray(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> values
            ? values
            : Array.Empty<object>();
    }

    private static string ReadTypedString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => fallback,
        };
    }

    private static StringName ReadTypedStringName(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return "";
        return rawValue switch
        {
            StringName stringNameValue => stringNameValue,
            string stringValue => new StringName(stringValue),
            _ => "",
        };
    }

    private static bool ReadTypedBool(
        IReadOnlyDictionary<string, object> source,
        string key,
        bool fallback
    )
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue is bool boolValue ? boolValue : fallback;
    }

}
