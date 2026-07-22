using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentStatusActionResolver
{
    private BattleRuntimeModule _runtime;
    private BattleEquipmentAbilityRuntimeService _owner;
    private BattleEquipmentTargetMarkResolver _targetMarkResolver;
    private BattleEquipmentAbilityStateResolver _abilityStateResolver;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleEquipmentAbilityRuntimeService owner,
        BattleEquipmentTargetMarkResolver targetMarkResolver,
        BattleEquipmentAbilityStateResolver abilityStateResolver
    )
    {
        _runtime = runtime;
        _owner = owner;
        _targetMarkResolver = targetMarkResolver;
        _abilityStateResolver = abilityStateResolver;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _targetMarkResolver = null;
        _abilityStateResolver = null;
    }

    internal void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context,
        BattleEquipmentAbilityOnKillResult result
    )
    {
        foreach (
            BattleUnitState targetUnit in BattleEquipmentAbilityRuntimeService.ResolveApplyStatusTargets(
                payload?.TargetSelector ?? "",
                context.SourceUnit,
                context.DefeatedUnit,
                context.BattleState ?? _runtime?.GetState()
            )
        )
        {
            ResolveApplyStatusAction(
                binding,
                action,
                payload,
                context.SourceUnit,
                targetUnit,
                context.SaveContext,
                result != null ? result.AddStatusResult : null
            );
        }
    }

    internal void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        foreach (
            BattleUnitState targetUnit in BattleEquipmentAbilityRuntimeService.ResolveApplyStatusTargets(
                payload?.TargetSelector ?? "",
                context.SourceUnit,
                context.TargetUnit,
                context.BattleState
            )
        )
        {
            ResolveApplyStatusAction(
                binding,
                action,
                payload,
                context.SourceUnit,
                targetUnit,
                context.SaveContext,
                result != null ? result.AddStatusResult : null
            );
        }
    }

    internal bool ResolveClearStatusAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        ClearStatusActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (payload == null || sourceUnit == null || payload.StatusId == "")
            return false;
        BattleUnitState resolvedTarget = _owner.ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            payload.MarkBindingId,
            payload.MarkStateKey,
            battleState
        );
        bool changed = false;
        BattleStatusEffectState existing = resolvedTarget?.GetStatusEffect(payload.StatusId);
        bool canClearStatus =
            existing != null
            && (!payload.RequireSourceUnitMatch
                || ProgressionDataUtils.to_string_name(existing.source_unit_id)
                    == sourceUnit.unit_id);
        bool mirrorHandledByMarkRemoval = false;
        if (payload.ClearTargetMark)
        {
            BattleState state = battleState ?? _runtime?.GetState();
            EquipmentAbilityBindingDefinition markBinding = _abilityStateResolver.ResolveStateBinding(
                activeBinding,
                binding,
                payload.MarkBindingId
            );
            if (
                state != null
                && markBinding != null
                && payload.MarkStateKey != ""
                && state.TryGetEquipmentTargetMark(
                    sourceUnit.unit_id,
                    activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                    markBinding.BindingId,
                    payload.MarkStateKey,
                    out BattleEquipmentTargetMarkState removedMark
                )
                && state.RemoveEquipmentTargetMark(
                    sourceUnit.unit_id,
                    activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                    markBinding.BindingId,
                    payload.MarkStateKey
                )
            )
            {
                changed = true;
                mirrorHandledByMarkRemoval = BattleEquipmentTargetMarkResolver.TargetMarkMirrorsStatus(
                    markBinding,
                    removedMark.StateKey,
                    payload.StatusId
                );
                _targetMarkResolver.ReconcileTargetMarkStatusesAfterRemoval(
                    state,
                    resolvedTarget,
                    removedMark,
                    markBinding
                );
            }
        }
        if (canClearStatus && !mirrorHandledByMarkRemoval)
        {
            resolvedTarget.EraseStatusEffect(payload.StatusId);
            changed = true;
        }
        return changed;
    }

    internal List<StringName> ResolveConsumeStatusStacksAction(
        ConsumeStatusStacksActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (payload == null || sourceUnit == null || payload.StatusId == "" || payload.Count <= 0)
            return null;
        BattleState state = battleState ?? _runtime?.GetState();
        StringName selector = ProgressionDataUtils.to_string_name(payload.TargetSelector);
        var candidates = new List<BattleUnitState>();
        if (selector == "all_units")
        {
            if (state == null)
                return null;
            foreach (BattleUnitState unit in state.GetUnitsTyped())
            {
                if (unit != null)
                    candidates.Add(unit);
            }
        }
        else
        {
            BattleUnitState resolved = BattleEquipmentAbilityRuntimeService.ResolveSubject(selector, sourceUnit, targetUnit);
            if (resolved != null)
                candidates.Add(resolved);
        }
        var holders = new List<(BattleUnitState Unit, BattleStatusEffectState Status)>();
        foreach (BattleUnitState unit in candidates)
        {
            BattleStatusEffectState status = unit.GetStatusEffect(payload.StatusId);
            if (status == null || status.stacks <= 0)
                continue;
            if (
                payload.RequireSourceUnitMatch
                && ProgressionDataUtils.to_string_name(status.source_unit_id) != sourceUnit.unit_id
            )
            {
                continue;
            }
            holders.Add((unit, status));
        }
        if (holders.Count == 0)
            return null;
        holders.Sort(
            (left, right) =>
            {
                int byStacks = right.Status.stacks.CompareTo(left.Status.stacks);
                if (byStacks != 0)
                    return byStacks;
                return string.CompareOrdinal(
                    left.Unit.unit_id.ToString(),
                    right.Unit.unit_id.ToString()
                );
            }
        );
        int remaining = payload.Count;
        var changedUnitIds = new List<StringName>();
        foreach ((BattleUnitState unit, BattleStatusEffectState status) in holders)
        {
            if (remaining <= 0)
                break;
            int consumed = Math.Min(status.stacks, remaining);
            remaining -= consumed;
            int stacksLeft = status.stacks - consumed;
            if (stacksLeft > 0)
                status.stacks = stacksLeft;
            else
                unit.EraseStatusEffect(payload.StatusId);
            changedUnitIds.Add(unit.unit_id);
        }
        return changedUnitIds.Count > 0 ? changedUnitIds : null;
    }

    internal void ResolveApplyStatusAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ApplyStatusActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleSaveContext saveContext,
        Action<BattleEquipmentAbilityStatusActionResult> addResult
    )
    {
        if (payload == null || sourceUnit == null || targetUnit == null || payload.StatusId == "")
            return;

        BattleSaveResult saveResult = default;
        if (payload.SaveDc > 0)
        {
            CombatEffectDefinition saveEffect = BattleRuntimeEffectDefinitions.StaticSave(
                payload.SaveDc,
                payload.SaveAbility,
                payload.SaveTag
            );
            saveResult = BattleSaveResolver.ResolveSaveResult(
                sourceUnit,
                targetUnit,
                saveEffect,
                saveContext
            );
            if (payload.ApplyOnSaveFailure && saveResult.Success)
            {
                addResult?.Invoke(
                    new BattleEquipmentAbilityStatusActionResult
                    {
                        BindingId = binding.BindingId,
                        ActionId = action.ActionId,
                        TargetUnitId = targetUnit.unit_id,
                        StatusId = payload.StatusId,
                        Applied = false,
                        SaveResult = saveResult,
                    }
                );
                return;
            }
        }

        int durationTu = ResolveStatusDurationTu(payload);
        CombatEffectDefinition statusEffect = BattleRuntimeEffectDefinitions.Status(
            payload.StatusId,
            Math.Max(payload.StackDelta, 1),
            durationTu,
            stackBehavior: payload.StackBehavior,
            stackLimit: payload.StackLimit,
            displayName: payload.DisplayLabel,
            attackRollPenalty: payload.AttackRollPenalty,
            sourceBoundAttackRollPenalty: payload.SourceBoundAttackRollPenalty,
            sourceBoundAttackRollPenaltyMinStacks: payload.SourceBoundAttackRollPenaltyMinStacks,
            sourceBoundIncomingAttackRollBonusPerStack:
                payload.SourceBoundIncomingAttackRollBonusPerStack,
            sourceBoundIncomingAttackRollBonusMinStacks:
                payload.SourceBoundIncomingAttackRollBonusMinStacks,
            countsAsDebuffOverride: payload.CountsAsDebuffOverride,
            countsAsDebuff: payload.CountsAsDebuff,
            undispellable: payload.Undispellable,
            dispellableMagic: payload.DispellableMagic,
            dispellableHarmfulMagic: payload.DispellableHarmfulMagic,
            dispellableBeneficialMagic: payload.DispellableBeneficialMagic,
            lockCounterattack: payload.LockCounterattack,
            lockGuard: payload.LockGuard,
            lockDodgeBonus: payload.LockDodgeBonus
        );
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
            statusEffect,
            sourceUnit.unit_id,
            targetUnit.GetStatusEffect(payload.StatusId),
            payload.StatusId
        );
        if (statusEntry == null)
            return;
        ApplyStatusTimelineDamagePayload(statusEntry, payload);
        if (payload.OverrideHealMultiplierPercent)
            statusEntry.heal_multiplier_percent = Math.Clamp(payload.HealMultiplierPercent, 0, 100);
        else
            statusEntry.heal_multiplier_percent = null;
        if (payload.MovePointCapacityDelta != 0)
            statusEntry.move_point_capacity_delta = payload.MovePointCapacityDelta;
        statusEntry.forced_move_immune = payload.ForcedMoveImmune;
        targetUnit.SetStatusEffect(statusEntry);
        if (payload.MovePointCapacityDelta != 0)
            targetUnit.ClampCurrentMovePointsToCapacity();
        _runtime?.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            new Godot.Collections.Array<StringName> { payload.StatusId }
        );
        addResult?.Invoke(
            new BattleEquipmentAbilityStatusActionResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                TargetUnitId = targetUnit.unit_id,
                StatusId = payload.StatusId,
                Applied = true,
                SaveResult = saveResult,
            }
        );
    }

    private static void ApplyStatusTimelineDamagePayload(
        BattleStatusEffectState statusEntry,
        ApplyStatusActionPayloadDefinition payload
    )
    {
        if (statusEntry == null || payload == null)
            return;
        if (payload.TickIntervalTu > 0)
            statusEntry.tick_interval_tu = payload.TickIntervalTu;
        if (payload.TimelineDamageDiceCount > 0 && payload.TimelineDamageDiceSides > 0)
        {
            statusEntry.timeline_damage_dice_count = payload.TimelineDamageDiceCount;
            statusEntry.timeline_damage_dice_sides = payload.TimelineDamageDiceSides;
            statusEntry.timeline_damage_flat_bonus = Math.Max(payload.TimelineDamageFlatBonus, 0);
        }
    }

    private static int ResolveStatusDurationTu(ApplyStatusActionPayloadDefinition payload)
    {
        if (payload == null)
            return 0;
        if (payload.DurationTu > 0)
            return payload.DurationTu;
        return Math.Max(payload.DurationTurns, 0);
    }
}
