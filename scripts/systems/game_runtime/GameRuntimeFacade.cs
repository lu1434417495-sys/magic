using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class GameRuntimeFacade : RefCounted, IGameRuntimeSnapshotSource
{
    private static readonly StringName EncounterKindSettlement = "settlement";
    private const float WorldMoveRepeatInterval = 0.5f;
    private const int BattleAutoAdvanceTickMsec = 1000;
    private const int MaxCommandWorldMoveCount = 256;
    private const string PartyWarehouseInteractionId = "party_warehouse";
    private const string BattleLoadingModalId = "battle_loading";

    private sealed class RuntimeCommandResult
    {
        public bool Ok { get; private set; }
        public string Message { get; private set; } = "";
        public string BattleRefreshMode { get; private set; } = "";

        public static RuntimeCommandResult Success(
            string message = "",
            string battleRefreshMode = ""
        )
        {
            return new RuntimeCommandResult
            {
                Ok = true,
                Message = message ?? "",
                BattleRefreshMode = battleRefreshMode ?? "",
            };
        }

        public static RuntimeCommandResult Failure(string message)
        {
            return new RuntimeCommandResult { Ok = false, Message = message ?? "" };
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary { ["ok"] = Ok, ["message"] = Message };
            if (Ok)
                result["battle_refresh_mode"] = BattleRefreshMode;
            return result;
        }
    }

    public WorldMapGenerationConfig _generation_config;
    public GameSession _game_session;
    public WorldMapGridSystem _grid_system = new();
    public WorldMapFogSystem _fog_system = new();
    public BattleGridService _battle_grid_service = new();
    public CharacterManagementModule _character_management = new();
    public PartyWarehouseService _party_warehouse_service = new();
    public EquipmentDropService _equipment_drop_service = new();
    public PartyItemUseService _party_item_use_service = new();
    public PartyEquipmentService _party_equipment_service = new();
    public EncounterRosterBuilder _encounter_roster_builder = new();
    public WildEncounterGrowthSystem _wild_encounter_growth_system = new();
    public BattleRuntimeModule _battle_runtime;
    public Vector2I _player_coord = Vector2I.Zero;
    public Vector2I _selected_coord = Vector2I.Zero;
    public bool _settlement_entry_active;
    public Vector2I _settlement_entry_source_coord = new(-1, -1);
    public Vector2I _settlement_entry_target_coord = new(-1, -1);
    public string _player_faction_id = "player";
    public WorldMapDataContext _world_map_data_context = new();
    private readonly GameRuntimePendingSubmapPrompt _pending_submap_prompt = new();
    public GDictionary _pending_battle_start_prompt = new();
    private readonly GameRuntimePendingBattleGenerationRequest _pending_battle_generation_request = new();
    public PartyState _party_state;
    public BattleState _battle_state;
    public int _battle_auto_tick_remainder_msec;
    public GameRuntimeSnapshotBuilder _snapshot_builder = new();
    public GameRuntimeCommandLogger _command_logger = new();
    public GameRuntimeBattleWritebackService _battle_writeback_service = new();
    public GameRuntimeBattleLootCommitService _battle_loot_commit_service = new();
    public GameRuntimeCharacterInfoBuilder _character_info_builder = new();
    public BattleSessionFacade _battle_session_facade = new();
    public GameRuntimeBattleSelection _battle_selection = new();
    private readonly GameRuntimeBattleSelectionState _battle_selection_state = new();
    public GameRuntimeSettlementCommandHandler _settlement_command_handler = new();
    public GameRuntimeWarehouseHandler _warehouse_handler = new();
    public GameRuntimePartyCommandHandler _party_command_handler = new();
    public GameRuntimeRewardFlowHandler _reward_flow_handler = new();
    public GameRuntimeQuestCommandHandler _quest_command_handler = new();
    public StringName _active_battle_encounter_id = "";
    public string _active_battle_encounter_name = "";
    public GDictionary _pending_promotion_prompt = new();
    public GArray _held_world_move_keys = new();
    public float _world_move_repeat_timer;
    public PendingCharacterReward _active_reward;
    public GDictionary _pending_world_promotion_prompt = new();
    public string _active_modal_id = "";
    public string _active_warehouse_entry_label = "";
    public string _active_settlement_id = "";
    public string _active_settlement_feedback_text = "";
    public GDictionary _active_contract_board_context = new();
    public GDictionary _active_shop_context = new();
    public GDictionary _active_forge_context = new();
    public GDictionary _active_stagecoach_context = new();
    public string _current_status_message = "";
    public string _last_advance_battle_refresh_mode = "";
    public GDictionary _last_battle_loot_snapshot = new();
    public GArray _pending_command_battle_batches = new();
    public GDictionary _active_character_info_context = new();
    public GDictionary _active_game_over_context = new();
    public StringName _party_selected_member_id = "";
    public GDictionary _wild_encounter_rosters = new();
    private readonly Dictionary<StringName, WildEncounterRosterDef> _wild_encounter_roster_defs = new();

    public Vector2I _battle_selected_coord
    {
        get => _battle_selection_state.battle_selected_coord;
        set => _battle_selection_state.battle_selected_coord = value;
    }

    public StringName _selected_battle_skill_id
    {
        get => _battle_selection_state.selected_skill_id;
        set => _battle_selection_state.selected_skill_id = value;
    }

    public StringName _selected_battle_skill_variant_id
    {
        get => _battle_selection_state.selected_skill_variant_id;
        set => _battle_selection_state.selected_skill_variant_id = value;
    }

    public GVector2IArray _queued_battle_skill_target_coords
    {
        get => ToVector2IArray(_battle_selection_state.queued_target_coords);
        set => _battle_selection_state.SetTargetCoords(value ?? new GVector2IArray());
    }

    public GStringNameArray _queued_battle_skill_target_unit_ids
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
        _bind_runtime_sidecar_owners();
    }

    public void setup(GameSession game_session)
    {
        _game_session = game_session;
        if (_game_session == null || !_game_session.has_active_world())
            return;

        _generation_config = _game_session.get_generation_config();
        if (_generation_config == null)
            return;

        _world_map_data_context.bind_root_world_data(
            _game_session.get_world_data()
        );
        _wild_encounter_rosters = _game_session.get_wild_encounter_rosters().Duplicate();
        RebuildWildEncounterRosterDefIndex();
        _encounter_roster_builder.setup(
            _wild_encounter_rosters,
            _game_session.get_enemy_templates()
        );
        _party_state = _game_session.get_party_state();
        _player_coord = _game_session.get_player_coord();
        _player_faction_id = _game_session.get_player_faction_id();

        _character_management.setup(
            _party_state,
            _game_session.get_skill_defs(),
            _game_session.get_profession_defs(),
            _game_session.get_achievement_defs(),
            _game_session.get_item_defs(),
            _game_session.get_quest_defs(),
            _get_equipment_instance_id_allocator(),
            _game_session.get_progression_content_bundle()
        );
        _setup_party_warehouse_service(
            _party_warehouse_service,
            _party_state,
            _game_session.get_item_defs()
        );
        _party_item_use_service.setup(
            _party_state,
            _game_session.get_item_defs(),
            _game_session.get_skill_defs(),
            _party_warehouse_service,
            _character_management
        );
        _party_equipment_service.setup(
            _party_state,
            _game_session.get_item_defs(),
            _party_warehouse_service,
            _get_equipment_instance_id_allocator()
        );
        _battle_runtime.setup(
            _character_management,
            _game_session.get_skill_defs(),
            _game_session.get_enemy_templates(),
            _game_session.get_enemy_ai_brains(),
            _encounter_roster_builder,
            _equipment_drop_service,
            _game_session.get_item_defs(),
            null,
            _get_equipment_instance_id_allocator(),
            _game_session.get_battle_special_profile_registry_snapshot()
        );

        _snapshot_builder.Setup(this);
        _command_logger.Setup(this);
        _battle_writeback_service.setup(this);
        _battle_loot_commit_service.setup(this);
        _character_info_builder.setup(this);
        _battle_session_facade.setup(this);
        _battle_selection_state.ResetForBattleEnd();
        _battle_selection.setup(this);
        _settlement_command_handler.setup(this);
        _warehouse_handler.setup(this);
        _party_command_handler.setup(this);
        _reward_flow_handler.setup(this);
        _quest_command_handler.setup(this);

        _sync_active_world_context();
        _selected_coord = _player_coord;
        _refresh_fog();
        _active_modal_id = "";
        _active_settlement_id = "";
        _active_settlement_feedback_text = "";
        _clear_settlement_entry_context();
        _active_contract_board_context.Clear();
        _active_shop_context.Clear();
        _active_forge_context.Clear();
        _active_stagecoach_context.Clear();
        _last_advance_battle_refresh_mode = "";
        _last_battle_loot_snapshot.Clear();
        _active_character_info_context.Clear();
        _active_game_over_context.Clear();
        _party_selected_member_id = "";
        _active_warehouse_entry_label = "";
        _pending_submap_prompt.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();

        if (_is_main_character_dead())
        {
            _activate_game_over(_build_main_character_game_over_context());
            _update_status(
                DictString(_active_game_over_context, "description", "主角已阵亡，本次旅程结束。")
            );
            return;
        }
        if (is_submap_active())
        {
            _update_status(
                $"已载入 {get_active_map_display_name()}。{get_submap_return_hint_text()}"
            );
            return;
        }
        string startSettlementName = _world_map_data_context.get_player_start_settlement_name();
        _update_status(
            startSettlementName.Length == 0
                ? "大地图已载入。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。"
                : $"大地图已载入，初始村庄为 {startSettlementName}。方向键/WASD 可按住持续移动，点击可见据点或按 Enter 打开据点窗口，按 P 打开队伍管理，右键人物可查看信息。"
        );
    }

    private void RebuildWildEncounterRosterDefIndex()
    {
        _wild_encounter_roster_defs.Clear();
        foreach (Variant rawKey in _wild_encounter_rosters.Keys)
        {
            if (
                _wild_encounter_rosters[rawKey].AsGodotObject()
                is not WildEncounterRosterDef roster
            )
                continue;
            if (roster.profile_id == "")
                continue;
            _wild_encounter_roster_defs[roster.profile_id] = roster;
        }
    }

    public void dispose()
    {
        _commit_pending_runtime_state_on_dispose();
        if (_battle_runtime != null)
            _battle_runtime.dispose();
        _snapshot_builder?.Dispose();
        _command_logger?.Dispose();
        _battle_writeback_service?.dispose();
        _battle_loot_commit_service?.dispose();
        _character_info_builder?.dispose();
        _battle_session_facade?.dispose();
        _battle_selection?.dispose();
        _settlement_command_handler?.dispose();
        _warehouse_handler?.dispose();
        _party_command_handler?.dispose();
        _reward_flow_handler?.dispose();
        _quest_command_handler?.dispose();

        _game_session = null;
        _generation_config = null;
        _world_map_data_context.reset();
        _pending_submap_prompt.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _wild_encounter_rosters = new GDictionary();
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
        _last_advance_battle_refresh_mode = "";
        _last_battle_loot_snapshot.Clear();
        _battle_selection_state.ResetForBattleEnd();
        _held_world_move_keys.Clear();
        _active_reward = null;
        _clear_settlement_entry_context();
    }

    public void _bind_runtime_sidecar_owners()
    {
        _snapshot_builder?.Setup(this);
        _command_logger?.Setup(this);
        _battle_writeback_service?.setup(this);
        _battle_loot_commit_service?.setup(this);
        _character_info_builder?.setup(this);
    }

    public string get_status_text() => _current_status_message;

    public GDictionary get_log_snapshot() => get_log_snapshot(30);

    public GDictionary get_log_snapshot(int limit) =>
        _game_session != null
            ? _game_session.get_log_snapshot(limit)
            : new GDictionary();

    public GArray get_recent_logs() => get_recent_logs(30);

    public GArray get_recent_logs(int limit) =>
        _game_session != null
            ? _game_session.get_recent_logs(limit)
            : new GArray();

    public string get_active_log_file_path() =>
        _game_session != null ? _game_session.get_active_log_file_path() : "";

    public string get_active_modal_id() => _active_modal_id;

    public GDictionary get_game_over_context() => _active_game_over_context.Duplicate(true);

    public string get_active_settlement_id() => _active_settlement_id;

    public string get_active_map_id() => _world_map_data_context.get_active_map_id();

    public string get_active_map_display_name() =>
        _world_map_data_context.get_active_map_display_name();

    public string get_submap_return_hint_text() =>
        _world_map_data_context.get_submap_return_hint_text();

    public GDictionary get_pending_submap_prompt() => _pending_submap_prompt.ToDictionary();

    public GDictionary get_pending_battle_start_prompt() =>
        _pending_battle_start_prompt.Duplicate(true);

    public bool is_submap_active() => _world_map_data_context.is_submap_active();

    public int get_world_step() => _world_map_data_context.get_world_step();

    public GDictionary get_selected_settlement()
    {
        var settlement = _get_settlement_at(_selected_coord);
        return settlement.Count > 0 ? settlement.Duplicate(true) : new GDictionary();
    }

    public GDictionary get_selected_world_npc()
    {
        var npc = _get_world_npc_at(_selected_coord);
        return npc.Count > 0 ? npc.Duplicate(true) : new GDictionary();
    }

    public EncounterAnchorData get_selected_encounter_anchor() =>
        _get_encounter_anchor_at(_selected_coord);

    public GDictionary get_selected_world_event()
    {
        var worldEvent = _get_world_event_at(_selected_coord);
        return worldEvent.Count > 0 ? worldEvent.Duplicate(true) : new GDictionary();
    }

    public GArray get_nearby_encounter_entries() => get_nearby_encounter_entries(8);

    public GArray get_nearby_encounter_entries(int limit)
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

    public GArray get_nearby_world_event_entries() => get_nearby_world_event_entries(8);

    public GArray get_nearby_world_event_entries(int limit)
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

    public string get_resolved_settlement_id() => _resolve_command_settlement_id();

    public WorldMapGridSystem get_grid_system() => _grid_system;

    internal WorldMapFogSystem GetFogSystem() => _fog_system;

    public bool is_world_coord_visible(Vector2I coord, string faction_id = "")
    {
        string factionId = string.IsNullOrWhiteSpace(faction_id)
            ? _player_faction_id
            : faction_id.StripEdges();
        return _fog_system.is_visible(coord, factionId);
    }

    public GDictionary get_world_data() => _world_map_data_context.get_active_world_data();

    public WorldMapGenerationConfig get_generation_config() =>
        _world_map_data_context.get_active_generation_config();

    public Vector2I get_player_coord() => _player_coord;

    public bool is_player_visible_on_world_map() => !_is_settlement_entry_hidden_on_world_map();

    public Vector2I get_selected_coord() => _selected_coord;

    public string get_player_faction_id() => _player_faction_id;

    public PartyState get_party_state() => _party_state;

    public GArray get_active_quest_states() =>
        _character_management != null
            ? UntypedQuestArray(_character_management.get_active_quest_states())
            : new GArray();

    public GArray get_claimable_quest_states() =>
        _character_management != null
            ? UntypedQuestArray(_character_management.get_claimable_quest_states())
            : new GArray();

    public GStringNameArray get_claimable_quest_ids() =>
        _character_management != null
            ? _character_management.get_claimable_quest_ids()
            : new GStringNameArray();

    public GStringNameArray get_completed_quest_ids() =>
        _character_management != null
            ? _character_management.get_completed_quest_ids()
            : new GStringNameArray();

    public GDictionary get_member_achievement_summary(StringName member_id) =>
        _character_management != null
            ? _character_management.get_member_achievement_summary(member_id)
            : new GDictionary();

    public AttributeSnapshot get_member_attribute_snapshot(StringName member_id) =>
        _character_management != null
            ? _character_management.get_member_attribute_snapshot(member_id)
            : null;

    public GArray get_member_equipped_entries(StringName member_id) =>
        _party_equipment_service != null
            ? UntypedDictionaryArray(_party_equipment_service.get_equipped_entries(member_id))
            : new GArray();

    public string get_member_display_name(StringName member_id) =>
        _get_member_display_name(member_id);

    public StringName get_party_selected_member_id() => _party_selected_member_id;

    public void set_party_selected_member_id(StringName member_id) =>
        _party_selected_member_id = member_id;

    public string get_item_display_name(StringName item_id) => _get_item_display_name(item_id);

    public GDictionary get_settlement_window_data() => get_settlement_window_data("");

    public GDictionary get_settlement_window_data(string settlement_id) =>
        _settlement_command_handler.get_settlement_window_data(settlement_id);

    public string get_settlement_feedback_text() => _active_settlement_feedback_text;

    public void set_active_settlement_id(string settlement_id) =>
        _active_settlement_id = settlement_id;

    public void set_settlement_feedback_text(string feedback_text) =>
        _active_settlement_feedback_text = feedback_text;

    public GDictionary get_settlement_record(string settlement_id) =>
        _world_map_data_context.get_settlement_record(settlement_id);

    public GArray get_all_settlement_records() =>
        UntypedDictionaryArray(_world_map_data_context.get_all_settlement_records());

    public GDictionary get_character_info_context() =>
        _active_character_info_context.Duplicate(true);

    public string get_active_warehouse_entry_label() => _active_warehouse_entry_label;

    public void set_active_warehouse_entry_label(string entry_label) =>
        _active_warehouse_entry_label = entry_label;

    public GDictionary get_shop_window_data() => _settlement_command_handler.get_shop_window_data();

    public GDictionary get_contract_board_window_data() =>
        _settlement_command_handler.get_contract_board_window_data();

    public GDictionary get_forge_window_data() =>
        _settlement_command_handler.get_forge_window_data();

    public void set_active_contract_board_context(GDictionary context) =>
        _active_contract_board_context = (context ?? new GDictionary()).Duplicate(true);

    public void set_active_shop_context(GDictionary context) =>
        _active_shop_context = (context ?? new GDictionary()).Duplicate(true);

    public void set_active_forge_context(GDictionary context) =>
        _active_forge_context = (context ?? new GDictionary()).Duplicate(true);

    public void clear_active_contract_board_context() => _active_contract_board_context.Clear();

    public void clear_active_shop_context() => _active_shop_context.Clear();

    public void clear_active_forge_context() => _active_forge_context.Clear();

    public GDictionary get_active_contract_board_context() =>
        _active_contract_board_context.Duplicate(true);

    public GDictionary get_active_shop_context() => _active_shop_context.Duplicate(true);

    public GDictionary get_active_forge_context() => _active_forge_context.Duplicate(true);

    public GDictionary get_stagecoach_window_data() =>
        _settlement_command_handler.get_stagecoach_window_data();

    public void set_active_stagecoach_context(GDictionary context) =>
        _active_stagecoach_context = (context ?? new GDictionary()).Duplicate(true);

    public void clear_active_stagecoach_context() => _active_stagecoach_context.Clear();

    public GDictionary get_active_stagecoach_context() =>
        _active_stagecoach_context.Duplicate(true);

    public GDictionary get_warehouse_window_data() =>
        _party_state != null ? _warehouse_handler.get_warehouse_window_data() : new GDictionary();

    public BattleState get_battle_state() => _battle_state;

    public BattleRuntimeModule get_battle_runtime() => _battle_runtime;

    public BattleGridService get_battle_grid_service() => _battle_grid_service;

    public GameRuntimeBattleSelection get_battle_selection() => _battle_selection;

    public GameSession get_game_session() => _game_session;

    public SettlementShopService get_settlement_shop_service() => new();

    public CharacterManagementModule get_character_management() => _character_management;

    public PartyWarehouseService get_party_warehouse_service() => _party_warehouse_service;

    public PartyItemUseService get_party_item_use_service() => _party_item_use_service;

    public PartyEquipmentService get_party_equipment_service() => _party_equipment_service;

    public GameRuntimeWarehouseHandler get_warehouse_handler() => _warehouse_handler;

    public StringName get_active_battle_encounter_id() => _active_battle_encounter_id;

    public string get_active_battle_encounter_name() => _active_battle_encounter_name;

    public Vector2I get_battle_selected_coord() => _battle_selected_coord;

    public string get_last_advance_battle_refresh_mode() => _last_advance_battle_refresh_mode;

    public StringName get_selected_battle_skill_id() => _selected_battle_skill_id;

    public StringName get_selected_battle_skill_variant_id() => _selected_battle_skill_variant_id;

    public void set_battle_selection_skill_id(StringName skill_id) =>
        _selected_battle_skill_id = skill_id;

    public void set_battle_selection_skill_variant_id(StringName variant_id) =>
        _selected_battle_skill_variant_id = variant_id;

    public StringName get_battle_selection_last_manual_unit_id() => _last_manual_battle_unit_id;

    public void set_battle_selection_last_manual_unit_id(StringName unit_id) =>
        _last_manual_battle_unit_id = unit_id;

    public GVector2IArray get_battle_selection_target_coords_state() =>
        _queued_battle_skill_target_coords;

    public void set_battle_selection_target_coords_state(GVector2IArray target_coords) =>
        _queued_battle_skill_target_coords = target_coords;

    public GStringNameArray get_battle_selection_target_unit_ids_state() =>
        _queued_battle_skill_target_unit_ids;

    public void set_battle_selection_target_unit_ids_state(GStringNameArray target_unit_ids) =>
        _queued_battle_skill_target_unit_ids = target_unit_ids;

    public BattleUnitState get_manual_battle_unit() => _get_manual_active_unit();

    public BattleUnitState get_runtime_battle_active_unit() => _get_runtime_active_unit();

    public BattleUnitState get_runtime_battle_unit_at_coord(Vector2I coord) =>
        _get_runtime_unit_at_coord(coord);

    public BattleUnitState get_runtime_battle_unit_by_id(StringName unit_id) =>
        _get_battle_unit_by_id(unit_id);

    public BattlePreview preview_battle_command(BattleCommand command) =>
        _battle_runtime != null
            ? _battle_runtime.preview_command(command)
            : null;

    public string get_battle_skill_cast_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) =>
        _battle_runtime != null
            ? _battle_runtime.get_skill_cast_block_reason(active_unit, skill_def)
            : "";

    public StringName issue_battle_command(BattleCommand command) => _issue_battle_command(command);

    public void refresh_battle_selection_state() => _refresh_battle_selection_state();

    public GDictionary build_command_ok() => _command_ok("", "");

    public GDictionary build_command_ok(string message) => _command_ok(message, "");

    public GDictionary build_command_ok(string message, string battle_refresh_mode) =>
        _command_ok(message, battle_refresh_mode);

    public GDictionary build_command_error(string message) => _command_error(message);

    public bool batch_has_updates(BattleEventBatch batch) => _batch_has_updates(batch);

    public bool try_open_character_info_at_battle_coord(Vector2I coord) =>
        _try_open_character_info_at_battle_coord(coord);

    public void update_status(string message) => _update_status(message);

    public void close_settlement_modal() =>
        _settlement_command_handler.on_settlement_window_closed();

    public void close_contract_board_modal() =>
        _settlement_command_handler.on_contract_board_window_closed();

    public void close_shop_modal() => _settlement_command_handler.on_shop_window_closed();

    public void close_forge_modal() => _settlement_command_handler.on_forge_window_closed();

    public void close_stagecoach_modal() =>
        _settlement_command_handler.on_stagecoach_window_closed();

    public string format_coord(Vector2I coord) => _format_coord(coord);

    public GDictionary get_skill_defs() =>
        _game_session != null
            ? _game_session.get_skill_defs()
            : new GDictionary();

    public GDictionary get_item_defs() =>
        _game_session != null
            ? _game_session.get_item_defs()
            : new GDictionary();

    public string get_selected_battle_skill_name() =>
        _battle_session_facade.get_selected_battle_skill_name();

    public string get_selected_battle_skill_variant_name() =>
        _battle_session_facade.get_selected_battle_skill_variant_name();

    public GVector2IArray get_selected_battle_skill_target_coords() =>
        _battle_session_facade.get_selected_battle_skill_target_coords();

    public GStringNameArray get_selected_battle_skill_target_unit_ids() =>
        _battle_session_facade.get_selected_battle_skill_target_unit_ids();

    public GVector2IArray get_selected_battle_skill_valid_target_coords() =>
        _battle_session_facade.get_selected_battle_skill_valid_target_coords();

    public GVector2IArray get_battle_movement_reachable_coords() =>
        _battle_session_facade.get_battle_movement_reachable_coords();

    public GVector2IArray get_battle_overlay_target_coords() =>
        _battle_session_facade.get_battle_overlay_target_coords();

    public int get_selected_battle_skill_required_coord_count() =>
        _battle_session_facade.get_selected_battle_skill_required_coord_count();

    public string get_battle_active_unit_name() =>
        _battle_session_facade.get_battle_active_unit_name();

    public GDictionary get_battle_terrain_counts() =>
        _battle_session_facade.get_battle_terrain_counts();

    public GDictionary get_last_battle_loot_snapshot() =>
        _last_battle_loot_snapshot.Duplicate(true);

    public PendingCharacterReward get_active_reward() => _active_reward;

    public PendingCharacterReward get_snapshot_reward() =>
        _active_reward ?? _party_state?.get_next_pending_character_reward();

    public int get_pending_reward_count() =>
        _party_state != null ? _party_state.pending_character_rewards.Count : 0;

    public GDictionary get_current_promotion_prompt() =>
        _reward_flow_handler != null
            ? _reward_flow_handler.get_current_promotion_prompt()
            : new GDictionary();

    public GDictionary get_pending_promotion_prompt() => _pending_promotion_prompt.Duplicate(true);

    public GDictionary get_pending_world_promotion_prompt_state() =>
        _pending_world_promotion_prompt.Duplicate(true);

    public PendingCharacterReward get_active_reward_state() => _active_reward;

    public bool is_battle_active() => _is_battle_active();

    public bool is_modal_window_open() => _is_modal_window_open();

    public void set_runtime_battle_state(BattleState state)
    {
        _battle_state = state;
        _battle_auto_tick_remainder_msec = 0;
    }

    public void set_runtime_battle_selected_coord(Vector2I coord) => _battle_selected_coord = coord;

    public void set_runtime_active_modal_id(string modal_id) => _active_modal_id = modal_id;

    public void set_pending_promotion_prompt(GDictionary prompt) =>
        _pending_promotion_prompt = (prompt ?? new GDictionary()).Duplicate(true);

    public void clear_pending_promotion_prompt() => _pending_promotion_prompt.Clear();

    public void set_pending_world_promotion_prompt_state(GDictionary prompt) =>
        _pending_world_promotion_prompt = (prompt ?? new GDictionary()).Duplicate(true);

    public void clear_pending_world_promotion_prompt_state() =>
        _pending_world_promotion_prompt.Clear();

    public void set_active_reward_state(PendingCharacterReward reward) => _active_reward = reward;

    public void clear_active_reward_state() => _active_reward = null;

    public void clear_active_character_info_context() => _active_character_info_context.Clear();

    public void clear_battle_selection_targets() => _battle_selection_state.ClearTargets();

    public void close_party_management_modal() =>
        _party_command_handler?.on_party_management_window_closed();

    public void close_party_warehouse_modal() =>
        _warehouse_handler?.on_party_warehouse_window_closed();

    public void open_party_warehouse_window(string entry_label) =>
        _warehouse_handler?.open_party_warehouse_window(entry_label);

    public BattleEventBatch submit_battle_promotion_choice(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    ) =>
        _battle_runtime != null
            ? _battle_runtime.submit_promotion_choice(member_id, profession_id, selection)
            : null;

    public void apply_battle_batch(BattleEventBatch batch) => _apply_battle_batch(batch);

    public CharacterProgressionDelta promote_profession(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    ) => _character_management?.promote_profession(member_id, profession_id, selection);

    public GDictionary set_active_level_trigger_core_skill(
        StringName member_id,
        StringName skill_id
    )
    {
        if (_character_management == null)
            return new GDictionary
            {
                ["ok"] = false,
                ["error"] = "character_management_unavailable",
            };
        LevelGrowthTriggerResult result =
            _character_management.SetActiveLevelTriggerCoreSkillTyped(member_id, skill_id);
        if (result.Ok)
        {
            _party_state = _character_management.get_party_state();
            _persist_party_state();
        }
        return result.ToDictionary();
    }

    public GDictionary clear_active_level_trigger_core_skill(StringName member_id)
    {
        if (_character_management == null)
            return new GDictionary
            {
                ["ok"] = false,
                ["error"] = "character_management_unavailable",
            };
        LevelGrowthTriggerResult result =
            _character_management.ClearActiveLevelTriggerCoreSkillTyped(member_id);
        if (result.Ok)
        {
            _party_state = _character_management.get_party_state();
            _persist_party_state();
        }
        return result.ToDictionary();
    }

    public CharacterProgressionDelta apply_pending_character_reward_to_party(
        PendingCharacterReward reward
    ) => _character_management?.apply_pending_character_reward(reward);

    public void enqueue_character_rewards(GArray reward_options)
    {
        if (_character_management == null)
            return;
        _character_management.enqueue_pending_character_rewards(reward_options);
        _party_state = _character_management.get_party_state();
    }

    public GDictionary apply_quest_progress_events_to_party(GArray event_options) =>
        apply_quest_progress_events_to_party(event_options, "quest");

    public GDictionary apply_quest_progress_events_to_party(
        GArray event_options,
        string source_domain
    )
    {
        if (_character_management == null)
        {
            return new GDictionary
            {
                ["accepted_quest_ids"] = new GArray(),
                ["progressed_quest_ids"] = new GArray(),
                ["claimable_quest_ids"] = new GArray(),
                ["completed_quest_ids"] = new GArray(),
            };
        }
        var summary = _character_management.apply_quest_progress_events(
            event_options,
            get_world_step()
        );
        _party_state = _character_management.get_party_state();
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

    public void sync_party_state_from_character_management()
    {
        if (_character_management != null)
            _party_state = _character_management.get_party_state();
    }

    public int persist_party_state() => _persist_party_state();

    public GDictionary build_runtime_promotion_prompt(CharacterProgressionDelta delta) =>
        build_runtime_promotion_prompt(delta, "确认后将在战斗中立即生效。");

    public GDictionary build_runtime_promotion_prompt(
        CharacterProgressionDelta delta,
        string selection_hint
    ) =>
        _build_promotion_prompt(delta, selection_hint);

    public GDictionary equip_party_item(
        StringName member_id,
        StringName item_id,
        StringName slot_id
    ) => equip_party_item(member_id, item_id, slot_id, "");

    public GDictionary equip_party_item(
        StringName member_id,
        StringName item_id,
        StringName slot_id,
        StringName instance_id
    ) =>
        _party_equipment_service != null
            ? _party_equipment_service.equip_item(member_id, item_id, slot_id, instance_id)
            : new GDictionary();

    public GDictionary unequip_party_item(StringName member_id, StringName slot_id) =>
        _party_equipment_service != null
            ? _party_equipment_service.unequip_item(member_id, slot_id)
            : new GDictionary();

    public bool present_pending_reward_if_ready() => _present_pending_reward_if_ready();

    public void sync_character_management_party_state() =>
        _character_management?.set_party_state(_party_state);

    public void enqueue_pending_character_rewards(GArray reward_options) =>
        _enqueue_pending_character_rewards(reward_options);

    public void record_member_achievement_event(
        StringName member_id,
        StringName event_id,
        int value
    ) => record_member_achievement_event(member_id, event_id, value, "");

    public void record_member_achievement_event(
        StringName member_id,
        StringName event_id,
        int value,
        StringName detail_id
    ) => _character_management?.record_achievement_event(member_id, event_id, value, detail_id);

    public void prepare_battle_start(EncounterAnchorData encounter_anchor)
    {
        if (encounter_anchor == null)
            return;
        var fateRuntime = _battle_runtime?.get_fate_runtime();
        fateRuntime?.clear_misfortune_exalted_ready_flags(new GArray());
        _active_battle_encounter_id = encounter_anchor.entity_id;
        _active_battle_encounter_name = encounter_anchor.display_name;
        _last_battle_loot_snapshot.Clear();
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _pending_promotion_prompt.Clear();
        _battle_selection.clear_battle_skill_selection(false);
        _character_management.set_party_state(_party_state);
    }

    public StringName begin_battle_start(
        EncounterAnchorData encounter_anchor,
        int seed,
        GDictionary context
    )
    {
        if (encounter_anchor == null || _battle_runtime == null)
            return "failed";
        _pending_battle_generation_request.Set(encounter_anchor, seed, context);
        _pending_battle_start_prompt.Clear();
        _active_modal_id = BattleLoadingModalId;
        string encounterName = _resolve_battle_encounter_display_name(encounter_anchor);
        _update_status($"遭遇 {encounterName}，战斗地图生成中。");
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

    public void handle_battle_start_failure()
    {
        string failedEncounterId = _active_battle_encounter_id.ToString();
        string failedEncounterName = _active_battle_encounter_name;
        _active_battle_encounter_id = "";
        _active_battle_encounter_name = "";
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _active_modal_id = "";
        _battle_state = null;
        _battle_auto_tick_remainder_msec = 0;
        _battle_selected_coord = new Vector2I(-1, -1);
        _update_status("遭遇战生成失败。");
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

    public void present_battle_start_confirmation()
    {
        if (!_is_battle_active() || _battle_state == null)
            return;
        _pending_battle_start_prompt = new GDictionary
        {
            ["title"] = "开始战斗",
            ["description"] = "是否开始战斗？确认后 TU 将按整数 tick 推进。",
            ["confirm_text"] = "开始战斗",
            ["cancel_visible"] = false,
            ["dismiss_on_shade"] = false,
        };
        _active_modal_id = "battle_start_confirm";
        _battle_state.modal_state = "start_confirm";
        if (_battle_state.timeline != null)
        {
            _battle_state.timeline.frozen = true;
            _battle_state.timeline.tu_per_tick = 5;
        }
        _battle_auto_tick_remainder_msec = 0;
        _update_status("战斗地图已载入，请确认开始战斗。");
        _log_runtime_event(
            "info",
            "battle",
            "battle.start_prepared",
            "战斗地图已载入，请确认开始战斗。",
            Json.Stringify(new GDictionary { ["runtime"] = _build_runtime_log_state() })
        );
    }

    public bool _try_complete_pending_battle_start()
    {
        if (_pending_battle_generation_request.IsEmpty || _battle_runtime == null)
            return false;
        var encounterAnchor = _pending_battle_generation_request.EncounterAnchor;
        if (encounterAnchor == null)
            return false;
        int seed = _pending_battle_generation_request.Seed;
        var context = _pending_battle_generation_request.CloneContext();
        var runtimeState = _battle_runtime.start_battle(encounterAnchor, seed, context);
        if (runtimeState == null || runtimeState.is_empty())
            return false;
        _pending_battle_generation_request.Clear();
        if (_battle_session_facade != null)
            _battle_session_facade.refresh_battle_runtime_state();
        else
            _battle_state = runtimeState;
        present_battle_start_confirmation();
        return true;
    }

    public string _resolve_battle_encounter_display_name(EncounterAnchorData encounter_anchor)
    {
        if (encounter_anchor == null)
            return "遭遇";
        return string.IsNullOrEmpty(encounter_anchor.display_name)
            ? "遭遇"
            : encounter_anchor.display_name;
    }

    public bool finalize_battle_resolution(BattleResolutionResult battle_resolution_result)
    {
        if (
            battle_resolution_result == null
            || _game_session == null
            || _character_management == null
            || _battle_runtime == null
        )
        {
            _update_status("战斗结算失败：运行时状态不完整，已保留战斗上下文。");
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
            GDictionary writebackPayload = writebackResult.ToDictionary();
            _report_battle_local_writeback_inoption_failure(
                writebackPayload,
                battleSummary,
                winnerFactionId
            );
            _update_status("战斗结算失败：战斗内队伍状态回写失败，已保留战斗上下文。");
            return false;
        }

        var fateResolution = _battle_runtime.handle_fate_battle_resolution(
            _battle_state,
            battle_resolution_result
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

        var resolvedPendingRewards = battle_resolution_result.get_pending_character_rewards_copy();
        var resolvedQuestProgressEvents = battle_resolution_result.quest_progress_events.Duplicate(
            true
        );
        bool mainCharacterDead =
            _is_main_character_dead() || _is_main_character_dead_in_battle_state();
        var questSummary = new GDictionary();
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
                _update_status(
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
        _party_state = _character_management.get_party_state();
        if (!mainCharacterDead)
        {
            _character_management.enqueue_pending_character_rewards(resolvedPendingRewards);
            var mergedQuestProgressEvents = resolvedQuestProgressEvents.Duplicate(true);
            foreach (
                Variant eventValue in _build_default_battle_quest_progress_events(winnerFactionId)
            )
                mergedQuestProgressEvents.Add(eventValue);
            questSummary = _character_management.apply_quest_progress_events(
                mergedQuestProgressEvents,
                get_world_step()
            );
            _party_state = _character_management.get_party_state();
            partyPersistError = _game_session.set_party_state(_party_state);
            if (partyPersistError != (int)Error.Ok)
            {
                _update_status(
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
            worldPersistError = _game_session.set_world_data(
                _world_map_data_context.root_world_data
            );
            if (worldPersistError != (int)Error.Ok)
            {
                _world_map_data_context.bind_root_world_data(worldDataBefore);
                _update_status(
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
            _game_session.discard_pending_save();
            _game_session.set_battle_save_lock(false);
        }
        else
        {
            _game_session.set_battle_save_lock(false);
            flushError = _game_session.flush_game_state();
            if (flushError != (int)Error.Ok)
            {
                _game_session.set_battle_save_lock(true);
                _update_status(
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

        _clear_resolved_battle_runtime_context();
        if (mainCharacterDead)
        {
            _last_battle_loot_snapshot.Clear();
            _activate_game_over(_build_main_character_game_over_context());
        }
        else
        {
            _last_battle_loot_snapshot = BuildLastBattleLootSnapshotTyped(
                battleName,
                winnerFactionId,
                battle_resolution_result,
                lootCommitResult
            );
        }

        _refresh_fog();
        if (mainCharacterDead)
        {
            _update_status(
                DictString(_active_game_over_context, "description", "主角已阵亡，本次旅程结束。")
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
        _update_status(
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
        _present_pending_reward_if_ready();
        return true;
    }

    public void _release_battle_save_lock()
    {
        _game_session?.set_battle_save_lock(false);
    }

    public GStringNameArray handle_fortuna_chapter_completed(GDictionary payload)
    {
        var fateRuntime = _battle_runtime?.get_fate_runtime();
        if (fateRuntime == null)
            return new GStringNameArray();
        var unlockedIds = fateRuntime.handle_fortuna_chapter_completed(payload);
        if (_character_management != null)
            _party_state = _character_management.get_party_state();
        if (_party_state != null)
            _clear_regular_battle_calamity_shard_flags();
        if (
            _game_session != null
            && _party_state != null
            && _game_session.has_active_world()
        )
        {
            _game_session.set_party_state(_party_state);
            _game_session.flush_game_state();
        }
        return unlockedIds;
    }

    public GStringNameArray handle_misfortune_forge_result(StringName member_id, GDictionary result)
    {
        return HandleMisfortuneForgeResult(
            member_id,
            SettlementServiceResult.FromDictionary(result)
        );
    }

    public GStringNameArray HandleMisfortuneForgeResult(
        StringName member_id,
        SettlementServiceResult result)
    {
        var fateRuntime = _battle_runtime?.get_fate_runtime();
        if (
            fateRuntime == null
            || member_id == ""
            || result == null
        )
            return new GStringNameArray();
        var itemDefs =
            _game_session != null
                ? _game_session.get_item_defs()
                : new GDictionary();
        var unlockedIds = fateRuntime.HandleMisfortuneForgeResult(member_id, result, itemDefs);
        if (_character_management != null)
            _party_state = _character_management.get_party_state();
        return unlockedIds;
    }

    public GDictionary resolve_low_luck_settlement_event_rewards(GDictionary context)
    {
        var fateRuntime = _battle_runtime?.get_fate_runtime();
        if (fateRuntime == null)
            return new GDictionary();
        var result = fateRuntime.resolve_low_luck_settlement_event_rewards(context);
        if (_character_management != null)
            _party_state = _character_management.get_party_state();
        return result;
    }

    public GDictionary _commit_battle_local_views_to_party_state(
        BattleState battle_state,
        PartyState party_state
    )
    {
        _bind_runtime_sidecar_owners();
        return _battle_writeback_service.commit_battle_local_views_to_party_state(
            battle_state,
            party_state
        );
    }

    internal GameRuntimeBattleWritebackService.BattleLocalWritebackResult CommitBattleLocalViewsToPartyStateTyped(
        BattleState battleState,
        PartyState partyState
    )
    {
        _bind_runtime_sidecar_owners();
        return _battle_writeback_service.CommitBattleLocalViewsToPartyStateTyped(
            battleState,
            partyState
        );
    }

    public void _report_battle_local_writeback_inoption_failure(
        GDictionary writeback_result,
        GDictionary battle_summary,
        string winner_faction_id
    )
    {
        _bind_runtime_sidecar_owners();
        _battle_writeback_service.report_inoption_failure(
            writeback_result,
            battle_summary,
            winner_faction_id
        );
    }

    public GDictionary _commit_battle_loot_to_shared_warehouse(
        BattleResolutionResult battle_resolution_result
    )
    {
        _bind_runtime_sidecar_owners();
        return CommitBattleLootToSharedWarehouseTyped(battle_resolution_result).ToDictionary();
    }

    internal GameRuntimeBattleLootCommitService.BattleLootCommitResult CommitBattleLootToSharedWarehouseTyped(
        BattleResolutionResult battleResolutionResult
    )
    {
        _bind_runtime_sidecar_owners();
        return _battle_loot_commit_service.CommitBattleLootToSharedWarehouseTyped(
            battleResolutionResult
        );
    }

    public GDictionary _commit_fixed_item_loot_entry(GDictionary loot_entry_data)
    {
        _bind_runtime_sidecar_owners();
        return _battle_loot_commit_service._commit_fixed_item_loot_entry(loot_entry_data);
    }

    public void _clear_regular_battle_calamity_shard_flags()
    {
        _bind_runtime_sidecar_owners();
        _battle_loot_commit_service.clear_regular_battle_calamity_shard_flags();
    }

    public string _build_battle_resolution_status_message(
        string battle_name,
        string winner_faction_id,
        GDictionary loot_commit_result,
        bool persisted_ok
    ) =>
        _battle_loot_commit_service.build_battle_resolution_status_message(
            battle_name,
            winner_faction_id,
            loot_commit_result,
            persisted_ok
        );

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

    public GDictionary _build_last_battle_loot_snapshot(
        string battle_name,
        string winner_faction_id,
        BattleResolutionResult battle_resolution_result,
        GDictionary loot_commit_result
    ) =>
        _battle_loot_commit_service.build_last_battle_loot_snapshot(
            battle_name,
            winner_faction_id,
            battle_resolution_result,
            loot_commit_result
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

    public string _format_battle_drop_entries(GArray drop_entry_options) =>
        _battle_loot_commit_service.format_battle_drop_entries(drop_entry_options);

    public bool advance(float delta)
    {
        _last_advance_battle_refresh_mode = "";
        if (_generation_config == null)
            return false;
        if (_try_complete_pending_battle_start())
        {
            _last_advance_battle_refresh_mode = "full";
            return true;
        }
        if (_has_pending_battle_generation_request())
            return false;
        if (_is_battle_active())
        {
            if (_is_battle_finished() || _is_battle_timeline_modal_active())
                return false;
            int previousTu =
                _battle_state?.timeline != null ? _battle_state.timeline.current_tu : -1;
            int tickCount = _resolve_battle_auto_tick_count(delta);
            var batch = _battle_runtime.advance(tickCount);
            if (_batch_has_updates(batch))
            {
                _apply_battle_batch(batch);
                _last_advance_battle_refresh_mode = _batch_requires_full_battle_refresh(batch)
                    ? "full"
                    : "overlay";
                return true;
            }
            int currentTu =
                _battle_state?.timeline != null ? _battle_state.timeline.current_tu : -1;
            if (currentTu != previousTu)
            {
                _last_advance_battle_refresh_mode = "overlay";
                return true;
            }
            return false;
        }
        if (_is_modal_window_open())
            return false;
        return _present_pending_reward_if_ready();
    }

    public int _resolve_battle_auto_tick_count(float delta)
    {
        if (delta <= 0.0f)
            return 0;
        _battle_auto_tick_remainder_msec += Math.Max((int)Math.Round(delta * 1000.0f), 0);
        int tickCount = _battle_auto_tick_remainder_msec / BattleAutoAdvanceTickMsec;
        if (tickCount > 0)
            _battle_auto_tick_remainder_msec -= tickCount * BattleAutoAdvanceTickMsec;
        return Mathf.Min(tickCount, 1);
    }

    public GDictionary build_headless_snapshot() => _snapshot_builder.BuildHeadlessSnapshot();

    public string build_text_snapshot() => _snapshot_builder.BuildTextSnapshot();

    public void advance_world_time_by_steps(int delta_steps) =>
        _advance_world_time_by_steps(delta_steps);

    public void refresh_world_visibility()
    {
        _world_map_data_context.refresh_world_event_discovery();
        _refresh_fog();
    }

    public void refresh_fog() => _refresh_fog();

    public void set_party_state(PartyState party_state)
    {
        _party_state = party_state;
        _sync_party_state_services();
    }

    public int persist_world_data() => _persist_world_data();

    public int persist_player_coord()
    {
        if (_game_session == null)
            return (int)Error.Unavailable;
        int stageError = _game_session.set_player_coord(_player_coord);
        if (stageError != (int)Error.Ok)
            return stageError;
        return _commit_runtime_state("player_coord");
    }

    public void set_player_coord(Vector2I coord) => _player_coord = coord;

    public void set_selected_coord(Vector2I coord) => _selected_coord = coord;

    public void clear_settlement_entry_context() => _clear_settlement_entry_context(true);

    public void clear_settlement_entry_context(bool reset_selected) =>
        _clear_settlement_entry_context(reset_selected);

    public bool set_active_settlement_state(string settlement_id, GDictionary settlement_state) =>
        _world_map_data_context.set_active_settlement_state(settlement_id, settlement_state);

    public GDictionary get_settlement_state(string settlement_id) =>
        _world_map_data_context.get_settlement_state(settlement_id);

    public WorldMapSettlementStateData GetSettlementStateData(string settlement_id) =>
        _world_map_data_context.GetSettlementStateData(settlement_id);

    public bool IsSettlementVisited(string settlement_id) =>
        _world_map_data_context.IsSettlementVisited(settlement_id);

    public bool MarkSettlementVisited(string settlement_id) =>
        _world_map_data_context.MarkSettlementVisited(settlement_id);

    public GDictionary command_world_move(Vector2I direction) => command_world_move(direction, 1);

    public GDictionary command_world_move(Vector2I direction, int count)
    {
        return _execute_logged_command(
            "world.move",
            "world",
            new GDictionary { ["direction"] = direction, ["count"] = count },
            () =>
            {
                if (_generation_config == null)
                    return _command_error("世界地图尚未初始化。");
                if (_is_battle_active())
                    return _command_error("当前处于战斗中，不能执行大地图移动。");
                if (_is_modal_window_open())
                    return _command_error("当前有窗口打开，不能执行大地图移动。");
                if (direction == Vector2I.Zero)
                    return _command_error("移动方向不能为空。");
                int moveCount = Math.Min(Math.Max(count, 1), MaxCommandWorldMoveCount);
                for (int i = 0; i < moveCount; i++)
                {
                    _move_player(direction);
                    if (_is_battle_active() || _is_modal_window_open())
                        break;
                }
                return _command_ok();
            }
        );
    }

    public GDictionary command_world_select(Vector2I coord)
    {
        return _execute_logged_command(
            "world.select",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return _command_error("世界地图尚未初始化。");
                if (_is_battle_active())
                    return _command_error("当前处于战斗中，不能选择大地图坐标。");
                if (_is_modal_window_open())
                    return _command_error("当前有窗口打开，不能切换大地图选择。");
                if (!_grid_system.is_cell_walkable(coord))
                    return _command_error("该大地图格超出当前世界范围。");
                _selected_coord = coord;
                _update_status($"已选中格子 {_format_coord(coord)}。");
                return _command_ok();
            }
        );
    }

    public GDictionary command_open_settlement() => command_open_settlement(new Vector2I(-1, -1));

    public GDictionary command_open_settlement(Vector2I coord)
    {
        return _execute_logged_command(
            "settlement.open",
            "settlement",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return _command_error("世界地图尚未初始化。");
                if (_is_battle_active())
                    return _command_error("当前处于战斗中，不能打开据点。");
                if (_is_modal_window_open())
                    return _command_error("当前有窗口打开，不能打开新的据点窗口。");
                var targetCoord = coord == new Vector2I(-1, -1) ? _selected_coord : coord;
                if (_try_open_settlement_at(targetCoord))
                    return _command_ok();
                return _command_error(
                    string.IsNullOrEmpty(_current_status_message)
                        ? "据点打开失败。"
                        : _current_status_message
                );
            }
        );
    }

    public GDictionary command_world_inspect(Vector2I coord)
    {
        return _execute_logged_command(
            "world.inspect",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (_generation_config == null)
                    return _command_error("世界地图尚未初始化。");
                if (_is_battle_active())
                    return _command_error("当前处于战斗中，不能查看大地图人物。");
                if (_is_modal_window_open())
                    return _command_error("当前有窗口打开，不能查看大地图人物。");
                if (!_fog_system.is_visible(coord, _player_faction_id))
                {
                    _update_status("该格当前不在视野中。");
                    return _command_error(_current_status_message);
                }
                if (_try_open_character_info_at_world_coord(coord))
                    return _command_ok();
                _update_status("当前格没有可查看人物。");
                return _command_error(_current_status_message);
            }
        );
    }

    public GDictionary command_open_party() =>
        _execute_logged_command(
            "party.open",
            "party",
            new GDictionary(),
            () => _party_command_handler.command_open_party()
        );

    public GDictionary command_accept_quest(StringName quest_id) =>
        command_accept_quest(quest_id, false);

    public GDictionary command_accept_quest(StringName quest_id, bool allow_reaccept) =>
        _execute_logged_command(
            "quest.accept",
            "quest",
            new GDictionary { ["quest_id"] = quest_id, ["allow_reaccept"] = allow_reaccept },
            () => _quest_command_handler.command_accept_quest(quest_id, allow_reaccept)
        );

    public GDictionary command_progress_quest(StringName quest_id, StringName objective_id) =>
        command_progress_quest(quest_id, objective_id, 1, new GDictionary());

    public GDictionary command_progress_quest(
        StringName quest_id,
        StringName objective_id,
        int progress_delta
    ) => command_progress_quest(quest_id, objective_id, progress_delta, new GDictionary());

    public GDictionary command_progress_quest(
        StringName quest_id,
        StringName objective_id,
        int progress_delta,
        GDictionary payload
    ) =>
        _execute_logged_command(
            "quest.progress",
            "quest",
            new GDictionary
            {
                ["quest_id"] = quest_id,
                ["objective_id"] = objective_id,
                ["progress_delta"] = progress_delta,
                ["payload"] = (payload ?? new GDictionary()).Duplicate(true),
            },
            () =>
                _quest_command_handler.command_progress_quest(
                    quest_id,
                    objective_id,
                    progress_delta,
                    payload ?? new GDictionary()
                )
        );

    public GDictionary command_complete_quest(StringName quest_id) =>
        _execute_logged_command(
            "quest.complete",
            "quest",
            new GDictionary { ["quest_id"] = quest_id },
            () => _quest_command_handler.command_complete_quest(quest_id)
        );

    public GDictionary command_submit_quest_item(StringName quest_id) =>
        command_submit_quest_item(quest_id, "");

    public GDictionary command_submit_quest_item(StringName quest_id, StringName objective_id) =>
        _execute_logged_command(
            "quest.submit_item",
            "quest",
            new GDictionary { ["quest_id"] = quest_id, ["objective_id"] = objective_id },
            () => _quest_command_handler.command_submit_quest_item(quest_id, objective_id)
        );

    public GDictionary command_claim_quest(StringName quest_id) =>
        _execute_logged_command(
            "quest.claim",
            "quest",
            new GDictionary { ["quest_id"] = quest_id },
            () => _quest_command_handler.command_claim_quest(quest_id)
        );

    public GDictionary command_select_party_member(StringName member_id) =>
        _execute_logged_command(
            "party.select_member",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.command_select_party_member(member_id)
        );

    public GDictionary command_set_party_leader(StringName member_id) =>
        _execute_logged_command(
            "party.set_leader",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.command_set_party_leader(member_id)
        );

    public GDictionary command_move_member_to_active(StringName member_id) =>
        _execute_logged_command(
            "party.move_member_to_active",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.command_move_member_to_active(member_id)
        );

    public GDictionary command_move_member_to_reserve(StringName member_id) =>
        _execute_logged_command(
            "party.move_member_to_reserve",
            "party",
            new GDictionary { ["member_id"] = member_id },
            () => _party_command_handler.command_move_member_to_reserve(member_id)
        );

    public GDictionary command_party_equip_item(StringName member_id, StringName item_id) =>
        command_party_equip_item(member_id, item_id, "", "");

    public GDictionary command_party_equip_item(
        StringName member_id,
        StringName item_id,
        StringName slot_id
    ) => command_party_equip_item(member_id, item_id, slot_id, "");

    public GDictionary command_party_equip_item(
        StringName member_id,
        StringName item_id,
        StringName slot_id,
        StringName instance_id
    ) =>
        _execute_logged_command(
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
                _party_command_handler.command_party_equip_item(
                    member_id,
                    item_id,
                    slot_id,
                    instance_id
                )
        );

    public GDictionary command_party_unequip_item(StringName member_id, StringName slot_id) =>
        _execute_logged_command(
            "party.unequip_item",
            "party",
            new GDictionary { ["member_id"] = member_id, ["slot_id"] = slot_id },
            () => _party_command_handler.command_party_unequip_item(member_id, slot_id)
        );

    public GDictionary command_open_party_warehouse() =>
        _execute_logged_command(
            "warehouse.open",
            "warehouse",
            new GDictionary(),
            () => _warehouse_handler.command_open_party_warehouse()
        );

    public GDictionary command_warehouse_discard_one(StringName item_id, StringName instance_id) =>
        _execute_logged_command(
            "warehouse.discard_one",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["instance_id"] = instance_id },
            () => _warehouse_handler.command_discard_one(item_id, instance_id)
        );

    public GDictionary command_warehouse_discard_all(StringName item_id) =>
        command_warehouse_discard_all(item_id, "");

    public GDictionary command_warehouse_discard_all(StringName item_id, StringName instance_id) =>
        _execute_logged_command(
            "warehouse.discard_all",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["instance_id"] = instance_id },
            () => _warehouse_handler.command_discard_all(item_id, instance_id)
        );

    public GDictionary command_warehouse_use_item(StringName item_id) =>
        command_warehouse_use_item(item_id, "", new GDictionary());

    public GDictionary command_warehouse_use_item(StringName item_id, StringName member_id) =>
        command_warehouse_use_item(item_id, member_id, new GDictionary());

    public GDictionary command_warehouse_use_item(
        StringName item_id,
        StringName member_id,
        GDictionary options
    ) =>
        _execute_logged_command(
            "warehouse.use_item",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["member_id"] = member_id },
            () =>
                _warehouse_handler.command_use_item(
                    item_id,
                    member_id,
                    options ?? new GDictionary()
                )
        );

    public GDictionary command_warehouse_add_item(StringName item_id, int quantity) =>
        _execute_logged_command(
            "warehouse.add_item",
            "warehouse",
            new GDictionary { ["item_id"] = item_id, ["quantity"] = quantity },
            () => _warehouse_handler.command_add_item(item_id, quantity)
        );

    public GDictionary command_execute_settlement_action(string action_id) =>
        command_execute_settlement_action(action_id, new GDictionary());

    public GDictionary command_execute_settlement_action(string action_id, GDictionary payload) =>
        _execute_logged_command(
            "settlement.execute_action",
            "settlement",
            new GDictionary
            {
                ["action_id"] = action_id,
                ["payload"] = payload ?? new GDictionary(),
            },
            () =>
                _settlement_command_handler.command_execute_settlement_action(
                    action_id,
                    payload ?? new GDictionary()
                )
        );

    public GDictionary command_shop_buy(StringName item_id, int quantity) =>
        _execute_logged_command(
            "shop.buy",
            "shop",
            new GDictionary { ["item_id"] = item_id, ["quantity"] = quantity },
            () => _settlement_command_handler.command_shop_buy(item_id, quantity)
        );

    public GDictionary command_shop_sell(StringName item_id, int quantity) =>
        command_shop_sell(item_id, quantity, "");

    public GDictionary command_shop_sell(
        StringName item_id,
        int quantity,
        StringName instance_id
    ) =>
        _execute_logged_command(
            "shop.sell",
            "shop",
            new GDictionary
            {
                ["item_id"] = item_id,
                ["quantity"] = quantity,
                ["instance_id"] = instance_id,
            },
            () => _settlement_command_handler.command_shop_sell(item_id, quantity, instance_id)
        );

    public GDictionary command_stagecoach_travel(string settlement_id) =>
        _execute_logged_command(
            "stagecoach.travel",
            "stagecoach",
            new GDictionary { ["settlement_id"] = settlement_id },
            () => _settlement_command_handler.command_stagecoach_travel(settlement_id)
        );

    public GDictionary command_battle_tick(int tick_count) =>
        _execute_logged_command(
            "battle.tick",
            "battle",
            new GDictionary { ["tick_count"] = tick_count },
            () => _battle_session_facade.command_battle_tick(tick_count)
        );

    public GDictionary command_battle_select_skill(int slot_index) =>
        _execute_logged_command(
            "battle.select_skill",
            "battle",
            new GDictionary { ["slot_index"] = slot_index },
            () => _battle_session_facade.command_battle_select_skill(slot_index)
        );

    public GDictionary command_battle_cycle_variant(int step) =>
        _execute_logged_command(
            "battle.cycle_variant",
            "battle",
            new GDictionary { ["step"] = step },
            () => _battle_session_facade.command_battle_cycle_variant(step)
        );

    public GDictionary command_battle_clear_skill() =>
        _execute_logged_command(
            "battle.clear_skill",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.command_battle_clear_skill()
        );

    public GDictionary command_battle_move_to(Vector2I target_coord) =>
        _execute_logged_command(
            "battle.move_to",
            "battle",
            new GDictionary { ["target_coord"] = target_coord },
            () => _battle_session_facade.command_battle_move_to(target_coord)
        );

    public GDictionary command_battle_move_direction(Vector2I direction) =>
        _execute_logged_command(
            "battle.move_direction",
            "battle",
            new GDictionary { ["direction"] = direction },
            () => _battle_session_facade.command_battle_move_direction(direction)
        );

    public GDictionary command_battle_wait_or_resolve() =>
        _execute_logged_command(
            "battle.wait_or_resolve",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.command_battle_wait_or_resolve()
        );

    public GDictionary command_battle_inspect(Vector2I coord) =>
        _execute_logged_command(
            "battle.inspect",
            "battle",
            new GDictionary { ["coord"] = coord },
            () => _battle_session_facade.command_battle_inspect(coord)
        );

    public GDictionary command_confirm_pending_reward() =>
        _execute_logged_command(
            "reward.confirm_pending",
            "reward",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.command_confirm_pending_reward()
                    : _command_error("运行时尚未初始化。")
        );

    public GDictionary command_choose_promotion(StringName profession_id) =>
        _execute_logged_command(
            "promotion.choose",
            "promotion",
            new GDictionary { ["profession_id"] = profession_id },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.command_choose_promotion(profession_id)
                    : _command_error("运行时尚未初始化。")
        );

    public GDictionary command_confirm_submap_entry()
    {
        return _execute_logged_command(
            "submap.confirm_entry",
            "submap",
            new GDictionary
            {
                ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            },
            () =>
            {
                if (_pending_submap_prompt.IsEmpty)
                    return _command_error("当前没有待确认的子地图入口。");
                return _confirm_pending_submap_entry();
            }
        );
    }

    public GDictionary command_cancel_submap_entry()
    {
        return _execute_logged_command(
            "submap.cancel_entry",
            "submap",
            new GDictionary
            {
                ["target_submap_id"] = _pending_submap_prompt.TargetSubmapId.ToString(),
            },
            () =>
            {
                if (_pending_submap_prompt.IsEmpty)
                    return _command_error("当前没有待确认的子地图入口。");
                string targetName = string.IsNullOrEmpty(_pending_submap_prompt.TargetDisplayName)
                    ? "子地图"
                    : _pending_submap_prompt.TargetDisplayName;
                _pending_submap_prompt.Clear();
                _active_modal_id = "";
                _update_status($"已取消进入 {targetName}。");
                return _command_ok();
            }
        );
    }

    public GDictionary command_confirm_battle_start()
    {
        return _execute_logged_command(
            "battle.confirm_start",
            "battle",
            new GDictionary { ["encounter_id"] = _active_battle_encounter_id },
            () =>
            {
                if (_pending_battle_start_prompt.Count == 0)
                    return _command_error("当前没有待确认的战斗开始提示。");
                if (!_is_battle_active() || _battle_state == null)
                    return _command_error("当前没有待开始的战斗。");
                _pending_battle_start_prompt.Clear();
                _active_modal_id = "";
                _battle_state.modal_state = "";
                if (_battle_state.timeline != null)
                    _battle_state.timeline.frozen = false;
                _update_status("战斗开始，TU 现在按每秒 5 点推进。");
                return _command_ok();
            }
        );
    }

    public GDictionary command_return_from_submap()
    {
        return _execute_logged_command(
            "submap.return",
            "submap",
            new GDictionary { ["active_map_id"] = _world_map_data_context.active_map_id },
            () =>
            {
                if (!is_submap_active())
                    return _command_error("当前不在子地图中。");
                if (_is_battle_active())
                    return _command_error("当前处于战斗中，不能从子地图返回。");
                if (_is_modal_window_open())
                    return _command_error("当前有窗口打开，不能从子地图返回。");
                return _return_from_active_submap();
            }
        );
    }

    public GDictionary command_close_active_modal() =>
        _execute_logged_command(
            "modal.close_active",
            "ui",
            new GDictionary { ["modal_id"] = _active_modal_id },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.command_close_active_modal()
                    : _command_error("运行时尚未初始化。")
        );

    public GDictionary apply_party_roster(
        GStringNameArray active_member_ids,
        GStringNameArray reserve_member_ids
    ) =>
        _execute_logged_command(
            "party.apply_roster",
            "party",
            new GDictionary
            {
                ["active_member_ids"] = active_member_ids,
                ["reserve_member_ids"] = reserve_member_ids,
            },
            () => _party_command_handler.apply_party_roster(active_member_ids, reserve_member_ids)
        );

    public GDictionary submit_promotion_choice(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    ) =>
        _execute_logged_command(
            "promotion.submit_choice",
            "promotion",
            new GDictionary
            {
                ["member_id"] = member_id,
                ["profession_id"] = profession_id,
                ["selection"] = selection,
            },
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.submit_promotion_choice(
                        member_id,
                        profession_id,
                        selection
                    )
                    : _command_error("运行时尚未初始化。")
        );

    public GDictionary cancel_promotion_choice() =>
        _execute_logged_command(
            "promotion.cancel_choice",
            "promotion",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.cancel_promotion_choice()
                    : _command_error("运行时尚未初始化。")
        );

    public GDictionary confirm_active_reward() =>
        _execute_logged_command(
            "reward.confirm_active",
            "reward",
            new GDictionary(),
            () =>
                _reward_flow_handler != null
                    ? _reward_flow_handler.confirm_active_reward()
                    : _command_error("运行时尚未初始化。")
        );

    public GDictionary reset_battle_focus() =>
        _execute_logged_command(
            "battle.reset_focus",
            "battle",
            new GDictionary(),
            () => _battle_session_facade.reset_battle_focus()
        );

    public GDictionary select_world_cell(Vector2I coord)
    {
        return _execute_logged_command(
            "world.click_select",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                if (is_submap_active() && !_is_battle_active() && !_is_modal_window_open())
                    return _return_from_active_submap();
                _on_world_map_cell_clicked(coord);
                return _command_ok();
            }
        );
    }

    public GDictionary inspect_world_cell(Vector2I coord) =>
        _execute_logged_command(
            "world.click_inspect",
            "world",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                _on_world_map_cell_right_clicked(coord);
                return _command_ok();
            }
        );

    public GDictionary select_battle_cell(Vector2I coord) =>
        _execute_logged_command(
            "battle.click_select",
            "battle",
            new GDictionary { ["coord"] = coord },
            () => _battle_session_facade.command_battle_move_to(coord)
        );

    public GDictionary inspect_battle_cell(Vector2I coord) =>
        _execute_logged_command(
            "battle.click_inspect",
            "battle",
            new GDictionary { ["coord"] = coord },
            () =>
            {
                _on_battle_cell_right_clicked(coord);
                return _command_ok();
            }
        );

    public GDictionary CommandShopSell(StringName itemId, int quantity, StringName instanceId) =>
        command_shop_sell(itemId, quantity, instanceId);

    public GDictionary CommandWarehouseDiscardOne(StringName itemId, StringName instanceId) =>
        command_warehouse_discard_one(itemId, instanceId);

    public GDictionary CommandWarehouseDiscardAll(StringName itemId, StringName instanceId) =>
        command_warehouse_discard_all(itemId, instanceId);

    public GDictionary _command_ok() => _command_ok("", "");

    public GDictionary _command_ok(string message) => _command_ok(message, "");

    public GDictionary _command_ok(string message, string battle_refresh_mode) =>
        FinalizeCommandResult(BuildCommandOkResult(message, battle_refresh_mode));

    public GDictionary _command_error(string message) =>
        FinalizeCommandResult(BuildCommandErrorResult(message));

    private RuntimeCommandResult BuildCommandOkResult(
        string message = "",
        string battleRefreshMode = ""
    )
    {
        string resolvedMessage = string.IsNullOrEmpty(message) ? _current_status_message : message;
        return RuntimeCommandResult.Success(resolvedMessage, battleRefreshMode);
    }

    private RuntimeCommandResult BuildCommandErrorResult(string message)
    {
        string resolvedMessage = message ?? "";
        if (!string.IsNullOrEmpty(resolvedMessage))
            _update_status(resolvedMessage);
        return RuntimeCommandResult.Failure(resolvedMessage);
    }

    private GDictionary FinalizeCommandResult(RuntimeCommandResult commandResult)
    {
        var result = (commandResult ?? RuntimeCommandResult.Failure("")).ToDictionary();
        _log_active_command_scope_result(result);
        return result;
    }

    public GDictionary _execute_logged_command(
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

    public void _log_active_command_scope_result(GDictionary result) =>
        _command_logger.LogActiveCommandScopeResult(result);

    public GDictionary _build_runtime_log_state() => _command_logger.BuildRuntimeLogState();

    public void _log_runtime_event(string level, string domain, string event_id, string message) =>
        _command_logger.LogRuntimeEvent(level, domain, event_id, message, "");

    public void _log_runtime_event(
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

    public void _log_battle_batch_entries(BattleEventBatch batch) =>
        _command_logger.LogBattleBatchEntries(batch);

    public GDictionary _build_battle_log_state() => _command_logger.BuildBattleLogState();

    public GDictionary _build_battle_batch_log_context(BattleEventBatch batch) =>
        _command_logger.BuildBattleBatchLogContext(batch);

    public string _resolve_command_settlement_id() =>
        _settlement_command_handler.resolve_command_settlement_id();

    public GDictionary _get_current_promotion_prompt() =>
        _reward_flow_handler != null
            ? _reward_flow_handler.get_current_promotion_prompt()
            : new GDictionary();

    public void _move_player(Vector2I direction)
    {
        if (_game_session == null)
        {
            _update_status("游戏会话不可用，无法移动。");
            return;
        }
        var sourceCoord = _player_coord;
        WorldMapSettlementData previousSettlement =
            _world_map_data_context.GetSettlementAt(sourceCoord);
        var targetCoord = sourceCoord + direction;
        if (!_grid_system.is_cell_walkable(targetCoord))
        {
            _update_status("已到达大地图边界。");
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
            _advance_world_time_by_steps(1);
            _activate_settlement_entry_context(sourceCoord, targetCoord);
            if (_try_open_settlement_at(targetCoord, false))
            {
                int persistError = _game_session.set_world_data(
                    _world_map_data_context.root_world_data
                );
                if (persistError != (int)Error.Ok)
                    _update_status(
                        $"已打开 {targetSettlement.DisplayNameOrFallback("据点")} 的据点窗口，但世界状态持久化失败。"
                    );
                return;
            }
            _clear_settlement_entry_context();
            if (string.IsNullOrEmpty(_current_status_message))
                _update_status("进入据点失败。");
            return;
        }

        _player_coord = targetCoord;
        _selected_coord = _player_coord;
        _advance_world_time_by_steps(1);
        _world_map_data_context.refresh_world_event_discovery();
        _refresh_fog();

        var triggeredEvent = GetTriggerableWorldEventAt(_player_coord);
        if (triggeredEvent != null)
        {
            int playerPersistError = _game_session.set_player_coord(_player_coord);
            int worldPersistError = _game_session.set_world_data(
                _world_map_data_context.root_world_data
            );
            OpenWorldEventPrompt(triggeredEvent);
            if (playerPersistError != (int)Error.Ok || worldPersistError != (int)Error.Ok)
                _update_status(
                    $"{ResolveWorldEventDisplayName(triggeredEvent, "事件入口")} 已显现，但当前位置或世界状态持久化失败。"
                );
            return;
        }

        var encounterAnchor = _get_encounter_anchor_at(_player_coord);
        if (encounterAnchor != null)
        {
            _game_session.set_battle_save_lock(true);
            int playerPersistError = _game_session.set_player_coord(_player_coord);
            int worldPersistError = _game_session.set_world_data(
                _world_map_data_context.root_world_data
            );
            _start_battle(encounterAnchor);
            if (!_is_battle_active() && !_has_pending_battle_generation_request())
            {
                _game_session.set_battle_save_lock(false);
                int flushError = _game_session.flush_game_state();
                _update_status(
                    playerPersistError != (int)Error.Ok
                    || worldPersistError != (int)Error.Ok
                    || flushError != (int)Error.Ok
                        ? "遭遇战未能开始，且玩家位置或世界时间持久化失败。"
                        : "遭遇战未能开始，已保留玩家当前位置与世界时间。"
                );
            }
            return;
        }

        int playerError = _game_session.set_player_coord(_player_coord);
        int worldError = _game_session.set_world_data(_world_map_data_context.root_world_data);
        _update_status(
            playerError == (int)Error.Ok && worldError == (int)Error.Ok
                ? $"玩家移动到 {_format_coord(_player_coord)}，视野与世界时间已刷新。"
                : $"玩家移动到 {_format_coord(_player_coord)}，但大地图位置或世界时间持久化失败。"
        );
    }

    public void _advance_world_time_by_steps(int delta_steps)
    {
        WorldTimeAdvanceResult advanceResult = WorldTimeSystem.AdvanceWorldStep(
            _world_map_data_context.get_world_step(),
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
                _party_state = _character_management.get_party_state();
                _persist_party_state();
            }
        }
    }

    public void _resolve_world_encounter_after_battle(string winner_faction_id)
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
                _world_map_data_context.get_world_step(),
                _wild_encounter_roster_defs
            );
            return;
        }
        _remove_active_battle_encounter_anchor();
    }

    public void start_battle(EncounterAnchorData encounter_anchor) =>
        _start_battle(encounter_anchor);

    public void _start_battle(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.start_battle(encounter_anchor);

    public GDictionary _build_battle_start_context(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.build_battle_start_context(encounter_anchor);

    public StringName _resolve_battle_terrain_profile(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.resolve_battle_terrain_profile(encounter_anchor);

    public void _resolve_active_battle() => _battle_session_facade.resolve_active_battle();

    public StringName _attempt_battle_move(Vector2I direction) =>
        _battle_session_facade.attempt_battle_move(direction);

    public void _refresh_fog()
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

    public void _on_world_map_cell_clicked(Vector2I coord)
    {
        if (_is_battle_active() || _is_modal_window_open())
            return;
        if (is_submap_active())
        {
            var result = ReturnFromActiveSubmapTyped();
            if (!result.Ok && string.IsNullOrEmpty(_current_status_message))
                _update_status(string.IsNullOrEmpty(result.Message) ? "返回主地图失败。" : result.Message);
            return;
        }
        _selected_coord = coord;
        if (_fog_system.is_visible(coord, _player_faction_id) && _try_open_settlement_at(coord))
            return;
        _update_status($"已选中格子 {_format_coord(coord)}。");
    }

    public void _on_world_map_cell_right_clicked(Vector2I coord)
    {
        if (_is_battle_active() || _is_modal_window_open())
            return;
        if (!_fog_system.is_visible(coord, _player_faction_id))
        {
            _update_status("该格当前不在视野中。");
            return;
        }
        if (_try_open_character_info_at_world_coord(coord))
            return;
        _update_status("当前格没有可查看人物。");
    }

    public void _on_battle_cell_clicked(Vector2I coord) =>
        _battle_session_facade.on_battle_cell_clicked(coord);

    public void _on_battle_cell_right_clicked(Vector2I coord) =>
        _battle_session_facade.on_battle_cell_right_clicked(coord);

    public void _on_battle_skill_slot_selected(int index) =>
        _battle_session_facade.on_battle_skill_slot_selected(index);

    public bool _try_open_settlement_at(Vector2I coord) => _try_open_settlement_at(coord, true);

    public bool _try_open_settlement_at(Vector2I coord, bool announce_failure)
    {
        if (_is_battle_active())
            return false;
        if (!_fog_system.is_visible(coord, _player_faction_id))
        {
            if (announce_failure)
                _update_status("该格当前不在视野中。");
            return false;
        }
        WorldMapSettlementData settlement = _world_map_data_context.GetSettlementAt(coord);
        if (settlement.IsEmpty)
        {
            if (announce_failure)
                _update_status("当前格没有可交互据点。");
            return false;
        }
        _active_settlement_id = settlement.SettlementId;
        if (
            coord == _player_coord
            || (_settlement_entry_active && _settlement_entry_target_coord == coord)
        )
            _mark_settlement_visited(_active_settlement_id);
        _active_settlement_feedback_text = "据点通过窗口交付，不切换到城内地图。";
        _active_modal_id = "settlement";
        _update_status(
            $"已打开 {settlement.DisplayNameOrFallback("据点")} 的据点窗口。"
        );
        return true;
    }

    public bool _try_open_character_info_at_world_coord(Vector2I coord)
    {
        WorldMapNpcData npc = _world_map_data_context.GetWorldNpcAt(coord);
        if (!npc.HasValidCharacterInfoFields)
            return false;
        string displayName = npc.DisplayName;
        string factionLabel = _format_faction_label(npc.FactionId);
        _active_character_info_context = new GDictionary
        {
            ["display_name"] = displayName,
            ["meta_label"] = _build_character_info_meta_label("世界 NPC", factionLabel, coord),
            ["sections"] = _build_world_character_info_sections(npc.ToDictionary(), coord, factionLabel),
            ["status_label"] = "可见提示单位",
            ["source"] = "world",
        };
        _active_modal_id = "character_info";
        _update_status($"已打开 {displayName} 的人物信息窗。");
        return true;
    }

    public bool _try_open_character_info_at_battle_coord(Vector2I coord)
    {
        var unit = _get_battle_unit_at_coord(coord);
        if (unit == null)
            return false;
        string unitId = unit.unit_id.ToString();
        string displayName = string.IsNullOrEmpty(unit.display_name) ? unitId : unit.display_name;
        string factionId = unit.faction_id.ToString();
        string typeLabel = _get_battle_unit_type_label(unitId);
        string factionLabel = _format_faction_label(factionId);
        string statusLabel =
            unit.unit_id == _battle_state.active_unit_id ? "当前行动单位" : "战斗单位";
        _active_character_info_context = new GDictionary
        {
            ["display_name"] = displayName,
            ["meta_label"] = _build_character_info_meta_label(typeLabel, factionLabel, unit.coord),
            ["sections"] = _build_battle_character_info_sections(unit, typeLabel, factionLabel),
            ["status_label"] = statusLabel,
            ["source"] = "battle",
            ["unit_id"] = unitId,
        };
        var fatePayload = _build_battle_character_info_fate_payload(unit);
        if (fatePayload.Count > 0)
            _active_character_info_context["fate"] = fatePayload;
        _active_modal_id = "character_info";
        _update_status($"已打开 {displayName} 的人物信息窗。");
        return true;
    }

    public string _build_character_info_meta_label(
        string type_label,
        string faction_label,
        Vector2I coord
    ) => _character_info_builder.build_character_info_meta_label(type_label, faction_label, coord);

    public GArray _build_world_character_info_sections(
        GDictionary npc,
        Vector2I coord,
        string faction_label
    ) =>
        UntypedDictionaryArray(
            _character_info_builder.build_world_character_info_sections(npc, coord, faction_label)
        );

    public GArray _build_battle_character_info_sections(
        BattleUnitState unit,
        string type_label,
        string faction_label
    ) =>
        UntypedDictionaryArray(
            _character_info_builder.build_battle_character_info_sections(
                unit,
                type_label,
                faction_label
            )
        );

    public GDictionary _build_battle_character_info_fate_payload(BattleUnitState unit) =>
        _character_info_builder.build_battle_character_info_fate_payload(unit);

    public GArray _build_battle_character_info_base_entries(
        BattleUnitState unit,
        string type_label,
        string faction_label
    ) =>
        UntypedDictionaryArray(
            _character_info_builder.build_battle_character_info_base_entries(
                unit,
                type_label,
                faction_label
            )
        );

    public GArray _build_battle_character_status_entries(BattleUnitState unit) =>
        UntypedDictionaryArray(_character_info_builder.build_battle_character_status_entries(unit));

    public GArray _build_battle_character_skill_entries(BattleUnitState unit) =>
        UntypedDictionaryArray(_character_info_builder.build_battle_character_skill_entries(unit));

    public int _get_battle_unit_attribute_value(BattleUnitState unit, StringName attribute_id) =>
        _character_info_builder.get_battle_unit_attribute_value(unit, attribute_id);

    public GDictionary _get_settlement_at(Vector2I coord) =>
        _world_map_data_context.get_settlement_at(coord);

    public GDictionary _get_world_npc_at(Vector2I coord) =>
        _world_map_data_context.get_world_npc_at(coord);

    public EncounterAnchorData _get_encounter_anchor_at(Vector2I coord) =>
        _world_map_data_context.get_encounter_anchor_at(coord);

    public EncounterAnchorData _get_encounter_anchor_by_id(StringName entity_id) =>
        _world_map_data_context.get_encounter_anchor_by_id(entity_id);

    public void _refresh_battle_selection_state()
    {
        if (!_is_battle_active())
            return;
        _battle_selection.sync_selected_battle_skill_state();
        if (_battle_state == null || _battle_state.is_empty())
        {
            _refresh_battle_runtime_state();
            return;
        }
        if (
            _battle_selected_coord == new Vector2I(-1, -1)
            || !_battle_state.cells.ContainsKey(_battle_selected_coord)
        )
            _battle_selected_coord = _get_default_battle_selected_coord();
    }

    public void _remove_active_battle_encounter_anchor() =>
        _world_map_data_context.remove_encounter_anchor_by_id(_active_battle_encounter_id);

    public void _on_settlement_action_requested(
        string settlement_id,
        string action_id,
        GDictionary payload
    ) =>
        _settlement_command_handler.on_settlement_action_requested(
            settlement_id,
            action_id,
            payload
        );

    public void _on_settlement_window_closed() =>
        _settlement_command_handler.on_settlement_window_closed();

    public void _on_character_info_window_closed() =>
        _reward_flow_handler?.on_character_info_window_closed();

    public void _open_party_management_window() =>
        _party_command_handler.open_party_management_window();

    public void _on_party_leader_change_requested(StringName member_id) =>
        _party_command_handler.on_party_leader_change_requested(member_id);

    public void _on_party_roster_change_requested(
        GStringNameArray active_member_ids,
        GStringNameArray reserve_member_ids
    ) =>
        _party_command_handler.on_party_roster_change_requested(
            active_member_ids,
            reserve_member_ids
        );

    public void _on_party_management_window_closed() =>
        _party_command_handler.on_party_management_window_closed();

    public void _on_party_management_warehouse_requested() =>
        _party_command_handler.on_party_management_warehouse_requested();

    public void _on_promotion_choice_submitted(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    ) => _reward_flow_handler?.on_promotion_choice_submitted(member_id, profession_id, selection);

    public void _on_promotion_choice_cancelled() =>
        _reward_flow_handler?.on_promotion_choice_cancelled();

    public void _on_character_reward_confirmed() =>
        _reward_flow_handler?.on_character_reward_confirmed();

    public void _apply_party_state_to_runtime(string success_message) =>
        _party_command_handler.apply_party_state_to_runtime(success_message);

    public bool _batch_has_updates(BattleEventBatch batch)
    {
        if (batch == null)
            return false;
        return batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.changed_unit_ids.Count > 0
            || batch.changed_coords.Count > 0
            || batch.log_lines.Count > 0
            || batch.progression_deltas.Count > 0;
    }

    public bool _batch_requires_full_battle_refresh(BattleEventBatch batch)
    {
        if (batch == null)
            return false;
        return batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.changed_coords.Count > 0
            || batch.log_lines.Count > 0
            || batch.progression_deltas.Count > 0;
    }

    public void _apply_battle_batch(BattleEventBatch batch)
    {
        _battle_session_facade.apply_battle_batch(batch);
        _log_battle_batch_entries(batch);
    }

    public void record_command_battle_batch(BattleEventBatch batch)
    {
        if (batch == null)
            return;
        _pending_command_battle_batches.Add(_build_battle_batch_log_context(batch));
    }

    public void refresh_battle_runtime_state() => _refresh_battle_runtime_state();

    public void _refresh_battle_runtime_state() =>
        _battle_session_facade.refresh_battle_runtime_state();

    public int _build_battle_seed(EncounterAnchorData encounter_anchor) =>
        _battle_session_facade.build_battle_seed(encounter_anchor);

    public BattleState _get_runtime_battle_state() =>
        _battle_session_facade.get_runtime_battle_state();

    public bool _is_battle_finished() => _battle_session_facade.is_battle_finished();

    public BattleUnitState _get_runtime_active_unit() =>
        _battle_session_facade.get_runtime_active_unit();

    public BattleUnitState _get_manual_active_unit() =>
        _battle_session_facade.get_manual_active_unit();

    public BattleUnitState _get_runtime_unit_at_coord(Vector2I coord) =>
        _battle_session_facade.get_runtime_unit_at_coord(coord);

    public BattleCommand _build_wait_command() => _battle_session_facade.build_wait_command();

    public StringName _issue_battle_command(BattleCommand command) =>
        _battle_session_facade.issue_battle_command(command);

    public void _capture_pending_promotion_prompt(GArray progression_deltas) =>
        _battle_session_facade.capture_pending_promotion_prompt(progression_deltas);

    public GDictionary _build_promotion_prompt(CharacterProgressionDelta delta) =>
        _build_promotion_prompt(delta, "确认后将在战斗中立即生效。");

    public GDictionary _build_promotion_prompt(CharacterProgressionDelta delta, string selection_hint) =>
        _battle_session_facade.build_promotion_prompt(delta, selection_hint);

    public Vector2I _get_default_battle_selected_coord() =>
        _battle_session_facade.get_default_battle_selected_coord();

    public BattleUnitState _get_battle_unit_by_id(StringName unit_id) =>
        _battle_session_facade.get_battle_unit_by_id(unit_id);

    public BattleUnitState _get_battle_unit_at_coord(Vector2I coord) =>
        _battle_session_facade.get_battle_unit_at_coord(coord);

    public BattleUnitState _get_battle_active_unit() =>
        _battle_session_facade.get_battle_active_unit();

    public string _get_battle_active_unit_name() =>
        _battle_session_facade.get_battle_active_unit_name();

    public string _get_battle_unit_type_label(string unit_id) =>
        _battle_session_facade.get_battle_unit_type_label(unit_id);

    public GDictionary _count_battle_terrain_types() =>
        _battle_session_facade.get_battle_terrain_counts();

    public string _format_optional_text(string value) => string.IsNullOrEmpty(value) ? "无" : value;

    public GArray _build_default_battle_quest_progress_events(string winner_faction_id)
    {
        if (winner_faction_id != "player")
            return new GArray();
        var encounterAnchor = _get_encounter_anchor_by_id(_active_battle_encounter_id);
        if (encounterAnchor == null)
            return new GArray();
        return new GArray
        {
            new GDictionary
            {
                ["event_type"] = "progress",
                ["objective_type"] = "defeat_enemy",
                ["target_id"] = encounterAnchor.enemy_roster_template_id.ToString(),
                ["progress_delta"] = 1,
                ["world_step"] = get_world_step(),
                ["enemy_template_id"] = encounterAnchor.enemy_roster_template_id.ToString(),
                ["encounter_id"] = encounterAnchor.entity_id.ToString(),
                ["encounter_kind"] = encounterAnchor.encounter_kind.ToString(),
            },
        };
    }

    public bool _has_quest_progress_summary_changes(GDictionary summary)
    {
        return DictArray(summary, "accepted_quest_ids").Count > 0
            || DictArray(summary, "progressed_quest_ids").Count > 0
            || DictArray(summary, "claimable_quest_ids").Count > 0
            || DictArray(summary, "completed_quest_ids").Count > 0;
    }

    public string _format_quest_progress_summary(GDictionary summary)
    {
        var parts = new System.Collections.Generic.List<string>();
        var acceptedIds = DictArray(summary, "accepted_quest_ids");
        var progressedIds = DictArray(summary, "progressed_quest_ids");
        var claimableIds = DictArray(summary, "claimable_quest_ids");
        var completedIds = DictArray(summary, "completed_quest_ids");
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

    public GDictionary _get_quest_def_data(StringName quest_id)
    {
        if (_game_session == null || quest_id == "")
            return new GDictionary();
        var questDefs = _game_session.get_quest_defs();
        if (!TryGetExactStringNameKey(questDefs, quest_id, out Variant questValue))
            return new GDictionary();
        if (questValue.VariantType == Variant.Type.Dictionary)
            return questValue.AsGodotDictionary().Duplicate(true);
        if (
            questValue.VariantType == Variant.Type.Object
            && questValue.AsGodotObject() is QuestDef questDef
        )
        {
            return questDef.to_dict().Duplicate(true);
        }
        return new GDictionary();
    }

    public QuestDef _get_quest_def(StringName quest_id)
    {
        if (_game_session == null || quest_id == "")
            return null;
        var questDefs = _game_session.get_quest_defs();
        if (!TryGetExactStringNameKey(questDefs, quest_id, out Variant questValue))
            return null;
        if (questValue.VariantType == Variant.Type.Object)
            return questValue.AsGodotObject() as QuestDef;
        if (questValue.VariantType == Variant.Type.Dictionary)
            return QuestDef.from_dict(questValue.AsGodotDictionary());
        return null;
    }

    private static bool TryGetExactStringNameKey(
        GDictionary dictionary,
        StringName key,
        out Variant value
    )
    {
        if (dictionary == null || key == "")
        {
            value = default;
            return false;
        }
        foreach (Variant rawKey in dictionary.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            if (rawKey.AsStringName() != key)
                continue;
            value = dictionary[rawKey];
            return true;
        }
        value = default;
        return false;
    }

    public GDictionary _quest_progress_summary_to_string_dict(GDictionary summary)
    {
        return new GDictionary
        {
            ["accepted_quest_ids"] = _string_name_array_to_string_array(
                DictArray(summary, "accepted_quest_ids")
            ),
            ["progressed_quest_ids"] = _string_name_array_to_string_array(
                DictArray(summary, "progressed_quest_ids")
            ),
            ["claimable_quest_ids"] = _string_name_array_to_string_array(
                DictArray(summary, "claimable_quest_ids")
            ),
            ["completed_quest_ids"] = _string_name_array_to_string_array(
                DictArray(summary, "completed_quest_ids")
            ),
        };
    }

    public string _format_string_name_list(GArray values)
    {
        var labels = _string_name_array_to_string_array(values);
        var strings = new string[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            strings[i] = labels[i];
        return string.Join("、", strings);
    }

    public Godot.Collections.Array<string> _string_name_array_to_string_array(GArray values)
    {
        var labels = new Godot.Collections.Array<string>();
        foreach (StringName value in ProgressionDataUtils.to_string_name_array(values))
            labels.Add(value.ToString());
        return labels;
    }

    public string _build_quest_claim_reward_summary_text(GDictionary claim_result)
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

    public void _update_status(string message) => _current_status_message = message;

    public bool _is_modal_window_open() => _active_modal_id != "";

    public bool _is_battle_timeline_modal_active() =>
        _is_battle_active() && _battle_state != null && _battle_state.modal_state != "";

    public void _enqueue_pending_character_rewards(GArray reward_options) =>
        _reward_flow_handler?.enqueue_pending_character_rewards(reward_options);

    public bool _present_pending_reward_if_ready() =>
        _reward_flow_handler != null && _reward_flow_handler.present_pending_reward_if_ready();

    public Func<StringName> _get_equipment_instance_id_allocator() =>
        _game_session != null ? _game_session.allocate_equipment_instance_id : null;

    public void _setup_party_warehouse_service(
        PartyWarehouseService service,
        PartyState party_state
    ) =>
        _setup_party_warehouse_service(service, party_state, new GDictionary());

    public void _setup_party_warehouse_service(
        PartyWarehouseService service,
        PartyState party_state,
        GDictionary item_defs
    )
    {
        if (service == null)
            return;
        service.setup(
            party_state,
            item_defs ?? new GDictionary(),
            _get_equipment_instance_id_allocator()
        );
    }

    public void _sync_party_state_services()
    {
        var itemDefs =
            _game_session != null
                ? _game_session.get_item_defs()
                : new GDictionary();
        _character_management?.set_party_state(_party_state);
        _setup_party_warehouse_service(_party_warehouse_service, _party_state, itemDefs);
        if (_party_item_use_service != null && _game_session != null)
            _party_item_use_service.setup(
                _party_state,
                itemDefs,
                _game_session.get_skill_defs(),
                _party_warehouse_service,
                _character_management
            );
        _party_equipment_service?.setup(
            _party_state,
            itemDefs,
            _party_warehouse_service,
            _get_equipment_instance_id_allocator()
        );
    }

    public int _persist_party_state()
    {
        if (_game_session == null)
            return (int)Error.Unavailable;
        int persistError = _game_session.set_party_state(_party_state);
        if (persistError == (int)Error.Ok)
            persistError = _commit_runtime_state("party_state");
        _party_state = _game_session.get_party_state();
        _sync_party_state_services();
        _refresh_fog();
        return persistError;
    }

    public int _persist_world_data()
    {
        if (_game_session == null)
            return (int)Error.Unavailable;
        _save_active_fog_state_to_world_data();
        int stageError = _game_session.set_world_data(_world_map_data_context.root_world_data);
        if (stageError != (int)Error.Ok)
            return stageError;
        return _commit_runtime_state("world_data");
    }

    public int _commit_runtime_state(StringName reason) =>
        _game_session != null
            ? _game_session.commit_runtime_state(reason)
            : (int)Error.Unavailable;

    public void _commit_pending_runtime_state_on_dispose()
    {
        if (_game_session == null)
            return;
        if (
            !_game_session.HasMethod("has_pending_save")
            || !_game_session.has_pending_save()
        )
            return;
        if (
            _game_session.HasMethod("is_battle_save_locked")
            && _game_session.is_battle_save_locked()
        )
            return;
        int commitError = _commit_runtime_state("runtime.dispose");
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

    public void _clear_resolved_battle_runtime_context()
    {
        _active_modal_id = "";
        _pending_battle_start_prompt.Clear();
        _pending_battle_generation_request.Clear();
        _pending_promotion_prompt.Clear();
        _battle_selection.clear_battle_skill_selection(false);
        _battle_state = null;
        _battle_auto_tick_remainder_msec = 0;
        _battle_selected_coord = new Vector2I(-1, -1);
        _active_battle_encounter_id = "";
        _active_battle_encounter_name = "";
        _selected_coord = _player_coord;
    }

    public void _activate_game_over(GDictionary context)
    {
        _active_game_over_context = (context ?? new GDictionary()).Duplicate(true);
        _active_modal_id = "game_over";
    }

    public bool _is_main_character_dead()
    {
        if (_party_state == null)
            return false;
        var memberId = _party_state.get_resolved_main_character_member_id();
        return memberId != "" && _party_state.is_member_dead(memberId);
    }

    public bool _is_main_character_dead_in_battle_state()
    {
        if (_battle_state == null || _party_state == null)
            return false;
        var memberId = _party_state.get_resolved_main_character_member_id();
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

    public GDictionary _build_main_character_game_over_context()
    {
        var memberId =
            _party_state != null
                ? _party_state.get_resolved_main_character_member_id()
                : new StringName("");
        string memberName = _get_member_display_name(memberId);
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

    public void _mark_settlement_visited(string settlement_id)
    {
        if (settlement_id.Length == 0)
            return;
        MarkSettlementVisited(settlement_id);
    }

    public void _activate_settlement_entry_context(Vector2I source_coord, Vector2I target_coord)
    {
        _settlement_entry_active = true;
        _settlement_entry_source_coord = source_coord;
        _settlement_entry_target_coord = target_coord;
    }

    public void _clear_settlement_entry_context() => _clear_settlement_entry_context(true);

    public void _clear_settlement_entry_context(bool reset_selected)
    {
        _settlement_entry_active = false;
        _settlement_entry_source_coord = new Vector2I(-1, -1);
        _settlement_entry_target_coord = new Vector2I(-1, -1);
        if (reset_selected)
            _selected_coord = _player_coord;
    }

    public bool _is_settlement_entry_hidden_on_world_map()
    {
        if (!_settlement_entry_active)
            return false;
        return _active_modal_id == "settlement"
            || _active_modal_id == "shop"
            || _active_modal_id == "contract_board"
            || _active_modal_id == "forge"
            || _active_modal_id == "stagecoach";
    }

    public string _get_item_display_name(StringName item_id)
    {
        var itemDef = _party_warehouse_service.get_item_def(item_id);
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.display_name))
            return itemDef.display_name;
        return item_id.ToString();
    }

    public string _get_skill_display_name(StringName skill_id)
    {
        SkillDef skillDef = null;
        if (_game_session != null)
        if (
            _game_session != null
            && TryGetDictionaryValue(_game_session.get_skill_defs(), skill_id, out Variant skillValue)
            && skillValue.VariantType == Variant.Type.Object
        )
        {
            skillDef = skillValue.AsGodotObject() as SkillDef;
        }
        if (skillDef != null && !string.IsNullOrEmpty(skillDef.display_name))
            return skillDef.display_name;
        return skill_id.ToString();
    }

    public string _get_member_display_name(StringName member_id)
    {
        var memberState = _party_state != null ? _party_state.get_member_state(member_id) : null;
        if (memberState != null && !string.IsNullOrEmpty(memberState.display_name))
            return memberState.display_name;
        return member_id.ToString();
    }

    public string _build_equipment_error_message(GDictionary result, bool is_equip_action)
    {
        var memberId = DictStringName(result, "member_id");
        string slotLabel = DictString(result, "slot_label", "装备槽");
        var itemId = DictStringName(result, "item_id");
        return DictString(result, "error_code") switch
        {
            "member_not_found" => $"未找到队伍成员 {memberId}。",
            "item_not_found" => $"未找到物品定义 {itemId}。",
            "item_not_equipment" => $"{_get_item_display_name(itemId)} 不是可装备物品。",
            "slot_unresolved" => $"{_get_item_display_name(itemId)} 当前没有可用装备槽。",
            "slot_not_allowed" => $"{_get_item_display_name(itemId)} 不能装备到 {slotLabel}。",
            "warehouse_missing_item" =>
                $"共享仓库中没有可用于装备的 {_get_item_display_name(itemId)}。",
            "warehouse_blocked_swap" => $"{slotLabel} 当前没有空间接回被替换下来的装备。",
            "slot_invalid" => "装备槽无效。",
            "slot_empty" => $"{slotLabel} 当前没有已装备物品。",
            "warehouse_full" => $"共享仓库空间不足，无法卸下 {_get_item_display_name(itemId)}。",
            "missing_profession" =>
                $"{_get_member_display_name(memberId)} 当前职业不满足 {_get_item_display_name(itemId)} 的装备要求。",
            "body_size_too_small" =>
                $"{_get_member_display_name(memberId)} 体型过小，无法装备 {_get_item_display_name(itemId)}。",
            "body_size_too_large" =>
                $"{_get_member_display_name(memberId)} 体型过大，无法装备 {_get_item_display_name(itemId)}。",
            "requirement_failed" => $"{_get_item_display_name(itemId)} 不满足装备要求。",
            _ => is_equip_action ? "装备操作失败。" : "卸装操作失败。",
        };
    }

    public string _format_faction_label(string faction_id) =>
        faction_id switch
        {
            "" => "中立",
            "neutral" => "中立",
            "player" => "玩家",
            "hostile" => "敌对",
            _ => faction_id,
        };

    public string _get_fog_state_name(int fog_state)
    {
        if (fog_state == WorldMapFogSystem.FOG_VISIBLE_ID())
            return "当前可见";
        if (fog_state == WorldMapFogSystem.FOG_EXPLORED_ID())
            return "已探索";
        return "未探索";
    }

    public bool _is_battle_active() => _battle_state != null && !_battle_state.is_empty();

    public bool _has_pending_battle_generation_request() =>
        !_pending_battle_generation_request.IsEmpty;

    internal GameRuntimePendingBattleGenerationRequest GetPendingBattleGenerationRequestState() =>
        _pending_battle_generation_request;

    internal void ClearPendingBattleGenerationRequest() =>
        _pending_battle_generation_request.Clear();

    public bool _is_adjacent_4(Vector2I from_coord, Vector2I to_coord) =>
        Math.Abs(from_coord.X - to_coord.X) + Math.Abs(from_coord.Y - to_coord.Y) == 1;

    public string _format_coord(Vector2I coord) => $"({coord.X}, {coord.Y})";

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

    public void _sync_active_world_context()
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
            _fog_system.setup(
                _world_map_data_context.active_generation_config.get_world_size_cells(),
                _get_active_world_fog_state()
            );
            _world_map_data_context.validate_world_system_size_consistency(
                _grid_system,
                _fog_system
            );
        }
    }

    public GDictionary _get_active_world_fog_state() =>
        _world_map_data_context.GetActiveWorldFogState();

    public void _save_active_fog_state_to_world_data()
    {
        _world_map_data_context.SaveActiveWorldFogState(_fog_system);
    }

    public GDictionary _get_world_event_at(Vector2I coord)
    {
        var worldEvent = _world_map_data_context.get_world_event_at(coord);
        return worldEvent.Count > 0 ? worldEvent.Duplicate(true) : new GDictionary();
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
            _update_status($"未找到目标子地图 {targetSubmapId}。");
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
        _active_modal_id = "submap_confirm";
        _update_status(
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

    public GDictionary _confirm_pending_submap_entry()
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
            _active_modal_id = "";
        }
        return result;
    }

    public GDictionary _enter_submap(string submap_id, string source_map_id, Vector2I source_coord)
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
        _refresh_fog();
        int playerPersistError = _game_session.set_player_coord(_player_coord);
        int worldPersistError = _game_session.set_world_data(
            _world_map_data_context.root_world_data
        );
        int commitError = (int)Error.Ok;
        if (playerPersistError == (int)Error.Ok && worldPersistError == (int)Error.Ok)
            commitError = _commit_runtime_state("submap_entry");
        string targetName = enterResult.TargetDisplayName;
        if (
            playerPersistError != (int)Error.Ok
            || worldPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            _update_status($"已进入 {targetName}，但世界状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }
        _update_status($"已进入 {targetName}。{get_submap_return_hint_text()}");
        return BuildCommandOkResult();
    }

    public GDictionary _return_from_active_submap()
    {
        return FinalizeCommandResult(ReturnFromActiveSubmapTyped());
    }

    private RuntimeCommandResult ReturnFromActiveSubmapTyped()
    {
        if (_game_session == null)
            return BuildCommandErrorResult("游戏会话不可用，无法返回主地图。");
        if (!is_submap_active())
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
        _active_modal_id = "";
        _sync_active_world_context();
        _refresh_fog();
        int playerPersistError = _game_session.set_player_coord(_player_coord);
        int worldPersistError = _game_session.set_world_data(
            _world_map_data_context.root_world_data
        );
        int commitError = (int)Error.Ok;
        if (playerPersistError == (int)Error.Ok && worldPersistError == (int)Error.Ok)
            commitError = _commit_runtime_state("submap_return");
        if (
            playerPersistError != (int)Error.Ok
            || worldPersistError != (int)Error.Ok
            || commitError != (int)Error.Ok
        )
        {
            _update_status("已返回原位置，但世界状态持久化失败。");
            return BuildCommandErrorResult(_current_status_message);
        }
        _update_status($"已返回原位置 {_format_coord(_player_coord)}。");
        return BuildCommandOkResult();
    }

    public bool _ensure_submap_generated(string submap_id) =>
        _world_map_data_context.ensure_submap_generated(submap_id);

    public WorldMapGenerationConfig _load_submap_generation_config(string submap_id) =>
        _world_map_data_context.load_submap_generation_config(submap_id);

    public GDictionary _get_mounted_submap_entry(string submap_id) =>
        _world_map_data_context.get_mounted_submap_entry(submap_id);

    public void _set_mounted_submap_entry(string submap_id, GDictionary submap_entry) =>
        _world_map_data_context.set_mounted_submap_entry(submap_id, submap_entry);

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
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return true;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return true;
            }
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

    private string BuildBattleResolvedLogContext(
        GDictionary battleSummary,
        string winnerFactionId,
        GArray resolvedPendingRewards,
        GStringNameArray guidanceUnlocks,
        GStringNameArray misfortuneGuidanceUnlocks,
        GDictionary lowLuckEventResult,
        GDictionary questSummary,
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
                    ? _party_state.get_resolved_main_character_member_id().ToString()
                    : "",
            ["pending_reward_count"] = resolvedPendingRewards.Count,
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
