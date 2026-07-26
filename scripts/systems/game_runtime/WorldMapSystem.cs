using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[GlobalClass]
public partial class WorldMapSystem : Control, IApplicationShutdownParticipant
{
    private const string ApplicationShutdownParticipantId = "world-map-system";
    private const ApplicationShutdownParticipantStage ApplicationShutdownStage =
        ApplicationShutdownParticipantStage.Runtime;
    private const int ApplicationShutdownOrder = 0;
    private const float WORLD_MOVE_REPEAT_INTERVAL = 0.5f;
    private const string STARTUP_SCENE_SETTING = "application/run/main_scene";
    private const string BATTLE_LOADING_LABEL_TEXT = "LOADING...";
    private static readonly Color BATTLE_LOADING_TEXT_COLOR = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Color BATTLE_LOADING_PERCENT_TEXT_COLOR = new(
        0.86f,
        0.92f,
        1.0f,
        0.95f
    );
    private static readonly Color BATTLE_LOADING_BAR_BG_COLOR = new(0.1f, 0.13f, 0.19f, 0.96f);
    private static readonly Color BATTLE_LOADING_BAR_FILL_COLOR = new(0.39f, 0.72f, 0.98f, 1.0f);
    private static readonly Color BATTLE_LOADING_BAR_BORDER_COLOR = new(0.76f, 0.87f, 0.98f, 0.78f);
    private const float BATTLE_LOADING_PROGRESS_MIN = 0.0f;
    private const float BATTLE_LOADING_PROGRESS_MAX = 100.0f;
    private const string BATTLE_LOADING_MODAL_ID = "battle_loading";
    private const string ContingencySetupWindowScenePath =
        "res://scenes/ui/contingency_setup_window.tscn";
    private const float LOG_DOCK_DESIGN_TOP_MARGIN = 60.0f;
    private const float LOG_DOCK_DESIGN_BOTTOM_MARGIN = 60.0f;
    private const float LOG_DOCK_DESIGN_RIGHT_MARGIN = 12.0f;
    private const float LOG_DOCK_MIN_HEIGHT = 360.0f;
    private const float LOG_DOCK_MAX_HEIGHT = 520.0f;
    private const float LOG_DOCK_BATTLE_TOP_MARGIN = 96.0f;
    private const float LOG_DOCK_BATTLE_BOTTOM_MARGIN = 184.0f;

    public WorldMapView world_map_view;
    public Control map_viewport;
    public CanvasItem world_map_background;
    public BattleMapPanel battle_map_panel;
    public RuntimeLogDock runtime_log_dock;
    public Label status_label;
    public SettlementWindow settlement_window;
    public ShopWindow contract_board_service_modal;
    public ShopWindow shop_service_modal;
    public ShopWindow forge_service_modal;
    public ShopWindow stagecoach_service_modal;
    // Dedicated modal for NPC quest offers; driven by RuntimeModalKind.NpcQuestOffer.
    public NpcQuestOfferDialog npc_quest_offer_dialog;
    public BountyBoardWindow bounty_board_window;
    public CharacterInfoWindow character_info_window;
    public PartyManagementWindow party_management_window;
    public ContingencySetupWindow contingency_setup_window;
    public PartyWarehouseWindow party_warehouse_window;
    public PromotionChoiceWindow promotion_choice_window;
    public MasteryRewardWindow character_reward_window;
    public SubmapEntryWindow submap_entry_window;
    public Control submap_hint_panel;
    public Label submap_hint_label;
    public Control bottom_action_bar;
    public Button party_button;
    public Control battle_loading_overlay;
    public Label battle_loading_label;
    public ProgressBar battle_loading_progress_bar;
    public Label battle_loading_percent_label;

    public GameSession _game_session;
    public GameRuntimeFacade _runtime;
    internal WorldMapRuntimeProxy _runtime_proxy = new WorldMapRuntimeProxy();
    public List<Key> _held_world_move_keys = new();
    public float _world_move_repeat_timer;
    private ApplicationLifetimeCoordinator _applicationLifetimeCoordinator;
    private bool _signalsConnected;
    private bool _closed;

    string IApplicationShutdownParticipant.ShutdownParticipantId =>
        ApplicationShutdownParticipantId;

    ApplicationShutdownParticipantStage IApplicationShutdownParticipant.ShutdownStage =>
        ApplicationShutdownStage;

    int IApplicationShutdownParticipant.ShutdownOrder => ApplicationShutdownOrder;

    ValueTask IApplicationShutdownParticipant.CloseForApplicationShutdownAsync(
        ShutdownReport report
    )
    {
        CloseRuntimeOwner();
        return ValueTask.CompletedTask;
    }

    public override void _Ready()
    {
        _game_session = GetTree()?.Root.GetNodeOrNull<GameSession>("GameSession");
        if (_game_session == null)
        {
            GameLog.Error("World map requires the GameSession autoload.", "worldmap.missing_session", "worldmap");
            return;
        }
        if (!_game_session.HasActiveWorld())
        {
            GameLog.Error("World map requires an active save loaded in GameSession.", "worldmap.no_active_save", "worldmap");
            return;
        }
        if (_game_session.GetGenerationDefinition() == null)
        {
            GameLog.Error("GameSession is missing an active world generation definition.", "worldmap.missing_generation_definition", "worldmap");
            return;
        }

        BindNodes();
        var runtime = new GameRuntimeFacade();
        runtime.Setup(_game_session);
        _runtime = runtime;
        var proxy = new WorldMapRuntimeProxy();
        _runtime_proxy = proxy;
        proxy.Setup(_runtime, this);
        RegisterApplicationShutdownParticipant();

        battle_map_panel.SetupRuntimeContext(_runtime_proxy, _runtime);

        Resized += _update_responsive_log_layout;
        _signalsConnected = true;
        if (runtime_log_dock != null)
            runtime_log_dock.panel_layout_changed += _update_responsive_log_layout;
        _update_responsive_log_layout();
        CallDeferred(MethodName._update_responsive_log_layout);

        battle_map_panel.battle_loading_state_changed += _on_battle_loading_state_changed;
        battle_map_panel.HideBattle();
        runtime_log_dock?.ClearLogs();
        _apply_battle_loading_overlay_skin();
        _set_battle_loading_overlay(false, 0.0f);

        GameContentCatalog contentCatalog = _game_session.GetContentCatalogTyped();
        party_management_window.SetAchievementDefs(contentCatalog.GetAchievementDefsTyped());
        party_management_window.SetItemDefs(contentCatalog.GetItemDefsTyped());
        party_management_window.SetTraitDefs(contentCatalog.GetTraitDefsTyped());
        party_management_window.SetSkillDefinitions(contentCatalog.GetSkillDefinitionsTyped());
        party_management_window.SetProfessionDefs(contentCatalog.GetProfessionDefsTyped());
        party_management_window.SetCharacterManagement(_runtime_proxy.GetCharacterManagement());

        ConnectSignals();
        world_map_view.Configure(
            _runtime_proxy.GetGridSystem(),
            _runtime_proxy.GetFogSystem(),
            _runtime_proxy.GetWorldRuntimeData(),
            _runtime_proxy.GetPlayerCoord(),
            _runtime_proxy.GetSelectedCoord(),
            _runtime_proxy.IsPlayerVisibleOnWorldMap(),
            _runtime_proxy.GetPlayerFactionId()
        );
        RenderFromRuntime(true);
    }

