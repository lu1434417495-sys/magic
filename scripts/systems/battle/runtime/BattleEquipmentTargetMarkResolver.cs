using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentTargetMarkResolver
{
    private BattleRuntimeModule _runtime;
    private BattleEquipmentAbilityRuntimeService _owner;
    private BattleEquipmentAbilityConditionEvaluator _conditionEvaluator;
    private BattleEquipmentSkillTriggerActionResolver _skillTriggerActionResolver;
    private BattleEquipmentAbilityStateResolver _abilityStateResolver;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleEquipmentAbilityRuntimeService owner,
        BattleEquipmentAbilityConditionEvaluator conditionEvaluator,
        BattleEquipmentSkillTriggerActionResolver skillTriggerActionResolver,
        BattleEquipmentAbilityStateResolver abilityStateResolver
    )
    {
        _runtime = runtime;
        _owner = owner;
        _conditionEvaluator = conditionEvaluator;
        _skillTriggerActionResolver = skillTriggerActionResolver;
        _abilityStateResolver = abilityStateResolver;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _conditionEvaluator = null;
        _skillTriggerActionResolver = null;
        _abilityStateResolver = null;
    }

    internal bool ResolveMarkTargetAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        MarkTargetActionPayloadDefinition payload,
        BattleEquipmentAbilityGrantedSkillUsedContext context
    )
    {
        BattleState battleState = context?.BattleState ?? _runtime?.GetState();
        BattleUnitState targetUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.TargetSelector ?? "",
            context?.SourceUnit,
            context?.TargetUnit
        );
        if (
            battleState == null
            || context?.SourceUnit == null
            || targetUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
        )
        {
            return false;
        }

        BattleEquipmentTargetMarkState nextMark = new()
        {
            SourceUnitId = context.SourceUnit.unit_id,
            TargetUnitId = targetUnit.unit_id,
            SourceEquipmentInstanceId = activeBinding.Source?.SourceEquipmentInstanceId ?? "",
            BindingId = binding.BindingId,
            StateKey = payload.StateKey,
            Stacks = Math.Max(payload.StackDelta, 1),
            RemainingDurationTu = payload.MirrorStatusDurationTu > 0
                ? payload.MirrorStatusDurationTu
                : -1,
            RemoveOnSourceMissing = payload.RemoveOnSourceMissing,
        };
        if (
            !battleState.SetEquipmentTargetMark(
                nextMark,
                payload.UniquePerSource,
                out BattleEquipmentTargetMarkState replaced
            )
        )
        {
            return false;
        }

        bool changed = true;
        StringName replacedTargetId = replaced != null
            ? ProgressionDataUtils.to_string_name(replaced.TargetUnitId)
            : new StringName("");
        StringName currentTargetId = ProgressionDataUtils.to_string_name(targetUnit.unit_id);
        if (replacedTargetId != "" && replacedTargetId != currentTargetId)
        {
            BattleUnitState previousTarget = battleState.GetUnit(replacedTargetId);
            if (previousTarget != null)
            {
                ReconcileTargetMarkStatusesAfterRemoval(
                    battleState,
                    previousTarget,
                    replaced,
                    binding
                );
                context.Batch?.AddChangedUnitId(previousTarget.unit_id);
            }
        }

        if (ApplyMirrorStatus(battleState, targetUnit, payload))
        {
            context.Batch?.AddChangedUnitId(targetUnit.unit_id);
        }
        return changed;
    }

    private static void AddUniqueStatusId(List<StringName> result, StringName statusId)
    {
        if (statusId == "" || result.Contains(statusId))
            return;
        result.Add(statusId);
    }

    private bool ApplyMirrorStatus(
        BattleState battleState,
        BattleUnitState targetUnit,
        MarkTargetActionPayloadDefinition payload
    )
    {
        if (
            battleState == null
            || targetUnit == null
            || payload == null
            || payload.MirrorStatusId == ""
            || !RefreshTargetMarkMirrorStatus(
                battleState,
                targetUnit,
                payload.MirrorStatusId
            )
        )
        {
            return false;
        }
        _runtime?.MarkAppliedStatusesForTurnTiming(
            targetUnit,
            new Godot.Collections.Array<StringName> { payload.MirrorStatusId }
        );
        return true;
    }

    private bool RefreshTargetMarkMirrorStatus(
        BattleState battleState,
        BattleUnitState targetUnit,
        StringName mirrorStatusId,
        bool preserveExistingDuration = false
    )
    {
        if (battleState == null || targetUnit == null || mirrorStatusId == "")
            return false;

        BattleStatusEffectState existing = targetUnit.GetStatusEffect(mirrorStatusId);
        StringName preferredSourceUnitId = ProgressionDataUtils.to_string_name(
            existing?.source_unit_id
        );
        BattleEquipmentTargetMarkState selectedMark = null;
        MarkTargetActionPayloadDefinition selectedPayload = null;
        foreach (BattleEquipmentTargetMarkState candidate in battleState.GetEquipmentTargetMarksTyped())
        {
            if (candidate?.IsValid != true || candidate.TargetUnitId != targetUnit.unit_id)
                continue;
            EquipmentAbilityBindingDefinition candidateBinding = ResolveBindingForTargetMark(
                candidate
            );
            MarkTargetActionPayloadDefinition candidatePayload = ResolveTargetMarkPayload(
                candidateBinding,
                candidate.StateKey,
                mirrorStatusId
            );
            if (
                candidatePayload == null
                || !IsPreferredMirrorMark(candidate, selectedMark, preferredSourceUnitId)
            )
            {
                continue;
            }
            selectedMark = candidate;
            selectedPayload = candidatePayload;
        }
        if (selectedMark == null || selectedPayload == null)
            return false;

        StringName stackBehavior = selectedPayload.MirrorStatusStackBehavior == ""
            ? new StringName("refresh")
            : selectedPayload.MirrorStatusStackBehavior;
        int stackLimit = Math.Max(selectedPayload.MirrorStatusStackLimit, 0);
        int stacks = Math.Max(selectedMark.Stacks, 1);
        if (stackLimit > 0)
            stacks = Math.Min(stacks, stackLimit);

        BattleStatusEffectState status = existing?.DuplicateState() ?? new BattleStatusEffectState();
        status.status_id = mirrorStatusId;
        status.source_unit_id = selectedMark.SourceUnitId;
        status.power = stacks;
        status.stacks = stacks;
        status.duration =
            preserveExistingDuration
                && existing != null
                && preferredSourceUnitId == selectedMark.SourceUnitId
                && existing.duration > 0
            ? existing.duration
            : selectedMark.RemainingDurationTu > 0
                ? selectedMark.RemainingDurationTu
                : -1;
        status.stack_behavior = stackBehavior;
        status.stack_limit = stackLimit;
        if (!string.IsNullOrWhiteSpace(selectedPayload.MirrorStatusDisplayLabel))
            status.display_label = selectedPayload.MirrorStatusDisplayLabel;
        targetUnit.SetStatusEffect(status);
        return true;
    }

    private static bool IsPreferredMirrorMark(
        BattleEquipmentTargetMarkState candidate,
        BattleEquipmentTargetMarkState selected,
        StringName preferredSourceUnitId
    )
    {
        if (candidate?.IsValid != true)
            return false;
        if (selected?.IsValid != true)
            return true;
        int candidateDuration = candidate.RemainingDurationTu < 0
            ? int.MaxValue
            : candidate.RemainingDurationTu;
        int selectedDuration = selected.RemainingDurationTu < 0
            ? int.MaxValue
            : selected.RemainingDurationTu;
        if (candidateDuration != selectedDuration)
            return candidateDuration > selectedDuration;
        return candidate.SourceUnitId == preferredSourceUnitId
            && selected.SourceUnitId != preferredSourceUnitId;
    }

    private static MarkTargetActionPayloadDefinition ResolveTargetMarkPayload(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        StringName mirrorStatusId
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition is MarkTargetActionPayloadDefinition payload
                    && ProgressionDataUtils.to_string_name(payload.StateKey) == stateKey
                    && ProgressionDataUtils.to_string_name(payload.MirrorStatusId)
                        == mirrorStatusId
                )
                {
                    return payload;
                }
            }
        }
        return null;
    }

    internal BattleUnitState ResolveMarkedTarget(
        BattleUnitState sourceUnit,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition fallbackBinding,
        StringName markBindingId,
        StringName markStateKey,
        BattleState battleState
    )
    {
        if (sourceUnit == null)
            return null;
        BattleState state = battleState ?? _runtime?.GetState();
        if (state == null)
            return null;
        EquipmentAbilityBindingDefinition markBinding = _abilityStateResolver.ResolveStateBinding(
            activeBinding,
            fallbackBinding,
            markBindingId
        );
        StringName stateKey = ProgressionDataUtils.to_string_name(markStateKey);
        if (markBinding == null || markBinding.BindingId == "" || stateKey == "")
            return null;
        if (!state.TryGetEquipmentTargetMark(
            sourceUnit.unit_id,
            activeBinding.Source?.SourceEquipmentInstanceId ?? "",
            markBinding.BindingId,
            stateKey,
            out BattleEquipmentTargetMarkState mark
        ))
            return null;
        if (ClearStaleEquipmentTargetMarkIfNeeded(state, markBinding, mark))
            return null;
        return state.GetUnit(mark.TargetUnitId);
    }

    internal bool TryResolveEquipmentTargetMark(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        out BattleEquipmentTargetMarkState mark
    )
    {
        mark = null;
        if (query == null || sourceUnit == null)
            return false;
        BattleState state = factContext.BattleState ?? _runtime?.GetState();
        if (state == null)
            return false;
        EquipmentAbilityBindingDefinition stateBinding = _abilityStateResolver.ResolveStateBinding(
            activeBinding,
            activeBinding.Binding,
            query.BindingId
        );
        StringName stateKey = ProgressionDataUtils.to_string_name(query.StateKey);
        if (stateBinding == null || stateBinding.BindingId == "" || stateKey == "")
            return false;
        if (!state.TryGetEquipmentTargetMark(
            sourceUnit.unit_id,
            activeBinding.Source?.SourceEquipmentInstanceId ?? "",
            stateBinding.BindingId,
            stateKey,
            out mark
        ))
        {
            return false;
        }
        if (ClearStaleEquipmentTargetMarkIfNeeded(state, stateBinding, mark))
        {
            mark = null;
            return false;
        }
        return true;
    }

    internal bool AdvanceTargetMarkDurations(
        BattleUnitState targetUnit,
        int elapsedTu,
        BattleEventBatch batch = null
    )
    {
        BattleState state = _runtime?.GetState();
        if (state == null || targetUnit == null || elapsedTu <= 0)
            return false;

        bool changed = false;
        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (
                mark?.IsValid != true
                || mark.TargetUnitId != targetUnit.unit_id
                || mark.RemainingDurationTu <= 0
            )
            {
                continue;
            }

            int remainingDurationTu = Math.Max(mark.RemainingDurationTu - elapsedTu, 0);
            if (remainingDurationTu > 0)
            {
                if (
                    state.SetEquipmentTargetMark(
                        mark.WithRemainingDurationTu(remainingDurationTu),
                        uniquePerSource: true,
                        out _
                    )
                )
                {
                    changed = true;
                }
                continue;
            }

            EquipmentAbilityBindingDefinition markBinding = ResolveBindingForTargetMark(mark);
            ResolveExpiredTargetMark(state, targetUnit, mark, batch);
            ReconcileTargetMarkStatusesAfterRemoval(
                state,
                targetUnit,
                mark,
                markBinding,
                preserveExistingMirrorDuration: true
            );
            changed = true;
        }
        return changed;
    }

    internal bool ResolveTargetMarkExpired(
        BattleUnitState targetUnit,
        BattleStatusEffectState expiredStatus,
        BattleEventBatch batch = null
    )
    {
        BattleState state = _runtime?.GetState();
        if (state == null || targetUnit == null || expiredStatus == null)
            return false;
        bool changed = false;
        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (
                mark?.IsValid != true
                || mark.TargetUnitId != targetUnit.unit_id
                || mark.SourceUnitId
                    != ProgressionDataUtils.to_string_name(expiredStatus.source_unit_id)
            )
            {
                continue;
            }
            EquipmentAbilityBindingDefinition markBinding = ResolveBindingForTargetMark(mark);
            if (!TargetMarkMirrorsStatus(markBinding, mark.StateKey, expiredStatus.status_id))
                continue;
            changed |= ResolveExpiredTargetMark(state, targetUnit, mark, batch);
        }
        return changed;
    }

    private bool ResolveExpiredTargetMark(
        BattleState state,
        BattleUnitState targetUnit,
        BattleEquipmentTargetMarkState mark,
        BattleEventBatch batch
    )
    {
        if (state == null || targetUnit == null || mark?.IsValid != true)
            return false;

        BattleUnitState sourceUnit = state.GetUnit(mark.SourceUnitId);
        if (sourceUnit?.is_alive == true)
        {
            var context = new BattleEquipmentAbilityTargetMarkExpiredContext
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                BattleState = state,
                Batch = batch,
                Mark = mark,
            };
            foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(sourceUnit))
            {
                if (
                    activeBinding.Source?.SourceEquipmentInstanceId
                    != mark.SourceEquipmentInstanceId
                )
                {
                    continue;
                }
                EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
                foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
                {
                    if (
                        reaction == null
                        || reaction.Trigger != EquipmentAbilityTriggerKind.OnTargetMarkExpired
                        || reaction.Timing != EquipmentAbilityTimingKind.AfterStatusExpired
                        || !_conditionEvaluator.ConditionGroupPasses(
                            reaction.ConditionGroup,
                            sourceUnit,
                            targetUnit,
                            EquipmentAbilityFactContext.FromTargetMarkExpired(context),
                            activeBinding
                        )
                        || !_owner.RollGatePasses(
                            reaction.RollGate,
                            binding.BindingId,
                            reaction.ReactionId,
                            "",
                            forcedRollValue: 0,
                            result: null
                        )
                    )
                    {
                        continue;
                    }
                    ResolveTargetMarkExpiredActions(
                        activeBinding,
                        binding,
                        reaction,
                        context
                    );
                }
            }
        }

        bool removed = state.RemoveEquipmentTargetMark(
            mark.SourceUnitId,
            mark.SourceEquipmentInstanceId,
            mark.BindingId,
            mark.StateKey
        );
        if (removed)
        {
            batch?.AddChangedUnitId(mark.SourceUnitId);
            batch?.AddChangedUnitId(mark.TargetUnitId);
        }
        return removed;
    }

    private void ResolveTargetMarkExpiredActions(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityTargetMarkExpiredContext context
    )
    {
        foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || !_conditionEvaluator.ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.TargetUnit,
                    EquipmentAbilityFactContext.FromTargetMarkExpired(context),
                    activeBinding
                )
                || !_owner.RollGatePasses(
                    action.RollGate,
                    binding.BindingId,
                    reaction.ReactionId,
                    action.ActionId,
                    forcedRollValue: 0,
                    result: null
                )
            )
            {
                continue;
            }
            if (
                action.Kind == BattleEquipmentAbilityRuntimeService.ActionKindTriggerSkill
                && action.PayloadDefinition is TriggerSkillActionPayloadDefinition triggerSkillPayload
            )
            {
                _skillTriggerActionResolver.ResolveTriggerSkillAction(
                    activeBinding,
                    binding,
                    action,
                    triggerSkillPayload,
                    context.SourceUnit,
                    context.TargetUnit,
                    context.BattleState,
                    context.Batch,
                    BattleSaveContext.Empty,
                    addResult: null
                );
            }
        }
    }

    internal static bool TargetMarkMirrorsStatus(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        StringName statusId
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition is MarkTargetActionPayloadDefinition payload
                    && ProgressionDataUtils.to_string_name(payload.StateKey) == stateKey
                    && ProgressionDataUtils.to_string_name(payload.MirrorStatusId) == statusId
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal IReadOnlyList<StringName> ClearTargetMarksForDefeatedUnit(
        BattleState state,
        BattleUnitState defeatedUnit
    )
    {
        var changedUnitIds = new List<StringName>();
        if (state == null || defeatedUnit == null || defeatedUnit.unit_id == "")
            return changedUnitIds;

        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (mark?.IsValid != true)
                continue;
            EquipmentAbilityBindingDefinition binding = ResolveBindingForTargetMark(mark);
            bool targetWasDefeated =
                mark.TargetUnitId == defeatedUnit.unit_id
                && ShouldRemoveTargetMarkOnTargetDefeated(binding, mark);
            bool sourceWasDefeated =
                mark.SourceUnitId == defeatedUnit.unit_id && mark.RemoveOnSourceMissing;
            if (!targetWasDefeated && !sourceWasDefeated)
                continue;

            BattleUnitState targetUnit = targetWasDefeated
                ? defeatedUnit
                : state.GetUnit(mark.TargetUnitId);
            if (
                state.RemoveEquipmentTargetMark(
                    mark.SourceUnitId,
                    mark.SourceEquipmentInstanceId,
                    mark.BindingId,
                    mark.StateKey
                )
            )
            {
                ReconcileTargetMarkStatusesAfterRemoval(state, targetUnit, mark, binding);
                AddUniqueUnitId(changedUnitIds, mark.SourceUnitId);
                AddUniqueUnitId(changedUnitIds, mark.TargetUnitId);
            }
        }
        return changedUnitIds;
    }

    internal IReadOnlyList<StringName> ClearTargetMarksForRemovedEquipmentSources(
        BattleState state,
        BattleUnitState sourceUnit
    )
    {
        var changedUnitIds = new List<StringName>();
        if (state == null || sourceUnit == null || sourceUnit.unit_id == "")
            return changedUnitIds;

        foreach (BattleEquipmentTargetMarkState mark in state.GetEquipmentTargetMarksTyped())
        {
            if (
                mark?.IsValid != true
                || mark.SourceUnitId != sourceUnit.unit_id
                || !mark.RemoveOnSourceMissing
                || HasProjectedEquipmentAbilitySource(sourceUnit, mark)
            )
            {
                continue;
            }

            EquipmentAbilityBindingDefinition binding = ResolveBindingForTargetMark(mark);
            BattleUnitState targetUnit = state.GetUnit(mark.TargetUnitId);
            if (
                !state.RemoveEquipmentTargetMark(
                    mark.SourceUnitId,
                    mark.SourceEquipmentInstanceId,
                    mark.BindingId,
                    mark.StateKey
                )
            )
            {
                continue;
            }

            ReconcileTargetMarkStatusesAfterRemoval(state, targetUnit, mark, binding);
            AddUniqueUnitId(changedUnitIds, mark.SourceUnitId);
            AddUniqueUnitId(changedUnitIds, mark.TargetUnitId);
        }
        return changedUnitIds;
    }

    internal IReadOnlyList<StringName> RefreshEquipmentProjectionAfterDurabilityDestruction(
        BattleUnitState targetUnit,
        BattleEventBatch batch = null
    )
    {
        if (
            targetUnit == null
            || targetUnit.source_member_id == ""
            || _runtime?._unit_factory == null
        )
        {
            return Array.Empty<StringName>();
        }

        IReadOnlyList<StringName> changedUnitIds =
            _runtime._unit_factory.RefreshEquipmentProjection(targetUnit);
        batch?.AddChangedUnitId(targetUnit.unit_id);
        foreach (StringName changedUnitId in changedUnitIds)
            batch?.AddChangedUnitId(changedUnitId);
        return changedUnitIds;
    }

    internal bool ClearStaleEquipmentTargetMarkIfNeeded(
        BattleState state,
        EquipmentAbilityBindingDefinition binding,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (state == null || binding == null || mark?.IsValid != true)
            return false;
        BattleUnitState sourceUnit = state.GetUnit(mark.SourceUnitId);
        BattleUnitState targetUnit = state.GetUnit(mark.TargetUnitId);
        bool sourceMissing =
            !IsLivingUnit(sourceUnit) || !HasProjectedEquipmentAbilitySource(sourceUnit, mark);
        bool targetMissing =
            !IsLivingUnit(targetUnit)
            && ShouldRemoveTargetMarkOnTargetDefeated(binding, mark);
        if (!targetMissing && !(mark.RemoveOnSourceMissing && sourceMissing))
            return false;

        bool removed = state.RemoveEquipmentTargetMark(
            mark.SourceUnitId,
            mark.SourceEquipmentInstanceId,
            mark.BindingId,
            mark.StateKey
        );
        if (removed)
            ReconcileTargetMarkStatusesAfterRemoval(state, targetUnit, mark, binding);
        return removed;
    }

    private static bool HasProjectedEquipmentAbilitySource(
        BattleUnitState sourceUnit,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (sourceUnit == null || mark?.IsValid != true)
            return false;
        foreach (
            BattleEquipmentAbilitySourceState source in sourceUnit.equipment_ability_sources
                ?? new List<BattleEquipmentAbilitySourceState>()
        )
        {
            if (
                source != null
                && source.SourceEquipmentInstanceId == mark.SourceEquipmentInstanceId
                && source.AbilityIds?.Contains(mark.BindingId) == true
            )
            {
                return true;
            }
        }
        return false;
    }

    private EquipmentAbilityBindingDefinition ResolveBindingForTargetMark(
        BattleEquipmentTargetMarkState mark
    )
    {
        if (mark == null || mark.BindingId == "")
            return null;
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
            _runtime?.GetEquipmentAbilityBindingIndexTyped();
        return bindingIndex != null
            && bindingIndex.TryGetValue(mark.BindingId, out EquipmentAbilityBindingDefinition binding)
            ? binding
            : null;
    }

    private static bool IsLivingUnit(BattleUnitState unit) =>
        unit != null && unit.is_alive && unit.current_hp > 0;

    private static bool ShouldRemoveTargetMarkOnTargetDefeated(
        EquipmentAbilityBindingDefinition binding,
        BattleEquipmentTargetMarkState mark
    )
    {
        if (binding == null || mark?.IsValid != true)
            return false;
        StringName stateKey = ProgressionDataUtils.to_string_name(mark.StateKey);
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.PayloadDefinition is not MarkTargetActionPayloadDefinition payload)
                    continue;
                if (ProgressionDataUtils.to_string_name(payload.StateKey) != stateKey)
                    continue;
                if (payload.RemoveOnTargetDefeated)
                    return true;
            }
        }
        return false;
    }

    internal void ReconcileTargetMarkStatusesAfterRemoval(
        BattleState state,
        BattleUnitState targetUnit,
        BattleEquipmentTargetMarkState mark,
        EquipmentAbilityBindingDefinition binding,
        bool preserveExistingMirrorDuration = false
    )
    {
        if (targetUnit == null || mark == null || binding == null)
            return;
        List<StringName> mirrorStatusIds = BuildTargetMarkMirrorStatusIds(
            binding,
            mark.StateKey
        );
        foreach (StringName mirrorStatusId in mirrorStatusIds)
        {
            if (mirrorStatusId == "")
                continue;
            if (
                RefreshTargetMarkMirrorStatus(
                    state,
                    targetUnit,
                    mirrorStatusId,
                    preserveExistingMirrorDuration
                )
            )
                continue;
            targetUnit.EraseStatusEffect(mirrorStatusId);
        }
        foreach (StringName statusId in BuildTargetMarkClearStatusIds(binding, mark.StateKey))
        {
            if (statusId == "" || mirrorStatusIds.Contains(statusId))
                continue;
            BattleStatusEffectState status = targetUnit.GetStatusEffect(statusId);
            if (
                status == null
                || ProgressionDataUtils.to_string_name(status.source_unit_id) != mark.SourceUnitId
            )
            {
                continue;
            }
            targetUnit.EraseStatusEffect(statusId);
        }
    }

    private static List<StringName> BuildTargetMarkMirrorStatusIds(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        var result = new List<StringName>();
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.PayloadDefinition is not MarkTargetActionPayloadDefinition payload)
                    continue;
                if (ProgressionDataUtils.to_string_name(payload.StateKey) != stateKey)
                    continue;
                AddUniqueStatusId(result, payload.MirrorStatusId);
            }
        }
        return result;
    }

    private static IReadOnlyList<StringName> BuildTargetMarkClearStatusIds(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        var result = new List<StringName>();
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.PayloadDefinition is not MarkTargetActionPayloadDefinition payload)
                    continue;
                if (ProgressionDataUtils.to_string_name(payload.StateKey) != stateKey)
                    continue;
                AddUniqueStatusId(result, payload.MirrorStatusId);
                foreach (StringName statusId in payload.ClearStatusIdsOnReplace ?? Array.Empty<StringName>())
                    AddUniqueStatusId(result, statusId);
            }
        }
        return result;
    }

    private static void AddUniqueUnitId(List<StringName> result, StringName unitId)
    {
        if (unitId == "" || result.Contains(unitId))
            return;
        result.Add(unitId);
    }
}
