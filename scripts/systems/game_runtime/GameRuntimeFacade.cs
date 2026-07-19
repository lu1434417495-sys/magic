using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed partial class GameRuntimeFacade : IGameRuntimeSnapshotSource, IDisposable
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

    private static void ReplacePlainPayload(
        Dictionary<string, object> target,
        GDictionary payload,
        string ownerPath
    )
    {
        target.Clear();
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(payload ?? new GDictionary(), ownerPath);
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static void ReplacePlainPayload(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> payload
    )
    {
        Dictionary<string, object> cloned = RuntimePlainPayload.CloneDictionary(payload);
        target.Clear();
        foreach (KeyValuePair<string, object> entry in cloned)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static GodotProjectionLease<GDictionary> ProjectPlainPayloadLease(
        IReadOnlyDictionary<string, object> source,
        string ownerPath
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            source,
            ownerPath,
            LifetimeDomain.Request,
            ownerPath
        );

    private static void PutPlainPayloadValue(
        Dictionary<string, object> target,
        string key,
        Variant value,
        string ownerPath
    )
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        target[key] = RuntimePlainPayload.NormalizeVariant(value, $"{ownerPath}.{key}");
    }

    private static string PlainPayloadString(
        IReadOnlyDictionary<string, object> payload,
        string key,
        string fallback = ""
    )
    {
        if (payload == null || !payload.TryGetValue(key, out object value))
            return fallback ?? "";
        return value switch
        {
            string text => text,
            StringName name => name.ToString(),
            _ => fallback ?? "",
        };
    }

    private sealed class BattleFinalizationRollbackState
    {
        private readonly FateRuntimeRollbackState _fateRuntimeState;
        private readonly BattleResolutionResult _resolutionResult;

        private BattleFinalizationRollbackState(
            FateRuntimeRollbackState fateRuntimeState,
            BattleResolutionResult resolutionResult
        )
        {
            _fateRuntimeState = fateRuntimeState;
            _resolutionResult = resolutionResult?.Duplicate();
        }

        internal static BattleFinalizationRollbackState Capture(
            BattleRuntimeModule battleRuntime,
            BattleResolutionResult resolutionResult
        ) =>
            new(
                battleRuntime?.GetFateRuntime()?.CaptureRollbackState(),
                resolutionResult
            );

        internal void Restore(
            BattleRuntimeModule battleRuntime,
            BattleResolutionResult resolutionResult
        )
        {
            battleRuntime?.GetFateRuntime()?.RestoreRollbackState(_fateRuntimeState);
            resolutionResult?.RestoreFrom(_resolutionResult);
        }
    }

    internal WorldGenerationDefinition _generation_definition;
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
    internal Vector2I _pending_harvest_coord = new(-1, -1);
    internal bool _settlement_entry_active;
    internal Vector2I _settlement_entry_source_coord = new(-1, -1);
    internal Vector2I _settlement_entry_target_coord = new(-1, -1);
    internal string _player_faction_id = "player";
    internal WorldMapDataContext _world_map_data_context = new();
    private readonly GameRuntimePendingSubmapPrompt _pending_submap_prompt = new();
    internal readonly Dictionary<string, object> _pending_battle_start_prompt =
        new(StringComparer.Ordinal);
    private readonly GameRuntimePendingBattleGenerationRequest _pending_battle_generation_request = new();
    internal PartyState _party_state;
    internal BattleState _battle_state;
    internal int _battle_auto_tick_remainder_msec;
    private GameRuntimeSnapshotBuilder _snapshot_builder = new();
    internal GameRuntimeCommandLogger _command_logger = new();
    private GameRuntimeBattleWritebackService _battle_writeback_service = new();
    internal GameRuntimeBattleLootCommitService _battle_loot_commit_service = new();
    private GameRuntimeCharacterInfoBuilder _character_info_builder = new();
    internal BattleSessionFacade _battle_session_facade;
    internal GameRuntimeBattleSelection _battle_selection = new();
    private readonly GameRuntimeBattleSelectionState _battle_selection_state = new();
    internal GameRuntimeSettlementCommandHandler _settlement_command_handler = new();
    internal GameRuntimeWarehouseHandler _warehouse_handler = new();
    internal GameRuntimePartyCommandHandler _party_command_handler = new();
    internal GameRuntimeRewardFlowHandler _reward_flow_handler = new();
    internal GameRuntimeQuestCommandHandler _quest_command_handler = new();
    internal StringName _active_battle_encounter_id = "";
    internal string _active_battle_encounter_name = "";
    internal readonly Dictionary<string, object> _pending_promotion_prompt =
        new(StringComparer.Ordinal);
    internal PendingCharacterReward _active_reward;
    internal readonly Dictionary<string, object> _pending_world_promotion_prompt =
        new(StringComparer.Ordinal);
    internal RuntimeModalKind _active_modal_kind = RuntimeModalKind.None;
    internal string _active_warehouse_entry_label = "";
    internal string _active_settlement_id = "";
    internal string _active_settlement_feedback_text = "";
    internal readonly Dictionary<string, object> _active_contract_board_context =
        new(StringComparer.Ordinal);
    internal NpcQuestOfferWindowData _active_npc_quest_offer_data;
    internal BountyBoardWindowData _active_bounty_board_data;
    internal readonly Dictionary<string, object> _active_shop_context = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, object> _active_forge_context = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, object> _active_stagecoach_context =
        new(StringComparer.Ordinal);
    internal string _current_status_message = "";
    internal BattleRefreshMode _last_advance_battle_refresh_mode = BattleRefreshMode.None;
    internal BattlePresentationDelta _last_advance_battle_presentation_delta =
        BattlePresentationDelta.None;
    internal BattlePresentationDelta _last_command_battle_presentation_delta =
        BattlePresentationDelta.None;
    internal readonly Dictionary<string, object> _last_battle_loot_snapshot =
        new(StringComparer.Ordinal);
    internal readonly List<Dictionary<string, object>> _pending_command_battle_batches = new();
    internal readonly Dictionary<string, object> _active_character_info_context =
        new(StringComparer.Ordinal);
    internal readonly Dictionary<string, object> _active_game_over_context =
        new(StringComparer.Ordinal);
    internal StringName _party_selected_member_id = "";
    private ContingencySetupMutationResult _last_contingency_command_result =
        ContingencySetupMutationResult.Failure("", "", "");
    private readonly Dictionary<StringName, WildEncounterRosterDefinition> _wild_encounter_roster_definitions = new();
    private bool _disposed;

    internal Vector2I _battle_selected_coord
    {
        get => _battle_selection_state.battle_selected_coord;
        set => _battle_selection_state.battle_selected_coord = value;
    }

    internal void SetWildEncounterRosterDefinitionForTests(
        WildEncounterRosterDefinition roster
    )
    {
        ArgumentNullException.ThrowIfNull(roster);
        _wild_encounter_roster_definitions[roster.ProfileId] = roster;
    }

    internal StringName _selected_battle_skill_id
    {
        get => _battle_selection_state.selected_skill_id;
        set => _battle_selection_state.selected_skill_id = value;
    }

    internal StringName _selected_battle_skill_entry_id
    {
        get => _battle_selection_state.selected_skill_entry_id;
        set => _battle_selection_state.selected_skill_entry_id = value;
    }

    internal StringName _selected_battle_skill_variant_id
    {
        get => _battle_selection_state.selected_skill_variant_id;
        set => _battle_selection_state.selected_skill_variant_id = value;
    }

    public StringName _last_manual_battle_unit_id
    {
        get => _battle_selection_state.last_manual_unit_id;
        set => _battle_selection_state.last_manual_unit_id = value;
    }

    public GameRuntimeFacade()
        : this(new TrueRandomBattleSeedSource())
    {
    }

    internal GameRuntimeFacade(IBattleSeedSource seedSource)
    {
        ArgumentNullException.ThrowIfNull(seedSource);
        _battle_session_facade = new BattleSessionFacade(seedSource);
        _battle_runtime = new BattleRuntimeModule();
        BindRuntimeSidecarOwners();
    }

    public void Setup(GameSession game_session)
    {
        _game_session = game_session;
        if (_game_session == null)
            return;

        _ = _game_session.GetContentSnapshotEpoch();
        _game_root = _game_session.GetGameRootTyped();
        _content_catalog = _game_root.GetContentCatalogTyped();
        if (!_game_session.HasActiveWorld())
            return;

        _generation_definition = _game_session.GetGenerationDefinition();
        if (_generation_definition == null)
            return;

        using GodotProjectionLease<GDictionary> worldDataLease =
            _game_session.GetWorldDataLease();
        _world_map_data_context.BindRootWorldData(worldDataLease.Value);
        RebuildWildEncounterRosterDefinitionIndex(
            _content_catalog.GetEncounterRosterDefinitions()
        );
        _encounter_roster_builder.Setup(
            _content_catalog.GetEncounterRosterDefinitions(),
            _content_catalog.GetEnemyTemplateDefinitions()
        );
        _party_state = _game_session.GetPartyState();
        _player_coord = _game_session.GetPlayerCoord();
        _player_faction_id = _game_session.GetPlayerFactionId();

        _character_management.setup(
            _party_state,
            _content_catalog.GetSkillDefinitionsTyped(),
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
            _content_catalog.GetSkillDefinitionsTyped(),
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
            character_gateway: _character_management,
            enemy_templates: _content_catalog.GetEnemyTemplateDefinitions(),
            enemy_ai_brains: _content_catalog.GetEnemyAiBrainDefinitions(),
            encounter_builder: _encounter_roster_builder,
            equipment_drop_service: _equipment_drop_service,
            item_defs: _content_catalog.GetItemDefsTyped(),
            trait_defs: _content_catalog.GetTraitDefsTyped(),
            equipment_ability_bindings: _content_catalog.GetEquipmentAbilityBindingDefinitionsTyped(),
            equipment_instance_id_allocator: GetEquipmentInstanceIdAllocator(),
            skill_catalog: _content_catalog.GetSkillCatalogTyped(),
            skill_definitions: _content_catalog.GetSkillDefinitionsTyped(),
            battle_special_profile_view: _content_catalog.GetBattleSpecialProfileView(),
            barrier_profile_definitions: _content_catalog.GetBarrierProfileDefinitionsTyped()
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
        _active_npc_quest_offer_data = null;
        _active_bounty_board_data = null;
        _active_shop_context.Clear();
        _active_forge_context.Clear();
        _active_stagecoach_context.Clear();
        _last_advance_battle_refresh_mode = BattleRefreshMode.None;
        _last_advance_battle_presentation_delta = BattlePresentationDelta.None;
        _last_command_battle_presentation_delta = BattlePresentationDelta.None;
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
                PlainPayloadString(
                    GetGameOverContextSnapshotPlain(),
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

    private void RebuildWildEncounterRosterDefinitionIndex(
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> wildEncounterRosters
    )
    {
        _wild_encounter_roster_definitions.Clear();
        if (wildEncounterRosters == null)
        {
            return;
        }
        foreach (
            (StringName rosterId, WildEncounterRosterDefinition roster) in wildEncounterRosters
        )
        {
            if (rosterId == "" || roster == null || roster.ProfileId == "")
                continue;
            _wild_encounter_roster_definitions[roster.ProfileId] = roster;
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
        _release_battle_save_lock();
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
        _equipment_trait_roll_service?.Dispose();
        _equipment_drop_service?.Dispose();

        _game_session = null;
        _game_root = null;
        _content_catalog = null;
        _generation_definition = null;
        _world_map_data_context.Dispose();
        _pending_submap_prompt.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _wild_encounter_roster_definitions.Clear();
        _party_state = null;
        ClearRuntimeBattleStateReference();
        _pending_promotion_prompt.Clear();
        _pending_world_promotion_prompt.Clear();
        _active_character_info_context.Clear();
        _active_game_over_context.Clear();
        _active_contract_board_context.Clear();
        _active_npc_quest_offer_data = null;
        _active_bounty_board_data = null;
        _active_shop_context.Clear();
        _active_forge_context.Clear();
        _active_stagecoach_context.Clear();
        _last_advance_battle_refresh_mode = BattleRefreshMode.None;
        _last_advance_battle_presentation_delta = BattlePresentationDelta.None;
        _last_command_battle_presentation_delta = BattlePresentationDelta.None;
        _last_battle_loot_snapshot.Clear();
        _battle_selection_state.ResetForBattleEnd();
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

    public IReadOnlyDictionary<string, object> GetLogSnapshotPlain(int limit = 30) =>
        _game_session != null
            ? _game_session.GetLogSnapshotPlain(limit)
            : new Dictionary<string, object>(StringComparer.Ordinal);

    public string GetActiveLogFilePath() =>
        _game_session != null ? _game_session.GetActiveLogFilePath() : "";

    public string GetActiveModalId() =>
        RuntimeModalKinds.ToPayloadValue(_active_modal_kind);

    public RuntimeModalKind GetActiveModalKind() => _active_modal_kind;

    internal GodotProjectionLease<GDictionary> GetGameOverContextLease() =>
        ProjectPlainPayloadLease(
            _active_game_over_context,
            "GameRuntimeFacade.game_over_context"
        );

    public IReadOnlyDictionary<string, object> GetGameOverContextSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_active_game_over_context);

    public string GetActiveSettlementId() => _active_settlement_id;

    public string GetActiveMapId() => _world_map_data_context.GetActiveMapId();

    public string GetActiveMapDisplayName() =>
        _world_map_data_context.GetActiveMapDisplayName();

    public string GetSubmapReturnHintText() =>
        _world_map_data_context.GetSubmapReturnHintText();

    public GDictionary GetPendingSubmapPrompt() =>
        GameRuntimePendingSubmapPromptProjection.Project(_pending_submap_prompt);

    public IReadOnlyDictionary<string, object> GetPendingSubmapPromptSnapshotPlain()
    {
        if (_pending_submap_prompt == null || _pending_submap_prompt.IsEmpty)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["event_id"] = _pending_submap_prompt.EventId.ToString(),
            ["source_map_id"] = _pending_submap_prompt.SourceMapId,
            ["source_coord"] = _pending_submap_prompt.SourceCoord,
            ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            ["target_display_name"] = _pending_submap_prompt.TargetDisplayName,
            ["title"] = _pending_submap_prompt.Title,
            ["description"] = _pending_submap_prompt.Description,
        };
    }

    internal GodotProjectionLease<GDictionary> GetPendingBattleStartPromptLease() =>
        ProjectPlainPayloadLease(
            _pending_battle_start_prompt,
            "GameRuntimeFacade.pending_battle_start_prompt"
        );

    public IReadOnlyDictionary<string, object> GetPendingBattleStartPromptSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_pending_battle_start_prompt);

    public bool IsSubmapActive() => _world_map_data_context.IsSubmapActive();

    public int GetWorldStep() => _world_map_data_context.GetWorldStep();

    public WorldMapSettlementData GetSelectedSettlementData() =>
        _world_map_data_context.GetSettlementAt(_selected_coord);

    public WorldMapNpcData GetSelectedWorldNpcData() =>
        _world_map_data_context.GetWorldNpcAt(_selected_coord);

    public EncounterAnchorData GetSelectedEncounterAnchor() =>
        _get_encounter_anchor_at(_selected_coord);

    public WorldMapEventData GetSelectedWorldEventData() =>
        _world_map_data_context.GetWorldEventAt(_selected_coord);

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

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetNearbyEncounterEntriesSnapshotPlain(
        int limit
    )
    {
        var result = new List<IReadOnlyDictionary<string, object>>();
        foreach (WorldEncounterViewModel entry in BuildNearbyEncounterViewModels(limit))
        {
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["entity_id"] = entry.EntityId,
                    ["display_name"] = entry.DisplayName,
                    ["coord"] = CoordPlain(entry.Coord),
                    ["distance"] = entry.Distance,
                    ["encounter_kind"] = entry.EncounterKind,
                    ["growth_stage"] = entry.GrowthStage,
                }
            );
        }
        return result.AsReadOnly();
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

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetNearbyWorldEventEntriesSnapshotPlain(
        int limit
    )
    {
        var result = new List<IReadOnlyDictionary<string, object>>();
        foreach (WorldEventViewModel entry in BuildNearbyWorldEventViewModels(limit))
        {
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["event_id"] = entry.EventId,
                    ["display_name"] = entry.DisplayName,
                    ["coord"] = CoordPlain(entry.Coord),
                    ["distance"] = entry.Distance,
                    ["event_type"] = entry.EventType,
                    ["target_submap_id"] = entry.TargetSubmapId,
                }
            );
        }
        return result.AsReadOnly();
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

    internal GodotProjectionLease<GDictionary> GetWorldDataLease() =>
        _world_map_data_context.GetActiveWorldDataLease();

    public IReadOnlyDictionary<string, object> GetWorldDataSnapshotPlain() =>
        _world_map_data_context.GetActiveWorldDataSnapshotPlain();

    // Typed handle to the already-parsed active world data, so the world-map view
    // can render without round-tripping through ToDictionary/FromDictionary.
    internal WorldRuntimeData GetActiveWorldRuntimeData() =>
        _world_map_data_context.ActiveRuntimeData;

    internal WorldGenerationDefinition GetGenerationDefinition() =>
        _world_map_data_context.GetActiveGenerationDefinition();

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

    internal GodotProjectionLease<GDictionary> GetMemberAchievementSummaryLease(
        StringName member_id
    ) =>
        _character_management != null
            ? _character_management.GetMemberAchievementSummaryLease(member_id)
            : RuntimePlainPayload.ProjectDictionaryLease(
                new Dictionary<string, object>(StringComparer.Ordinal),
                "GameRuntimeFacade.GetMemberAchievementSummary",
                LifetimeDomain.Request,
                "GameRuntimeFacade.GetMemberAchievementSummary"
            );

    public IReadOnlyDictionary<string, object> GetMemberAchievementSummarySnapshotPlain(
        StringName member_id
    ) =>
        _character_management != null
            ? _character_management.GetMemberAchievementSummarySnapshotPlain(member_id)
            : new Dictionary<string, object>(StringComparer.Ordinal);

    public AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id) =>
        _character_management != null
            ? _character_management.GetMemberAttributeSnapshot(member_id)
            : null;

    public GArray GetMemberEquippedEntries(StringName member_id) =>
        _party_equipment_service != null
            ? ProjectEquipmentEntries(_party_equipment_service.GetEquippedEntriesTyped(member_id))
            : new GArray();

    public IReadOnlyList<IReadOnlyDictionary<string, object>> GetMemberEquippedEntriesSnapshotPlain(
        StringName member_id
    )
    {
        var result = new List<IReadOnlyDictionary<string, object>>();
        if (_party_equipment_service == null)
            return result.AsReadOnly();
        foreach (
            PartyEquipmentService.EquipmentViewEntry entry in
            _party_equipment_service.GetEquippedEntriesTyped(member_id)
        )
        {
            if (entry == null)
                continue;
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["slot_id"] = entry.SlotId.ToString(),
                    ["slot_label"] = entry.SlotLabel,
                    ["item_id"] = entry.ItemId.ToString(),
                    ["instance_id"] = entry.InstanceId.ToString(),
                    ["equipment_type_id"] = entry.EquipmentTypeId.ToString(),
                    ["display_name"] = entry.DisplayName,
                    ["icon"] = entry.Icon,
                    ["description"] = entry.Description,
                }
            );
        }
        return result.AsReadOnly();
    }

    public string GetMemberDisplayName(StringName member_id) =>
        GetMemberDisplayNameInternal(member_id);

    public StringName GetPartySelectedMemberId() => _party_selected_member_id;

    internal void SetPartySelectedMemberId(StringName member_id) =>
        _party_selected_member_id = member_id;

    public GDictionary GetSettlementWindowData() => GetSettlementWindowData("");

    public GDictionary GetSettlementWindowData(string settlement_id) =>
        _settlement_command_handler.GetSettlementWindowData(settlement_id);

    public IReadOnlyDictionary<string, object> GetSettlementHeadlessFactsPlain(
        string settlement_id
    ) => _settlement_command_handler.GetSettlementHeadlessFactsPlain(settlement_id);

    public string GetSettlementFeedbackText() => _active_settlement_feedback_text;

    internal void SetActiveSettlementId(string settlement_id) =>
        _active_settlement_id = settlement_id;

    internal void SetSettlementFeedbackText(string feedback_text) =>
        _active_settlement_feedback_text = feedback_text;

    internal GodotProjectionLease<GDictionary> GetSettlementRecordLease(string settlement_id) =>
        _world_map_data_context.GetSettlementRecordLease(settlement_id);

    internal GodotProjectionLease<GArray> GetAllSettlementRecordsLease() =>
        _world_map_data_context.GetAllSettlementRecordsLease();

    internal GodotProjectionLease<GDictionary> GetCharacterInfoContextLease() =>
        ProjectPlainPayloadLease(
            _active_character_info_context,
            "GameRuntimeFacade.active_character_info_context"
        );

    public IReadOnlyDictionary<string, object> GetCharacterInfoContextSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_active_character_info_context);

    public string GetActiveWarehouseEntryLabel() => _active_warehouse_entry_label;

    internal void SetActiveWarehouseEntryLabel(string entry_label) =>
        _active_warehouse_entry_label = entry_label;

    internal GodotProjectionLease<GDictionary> GetShopWindowDataLease() =>
        _settlement_command_handler.GetShopWindowDataLease();

    public IReadOnlyDictionary<string, object> GetShopWindowDataSnapshotPlain() =>
        _settlement_command_handler.GetShopWindowDataSnapshotPlain();

    internal GodotProjectionLease<GDictionary> GetContractBoardWindowDataLease() =>
        _settlement_command_handler.GetContractBoardWindowDataLease();

    public IReadOnlyDictionary<string, object> GetContractBoardWindowDataSnapshotPlain() =>
        _settlement_command_handler.GetContractBoardWindowDataSnapshotPlain();

    public IReadOnlyDictionary<string, object> GetNpcQuestOfferWindowDataSnapshotPlain() =>
        _settlement_command_handler.GetNpcQuestOfferWindowDataSnapshotPlain();

    public IReadOnlyDictionary<string, object> GetBountyBoardWindowDataSnapshotPlain() =>
        _settlement_command_handler.GetBountyBoardWindowDataSnapshotPlain();

    internal GodotProjectionLease<GDictionary> GetForgeWindowDataLease() =>
        _settlement_command_handler.GetForgeWindowDataLease();

    public IReadOnlyDictionary<string, object> GetForgeWindowDataSnapshotPlain() =>
        _settlement_command_handler.GetForgeWindowDataSnapshotPlain();

    internal void SetActiveContractBoardContext(GDictionary context) =>
        ReplacePlainPayload(
            _active_contract_board_context,
            context,
            "GameRuntimeFacade.active_contract_board_context"
        );

    internal void SetActiveNpcQuestOfferContext(NpcQuestOfferWindowData data) =>
        _active_npc_quest_offer_data = data;

    internal NpcQuestOfferWindowData GetActiveNpcQuestOfferData() =>
        _active_npc_quest_offer_data;

    internal void SetActiveBountyBoardContext(BountyBoardWindowData data) =>
        _active_bounty_board_data = data;

    internal BountyBoardWindowData GetActiveBountyBoardData() =>
        _active_bounty_board_data;

    internal void SetActiveShopContext(GDictionary context) =>
        ReplacePlainPayload(_active_shop_context, context, "GameRuntimeFacade.active_shop_context");

    internal void SetActiveForgeContext(GDictionary context) =>
        ReplacePlainPayload(
            _active_forge_context,
            context,
            "GameRuntimeFacade.active_forge_context"
        );

    internal void ClearActiveContractBoardContext() => _active_contract_board_context.Clear();

    internal void ClearActiveNpcQuestOfferContext() => _active_npc_quest_offer_data = null;

    internal void ClearActiveBountyBoardContext() => _active_bounty_board_data = null;

    internal void ClearActiveShopContext() => _active_shop_context.Clear();

    internal void ClearActiveForgeContext() => _active_forge_context.Clear();

    internal GodotProjectionLease<GDictionary> GetActiveContractBoardContextLease() =>
        ProjectPlainPayloadLease(
            _active_contract_board_context,
            "GameRuntimeFacade.active_contract_board_context"
        );

    internal IReadOnlyDictionary<string, object> GetActiveContractBoardContextPlain() =>
        RuntimePlainPayload.CloneDictionary(_active_contract_board_context);

    internal GodotProjectionLease<GDictionary> GetActiveShopContextLease() =>
        ProjectPlainPayloadLease(
            _active_shop_context,
            "GameRuntimeFacade.active_shop_context"
        );

    internal IReadOnlyDictionary<string, object> GetActiveShopContextPlain() =>
        RuntimePlainPayload.CloneDictionary(_active_shop_context);

    internal GodotProjectionLease<GDictionary> GetActiveForgeContextLease() =>
        ProjectPlainPayloadLease(
            _active_forge_context,
            "GameRuntimeFacade.active_forge_context"
        );

    internal IReadOnlyDictionary<string, object> GetActiveForgeContextPlain() =>
        RuntimePlainPayload.CloneDictionary(_active_forge_context);

    internal GodotProjectionLease<GDictionary> GetStagecoachWindowDataLease() =>
        _settlement_command_handler.GetStagecoachWindowDataLease();

    public IReadOnlyDictionary<string, object> GetStagecoachWindowDataSnapshotPlain() =>
        _settlement_command_handler.GetStagecoachWindowDataSnapshotPlain();

    internal void SetActiveStagecoachContext(GDictionary context) =>
        ReplacePlainPayload(
            _active_stagecoach_context,
            context,
            "GameRuntimeFacade.active_stagecoach_context"
        );

    internal void ClearActiveStagecoachContext() => _active_stagecoach_context.Clear();

    internal GodotProjectionLease<GDictionary> GetActiveStagecoachContextLease() =>
        ProjectPlainPayloadLease(
            _active_stagecoach_context,
            "GameRuntimeFacade.active_stagecoach_context"
        );

    internal IReadOnlyDictionary<string, object> GetActiveStagecoachContextPlain() =>
        RuntimePlainPayload.CloneDictionary(_active_stagecoach_context);

    public GDictionary GetWarehouseWindowData() =>
        _party_state != null ? _warehouse_handler.GetWarehouseWindowData() : new GDictionary();

    public IReadOnlyDictionary<string, object> GetWarehouseWindowDataSnapshotPlain() =>
        _party_state != null
            ? _warehouse_handler.GetWarehouseWindowDataSnapshotPlain()
            : new Dictionary<string, object>(StringComparer.Ordinal);

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

    public StringName GetActiveBattleEncounterId() => _active_battle_encounter_id;

    public string GetActiveBattleEncounterName() => _active_battle_encounter_name;

    public Vector2I GetBattleSelectedCoord() => _battle_selected_coord;

    public string GetLastAdvanceBattleRefreshMode() =>
        BattleRefreshModes.ToPayloadValue(_last_advance_battle_refresh_mode);

    internal BattlePresentationDelta GetLastAdvanceBattlePresentationDelta() =>
        _last_advance_battle_presentation_delta;

    internal BattlePresentationDelta GetLastCommandBattlePresentationDelta() =>
        _last_command_battle_presentation_delta;

    internal void ResetLastCommandBattlePresentationDelta() =>
        _last_command_battle_presentation_delta = BattlePresentationDelta.None;

    internal void CaptureLastCommandBattlePresentationDelta(BattleEventBatch batch) =>
        _last_command_battle_presentation_delta = BattlePresentationDeltaFactory.Create(batch);

    public StringName GetSelectedBattleSkillId() => _selected_battle_skill_id;

    public StringName GetSelectedBattleSkillEntryId() => _selected_battle_skill_entry_id;

    public StringName GetSelectedBattleSkillVariantId() => _selected_battle_skill_variant_id;

    internal void SetBattleSelectionSkillEntryId(StringName skillEntryId) =>
        _selected_battle_skill_entry_id = skillEntryId;

    internal void SetBattleSelectionSkillId(StringName skill_id) =>
        _selected_battle_skill_id = skill_id;

    internal void SetBattleSelectionSkillVariantId(StringName variant_id) =>
        _selected_battle_skill_variant_id = variant_id;

    internal StringName GetBattleSelectionLastManualUnitId() => _last_manual_battle_unit_id;

    internal void SetBattleSelectionLastManualUnitId(StringName unit_id) =>
        _last_manual_battle_unit_id = unit_id;

    internal IReadOnlyList<Vector2I> GetBattleSelectionTargetCoordsStateTyped() =>
        _battle_selection_state.queued_target_coords;

    internal void SetBattleSelectionTargetCoordsStateTyped(IEnumerable<Vector2I> targetCoords) =>
        _battle_selection_state.SetTargetCoords(targetCoords ?? Array.Empty<Vector2I>());

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

    internal string GetBattleSkillCastBlockMessage(
        BattleUnitState active_unit,
        StringName skill_id
    ) =>
        _battle_runtime != null
            ? _battle_runtime.GetSkillCastBlockMessage(active_unit, skill_id)
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

    internal void CloseNpcQuestOfferModal() =>
        _settlement_command_handler.OnNpcQuestOfferWindowClosed();

    internal void CloseBountyBoardModal() =>
        _settlement_command_handler.OnBountyBoardWindowClosed();

    internal string FormatCoord(Vector2I coord) => FormatCoordInternal(coord);

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
        GetContentCatalogTyped() != null
            ? GetContentCatalogTyped().GetSkillDefinitionsTyped()
            : new Dictionary<StringName, SkillDefinition>();

    internal IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() =>
        GetContentCatalogTyped() != null
            ? GetContentCatalogTyped().GetItemDefsTyped()
            : new Dictionary<StringName, ItemDefinition>();

    internal ISkillCatalog GetSkillCatalogTyped() =>
        GetContentCatalogTyped()?.GetSkillCatalogTyped();

    public string GetSelectedBattleSkillName() =>
        _battle_session_facade.GetSelectedBattleSkillName();

    public string GetSelectedBattleSkillVariantName() =>
        _battle_session_facade.GetSelectedBattleSkillVariantName();

    public IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoords() =>
        _battle_session_facade.GetSelectedBattleSkillTargetCoords();

    public IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIds() =>
        _battle_session_facade.GetSelectedBattleSkillTargetUnitIds();

    public IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoordsSnapshotPlain() =>
        _battle_session_facade.GetSelectedBattleSkillTargetCoordsSnapshotPlain();

    public IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain() =>
        _battle_session_facade.GetSelectedBattleSkillTargetUnitIdsSnapshotPlain();

    public IReadOnlyList<Vector2I> GetSelectedBattleSkillValidTargetCoords() =>
        _battle_session_facade.GetSelectedBattleSkillValidTargetCoords();

    public IReadOnlyList<Vector2I> GetBattleMovementReachableCoords() =>
        _battle_session_facade.GetBattleMovementReachableCoords();

    public IReadOnlyList<Vector2I> GetBattleOverlayTargetCoords() =>
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

    public IReadOnlyDictionary<string, int> GetBattleTerrainCountsSnapshotTyped() =>
        _battle_session_facade.GetBattleTerrainCountsSnapshotTyped();

    internal GodotProjectionLease<GDictionary> GetLastBattleLootSnapshotLease() =>
        ProjectPlainPayloadLease(
            _last_battle_loot_snapshot,
            "GameRuntimeFacade.last_battle_loot_snapshot"
        );

    public IReadOnlyDictionary<string, object> GetLastBattleLootSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_last_battle_loot_snapshot);

    public PendingCharacterReward GetActiveReward() => _active_reward;

    public PendingCharacterReward GetSnapshotReward() =>
        _active_reward ?? _party_state?.GetNextPendingCharacterReward();

    public int GetPendingRewardCount() =>
        _party_state != null ? _party_state.pending_character_rewards.Count : 0;

    public IReadOnlyDictionary<string, object> GetCurrentPromotionPromptSnapshotPlain()
    {
        if (_pending_promotion_prompt.Count > 0)
            return GetPendingPromotionPromptSnapshotPlain();
        return GetPendingWorldPromotionPromptSnapshotPlain();
    }

    internal IReadOnlyDictionary<string, object> GetPendingPromotionPromptSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_pending_promotion_prompt);

    internal bool HasPendingPromotionPrompt() => _pending_promotion_prompt.Count > 0;

    internal IReadOnlyDictionary<string, object> GetPendingWorldPromotionPromptSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_pending_world_promotion_prompt);

    internal bool HasPendingWorldPromotionPrompt() =>
        _pending_world_promotion_prompt.Count > 0;


    public bool IsModalWindowOpen() => IsModalWindowOpenInternal();

    internal void SetRuntimeBattleState(BattleState state)
    {
        _battle_state = state;
        _battle_auto_tick_remainder_msec = 0;
    }

    private void ClearRuntimeBattleStateReference()
    {
        _battle_state = null;
    }

    internal void SetRuntimeBattleSelectedCoord(Vector2I coord) => _battle_selected_coord = coord;

    internal void SetRuntimeActiveModalKind(RuntimeModalKind modalKind) =>
        _active_modal_kind = modalKind;

    internal void SetPendingBattleStartPrompt(GDictionary prompt) =>
        ReplacePlainPayload(
            _pending_battle_start_prompt,
            prompt,
            "GameRuntimeFacade.pending_battle_start_prompt"
        );

    internal void SetPendingPromotionPromptPlain(
        IReadOnlyDictionary<string, object> prompt
    ) => ReplacePlainPayload(_pending_promotion_prompt, prompt);

    internal void ClearPendingPromotionPrompt() => _pending_promotion_prompt.Clear();

    internal void SetPendingWorldPromotionPromptStatePlain(
        IReadOnlyDictionary<string, object> prompt
    ) => ReplacePlainPayload(_pending_world_promotion_prompt, prompt);

    internal void ClearPendingWorldPromotionPromptState() =>
        _pending_world_promotion_prompt.Clear();

    internal void SetActiveRewardState(PendingCharacterReward reward) => _active_reward = reward;

    internal void ClearActiveRewardState() => _active_reward = null;

    internal void SetActiveCharacterInfoContext(GDictionary context) =>
        ReplacePlainPayload(
            _active_character_info_context,
            context,
            "GameRuntimeFacade.active_character_info_context"
        );

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
                GameLogLevel.Info,
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

    public bool advance(float delta)
    {
        _last_advance_battle_refresh_mode = BattleRefreshMode.None;
        _last_advance_battle_presentation_delta = BattlePresentationDelta.None;
        if (_generation_definition == null)
            return false;
        if (_try_complete_pending_battle_start())
        {
            _last_advance_battle_refresh_mode = BattleRefreshMode.Full;
            _last_advance_battle_presentation_delta = BattlePresentationDelta.Full;
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
                BattlePresentationDelta presentationDelta =
                    BattlePresentationDeltaFactory.Create(batch);
                ApplyBattleBatch(batch);
                _last_advance_battle_presentation_delta = presentationDelta;
                _last_advance_battle_refresh_mode = presentationDelta.ToLegacyRefreshMode();
                return true;
            }
            int currentTu =
                _battle_state?.timeline != null ? _battle_state.timeline.current_tu : -1;
            if (currentTu != previousTu)
            {
                _last_advance_battle_refresh_mode = BattleRefreshMode.Overlay;
                _last_advance_battle_presentation_delta = BattlePresentationDelta.Overlay;
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

    public IReadOnlyDictionary<string, object> BuildHeadlessSnapshotPlain() =>
        _snapshot_builder.BuildHeadlessSnapshotPlain();

    internal GodotProjectionLease<GDictionary> BuildHeadlessSnapshotLease() =>
        _snapshot_builder.BuildHeadlessSnapshotLease();

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

    internal GodotProjectionLease<GDictionary> GetSettlementStateLease(string settlement_id) =>
        _world_map_data_context.GetSettlementStateLease(settlement_id);

    internal WorldMapSettlementStateData GetSettlementStateData(string settlement_id) =>
        _world_map_data_context.GetSettlementStateData(settlement_id);

    internal bool IsSettlementVisited(string settlement_id) =>
        _world_map_data_context.IsSettlementVisited(settlement_id);

    internal bool MarkSettlementVisited(string settlement_id) =>
        _world_map_data_context.MarkSettlementVisited(settlement_id);

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
                // World state syncs to the save layer lazily at flush
                // (_flush_game_state_with_world_sync), not via a full inline push here.
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
            OpenWorldEventPrompt(triggeredEvent);
            if (playerPersistError != (int)Error.Ok)
                UpdateStatusInternal(
                    $"{ResolveWorldEventDisplayName(triggeredEvent, "事件入口")} 已显现，但当前位置持久化失败。"
                );
            return;
        }

        var encounterAnchor = _get_encounter_anchor_at(_player_coord);
        if (encounterAnchor != null)
        {
            _game_session.SetBattleSaveLock(true);
            int playerPersistError = _game_session.SetPlayerCoord(_player_coord);
            // No inline world push: saves are locked through the battle, and the
            // post-battle flush below (or on resolution) syncs current world data.
            _StartBattle(encounterAnchor);
            if (!IsBattleActive() && !HasPendingBattleGenerationRequest())
            {
                _game_session.SetBattleSaveLock(false);
                int flushError = _flush_game_state_with_world_sync();
                UpdateStatusInternal(
                    playerPersistError != (int)Error.Ok || flushError != (int)Error.Ok
                        ? "遭遇战未能开始，且玩家位置或世界时间持久化失败。"
                        : "遭遇战未能开始，已保留玩家当前位置与世界时间。"
                );
            }
            return;
        }

        int playerError = _game_session.SetPlayerCoord(_player_coord);
        // World data is no longer pushed to the save layer on every step (that did a
        // full ToDictionary + NormalizeWorldData/Duplicate per move). The typed data
        // context is the live source of truth; it is synced into the save layer only
        // just before a flush via _flush_game_state_with_world_sync().
        UpdateStatusInternal(
            playerError == (int)Error.Ok
                ? $"玩家移动到 {FormatCoordInternal(_player_coord)}，视野与世界时间已刷新。"
                : $"玩家移动到 {FormatCoordInternal(_player_coord)}，但大地图位置持久化失败。"
        );
    }

    // Sync the typed world data into the save layer immediately before flushing, so
    // deferring the per-move SetWorldData never persists stale world state.
    private int _flush_game_state_with_world_sync()
    {
        _materialize_active_world_state_to_root();
        _game_session.SetWorldData(_world_map_data_context.RootRuntimeData);
        return _game_session.FlushGameState();
    }

    private void _AdvanceWorldTimeBySteps(int delta_steps)
    {
        WorldTimeAdvanceResult advanceResult = WorldTimeSystem.AdvanceWorldStep(
            _world_map_data_context.GetWorldStep(),
            delta_steps
        );
        if (advanceResult.IsValid)
        {
            _world_map_data_context.SetWorldStep(advanceResult.new_step);
        }
        bool encounterGrowthChanged = _wild_encounter_growth_system.ApplyStepAdvance(
            _world_map_data_context.GetActiveEncounterAnchors(),
            advanceResult.old_step,
            advanceResult.new_step,
            _wild_encounter_roster_definitions
        );
        if (encounterGrowthChanged)
            // Growth only changed anchor growth_stage (positions unchanged), so skip
            // the O(all markers) coord-lookup rebuild.
            _world_map_data_context.SyncActiveWorldPayloadFromTypedState(rebuildLookups: false);
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
            if (_wild_encounter_growth_system.ApplyBattleVictory(
                encounterAnchor,
                _world_map_data_context.GetWorldStep(),
                _wild_encounter_roster_definitions
            ))
                _world_map_data_context.SyncActiveWorldPayloadFromTypedState();
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
        if (_world_map_data_context.active_generation_definition == null)
            return;
        string leaderMemberId = "player_main";
        if (_party_state != null && _party_state.leader_member_id != "")
            leaderMemberId = _party_state.leader_member_id.ToString();
        var visionSource = new VisionSourceData
        {
            source_id = leaderMemberId,
            center = _player_coord,
            range = _world_map_data_context.active_generation_definition.PlayerVisionRange,
            faction_id = _player_faction_id,
        };
        _fog_system.RebuildVisibilityForFaction(_player_faction_id, new[] { visionSource });
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
        if (_fog_system.IsVisible(coord, _player_faction_id) && _try_open_resource_harvest_at(coord))
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

    // Clicking a resource node cell opens a harvest confirmation. Like settlements it
    // requires visibility, but harvesting is a hands-on action, so the player must also
    // be standing on or next to the node (Chebyshev distance <= 1).
    private bool _try_open_resource_harvest_at(Vector2I coord)
    {
        WorldMapResourceNodeData node = _world_map_data_context.GetResourceNodeAt(coord);
        if (node == null || !node.Exists)
            return false;
        if (node.RemainingCharges <= 0)
        {
            UpdateStatusInternal($"{node.DisplayName} 的资源已经采集殆尽。");
            return true;
        }
        int distance = Mathf.Max(
            Mathf.Abs(coord.X - _player_coord.X),
            Mathf.Abs(coord.Y - _player_coord.Y)
        );
        if (distance > 1)
        {
            UpdateStatusInternal($"距离 {node.DisplayName} 太远，靠近后才能采集。");
            return true;
        }
        _pending_harvest_coord = coord;
        _active_modal_kind = RuntimeModalKind.ResourceHarvestConfirm;
        UpdateStatusInternal($"已靠近 {node.DisplayName}，确认后可采集。");
        return true;
    }

    public GDictionary GetPendingResourceHarvestPrompt()
    {
        WorldMapResourceNodeData node =
            _world_map_data_context.GetResourceNodeAt(_pending_harvest_coord);
        if (node == null || !node.Exists)
            return new GDictionary();
        string itemName = GetItemDisplayName(node.YieldItemId);
        return new GDictionary
        {
            ["title"] = $"采集 · {node.DisplayName}",
            ["description"] =
                $"在此采集 1 次可获得【{itemName}】。\n剩余可采集次数：{node.RemainingCharges} / {node.MaxCharges}。",
            ["confirm_text"] = "采集",
            ["cancel_text"] = "离开",
        };
    }

    private RuntimeCommandResult HarvestPendingResourceNodeTyped()
    {
        if (_active_modal_kind != RuntimeModalKind.ResourceHarvestConfirm)
            return BuildCommandErrorResult("当前没有待确认的采集点。");
        Vector2I coord = _pending_harvest_coord;
        WorldMapResourceNodeData node = _world_map_data_context.GetResourceNodeAt(coord);
        if (node == null || !node.Exists)
        {
            _clear_pending_resource_harvest();
            return BuildCommandErrorResult("采集点已不存在。");
        }
        if (node.RemainingCharges <= 0)
        {
            _clear_pending_resource_harvest();
            return BuildCommandErrorResult($"{node.DisplayName} 的资源已经采集殆尽。");
        }

        string itemName = GetItemDisplayName(node.YieldItemId);
        var addResult = _party_warehouse_service.AddItemTyped(node.YieldItemId, 1);
        if (!addResult.ItemFound)
            return BuildCommandErrorResult($"采集失败：未找到【{node.YieldItemId}】的物品定义。");
        if (addResult.AddedQuantity <= 0)
            return BuildCommandErrorResult("共享仓库已满，无法采集更多资源。");

        if (!_world_map_data_context.TryHarvestResourceNodeAt(coord))
        {
            // Node mutation should not fail after the visibility/charge checks above,
            // but if it does, undo the warehouse add so nothing is granted for free.
            _party_warehouse_service.RemoveItemTyped(node.YieldItemId, addResult.AddedQuantity);
            return BuildCommandErrorResult("采集失败：无法更新采集点状态。");
        }

        int worldPersistError = (int)Error.Unavailable;
        if (_game_session != null)
        {
            _materialize_active_world_state_to_root();
            worldPersistError = _game_session.SetWorldData(
                _world_map_data_context.RootRuntimeData
            );
        }
        int partyPersistError = PersistPartyStateInternal();
        int commitError = (int)Error.Ok;
        if (worldPersistError == (int)Error.Ok && partyPersistError == (int)Error.Ok)
            commitError = CommitRuntimeStateInternal("resource_harvest");

        WorldMapResourceNodeData afterNode = _world_map_data_context.GetResourceNodeAt(coord);
        int remaining = afterNode != null && afterNode.Exists ? afterNode.RemainingCharges : 0;
        _clear_pending_resource_harvest();

        if (
            worldPersistError != (int)Error.Ok
            || partyPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            UpdateStatusInternal($"已采集 1 个【{itemName}】，但状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }

        string tail = remaining <= 0 ? "，采集点已耗尽。" : $"，剩余可采集 {remaining} 次。";
        UpdateStatusInternal($"已采集 1 个【{itemName}】{tail}");
        return BuildCommandOkResult();
    }

    private RuntimeCommandResult CancelPendingResourceHarvestTyped()
    {
        _clear_pending_resource_harvest();
        UpdateStatusInternal("已离开采集点。");
        return BuildCommandOkResult();
    }

    private void _clear_pending_resource_harvest()
    {
        _pending_harvest_coord = new Vector2I(-1, -1);
        if (_active_modal_kind == RuntimeModalKind.ResourceHarvestConfirm)
            _active_modal_kind = RuntimeModalKind.None;
    }

    private bool _try_open_character_info_at_world_coord(Vector2I coord)
    {
        WorldMapNpcData npc = _world_map_data_context.GetWorldNpcAt(coord);
        if (!npc.HasValidCharacterInfoFields)
            return false;
        string displayName = npc.DisplayName;
        string factionLabel = FormatFactionLabel(npc.FactionId);
        using GodotProjectionLease<GDictionary> npcLease =
            WorldMapDataProjection.ProjectLease(npc);
        ReplacePlainPayload(
            _active_character_info_context,
            new GDictionary
            {
                ["display_name"] = displayName,
                ["meta_label"] = _build_character_info_meta_label("世界 NPC", factionLabel, coord),
                ["sections"] = _build_world_character_info_sections(
                    npcLease.Value,
                    coord,
                    factionLabel
                ),
                ["status_label"] = "可见提示单位",
                ["source"] = "world",
            },
            "GameRuntimeFacade.active_character_info_context"
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
        ReplacePlainPayload(
            _active_character_info_context,
            new GDictionary
            {
                ["display_name"] = displayName,
                ["meta_label"] = _build_character_info_meta_label(typeLabel, factionLabel, unit.coord),
                ["sections"] = _build_battle_character_info_sections(unit, typeLabel, factionLabel),
                ["status_label"] = statusLabel,
                ["source"] = "battle",
                ["unit_id"] = unitId,
            },
            "GameRuntimeFacade.active_character_info_context"
        );
        var fatePayload = _build_battle_character_info_fate_payload(unit);
        if (fatePayload.Count > 0)
        {
            PutPlainPayloadValue(
                _active_character_info_context,
                "fate",
                Variant.From(fatePayload),
                "GameRuntimeFacade.active_character_info_context"
            );
        }
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
        return batch.ChangeFlags != BattleChangeFlags.None
            || batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.ChangedUnitIdsTyped.Count > 0
            || batch.ChangedCoordsTyped.Count > 0
            || batch.LogLinesTyped.Count > 0
            || batch.ProgressionDeltaCount > 0;
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
        _pending_command_battle_batches.Add(
            RuntimePlainPayload.NormalizeDictionary(
                _build_battle_batch_log_context(batch),
                $"GameRuntimeFacade.pending_command_battle_batches[{_pending_command_battle_batches.Count}]"
            )
        );
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

    internal IReadOnlyDictionary<string, object> BuildRuntimePromotionPromptPlain(
        CharacterProgressionDelta delta
    ) =>
        BuildRuntimePromotionPromptPlain(delta, "确认后将在战斗中立即生效。");

    internal IReadOnlyDictionary<string, object> BuildRuntimePromotionPromptPlain(
        CharacterProgressionDelta delta,
        string selection_hint
    ) =>
        _battle_session_facade.BuildPromotionPromptPlain(delta, selection_hint);

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
            _equipment_trait_roll_service?.Dispose();
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
            _equipment_trait_roll_service?.Dispose();
            IEnumerable<TraitDefinition> traitDefs =
                contentCatalog != null
                    ? contentCatalog.GetTraitDefsTyped().Values
                    : Array.Empty<TraitDefinition>();
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
        SetupPartyWarehouseService(
            service,
            party_state,
            new Dictionary<StringName, ItemDefinition>()
        );

    internal void SetupPartyWarehouseService(
        PartyWarehouseService service,
        PartyState party_state,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (service == null)
            return;
        service.Setup(
            party_state,
            itemDefinitions ?? new Dictionary<StringName, ItemDefinition>(),
            GetEquipmentInstanceIdAllocator(),
            GetEquipmentTraitRollService()
        );
    }

    internal void SyncPartyStateServices()
    {
        var typedItemDefs =
            GetContentCatalogTyped() != null
                ? GetContentCatalogTyped().GetItemDefsTyped()
                : new Dictionary<StringName, ItemDefinition>();
        var typedSkillDefinitions =
            GetContentCatalogTyped() != null
                ? GetContentCatalogTyped().GetSkillDefinitionsTyped()
                : new Dictionary<StringName, SkillDefinition>();
        _character_management?.SetPartyState(_party_state);
        SetupPartyWarehouseService(_party_warehouse_service, _party_state, typedItemDefs);
        if (_party_item_use_service != null && _game_session != null)
            _party_item_use_service.Setup(
                _party_state,
                typedItemDefs,
                typedSkillDefinitions,
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
                _materialize_active_world_state_to_root();
                return _world_map_data_context.RootRuntimeData;
            },
            () => _player_coord
        );

    internal int CommitRuntimeStateInternal(StringName reason) =>
        _game_session != null
            ? _game_session.CommitRuntimeState(reason)
            : (int)Error.Unavailable;

    internal int FlushCanonicalRuntimeState(StringName reason)
    {
        if (_game_session == null || !_game_session.HasActiveWorld())
            return (int)Error.Ok;
        if (_game_session.IsBattleSaveLocked())
            return (int)Error.Busy;

        RuntimeCommitResult result = new RuntimeTransaction()
            .MarkPartyChanged()
            .MarkWorldChanged()
            .MarkPlayerCoordChanged()
            .Commit(
                _game_session,
                BuildRuntimeStateSource(),
                reason == "" ? new StringName("runtime.flush_canonical") : reason
            );
        return result.FirstError();
    }

    private void CommitPendingRuntimeStateOnDispose()
    {
        if (_game_session == null)
            return;
        if (_game_session.IsBattleSaveLocked())
            return;
        int commitError = FlushCanonicalRuntimeState("runtime.dispose");
        if (commitError == (int)Error.Ok)
            return;
        _log_runtime_event(
            GameLogLevel.Warning,
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
        ClearRuntimeBattleStateReference();
        _battle_auto_tick_remainder_msec = 0;
        _battle_selected_coord = new Vector2I(-1, -1);
        _active_battle_encounter_id = "";
        _active_battle_encounter_name = "";
        _selected_coord = _player_coord;
    }

    private void ActivateGameOver(GDictionary context)
    {
        ReplacePlainPayload(
            _active_game_over_context,
            context,
            "GameRuntimeFacade.game_over_context"
        );
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
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.DisplayName))
            return itemDef.DisplayName;
        return item_id.ToString();
    }

    internal string GetSkillDisplayName(StringName skill_id)
    {
        SkillDefinition skillDefinition = null;
        GameContentCatalog contentCatalog = GetContentCatalogTyped();
        if (contentCatalog != null)
            contentCatalog.GetSkillDefinitionsTyped().TryGetValue(skill_id, out skillDefinition);
        if (skillDefinition != null && !string.IsNullOrEmpty(skillDefinition.DisplayName))
            return skillDefinition.DisplayName;
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
            ["loot_overflow_entries"] = lootCommitResult?.ProjectOverflowEntries()
                ?? new GArray(),
            ["quest_progress_summary"] = _quest_progress_summary_to_string_dict(questSummary),
            ["save_skipped"] = saveSkipped,
            ["party_persist_error"] = partyPersistError,
            ["world_persist_error"] = worldPersistError,
            ["flush_error"] = flushError,
        });
    }
}
