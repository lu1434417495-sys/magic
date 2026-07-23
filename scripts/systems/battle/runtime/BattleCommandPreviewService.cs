using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleCommandPreviewService : BattleRuntimeModuleBorrower
{

    public BattlePreview PreviewCommand(BattleCommand command)
    {
        _runtime._ensure_sidecars_ready();
        var preview = new BattlePreview();
        if (!CanPreviewCommand(command))
            return preview;
        if (_runtime._state.ModalStateKind != BattleModalStateKind.None)
        {
            preview.AddLogLine(_get_battle_interaction_block_message());
            return preview;
        }
        if (command.IsCancelCast())
        {
            _runtime._casting_time_service.PreviewCancelCast(command, preview);
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
            _runtime.PreviewObjectiveInteraction(activeUnit, command, preview);
        else if (command.IsChangeEquipment())
            PreviewChangeEquipmentCommand(activeUnit, command, preview);
        else
            PreviewUnknownCommand(preview);
        return preview;
    }

    private bool CanPreviewCommand(BattleCommand command)
    {
        return _runtime._state != null && command != null && _runtime._state.PhaseKind != BattlePhaseKind.BattleEnded;
    }

    private BattleUnitReadView ResolvePreviewActiveUnit(BattleCommand command)
    {
        return command != null ? _runtime._state.AsReadView().GetUnit(command.unit_id) : default;
    }

    private void PreviewMoveCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:move");
        if (_runtime._movement_service.IsMovementBlocked(activeUnit))
        {
            preview.AddLogLine($"{activeUnit.DisplayName} 当前被限制移动。");
            return;
        }

        BattleMovePathResult moveResult;
        using (new BattleAiTraceSpan("preview:move.resolve_path_result"))
        {
            moveResult = _runtime._movement_service.ResolveMovePathResultTyped(
                activeUnit,
                command.target_coord
            );
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
        foreach (Vector2I targetCoord in _runtime._grid_service.GetUnitTargetCoords(activeUnit, anchorCoord))
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
        _runtime._preview_skill_command(activeUnit, command, preview);
    }

    private BattleSkillAccessResult ValidateSkillCommandEntryAccess(
        BattleCommand command,
        BattleSkillAvailabilityConsumer consumer
    )
    {
        BattleSkillAvailabilityService service = new(
            _runtime._skillCatalog,
            _runtime._skillDefinitionIndex,
            _runtime._equipmentAbilityBindingIndex,
            _runtime._itemDefIndex
        );
        return service.ValidateSkillCommandEntryAccess(
            _runtime._state,
            command,
            consumer,
            _runtime.GetBattleWorldStep()
        );
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
        if (_runtime._state == null)
            return "当前无法操作。";
        return _runtime._state.ModalStateKind switch
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
            BattleRuntimeModule.DisposeBattlePreview(preview);
        }
    }

    internal void _preview_change_equipment_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    ) => _runtime._change_equipment_resolver.PreviewCommand(active_unit, command, preview);
}
