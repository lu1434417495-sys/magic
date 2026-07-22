using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAbilityConditionEvaluator
{
    private BattleRuntimeModule _runtime;
    private BattleEquipmentAbilityRuntimeService _owner;
    private BattleEquipmentSummonResolver _summonResolver;
    private BattleEquipmentTargetMarkResolver _targetMarkResolver;
    private BattleEquipmentAbilityStateResolver _abilityStateResolver;

    internal void Setup(
        BattleRuntimeModule runtime,
        BattleEquipmentAbilityRuntimeService owner,
        BattleEquipmentSummonResolver summonResolver,
        BattleEquipmentTargetMarkResolver targetMarkResolver,
        BattleEquipmentAbilityStateResolver abilityStateResolver
    )
    {
        _runtime = runtime;
        _owner = owner;
        _summonResolver = summonResolver;
        _targetMarkResolver = targetMarkResolver;
        _abilityStateResolver = abilityStateResolver;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _summonResolver = null;
        _targetMarkResolver = null;
        _abilityStateResolver = null;
    }

    private static readonly StringName ConditionKindCompareFact = "compare_fact";
    private static readonly StringName ConditionKindHasStatus = "has_status";
    private static readonly StringName ConditionKindHasEquipmentTag = "has_equipment_tag";
    private static readonly StringName FactCreatureTypeTags = "creature_type_tags";
    private static readonly StringName FactBattleEnvironmentTag = "battle_environment_tag";
    private static readonly StringName FactHpPercentBp = "hp_percent_bp";
    private static readonly StringName FactCriticalHit = "critical_hit";
    private static readonly StringName FactHpDamage = "hp_damage";
    private static readonly StringName FactSkillDamagedTargetCount =
        "skill_damaged_target_count";
    private static readonly StringName FactSkillKilledTargetCount =
        "skill_killed_target_count";
    private static readonly StringName FactSkillHpDamageDealt =
        "skill_hp_damage_dealt";
    private static readonly StringName FactSkillMovedTargetCount =
        "skill_moved_target_count";
    private static readonly StringName FactSkillUnmovedTargetCount =
        "skill_unmoved_target_count";
    private static readonly StringName FactBodySize = "body_size";
    private static readonly StringName FactAttributeValue = "attribute_value";
    private static readonly StringName FactCurrentTu = "current_tu";
    private static readonly StringName FactCurrentActionPoints = "current_action_points";
    private static readonly StringName FactEquipmentAbilityState = "equipment_ability_state";
    private static readonly StringName FactKillSourceIsAttack = "kill_source_is_attack";
    private static readonly StringName FactKillSourceEquipmentInstanceMatches =
        "kill_source_equipment_instance_matches";
    private static readonly StringName FactKillSourceBindingMatches =
        "kill_source_binding_matches";
    private static readonly StringName FactEquipmentTargetMarkMatches =
        "equipment_target_mark_matches";
    private static readonly StringName FactEquipmentTargetMarkStacks =
        "equipment_target_mark_stacks";
    private static readonly StringName FactExpiredTargetMarkMatches =
        "expired_target_mark_matches";
    private static readonly StringName FactStatusStacks = "status_stacks";
    private static readonly StringName FactNearbyEnemyCount = "nearby_enemy_count";
    private static readonly StringName FactNearbyUnitCount = "nearby_unit_count";
    private static readonly StringName FactNearbyAllyCount = "nearby_ally_count";
    private static readonly StringName FactSummonedUnitCount = "summoned_unit_count";
    private static readonly StringName FactSourceStatusTotalStacks =
        "source_status_total_stacks";
    private static readonly StringName FactUnitDistance = "unit_distance";
    private static readonly StringName FactWeaponRangeType = "weapon_range_type";
    private static readonly StringName QueryKindFact = "fact";
    private static readonly StringName QueryKindLiteral = "literal";

    private static int ApplyFactIntAggregation(
        EquipmentAbilityFactQueryDefinition query,
        long rawValue
    )
    {
        long normalizedValue = Math.Max(rawValue, 0L);
        StringName aggregation = ProgressionDataUtils.to_string_name(
            query?.Aggregation ?? new StringName("")
        );
        if (aggregation == "" || aggregation == "value")
            return BattleEquipmentAbilityStateResolver.ClampFactInt(normalizedValue);
        if (aggregation == "floor_div")
        {
            int divisor = Math.Max(query?.IntLiteral ?? 0, 1);
            return BattleEquipmentAbilityStateResolver.ClampFactInt(normalizedValue / divisor);
        }
        return BattleEquipmentAbilityStateResolver.ClampFactInt(normalizedValue);
    }

    internal bool ConditionGroupPasses(
        EquipmentConditionGroupDefinition group,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext = default,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding = default
    )
    {
        if (group == null)
            return true;
        bool anyMode = group.Mode == "any";
        bool sawAny = false;
        bool passed = anyMode ? false : true;

        foreach (EquipmentAbilityConditionDefinition condition in group.Conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>())
        {
            sawAny = true;
            bool conditionPassed = ConditionPasses(
                condition,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding
            );
            if (anyMode)
            {
                passed = passed || conditionPassed;
            }
            else
            {
                passed = passed && conditionPassed;
            }
        }
        foreach (EquipmentConditionGroupDefinition child in group.Groups ?? Array.Empty<EquipmentConditionGroupDefinition>())
        {
            sawAny = true;
            bool childPassed = ConditionGroupPasses(
                child,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding
            );
            if (anyMode)
            {
                passed = passed || childPassed;
            }
            else
            {
                passed = passed && childPassed;
            }
        }
        if (!sawAny)
            passed = true;
        return group.Negate ? !passed : passed;
    }

    private bool ConditionPasses(
        EquipmentAbilityConditionDefinition condition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding
    )
    {
        if (condition == null)
            return false;
        if (
            condition.Kind == ConditionKindHasEquipmentTag
            && condition.PayloadDefinition is HasEquipmentTagConditionPayloadDefinition equipmentPayload
        )
        {
            return HasEquipmentTagConditionPasses(equipmentPayload, sourceUnit, targetUnit);
        }
        if (
            condition.Kind == ConditionKindHasStatus
            && condition.PayloadDefinition is HasStatusConditionPayloadDefinition statusPayload
        )
        {
            return HasStatusConditionPasses(statusPayload, sourceUnit, targetUnit);
        }
        if (
            condition.Kind == ConditionKindCompareFact
            && condition.PayloadDefinition is CompareFactConditionPayloadDefinition comparePayload
        )
        {
            return CompareFactConditionPasses(
                comparePayload,
                sourceUnit,
                targetUnit,
                factContext,
                activeBinding
            );
        }
        return false;
    }

    private static bool HasStatusConditionPasses(
        HasStatusConditionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (payload == null)
            return false;
        BattleUnitState subject = BattleEquipmentAbilityRuntimeService.ResolveSubject(payload.Subject, sourceUnit, targetUnit);
        StringName statusId = ProgressionDataUtils.to_string_name(payload.StatusId);
        return subject != null && statusId != "" && subject.HasStatusEffect(statusId);
    }

    private bool HasEquipmentTagConditionPasses(
        HasEquipmentTagConditionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        if (payload == null)
            return false;
        BattleUnitState subject = BattleEquipmentAbilityRuntimeService.ResolveSubject(payload.Subject, sourceUnit, targetUnit);
        if (subject == null)
            return false;
        StringName selector = ProgressionDataUtils.to_string_name(payload.EquipmentSelector);
        if (selector == "")
            return false;
        StringName itemId = ProgressionDataUtils.to_string_name(
            subject.GetEquipmentView()?.GetEquippedItemId(selector) ?? ""
        );
        ItemDefinition itemDef = _owner.ResolveItemDef(itemId);
        if (itemDef == null)
            return false;
        bool hasAll = BattleEquipmentAbilityRuntimeService.AllTagsPresent(itemDef, payload.AllTags);
        bool hasAny =
            payload.AnyTags == null
            || payload.AnyTags.Count == 0
            || BattleEquipmentAbilityRuntimeService.AnyTagPresent(itemDef, payload.AnyTags);
        return hasAll && hasAny;
    }

    private bool CompareFactConditionPasses(
        CompareFactConditionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding
    )
    {
        if (payload == null)
            return false;
        if (payload.Compare == "contains")
        {
            IReadOnlyList<StringName> leftSet =
                ResolveFactStringNameSet(payload.Left, sourceUnit, targetUnit, factContext);
            StringName rightValue = ResolveFactStringName(
                payload.Right,
                sourceUnit,
                targetUnit,
                factContext
            );
            if (rightValue == "" || leftSet == null)
                return false;
            foreach (StringName value in leftSet)
                if (value == rightValue)
                    return true;
            return false;
        }
        if (
            TryResolveFactInt(payload.Left, sourceUnit, targetUnit, factContext, activeBinding, out int leftInt)
            && TryResolveFactInt(payload.Right, sourceUnit, targetUnit, factContext, activeBinding, out int rightInt)
        )
        {
            return BattleEquipmentAbilityRuntimeService.CompareInt(leftInt, payload.Compare, rightInt);
        }
        if (payload.Compare == "eq")
        {
            StringName leftValue = ResolveFactStringName(
                payload.Left,
                sourceUnit,
                targetUnit,
                factContext
            );
            StringName rightValue = ResolveFactStringName(
                payload.Right,
                sourceUnit,
                targetUnit,
                factContext
            );
            return leftValue != "" && rightValue != "" && leftValue == rightValue;
        }
        return false;
    }

    internal bool TryResolveFactInt(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext,
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        out int value
    )
    {
        value = 0;
        if (query == null)
            return false;
        if (query.QueryKind == QueryKindLiteral)
        {
            value = query.IntLiteral;
            return true;
        }
        if (query.QueryKind != QueryKindFact)
            return false;
        if (query.FactId == FactCriticalHit)
        {
            value = factContext.CriticalHit ? 1 : 0;
            return true;
        }
        if (query.FactId == FactCurrentTu)
        {
            value = factContext.CurrentTu >= 0
                ? factContext.CurrentTu
                : Math.Max(_runtime?.GetState()?.timeline?.current_tu ?? -1, -1);
            return value >= 0;
        }
        if (query.FactId == FactCurrentActionPoints)
        {
            BattleUnitState apSubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (apSubject == null)
                return false;
            value = Math.Max(apSubject.current_ap, 0);
            return true;
        }
        if (query.FactId == FactKillSourceIsAttack)
        {
            value =
                factContext.KillProvenance.IsAttack
                && factContext.KillProvenance.IncludesWeaponDamage
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactKillSourceEquipmentInstanceMatches)
        {
            StringName sourceEquipmentInstanceId =
                activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("");
            value =
                sourceEquipmentInstanceId != ""
                && factContext.KillProvenance.SourceEquipmentInstanceId
                    == sourceEquipmentInstanceId
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactKillSourceBindingMatches)
        {
            StringName bindingId =
                activeBinding.Binding?.BindingId ?? new StringName("");
            value =
                bindingId != ""
                && factContext.KillProvenance.SourceBindingId == bindingId
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactHpDamage)
        {
            value = Math.Max(factContext.HpDamage, 0);
            return true;
        }
        if (query.FactId == FactSkillDamagedTargetCount)
        {
            value = Math.Max(factContext.SkillDamagedTargetCount, 0);
            return true;
        }
        if (query.FactId == FactSkillKilledTargetCount)
        {
            value = Math.Max(factContext.SkillKilledTargetCount, 0);
            return true;
        }
        if (query.FactId == FactSkillHpDamageDealt)
        {
            value = Math.Max(factContext.SkillHpDamageDealt, 0);
            return true;
        }
        if (query.FactId == FactSkillMovedTargetCount)
        {
            value = Math.Max(factContext.SkillMovedTargetCount, 0);
            return true;
        }
        if (query.FactId == FactSkillUnmovedTargetCount)
        {
            value = Math.Max(factContext.SkillUnmovedTargetCount, 0);
            return true;
        }
        if (query.FactId == FactBodySize)
        {
            BattleUnitState bodySizeSubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (bodySizeSubject == null)
                return false;
            value = Math.Max(bodySizeSubject.body_size, 0);
            return true;
        }
        if (query.FactId == FactAttributeValue)
        {
            BattleUnitState attributeSubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName attributeId = ProgressionDataUtils.to_string_name(query.AttributeId);
            if (attributeSubject?.attribute_snapshot == null || attributeId == "")
                return false;
            value = attributeSubject.attribute_snapshot.GetValue(attributeId);
            return true;
        }
        if (query.FactId == FactEquipmentAbilityState)
        {
            BattleUnitState owner = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            EquipmentAbilityBindingDefinition stateBinding = _abilityStateResolver.ResolveStateBinding(
                activeBinding,
                activeBinding.Binding,
                query.BindingId
            );
            StringName stateKey = ProgressionDataUtils.to_string_name(query.StateKey);
            if (owner == null || stateBinding == null || stateKey == "")
                return false;
            if (BattleEquipmentAbilityStateResolver.IsPersistentCounterState(stateBinding, stateKey))
            {
                EquipmentInstanceState instance = EquipmentAbilityUsageRuntime.FindEquipmentInstance(
                    owner,
                    activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("")
                );
                if (instance == null)
                    return false;
                value = ApplyFactIntAggregation(
                    query,
                    BattleEquipmentAbilityStateResolver.GetPersistentCounterValue(instance, stateBinding, stateKey, 0)
                );
                return true;
            }
            StringName chargeKey = BattleEquipmentAbilityStateResolver.BuildBindingStateChargeKey(
                activeBinding.Source,
                stateBinding,
                stateKey
            );
            if (chargeKey == "")
                return false;
            value = ApplyFactIntAggregation(
                query,
                BattleEquipmentAbilityStateResolver.GetAbilityStateValue(owner, stateBinding, chargeKey, stateKey, 0)
            );
            return true;
        }
        if (
            query.FactId == FactEquipmentTargetMarkMatches
            || query.FactId == FactEquipmentTargetMarkStacks
        )
        {
            if (
                !_targetMarkResolver.TryResolveEquipmentTargetMark(
                    query,
                    sourceUnit,
                    targetUnit,
                    factContext,
                    activeBinding,
                    out BattleEquipmentTargetMarkState mark
                )
            )
            {
                return false;
            }
            BattleUnitState markSubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            bool subjectMatches = markSubject != null && mark.TargetUnitId == markSubject.unit_id;
            if (query.FactId == FactEquipmentTargetMarkMatches)
            {
                value = subjectMatches ? 1 : 0;
                return true;
            }
            value = markSubject == null || subjectMatches ? Math.Max(mark.Stacks, 0) : 0;
            return true;
        }
        if (query.FactId == FactExpiredTargetMarkMatches)
        {
            BattleEquipmentTargetMarkState expiredMark = factContext.ExpiredTargetMark;
            StringName bindingId = ProgressionDataUtils.to_string_name(query.BindingId);
            StringName stateKey = ProgressionDataUtils.to_string_name(query.StateKey);
            BattleUnitState expiredSubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            value =
                expiredMark?.IsValid == true
                && expiredMark.SourceUnitId == (sourceUnit?.unit_id ?? new StringName(""))
                && expiredMark.SourceEquipmentInstanceId
                    == (activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName(""))
                && (bindingId == "" || expiredMark.BindingId == bindingId)
                && (stateKey == "" || expiredMark.StateKey == stateKey)
                && (expiredSubject == null || expiredMark.TargetUnitId == expiredSubject.unit_id)
                    ? 1
                    : 0;
            return true;
        }
        if (query.FactId == FactStatusStacks)
        {
            BattleUnitState statusSubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName statusId = ProgressionDataUtils.to_string_name(query.StatusId);
            if (statusSubject == null || statusId == "")
                return false;
            BattleStatusEffectState status = statusSubject.GetStatusEffect(statusId);
            if (
                query.RequireSourceUnitMatch
                && (sourceUnit == null || status?.source_unit_id != sourceUnit.unit_id)
            )
            {
                value = 0;
                return true;
            }
            value = Math.Max(status?.stacks ?? 0, 0);
            return true;
        }
        if (
            query.FactId == FactNearbyEnemyCount
            || query.FactId == FactNearbyUnitCount
            || query.FactId == FactNearbyAllyCount
        )
        {
            BattleUnitState nearbySubject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (nearbySubject == null)
                return false;
            BattleState state =
                factContext.BattleState
                ?? _runtime?.GetState();
            if (state == null)
                return false;
            int radius = Math.Max(query.IntLiteral, 0);
            if (query.FactId == FactNearbyUnitCount)
                value = CountNearbyLivingUnits(state, nearbySubject, radius);
            else if (query.FactId == FactNearbyAllyCount)
                value = CountNearbyLivingAllies(state, nearbySubject, radius);
            else
                value = CountNearbyLivingEnemies(state, nearbySubject, radius);
            return true;
        }
        if (query.FactId == FactUnitDistance)
        {
            if (sourceUnit == null || targetUnit == null)
                return false;
            value = BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, targetUnit);
            return true;
        }
        if (query.FactId == FactSourceStatusTotalStacks)
        {
            BattleUnitState stacksOwner = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            StringName totalStatusId = ProgressionDataUtils.to_string_name(query.StatusId);
            if (stacksOwner == null || totalStatusId == "")
                return false;
            BattleState totalState =
                factContext.BattleState
                ?? _runtime?.GetState();
            if (totalState == null)
                return false;
            int totalStacks = 0;
            foreach (BattleUnitState unit in totalState.GetUnitsTyped())
            {
                BattleStatusEffectState status = unit?.GetStatusEffect(totalStatusId);
                if (status == null || status.stacks <= 0)
                    continue;
                if (
                    ProgressionDataUtils.to_string_name(status.source_unit_id)
                    != stacksOwner.unit_id
                )
                {
                    continue;
                }
                totalStacks += status.stacks;
            }
            value = totalStacks;
            return true;
        }
        if (query.FactId == FactSummonedUnitCount)
        {
            BattleUnitState summonOwner = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            if (summonOwner == null)
                return false;
            BattleState state =
                factContext.BattleState
                ?? _runtime?.GetState();
            if (state == null)
                return false;
            EquipmentAbilityBindingDefinition summonBinding = _abilityStateResolver.ResolveStateBinding(
                activeBinding,
                activeBinding.Binding,
                query.BindingId
            );
            BattleUnitState radiusSubject = query.IntLiteral > 0
                ? BattleEquipmentAbilityRuntimeService.ResolveSubject(query.ValueKind, sourceUnit, targetUnit)
                : null;
            value = _summonResolver.CountLivingSummonedUnits(
                state,
                summonOwner,
                activeBinding.Source,
                summonBinding?.BindingId ?? query.BindingId,
                query.StateKey,
                radiusSubject,
                query.IntLiteral > 0 ? query.IntLiteral : -1
            );
            return true;
        }
        if (query.FactId != FactHpPercentBp)
            return false;
        BattleUnitState subject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
        if (subject == null)
            return false;
        int maxHp = Math.Max(subject.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 0, 1);
        value = Math.Clamp(subject.current_hp * 10000 / maxHp, 0, 10000);
        return true;
    }

    private static int CountNearbyLivingEnemies(
        BattleState state,
        BattleUnitState sourceUnit,
        int radius
    )
    {
        if (state == null || sourceUnit == null || radius < 0)
            return 0;
        int count = 0;
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == sourceUnit.unit_id
                || candidate.faction_id == sourceUnit.faction_id
            )
            {
                continue;
            }
            if (BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, candidate) <= radius)
                count++;
        }
        return count;
    }

    private static int CountNearbyLivingAllies(
        BattleState state,
        BattleUnitState sourceUnit,
        int radius
    )
    {
        if (state == null || sourceUnit == null || radius < 0)
            return 0;
        int count = 0;
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == sourceUnit.unit_id
                || candidate.faction_id != sourceUnit.faction_id
            )
            {
                continue;
            }
            if (BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, candidate) <= radius)
                count++;
        }
        return count;
    }

    private static int CountNearbyLivingUnits(
        BattleState state,
        BattleUnitState sourceUnit,
        int radius
    )
    {
        if (state == null || sourceUnit == null || radius < 0)
            return 0;
        int count = 0;
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || !candidate.is_alive
                || candidate.unit_id == sourceUnit.unit_id
            )
            {
                continue;
            }
            if (BattleGridDistanceService.GetDistanceBetweenUnits(sourceUnit, candidate) <= radius)
                count++;
        }
        return count;
    }

    private IReadOnlyList<StringName> ResolveFactStringNameSet(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext
    )
    {
        if (query == null || query.QueryKind != QueryKindFact)
            return Array.Empty<StringName>();
        if (query.FactId == FactCreatureTypeTags)
        {
            BattleUnitState subject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            return subject != null
                ? subject.creature_type_tags
                : Array.Empty<StringName>();
        }
        if (query.FactId == FactBattleEnvironmentTag)
        {
            return factContext.BattleState?.GetEnvironmentSnapshot()?.GlobalEnvironmentTags
                ?? _runtime?.GetState()?.GetEnvironmentSnapshot()?.GlobalEnvironmentTags
                ?? Array.Empty<StringName>();
        }
        return Array.Empty<StringName>();
    }

    private StringName ResolveFactStringName(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        EquipmentAbilityFactContext factContext
    )
    {
        if (query == null)
            return "";
        if (query.QueryKind == QueryKindLiteral)
            return ProgressionDataUtils.to_string_name(query.StringNameLiteral);
        if (query.QueryKind == QueryKindFact && query.FactId == FactWeaponRangeType)
        {
            BattleUnitState subject = BattleEquipmentAbilityRuntimeService.ResolveSubject(query.Subject, sourceUnit, targetUnit);
            return ProgressionDataUtils.to_string_name(subject?.weapon_range_type ?? new StringName(""));
        }
        IReadOnlyList<StringName> values = ResolveFactStringNameSet(
            query,
            sourceUnit,
            targetUnit,
            factContext
        );
        return values.Count > 0 ? values[0] : "";
    }
}
