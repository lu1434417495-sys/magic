using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleCommandPreviewService
{
    private WeakReference<IBattleCommandPreviewRuntimePort> _runtimeRef;

    private IBattleCommandPreviewRuntimePort Runtime =>
        _runtimeRef != null && _runtimeRef.TryGetTarget(out IBattleCommandPreviewRuntimePort port)
            ? port
            : null;

    internal void Setup(IBattleCommandPreviewRuntimePort runtime)
    {
        _runtimeRef =
            runtime != null ? new WeakReference<IBattleCommandPreviewRuntimePort>(runtime) : null;
    }

    internal void DisposeRuntime()
    {
        _runtimeRef = null;
    }

    public BattlePreview PreviewCommand(BattleCommand command)
    {
        Runtime.EnsureSidecarsReady();
        var preview = new BattlePreview();
        if (!CanPreviewCommand(command))
            return preview;
        if (Runtime.GetBattleState().ModalStateKind != BattleModalStateKind.None)
        {
            preview.AddLogLine(_get_battle_interaction_block_message());
            return preview;
        }
        if (command.IsCancelCast())
        {
            Runtime.PreviewCancelCast(command, preview);
            return preview;
        }

        BattleUnitReadView activeUnit = ResolvePreviewActiveUnit(command);
        if (!activeUnit.IsValid || !activeUnit.IsAlive)
            return preview;

        if (command.IsMove())
            PreviewMoveCommand(activeUnit, command, preview);
        else if (command.IsSkill())
            PreviewSkillCommand(activeUnit, command, preview);
        else if (command.IsWait())
            PreviewWaitCommand(activeUnit, preview);
        else if (command.IsInteract())
            Runtime.PreviewObjectiveInteraction(activeUnit, command, preview);
        else if (command.IsChangeEquipment())
            PreviewChangeEquipmentCommand(activeUnit, command, preview);
        else
            PreviewUnknownCommand(preview);
        return preview;
    }

    private bool CanPreviewCommand(BattleCommand command)
    {
        BattleState state = Runtime?.GetBattleState();
        return state != null && command != null && state.PhaseKind != BattlePhaseKind.BattleEnded;
    }

    private BattleUnitReadView ResolvePreviewActiveUnit(BattleCommand command)
    {
        return command != null
            ? Runtime.GetBattleState().AsReadView().GetUnit(command.unit_id)
            : default;
    }

    private void PreviewMoveCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:move");
        if (Runtime.IsMovementBlocked(activeUnit))
        {
            preview.AddLogLine($"{activeUnit.DisplayName} 当前被限制移动。");
            return;
        }

        BattleMovePathResult moveResult;
        using (new BattleAiTraceSpan("preview:move.resolve_path_result"))
        {
            moveResult = Runtime.ResolveMovePathResult(activeUnit, command.target_coord);
        }

        using (new BattleAiTraceSpan("preview:move.build_preview"))
        {
            ApplyMovePreviewResult(activeUnit, command, preview, moveResult);
        }
    }

    private void ApplyMovePreviewResult(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview,
        BattleMovePathResult moveResult
    )
    {
        if (!moveResult.Allowed)
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(moveResult.Message) ? "该移动不可执行。" : moveResult.Message
            );
            return;
        }

        preview.allowed = true;
        preview.move_cost = moveResult.Cost;
        preview.resolved_anchor_coord = command.target_coord;
        preview.AddLogLine(
            $"移动可执行，距离消耗 {moveResult.Cost} 点移动力，执行后锁定剩余移动力。"
        );
        AddPreviewFootprintCoords(preview, activeUnit, command.target_coord);
    }

    private void AddPreviewFootprintCoords(
        BattlePreview preview,
        BattleUnitReadView activeUnit,
        Vector2I anchorCoord
    )
    {
        foreach (Vector2I targetCoord in Runtime.GetUnitFootprintCoords(activeUnit, anchorCoord))
            preview.AddTargetCoord(targetCoord);
    }

    private void PreviewSkillCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:skill");
        if (activeUnit.TurnCastingExhausted)
        {
            preview.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            return;
        }
        BattleSkillAccessResult accessResult = ValidateSkillCommandEntryAccess(
            command,
            BattleSkillAvailabilityConsumer.PreviewExecution
        );
        if (!accessResult.Allowed)
        {
            preview.AddLogLine(accessResult.Message);
            return;
        }
        Runtime.PreviewSkillCommand(activeUnit, command, preview);
        if (preview.allowed)
            ProjectEquipmentGrantedSkillReaction(command, accessResult.Entry, preview);
    }

    private void ProjectEquipmentGrantedSkillReaction(
        BattleCommand command,
        BattleAvailableSkillEntry entry,
        BattlePreview preview
    )
    {
        BattleState state = Runtime?.GetBattleState();
        if (
            command == null
            || entry?.EntryRef == null
            || entry.EquipmentBindingId == ""
            || entry.EquipmentGrantedActionId == ""
            || state == null
        )
        {
            return;
        }
        if (
            !state.TryGetUnitTyped(command.unit_id, out BattleUnitState canonicalSource)
            || canonicalSource == null
        )
        {
            return;
        }

        BattleDetachedPreviewState detached =
            BattleDetachedPreviewState.Create(state, canonicalSource);
        BattleUnitState sourcePreview = detached.GetUnit(canonicalSource.unit_id);
        BattleUnitState targetPreview = ResolvePreviewPrimaryTarget(
            detached.State,
            command
        );
        if (sourcePreview == null)
            return;

        var actions = new List<BattleEquipmentAbilityActionPreviewResult>();
        bool triggered = Runtime.ResolveEquipmentGrantedSkillUsed(
            new BattleEquipmentAbilityGrantedSkillUsedContext
            {
                SourceUnit = sourcePreview,
                TargetUnit = targetPreview,
                BattleState = detached.State,
                Batch = null,
                BindingId = entry.EquipmentBindingId,
                GrantedActionId = entry.EquipmentGrantedActionId,
                SkillId = entry.EntryRef.SkillId,
                SkillEntryId = entry.EntryRef.SkillEntryId,
                SkillOutcome = BattleEquipmentSkillUseOutcome.Empty,
                IsPreview = true,
                PreviewActionSink = actions.Add,
            }
        );
        preview.SetEquipmentAbilityPreview(
            new BattleEquipmentAbilityCommandPreviewResult
            {
                Triggered = triggered,
                SourceUnitId = sourcePreview.unit_id,
                SourceUnitAfter = sourcePreview,
                Actions = actions.AsReadOnly(),
            }
        );
    }

    private static BattleUnitState ResolvePreviewPrimaryTarget(
        BattleState state,
        BattleCommand command
    )
    {
        if (state == null || command == null)
            return null;
        StringName targetUnitId = ProgressionDataUtils.to_string_name(command.target_unit_id);
        if (targetUnitId != "" && state.TryGetUnitTyped(targetUnitId, out BattleUnitState target))
            return target;
        foreach (StringName candidateId in command.TargetUnitIdsTyped ?? Array.Empty<StringName>())
        {
            if (state.TryGetUnitTyped(candidateId, out target))
                return target;
        }
        return null;
    }

    private BattleSkillAccessResult ValidateSkillCommandEntryAccess(
        BattleCommand command,
        BattleSkillAvailabilityConsumer consumer
    )
    {
        return Runtime.ValidateSkillCommandEntryAccess(command, consumer);
    }

    private static void PreviewWaitCommand(BattleUnitReadView activeUnit, BattlePreview preview)
    {
        using BattleAiTraceSpan trace = new("preview:wait");
        preview.allowed = true;
        preview.AddLogLine($"{activeUnit.DisplayName} 可以结束行动。");
    }

    private void PreviewChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:change_equipment");
        if (activeUnit.TurnCastingExhausted)
        {
            preview.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            return;
        }
        _preview_change_equipment_command(activeUnit, command, preview);
    }

    private static void PreviewUnknownCommand(BattlePreview preview)
    {
        preview.AddLogLine("未知命令类型。");
    }

    internal string _get_battle_interaction_block_message()
    {
        BattleState state = Runtime?.GetBattleState();
        if (state == null)
            return "当前无法操作。";
        return state.ModalStateKind switch
        {
            BattleModalStateKind.StartConfirm => "战斗尚未开始，确认后才能操作。",
            BattleModalStateKind.PromotionChoice => "当前处于晋升选择中，无法操作。",
            _ => "当前有待处理的战斗流程，暂时无法操作。",
        };
    }

    internal bool _should_block_skill_issue_from_preview(
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        BattlePreview preview = PreviewCommand(command);
        try
        {
            if (preview != null && preview.allowed)
                return false;
            if (preview != null)
            {
                foreach (string logLine in preview.LogLinesTyped)
                    batch.AddLogLine(logLine);
            }
            if (batch.LogLinesTyped.Count == 0)
                batch.AddLogLine("技能或目标无效。");
            return true;
        }
        finally
        {
            preview?.ReleaseHitPreview();
        }
    }

    internal void _preview_change_equipment_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    ) => Runtime.PreviewChangeEquipmentCommand(active_unit, command, preview);
}
