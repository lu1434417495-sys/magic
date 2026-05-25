using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSessionFacade : RefCounted
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public new void Dispose()
    {
        _runtime = null;
    }

    public string GetSelectedBattleSkillName()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "";
        return battleSelection.Call("get_selected_battle_skill_name").AsString();
    }

    public string GetSelectedBattleSkillVariantName()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "";
        return battleSelection.Call("get_selected_battle_skill_variant_name").AsString();
    }

    public Godot.Collections.Array GetSelectedBattleSkillTargetCoords()
    {
        if (IsBattleInteractionBlocked())
            return new Godot.Collections.Array();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return new Godot.Collections.Array();
        return battleSelection.Call("get_selected_battle_skill_target_coords").AsGodotArray();
    }

    public Godot.Collections.Array GetSelectedBattleSkillTargetUnitIds()
    {
        if (IsBattleInteractionBlocked())
            return new Godot.Collections.Array();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return new Godot.Collections.Array();
        return battleSelection.Call("get_selected_battle_skill_target_unit_ids").AsGodotArray();
    }

    public Godot.Collections.Array GetSelectedBattleSkillValidTargetCoords()
    {
        if (IsBattleInteractionBlocked())
            return new Godot.Collections.Array();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return new Godot.Collections.Array();
        return battleSelection.Call("get_selected_battle_skill_valid_target_coords").AsGodotArray();
    }

    public int GetSelectedBattleSkillRequiredCoordCount()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return 0;
        return battleSelection.Call("get_selected_battle_skill_required_coord_count").AsInt32();
    }

    public Godot.Collections.Array GetBattleMovementReachableCoords()
    {
        var battleRuntime = GetBattleRuntime();
        if (!IsBattleReady() || !IsBattleActive() || battleRuntime == null)
            return new Godot.Collections.Array();
        if (IsBattleInteractionBlocked())
            return new Godot.Collections.Array();
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
            return new Godot.Collections.Array();
        return battleRuntime.Call("get_unit_reachable_move_coords", activeUnit).AsGodotArray();
    }

    public Godot.Collections.Array GetBattleOverlayTargetCoords()
    {
        if (!IsBattleReady())
            return new Godot.Collections.Array();
        if (IsBattleInteractionBlocked())
            return new Godot.Collections.Array();
        if (_runtime.Call("get_selected_battle_skill_id").AsStringName() != "")
            return GetSelectedBattleSkillValidTargetCoords();
        return GetBattleMovementReachableCoords();
    }

    public string GetBattleActiveUnitName()
    {
        var activeUnit = GetBattleActiveUnit();
        if (activeUnit == null)
            return "无";
        return !string.IsNullOrEmpty(activeUnit.display_name) ? activeUnit.display_name : activeUnit.unit_id.ToString();
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
        foreach (var cellVariant in battleState.Get("cells").AsGodotDictionary().Values)
        {
            var cellState = cellVariant.As<BattleCellState>();
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
            if (runtimeState != null && runtimeState.Get("modal_state").AsString() != "")
                break;
            var batch = battleRuntime.Call("advance", 1).AsGodotObject();
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
        var selectResult = battleSelection.Call("select_battle_skill_slot", slotIndex).AsGodotDictionary();
        if (!DictionaryGet(selectResult, "ok", false).AsBool())
            return CommandError(DictionaryGet(selectResult, "message", "").AsString());
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
        battleSelection.Call("cycle_selected_battle_skill_variant", step);
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
        battleSelection.Call("clear_battle_skill_selection", true);
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
        var battleRefreshMode = battleSelection.Call("attempt_battle_move_to", targetCoord).AsStringName();
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
        if (!DictionaryGet(resolveResult, "ok", false).AsBool())
            return CommandError(DictionaryGet(resolveResult, "message", "战斗结算失败。").AsString());
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
        return CommandOk("", battleSelection.Call("reset_battle_movement").AsString());
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
                battleSelection.Call("select_battle_skill_slot", (int)keyEvent.Keycode - (int)Key.Key1);
                break;
            case Key.Q:
                battleSelection.Call("cycle_selected_battle_skill_variant", -1);
                break;
            case Key.E:
                battleSelection.Call("cycle_selected_battle_skill_variant", 1);
                break;
            case Key.Escape:
                battleSelection.Call("clear_battle_skill_selection", true);
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
                battleSelection.Call("reset_battle_movement");
                break;
            case Key.Space:
                ResolveActiveBattle();
                break;
            default:
                return false;
        }
        return true;
    }

    public void StartBattle(Variant encounterAnchor)
    {
        if (!IsBattleReady() || encounterAnchor.VariantType == Variant.Type.Nil)
            return;
        var anchor = encounterAnchor.As<EncounterAnchorData>();
        _runtime.Call("prepare_battle_start", encounterAnchor);
        var startState = _runtime.Call("begin_battle_start", encounterAnchor, BuildBattleSeed(anchor), BuildBattleStartContext(anchor)).AsStringName();
        if (startState == "failed")
        {
            _runtime.Call("handle_battle_start_failure");
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
        var finalized = _runtime.Call("finalize_battle_resolution", battleResolutionResult).AsBool();
        if (!finalized)
            return CommandError("战斗结算失败，已保留当前战斗状态以便重试。");
        ConsumeBattleResolutionResult(battleRuntime);
        return CommandOk();
    }

    public GodotObject GetBattleResolutionResult(GodotObject battleRuntime)
    {
        if (battleRuntime == null || !battleRuntime.HasMethod("get_battle_resolution_result"))
            return null;
        return battleRuntime.Call("get_battle_resolution_result").AsGodotObject();
    }

    public GodotObject ConsumeBattleResolutionResult(GodotObject battleRuntime)
    {
        if (battleRuntime == null || !battleRuntime.HasMethod("consume_battle_resolution_result"))
            return null;
        return battleRuntime.Call("consume_battle_resolution_result").AsGodotObject();
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
        return battleSelection.Call("attempt_battle_move_to", activeUnit.coord + direction).AsStringName();
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
            battleSelection.Call("attempt_battle_move_to", coord);
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
            battleSelection.Call("select_battle_skill_slot", index);
    }

    public void ApplyBattleBatch(GodotObject batch)
    {
        if (batch == null)
            return;
        var progressionDeltas = batch.Get("progression_deltas").AsGodotArray();
        CapturePendingPromotionPrompt(progressionDeltas);
        RefreshBattleRuntimeState();
        if (_runtime != null && _runtime.HasMethod("record_command_battle_batch"))
            _runtime.Call("record_command_battle_batch", batch);
        var logLines = batch.Get("log_lines").AsGodotArray();
        if (logLines.Count > 0)
            UpdateStatus(logLines[logLines.Count - 1].AsString());
        var battleState = GetBattleState();
        if (GetPendingPromotionPrompt().Count > 0 && battleState != null && battleState.Get("modal_state").AsString() == "promotion_choice")
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
            battleSelection.Call("sync_selected_battle_skill_state");
        var battleState = GetRuntimeBattleState();
        if (battleState == null || (battleState.HasMethod("is_empty") && battleState.Call("is_empty").AsBool()))
        {
            SetBattleState(null);
            SetBattleSelectedCoord(new Vector2I(-1, -1));
            return;
        }
        SetBattleState(battleState);
        if (_runtime.Call("get_battle_selected_coord").AsVector2I() == new Vector2I(-1, -1) || !battleState.Get("cells").AsGodotDictionary().ContainsKey(_runtime.Call("get_battle_selected_coord").AsVector2I()))
            SetBattleSelectedCoord(GetDefaultBattleSelectedCoord());
    }

    public int BuildBattleSeed(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
            return 0;
        return TrueRandomSeedService.GenerateSeed();
    }

    public BattleState GetRuntimeBattleState()
    {
        var battleRuntime = GetBattleRuntime();
        return battleRuntime != null ? battleRuntime.Call("get_state").As<BattleState>() : null;
    }

    public bool IsBattleFinished()
    {
        var runtimeState = GetRuntimeBattleState();
        return runtimeState != null && runtimeState.Get("phase").AsString() == "battle_ended";
    }

    public BattleUnitState GetRuntimeActiveUnit()
    {
        var runtimeState = GetRuntimeBattleState();
        if (runtimeState == null || runtimeState.Get("active_unit_id").AsStringName() == "")
            return null;
        return DictionaryGet(runtimeState.Get("units").AsGodotDictionary(), runtimeState.Get("active_unit_id").AsStringName(), default(Variant)).As<BattleUnitState>();
    }

    public BattleUnitState GetManualActiveUnit()
    {
        var runtimeState = GetRuntimeBattleState();
        var activeUnit = GetRuntimeActiveUnit();
        if (runtimeState == null || activeUnit == null)
            return null;
        if (runtimeState.Get("phase").AsString() != "unit_acting")
            return null;
        if (runtimeState.Get("modal_state").AsString() != "")
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
        return battleGridService.Call("get_unit_at_coord", runtimeState, coord).As<BattleUnitState>();
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
        var batch = battleRuntime.Call("issue_command", command).AsGodotObject();
        if (command.command_type == BattleCommand.TYPE_SKILL() && DidSkillCommandExecute(command, batch))
            ClearBattleSelectionTargets();
        ApplyBattleBatch(batch);
        return "full";
    }

    public void CapturePendingPromotionPrompt(Godot.Collections.Array progressionDeltas)
    {
        foreach (var delta in progressionDeltas)
        {
            var deltaObj = delta.AsGodotObject();
            if (deltaObj == null || !deltaObj.Get("needs_promotion_modal").AsBool())
                continue;
            SetPendingPromotionPrompt(BuildPromotionPrompt(deltaObj));
            if (GetPendingPromotionPrompt().Count > 0)
                return;
        }
    }

    public Dictionary BuildPromotionPrompt(GodotObject delta, string selectionHint = "确认后将在战斗中立即生效。")
    {
        if (delta == null || delta.Get("pending_profession_choices").AsGodotArray().Count == 0)
            return new Dictionary();
        var partyState = _runtime != null ? _runtime.Call("get_party_state").AsGodotObject() : null;
        var gameSession = GetGameSession();
        var memberId = delta.Get("member_id").AsStringName();
        var memberState = partyState != null ? partyState.Call("get_member_state", memberId).AsGodotObject() : null;
        var memberName = memberState != null ? memberState.Get("display_name").AsString() : memberId.ToString();
        var professionDefs = gameSession != null ? gameSession.Call("get_profession_defs").AsGodotDictionary() : new Dictionary();
        var choiceEntries = new Godot.Collections.Array();
        foreach (var pendingChoice in delta.Get("pending_profession_choices").AsGodotArray())
        {
            var choiceObj = pendingChoice.AsGodotObject();
            if (choiceObj == null)
                continue;
            foreach (var professionId in choiceObj.Get("candidate_profession_ids").AsGodotArray())
            {
                var pid = professionId.AsStringName();
                if (pid == "")
                    continue;
                if (!professionDefs.ContainsKey(pid))
                    continue;
                var targetRankMap = choiceObj.Get("target_rank_map").AsGodotDictionary();
                if (!targetRankMap.ContainsKey(pid))
                    continue;
                var targetRank = targetRankMap[pid].AsInt32();
                if (targetRank <= 0)
                    continue;
                var professionDef = professionDefs[pid].AsGodotObject();
                if (professionDef == null)
                    continue;
                var grantedSkillIds = new Godot.Collections.Array();
                var grantedSkills = professionDef.Call("get_granted_skills_for_rank", targetRank).AsGodotArray();
                foreach (var grantedSkill in grantedSkills)
                {
                    var skillObj = grantedSkill.AsGodotObject();
                    if (skillObj != null && skillObj.Get("skill_id").AsStringName() != "")
                        grantedSkillIds.Add(skillObj.Get("skill_id").AsStringName().ToString());
                }
                choiceEntries.Add(new Dictionary
                {
                    ["profession_id"] = pid.ToString(),
                    ["display_name"] = !string.IsNullOrEmpty(professionDef.Get("display_name").AsString()) ? professionDef.Get("display_name").AsString() : pid.ToString(),
                    ["summary"] = string.Format("Rank {0}", targetRank),
                    ["description"] = professionDef.Get("description").AsString(),
                    ["granted_skill_ids"] = grantedSkillIds,
                    ["selection_hint"] = selectionHint,
                    ["selection"] = new Dictionary(),
                });
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
            foreach (var allyUnitId in battleState.Get("ally_unit_ids").AsGodotArray())
            {
                var unit = GetBattleUnitById(allyUnitId.AsStringName());
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
        return DictionaryGet(battleState.Get("units").AsGodotDictionary(), unitId, default(Variant)).As<BattleUnitState>();
    }

    public BattleUnitState GetBattleUnitAtCoord(Vector2I coord)
    {
        var battleState = GetBattleState();
        var battleGridService = GetBattleGridService();
        if (battleState == null || battleGridService == null)
            return null;
        return battleGridService.Call("get_unit_at_coord", battleState, coord).As<BattleUnitState>();
    }

    public BattleUnitState GetBattleActiveUnit()
    {
        var battleState = GetBattleState();
        if (battleState == null)
            return null;
        return GetBattleUnitById(battleState.Get("active_unit_id").AsStringName());
    }

    public string GetBattleUnitTypeLabel(string unitId)
    {
        var battleState = GetBattleState();
        if (battleState == null)
            return "战斗单位";
        foreach (var allyUnitId in battleState.Get("ally_unit_ids").AsGodotArray())
        {
            if (allyUnitId.AsString() == unitId)
                return "己方单位";
        }
        foreach (var enemyUnitId in battleState.Get("enemy_unit_ids").AsGodotArray())
        {
            if (enemyUnitId.AsString() == unitId)
                return "敌方单位";
        }
        return "战斗单位";
    }

    public Dictionary BuildBattleStartContext(EncounterAnchorData encounterAnchor)
    {
        var context = new Dictionary
        {
            ["world_coord"] = encounterAnchor != null ? encounterAnchor.world_coord : _runtime.Call("get_player_coord").AsVector2I(),
        };
        context["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor).ToString();
        return context;
    }

    public StringName ResolveBattleTerrainProfile(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
            return "default";
        var regionTag = encounterAnchor.region_tag.ToString().StripEdges().ToLowerInvariant();
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

    private GodotObject GetBattleSelection()
    {
        return _runtime != null ? _runtime.Call("get_battle_selection").AsGodotObject() : null;
    }

    private bool IsBattleReady()
    {
        return _runtime != null && GetBattleSelection() != null && GetBattleRuntime() != null;
    }

    private string GetRuntimeStatusText(string fallbackMessage)
    {
        if (_runtime != null)
        {
            var statusText = _runtime.Call("get_status_text").AsString();
            if (!string.IsNullOrEmpty(statusText))
                return statusText;
        }
        return fallbackMessage;
    }

    private Dictionary CommandOk(string message = "", string battleRefreshMode = "")
    {
        if (_runtime != null)
            return _runtime.Call("build_command_ok", message, battleRefreshMode).AsGodotDictionary();
        return new Dictionary { ["ok"] = true, ["message"] = message, ["battle_refresh_mode"] = battleRefreshMode };
    }

    private Dictionary CommandError(string message)
    {
        if (_runtime != null)
            return _runtime.Call("build_command_error", message).AsGodotDictionary();
        return new Dictionary { ["ok"] = false, ["message"] = message };
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private GodotObject GetBattleRuntime()
    {
        return _runtime != null ? _runtime.Call("get_battle_runtime").AsGodotObject() : null;
    }

    private GodotObject GetBattleGridService()
    {
        return _runtime != null ? _runtime.Call("get_battle_grid_service").AsGodotObject() : null;
    }

    private BattleState GetBattleState()
    {
        return _runtime != null ? _runtime.Call("get_battle_state").As<BattleState>() : null;
    }

    private GodotObject GetGameSession()
    {
        return _runtime != null ? _runtime.Call("get_game_session").AsGodotObject() : null;
    }

    private Dictionary GetPendingPromotionPrompt()
    {
        return _runtime != null ? _runtime.Call("get_pending_promotion_prompt").AsGodotDictionary() : new Dictionary();
    }

    private void SetPendingPromotionPrompt(Dictionary prompt)
    {
        if (_runtime != null)
            _runtime.Call("set_pending_promotion_prompt", prompt);
    }

    private void SetBattleState(BattleState state)
    {
        if (_runtime != null)
            _runtime.Call("set_runtime_battle_state", state);
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        if (_runtime != null)
            _runtime.Call("set_runtime_battle_selected_coord", coord);
    }

    private void SetActiveModalId(string modalId)
    {
        if (_runtime != null)
            _runtime.Call("set_runtime_active_modal_id", modalId);
    }

    private void ClearBattleSelectionTargets()
    {
        if (_runtime != null)
            _runtime.Call("clear_battle_selection_targets");
    }

    private bool IsBattleActive()
    {
        return _runtime != null && _runtime.Call("is_battle_active").AsBool();
    }

    private string GetBattleInteractionBlockReason()
    {
        if (!IsBattleReady() || !IsBattleActive())
            return "";
        var battleState = GetBattleState();
        if (battleState != null && battleState.Get("modal_state").AsStringName() != "")
            return GetBattleModalBlockReason(battleState.Get("modal_state").AsStringName());
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
        return _runtime != null ? _runtime.Call("get_active_modal_id").AsString() : "";
    }

    private bool IsModalWindowOpen()
    {
        return _runtime != null && _runtime.Call("is_modal_window_open").AsBool();
    }

    private bool BatchHasUpdates(GodotObject batch)
    {
        return _runtime != null && _runtime.Call("batch_has_updates", batch).AsBool();
    }

    private bool DidSkillCommandExecute(BattleCommand command, GodotObject batch)
    {
        if (command == null || batch == null)
            return false;
        return batch.Get("changed_unit_ids").AsGodotArray().Contains(command.unit_id);
    }

    private void UpdateStatus(string message)
    {
        if (_runtime != null)
            _runtime.Call("update_status", message);
    }

    private bool TryOpenCharacterInfoAtBattleCoord(Vector2I coord)
    {
        return _runtime != null && _runtime.Call("try_open_character_info_at_battle_coord", coord).AsBool();
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }
}
