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
        BattleEquipmentAbilitySourceReadView source,
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
                source,
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
        BattleEquipmentAbilitySourceReadView source,
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
                source,
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
        BattleEquipmentAbilitySourceReadView source,
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
            payload.StatusId,
            BattleStatusSourceIdentity.EquipmentAbility(
                sourceUnit.unit_id,
                binding.BindingId
            )
        );
        if (statusEntry == null)
            return;
        ApplyStatusTimelineDamagePayload(statusEntry, payload);
        if (payload.OverrideHealMultiplierPercent)
            statusEntry.heal_multiplier_percent = Math.Clamp(payload.HealMultiplierPercent, 0, 100);
        else
            statusEntry.heal_multiplier_percent = null;
        statusEntry.armor_class_bonus_per_stack =
            Math.Max(payload.ArmorClassBonusPerStack, 0);
        if (payload.MovePointCapacityDelta != 0)
            statusEntry.move_point_capacity_delta = payload.MovePointCapacityDelta;
        statusEntry.forced_move_immune = payload.ForcedMoveImmune;
        statusEntry.damage_tag = payload.DamageTag;
        statusEntry.damage_tags = new List<StringName>(
            payload.DamageTags ?? Array.Empty<StringName>()
        );
        statusEntry.mitigation_tier = payload.MitigationTier;
        BattleStatusSemanticTable.SynchronizeSourceContributionTimelinePayload(
            statusEntry,
            BattleStatusSourceIdentity.EquipmentAbility(
                sourceUnit.unit_id,
                binding.BindingId
            )
        );
        RecordSourceBoundProvenance(statusEntry, payload, source, binding, action, sourceUnit);
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

    // §8.7：status 投影时记录 typed provenance（source kind + effective source key +
    // binding/action id），remove_on_source_deactivated 是 authoring opt-in 标记。
    // 最近一次 apply 覆盖旧 provenance，与本方法覆盖其他 status 字段的语义一致。
    private static void RecordSourceBoundProvenance(
        BattleStatusEffectState statusEntry,
        ApplyStatusActionPayloadDefinition payload,
        BattleEquipmentAbilitySourceReadView source,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        BattleUnitState sourceUnit
    )
    {
        if (statusEntry == null)
            return;
        statusEntry.remove_on_source_deactivated =
            payload?.RemoveOnSourceDeactivated == true;
        if (
            source != null
            && source.SourceKind != EquipmentAbilitySourceKind.Unknown
            && binding != null
        )
        {
            statusEntry.source_provenance_unit_id = sourceUnit?.unit_id ?? "";
            statusEntry.source_provenance_source_kind =
                BattleEquipmentAbilitySourceState.ToStringName(source.SourceKind);
            statusEntry.source_provenance_effective_key =
                source.EffectiveInstanceKey ?? "";
            statusEntry.source_provenance_binding_id = binding.BindingId;
            statusEntry.source_provenance_action_id = action?.ActionId ?? "";
            return;
        }
        statusEntry.source_provenance_unit_id = "";
        statusEntry.source_provenance_source_kind = "";
        statusEntry.source_provenance_effective_key = "";
        statusEntry.source_provenance_binding_id = "";
        statusEntry.source_provenance_action_id = "";
    }

    // §8.7 opt-in source-bound buff 清除：只移除声明了 remove_on_source_deactivated
    // 且 provenance 精确匹配该单位已失效 source 的 status；不按 status id 全局删除。
    // 挂接在换装/摧毁后的 source 重投影之后（与 target-mark 清理对称），失败换装
    // 不会走到这里，因此不会留下半清状态。
    internal IReadOnlyList<StringName> ClearSourceBoundStatusesForRemovedEquipmentSources(
        BattleState state,
        BattleUnitState sourceUnit
    )
    {
        var changedUnitIds = new List<StringName>();
        if (state == null || sourceUnit == null || sourceUnit.unit_id == "")
            return changedUnitIds;

        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            if (unit == null)
                continue;
            var removals = new List<BattleStatusEffectState>();
            foreach (BattleStatusEffectState status in unit.GetStatusEffectsTyped())
            {
                if (
                    status == null
                    || !status.remove_on_source_deactivated
                    || status.source_provenance_unit_id != sourceUnit.unit_id
                )
                {
                    continue;
                }
                EquipmentAbilitySourceKind provenanceKind =
                    BattleEquipmentAbilitySourceState.ToSourceKind(
                        status.source_provenance_source_kind
                    );
                if (
                    provenanceKind == EquipmentAbilitySourceKind.Unknown
                    || status.source_provenance_binding_id == ""
                    || HasActiveProvenanceSource(
                        sourceUnit,
                        provenanceKind,
                        status.source_provenance_effective_key,
                        status.source_provenance_binding_id
                    )
                )
                {
                    continue;
                }
                removals.Add(status);
            }
            foreach (BattleStatusEffectState status in removals)
            {
                RemoveSourceBoundStatus(unit, status);
                if (!changedUnitIds.Contains(unit.unit_id))
                    changedUnitIds.Add(unit.unit_id);
            }
        }
        return changedUnitIds;
    }

    private bool HasActiveProvenanceSource(
        BattleUnitState sourceUnit,
        EquipmentAbilitySourceKind provenanceKind,
        StringName effectiveKey,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source
            in sourceUnit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (
                source != null
                && source.SourceKind == provenanceKind
                && source.EffectiveInstanceKey == effectiveKey
                && source.AbilityIds?.Contains(bindingId) == true
            )
            {
                return true;
            }
        }
        // BattleStatusDerived source 不进入投影来源列表；它与 CollectActiveBindings
        // 的合成规则保持一致：activation status 仍在单位身上即视为存活。
        if (provenanceKind == EquipmentAbilitySourceKind.BattleStatusDerived)
        {
            IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
                _runtime?.GetEquipmentAbilityBindingIndexTyped();
            if (
                bindingIndex != null
                && bindingIndex.TryGetValue(bindingId, out EquipmentAbilityBindingDefinition binding)
                && binding?.ActivationStatusId != ""
                && sourceUnit.HasStatusEffect(binding.ActivationStatusId)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static void RemoveSourceBoundStatus(
        BattleUnitState unit,
        BattleStatusEffectState status
    )
    {
        if (status.HasSourceContributionsTyped())
        {
            // source-definition 叠加状态：只摘除失效 source 的 contribution，
            // 其他来源（技能、其他 binding）的份额保留，不清除整个 status。
            status.RemoveSourceContributionTyped(
                BattleStatusSourceIdentity.EquipmentAbility(
                    status.source_provenance_unit_id,
                    status.source_provenance_binding_id
                )
            );
            if (status.GetSourceContributionsTyped().Count > 0)
            {
                status.RebuildSourceContributionAggregateTyped();
                ClearSourceBoundProvenance(status);
                return;
            }
        }
        unit.EraseStatusEffect(status.status_id);
    }

    private static void ClearSourceBoundProvenance(BattleStatusEffectState status)
    {
        status.remove_on_source_deactivated = false;
        status.source_provenance_unit_id = "";
        status.source_provenance_source_kind = "";
        status.source_provenance_effective_key = "";
        status.source_provenance_binding_id = "";
        status.source_provenance_action_id = "";
    }
}
