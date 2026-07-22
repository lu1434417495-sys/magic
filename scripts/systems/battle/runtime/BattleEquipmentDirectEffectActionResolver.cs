using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentDirectEffectActionResolver
{
    private BattleRuntimeModule _runtime;
    private BattleEquipmentAbilityRuntimeService _owner;
    private BattleEquipmentAbilityConditionEvaluator _conditionEvaluator;
    private BattleEquipmentAbilityStateResolver _abilityStateResolver;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleEquipmentAbilityRuntimeService owner,
        BattleEquipmentAbilityConditionEvaluator conditionEvaluator,
        BattleEquipmentAbilityStateResolver abilityStateResolver
    )
    {
        _runtime = runtime;
        _owner = owner;
        _conditionEvaluator = conditionEvaluator;
        _abilityStateResolver = abilityStateResolver;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _conditionEvaluator = null;
        _abilityStateResolver = null;
    }

    internal void ResolveDealDamageAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        DealDamageActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context
    )
    {
        ResolveDealDamageAction(
            activeBinding,
            binding,
            payload,
            context?.SourceUnit,
            context?.TargetUnit,
            context?.BattleState
        );
    }

    internal BattleUnitState ResolveDealDamageAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        DealDamageActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (_owner.DamageResolver == null || payload?.Dice == null || sourceUnit == null)
            return null;
        BattleUnitState resolvedTarget = _owner.ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            "",
            "",
            battleState
        );
        if (resolvedTarget?.is_alive != true)
            return null;
        IReadOnlyList<CombatEffectDefinition> effects = BuildDamageEffects(payload);
        if (effects.Count == 0)
            return null;
        _owner.DamageResolver.ResolveEffects(
            sourceUnit,
            resolvedTarget,
            effects,
            DamageResolutionContext.Empty()
        );
        return resolvedTarget;
    }

    internal BattleUnitState ResolveHealAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        HealActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState
    )
    {
        if (_owner.DamageResolver == null || payload?.Dice == null || sourceUnit == null)
            return null;
        BattleUnitState resolvedTarget = _owner.ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            "",
            "",
            battleState
        );
        if (resolvedTarget == null)
            return null;
        IReadOnlyList<CombatEffectDefinition> effects = BuildHealEffects(payload);
        if (effects.Count == 0)
            return null;
        int previousHp = resolvedTarget.current_hp;
        bool previousAlive = resolvedTarget.is_alive;
        _owner.DamageResolver.ResolveEffects(
            sourceUnit,
            resolvedTarget,
            effects,
            DamageResolutionContext.Empty()
        );
        if (resolvedTarget.current_hp == previousHp && resolvedTarget.is_alive == previousAlive)
            return null;
        return resolvedTarget;
    }

    internal BattleUnitState ResolveHealFromFactAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        HealFromFactActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleState battleState,
        EquipmentAbilityFactContext factContext
    )
    {
        if (payload == null || sourceUnit == null)
            return null;
        if (
            !_conditionEvaluator.TryResolveFactInt(
                payload.AmountFact,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding,
                out int factAmount
            )
        )
        {
            return null;
        }

        int multiplier = Math.Max(payload.MultiplierPercent, 0);
        int healAmount = (int)Math.Max((long)Math.Max(factAmount, 0) * multiplier / 100L, 0L);
        if (payload.MaxAmount > 0)
            healAmount = Math.Min(healAmount, payload.MaxAmount);
        if (healAmount <= 0)
            return null;

        BattleUnitState resolvedTarget = _owner.ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            targetUnit,
            activeBinding,
            binding,
            "",
            "",
            battleState
        );
        if (resolvedTarget?.is_alive != true)
            return null;

        int maxHp = Math.Max(
            resolvedTarget.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 0,
            1
        );
        int healed = resolvedTarget.ApplyHealing(healAmount, maxHp);
        return healed > 0 ? resolvedTarget : null;
    }

    private static IReadOnlyList<CombatEffectDefinition> BuildDamageEffects(
        DealDamageActionPayloadDefinition payload
    )
    {
        if (payload?.Dice == null || payload.DamageType == "")
            return Array.Empty<CombatEffectDefinition>();

        var result = new List<CombatEffectDefinition>();
        bool usedFlatBonus = false;
        int flatBonus = Math.Max(payload.Dice.FlatBonus, 0);
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            result.Add(
                BattleRuntimeEffectDefinitions.Damage(
                    payload.DamageType,
                    term.DiceCount,
                    term.DiceSides,
                    usedFlatBonus ? 0 : flatBonus,
                    payload.DamageTags,
                    payload.MitigationBypassDamageTags,
                    payload.MitigationBypassTiers
                )
            );
            usedFlatBonus = true;
        }
        if (result.Count == 0 && flatBonus > 0)
        {
            result.Add(
                BattleRuntimeEffectDefinitions.Damage(
                    payload.DamageType,
                    0,
                    0,
                    0,
                    payload.DamageTags,
                    payload.MitigationBypassDamageTags,
                    payload.MitigationBypassTiers,
                    power: flatBonus
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<CombatEffectDefinition> BuildHealEffects(
        HealActionPayloadDefinition payload
    )
    {
        if (payload?.Dice == null)
            return Array.Empty<CombatEffectDefinition>();

        var result = new List<CombatEffectDefinition>();
        bool usedFlatBonus = false;
        int flatBonus = Math.Max(payload.Dice.FlatBonus, 0);
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
                continue;
            result.Add(
                BattleRuntimeEffectDefinitions.Heal(
                    term.DiceCount,
                    term.DiceSides,
                    usedFlatBonus ? 0 : flatBonus
                )
            );
            usedFlatBonus = true;
        }
        if (result.Count == 0 && flatBonus > 0)
        {
            result.Add(BattleRuntimeEffectDefinitions.Heal(0, 0, 0, flatBonus));
        }
        return result;
    }

    internal BattleUnitState ResolveModifyActionPointsAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ModifyActionPointsActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        BattleUnitState target = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.TargetSelector ?? "",
            sourceUnit,
            targetUnit
        );
        if (payload == null || sourceUnit == null || target == null)
            return null;

        StringName mode = ProgressionDataUtils.to_string_name(payload.Mode);
        if (mode == "add_base_action_points")
        {
            int amount = payload.Amount > 0
                ? payload.Amount
                : Math.Max(target.attribute_snapshot?.GetValue(AttributeService.ACTION_POINTS) ?? 1, 1);
            target.SetCurrentAp(target.current_ap + amount);
            return target;
        }
        if (mode == "subtract_current_action_points")
        {
            int amount = payload.Amount > 0 ? payload.Amount : 1;
            target.SetCurrentAp(Math.Max(target.current_ap - amount, 0));
            return target;
        }
        if (mode == "restore_current_action_points_capped")
        {
            int amount = Math.Max(payload.Amount, 0);
            int actionPointCap = Math.Max(
                target.attribute_snapshot?.GetValue(AttributeService.ACTION_POINTS) ?? 0,
                0
            );
            int nextActionPoints = Math.Min(
                target.current_ap + amount,
                Math.Max(actionPointCap, target.current_ap)
            );
            if (nextActionPoints <= target.current_ap)
                return null;
            target.SetCurrentAp(nextActionPoints);
            return target;
        }
        if (mode == "set_next_turn_ap_to_zero")
        {
            StringName statusId = ProgressionDataUtils.to_string_name(payload.StatusId);
            if (statusId == "")
                statusId = BattleStatusSemanticTable.STATUS_TEMPORAL_AP_STOLEN;
            CombatEffectDefinition statusEffect = BattleRuntimeEffectDefinitions.Status(
                statusId,
                1,
                -1,
                stackBehavior: "refresh",
                stackLimit: 1,
                displayName: payload.DisplayLabel
            );
            BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
                statusEffect,
                sourceUnit.unit_id,
                target.GetStatusEffect(statusId),
                statusId
            );
            if (statusEntry == null)
                return null;
            target.SetStatusEffect(statusEntry);
            _runtime?.MarkAppliedStatusesForTurnTiming(
                target,
                new Godot.Collections.Array<StringName> { statusId }
            );
            return target;
        }
        return null;
    }

    internal void CollectBonusDamageDiceActions(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityBonusDamageDiceContext context,
        List<BattleEquipmentAbilityBonusDamageDiceResult> result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        bool resolvedLinkedStateActions = false;
        foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindAddDamageDice
                || action.PayloadDefinition is not AddDamageDiceActionPayloadDefinition dicePayload
                || !_conditionEvaluator.ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.TargetUnit,
                    EquipmentAbilityFactContext.FromBonusDamageDice(context),
                    activeBinding
                )
            )
            {
                continue;
            }
            if (
                !_owner.RollGatePasses(
                    action.RollGate,
                    binding.BindingId,
                    reaction.ReactionId,
                    action.ActionId,
                    forcedRollValue: 0,
                    new BattleEquipmentAbilityAfterHitResult()
                )
            )
            {
                continue;
            }
            if (
                !BattleEquipmentAbilityStateResolver.TryConsumeOnceScope(
                    activeBinding.Source,
                    binding,
                    reaction,
                    action,
                    context.SourceUnit
                )
            )
            {
                continue;
            }
            AppendBonusDamageDiceResult(
                activeBinding,
                binding,
                action,
                dicePayload,
                context.SourceUnit,
                context.TargetUnit,
                EquipmentAbilityFactContext.FromBonusDamageDice(context),
                result
            );
            if (!resolvedLinkedStateActions)
            {
                ResolveBonusDamageLinkedSetStateActions(activeBinding, reaction, context);
                resolvedLinkedStateActions = true;
            }
        }
    }

    private void ResolveBonusDamageLinkedSetStateActions(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityReactionDefinition reaction,
        BattleEquipmentAbilityBonusDamageDiceContext context
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action == null
                || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindModifyAbilityState
                || action.PayloadDefinition is not ModifyAbilityStateActionPayloadDefinition statePayload
                || ProgressionDataUtils.to_string_name(statePayload.Operation) != "set"
                || !_conditionEvaluator.ConditionGroupPasses(
                    action.ConditionGroup,
                    context.SourceUnit,
                    context.TargetUnit,
                    EquipmentAbilityFactContext.FromBonusDamageDice(context),
                    activeBinding
                )
            )
            {
                continue;
            }
            if (
                !_owner.RollGatePasses(
                    action.RollGate,
                    binding.BindingId,
                    reaction.ReactionId,
                    action.ActionId,
                    forcedRollValue: 0,
                    new BattleEquipmentAbilityAfterHitResult()
                )
            )
            {
                continue;
            }
            _abilityStateResolver.ResolveModifyAbilityStateAction(
                activeBinding,
                binding,
                statePayload,
                context.SourceUnit,
                context.TargetUnit
            );
        }
    }

    internal void ResolveEquipmentDurabilityAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        EquipmentDurabilityDamageActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (_owner.DamageResolver == null || payload == null)
            return;
        BattleDamageResolver.EquipmentDurabilitySelectionResult selectionResult =
            _owner.DamageResolver.SelectEquipmentForDurabilityDamage(
                new BattleDamageResolver.EquipmentDurabilitySelectionQuery
                {
                    TargetUnit = context.TargetUnit,
                    TargetSlots = payload.TargetSlots,
                    SlotWeights = payload.SlotWeights,
                    ConsumeRandom = true,
                }
            );
        if (!selectionResult.HasSelection)
            return;
        EquipmentAbilityEquipmentTargetRef target = selectionResult.SelectedTarget;
        if (!_owner.EquipmentTargetMatchesRequirements(target, payload))
            return;

        EquipmentDurabilityCommitResult commit =
            _owner.DamageResolver.ApplyEquipmentDurabilityDamageToSelection(
                new EquipmentDurabilityDirectCommitRequest
                {
                    SourceUnit = context.SourceUnit,
                    TargetUnit = context.TargetUnit,
                    TargetEquipment = target,
                    DurabilityLoss = payload.DurabilityLoss,
                    SourceKey = binding.BindingId,
                    ActionId = action.ActionId,
                }
            );
        if (commit.Destroyed)
        {
            _owner.RefreshEquipmentProjectionAfterDurabilityDestruction(
                context.TargetUnit,
                context.Batch
            );
        }
        if (commit.Resolved)
        {
            result.AddDurabilityResult(
                new BattleEquipmentAbilityDurabilityResult
                {
                    BindingId = binding.BindingId,
                    ActionId = action.ActionId,
                    CommitResult = commit,
                }
            );
        }
    }

    internal void ResolveAddDamageDiceAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (payload?.Dice == null)
            return;
        AppendBonusDamageDiceResult(
            activeBinding,
            binding,
            action,
            payload,
            context?.SourceUnit,
            context?.TargetUnit,
            EquipmentAbilityFactContext.FromAfterHit(context),
            result
        );
    }

    private void AppendBonusDamageDiceResult(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        if (result == null)
            return;
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in BuildBonusDamageDiceResults(
            activeBinding,
            binding,
            action,
            payload,
            sourceUnit,
            targetUnit,
            factContext
        ))
        {
            result.AddBonusDamageDice(dice);
        }
    }

    private void AppendBonusDamageDiceResult(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        List<BattleEquipmentAbilityBonusDamageDiceResult> result
    )
    {
        if (result == null)
            return;
        result.AddRange(
            BuildBonusDamageDiceResults(
                activeBinding,
                binding,
                action,
                payload,
                sourceUnit,
                targetUnit,
                factContext
            )
        );
    }

    private IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> BuildBonusDamageDiceResults(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        AddDamageDiceActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext
    )
    {
        if (payload?.Dice == null)
            yield break;
        bool emittedTerm = false;
        foreach (DiceExpressionTermDefinition term in payload.Dice.Terms ?? Array.Empty<DiceExpressionTermDefinition>())
        {
            if (term == null || term.DiceSides <= 0)
                continue;
            int diceCount = Math.Max(term.DiceCount, 0);
            if (
                term.CountBonusFact != null
                && _conditionEvaluator.TryResolveFactInt(
                    term.CountBonusFact,
                    sourceUnit,
                    targetUnit,
                    factContext,
                    activeBinding,
                    out int factBonus
                )
            )
            {
                diceCount += Mathf.FloorToInt(
                    Math.Max(factBonus, 0) * Math.Max(term.CountBonusMultiplier, 0f)
                );
            }
            if (term.MaxDiceCount > 0)
                diceCount = Math.Min(diceCount, term.MaxDiceCount);
            if (diceCount <= 0)
                continue;
            emittedTerm = true;
            yield return new BattleEquipmentAbilityBonusDamageDiceResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                DiceCount = diceCount,
                DiceSides = term.DiceSides,
                FlatBonus = Math.Max(payload.Dice.FlatBonus, 0),
                Subtract = payload.Subtract,
                DamageType = payload.DamageType,
                DamageTags = CopyStringNames(payload.DamageTags),
                MitigationBypassDamageTags = CopyStringNames(payload.MitigationBypassDamageTags),
                MitigationBypassTiers = CopyStringNames(payload.MitigationBypassTiers),
            };
        }
        if (!emittedTerm && payload.Dice.FlatBonus > 0)
        {
            yield return new BattleEquipmentAbilityBonusDamageDiceResult
            {
                BindingId = binding.BindingId,
                ActionId = action.ActionId,
                DiceCount = 0,
                DiceSides = 0,
                FlatBonus = payload.Dice.FlatBonus,
                Subtract = payload.Subtract,
                DamageType = payload.DamageType,
                DamageTags = CopyStringNames(payload.DamageTags),
                MitigationBypassDamageTags = CopyStringNames(payload.MitigationBypassDamageTags),
                MitigationBypassTiers = CopyStringNames(payload.MitigationBypassTiers),
            };
        }
    }

    private static IReadOnlyList<StringName> CopyStringNames(IReadOnlyList<StringName> values)
    {
        if (values == null || values.Count == 0)
            return Array.Empty<StringName>();
        StringName[] result = new StringName[values.Count];
        for (int index = 0; index < values.Count; index++)
            result[index] = ProgressionDataUtils.to_string_name(values[index]);
        return result;
    }

    internal static bool HasAddDamageDiceAction(EquipmentAbilityReactionDefinition reaction)
    {
        foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
        {
            if (
                action?.Kind == BattleEquipmentAbilityRuntimeService.ActionKindAddDamageDice
                && action.PayloadDefinition is AddDamageDiceActionPayloadDefinition
            )
            {
                return true;
            }
        }
        return false;
    }
}
