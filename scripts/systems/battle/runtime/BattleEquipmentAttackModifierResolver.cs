using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAttackModifierResolver
    : IBattleEquipmentAttackCheckQuery,
      IBattleEquipmentDamageQuery
{
    private BattleEquipmentAbilityRuntimeService _owner;
    private BattleEquipmentAbilityConditionEvaluator _conditionEvaluator;
    private BattleEquipmentSummonResolver _summonResolver;
    private BattleEquipmentDirectEffectActionResolver _directEffectActionResolver;

    internal void Setup(
        BattleEquipmentAbilityRuntimeService owner,
        BattleEquipmentAbilityConditionEvaluator conditionEvaluator,
        BattleEquipmentSummonResolver summonResolver,
        BattleEquipmentDirectEffectActionResolver directEffectActionResolver
    )
    {
        _owner = owner;
        _conditionEvaluator = conditionEvaluator;
        _summonResolver = summonResolver;
        _directEffectActionResolver = directEffectActionResolver;
    }

    internal void DisposeRuntime()
    {
        _owner = null;
        _conditionEvaluator = null;
        _summonResolver = null;
        _directEffectActionResolver = null;
    }

    IReadOnlyList<BattleAttackRollModifierSpec>
        IBattleEquipmentAttackCheckQuery.CollectAttackRollModifierCandidates(
            BattleAttackCheckPolicyContext context
        ) => CollectAttackRollModifierCandidates(context);

    EquipmentAttackDefenseAdjustment
        IBattleEquipmentAttackCheckQuery.CollectAttackDefenseAdjustment(
            BattleAttackCheckPolicyContext context
        ) => CollectAttackDefenseAdjustment(context);

    BattleEquipmentAbilityCriticalHitOverrideResult
        IBattleEquipmentAttackCheckQuery.ResolveCriticalHitOverride(
            BattleAttackCheckPolicyContext context
        ) => ResolveCriticalHitOverride(context);

    IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult>
        IBattleEquipmentDamageQuery.CollectBonusDamageDiceOnHit(
            BattleEquipmentAbilityBonusDamageDiceContext context
        ) => CollectBonusDamageDiceOnHit(context);

    StringName IBattleEquipmentDamageQuery.ResolveDamageRollModeOverride(
        BattleEquipmentAbilityDamageRollModeContext context
    ) => ResolveDamageRollModeOverride(context);

    IReadOnlyList<BattleEquipmentAbilityDamageReductionResult>
        IBattleEquipmentDamageQuery.CollectDamageReductions(
            BattleEquipmentAbilityDamageReductionContext context
        ) => CollectDamageReductions(context);

    internal List<BattleAttackRollModifierSpec> CollectAttackRollModifierCandidates(
        BattleAttackCheckPolicyContext context
    )
    {
        var result = new List<BattleAttackRollModifierSpec>();
        BattleUnitState attacker = ResolveContextAttacker(context);
        BattleUnitState target = ResolveContextTarget(context);
        if (context == null || attacker == null || target == null)
            return result;

        foreach (
            BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(attacker)
        )
        {
            CollectAttackRollBonusActions(
                context,
                activeBinding,
                attacker,
                target,
                defensiveSource: false,
                result
            );
            CollectAttackRollAdvantageActions(
                context,
                activeBinding,
                attacker,
                target,
                defensiveSource: false,
                result
            );
        }
        foreach (
            BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(target)
        )
        {
            CollectAttackRollBonusActions(
                context,
                activeBinding,
                target,
                attacker,
                defensiveSource: true,
                result
            );
        }
        _summonResolver.CollectSummonedUnitAttackRollModifierActions(context, attacker, target, result);
        return result;
    }

    internal EquipmentAttackDefenseAdjustment CollectAttackDefenseAdjustment(
        BattleAttackCheckPolicyContext context
    )
    {
        var adjustment = new EquipmentAttackDefenseAdjustment();
        BattleUnitState attacker = ResolveContextAttacker(context);
        BattleUnitState target = ResolveContextTarget(context);
        if (context == null || attacker == null || target == null)
            return adjustment;

        foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(attacker))
        {
            CollectAttackDefenseModifierActions(context, activeBinding, attacker, target, adjustment);
        }
        return adjustment;
    }

    internal BattleEquipmentAbilityCriticalHitOverrideResult ResolveCriticalHitOverride(
        BattleAttackCheckPolicyContext context
    )
    {
        BattleUnitState attacker = ResolveContextAttacker(context);
        BattleUnitState target = ResolveContextTarget(context);
        if (context == null || attacker == null || target == null || context.force_hit_no_crit)
            return BattleEquipmentAbilityCriticalHitOverrideResult.None;

        foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(attacker))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                    || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                    || !_conditionEvaluator.ConditionGroupPasses(
                        reaction.ConditionGroup,
                        attacker,
                        target,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                        activeBinding
                    )
                )
                {
                    continue;
                }
                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    if (
                        action == null
                        || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindCriticalHitOverride
                        || action.PayloadDefinition is not CriticalHitOverrideActionPayloadDefinition payload
                        || (payload.RequireWeaponDamage && !ContextIncludesWeaponDamage(context))
                        || !AttackRollPayloadSelectorMatches(payload.TargetSelector, defensiveSource: false)
                        || !_conditionEvaluator.ConditionGroupPasses(
                            action.ConditionGroup,
                            attacker,
                            target,
                            EquipmentAbilityFactContext.FromBattleState(context.battle_state),
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
                    return new BattleEquipmentAbilityCriticalHitOverrideResult
                    {
                        ForceCriticalOnHit = true,
                        SourceEquipmentInstanceId =
                            activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName(""),
                        BindingId = binding.BindingId,
                        ActionId = action.ActionId,
                    };
                }
            }
        }
        return BattleEquipmentAbilityCriticalHitOverrideResult.None;
    }

    private void CollectAttackRollBonusActions(
        BattleAttackCheckPolicyContext context,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool defensiveSource,
        List<BattleAttackRollModifierSpec> result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        if (binding?.Reactions == null || result == null)
            return;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            if (
                reaction == null
                || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                || !_conditionEvaluator.ConditionGroupPasses(
                    reaction.ConditionGroup,
                    sourceUnit,
                    targetUnit,
                    EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                    activeBinding
                )
            )
            {
                continue;
            }
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action == null
                    || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindAttackRollBonus
                    || action.PayloadDefinition is not AttackRollBonusActionPayloadDefinition payload
                    || (payload.RequireWeaponDamage && !ContextIncludesWeaponDamage(context))
                    || !AttackRollPayloadSelectorMatches(payload.TargetSelector, defensiveSource)
                    || !_conditionEvaluator.ConditionGroupPasses(
                        action.ConditionGroup,
                        sourceUnit,
                        targetUnit,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
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
                int modifierDelta = ResolveAttackRollBonusDelta(payload, sourceUnit);
                if (modifierDelta == 0)
                {
                    continue;
                }
                result.Add(
                    new BattleAttackRollModifierSpec
                    {
                        source_domain = "equipment_ability",
                        source_id = binding.BindingId,
                        source_instance_id =
                            activeBinding.Source?.SourceEquipmentInstanceId.ToString() ?? "",
                        label = BattleEquipmentAbilityRuntimeService.ResolveActionLabel(binding, payload.Label),
                        modifier_delta = modifierDelta,
                        stack_key = action.ActionId != ""
                            ? action.ActionId
                            : binding.BindingId,
                        stack_mode = payload.StackMode == "" ? BattleEquipmentAbilityRuntimeService.StackModeMax : payload.StackMode,
                        target_team_filter = defensiveSource ? "any" : "enemy",
                        endpoint_mode = "target",
                        footprint_mode = "any_cell",
                        applies_to = "attack_roll",
                    }
                );
            }
        }
    }

    private static int ResolveAttackRollBonusDelta(
        AttackRollBonusActionPayloadDefinition payload,
        BattleUnitState sourceUnit
    )
    {
        if (payload == null)
            return 0;
        int result = payload.Bonus;
        StringName attributeModifierId =
            ProgressionDataUtils.to_string_name(payload.AttributeModifierId);
        if (attributeModifierId != "" && sourceUnit?.attribute_snapshot != null)
        {
            result += sourceUnit.attribute_snapshot.GetValue(attributeModifierId);
        }
        return result;
    }

    internal static int ClampSignedModifier(int value, int maxAbsolute)
    {
        int limit = Math.Max(maxAbsolute, 0);
        if (limit == 0)
            return value;
        return Math.Clamp(value, -limit, limit);
    }

    private void CollectAttackRollAdvantageActions(
        BattleAttackCheckPolicyContext context,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool defensiveSource,
        List<BattleAttackRollModifierSpec> result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        if (binding?.Reactions == null || result == null)
            return;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            if (
                reaction == null
                || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                || !_conditionEvaluator.ConditionGroupPasses(
                    reaction.ConditionGroup,
                    sourceUnit,
                    targetUnit,
                    EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                    activeBinding
                )
            )
            {
                continue;
            }
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action == null
                    || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindAttackRollAdvantage
                    || action.PayloadDefinition is not AttackRollAdvantageActionPayloadDefinition payload
                    || payload.Mode != "advantage"
                    || !AttackRollPayloadSelectorMatches(payload.TargetSelector, defensiveSource)
                    || !_conditionEvaluator.ConditionGroupPasses(
                        action.ConditionGroup,
                        sourceUnit,
                        targetUnit,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                        activeBinding
                    )
                )
                {
                    continue;
                }
                result.Add(
                    new BattleAttackRollModifierSpec
                    {
                        source_domain = "equipment_ability",
                        source_id = binding.BindingId,
                        source_instance_id =
                            activeBinding.Source?.SourceEquipmentInstanceId.ToString() ?? "",
                        label = BattleEquipmentAbilityRuntimeService.ResolveActionLabel(binding, payload.Label),
                        modifier_delta = 0,
                        stack_key = action.ActionId != ""
                            ? action.ActionId
                            : binding.BindingId,
                        stack_mode = payload.StackMode == "" ? BattleEquipmentAbilityRuntimeService.StackModeMax : payload.StackMode,
                        target_team_filter = defensiveSource ? "any" : "enemy",
                        endpoint_mode = "target",
                        footprint_mode = "any_cell",
                        applies_to = "attack_advantage",
                    }
                );
            }
        }
    }

    private void CollectAttackDefenseModifierActions(
        BattleAttackCheckPolicyContext context,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAttackDefenseAdjustment result
    )
    {
        EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
        if (binding?.Reactions == null || result == null)
            return;
        foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
        {
            if (
                reaction == null
                || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                || !_conditionEvaluator.ConditionGroupPasses(
                    reaction.ConditionGroup,
                    sourceUnit,
                    targetUnit,
                    EquipmentAbilityFactContext.FromBattleState(context.battle_state),
                    activeBinding
                )
            )
            {
                continue;
            }
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action == null
                    || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindAttackDefenseModifier
                    || action.PayloadDefinition is not EquipmentAttackDefenseModifierDefinition payload
                    || !AttackDefensePayloadTargetFiltersPass(payload, targetUnit)
                    || !_conditionEvaluator.ConditionGroupPasses(
                        action.ConditionGroup,
                        sourceUnit,
                        targetUnit,
                        EquipmentAbilityFactContext.FromBattleState(context.battle_state),
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
                foreach (StringName componentId in payload.IgnoredAcComponents ?? Array.Empty<StringName>())
                {
                    result.AddIgnoredAcComponent(componentId);
                }
                foreach (EquipmentAcComponentMultiplierDefinition multiplier in payload.AcComponentMultipliers ?? Array.Empty<EquipmentAcComponentMultiplierDefinition>())
                {
                    if (multiplier != null)
                    {
                        result.AddComponentMultiplier(
                            multiplier.AcComponentId,
                            multiplier.MultiplierPercent
                        );
                    }
                }
                if (payload.LockDodgeBonus)
                {
                    result.AddLockDodgeBonus();
                }
            }
        }
    }

    private bool AttackDefensePayloadTargetFiltersPass(
        EquipmentAttackDefenseModifierDefinition payload,
        BattleUnitState targetUnit
    )
    {
        if (payload == null)
            return false;
        StringName selector = ProgressionDataUtils.to_string_name(
            payload.RequiredTargetEquipmentSelector
        );
        bool hasTagFilter = payload.RequiredTargetItemTags != null
            && payload.RequiredTargetItemTags.Count > 0;
        bool hasTypeFilter = payload.RequiredTargetEquipmentTypeIds != null
            && payload.RequiredTargetEquipmentTypeIds.Count > 0;
        if (!hasTagFilter && !hasTypeFilter && selector == "")
            return true;
        StringName slotId = "";
        if (selector == "target_armor")
            slotId = "body";
        else if (selector == "target_shield")
            slotId = "off_hand";
        if (slotId == "" || targetUnit == null)
            return false;
        StringName itemId = ProgressionDataUtils.to_string_name(
            targetUnit.GetEquipmentView()?.GetEquippedItemId(slotId) ?? ""
        );
        ItemDefinition itemDef = _owner.ResolveItemDef(itemId);
        if (itemDef == null)
            return false;
        if (!BattleEquipmentAbilityRuntimeService.AllTagsPresent(itemDef, payload.RequiredTargetItemTags))
            return false;
        if (hasTypeFilter)
        {
            StringName equipmentTypeId = itemDef.GetEquipmentTypeIdNormalized();
            bool matched = false;
            foreach (StringName requiredType in payload.RequiredTargetEquipmentTypeIds)
            {
                if (equipmentTypeId == requiredType)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;
        }
        return true;
    }

    private static bool AttackRollPayloadSelectorMatches(
        StringName targetSelector,
        bool defensiveSource
    )
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        if (!defensiveSource)
            return selector == "" || selector == "target" || selector == "attack_target";
        return selector == "attacker" || selector == "source_attacker";
    }

    private static bool ContextIncludesWeaponDamage(BattleAttackCheckPolicyContext context)
    {
        foreach (CombatEffectDefinition effect in context?.skill_definition?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect != null && (effect.AddWeaponDice || effect.RequiresWeapon))
                return true;
        }
        return false;
    }

    internal IReadOnlyList<StringName> CollectProjectedWeaponEffectCategories(
        BattleUnitState sourceUnit,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        SkillDefinition skillDefinition = null
    )
    {
        IReadOnlyList<CombatEffectDefinition> normalizedEffects =
            effectDefinitions as IReadOnlyList<CombatEffectDefinition>
            ?? new List<CombatEffectDefinition>(
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
            );
        if (
            sourceUnit == null
            || sourceUnit.weapon_item_id == ""
            || sourceUnit.weapon_range_type != "ranged"
            || !EffectsIncludeWeaponDamage(normalizedEffects)
        )
        {
            return Array.Empty<StringName>();
        }

        ItemDefinition weaponDefinition = _owner.ResolveItemDef(sourceUnit.weapon_item_id);
        if (
            weaponDefinition == null
            || !weaponDefinition.IsWeapon()
            || weaponDefinition.GetWeaponRangeType() != "ranged"
        )
        {
            return Array.Empty<StringName>();
        }

        var categories = new List<StringName>();
        var seen = new HashSet<StringName>();
        foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(sourceUnit))
        {
            if (activeBinding.Source?.EquipmentDefId != sourceUnit.weapon_item_id)
                continue;
            foreach (
                EquipmentAbilityReactionDefinition reaction in activeBinding.Binding?.Reactions
                    ?? Array.Empty<EquipmentAbilityReactionDefinition>()
            )
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterHit
                )
                {
                    continue;
                }
                foreach (
                    StringName category in reaction.ProjectedEffectCategories
                        ?? Array.Empty<StringName>()
                )
                {
                    StringName normalized = ProgressionDataUtils.to_string_name(category);
                    if (normalized != "" && seen.Add(normalized))
                        categories.Add(normalized);
                }
            }
        }

        IReadOnlyList<StringName> explicitCategories = BattleEffectCategoryResolver.ResolveCategories(
            skillDefinition,
            normalizedEffects,
            categories
        );
        bool hasExplicitMissileCategory = false;
        foreach (StringName category in explicitCategories)
        {
            if (category != "nonmagical_missile" && category != "magical_missile")
                continue;
            hasExplicitMissileCategory = true;
            break;
        }
        if (!hasExplicitMissileCategory)
        {
            categories.Insert(0, new StringName("nonmagical_missile"));
        }
        return categories.Count == 0 ? Array.Empty<StringName>() : categories;
    }

    private static bool EffectsIncludeWeaponDamage(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        if (effectDefinitions == null)
            return false;
        foreach (CombatEffectDefinition effect in effectDefinitions)
        {
            if (effect != null && (effect.AddWeaponDice || effect.RequiresWeapon))
                return true;
        }
        return false;
    }

    internal IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> CollectBonusDamageDiceOnHit(
        BattleEquipmentAbilityBonusDamageDiceContext context
    )
    {
        var result = new List<BattleEquipmentAbilityBonusDamageDiceResult>();
        if (
            context == null
            || context.SourceUnit == null
            || context.TargetUnit == null
            || !context.AttackSucceeded
        )
        {
            return result;
        }

        foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                    || reaction.Timing != EquipmentAbilityTimingKind.AfterHit
                    || !BattleEquipmentDirectEffectActionResolver.HasAddDamageDiceAction(reaction)
                    || !_conditionEvaluator.ConditionGroupPasses(
                        reaction.ConditionGroup,
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
                        reaction.RollGate,
                        binding.BindingId,
                        reaction.ReactionId,
                        "",
                        forcedRollValue: 0,
                        new BattleEquipmentAbilityAfterHitResult()
                    )
                )
                {
                    continue;
                }
                _directEffectActionResolver.CollectBonusDamageDiceActions(activeBinding, reaction, context, result);
            }
        }
        return result;
    }

    internal StringName ResolveDamageRollModeOverride(
        BattleEquipmentAbilityDamageRollModeContext context
    )
    {
        StringName rollMode = ProgressionDataUtils.to_string_name(
            context?.CurrentRollMode ?? new StringName("")
        );
        if (context == null || context.SourceUnit == null || context.TargetUnit == null)
            return rollMode;

        foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(context.SourceUnit))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnDamageRoll
                    || reaction.Timing != EquipmentAbilityTimingKind.BeforeDamage
                    || !_conditionEvaluator.ConditionGroupPasses(
                        reaction.ConditionGroup,
                        context.SourceUnit,
                        context.TargetUnit,
                        EquipmentAbilityFactContext.FromDamageRollMode(context),
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

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    if (
                        action == null
                        || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindDamageRollModeOverride
                        || action.PayloadDefinition is not DamageRollModeOverrideActionPayloadDefinition payload
                        || !DamageRollModePayloadSelectorMatches(payload.TargetSelector)
                        || !_conditionEvaluator.ConditionGroupPasses(
                            action.ConditionGroup,
                            context.SourceUnit,
                            context.TargetUnit,
                            EquipmentAbilityFactContext.FromDamageRollMode(context),
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

                    StringName nextMode = ProgressionDataUtils.to_string_name(payload.RollMode);
                    if (nextMode != "")
                        rollMode = nextMode;
                }
            }
        }
        return rollMode;
    }

    internal IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
        BattleEquipmentAbilityDamageReductionContext context
    )
    {
        var result = new List<BattleEquipmentAbilityDamageReductionResult>();
        StringName damageTag = ProgressionDataUtils.to_string_name(context?.DamageTag ?? new StringName(""));
        if (context == null || context.SourceUnit == null || context.TargetUnit == null || damageTag == "")
            return result;

        EquipmentAbilityFactContext factContext =
            EquipmentAbilityFactContext.FromDamageReduction(context);
        BattleUnitState holder = context.TargetUnit;
        BattleUnitState attacker = context.SourceUnit;
        foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(holder))
        {
            EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
            if (binding?.Reactions == null)
                continue;
            foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
            {
                if (
                    reaction == null
                    || reaction.Trigger != EquipmentAbilityTriggerKind.OnDamageRoll
                    || reaction.Timing != EquipmentAbilityTimingKind.BeforeDamage
                    || !_conditionEvaluator.ConditionGroupPasses(
                        reaction.ConditionGroup,
                        holder,
                        attacker,
                        factContext,
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

                foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
                {
                    if (
                        action == null
                        || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindDamageReduction
                        || action.PayloadDefinition is not DamageReductionActionPayloadDefinition payload
                        || payload.Amount <= 0
                        || !DamageReductionPayloadSelectorMatches(payload.TargetSelector)
                        || !DamageReductionMatchesTag(payload, damageTag)
                        || !_conditionEvaluator.ConditionGroupPasses(
                            action.ConditionGroup,
                            holder,
                            attacker,
                            factContext,
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

                    result.Add(
                        new BattleEquipmentAbilityDamageReductionResult
                        {
                            BindingId = binding.BindingId,
                            ActionId = action.ActionId,
                            Amount = Math.Max(payload.Amount, 0),
                            Label = BattleEquipmentAbilityRuntimeService.ResolveActionLabel(binding, payload.Label),
                        }
                    );
                }
            }
        }
        return result;
    }

    private static bool DamageReductionPayloadSelectorMatches(StringName targetSelector)
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        return selector == ""
            || selector == "self"
            || selector == "holder"
            || selector == "defender"
            || selector == "damage_target";
    }

    private static bool DamageReductionMatchesTag(
        DamageReductionActionPayloadDefinition payload,
        StringName damageTag
    )
    {
        if (payload?.DamageTags == null || payload.DamageTags.Count == 0 || damageTag == "")
            return false;
        foreach (StringName value in payload.DamageTags)
        {
            if (ProgressionDataUtils.to_string_name(value) == damageTag)
                return true;
        }
        return false;
    }

    private static bool DamageRollModePayloadSelectorMatches(StringName targetSelector)
    {
        StringName selector = ProgressionDataUtils.to_string_name(targetSelector);
        return selector == "" || selector == "source" || selector == "attacker" || selector == "owner";
    }

    internal List<BattleLootEntry> ApplyLootQuantityMultipliers(
        IEnumerable<BattleLootEntry> lootEntries,
        BattleEquipmentAbilityOnKillResult onKillResult
    )
    {
        var result = new List<BattleLootEntry>();
        foreach (BattleLootEntry entry in lootEntries ?? Array.Empty<BattleLootEntry>())
        {
            BattleLootEntry resolvedEntry = ApplyLootQuantityMultipliers(entry, onKillResult);
            if (resolvedEntry != null)
                result.Add(resolvedEntry);
        }
        return result;
    }

    private BattleLootEntry ApplyLootQuantityMultipliers(
        BattleLootEntry entry,
        BattleEquipmentAbilityOnKillResult onKillResult
    )
    {
        if (entry == null || onKillResult == null || onKillResult.LootMultipliers.Count == 0)
            return entry?.Duplicate();
        int multiplierPercent = 100;
        foreach (BattleEquipmentAbilityLootQuantityMultiplierResult multiplier in onKillResult.LootMultipliers)
        {
            if (LootMultiplierApplies(entry, multiplier?.Payload))
                multiplierPercent = Math.Max(multiplierPercent * multiplier.Payload.MultiplierPercent / 100, 0);
        }
        if (multiplierPercent == 100)
            return entry.Duplicate();
        return entry.WithQuantity(Math.Max(entry.Quantity * multiplierPercent / 100, 1));
    }

    private bool LootMultiplierApplies(
        BattleLootEntry entry,
        LootQuantityMultiplierActionPayloadDefinition payload
    )
    {
        if (entry == null || payload == null || payload.MultiplierPercent <= 0)
            return false;
        if (payload.AffectedDropKinds != null && payload.AffectedDropKinds.Count > 0)
        {
            StringName entryDropKind = BattleLootIds.ToStringName(entry.DropKind);
            bool matchedDropKind = false;
            foreach (StringName affectedDropKind in payload.AffectedDropKinds)
            {
                if (entryDropKind == affectedDropKind)
                {
                    matchedDropKind = true;
                    break;
                }
            }
            if (!matchedDropKind)
                return false;
        }
        if (payload.AnyItemTags == null || payload.AnyItemTags.Count == 0)
            return true;
        ItemDefinition itemDef = _owner.ResolveItemDef(entry.ItemId);
        return BattleEquipmentAbilityRuntimeService.AnyTagPresent(itemDef, payload.AnyItemTags);
    }

    private static BattleUnitState ResolveContextAttacker(BattleAttackCheckPolicyContext context)
    {
        if (context == null)
            return null;
        return context.attacker ?? context.attacker_view.UnsafeUnitForReadOnlyRules;
    }

    private static BattleUnitState ResolveContextTarget(BattleAttackCheckPolicyContext context)
    {
        if (context == null)
            return null;
        return context.target ?? context.target_view.UnsafeUnitForReadOnlyRules;
    }
}