    public override void _ExitTree() => CloseRuntimeOwner();

    private void CloseRuntimeOwner()
    {
        if (_closed)
        {
            UnregisterApplicationShutdownParticipant();
            return;
        }
        _closed = true;
        UnregisterApplicationShutdownParticipant();

        try
        {
            if (_signalsConnected)
            {
                _signalsConnected = false;
                DisconnectSignals();
            }
            if (battle_map_panel != null)
                battle_map_panel.SetupRuntimeContext(null, null);
        }
        finally
        {
            WorldMapRuntimeProxy runtimeProxy = _runtime_proxy;
            GameRuntimeFacade runtime = _runtime;
            _runtime_proxy = null;
            _runtime = null;
            try
            {
                runtimeProxy?.Dispose();
            }
            finally
            {
                runtime?.Dispose();
                _clear_world_move_hold();
                ClearNodeRefs();
                _game_session = null;
            }
        }
    }

    private void RegisterApplicationShutdownParticipant()
    {
        ApplicationLifetimeCoordinator coordinator = GetTree()
            ?.Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        if (coordinator == null)
        {
            throw new InvalidOperationException(
                "WorldMapSystem requires ApplicationLifetimeCoordinator."
            );
        }

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

    public void _update_responsive_log_layout()
    {
        if (runtime_log_dock == null || map_viewport == null)
            return;
        Vector2 viewportSize = Size;
        if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
            return;
        Vector2 designPanelSize = runtime_log_dock.GetDesignPanelSize();
        float rightMargin = LOG_DOCK_DESIGN_RIGHT_MARGIN;
        bool isBattleActive = _runtime_proxy != null && _runtime_proxy.IsBattleActive();
        float topMargin = isBattleActive ? LOG_DOCK_BATTLE_TOP_MARGIN : LOG_DOCK_DESIGN_TOP_MARGIN;
        float bottomMargin = isBattleActive
            ? LOG_DOCK_BATTLE_BOTTOM_MARGIN
            : LOG_DOCK_DESIGN_BOTTOM_MARGIN;
        float availableHeight = viewportSize.Y - topMargin - bottomMargin;
        float preferredHeight = runtime_log_dock.GetPreferredHeight(
            availableHeight,
            LOG_DOCK_MIN_HEIGHT
        );
        if (isBattleActive && !runtime_log_dock.IsCollapsed())
            preferredHeight = Mathf.Clamp(
                preferredHeight,
                LOG_DOCK_MIN_HEIGHT,
                LOG_DOCK_MAX_HEIGHT
            );
        Vector2 panelSize = new(designPanelSize.X, preferredHeight);

        runtime_log_dock.AnchorLeft = 1.0f;
        runtime_log_dock.AnchorTop = 0.0f;
        runtime_log_dock.AnchorRight = 1.0f;
        runtime_log_dock.AnchorBottom = 0.0f;
        runtime_log_dock.OffsetLeft = -rightMargin - panelSize.X;
        runtime_log_dock.OffsetTop = topMargin;
        runtime_log_dock.OffsetRight = -rightMargin;
        runtime_log_dock.OffsetBottom = topMargin + panelSize.Y;
        runtime_log_dock.ApplyLayoutScale(1.0f);
        map_viewport.OffsetRight = 0.0f;
    }

    public void RenderFromRuntime()
    {
        RenderFromRuntimeCore(true, null, null);
    }

    public void RenderFromRuntime(bool refresh_world)
    {
        RenderFromRuntimeCore(refresh_world, null, null);
    }

    public void RenderFromRuntime(bool refresh_world, GDictionary command_result)
    {
        RenderFromRuntimeCore(refresh_world, command_result, null);
    }

    internal void RenderFromRuntime(
        bool refresh_world,
        BattlePresentationDelta battle_presentation_delta
    )
    {
        RenderFromRuntimeCore(refresh_world, null, battle_presentation_delta);
    }

    private void RenderFromRuntimeCore(
        bool refresh_world,
        GDictionary command_result,
        BattlePresentationDelta battle_presentation_delta
    )
    {
        if (_runtime == null)
            return;
        // nearbyLimit:0 — RenderFromRuntime only reads status/coords/modal from the
        // view model, never NearbyEncounters/NearbyWorldEvents (those feed the text
        // snapshot via a separate path). Passing 0 skips an O(all anchors) scan+sort
        // that otherwise ran on every render of both world and battle maps.
        WorldRuntimeViewModel worldViewModel = _runtime_proxy.GetWorldRuntimeViewModel(0);
        // 裸节点（未进场景树、UI 子节点未装配）上的渲染是 no-op；
        // headless 测试经 proxy 自动渲染时不应 NRE 吞掉 Quit。
        if (world_map_view == null || battle_map_panel == null)
            return;
        if (status_label != null)
            status_label.Text = worldViewModel.StatusText;
        string modalId = worldViewModel.ActiveModalId;
        _update_responsive_log_layout();
        if (bottom_action_bar != null)
            bottom_action_bar.Visible = !_runtime_proxy.IsBattleActive();
        if (party_button != null)
            party_button.Disabled =
                _runtime_proxy.IsBattleActive() || _runtime_proxy.IsModalWindowOpen();

        if (_runtime_proxy.IsBattleActive())
        {
            if (world_map_background != null)
                world_map_background.Visible = false;
            world_map_view.Visible = false;
            if (submap_hint_panel != null)
                submap_hint_panel.Visible = false;
            bool battlePanelWasVisible = battle_map_panel.Visible;
            BattleState battleState = _runtime_proxy.GetBattleState();
            bool skipBattlePanelRefresh =
                battle_map_panel.Visible
                && battle_presentation_delta != null
                && !battle_presentation_delta.RequiresPanelRefresh;
            if (skipBattlePanelRefresh)
            {
                if (
                    (
                        battle_presentation_delta.DirtyFlags
                        & BattlePresentationDirtyFlags.Log
                    ) != 0
                )
                {
                    // Keep the compact command-dock log current without touching board/HUD state.
                    battle_map_panel.RefreshLogs(battleState);
                }
            }
            else
            {
                string refreshMode = DictString(command_result, "battle_refresh_mode", "full");
                Vector2I selectedCoord = _runtime_proxy.GetBattleSelectedCoord();
                StringName selectedSkillId = _runtime_proxy.GetSelectedBattleSkillId();
                string selectedSkillName = _runtime_proxy.GetSelectedBattleSkillName();
                string selectedSkillVariantName =
                    _runtime_proxy.GetSelectedBattleSkillVariantName();
                StringName selectedSkillVariantId =
                    _runtime_proxy.GetSelectedBattleSkillVariantId();
                IReadOnlyList<Vector2I> selectedTargetCoords =
                    _runtime_proxy.GetSelectedBattleSkillTargetCoords();
                IReadOnlyList<StringName> selectedTargetUnitIds =
                    _runtime_proxy.GetSelectedBattleSkillTargetUnitIds();
                IReadOnlyList<Vector2I> validTargetCoords =
                    _runtime_proxy.GetBattleOverlayTargetCoords();
                int requiredCoordCount =
                    _runtime_proxy.GetSelectedBattleSkillRequiredCoordCount();
                bool useTypedOverlayRefresh =
                    battle_map_panel.Visible
                    && battle_presentation_delta?.RequiresPanelRefresh == true
                    && !battle_presentation_delta.RequiresFullBoardRefresh;
                if (
                    useTypedOverlayRefresh
                    || (battle_map_panel.Visible && refreshMode == "overlay")
                )
                {
                    battle_map_panel.RefreshOverlay(
                        battleState,
                        selectedCoord,
                        selectedSkillId,
                        selectedSkillName,
                        selectedSkillVariantName,
                        selectedTargetCoords,
                        validTargetCoords,
                        requiredCoordCount,
                        selectedTargetUnitIds,
                        selectedSkillVariantId
                    );
                    if (
                        battle_presentation_delta != null
                        && (
                            battle_presentation_delta.DirtyFlags
                            & BattlePresentationDirtyFlags.Units
                        ) != 0
                    )
                    {
                        battle_map_panel.RefreshUnits(
                            battleState,
                            battle_presentation_delta.ChangedUnitIds
                        );
                    }
                }
                else
                {
                    battle_map_panel.ShowBattle(
                        battleState,
                        selectedCoord,
                        selectedSkillId,
                        selectedSkillName,
                        selectedSkillVariantName,
                        selectedTargetCoords,
                        validTargetCoords,
                        requiredCoordCount,
                        selectedTargetUnitIds,
                        selectedSkillVariantId
                    );
                }
            }
            _set_battle_loading_overlay(
                battle_map_panel.IsLoadingBattle(),
                battle_map_panel.GetLoadingProgress()
            );
            if (ShouldRefreshBattleLogDock(battlePanelWasVisible, battle_presentation_delta))
                runtime_log_dock?.ShowBattleLogs(battleState);
        }
        else
        {
            if (world_map_background != null)
                world_map_background.Visible = true;
            world_map_view.Visible = true;
            battle_map_panel.HideBattle();
            _set_battle_loading_overlay(modalId == BATTLE_LOADING_MODAL_ID, 0.0f);
            if (refresh_world)
                world_map_view.RefreshWorld(_runtime_proxy.GetWorldRuntimeData());
            world_map_view.SetRuntimeState(
                worldViewModel.PlayerCoord,
                worldViewModel.SelectedCoord,
                worldViewModel.PlayerVisible
            );
            if (submap_hint_panel != null)
                submap_hint_panel.Visible = _runtime_proxy.IsSubmapActive();
            if (submap_hint_label != null)
                submap_hint_label.Text = worldViewModel.SubmapReturnHintText;
            runtime_log_dock?.ShowWorldLogs(
                _runtime_proxy.GetLogSnapshotPlain(120),
                worldViewModel.ActiveMapDisplayName,
                worldViewModel.StatusText
            );
        }

        RenderWindows(modalId);
    }

    internal static bool ShouldRefreshBattleLogDock(
        bool battlePanelWasVisible,
        BattlePresentationDelta battlePresentationDelta
    ) =>
        !battlePanelWasVisible
        || battlePresentationDelta == null
        || (
            battlePresentationDelta.DirtyFlags
            & (BattlePresentationDirtyFlags.Log | BattlePresentationDirtyFlags.FullBoard)
        ) != 0;

    public override void _Process(double delta)
    {
        if (_runtime == null)
            return;
        bool changed = _runtime_proxy.Advance((float)delta);
        if (changed)
        {
            if (_runtime_proxy.IsBattleActive())
            {
                RenderFromRuntime(
                    true,
                    _runtime_proxy.GetLastAdvanceBattlePresentationDelta()
                );
            }
            else
            {
                RenderFromRuntime(true);
            }
        }
        if (_runtime_proxy.IsBattleActive() || _runtime_proxy.IsModalWindowOpen())
        {
            _clear_world_move_hold();
            return;
        }
        _process_world_held_movement((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_runtime == null || @event is not InputEventKey keyEvent)
            return;
        if (_runtime_proxy.IsBattleActive())
        {
            if (battle_map_panel != null && battle_map_panel.IsLoadingBattle())
                return;
            if (_runtime_proxy.IsModalWindowOpen())
                return;
            if (!keyEvent.Pressed || keyEvent.Echo)
                return;
            if (_handle_battle_input(keyEvent))
                GetViewport().SetInputAsHandled();
            return;
        }
        if (_handle_world_input(keyEvent))
            GetViewport().SetInputAsHandled();
    }

    public bool _handle_world_input(InputEventKey key_event)
    {
        Vector2I movement = _get_world_move_direction_for_key(key_event.Keycode);
        if (!key_event.Pressed)
        {
            if (movement == Vector2I.Zero)
                return false;
            _release_world_move_key(key_event.Keycode);
            return true;
        }
        if (key_event.Echo)
            return movement != Vector2I.Zero;
        if (_runtime_proxy.IsModalWindowOpen())
            return false;
        if (movement != Vector2I.Zero)
        {
            _runtime_proxy.CommandWorldMove(movement, 1);
            _press_world_move_key(key_event.Keycode);
            if (_runtime_proxy.IsBattleActive())
                _clear_world_move_hold();
            return true;
        }
        if (key_event.Keycode == Key.P)
        {
            _runtime_proxy.CommandOpenParty();
            return true;
        }
        if (_is_world_settlement_confirm_key(key_event.Keycode))
        {
            _runtime_proxy.CommandOpenSettlement(new Vector2I(-1, -1));
            return true;
        }
        return false;
    }

    public void _process_world_held_movement(float delta)
    {
        if (_held_world_move_keys.Count == 0)
        {
            _world_move_repeat_timer = 0.0f;
            return;
        }
        _world_move_repeat_timer -= delta;
        while (_world_move_repeat_timer <= 0.0f)
        {
            Vector2I movement = _get_active_world_move_direction();
            if (movement == Vector2I.Zero)
            {
                _clear_world_move_hold();
                return;
            }
            _runtime_proxy.CommandWorldMove(movement, 1);
            if (_runtime_proxy.IsBattleActive() || _runtime_proxy.IsModalWindowOpen())
            {
                _clear_world_move_hold();
                return;
            }
            _world_move_repeat_timer += WORLD_MOVE_REPEAT_INTERVAL;
        }
    }

    public Vector2I _get_world_move_direction_for_key(Key keycode)
    {
        return keycode switch
        {
            Key.Left or Key.A => Vector2I.Left,
            Key.Right or Key.D => Vector2I.Right,
            Key.Up or Key.W => Vector2I.Up,
            Key.Down or Key.S => Vector2I.Down,
            _ => Vector2I.Zero,
        };
    }

    public void _press_world_move_key(Key keycode)
    {
        _held_world_move_keys.Remove(keycode);
        _held_world_move_keys.Add(keycode);
        _world_move_repeat_timer = WORLD_MOVE_REPEAT_INTERVAL;
    }

    public void _release_world_move_key(Key keycode)
    {
        bool wasActive = keycode == _get_active_world_move_keycode();
        _held_world_move_keys.Remove(keycode);
        if (_held_world_move_keys.Count == 0)
            _world_move_repeat_timer = 0.0f;
        else if (wasActive)
            _world_move_repeat_timer = WORLD_MOVE_REPEAT_INTERVAL;
    }

    public Vector2I _get_active_world_move_direction()
    {
        Key keycode = _get_active_world_move_keycode();
        return keycode == Key.None ? Vector2I.Zero : _get_world_move_direction_for_key(keycode);
    }

    public Key _get_active_world_move_keycode() =>
        _held_world_move_keys.Count == 0 ? Key.None : _held_world_move_keys[^1];

    public void _clear_world_move_hold()
    {
        _held_world_move_keys.Clear();
        _world_move_repeat_timer = 0.0f;
    }

    public bool _is_world_settlement_confirm_key(Key keycode) =>
        keycode == Key.Enter || keycode == Key.KpEnter;

    public bool _is_battle_wait_confirm_key(Key keycode) =>
        keycode == Key.Enter || keycode == Key.KpEnter;

    public bool _handle_battle_input(InputEventKey key_event)
    {
        switch (key_event.Keycode)
        {
            // Skill slots are mouse-click only (A2): combat is a deliberate strategy
            // pace, so the 1-9 keyboard skill binding was removed. Variant/clear/
            // resolve keep keyboard shortcuts as high-frequency tactical controls.
            case Key.Q:
                _runtime_proxy.CommandBattleCycleVariant(-1);
                break;
            case Key.E:
                _runtime_proxy.CommandBattleCycleVariant(1);
                break;
            case Key.Escape:
            case Key.R:
                _runtime_proxy.CommandBattleClearSkill();
                break;
            case Key.Space:
                _runtime_proxy.ResetBattleFocus();
                break;
            case Key.A:
                return PanBattleCamera(Vector2I.Left);
            case Key.D:
                return PanBattleCamera(Vector2I.Right);
            case Key.W:
                return PanBattleCamera(Vector2I.Up);
            case Key.S:
                return PanBattleCamera(Vector2I.Down);
            case Key.Left:
                _runtime_proxy.CommandBattleMoveDirection(Vector2I.Left);
                break;
            case Key.Right:
                _runtime_proxy.CommandBattleMoveDirection(Vector2I.Right);
                break;
            case Key.Up:
                _runtime_proxy.CommandBattleMoveDirection(Vector2I.Up);
                break;
            case Key.Down:
                _runtime_proxy.CommandBattleMoveDirection(Vector2I.Down);
                break;
            default:
                if (_is_battle_wait_confirm_key(key_event.Keycode))
                    _runtime_proxy.CommandBattleWaitOrResolve();
                else
                    return false;
                break;
        }
        return true;
    }

    public bool PanBattleCamera(Vector2I direction) =>
        battle_map_panel != null && battle_map_panel.PanBattleCamera(direction);

    public void _on_battle_loading_state_changed(bool is_loading, float progress_value) =>
        _set_battle_loading_overlay(is_loading, progress_value);

    public void _set_battle_loading_overlay(bool is_visible, float progress_value)
    {
        if (battle_loading_overlay == null || battle_loading_label == null)
            return;
        float clampedProgress = Mathf.Clamp(
            progress_value,
            BATTLE_LOADING_PROGRESS_MIN,
            BATTLE_LOADING_PROGRESS_MAX
        );
        battle_loading_label.Text = BATTLE_LOADING_LABEL_TEXT;
        if (battle_loading_progress_bar != null)
        {
            battle_loading_progress_bar.MinValue = BATTLE_LOADING_PROGRESS_MIN;
            battle_loading_progress_bar.MaxValue = BATTLE_LOADING_PROGRESS_MAX;
            battle_loading_progress_bar.Value = clampedProgress;
        }
        if (battle_loading_percent_label != null)
            battle_loading_percent_label.Text = _format_battle_loading_percent(clampedProgress);
        battle_loading_overlay.Visible = is_visible;
    }

    public void _apply_battle_loading_overlay_skin()
    {
        if (battle_loading_label == null)
            return;
        _style_loading_label(battle_loading_label, 24, BATTLE_LOADING_TEXT_COLOR);
        _style_loading_label(battle_loading_percent_label, 16, BATTLE_LOADING_PERCENT_TEXT_COLOR);
        _style_loading_progress_bar();
    }

    public void _style_loading_label(Label label, int font_size, Color font_color)
    {
        if (label == null)
            return;
        label.AddThemeFontSizeOverride("font_size", font_size);
        label.AddThemeColorOverride("font_color", font_color);
    }

    public void _style_loading_progress_bar()
    {
        if (battle_loading_progress_bar == null)
            return;
        var backgroundStyle = new StyleBoxFlat
        {
            BgColor = BATTLE_LOADING_BAR_BG_COLOR,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = BATTLE_LOADING_BAR_BORDER_COLOR,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomRight = 7,
            CornerRadiusBottomLeft = 7,
        };
        var fillStyle = new StyleBoxFlat
        {
            BgColor = BATTLE_LOADING_BAR_FILL_COLOR,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomRight = 7,
            CornerRadiusBottomLeft = 7,
        };
        battle_loading_progress_bar.AddThemeStyleboxOverride("background", backgroundStyle);
        battle_loading_progress_bar.AddThemeStyleboxOverride("fill", fillStyle);
    }

    public string _format_battle_loading_percent(float progress_value) =>
        $"{Mathf.RoundToInt(progress_value)}%";

    public void _on_world_map_cell_clicked(Vector2I coord)
    {
        if (_runtime != null)
            _runtime_proxy.SelectWorldCell(coord);
    }

    public void _on_world_map_cell_right_clicked(Vector2I coord)
    {
        if (_runtime != null)
            _runtime_proxy.InspectWorldCell(coord);
    }

    public void _on_battle_cell_clicked(Vector2I coord)
    {
        if (_runtime != null)
            _runtime_proxy.SelectBattleCell(coord);
    }

    public void _on_battle_cell_right_clicked(Vector2I coord)
    {
        if (_runtime == null)
            return;
        if (_runtime_proxy.GetSelectedBattleSkillId() != "")
        {
            _runtime_proxy.CommandBattleClearSkill();
            return;
        }
        _runtime_proxy.InspectBattleCell(coord);
    }

    public void _on_battle_cell_hovered(Vector2I coord)
    {
        if (_runtime == null || !_runtime_proxy.IsBattleActive())
            return;
        if (battle_map_panel.IsLoadingBattle())
            return;
        StringName selectedSkillId = _runtime_proxy.GetSelectedBattleSkillId();
        IReadOnlyList<Vector2I> validTargetCoords =
            _runtime_proxy.GetBattleOverlayTargetCoords();
        StringName selectedSkillVariantId = _runtime_proxy.GetSelectedBattleSkillVariantId();
        BattleState battleState = _runtime_proxy.GetBattleState();
        BattlePreview hoverPreview = battle_map_panel.UpdateHoverPreview(
            battleState,
            coord,
            validTargetCoords,
            selectedSkillId,
            selectedSkillVariantId
        );
        if (selectedSkillId == "")
            return;
        string selectedSkillName = _runtime_proxy.GetSelectedBattleSkillName();
        string selectedSkillVariantName = _runtime_proxy.GetSelectedBattleSkillVariantName();
        IReadOnlyList<Vector2I> selectedTargetCoords =
            _runtime_proxy.GetSelectedBattleSkillTargetCoords();
        int requiredCoordCount = _runtime_proxy.GetSelectedBattleSkillRequiredCoordCount();
        IReadOnlyList<StringName> selectedTargetUnitIds =
            _runtime_proxy.GetSelectedBattleSkillTargetUnitIds();
        if (validTargetCoords.Contains(coord))
        {
            battle_map_panel.RefreshOverlayWithPreview(
                battleState,
                coord,
                selectedSkillId,
                selectedSkillName,
                selectedSkillVariantName,
                selectedTargetCoords,
                validTargetCoords,
                requiredCoordCount,
                selectedTargetUnitIds,
                selectedSkillVariantId,
                hoverPreview
            );
            return;
        }
        bool restoredCachedPreview = battle_map_panel.RefreshOverlayWithCachedPreview(
            battleState,
            _runtime_proxy.GetBattleSelectedCoord(),
            selectedSkillId,
            selectedSkillName,
            selectedSkillVariantName,
            selectedTargetCoords,
            validTargetCoords,
            requiredCoordCount,
            selectedTargetUnitIds,
            selectedSkillVariantId
        );
        if (!restoredCachedPreview)
        {
            battle_map_panel.RefreshOverlay(
                battleState,
                _runtime_proxy.GetBattleSelectedCoord(),
                selectedSkillId,
                selectedSkillName,
                selectedSkillVariantName,
                selectedTargetCoords,
                validTargetCoords,
                requiredCoordCount,
                selectedTargetUnitIds,
                selectedSkillVariantId
            );
        }
    }

    public void _on_battle_skill_slot_selected(int index)
    {
        if (_runtime != null && !_runtime_proxy.IsModalWindowOpen())
            _runtime_proxy.CommandBattleSelectSkill(index);
    }

    public void _on_battle_resolve_pressed()
    {
        if (_runtime != null && !_runtime_proxy.IsModalWindowOpen())
            _runtime_proxy.CommandBattleWaitOrResolve();
    }

    public void _on_battle_cycle_variant_pressed(int step)
    {
        if (_runtime != null && !_runtime_proxy.IsModalWindowOpen())
            _runtime_proxy.CommandBattleCycleVariant(step);
    }

    public void _on_battle_clear_skill_pressed()
    {
        if (_runtime != null && !_runtime_proxy.IsModalWindowOpen())
            _runtime_proxy.CommandBattleClearSkill();
    }

    public void _on_settlement_action_requested(
        string settlement_id,
        string service_id,
        string action_id,
        string member_id,
        int quantity,
        string submission_source
    )
    {
        if (_runtime != null)
        {
            if (
                !SettlementSubmissionSources.TryParse(
                    submission_source,
                    out SettlementSubmissionSource source
                )
            )
            {
                source = SettlementSubmissionSource.None;
            }
            _runtime_proxy.CommandExecuteSettlementAction(
                new SettlementActionRequest(
                    new StringName(settlement_id ?? ""),
                    new StringName(
                        string.IsNullOrEmpty(service_id) ? action_id ?? "" : service_id
                    ),
                    new StringName(action_id ?? ""),
                    new StringName(member_id ?? ""),
                    Mathf.Max(quantity, 0),
                    source
                )
            );
        }
    }

    public void _on_settlement_window_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_shop_service_modal_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_contract_board_service_modal_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_contract_board_service_modal_action_requested(
        string _settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (_runtime != null)
            _runtime_proxy.CommandExecuteSettlementAction(action_id, payload);
    }

    public void _on_forge_service_modal_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_stagecoach_service_modal_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_npc_quest_offer_dialog_action_requested(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (_runtime != null)
            _runtime_proxy.CommandExecuteSettlementAction(action_id, payload);
    }

    public void _on_npc_quest_offer_dialog_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_bounty_board_window_action_requested(
        string settlement_id,
        string action_id,
        GDictionary payload
    )
    {
        if (_runtime != null)
            _runtime_proxy.CommandExecuteSettlementAction(action_id, payload);
    }

    public void _on_bounty_board_window_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_shop_service_modal_action_requested(
        string _settlement_id,
        string _action_id,
        GDictionary payload
    )
    {
        if (_runtime == null)
            return;
        int quantity = Mathf.Max(DictInt(payload, "request_quantity", 1), 1);
        StringName itemId = new(DictString(payload, "item_id"));
        StringName instanceId = new(DictString(payload, "instance_id"));
        if (DictString(payload, "shop_action", "buy") == "sell")
        {
            _runtime_proxy.CommandShopSell(itemId, quantity, instanceId);
            RenderFromRuntime();
        }
        else
        {
            _runtime_proxy.CommandShopBuy(itemId, quantity);
        }
    }

    private void _on_forge_service_modal_action_requested(ForgeActionRequest request)
    {
        if (_runtime != null)
            _runtime_proxy.CommandExecuteForgeAction(request);
    }

    public void _on_stagecoach_service_modal_action_requested(
        string _settlement_id,
        string _action_id,
        GDictionary payload
    )
    {
        if (_runtime == null)
            return;
        string targetSettlementId = DictString(payload, "target_settlement_id");
        if (string.IsNullOrEmpty(targetSettlementId))
            return;
        _runtime_proxy.CommandStagecoachTravel(targetSettlementId);
    }

    public void _on_character_info_window_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_party_leader_change_requested(StringName member_id)
    {
        if (_runtime != null)
            _runtime_proxy.CommandSetPartyLeader(member_id);
    }

    public void _on_party_roster_change_requested(
        GStringNameArray active_member_ids,
        GStringNameArray reserve_member_ids
    )
    {
        if (_runtime != null)
            _runtime_proxy.ApplyPartyRoster(active_member_ids, reserve_member_ids);
    }

    public void _on_party_management_window_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_party_management_warehouse_requested()
    {
        if (_runtime != null)
            _runtime_proxy.CommandOpenPartyWarehouse();
    }

    public void _on_party_contingency_setup_requested(StringName member_id)
    {
        ShowContingencySetupWindow(member_id);
    }

    public void _on_contingency_setup_save_requested(
        StringName member_id,
        StringName setup_payload_name
    )
    {
        if (_runtime == null)
            return;
        _runtime.SaveContingencySetupTemplateRuntimeTyped(member_id, setup_payload_name);
        RefreshAfterContingencyMutation(member_id);
    }

    public void _on_contingency_setup_charge_requested(StringName member_id, StringName setup_id)
    {
        if (_runtime == null)
            return;
        _runtime.ChargeContingencySetupRuntimeTyped(member_id, setup_id);
        RefreshAfterContingencyMutation(member_id);
    }

    public void _on_contingency_setup_clear_charge_requested(StringName member_id, StringName setup_id)
    {
        if (_runtime == null)
            return;
        _runtime.ClearContingencyChargeRuntimeTyped(member_id, setup_id);
        RefreshAfterContingencyMutation(member_id);
    }

    public void _on_contingency_setup_window_closed() { }

    private void RefreshAfterContingencyMutation(StringName memberId)
    {
        RenderFromRuntime(true);
        ShowContingencySetupWindow(memberId);
        if (party_management_window != null && party_management_window.Visible)
        {
            party_management_window.SetPartyState(_runtime_proxy.GetPartyState());
            party_management_window.SelectMember(memberId);
        }
    }

    private void ShowContingencySetupWindow(StringName memberId)
    {
        if (contingency_setup_window == null || _runtime_proxy == null)
            return;
        PartyState partyState = _runtime_proxy.GetPartyState();
        PartyMemberState member = partyState?.GetMemberState(memberId);
        if (member == null)
            return;
        contingency_setup_window.ShowForMember(member, _runtime_proxy.GetCharacterManagement());
    }

    public void _on_party_button_pressed()
    {
        if (
            _runtime != null
            && !_runtime_proxy.IsBattleActive()
            && !_runtime_proxy.IsModalWindowOpen()
        )
            _runtime_proxy.CommandOpenParty();
    }

    public void _on_party_warehouse_discard_one_requested(
        StringName item_id,
        StringName instance_id
    )
    {
        if (_runtime != null)
        {
            _runtime_proxy.CommandWarehouseDiscardOne(item_id, instance_id);
            RenderFromRuntime();
        }
    }

    public void _on_party_warehouse_discard_all_requested(StringName item_id)
    {
        if (_runtime != null)
        {
            _runtime_proxy.CommandWarehouseDiscardAll(item_id);
            RenderFromRuntime();
        }
    }

    public void _on_party_warehouse_use_requested(StringName item_id, StringName member_id)
    {
        if (_runtime != null)
        {
            _runtime_proxy.CommandWarehouseUseItem(
                item_id,
                member_id,
                (PartyItemUseService.PartyItemUseOptions)null
            );
            RenderFromRuntime();
        }
    }

    public void _on_party_warehouse_window_closed()
    {
        if (_runtime != null)
            _runtime_proxy.CommandCloseActiveModal();
    }

    public void _on_promotion_choice_submitted(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    )
    {
        if (_runtime != null)
            _runtime_proxy.SubmitPromotionChoice(
                member_id,
                profession_id,
                PromotionSelectionData.FromPayload(selection)
            );
    }

    public void _on_promotion_choice_cancelled()
    {
        if (_runtime != null)
            _runtime_proxy.CancelPromotionChoice();
    }

    public void _on_character_reward_confirmed()
    {
        if (_runtime != null)
            _runtime_proxy.ConfirmActiveReward();
    }

    public void _on_submap_entry_confirmed()
    {
        if (_runtime == null)
            return;
        string modalId = _runtime_proxy.GetActiveModalId();
        if (modalId == "battle_start_confirm")
        {
            _runtime_proxy.CommandConfirmBattleStart();
            return;
        }
        if (modalId == "game_over")
        {
            _return_to_startup_scene();
            return;
        }
        if (modalId == "resource_harvest_confirm")
        {
            _runtime_proxy.CommandConfirmResourceHarvest();
            return;
        }
        _runtime_proxy.CommandConfirmSubmapEntry();
    }

    public void _on_submap_entry_cancelled()
    {
        if (_runtime == null)
            return;
        string modalId = _runtime_proxy.GetActiveModalId();
        if (modalId == "battle_start_confirm" || modalId == "game_over")
        {
            RenderFromRuntime(false);
            return;
        }
        if (modalId == "resource_harvest_confirm")
        {
            _runtime_proxy.CommandCancelResourceHarvest();
            return;
        }
        _runtime_proxy.CommandCancelSubmapEntry();
    }

    private GodotProjectionLease<GDictionary> _build_confirmation_prompt(string modal_id)
    {
        if (modal_id == "submap_confirm")
        {
            using GDictionary raw = _runtime_proxy.GetPendingSubmapPrompt();
            return RuntimePlainPayload.ProjectDictionaryLease(
                RuntimePlainPayload.NormalizeDictionary(raw, "WorldMapSystem.submap_prompt"),
                "WorldMapSystem.submap_prompt",
                LifetimeDomain.Request,
                "WorldMapSystem.submap_prompt"
            );
        }
        if (modal_id == "resource_harvest_confirm")
        {
            using GDictionary raw = _runtime_proxy.GetPendingResourceHarvestPrompt();
            return RuntimePlainPayload.ProjectDictionaryLease(
                RuntimePlainPayload.NormalizeDictionary(
                    raw,
                    "WorldMapSystem.resource_harvest_prompt"
                ),
                "WorldMapSystem.resource_harvest_prompt",
                LifetimeDomain.Request,
                "WorldMapSystem.resource_harvest_prompt"
            );
        }
        if (modal_id == "battle_start_confirm")
            return _runtime_proxy.GetPendingBattleStartPromptLease();
        if (modal_id == "game_over")
        {
            GodotProjectionLease<GDictionary> lease =
                _runtime_proxy.GetGameOverContextLease();
            GDictionary prompt = lease.Value;
            prompt["cancel_visible"] = false;
            prompt["dismiss_on_shade"] = false;
            prompt["accept_input_enabled"] = true;
            prompt["panel_min_size"] = new Vector2(760, 320);
            prompt["title_font_size"] = 40;
            prompt["description_font_size"] = 24;
            prompt["confirm_button_min_size"] = new Vector2(240, 64);
            prompt["confirm_button_font_size"] = 24;
            prompt["margin_left"] = 40;
            prompt["margin_top"] = 34;
            prompt["margin_right"] = 40;
            prompt["margin_bottom"] = 34;
            prompt["layout_separation"] = 26;
            return lease;
        }
        return RuntimePlainPayload.ProjectDictionaryLease(
            new System.Collections.Generic.Dictionary<string, object>(
                System.StringComparer.Ordinal
            ),
            "WorldMapSystem.empty_prompt",
            LifetimeDomain.Request,
            "WorldMapSystem.empty_prompt"
        );
    }

    public void _return_to_startup_scene()
    {
        _clear_world_move_hold();
        SceneTree sceneTree = GetTree();
        if (sceneTree == null)
        {
            GameLog.Error("WorldMapSystem 无法获取 SceneTree，不能返回标题。", "worldmap.return_title.no_scene_tree", "worldmap");
            return;
        }
        string startupScenePath = _get_configured_startup_scene_path();
        if (string.IsNullOrEmpty(startupScenePath))
        {
            GameLog.Error("WorldMapSystem 未配置 application/run/main_scene，不能返回标题。", "worldmap.return_title.no_main_scene", "worldmap");
            return;
        }
        int flushError = _runtime?.FlushCanonicalRuntimeState("runtime.return_title")
            ?? (int)Error.Ok;
        if (flushError != (int)Error.Ok)
        {
            GameLog.Error(
                $"WorldMapSystem 返回标题前保存运行时状态失败，错误码 {flushError}。",
                "worldmap.return_title.save_failed",
                "worldmap"
            );
            return;
        }
        Error changeError = sceneTree.ChangeSceneToFile(startupScenePath);
        if (changeError != Error.Ok)
        {
            GameLog.Error($"WorldMapSystem 返回标题失败，错误码 {(int)changeError}。", "worldmap.return_title.failed", "worldmap");
            return;
        }
        _game_session?.UnloadActiveWorld();
    }

    public string _get_configured_startup_scene_path() =>
        ProjectSettings.GetSetting(STARTUP_SCENE_SETTING, "").AsString().StripEdges();

    private void BindNodes()
    {
        world_map_view = GetNode<WorldMapView>("MapViewport/WorldMapView");
        map_viewport = GetNode<Control>("MapViewport");
        world_map_background = GetNodeOrNull<CanvasItem>("MapViewport/WorldMapBackground");
        battle_map_panel = GetNode<BattleMapPanel>("MapViewport/BattleMapPanel");
        runtime_log_dock = GetNode<RuntimeLogDock>("%RuntimeLogDock");
        status_label = GetNodeOrNull<Label>("StatusPanel/StatusMargin/StatusLabel");
        settlement_window = GetNode<SettlementWindow>("SettlementWindow");
        contract_board_service_modal = GetNode<ShopWindow>("ContractBoardServiceModal");
        shop_service_modal = GetNode<ShopWindow>("ShopServiceModal");
        forge_service_modal = GetNode<ShopWindow>("ForgeServiceModal");
        stagecoach_service_modal = GetNode<ShopWindow>("StagecoachServiceModal");
        npc_quest_offer_dialog = GetNode<NpcQuestOfferDialog>("NpcQuestOfferDialog");
        bounty_board_window = GetNode<BountyBoardWindow>("BountyBoardWindow");
        character_info_window = GetNode<CharacterInfoWindow>("CharacterInfoWindow");
        party_management_window = GetNode<PartyManagementWindow>("PartyManagementWindow");
        contingency_setup_window = GetNodeOrNull<ContingencySetupWindow>("ContingencySetupWindow");
        if (contingency_setup_window == null)
        {
            contingency_setup_window = EngineAssetAccess
                .ResolveBorrowed<PackedScene>(this, ContingencySetupWindowScenePath)
                .Instantiate<ContingencySetupWindow>();
            contingency_setup_window.Name = "ContingencySetupWindow";
            AddChild(contingency_setup_window);
        }
        party_warehouse_window = GetNode<PartyWarehouseWindow>("PartyWarehouseWindow");
        promotion_choice_window = GetNode<PromotionChoiceWindow>("PromotionChoiceWindow");
        character_reward_window = GetNode<MasteryRewardWindow>("MasteryRewardWindow");
        submap_entry_window = GetNode<SubmapEntryWindow>("SubmapEntryWindow");
        submap_hint_panel = GetNode<Control>("%SubmapHintPanel");
        submap_hint_label = GetNode<Label>("%SubmapHintLabel");
        bottom_action_bar = GetNode<Control>("%BottomActionBar");
        party_button = GetNode<Button>("%PartyButton");
        battle_loading_overlay = GetNode<Control>("%BattleLoadingOverlay");
        battle_loading_label = GetNode<Label>("%BattleLoadingLabel");
        battle_loading_progress_bar = GetNode<ProgressBar>("%BattleLoadingProgressBar");
        battle_loading_percent_label = GetNode<Label>("%BattleLoadingPercentLabel");
    }

    private void ConnectSignals()
    {
        settlement_window.action_requested += _on_settlement_action_requested;
        settlement_window.closed += _on_settlement_window_closed;
        contract_board_service_modal.action_requested +=
            _on_contract_board_service_modal_action_requested;
        contract_board_service_modal.closed += _on_contract_board_service_modal_closed;
        shop_service_modal.action_requested += _on_shop_service_modal_action_requested;
        shop_service_modal.closed += _on_shop_service_modal_closed;
        forge_service_modal.ForgeActionRequested +=
            _on_forge_service_modal_action_requested;
        forge_service_modal.closed += _on_forge_service_modal_closed;
        stagecoach_service_modal.action_requested += _on_stagecoach_service_modal_action_requested;
        stagecoach_service_modal.closed += _on_stagecoach_service_modal_closed;
        npc_quest_offer_dialog.action_requested += _on_npc_quest_offer_dialog_action_requested;
        npc_quest_offer_dialog.closed += _on_npc_quest_offer_dialog_closed;
        bounty_board_window.action_requested += _on_bounty_board_window_action_requested;
        bounty_board_window.closed += _on_bounty_board_window_closed;
        character_info_window.closed += _on_character_info_window_closed;
        party_management_window.leader_change_requested += _on_party_leader_change_requested;
        party_management_window.roster_change_requested += _on_party_roster_change_requested;
        party_management_window.warehouse_requested += _on_party_management_warehouse_requested;
        party_management_window.contingency_setup_requested += _on_party_contingency_setup_requested;
        party_management_window.closed += _on_party_management_window_closed;
        if (contingency_setup_window != null)
        {
            contingency_setup_window.save_requested += _on_contingency_setup_save_requested;
            contingency_setup_window.charge_requested += _on_contingency_setup_charge_requested;
            contingency_setup_window.clear_charge_requested += _on_contingency_setup_clear_charge_requested;
            contingency_setup_window.closed += _on_contingency_setup_window_closed;
        }
        party_warehouse_window.discard_one_requested += _on_party_warehouse_discard_one_requested;
        party_warehouse_window.discard_all_requested += _on_party_warehouse_discard_all_requested;
        party_warehouse_window.use_requested += _on_party_warehouse_use_requested;
        party_warehouse_window.closed += _on_party_warehouse_window_closed;
        promotion_choice_window.choice_submitted += _on_promotion_choice_submitted;
        promotion_choice_window.cancelled += _on_promotion_choice_cancelled;
        character_reward_window.confirmed += _on_character_reward_confirmed;
        submap_entry_window.confirmed += _on_submap_entry_confirmed;
        submap_entry_window.cancelled += _on_submap_entry_cancelled;
        party_button.Pressed += _on_party_button_pressed;
        world_map_view.cell_clicked += _on_world_map_cell_clicked;
        world_map_view.cell_right_clicked += _on_world_map_cell_right_clicked;
        battle_map_panel.battle_cell_clicked += _on_battle_cell_clicked;
        battle_map_panel.battle_cell_right_clicked += _on_battle_cell_right_clicked;
        battle_map_panel.battle_cell_hovered += _on_battle_cell_hovered;
        battle_map_panel.battle_skill_slot_selected += _on_battle_skill_slot_selected;
        battle_map_panel.battle_resolve_pressed += _on_battle_resolve_pressed;
        battle_map_panel.battle_cycle_variant_pressed += _on_battle_cycle_variant_pressed;
        battle_map_panel.battle_clear_skill_pressed += _on_battle_clear_skill_pressed;
    }

    private void DisconnectSignals()
    {
        Resized -= _update_responsive_log_layout;
        if (runtime_log_dock != null)
            runtime_log_dock.panel_layout_changed -= _update_responsive_log_layout;
        if (battle_map_panel != null)
            battle_map_panel.battle_loading_state_changed -= _on_battle_loading_state_changed;
        if (settlement_window != null)
        {
            settlement_window.action_requested -= _on_settlement_action_requested;
            settlement_window.closed -= _on_settlement_window_closed;
        }
        if (contract_board_service_modal != null)
        {
            contract_board_service_modal.action_requested -=
                _on_contract_board_service_modal_action_requested;
            contract_board_service_modal.closed -= _on_contract_board_service_modal_closed;
        }
        if (shop_service_modal != null)
        {
            shop_service_modal.action_requested -= _on_shop_service_modal_action_requested;
            shop_service_modal.closed -= _on_shop_service_modal_closed;
        }
        if (forge_service_modal != null)
        {
            forge_service_modal.ForgeActionRequested -=
                _on_forge_service_modal_action_requested;
            forge_service_modal.closed -= _on_forge_service_modal_closed;
        }
        if (stagecoach_service_modal != null)
        {
            stagecoach_service_modal.action_requested -= _on_stagecoach_service_modal_action_requested;
            stagecoach_service_modal.closed -= _on_stagecoach_service_modal_closed;
        }
        if (npc_quest_offer_dialog != null)
        {
            npc_quest_offer_dialog.action_requested -= _on_npc_quest_offer_dialog_action_requested;
            npc_quest_offer_dialog.closed -= _on_npc_quest_offer_dialog_closed;
        }
        if (bounty_board_window != null)
        {
            bounty_board_window.action_requested -= _on_bounty_board_window_action_requested;
            bounty_board_window.closed -= _on_bounty_board_window_closed;
        }
        if (character_info_window != null)
            character_info_window.closed -= _on_character_info_window_closed;
        if (party_management_window != null)
        {
            party_management_window.leader_change_requested -= _on_party_leader_change_requested;
            party_management_window.roster_change_requested -= _on_party_roster_change_requested;
            party_management_window.warehouse_requested -= _on_party_management_warehouse_requested;
            party_management_window.contingency_setup_requested -= _on_party_contingency_setup_requested;
            party_management_window.closed -= _on_party_management_window_closed;
        }
        if (contingency_setup_window != null)
        {
            contingency_setup_window.save_requested -= _on_contingency_setup_save_requested;
            contingency_setup_window.charge_requested -= _on_contingency_setup_charge_requested;
            contingency_setup_window.clear_charge_requested -= _on_contingency_setup_clear_charge_requested;
            contingency_setup_window.closed -= _on_contingency_setup_window_closed;
        }
        if (party_warehouse_window != null)
        {
            party_warehouse_window.discard_one_requested -= _on_party_warehouse_discard_one_requested;
            party_warehouse_window.discard_all_requested -= _on_party_warehouse_discard_all_requested;
            party_warehouse_window.use_requested -= _on_party_warehouse_use_requested;
            party_warehouse_window.closed -= _on_party_warehouse_window_closed;
        }
        if (promotion_choice_window != null)
        {
            promotion_choice_window.choice_submitted -= _on_promotion_choice_submitted;
            promotion_choice_window.cancelled -= _on_promotion_choice_cancelled;
        }
        if (character_reward_window != null)
            character_reward_window.confirmed -= _on_character_reward_confirmed;
        if (submap_entry_window != null)
        {
            submap_entry_window.confirmed -= _on_submap_entry_confirmed;
            submap_entry_window.cancelled -= _on_submap_entry_cancelled;
        }
        if (party_button != null)
            party_button.Pressed -= _on_party_button_pressed;
        if (world_map_view != null)
        {
            world_map_view.cell_clicked -= _on_world_map_cell_clicked;
            world_map_view.cell_right_clicked -= _on_world_map_cell_right_clicked;
        }
        if (battle_map_panel != null)
        {
            battle_map_panel.battle_cell_clicked -= _on_battle_cell_clicked;
            battle_map_panel.battle_cell_right_clicked -= _on_battle_cell_right_clicked;
            battle_map_panel.battle_cell_hovered -= _on_battle_cell_hovered;
            battle_map_panel.battle_skill_slot_selected -= _on_battle_skill_slot_selected;
            battle_map_panel.battle_resolve_pressed -= _on_battle_resolve_pressed;
            battle_map_panel.battle_cycle_variant_pressed -= _on_battle_cycle_variant_pressed;
            battle_map_panel.battle_clear_skill_pressed -= _on_battle_clear_skill_pressed;
        }
    }

    private void ClearNodeRefs()
    {
        world_map_view = null;
        map_viewport = null;
        world_map_background = null;
        battle_map_panel = null;
        runtime_log_dock = null;
        status_label = null;
        settlement_window = null;
        contract_board_service_modal = null;
        shop_service_modal = null;
        forge_service_modal = null;
        stagecoach_service_modal = null;
        npc_quest_offer_dialog = null;
        bounty_board_window = null;
        character_info_window = null;
        party_management_window = null;
        contingency_setup_window = null;
        party_warehouse_window = null;
        promotion_choice_window = null;
        character_reward_window = null;
        submap_entry_window = null;
        submap_hint_panel = null;
        submap_hint_label = null;
        bottom_action_bar = null;
        party_button = null;
        battle_loading_overlay = null;
        battle_loading_label = null;
        battle_loading_progress_bar = null;
        battle_loading_percent_label = null;
    }

    private void RenderWindows(string modalId)
    {
        if (modalId == "settlement")
        {
            settlement_window.ShowSettlement(
                _runtime_proxy.GetSettlementWindowData("")
            );
            string settlementFeedback = _runtime_proxy.GetSettlementFeedbackText();
            if (!string.IsNullOrEmpty(settlementFeedback))
                settlement_window.SetFeedback(settlementFeedback);
        }
        else
            settlement_window.HideWindow();
        if (modalId == "shop")
        {
            using GodotProjectionLease<GDictionary> windowLease =
                _runtime_proxy.GetShopWindowDataLease();
            shop_service_modal.ShowShop(windowLease.Value);
        }
        else
            shop_service_modal.HideWindow();
        if (modalId == "contract_board")
        {
            using GodotProjectionLease<GDictionary> windowLease =
                _runtime_proxy.GetContractBoardWindowDataLease();
            contract_board_service_modal.ShowShop(windowLease.Value);
        }
        else
            contract_board_service_modal.HideWindow();
        if (modalId == "forge")
        {
            using GodotProjectionLease<GDictionary> windowLease =
                _runtime_proxy.GetForgeWindowDataLease();
            forge_service_modal.ShowShop(windowLease.Value);
        }
        else
            forge_service_modal.HideWindow();
        if (modalId == "stagecoach")
        {
            using GodotProjectionLease<GDictionary> windowLease =
                _runtime_proxy.GetStagecoachWindowDataLease();
            stagecoach_service_modal.ShowStagecoach(windowLease.Value);
        }
        else
            stagecoach_service_modal.HideWindow();
        // NPC quest offer modal lifecycle is tied to RuntimeModalKind.NpcQuestOffer.
        if (modalId == "npc_quest_offer")
            npc_quest_offer_dialog.ShowDialog(_runtime_proxy.GetNpcQuestOfferWindowDataTyped());
        else
            npc_quest_offer_dialog.HideDialog();
        if (modalId == "bounty_board")
            bounty_board_window.ShowBoard(_runtime_proxy.GetBountyBoardWindowDataTyped());
        else
            bounty_board_window.HideWindow();
        if (modalId == "character_info")
        {
            using GodotProjectionLease<GDictionary> contextLease =
                _runtime_proxy.GetCharacterInfoContextLease();
            character_info_window.ShowCharacter(contextLease.Value);
        }
        else
            character_info_window.HideWindow();
        if (modalId == "party")
        {
            party_management_window.ShowParty(_runtime_proxy.GetPartyState());
            StringName selectedMemberId = _runtime_proxy.GetPartySelectedMemberId();
            if (selectedMemberId != "")
                party_management_window.SelectMember(selectedMemberId);
        }
        else
            party_management_window.HideWindow();
        if (modalId == "warehouse")
            party_warehouse_window.ShowWarehouse(_runtime_proxy.GetWarehouseWindowData());
        else
            party_warehouse_window.HideWindow();
        if (modalId == "promotion")
            promotion_choice_window.ShowPromotion(
                _runtime_proxy.GetCurrentPromotionPromptSnapshotPlain()
            );
        else
            promotion_choice_window.HideWindow();
        if (modalId == "reward")
            character_reward_window.ShowReward(
                _runtime_proxy.GetActiveReward(),
                _runtime_proxy.GetPendingRewardCount()
            );
        else
            character_reward_window.HideWindow();
        if (
            modalId == "submap_confirm"
            || modalId == "resource_harvest_confirm"
            || modalId == "battle_start_confirm"
            || modalId == "game_over"
        )
        {
            using GodotProjectionLease<GDictionary> promptLease =
                _build_confirmation_prompt(modalId);
            submap_entry_window.ShowPrompt(promptLease.Value);
        }
        else
            submap_entry_window.HideWindow();
    }

    private static string DictString(GDictionary dict, string key, string fallback = "") =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsString() : fallback;

    private static int DictInt(GDictionary dict, string key, int fallback = 0) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt32() : fallback;
}
