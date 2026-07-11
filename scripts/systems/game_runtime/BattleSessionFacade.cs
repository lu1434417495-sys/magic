using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class BattleSessionFacade : IDisposable
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

    public void Dispose()
    {
        _runtime = null;
    }

    public string GetSelectedBattleSkillName()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "";
        return battleSelection.GetSelectedBattleSkillName();
    }

    public string GetSelectedBattleSkillVariantName()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return "";
        return battleSelection.GetSelectedBattleSkillVariantName();
    }

    public IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoords()
    {
        if (IsBattleInteractionBlocked())
            return EmptyVector2IArray();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return EmptyVector2IArray();
        return DuplicateVector2IArray(
            battleSelection.GetSelectedBattleSkillTargetCoordsSnapshotPlain()
        );
    }

    public IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIds()
    {
        if (IsBattleInteractionBlocked())
            return EmptyStringNameArray();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return EmptyStringNameArray();
        return DuplicateStringNameArray(
            battleSelection.GetSelectedBattleSkillTargetUnitIdsSnapshotPlain()
        );
    }

    internal IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoordsSnapshotPlain()
    {
        if (IsBattleInteractionBlocked())
            return System.Array.Empty<Vector2I>();
        GameRuntimeBattleSelection battleSelection = GetBattleSelection();
        return battleSelection?.GetSelectedBattleSkillTargetCoordsSnapshotPlain()
            ?? System.Array.Empty<Vector2I>();
    }

    internal IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain()
    {
        if (IsBattleInteractionBlocked())
            return System.Array.Empty<StringName>();
        GameRuntimeBattleSelection battleSelection = GetBattleSelection();
        return battleSelection?.GetSelectedBattleSkillTargetUnitIdsSnapshotPlain()
            ?? System.Array.Empty<StringName>();
    }

    public IReadOnlyList<Vector2I> GetSelectedBattleSkillValidTargetCoords()
    {
        if (IsBattleInteractionBlocked())
            return EmptyVector2IArray();
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return EmptyVector2IArray();
        return DuplicateVector2IArray(
            battleSelection.GetSelectedBattleSkillValidTargetCoordsSnapshotPlain()
        );
    }

    public int GetSelectedBattleSkillRequiredCoordCount()
    {
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return 0;
        return battleSelection.GetSelectedBattleSkillRequiredCoordCount();
    }

    public BattlePreview GetSelectedBattleSkillPreview()
    {
        if (IsBattleInteractionBlocked())
            return null;
        var battleSelection = GetBattleSelection();
        return battleSelection?.GetSelectedBattleSkillPreview();
    }

    public BattlePreview PreviewSelectedBattleSkillAtCoord(Vector2I coord)
    {
        if (IsBattleInteractionBlocked())
            return null;
        var battleSelection = GetBattleSelection();
        return battleSelection?.PreviewSelectedBattleSkillAtCoord(coord);
    }

    public IReadOnlyList<Vector2I> GetBattleMovementReachableCoords()
    {
        var battleRuntime = GetBattleRuntime();
        if (!IsBattleReady() || !IsBattleActive() || battleRuntime == null)
            return EmptyVector2IArray();
        if (IsBattleInteractionBlocked())
            return EmptyVector2IArray();
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
            return EmptyVector2IArray();
        return DuplicateVector2IArray(battleRuntime.GetUnitReachableMoveCoordsTyped(activeUnit));
    }

    public IReadOnlyList<Vector2I> GetBattleOverlayTargetCoords()
    {
        if (!IsBattleReady())
            return EmptyVector2IArray();
        if (IsBattleInteractionBlocked())
            return EmptyVector2IArray();
        if (_runtime.GetSelectedBattleSkillId() != "")
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

    internal Dictionary GetBattleTerrainCounts()
    {
        var result = new Dictionary();
        foreach ((string terrainId, int count) in GetBattleTerrainCountsSnapshotTyped())
            result[terrainId] = count;
        return result;
    }

    internal IReadOnlyDictionary<string, int> GetBattleTerrainCountsSnapshotTyped()
    {
        var counts = new System.Collections.Generic.Dictionary<string, int>(
            StringComparer.Ordinal
        )
        {
            [BattleTerrainRules.ToStringName(BattleTerrainKind.Land).ToString()] = 0,
            [BattleTerrainRules.ToStringName(BattleTerrainKind.Forest).ToString()] = 0,
            [BattleTerrainRules.ToStringName(BattleTerrainKind.ShallowWater).ToString()] = 0,
            [BattleTerrainRules.ToStringName(BattleTerrainKind.FlowingWater).ToString()] = 0,
            [BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater).ToString()] = 0,
            [BattleTerrainRules.ToStringName(BattleTerrainKind.Mud).ToString()] = 0,
            [BattleTerrainRules.ToStringName(BattleTerrainKind.Spike).ToString()] = 0,
        };
        var battleState = GetBattleState();
        if (!IsBattleReady() || battleState == null)
            return counts;
        foreach (BattleCellState cellState in battleState.Cells())
        {
            if (cellState == null)
                continue;
            var terrainId = cellState.base_terrain.ToString();
            if (!counts.ContainsKey(terrainId))
                counts[terrainId] = 0;
            counts[terrainId] += 1;
        }
        return counts;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleTickTyped(int tickCount)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        if (tickCount <= 0)
            return CommandErrorTyped("推进 tick 必须大于 0。");
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return RuntimeUnavailableTypedResult();
        for (int i = 0; i < Mathf.Max(tickCount, 0); i++)
        {
            if (!IsBattleActive())
                break;
            var runtimeState = GetRuntimeBattleState();
            if (runtimeState != null && runtimeState.ModalStateKind != BattleModalStateKind.None)
                break;
            BattleEventBatch batch = battleRuntime.advance(1);
            if (BatchHasUpdates(batch))
                ApplyBattleBatch(batch);
        }
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleSelectSkillTyped(int slotIndex)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableTypedResult();
        var selectResult = battleSelection.SelectBattleSkillSlotTyped(slotIndex);
        if (!selectResult.Ok)
        {
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                selectResult.Message ?? "",
                selectResult.Code
            );
        }
        return CommandOkTyped("", BattleRefreshMode.Overlay);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleCycleVariantTyped(int step)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableTypedResult();
        battleSelection.CycleSelectedBattleSkillOption(step);
        return CommandOkTyped("", BattleRefreshMode.Overlay);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleClearSkillTyped()
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableTypedResult();
        battleSelection.ClearBattleSkillSelection(true);
        return CommandOkTyped("", BattleRefreshMode.Overlay);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleMoveToTyped(Vector2I targetCoord)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableTypedResult();
        var battleRefreshMode = battleSelection.AttemptBattleMoveTo(targetCoord);
        if (battleRefreshMode == BattleRefreshMode.Error)
            return CommandErrorTyped(GetRuntimeStatusText("当前技能无法施放。"));
        return CommandOkTyped("", battleRefreshMode);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleMoveDirectionTyped(Vector2I direction)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        if (direction == Vector2I.Zero)
            return CommandErrorTyped("战斗移动方向不能为空。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleRefreshMode = AttemptBattleMove(direction);
        if (battleRefreshMode == BattleRefreshMode.Error)
            return CommandErrorTyped(GetRuntimeStatusText("当前技能无法施放。"));
        return CommandOkTyped("", battleRefreshMode);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleWaitOrResolveTyped()
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        return ResolveActiveBattleTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleCancelCastTyped(StringName unitId)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return RuntimeUnavailableTypedResult();
        BattleUnitState unitState = ResolveCancelCastUnit(unitId);
        if (unitState == null)
            return CommandErrorTyped("未找到可取消读条的单位。");
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.CancelCast,
            unit_id = unitState.unit_id,
        };
        BattlePreview preview = battleRuntime.PreviewCommand(command);
        if (preview == null || !preview.allowed)
            return CommandErrorTyped(FirstPreviewLogLine(preview, "当前没有可取消的读条。"));
        BattleEventBatch batch = battleRuntime.IssueCommand(command);
        ApplyBattleBatch(batch);
        return CommandOkTyped("", BattleRefreshMode.Full);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandBattleInspectTyped(Vector2I coord)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        if (TryOpenCharacterInfoAtBattleCoord(coord))
            return CommandOkTyped();
        return CommandErrorTyped("该战斗格没有可查看单位。");
    }

    private BattleUnitState ResolveCancelCastUnit(StringName unitId)
    {
        BattleState battleState = GetRuntimeBattleState();
        if (battleState == null)
            return null;
        StringName normalizedUnitId = ProgressionDataUtils.to_string_name(unitId);
        if (normalizedUnitId != "")
        {
            battleState.TryGetUnitTyped(normalizedUnitId, out BattleUnitState explicitUnit);
            return explicitUnit;
        }
        BattleUnitState onlyCandidate = null;
        foreach (BattleUnitState unitState in battleState.GetUnitsTyped())
        {
            if (
                unitState == null
                || !unitState.HasPendingCast()
                || unitState.ControlModeKind != BattleUnitControlMode.Manual
                || !battleState.ally_unit_ids.Contains(unitState.unit_id)
            )
            {
                continue;
            }
            if (onlyCandidate != null)
                return null;
            onlyCandidate = unitState;
        }
        return onlyCandidate;
    }

    private static string FirstPreviewLogLine(BattlePreview preview, string fallback)
    {
        if (preview != null)
        {
            foreach (string line in preview.LogLinesTyped)
            {
                if (!string.IsNullOrEmpty(line))
                    return line;
            }
        }
        return fallback ?? "";
    }

    internal GameRuntimeFacade.RuntimeCommandResult ResetBattleFocusTyped()
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return RuntimeUnavailableTypedResult();
        return CommandOkTyped("", battleSelection.ResetBattleMovement());
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
                battleSelection.SelectBattleSkillSlotTyped((int)keyEvent.Keycode - (int)Key.Key1);
                break;
            case Key.Q:
                battleSelection.CycleSelectedBattleSkillOption(-1);
                break;
            case Key.E:
                battleSelection.CycleSelectedBattleSkillOption(1);
                break;
            case Key.Escape:
                battleSelection.ClearBattleSkillSelection(true);
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
                battleSelection.ResetBattleMovement();
                break;
            case Key.Space:
                ResolveActiveBattleTyped();
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
        _runtime.PrepareBattleStart(encounterAnchor);
        StringName startState = _runtime.BeginBattleStart(
            encounterAnchor,
            BuildBattleSeed(encounterAnchor),
            BuildBattleStartContext(encounterAnchor)
        );
        if (startState == "failed")
        {
            _runtime.HandleBattleStartFailure();
            return;
        }
    }

    internal GameRuntimeFacade.RuntimeCommandResult ResolveActiveBattleTyped()
    {
        if (!IsBattleReady() || !IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        if (!IsBattleFinished())
        {
            var waitCommand = BuildWaitCommand();
            if (waitCommand == null)
            {
                UpdateStatus("当前尚未到可操作单位或战斗结果未结算。");
                return CommandErrorTyped("当前尚未到可操作单位或战斗结果未结算。");
            }
            IssueBattleCommand(waitCommand);
            return CommandOkTyped();
        }
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return RuntimeUnavailableTypedResult();
        var battleResolutionResult = GetBattleResolutionResult(battleRuntime);
        if (battleResolutionResult == null)
        {
            UpdateStatus("战斗已结束，但缺少正式结算结果。");
            return CommandErrorTyped("战斗已结束，但缺少正式结算结果。");
        }
        bool finalized = _runtime.FinalizeBattleResolution(battleResolutionResult);
        if (!finalized)
            return CommandErrorTyped("战斗结算失败，已保留当前战斗状态以便重试。");
        ConsumeBattleResolutionResult(battleRuntime);
        return CommandOkTyped();
    }

    internal BattleResolutionResult GetBattleResolutionResult(BattleRuntimeModule battleRuntime)
    {
        if (battleRuntime == null)
            return null;
        return battleRuntime.GetBattleResolutionResult();
    }

    internal BattleResolutionResult ConsumeBattleResolutionResult(BattleRuntimeModule battleRuntime)
    {
        if (battleRuntime == null)
            return null;
        return battleRuntime.ConsumeBattleResolutionResult();
    }

    internal BattleRefreshMode AttemptBattleMove(Vector2I direction)
    {
        if (!IsBattleReady() || !IsBattleActive())
            return BattleRefreshMode.Full;
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateStatus(blockReason);
            return BattleRefreshMode.Overlay;
        }
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return BattleRefreshMode.Overlay;
        }
        var battleSelection = GetBattleSelection();
        if (battleSelection == null)
            return BattleRefreshMode.Full;
        return battleSelection.AttemptBattleMoveTo(activeUnit.coord + direction);
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
            battleSelection.AttemptBattleMoveTo(coord);
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
            battleSelection.SelectBattleSkillSlotTyped(index);
    }

    public void ApplyBattleBatch(BattleEventBatch batch)
    {
        if (batch == null)
            return;
        CapturePendingPromotionPrompt(batch.ProgressionDeltasTyped);
        RefreshBattleRuntimeState();
        _runtime?.RecordCommandBattleBatch(batch);
        if (batch.LogLinesTyped.Count > 0)
            UpdateStatus(batch.LogLinesTyped[batch.LogLinesTyped.Count - 1]);
        var battleState = GetBattleState();
        if (
            HasPendingPromotionPrompt()
            && battleState != null
            && battleState.ModalStateKind == BattleModalStateKind.PromotionChoice
        )
            SetActiveModalKind(RuntimeModalKind.Promotion);
        if (IsBattleFinished())
            ResolveActiveBattleTyped();
    }

    public void RefreshBattleRuntimeState()
    {
        if (!IsBattleReady())
            return;
        var battleSelection = GetBattleSelection();
        if (battleSelection != null)
            battleSelection.SyncSelectedBattleSkillState();
        var battleState = GetRuntimeBattleState();
        if (battleState == null || battleState.IsEmpty())
        {
            SetBattleState(null);
            SetBattleSelectedCoord(new Vector2I(-1, -1));
            return;
        }
        SetBattleState(battleState);
        if (
            _runtime.GetBattleSelectedCoord() == new Vector2I(-1, -1)
            || !battleState.ContainsCell(_runtime.GetBattleSelectedCoord())
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
        return battleRuntime?.GetState();
    }

    public bool IsBattleFinished()
    {
        var runtimeState = GetRuntimeBattleState();
        return runtimeState != null && runtimeState.PhaseKind == BattlePhaseKind.BattleEnded;
    }

    public BattleUnitState GetRuntimeActiveUnit()
    {
        var runtimeState = GetRuntimeBattleState();
        if (runtimeState == null || runtimeState.active_unit_id == "")
            return null;
        return runtimeState.GetUnit(runtimeState.active_unit_id);
    }

    public BattleUnitState GetManualActiveUnit()
    {
        var runtimeState = GetRuntimeBattleState();
        var activeUnit = GetRuntimeActiveUnit();
        if (runtimeState == null || activeUnit == null)
            return null;
        if (runtimeState.PhaseKind != BattlePhaseKind.UnitActing)
            return null;
        if (runtimeState.ModalStateKind != BattleModalStateKind.None)
            return null;
        if (activeUnit.ControlModeKind != BattleUnitControlMode.Manual)
            return null;
        return activeUnit;
    }

    public BattleUnitState GetRuntimeUnitAtCoord(Vector2I coord)
    {
        var runtimeState = GetRuntimeBattleState();
        var battleGridService = GetBattleGridService();
        if (runtimeState == null || battleGridService == null)
            return null;
        return battleGridService.GetUnitAtCoord(runtimeState, coord);
    }

    public BattleCommand BuildWaitCommand()
    {
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
            return null;
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Wait,
            unit_id = activeUnit.unit_id,
        };
    }

    internal BattleRefreshMode IssueBattleCommand(BattleCommand command)
    {
        if (command == null)
            return BattleRefreshMode.Overlay;
        var battleRuntime = GetBattleRuntime();
        if (battleRuntime == null)
            return BattleRefreshMode.Overlay;
        BattleEventBatch batch = battleRuntime.IssueCommand(command);
        if (
            command.CommandKind == BattleCommandKind.Skill
            && DidSkillCommandExecute(command, batch)
        )
            ClearBattleSelectionTargets();
        ApplyBattleBatch(batch);
        return BattleRefreshMode.Full;
    }

    internal void CapturePendingPromotionPrompt(Godot.Collections.Array progressionDeltas) =>
        CapturePendingPromotionPrompt(ReadProgressionDeltas(progressionDeltas));

    internal void CapturePendingPromotionPrompt(
        IReadOnlyList<CharacterProgressionDelta> progressionDeltas
    )
    {
        if (progressionDeltas == null)
            return;
        foreach (CharacterProgressionDelta delta in progressionDeltas)
        {
            if (delta == null || !delta.needs_promotion_modal)
                continue;
            SetPendingPromotionPrompt(BuildPromotionPrompt(delta));
            if (HasPendingPromotionPrompt())
                return;
        }
    }

    internal Dictionary BuildPromotionPrompt(
        CharacterProgressionDelta delta,
        string selectionHint = "确认后将在战斗中立即生效。"
    )
    {
        if (delta == null || delta.PendingProfessionChoicesTyped.Count == 0)
            return new Dictionary();
        PartyState partyState = _runtime?.GetPartyState();
        GameContentCatalog contentCatalog = _runtime?.GetContentCatalogTyped();
        var memberId = delta.member_id;
        var memberState =
            partyState != null
                ? partyState.GetMemberState(memberId)
                : null;
        var memberName =
            memberState != null ? memberState.display_name : memberId.ToString();
        IReadOnlyDictionary<StringName, ProfessionDefinition> professionDefs =
            contentCatalog != null
                ? contentCatalog.GetProfessionDefsTyped()
                : new System.Collections.Generic.Dictionary<StringName, ProfessionDefinition>();
        var choiceEntries = new Godot.Collections.Array();
        foreach (PendingProfessionChoice choiceObj in delta.PendingProfessionChoicesTyped)
        {
            if (choiceObj == null)
                continue;
            foreach (StringName pid in choiceObj.CandidateProfessionIdsTyped)
            {
                if (pid == "")
                    continue;
                if (!professionDefs.ContainsKey(pid))
                    continue;
                if (!choiceObj.TryGetTargetRank(pid, out int targetRank))
                    continue;
                if (targetRank <= 0)
                    continue;
                if (!professionDefs.TryGetValue(pid, out ProfessionDefinition professionDef))
                    continue;
                var grantedSkillIds = new Godot.Collections.Array();
                var grantedSkills = professionDef.GetGrantedSkillsForRank(targetRank);
                foreach (ProfessionGrantedSkillDefinition skillObj in grantedSkills)
                {
                    if (skillObj != null && skillObj.SkillId != "")
                        grantedSkillIds.Add(skillObj.SkillId.ToString());
                }
                choiceEntries.Add(
                    new Dictionary
                    {
                        ["profession_id"] = pid.ToString(),
                        ["display_name"] = !string.IsNullOrEmpty(
                            professionDef.DisplayName
                        )
                            ? professionDef.DisplayName
                            : pid.ToString(),
                        ["summary"] = string.Format("Rank {0}", targetRank),
                        ["description"] = professionDef.Description,
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

    private static IReadOnlyList<CharacterProgressionDelta> ReadProgressionDeltas(
        Godot.Collections.Array values
    )
    {
        var result = new List<CharacterProgressionDelta>();
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                continue;
            CharacterProgressionDelta delta = CharacterProgressionDelta.FromDictionary(
                value.AsGodotDictionary()
            );
            if (delta != null)
                result.Add(delta);
        }
        return result;
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
        return battleState.GetUnit(unitId);
    }

    public BattleUnitState GetBattleUnitAtCoord(Vector2I coord)
    {
        var battleState = GetBattleState();
        var battleGridService = GetBattleGridService();
        if (battleState == null || battleGridService == null)
            return null;
        return battleGridService.GetUnitAtCoord(battleState, coord);
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

    internal Dictionary BuildBattleStartContext(EncounterAnchorData encounterAnchor)
    {
        var context = new Dictionary
        {
            ["world_coord"] =
                encounterAnchor != null
                    ? encounterAnchor.world_coord
                    : _runtime.GetPlayerCoord(),
        };
        context["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor).ToString();
        context["validate_spawn_reachability"] = false;
        if (_runtime != null)
            context["world_step"] = _runtime.GetWorldStep();
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
        return _runtime?.GetBattleSelection();
    }

    private bool IsBattleReady()
    {
        return _runtime != null && GetBattleSelection() != null && GetBattleRuntime() != null;
    }

    private string GetRuntimeStatusText(string fallbackMessage)
    {
        if (_runtime != null)
        {
            string statusText = _runtime.GetStatusText();
            if (!string.IsNullOrEmpty(statusText))
                return statusText;
        }
        return fallbackMessage;
    }

    private Dictionary CommandOk(
        string message = "",
        BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
    )
    {
        if (_runtime != null)
            return _runtime.BuildCommandOk(message, battleRefreshMode);
        return new Dictionary
        {
            ["ok"] = true,
            ["message"] = message,
            ["battle_refresh_mode"] = BattleRefreshModes.ToPayloadValue(battleRefreshMode),
        };
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandOkTyped(
        string message = "",
        BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
    )
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.Ok,
            battleRefreshMode
        );
    }

    private Dictionary CommandError(string message)
    {
        if (_runtime != null)
            return _runtime.BuildCommandError(message);
        return new Dictionary { ["ok"] = false, ["message"] = message };
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandErrorTyped(string message)
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private BattleRuntimeModule GetBattleRuntime()
    {
        return _runtime?.GetBattleRuntime();
    }

    private BattleGridService GetBattleGridService()
    {
        return _runtime?.GetBattleGridService();
    }

    private BattleState GetBattleState()
    {
        return _runtime?.GetBattleState();
    }

    private GameSession GetGameSession()
    {
        return _runtime?.GetGameSession();
    }

    private bool HasPendingPromotionPrompt() =>
        _runtime?.HasPendingPromotionPrompt() ?? false;

    private void SetPendingPromotionPrompt(Dictionary prompt)
    {
        if (_runtime != null)
            _runtime.SetPendingPromotionPrompt(prompt);
    }

    private void SetBattleState(BattleState state)
    {
        if (_runtime != null)
            _runtime.SetRuntimeBattleState(state);
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        if (_runtime != null)
            _runtime.SetRuntimeBattleSelectedCoord(coord);
    }

    private void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        if (_runtime != null)
            _runtime.SetRuntimeActiveModalKind(modalKind);
    }

    private void ClearBattleSelectionTargets()
    {
        if (_runtime != null)
            _runtime.ClearBattleSelectionTargets();
    }

    private bool IsBattleActive()
    {
        return _runtime != null && _runtime.IsBattleActive();
    }

    private string GetBattleInteractionBlockReason()
    {
        if (!IsBattleReady() || !IsBattleActive())
            return "";
        var battleState = GetBattleState();
        if (battleState != null && battleState.ModalStateKind != BattleModalStateKind.None)
            return GetBattleModalBlockReason(battleState.modal_state);
        var activeModalKind = GetActiveModalKind();
        if (IsModalWindowOpen() && IsBattleOverlayModalKind(activeModalKind))
            return GetRuntimeModalBlockReason(activeModalKind);
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

    private static string GetRuntimeModalBlockReason(RuntimeModalKind modalKind)
    {
        if (modalKind == RuntimeModalKind.CharacterInfo)
            return "当前正在查看角色信息，无法操作战斗。";
        return "";
    }

    private static bool IsBattleOverlayModalKind(RuntimeModalKind modalKind)
    {
        return modalKind == RuntimeModalKind.CharacterInfo;
    }

    private RuntimeModalKind GetActiveModalKind()
    {
        return _runtime?.GetActiveModalKind() ?? RuntimeModalKind.None;
    }

    private bool IsModalWindowOpen()
    {
        return _runtime != null && _runtime.IsModalWindowOpen();
    }

    private bool BatchHasUpdates(BattleEventBatch batch)
    {
        return _runtime != null && _runtime.BatchHasUpdates(batch);
    }

    private bool DidSkillCommandExecute(BattleCommand command, BattleEventBatch batch)
    {
        if (command == null || batch == null)
            return false;
        return batch.ContainsChangedUnitId(command.unit_id);
    }

    private void UpdateStatus(string message)
    {
        if (_runtime != null)
            _runtime.UpdateStatus(message);
    }

    private bool TryOpenCharacterInfoAtBattleCoord(Vector2I coord)
    {
        return _runtime != null && _runtime.TryOpenCharacterInfoAtBattleCoord(coord);
    }

    private static BattleUnitState DictionaryBattleUnitState(Dictionary dictionary, StringName key)
    {
        if (dictionary == null || key == null || !dictionary.ContainsKey(key))
            return null;
        var value = dictionary[key];
        return BattleUnitState.TryReadUnitPayload(value, out BattleUnitState unitState)
            ? unitState
            : null;
    }

    private static IReadOnlyList<Vector2I> DuplicateVector2IArray(
        System.Collections.Generic.IEnumerable<Vector2I> values
    ) => new Vector2IList(values);

    private static IReadOnlyList<Vector2I> EmptyVector2IArray() =>
        System.Array.Empty<Vector2I>();

    private static IReadOnlyList<StringName> DuplicateStringNameArray(
        System.Collections.Generic.IEnumerable<StringName> values
    ) => new StringNameList(values);

    private static IReadOnlyList<StringName> EmptyStringNameArray() =>
        System.Array.Empty<StringName>();

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
