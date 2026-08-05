using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public sealed class GameRuntimeBattleSelection : IDisposable, IBattleSelectionSessionSurface
{
    private static readonly StringName StatusBlackStarBrandElite = "black_star_brand_elite";
    private static readonly StringName CrownBreakSkillId = "crown_break";
    private static readonly StringName DoomSentenceSkillId = "doom_sentence";
    private static readonly StringName DoomShiftSkillId = "doom_shift";
    private static readonly StringName BlackCrownSealSkillId = "black_crown_seal";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";
    private static readonly StringName GroundTargetMode = BattleTypedNames.TargetModeGround;
    private static readonly StringName SelfSelectionMode = BattleTypedNames.TargetSelectionSelf;
    private static readonly StringName EnemyFilter = "enemy";
    private readonly BattleTargetCollectionService _targetCollectionService = new();
    private readonly Dictionary<StringName, CombatCastVariantDefinition> _implicitGroundCastVariantsBySkillId =
        new();
    private WeakReference<IGameRuntimeBattleSelectionPort> _portRef;

    private IGameRuntimeBattleSelectionPort Port
    {
        get => ResolveWeakRef(_portRef);
        set =>
            _portRef =
                value != null
                    ? new WeakReference<IGameRuntimeBattleSelectionPort>(value)
                    : null;
    }

    internal void Setup(IGameRuntimeBattleSelectionPort port)
    {
        Port = port;
    }

    public void Dispose()
    {
        _implicitGroundCastVariantsBySkillId.Clear();
        Port = null;
    }

    internal string GetSelectedBattleSkillName()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        return skillDefinition?.DisplayName ?? "";
    }

    internal string GetSelectedBattleSkillVariantName()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        if (
            activeUnit != null
            && skillDefinition?.CombatProfile?.Windup != null
            && BattleWindupRules.TryBuildQuote(
                activeUnit,
                skillDefinition,
                GetSelectedWindupTier(),
                out BattleWindupQuote quote,
                out _,
                requireAffordable: false
            )
        )
        {
            return $"蓄力 {quote.Tier} 挡 · {quote.TotalWindupTu} TU · {quote.TotalStaminaCost} 体力 · {quote.WeaponDiceMultiplier}W";
        }
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        return castVariant?.DisplayName ?? "";
    }

    internal IReadOnlyList<Vector2I> GetSelectedBattleSkillTargetCoordsSnapshotPlain() =>
        CollectSelectedBattleSkillTargetCoordsTyped();

    internal IReadOnlyList<StringName> GetSelectedBattleSkillTargetUnitIdsSnapshotPlain() =>
        GetTargetUnitIdsStateTyped();

    internal IReadOnlyList<Vector2I> GetSelectedBattleSkillValidTargetCoordsSnapshotPlain() =>
        CollectSelectedBattleSkillValidTargetCoordsTyped();

    internal int GetSelectedBattleSkillRequiredCoordCount()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile != null)
        {
            BattleTargetSelectionMode selectionMode =
                GetSelectedBattleSkillTargetSelectionModeKind(activeUnit);
            if (selectionMode == BattleTargetSelectionMode.MultiUnit)
            {
                SkillEffectiveCombatDefinition effectiveProfile = GetEffectiveCombatProfileForUnit(
                    activeUnit,
                    skillDefinition
                );
                return Math.Max(
                    effectiveProfile.MaxTargetCount,
                    combatProfile.MinTargetCount
                );
            }
            if (combatProfile.TargetModeKind == BattleTargetMode.Unit)
            {
                return 1;
            }
        }
        return castVariant == null ? 0 : castVariant.RequiredCoordCount;
    }

    internal BattlePreview GetSelectedBattleSkillPreview()
    {
        return PreviewSelectedBattleSkillAtCoord(
            Port?.GetBattleSelectedCoord() ?? new Vector2I(-1, -1)
        );
    }

    internal BattlePreview PreviewSelectedBattleSkillAtCoord(Vector2I coord)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        BattleCommand command = BuildSelectedSkillPreviewCommand(activeUnit, coord);
        return command != null ? PreviewBattleCommand(command) : null;
    }

    internal BattleSelectionCommandResult SelectBattleSkillSlotTyped(int index)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return SelectionErrorTyped("当前没有可手动操作的单位。");
        }
        if (
            !TryGetAvailableSkillEntryBySlot(
                activeUnit,
                index,
                BattleSkillAvailabilityConsumer.ManualSelection,
                out BattleAvailableSkillEntry skillEntry
            )
        )
        {
            UpdateStatus("该技能栏当前没有技能。");
            return SelectionErrorTyped("该技能栏当前没有技能。");
        }

        StringName skillId = skillEntry.EntryRef.SkillId;
        StringName skillEntryId = skillEntry.EntryRef.SkillEntryId;
        SkillDefinition skillDefinition = skillEntry.SkillDefinition;
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            UpdateStatus("该技能当前不可用于战斗。");
            return SelectionErrorTyped("该技能当前不可用于战斗。");
        }

        if (GetSelectedSkillEntryId() == skillEntryId)
        {
            ClearBattleSkillSelection(true);
            return SelectionOkTyped();
        }

        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDefinition);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return SelectionErrorTyped(blockReason);
        }

        SetSelectedSkillEntryId(skillEntryId);
        SetSelectedSkillId(skillId);
        SetSelectedSkillVariantId("");
        SetSelectedWindupTier(1);
        ClearBattleSkillTargetSelection();

        if (combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain)
        {
            var chainCommand = new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = activeUnit.unit_id,
                skill_entry_id = skillEntryId,
                skill_id = skillId,
                skill_variant_id = GetDefaultUnitSkillVariantId(activeUnit, skillDefinition),
                target_unit_ids = new StringNameList(),
            };
            BattlePreview chainPreview = PreviewBattleCommand(chainCommand);
            if (chainPreview != null && chainPreview.allowed)
            {
                IssueBattleCommand(chainCommand);
                return SelectionOkTyped();
            }
            RefreshBattleSelectionState();
            UpdateStatus(
                chainPreview != null && chainPreview.LogLinesTyped.Count > 0
                    ? chainPreview.LogLinesTyped[chainPreview.LogLinesTyped.Count - 1]
                    : "无法执行连锁攻击。"
            );
            return SelectionErrorTyped("无法执行连锁攻击。");
        }

        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions = GetUnlockedCastVariants(
            activeUnit,
            skillDefinition
        );
        if (unlockedOptions.Count > 0 && unlockedOptions[0] is CombatCastVariantDefinition firstValue)
        {
            SetSelectedSkillVariantId(firstValue.VariantId);
        }
        RefreshBattleSelectionState();
        UpdateStatus(BuildBattleSkillSelectionStatus(skillDefinition, activeUnit));
        return SelectionOkTyped();
    }

    internal void CycleSelectedBattleSkillOption(int step)
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可手动操作的单位。");
            return;
        }
        if (GetSelectedSkillId() == "")
        {
            UpdateStatus("请先用数字键选择一个技能。");
            return;
        }

        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile?.Windup != null)
        {
            int maxTier = BattleWindupRules.GetMaxTier(activeUnit, skillDefinition);
            if (maxTier <= 0)
            {
                UpdateStatus("当前没有可选择的蓄力挡位。");
                return;
            }
            int nextTier = Math.Clamp(GetSelectedWindupTier() + step, 1, maxTier);
            SetSelectedWindupTier(nextTier);
            ClearBattleSkillTargetSelection();
            RefreshBattleSelectionState();
            UpdateStatus(BuildBattleSkillSelectionStatus(skillDefinition, activeUnit));
            return;
        }
        if (combatProfile == null || combatProfile.CastVariants.Count == 0)
        {
            UpdateStatus("当前技能没有可切换的施法形态。");
            return;
        }

        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions = GetUnlockedCastVariants(
            activeUnit,
            skillDefinition
        );
        if (unlockedOptions.Count == 0)
        {
            UpdateStatus("当前技能等级尚未解锁任何施法形态。");
            return;
        }

        int currentIndex = 0;
        for (int optionIndex = 0; optionIndex < unlockedOptions.Count; optionIndex++)
        {
            CombatCastVariantDefinition castVariant = unlockedOptions[optionIndex];
            if (castVariant != null && castVariant.VariantId == GetSelectedSkillVariantId())
            {
                currentIndex = optionIndex;
                break;
            }
        }

        int nextIndex = PosMod(currentIndex + step, unlockedOptions.Count);
        if (unlockedOptions[nextIndex] is CombatCastVariantDefinition nextValue)
        {
            SetSelectedSkillVariantId(nextValue.VariantId);
        }
        ClearBattleSkillTargetSelection();
        RefreshBattleSelectionState();
        UpdateStatus(BuildBattleSkillSelectionStatus(skillDefinition, activeUnit));
    }

    internal void ClearBattleSkillSelection(bool announce = false)
    {
        if (
            GetSelectionStage()
                == GameRuntimeBattleSelectionStage.SourceRetreatDirection
            && GetSelectedSkillId() != ""
        )
        {
            ClearBattleSkillTargetSelection();
            if (IsBattleActive())
            {
                RefreshBattleSelectionState();
            }
            if (announce)
            {
                UpdateStatus("已返回攻击目标选择。");
            }
            return;
        }
        SetSelectedSkillEntryId("");
        SetSelectedSkillId("");
        SetSelectedSkillVariantId("");
        SetSelectedWindupTier(1);
        ClearBattleSkillTargetSelection();
        SetLastManualUnitId("");
        if (IsBattleActive())
        {
            RefreshBattleSelectionState();
        }
        if (announce)
        {
            UpdateStatus("已清除当前战斗技能选择。");
        }
    }

    internal void SyncSelectedBattleSkillState()
    {
        BattleUnitState activeUnit = GetManualActiveUnit();
        StringName activeUnitId = activeUnit?.unit_id ?? new StringName("");
        if (activeUnitId != GetLastManualUnitId())
        {
            SetSelectedSkillEntryId("");
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
        }
        SetLastManualUnitId(activeUnitId);
        if (activeUnit == null)
        {
            SetSelectedSkillEntryId("");
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }
        if (GetSelectedSkillId() == "")
        {
            if (GetSelectedSkillEntryId() != "")
            {
                SetSelectedSkillEntryId("");
                ClearBattleSkillTargetSelection();
            }
            return;
        }
        if (
            !TryResolveAvailableSkillEntry(
                activeUnit,
                GetSelectedSkillEntryId(),
                BattleSkillAvailabilityConsumer.ManualSelection,
                out BattleAvailableSkillEntry selectedEntry
            )
        )
        {
            SetSelectedSkillEntryId("");
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }
        SetSelectedSkillId(selectedEntry.EntryRef.SkillId);

        SkillDefinition skillDefinition = selectedEntry.SkillDefinition;
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            SetSelectedSkillEntryId("");
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            SetSelectedSkillVariantId("");
            return;
        }
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (castVariant == null)
        {
            SetSelectedSkillEntryId("");
            SetSelectedSkillId("");
            SetSelectedSkillVariantId("");
            ClearBattleSkillTargetSelection();
            return;
        }
        SetSelectedSkillVariantId(castVariant.VariantId);
    }

    internal BattleRefreshMode AttemptBattleMoveTo(Vector2I target_coord)
    {
        if (!IsBattleActive())
        {
            return BattleRefreshMode.Full;
        }

        SetBattleSelectedCoord(target_coord);
        BattleState battleState = GetBattleState();
        if (battleState == null || !battleState.TryGetCellTyped(target_coord, out _))
        {
            RefreshBattleSelectionState();
            UpdateStatus("该战斗格超出当前战场范围。");
            return BattleRefreshMode.Overlay;
        }

        BattleUnitState activeUnit = GetManualActiveUnit();
        if (activeUnit == null)
        {
            RefreshBattleSelectionState();
            UpdateStatus("等待当前单位进入可操作状态。");
            return BattleRefreshMode.Overlay;
        }

        if (
            GetSelectionStage()
                == GameRuntimeBattleSelectionStage.SourceRetreatDirection
        )
        {
            return HandleSourceRetreatDirectionClick(activeUnit, target_coord);
        }

        if (IsSelectedGroundSkillReady(activeUnit))
        {
            return HandleSelectedGroundSkillClick(activeUnit, target_coord);
        }

        BattleUnitState targetUnit = GetRuntimeUnitAtCoord(target_coord);
        if (targetUnit != null)
        {
            BattleRefreshMode selectedSkillResult = HandleSelectedUnitSkillClick(activeUnit, targetUnit);
            if (selectedSkillResult != BattleRefreshMode.None)
            {
                return selectedSkillResult;
            }

            BattleCommand skillCommand = BuildSelectedSkillCommand(activeUnit, targetUnit);
            if (skillCommand != null)
            {
                return IssueBattleCommand(skillCommand);
            }

            skillCommand = BuildSkillCommand(activeUnit, targetUnit);
            if (skillCommand != null)
            {
                return IssueBattleCommand(skillCommand);
            }

        }
        else if (GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit)
        {
            SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile != null)
            {
                int minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
                if (GetTargetUnitIdsStateTyped().Count >= minTargetCount)
                {
                    return IssueSelectedMultiUnitSkill(activeUnit, skillDefinition);
                }
            }
        }

        BattleCommand interactionCommand = BuildObjectiveInteractionCommand(
            activeUnit,
            target_coord,
            targetUnit
        );
        if (interactionCommand != null)
        {
            BattlePreview interactionPreview = PreviewBattleCommand(
                interactionCommand
            );
            if (interactionPreview?.allowed == true)
                return IssueBattleCommand(interactionCommand);
        }

        if (activeUnit.OccupiesCoord(target_coord))
        {
            RefreshBattleSelectionState();
            UpdateStatus("已选中当前行动单位。");
            return BattleRefreshMode.Overlay;
        }

        var moveCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Move,
            unit_id = activeUnit.unit_id,
            target_coord = target_coord,
        };
        BattlePreview preview = PreviewBattleCommand(moveCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(moveCommand);
        }

        RefreshBattleSelectionState();
        if (preview != null && preview.LogLinesTyped.Count > 0)
        {
            UpdateStatus(preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]);
        }
        else
        {
            UpdateStatus($"已选中战斗格 {FormatCoord(target_coord)}。");
        }
        return BattleRefreshMode.Overlay;
    }

    private BattleCommand BuildObjectiveInteractionCommand(
        BattleUnitState activeUnit,
        Vector2I targetCoord,
        BattleUnitState targetUnit
    )
    {
        if (
            activeUnit == null
            || GetSelectedSkillId() != ""
        )
        {
            return null;
        }

        BattleObjectiveRuntimeState objective = GetBattleState()?.ObjectiveRuntimeState;
        if (
            objective is BattleRescueObjectiveRuntimeState rescueObjective
            && targetUnit != null
            && !rescueObjective.TargetSecured
            && rescueObjective.TargetUnitId == targetUnit.unit_id
        )
        {
            return new BattleCommand
            {
                CommandKind = BattleCommandKind.Interact,
                unit_id = activeUnit.unit_id,
                target_unit_id = targetUnit.unit_id,
                target_coord = targetUnit.GetAnchorCoord(),
            };
        }
        if (
            objective is BattleNodeOperationObjectiveRuntimeState nodeObjective
            && nodeObjective.TryGetNodeAtCoord(
                targetCoord,
                out BattleOperationNodeRuntimeState node
            )
            && !node.IsCompleted
        )
        {
            return new BattleCommand
            {
                CommandKind = BattleCommandKind.Interact,
                unit_id = activeUnit.unit_id,
                target_coord = targetCoord,
            };
        }
        return null;
    }

    internal BattleRefreshMode ResetBattleMovement()
    {
        if (!IsBattleActive())
        {
            return BattleRefreshMode.Full;
        }

        BattleUnitState activeUnit = GetRuntimeActiveUnit();
        if (activeUnit == null)
        {
            UpdateStatus("当前没有可聚焦的行动单位。");
            return BattleRefreshMode.Overlay;
        }

        SetBattleSelectedCoord(activeUnit.GetAnchorCoord());
        RefreshBattleSelectionState();
        UpdateStatus("已聚焦当前行动单位。");
        return BattleRefreshMode.Overlay;
    }

    private BattleCommand BuildSkillCommand(BattleUnitState activeUnit, BattleUnitState targetUnit)
    {
        if (activeUnit == null || targetUnit == null)
        {
            return null;
        }

        BattleSkillAvailabilityView availabilityView = BuildSkillAvailabilityView(
            activeUnit,
            BattleSkillAvailabilityConsumer.ManualSelection
        );
        foreach (BattleAvailableSkillEntry skillEntry in availabilityView.SkillEntries)
        {
            if (skillEntry == null || !skillEntry.IsSelectable)
            {
                continue;
            }
            StringName skillId = skillEntry.EntryRef.SkillId;
            SkillDefinition skillDefinition = skillEntry.SkillDefinition;
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile == null)
            {
                continue;
            }
            if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
            {
                continue;
            }
            if (GetSourceRetreatEffect(activeUnit, skillDefinition) != null)
            {
                continue;
            }
            CombatCastVariantDefinition castVariant = GetCastVariant(
                combatProfile,
                GetDefaultUnitSkillVariantId(activeUnit, skillDefinition)
            );
            if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDefinition, castVariant))
            {
                continue;
            }
            return new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = activeUnit.unit_id,
                skill_entry_id = skillEntry.EntryRef.SkillEntryId,
                skill_id = skillId,
                skill_variant_id = GetDefaultUnitSkillVariantId(activeUnit, skillDefinition),
                windup_tier = skillDefinition.CombatProfile.Windup != null ? 1 : 0,
                target_unit_id = targetUnit.unit_id,
                target_coord = targetUnit.GetAnchorCoord(),
            };
        }
        return null;
    }

    private BattleCommand BuildSelectedSkillCommand(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        if (activeUnit == null || targetUnit == null || GetSelectedSkillId() == "")
        {
            return null;
        }

        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return null;
        }
        if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
        {
            return null;
        }
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDefinition, castVariant))
        {
            return null;
        }
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_entry_id = GetSelectedSkillEntryId(),
            skill_id = GetSelectedSkillId(),
            skill_variant_id = GetSelectedSkillVariantId(),
            windup_tier = GetSelectedWindupTier(),
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.GetAnchorCoord(),
        };
    }

    internal BattleCommand BuildSelectedSkillPreviewCommand(
        BattleUnitState activeUnit,
        Vector2I coord
    )
    {
        if (activeUnit == null || GetSelectedSkillId() == "")
            return null;

        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return null;

        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_entry_id = GetSelectedSkillEntryId(),
            skill_id = GetSelectedSkillId(),
            skill_variant_id = castVariant?.VariantId ?? GetSelectedSkillVariantId(),
            windup_tier = GetSelectedWindupTier(),
            target_coord = coord,
        };

        if (
            GetSelectionStage()
                == GameRuntimeBattleSelectionStage.SourceRetreatDirection
        )
        {
            BattleUnitState queuedTargetUnit = ResolveQueuedSourceRetreatTarget();
            if (
                queuedTargetUnit == null
                || !BattleSourceRetreatRules.TryResolveSelectionDirection(
                    activeUnit.GetAnchorCoord(),
                    coord,
                    out Vector2I direction
                )
            )
            {
                return null;
            }
            command.target_unit_id = queuedTargetUnit.unit_id;
            command.target_coord = queuedTargetUnit.GetAnchorCoord();
            command.source_retreat_direction = direction;
            return command;
        }

        BattleTargetSelectionMode selectionMode =
            GetSelectedBattleSkillTargetSelectionModeKind(activeUnit);
        if (selectionMode == BattleTargetSelectionMode.MultiUnit)
        {
            return BuildSelectedMultiUnitPreviewCommand(command, activeUnit, skillDefinition, coord);
        }

        BattleTargetMode targetMode = castVariant?.TargetModeKind
            ?? combatProfile.TargetModeKind;
        if (targetMode == BattleTargetMode.Ground)
        {
            return BuildSelectedGroundPreviewCommand(command, castVariant, coord);
        }

        BattleUnitState targetUnit = GetRuntimeUnitAtCoord(coord);
        if (
            targetUnit == null
            && (
                selectionMode == BattleTargetSelectionMode.Self
                || combatProfile.TargetTeamFilter == SelfSelectionMode
            )
        )
        {
            targetUnit = activeUnit;
        }
        if (targetUnit == null)
            return null;

        command.target_unit_id = targetUnit.unit_id;
        command.target_coord = targetUnit.GetAnchorCoord();
        return command;
    }

    private BattleCommand BuildSelectedMultiUnitPreviewCommand(
        BattleCommand command,
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        Vector2I coord
    )
    {
        if (command == null)
            return null;

        var targetUnitIds = new List<StringName>(GetTargetUnitIdsStateTyped());
        BattleUnitState hoveredUnit = GetRuntimeUnitAtCoord(coord);
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (
            hoveredUnit != null
            && !targetUnitIds.Contains(hoveredUnit.unit_id)
            && CanSkillTargetUnit(activeUnit, hoveredUnit, skillDefinition, castVariant)
        )
        {
            targetUnitIds.Add(hoveredUnit.unit_id);
        }
        if (targetUnitIds.Count == 0)
            return null;

        command.target_unit_ids = DuplicateStringNameArray(targetUnitIds);
        BattleUnitState firstTarget = GetBattleUnitById(targetUnitIds[0]);
        if (firstTarget != null)
        {
            command.target_coord = firstTarget.GetAnchorCoord();
        }
        return command;
    }

    private BattleCommand BuildSelectedGroundPreviewCommand(
        BattleCommand command,
        CombatCastVariantDefinition castVariant,
        Vector2I coord
    )
    {
        if (command == null || castVariant == null)
            return null;

        var targetCoords = new List<Vector2I>(GetTargetCoordsStateTyped());
        if (coord.X >= 0 && coord.Y >= 0 && !targetCoords.Contains(coord))
        {
            targetCoords.Add(coord);
        }

        int requiredCoordCount = Math.Max(castVariant.RequiredCoordCount, 1);
        if (targetCoords.Count > requiredCoordCount)
        {
            targetCoords = targetCoords.GetRange(
                targetCoords.Count - requiredCoordCount,
                requiredCoordCount
            );
        }
        if (targetCoords.Count == 0)
            return null;

        command.target_coords = DuplicateVector2IArray(targetCoords);
        command.target_coord = targetCoords[targetCoords.Count - 1];
        return command;
    }

    private StringName GetDefaultUnitSkillVariantId(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null || combatProfile.CastVariants.Count == 0)
        {
            return "";
        }
        foreach (CombatCastVariantDefinition castVariant in GetUnlockedCastVariants(activeUnit, skillDefinition))
        {
            if (castVariant != null && castVariant.TargetModeKind == BattleTargetMode.Unit)
            {
                return castVariant.VariantId;
            }
        }
        return "";
    }

    private bool IsSelectedGroundSkillReady(BattleUnitState activeUnit)
    {
        if (GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit)
        {
            return false;
        }
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        return castVariant != null && castVariant.TargetModeKind == BattleTargetMode.Ground;
    }

    private BattleRefreshMode HandleSelectedGroundSkillClick(
        BattleUnitState activeUnit,
        Vector2I targetCoord
    )
    {
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        if (castVariant == null || skillDefinition == null)
        {
            RefreshBattleSelectionState();
            UpdateStatus("当前地面技能形态不可用。");
            return BattleRefreshMode.Overlay;
        }

        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDefinition);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return BattleRefreshMode.Error;
        }

        int requiredCoordCount = Math.Max(castVariant.RequiredCoordCount, 1);
        List<Vector2I> queuedTargetCoords = GetTargetCoordsStateTyped();
        List<Vector2I> previousTargets = new(queuedTargetCoords);
        int existingIndex = queuedTargetCoords.IndexOf(targetCoord);
        if (existingIndex >= 0)
        {
            queuedTargetCoords.RemoveAt(existingIndex);
            SetTargetCoordsStateTyped(queuedTargetCoords);
            RefreshBattleSelectionState();
            UpdateStatus($"已取消目标格 {FormatCoord(targetCoord)}。");
            return BattleRefreshMode.Overlay;
        }

        if (requiredCoordCount == 1)
        {
            SetTargetCoordsStateTyped(new[] { targetCoord });
        }
        else
        {
            if (queuedTargetCoords.Count >= requiredCoordCount)
            {
                UpdateStatus(
                    $"该技能形态最多选择 {requiredCoordCount} 个地格；点击已选地格可取消。"
                );
                return BattleRefreshMode.Overlay;
            }
            queuedTargetCoords.Add(targetCoord);
            SetTargetCoordsStateTyped(queuedTargetCoords);
        }

        List<Vector2I> resolvedTargetCoords = GetTargetCoordsStateTyped();
        if (resolvedTargetCoords.Count < requiredCoordCount)
        {
            RefreshBattleSelectionState();
            UpdateStatus(
                $"{BuildSkillVariantDisplayName(skillDefinition, castVariant)}：已选择 {resolvedTargetCoords.Count} / {requiredCoordCount} 个地格。"
            );
            return BattleRefreshMode.Overlay;
        }

        var skillCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_entry_id = GetSelectedSkillEntryId(),
            skill_id = GetSelectedSkillId(),
            skill_variant_id = castVariant.VariantId,
            target_coords = DuplicateVector2IArray(resolvedTargetCoords),
            target_coord = targetCoord,
        };

        BattlePreview preview = PreviewBattleCommand(skillCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(skillCommand);
        }

        SetTargetCoordsStateTyped(requiredCoordCount > 1 ? previousTargets : Array.Empty<Vector2I>());
        RefreshBattleSelectionState();
        if (preview != null && preview.LogLinesTyped.Count > 0)
        {
            UpdateStatus(preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]);
        }
        else
        {
            UpdateStatus("当前地面技能目标无效。");
        }
        return BattleRefreshMode.Overlay;
    }

    private BattleRefreshMode HandleSelectedUnitSkillClick(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return BattleRefreshMode.None;
        }
        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDefinition);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return BattleRefreshMode.Error;
        }

        BattleTargetSelectionMode selectionMode = combatProfile.TargetSelectionModeKind;
        if (selectionMode == BattleTargetSelectionMode.MultiUnit)
        {
            return ToggleSelectedMultiUnitSkillTarget(activeUnit, targetUnit, skillDefinition);
        }
        if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
        {
            return BattleRefreshMode.None;
        }

        CombatEffectDefinition sourceRetreatEffect = GetSourceRetreatEffect(
            activeUnit,
            skillDefinition
        );
        if (sourceRetreatEffect != null)
        {
            CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(
                activeUnit
            );
            if (
                !CanSkillTargetUnit(
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    castVariant
                )
            )
            {
                return BattleRefreshMode.None;
            }
            SetTargetUnitIdsStateTyped(new[] { targetUnit.unit_id });
            SetTargetCoordsStateTyped(
                new[] { targetUnit.GetAnchorCoord() }
            );
            SetSelectionStage(
                GameRuntimeBattleSelectionStage.SourceRetreatDirection
            );
            RefreshBattleSelectionState();
            UpdateStatus(
                $"已选择 {targetUnit.display_name}，请选择一个远离目标的直线后撤方向。"
            );
            return BattleRefreshMode.Overlay;
        }

        BattleCommand skillCommand = BuildSelectedSkillCommand(activeUnit, targetUnit);
        return skillCommand != null ? IssueBattleCommand(skillCommand) : BattleRefreshMode.None;
    }

    private BattleRefreshMode HandleSourceRetreatDirectionClick(
        BattleUnitState activeUnit,
        Vector2I selectedCoord
    )
    {
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatEffectDefinition sourceRetreatEffect = GetSourceRetreatEffect(
            activeUnit,
            skillDefinition
        );
        BattleUnitState targetUnit = ResolveQueuedSourceRetreatTarget();
        if (
            activeUnit == null
            || skillDefinition == null
            || sourceRetreatEffect == null
            || targetUnit == null
        )
        {
            ClearBattleSkillTargetSelection();
            RefreshBattleSelectionState();
            UpdateStatus("后撤目标已经失效，请重新选择攻击目标。");
            return BattleRefreshMode.Error;
        }
        if (
            !BattleSourceRetreatRules.TryResolveSelectionDirection(
                activeUnit.GetAnchorCoord(),
                selectedCoord,
                out Vector2I direction
            )
            || !BattleSourceRetreatRules.IncreasesDistanceFromTarget(
                activeUnit.GetAnchorCoord(),
                targetUnit.GetAnchorCoord(),
                direction
            )
        )
        {
            RefreshBattleSelectionState();
            UpdateStatus("请选择一个使你远离攻击目标的上下左右方向。");
            return BattleRefreshMode.Error;
        }

        BattleCommand skillCommand = BuildSelectedSkillCommand(
            activeUnit,
            targetUnit
        );
        if (skillCommand == null)
        {
            RefreshBattleSelectionState();
            UpdateStatus("当前攻击目标已经无效。");
            return BattleRefreshMode.Error;
        }
        skillCommand.source_retreat_direction = direction;
        BattlePreview preview = PreviewBattleCommand(skillCommand);
        if (preview?.allowed == true)
        {
            return IssueBattleCommand(skillCommand);
        }

        RefreshBattleSelectionState();
        UpdateStatus(
            preview != null && preview.LogLinesTyped.Count > 0
                ? preview.LogLinesTyped[^1]
                : "当前后撤方向无效。"
        );
        return BattleRefreshMode.Error;
    }

    private BattleUnitState ResolveQueuedSourceRetreatTarget()
    {
        IReadOnlyList<StringName> targetUnitIds = GetTargetUnitIdsStateTyped();
        return targetUnitIds.Count == 1
            ? GetBattleUnitById(targetUnitIds[0])
            : null;
    }

    private SkillDefinition GetSelectedBattleSkillDefinition(BattleUnitState activeUnit)
    {
        if (activeUnit == null || GetSelectedSkillId() == "")
        {
            return null;
        }
        if (
            !TryResolveAvailableSkillEntry(
                activeUnit,
                GetSelectedSkillEntryId(),
                BattleSkillAvailabilityConsumer.ManualSelection,
                out BattleAvailableSkillEntry selectedEntry
            )
        )
        {
            return null;
        }
        return selectedEntry.SkillDefinition;
    }

    private CombatCastVariantDefinition GetSelectedBattleSkillVariant(BattleUnitState activeUnit)
    {
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return null;
        }
        if (combatProfile.CastVariants.Count == 0)
        {
            return combatProfile.TargetModeKind == BattleTargetMode.Ground
                ? BuildImplicitGroundCastVariant(skillDefinition)
                : null;
        }

        IReadOnlyList<CombatCastVariantDefinition> unlockedOptions = GetUnlockedCastVariants(
            activeUnit,
            skillDefinition
        );
        if (unlockedOptions.Count == 0)
        {
            return null;
        }
        if (GetSelectedSkillVariantId() == "")
        {
            return unlockedOptions[0];
        }
        foreach (CombatCastVariantDefinition castVariant in unlockedOptions)
        {
            if (castVariant != null && castVariant.VariantId == GetSelectedSkillVariantId())
            {
                return castVariant;
            }
        }
        return unlockedOptions[0];
    }

    private IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariants(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        if (activeUnit == null || skillDefinition?.CombatProfile == null)
        {
            return Array.Empty<CombatCastVariantDefinition>();
        }
        SkillEffectiveCombatDefinition effectiveProfile = GetEffectiveCombatProfileForUnit(
            activeUnit,
            skillDefinition
        );
        return effectiveProfile.UnlockedCastVariants;
    }

    private static CombatCastVariantDefinition GetCastVariant(
        CombatSkillDefinition combatProfile,
        StringName variantId
    )
    {
        if (combatProfile == null || variantId == "")
        {
            return null;
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant != null && castVariant.VariantId == variantId)
            {
                return castVariant;
            }
        }
        return null;
    }

    private CombatCastVariantDefinition BuildImplicitGroundCastVariant(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            combatProfile == null
            || combatProfile.TargetModeKind != BattleTargetMode.Ground
        )
        {
            return null;
        }
        StringName skillId = ProgressionDataUtils.to_string_name(skillDefinition.SkillId);
        if (
            skillId != ""
            && _implicitGroundCastVariantsBySkillId.TryGetValue(
                skillId,
                out CombatCastVariantDefinition cachedVariant
            )
        )
        {
            return cachedVariant;
        }

        var variant = new CombatCastVariantDefinition(
            "",
            "",
            "",
            0,
            BattleTypedNames.TargetModeGround,
            CombatSkillTargetingContentRules.ToFootprintPatternId(
                CombatCastFootprintPattern.Single
            ),
            1,
            Array.Empty<StringName>(),
            combatProfile.EffectDefinitions,
            null
        );
        if (skillId != "")
            _implicitGroundCastVariantsBySkillId[skillId] = variant;
        return variant;
    }

    private SkillDefinition GetSkillDefinition(StringName skillId)
    {
        ISkillCatalog skillCatalog = GetSkillCatalog();
        return skillCatalog != null
            && skillCatalog.TryGetSkillDefinition(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private List<Vector2I> CollectSelectedBattleSkillValidTargetCoordsTyped()
    {
        if (!IsBattleActive())
        {
            return new List<Vector2I>();
        }
        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (activeUnit == null || combatProfile == null)
        {
            return new List<Vector2I>();
        }
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDefinition)))
        {
            return new List<Vector2I>();
        }
        if (
            GetSelectionStage()
                == GameRuntimeBattleSelectionStage.SourceRetreatDirection
        )
        {
            return CollectSourceRetreatDirectionCoords(activeUnit);
        }
        if (GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit)
        {
            return CollectValidUnitSkillTargetCoords(
                activeUnit,
                skillDefinition,
                GetTargetUnitIdsStateTyped()
            );
        }
        if (combatProfile.TargetModeKind == BattleTargetMode.Unit)
        {
            return CollectValidUnitSkillTargetCoords(
                activeUnit,
                skillDefinition,
                GetTargetUnitIdsStateTyped()
            );
        }
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (castVariant == null || castVariant.TargetModeKind != BattleTargetMode.Ground)
        {
            return new List<Vector2I>();
        }
        return CollectValidGroundSkillTargetCoords(activeUnit, skillDefinition, castVariant);
    }

    private List<Vector2I> CollectSourceRetreatDirectionCoords(
        BattleUnitState activeUnit
    )
    {
        var result = new List<Vector2I>();
        BattleState battleState = GetBattleState();
        BattleUnitState targetUnit = ResolveQueuedSourceRetreatTarget();
        if (battleState == null || activeUnit == null || targetUnit == null)
            return result;

        Vector2I sourceCoord = activeUnit.GetAnchorCoord();
        Vector2I targetCoord = targetUnit.GetAnchorCoord();
        foreach (Vector2I direction in BattleSourceRetreatRules.CardinalDirections)
        {
            Vector2I selectionCoord = sourceCoord + direction;
            if (
                BattleSourceRetreatRules.IncreasesDistanceFromTarget(
                    sourceCoord,
                    targetCoord,
                    direction
                )
                && battleState.TryGetCellTyped(selectionCoord, out _)
            )
            {
                result.Add(selectionCoord);
            }
        }
        return SortCoordsTyped(result);
    }

    private List<Vector2I> CollectValidUnitSkillTargetCoords(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        IEnumerable<StringName> excludedUnitIds
    )
    {
        var coordSet = new HashSet<Vector2I>();
        BattleState battleState = GetBattleState();
        if (battleState == null || activeUnit == null || skillDefinition == null)
        {
            return new List<Vector2I>();
        }

        var excludedUnitIdSet = new HashSet<StringName>();
        foreach (StringName excludedUnitId in excludedUnitIds ?? Array.Empty<StringName>())
        {
            excludedUnitIdSet.Add(excludedUnitId);
        }
        bool useAnchorCoords =
            GetSelectedBattleSkillTargetSelectionModeKind(activeUnit) == BattleTargetSelectionMode.MultiUnit;
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        foreach (BattleUnitState targetUnit in battleState.GetUnitsTyped())
        {
            if (targetUnit == null || excludedUnitIdSet.Contains(targetUnit.unit_id))
            {
                continue;
            }
            if (!CanSkillTargetUnit(activeUnit, targetUnit, skillDefinition, castVariant))
            {
                continue;
            }
            if (useAnchorCoords)
            {
                coordSet.Add(targetUnit.GetAnchorCoord());
            }
            else
            {
                foreach (
                    Vector2I occupiedCoord
                    in targetUnit.GetOccupiedCoordsReadViewTyped()
                )
                {
                    coordSet.Add(occupiedCoord);
                }
            }
        }
        return SortCoordsTyped(coordSet);
    }

    private List<Vector2I> CollectValidGroundSkillTargetCoords(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var coordSet = new HashSet<Vector2I>();
        BattleState battleState = GetBattleState();
        if (
            battleState == null
            || activeUnit == null
            || skillDefinition == null
            || castVariant == null
        )
        {
            return new List<Vector2I>();
        }
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDefinition)))
        {
            return new List<Vector2I>();
        }

        List<Vector2I> queuedCoords = GetTargetCoordsStateTyped();
        foreach (BattleState.BattleCellEntry cellEntry in battleState.GetCellEntriesTyped())
        {
            Vector2I targetCoord = cellEntry.Coord;
            if (queuedCoords.Contains(targetCoord))
            {
                continue;
            }
            if (
                !IsNextGroundTargetCoordSelectable(
                    activeUnit,
                    skillDefinition,
                    castVariant,
                    queuedCoords,
                    targetCoord
                )
            )
            {
                continue;
            }
            coordSet.Add(targetCoord);
        }
        return SortCoordsTyped(coordSet);
    }

    private bool IsNextGroundTargetCoordSelectable(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<Vector2I> queuedCoords,
        Vector2I candidateCoord
    )
    {
        if (!string.IsNullOrEmpty(GetSkillCastBlockMessage(activeUnit, skillDefinition)))
        {
            return false;
        }
        var nextCoords = new List<Vector2I>(queuedCoords ?? Array.Empty<Vector2I>());
        nextCoords.Add(candidateCoord);
        if (
            !AreGroundTargetCoordsIndividuallyValid(
                activeUnit,
                skillDefinition,
                castVariant,
                nextCoords
            )
        )
        {
            return false;
        }
        int requiredCoordCount = Math.Max(castVariant.RequiredCoordCount, 1);
        if (nextCoords.Count >= requiredCoordCount)
        {
            return IsGroundTargetComboAllowed(
                activeUnit,
                skillDefinition,
                castVariant,
                nextCoords
            );
        }
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Unordered)
        {
            return true;
        }
        foreach (IReadOnlyList<Vector2I> fullCoords in BuildGroundCompletionSets(castVariant, nextCoords))
        {
            if (
                !AreGroundTargetCoordsIndividuallyValid(
                    activeUnit,
                    skillDefinition,
                    castVariant,
                    fullCoords
                )
            )
            {
                continue;
            }
            if (IsGroundTargetComboAllowed(activeUnit, skillDefinition, castVariant, fullCoords))
            {
                return true;
            }
        }
        return false;
    }

    private bool AreGroundTargetCoordsIndividuallyValid(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        BattleState battleState = GetBattleState();
        BattleGridService battleGridService = GetBattleGridService();
        if (
            battleState == null
            || battleGridService == null
            || activeUnit == null
            || skillDefinition?.CombatProfile == null
            || castVariant == null
        )
        {
            return false;
        }

        CombatEffectDefinition relocationEffectDefinition = ResolveGroundRelocationEffectDef(
            skillDefinition,
            castVariant
        );
        var seenCoords = new HashSet<Vector2I>();
        foreach (Vector2I coord in targetCoords ?? Array.Empty<Vector2I>())
        {
            if (!seenCoords.Add(coord))
            {
                return false;
            }
            int targetDistance =
                relocationEffectDefinition != null
                    ? battleGridService.GetChebyshevDistance(
                        activeUnit.GetAnchorCoord(),
                        coord
                    )
                    : battleGridService.GetDistanceFromUnitToCoord(activeUnit, coord);
            if (targetDistance > GetEffectiveSkillRange(activeUnit, skillDefinition))
            {
                return false;
            }
            if (!battleState.TryGetCellTyped(coord, out BattleCellState cell))
            {
                return false;
            }
            if (castVariant.AllowedBaseTerrains.Count > 0)
            {
                bool normalizedAllowed = false;
                StringName normalizedCellTerrain = BattleTerrainRules.NormalizeTerrainId(
                    cell.base_terrain
                );
                foreach (StringName allowedTerrain in castVariant.AllowedBaseTerrains)
                {
                    if (
                        BattleTerrainRules.NormalizeTerrainId(allowedTerrain)
                        == normalizedCellTerrain
                    )
                    {
                        normalizedAllowed = true;
                        break;
                    }
                }
                if (!normalizedAllowed)
                {
                    return false;
                }
            }
            if (
                IsCrownBreakSkill(skillDefinition)
                && !IsCrownBreakTargetEligible(activeUnit, GetRuntimeUnitAtCoord(coord))
            )
            {
                return false;
            }
            if (
                relocationEffectDefinition != null
                && !CanUseGroundRelocation(
                    battleState,
                    battleGridService,
                    activeUnit,
                    coord,
                    relocationEffectDefinition
                )
            )
            {
                return false;
            }
        }
        return true;
    }

    private static CombatEffectDefinition ResolveGroundRelocationEffectDef(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (castVariant != null)
        {
            foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions)
            {
                if (IsGroundRelocationEffect(effectDefinition))
                {
                    return effectDefinition;
                }
            }
        }
        if (skillDefinition?.CombatProfile != null)
        {
            foreach (CombatEffectDefinition effectDefinition in skillDefinition.CombatProfile.EffectDefinitions)
            {
                if (IsGroundRelocationEffect(effectDefinition))
                {
                    return effectDefinition;
                }
            }
        }
        return null;
    }

    private static bool IsGroundRelocationEffect(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null || effectDefinition.EffectKind != BattleEffectKind.ForcedMove)
        {
            return false;
        }
        return effectDefinition.ForcedMoveModeKind == BattleForcedMoveMode.Jump
            || effectDefinition.ForcedMoveModeKind == BattleForcedMoveMode.Blink;
    }

    private static bool CanUseGroundRelocation(
        BattleState battleState,
        BattleGridService battleGridService,
        BattleUnitState activeUnit,
        Vector2I coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null)
        {
            return false;
        }
        if (effectDefinition.ForcedMoveModeKind == BattleForcedMoveMode.Jump)
        {
            return battleGridService.CanJumpArc(battleState, activeUnit, coord, effectDefinition);
        }
        if (effectDefinition.ForcedMoveModeKind == BattleForcedMoveMode.Blink)
        {
            return battleGridService.CanBlinkToCoord(battleState, activeUnit, coord, effectDefinition);
        }
        return false;
    }

    private bool IsGroundTargetComboAllowed(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        if (activeUnit == null || skillDefinition == null || castVariant == null)
        {
            return false;
        }
        List<Vector2I> sortedTargetCoords = SortCoordsTyped(targetCoords);
        var skillCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillDefinition.SkillId),
            skill_id = skillDefinition.SkillId,
            skill_variant_id = castVariant.VariantId,
            target_coords = DuplicateVector2IArray(sortedTargetCoords),
        };
        if (sortedTargetCoords.Count > 0)
        {
            skillCommand.target_coord = sortedTargetCoords[sortedTargetCoords.Count - 1];
        }
        BattlePreview preview = PreviewBattleCommand(skillCommand);
        return preview != null && preview.allowed;
    }

    private IEnumerable<List<Vector2I>> BuildGroundCompletionSets(
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<Vector2I> partialCoords
    )
    {
        if (castVariant == null)
        {
            yield break;
        }
        int requiredCoordCount = Math.Max(castVariant.RequiredCoordCount, 1);
        if (partialCoords.Count > requiredCoordCount)
        {
            yield break;
        }
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Single)
        {
            if (partialCoords.Count == 1)
            {
                yield return SortCoordsTyped(partialCoords);
            }
            yield break;
        }
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Line2)
        {
            foreach (List<Vector2I> completionSet in BuildLine2CompletionSets(partialCoords))
            {
                yield return completionSet;
            }
            yield break;
        }
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Square2)
        {
            foreach (List<Vector2I> completionSet in BuildSquare2CompletionSets(partialCoords))
            {
                yield return completionSet;
            }
            yield break;
        }
        if (
            castVariant.FootprintPatternKind == CombatCastFootprintPattern.Unordered
            && partialCoords.Count == requiredCoordCount
        )
        {
            yield return SortCoordsTyped(partialCoords);
        }
    }

    private IEnumerable<List<Vector2I>> BuildLine2CompletionSets(IReadOnlyList<Vector2I> partialCoords)
    {
        var seenSignatures = new HashSet<string>();
        Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        foreach (Vector2I origin in partialCoords)
        {
            foreach (Vector2I direction in directions)
            {
                List<Vector2I> candidatePair = SortCoordsTyped(new[] { origin, origin + direction });
                if (!CoordArrayContainsAll(candidatePair, partialCoords))
                {
                    continue;
                }
                string signature = BuildCoordSignature(candidatePair);
                if (seenSignatures.Add(signature))
                {
                    yield return candidatePair;
                }
            }
        }
    }

    private IEnumerable<List<Vector2I>> BuildSquare2CompletionSets(IReadOnlyList<Vector2I> partialCoords)
    {
        var seenSignatures = new HashSet<string>();
        var candidateOrigins = new HashSet<Vector2I>();
        foreach (Vector2I coord in partialCoords)
        {
            foreach (
                Vector2I offset in new[]
                {
                    Vector2I.Zero,
                    Vector2I.Left,
                    Vector2I.Up,
                    new Vector2I(-1, -1),
                }
            )
            {
                candidateOrigins.Add(coord + offset);
            }
        }
        foreach (Vector2I origin in candidateOrigins)
        {
            List<Vector2I> blockCoords = SortCoordsTyped(
                new[]
                {
                    origin,
                    origin + Vector2I.Right,
                    origin + Vector2I.Down,
                    origin + Vector2I.One,
                }
            );
            if (!CoordArrayContainsAll(blockCoords, partialCoords))
            {
                continue;
            }
            string signature = BuildCoordSignature(blockCoords);
            if (seenSignatures.Add(signature))
            {
                yield return blockCoords;
            }
        }
    }

    private static bool CoordArrayContainsAll(
        IReadOnlyCollection<Vector2I> fullCoords,
        IEnumerable<Vector2I> partialCoords
    )
    {
        var coordSet = new HashSet<Vector2I>(fullCoords);
        foreach (Vector2I coord in partialCoords)
        {
            if (!coordSet.Contains(coord))
            {
                return false;
            }
        }
        return true;
    }

    private static string BuildCoordSignature(IEnumerable<Vector2I> targetCoords)
    {
        var segments = new List<string>();
        foreach (Vector2I coord in SortCoordsTyped(targetCoords))
        {
            segments.Add($"{coord.X}:{coord.Y}");
        }
        return string.Join("|", segments);
    }

    private bool CanSkillTargetUnit(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null
    )
    {
        if (activeUnit == null || targetUnit == null || skillDefinition?.CombatProfile == null)
        {
            return false;
        }
        if (!targetUnit.IsAlive())
        {
            return false;
        }
        return GetUnitSkillTargetAffordance(
                activeUnit,
                targetUnit,
                skillDefinition,
                castVariant
            )
            .Allowed;
    }

    private static bool IsCrownBreakSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition != null
            && ProgressionDataUtils.to_string_name(skillDefinition.SkillId) == CrownBreakSkillId;
    }

    private static bool IsDoomSentenceSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition != null
            && ProgressionDataUtils.to_string_name(skillDefinition.SkillId) == DoomSentenceSkillId;
    }

    private static bool IsDoomShiftSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition != null
            && ProgressionDataUtils.to_string_name(skillDefinition.SkillId) == DoomShiftSkillId;
    }

    private static bool IsBlackCrownSealSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition != null
            && ProgressionDataUtils.to_string_name(skillDefinition.SkillId) == BlackCrownSealSkillId;
    }

    private static bool IsCrownBreakTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        return targetUnit != null
            && SkillTargetFilterMatchesUnit(activeUnit, targetUnit, EnemyFilter)
            && targetUnit.HasStatusEffect(StatusBlackStarBrandElite);
    }

    private static bool IsDoomSentenceTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        return targetUnit != null
            && SkillTargetFilterMatchesUnit(activeUnit, targetUnit, EnemyFilter)
            && IsEliteOrBossTarget(targetUnit);
    }

    private static bool IsBlackCrownSealTargetEligible(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit
    )
    {
        return targetUnit != null
            && SkillTargetFilterMatchesUnit(activeUnit, targetUnit, EnemyFilter)
            && IsBossTarget(targetUnit);
    }

    private static bool IsEliteOrBossTarget(BattleUnitState targetUnit)
    {
        return GetAttributeValue(targetUnit, FortuneMarkTargetStatId) > 0;
    }

    private static bool IsBossTarget(BattleUnitState targetUnit)
    {
        return GetAttributeValue(targetUnit, BossTargetStatId) > 0
            || GetAttributeValue(targetUnit, FortuneMarkTargetStatId) > 1;
    }

    private static int GetAttributeValue(BattleUnitState unitState, StringName attributeId)
    {
        return unitState?.attribute_snapshot is AttributeSnapshot snapshot
            ? snapshot.GetValue(attributeId)
            : 0;
    }

    private static bool SkillTargetFilterMatchesUnit(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        StringName targetTeamFilter
    )
    {
        return BattleTargetTeamRules.IsUnitValidForFilter(
            activeUnit,
            targetUnit,
            targetTeamFilter,
            default
        );
    }

    private int GetEffectiveSkillRange(BattleUnitState activeUnit, SkillDefinition skillDefinition)
    {
        return BattleRangeService.GetEffectiveSkillRange(
            activeUnit,
            skillDefinition,
            GetSkillCatalog()
        );
    }

    private string GetSkillCastBlockMessage(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        if (Port != null)
        {
            return Port.GetBattleSkillCastBlockMessage(
                activeUnit,
                skillDefinition?.SkillId ?? ""
            );
        }
        return "正式技能检查未绑定，无法施放该技能。";
    }

    private int GetUnitSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
        {
            return 0;
        }
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId, int.MinValue);
        if (knownSkillLevel != int.MinValue)
        {
            return knownSkillLevel;
        }
        SkillDefinition skillDefinition = GetSkillDefinition(skillId);
        if (IsLevelLessSkill(skillDefinition))
        {
            return 0;
        }
        return unitState.KnowsActiveSkill(skillId) ? 1 : 0;
    }

    private CombatEffectDefinition GetSourceRetreatEffect(
        BattleUnitState unitState,
        SkillDefinition skillDefinition
    )
    {
        if (unitState == null || skillDefinition?.CombatProfile == null)
            return null;
        int skillLevel = GetUnitSkillLevel(unitState, skillDefinition.SkillId);
        foreach (
            CombatEffectDefinition effectDefinition
            in skillDefinition.CombatProfile.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind != BattleEffectKind.SourceRetreat
                || skillLevel < Math.Max(effectDefinition.MinSkillLevel, 0)
                || (
                    effectDefinition.MaxSkillLevel >= 0
                    && skillLevel > effectDefinition.MaxSkillLevel
                )
            )
            {
                continue;
            }
            return effectDefinition;
        }
        return null;
    }

    private SkillEffectiveCombatDefinition GetEffectiveCombatProfileForUnit(
        BattleUnitState unitState,
        SkillDefinition skillDefinition
    )
    {
        if (skillDefinition == null)
        {
            return SkillEffectiveCombatDefinition.BuildMissing(0);
        }
        int skillLevel = GetUnitSkillLevel(unitState, skillDefinition.SkillId);
        ISkillCatalog skillCatalog = GetSkillCatalog();
        return skillCatalog != null
            ? skillCatalog.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            : SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
    }

    private static bool IsLevelLessSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition != null
            && skillDefinition.MaxLevel == 0
            && skillDefinition.DynamicMaxLevelStatId == "";
    }

    private string BuildBattleSkillSelectionStatus(
        SkillDefinition skillDefinition,
        BattleUnitState activeUnit
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (skillDefinition == null || combatProfile == null)
        {
            return "当前技能不可用。";
        }

        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDefinition);
        if (!string.IsNullOrEmpty(blockReason))
        {
            return $"{blockReason}按 Esc 清除选择。";
        }
        if (
            combatProfile.Windup != null
            && BattleWindupRules.TryBuildQuote(
                activeUnit,
                skillDefinition,
                GetSelectedWindupTier(),
                out BattleWindupQuote windupQuote,
                out _,
                requireAffordable: false
            )
        )
        {
            return
                $"已选择 {skillDefinition.DisplayName}：蓄力 {windupQuote.Tier} 挡，{windupQuote.TotalWindupTu} TU，{windupQuote.TotalStaminaCost} 体力，{windupQuote.WeaponDiceMultiplier}W。Q/E 调整挡位，左键选择目标；确认后不能主动取消，Esc 仅清除尚未确认的选择。";
        }
        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        StringName selectionMode = GetSelectedBattleSkillTargetSelectionMode(activeUnit);
        BattleTargetSelectionMode selectionModeKind = GetSelectedBattleSkillTargetSelectionModeKind(
            activeUnit
        );
        if (selectionModeKind == BattleTargetSelectionMode.RandomChain)
        {
            return $"已选择技能 {skillDefinition.DisplayName}，将自动攻击范围内随机敌军。Esc 清除选择。";
        }
        if (selectionModeKind == BattleTargetSelectionMode.MultiUnit)
        {
            int minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
            SkillEffectiveCombatDefinition effectiveProfile = GetEffectiveCombatProfileForUnit(
                activeUnit,
                skillDefinition
            );
            int maxTargetCount = Math.Max(
                effectiveProfile.MaxTargetCount,
                minTargetCount
            );
            return BuildMultiUnitTargetStatus(skillDefinition, minTargetCount, maxTargetCount);
        }
        if (castVariant == null)
        {
            if (
                combatProfile.TargetModeKind == BattleTargetMode.Unit
                && (
                    selectionMode == SelfSelectionMode
                    || combatProfile.TargetTeamFilter == SelfSelectionMode
                )
            )
            {
                return $"已选择技能 {skillDefinition.DisplayName}。点击自身即可施放，Esc 清除选择。";
            }
            return $"已选择技能 {skillDefinition.DisplayName}。左键选择目标单位施放，Esc 清除选择。";
        }
        if (combatProfile.TargetModeKind == BattleTargetMode.Unit)
        {
            if (
                selectionMode == SelfSelectionMode
                || combatProfile.TargetTeamFilter == SelfSelectionMode
            )
            {
                return $"已选择 {BuildSkillVariantDisplayName(skillDefinition, castVariant)}，点击自身即可施放，Esc 清除选择。";
            }
            return $"已选择 {BuildSkillVariantDisplayName(skillDefinition, castVariant)}，左键选择目标单位施放，Esc 清除选择。";
        }
        return $"已选择 {BuildSkillVariantDisplayName(skillDefinition, castVariant)}，需目标 {castVariant.RequiredCoordCount} 格。左键逐格选点，Q/E 切换形态，Esc 清除选择。";
    }

    private static string BuildSkillVariantDisplayName(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (skillDefinition == null)
        {
            return "技能";
        }
        if (castVariant == null || string.IsNullOrEmpty(castVariant.DisplayName))
        {
            return skillDefinition.DisplayName;
        }
        return $"{skillDefinition.DisplayName}·{castVariant.DisplayName}";
    }

    private BattleRefreshMode ToggleSelectedMultiUnitSkillTarget(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (activeUnit == null || combatProfile == null)
        {
            return BattleRefreshMode.Overlay;
        }
        string blockReason = GetSkillCastBlockMessage(activeUnit, skillDefinition);
        if (!string.IsNullOrEmpty(blockReason))
        {
            RefreshBattleSelectionState();
            UpdateStatus(blockReason);
            return BattleRefreshMode.Overlay;
        }

        int minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
        SkillEffectiveCombatDefinition effectiveProfile = GetEffectiveCombatProfileForUnit(
            activeUnit,
            skillDefinition
        );
        int maxTargetCount = Math.Max(
            effectiveProfile.MaxTargetCount,
            minTargetCount
        );
        List<StringName> queuedTargetUnitIds = GetTargetUnitIdsStateTyped();
        if (targetUnit == null)
        {
            if (queuedTargetUnitIds.Count >= minTargetCount)
            {
                return IssueSelectedMultiUnitSkill(activeUnit, skillDefinition);
            }
            RefreshBattleSelectionState();
            UpdateStatus(BuildMultiUnitTargetStatus(skillDefinition, minTargetCount, maxTargetCount));
            return BattleRefreshMode.Overlay;
        }

        StringName targetUnitId = targetUnit.unit_id;
        int existingIndex = queuedTargetUnitIds.IndexOf(targetUnitId);
        bool allowRepeat = combatProfile.AllowRepeatTarget;
        int maxHitsPerTarget = Math.Max(combatProfile.MaxHitsPerTarget, 0);
        int existingCount = 0;
        foreach (StringName queuedId in queuedTargetUnitIds)
        {
            if (queuedId == targetUnitId)
            {
                existingCount += 1;
            }
        }

        if (existingIndex >= 0 && !allowRepeat)
        {
            queuedTargetUnitIds.RemoveAt(existingIndex);
            SetTargetUnitIdsStateTyped(queuedTargetUnitIds);
            RefreshSelectedUnitTargetCoordsFromQueue();
            SyncMultiUnitConfirmFocus(activeUnit, minTargetCount, maxTargetCount);
            RefreshBattleSelectionState();
            UpdateStatus(BuildMultiUnitTargetStatus(skillDefinition, minTargetCount, maxTargetCount));
            return BattleRefreshMode.Overlay;
        }

        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        BattleUnitSkillTargetAffordance affordance = GetUnitSkillTargetAffordance(
            activeUnit,
            targetUnit,
            skillDefinition,
            castVariant
        );
        if (!affordance.Allowed)
        {
            if (
                targetUnit.unit_id == activeUnit.unit_id
                && queuedTargetUnitIds.Count >= minTargetCount
            )
            {
                return IssueSelectedMultiUnitSkill(activeUnit, skillDefinition);
            }
            RefreshBattleSelectionState();
            UpdateStatus(
                string.IsNullOrEmpty(affordance.Reason)
                    ? "该单位不是当前技能的合法目标。"
                    : affordance.Reason
            );
            return BattleRefreshMode.Overlay;
        }
        if (maxHitsPerTarget > 0 && existingCount >= maxHitsPerTarget)
        {
            UpdateStatus($"该目标已达到最大命中次数限制 ({maxHitsPerTarget} 次)。");
            return BattleRefreshMode.Overlay;
        }
        if (queuedTargetUnitIds.Count >= maxTargetCount)
        {
            string hint = allowRepeat ? "按 Esc 清除选择。" : "点击已选目标可取消。";
            UpdateStatus($"该技能最多选择 {maxTargetCount} 个单位目标；{hint}");
            return BattleRefreshMode.Overlay;
        }

        queuedTargetUnitIds.Add(targetUnitId);
        SetTargetUnitIdsStateTyped(queuedTargetUnitIds);
        RefreshSelectedUnitTargetCoordsFromQueue();
        if (queuedTargetUnitIds.Count >= maxTargetCount)
        {
            return IssueSelectedMultiUnitSkill(activeUnit, skillDefinition);
        }
        SyncMultiUnitConfirmFocus(activeUnit, minTargetCount, maxTargetCount);
        RefreshBattleSelectionState();
        UpdateStatus(BuildMultiUnitTargetStatus(skillDefinition, minTargetCount, maxTargetCount));
        return BattleRefreshMode.Overlay;
    }

    private BattleRefreshMode IssueSelectedMultiUnitSkill(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        if (activeUnit == null || skillDefinition == null)
        {
            return BattleRefreshMode.Overlay;
        }

        var skillCommand = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_entry_id = GetSelectedSkillEntryId(),
            skill_id = GetSelectedSkillId(),
            skill_variant_id = GetSelectedSkillVariantId(),
            windup_tier = GetSelectedWindupTier(),
            target_unit_ids = DuplicateStringNameArray(GetTargetUnitIdsStateTyped()),
        };
        if (skillCommand.target_unit_ids.Count > 0)
        {
            BattleUnitState firstTarget = GetBattleUnitById(skillCommand.target_unit_ids[0]);
            if (firstTarget != null)
            {
                skillCommand.target_coord = firstTarget.GetAnchorCoord();
            }
        }

        BattlePreview preview = PreviewBattleCommand(skillCommand);
        if (preview != null && preview.allowed)
        {
            return IssueBattleCommand(skillCommand);
        }
        RefreshBattleSelectionState();
        if (preview != null && preview.LogLinesTyped.Count > 0)
        {
            UpdateStatus(preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]);
        }
        else
        {
            UpdateStatus("当前单位技能目标无效。");
        }
        return BattleRefreshMode.Overlay;
    }

    private void SyncMultiUnitConfirmFocus(
        BattleUnitState activeUnit,
        int minTargetCount,
        int maxTargetCount
    )
    {
        if (activeUnit == null)
        {
            return;
        }
        int selectedCount = GetTargetUnitIdsStateTyped().Count;
        if (selectedCount >= minTargetCount && selectedCount < maxTargetCount)
        {
            SetBattleSelectedCoord(activeUnit.GetAnchorCoord());
        }
    }

    private string BuildMultiUnitTargetStatus(
        SkillDefinition skillDefinition,
        int minTargetCount,
        int maxTargetCount
    )
    {
        int selectedCount = GetTargetUnitIdsStateTyped().Count;
        string title = skillDefinition?.DisplayName ?? "技能";
        bool allowRepeat =
            skillDefinition?.CombatProfile != null
            && skillDefinition.CombatProfile.AllowRepeatTarget;
        string cancelHint = allowRepeat ? "点击已选目标可追加" : "点击已选目标可取消";
        if (selectedCount <= 0)
        {
            return $"已选择技能 {title}。左键逐个点选单位目标，{cancelHint}，Esc 清除选择。";
        }
        if (selectedCount < minTargetCount)
        {
            return $"已选择 {title}，已选择 {selectedCount} / {minTargetCount} 个单位目标。继续点选，{cancelHint}，Esc 清除选择。";
        }
        if (selectedCount < maxTargetCount)
        {
            return $"已选择 {title}，已选择 {selectedCount} / {maxTargetCount} 个单位目标。还可继续添加，{cancelHint}，Esc 清除选择。";
        }
        string maxHint = allowRepeat ? "按 Esc 清除选择。" : "点击已选目标可取消，Esc 清除选择。";
        return $"已选择 {title}，已选择 {selectedCount} / {maxTargetCount} 个单位目标。已达到上限，{maxHint}";
    }

    private void RefreshSelectedUnitTargetCoordsFromQueue()
    {
        var targetCoords = new List<Vector2I>();
        BattleState battleState = GetBattleState();
        if (battleState == null)
        {
            SetTargetCoordsStateTyped(targetCoords);
            return;
        }
        foreach (StringName targetUnitId in GetTargetUnitIdsStateTyped())
        {
            BattleUnitState targetUnit = GetBattleUnitById(targetUnitId);
            if (targetUnit != null)
            {
                targetCoords.Add(targetUnit.GetAnchorCoord());
            }
        }
        SetTargetCoordsStateTyped(SortCoordsTyped(targetCoords));
    }

    private List<Vector2I> CollectSelectedBattleSkillTargetCoordsTyped()
    {
        if (GetTargetUnitIdsStateTyped().Count > 0)
        {
            RefreshSelectedUnitTargetCoordsFromQueue();
        }

        BattleUnitState activeUnit = GetManualActiveUnit();
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        List<Vector2I> targetCoords = GetTargetCoordsStateTyped();
        if (activeUnit == null || combatProfile == null)
        {
            return new List<Vector2I>(targetCoords);
        }

        CombatCastVariantDefinition castVariant = GetSelectedBattleSkillVariant(activeUnit);
        if (combatProfile.TargetModeKind == BattleTargetMode.Ground)
        {
            if (castVariant == null || castVariant.TargetModeKind != BattleTargetMode.Ground)
            {
                return new List<Vector2I>(targetCoords);
            }
            if (targetCoords.Count < Math.Max(castVariant.RequiredCoordCount, 1))
            {
                return new List<Vector2I>(targetCoords);
            }
        }

        int skillLevel = GetUnitSkillLevel(activeUnit, skillDefinition.SkillId);
        BattleTargetCollectionResult collectedTargetCoords =
            _targetCollectionService.CollectCombatProfileTargetCoords(
                GetBattleState(),
                GetBattleGridService(),
                activeUnit.GetAnchorCoord(),
                combatProfile,
                targetCoords,
                activeUnit,
                CollectSelectedTargetUnits(activeUnit, skillDefinition),
                skillLevel
            );
        if (collectedTargetCoords.Handled)
        {
            return SortCoordsTyped(collectedTargetCoords.TargetCoords);
        }
        return new List<Vector2I>(targetCoords);
    }

    private List<BattleUnitState> CollectSelectedTargetUnits(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        var targetUnits = new List<BattleUnitState>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (activeUnit == null || combatProfile == null)
        {
            return targetUnits;
        }
        foreach (StringName targetUnitId in GetTargetUnitIdsStateTyped())
        {
            BattleUnitState targetUnit = GetBattleUnitById(targetUnitId);
            if (targetUnit != null)
            {
                targetUnits.Add(targetUnit);
            }
        }
        if (targetUnits.Count > 0)
        {
            return targetUnits;
        }
        if (
            GetSelectedBattleSkillTargetSelectionMode(activeUnit) == SelfSelectionMode
            || combatProfile.TargetTeamFilter == SelfSelectionMode
            || combatProfile.AreaPattern == SelfSelectionMode
        )
        {
            targetUnits.Add(activeUnit);
        }
        return targetUnits;
    }

    private void ClearBattleSkillTargetSelection()
    {
        ClearTargetCoordsState();
        ClearTargetUnitIdsState();
        SetSelectionStage(GameRuntimeBattleSelectionStage.Target);
    }

    private StringName GetSelectedBattleSkillTargetSelectionMode(BattleUnitState activeUnit)
    {
        SkillDefinition skillDefinition = GetSelectedBattleSkillDefinition(activeUnit);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return BattleTypedNames.TargetSelectionSingleUnit;
        }
        StringName selectionMode = BattleTypedNames.ToStringName(combatProfile.TargetSelectionModeKind);
        return selectionMode == "" ? BattleTypedNames.TargetSelectionSingleUnit : selectionMode;
    }

    private BattleTargetSelectionMode GetSelectedBattleSkillTargetSelectionModeKind(
        BattleUnitState activeUnit
    )
    {
        return BattleTypedNames.ToTargetSelectionMode(
            GetSelectedBattleSkillTargetSelectionMode(activeUnit)
        );
    }

    private BattleUnitState GetManualActiveUnit()
    {
        return Port?.GetManualBattleUnit();
    }

    private BattleUnitState GetRuntimeActiveUnit()
    {
        return Port?.GetRuntimeBattleActiveUnit();
    }

    private BattleUnitState GetRuntimeUnitAtCoord(Vector2I coord)
    {
        return Port?.GetRuntimeBattleUnitAtCoord(coord);
    }

    private BattleUnitState GetBattleUnitById(StringName unitId)
    {
        return Port?.GetRuntimeBattleUnitById(unitId);
    }

    private BattleState GetBattleState()
    {
        return Port?.GetBattleState();
    }

    private BattleGridService GetBattleGridService()
    {
        return Port?.GetBattleGridService();
    }

    private ISkillCatalog GetSkillCatalog()
    {
        return Port?.GetSkillCatalog();
    }

    private IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindings()
    {
        return Port?.GetEquipmentAbilityBindings();
    }

    private BattleSkillAvailabilityView BuildSkillAvailabilityView(
        BattleUnitState activeUnit,
        BattleSkillAvailabilityConsumer consumer
    )
    {
        BattleSkillAvailabilityService service = new(
            GetSkillCatalog(),
            equipmentAbilityBindings: GetEquipmentAbilityBindings()
        );
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = activeUnit,
                Consumer = consumer,
                IncludeEquipmentSkills = true,
                WorldStep = GetBattleWorldStep(),
                BattleState = GetBattleState(),
            }
        );
    }

    private bool TryGetAvailableSkillEntryBySlot(
        BattleUnitState activeUnit,
        int index,
        BattleSkillAvailabilityConsumer consumer,
        out BattleAvailableSkillEntry entry
    )
    {
        BattleSkillAvailabilityService service = new(
            GetSkillCatalog(),
            equipmentAbilityBindings: GetEquipmentAbilityBindings()
        );
        return service.TryGetSkillEntryBySlot(
            new BattleSkillAvailabilityQuery
            {
                User = activeUnit,
                Consumer = consumer,
                IncludeEquipmentSkills = true,
                WorldStep = GetBattleWorldStep(),
                BattleState = GetBattleState(),
            },
            index,
            out entry
        );
    }

    private bool TryResolveAvailableSkillEntry(
        BattleUnitState activeUnit,
        StringName skillEntryId,
        BattleSkillAvailabilityConsumer consumer,
        out BattleAvailableSkillEntry entry
    )
    {
        BattleSkillAvailabilityService service = new(
            GetSkillCatalog(),
            equipmentAbilityBindings: GetEquipmentAbilityBindings()
        );
        return service.TryResolveSkillEntry(
            new BattleSkillAvailabilityQuery
            {
                User = activeUnit,
                Consumer = consumer,
                IncludeEquipmentSkills = true,
                WorldStep = GetBattleWorldStep(),
                BattleState = GetBattleState(),
            },
            skillEntryId,
            out entry
        );
    }

    private int GetBattleWorldStep() => Port?.GetBattleWorldStep() ?? -1;

    private BattlePreview PreviewBattleCommand(BattleCommand command)
    {
        return Port?.PreviewBattleCommand(command);
    }

    private BattleUnitSkillTargetAffordance GetUnitSkillTargetAffordance(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        bool requireAp = true
    )
    {
        if (activeUnit == null || targetUnit == null || skillDefinition?.CombatProfile == null)
        {
            return BattleUnitSkillTargetAffordance.Denied("技能目标无效。");
        }
        string blockReason = requireAp
            ? GetSkillCastBlockMessage(activeUnit, skillDefinition)
            : "";
        if (!string.IsNullOrEmpty(blockReason))
        {
            return BattleUnitSkillTargetAffordance.Denied(blockReason);
        }
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = activeUnit.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillDefinition.SkillId),
            skill_id = skillDefinition.SkillId,
            skill_variant_id =
                castVariant?.VariantId ?? GetDefaultUnitSkillVariantId(activeUnit, skillDefinition),
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.GetAnchorCoord(),
        };
        if (GetSourceRetreatEffect(activeUnit, skillDefinition) != null)
        {
            foreach (
                Vector2I direction
                in BattleSourceRetreatRules.CardinalDirections
            )
            {
                if (
                    !BattleSourceRetreatRules.IncreasesDistanceFromTarget(
                        activeUnit.GetAnchorCoord(),
                        targetUnit.GetAnchorCoord(),
                        direction
                    )
                )
                {
                    continue;
                }
                command.source_retreat_direction = direction;
                break;
            }
        }
        BattlePreview preview = PreviewBattleCommand(command);
        if (preview != null && preview.allowed)
        {
            return BattleUnitSkillTargetAffordance.AllowedResult();
        }
        string reason =
            preview != null && preview.LogLinesTyped.Count > 0
                ? preview.LogLinesTyped[preview.LogLinesTyped.Count - 1]
                : "技能目标无效。";
        return BattleUnitSkillTargetAffordance.Denied(reason);
    }

    private BattleRefreshMode IssueBattleCommand(BattleCommand command)
    {
        return Port?.IssueBattleCommand(command) ?? BattleRefreshMode.Overlay;
    }

    private void RefreshBattleSelectionState()
    {
        Port?.RefreshBattleSelectionState();
    }

    private void UpdateStatus(string message)
    {
        Port?.UpdateStatus(message);
    }

    private string FormatCoord(Vector2I coord)
    {
        return Port?.FormatCoord(coord) ?? $"({coord.X},{coord.Y})";
    }

    private bool IsBattleActive()
    {
        return Port?.IsBattleActive() ?? false;
    }

    private StringName GetSelectedSkillId()
    {
        return Port?.GetSelectedSkillId() ?? new StringName("");
    }

    private StringName GetSelectedSkillEntryId()
    {
        return Port?.GetSelectedSkillEntryId() ?? new StringName("");
    }

    private void SetSelectedSkillEntryId(StringName skillEntryId)
    {
        Port?.SetSelectedSkillEntryId(skillEntryId);
    }

    private void SetSelectedSkillId(StringName skillId)
    {
        Port?.SetSelectedSkillId(skillId);
    }

    private StringName GetSelectedSkillVariantId()
    {
        return Port?.GetSelectedSkillVariantId() ?? new StringName("");
    }

    private void SetSelectedSkillVariantId(StringName optionId)
    {
        Port?.SetSelectedSkillVariantId(optionId);
    }

    private int GetSelectedWindupTier() =>
        Port?.GetSelectedWindupTier() ?? 1;

    private void SetSelectedWindupTier(int tier) =>
        Port?.SetSelectedWindupTier(tier);

    private GameRuntimeBattleSelectionStage GetSelectionStage() =>
        Port?.GetSelectionStage() ?? GameRuntimeBattleSelectionStage.Target;

    private void SetSelectionStage(GameRuntimeBattleSelectionStage stage) =>
        Port?.SetSelectionStage(stage);

    private StringName GetLastManualUnitId()
    {
        return Port?.GetLastManualUnitId() ?? new StringName("");
    }

    private void SetLastManualUnitId(StringName unitId)
    {
        Port?.SetLastManualUnitId(unitId);
    }

    private List<Vector2I> GetTargetCoordsStateTyped()
    {
        if (Port == null)
        {
            return new List<Vector2I>();
        }
        return new List<Vector2I>(Port.GetTargetCoords());
    }

    private void SetTargetCoordsStateTyped(IEnumerable<Vector2I> targetCoords)
    {
        Port?.SetTargetCoords(targetCoords ?? Array.Empty<Vector2I>());
    }

    private void ClearTargetCoordsState()
    {
        SetTargetCoordsStateTyped(Array.Empty<Vector2I>());
    }

    private List<StringName> GetTargetUnitIdsStateTyped()
    {
        if (Port == null)
        {
            return new List<StringName>();
        }
        return new List<StringName>(Port.GetTargetUnitIds());
    }

    private void SetTargetUnitIdsStateTyped(IEnumerable<StringName> targetUnitIds)
    {
        Port?.SetTargetUnitIds(targetUnitIds ?? Array.Empty<StringName>());
    }

    private void ClearTargetUnitIdsState()
    {
        SetTargetUnitIdsStateTyped(Array.Empty<StringName>());
    }

    private void SetBattleSelectedCoord(Vector2I coord)
    {
        Port?.SetBattleSelectedCoord(coord);
    }

    string IBattleSelectionSessionSurface.GetSelectedBattleSkillName() =>
        GetSelectedBattleSkillName();

    string IBattleSelectionSessionSurface.GetSelectedBattleSkillVariantName() =>
        GetSelectedBattleSkillVariantName();

    IReadOnlyList<Vector2I>
        IBattleSelectionSessionSurface.GetSelectedBattleSkillTargetCoordsSnapshotPlain() =>
        GetSelectedBattleSkillTargetCoordsSnapshotPlain();

    IReadOnlyList<StringName>
        IBattleSelectionSessionSurface.GetSelectedBattleSkillTargetUnitIdsSnapshotPlain() =>
        GetSelectedBattleSkillTargetUnitIdsSnapshotPlain();

    IReadOnlyList<Vector2I>
        IBattleSelectionSessionSurface.GetSelectedBattleSkillValidTargetCoordsSnapshotPlain() =>
        GetSelectedBattleSkillValidTargetCoordsSnapshotPlain();

    int IBattleSelectionSessionSurface.GetSelectedBattleSkillRequiredCoordCount() =>
        GetSelectedBattleSkillRequiredCoordCount();

    BattlePreview IBattleSelectionSessionSurface.GetSelectedBattleSkillPreview() =>
        GetSelectedBattleSkillPreview();

    BattlePreview IBattleSelectionSessionSurface.PreviewSelectedBattleSkillAtCoord(
        Vector2I coord
    ) => PreviewSelectedBattleSkillAtCoord(coord);

    BattleSelectionCommandResult IBattleSelectionSessionSurface.SelectBattleSkillSlotTyped(
        int index
    ) => SelectBattleSkillSlotTyped(index);

    void IBattleSelectionSessionSurface.CycleSelectedBattleSkillOption(int step) =>
        CycleSelectedBattleSkillOption(step);

    void IBattleSelectionSessionSurface.ClearBattleSkillSelection(bool announce) =>
        ClearBattleSkillSelection(announce);

    BattleRefreshMode IBattleSelectionSessionSurface.AttemptBattleMoveTo(
        Vector2I targetCoord
    ) => AttemptBattleMoveTo(targetCoord);

    BattleRefreshMode IBattleSelectionSessionSurface.ResetBattleMovement() =>
        ResetBattleMovement();

    void IBattleSelectionSessionSurface.SyncSelectedBattleSkillState() =>
        SyncSelectedBattleSkillState();

    private static BattleSelectionCommandResult SelectionOkTyped()
    {
        return BattleSelectionCommandResult.Success();
    }

    private static BattleSelectionCommandResult SelectionErrorTyped(string message)
    {
        return BattleSelectionCommandResult.Failure(message);
    }

    private static int PosMod(int value, int modulo)
    {
        return modulo <= 0 ? 0 : ((value % modulo) + modulo) % modulo;
    }

    private static List<Vector2I> SortCoordsTyped(IEnumerable<Vector2I> targetCoords)
    {
        var coords = new List<Vector2I>();
        if (targetCoords != null)
        {
            coords.AddRange(targetCoords);
        }
        coords.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return coords;
    }

    private static Vector2IList DuplicateVector2IArray(IEnumerable<Vector2I> values) =>
        new(values);

    private static StringNameList DuplicateStringNameArray(IEnumerable<StringName> values) =>
        new(values);

    private static IGameRuntimeBattleSelectionPort ResolveWeakRef(
        WeakReference<IGameRuntimeBattleSelectionPort> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimeBattleSelectionPort target)
        )
        {
            return null;
        }
        return target;
    }
}
