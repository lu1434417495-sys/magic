using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

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
    private const string BATTLE_EQUIPMENT_EMPTY_TEXT = "战中队伍共享背包暂无可装备实例。";
    private const string BATTLE_EQUIPMENT_SOURCE_HINT =
        "来源：战斗局部队伍共享背包（不是据点共享仓库）。";
    private const string BATTLE_EQUIPMENT_COMMAND_UNAVAILABLE_TEXT = "战斗换装入口尚未连接运行时。";
    private const int AP_DOT_MAX_PIPS = 8;
    private static readonly Vector2 AP_DOT_SIZE = new(10, 10);
    private const string SKILL_ICON_DIR = "res://assets/main/battle/skills/";
    private const string SKILL_ICON_FALLBACK_KEY = "warrior_whirlwind_slash";
    private const string SKILL_ICON_GRAYSCALE_SHADER =
        "res://assets/shaders/skill_icon_grayscale.gdshader";
    private static readonly Color SKILL_ICON_DISABLED_MODULATE = new(0.62f, 0.62f, 0.62f, 0.85f);
    private ShaderMaterial _skill_icon_grayscale_material;

    private readonly BattleHudAdapter _hud_adapter = new();
    private readonly Dictionary<string, Texture2D> _skill_icon_cache = new();
    private static readonly PackedScene BATTLE_BOARD_SCENE = GD.Load<PackedScene>(
        "res://scenes/ui/battle_board_2d.tscn"
    );

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
    private GVector2IArray _pending_selected_skill_target_coords = new();
    private GVector2IArray _pending_selected_skill_valid_target_coords = new();
    private int _pending_selected_skill_required_coord_count;
    private GStringNameArray _pending_selected_skill_target_unit_ids = new();
    private StringName _pending_selected_skill_variant_id = "";

    private Dictionary<string, object> _battleEquipmentSnapshot = new(StringComparer.Ordinal);
    private string _battle_equipment_feedback_text = "";
    private StringName _selected_backpack_instance_id = "";
    private StringName _selected_backpack_slot_id = "";
    private readonly List<Dictionary<string, object>> _battleEquipmentBackpackEntriesByIndex =
        new();
    private readonly List<StringName> _battle_equipment_slot_ids_by_index = new();
    private Button _battle_equipment_button;
    private Control _battle_equipment_overlay;
    public Label _battle_equipment_title_label;
    public Label _battle_equipment_meta_label;
    public Label _battle_equipment_summary_label;
    public Label _battle_equipment_status_label;
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
    private GVector2IArray _hover_preview_valid_coords = new();
    private StringName _hover_preview_selected_skill_id = "";
    private StringName _hover_preview_selected_skill_variant_id = "";
    private BattleState _hover_preview_battle_state;

    public static Vector2I INVALID_HOVER_COORD() => InvalidHoverCoord;

    public override void _Ready()
    {
        Visible = false;
        map_frame = GetNode<PanelContainer>("%MapFrame");
        map_viewport_container = GetNode<SubViewportContainer>("%MapViewportContainer");
        top_bar = GetNode<PanelContainer>("%TopBar");
        bottom_panel = GetNode<PanelContainer>("%BottomPanel");
        header_title_label = GetNode<Label>("%HeaderTitleLabel");
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
        _ensure_battle_board();
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
        if (_battle_equipment_button != null)
            _battle_equipment_button.Pressed -= _open_battle_equipment_panel;
        if (_battle_equipment_close_button != null)
            _battle_equipment_close_button.Pressed -= _close_battle_equipment_panel;
        if (_battle_equipment_equip_button != null)
            _battle_equipment_equip_button.Pressed -= _on_battle_equipment_equip_pressed;
        SuppressNodeFieldFinalizers();
        _runtime_proxy = null;
        _hud_adapter.SetupRuntimeContext(null, null);
        _hud_adapter.Dispose();
        _skill_icon_cache.Clear();
        _battleEquipmentBackpackEntriesByIndex.Clear();
        _battle_equipment_slot_ids_by_index.Clear();
        ClearDynamicNodeRefs();
        GC.SuppressFinalize(this);
    }

    private void SuppressNodeFieldFinalizers()
    {
        SuppressGodotFinalizer(map_frame);
        SuppressGodotFinalizer(map_viewport_container);
        SuppressGodotFinalizer(top_bar);
        SuppressGodotFinalizer(bottom_panel);
        SuppressGodotFinalizer(header_title_label);
        SuppressGodotFinalizer(timeline_row);
        SuppressGodotFinalizer(round_chip);
        SuppressGodotFinalizer(tu_label);
        SuppressGodotFinalizer(ready_label);
        SuppressGodotFinalizer(mode_chip);
        SuppressGodotFinalizer(mode_value_label);
        SuppressGodotFinalizer(unit_card);
        SuppressGodotFinalizer(portrait_frame);
        SuppressGodotFinalizer(portrait_glyph_label);
        SuppressGodotFinalizer(unit_name_label);
        SuppressGodotFinalizer(unit_role_label);
        SuppressGodotFinalizer(hp_bar);
        SuppressGodotFinalizer(hp_value_label);
        SuppressGodotFinalizer(stamina_bar);
        SuppressGodotFinalizer(stamina_value_label);
        SuppressGodotFinalizer(mp_bar);
        SuppressGodotFinalizer(mp_value_label);
        SuppressGodotFinalizer(aura_bar);
        SuppressGodotFinalizer(aura_value_label);
        SuppressGodotFinalizer(ap_dot_container);
        SuppressGodotFinalizer(ap_value_label);
        SuppressGodotFinalizer(equipment_button_slot);
        SuppressGodotFinalizer(skill_panel);
        SuppressGodotFinalizer(skill_header);
        SuppressGodotFinalizer(skill_subtitle_label);
        SuppressGodotFinalizer(fate_badge_row);
        SuppressGodotFinalizer(skill_grid);
        SuppressGodotFinalizer(hover_overlay);
        SuppressGodotFinalizer(_map_subviewport);
        SuppressGodotFinalizer(_battle_background_rect);
        SuppressGodotFinalizer(_battle_board);
        SuppressGodotFinalizer(_battle_equipment_button);
        SuppressGodotFinalizer(_battle_equipment_overlay);
        SuppressGodotFinalizer(_battle_equipment_title_label);
        SuppressGodotFinalizer(_battle_equipment_meta_label);
        SuppressGodotFinalizer(_battle_equipment_summary_label);
        SuppressGodotFinalizer(_battle_equipment_status_label);
        SuppressGodotFinalizer(_battle_equipment_slot_list);
        SuppressGodotFinalizer(_battle_equipment_backpack_list);
        SuppressGodotFinalizer(_battle_equipment_details_label);
        SuppressGodotFinalizer(_battle_equipment_slot_selector);
        SuppressGodotFinalizer(_battle_equipment_equip_button);
        SuppressGodotFinalizer(_battle_equipment_close_button);
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
    }

    private static void SuppressGodotFinalizer(GodotObject instance)
    {
        if (instance != null)
            GC.SuppressFinalize(instance);
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
        GVector2IArray selected_skill_target_coords,
        GVector2IArray selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        GStringNameArray selected_skill_target_unit_ids,
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
        GVector2IArray selected_skill_target_coords,
        GVector2IArray selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        GStringNameArray selected_skill_target_unit_ids,
        StringName selected_skill_variant_id
    )
    {
        _has_pending_show_battle_payload = true;
        _pending_battle_state = battle_state;
        _pending_selected_coord = selected_coord;
        _pending_selected_skill_id = NormalizeStringName(selected_skill_id);
        _pending_selected_skill_name = selected_skill_name ?? "";
        _pending_selected_skill_variant_name = selected_skill_variant_name ?? "";
        _pending_selected_skill_target_coords = CloneVector2IArray(selected_skill_target_coords);
        _pending_selected_skill_valid_target_coords = CloneVector2IArray(
            selected_skill_valid_target_coords
        );
        _pending_selected_skill_required_coord_count = selected_skill_required_coord_count;
        _pending_selected_skill_target_unit_ids = CloneStringNameArray(
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
        GVector2IArray selected_skill_target_coords,
        GVector2IArray selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        GStringNameArray selected_skill_target_unit_ids,
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

    public void Refresh(
        BattleState battle_state,
        Vector2I selected_coord,
        StringName selected_skill_id,
        string selected_skill_name,
        string selected_skill_variant_name,
        GVector2IArray selected_skill_target_coords,
        GVector2IArray selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        GStringNameArray selected_skill_target_unit_ids,
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
        GVector2IArray selected_skill_target_coords,
        GVector2IArray selected_skill_valid_target_coords,
        int selected_skill_required_coord_count,
        GStringNameArray selected_skill_target_unit_ids,
        StringName selected_skill_variant_id,
        bool redraw_board
    )
    {
        if (battle_state == null)
        {
            HideBattle();
            return;
        }

        var targetCoords = CloneVector2IArray(selected_skill_target_coords);
        var validTargetCoords = CloneVector2IArray(selected_skill_valid_target_coords);
        var targetUnitIds = CloneStringNameArray(selected_skill_target_unit_ids);
        selected_skill_id = NormalizeStringName(selected_skill_id);
        selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        BattlePreview selectedSkillPreview = !StringNameIsEmpty(selected_skill_id)
            ? _runtime_proxy?.PreviewSelectedBattleSkillAtCoord(selected_coord)
            : null;

        GDictionary snapshot = _hud_adapter.BuildSnapshot(
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
                DictString(snapshot, "selected_skill_target_selection_mode", "single_unit")
            );
            if (StringNameIsEmpty(selected_skill_id) && validTargetCoords.Count > 0)
                targetSelectionMode = "movement";
            int minCount = DictInt(snapshot, "selected_skill_target_min_count", 1);
            int maxCount = DictInt(snapshot, "selected_skill_target_max_count", 1);
            GDictionary hitBadges = _build_selected_skill_target_hit_badges(
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

    private GDictionary _build_selected_skill_target_hit_badges(
        Vector2I selected_coord,
        StringName selected_skill_id,
        GVector2IArray selected_skill_target_coords,
        GVector2IArray selected_skill_valid_target_coords,
        GDictionary snapshot
    )
    {
        var badges = new GDictionary();
        if (StringNameIsEmpty(selected_skill_id))
            return badges;
        string badgeText = DictString(snapshot, "selected_skill_hit_badge_text", "");
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
            CloneVector2IArray(_pending_selected_skill_target_coords),
            CloneVector2IArray(_pending_selected_skill_valid_target_coords),
            _pending_selected_skill_required_coord_count,
            CloneStringNameArray(_pending_selected_skill_target_unit_ids),
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

    public void UpdateHoverPreview(
        BattleState battle_state,
        Vector2I hover_coord,
        GVector2IArray valid_target_coords,
        StringName selected_skill_id,
        StringName selected_skill_variant_id
    )
    {
        if (hover_overlay == null)
            return;
        if (battle_state == null || hover_coord == InvalidHoverCoord)
        {
            _clear_hover_preview_state();
            hover_overlay.Clear();
            return;
        }
        var validTargetCoords = CloneVector2IArray(valid_target_coords);
        _hover_preview_battle_state = battle_state;
        _hover_preview_coord = hover_coord;
        _hover_preview_valid_coords = CloneVector2IArray(validTargetCoords);
        _hover_preview_selected_skill_id = NormalizeStringName(selected_skill_id);
        _hover_preview_selected_skill_variant_id = NormalizeStringName(selected_skill_variant_id);
        BattlePreview hoverPreview = !StringNameIsEmpty(_hover_preview_selected_skill_id)
            ? _runtime_proxy?.PreviewSelectedBattleSkillAtCoord(hover_coord)
            : null;

        GDictionary preview = _hud_adapter.BuildHoverPreview(
            battle_state,
            hover_coord,
            _hover_preview_selected_skill_id,
            _hover_preview_selected_skill_variant_id,
            validTargetCoords,
            hoverPreview
        );
        hover_overlay.ApplyPreview(preview);
        if (!hover_overlay.Visible)
            return;
        _position_hover_overlay(hover_coord);
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

    public override void _Process(double delta)
    {
        if (hover_overlay == null || !hover_overlay.Visible)
            return;
        if (_hover_preview_coord == InvalidHoverCoord)
            return;
        _position_hover_overlay(_hover_preview_coord);
    }

    private void _position_hover_overlay(Vector2I hover_coord)
    {
        if (hover_overlay == null || _battle_board == null || map_viewport_container == null)
            return;
        if (!_battle_board.IsCoordInViewport(hover_coord))
        {
            hover_overlay.Visible = false;
            return;
        }
        Vector2 viewportPosition = _battle_board.CoordToViewportPosition(hover_coord);
        if (
            float.IsNegativeInfinity(viewportPosition.X)
            || float.IsNegativeInfinity(viewportPosition.Y)
        )
        {
            hover_overlay.Visible = false;
            return;
        }
        hover_overlay.ResetSize();
        Vector2 overlaySize = hover_overlay.Size;
        Vector2 anchorScreenPosition = map_viewport_container.Position + viewportPosition;
        Vector2 overlayPosition =
            anchorScreenPosition
            - new Vector2(overlaySize.X * 0.5f, overlaySize.Y + HOVER_OVERLAY_ANCHOR_OFFSET_Y);
        Vector2 panelSize = Size;
        if (overlayPosition.Y < HOVER_OVERLAY_EDGE_MARGIN)
            overlayPosition.Y = anchorScreenPosition.Y + HOVER_OVERLAY_ANCHOR_OFFSET_Y;
        if (overlayPosition.X < HOVER_OVERLAY_EDGE_MARGIN)
            overlayPosition.X = HOVER_OVERLAY_EDGE_MARGIN;
        float maxX = panelSize.X - overlaySize.X - HOVER_OVERLAY_EDGE_MARGIN;
        if (overlayPosition.X > maxX)
            overlayPosition.X = Mathf.Max(maxX, HOVER_OVERLAY_EDGE_MARGIN);
        float maxY = panelSize.Y - overlaySize.Y - HOVER_OVERLAY_EDGE_MARGIN;
        if (overlayPosition.Y > maxY)
            overlayPosition.Y = Mathf.Max(maxY, HOVER_OVERLAY_EDGE_MARGIN);
        hover_overlay.Position = overlayPosition;
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

        Node boardInstance = BATTLE_BOARD_SCENE.Instantiate();
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

        _apply_progress_bar_skin(hp_bar, BattleUiTheme.RESOURCE_HP());
        _apply_progress_bar_skin(stamina_bar, BattleUiTheme.RESOURCE_STAMINA());
        _apply_progress_bar_skin(mp_bar, BattleUiTheme.RESOURCE_MP());
        _apply_progress_bar_skin(aura_bar, BattleUiTheme.RESOURCE_AURA());
    }

    private void _set_placeholder_state()
    {
        _battleEquipmentSnapshot.Clear();
        _battleEquipmentBackpackEntriesByIndex.Clear();
        _battle_equipment_feedback_text = "";
        _selected_backpack_instance_id = "";
        _selected_backpack_slot_id = "";
        header_title_label.Text = "战斗地图";
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
        _rebuild_fate_badges(new GArray());
        _rebuild_skill_grid(new GArray());
        _rebuild_timeline_row(new GArray());
        _apply_command_dock(new GDictionary());
        _refresh_battle_equipment_ui();
    }

    public void _apply_snapshot(GDictionary snapshot)
    {
        GDictionary equipmentSnapshot = DictDictionary(snapshot, "equipment_panel");
        _battleEquipmentSnapshot =
            equipmentSnapshot.Count > 0
                ? RuntimePlainPayload.NormalizeDictionary(
                    equipmentSnapshot,
                    "BattleMapPanel.equipment_panel"
                )
                : new Dictionary<string, object>(StringComparer.Ordinal);
        header_title_label.Text = DictString(snapshot, "header_title", "战斗地图");
        GDictionary roundBadge = DictDictionary(snapshot, "round_badge");
        tu_label.Text = DictString(roundBadge, "tu_text", "TU --");
        ready_label.Text = DictString(roundBadge, "ready_text", "READY 0");
        mode_value_label.Text = DictString(snapshot, "mode_text", "手动");
        _refresh_focus_unit_card(DictDictionary(snapshot, "focus_unit"));
        _rebuild_timeline_row(DictArray(snapshot, "queue_entries"));
        _rebuild_skill_grid(DictArray(snapshot, "skill_slots"));
        skill_subtitle_label.Text = DictString(snapshot, "skill_subtitle", "");
        skill_subtitle_label.TooltipText = DictString(
            snapshot,
            "selected_skill_preview_tooltip_text",
            ""
        );
        _rebuild_fate_badges(DictArray(snapshot, "selected_skill_fate_badges"));
        _apply_command_dock(snapshot);
        _refresh_battle_equipment_ui();
    }

    // Region A — resolve button in the TopBar right cell; regions B/C — the command
    // band and the hint/log info rows inside the skill panel column. Built in code so
    // each button's enable state and emitted signal stay together. The panel stays a
    // pure renderer: buttons emit panel-level signals, WorldMapSystem routes them.
    private void _create_command_dock()
    {
        if (mode_chip?.GetParent() is HBoxContainer rightCell)
        {
            resolve_button = _create_dock_button("ResolveBattleButton", "结算 ⏎");
            resolve_button.Pressed += _on_resolve_pressed;
            rightCell.AddChild(resolve_button);
            rightCell.MoveChild(resolve_button, 0);
        }

        if (skill_grid?.GetParent() is not VBoxContainer skillLayout)
            return;

        var dockRow = new HBoxContainer { Name = "CommandDock" };
        dockRow.AddThemeConstantOverride("separation", 8);

        clear_skill_button = _create_dock_button("ClearSkillButton", "取消 Esc");
        clear_skill_button.Pressed += _on_clear_skill_pressed;
        prev_variant_button = _create_dock_button("PrevVariantButton", "◀ Q");
        prev_variant_button.Pressed += _on_prev_variant_pressed;
        variant_name_label = new Label
        {
            Name = "VariantNameLabel",
            VerticalAlignment = VerticalAlignment.Center,
        };
        next_variant_button = _create_dock_button("NextVariantButton", "E ▶");
        next_variant_button.Pressed += _on_next_variant_pressed;
        command_summary_label = new Label
        {
            Name = "CommandSummaryLabel",
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        dockRow.AddChild(clear_skill_button);
        dockRow.AddChild(prev_variant_button);
        dockRow.AddChild(variant_name_label);
        dockRow.AddChild(next_variant_button);
        dockRow.AddChild(command_summary_label);
        skillLayout.AddChild(dockRow);
        skillLayout.MoveChild(dockRow, 1);

        hint_label = new Label
        {
            Name = "HintLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint_label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        log_label = new Label
        {
            Name = "LogLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        log_label.AddThemeFontSizeOverride("font_size", 11);
        log_label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        skillLayout.AddChild(hint_label);
        skillLayout.AddChild(log_label);
    }

    private static Button _create_dock_button(string name, string text)
    {
        return new Button
        {
            Name = name,
            Text = text,
            FocusMode = FocusModeEnum.None,
            Disabled = true,
        };
    }

    private void _on_resolve_pressed() => EmitSignal(SignalName.battle_resolve_pressed);

    private void _on_clear_skill_pressed() => EmitSignal(SignalName.battle_clear_skill_pressed);

    private void _on_prev_variant_pressed() =>
        EmitSignal(SignalName.battle_cycle_variant_pressed, -1);

    private void _on_next_variant_pressed() =>
        EmitSignal(SignalName.battle_cycle_variant_pressed, 1);

    private void _apply_command_dock(GDictionary snapshot)
    {
        GDictionary dock = DictDictionary(snapshot, "command_dock");
        if (resolve_button != null)
        {
            resolve_button.Disabled = !DictBool(dock, "resolve_enabled", false);
            // A1 residual: light up the resolve button when a multi-target cast has
            // reached its minimum and is ready to confirm.
            _set_resolve_highlight(DictBool(snapshot, "selected_skill_confirm_ready", false));
        }
        if (clear_skill_button != null)
            clear_skill_button.Disabled = !DictBool(dock, "clear_skill_enabled", false);
        if (prev_variant_button != null)
            prev_variant_button.Disabled = !DictBool(dock, "prev_variant_enabled", false);
        if (next_variant_button != null)
            next_variant_button.Disabled = !DictBool(dock, "next_variant_enabled", false);
        if (variant_name_label != null)
            variant_name_label.Text = DictString(snapshot, "selected_skill_variant_name", "");
        if (command_summary_label != null)
            command_summary_label.Text = _build_command_summary(snapshot);
        if (hint_label != null)
            hint_label.Text = DictString(snapshot, "hint_text", "");
        if (log_label != null)
            log_label.Text = _join_recent_log_lines(snapshot);
    }

    private void _set_resolve_highlight(bool highlighted)
    {
        if (resolve_button == null)
            return;
        if (highlighted)
            resolve_button.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_ACCENT());
        else
            resolve_button.RemoveThemeColorOverride("font_color");
    }

    private static string _build_command_summary(GDictionary snapshot)
    {
        if (DictString(snapshot, "selected_skill_target_selection_mode", "single_unit") != "multi_unit")
            return "";
        int count = DictInt(snapshot, "selected_skill_target_count", 0);
        int maxCount = Mathf.Max(DictInt(snapshot, "selected_skill_target_max_count", 1), 1);
        return $"已选 {count}/{maxCount} 目标";
    }

    private static string _join_recent_log_lines(GDictionary snapshot)
    {
        GArray lines = DictArray(snapshot, "recent_battle_log_lines");
        var parts = new List<string>();
        foreach (Variant line in lines)
        {
            string text = line.AsString();
            if (!string.IsNullOrEmpty(text))
                parts.Add(text);
        }
        return string.Join("\n", parts);
    }

    private void _refresh_focus_unit_card(GDictionary focus_unit)
    {
        Color edgeColor = DictColor(focus_unit, "edge_color", BattleUiTheme.PANEL_EDGE());
        Color primaryColor = DictColor(focus_unit, "primary_color", BattleUiTheme.PANEL_BG_ALT());
        portrait_frame.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                primaryColor.Darkened(0.18f),
                edgeColor,
                BattleUiTheme.PANEL_RADIUS_SMALL(),
                BattleUiTheme.PANEL_BORDER()
            )
        );
        portrait_glyph_label.Text = DictString(focus_unit, "glyph", "?");
        unit_name_label.Text = DictString(focus_unit, "name", "待命");
        unit_role_label.Text = DictString(focus_unit, "role_text", "未选中单位");
        GDictionary resourceInfo = DictDictionary(focus_unit, "resource_info");

        _set_progress_bar_values(
            hp_bar,
            hp_value_label,
            DictInt(focus_unit, "hp_current", 0),
            DictInt(focus_unit, "hp_max", 1),
            "HP",
            BattleUiTheme.RESOURCE_HP()
        );
        _set_progress_bar_values(
            stamina_bar,
            stamina_value_label,
            DictInt(focus_unit, "stamina_current", _get_resource_current(resourceInfo, "stamina")),
            DictInt(focus_unit, "stamina_max", _get_resource_max(resourceInfo, "stamina")),
            "体力",
            BattleUiTheme.RESOURCE_STAMINA()
        );
        _set_progress_bar_values(
            mp_bar,
            mp_value_label,
            DictInt(focus_unit, "mp_current", 0),
            DictInt(focus_unit, "mp_max", 1),
            "MP",
            BattleUiTheme.RESOURCE_MP()
        );
        _set_progress_bar_values(
            aura_bar,
            aura_value_label,
            DictInt(focus_unit, "aura_current", _get_resource_current(resourceInfo, "aura")),
            DictInt(focus_unit, "aura_max", _get_resource_max(resourceInfo, "aura")),
            "斗气",
            BattleUiTheme.RESOURCE_AURA()
        );
        _rebuild_ap_dots(
            DictInt(focus_unit, "move_current", 0),
            DictInt(focus_unit, "move_max", 2)
        );
        _set_resource_row_visible(mp_bar, mp_value_label, _is_resource_visible(resourceInfo, "mp"));
        _set_resource_row_visible(
            aura_bar,
            aura_value_label,
            _is_resource_visible(resourceInfo, "aura")
        );
    }

    private static int _get_resource_current(GDictionary resource_info, string resource_key)
    {
        GDictionary data = DictDictionary(resource_info, resource_key);
        return data.Count > 0 ? DictInt(data, "current", 0) : 0;
    }

    private static int _get_resource_max(GDictionary resource_info, string resource_key)
    {
        GDictionary data = DictDictionary(resource_info, resource_key);
        return data.Count > 0 ? DictInt(data, "max", 1) : 1;
    }

    private static bool _is_resource_visible(GDictionary resource_info, string resource_key)
    {
        GDictionary data = DictDictionary(resource_info, resource_key);
        return data.Count > 0 ? DictBool(data, "visible", true) : true;
    }

    private void _apply_chip_skin(PanelContainer panel, Color edge)
    {
        if (panel == null)
            return;
        StyleBoxFlat chipStyle = _build_panel_style(
            BattleUiTheme.CHIP_BG(),
            edge,
            BattleUiTheme.PANEL_RADIUS_SMALL(),
            BattleUiTheme.PANEL_BORDER(),
            new Color(0, 0, 0, 0)
        );
        chipStyle.ContentMarginLeft = 10;
        chipStyle.ContentMarginRight = 10;
        chipStyle.ContentMarginTop = 4;
        chipStyle.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", chipStyle);
    }

    private static void _set_resource_row_visible(ProgressBar bar, Label label, bool is_visible)
    {
        if (bar != null)
            bar.Visible = is_visible;
        if (label != null)
            label.Visible = is_visible;
    }

    private static void _style_ap_value_label(Label label)
    {
        if (label == null)
            return;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
    }

    private void _style_ap_prefix_label()
    {
        var prefix = ap_dot_container.GetParent().GetNodeOrNull<Label>("ApPrefixLabel");
        if (prefix == null)
            return;
        prefix.AddThemeFontSizeOverride("font_size", 11);
        prefix.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
    }

    private void _rebuild_ap_dots(int current, int max_value)
    {
        if (ap_dot_container == null)
            return;
        int safeMax = Mathf.Max(max_value, 0);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        _clear_container(ap_dot_container);
        if (safeMax <= AP_DOT_MAX_PIPS)
        {
            for (int index = 0; index < safeMax; index++)
                ap_dot_container.AddChild(_create_ap_dot(index < safeCurrent));
            ap_value_label.Visible = false;
            ap_value_label.Text = "";
        }
        else
        {
            int visiblePips = Mathf.Min(safeCurrent, AP_DOT_MAX_PIPS);
            for (int index = 0; index < visiblePips; index++)
                ap_dot_container.AddChild(_create_ap_dot(true));
            ap_value_label.Visible = true;
            ap_value_label.Text = $"{safeCurrent}/{safeMax}";
        }
    }

    private Control _create_ap_dot(bool is_filled)
    {
        return new ColorRect
        {
            CustomMinimumSize = AP_DOT_SIZE,
            Color = is_filled ? BattleUiTheme.AP_DOT_FILL() : BattleUiTheme.AP_DOT_EMPTY(),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
    }

    private void _ensure_battle_equipment_ui()
    {
        if (_battle_equipment_overlay != null)
            return;

        if (_battle_equipment_button == null && equipment_button_slot != null)
            _battle_equipment_button = equipment_button_slot.GetNodeOrNull<Button>(
                "BattleEquipmentButton"
            );
        if (_battle_equipment_button != null)
        {
            _battle_equipment_button.TooltipText =
                "打开队伍共享背包（战斗局部）；战中不访问据点共享仓库。";
            _battle_equipment_button.MouseDefaultCursorShape = CursorShape.PointingHand;
            _apply_button_skin(_battle_equipment_button, true, true);
            _battle_equipment_button.Pressed += _open_battle_equipment_panel;
        }

        var hudRoot = GetNodeOrNull<Control>("HudRoot") ?? this;
        _battle_equipment_overlay = new Control
        {
            Name = "BattleEquipmentOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 1024,
        };
        _set_control_full_rect(_battle_equipment_overlay);
        hudRoot.AddChild(_battle_equipment_overlay);

        var shade = new ColorRect
        {
            Name = "BattleEquipmentShade",
            Color = new Color(0.03f, 0.015f, 0.01f, 0.76f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _set_control_full_rect(shade);
        _battle_equipment_overlay.AddChild(shade);

        var center = new CenterContainer
        {
            Name = "BattleEquipmentCenter",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _set_control_full_rect(center);
        _battle_equipment_overlay.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "BattleEquipmentPanel",
            CustomMinimumSize = new Vector2(820, 520),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                BattleUiTheme.PANEL_BG_ALT(),
                BattleUiTheme.PANEL_EDGE(),
                18,
                2,
                new Color(0.0f, 0.0f, 0.0f, 0.48f),
                14
            )
        );
        center.AddChild(panel);

        var content = new VBoxContainer { Name = "BattleEquipmentContent" };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        var header = new HBoxContainer { Name = "BattleEquipmentHeader" };
        header.AddThemeConstantOverride("separation", 12);
        content.AddChild(header);

        var titleStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleStack.AddThemeConstantOverride("separation", 3);
        header.AddChild(titleStack);

        _battle_equipment_title_label = new Label { Name = "BattleEquipmentTitleLabel" };
        _style_header_label(_battle_equipment_title_label, 22, BattleUiTheme.TEXT_PRIMARY());
        titleStack.AddChild(_battle_equipment_title_label);

        _battle_equipment_meta_label = new Label
        {
            Name = "BattleEquipmentMetaLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _style_header_label(_battle_equipment_meta_label, 12, BattleUiTheme.TEXT_SECONDARY());
        titleStack.AddChild(_battle_equipment_meta_label);

        _battle_equipment_close_button = new Button
        {
            Name = "BattleEquipmentCloseButton",
            Text = "关闭",
            CustomMinimumSize = new Vector2(82, 30),
        };
        _apply_button_skin(_battle_equipment_close_button, true);
        _battle_equipment_close_button.Pressed += _close_battle_equipment_panel;
        header.AddChild(_battle_equipment_close_button);

        _battle_equipment_summary_label = new Label
        {
            Name = "BattleEquipmentSummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _style_header_label(_battle_equipment_summary_label, 13, BattleUiTheme.TEXT_SECONDARY());
        content.AddChild(_battle_equipment_summary_label);

        var body = new HBoxContainer
        {
            Name = "BattleEquipmentBody",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        content.AddChild(body);

        var slotsPanel = _create_equipment_section_panel("BattleEquipmentSlotsPanel");
        slotsPanel.CustomMinimumSize = new Vector2(305, 0);
        body.AddChild(slotsPanel);
        VBoxContainer slotsLayout = _create_equipment_section_layout(slotsPanel);
        slotsLayout.AddChild(_create_equipment_section_title("当前行动单位装备"));
        var slotsScroll = new ScrollContainer
        {
            Name = "BattleEquipmentSlotsScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        slotsLayout.AddChild(slotsScroll);
        _battle_equipment_slot_list = new VBoxContainer
        {
            Name = "BattleEquipmentSlotList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _battle_equipment_slot_list.AddThemeConstantOverride("separation", 6);
        slotsScroll.AddChild(_battle_equipment_slot_list);

        var backpackPanel = _create_equipment_section_panel("BattleEquipmentBackpackPanel");
        backpackPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(backpackPanel);
        VBoxContainer backpackLayout = _create_equipment_section_layout(backpackPanel);
        backpackLayout.AddChild(_create_equipment_section_title("队伍共享背包（战斗局部）"));

        _battle_equipment_backpack_list = new ItemList
        {
            Name = "BattleEquipmentBackpackList",
            CustomMinimumSize = new Vector2(420, 180),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AllowReselect = true,
            FixedIconSize = Vector2I.Zero,
        };
        _battle_equipment_backpack_list.ItemSelected += index =>
            _on_battle_equipment_backpack_selected((int)index);
        backpackLayout.AddChild(_battle_equipment_backpack_list);

        _battle_equipment_details_label = new Label
        {
            Name = "BattleEquipmentDetailsLabel",
            CustomMinimumSize = new Vector2(0, 86),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _battle_equipment_details_label.AddThemeFontSizeOverride("font_size", 12);
        _battle_equipment_details_label.AddThemeColorOverride(
            "font_color",
            BattleUiTheme.TEXT_SECONDARY()
        );
        backpackLayout.AddChild(_battle_equipment_details_label);

        var commandRow = new HBoxContainer { Name = "BattleEquipmentCommandRow" };
        commandRow.AddThemeConstantOverride("separation", 8);
        backpackLayout.AddChild(commandRow);

        _battle_equipment_slot_selector = new OptionButton
        {
            Name = "BattleEquipmentSlotSelector",
            CustomMinimumSize = new Vector2(150, 30),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _battle_equipment_slot_selector.ItemSelected += index =>
            _on_battle_equipment_slot_selected((int)index);
        commandRow.AddChild(_battle_equipment_slot_selector);

        _battle_equipment_equip_button = new Button
        {
            Name = "BattleEquipmentEquipButton",
            Text = "装备",
            CustomMinimumSize = new Vector2(92, 30),
        };
        _apply_button_skin(_battle_equipment_equip_button, true, true);
        _battle_equipment_equip_button.Pressed += _on_battle_equipment_equip_pressed;
        commandRow.AddChild(_battle_equipment_equip_button);

        _battle_equipment_status_label = new Label
        {
            Name = "BattleEquipmentStatusLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _style_header_label(_battle_equipment_status_label, 12, BattleUiTheme.TEXT_SECONDARY());
        content.AddChild(_battle_equipment_status_label);
    }

    private static void _set_control_full_rect(Control control)
    {
        control.LayoutMode = 1;
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
        control.GrowHorizontal = GrowDirection.Both;
        control.GrowVertical = GrowDirection.Both;
    }

    private PanelContainer _create_equipment_section_panel(string section_name)
    {
        var panel = new PanelContainer
        {
            Name = section_name,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                new Color(0.1f, 0.04f, 0.025f, 0.9f),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                10,
                1,
                new Color(0.0f, 0.0f, 0.0f, 0.22f),
                10
            )
        );
        return panel;
    }

    private VBoxContainer _create_equipment_section_layout(PanelContainer panel)
    {
        var layout = new VBoxContainer
        {
            Name = $"{panel.Name}Layout",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        layout.AddThemeConstantOverride("separation", 7);
        panel.AddChild(layout);
        return layout;
    }

    private Label _create_equipment_section_title(string title)
    {
        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        return label;
    }

    public void _open_battle_equipment_panel()
    {
        if (_battle_equipment_overlay == null)
            return;
        if (_battleEquipmentSnapshot.Count == 0)
            return;
        _battle_equipment_feedback_text = "";
        _battle_equipment_overlay.Visible = true;
        _refresh_battle_equipment_ui();
    }

    public void _close_battle_equipment_panel()
    {
        if (_battle_equipment_overlay == null)
            return;
        _battle_equipment_overlay.Visible = false;
    }

    private void _refresh_battle_equipment_ui()
    {
        if (_battle_equipment_button != null)
        {
            bool hasSnapshot = _battleEquipmentSnapshot.Count > 0;
            _battle_equipment_button.Disabled = !hasSnapshot;
            _battle_equipment_button.TooltipText = hasSnapshot
                ? "打开队伍共享背包（战斗局部）；战中不访问据点共享仓库。"
                : "等待战斗数据。";
        }
        if (_battle_equipment_overlay == null || !_battle_equipment_overlay.Visible)
            return;

        GDictionary battleEquipmentSnapshot = _battle_equipment_snapshot_payload(
            "BattleMapPanel.refresh_battle_equipment_ui"
        );
        string disabledReason = _get_battle_equipment_panel_disabled_reason();
        _battle_equipment_title_label.Text = DictString(
            battleEquipmentSnapshot,
            "title",
            "队伍共享背包（战斗局部）"
        );
        _battle_equipment_meta_label.Text = DictString(
            battleEquipmentSnapshot,
            "meta",
            "战中不展示或访问据点共享仓库入口。"
        );
        _battle_equipment_summary_label.Text = DictString(
            battleEquipmentSnapshot,
            "summary_text",
            ""
        );
        if (!string.IsNullOrEmpty(_battle_equipment_feedback_text))
            _battle_equipment_status_label.Text = _battle_equipment_feedback_text;
        else if (!string.IsNullOrEmpty(disabledReason))
            _battle_equipment_status_label.Text = disabledReason;
        else
            _battle_equipment_status_label.Text =
                "选择队伍共享背包中的装备实例，装备到当前行动单位。";
        _rebuild_battle_equipment_slot_rows();
        _rebuild_battle_equipment_backpack_list();
        _refresh_battle_equipment_backpack_details();
    }

    private GDictionary _battle_equipment_snapshot_payload(string reason) =>
        RuntimePlainPayload.ProjectDictionary(_battleEquipmentSnapshot, reason);

    private string _get_battle_equipment_panel_disabled_reason()
    {
        if (_battleEquipmentSnapshot.Count == 0)
            return "战斗装备数据尚未就绪。";
        GDictionary battleEquipmentSnapshot = _battle_equipment_snapshot_payload(
            "BattleMapPanel.get_battle_equipment_panel_disabled_reason"
        );
        if (DictBool(battleEquipmentSnapshot, "can_change_equipment", false))
            return "";
        return DictString(battleEquipmentSnapshot, "disabled_reason", "当前不能换装。");
    }

    private void _rebuild_battle_equipment_slot_rows()
    {
        if (_battle_equipment_slot_list == null)
            return;
        _clear_container(_battle_equipment_slot_list);
        GDictionary battleEquipmentSnapshot = _battle_equipment_snapshot_payload(
            "BattleMapPanel.rebuild_battle_equipment_slot_rows"
        );
        GArray slots = DictArray(battleEquipmentSnapshot, "slots");
        if (slots.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "当前行动单位暂无 battle-local 装备视图。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            emptyLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_MUTED());
            _battle_equipment_slot_list.AddChild(emptyLabel);
            return;
        }
        foreach (GDictionary slot in ReadDictionaryItems(slots))
        {
            _battle_equipment_slot_list.AddChild(_create_battle_equipment_slot_row(slot));
        }
    }

    private Control _create_battle_equipment_slot_row(GDictionary slot)
    {
        var row = new PanelContainer
        {
            Name = "BattleEquipmentSlotRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                new Color(0.14f, 0.06f, 0.035f, 0.92f),
                new Color(0.34f, 0.22f, 0.13f, 0.82f),
                8,
                1
            )
        );

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        row.AddChild(margin);

        var layout = new HBoxContainer();
        layout.AddThemeConstantOverride("separation", 8);
        margin.AddChild(layout);

        var textStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        textStack.AddThemeConstantOverride("separation", 2);
        layout.AddChild(textStack);

        var title = new Label
        {
            Text =
                $"{DictString(slot, "slot_label", "槽位")}：{DictString(slot, "item_display_name", "空")}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        textStack.AddChild(title);

        var detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var detailLines = new List<string>();
        string instanceId = DictString(slot, "instance_id", "");
        if (string.IsNullOrEmpty(instanceId))
            detailLines.Add("未装备");
        else
            detailLines.Add($"实例 {instanceId}");
        List<string> occupiedLabels = _to_string_list(DictArray(slot, "occupied_slot_labels"));
        if (occupiedLabels.Count > 0)
            detailLines.Add($"占用 {string.Join("、", occupiedLabels)}");
        string disabledReason = DictString(slot, "disabled_reason", "");
        if (!string.IsNullOrEmpty(disabledReason))
            detailLines.Add(disabledReason);
        detail.Text = string.Join("  |  ", detailLines);
        detail.AddThemeFontSizeOverride("font_size", 11);
        detail.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_MUTED());
        textStack.AddChild(detail);

        var button = new Button { Text = "卸下", CustomMinimumSize = new Vector2(62, 28) };
        _apply_button_skin(button, true);
        string panelDisabledReason = _get_battle_equipment_panel_disabled_reason();
        bool canUnequip =
            string.IsNullOrEmpty(panelDisabledReason)
            && DictBool(slot, "can_unequip", false)
            && !string.IsNullOrEmpty(instanceId);
        button.Disabled = !canUnequip;
        button.TooltipText = canUnequip
            ? ""
            : _resolve_unequip_disabled_reason(slot, panelDisabledReason);
        StringName entrySlotId = new(DictString(slot, "entry_slot_id", ""));
        StringName slotInstanceId = new(instanceId);
        button.Pressed += () => _on_battle_equipment_unequip_pressed(entrySlotId, slotInstanceId);
        layout.AddChild(button);
        return row;
    }

    private string _resolve_unequip_disabled_reason(GDictionary slot, string panel_disabled_reason)
    {
        if (!string.IsNullOrEmpty(panel_disabled_reason))
            return panel_disabled_reason;
        string disabledReason = DictString(slot, "disabled_reason", "");
        if (!string.IsNullOrEmpty(disabledReason))
            return disabledReason;
        if (string.IsNullOrEmpty(DictString(slot, "instance_id", "")))
            return "该槽位没有可卸下的装备。";
        return "只能从装备入口槽卸下。";
    }

    private void _rebuild_battle_equipment_backpack_list()
    {
        if (_battle_equipment_backpack_list == null)
            return;
        StringName previousSelection = _selected_backpack_instance_id;
        _battle_equipment_backpack_list.Clear();
        _battleEquipmentBackpackEntriesByIndex.Clear();
        GDictionary battleEquipmentSnapshot = _battle_equipment_snapshot_payload(
            "BattleMapPanel.rebuild_battle_equipment_backpack_list"
        );
        GArray entries = DictArray(battleEquipmentSnapshot, "backpack_entries");
        if (entries.Count == 0)
        {
            _battle_equipment_backpack_list.AddItem(BATTLE_EQUIPMENT_EMPTY_TEXT);
            _battle_equipment_backpack_list.SetItemDisabled(0, true);
            _selected_backpack_instance_id = "";
            _selected_backpack_slot_id = "";
            return;
        }
        int selectedIndex = -1;
        int firstIndex = -1;
        foreach (GDictionary entry in ReadDictionaryItems(entries))
        {
            string itemStatus = DictBool(entry, "can_equip", false) ? "可装备" : "不可用";
            int itemIndex = _battle_equipment_backpack_list.AddItem(
                $"{DictString(entry, "display_name", "")}  ·  {itemStatus}"
            );
            _battleEquipmentBackpackEntriesByIndex.Add(
                RuntimePlainPayload.NormalizeDictionary(
                    entry,
                    "BattleMapPanel.backpack_entries_by_index"
                )
            );
            _battle_equipment_backpack_list.SetItemTooltipEnabled(itemIndex, true);
            _battle_equipment_backpack_list.SetItemTooltip(
                itemIndex,
                _build_backpack_entry_tooltip(entry)
            );
            if (firstIndex < 0)
                firstIndex = itemIndex;
            if (new StringName(DictString(entry, "instance_id", "")) == previousSelection)
                selectedIndex = itemIndex;
        }
        if (selectedIndex < 0)
            selectedIndex = firstIndex;
        if (selectedIndex >= 0)
        {
            _battle_equipment_backpack_list.Select(selectedIndex);
            GDictionary selectedEntry = _get_backpack_entry_at_index(selectedIndex);
            _selected_backpack_instance_id = new StringName(
                DictString(selectedEntry, "instance_id", "")
            );
            _sync_selected_backpack_slot(selectedEntry);
        }
    }

    private string _build_backpack_entry_tooltip(GDictionary entry)
    {
        var lines = new List<string>
        {
            DictString(entry, "display_name", ""),
            $"实例：{DictString(entry, "instance_id", "")}",
            BATTLE_EQUIPMENT_SOURCE_HINT,
        };
        List<string> allowedLabels = _to_string_list(DictArray(entry, "allowed_slot_labels"));
        if (allowedLabels.Count > 0)
            lines.Add($"可装备槽位：{string.Join("、", allowedLabels)}");
        string disabledReason = DictString(entry, "disabled_reason", "");
        if (!string.IsNullOrEmpty(disabledReason))
            lines.Add($"不可用：{disabledReason}");
        return string.Join("\n", lines);
    }

    public void _on_battle_equipment_backpack_selected(int index)
    {
        GDictionary entry = _get_backpack_entry_at_index(index);
        if (entry.Count == 0)
        {
            _selected_backpack_instance_id = "";
            _selected_backpack_slot_id = "";
        }
        else
        {
            _selected_backpack_instance_id = new StringName(DictString(entry, "instance_id", ""));
            _sync_selected_backpack_slot(entry);
        }
        _refresh_battle_equipment_backpack_details();
    }

    public void _on_battle_equipment_slot_selected(int index)
    {
        if (
            _battle_equipment_slot_selector == null
            || index < 0
            || index >= _battle_equipment_slot_ids_by_index.Count
        )
            return;
        _selected_backpack_slot_id = _battle_equipment_slot_ids_by_index[index];
        _refresh_battle_equipment_backpack_details();
    }

    private void _refresh_battle_equipment_backpack_details()
    {
        if (
            _battle_equipment_details_label == null
            || _battle_equipment_slot_selector == null
            || _battle_equipment_equip_button == null
        )
            return;
        GDictionary entry = _get_selected_backpack_entry();
        _battle_equipment_slot_selector.Clear();
        _battle_equipment_slot_ids_by_index.Clear();
        if (entry.Count == 0)
        {
            _battle_equipment_details_label.Text =
                BATTLE_EQUIPMENT_EMPTY_TEXT + "\n" + BATTLE_EQUIPMENT_SOURCE_HINT;
            _battle_equipment_slot_selector.Disabled = true;
            _battle_equipment_equip_button.Disabled = true;
            _battle_equipment_equip_button.TooltipText = "请选择战斗局部队伍共享背包中的装备实例。";
            return;
        }

        _sync_selected_backpack_slot(entry);
        List<string> allowedSlotIds = _to_string_list(DictArray(entry, "allowed_slot_ids"));
        List<string> allowedSlotLabels = _to_string_list(DictArray(entry, "allowed_slot_labels"));
        int selectedIndex = -1;
        for (int index = 0; index < allowedSlotIds.Count; index++)
        {
            string slotId = allowedSlotIds[index];
            string slotLabel = index < allowedSlotLabels.Count ? allowedSlotLabels[index] : slotId;
            _battle_equipment_slot_selector.AddItem(slotLabel);
            _battle_equipment_slot_ids_by_index.Add(new StringName(slotId));
            if (new StringName(slotId) == _selected_backpack_slot_id)
                selectedIndex = index;
        }
        if (selectedIndex >= 0)
            _battle_equipment_slot_selector.Select(selectedIndex);
        _battle_equipment_slot_selector.Disabled = allowedSlotIds.Count == 0;

        var detailLines = new List<string>
        {
            $"{DictString(entry, "display_name", "")}  |  物品 {DictString(entry, "item_id", "")}  |  实例 {DictString(entry, "instance_id", "")}",
            $"可装备槽位：{(allowedSlotLabels.Count > 0 ? string.Join("、", allowedSlotLabels) : "无")}",
            DictString(entry, "description", "暂无说明。"),
            BATTLE_EQUIPMENT_SOURCE_HINT,
        };
        string disabledReason = _get_equip_disabled_reason(entry);
        if (!string.IsNullOrEmpty(disabledReason))
            detailLines.Add($"不可用：{disabledReason}");
        _battle_equipment_details_label.Text = string.Join("\n", detailLines);
        _battle_equipment_equip_button.Disabled = !string.IsNullOrEmpty(disabledReason);
        _battle_equipment_equip_button.TooltipText = disabledReason;
    }

    private void _sync_selected_backpack_slot(GDictionary entry)
    {
        List<string> allowedSlotIds = _to_string_list(DictArray(entry, "allowed_slot_ids"));
        if (allowedSlotIds.Count == 0)
        {
            _selected_backpack_slot_id = "";
            return;
        }
        if (
            !StringNameIsEmpty(_selected_backpack_slot_id)
            && allowedSlotIds.Contains(_selected_backpack_slot_id.ToString())
        )
            return;
        string defaultSlot = DictString(entry, "default_slot_id", "");
        _selected_backpack_slot_id = new StringName(
            allowedSlotIds.Contains(defaultSlot) ? defaultSlot : allowedSlotIds[0]
        );
    }

    private string _get_equip_disabled_reason(GDictionary entry)
    {
        string panelDisabledReason = _get_battle_equipment_panel_disabled_reason();
        if (!string.IsNullOrEmpty(panelDisabledReason))
            return panelDisabledReason;
        string entryDisabledReason = DictString(entry, "disabled_reason", "");
        if (!string.IsNullOrEmpty(entryDisabledReason))
            return entryDisabledReason;
        if (!DictBool(entry, "can_equip", false))
            return "该实例当前不能装备。";
        if (StringNameIsEmpty(_selected_backpack_slot_id))
            return "请选择装备槽位。";
        return "";
    }

    private GDictionary _get_selected_backpack_entry()
    {
        if (_battle_equipment_backpack_list == null)
            return new GDictionary();
        int[] selectedItems = _battle_equipment_backpack_list.GetSelectedItems();
        if (selectedItems.Length == 0)
            return new GDictionary();
        return _get_backpack_entry_at_index(selectedItems[0]);
    }

    private GDictionary _get_backpack_entry_at_index(int index)
    {
        if (
            _battle_equipment_backpack_list == null
            || index < 0
            || index >= _battle_equipment_backpack_list.ItemCount
            || index >= _battleEquipmentBackpackEntriesByIndex.Count
        )
            return new GDictionary();
        return RuntimePlainPayload.ProjectDictionary(
            _battleEquipmentBackpackEntriesByIndex[index],
            "BattleMapPanel.get_backpack_entry_at_index"
        );
    }

    public void _on_battle_equipment_equip_pressed()
    {
        GDictionary entry = _get_selected_backpack_entry();
        if (entry.Count == 0)
        {
            _set_battle_equipment_feedback("请选择战斗局部队伍共享背包中的装备实例。");
            return;
        }
        string disabledReason = _get_equip_disabled_reason(entry);
        if (!string.IsNullOrEmpty(disabledReason))
        {
            _set_battle_equipment_feedback(disabledReason);
            return;
        }
        GDictionary battleEquipmentSnapshot = _battle_equipment_snapshot_payload(
            "BattleMapPanel.battle_equipment_equip_pressed"
        );
        StringName activeUnitId = new(DictString(battleEquipmentSnapshot, "active_unit_id", ""));
        if (StringNameIsEmpty(activeUnitId))
        {
            _set_battle_equipment_feedback("当前没有可换装单位。");
            return;
        }
        StringName itemId = new(DictString(entry, "item_id", ""));
        StringName instanceId = new(DictString(entry, "instance_id", ""));
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.ChangeEquipment,
            unit_id = activeUnitId,
            target_unit_id = activeUnitId,
            EquipmentOperationKind = BattleEquipmentOperationKind.Equip,
            equipment_slot_id = _selected_backpack_slot_id,
            equipment_item_id = itemId,
            equipment_instance_id = instanceId,
        };
        EmitSignal(
            SignalName.battle_equipment_equip_requested,
            _selected_backpack_slot_id,
            itemId,
            instanceId
        );
        _submit_battle_equipment_command(command);
    }

    public void _on_battle_equipment_unequip_pressed(StringName slot_id, StringName instance_id)
    {
        string panelDisabledReason = _get_battle_equipment_panel_disabled_reason();
        if (!string.IsNullOrEmpty(panelDisabledReason))
        {
            _set_battle_equipment_feedback(panelDisabledReason);
            return;
        }
        GDictionary battleEquipmentSnapshot = _battle_equipment_snapshot_payload(
            "BattleMapPanel.battle_equipment_unequip_pressed"
        );
        StringName activeUnitId = new(DictString(battleEquipmentSnapshot, "active_unit_id", ""));
        if (StringNameIsEmpty(activeUnitId))
        {
            _set_battle_equipment_feedback("当前没有可换装单位。");
            return;
        }
        if (StringNameIsEmpty(slot_id) || StringNameIsEmpty(instance_id))
        {
            _set_battle_equipment_feedback("该槽位没有可卸下的装备。");
            return;
        }
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.ChangeEquipment,
            unit_id = activeUnitId,
            target_unit_id = activeUnitId,
            EquipmentOperationKind = BattleEquipmentOperationKind.Unequip,
            equipment_slot_id = NormalizeStringName(slot_id),
            equipment_instance_id = NormalizeStringName(instance_id),
        };
        EmitSignal(SignalName.battle_equipment_unequip_requested, slot_id, instance_id);
        _submit_battle_equipment_command(command);
    }

    private void _submit_battle_equipment_command(BattleCommand command)
    {
        if (_runtime_proxy == null)
        {
            _set_battle_equipment_feedback(BATTLE_EQUIPMENT_COMMAND_UNAVAILABLE_TEXT);
            return;
        }
        GameRuntimeFacade.RuntimeCommandResult result = _runtime_proxy.IssueBattleCommand(command);
        string statusText = result.Message ?? "";
        if (string.IsNullOrEmpty(statusText))
            statusText = result.Ok
                ? "换装命令已提交。"
                : BATTLE_EQUIPMENT_COMMAND_UNAVAILABLE_TEXT;
        _battle_equipment_feedback_text = statusText;
        _refresh_battle_equipment_ui();
    }

    private string _resolve_encounter_display_name() =>
        _runtime_proxy?.GetActiveBattleEncounterName() ?? "";

    private void _set_battle_equipment_feedback(string message)
    {
        _battle_equipment_feedback_text = message ?? "";
        _refresh_battle_equipment_ui();
    }

    private static List<string> _to_string_list(GArray values)
    {
        var result = new List<string>();
        foreach (var item in values)
            result.Add(item.ToString());
        return result;
    }

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

    private void _rebuild_skill_grid(GArray slots)
    {
        _clear_container(skill_grid);
        if (slots.Count == 0)
        {
            for (int index = 0; index < 20; index++)
                skill_grid.AddChild(
                    _create_skill_slot(new GDictionary { ["index"] = index, ["is_empty"] = true })
                );
            return;
        }
        foreach (GDictionary slot in ReadDictionaryItems(slots))
        {
            skill_grid.AddChild(_create_skill_slot(slot));
        }
        _update_skill_grid_columns();
    }

    private void _update_skill_grid_columns()
    {
        if (skill_grid == null)
            return;
        int slotCount = skill_grid.GetChildCount();
        if (slotCount == 0)
            return;
        float available = skill_grid.Size.X;
        if (available <= 0.0f)
            return;
        int hSeparation = skill_grid.GetThemeConstant("h_separation");
        float cellStride = BattleUiTheme.SKILL_SLOT_SIZE() + hSeparation;
        int columns = Mathf.FloorToInt((available + hSeparation) / cellStride);
        columns = Mathf.Clamp(columns, 1, slotCount);
        if (skill_grid.Columns != columns)
            skill_grid.Columns = columns;
    }

    private void _rebuild_fate_badges(GArray badges)
    {
        _clear_container(fate_badge_row);
        fate_badge_row.Visible = badges.Count > 0;
        if (badges.Count == 0)
            return;
        foreach (GDictionary badge in ReadDictionaryItems(badges))
        {
            fate_badge_row.AddChild(_create_fate_badge(badge));
        }
    }

    private void _rebuild_timeline_row(GArray entries)
    {
        _clear_container(timeline_row);
        timeline_row.Visible = entries.Count > 0;
        if (entries.Count == 0)
            return;
        foreach (GDictionary entry in ReadDictionaryItems(entries))
        {
            timeline_row.AddChild(
                DictBool(entry, "is_overflow", false)
                    ? _create_timeline_overflow(entry)
                    : _create_timeline_entry(entry)
            );
        }
    }

    private Control _create_timeline_entry(GDictionary entry)
    {
        bool isActive = DictBool(entry, "is_active", false);
        bool isReady = DictBool(entry, "is_ready", false);
        bool isEnemy = DictBool(entry, "is_enemy", false);
        float hpRatio = Mathf.Clamp(DictFloat(entry, "hp_ratio", 1.0f), 0.0f, 1.0f);
        Color ringColor = isEnemy
            ? BattleUiTheme.TIMELINE_ENEMY_RING()
            : BattleUiTheme.TIMELINE_ALLY_RING();
        if (isActive)
            ringColor = BattleUiTheme.TIMELINE_ACTIVE_RING();

        var stack = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        stack.AddThemeConstantOverride("separation", 2);
        stack.TooltipText = BuildTimelineTooltip(
            DictString(entry, "name", "?"),
            DictString(entry, "hp_text", ""),
            DictString(entry, "ap_text", "")
        );
        if (!isReady && !isActive)
            stack.Modulate = new Color(1.0f, 1.0f, 1.0f, BattleUiTheme.TIMELINE_INACTIVE_ALPHA());

        var portrait = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                BattleUiTheme.TIMELINE_ENTRY_SIZE(),
                BattleUiTheme.TIMELINE_ENTRY_SIZE()
            ),
        };
        portrait.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                ringColor,
                BattleUiTheme.PANEL_RADIUS_TINY(),
                isActive ? 2 : 1,
                new Color(0, 0, 0, 0)
            )
        );
        stack.AddChild(portrait);

        var glyphLabel = new Label
        {
            Text = DictString(entry, "glyph", "?"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_LABEL());
        glyphLabel.AddThemeColorOverride(
            "font_color",
            isActive ? BattleUiTheme.TEXT_ACCENT() : BattleUiTheme.TEXT_PRIMARY()
        );
        portrait.AddChild(glyphLabel);

        var hpBand = new Control
        {
            CustomMinimumSize = new Vector2(
                BattleUiTheme.TIMELINE_ENTRY_SIZE(),
                BattleUiTheme.TIMELINE_HP_BAND_HEIGHT()
            ),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        stack.AddChild(hpBand);

        var hpBg = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0.0f,
            AnchorTop = 0.0f,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            Color = BattleUiTheme.PANEL_BG_DEEP(),
        };
        hpBand.AddChild(hpBg);

        var hpFill = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorLeft = 0.0f,
            AnchorTop = 0.0f,
            AnchorRight = hpRatio,
            AnchorBottom = 1.0f,
            Color = !isEnemy ? BattleUiTheme.RESOURCE_HP() : BattleUiTheme.TIMELINE_ENEMY_RING(),
        };
        hpBand.AddChild(hpFill);
        return stack;
    }

    internal static string BuildTimelineTooltipForTest(string name, int hp, int ap) =>
        BuildTimelineTooltip(name, hp.ToString(), ap.ToString());

    private static string BuildTimelineTooltip(string name, string hpText, string apText) =>
        $"{name}\n{hpText}\n{apText}";

    private Control _create_timeline_overflow(GDictionary entry)
    {
        var chip = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, BattleUiTheme.TIMELINE_ENTRY_SIZE()),
        };
        StyleBoxFlat chipStyle = _build_panel_style(
            BattleUiTheme.CHIP_BG(),
            BattleUiTheme.PANEL_EDGE_SOFT(),
            BattleUiTheme.PANEL_RADIUS_SMALL(),
            BattleUiTheme.PANEL_BORDER(),
            new Color(0, 0, 0, 0)
        );
        chipStyle.ContentMarginLeft = 6;
        chipStyle.ContentMarginRight = 6;
        chip.AddThemeStyleboxOverride("panel", chipStyle);

        var label = new Label
        {
            Text = DictString(entry, "overflow_text", "+"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        chip.AddChild(label);
        return chip;
    }

    private Control _create_fate_badge(GDictionary badge)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = DictString(badge, "tooltip_text", ""),
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            _build_fate_badge_style(new StringName(DictString(badge, "tone", "gate")))
        );

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        panel.AddChild(margin);

        var label = new Label { Text = DictString(badge, "text", "") };
        label.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_LABEL());
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        margin.AddChild(label);
        return panel;
    }

    private Control _create_skill_slot(GDictionary slot)
    {
        bool isEmpty = DictBool(slot, "is_empty", false);
        bool isDisabled = DictBool(slot, "is_disabled", false);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(
                BattleUiTheme.SKILL_SLOT_SIZE(),
                BattleUiTheme.SKILL_SLOT_SIZE()
            ),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = _build_skill_slot_tooltip(slot),
        };
        panel.AddThemeStyleboxOverride("panel", _build_skill_slot_style(slot));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        panel.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 0);
        margin.AddChild(layout);

        var hotkeyRow = new HBoxContainer();
        layout.AddChild(hotkeyRow);

        var hotkeyLabel = new Label
        {
            Text = DictString(slot, "hotkey", ""),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        hotkeyLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
        hotkeyLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_MUTED());
        hotkeyRow.AddChild(hotkeyLabel);

        int cdValue = DictInt(slot, "cooldown", 0);
        var cdLabel = new Label
        {
            Text = cdValue > 0 ? $"CD {cdValue}" : "",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        cdLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_CAPTION());
        cdLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_ACCENT());
        hotkeyRow.AddChild(cdLabel);

        Control glyphNode = _create_skill_glyph_node(slot, isEmpty, isDisabled);
        layout.AddChild(glyphNode);

        if (!isEmpty)
        {
            var glowBand = new ColorRect
            {
                Name = "FateGlow",
                MouseFilter = MouseFilterEnum.Ignore,
                LayoutMode = 1,
                AnchorLeft = 0.0f,
                AnchorRight = 1.0f,
                AnchorTop = 1.0f,
                AnchorBottom = 1.0f,
                OffsetTop = -BattleUiTheme.SKILL_GLOW_BAND_HEIGHT(),
                OffsetLeft = 0.0f,
                OffsetRight = 0.0f,
                OffsetBottom = 0.0f,
            };
            Color accentColor = DictColor(slot, "accent_color", BattleUiTheme.FATE_GATE());
            if (isDisabled)
                accentColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.32f);
            glowBand.Color = accentColor;
            panel.AddChild(glowBand);
        }

        var clickTarget = new BattleSkillSlotButton
        {
            Flat = true,
            FocusMode = FocusModeEnum.None,
            LayoutMode = 1,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            Disabled = isEmpty || isDisabled,
            Text = "",
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        if (!isEmpty)
        {
            clickTarget.TooltipText = DictString(slot, "display_name", "");
            clickTarget.skill_display_name = DictString(slot, "display_name", "");
            clickTarget.skill_description = DictString(slot, "description", "");
            clickTarget.skill_footer_text = DictString(slot, "footer_text", "");
            clickTarget.skill_disabled_reason = DictString(slot, "disabled_reason", "");
            clickTarget.skill_cooldown = DictInt(slot, "cooldown", 0);
            clickTarget.skill_accent_color = DictColor(
                slot,
                "accent_color",
                BattleUiTheme.FATE_GATE()
            );
        }
        int slotIndex = DictInt(slot, "index", -1);
        clickTarget.Pressed += () => _on_skill_slot_pressed(slotIndex);
        panel.AddChild(clickTarget);
        return panel;
    }

    public void _on_skill_slot_pressed(int index)
    {
        if (index < 0)
            return;
        EmitSignal(SignalName.battle_skill_slot_selected, index);
    }

    private string _build_skill_slot_tooltip(GDictionary slot)
    {
        if (DictBool(slot, "is_empty", false))
            return "";
        var lines = new List<string> { DictString(slot, "display_name", "") };
        string disabledReason = DictString(slot, "disabled_reason", "");
        if (!string.IsNullOrEmpty(disabledReason))
        {
            lines.Add($"不可用：{disabledReason}");
        }
        else
        {
            string footerText = DictString(slot, "footer_text", "");
            if (!string.IsNullOrEmpty(footerText) && footerText != "READY")
                lines.Add($"信息：{footerText}");
        }
        return string.Join("\n", lines);
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

    private Control _create_skill_glyph_node(GDictionary slot, bool is_empty, bool is_disabled)
    {
        if (is_empty)
        {
            return new Control
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        }

        string iconKey = DictString(slot, "icon_key", "");
        Texture2D texture = _resolve_skill_icon(iconKey)
            ?? _resolve_skill_icon(SKILL_ICON_FALLBACK_KEY);
        if (texture != null)
        {
            var icon = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            if (is_disabled)
            {
                icon.Modulate = SKILL_ICON_DISABLED_MODULATE;
                icon.Material = _get_skill_icon_grayscale_material();
            }
            return icon;
        }

        var glyphLabel = new Label
        {
            Text = DictString(slot, "short_name", "--"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_TITLE());
        glyphLabel.AddThemeColorOverride(
            "font_color",
            is_disabled ? BattleUiTheme.TEXT_MUTED() : BattleUiTheme.TEXT_PRIMARY()
        );
        return glyphLabel;
    }

    private Texture2D _resolve_skill_icon(string icon_key)
    {
        if (string.IsNullOrEmpty(icon_key))
            return null;
        if (_skill_icon_cache.TryGetValue(icon_key, out Texture2D cachedTexture))
            return cachedTexture;
        string path = $"{SKILL_ICON_DIR}{icon_key}.png";
        Texture2D texture = null;
        if (ResourceLoader.Exists(path, "Texture2D"))
            texture = ResourceLoader.Load<Texture2D>(path);
        _skill_icon_cache[icon_key] = texture;
        return texture;
    }

    private ShaderMaterial _get_skill_icon_grayscale_material()
    {
        if (_skill_icon_grayscale_material != null)
            return _skill_icon_grayscale_material;
        if (ResourceLoader.Exists(SKILL_ICON_GRAYSCALE_SHADER, "Shader")
            && ResourceLoader.Load<Shader>(SKILL_ICON_GRAYSCALE_SHADER) is Shader shader)
        {
            _skill_icon_grayscale_material = new ShaderMaterial { Shader = shader };
        }
        return _skill_icon_grayscale_material;
    }

    private void _apply_button_skin(Button button, bool is_compact, bool is_primary = false)
    {
        int radius = is_compact
            ? BattleUiTheme.PANEL_RADIUS_SMALL()
            : BattleUiTheme.PANEL_RADIUS_MEDIUM();
        Color primaryEdge = BattleUiTheme.PANEL_EDGE_GLOW();
        Color normalBg = !is_primary ? BattleUiTheme.CHIP_BG() : BattleUiTheme.PANEL_BG_ALT();
        Color normalEdge = !is_primary ? BattleUiTheme.PANEL_EDGE_SOFT() : primaryEdge;
        button.AddThemeFontSizeOverride(
            "font_size",
            is_compact ? BattleUiTheme.FONT_BODY() : BattleUiTheme.FONT_HEADING()
        );
        button.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        button.AddThemeColorOverride("font_focus_color", BattleUiTheme.TEXT_PRIMARY());
        button.AddThemeStyleboxOverride(
            "normal",
            _build_button_style(normalBg, normalEdge, radius, BattleUiTheme.PANEL_BORDER())
        );
        button.AddThemeStyleboxOverride(
            "hover",
            _build_button_style(
                normalBg.Lightened(0.06f),
                normalEdge.Lightened(0.18f),
                radius,
                BattleUiTheme.PANEL_BORDER()
            )
        );
        button.AddThemeStyleboxOverride(
            "pressed",
            _build_button_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                normalEdge.Darkened(0.12f),
                radius,
                BattleUiTheme.PANEL_BORDER()
            )
        );
        button.AddThemeStyleboxOverride(
            "disabled",
            _build_button_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                radius,
                BattleUiTheme.PANEL_BORDER()
            )
        );
    }

    private StyleBoxFlat _build_skill_slot_style(GDictionary slot)
    {
        int radius = BattleUiTheme.PANEL_RADIUS_TINY();
        if (DictBool(slot, "is_empty", false))
        {
            Color edge = BattleUiTheme.PANEL_EDGE_SOFT();
            Color emptyEdge = new(edge.R, edge.G, edge.B, 0.4f);
            return _build_panel_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                emptyEdge,
                radius,
                1,
                new Color(0, 0, 0, 0)
            );
        }
        if (DictBool(slot, "is_selected", false))
            return _build_panel_style(
                BattleUiTheme.PANEL_BG_ALT(),
                BattleUiTheme.TEXT_ACCENT(),
                radius,
                2,
                new Color(0, 0, 0, 0)
            );
        if (DictBool(slot, "is_disabled", false))
        {
            Color bg = BattleUiTheme.PANEL_BG_DEEP();
            Color dimBg = new(bg.R, bg.G, bg.B, 0.78f);
            return _build_panel_style(
                dimBg,
                BattleUiTheme.PANEL_EDGE_SOFT(),
                radius,
                1,
                new Color(0, 0, 0, 0)
            );
        }
        return _build_panel_style(
            BattleUiTheme.PANEL_BG_DEEP(),
            BattleUiTheme.PANEL_EDGE_GLOW(),
            radius,
            2,
            new Color(0, 0, 0, 0)
        );
    }

    private StyleBoxFlat _build_fate_badge_style(StringName tone)
    {
        Color accent = BattleUiTheme.FateColor(tone);
        Color tintedBg = BattleUiTheme.CHIP_BG().Lerp(accent, 0.18f);
        return _build_button_style(tintedBg, accent, 999, 1);
    }

    private void _apply_progress_bar_skin(ProgressBar progress_bar, Color fill_color)
    {
        progress_bar.ShowPercentage = false;
        progress_bar.AddThemeStyleboxOverride(
            "background",
            _build_button_style(
                BattleUiTheme.PANEL_BG_DEEP(),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                4,
                1
            )
        );
        progress_bar.AddThemeStyleboxOverride(
            "fill",
            _build_button_style(fill_color.Darkened(0.05f), fill_color.Lightened(0.08f), 4, 0)
        );
    }

    private static void _style_header_label(Label label, int font_size, Color font_color)
    {
        label.AddThemeFontSizeOverride("font_size", font_size);
        label.AddThemeColorOverride("font_color", font_color);
    }

    private static void _style_stat_label(Label label)
    {
        label.AddThemeFontSizeOverride("font_size", BattleUiTheme.FONT_BODY());
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
    }

    private static StyleBoxFlat _build_panel_style(
        Color background_color,
        Color border_color,
        int radius = 8,
        int border_width = 1,
        Color? shadow_color = null,
        int content_margin = 0
    )
    {
        Color shadowColor = shadow_color ?? new Color(0.0f, 0.0f, 0.0f, 0.0f);
        var style = new StyleBoxFlat
        {
            BgColor = background_color,
            BorderWidthLeft = border_width,
            BorderWidthTop = border_width,
            BorderWidthRight = border_width,
            BorderWidthBottom = border_width,
            BorderColor = border_color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
            ShadowColor = shadowColor,
            ShadowSize = shadowColor.A == 0.0f ? 0 : 8,
        };
        if (content_margin > 0)
        {
            style.ContentMarginLeft = content_margin;
            style.ContentMarginTop = content_margin;
            style.ContentMarginRight = content_margin;
            style.ContentMarginBottom = content_margin;
        }
        return style;
    }

    private static StyleBoxFlat _build_button_style(
        Color background_color,
        Color border_color,
        int radius = 14,
        int border_width = 2
    )
    {
        return new StyleBoxFlat
        {
            BgColor = background_color,
            BorderWidthLeft = border_width,
            BorderWidthTop = border_width,
            BorderWidthRight = border_width,
            BorderWidthBottom = border_width,
            BorderColor = border_color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
        };
    }

    private static GVector2IArray CloneVector2IArray(GVector2IArray source)
        => new Vector2IList(source).ToGodotArray();

    private static GStringNameArray CloneStringNameArray(GStringNameArray source)
        => new StringNameList(source).ToGodotArray();

    private static StringName NormalizeStringName(StringName value) => value ?? new StringName("");

    private static bool StringNameIsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());

    private static GDictionary DictDictionary(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray DictArray(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static string DictString(GDictionary dict, string key, string fallback = "")
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static int DictInt(GDictionary dict, string key, int fallback = 0)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : fallback;
    }

    private static float DictFloat(GDictionary dict, string key, float fallback = 0.0f)
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (float)value.AsDouble(),
            _ => fallback,
        };
    }

    private static bool DictBool(GDictionary dict, string key, bool fallback = false)
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Color DictColor(GDictionary dict, string key, Color fallback)
    {
        return
            TryRead(dict, key, out Variant value)
            && value.VariantType == Variant.Type.Color
            ? value.AsColor()
            : fallback;
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

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        if (dict == null || string.IsNullOrEmpty(key))
        {
            value = default;
            return false;
        }
        if (dict.ContainsKey(key))
        {
            value = dict[key];
            return true;
        }
        value = default;
        return false;
    }
}
