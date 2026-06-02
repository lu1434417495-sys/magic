using System;
using Godot;
using Godot.Collections;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleSessionFacade : RefCounted
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    public void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    public void setup(GameRuntimeFacade runtime)
    {
        Setup(runtime);
    }

    public new void Dispose()
    {
        _runtime = null;
    }

    public void dispose()
    {
        Dispose();
    }

    public string get_selected_battle_skill_name() => GetSelectedBattleSkillName();

    public string get_selected_battle_skill_variant_name() => GetSelectedBattleSkillVariantName();

    public GVector2IArray get_selected_battle_skill_target_coords() =>
        GetSelectedBattleSkillTargetCoords();

    public GStringNameArray get_selected_battle_skill_target_unit_ids() =>
        GetSelectedBattleSkillTargetUnitIds();

    public GVector2IArray get_selected_battle_skill_valid_target_coords() =>
        GetSelectedBattleSkillValidTargetCoords();

    public int get_selected_battle_skill_required_coord_count() =>
        GetSelectedBattleSkillRequiredCoordCount();

    public GVector2IArray get_battle_movement_reachable_coords() =>
        GetBattleMovementReachableCoords();

    public GVector2IArray get_battle_overlay_target_coords() => GetBattleOverlayTargetCoords();

    public string get_battle_active_unit_name() => GetBattleActiveUnitName();

    public Dictionary get_battle_terrain_counts() => GetBattleTerrainCounts();

    public Dictionary command_battle_tick(int tickCount) => CommandBattleTick(tickCount);

    public Dictionary command_battle_select_skill(int slotIndex) =>
        CommandBattleSelectSkill(slotIndex);

    public Dictionary command_battle_cycle_variant(int step) => CommandBattleCycleVariant(step);

    public Dictionary command_battle_clear_skill() => CommandBattleClearSkill();

    public Dictionary command_battle_move_to(Vector2I targetCoord) =>
        CommandBattleMoveTo(targetCoord);

    public Dictionary command_battle_move_direction(Vector2I direction) =>
        CommandBattleMoveDirection(direction);

    public Dictionary command_battle_wait_or_resolve() => CommandBattleWaitOrResolve();

    public Dictionary command_battle_inspect(Vector2I coord) => CommandBattleInspect(coord);

    public Dictionary reset_battle_focus() => ResetBattleFocus();

    public bool handle_battle_input(InputEventKey keyEvent) => HandleBattleInput(keyEvent);

    public void start_battle(EncounterAnchorData encounterAnchor) => StartBattle(encounterAnchor);

    public Dictionary resolve_active_battle() => ResolveActiveBattle();

    public BattleResolutionResult get_battle_resolution_result(BattleRuntimeModule battleRuntime) =>
        GetBattleResolutionResult(battleRuntime);

    public BattleResolutionResult consume_battle_resolution_result(BattleRuntimeModule battleRuntime) =>
        ConsumeBattleResolutionResult(battleRuntime);

    public StringName attempt_battle_move(Vector2I direction) => AttemptBattleMove(direction);

    public void on_battle_cell_clicked(Vector2I coord) => OnBattleCellClicked(coord);

    public void on_battle_cell_right_clicked(Vector2I coord) => OnBattleCellRightClicked(coord);

    public void on_battle_skill_slot_selected(int index) => OnBattleSkillSlotSelected(index);

    public void apply_battle_batch(BattleEventBatch batch) => ApplyBattleBatch(batch);

    public void refresh_battle_runtime_state() => RefreshBattleRuntimeState();

    public int build_battle_seed(EncounterAnchorData encounterAnchor) =>
        BuildBattleSeed(encounterAnchor);

    public BattleState get_runtime_battle_state() => GetRuntimeBattleState();

    public bool is_battle_finished() => IsBattleFinished();

    public BattleUnitState get_runtime_active_unit() => GetRuntimeActiveUnit();

    public BattleUnitState get_manual_active_unit() => GetManualActiveUnit();

    public BattleUnitState get_runtime_unit_at_coord(Vector2I coord) =>
        GetRuntimeUnitAtCoord(coord);

    public BattleCommand build_wait_command() => BuildWaitCommand();

    public StringName issue_battle_command(BattleCommand command) => IssueBattleCommand(command);

    public void capture_pending_promotion_prompt(Godot.Collections.Array progressionDeltas) =>
        CapturePendingPromotionPrompt(progressionDeltas);

    public Dictionary build_promotion_prompt(CharacterProgressionDelta delta, string selectionHint) =>
        BuildPromotionPrompt(delta, selectionHint);

    public Vector2I get_default_battle_selected_coord() => GetDefaultBattleSelectedCoord();

    public BattleUnitState get_battle_unit_by_id(StringName unitId) => GetBattleUnitById(unitId);

    public BattleUnitState get_battle_unit_at_coord(Vector2I coord) => GetBattleUnitAtCoord(coord);

    public BattleUnitState get_battle_active_unit() => GetBattleActiveUnit();

    public string get_battle_unit_type_label(string unitId) => GetBattleUnitTypeLabel(unitId);

    public Dictionary build_battle_start_context(EncounterAnchorData encounterAnchor) =>
        BuildBattleStartContext(encounterAnchor);

    public StringName resolve_battle_terrain_profile(EncounterAnchorData encounterAnchor) =>
        ResolveBattleTerrainProfile(encounterAnchor);

    public string GetSelectedBattleSkillName()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "";
        return battleSelection.get_selected_battle_skill_name();
    }

    public string GetSelectedBattleSkillVariantName()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "";
        return battleSelection.get_selected_battle_skill_variant_name();
    }

    public GVector2IArray GetSelectedBattleSkillTargetCoords()
    {
        if (IsBattleInteractionBlocked())
            return new GVector2IArray();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return new GVector2IArray();
        return DuplicateVector2IArray(battleSelection.get_selected_battle_skill_target_coords());
    }

    public GStringNameArray GetSelectedBattleSkillTargetUnitIds()
    {
        if (IsBattleInteractionBlocked())
            return new GStringNameArray();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return new GStringNameArray();
        return DuplicateStringNameArray(battleSelection.get_selected_battle_skill_target_unit_ids());
    }

    public GVector2IArray GetSelectedBattleSkillValidTargetCoords()
    {
        if (IsBattleInteractionBlocked())
            return new GVector2IArray();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return new GVector2IArray();
        return DuplicateVector2IArray(
            battleSelection.get_selected_battle_skill_valid_target_coords()
        );
    }

    public int GetSelectedBattleSkillRequiredCoordCount()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return 0;
        return battleSelection.get_selected_battle_skill_required_coord_count();
    }

    public GVector2IArray GetBattleMovementReachableCoords()
    {
        var battleRuntime = GetBattleRuntime();
        if (!IsBattleReady() || !IsBattleActive() || battleRuntime == null)
            return new GVector2IArray();
        if (IsBattleInteractionBlocked())
            return new GVector2IArray();
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
            return new GVector2IArray();
        return DuplicateVector2IArray(battleRuntime.get_unit_reachable_move_coords(activeUnit));
    }

    public GVector2IArray GetBattleOverlayTargetCoords()
    {
        if (!IsBattleReady())
            return new GVector2IArray();
        if (IsBattleInteractionBlocked())
            return new GVector2IArray();
        if (_runtime.get_selected_battle_skill_id() != "")
            return GetSelectedBattleSkillValidTargetCoords();
        return GetBattleMovementReachableCoords();
    }

    public string GetBattleActiveUnitName()
    {
        var activeUnit = GetBattleActiveUnit();
        if (activeUnit == null)
            return "无";
        return !string.IsNullOrEmpty(activeUnit.display_name)
            ? activeUnit.display_name
            : activeUnit.unit_id.ToString();
    }

    public Dictionary GetBattleTerrainCounts()
    {
        var counts = new Dictionary
        {
            [BattleCellState.TERRAIN_LAND().ToString()] = 0,
            [BattleCellState.TERRAIN_FOREST().ToString()] = 0,
            [BattleCellState.TERRAIN_SHALLOW_WATER().ToString()] = 0,
            [BattleCellState.TERRAIN_FLOWING_WATER().ToString()] = 0,
            [BattleCellState.TERRAIN_DEEP_WATER().ToString()] = 0,
            [BattleCellState.TERRAIN_MUD().ToString()] = 0,
            [BattleCellState.TERRAIN_SPIKE().ToString()] = 0,
        };
        var battleState = GetBattleState();
        if (!IsBattleReady() || battleState == null)
            return counts;
        foreach (var cellValue in battleState.cells.Values)
        {
            var cellState = cellValue.As<BattleCellState>();
            if (cellState == null)
                continue;
            var terrainId = cellState.base_terrain.ToString();
            if (!counts.ContainsKey(terrainId))
                counts[terrainId] = 0;
            counts[terrainId] = counts[terrainId].AsInt32() + 1;
        }
        return counts;
    }

    public Dictionary CommandBattleTick(int tickCount)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        if (tickCount <= 0)
            return CommandError("推进 tick 必须大于 0。");
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return RuntimeUnavailableError();
        for (int i = 0; i < Mathf.Max(tickCount, 0); i++)
        {
            if (!IsBattleActive())
                break;
            var runtimeState = GetRuntimeBattleState();
            if (runtimeState != null && runtimeState.modal_state != "")
                break;
            BattleEventBatch batch = battleRuntime.advance(1);
            if (BatchHasUpdates(batch))
                ApplyBattleBatch(batch);
        }
        return CommandOk();
    }

    public Dictionary CommandBattleSelectSkill(int slotIndex)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableError();
        var selectResult = battleSelection.select_battle_skill_slot(slotIndex);
        if (!DictionaryBool(selectResult, "ok", false))
            return CommandError(DictionaryString(selectResult, "message"));
        return CommandOk("", "overlay");
    }

    public Dictionary CommandBattleCycleVariant(int step)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableError();
        battleSelection.cycle_selected_battle_skill_option(step);
        return CommandOk("", "overlay");
    }

    public Dictionary CommandBattleClearSkill()
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableError();
        battleSelection.clear_battle_skill_selection(true);
        return CommandOk("", "overlay");
    }

    public Dictionary CommandBattleMoveTo(Vector2I targetCoord)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableError();
        var battleRefreshMode = battleSelection.attempt_battle_move_to(targetCoord);
        if (battleRefreshMode == "error")
            return CommandError(GetRuntimeStatusText("当前技能无法施放。"));
        return CommandOk("", battleRefreshMode.ToString());
    }

    public Dictionary CommandBattleMoveDirection(Vector2I direction)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        if (direction == Vector2I.Zero)
            return CommandError("战斗移动方向不能为空。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var battleRefreshMode = AttemptBattleMove(direction);
        if (battleRefreshMode == "error")
            return CommandError(GetRuntimeStatusText("当前技能无法施放。"));
        return CommandOk("", battleRefreshMode.ToString());
    }

    public Dictionary CommandBattleWaitOrResolve()
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var resolveResult = ResolveActiveBattle();
        if (!DictionaryBool(resolveResult, "ok", false))
            return CommandError(
                DictionaryString(resolveResult, "message", "战斗结算失败。")
            );
        return resolveResult;
    }

    public Dictionary CommandBattleInspect(Vector2I coord)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        if (!IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        if (TryOpenCharacterInfoAtBattleCoord(coord))
            return CommandOk();
        return CommandError("该战斗格没有可查看单位。");
    }

    public Dictionary ResetBattleFocus()
    {
        if (!IsBattleReady())
            return RuntimeUnavailableError();
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandError(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableError();
        return CommandOk("", battleSelection.reset_battle_movement().ToString());
    }

    public bool HandleBattleInput(InputEventKey keyEvent)
    {
        if (!IsBattleReady())
            return false;
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateStatus(blockReason);
            return false;
        }
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return false;
        switch (keyEvent.Keycode)
        {
            case Key.Key1:
            case Key.Key2:
            case Key.Key3:
            case Key.Key4:
            case Key.Key5:
            case Key.Key6:
            case Key.Key7:
            case Key.Key8:
            case Key.Key9:
                battleSelection.select_battle_skill_slot((int)keyEvent.Keycode - (int)Key.Key1);
                break;
            case Key.Q:
                battleSelection.cycle_selected_battle_skill_option(-1);
                break;
            case Key.E:
                battleSelection.cycle_selected_battle_skill_option(1);
                break;
            case Key.Escape:
                battleSelection.clear_battle_skill_selection(true);
                break;
            case Key.Left:
                AttemptBattleMove(Vector2I.Left);
                break;
            case Key.Right:
                AttemptBattleMove(Vector2I.Right);
                break;
            case Key.Up:
                AttemptBattleMove(Vector2I.Up);
                break;
            case Key.Down:
                AttemptBattleMove(Vector2I.Down);
                break;
            case Key.R:
                battleSelection.reset_battle_movement();
                break;
            case Key.Space:
                ResolveActiveBattle();
                break;
            default:
                return false;
        }
        return true;
    }

    public void StartBattle(EncounterAnchorData encounterAnchor)
    {
        if (!IsBattleReady() || encounterAnchor == null)
            return;
        _runtime.prepare_battle_start(encounterAnchor);
        StringName startState = _runtime.begin_battle_start(
            encounterAnchor,
            BuildBattleSeed(encounterAnchor),
            BuildBattleStartContext(encounterAnchor)
        );
        if (startState == "failed")
        {
            _runtime.handle_battle_start_failure();
            return;
        }
    }

    public Dictionary ResolveActiveBattle()
    {
        if (!IsBattleReady() || !IsBattleActive())
            return CommandError("当前没有进行中的战斗。");
        if (!IsBattleFinished())
        {
            var waitCommand = BuildWaitCommand();
            if (waitCommand == null)
            {
                UpdateStatus("当前尚未到可操作单位或战斗结果未结算。");
                return CommandError("当前尚未到可操作单位或战斗结果未结算。");
            }
            IssueBattleCommand(waitCommand);
            return CommandOk();
        }
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return RuntimeUnavailableError();
        var battleResolutionResult = GetBattleResolutionResult(battleRuntime);
        if (battleResolutionResult == null)
        {
            UpdateStatus("战斗已结束，但缺少正式结算结果。");
            return CommandError("战斗已结束，但缺少正式结算结果。");
        }
        bool finalized = _runtime.finalize_battle_resolution(battleResolutionResult);
        if (!finalized)
            return CommandError("战斗结算失败，已保留当前战斗状态以便重试。");
        ConsumeBattleResolutionResult(battleRuntime);
        return CommandOk();
    }

    public BattleResolutionResult GetBattleResolutionResult(BattleRuntimeModule battleRuntime)
    {
        if (battleRuntime == null)
            return null;
        return battleRuntime.get_battle_resolution_result();
    }

    public BattleResolutionResult ConsumeBattleResolutionResult(BattleRuntimeModule battleRuntime)
    {
        if (battleRuntime == null)
            return null;
        return battleRuntime.consume_battle_resolution_result();
    }

    public StringName AttemptBattleMove(Vector2I direction)
    {
        if (!IsBattleReady() || !IsBattleActive())
            return "full";
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateStatus(blockReason);
            return "overlay";
        }
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return "overlay";
        }
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "full";
        return battleSelection.attempt_battle_move_to(activeUnit.coord + direction);
    }

    public void OnBattleCellClicked(Vector2I coord)
    {
        if (!IsBattleReady() || !IsBattleActive())
            return;
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateStatus(blockReason);
            return;
        }
        var battleSelection = GetBattleSelection();
        if (battleSelection != null)
            battleSelection.attempt_battle_move_to(coord);
    }

    public void OnBattleCellRightClicked(Vector2I coord)
    {
        if (!IsBattleReady() || !IsBattleActive())
            return;
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateStatus(blockReason);
            return;
        }
        if (TryOpenCharacterInfoAtBattleCoord(coord))
            return;
        UpdateStatus("该战斗格没有可查看单位。");
    }

    public void OnBattleSkillSlotSelected(int index)
    {
        if (!IsBattleReady())
            return;
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateStatus(blockReason);
            return;
        }
        var battleSelection = GetBattleSelection();
        if (battleSelection != null)
            battleSelection.select_battle_skill_slot(index);
    }

    public void ApplyBattleBatch(BattleEventBatch batch)
    {
        if (batch == null)
            return;
        CapturePendingPromotionPrompt(batch.progression_deltas);
        RefreshBattleRuntimeState();
        _runtime?.record_command_battle_batch(batch);
        if (batch.log_lines.Count > 0)
            UpdateStatus(batch.log_lines[batch.log_lines.Count - 1]);
        var battleState = GetBattleState();
        if (
            GetPendingPromotionPrompt().Count > 0
            && battleState != null
            && battleState.modal_state == "promotion_choice"
        )
            SetActiveModalId("promotion");
        if (IsBattleFinished())
            ResolveActiveBattle();
    }

    public void RefreshBattleRuntimeState()
    {
        if (!IsBattleReady())
            return;
        var battleSelection = GetBattleSelection();
        if (battleSelection != null)
            battleSelection.sync_selected_battle_skill_state();
        var battleState = GetRuntimeBattleState();
        if (battleState == null || battleState.is_empty())
        {
            SetBattleState(null);
            SetBattleSelectedCoord(new Vector2I(-1, -1));
            return;
        }
        SetBattleState(battleState);
        if (
            _runtime.get_battle_selected_coord() == new Vector2I(-1, -1)
            || !battleState.cells.ContainsKey(_runtime.get_battle_selected_coord())
        )
            SetBattleSelectedCoord(GetDefaultBattleSelectedCoord());
    }

    public int BuildBattleSeed(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
            return 0;
        return (int)TrueRandomSeedService.GenerateSeed();
    }

    public BattleState GetRuntimeBattleState()
    {
        var battleRuntime = GetBattleRuntime();
        return battleRuntime?.get_state();
    }

    public bool IsBattleFinished()
    {
        var runtimeState = GetRuntimeBattleState();
        return runtimeState != null && runtimeState.phase == "battle_ended";
    }

    public BattleUnitState GetRuntimeActiveUnit()
    {
        var runtimeState = GetRuntimeBattleState();
        if (runtimeState == null || runtimeState.active_unit_id == "")
            return null;
        return DictionaryBattleUnitState(runtimeState.units, runtimeState.active_unit_id);
    }

    public BattleUnitState GetManualActiveUnit()
    {
        var runtimeState = GetRuntimeBattleState();
        var activeUnit = GetRuntimeActiveUnit();
        if (runtimeState == null || activeUnit == null)
            return null;
        if (runtimeState.phase != "unit_acting")
            return null;
        if (runtimeState.modal_state != "")
            return null;
        if (activeUnit.control_mode.ToString() != "manual")
            return null;
        return activeUnit;
    }

    public BattleUnitState GetRuntimeUnitAtCoord(Vector2I coord)
    {
        var runtimeState = GetRuntimeBattleState();
        var battleGridService = GetBattleGridService();
        if (runtimeState == null || battleGridService == null)
            return null;
        return battleGridService.get_unit_at_coord(runtimeState, coord);
    }

    public BattleCommand BuildWaitCommand()
    {
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
            return null;
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = activeUnit.unit_id,
        };
    }

    public StringName IssueBattleCommand(BattleCommand command)
    {
        if (command == null)
            return "overlay";
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return "overlay";
        BattleEventBatch batch = battleRuntime.issue_command(command);
        if (
            command.command_type == BattleCommand.TYPE_SKILL()
            && DidSkillCommandExecute(command, batch)
        )
            ClearBattleSelectionTargets();
        ApplyBattleBatch(batch);
        return "full";
    }

    public void CapturePendingPromotionPrompt(Godot.Collections.Array progressionDeltas)
    {
        foreach (var delta in progressionDeltas)
        {
            var deltaObj = delta.AsGodotObject() as CharacterProgressionDelta;
            if (deltaObj == null || !deltaObj.needs_promotion_modal)
                continue;
            SetPendingPromotionPrompt(BuildPromotionPrompt(deltaObj));
            if (GetPendingPromotionPrompt().Count > 0)
                return;
        }
    }

    public Dictionary BuildPromotionPrompt(
        CharacterProgressionDelta delta,
        string selectionHint = "确认后将在战斗中立即生效。"
    )
    {
        if (delta == null || delta.pending_profession_choices.Count == 0)
            return new Dictionary();
        PartyState partyState = _runtime?.get_party_state();
        var gameSession = GetGameSession();
        var memberId = delta.member_id;
        var memberState =
            partyState != null
                ? partyState.get_member_state(memberId)
                : null;
        var memberName =
            memberState != null ? memberState.display_name : memberId.ToString();
        var professionDefs =
            gameSession != null
                ? gameSession.get_profession_defs()
                : new Dictionary();
        var choiceEntries = new Godot.Collections.Array();
        foreach (var pendingChoice in delta.pending_profession_choices)
        {
            var choiceObj = pendingChoice.AsGodotObject() as PendingProfessionChoice;
            if (choiceObj == null)
                continue;
            foreach (StringName pid in choiceObj.candidate_profession_ids)
            {
                if (pid == "")
                    continue;
                if (!professionDefs.ContainsKey(pid))
                    continue;
                if (!choiceObj.target_rank_map.ContainsKey(pid))
                    continue;
                var targetRank = choiceObj.target_rank_map[pid].AsInt32();
                if (targetRank <= 0)
                    continue;
                var professionDef = professionDefs[pid].AsGodotObject() as ProfessionDef;
                if (professionDef == null)
                    continue;
                var grantedSkillIds = new Godot.Collections.Array();
                var grantedSkills = professionDef.get_granted_skills_for_rank(targetRank);
                foreach (ProfessionGrantedSkill skillObj in grantedSkills)
                {
                    if (skillObj != null && skillObj.skill_id != "")
                        grantedSkillIds.Add(skillObj.skill_id.ToString());
                }
                choiceEntries.Add(
                    new Dictionary
                    {
                        ["profession_id"] = pid.ToString(),
                        ["display_name"] = !string.IsNullOrEmpty(
                            professionDef.display_name
                        )
                            ? professionDef.display_name
                            : pid.ToString(),
                        ["summary"] = string.Format("Rank {0}", targetRank),
                        ["description"] = professionDef.description,
                        ["granted_skill_ids"] = grantedSkillIds,
                        ["selection_hint"] = selectionHint,
                        ["selection"] = new Dictionary(),
                    }
                );
            }
        }
        if (choiceEntries.Count == 0)
            return new Dictionary();
        return new Dictionary
        {
            ["member_id"] = memberId.ToString(),
            ["member_name"] = memberName,
            ["choices"] = choiceEntries,
        };
    }

    public Vector2I GetDefaultBattleSelectedCoord()
    {
        var activeUnit = GetBattleActiveUnit();
        if (activeUnit != null)
            return activeUnit.coord;
        var battleState = GetBattleState();
        if (battleState != null)
        {
            foreach (StringName allyUnitId in battleState.ally_unit_ids)
            {
                var unit = GetBattleUnitById(allyUnitId);
                if (unit != null)
                    return unit.coord;
            }
        }
        return Vector2I.Zero;
    }

    public BattleUnitState GetBattleUnitById(StringName unitId)
    {
        var battleState = GetBattleState();
        if (battleState == null || unitId == "")
            return null;
        return DictionaryBattleUnitState(battleState.units, unitId);
    }

    public BattleUnitState GetBattleUnitAtCoord(Vector2I coord)
    {
        var battleState = GetBattleState();
        var battleGridService = GetBattleGridService();
        if (battleState == null || battleGridService == null)
            return null;
        return battleGridService.get_unit_at_coord(battleState, coord);
    }

    public BattleUnitState GetBattleActiveUnit()
    {
        var battleState = GetBattleState();
        if (battleState == null)
            return null;
        return GetBattleUnitById(battleState.active_unit_id);
    }

    public string GetBattleUnitTypeLabel(string unitId)
    {
        var battleState = GetBattleState();
        if (battleState == null)
            return "战斗单位";
        foreach (StringName allyUnitId in battleState.ally_unit_ids)
        {
            if (allyUnitId.ToString() == unitId)
                return "己方单位";
        }
        foreach (StringName enemyUnitId in battleState.enemy_unit_ids)
        {
            if (enemyUnitId.ToString() == unitId)
                return "敌方单位";
        }
        return "战斗单位";
    }

    public Dictionary BuildBattleStartContext(EncounterAnchorData encounterAnchor)
    {
        var context = new Dictionary
        {
            ["world_coord"] =
                encounterAnchor != null
                    ? encounterAnchor.world_coord
                    : _runtime.get_player_coord(),
        };
        context["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor).ToString();
        context["validate_spawn_reachability"] = false;
        return context;
    }

    public StringName ResolveBattleTerrainProfile(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
            return "default";
        var regionTag = encounterAnchor.region_tag.ToString().StripEdges().ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        switch (regionTag)
        {
            case "canyon":
            case "north_wilds":
            case "south_wilds":
                return "canyon";
            case "narrow_assault":
                return "narrow_assault";
            case "holdout_push":
                return "holdout_push";
            default:
                return "default";
        }
    }

    private GameRuntimeBattleSelection GetBattleSelection()
    {
        return _runtime?.get_battle_selection();
    }

    private bool IsBattleReady()
    {
        return _runtime != null && GetBattleSelection() != null && GetBattleRuntime() != null;
    }

    private string GetRuntimeStatusText(string fallbackMessage)
    {
        if (_runtime != null)
        {
            string statusText = _runtime.get_status_text();
            if (!string.IsNullOrEmpty(statusText))
                return statusText;
        }
        return fallbackMessage;
    }

    private Dictionary CommandOk(string message = "", string battleRefreshMode = "")
    {
        if (_runtime != null)
            return _runtime.build_command_ok(message, battleRefreshMode);
        return new Dictionary
        {
            ["ok"] = true,
            ["message"] = message,
            ["battle_refresh_mode"] = battleRefreshMode,
        };
    }

    private Dictionary CommandError(string message)
    {
        if (_runtime != null)
            return _runtime.build_command_error(message);
        return new Dictionary { ["ok"] = false, ["message"] = message };
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private BattleRuntimeModule GetBattleRuntime()
    {
        return _runtime?.get_battle_runtime();
    }

    private BattleGridService GetBattleGridService()
    {
        return _runtime?.get_battle_grid_service();
    }

    private BattleState GetBattleState()
    {
        return _runtime?.get_battle_state();
    }

    private GameSession GetGameSession()
    {
        return _runtime?.get_game_session();
    }

    private Dictionary GetPendingPromotionPrompt()
    {
        return _runtime != null ? _runtime.get_pending_promotion_prompt() : new Dictionary();
    }

    private void SetPendingPromotionPrompt(Dictionary prompt)
    {
        if (_runtime != null)
            _runtime.set_pending_promotion_prompt(prompt);
    }

    private void SetBattleState(BattleState state)
    {
        if (_runtime != null)
            _runtime.set_runtime_battle_state(state);
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        if (_runtime != null)
            _runtime.set_runtime_battle_selected_coord(coord);
    }

    private void SetActiveModalId(string modalId)
    {
        if (_runtime != null)
            _runtime.set_runtime_active_modal_id(modalId);
    }

    private void ClearBattleSelectionTargets()
    {
        if (_runtime != null)
            _runtime.clear_battle_selection_targets();
    }

    private bool IsBattleActive()
    {
        return _runtime != null && _runtime.is_battle_active();
    }

    private string GetBattleInteractionBlockReason()
    {
        if (!IsBattleReady() || !IsBattleActive())
            return "";
        var battleState = GetBattleState();
        if (battleState != null && battleState.modal_state != "")
            return GetBattleModalBlockReason(battleState.modal_state);
        var activeModalId = GetActiveModalId();
        if (IsModalWindowOpen() && IsBattleOverlayModalId(activeModalId))
            return GetRuntimeModalBlockReason(activeModalId);
        return "";
    }

    private bool IsBattleInteractionBlocked()
    {
        return !string.IsNullOrEmpty(GetBattleInteractionBlockReason());
    }

    private static string GetBattleModalBlockReason(StringName modalState)
    {
        if (modalState == "start_confirm")
            return "战斗尚未开始，确认后才能操作。";
        if (modalState == "promotion_choice")
            return "当前处于晋升选择中，无法操作。";
        return "当前有待处理的战斗流程，暂时无法操作。";
    }

    private static string GetRuntimeModalBlockReason(string modalId)
    {
        if (modalId == "character_info")
            return "当前正在查看角色信息，无法操作战斗。";
        return "";
    }

    private static bool IsBattleOverlayModalId(string modalId)
    {
        return modalId == "character_info";
    }

    private string GetActiveModalId()
    {
        return _runtime?.get_active_modal_id() ?? "";
    }

    private bool IsModalWindowOpen()
    {
        return _runtime != null && _runtime.is_modal_window_open();
    }

    private bool BatchHasUpdates(BattleEventBatch batch)
    {
        return _runtime != null && _runtime.batch_has_updates(batch);
    }

    private bool DidSkillCommandExecute(BattleCommand command, BattleEventBatch batch)
    {
        if (command == null || batch == null)
            return false;
        return batch.changed_unit_ids.Contains(command.unit_id);
    }

    private void UpdateStatus(string message)
    {
        if (_runtime != null)
            _runtime.update_status(message);
    }

    private bool TryOpenCharacterInfoAtBattleCoord(Vector2I coord)
    {
        return _runtime != null && _runtime.try_open_character_info_at_battle_coord(coord);
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

    private static BattleUnitState DictionaryBattleUnitState(Dictionary dictionary, StringName key)
    {
        if (dictionary == null || key == null || !dictionary.ContainsKey(key))
            return null;
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as BattleUnitState : null;
    }

    private static GVector2IArray DuplicateVector2IArray(
        System.Collections.Generic.IEnumerable<Vector2I> values
    )
    {
        var result = new GVector2IArray();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray DuplicateStringNameArray(
        System.Collections.Generic.IEnumerable<StringName> values
    )
    {
        var result = new GStringNameArray();
        if (values == null)
            return result;
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
