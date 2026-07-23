using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class BattleMapPanel : Control
{
    [Signal]
    public delegate void battle_cell_clickedEventHandler(Vector2I coord);

    [Signal]
    public delegate void battle_cell_right_clickedEventHandler(Vector2I coord);

    [Signal]
    public delegate void battle_cell_hoveredEventHandler(Vector2I coord);

    [Signal]
    public delegate void battle_skill_slot_selectedEventHandler(int index);

    [Signal]
    public delegate void battle_resolve_pressedEventHandler();

    [Signal]
    public delegate void battle_cycle_variant_pressedEventHandler(int step);

    [Signal]
    public delegate void battle_clear_skill_pressedEventHandler();

    [Signal]
    public delegate void battle_equipment_equip_requestedEventHandler(
        StringName slot_id,
        StringName item_id,
        StringName instance_id
    );

    [Signal]
    public delegate void battle_equipment_unequip_requestedEventHandler(
        StringName slot_id,
        StringName instance_id
    );

    [Signal]
    public delegate void battle_loading_state_changedEventHandler(
        bool is_loading,
        float progress_value
    );

    private const float HOVER_OVERLAY_ANCHOR_OFFSET_Y = 8.0f;
    private const float HOVER_OVERLAY_EDGE_MARGIN = 6.0f;
    private static readonly Vector2I InvalidHoverCoord = new(-1, -1);

    private const float LOADING_PROGRESS_PREPARE = 12.0f;
    private const float LOADING_PROGRESS_DRAW_REQUESTED = 48.0f;
    private const float LOADING_PROGRESS_FRAME_QUEUED = 82.0f;
    private const float LOADING_PROGRESS_READY = 100.0f;
    private const float MIN_BATTLE_LOADING_DURATION_SECONDS = 0.35f;
    private const int MAX_BATTLE_RENDER_READY_FRAMES = 12;
    private static readonly Color BATTLE_BACKGROUND_COLOR = Colors.Black;
    private const string BATTLE_BOARD_SCENE_PATH =
        "res://scenes/ui/battle_board_2d.tscn";
    private ShaderMaterial _skill_icon_grayscale_material;
    private GodotProjectionLease<ShaderMaterial> _presentationLease;

    private readonly BattleHudAdapter _hud_adapter = new();
    private readonly Dictionary<string, Texture2D> _skill_icon_cache = new();

    private SubViewport _map_subviewport;
    private ColorRect _battle_background_rect;
    public BattleBoard2D _battle_board;
    private StringName _revealing_battle_id = "";
    private StringName _revealed_battle_id = "";
    private int _battle_reveal_ticket;
    private float _battle_loading_progress;
    private long _battle_reveal_started_at_msec;

    private bool _has_pending_show_battle_payload;
    private BattleState _pending_battle_state;
    private Vector2I _pending_selected_coord = Vector2I.Zero;
    private StringName _pending_selected_skill_id = "";
    private string _pending_selected_skill_name = "";
    private string _pending_selected_skill_variant_name = "";
    private List<Vector2I> _pending_selected_skill_target_coords = new();
    private List<Vector2I> _pending_selected_skill_valid_target_coords = new();
    private int _pending_selected_skill_required_coord_count;
    private List<StringName> _pending_selected_skill_target_unit_ids = new();
    private StringName _pending_selected_skill_variant_id = "";

    private BattleHudEquipmentPanelSnapshot _battleEquipmentSnapshot;
    private string _battle_equipment_feedback_text = "";
    private StringName _selected_backpack_instance_id = "";
    private StringName _selected_backpack_slot_id = "";
    private readonly List<BattleHudBackpackEntrySnapshot> _battleEquipmentBackpackEntriesByIndex =
        new();
    private readonly List<StringName> _battle_equipment_slot_ids_by_index = new();
    private Button _battle_equipment_button;
    private Control _battle_equipment_overlay;
    private HFlowContainer _status_badge_row;
    private PanelContainer _zoom_chip;
    private Label _zoom_value_label;
    private Button _reset_view_button;
    public Label _battle_equipment_title_label;
    public Label _battle_equipment_meta_label;
    public Label _battle_equipment_summary_label;
    public Label _battle_equipment_status_label;
    public Label barrier_status_label;
    public VBoxContainer _battle_equipment_slot_list;
    public ItemList _battle_equipment_backpack_list;
    public Label _battle_equipment_details_label;
    public OptionButton _battle_equipment_slot_selector;
    public Button _battle_equipment_equip_button;
    public Button _battle_equipment_close_button;
    private WorldMapRuntimeProxy _runtime_proxy;

    public PanelContainer map_frame;
    public SubViewportContainer map_viewport_container;
    public PanelContainer top_bar;
    public PanelContainer bottom_panel;
    public Label header_title_label;
    public Label objective_status_label;
    public HBoxContainer timeline_row;
    public PanelContainer round_chip;
    public Label tu_label;
    public Label ready_label;
    public PanelContainer mode_chip;
    public Label mode_value_label;
    public PanelContainer unit_card;
    public PanelContainer portrait_frame;
    public Label portrait_glyph_label;
    public Label unit_name_label;
    public Label unit_role_label;
    public ProgressBar hp_bar;
    public Label hp_value_label;
    public ProgressBar stamina_bar;
    public Label stamina_value_label;
    public ProgressBar mp_bar;
    public Label mp_value_label;
    public ProgressBar aura_bar;
    public Label aura_value_label;
    public HBoxContainer ap_dot_container;
    public Label ap_value_label;
    public VBoxContainer equipment_button_slot;
    public PanelContainer skill_panel;
    public HBoxContainer skill_header;
    public Label skill_subtitle_label;
    public HFlowContainer fate_badge_row;
    public GridContainer skill_grid;
    public BattleHoverPreviewOverlay hover_overlay;

    // Command dock (A2): rebuilt button band + info column, created in code so the
    // enable states / signals live next to each other. See _create_command_dock.
    public Button resolve_button;
    public Button clear_skill_button;
    public Button prev_variant_button;
    public Button next_variant_button;
    public Label variant_name_label;
    public Label command_summary_label;
    public Label hint_label;
    public Label log_label;

    private Vector2I _hover_preview_coord = InvalidHoverCoord;
    private List<Vector2I> _hover_preview_valid_coords = new();
    private StringName _hover_preview_selected_skill_id = "";
    private StringName _hover_preview_selected_skill_variant_id = "";
    private BattleState _hover_preview_battle_state;
    private BattlePreview _selected_skill_preview_cache;
    private StringName _selected_skill_preview_cache_battle_id = "";
    private Vector2I _selected_skill_preview_cache_coord = InvalidHoverCoord;
    private StringName _selected_skill_preview_cache_skill_id = "";
    private StringName _selected_skill_preview_cache_variant_id = "";

    public static Vector2I INVALID_HOVER_COORD() => InvalidHoverCoord;

    public override void _Ready()
    {
        Visible = false;
        map_frame = GetNode<PanelContainer>("%MapFrame");
        map_viewport_container = GetNode<SubViewportContainer>("%MapViewportContainer");
        top_bar = GetNode<PanelContainer>("%TopBar");
        bottom_panel = GetNode<PanelContainer>("%BottomPanel");
        header_title_label = GetNode<Label>("%HeaderTitleLabel");
        objective_status_label = GetNode<Label>("%ObjectiveStatusLabel");
        timeline_row = GetNode<HBoxContainer>("%TimelineRow");
        round_chip = GetNode<PanelContainer>("%RoundChip");
        tu_label = GetNode<Label>("%TuLabel");
        ready_label = GetNode<Label>("%ReadyLabel");
        mode_chip = GetNode<PanelContainer>("%ModeChip");
        mode_value_label = GetNode<Label>("%ModeValueLabel");
        unit_card = GetNode<PanelContainer>("%UnitCard");
        portrait_frame = GetNode<PanelContainer>("%PortraitFrame");
        portrait_glyph_label = GetNode<Label>("%PortraitGlyphLabel");
        unit_name_label = GetNode<Label>("%UnitNameLabel");
        unit_role_label = GetNode<Label>("%UnitRoleLabel");
        hp_bar = GetNode<ProgressBar>("%HpBar");
        hp_value_label = GetNode<Label>("%HpValueLabel");
        stamina_bar = GetNode<ProgressBar>("%StaminaBar");
        stamina_value_label = GetNode<Label>("%StaminaValueLabel");
        mp_bar = GetNode<ProgressBar>("%MpBar");
        mp_value_label = GetNode<Label>("%MpValueLabel");
        aura_bar = GetNode<ProgressBar>("%AuraBar");
        aura_value_label = GetNode<Label>("%AuraValueLabel");
        ap_dot_container = GetNode<HBoxContainer>("%ApDotContainer");
        ap_value_label = GetNode<Label>("%ApValueLabel");
        equipment_button_slot = GetNode<VBoxContainer>("%EquipmentButtonSlot");
        skill_panel = GetNode<PanelContainer>("%SkillPanel");
        skill_header = GetNode<HBoxContainer>("%SkillHeader");
        skill_subtitle_label = GetNode<Label>("%SkillSubtitleLabel");
        fate_badge_row = GetNode<HFlowContainer>("%FateBadgeRow");
        skill_grid = GetNode<GridContainer>("%SkillGrid");
        skill_grid.Resized += _update_skill_grid_columns;
        hover_overlay = GetNode<BattleHoverPreviewOverlay>("%HoverPreviewOverlay");

        _create_command_dock();
        _create_status_badge_row();
        _create_viewport_controls();
        _ensure_battle_board();
        _update_zoom_chip();
        map_viewport_container.GuiInput += _on_map_viewport_container_gui_input;
        _apply_static_skin();
        _ensure_battle_equipment_ui();
        _set_placeholder_state();
        _update_battle_loading_state(false, 0.0f);
        _resize_map_viewport();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            _resize_map_viewport();
    }

    public override void _ExitTree()
    {
        if (skill_grid != null)
            skill_grid.Resized -= _update_skill_grid_columns;
        if (map_viewport_container != null)
            map_viewport_container.GuiInput -= _on_map_viewport_container_gui_input;
        if (_battle_board != null)
        {
            _battle_board.battle_cell_clicked -= _on_battle_board_cell_clicked;
            _battle_board.battle_cell_right_clicked -= _on_battle_board_cell_right_clicked;
            _battle_board.battle_cell_hovered -= _on_battle_board_cell_hovered;
        }
        if (_reset_view_button != null)
            _reset_view_button.Pressed -= _on_reset_view_pressed;
        if (_battle_equipment_button != null)
            _battle_equipment_button.Pressed -= _open_battle_equipment_panel;
        if (_battle_equipment_close_button != null)
            _battle_equipment_close_button.Pressed -= _close_battle_equipment_panel;
        if (_battle_equipment_equip_button != null)
            _battle_equipment_equip_button.Pressed -= _on_battle_equipment_equip_pressed;
        _runtime_proxy = null;
        _hud_adapter.SetupRuntimeContext(null, null);
        _hud_adapter.Dispose();
        ClearSelectedSkillPreviewCache();
        ClearSkillIconPresentationBindings();
        _skill_icon_cache.Clear();
        _battleEquipmentBackpackEntriesByIndex.Clear();
        _battle_equipment_slot_ids_by_index.Clear();
        GodotProjectionLease<ShaderMaterial> presentationLease =
            _presentationLease;
        _presentationLease = null;
        if (_skill_icon_grayscale_material != null)
            _skill_icon_grayscale_material.Shader = null;
        _skill_icon_grayscale_material = null;
        presentationLease?.Dispose();
        ClearDynamicNodeRefs();
    }

    private void ClearDynamicNodeRefs()
    {
        _map_subviewport = null;
        _battle_background_rect = null;
        _battle_board = null;
        _battle_equipment_overlay = null;
        _battle_equipment_title_label = null;
        _battle_equipment_meta_label = null;
        _battle_equipment_summary_label = null;
        _battle_equipment_status_label = null;
        _battle_equipment_slot_list = null;
        _battle_equipment_backpack_list = null;
        _battle_equipment_details_label = null;
        _battle_equipment_slot_selector = null;
        _battle_equipment_equip_button = null;
        _battle_equipment_close_button = null;
        _status_badge_row = null;
        _zoom_chip = null;
        _zoom_value_label = null;
        _reset_view_button = null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_battle_equipment_overlay == null || !_battle_equipment_overlay.Visible)
            return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            _close_battle_equipment_panel();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventKey or InputEventMouseButton)
            GetViewport().SetInputAsHandled();
    }

    public bool IsLoadingBattle() => !StringNameIsEmpty(_revealing_battle_id);

    public float GetLoadingProgress() => _battle_loading_progress;

    public bool IsBattleRenderContentReady() =>
        _battle_board != null && _battle_board.IsRenderContentReady();

    public bool PanBattleCamera(Vector2I direction)
    {
        if (_battle_board == null)
            return false;
        bool didPan = _battle_board.PanViewportDirection(direction);
        if (didPan)
            _request_map_viewport_update();
        return didPan;
    }

    internal void SetupRuntimeContext(
        WorldMapRuntimeProxy runtimeProxy,
        GameRuntimeFacade runtime,
        GameSession gameSession
    )
    {
        _runtime_proxy = runtimeProxy;
        _hud_adapter.SetupRuntimeContext(runtime, gameSession);
    }

    public void ShowBattle(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id
    )
    {
        StringName battleId = _resolve_battle_id(battle_state);
        _store_pending_show_battle_payload(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name,
            selected_skill_variant_name,
            selected_skill_target_coords,
            selected_skill_valid_target_coords,
            selected_skill_required_coord_count,
            selected_skill_target_unit_ids,
            selected_skill_variant_id
        );
        int revealTicket = _begin_battle_reveal_if_needed(battleId);
        Visible = true;
        if (revealTicket > 0)
        {
            _begin_battle_first_presented_frame(revealTicket, battleId);
            return;
        }
        if (_revealing_battle_id == battleId)
            return;
        Refresh(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name,
            selected_skill_variant_name,
            selected_skill_target_coords,
            selected_skill_valid_target_coords,
            selected_skill_required_coord_count,
            selected_skill_target_unit_ids,
            selected_skill_variant_id
        );
    }

    private void _store_pending_show_battle_payload(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id
    )
    {
        _has_pending_show_battle_payload = true;
        _pending_battle_state = battle_state;
        _pending_selected_coord = selected_coord;
        _pending_selected_skill_id = NormalizeStringName(selected_skill_id);
        _pending_selected_skill_name = selected_skill_name ?? "";
        _pending_selected_skill_variant_name = selected_skill_variant_name ?? "";
        _pending_selected_skill_target_coords = CloneVector2IList(selected_skill_target_coords);
        _pending_selected_skill_valid_target_coords = CloneVector2IList(
            selected_skill_valid_target_coords
        );
        _pending_selected_skill_required_coord_count = selected_skill_required_coord_count;
        _pending_selected_skill_target_unit_ids = CloneStringNameList(
            selected_skill_target_unit_ids
        );
        _pending_selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
    }

    public void RefreshOverlay(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id
    )
    {
        _refresh_internal(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name,
            selected_skill_variant_name,
            selected_skill_target_coords,
            selected_skill_valid_target_coords,
            selected_skill_required_coord_count,
            selected_skill_target_unit_ids,
            selected_skill_variant_id,
            false
        );
    }

    internal void RefreshOverlayWithPreview(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id,
        BattlePreview selected_skill_preview
    )
    {
        _refresh_internal(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name,
            selected_skill_variant_name,
            selected_skill_target_coords,
            selected_skill_valid_target_coords,
            selected_skill_required_coord_count,
            selected_skill_target_unit_ids,
            selected_skill_variant_id,
            false,
            selected_skill_preview,
            true
        );
    }

    internal bool RefreshOverlayWithCachedPreview(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id
    )
    {
        if (
            !HasCachedSelectedSkillPreview(
                battle_state,
                selected_coord,
                selected_skill_id,
                selected_skill_variant_id
            )
        )
        {
            return false;
        }
        RefreshOverlayWithPreview(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name,
            selected_skill_variant_name,
            selected_skill_target_coords,
            selected_skill_valid_target_coords,
            selected_skill_required_coord_count,
            selected_skill_target_unit_ids,
            selected_skill_variant_id,
            _selected_skill_preview_cache
        );
        return true;
    }

    public void RefreshUnits(
        BattleState battle_state,
        IEnumerable<StringName> changed_unit_ids
    )
    {
        if (_battle_board == null || battle_state == null || changed_unit_ids == null)
            return;
        _battle_board.RefreshUnits(battle_state, changed_unit_ids);
        _request_map_viewport_update();
    }

    public void RefreshLogs(BattleState battle_state)
    {
        if (log_label == null)
            return;
        log_label.Text = BuildRecentLogText(battle_state);
    }

    internal static string BuildRecentLogText(BattleState battleState) =>
        string.Join("\n", BattleHudAdapter.BuildRecentBattleLogLines(battleState));

    public void Refresh(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id
    )
    {
        _refresh_internal(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name,
            selected_skill_variant_name,
            selected_skill_target_coords,
            selected_skill_valid_target_coords,
            selected_skill_required_coord_count,
            selected_skill_target_unit_ids,
            selected_skill_variant_id,
            true
        );
    }

    private void _refresh_internal(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        IEnumerable<Vector2I> selected_skill_target_coords,
        IEnumerable<Vector2I> selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        IEnumerable<StringName> selected_skill_target_unit_ids,
        StringName selected_skill_variant_id,
        bool redraw_board,
        BattlePreview selected_skill_preview_override = null,
        bool use_selected_skill_preview_override = false
    )
    {
        if (battle_state == null)
        {
            HideBattle();
            return;
        }

        List<Vector2I> targetCoords = CloneVector2IList(selected_skill_target_coords);
        List<Vector2I> validTargetCoords = CloneVector2IList(selected_skill_valid_target_coords);
        List<StringName> targetUnitIds = CloneStringNameList(selected_skill_target_unit_ids);
        selected_skill_id = NormalizeStringName(selected_skill_id);
        selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        BattlePreview selectedSkillPreview = use_selected_skill_preview_override
            ? selected_skill_preview_override
            : !StringNameIsEmpty(selected_skill_id)
                ? _runtime_proxy?.PreviewSelectedBattleSkillAtCoord(selected_coord)
                : null;
        if (!use_selected_skill_preview_override)
        {
            CacheSelectedSkillPreview(
                battle_state,
                selected_coord,
                selected_skill_id,
                selected_skill_variant_id,
                selectedSkillPreview
            );
        }

        BattleHudSnapshot snapshot = _hud_adapter.BuildSnapshot(
            battle_state,
            selected_coord,
            selected_skill_id,
            selected_skill_name ?? "",
            selected_skill_variant_name ?? "",
            targetCoords,
            selected_skill_required_coord_count,
            targetUnitIds,
            selected_skill_variant_id,
            _resolve_encounter_display_name(),
            selectedSkillPreview
        );
        _apply_snapshot(snapshot);
        if (_battle_board != null)
        {
            StringName targetSelectionMode = new(
                string.IsNullOrEmpty(snapshot.SelectedSkillTargetSelectionMode)
                    ? "single_unit"
                    : snapshot.SelectedSkillTargetSelectionMode
            );
            if (StringNameIsEmpty(selected_skill_id) && validTargetCoords.Count > 0)
                targetSelectionMode = "movement";
            int minCount = snapshot.SelectedSkillTargetMinCount;
            int maxCount = snapshot.SelectedSkillTargetMaxCount;
            IReadOnlyDictionary<Vector2I, string> hitBadges =
                _build_selected_skill_target_hit_badges(
                selected_coord,
                selected_skill_id,
                targetCoords,
                validTargetCoords,
                snapshot
            );
            if (redraw_board)
            {
                _battle_board.Configure(
                    battle_state,
                    selected_coord,
                    targetCoords,
                    validTargetCoords,
                    targetSelectionMode,
                    minCount,
                    maxCount,
                    hitBadges
                );
            }
            else
            {
                _battle_board.UpdateSelection(
                    selected_coord,
                    targetCoords,
                    validTargetCoords,
                    targetSelectionMode,
                    minCount,
                    maxCount,
                    hitBadges
                );
            }
            _request_map_viewport_update();
        }
        if (redraw_board)
            _resize_map_viewport();
    }

    private IReadOnlyDictionary<Vector2I, string> _build_selected_skill_target_hit_badges(
        Vector2I selected_coord,
        StringName selected_skill_id,
        IReadOnlyCollection<Vector2I> selected_skill_target_coords,
        IReadOnlyCollection<Vector2I> selected_skill_valid_target_coords,
        BattleHudSnapshot snapshot
    )
    {
        var badges = new Dictionary<Vector2I, string>();
        if (StringNameIsEmpty(selected_skill_id))
            return badges;
        string badgeText = snapshot?.SelectedSkillHitBadgeText ?? "";
        if (string.IsNullOrEmpty(badgeText))
            return badges;
        bool canShowOnSelected =
            selected_skill_valid_target_coords.Contains(selected_coord)
            || selected_skill_target_coords.Contains(selected_coord);
        if (canShowOnSelected)
            badges[selected_coord] = badgeText;
        return badges;
    }

    public void HideBattle()
    {
        _cancel_battle_reveal();
        _has_pending_show_battle_payload = false;
        _close_battle_equipment_panel();
        ClearHoverPreview();
        ClearSelectedSkillPreviewCache();
        _set_placeholder_state();
        Visible = false;
        _battle_board?.ClearBoard();
        _request_map_viewport_update();
    }

    private StringName _resolve_battle_id(BattleState battle_state) =>
        battle_state != null ? battle_state.battle_id : new StringName("");

    private int _begin_battle_reveal_if_needed(StringName battle_id)
    {
        if (StringNameIsEmpty(battle_id))
            return 0;
        if (battle_id == _revealed_battle_id || battle_id == _revealing_battle_id)
            return 0;
        _battle_reveal_ticket += 1;
        int revealTicket = _battle_reveal_ticket;
        _revealing_battle_id = battle_id;
        _revealed_battle_id = "";
        _battle_reveal_started_at_msec = (long)Time.GetTicksMsec();
        _update_battle_loading_state(true, LOADING_PROGRESS_PREPARE);
        return revealTicket;
    }

    private void _cancel_battle_reveal()
    {
        _battle_reveal_ticket += 1;
        _revealing_battle_id = "";
        _revealed_battle_id = "";
        _update_battle_loading_state(false, 0.0f);
    }

    private void _begin_battle_first_presented_frame(int reveal_ticket, StringName battle_id)
    {
        _complete_battle_reveal_async(reveal_ticket, battle_id);
    }

    private async void _complete_battle_reveal_async(int reveal_ticket, StringName battle_id)
    {
        if (!_is_battle_reveal_current(reveal_ticket, battle_id))
            return;
        _update_battle_loading_state(true, LOADING_PROGRESS_DRAW_REQUESTED);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!_is_battle_reveal_current(reveal_ticket, battle_id))
            return;
        _apply_pending_show_battle_payload();
        if (!_is_battle_reveal_current(reveal_ticket, battle_id))
            return;
        bool contentReady = IsBattleRenderContentReady();
        int waitedFrames = 0;
        while (!contentReady && waitedFrames < MAX_BATTLE_RENDER_READY_FRAMES)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_is_battle_reveal_current(reveal_ticket, battle_id))
                return;
            _request_map_viewport_update();
            contentReady = IsBattleRenderContentReady();
            waitedFrames += 1;
        }
        if (!_is_battle_reveal_current(reveal_ticket, battle_id))
            return;
        _update_battle_loading_state(true, LOADING_PROGRESS_FRAME_QUEUED);
        // 等按需渲染的这一帧真正落地 GPU 再揭示，避免首帧拿到陈旧/半成品帧（SubViewport 为 UPDATE_ONCE）。
        // headless 下 frame_post_draw 永不触发，直接 await 会永久挂起，必须退回 process_frame。
        if (DisplayServer.GetName() == "headless")
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        else
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (!_is_battle_reveal_current(reveal_ticket, battle_id))
            return;
        float elapsedSeconds =
            ((long)Time.GetTicksMsec() - _battle_reveal_started_at_msec) / 1000.0f;
        float remainingSeconds = MIN_BATTLE_LOADING_DURATION_SECONDS - elapsedSeconds;
        if (remainingSeconds > 0.0f)
        {
            long targetTimeMsec =
                _battle_reveal_started_at_msec
                + Mathf.RoundToInt(MIN_BATTLE_LOADING_DURATION_SECONDS * 1000.0f);
            while ((long)Time.GetTicksMsec() < targetTimeMsec)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!_is_battle_reveal_current(reveal_ticket, battle_id))
                    return;
            }
        }
        if (!_is_battle_reveal_current(reveal_ticket, battle_id))
            return;
        _revealing_battle_id = "";
        _revealed_battle_id = battle_id;
        Visible = true;
        _update_battle_loading_state(false, LOADING_PROGRESS_READY);
    }

    private void _apply_pending_show_battle_payload()
    {
        if (!_has_pending_show_battle_payload)
            return;
        Refresh(
            _pending_battle_state,
            _pending_selected_coord,
            _pending_selected_skill_id,
            _pending_selected_skill_name,
            _pending_selected_skill_variant_name,
            CloneVector2IList(_pending_selected_skill_target_coords),
            CloneVector2IList(_pending_selected_skill_valid_target_coords),
            _pending_selected_skill_required_coord_count,
            CloneStringNameList(_pending_selected_skill_target_unit_ids),
            _pending_selected_skill_variant_id
        );
    }

    private bool _is_battle_reveal_current(int reveal_ticket, StringName battle_id)
    {
        return reveal_ticket == _battle_reveal_ticket
            && !StringNameIsEmpty(battle_id)
            && battle_id == _revealing_battle_id;
    }

    private void _update_battle_loading_state(bool is_loading, float progress_value)
    {
        _battle_loading_progress = Mathf.Clamp(progress_value, 0.0f, LOADING_PROGRESS_READY);
        EmitSignal(SignalName.battle_loading_state_changed, is_loading, _battle_loading_progress);
    }

    public void _on_battle_board_cell_clicked(Vector2I coord) =>
        EmitSignal(SignalName.battle_cell_clicked, coord);

    public void _on_battle_board_cell_right_clicked(Vector2I coord) =>
        EmitSignal(SignalName.battle_cell_right_clicked, coord);

    public void _on_battle_board_cell_hovered(Vector2I coord) =>
        EmitSignal(SignalName.battle_cell_hovered, coord);

    public BattlePreview UpdateHoverPreview(
        BattleState battle_state,
        Vector2I hover_coord,
        IEnumerable<Vector2I> valid_target_coords,
        StringName selected_skill_id,
        StringName selected_skill_variant_id
    )
    {
        if (hover_overlay == null)
            return null;
        if (battle_state == null || hover_coord == InvalidHoverCoord)
        {
            _clear_hover_preview_state();
            hover_overlay.Clear();
            return null;
        }
        List<Vector2I> validTargetCoords = CloneVector2IList(valid_target_coords);
        _hover_preview_battle_state = battle_state;
        _hover_preview_coord = hover_coord;
        _hover_preview_valid_coords = CloneVector2IList(validTargetCoords);
        _hover_preview_selected_skill_id = NormalizeStringName(selected_skill_id);
        _hover_preview_selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        BattlePreview hoverPreview = !StringNameIsEmpty(_hover_preview_selected_skill_id)
            ? _runtime_proxy?.PreviewSelectedBattleSkillAtCoord(hover_coord)
            : null;

        BattleHoverSnapshot preview = _hud_adapter.BuildHoverPreview(
            battle_state,
            hover_coord,
            _hover_preview_selected_skill_id,
            _hover_preview_selected_skill_variant_id,
            validTargetCoords,
            hoverPreview
        );
        hover_overlay.ApplyPreview(preview);
        if (!hover_overlay.Visible)
            return hoverPreview;
        _position_hover_overlay(hover_coord);
        return hoverPreview;
    }

    public void ClearHoverPreview()
    {
        _clear_hover_preview_state();
        hover_overlay?.Clear();
    }

    private void _clear_hover_preview_state()
    {
        _hover_preview_battle_state = null;
        _hover_preview_coord = InvalidHoverCoord;
        _hover_preview_valid_coords.Clear();
        _hover_preview_selected_skill_id = "";
        _hover_preview_selected_skill_variant_id = "";
    }

    private void CacheSelectedSkillPreview(
        BattleState battleState,
        Vector2I selectedCoord,
        StringName selectedSkillId,
        StringName selectedSkillVariantId,
        BattlePreview preview
    )
    {
        if (battleState == null || StringNameIsEmpty(selectedSkillId))
        {
            ClearSelectedSkillPreviewCache();
            return;
        }
        _selected_skill_preview_cache = preview;
        _selected_skill_preview_cache_battle_id = battleState.battle_id;
        _selected_skill_preview_cache_coord = selectedCoord;
        _selected_skill_preview_cache_skill_id = NormalizeStringName(selectedSkillId);
        _selected_skill_preview_cache_variant_id = NormalizeStringName(selectedSkillVariantId);
    }

    private bool HasCachedSelectedSkillPreview(
        BattleState battleState,
        Vector2I selectedCoord,
        StringName selectedSkillId,
        StringName selectedSkillVariantId
    )
    {
        return battleState != null
            && _selected_skill_preview_cache_battle_id == battleState.battle_id
            && _selected_skill_preview_cache_coord == selectedCoord
            && _selected_skill_preview_cache_skill_id == NormalizeStringName(selectedSkillId)
            && _selected_skill_preview_cache_variant_id
                == NormalizeStringName(selectedSkillVariantId);
    }

    private void ClearSelectedSkillPreviewCache()
    {
        _selected_skill_preview_cache = null;
        _selected_skill_preview_cache_battle_id = "";
        _selected_skill_preview_cache_coord = InvalidHoverCoord;
        _selected_skill_preview_cache_skill_id = "";
        _selected_skill_preview_cache_variant_id = "";
    }

    public override void _Process(double delta)
    {
        if (hover_overlay == null || !hover_overlay.Visible)
            return;
        if (_hover_preview_coord == InvalidHoverCoord)
            return;
        _position_hover_overlay(_hover_preview_coord);
    }

    // 放大后的 preview 跟随悬停会压住战场单位贴图,改为固定钉在地图视口左侧居中
    // (右上角已被战斗日志占用),不再随光标移动。hover_coord 保留参数以兼容调用点。
    private void _position_hover_overlay(Vector2I hover_coord)
    {
        if (hover_overlay == null || map_viewport_container == null)
            return;
        hover_overlay.ResetSize();
        Vector2 overlaySize = hover_overlay.Size;
        Vector2 mapPosition = map_viewport_container.Position;
        Vector2 mapSize = map_viewport_container.Size;
        float x = mapPosition.X + HOVER_OVERLAY_EDGE_MARGIN;
        float y = mapPosition.Y + Mathf.Max((mapSize.Y - overlaySize.Y) * 0.5f, HOVER_OVERLAY_EDGE_MARGIN);
        hover_overlay.Position = new Vector2(x, y);
    }

    private void _on_map_viewport_container_gui_input(InputEvent @event)
    {
        if (_battle_board == null)
            return;
        if (@event is InputEventMouseMotion motionEvent)
        {
            if (
                _battle_board.HandleViewportMouseMotion(
                    motionEvent.Position,
                    (int)motionEvent.ButtonMask
                )
            )
            {
                _request_map_viewport_update();
                AcceptEvent();
            }
            return;
        }
        if (@event is not InputEventMouseButton mouseEvent)
            return;

        if (mouseEvent.ButtonIndex == MouseButton.Middle)
        {
            if (mouseEvent.Pressed)
                _battle_board.BeginViewportPan(mouseEvent.Position);
            else
                _battle_board.EndViewportPan();
            _request_map_viewport_update();
            AcceptEvent();
            return;
        }
        if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.WheelUp)
        {
            if (_battle_board.ZoomViewport(1, mouseEvent.Position))
            {
                _request_map_viewport_update();
                AcceptEvent();
            }
            return;
        }
        if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.WheelDown)
        {
            if (_battle_board.ZoomViewport(-1, mouseEvent.Position))
            {
                _request_map_viewport_update();
                AcceptEvent();
            }
            return;
        }
        if (!mouseEvent.Pressed || _battle_board.IsViewportPanning())
            return;
        if (
            _battle_board.HandleViewportMouseButton(
                mouseEvent.Position,
                (int)mouseEvent.ButtonIndex
            )
        )
            AcceptEvent();
    }

    private void _ensure_battle_board()
    {
        if (_battle_board != null)
            return;

        _map_subviewport = new SubViewport
        {
            Name = "MapSubViewport",
            Disable3D = true,
            TransparentBg = true,
            HandleInputLocally = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        map_viewport_container.AddChild(_map_subviewport);

        _battle_background_rect = new ColorRect
        {
            Name = "BattleBackground",
            MouseFilter = MouseFilterEnum.Ignore,
            Color = BATTLE_BACKGROUND_COLOR,
            ZIndex = -4096,
        };
        _map_subviewport.AddChild(_battle_background_rect);

        Node boardInstance = EngineAssetAccess
            .ResolveBorrowed<PackedScene>(this, BATTLE_BOARD_SCENE_PATH)
            .Instantiate();
        _battle_board = boardInstance as BattleBoard2D;
        if (_battle_board == null)
            return;
        _map_subviewport.AddChild(_battle_board);
        _battle_board.battle_cell_clicked += _on_battle_board_cell_clicked;
        _battle_board.battle_cell_right_clicked += _on_battle_board_cell_right_clicked;
        _battle_board.battle_cell_hovered += _on_battle_board_cell_hovered;
    }

    private void _resize_map_viewport()
    {
        if (map_viewport_container == null || _map_subviewport == null || _battle_board == null)
            return;
        Vector2 containerSize = map_viewport_container.Size;
        Vector2I viewportSize = new(
            Mathf.Max(Mathf.RoundToInt(containerSize.X), 1),
            Mathf.Max(Mathf.RoundToInt(containerSize.Y), 1)
        );
        _map_subviewport.Size = viewportSize;
        if (_battle_background_rect != null)
            _battle_background_rect.Size = viewportSize;
        _battle_board.SetViewportSize(viewportSize);
        _request_map_viewport_update();
    }

    private void _request_map_viewport_update()
    {
        _update_zoom_chip();
        if (_map_subviewport == null)
            return;
        _map_subviewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void _apply_static_skin()
    {
        AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());

        foreach (PanelContainer panel in new[] { map_frame, bottom_panel })
        {
            panel.AddThemeStyleboxOverride(
                "panel",
                _build_panel_style(
                    BattleUiTheme.PANEL_BG(),
                    BattleUiTheme.PANEL_EDGE_SOFT(),
                    BattleUiTheme.PANEL_RADIUS_LARGE(),
                    BattleUiTheme.PANEL_BORDER(),
                    new Color(0, 0, 0, 0)
                )
            );
        }

        top_bar.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                BattleUiTheme.PANEL_BG(),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                BattleUiTheme.TOPBAR_RADIUS(),
                BattleUiTheme.PANEL_BORDER(),
                new Color(0, 0, 0, 0),
                BattleUiTheme.PANEL_CONTENT_MARGIN()
            )
        );

        foreach (PanelContainer paddedPanel in new[] { unit_card, skill_panel })
        {
            paddedPanel.AddThemeStyleboxOverride(
                "panel",
                _build_panel_style(
                    BattleUiTheme.PANEL_BG_ALT(),
                    BattleUiTheme.PANEL_EDGE_SOFT(),
                    BattleUiTheme.PANEL_RADIUS_MEDIUM(),
                    BattleUiTheme.PANEL_BORDER(),
                    new Color(0, 0, 0, 0),
                    BattleUiTheme.PANEL_CONTENT_MARGIN()
                )
            );
        }

        portrait_frame.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                BattleUiTheme.PANEL_EDGE(),
                BattleUiTheme.PANEL_RADIUS_SMALL(),
                BattleUiTheme.PANEL_BORDER()
            )
        );

        _style_header_label(
            header_title_label,
            BattleUiTheme.FONT_HEADING(),
            BattleUiTheme.TEXT_PRIMARY()
        );
        _style_header_label(
            objective_status_label,
            BattleUiTheme.FONT_LABEL(),
            BattleUiTheme.TEXT_SECONDARY()
        );
        _style_header_label(tu_label, BattleUiTheme.FONT_HEADING(), BattleUiTheme.TEXT_PRIMARY());
        _style_header_label(
            ready_label,
            BattleUiTheme.FONT_HEADING(),
            BattleUiTheme.TEXT_PRIMARY()
        );
        _style_header_label(
            mode_value_label,
            BattleUiTheme.FONT_HEADING(),
            BattleUiTheme.TEXT_PRIMARY()
        );
        _style_header_label(
            unit_name_label,
            BattleUiTheme.FONT_TITLE(),
            BattleUiTheme.TEXT_PRIMARY()
        );
        _style_header_label(
            unit_role_label,
            BattleUiTheme.FONT_LABEL(),
            BattleUiTheme.TEXT_SECONDARY()
        );
        _style_header_label(
            skill_subtitle_label,
            BattleUiTheme.FONT_LABEL(),
            BattleUiTheme.TEXT_SECONDARY()
        );
        _style_header_label(
            portrait_glyph_label,
            BattleUiTheme.FONT_GLYPH(),
            BattleUiTheme.TEXT_PRIMARY()
        );
        _style_stat_label(hp_value_label);
        _style_stat_label(stamina_value_label);
        _style_stat_label(mp_value_label);
        _style_stat_label(aura_value_label);
        _style_ap_value_label(ap_value_label);
        _style_ap_prefix_label();

        _apply_chip_skin(round_chip, BattleUiTheme.PANEL_EDGE_SOFT());
        _apply_chip_skin(mode_chip, BattleUiTheme.PANEL_EDGE_SOFT());
        if (_zoom_chip != null)
        {
            _apply_chip_skin(_zoom_chip, BattleUiTheme.PANEL_EDGE_SOFT());
            _style_header_label(
                _zoom_value_label,
                BattleUiTheme.FONT_HEADING(),
                BattleUiTheme.TEXT_SECONDARY()
            );
        }

        _apply_progress_bar_skin(hp_bar, BattleUiTheme.RESOURCE_HP());
        _apply_progress_bar_skin(stamina_bar, BattleUiTheme.RESOURCE_STAMINA());
        _apply_progress_bar_skin(mp_bar, BattleUiTheme.RESOURCE_MP());
        _apply_progress_bar_skin(aura_bar, BattleUiTheme.RESOURCE_AURA());
    }

    private void _set_placeholder_state()
    {
        _battleEquipmentSnapshot = null;
        _battleEquipmentBackpackEntriesByIndex.Clear();
        _battle_equipment_feedback_text = "";
        _selected_backpack_instance_id = "";
        _selected_backpack_slot_id = "";
        header_title_label.Text = "遭遇战";
        objective_status_label.Text = "";
        objective_status_label.TooltipText = "";
        objective_status_label.Visible = false;
        tu_label.Text = "TU --";
        ready_label.Text = "READY 0";
        mode_value_label.Text = "手动";
        unit_name_label.Text = "待命";
        unit_role_label.Text = "未选中单位";
        portrait_glyph_label.Text = "?";
        _set_progress_bar_values(hp_bar, hp_value_label, 0, 1, "HP");
        _set_progress_bar_values(stamina_bar, stamina_value_label, 0, 1, "体力");
        _set_progress_bar_values(mp_bar, mp_value_label, 0, 1, "MP");
        _set_progress_bar_values(aura_bar, aura_value_label, 0, 1, "斗气");
        _rebuild_ap_dots(0, 2);
        mp_bar.Visible = false;
        mp_value_label.Visible = false;
        aura_bar.Visible = false;
        aura_value_label.Visible = false;
        skill_subtitle_label.Text = "等待战斗数据";
        skill_subtitle_label.TooltipText = "";
        if (barrier_status_label != null)
        {
            barrier_status_label.Text = "";
            barrier_status_label.Visible = false;
        }
        _rebuild_fate_badges(Array.Empty<BattleHudFateBadgeSnapshot>());
        _rebuild_status_badges(Array.Empty<BattleHudStatusEffectSnapshot>());
        _rebuild_skill_grid(Array.Empty<BattleHudSkillSlotSnapshot>());
        _rebuild_timeline_row(Array.Empty<BattleHudQueueEntrySnapshot>());
        _apply_command_dock(BattleHudSnapshot.Empty);
        _refresh_battle_equipment_ui();
    }

    internal void _apply_snapshot(BattleHudSnapshot snapshot)
    {
        if (snapshot == null || snapshot.IsEmpty)
        {
            _set_placeholder_state();
            return;
        }
        _battleEquipmentSnapshot = snapshot.EquipmentPanel;
        header_title_label.Text = string.IsNullOrEmpty(snapshot.HeaderTitle)
            ? "遭遇战"
            : snapshot.HeaderTitle;
        BattleHudObjectiveProgressSnapshot objectiveProgress =
            snapshot.ObjectiveProgress;
        bool hasObjectiveProgress = objectiveProgress?.IsValid == true;
        objective_status_label.Text = hasObjectiveProgress
            ? $"{objectiveProgress.Title} · {objectiveProgress.ProgressText}"
            : "";
        objective_status_label.TooltipText = hasObjectiveProgress
            ? objectiveProgress.ProgressText
            : "";
        objective_status_label.Visible = hasObjectiveProgress;
        tu_label.Text = snapshot.RoundBadge?.TuText ?? "TU --";
        ready_label.Text = snapshot.RoundBadge?.ReadyText ?? "READY 0";
        mode_value_label.Text = string.IsNullOrEmpty(snapshot.ModeText)
            ? "手动"
            : snapshot.ModeText;
        _refresh_focus_unit_card(snapshot.FocusUnit);
        _rebuild_timeline_row(snapshot.QueueEntries);
        _rebuild_skill_grid(snapshot.SkillSlots);
        skill_subtitle_label.Text = snapshot.SkillSubtitle;
        skill_subtitle_label.TooltipText = snapshot.SelectedSkillPreviewTooltipText;
        _rebuild_fate_badges(snapshot.SelectedSkillFateBadges);
        _apply_command_dock(snapshot);
        _refresh_battle_equipment_ui();
    }

    // Region A — resolve button in the TopBar right cell; regions B/C — the command
    // band and the hint/log info rows inside the skill panel column. Built in code so
    // each button's enable state and emitted signal stay together. The panel stays a
    // pure renderer: buttons emit panel-level signals, WorldMapSystem routes them.
    private void _set_progress_bar_values(
        ProgressBar progress_bar,
        Label value_label,
        int current_value,
        int max_value,
        string label_prefix,
        Color? fill_color = null
    )
    {
        int safeMax = Mathf.Max(max_value, 1);
        progress_bar.MaxValue = safeMax;
        progress_bar.Value = Mathf.Clamp(current_value, 0, safeMax);
        _apply_progress_bar_skin(progress_bar, fill_color ?? Colors.White);
        value_label.Text = $"{label_prefix} {current_value}/{safeMax}";
    }

    private static void _clear_container(Node container)
    {
        if (container == null)
            return;
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    internal static PackedScene BattleBoardSceneForTest() =>
        EngineAssetAccess.ResolveBorrowed<PackedScene>(BATTLE_BOARD_SCENE_PATH);

    private static List<Vector2I> CloneVector2IList(IEnumerable<Vector2I> source) =>
        source != null ? new List<Vector2I>(source) : new List<Vector2I>();

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> source)
    {
        var result = new List<StringName>();
        if (source == null)
            return result;
        foreach (StringName value in source)
        {
            if (!StringNameIsEmpty(value))
                result.Add(value);
        }
        return result;
    }

    private static StringName NormalizeStringName(StringName value) => value ?? new StringName("");

    private static bool StringNameIsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());

}
