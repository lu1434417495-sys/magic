using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class GameRuntimeFacade : IGameRuntimeSnapshotSource, IDisposable
{
    public enum RuntimeCommandCode
    {
        None = 0,
        Ok = 1,
        Failed = 2,
        InvalidArgument = 3,
        InvalidState = 4,
        NotFound = 5,
        RuntimeUnavailable = 6,
        PersistenceFailure = 7,
    }

    private static readonly StringName EncounterKindSettlement = "settlement";
    private const float WorldMoveRepeatInterval = 0.5f;
    private const int BattleAutoAdvanceTickMsec = 1000;
    private const int MaxCommandWorldMoveCount = 256;
    private const string PartyWarehouseInteractionId = "party_warehouse";

    internal sealed class RuntimeCommandResult
    {
        public bool Ok { get; private set; }
        public string Message { get; private set; } = "";
        public RuntimeCommandCode Code { get; private set; }
        public BattleRefreshMode BattleRefreshMode { get; private set; } = BattleRefreshMode.None;

        public static RuntimeCommandResult Success(
            string message = "",
            RuntimeCommandCode code = RuntimeCommandCode.Ok,
            BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
        )
        {
            return new RuntimeCommandResult
            {
                Ok = true,
                Message = message ?? "",
                Code = code == RuntimeCommandCode.None ? RuntimeCommandCode.Ok : code,
                BattleRefreshMode = battleRefreshMode,
            };
        }

        public static RuntimeCommandResult Failure(
            string message,
            RuntimeCommandCode code = RuntimeCommandCode.Failed
        )
        {
            return new RuntimeCommandResult
            {
                Ok = false,
                Message = message ?? "",
                Code = code == RuntimeCommandCode.None ? RuntimeCommandCode.Failed : code,
            };
        }

    }

    internal WorldMapGenerationConfig _generation_config;
    internal GameSession _game_session;
    internal GameRoot _game_root;
    internal GameContentCatalog _content_catalog;
    internal WorldMapGridSystem _grid_system = new();
    internal WorldMapFogSystem _fog_system = new();
    internal BattleGridService _battle_grid_service = new();
    internal CharacterManagementModule _character_management = new();
    internal PartyWarehouseService _party_warehouse_service = new();
    internal EquipmentDropService _equipment_drop_service = new();
    internal EquipmentTraitRollService _equipment_trait_roll_service;
    private GameSession _equipment_trait_roll_service_session;
    private GameContentCatalog _equipment_trait_roll_service_catalog;
    private long _equipment_trait_roll_service_catalog_revision = long.MinValue;
    internal PartyItemUseService _party_item_use_service = new();
    internal PartyEquipmentService _party_equipment_service = new();
    internal EncounterRosterBuilder _encounter_roster_builder = new();
    internal WildEncounterGrowthSystem _wild_encounter_growth_system = new();
    internal BattleRuntimeModule _battle_runtime;
    internal Vector2I _player_coord = Vector2I.Zero;
    internal Vector2I _selected_coord = Vector2I.Zero;
    internal bool _settlement_entry_active;
    internal Vector2I _settlement_entry_source_coord = new(-1, -1);
    internal Vector2I _settlement_entry_target_coord = new(-1, -1);
    internal string _player_faction_id = "player";
    internal WorldMapDataContext _world_map_data_context = new();
    private readonly GameRuntimePendingSubmapPrompt _pending_submap_prompt = new();
    internal readonly RuntimePayloadStore _pending_battle_start_prompt = new();
    private readonly GameRuntimePendingBattleGenerationRequest _pending_battle_generation_request = new();
    internal PartyState _party_state;
    internal BattleState _battle_state;
    internal int _battle_auto_tick_remainder_msec;
    private GameRuntimeSnapshotBuilder _snapshot_builder = new();
    internal GameRuntimeCommandLogger _command_logger = new();
    private GameRuntimeBattleWritebackService _battle_writeback_service = new();
    internal GameRuntimeBattleLootCommitService _battle_loot_commit_service = new();
    private GameRuntimeCharacterInfoBuilder _character_info_builder = new();
    internal BattleSessionFacade _battle_session_facade = new();
    internal GameRuntimeBattleSelection _battle_selection = new();
    private readonly GameRuntimeBattleSelectionState _battle_selection_state = new();
    internal GameRuntimeSettlementCommandHandler _settlement_command_handler = new();
    internal GameRuntimeWarehouseHandler _warehouse_handler = new();
    internal GameRuntimePartyCommandHandler _party_command_handler = new();
    internal GameRuntimeRewardFlowHandler _reward_flow_handler = new();
    internal GameRuntimeQuestCommandHandler _quest_command_handler = new();
    internal StringName _active_battle_encounter_id = "";
    internal string _active_battle_encounter_name = "";
    internal readonly RuntimePayloadStore _pending_promotion_prompt = new();
    internal GArray _held_world_move_keys = new();
    internal PendingCharacterReward _active_reward;
    internal readonly RuntimePayloadStore _pending_world_promotion_prompt = new();
    internal RuntimeModalKind _active_modal_kind = RuntimeModalKind.None;
    internal string _active_warehouse_entry_label = "";
    internal string _active_settlement_id = "";
    internal string _active_settlement_feedback_text = "";
    internal readonly RuntimePayloadStore _active_contract_board_context = new();
    internal readonly RuntimePayloadStore _active_shop_context = new();
    internal readonly RuntimePayloadStore _active_forge_context = new();
    internal readonly RuntimePayloadStore _active_stagecoach_context = new();
    internal string _current_status_message = "";
    internal BattleRefreshMode _last_advance_battle_refresh_mode = BattleRefreshMode.None;
    internal readonly RuntimePayloadStore _last_battle_loot_snapshot = new();
    internal GArray _pending_command_battle_batches = new();
    internal readonly RuntimePayloadStore _active_character_info_context = new();
    internal readonly RuntimePayloadStore _active_game_over_context = new();
    internal StringName _party_selected_member_id = "";
    private ContingencySetupMutationResult _last_contingency_command_result =
        ContingencySetupMutationResult.Failure("", "", "");
    private readonly Dictionary<StringName, WildEncounterRosterDef> _wild_encounter_roster_defs = new();
    private bool _disposed;

    internal Vector2I _battle_selected_coord
    {
        get => _battle_selection_state.battle_selected_coord;
        set => _battle_selection_state.battle_selected_coord = value;
    }

    internal StringName _selected_battle_skill_id
    {
        get => _battle_selection_state.selected_skill_id;
        set => _battle_selection_state.selected_skill_id = value;
    }

    internal StringName _selected_battle_skill_variant_id
    {
        get => _battle_selection_state.selected_skill_variant_id;
        set => _battle_selection_state.selected_skill_variant_id = value;
    }

    internal GVector2IArray _queued_battle_skill_target_coords
    {
        get => ToVector2IArray(_battle_selection_state.queued_target_coords);
        set => _battle_selection_state.SetTargetCoords(value ?? new GVector2IArray());
    }

    internal GStringNameArray _queued_battle_skill_target_unit_ids
    {
        get => ToStringNameArray(_battle_selection_state.queued_target_unit_ids);
        set => _battle_selection_state.SetTargetUnitIds(value ?? new GStringNameArray());
    }

    public StringName _last_manual_battle_unit_id
    {
        get => _battle_selection_state.last_manual_unit_id;
        set => _battle_selection_state.last_manual_unit_id = value;
    }

    public GameRuntimeFacade()
    {
        _battle_runtime = new BattleRuntimeModule();
        BindRuntimeSidecarOwners();
    }

    public void Setup(GameSession game_session)
    {
        _game_session = game_session;
        _game_root = _game_session?.GetGameRootTyped();
        _content_catalog = _game_root?.GetContentCatalogTyped();
        if (_game_session == null || !_game_session.HasActiveWorld())
            return;

        _generation_config = _game_session.GetGenerationConfig();
        if (_generation_config == null)
            return;

        _world_map_data_context.BindRootWorldData(
            _game_session.GetWorldData()
        );
        RebuildWildEncounterRosterDefIndex(_content_catalog.GetWildEncounterRostersTyped());
        _encounter_roster_builder.Setup(
            _content_catalog.GetWildEncounterRostersTyped(),
            _content_catalog.GetEnemyTemplatesTyped()
        );
        _party_state = _game_session.GetPartyState();
        _player_coord = _game_session.GetPlayerCoord();
        _player_faction_id = _game_session.GetPlayerFactionId();

        _character_management.setup(
            _party_state,
            _content_catalog.GetSkillDefsTyped(),
            _content_catalog.GetProfessionDefsTyped(),
            _content_catalog.GetAchievementDefsTyped(),
            _content_catalog.GetItemDefsTyped(),
            _content_catalog.GetQuestDefsTyped(),
            _content_catalog.GetTraitDefsTyped(),
            GetEquipmentInstanceIdAllocator(),
            _content_catalog.GetProgressionIdentityCatalogTyped()
        );
        SetupPartyWarehouseService(
            _party_warehouse_service,
            _party_state,
            _content_catalog.GetItemDefsTyped()
        );
        _party_item_use_service.Setup(
            _party_state,
            _content_catalog.GetItemDefsTyped(),
            _content_catalog.GetSkillDefsTyped(),
            _party_warehouse_service,
            _character_management
        );
        _party_equipment_service.Setup(
            _party_state,
            _content_catalog.GetItemDefsTyped(),
            _party_warehouse_service,
            GetEquipmentInstanceIdAllocator()
        );
        _battle_runtime.setup(
            _character_management,
            _content_catalog.GetSkillDefsTyped(),
            _content_catalog.GetEnemyTemplatesTyped(),
            _content_catalog.GetEnemyAiBrainsTyped(),
            _encounter_roster_builder,
            _equipment_drop_service,
            _content_catalog.GetItemDefsTyped(),
            null,
            GetEquipmentInstanceIdAllocator(),
            _content_catalog.GetBattleSpecialProfileRegistrySnapshot(),
            _content_catalog.GetSkillCatalogTyped()
        );

        _snapshot_builder.Setup(this);
        _command_logger.Setup(this);
        _battle_writeback_service.Setup(this);
        _battle_loot_commit_service.Setup(this);
        _character_info_builder.Setup(this);
        _battle_session_facade.Setup(this);
        _battle_selection_state.ResetForBattleEnd();
        _battle_selection.Setup(this);
        _settlement_command_handler.SetupRuntime(this);
        _warehouse_handler.Setup(this);
        _party_command_handler.Setup(this);
        _reward_flow_handler.Setup(this);
        _quest_command_handler.Setup(this);

        _sync_active_world_context();
        _selected_coord = _player_coord;
        _RefreshFog();
        _active_modal_kind = RuntimeModalKind.None;
        _active_settlement_id = "";
        _active_settlement_feedback_text = "";
        _ClearSettlementEntryContext();
        _active_contract_board_context.Clear();
        _active_shop_context.Clear();
        _active_forge_context.Clear();
        _active_stagecoach_context.Clear();
        _last_advance_battle_refresh_mode = BattleRefreshMode.None;
        _last_battle_loot_snapshot.Clear();
        _active_character_info_context.Clear();
        _active_game_over_context.Clear();
        _party_selected_member_id = "";
        _active_warehouse_entry_label = "";
        _pending_submap_prompt.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();

        if (IsMainCharacterDead())
        {
            ActivateGameOver(BuildMainCharacterGameOverContext());
            UpdateStatusInternal(
                DictString(
                    _active_game_over_context.ProjectPayload(),
                    "description",
                    "主角已阵亡，本次旅程结束。"
                )
            );
            return;
        }
        if (IsSubmapActive())
        {
            UpdateStatusInternal(
                $"已载入 {GetActiveMapDisplayName()}。{GetSubmapReturnHintText()}"
            );
            return;
        }
        string startSettlementName = _world_map_data_context.GetPlayerStartSettlementName();
        UpdateStatusInternal(
            startSettlementName.Length == 0
                ? "大地图已载入。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。"
                : $"大地图已载入，初始村庄为 {startSettlementName}。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。"
        );
    }

    private void RebuildWildEncounterRosterDefIndex(
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> wildEncounterRosters
    )
    {
        _wild_encounter_roster_defs.Clear();
        if (wildEncounterRosters == null)
        {
            return;
        }
        foreach ((StringName rosterId, WildEncounterRosterDef roster) in wildEncounterRosters)
        {
            if (rosterId == "" || roster == null || roster.profile_id == "")
                continue;
            _wild_encounter_roster_defs[roster.profile_id] = roster;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
        CommitPendingRuntimeStateOnDispose();
        _battle_runtime?.dispose();
        _battle_grid_service?.Dispose();
        _snapshot_builder?.Dispose();
        _command_logger?.Dispose();
        _battle_writeback_service?.Dispose();
        _battle_loot_commit_service?.Dispose();
        _character_info_builder?.Dispose();
        _battle_session_facade?.Dispose();
        _battle_selection?.Dispose();
        _settlement_command_handler?.Dispose();
        _warehouse_handler?.Dispose();
        _party_command_handler?.Dispose();
        _reward_flow_handler?.Dispose();
        _quest_command_handler?.Dispose();
        _character_management?.Dispose();
        _party_warehouse_service?.Dispose();
        _party_item_use_service?.Dispose();
        _party_equipment_service?.Dispose();
        _encounter_roster_builder?.Dispose();

        _game_session = null;
        _game_root = null;
        _content_catalog = null;
        _generation_config = null;
        _world_map_data_context.Dispose();
        _pending_submap_prompt.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _wild_encounter_roster_defs.Clear();
        _party_state = null;
        _battle_state = null;
        _pending_promotion_prompt.Clear();
        _pending_world_promotion_prompt.Clear();
        _active_character_info_context.Clear();
        _active_game_over_context.Clear();
        _active_contract_board_context.Clear();
        _active_shop_context.Clear();
        _active_forge_context.Clear();
        _active_stagecoach_context.Clear();
        _last_advance_battle_refresh_mode = BattleRefreshMode.None;
        _last_battle_loot_snapshot.Clear();
        _battle_selection_state.ResetForBattleEnd();
        _held_world_move_keys.Clear();
        _active_reward = null;
        _ClearSettlementEntryContext();
    }

    private void BindRuntimeSidecarOwners()
    {
        _snapshot_builder?.Setup(this);
        _command_logger?.Setup(this);
        _battle_writeback_service?.Setup(this);
        _battle_loot_commit_service?.Setup(this);
        _character_info_builder?.Setup(this);
    }

    public string GetStatusText() => _current_status_message;

    public GDictionary GetLogSnapshot() => GetLogSnapshot(30);

    public GDictionary GetLogSnapshot(int limit) =>
        _game_session != null
            ? _game_session.GetLogSnapshot(limit)
            : new GDictionary();

    public GArray GetRecentLogs() => GetRecentLogs(30);

    public GArray GetRecentLogs(int limit) =>
        _game_session != null
            ? _game_session.GetRecentLogs(limit)
            : new GArray();

    public string GetActiveLogFilePath() =>
        _game_session != null ? _game_session.GetActiveLogFilePath() : "";

    public string GetActiveModalId() =>
        RuntimeModalKinds.ToPayloadValue(_active_modal_kind);

    public RuntimeModalKind GetActiveModalKind() => _active_modal_kind;

    public GDictionary GetGameOverContext() => _active_game_over_context.ProjectPayload();

    public string GetActiveSettlementId() => _active_settlement_id;

    public string GetActiveMapId() => _world_map_data_context.GetActiveMapId();

    public string GetActiveMapDisplayName() =>
        _world_map_data_context.GetActiveMapDisplayName();

    public string GetSubmapReturnHintText() =>
        _world_map_data_context.GetSubmapReturnHintText();

    public GDictionary GetPendingSubmapPrompt() =>
        GameRuntimePendingSubmapPromptProjection.Project(_pending_submap_prompt);

    public GDictionary GetPendingBattleStartPrompt() =>
        _pending_battle_start_prompt.ProjectPayload();

    public bool IsSubmapActive() => _world_map_data_context.IsSubmapActive();

    public int GetWorldStep() => _world_map_data_context.GetWorldStep();

    public GDictionary GetSelectedSettlement()
    {
        var settlement = _get_settlement_at(_selected_coord);
        return settlement.Count > 0 ? settlement.Duplicate(true) : new GDictionary();
    }

    public GDictionary GetSelectedWorldNpc()
    {
        var npc = _get_world_npc_at(_selected_coord);
        return npc.Count > 0 ? npc.Duplicate(true) : new GDictionary();
    }

    public EncounterAnchorData GetSelectedEncounterAnchor() =>
        _get_encounter_anchor_at(_selected_coord);

    public GDictionary GetSelectedWorldEvent()
    {
        var worldEvent = _get_world_event_at(_selected_coord);
        return worldEvent.Count > 0 ? worldEvent.Duplicate(true) : new GDictionary();
    }

    public GArray GetNearbyEncounterEntries() => GetNearbyEncounterEntries(8);

    public GArray GetNearbyEncounterEntries(int limit)
    {
        var entries = new GArray();
        int maxEntries = Math.Max(limit, 0);
        if (maxEntries <= 0)
            return entries;
        foreach (EncounterAnchorData encounter in _world_map_data_context.GetActiveEncounterAnchors(includeCleared: false))
        {
            var delta = encounter.world_coord - _player_coord;
            entries.Add(
                new GDictionary
                {
                    ["entity_id"] = encounter.entity_id.ToString(),
                    ["display_name"] = encounter.display_name,
                    ["coord"] = CoordDict(encounter.world_coord),
                    ["distance"] = Math.Abs(delta.X) + Math.Abs(delta.Y),
                    ["encounter_kind"] = encounter.encounter_kind.ToString(),
                    ["growth_stage"] = encounter.growth_stage,
                }
            );
        }
        SortDictionaryArray(entries, "distance", "entity_id");
        ResizeArray(entries, maxEntries);
        return entries;
    }

    public GArray GetNearbyWorldEventEntries() => GetNearbyWorldEventEntries(8);

    public GArray GetNearbyWorldEventEntries(int limit)
    {
        var entries = new GArray();
        int maxEntries = Math.Max(limit, 0);
        if (maxEntries <= 0)
            return entries;
        foreach (WorldMapEventData worldEvent in _world_map_data_context.GetDiscoveredWorldEvents())
        {
            var eventCoord = worldEvent.WorldCoord;
            var delta = eventCoord - _player_coord;
            entries.Add(
                new GDictionary
                {
                    ["event_id"] = worldEvent.EventId.ToString(),
                    ["display_name"] = worldEvent.DisplayName,
                    ["coord"] = CoordDict(eventCoord),
                    ["distance"] = Math.Abs(delta.X) + Math.Abs(delta.Y),
                    ["event_type"] = worldEvent.EventType.ToString(),
                    ["target_submap_id"] = worldEvent.TargetSubmapId.ToString(),
                }
            );
        }
        SortDictionaryArray(entries, "distance", "event_id");
        ResizeArray(entries, maxEntries);
        return entries;
    }

    private IReadOnlyList<WorldEncounterViewModel> BuildNearbyEncounterViewModels(int limit)
    {
        int maxEntries = Math.Max(limit, 0);
        if (maxEntries <= 0)
            return Array.Empty<WorldEncounterViewModel>();
        var entries = new List<WorldEncounterViewModel>();
        foreach (EncounterAnchorData encounter in _world_map_data_context.GetActiveEncounterAnchors(includeCleared: false))
        {
            var delta = encounter.world_coord - _player_coord;
            entries.Add(
                new WorldEncounterViewModel(
                    encounter.entity_id.ToString(),
                    encounter.display_name,
                    encounter.world_coord,
                    Math.Abs(delta.X) + Math.Abs(delta.Y),
                    encounter.encounter_kind.ToString(),
                    encounter.growth_stage
                )
            );
        }
        entries.Sort(
            (left, right) =>
            {
                int distanceCompare = left.Distance.CompareTo(right.Distance);
                return distanceCompare != 0
                    ? distanceCompare
                    : string.CompareOrdinal(left.EntityId, right.EntityId);
            }
        );
        if (entries.Count > maxEntries)
            entries.RemoveRange(maxEntries, entries.Count - maxEntries);
        return entries;
    }

    private IReadOnlyList<WorldEventViewModel> BuildNearbyWorldEventViewModels(int limit)
    {
        int maxEntries = Math.Max(limit, 0);
        if (maxEntries <= 0)
            return Array.Empty<WorldEventViewModel>();
        var entries = new List<WorldEventViewModel>();
        foreach (WorldMapEventData worldEvent in _world_map_data_context.GetDiscoveredWorldEvents())
        {
            var eventCoord = worldEvent.WorldCoord;
            var delta = eventCoord - _player_coord;
            entries.Add(
                new WorldEventViewModel(
                    worldEvent.EventId.ToString(),
                    worldEvent.DisplayName,
                    eventCoord,
                    Math.Abs(delta.X) + Math.Abs(delta.Y),
                    worldEvent.EventType.ToString(),
                    worldEvent.TargetSubmapId.ToString()
                )
            );
        }
        entries.Sort(
            (left, right) =>
            {
                int distanceCompare = left.Distance.CompareTo(right.Distance);
                return distanceCompare != 0
                    ? distanceCompare
                    : string.CompareOrdinal(left.EventId, right.EventId);
            }
        );
        if (entries.Count > maxEntries)
            entries.RemoveRange(maxEntries, entries.Count - maxEntries);
        return entries;
    }

    public string GetResolvedSettlementId() => ResolveCommandSettlementId();

    internal WorldMapGridSystem GetGridSystem() => _grid_system;

    internal WorldMapFogSystem GetFogSystem() => _fog_system;

    internal bool IsWorldCoordVisible(Vector2I coord, string faction_id = "")
    {
        string factionId = string.IsNullOrWhiteSpace(faction_id)
            ? _player_faction_id
            : faction_id.StripEdges();
        return _fog_system.IsVisible(coord, factionId);
    }

    public GDictionary GetWorldData() => _world_map_data_context.GetActiveWorldData();

    internal WorldMapGenerationConfig GetGenerationConfig() =>
        _world_map_data_context.GetActiveGenerationConfig();

    public Vector2I GetPlayerCoord() => _player_coord;

    public bool IsPlayerVisibleOnWorldMap() => !_is_settlement_entry_hidden_on_world_map();

    public Vector2I GetSelectedCoord() => _selected_coord;

    internal string GetPlayerFactionId() => _player_faction_id;

    internal WorldRuntimeViewModel GetWorldRuntimeViewModel(int nearbyLimit = 8) =>
        new()
        {
            StatusText = GetStatusText(),
            ActiveModalKind = GetActiveModalKind(),
            ActiveModalId = GetActiveModalId(),
            PlayerCoord = GetPlayerCoord(),
            SelectedCoord = GetSelectedCoord(),
            PlayerVisible = IsPlayerVisibleOnWorldMap(),
            PlayerFactionId = GetPlayerFactionId(),
            ActiveMapId = GetActiveMapId(),
            ActiveMapDisplayName = GetActiveMapDisplayName(),
            SubmapReturnHintText = GetSubmapReturnHintText(),
            NearbyEncounters = BuildNearbyEncounterViewModels(nearbyLimit),
            NearbyWorldEvents = BuildNearbyWorldEventViewModels(nearbyLimit),
        };

    public PartyState GetPartyState() => _party_state;

    public GArray GetActiveQuestStates() =>
        _character_management != null
            ? UntypedQuestArray(_character_management.GetActiveQuestStates())
            : new GArray();

    public GArray GetClaimableQuestStates() =>
        _character_management != null
            ? UntypedQuestArray(_character_management.GetClaimableQuestStates())
            : new GArray();

    public GStringNameArray GetClaimableQuestIds() =>
        _character_management != null
            ? _character_management.GetClaimableQuestIds()
            : new GStringNameArray();

    public GStringNameArray GetCompletedQuestIds() =>
        _character_management != null
            ? _character_management.GetCompletedQuestIds()
            : new GStringNameArray();

    public GDictionary GetMemberAchievementSummary(StringName member_id) =>
        _character_management != null
            ? _character_management.GetMemberAchievementSummary(member_id)
            : new GDictionary();

    public AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id) =>
        _character_management != null
            ? _character_management.GetMemberAttributeSnapshot(member_id)
            : null;

    public GArray GetMemberEquippedEntries(StringName member_id) =>
        _party_equipment_service != null
            ? ProjectEquipmentEntries(_party_equipment_service.GetEquippedEntriesTyped(member_id))
            : new GArray();

    public string GetMemberDisplayName(StringName member_id) =>
        GetMemberDisplayNameInternal(member_id);

    public StringName GetPartySelectedMemberId() => _party_selected_member_id;

    internal void SetPartySelectedMemberId(StringName member_id) =>
        _party_selected_member_id = member_id;

    public GDictionary GetSettlementWindowData() => GetSettlementWindowData("");

    public GDictionary GetSettlementWindowData(string settlement_id) =>
        _settlement_command_handler.GetSettlementWindowData(settlement_id);

    public string GetSettlementFeedbackText() => _active_settlement_feedback_text;

    internal void SetActiveSettlementId(string settlement_id) =>
        _active_settlement_id = settlement_id;

    internal void SetSettlementFeedbackText(string feedback_text) =>
        _active_settlement_feedback_text = feedback_text;

    internal GDictionary GetSettlementRecord(string settlement_id) =>
        _world_map_data_context.GetSettlementRecord(settlement_id);

    internal GArray GetAllSettlementRecords() =>
        UntypedDictionaryArray(_world_map_data_context.GetAllSettlementRecords());

    public GDictionary GetCharacterInfoContext() =>
        _active_character_info_context.ProjectPayload();

    public string GetActiveWarehouseEntryLabel() => _active_warehouse_entry_label;

    internal void SetActiveWarehouseEntryLabel(string entry_label) =>
        _active_warehouse_entry_label = entry_label;

    public GDictionary GetShopWindowData() => _settlement_command_handler.GetShopWindowData();

    public GDictionary GetContractBoardWindowData() =>
        _settlement_command_handler.GetContractBoardWindowData();

    public GDictionary GetForgeWindowData() =>
        _settlement_command_handler.GetForgeWindowData();

    internal void SetActiveContractBoardContext(GDictionary context) =>
        _active_contract_board_context.ReplaceWithPayload(context);

    internal void SetActiveShopContext(GDictionary context) =>
        _active_shop_context.ReplaceWithPayload(context);

    internal void SetActiveForgeContext(GDictionary context) =>
        _active_forge_context.ReplaceWithPayload(context);

    internal void ClearActiveContractBoardContext() => _active_contract_board_context.Clear();

    internal void ClearActiveShopContext() => _active_shop_context.Clear();

    internal void ClearActiveForgeContext() => _active_forge_context.Clear();

    public GDictionary GetActiveContractBoardContext() =>
        _active_contract_board_context.ProjectPayload();

    public GDictionary GetActiveShopContext() => _active_shop_context.ProjectPayload();

    public GDictionary GetActiveForgeContext() => _active_forge_context.ProjectPayload();

    public GDictionary GetStagecoachWindowData() =>
        _settlement_command_handler.GetStagecoachWindowData();

    internal void SetActiveStagecoachContext(GDictionary context) =>
        _active_stagecoach_context.ReplaceWithPayload(context);

    internal void ClearActiveStagecoachContext() => _active_stagecoach_context.Clear();

    public GDictionary GetActiveStagecoachContext() =>
        _active_stagecoach_context.ProjectPayload();

    public GDictionary GetWarehouseWindowData() =>
        _party_state != null ? _warehouse_handler.GetWarehouseWindowData() : new GDictionary();

    public BattleState GetBattleState() => _battle_state;

    public BattleRuntimeModule GetBattleRuntime() => _battle_runtime;

    internal BattleGridService GetBattleGridService() => _battle_grid_service;

    internal GameRuntimeBattleSelection GetBattleSelection() => _battle_selection;

    public GameSession GetGameSession() => _game_session;

    internal GameRoot GetGameRootTyped()
    {
        if (_game_root != null)
            return _game_root;
        _game_root = _game_session?.GetGameRootTyped();
        return _game_root;
    }

    internal GameContentCatalog GetContentCatalogTyped()
    {
        // 仅当缓存的 catalog 仍挂在有效 root 上且绑定的正是当前 _game_session 时才复用；
        // 否则从当前 session 的 root 重新解析，避免返回已失效（root 被销毁 / catalog 解绑）
        // 或绑定了其他 session 的旧实例。只检查 HasSessionTyped() 不够：绑定别的 session 的
        // catalog 仍“有效”，但并不是本 facade 当前 session 的 catalog。
        if (
            _content_catalog != null
            && _game_root != null
            && _content_catalog.IsBoundToSession(_game_session)
        )
        {
            return _content_catalog;
        }

        _game_root = _game_session?.GetGameRootTyped();
        _content_catalog = _game_root?.GetContentCatalogTyped();
        return _content_catalog;
    }

    internal void SetContentCatalogState(GameContentCatalog contentCatalog) =>
        _content_catalog = contentCatalog;

    internal CharacterManagementModule GetCharacterManagement() => _character_management;

    internal PartyWarehouseService GetPartyWarehouseService() => _party_warehouse_service;

    internal PartyItemUseService GetPartyItemUseService() => _party_item_use_service;

    internal PartyEquipmentService GetPartyEquipmentService() => _party_equipment_service;

    public ContingencySetupMutationResult GetLastContingencyCommandResultTyped() =>
        _last_contingency_command_result;

    internal ContingencySetupMutationResult CommandContingencyStatusTyped(StringName member_id)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(member_id);
        _last_contingency_command_result = BuildContingencyResultSnapshot(
            ContingencySetupMutationResult.Success(
                normalizedMemberId,
                "",
                false,
                0,
                GetContingencyEffectiveMpMax(normalizedMemberId)
            )
        );
        return _last_contingency_command_result;
    }

    internal ContingencySetupMutationResult SaveContingencySetupTemplateRuntimeTyped(
        StringName member_id,
        StringName setup_payload_name
    ) =>
        ExecuteContingencyTemplateSaveRuntimeMutation(
            member_id,
            setup_payload_name,
            "contingency_setup_save"
        );

    internal ContingencySetupMutationResult EditContingencySetupTemplateRuntimeTyped(
        StringName member_id,
        StringName setup_payload_name
    ) =>
        ExecuteContingencyTemplateSaveRuntimeMutation(
            member_id,
            setup_payload_name,
            "contingency_setup_edit"
        );

    internal ContingencySetupMutationResult ChargeContingencySetupRuntimeTyped(
        StringName member_id,
        StringName setup_id
    ) =>
        RecordContingencyResult(
            ExecuteContingencySetupRuntimeMutation(
            member_id,
            setup_id,
            "contingency_setup_charge",
                () =>
                    _character_management.ChargeContingencySetup(
                        member_id,
                        setup_id,
                        IsContingencyMutationBlocked
                    )
            )
        );

    internal ContingencySetupMutationResult ClearContingencyChargeRuntimeTyped(
        StringName member_id,
        StringName setup_id
    ) =>
        RecordContingencyResult(
            ExecuteContingencySetupRuntimeMutation(
            member_id,
            setup_id,
            "contingency_setup_clear",
                () =>
                    _character_management.ClearContingencyCharge(
                        member_id,
                        setup_id,
                        IsContingencyMutationBlocked
                    )
            )
        );

    private ContingencySetupMutationResult ExecuteContingencyTemplateSaveRuntimeMutation(
        StringName memberId,
        StringName setupPayloadName,
        StringName commitReason
    )
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        StringName normalizedPayloadName = ProgressionDataUtils.to_string_name(setupPayloadName);
        if (IsContingencyMutationBlocked())
            return RecordContingencyResult(
                ContingencySetupMutationResult.Failure(
                    "battle_mutation_blocked",
                    normalizedMemberId,
                    ResolveContingencyTemplateSetupId(normalizedPayloadName)
                )
            );

        ContingencyMatrixSetupState setup = BuildContingencySetupTemplate(
            normalizedMemberId,
            normalizedPayloadName,
            out string errorCode
        );
        if (setup == null)
            return RecordContingencyResult(
                ContingencySetupMutationResult.Failure(
                    errorCode,
                    normalizedMemberId,
                    ResolveContingencyTemplateSetupId(normalizedPayloadName)
                )
            );

        return RecordContingencyResult(
            ExecuteContingencySetupRuntimeMutation(
                normalizedMemberId,
                setup.SetupId,
                commitReason,
                () =>
                    _character_management.SaveContingencySetup(
                        normalizedMemberId,
                        setup,
                        IsContingencyMutationBlocked
                    )
            )
        );
    }

    private ContingencySetupMutationResult ExecuteContingencySetupRuntimeMutation(
        StringName memberId,
        StringName setupId,
        StringName commitReason,
        Func<ContingencySetupMutationResult> mutate
    )
    {
        if (_character_management == null || mutate == null)
            return ContingencySetupMutationResult.Failure(
                "runtime_unavailable",
                memberId,
                setupId
            );

        RuntimeTransaction transaction = new RuntimeTransaction().MarkPartyChanged();
        RuntimeTransactionRollbackState rollbackState = RuntimeTransactionRollbackState.Capture(this);
        ContingencySetupMutationResult mutationResult = mutate.Invoke();
        if (mutationResult == null)
            return ContingencySetupMutationResult.Failure("mutation_failed", memberId, setupId);
        if (!mutationResult.Ok)
            return mutationResult;

        _party_state = _character_management.GetPartyState();
        RuntimeCommitResult commitResult = CommitRuntimeTransaction(transaction, commitReason);
        if (commitResult.Ok)
            return mutationResult;

        transaction.Rollback(this, rollbackState);
        SyncPartyStateServices();
        return ContingencySetupMutationResult.Failure(
            "persistence_failure",
            mutationResult.MemberId,
            mutationResult.SetupId
        );
    }

    private ContingencySetupMutationResult RecordContingencyResult(
        ContingencySetupMutationResult result
    )
    {
        _last_contingency_command_result = BuildContingencyResultSnapshot(result);
        return _last_contingency_command_result;
    }

    private ContingencySetupMutationResult BuildContingencyResultSnapshot(
        ContingencySetupMutationResult result
    )
    {
        if (result == null)
            return ContingencySetupMutationResult.Failure("mutation_failed", "", "");

        StringName memberId = ProgressionDataUtils.to_string_name(result.MemberId);
        StringName setupId = ProgressionDataUtils.to_string_name(result.SetupId);
        bool charged = result.Charged;
        int reservedMpMax = result.ReservedMpMax;
        int effectiveMpMax = result.EffectiveMpMax;
        IReadOnlyList<ContingencyMaterialCostState> materialCosts = result.MaterialCosts;

        if (
            setupId != ""
            && _party_state?.GetMemberState(memberId) is PartyMemberState member
            && member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup)
            && setup != null
        )
        {
            charged = setup.Charged;
            reservedMpMax = setup.ReservedMpMax;
            materialCosts = setup.MaterialCosts;
            effectiveMpMax = GetContingencyEffectiveMpMax(memberId);
        }

        if (result.Ok)
            return ContingencySetupMutationResult.Success(
                memberId,
                setupId,
                charged,
                reservedMpMax,
                effectiveMpMax,
                materialCosts
            );

        return new ContingencySetupMutationResult
        {
            Ok = false,
            ErrorCode = result.ErrorCode,
            MemberId = memberId,
            SetupId = setupId,
            Charged = charged,
            ReservedMpMax = reservedMpMax,
            EffectiveMpMax = effectiveMpMax,
            MaterialCosts = materialCosts ?? Array.Empty<ContingencyMaterialCostState>(),
        };
    }

    private int GetContingencyEffectiveMpMax(StringName memberId)
    {
        if (_character_management == null)
            return 0;
        AttributeSnapshot snapshot = _character_management.GetMemberAttributeSnapshot(memberId);
        return Mathf.Max(snapshot?.GetValue(AttributeService.MP_MAX) ?? 0, 0);
    }

    private bool IsContingencyMutationBlocked() =>
        IsBattleActive() || (_game_session?.IsBattleSaveLocked() ?? false);

    private ContingencyMatrixSetupState BuildContingencySetupTemplate(
        StringName memberId,
        StringName payloadName,
        out string errorCode
    )
    {
        errorCode = "";
        if (payloadName != "hp_mirror_self")
        {
            errorCode = "unknown_setup_payload";
            return null;
        }

        PartyMemberState member = _party_state?.GetMemberState(memberId);
        UnitProgress progress = member?.progression;
        if (
            !TryGetLearnedSkillLevel(progress, "mage_chain_contingency", out int chainLevel)
            || !TryGetLearnedSkillLevel(progress, "mage_mirror_image", out int mirrorLevel)
        )
        {
            errorCode = "missing_required_skill";
            return null;
        }

        GDictionary payload = new()
        {
            ["setup_id"] = "hp_mirror_self",
            ["display_name"] = "濒死镜影",
            ["enabled"] = true,
            ["charged"] = false,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = Mathf.Max(chainLevel, 1),
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = 0,
            ["material_costs"] = new GArray(),
            ["trigger"] = new GDictionary
            {
                ["type"] = "hp_below_percent",
                ["subject"] = "owner",
                ["percent"] = 30,
                ["crossing_only"] = true,
                ["timing"] = "after_hp_changed",
            },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray
            {
                new GDictionary
                {
                    ["stored_skill_id"] = "mage_mirror_image",
                    ["cast_level"] = Mathf.Max(Mathf.Min(mirrorLevel, 2), 1),
                    ["order"] = 1,
                    ["target_resolver"] = new GDictionary { ["type"] = "self" },
                    ["parameter_bindings"] = new GDictionary(),
                    ["fallback_policy"] = "skip_if_invalid",
                },
            },
        };
        ContingencyMatrixSetupState setup = ContingencyMatrixSetupState.FromDictionary(payload);
        if (setup == null)
            errorCode = "invalid_setup";
        return setup;
    }

    private static StringName ResolveContingencyTemplateSetupId(StringName payloadName) =>
        payloadName == "hp_mirror_self" ? new StringName("hp_mirror_self") : new StringName("");

    private static bool TryGetLearnedSkillLevel(
        UnitProgress progress,
        StringName skillId,
        out int level
    )
    {
        level = 0;
        UnitSkillProgress skillProgress = progress?.GetSkillProgress(skillId);
        if (skillProgress == null || !skillProgress.is_learned)
            return false;
        level = Mathf.Max(skillProgress.skill_level, 1);
        return true;
    }

    public StringName GetActiveBattleEncounterId() => _active_battle_encounter_id;

    public string GetActiveBattleEncounterName() => _active_battle_encounter_name;

    public Vector2I GetBattleSelectedCoord() => _battle_selected_coord;

    public string GetLastAdvanceBattleRefreshMode() =>
        BattleRefreshModes.ToPayloadValue(_last_advance_battle_refresh_mode);

    public StringName GetSelectedBattleSkillId() => _selected_battle_skill_id;

    public StringName GetSelectedBattleSkillVariantId() => _selected_battle_skill_variant_id;

    internal void SetBattleSelectionSkillId(StringName skill_id) =>
        _selected_battle_skill_id = skill_id;

    internal void SetBattleSelectionSkillVariantId(StringName variant_id) =>
        _selected_battle_skill_variant_id = variant_id;

    internal StringName GetBattleSelectionLastManualUnitId() => _last_manual_battle_unit_id;

    internal void SetBattleSelectionLastManualUnitId(StringName unit_id) =>
        _last_manual_battle_unit_id = unit_id;

    internal GVector2IArray GetBattleSelectionTargetCoordsState() =>
        _queued_battle_skill_target_coords;

    internal void SetBattleSelectionTargetCoordsState(GVector2IArray target_coords) =>
        _queued_battle_skill_target_coords = target_coords;

    internal IReadOnlyList<Vector2I> GetBattleSelectionTargetCoordsStateTyped() =>
        _battle_selection_state.queued_target_coords;

    internal void SetBattleSelectionTargetCoordsStateTyped(IEnumerable<Vector2I> targetCoords) =>
        _battle_selection_state.SetTargetCoords(targetCoords ?? Array.Empty<Vector2I>());

    internal GStringNameArray GetBattleSelectionTargetUnitIdsState() =>
        _queued_battle_skill_target_unit_ids;

    internal void SetBattleSelectionTargetUnitIdsState(GStringNameArray target_unit_ids) =>
        _queued_battle_skill_target_unit_ids = target_unit_ids;

    internal IReadOnlyList<StringName> GetBattleSelectionTargetUnitIdsStateTyped() =>
        _battle_selection_state.queued_target_unit_ids;

    internal void SetBattleSelectionTargetUnitIdsStateTyped(IEnumerable<StringName> targetUnitIds) =>
        _battle_selection_state.SetTargetUnitIds(targetUnitIds ?? Array.Empty<StringName>());

    internal BattleUnitState GetManualBattleUnit() => _get_manual_active_unit();

    internal BattleUnitState GetRuntimeBattleActiveUnit() => _get_runtime_active_unit();

    internal BattleUnitState GetRuntimeBattleUnitAtCoord(Vector2I coord) =>
        _get_runtime_unit_at_coord(coord);

    internal BattleUnitState GetRuntimeBattleUnitById(StringName unit_id) =>
        _get_battle_unit_by_id(unit_id);

    internal BattlePreview PreviewBattleCommand(BattleCommand command) =>
        _battle_runtime != null
            ? _battle_runtime.PreviewCommand(command)
            : null;

    internal BattleUnitSkillTargetAffordance GetBattleUnitSkillTargetAffordance(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null,
        bool require_ap = true
    ) =>
        _battle_session_facade.GetUnitSkillTargetAffordance(
            active_unit,
            target_unit,
            skill_def,
            cast_variant,
            require_ap
        );

    internal BattleSkillCastBlockReasonKind GetBattleSkillCastBlockReasonKind(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) =>
        _battle_runtime != null
            ? _battle_runtime.GetSkillCastBlockReason(active_unit, skill_def)
            : BattleSkillCastBlockReasonKind.SkillCastCheckUnbound;

    internal string GetBattleSkillCastBlockMessage(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) =>
        _battle_runtime != null
            ? _battle_runtime.GetSkillCastBlockMessage(active_unit, skill_def)
            : "正式技能检查未绑定，无法施放该技能。";

    internal BattleRefreshMode IssueBattleCommand(BattleCommand command) =>
        _issue_battle_command(command);

    internal void RefreshBattleSelectionState() => _refresh_battle_selection_state();

    internal GDictionary BuildCommandOk() => _command_ok("", BattleRefreshMode.None);

    internal GDictionary BuildCommandOk(string message) => _command_ok(message, BattleRefreshMode.None);

    internal GDictionary BuildCommandOk(string message, BattleRefreshMode battleRefreshMode) =>
        _command_ok(message, battleRefreshMode);

    internal GDictionary BuildCommandError(string message) => _command_error(message);

    internal bool BatchHasUpdates(BattleEventBatch batch) => BatchHasUpdatesInternal(batch);

    internal bool TryOpenCharacterInfoAtBattleCoord(Vector2I coord) =>
        _try_open_character_info_at_battle_coord(coord);

    internal void UpdateStatus(string message) => UpdateStatusInternal(message);

    internal void CloseSettlementModal() =>
        _settlement_command_handler.OnSettlementWindowClosed();

    internal void CloseContractBoardModal() =>
        _settlement_command_handler.OnContractBoardWindowClosed();

    internal void CloseShopModal() => _settlement_command_handler.OnShopWindowClosed();

    internal void CloseForgeModal() => _settlement_command_handler.OnForgeWindowClosed();

    internal void CloseStagecoachModal() =>
        _settlement_command_handler.OnStagecoachWindowClosed();

    internal string FormatCoord(Vector2I coord) => FormatCoordInternal(coord);

    internal IReadOnlyDictionary<StringName, SkillDef> GetSkillDefsTyped() =>
        GetContentCatalogTyped() != null
            ? GetContentCatalogTyped().GetSkillDefsTyped()
            : new Dictionary<StringName, SkillDef>();

    internal IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() =>
        GetContentCatalogTyped() != null
            ? GetContentCatalogTyped().GetItemDefsTyped()
            : new Dictionary<StringName, ItemDef>();

    internal ISkillCatalog GetSkillCatalogTyped() =>
        GetContentCatalogTyped()?.GetSkillCatalogTyped();

    public string GetSelectedBattleSkillName() =>
        _battle_session_facade.GetSelectedBattleSkillName();

    public string GetSelectedBattleSkillVariantName() =>
        _battle_session_facade.GetSelectedBattleSkillVariantName();

    public GVector2IArray GetSelectedBattleSkillTargetCoords() =>
        _battle_session_facade.GetSelectedBattleSkillTargetCoords();

    public GStringNameArray GetSelectedBattleSkillTargetUnitIds() =>
        _battle_session_facade.GetSelectedBattleSkillTargetUnitIds();

    public GVector2IArray GetSelectedBattleSkillValidTargetCoords() =>
        _battle_session_facade.GetSelectedBattleSkillValidTargetCoords();

    public GVector2IArray GetBattleMovementReachableCoords() =>
        _battle_session_facade.GetBattleMovementReachableCoords();

    public GVector2IArray GetBattleOverlayTargetCoords() =>
        _battle_session_facade.GetBattleOverlayTargetCoords();

    public int GetSelectedBattleSkillRequiredCoordCount() =>
        _battle_session_facade.GetSelectedBattleSkillRequiredCoordCount();

    public BattlePreview GetSelectedBattleSkillPreview() =>
        _battle_session_facade.GetSelectedBattleSkillPreview();

    public BattlePreview PreviewSelectedBattleSkillAtCoord(Vector2I coord) =>
        _battle_session_facade.PreviewSelectedBattleSkillAtCoord(coord);

    public string GetBattleActiveUnitName() =>
        _battle_session_facade.GetBattleActiveUnitName();

    public GDictionary GetBattleTerrainCounts() =>
        _battle_session_facade.GetBattleTerrainCounts();

    public GDictionary GetLastBattleLootSnapshot() =>
        _last_battle_loot_snapshot.ProjectPayload();

    public PendingCharacterReward GetActiveReward() => _active_reward;

    public PendingCharacterReward GetSnapshotReward() =>
        _active_reward ?? _party_state?.GetNextPendingCharacterReward();

    public int GetPendingRewardCount() =>
        _party_state != null ? _party_state.pending_character_rewards.Count : 0;

    public GDictionary GetCurrentPromotionPrompt() =>
        _reward_flow_handler != null
            ? _reward_flow_handler.GetCurrentPromotionPrompt()
            : new GDictionary();

    internal GDictionary GetPendingPromotionPrompt() => _pending_promotion_prompt.ProjectPayload();

    internal GDictionary GetPendingWorldPromotionPromptState() =>
        _pending_world_promotion_prompt.ProjectPayload();


    public bool IsModalWindowOpen() => IsModalWindowOpenInternal();

    internal void SetRuntimeBattleState(BattleState state)
    {
        _battle_state = state;
        _battle_auto_tick_remainder_msec = 0;
    }

    internal void SetRuntimeBattleSelectedCoord(Vector2I coord) => _battle_selected_coord = coord;

    internal void SetRuntimeActiveModalKind(RuntimeModalKind modalKind) =>
        _active_modal_kind = modalKind;

    internal void SetPendingBattleStartPrompt(GDictionary prompt) =>
        _pending_battle_start_prompt.ReplaceWithPayload(prompt);

    internal void SetPendingPromotionPrompt(GDictionary prompt) =>
        _pending_promotion_prompt.ReplaceWithPayload(prompt);

    internal void ClearPendingPromotionPrompt() => _pending_promotion_prompt.Clear();

    internal void SetPendingWorldPromotionPromptState(GDictionary prompt) =>
        _pending_world_promotion_prompt.ReplaceWithPayload(prompt);

    internal void ClearPendingWorldPromotionPromptState() =>
        _pending_world_promotion_prompt.Clear();

    internal void SetActiveRewardState(PendingCharacterReward reward) => _active_reward = reward;

    internal void ClearActiveRewardState() => _active_reward = null;

    internal void SetActiveCharacterInfoContext(GDictionary context) =>
        _active_character_info_context.ReplaceWithPayload(context);

    internal void ClearActiveCharacterInfoContext() => _active_character_info_context.Clear();

    internal void ClearBattleSelectionTargets() => _battle_selection_state.ClearTargets();

    internal void ClosePartyManagementModal() =>
        _party_command_handler?.OnPartyManagementWindowClosed();

    internal void ClosePartyWarehouseModal() =>
        _warehouse_handler?.OnPartyWarehouseWindowClosed();

    internal void OpenPartyWarehouseWindow(string entry_label) =>
        _warehouse_handler?.OpenPartyWarehouseWindow(entry_label);

    internal BattleEventBatch SubmitBattlePromotionChoice(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    ) =>
        _battle_runtime != null
            ? _battle_runtime.SubmitPromotionChoice(member_id, profession_id, selection)
            : null;

    internal CharacterProgressionDelta PromoteProfession(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    ) => _character_management?.PromoteProfession(member_id, profession_id, selection);

    internal CharacterProgressionDelta ApplyPendingCharacterRewardToParty(
        PendingCharacterReward reward
    ) => _character_management?.ApplyPendingCharacterReward(reward);

    internal void EnqueueCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        if (_character_management == null)
            return;
        _character_management.EnqueuePendingCharacterRewardsTyped(rewards);
        _party_state = _character_management.GetPartyState();
    }

    internal QuestProgressApplyResultData ApplyQuestProgressEventsToPartyTyped(
        GArray event_options,
        string source_domain = "quest"
    ) =>
        ApplyQuestProgressEventsToPartyTyped(
            QuestProgressService.ReadEventOptions(event_options),
            source_domain
        );

    internal QuestProgressApplyResultData ApplyQuestProgressEventsToPartyTyped(
        IEnumerable<QuestProgressService.QuestProgressEventData> event_options,
        string source_domain = "quest"
    )
    {
        if (_character_management == null)
            return new QuestProgressApplyResultData();
        var summary = _character_management.ApplyQuestProgressEventsTyped(event_options);
        _party_state = _character_management.GetPartyState();
        if (_has_quest_progress_summary_changes(summary))
        {
            _log_runtime_event(
                "info",
                source_domain,
                $"{source_domain}.quest_progress",
                _format_quest_progress_summary(summary),
                Json.Stringify(new GDictionary
                {
                    ["runtime"] = _build_runtime_log_state(),
                    ["quest_progress_summary"] = _quest_progress_summary_to_string_dict(summary),
                })
            );
        }
        return summary;
    }

    internal void SyncPartyStateFromCharacterManagement()
    {
        if (_character_management != null)
            _party_state = _character_management.GetPartyState();
    }

    internal int PersistPartyState() => PersistPartyStateInternal();

    internal bool PresentPendingRewardIfReady() =>
        _reward_flow_handler != null && _reward_flow_handler.PresentPendingRewardIfReady();

    internal void SyncCharacterManagementPartyState() =>
        _character_management?.SetPartyState(_party_state);

    internal void RecordMemberAchievementEvent(
        StringName member_id,
        StringName event_id,
        int value
    ) => RecordMemberAchievementEvent(member_id, event_id, value, "");

    internal void RecordMemberAchievementEvent(
        StringName member_id,
        StringName event_id,
        int value,
        StringName detail_id
    ) => _character_management?.RecordAchievementEvent(member_id, event_id, value, detail_id);

    internal void PrepareBattleStart(EncounterAnchorData encounter_anchor)
    {
        if (encounter_anchor == null)
            return;
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        fateRuntime?.ClearMisfortuneExaltedReadyFlags(new GArray());
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
        _battle_state = null;
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
        _pending_battle_start_prompt.ReplaceWithPayload(
            new GDictionary
            {
                ["title"] = "开始战斗",
                ["description"] = "是否开始战斗？确认后 TU 将按整数 tick 推进。",
                ["confirm_text"] = "开始战斗",
                ["cancel_visible"] = false,
                ["dismiss_on_shade"] = false,
            }
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
        var context = _pending_battle_generation_request.CloneContext();
        var runtimeState = _battle_runtime.StartBattle(encounterAnchor, seed, context);
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
                return false;
            }
        }

        _battle_runtime.EndBattle(new BattleEndOptions(commitProgression: true));
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
                return false;
            }
            var worldDataBefore = _world_map_data_context.root_world_data.Duplicate(true);
            _resolve_world_encounter_after_battle(winnerFactionId);
            worldPersistError = _game_session.SetWorldData(
                _world_map_data_context.root_world_data
            );
            if (worldPersistError != (int)Error.Ok)
            {
                _world_map_data_context.BindRootWorldData(worldDataBefore);
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
            flushError = _game_session.FlushGameState();
            if (flushError != (int)Error.Ok)
            {
                _game_session.SetBattleSaveLock(true);
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
            _last_battle_loot_snapshot.ReplaceWithPayload(
                BuildLastBattleLootSnapshotTyped(
                    battleName,
                    winnerFactionId,
                    battle_resolution_result,
                    lootCommitResult
                )
            );
        }

        _RefreshFog();
        if (mainCharacterDead)
        {
            UpdateStatusInternal(
                DictString(
                    _active_game_over_context.ProjectPayload(),
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

    internal GStringNameArray HandleFortunaChapterCompleted(GDictionary payload)
    {
        var fateRuntime = _battle_runtime?.GetFateRuntime();
        if (fateRuntime == null)
            return new GStringNameArray();
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
            _game_session.FlushGameState();
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
            return new GStringNameArray();
        var itemDefs =
            GetContentCatalogTyped() != null
                ? GetContentCatalogTyped().GetItemDefsTyped()
                : new Dictionary<StringName, ItemDef>();
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

    public bool advance(float delta)
    {
        _last_advance_battle_refresh_mode = BattleRefreshMode.None;
        if (_generation_config == null)
            return false;
        if (_try_complete_pending_battle_start())
        {
            _last_advance_battle_refresh_mode = BattleRefreshMode.Full;
            return true;
        }
        if (HasPendingBattleGenerationRequest())
            return false;
        if (IsBattleActive())
        {
            if (_is_battle_finished() || IsBattleTimelineModalActive())
                return false;
            int previousTu =
                _battle_state?.timeline != null ? _battle_state.timeline.current_tu : -1;
            int tickCount = _resolve_battle_auto_tick_count(delta);
            var batch = _battle_runtime.advance(tickCount);
            if (BatchHasUpdatesInternal(batch))
            {
                ApplyBattleBatch(batch);
                _last_advance_battle_refresh_mode = BatchRequiresFullBattleRefresh(batch)
                    ? BattleRefreshMode.Full
                    : BattleRefreshMode.Overlay;
                return true;
            }
            int currentTu =
                _battle_state?.timeline != null ? _battle_state.timeline.current_tu : -1;
            if (currentTu != previousTu)
            {
                _last_advance_battle_refresh_mode = BattleRefreshMode.Overlay;
                return true;
            }
            return false;
        }
        if (IsModalWindowOpenInternal())
            return false;
        return PresentPendingRewardIfReady();
    }

    private int _resolve_battle_auto_tick_count(float delta)
    {
        if (delta <= 0.0f)
            return 0;
        _battle_auto_tick_remainder_msec += Math.Max((int)Math.Round(delta * 1000.0f), 0);
        int tickCount = _battle_auto_tick_remainder_msec / BattleAutoAdvanceTickMsec;
        if (tickCount > 0)
            _battle_auto_tick_remainder_msec -= tickCount * BattleAutoAdvanceTickMsec;
        return Mathf.Min(tickCount, 1);
    }

    public GDictionary BuildHeadlessSnapshot() => _snapshot_builder.BuildHeadlessSnapshot();

    public string BuildTextSnapshot() => _snapshot_builder.BuildTextSnapshot();

    internal void AdvanceWorldTimeBySteps(int delta_steps) =>
        _AdvanceWorldTimeBySteps(delta_steps);

    internal void RefreshWorldVisibility()
    {
        _world_map_data_context.RefreshWorldEventDiscovery();
        _RefreshFog();
    }

    internal void RefreshFog() => _RefreshFog();

    internal void SetPartyState(PartyState party_state)
    {
        _party_state = party_state;
        SyncPartyStateServices();
    }

    internal int PersistWorldData() => PersistWorldDataInternal();

    internal int PersistPlayerCoord()
    {
        RuntimeCommitResult result = CommitRuntimeTransaction(
            new RuntimeTransaction().MarkPlayerCoordChanged(),
            "player_coord"
        );
        return result.FirstError();
    }

    internal void SetPlayerCoord(Vector2I coord) => _player_coord = coord;

    internal void SetSelectedCoord(Vector2I coord) => _selected_coord = coord;

    internal void SetSettlementEntryContext(Vector2I source_coord, Vector2I target_coord) =>
        _activate_settlement_entry_context(source_coord, target_coord);

    internal void ClearSettlementEntryContext() => _ClearSettlementEntryContext(true);

    internal void ClearSettlementEntryContext(bool reset_selected) =>
        _ClearSettlementEntryContext(reset_selected);

    internal bool SetActiveSettlementState(string settlement_id, GDictionary settlement_state) =>
        _world_map_data_context.SetActiveSettlementState(settlement_id, settlement_state);

    internal GDictionary GetSettlementState(string settlement_id) =>
        _world_map_data_context.GetSettlementState(settlement_id);

    internal WorldMapSettlementStateData GetSettlementStateData(string settlement_id) =>
        _world_map_data_context.GetSettlementStateData(settlement_id);

    internal bool IsSettlementVisited(string settlement_id) =>
        _world_map_data_context.IsSettlementVisited(settlement_id);

    internal bool MarkSettlementVisited(string settlement_id) =>
        _world_map_data_context.MarkSettlementVisited(settlement_id);

    internal RuntimeCommandResult CommandWorldMoveTyped(Vector2I direction, int count) =>
        ExecuteLoggedCommandTyped(
            "world.move",
            "world",
            new GDictionary { ["direction"] = direction, ["count"] = count },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能执行大地图移动。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能执行大地图移动。");
                if (direction == Vector2I.Zero)
                    return BuildCommandErrorResult("移动方向不能为空。");
                int moveCount = Math.Min(Math.Max(count, 1), MaxCommandWorldMoveCount);
                for (int i = 0; i < moveCount; i++)
                {
                    _move_player(direction);
                    if (IsBattleActive() || IsModalWindowOpenInternal())
                        break;
                }
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandWorldSelectTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.select",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能选择大地图坐标。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能切换大地图选择。");
                if (!_grid_system.IsCellWalkable(coord))
                    return BuildCommandErrorResult("该大地图格超出当前世界范围。");
                _selected_coord = coord;
                UpdateStatusInternal($"已选中格子 {FormatCoordInternal(coord)}。");
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandOpenSettlementTyped() =>
        CommandOpenSettlementTyped(new Vector2I(-1, -1));

    internal RuntimeCommandResult CommandOpenSettlementTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "settlement.open",
            "settlement",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能打开据点。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能打开新的据点窗口。");
                var targetCoord = coord == new Vector2I(-1, -1) ? _selected_coord : coord;
                if (_try_open_settlement_at(targetCoord))
                    return BuildCommandOkResult();
                return BuildCommandErrorResult(
                    string.IsNullOrEmpty(_current_status_message)
                        ? "据点打开失败。"
                        : _current_status_message
                );
            }
        );

    internal RuntimeCommandResult CommandWorldInspectTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.inspect",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return BuildCommandErrorResult("世界地图尚未初始化。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能查看大地图人物。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能查看大地图人物。");
                if (!_fog_system.IsVisible(coord, _player_faction_id))
                {
                    UpdateStatusInternal("该格当前不在视野中。");
                    return BuildCommandErrorResult(_current_status_message);
                }
                if (_try_open_character_info_at_world_coord(coord))
                    return BuildCommandOkResult();
                UpdateStatusInternal("当前格没有可查看人物。");
                return BuildCommandErrorResult(_current_status_message);
            }
        );

    internal RuntimeCommandResult CommandOpenPartyTyped() =>
        ExecuteLoggedCommandTyped(
            "party.open",
            "party",
            new GDictionary(),
            () => _party_command_handler.CommandOpenPartyTyped()
        );

    internal RuntimeCommandResult CommandAcceptQuestTyped(
        StringName quest_id,
        bool allow_reaccept
    ) =>
        ExecuteLoggedCommandTyped(
            "quest.accept",
            "quest",
            new GDictionary { ["quest_id"] = quest_id, ["allow_reaccept"] = allow_reaccept },
            () => _quest_command_handler.CommandAcceptQuestTyped(quest_id, allow_reaccept)
        );

    internal RuntimeCommandResult CommandProgressQuestTyped(
        StringName quest_id,
        StringName objective_id,
        int progress_delta,
        QuestProgressCommandPayloadData payload
    ) =>
        ExecuteLoggedCommandTyped(
            "quest.progress",
            "quest",
            new GDictionary
            {
                ["quest_id"] = quest_id,
                ["objective_id"] = objective_id,
                ["progress_delta"] = progress_delta,
                ["payload"] = BuildQuestProgressPayloadContext(payload),
            },
            () =>
                _quest_command_handler.CommandProgressQuestTyped(
                    quest_id,
                    objective_id,
                    progress_delta,
                    payload
                )
        );

    private static GDictionary BuildQuestProgressPayloadContext(
        QuestProgressCommandPayloadData payload
    )
    {
        if (payload == null)
            return new GDictionary();

        GDictionary result = new()
        {
            ["world_step"] = payload.WorldStep,
            ["action_id"] = payload.ActionId,
            ["member_id"] = payload.MemberId,
            ["enemy_template_id"] = payload.EnemyTemplateId,
            ["settlement_id"] = payload.SettlementId,
            ["source_type"] = payload.SourceType,
            ["source_id"] = payload.SourceId,
        };

        if (payload.HasTargetValue)
            result["target_value"] = payload.TargetValue;

        return result;
    }

    internal RuntimeCommandResult CommandCompleteQuestTyped(StringName quest_id) =>
        ExecuteLoggedCommandTyped(
            "quest.complete",
            "quest",
            new GDictionary { ["quest_id"] = quest_id },
            () => _quest_command_handler.CommandCompleteQuestTyped(quest_id)
        );

    internal RuntimeCommandResult CommandSubmitQuestItemTyped(
        StringName quest_id,
        StringName objective_id
    ) =>
        ExecuteLoggedCommandTyped(
            "quest.submit_item",
            "quest",
            new GDictionary { ["quest_id"] = quest_id, ["objective_id"] = objective_id },
            () => _quest_command_handler.CommandSubmitQuestItemTyped(quest_id, objective_id)
        );

    internal RuntimeCommandResult CommandClaimQuestTyped(StringName quest_id) =>
        ExecuteLoggedCommandTyped(
            "quest.claim",
            "quest",
            new GDictionary { ["quest_id"] = quest_id },
            () => _quest_command_handler.CommandClaimQuestTyped(quest_id)
        );

    internal RuntimeCommandResult CommandSelectPartyMemberTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.select_member",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandSelectPartyMemberTyped(member_id)
        );

    internal RuntimeCommandResult CommandSetPartyLeaderTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.set_leader",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandSetPartyLeaderTyped(member_id)
        );

    internal RuntimeCommandResult CommandMoveMemberToActiveTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.move_member_to_active",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandMoveMemberToActiveTyped(member_id)
        );

    internal RuntimeCommandResult CommandMoveMemberToReserveTyped(StringName member_id) =>
        ExecuteLoggedCommandTyped(
            "party.move_member_to_reserve",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.CommandMoveMemberToReserveTyped(member_id)
        );

    internal RuntimeCommandResult CommandPartyEquipItemTyped(
        StringName member_id,
        StringName item_id,
        StringName slot_id,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "party.equip_item",
            "party",
            new GDictionary
            {
                ["member_id"] = member_id,
                ["item_id"] = item_id,
                ["slot_id"] = slot_id,
                ["instance_id"] = instance_id,
            },
            () =>
                _party_command_handler.CommandPartyEquipItemTyped(
                    member_id,
                    item_id,
                    slot_id,
                    instance_id
                )
        );

    internal RuntimeCommandResult CommandPartyUnequipItemTyped(
        StringName member_id,
        StringName slot_id
    ) =>
        ExecuteLoggedCommandTyped(
            "party.unequip_item",
            "party",
            new GDictionary { ["member_id"] = member_id, ["slot_id"] = slot_id },
            () => _party_command_handler.CommandPartyUnequipItemTyped(member_id, slot_id)
        );

    internal RuntimeCommandResult CommandOpenPartyWarehouseTyped() =>
        ExecuteLoggedCommandTyped(
            "warehouse.open",
            "warehouse",
            new GDictionary(),
            () => _warehouse_handler.CommandOpenPartyWarehouseTyped()
        );

    internal RuntimeCommandResult CommandWarehouseDiscardOneTyped(
        StringName item_id,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.discard_one",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["instance_id"] = instance_id },
            () => _warehouse_handler.CommandDiscardOneTyped(item_id, instance_id)
        );

    internal RuntimeCommandResult CommandWarehouseDiscardAllTyped(
        StringName item_id,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.discard_all",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["instance_id"] = instance_id },
            () => _warehouse_handler.CommandDiscardAllTyped(item_id, instance_id)
        );

    internal RuntimeCommandResult CommandWarehouseUseItemTyped(
        StringName item_id,
        StringName member_id,
        PartyItemUseService.PartyItemUseOptions options
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.use_item",
            "warehouse",
            new GDictionary
            {
                ["item_id"] = item_id,
                ["member_id"] = member_id,
                ["confirm_practice_replacement"] =
                    options?.ConfirmPracticeReplacement ?? false,
            },
            () => _warehouse_handler.CommandUseItemTyped(item_id, member_id, options)
        );

    internal RuntimeCommandResult CommandWarehouseAddItemTyped(
        StringName item_id,
        int quantity
    ) =>
        ExecuteLoggedCommandTyped(
            "warehouse.add_item",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["quantity"] = quantity },
            () => _warehouse_handler.CommandAddItemTyped(item_id, quantity)
        );

    internal RuntimeCommandResult CommandExecuteSettlementActionTyped(
        SettlementActionRequest request
    ) =>
        ExecuteLoggedCommandTyped(
            "settlement.execute_action",
            "settlement",
            new GDictionary
            {
                ["settlement_id"] = request.SettlementId.ToString(),
                ["service_id"] = request.ServiceId.ToString(),
                ["action_id"] = request.ActionId.ToString(),
                ["member_id"] = request.MemberId.ToString(),
                ["quantity"] = request.Quantity,
                ["submission_source"] = SettlementSubmissionSources.ToPayloadValue(request.Source),
            },
            () =>
                _settlement_command_handler.CommandExecuteSettlementActionRuntimeTyped(
                    request
                )
        );

    internal RuntimeCommandResult CommandExecuteSettlementActionTyped(
        string action_id,
        GDictionary payload
    ) =>
        ExecuteLoggedCommandTyped(
            "settlement.execute_action",
            "settlement",
            new GDictionary
            {
                ["action_id"] = action_id,
                ["payload"] = payload ?? new GDictionary(),
            },
            () =>
                _settlement_command_handler.CommandExecuteSettlementActionRuntimeTyped(
                    action_id,
                    payload ?? new GDictionary()
                )
        );

    internal RuntimeCommandResult CommandShopBuyTyped(StringName item_id, int quantity) =>
        ExecuteLoggedCommandTyped(
            "shop.buy",
            "shop",
            new GDictionary { ["item_id"] = item_id, ["quantity"] = quantity },
            () => _settlement_command_handler.CommandShopBuyTyped(item_id, quantity)
        );

    internal RuntimeCommandResult CommandShopSellTyped(
        StringName item_id,
        int quantity,
        StringName instance_id
    ) =>
        ExecuteLoggedCommandTyped(
            "shop.sell",
            "shop",
            new GDictionary
            {
                ["item_id"] = item_id,
                ["quantity"] = quantity,
                ["instance_id"] = instance_id,
            },
            () =>
                _settlement_command_handler.CommandShopSellTyped(
                    item_id,
                    quantity,
                    instance_id
                )
        );

    internal RuntimeCommandResult CommandStagecoachTravelTyped(string settlement_id) =>
        ExecuteLoggedCommandTyped(
            "stagecoach.travel",
            "stagecoach",
            new GDictionary { ["settlement_id"] = settlement_id },
            () => _settlement_command_handler.CommandStagecoachTravelTyped(settlement_id)
        );

    internal RuntimeCommandResult CommandBattleTickTyped(int tick_count) =>
        ExecuteLoggedCommandTyped(
            "battle.tick",
            "battle",
            new GDictionary { ["tick_count"] = tick_count },
            () => _battle_session_facade.CommandBattleTickTyped(tick_count)
        );

    internal RuntimeCommandResult CommandBattleSelectSkillTyped(int slot_index) =>
        ExecuteLoggedCommandTyped(
            "battle.select_skill",
            "battle",
            new GDictionary { ["slot_index"] = slot_index },
            () => _battle_session_facade.CommandBattleSelectSkillTyped(slot_index)
        );

    internal RuntimeCommandResult CommandBattleCycleVariantTyped(int step) =>
        ExecuteLoggedCommandTyped(
            "battle.cycle_variant",
            "battle",
            new GDictionary { ["step"] = step },
            () => _battle_session_facade.CommandBattleCycleVariantTyped(step)
        );

    internal RuntimeCommandResult CommandBattleClearSkillTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.clear_skill",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.CommandBattleClearSkillTyped()
        );

    internal RuntimeCommandResult CommandBattleMoveToTyped(Vector2I target_coord) =>
        ExecuteLoggedCommandTyped(
            "battle.move_to",
            "battle",
            new GDictionary { ["target_coord"] = target_coord },
            () => _battle_session_facade.CommandBattleMoveToTyped(target_coord)
        );

    internal RuntimeCommandResult CommandBattleMoveDirectionTyped(Vector2I direction) =>
        ExecuteLoggedCommandTyped(
            "battle.move_direction",
            "battle",
            new GDictionary { ["direction"] = direction },
            () => _battle_session_facade.CommandBattleMoveDirectionTyped(direction)
        );

    internal RuntimeCommandResult CommandBattleWaitOrResolveTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.wait_or_resolve",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.CommandBattleWaitOrResolveTyped()
        );

    internal RuntimeCommandResult CommandBattleCancelCastTyped(StringName unit_id) =>
        ExecuteLoggedCommandTyped(
            "battle.cancel_cast",
            "battle",
            new GDictionary { ["unit_id"] = unit_id },
            () => _battle_session_facade.CommandBattleCancelCastTyped(unit_id)
        );

    internal RuntimeCommandResult CommandBattleInspectTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "battle.inspect",
            "battle",
            new GDictionary { ["coord"] = coord },
            () => _battle_session_facade.CommandBattleInspectTyped(coord)
        );

    internal RuntimeCommandResult CommandConfirmPendingRewardTyped() =>
        ExecuteLoggedCommandTyped(
            "reward.confirm_pending",
            "reward",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandConfirmPendingRewardTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandChoosePromotionTyped(StringName profession_id) =>
        ExecuteLoggedCommandTyped(
            "promotion.choose",
            "promotion",
            new GDictionary { ["profession_id"] = profession_id },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandChoosePromotionTyped(profession_id)
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandConfirmSubmapEntryTyped() =>
        ExecuteLoggedCommandTyped(
            "submap.confirm_entry",
            "submap",
            new GDictionary
            {
                ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            },
            () =>
            {
                if (_pending_submap_prompt.IsEmpty)
                    return BuildCommandErrorResult("当前没有待确认的子地图入口。");
                return ConfirmPendingSubmapEntryTyped();
            }
        );

    internal RuntimeCommandResult CommandCancelSubmapEntryTyped() =>
        ExecuteLoggedCommandTyped(
            "submap.cancel_entry",
            "submap",
            new GDictionary
            {
                ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            },
            () =>
            {
                if (_pending_submap_prompt.IsEmpty)
                    return BuildCommandErrorResult("当前没有待确认的子地图入口。");
                string targetName = string.IsNullOrEmpty(_pending_submap_prompt.TargetDisplayName)
                    ? "子地图"
                    : _pending_submap_prompt.TargetDisplayName;
                _pending_submap_prompt.Clear();
                _active_modal_kind = RuntimeModalKind.None;
                UpdateStatusInternal($"已取消进入 {targetName}。");
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandConfirmBattleStartTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.confirm_start",
            "battle",
            new GDictionary { ["encounter_id"] = _active_battle_encounter_id },
            () =>
            {
                if (_pending_battle_start_prompt.Count == 0)
                    return BuildCommandErrorResult("当前没有待确认的战斗开始提示。");
                if (!IsBattleActive() || _battle_state == null)
                    return BuildCommandErrorResult("当前没有待开始的战斗。");
                _pending_battle_start_prompt.Clear();
                _active_modal_kind = RuntimeModalKind.None;
                _battle_state.ModalStateKind = BattleModalStateKind.None;
                if (_battle_state.timeline != null)
                    _battle_state.timeline.frozen = false;
                UpdateStatusInternal("战斗开始，TU 现在按每秒 5 点推进。");
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandReturnFromSubmapTyped() =>
        ExecuteLoggedCommandTyped(
            "submap.return",
            "submap",
            new GDictionary { ["active_map_id"] = _world_map_data_context.active_map_id },
            () =>
            {
                if (!IsSubmapActive())
                    return BuildCommandErrorResult("当前不在子地图中。");
                if (IsBattleActive())
                    return BuildCommandErrorResult("当前处于战斗中，不能从子地图返回。");
                if (IsModalWindowOpenInternal())
                    return BuildCommandErrorResult("当前有窗口打开，不能从子地图返回。");
                return ReturnFromActiveSubmapTyped();
            }
        );

    internal RuntimeCommandResult CommandCloseActiveModalTyped() =>
        ExecuteLoggedCommandTyped(
            "modal.close_active",
            "ui",
            new GDictionary { ["modal_id"] = GetActiveModalId() },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandCloseActiveModalTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandApplyPartyRosterTyped(
        GStringNameArray active_member_ids,
        GStringNameArray reserve_member_ids
    ) =>
        ExecuteLoggedCommandTyped(
            "party.apply_roster",
            "party",
            new GDictionary
            {
                ["active_member_ids"] = active_member_ids,
                ["reserve_member_ids"] = reserve_member_ids,
            },
            () => _party_command_handler.CommandApplyPartyRosterTyped(active_member_ids, reserve_member_ids)
        );

    internal RuntimeCommandResult CommandSubmitPromotionChoiceTyped(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    ) =>
        ExecuteLoggedCommandTyped(
            "promotion.submit_choice",
            "promotion",
            new GDictionary
            {
                ["member_id"] = member_id,
                ["profession_id"] = profession_id,
                ["selection"] = selection?.ToPayloadProjection() ?? new GDictionary(),
            },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandSubmitPromotionChoiceTyped(
                        member_id,
                        profession_id,
                        selection
                    )
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandCancelPromotionChoiceTyped() =>
        ExecuteLoggedCommandTyped(
            "promotion.cancel_choice",
            "promotion",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandCancelPromotionChoiceTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult CommandConfirmActiveRewardTyped() =>
        ExecuteLoggedCommandTyped(
            "reward.confirm_active",
            "reward",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.CommandConfirmActiveRewardTyped()
                    : BuildCommandErrorResult("运行时尚未初始化。")
        );

    internal RuntimeCommandResult ResetBattleFocusTyped() =>
        ExecuteLoggedCommandTyped(
            "battle.reset_focus",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.ResetBattleFocusTyped()
        );

    internal RuntimeCommandResult SelectWorldCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.click_select",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (IsSubmapActive() && !IsBattleActive() && !IsModalWindowOpenInternal())
                    return ReturnFromActiveSubmapTyped();
                _on_world_map_cell_clicked(coord);
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult InspectWorldCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "world.click_inspect",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                _on_world_map_cell_right_clicked(coord);
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult SelectBattleCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "battle.click_select",
            "battle",
            new GDictionary { ["coord"] = coord },
            () => _battle_session_facade.CommandBattleMoveToTyped(coord)
        );

    internal RuntimeCommandResult InspectBattleCellTyped(Vector2I coord) =>
        ExecuteLoggedCommandTyped(
            "battle.click_inspect",
            "battle",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                _on_battle_cell_right_clicked(coord);
                return BuildCommandOkResult();
            }
        );

    internal RuntimeCommandResult CommandShopSell(
        StringName itemId,
        int quantity,
        StringName instanceId
    ) => CommandShopSellTyped(itemId, quantity, instanceId);

    internal RuntimeCommandResult CommandWarehouseDiscardOne(
        StringName itemId,
        StringName instanceId
    ) => CommandWarehouseDiscardOneTyped(itemId, instanceId);

    internal RuntimeCommandResult CommandWarehouseDiscardAll(
        StringName itemId,
        StringName instanceId
    ) => CommandWarehouseDiscardAllTyped(itemId, instanceId);

    private GDictionary _command_ok() => _command_ok("", BattleRefreshMode.None);

    private GDictionary _command_ok(string message) => _command_ok(message, BattleRefreshMode.None);

    private GDictionary _command_ok(string message, BattleRefreshMode battleRefreshMode) =>
        FinalizeCommandResult(BuildCommandOkResult(message, battleRefreshMode));

    private GDictionary _command_error(string message) =>
        FinalizeCommandResult(BuildCommandErrorResult(message));

    private RuntimeCommandResult BuildCommandOkResult(
        string message = "",
        BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
    )
    {
        string resolvedMessage = string.IsNullOrEmpty(message) ? _current_status_message : message;
        return RuntimeCommandResult.Success(resolvedMessage, RuntimeCommandCode.Ok, battleRefreshMode);
    }

    private RuntimeCommandResult BuildCommandErrorResult(
        string message,
        RuntimeCommandCode code = RuntimeCommandCode.Failed
    )
    {
        string resolvedMessage = message ?? "";
        if (!string.IsNullOrEmpty(resolvedMessage))
            UpdateStatusInternal(resolvedMessage);
        return RuntimeCommandResult.Failure(resolvedMessage, code);
    }

    private GDictionary FinalizeCommandResult(RuntimeCommandResult commandResult)
    {
        var result = RuntimeCommandResultProjection.Project(commandResult);
        _log_active_command_scope_result(result);
        return result;
    }

    private RuntimeCommandResult ExecuteLoggedCommandTyped(
        string event_id,
        string domain,
        GDictionary context,
        Func<RuntimeCommandResult> action
    )
    {
        _command_logger.BeginLoggedCommand(event_id, domain, context ?? new GDictionary());
        RuntimeCommandResult result = action?.Invoke() ?? RuntimeCommandResult.Failure("");
        _log_active_command_scope_result(RuntimeCommandResultProjection.Project(result));
        return result;
    }

    private GDictionary _execute_logged_command(
        string event_id,
        string domain,
        GDictionary context,
        Func<GDictionary> action
    )
    {
        _command_logger.BeginLoggedCommand(event_id, domain, context ?? new GDictionary());
        var result = action?.Invoke() ?? new GDictionary();
        return _command_logger.FinishLoggedCommand(result);
    }

    private void _log_active_command_scope_result(GDictionary result) =>
        _command_logger.LogActiveCommandScopeResult(result);

    private GDictionary _build_runtime_log_state() => _command_logger.BuildRuntimeLogState();

    internal void _log_runtime_event(string level, string domain, string event_id, string message) =>
        _command_logger.LogRuntimeEvent(level, domain, event_id, message, "");

    internal void _log_runtime_event(
        string level,
        string domain,
        string event_id,
        string message,
        string context
    ) =>
        _command_logger.LogRuntimeEvent(
            level,
            domain,
            event_id,
            message,
            context ?? ""
        );

    private void _log_battle_batch_entries(BattleEventBatch batch) =>
        _command_logger.LogBattleBatchEntries(batch);

    private GDictionary _build_battle_log_state() => _command_logger.BuildBattleLogState();

    private GDictionary _build_battle_batch_log_context(BattleEventBatch batch) =>
        _command_logger.BuildBattleBatchLogContext(batch);

    private string ResolveCommandSettlementId() =>
        _settlement_command_handler.ResolveCommandSettlementId();

    private void _move_player(Vector2I direction)
    {
        if (_game_session == null)
        {
            UpdateStatusInternal("游戏会话不可用，无法移动。");
            return;
        }
        var sourceCoord = _player_coord;
        WorldMapSettlementData previousSettlement =
            _world_map_data_context.GetSettlementAt(sourceCoord);
        var targetCoord = sourceCoord + direction;
        if (!_grid_system.IsCellWalkable(targetCoord))
        {
            UpdateStatusInternal("已到达大地图边界。");
            return;
        }

        WorldMapSettlementData targetSettlement =
            _world_map_data_context.GetSettlementAt(targetCoord);
        bool enteredNewSettlement =
            !targetSettlement.IsEmpty
            && targetSettlement.SettlementId != previousSettlement.SettlementId;
        if (enteredNewSettlement)
        {
            _selected_coord = targetCoord;
            _AdvanceWorldTimeBySteps(1);
            _activate_settlement_entry_context(sourceCoord, targetCoord);
            if (_try_open_settlement_at(targetCoord, false))
            {
                int persistError = _game_session.SetWorldData(
                    _world_map_data_context.root_world_data
                );
                if (persistError != (int)Error.Ok)
                    UpdateStatusInternal(
                        $"已打开 {targetSettlement.DisplayNameOrFallback("据点")} 的据点窗口，但世界状态持久化失败。"
                    );
                return;
            }
            _ClearSettlementEntryContext();
            if (string.IsNullOrEmpty(_current_status_message))
                UpdateStatusInternal("进入据点失败。");
            return;
        }

        _player_coord = targetCoord;
        _selected_coord = _player_coord;
        _AdvanceWorldTimeBySteps(1);
        _world_map_data_context.RefreshWorldEventDiscovery();
        _RefreshFog();

        var triggeredEvent = GetTriggerableWorldEventAt(_player_coord);
        if (triggeredEvent != null)
        {
            int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
            int worldPersistError = _game_session.SetWorldData(
                _world_map_data_context.root_world_data
            );
            OpenWorldEventPrompt(triggeredEvent);
            if (playerPersistError != (int)Error.Ok || worldPersistError != (int)Error.Ok)
                UpdateStatusInternal(
                    $"{ResolveWorldEventDisplayName(triggeredEvent, "事件入口")} 已显现，但当前位置或世界状态持久化失败。"
                );
            return;
        }

        var encounterAnchor = _get_encounter_anchor_at(_player_coord);
        if (encounterAnchor != null)
        {
            _game_session.SetBattleSaveLock(true);
            int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
            int worldPersistError = _game_session.SetWorldData(
                _world_map_data_context.root_world_data
            );
            _StartBattle(encounterAnchor);
            if (!IsBattleActive() && !HasPendingBattleGenerationRequest())
            {
                _game_session.SetBattleSaveLock(false);
                int flushError = _game_session.FlushGameState();
                UpdateStatusInternal(
                    playerPersistError != (int)Error.Ok
                    || worldPersistError != (int)Error.Ok
                    || flushError != (int)Error.Ok
                        ? "遭遇战未能开始，且玩家位置或世界时间持久化失败。"
                        : "遭遇战未能开始，已保留玩家当前位置与世界时间。"
                );
            }
            return;
        }

        int playerError = _game_session.SetPlayerCoord(_player_coord);
        int worldError = _game_session.SetWorldData(_world_map_data_context.root_world_data);
        UpdateStatusInternal(
            playerError == (int)Error.Ok && worldError == (int)Error.Ok
                ? $"玩家移动到 {FormatCoordInternal(_player_coord)}，视野与世界时间已刷新。"
                : $"玩家移动到 {FormatCoordInternal(_player_coord)}，但大地图位置或世界时间持久化失败。"
        );
    }

    private void _AdvanceWorldTimeBySteps(int delta_steps)
    {
        WorldTimeAdvanceResult advanceResult = WorldTimeSystem.AdvanceWorldStep(
            _world_map_data_context.GetWorldStep(),
            delta_steps
        );
        if (advanceResult.IsValid)
        {
            _world_map_data_context.active_world_data["world_step"] = advanceResult.new_step;
        }
        _wild_encounter_growth_system.ApplyStepAdvance(
            _world_map_data_context.GetActiveEncounterAnchors(),
            advanceResult.old_step,
            advanceResult.new_step,
            _wild_encounter_roster_defs
        );
        int daysElapsed = advanceResult.days_elapsed;
        if (daysElapsed > 0 && _character_management != null)
        {
            var practiceGrowthResult = _character_management.ApplyDailyPracticeGrowthTyped(
                daysElapsed
            );
            if (practiceGrowthResult.Applied)
            {
                _party_state = _character_management.GetPartyState();
                PersistPartyStateInternal();
            }
        }
    }

    private void _resolve_world_encounter_after_battle(string winner_faction_id)
    {
        if (winner_faction_id != "player")
            return;
        var encounterAnchor = _get_encounter_anchor_by_id(_active_battle_encounter_id);
        if (encounterAnchor == null)
            return;
        if (encounterAnchor.encounter_kind == EncounterKindSettlement)
        {
            _wild_encounter_growth_system.ApplyBattleVictory(
                encounterAnchor,
                _world_map_data_context.GetWorldStep(),
                _wild_encounter_roster_defs
            );
            return;
        }
        _remove_active_battle_encounter_anchor();
    }

    public void StartBattle(EncounterAnchorData encounter_anchor) =>
        _StartBattle(encounter_anchor);

    private void _StartBattle(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.StartBattle(encounter_anchor);

    private GDictionary _build_battle_start_context(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.BuildBattleStartContext(encounter_anchor);

    private StringName _resolve_battle_terrain_profile(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.ResolveBattleTerrainProfile(encounter_anchor);

    private void _resolve_active_battle() => _battle_session_facade.ResolveActiveBattleTyped();

    private BattleRefreshMode _attempt_battle_move(Vector2I direction) =>
        _battle_session_facade.AttemptBattleMove(direction);

    private void _RefreshFog()
    {
        if (_world_map_data_context.active_generation_config == null)
            return;
        string leaderMemberId = "player_main";
        if (_party_state != null && _party_state.leader_member_id != "")
            leaderMemberId = _party_state.leader_member_id.ToString();
        var visionSource = new VisionSourceData
        {
            source_id = leaderMemberId,
            center = _player_coord,
            range = _world_map_data_context.active_generation_config.player_vision_range,
            faction_id = _player_faction_id,
        };
        _fog_system.RebuildVisibilityForFaction(_player_faction_id, new[] { visionSource });
        _save_active_fog_state_to_world_data();
    }

    private void _on_world_map_cell_clicked(Vector2I coord)
    {
        if (IsBattleActive() || IsModalWindowOpenInternal())
            return;
        if (IsSubmapActive())
        {
            var result = ReturnFromActiveSubmapTyped();
            if (!result.Ok && string.IsNullOrEmpty(_current_status_message))
                UpdateStatusInternal(string.IsNullOrEmpty(result.Message) ? "返回主地图失败。" : result.Message);
            return;
        }
        _selected_coord = coord;
        if (_fog_system.IsVisible(coord, _player_faction_id) && _try_open_settlement_at(coord))
            return;
        UpdateStatusInternal($"已选中格子 {FormatCoordInternal(coord)}。");
    }

    private void _on_world_map_cell_right_clicked(Vector2I coord)
    {
        if (IsBattleActive() || IsModalWindowOpenInternal())
            return;
        if (!_fog_system.IsVisible(coord, _player_faction_id))
        {
            UpdateStatusInternal("该格当前不在视野中。");
            return;
        }
        if (_try_open_character_info_at_world_coord(coord))
            return;
        UpdateStatusInternal("当前格没有可查看人物。");
    }

    private void _on_battle_cell_clicked(Vector2I coord) =>
        _battle_session_facade.OnBattleCellClicked(coord);

    private void _on_battle_cell_right_clicked(Vector2I coord) =>
        _battle_session_facade.OnBattleCellRightClicked(coord);

    private void _on_battle_skill_slot_selected(int index) =>
        _battle_session_facade.OnBattleSkillSlotSelected(index);

    private bool _try_open_settlement_at(Vector2I coord) => _try_open_settlement_at(coord, true);

    private bool _try_open_settlement_at(Vector2I coord, bool announce_failure)
    {
        if (IsBattleActive())
            return false;
        if (!_fog_system.IsVisible(coord, _player_faction_id))
        {
            if (announce_failure)
                UpdateStatusInternal("该格当前不在视野中。");
            return false;
        }
        WorldMapSettlementData settlement = _world_map_data_context.GetSettlementAt(coord);
        if (settlement.IsEmpty)
        {
            if (announce_failure)
                UpdateStatusInternal("当前格没有可交互据点。");
            return false;
        }
        _active_settlement_id = settlement.SettlementId;
        if (
            coord == _player_coord
            || (_settlement_entry_active && _settlement_entry_target_coord == coord)
        )
            _mark_settlement_visited(_active_settlement_id);
        _active_settlement_feedback_text = "据点通过窗口交付，不切换到城内地图。";
        _active_modal_kind = RuntimeModalKind.Settlement;
        UpdateStatusInternal(
            $"已打开 {settlement.DisplayNameOrFallback("据点")} 的据点窗口。"
        );
        return true;
    }

    private bool _try_open_character_info_at_world_coord(Vector2I coord)
    {
        WorldMapNpcData npc = _world_map_data_context.GetWorldNpcAt(coord);
        if (!npc.HasValidCharacterInfoFields)
            return false;
        string displayName = npc.DisplayName;
        string factionLabel = FormatFactionLabel(npc.FactionId);
        _active_character_info_context.ReplaceWithPayload(
            new GDictionary
            {
                ["display_name"] = displayName,
                ["meta_label"] = _build_character_info_meta_label("世界 NPC", factionLabel, coord),
                ["sections"] = _build_world_character_info_sections(
                    WorldMapDataProjection.Project(npc),
                    coord,
                    factionLabel
                ),
                ["status_label"] = "可见提示单位",
                ["source"] = "world",
            }
        );
        _active_modal_kind = RuntimeModalKind.CharacterInfo;
        UpdateStatusInternal($"已打开 {displayName} 的人物信息窗。");
        return true;
    }

    private bool _try_open_character_info_at_battle_coord(Vector2I coord)
    {
        var unit = _get_battle_unit_at_coord(coord);
        if (unit == null)
            return false;
        string unitId = unit.unit_id.ToString();
        string displayName = string.IsNullOrEmpty(unit.display_name) ? unitId : unit.display_name;
        string factionId = unit.faction_id.ToString();
        string typeLabel = _get_battle_unit_type_label(unitId);
        string factionLabel = FormatFactionLabel(factionId);
        string statusLabel =
            unit.unit_id == _battle_state.active_unit_id ? "当前行动单位" : "战斗单位";
        _active_character_info_context.ReplaceWithPayload(
            new GDictionary
            {
                ["display_name"] = displayName,
                ["meta_label"] = _build_character_info_meta_label(typeLabel, factionLabel, unit.coord),
                ["sections"] = _build_battle_character_info_sections(unit, typeLabel, factionLabel),
                ["status_label"] = statusLabel,
                ["source"] = "battle",
                ["unit_id"] = unitId,
            }
        );
        var fatePayload = _build_battle_character_info_fate_payload(unit);
        if (fatePayload.Count > 0)
            _active_character_info_context.SetValue("fate", Variant.From(fatePayload));
        _active_modal_kind = RuntimeModalKind.CharacterInfo;
        UpdateStatusInternal($"已打开 {displayName} 的人物信息窗。");
        return true;
    }

    private string _build_character_info_meta_label(
        string type_label,
        string faction_label,
        Vector2I coord
    ) => _character_info_builder.BuildCharacterInfoMetaLabel(type_label, faction_label, coord);

    private GArray _build_world_character_info_sections(
        GDictionary npc,
        Vector2I coord,
        string faction_label
    ) =>
        UntypedDictionaryArray(
            _character_info_builder.BuildWorldCharacterInfoSections(npc, coord, faction_label)
        );

    private GArray _build_battle_character_info_sections(
        BattleUnitState unit,
        string type_label,
        string faction_label
    ) =>
        UntypedDictionaryArray(
            _character_info_builder.BuildBattleCharacterInfoSections(
                unit,
                type_label,
                faction_label
            )
        );

    private GDictionary _build_battle_character_info_fate_payload(BattleUnitState unit) =>
        _character_info_builder.BuildBattleCharacterInfoFatePayload(unit);

    private GArray _build_battle_character_info_base_entries(
        BattleUnitState unit,
        string type_label,
        string faction_label
    ) =>
        UntypedDictionaryArray(
            _character_info_builder.BuildBattleCharacterInfoBaseEntries(
                unit,
                type_label,
                faction_label
            )
        );

    private GArray _build_battle_character_status_entries(BattleUnitState unit) =>
        UntypedDictionaryArray(_character_info_builder.BuildBattleCharacterStatusEntries(unit));

    private GArray _build_battle_character_skill_entries(BattleUnitState unit) =>
        UntypedDictionaryArray(_character_info_builder.BuildBattleCharacterSkillEntries(unit));

    private int _get_battle_unit_attribute_value(BattleUnitState unit, StringName attribute_id) =>
        _character_info_builder.GetBattleUnitAttributeValue(unit, attribute_id);

    private GDictionary _get_settlement_at(Vector2I coord)
    {
        WorldMapSettlementData settlement = _world_map_data_context.GetSettlementAt(coord);
        return settlement != null && !settlement.IsEmpty
            ? _world_map_data_context.GetSettlementRecord(settlement.SettlementId)
            : new GDictionary();
    }

    private GDictionary _get_world_npc_at(Vector2I coord)
    {
        WorldMapNpcData npc = _world_map_data_context.GetWorldNpcAt(coord);
        return WorldMapDataProjection.Project(npc);
    }

    private EncounterAnchorData _get_encounter_anchor_at(Vector2I coord) =>
        _world_map_data_context.GetEncounterAnchorAt(coord);

    private EncounterAnchorData _get_encounter_anchor_by_id(StringName entity_id) =>
        _world_map_data_context.GetEncounterAnchorById(entity_id);

    private void _refresh_battle_selection_state()
    {
        if (!IsBattleActive())
            return;
        _battle_selection.SyncSelectedBattleSkillState();
        if (_battle_state == null || _battle_state.IsEmpty())
        {
            RefreshBattleRuntimeStateInternal();
            return;
        }
        if (
            _battle_selected_coord == new Vector2I(-1, -1)
            || !_battle_state.ContainsCell(_battle_selected_coord)
        )
            _battle_selected_coord = _get_default_battle_selected_coord();
    }

    private void _remove_active_battle_encounter_anchor() =>
        _world_map_data_context.RemoveEncounterAnchorById(_active_battle_encounter_id);

    internal void OnSettlementActionRequested(
        string settlement_id,
        string action_id,
        GDictionary payload
    ) =>
        _settlement_command_handler.OnSettlementActionRequested(
            settlement_id,
            action_id,
            payload
        );

    internal void OnSettlementActionRequested(SettlementActionRequest request) =>
        _settlement_command_handler.OnSettlementActionRequested(request);

    internal void OnSettlementWindowClosed() =>
        _settlement_command_handler.OnSettlementWindowClosed();

    private bool BatchHasUpdatesInternal(BattleEventBatch batch)
    {
        if (batch == null)
            return false;
        return batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.ChangedUnitIdsTyped.Count > 0
            || batch.ChangedCoordsTyped.Count > 0
            || batch.LogLinesTyped.Count > 0
            || batch.ProgressionDeltasTyped.Count > 0;
    }

    private bool BatchRequiresFullBattleRefresh(BattleEventBatch batch)
    {
        if (batch == null)
            return false;
        return batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.ChangedCoordsTyped.Count > 0
            || batch.LogLinesTyped.Count > 0
            || batch.ProgressionDeltasTyped.Count > 0;
    }

    internal void ApplyBattleBatch(BattleEventBatch batch)
    {
        _battle_session_facade.ApplyBattleBatch(batch);
        _log_battle_batch_entries(batch);
    }

    internal void RecordCommandBattleBatch(BattleEventBatch batch)
    {
        if (batch == null)
            return;
        _pending_command_battle_batches.Add(_build_battle_batch_log_context(batch));
    }

    internal void RefreshBattleRuntimeState() => RefreshBattleRuntimeStateInternal();

    internal void RefreshBattleRuntimeStateInternal() =>
        _battle_session_facade.RefreshBattleRuntimeState();

    internal int _build_battle_seed(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.BuildBattleSeed(encounter_anchor);

    internal BattleState _get_runtime_battle_state() =>
        _battle_session_facade.GetRuntimeBattleState();

    internal bool _is_battle_finished() => _battle_session_facade.IsBattleFinished();

    internal BattleUnitState _get_runtime_active_unit() =>
        _battle_session_facade.GetRuntimeActiveUnit();

    internal BattleUnitState _get_manual_active_unit() =>
        _battle_session_facade.GetManualActiveUnit();

    internal BattleUnitState _get_runtime_unit_at_coord(Vector2I coord) =>
        _battle_session_facade.GetRuntimeUnitAtCoord(coord);

    internal BattleCommand _build_wait_command() => _battle_session_facade.BuildWaitCommand();

    internal BattleRefreshMode _issue_battle_command(BattleCommand command) =>
        _battle_session_facade.IssueBattleCommand(command);

    internal void _capture_pending_promotion_prompt(GArray progression_deltas) =>
        _battle_session_facade.CapturePendingPromotionPrompt(progression_deltas);

    internal GDictionary BuildRuntimePromotionPromptInternal(CharacterProgressionDelta delta) =>
        BuildRuntimePromotionPromptInternal(delta, "确认后将在战斗中立即生效。");

    internal GDictionary BuildRuntimePromotionPromptInternal(
        CharacterProgressionDelta delta,
        string selection_hint
    ) =>
        _battle_session_facade.BuildPromotionPrompt(delta, selection_hint);

    internal Vector2I _get_default_battle_selected_coord() =>
        _battle_session_facade.GetDefaultBattleSelectedCoord();

    internal BattleUnitState _get_battle_unit_by_id(StringName unit_id) =>
        _battle_session_facade.GetBattleUnitById(unit_id);

    internal BattleUnitState _get_battle_unit_at_coord(Vector2I coord) =>
        _battle_session_facade.GetBattleUnitAtCoord(coord);

    internal BattleUnitState _get_battle_active_unit() =>
        _battle_session_facade.GetBattleActiveUnit();

    internal string _get_battle_active_unit_name() =>
        _battle_session_facade.GetBattleActiveUnitName();

    internal string _get_battle_unit_type_label(string unit_id) =>
        _battle_session_facade.GetBattleUnitTypeLabel(unit_id);

    internal GDictionary _count_battle_terrain_types() =>
        _battle_session_facade.GetBattleTerrainCounts();

    private string _format_optional_text(string value) => string.IsNullOrEmpty(value) ? "无" : value;

    private List<QuestProgressService.QuestProgressEventData> BuildDefaultBattleQuestProgressEventsTyped(
        string winner_faction_id
    )
    {
        List<QuestProgressService.QuestProgressEventData> result = new();
        if (winner_faction_id != "player")
            return result;
        var encounterAnchor = _get_encounter_anchor_by_id(_active_battle_encounter_id);
        if (encounterAnchor == null)
            return result;
        QuestProgressService.QuestProgressEventData eventData =
            QuestProgressService.QuestProgressEventData.CreateProgressByObjectiveTarget(
                "defeat_enemy",
                encounterAnchor.enemy_roster_template_id,
                1,
                GetWorldStep(),
                encounterAnchor.enemy_roster_template_id,
                encounterAnchor.entity_id,
                encounterAnchor.encounter_kind
            );
        if (eventData != null && eventData.IsValid)
            result.Add(eventData);
        return result;
    }

    private static List<PendingCharacterReward> DuplicatePendingCharacterRewards(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        List<PendingCharacterReward> result = new();
        if (rewards == null)
            return result;
        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward != null && !reward.IsEmpty())
                result.Add(reward.DuplicateState());
        }
        return result;
    }

    private List<PendingCharacterReward> FilterBattlePendingCharacterRewardsForQueue(
        IEnumerable<PendingCharacterReward> rewards,
        GDictionary battleSummary,
        string winnerFactionId
    )
    {
        List<PendingCharacterReward> result = new();
        if (rewards == null)
            return result;
        PartyState partyState = _character_management?.GetPartyState() ?? _party_state;
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = GetSkillDefsTyped();
        foreach (PendingCharacterReward reward in rewards)
        {
            if (
                IsBattlePendingCharacterRewardQueueable(
                    reward,
                    partyState,
                    skillDefs,
                    out string errorCode,
                    out PendingCharacterRewardEntry invalidEntry
                )
            )
            {
                result.Add(reward);
                continue;
            }
            LogDroppedBattlePendingCharacterReward(
                reward,
                invalidEntry,
                errorCode,
                battleSummary,
                winnerFactionId
            );
        }
        return result;
    }

    private static bool IsBattlePendingCharacterRewardQueueable(
        PendingCharacterReward reward,
        PartyState partyState,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        out string errorCode,
        out PendingCharacterRewardEntry invalidEntry
    )
    {
        errorCode = "";
        invalidEntry = null;
        if (reward == null || reward.IsEmpty())
        {
            errorCode = "empty_reward";
            return false;
        }
        if (partyState == null || partyState.GetMemberState(reward.member_id) == null)
        {
            errorCode = "missing_member";
            return false;
        }
        bool hasValidEntry = false;
        foreach (PendingCharacterRewardEntry entry in reward.entries)
        {
            if (entry == null)
            {
                errorCode = "null_entry";
                return false;
            }
            invalidEntry = entry;
            PendingCharacterRewardEntryKind entryKind = entry.EntryKind;
            if (entryKind == PendingCharacterRewardEntryKind.Unknown)
            {
                errorCode = "unsupported_entry_type";
                return false;
            }
            if (entry.target_id == "")
            {
                errorCode = "missing_target";
                return false;
            }
            if (entry.amount == 0)
            {
                errorCode = "zero_amount";
                return false;
            }
            if (
                PendingCharacterRewardContentRules.RequiresSkillTarget(entry.entry_type)
                && skillDefs != null
                && skillDefs.Count > 0
                && !skillDefs.ContainsKey(entry.target_id)
            )
            {
                errorCode = "missing_skill_def";
                return false;
            }
            if (
                PendingCharacterRewardContentRules.IsAttributeProgressEntry(entry.entry_type)
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(entry.target_id)
            )
            {
                errorCode = "invalid_attribute_target";
                return false;
            }
            if (
                PendingCharacterRewardContentRules.IsAttributeDeltaEntry(entry.entry_type)
                && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(entry.target_id)
                && entry.target_id != "hp_max"
            )
            {
                errorCode = "invalid_attribute_target";
                return false;
            }
            hasValidEntry = true;
        }
        invalidEntry = null;
        if (!hasValidEntry)
        {
            errorCode = "empty_entries";
            return false;
        }
        return true;
    }

    private void LogDroppedBattlePendingCharacterReward(
        PendingCharacterReward reward,
        PendingCharacterRewardEntry invalidEntry,
        string errorCode,
        GDictionary battleSummary,
        string winnerFactionId
    )
    {
        var context = new GDictionary
        {
            ["battle"] = battleSummary?.Duplicate(true) ?? new GDictionary(),
            ["winner_faction_id"] = winnerFactionId ?? "",
            ["error_code"] = errorCode ?? "",
            ["reward_id"] = reward?.reward_id.ToString() ?? "",
            ["member_id"] = reward?.member_id.ToString() ?? "",
            ["source_type"] = reward?.source_type.ToString() ?? "",
            ["source_id"] = reward?.source_id.ToString() ?? "",
            ["entry_type"] = invalidEntry?.entry_type.ToString() ?? "",
            ["target_id"] = invalidEntry?.target_id.ToString() ?? "",
            ["amount"] = invalidEntry?.amount ?? 0,
        };
        string rewardId = reward?.reward_id.ToString() ?? "";
        string message = string.IsNullOrEmpty(rewardId)
            ? "战斗角色奖励不合法，已丢弃。"
            : $"战斗角色奖励 {rewardId} 不合法，已丢弃。";
        string contextText = Json.Stringify(context);
        GameLog.Warning(message, "battle.pending_reward_dropped", "battle", contextText);
        _log_runtime_event(
            "warn",
            "battle",
            "battle.pending_reward_dropped",
            message,
            contextText
        );
    }

    private bool _has_quest_progress_summary_changes(QuestProgressApplyResultData summary)
    {
        return summary != null
            && (
                summary.CloneAcceptedQuestIds().Count > 0
                || summary.CloneProgressedQuestIds().Count > 0
                || summary.CloneClaimableQuestIds().Count > 0
                || summary.CloneCompletedQuestIds().Count > 0
            );
    }

    private string _format_quest_progress_summary(QuestProgressApplyResultData summary)
    {
        var parts = new System.Collections.Generic.List<string>();
        var acceptedIds = summary?.CloneAcceptedQuestIds() ?? new GStringNameArray();
        var progressedIds = summary?.CloneProgressedQuestIds() ?? new GStringNameArray();
        var claimableIds = summary?.CloneClaimableQuestIds() ?? new GStringNameArray();
        var completedIds = summary?.CloneCompletedQuestIds() ?? new GStringNameArray();
        if (acceptedIds.Count > 0)
            parts.Add($"接取 {_format_string_name_list(acceptedIds)}");
        if (progressedIds.Count > 0)
            parts.Add($"推进 {_format_string_name_list(progressedIds)}");
        if (claimableIds.Count > 0)
            parts.Add($"待领奖励 {_format_string_name_list(claimableIds)}");
        if (completedIds.Count > 0)
            parts.Add($"完成 {_format_string_name_list(completedIds)}");
        return parts.Count > 0
            ? $"任务进度已更新：{string.Join("；", parts)}。"
            : "任务进度未变化。";
    }

    internal QuestDef GetQuestDef(StringName quest_id)
    {
        return GetContentCatalogTyped() != null && quest_id != ""
            ? GetContentCatalogTyped().GetQuestDefTyped(quest_id)
            : null;
    }

    private GDictionary _quest_progress_summary_to_string_dict(QuestProgressApplyResultData summary)
    {
        return new GDictionary
        {
            ["accepted_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneAcceptedQuestIds()
            ),
            ["progressed_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneProgressedQuestIds()
            ),
            ["claimable_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneClaimableQuestIds()
            ),
            ["completed_quest_ids"] = _string_name_array_to_string_array(
                summary?.CloneCompletedQuestIds()
            ),
        };
    }

    private string _format_string_name_list(IEnumerable<StringName> values)
    {
        var labels = _string_name_array_to_string_array(values);
        var strings = new string[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            strings[i] = labels[i];
        return string.Join("、", strings);
    }

    private string _format_string_name_list(GArray values)
    {
        var labels = _string_name_array_to_string_array(values);
        var strings = new string[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            strings[i] = labels[i];
        return string.Join("、", strings);
    }

    private Godot.Collections.Array<string> _string_name_array_to_string_array(
        IEnumerable<StringName> values
    )
    {
        var labels = new Godot.Collections.Array<string>();
        if (values == null)
        {
            return labels;
        }
        foreach (StringName value in values)
        {
            if (value != "")
            {
                labels.Add(value.ToString());
            }
        }
        return labels;
    }

    private Godot.Collections.Array<string> _string_name_array_to_string_array(GArray values)
    {
        var labels = new Godot.Collections.Array<string>();
        foreach (StringName value in ProgressionDataUtils.to_string_name_array(values))
            labels.Add(value.ToString());
        return labels;
    }

    private string _build_quest_claim_reward_summary_text(GDictionary claim_result)
    {
        var rewardParts = new List<string>();
        int goldDelta = DictInt(claim_result, "gold_delta", 0);
        if (goldDelta > 0)
            rewardParts.Add($"{goldDelta} 金");
        foreach (GDictionary rewardData in ReadDictionaryItems(DictArray(claim_result, "item_rewards")))
        {
            int quantity = DictInt(rewardData, "quantity", 0);
            string label = DictString(rewardData, "display_name", "").Trim();
            if (quantity <= 0 || label.Length == 0)
                continue;
            rewardParts.Add($"{label} x{quantity}");
        }
        foreach (
            GDictionary rewardData in ReadDictionaryItems(
                DictArray(claim_result, "pending_character_rewards")
            )
        )
        {
            string memberName = DictString(rewardData, "member_name", "").Trim();
            rewardParts.Add(memberName.Length > 0 ? $"{memberName}的角色奖励" : "角色奖励");
        }
        return string.Join("、", rewardParts);
    }

    internal void UpdateStatusInternal(string message) => _current_status_message = message;

    internal bool IsModalWindowOpenInternal() => _active_modal_kind != RuntimeModalKind.None;

    internal bool IsBattleTimelineModalActive() =>
        IsBattleActive()
        && _battle_state != null
        && _battle_state.ModalStateKind != BattleModalStateKind.None;

    internal void EnqueuePendingCharacterRewardsTyped(
        IEnumerable<PendingCharacterReward> rewards
    ) => _reward_flow_handler?.EnqueuePendingCharacterRewardsTyped(rewards);

    internal Func<StringName> GetEquipmentInstanceIdAllocator() =>
        _game_session != null ? _game_session.AllocateEquipmentInstanceId : null;

    internal EquipmentTraitRollService GetEquipmentTraitRollService()
    {
        if (_game_session == null)
        {
            _equipment_trait_roll_service = null;
            _equipment_trait_roll_service_session = null;
            _equipment_trait_roll_service_catalog = null;
            _equipment_trait_roll_service_catalog_revision = long.MinValue;
            return null;
        }

        GameContentCatalog contentCatalog = GetContentCatalogTyped();
        long catalogRevision = contentCatalog?.GetRevision() ?? 0;
        if (
            _equipment_trait_roll_service == null
            || !ReferenceEquals(_equipment_trait_roll_service_session, _game_session)
            || !ReferenceEquals(_equipment_trait_roll_service_catalog, contentCatalog)
            || _equipment_trait_roll_service_catalog_revision != catalogRevision
        )
        {
            IEnumerable<TraitDef> traitDefs =
                contentCatalog != null
                    ? contentCatalog.GetTraitDefsTyped().Values
                    : _game_session.GetTraitDefsTyped().Values;
            _equipment_trait_roll_service = new EquipmentTraitRollService(
                traitDefs
            );
            _equipment_trait_roll_service_session = _game_session;
            _equipment_trait_roll_service_catalog = contentCatalog;
            _equipment_trait_roll_service_catalog_revision = catalogRevision;
        }
        return _equipment_trait_roll_service;
    }

    internal void SetupPartyWarehouseService(
        PartyWarehouseService service,
        PartyState party_state
    ) =>
        SetupPartyWarehouseService(service, party_state, new Dictionary<StringName, ItemDef>());

    internal void SetupPartyWarehouseService(
        PartyWarehouseService service,
        PartyState party_state,
        IReadOnlyDictionary<StringName, ItemDef> item_defs
    )
    {
        if (service == null)
            return;
        service.Setup(
            party_state,
            item_defs ?? new Dictionary<StringName, ItemDef>(),
            GetEquipmentInstanceIdAllocator(),
            GetEquipmentTraitRollService()
        );
    }

    internal void SyncPartyStateServices()
    {
        var typedItemDefs =
            GetContentCatalogTyped() != null
                ? GetContentCatalogTyped().GetItemDefsTyped()
                : new Dictionary<StringName, ItemDef>();
        var typedSkillDefs =
            GetContentCatalogTyped() != null
                ? GetContentCatalogTyped().GetSkillDefsTyped()
                : new Dictionary<StringName, SkillDef>();
        _character_management?.SetPartyState(_party_state);
        SetupPartyWarehouseService(_party_warehouse_service, _party_state, typedItemDefs);
        if (_party_item_use_service != null && _game_session != null)
            _party_item_use_service.Setup(
                _party_state,
                typedItemDefs,
                typedSkillDefs,
                _party_warehouse_service,
                _character_management
            );
        _party_equipment_service?.Setup(
            _party_state,
            typedItemDefs,
            _party_warehouse_service,
            GetEquipmentInstanceIdAllocator()
        );
    }

    internal int PersistPartyStateInternal()
    {
        RuntimeCommitResult result = CommitRuntimeTransaction(
            new RuntimeTransaction().MarkPartyChanged(),
            "party_state"
        );
        return result.FirstError();
    }

    private int PersistWorldDataInternal()
    {
        RuntimeCommitResult result = CommitRuntimeTransaction(
            new RuntimeTransaction().MarkWorldChanged(),
            "world_data"
        );
        return result.FirstError();
    }

    internal RuntimeCommitResult CommitRuntimeTransaction(
        RuntimeTransaction transaction,
        StringName reason
    )
    {
        transaction ??= new RuntimeTransaction();
        RuntimeCommitResult result = transaction.Commit(
            _game_session,
            BuildRuntimeStateSource(),
            reason
        );
        if (
            transaction.PersistPartyState
            && result.PartyError == (int)Error.Ok
            && _game_session != null
        )
        {
            _party_state = _game_session.GetPartyState();
            SyncPartyStateServices();
            _RefreshFog();
        }
        return result;
    }

    private RuntimeStateSource BuildRuntimeStateSource() =>
        new(
            () => _party_state,
            () =>
            {
                _save_active_fog_state_to_world_data();
                return _world_map_data_context.root_world_data;
            },
            () => _player_coord
        );

    internal int CommitRuntimeStateInternal(StringName reason) =>
        _game_session != null
            ? _game_session.CommitRuntimeState(reason)
            : (int)Error.Unavailable;

    private void CommitPendingRuntimeStateOnDispose()
    {
        if (_game_session == null)
            return;
        if (!_game_session.HasPendingSave())
            return;
        if (_game_session.IsBattleSaveLocked())
            return;
        int commitError = CommitRuntimeStateInternal("runtime.dispose");
        if (commitError == (int)Error.Ok)
            return;
        _log_runtime_event(
            "warn",
            "save",
            "runtime.dispose.commit_failed",
            "运行时释放前保存 pending 状态失败。",
            Json.Stringify(new GDictionary { ["commit_error"] = commitError })
        );
    }

    internal void ClearResolvedBattleRuntimeContext()
    {
                _active_modal_kind = RuntimeModalKind.None;
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _pending_promotion_prompt.Clear();
        _battle_selection.ClearBattleSkillSelection(false);
        _battle_state = null;
        _battle_auto_tick_remainder_msec = 0;
        _battle_selected_coord = new Vector2I(-1, -1);
        _active_battle_encounter_id = "";
        _active_battle_encounter_name = "";
        _selected_coord = _player_coord;
    }

    private void ActivateGameOver(GDictionary context)
    {
        _active_game_over_context.ReplaceWithPayload(context);
        _active_modal_kind = RuntimeModalKind.GameOver;
    }

    internal bool IsMainCharacterDead()
    {
        if (_party_state == null)
            return false;
        var memberId = _party_state.GetResolvedMainCharacterMemberId();
        return memberId != "" && _party_state.IsMemberDead(memberId);
    }

    internal bool IsMainCharacterDeadInBattleState()
    {
        if (_battle_state == null || _party_state == null)
            return false;
        var memberId = _party_state.GetResolvedMainCharacterMemberId();
        if (memberId == "")
            return false;
        foreach (StringName allyUnitId in _battle_state.ally_unit_ids)
        {
            _battle_state.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState);
            if (
                unitState == null
                || ProgressionDataUtils.to_string_name(unitState.source_member_id) != memberId
            )
                continue;
            return !unitState.is_alive || unitState.current_hp <= 0;
        }
        return false;
    }

    private GDictionary BuildMainCharacterGameOverContext()
    {
        var memberId =
            _party_state != null
                ? _party_state.GetResolvedMainCharacterMemberId()
                : new StringName("");
        string memberName = GetMemberDisplayNameInternal(memberId);
        string description =
            memberName.Length > 0
                ? $"{memberName} 已在战斗中阵亡，本次旅程结束。"
                : "主角已在战斗中阵亡，本次旅程结束。";
        return new GDictionary
        {
            ["title"] = "Game Over",
            ["description"] = description,
            ["confirm_text"] = "返回标题",
            ["main_character_member_id"] = memberId.ToString(),
            ["main_character_name"] = memberName,
            ["main_character_dead"] = true,
        };
    }

    private void _mark_settlement_visited(string settlement_id)
    {
        if (settlement_id.Length == 0)
            return;
        MarkSettlementVisited(settlement_id);
    }

    private void _activate_settlement_entry_context(Vector2I source_coord, Vector2I target_coord)
    {
        _settlement_entry_active = true;
        _settlement_entry_source_coord = source_coord;
        _settlement_entry_target_coord = target_coord;
    }

    private void _ClearSettlementEntryContext() => _ClearSettlementEntryContext(true);

    private void _ClearSettlementEntryContext(bool reset_selected)
    {
        _settlement_entry_active = false;
        _settlement_entry_source_coord = new Vector2I(-1, -1);
        _settlement_entry_target_coord = new Vector2I(-1, -1);
        if (reset_selected)
            _selected_coord = _player_coord;
    }

    private bool _is_settlement_entry_hidden_on_world_map()
    {
        if (!_settlement_entry_active)
            return false;
        return RuntimeModalKinds.IsSettlementServiceModal(_active_modal_kind);
    }

    internal string GetItemDisplayName(StringName item_id)
    {
        var itemDef = _party_warehouse_service.GetItemDef(item_id);
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.display_name))
            return itemDef.display_name;
        return item_id.ToString();
    }

    internal string GetSkillDisplayName(StringName skill_id)
    {
        SkillDef skillDef = null;
        GameContentCatalog contentCatalog = GetContentCatalogTyped();
        if (contentCatalog != null)
            contentCatalog.GetSkillDefsTyped().TryGetValue(skill_id, out skillDef);
        if (skillDef != null && !string.IsNullOrEmpty(skillDef.display_name))
            return skillDef.display_name;
        return skill_id.ToString();
    }

    internal string GetMemberDisplayNameInternal(StringName member_id)
    {
        var memberState = _party_state != null ? _party_state.GetMemberState(member_id) : null;
        if (memberState != null && !string.IsNullOrEmpty(memberState.display_name))
            return memberState.display_name;
        return member_id.ToString();
    }

    internal string FormatFactionLabel(string faction_id) =>
        faction_id switch
        {
            "" => "中立",
            "neutral" => "中立",
            "player" => "玩家",
            "hostile" => "敌对",
            _ => faction_id,
        };

    internal string GetFogStateNameInternal(int fog_state)
    {
        WorldMapFogStateKind fogState = WorldMapFogSystem.ToFogStateKind(fog_state);
        if (fogState == WorldMapFogStateKind.Visible)
            return "当前可见";
        if (fogState == WorldMapFogStateKind.Explored)
            return "已探索";
        return "未探索";
    }

    public bool IsBattleActive() => _battle_state != null && !_battle_state.IsEmpty();

    internal bool HasPendingBattleGenerationRequest() =>
        !_pending_battle_generation_request.IsEmpty;

    internal GameRuntimePendingBattleGenerationRequest GetPendingBattleGenerationRequestState() =>
        _pending_battle_generation_request;

    internal void ClearPendingBattleGenerationRequest() =>
        _pending_battle_generation_request.Clear();

    internal bool IsAdjacent4(Vector2I from_coord, Vector2I to_coord) =>
        Math.Abs(from_coord.X - to_coord.X) + Math.Abs(from_coord.Y - to_coord.Y) == 1;

    internal string FormatCoordInternal(Vector2I coord) => $"({coord.X}, {coord.Y})";

    private static GVector2IArray ToVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GVector2IArray();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private void _sync_active_world_context()
    {
        _save_active_fog_state_to_world_data();
        WorldMapContextSyncResult syncResult = _world_map_data_context.SyncActiveWorldContext(
            _generation_config,
            _grid_system,
            _player_coord,
            _selected_coord
        );
        _player_coord = syncResult.PlayerCoord;
        _selected_coord = syncResult.SelectedCoord;
        if (_world_map_data_context.active_generation_config != null)
        {
            _fog_system.Setup(
                _world_map_data_context.active_generation_config.GetWorldSizeCells(),
                _get_active_world_fog_state()
            );
            _world_map_data_context.ValidateWorldSystemSizeConsistency(
                _grid_system,
                _fog_system
            );
        }
    }

    private GDictionary _get_active_world_fog_state() =>
        _world_map_data_context.GetActiveWorldFogState();

    private void _save_active_fog_state_to_world_data()
    {
        _world_map_data_context.SaveActiveWorldFogState(_fog_system);
    }

    private GDictionary _get_world_event_at(Vector2I coord)
    {
        WorldMapEventData worldEvent = _world_map_data_context.GetWorldEventAt(coord);
        return WorldMapDataProjection.Project(worldEvent);
    }

    private WorldMapEventData GetTriggerableWorldEventAt(Vector2I coord)
    {
        WorldMapEventData worldEvent = _world_map_data_context.GetWorldEventAt(coord);
        return worldEvent != null && worldEvent.IsTriggerableSubmapEntry ? worldEvent : null;
    }

    private void OpenWorldEventPrompt(WorldMapEventData worldEvent)
    {
        if (worldEvent == null)
        {
            return;
        }
        string targetSubmapId = worldEvent.TargetSubmapId.ToString();
        var submapEntry = _get_mounted_submap_entry(targetSubmapId);
        if (submapEntry.Count == 0)
        {
            UpdateStatusInternal($"未找到目标子地图 {targetSubmapId}。");
            return;
        }
        string targetName = _world_map_data_context.GetMountedSubmapDisplayName(
            targetSubmapId,
            targetSubmapId
        );
        string promptTitle = string.IsNullOrEmpty(worldEvent.PromptTitle)
            ? "进入子地图"
            : worldEvent.PromptTitle;
        if (promptTitle.Length == 0)
            promptTitle = $"进入 {targetName}";
        string promptText = worldEvent.PromptText;
        if (promptText.Length == 0)
            promptText = $"确认后将进入 {targetName}，返回时会回到当前坐标。";
        _pending_submap_prompt.Set(
            worldEvent.EventId,
            _world_map_data_context.active_map_id,
            _player_coord,
            worldEvent.TargetSubmapId,
            targetName,
            promptTitle,
            promptText
        );
        _active_modal_kind = RuntimeModalKind.SubmapConfirm;
        UpdateStatusInternal(
            $"已发现 {ResolveWorldEventDisplayName(worldEvent, targetName)}，确认后可进入。"
        );
    }

    private static string ResolveWorldEventDisplayName(
        WorldMapEventData worldEvent,
        string fallback
    )
    {
        if (worldEvent == null || string.IsNullOrEmpty(worldEvent.DisplayName))
        {
            return fallback;
        }
        return worldEvent.DisplayName;
    }

    internal GameRuntimePendingSubmapPrompt GetPendingSubmapPromptState() =>
        _pending_submap_prompt;

    private GDictionary _confirm_pending_submap_entry()
    {
        return FinalizeCommandResult(ConfirmPendingSubmapEntryTyped());
    }

    private RuntimeCommandResult ConfirmPendingSubmapEntryTyped()
    {
        if (_pending_submap_prompt.IsEmpty)
            return BuildCommandErrorResult("当前没有待确认的子地图入口。");
        var result = EnterSubmapTyped(
            _pending_submap_prompt.TargetSubmapId.ToString(),
            _pending_submap_prompt.SourceMapId,
            _pending_submap_prompt.SourceCoord
        );
        if (result.Ok)
        {
            _pending_submap_prompt.Clear();
            _active_modal_kind = RuntimeModalKind.None;
        }
        return result;
    }

    private GDictionary _enter_submap(string submap_id, string source_map_id, Vector2I source_coord)
    {
        return FinalizeCommandResult(EnterSubmapTyped(submap_id, source_map_id, source_coord));
    }

    private RuntimeCommandResult EnterSubmapTyped(
        string submap_id,
        string source_map_id,
        Vector2I source_coord
    )
    {
        if (_game_session == null)
            return BuildCommandErrorResult("游戏会话不可用，无法进入子地图。");
        if (submap_id.Length == 0)
            return BuildCommandErrorResult("子地图标识不能为空。");
        WorldMapSubmapEnterResult enterResult = _world_map_data_context.EnterSubmap(
            submap_id,
            source_map_id,
            source_coord
        );
        if (!enterResult.Ok)
            return BuildCommandErrorResult(enterResult.Message);
        _player_coord = enterResult.PlayerCoord;
        _selected_coord = _player_coord;
        _active_settlement_id = "";
        _active_settlement_feedback_text = "";
        _active_character_info_context.Clear();
        _sync_active_world_context();
        _RefreshFog();
        int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
        int worldPersistError = _game_session.SetWorldData(
            _world_map_data_context.root_world_data
        );
        int commitError = (int)Error.Ok;
        if (playerPersistError == (int)Error.Ok && worldPersistError == (int)Error.Ok)
            commitError = CommitRuntimeStateInternal("submap_entry");
        string targetName = enterResult.TargetDisplayName;
        if (
            playerPersistError != (int)Error.Ok
            || worldPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            UpdateStatusInternal($"已进入 {targetName}，但世界状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }
        UpdateStatusInternal($"已进入 {targetName}。{GetSubmapReturnHintText()}");
        return BuildCommandOkResult();
    }

    private GDictionary _return_from_active_submap()
    {
        return FinalizeCommandResult(ReturnFromActiveSubmapTyped());
    }

    private RuntimeCommandResult ReturnFromActiveSubmapTyped()
    {
        if (_game_session == null)
            return BuildCommandErrorResult("游戏会话不可用，无法返回主地图。");
        if (!IsSubmapActive())
            return BuildCommandErrorResult("当前不在子地图中。");
        WorldMapSubmapReturnResult returnResult =
            _world_map_data_context.ReturnFromActiveSubmap(_player_coord);
        if (!returnResult.Ok)
            return BuildCommandErrorResult(returnResult.Message);
        _player_coord = returnResult.PlayerCoord;
        _selected_coord = _player_coord;
        _active_settlement_id = "";
        _active_settlement_feedback_text = "";
        _active_character_info_context.Clear();
        _pending_submap_prompt.Clear();
            _active_modal_kind = RuntimeModalKind.None;
        _sync_active_world_context();
        _RefreshFog();
        int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
        int worldPersistError = _game_session.SetWorldData(
            _world_map_data_context.root_world_data
        );
        int commitError = (int)Error.Ok;
        if (playerPersistError == (int)Error.Ok && worldPersistError == (int)Error.Ok)
            commitError = CommitRuntimeStateInternal("submap_return");
        if (
            playerPersistError != (int)Error.Ok
            || worldPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            UpdateStatusInternal("已返回原位置，但世界状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }
        UpdateStatusInternal($"已返回原位置 {FormatCoordInternal(_player_coord)}。");
        return BuildCommandOkResult();
    }

    private bool _ensure_submap_generated(string submap_id) =>
        _world_map_data_context.EnsureSubmapGenerated(submap_id);

    private WorldMapGenerationConfig _load_submap_generation_config(string submap_id) =>
        _world_map_data_context.LoadSubmapGenerationConfig(submap_id);

    private GDictionary _get_mounted_submap_entry(string submap_id) =>
        _world_map_data_context.GetMountedSubmapEntry(submap_id);

    private void _set_mounted_submap_entry(string submap_id, GDictionary submap_entry) =>
        _world_map_data_context.SetMountedSubmapEntry(submap_id, submap_entry);

    private static GArray DictArray(GDictionary dictionary, object key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, object key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int DictInt(GDictionary dictionary, object key, int fallback = 0)
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static string DictString(GDictionary dictionary, object key, string fallback = "")
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        object key,
        StringName fallback = default
    )
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(value);
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool TryGetDictionaryValue(
        GDictionary dictionary,
        object key,
        out Variant value
    )
    {
        if (dictionary == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        value = default;
        return false;
    }

    private static GDictionary CoordDict(Vector2I coord) =>
        new() { ["x"] = coord.X, ["y"] = coord.Y };

    private static void ResizeArray(GArray values, int maxCount)
    {
        if (values.Count > maxCount)
            values.Resize(maxCount);
    }

    private static void DisposeOwned<T>(T owned, Action<T> cleanup)
        where T : GodotObject
    {
        if (owned == null || !GodotObject.IsInstanceValid(owned))
        {
            return;
        }

        GC.SuppressFinalize(owned);
        cleanup?.Invoke(owned);
        if (GodotObject.IsInstanceValid(owned))
        {
            owned.Dispose();
        }
    }

    private static void SortDictionaryArray(GArray values, string numericKey, string stringTieKey)
    {
        var list = new System.Collections.Generic.List<GDictionary>();
        foreach (GDictionary value in ReadDictionaryItems(values))
            list.Add(value);
        list.Sort(
            (left, right) =>
            {
                int leftValue = DictInt(left, numericKey);
                int rightValue = DictInt(right, numericKey);
                if (leftValue != rightValue)
                    return leftValue.CompareTo(rightValue);
                return string.CompareOrdinal(
                    DictString(left, stringTieKey),
                    DictString(right, stringTieKey)
                );
            }
        );
        values.Clear();
        foreach (var entry in list)
            values.Add(entry);
    }

    private static GArray UntypedQuestArray(Godot.Collections.Array<QuestState> values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GArray UntypedDictionaryArray(Godot.Collections.Array<GDictionary> values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GArray ProjectEquipmentEntries(
        IEnumerable<PartyEquipmentService.EquipmentViewEntry> values
    )
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (PartyEquipmentService.EquipmentViewEntry value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new GDictionary
                {
                    ["slot_id"] = value.SlotId.ToString(),
                    ["slot_label"] = value.SlotLabel,
                    ["item_id"] = value.ItemId.ToString(),
                    ["instance_id"] = value.InstanceId.ToString(),
                    ["equipment_type_id"] = value.EquipmentTypeId.ToString(),
                    ["display_name"] = value.DisplayName,
                    ["icon"] = value.Icon,
                    ["description"] = value.Description,
                }
            );
        }
        return result;
    }

    private string BuildBattleResolvedLogContext(
        GDictionary battleSummary,
        string winnerFactionId,
        IReadOnlyCollection<PendingCharacterReward> resolvedPendingRewards,
        GStringNameArray guidanceUnlocks,
        GStringNameArray misfortuneGuidanceUnlocks,
        GDictionary lowLuckEventResult,
        QuestProgressApplyResultData questSummary,
        BattleResolutionResult battleResolutionResult,
        GameRuntimeBattleLootCommitService.BattleLootCommitResult lootCommitResult,
        bool saveSkipped,
        int partyPersistError,
        int worldPersistError,
        int flushError
    )
    {
        return Json.Stringify(new GDictionary
        {
            ["battle"] = battleSummary,
            ["winner_faction_id"] = winnerFactionId,
            ["main_character_member_id"] =
                _party_state != null
                    ? _party_state.GetResolvedMainCharacterMemberId().ToString()
                    : "",
            ["pending_reward_count"] = resolvedPendingRewards?.Count ?? 0,
            ["fortuna_guidance_unlocks"] = ProgressionDataUtils.string_name_array_to_string_array(
                guidanceUnlocks
            ),
            ["misfortune_guidance_unlocks"] =
                ProgressionDataUtils.string_name_array_to_string_array(misfortuneGuidanceUnlocks),
            ["low_luck_event_ids"] = ProgressionDataUtils.string_name_array_to_string_array(
                ProgressionDataUtils.to_string_name_array(
                    DictArray(lowLuckEventResult, "triggered_event_ids")
                )
            ),
            ["loot_entry_count"] = battleResolutionResult.loot_entries.Count,
            ["overflow_entry_count"] = battleResolutionResult.overflow_entries.Count,
            ["loot_commit_ok"] = lootCommitResult?.Ok ?? false,
            ["loot_commit_error_code"] = lootCommitResult?.ErrorCode ?? "",
            ["loot_commit_blocked_item_id"] = lootCommitResult?.BlockedItemId ?? "",
            ["loot_committed_item_count"] = lootCommitResult?.CommittedItemCount ?? 0,
            ["loot_overflow_entries"] = lootCommitResult?.OverflowEntries.Duplicate(true)
                ?? new GArray(),
            ["quest_progress_summary"] = _quest_progress_summary_to_string_dict(questSummary),
            ["save_skipped"] = saveSkipped,
            ["party_persist_error"] = partyPersistError,
            ["world_persist_error"] = worldPersistError,
            ["flush_error"] = flushError,
        });
    }
}
