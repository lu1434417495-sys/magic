using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class BattleSessionFacade : IDisposable
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private readonly IBattleSeedSource _battleSeedSource;

    private WeakReference<IGameRuntimeBattleSessionPort> _portRef;

    internal BattleSessionFacade(IBattleSeedSource battleSeedSource)
    {
        _battleSeedSource = battleSeedSource
            ?? throw new ArgumentNullException(nameof(battleSeedSource));
    }

    private IGameRuntimeBattleSessionPort Port
    {
        get => ResolveWeakRef(_portRef);
        set =>
            _portRef =
                value != null
                    ? new WeakReference<IGameRuntimeBattleSessionPort>(value)
                    : null;
    }

    internal void Setup(IGameRuntimeBattleSessionPort port)
    {
        Port = port;
    }

    public void Dispose()
    {
        Port = null;
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
        IBattleSelectionSessionSurface battleSelection = GetBattleSelection();
        return battleSelection?.GetSelectedBattleSkillTargetCoordsSnapshotPlain()
            ?? System.Array.Empty<Vector2I>();
    }

    internal IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain()
    {
        if (IsBattleInteractionBlocked())
            return System.Array.Empty<StringName>();
        IBattleSelectionSessionSurface battleSelection = GetBattleSelection();
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
        if (!IsBattleReady() || !IsBattleActive())
            return EmptyVector2IArray();
        if (IsBattleInteractionBlocked())
            return EmptyVector2IArray();
        var activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
            return EmptyVector2IArray();
        return DuplicateVector2IArray(Port.GetBattleMovementReachableCoords(activeUnit));
    }

    public IReadOnlyList<Vector2I> GetBattleOverlayTargetCoords()
    {
        if (!IsBattleReady())
            return EmptyVector2IArray();
        if (IsBattleInteractionBlocked())
            return EmptyVector2IArray();
        if (Port.GetSelectedBattleSkillId() != "")
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

    internal RuntimeCommandResult CommandBattleTickTyped(int tickCount)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        if (tickCount <= 0)
            return CommandErrorTyped("推进 tick 必须大于 0。");
        var combinedBatch = new BattleEventBatch();
        for (int i = 0; i < Mathf.Max(tickCount, 0); i++)
        {
            if (!IsBattleActive())
                break;
            var runtimeState = GetRuntimeBattleState();
            if (runtimeState != null && runtimeState.ModalStateKind != BattleModalStateKind.None)
                break;
            BattleEventBatch batch = Port.AdvanceBattle(1);
            if (BatchHasUpdates(batch))
            {
                combinedBatch.MergeFrom(batch);
                ApplyBattleBatch(batch);
            }
        }
        if (BatchHasUpdates(combinedBatch))
            Port?.CaptureLastCommandBattlePresentationDelta(combinedBatch);
        return CommandOkTyped();
    }

    internal RuntimeCommandResult CommandBattleSelectSkillTyped(int slotIndex)
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
            return CommandErrorTyped(selectResult.Message);
        }
        return CommandOkTyped("", BattleRefreshMode.Overlay);
    }

    internal RuntimeCommandResult CommandBattleCycleVariantTyped(int step)
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

    internal RuntimeCommandResult CommandBattleClearSkillTyped()
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

    internal RuntimeCommandResult CommandBattleMoveToTyped(Vector2I targetCoord)
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

    internal RuntimeCommandResult CommandBattleMoveDirectionTyped(Vector2I direction)
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

    internal RuntimeCommandResult CommandBattleWaitOrResolveTyped()
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

    internal RuntimeCommandResult CommandBattleCancelCastTyped(StringName unitId)
    {
        if (!IsBattleReady())
            return RuntimeUnavailableTypedResult();
        if (!IsBattleActive())
            return CommandErrorTyped("当前没有进行中的战斗。");
        var blockReason = GetBattleInteractionBlockReason();
        if (!string.IsNullOrEmpty(blockReason))
            return CommandErrorTyped(blockReason);
        BattleUnitState unitState = ResolveCancelCastUnit(unitId);
        if (unitState == null)
            return CommandErrorTyped("未找到可取消读条的单位。");
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.CancelCast,
            unit_id = unitState.unit_id,
        };
        BattlePreview preview = Port.PreviewBattleCommand(command);
        if (preview == null || !preview.allowed)
            return CommandErrorTyped(FirstPreviewLogLine(preview, "当前没有可取消的读条。"));
        BattleEventBatch batch = Port.IssueBattleCommand(command);
        Port.CaptureLastCommandBattlePresentationDelta(batch);
        ApplyBattleBatch(batch);
        return CommandOkTyped("", BattleRefreshMode.Full);
    }

    internal RuntimeCommandResult CommandBattleInspectTyped(Vector2I coord)
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

    internal RuntimeCommandResult ResetBattleFocusTyped()
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
        Port.PrepareBattleStart(encounterAnchor);
        StringName startState = Port.BeginBattleStart(
            encounterAnchor,
            BuildBattleSeed(encounterAnchor),
            BuildBattleStartContext(encounterAnchor)
        );
        if (startState == "failed")
            return;
    }

    internal RuntimeCommandResult ResolveActiveBattleTyped()
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
        BattleResolutionResult battleResolutionResult = Port.GetBattleResolutionResult();
        if (battleResolutionResult == null)
        {
            UpdateStatus("战斗已结束，但缺少正式结算结果。");
            return CommandErrorTyped("战斗已结束，但缺少正式结算结果。");
        }
        bool finalized = Port.FinalizeBattleResolution(battleResolutionResult);
        if (!finalized)
            return CommandErrorTyped("战斗结算失败，已保留当前战斗状态以便重试。");
        Port.ConsumeBattleResolutionResult();
        return CommandOkTyped();
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
        return battleSelection.AttemptBattleMoveTo(
            activeUnit.GetAnchorCoord() + direction
        );
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
        Port?.RecordCommandBattleBatch(batch);
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
            Port.GetBattleSelectedCoord() == new Vector2I(-1, -1)
            || !battleState.ContainsCell(Port.GetBattleSelectedCoord())
        )
            SetBattleSelectedCoord(GetDefaultBattleSelectedCoord());
    }

    public int BuildBattleSeed(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
            return 0;
        return _battleSeedSource.NextSeed(encounterAnchor);
    }

    public BattleState GetRuntimeBattleState()
    {
        return Port?.GetRuntimeBattleState();
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
        if (runtimeState == null || Port == null)
            return null;
        return Port.GetBattleUnitAtCoord(runtimeState, coord);
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
        if (Port == null)
            return BattleRefreshMode.Overlay;
        BattleEventBatch batch = Port.IssueBattleCommand(command);
        if (
            command.CommandKind == BattleCommandKind.Skill
            && DidSkillCommandExecute(command, batch)
        )
            ClearBattleSelectionTargets();
        Port.CaptureLastCommandBattlePresentationDelta(batch);
        ApplyBattleBatch(batch);
        return BattleRefreshMode.Full;
    }

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

    internal GameRuntimePromotionPromptContext BuildPromotionPrompt(
        CharacterProgressionDelta delta,
        string selectionHint = "确认后将在战斗中立即生效。"
    )
    {
        if (delta == null || delta.PendingProfessionChoicesTyped.Count == 0)
            return GameRuntimePromotionPromptContext.Empty;
        var memberId = delta.member_id;
        string memberName = Port?.GetMemberDisplayName(memberId) ?? memberId.ToString();
        var choiceEntries = new List<GameRuntimePromotionChoiceContext>();
        foreach (PendingProfessionChoice choiceObj in delta.PendingProfessionChoicesTyped)
        {
            if (choiceObj == null)
                continue;
            foreach (StringName pid in choiceObj.CandidateProfessionIdsTyped)
            {
                if (pid == "")
                    continue;
                if (!choiceObj.TryGetTargetRank(pid, out int targetRank))
                    continue;
                if (targetRank <= 0)
                    continue;
                if (
                    Port == null
                    || !Port.TryGetProfessionDefinition(
                        pid,
                        out ProfessionDefinition professionDef
                    )
                )
                    continue;
                var grantedSkillIds = new List<StringName>();
                var grantedSkills = professionDef.GetGrantedSkillsForRank(targetRank);
                foreach (ProfessionGrantedSkillDefinition skillObj in grantedSkills)
                {
                    if (skillObj != null && skillObj.SkillId != "")
                        grantedSkillIds.Add(skillObj.SkillId);
                }
                choiceEntries.Add(
                    new GameRuntimePromotionChoiceContext(
                        pid,
                        !string.IsNullOrEmpty(professionDef.DisplayName)
                            ? professionDef.DisplayName
                            : pid.ToString(),
                        string.Format("Rank {0}", targetRank),
                        professionDef.Description,
                        grantedSkillIds,
                        selectionHint,
                        PromotionSelectionData.Empty
                    )
                );
            }
        }
        return choiceEntries.Count > 0
            ? new GameRuntimePromotionPromptContext(memberId, memberName, choiceEntries)
            : GameRuntimePromotionPromptContext.Empty;
    }

    internal IReadOnlyDictionary<string, object> BuildPromotionPromptPlain(
        CharacterProgressionDelta delta,
        string selectionHint = "确认后将在战斗中立即生效。"
    ) => BuildPromotionPrompt(delta, selectionHint).ToPlainSnapshot();

    public Vector2I GetDefaultBattleSelectedCoord()
    {
        var activeUnit = GetBattleActiveUnit();
        if (activeUnit != null)
            return activeUnit.GetAnchorCoord();
        var battleState = GetBattleState();
        if (battleState != null)
        {
            foreach (StringName allyUnitId in battleState.ally_unit_ids)
            {
                var unit = GetBattleUnitById(allyUnitId);
                if (unit != null)
                    return unit.GetAnchorCoord();
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
        if (battleState == null || Port == null)
            return null;
        return Port.GetBattleUnitAtCoord(battleState, coord);
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
                    : Port?.GetPlayerCoord() ?? Vector2I.Zero,
        };
        context["battle_terrain_profile"] = ResolveBattleTerrainProfile(encounterAnchor).ToString();
        context["validate_spawn_reachability"] = false;
        if (Port != null)
            context["world_step"] = Port.GetWorldStep();
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

    private IBattleSelectionSessionSurface GetBattleSelection()
    {
        return Port?.GetBattleSelection();
    }

    private bool IsBattleReady()
    {
        return Port != null && GetBattleSelection() != null;
    }

    private string GetRuntimeStatusText(string fallbackMessage)
    {
        if (Port != null)
        {
            string statusText = Port.GetStatusText();
            if (!string.IsNullOrEmpty(statusText))
                return statusText;
        }
        return fallbackMessage;
    }

    private RuntimeCommandResult CommandOkTyped(
        string message = "",
        BattleRefreshMode battleRefreshMode = BattleRefreshMode.None
    )
    {
        return RuntimeCommandResult.Success(
            message ?? "",
            RuntimeCommandCode.Ok,
            battleRefreshMode
        );
    }

    private RuntimeCommandResult CommandErrorTyped(string message)
    {
        return RuntimeCommandResult.Failure(
            message ?? "",
            RuntimeCommandCode.InvalidState
        );
    }

    private RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private BattleState GetBattleState()
    {
        return Port?.GetPublishedBattleState();
    }

    private bool HasPendingPromotionPrompt() =>
        Port?.HasPendingPromotionPrompt() ?? false;

    private void SetPendingPromotionPrompt(GameRuntimePromotionPromptContext prompt)
    {
        Port?.SetPendingPromotionPrompt(prompt);
    }

    private void SetBattleState(BattleState state)
    {
        Port?.SetPublishedBattleState(state);
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        Port?.SetBattleSelectedCoord(coord);
    }

    private void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        Port?.SetActiveModalKind(modalKind);
    }

    private void ClearBattleSelectionTargets()
    {
        Port?.ClearBattleSelectionTargets();
    }

    private bool IsBattleActive()
    {
        return Port?.IsBattleActive() ?? false;
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
        return Port?.GetActiveModalKind() ?? RuntimeModalKind.None;
    }

    private bool IsModalWindowOpen()
    {
        return Port?.IsModalWindowOpen() ?? false;
    }

    private static bool BatchHasUpdates(BattleEventBatch batch) =>
        batch != null
        && (
            batch.ChangeFlags != BattleChangeFlags.None
            || batch.phase_changed
            || batch.battle_ended
            || batch.modal_requested
            || batch.ChangedUnitIdsTyped.Count > 0
            || batch.ChangedCoordsTyped.Count > 0
            || batch.LogLinesTyped.Count > 0
            || batch.ProgressionDeltaCount > 0
        );

    private bool DidSkillCommandExecute(BattleCommand command, BattleEventBatch batch)
    {
        if (command == null || batch == null)
            return false;
        return batch.ContainsChangedUnitId(command.unit_id);
    }

    private void UpdateStatus(string message)
    {
        Port?.UpdateStatus(message);
    }

    private bool TryOpenCharacterInfoAtBattleCoord(Vector2I coord)
    {
        return Port?.TryOpenCharacterInfoAtBattleCoord(coord) ?? false;
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

    private static IGameRuntimeBattleSessionPort ResolveWeakRef(
        WeakReference<IGameRuntimeBattleSessionPort> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimeBattleSessionPort target)
        )
            return null;
        return target;
    }
}
