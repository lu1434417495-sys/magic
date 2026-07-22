using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentSummonResolver
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

    internal void CollectSummonedUnitAttackRollModifierActions(
        BattleAttackCheckPolicyContext context,
        BattleUnitState attacker,
        BattleUnitState target,
        List<BattleAttackRollModifierSpec> result
    )
    {
        BattleState state = context?.battle_state ?? _runtime?.GetState();
        if (state == null || attacker == null || result == null)
            return;

        foreach (BattleUnitState owner in state.GetUnitsTyped())
        {
            if (owner == null || !owner.is_alive || owner.faction_id == attacker.faction_id)
                continue;
            foreach (BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding in _owner.CollectActiveBindings(owner))
            {
                EquipmentAbilityBindingDefinition binding = activeBinding.Binding;
                if (binding?.Reactions == null)
                    continue;
                foreach (EquipmentAbilityReactionDefinition reaction in binding.Reactions)
                {
                    if (
                        reaction == null
                        || reaction.Trigger != EquipmentAbilityTriggerKind.OnHit
                        || reaction.Timing != EquipmentAbilityTimingKind.BeforeHit
                        || !_conditionEvaluator.ConditionGroupPasses(
                            reaction.ConditionGroup,
                            owner,
                            attacker,
                            EquipmentAbilityFactContext.FromBattleState(state),
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
                            || action.Kind != BattleEquipmentAbilityRuntimeService.ActionKindSummonedUnitAttackRollModifier
                            || action.PayloadDefinition is not SummonedUnitAttackRollModifierActionPayloadDefinition payload
                            || !SummonedUnitModifierSelectorMatches(payload.TargetSelector)
                            || !_conditionEvaluator.ConditionGroupPasses(
                                action.ConditionGroup,
                                owner,
                                attacker,
                                EquipmentAbilityFactContext.FromBattleState(state),
                                activeBinding
                            )
                        )
                        {
                            continue;
                        }

                        EquipmentAbilityBindingDefinition sourceBinding = _abilityStateResolver.ResolveStateBinding(
                            activeBinding,
                            binding,
                            payload.SourceBindingId
                        );
                        int count = CountLivingSummonedUnits(
                            state,
                            owner,
                            activeBinding.Source,
                            sourceBinding?.BindingId ?? payload.SourceBindingId,
                            payload.StateKey,
                            attacker,
                            Math.Max(payload.Radius, 0)
                        );
                        if (count < Math.Max(payload.MinUnits, 1))
                            continue;

                        int delta = BattleEquipmentAttackModifierResolver.ClampSignedModifier(
                            count * payload.BonusPerUnit,
                            payload.MaxAbsoluteBonus
                        );
                        if (delta == 0)
                            continue;
                        result.Add(
                            new BattleAttackRollModifierSpec
                            {
                                source_domain = "equipment_ability",
                                source_id = binding.BindingId,
                                source_instance_id =
                                    activeBinding.Source?.SourceEquipmentInstanceId.ToString()
                                    ?? "",
                                label = BattleEquipmentAbilityRuntimeService.ResolveActionLabel(binding, payload.Label),
                                modifier_delta = delta,
                                stack_key = action.ActionId != ""
                                    ? action.ActionId
                                    : binding.BindingId,
                                stack_mode = payload.StackMode == ""
                                    ? BattleEquipmentAbilityRuntimeService.StackModeMax
                                    : payload.StackMode,
                                target_team_filter = "any",
                                endpoint_mode = "target",
                                footprint_mode = "any_cell",
                                applies_to = "attack_roll",
                            }
                        );
                    }
                }
            }
        }
    }

    private static bool SummonedUnitModifierSelectorMatches(StringName selector)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(selector);
        return normalized == ""
            || normalized == "attacker"
            || normalized == "attack_source"
            || normalized == "source_attacker";
    }

    internal void ResolveSummonUnitsAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        SummonUnitsActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context,
        BattleEquipmentAbilityOnKillResult result
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        if (
            state == null
            || context?.SourceUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
        )
        {
            return;
        }

        BattleUnitState anchorUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload.AnchorSelector == "" ? new StringName("defeated") : payload.AnchorSelector,
            context.SourceUnit,
            context.DefeatedUnit
        ) ?? context.SourceUnit;

        int currentCount = CountLivingSummonedUnits(
            state,
            context.SourceUnit,
            activeBinding.Source,
            binding.BindingId,
            payload.StateKey
        );
        int capacity = Math.Max(payload.MaxLivingUnits, 0) - currentCount;
        if (capacity <= 0)
            return;

        int requested = Math.Max(BattleEquipmentAbilityRuntimeService.RollDiceExpression(payload.CountDice), 0);
        int toCreate = Math.Min(requested, capacity);
        if (toCreate <= 0)
            return;

        int created = 0;
        foreach (Vector2I coord in CollectSummonSpawnCoords(state, anchorUnit, payload.SpawnRadius))
        {
            if (created >= toCreate)
                break;
            BattleUnitState summoned = BuildSummonedUnit(
                state,
                context.SourceUnit,
                activeBinding.Source,
                binding,
                payload,
                coord
            );
            if (summoned == null)
                continue;
            state.SetUnit(summoned);
            if (!_runtime._grid_service.PlaceUnit(state, summoned, coord, true))
            {
                state.RemoveUnit(summoned.unit_id);
                continue;
            }
            AddSummonedUnitToFactionList(state, context.SourceUnit, summoned);
            context.Batch?.AddChangedUnitId(summoned.unit_id);
            foreach (Vector2I occupiedCoord in summoned.GetOccupiedCoordsTyped())
                context.Batch?.AddChangedCoord(occupiedCoord);
            created++;
        }

        if (created > 0)
        {
            result?.AddSummonResult(
                new BattleEquipmentAbilitySummonResult
                {
                    BindingId = binding.BindingId,
                    ActionId = action?.ActionId ?? "",
                    RequestedCount = requested,
                    CreatedCount = created,
                }
            );
        }
    }

    internal bool ResolveSummonUnitsAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        SummonUnitsActionPayloadDefinition payload,
        BattleEquipmentAbilityGrantedSkillUsedContext context
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        if (
            state == null
            || context?.SourceUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
        )
        {
            return false;
        }

        BattleUnitState anchorUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload.AnchorSelector == "" ? new StringName("skill_target") : payload.AnchorSelector,
            context.SourceUnit,
            context.TargetUnit
        ) ?? context.SourceUnit;

        int currentCount = CountLivingSummonedUnits(
            state,
            context.SourceUnit,
            activeBinding.Source,
            binding.BindingId,
            payload.StateKey
        );
        int capacity = Math.Max(payload.MaxLivingUnits, 0) - currentCount;
        if (capacity <= 0)
            return false;

        int requested = Math.Max(BattleEquipmentAbilityRuntimeService.RollDiceExpression(payload.CountDice), 0);
        int toCreate = Math.Min(requested, capacity);
        if (toCreate <= 0)
            return false;

        int created = 0;
        foreach (Vector2I coord in CollectSummonSpawnCoords(state, anchorUnit, payload.SpawnRadius))
        {
            if (created >= toCreate)
                break;
            BattleUnitState summoned = BuildSummonedUnit(
                state,
                context.SourceUnit,
                activeBinding.Source,
                binding,
                payload,
                coord
            );
            if (summoned == null)
                continue;
            state.SetUnit(summoned);
            if (!_runtime._grid_service.PlaceUnit(state, summoned, coord, true))
            {
                state.RemoveUnit(summoned.unit_id);
                continue;
            }
            AddSummonedUnitToFactionList(state, context.SourceUnit, summoned);
            context.Batch?.AddChangedUnitId(summoned.unit_id);
            foreach (Vector2I occupiedCoord in summoned.GetOccupiedCoordsTyped())
                context.Batch?.AddChangedCoord(occupiedCoord);
            created++;
        }

        if (created <= 0)
            return false;

        context.Batch?.AddLogLine(
            $"{context.SourceUnit.display_name} 触发 {BattleEquipmentAbilityRuntimeService.ResolveActionLabel(binding, action?.ActionId.ToString() ?? "")}。"
        );
        return true;
    }

    internal bool ResolveConsumeSummonedUnitsAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        ConsumeSummonedUnitsActionPayloadDefinition payload,
        BattleEquipmentAbilityGrantedSkillUsedContext context
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        if (
            state == null
            || context?.SourceUnit == null
            || binding == null
            || payload == null
            || payload.StateKey == ""
            || payload.Count <= 0
        )
        {
            return false;
        }

        StringName sourceBindingId = payload.SourceBindingId != ""
            ? payload.SourceBindingId
            : binding.BindingId;
        List<BattleUnitState> summons = CollectLivingSummonedUnits(
            state,
            context.SourceUnit,
            activeBinding.Source,
            sourceBindingId,
            payload.StateKey
        );
        SortSummonsForConsumption(summons, context.TargetUnit);
        int removed = 0;
        foreach (BattleUnitState summon in summons)
        {
            if (removed >= payload.Count)
                break;
            RemoveSummonedUnit(state, summon, context.Batch, "召唤单位被消耗。");
            removed++;
        }
        return removed > 0;
    }

    internal bool RemoveExpiredSummonedUnits(BattleState state, BattleEventBatch batch)
    {
        int currentTu = Math.Max(state?.timeline?.current_tu ?? -1, -1);
        if (state == null || currentTu < 0)
            return false;
        bool changed = false;
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            BattleAiBlackboard blackboard = unit?.ai_blackboard;
            if (
                unit == null
                || !unit.is_alive
                || blackboard?.summoned != true
                || blackboard.summon_expires_at_tu < 0
                || currentTu < blackboard.summon_expires_at_tu
            )
            {
                continue;
            }
            RemoveSummonedUnit(state, unit, batch, "召唤单位持续时间结束。");
            changed = true;
        }
        return changed;
    }

    private void RemoveSummonedUnit(
        BattleState state,
        BattleUnitState unit,
        BattleEventBatch batch,
        string logLine
    )
    {
        if (state == null || unit == null)
            return;
        if (_runtime?.GetState() == state)
        {
            _runtime.RemoveSummonedUnitFromBattle(unit, batch, logLine);
            return;
        }
        List<Vector2I> previousCoords = new(unit.GetOccupiedCoordsTyped());
        unit.MarkDead();
        _runtime?._grid_service.ClearUnitOccupancy(state, unit);
        batch?.AddChangedUnitId(unit.unit_id);
        foreach (Vector2I coord in previousCoords)
            batch?.AddChangedCoord(coord);
        if (!string.IsNullOrEmpty(logLine))
            batch?.AddLogLine(logLine);
    }

    private BattleUnitState BuildSummonedUnit(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        SummonUnitsActionPayloadDefinition payload,
        Vector2I coord
    )
    {
        if (state == null || sourceUnit == null || binding == null || payload == null)
            return null;
        int hpMax = Math.Max(payload.HpMax, 1);
        int actionPoints = Math.Max(payload.ActionPoints, 0);
        int movePoints = Math.Max(payload.MovePoints, 0);
        StringName unitId = BuildSummonedUnitId(
            state,
            payload.UnitIdPrefix == "" ? new StringName("summoned_unit") : payload.UnitIdPrefix,
            sourceUnit.unit_id,
            binding.BindingId,
            payload.StateKey
        );
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = string.IsNullOrWhiteSpace(payload.UnitDisplayName)
                ? unitId.ToString()
                : payload.UnitDisplayName,
            faction_id = sourceUnit.faction_id,
            control_mode = payload.ControlMode == "" ? new StringName("ai") : payload.ControlMode,
            ai_brain_id = payload.AiBrainId,
            ai_state_id = payload.AiStateId,
        };
        unit.SetAnchorCoord(coord);
        unit.SetBodySizeCategory(
            payload.BodySizeCategory == "" ? new StringName("tiny") : payload.BodySizeCategory
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hpMax);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, Math.Max(payload.ArmorClass, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, payload.AttackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, payload.BaseAttackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, actionPoints);
        unit.SetCombatResources(hpMax, 0, 0, 0, actionPoints, movePoints);
        foreach (StringName tag in payload.CreatureTypeTags ?? Array.Empty<StringName>())
        {
            if (tag != "" && !unit.creature_type_tags.Contains(tag))
                unit.creature_type_tags.Add(tag);
        }
        foreach (StringName tag in payload.MovementTags ?? Array.Empty<StringName>())
        {
            if (tag != "" && !unit.movement_tags.Contains(tag))
                unit.movement_tags.Add(tag);
        }
        foreach (StringName skillId in payload.KnownActiveSkillIds ?? Array.Empty<StringName>())
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId == "")
                continue;
            unit.AddKnownActiveSkill(normalizedSkillId);
            unit.SetKnownSkillLevelTyped(
                normalizedSkillId,
                normalizedSkillId == "basic_attack" ? 0 : 1,
                preserveZero: true
            );
        }
        WeaponDice naturalWeaponDice = BuildNaturalWeaponDice(payload.NaturalWeaponDamageDice);
        if (
            naturalWeaponDice != null
            && payload.NaturalWeaponProfileTypeId != ""
            && payload.NaturalWeaponDamageTag != ""
        )
        {
            unit.SetNaturalWeaponProjectionTyped(
                payload.NaturalWeaponProfileTypeId,
                payload.NaturalWeaponDamageTag,
                Math.Max(payload.NaturalWeaponAttackRange, 1),
                naturalWeaponDice,
                payload.NaturalWeaponFamily == ""
                    ? new StringName("natural")
                    : payload.NaturalWeaponFamily
            );
        }
        unit.ai_blackboard.summoned = true;
        unit.ai_blackboard.temporary_unit = true;
        unit.ai_blackboard.summon_source_unit_id = sourceUnit.unit_id;
        unit.ai_blackboard.summon_source_equipment_instance_id =
            source?.SourceEquipmentInstanceId ?? "";
        unit.ai_blackboard.summon_binding_id = binding.BindingId;
        unit.ai_blackboard.summon_state_key = payload.StateKey;
        unit.ai_blackboard.summon_expires_at_tu = payload.DurationTu > 0
            ? Math.Max(state.timeline?.current_tu ?? 0, 0) + payload.DurationTu
            : -1;
        return unit;
    }

    private static WeaponDice BuildNaturalWeaponDice(DiceExpressionDefinition dice)
    {
        if (dice?.Terms == null || dice.Terms.Count == 0)
            return null;
        DiceExpressionTermDefinition term = dice.Terms[0];
        if (term == null || term.DiceCount <= 0 || term.DiceSides <= 0)
            return null;
        return new WeaponDice
        {
            dice_count = term.DiceCount,
            dice_sides = term.DiceSides,
            flat_bonus = dice.FlatBonus,
        };
    }

    private IEnumerable<Vector2I> CollectSummonSpawnCoords(
        BattleState state,
        BattleUnitState anchorUnit,
        int radius
    )
    {
        if (state == null || anchorUnit == null)
            yield break;
        int resolvedRadius = Math.Max(radius, 0);
        Vector2I anchor = anchorUnit.coord;
        var coords = new List<Vector2I>();
        for (int y = anchor.Y - resolvedRadius; y <= anchor.Y + resolvedRadius; y++)
        {
            for (int x = anchor.X - resolvedRadius; x <= anchor.X + resolvedRadius; x++)
            {
                Vector2I coord = new(x, y);
                if (_runtime._grid_service.GetDistance(anchor, coord) <= resolvedRadius)
                    coords.Add(coord);
            }
        }
        coords.Sort(
            (left, right) =>
            {
                int leftDistance = _runtime._grid_service.GetDistance(anchor, left);
                int rightDistance = _runtime._grid_service.GetDistance(anchor, right);
                if (leftDistance != rightDistance)
                    return leftDistance.CompareTo(rightDistance);
                if (left.Y != right.Y)
                    return left.Y.CompareTo(right.Y);
                return left.X.CompareTo(right.X);
            }
        );
        foreach (Vector2I coord in coords)
            yield return coord;
    }

    private static void AddSummonedUnitToFactionList(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState summoned
    )
    {
        if (state == null || sourceUnit == null || summoned == null)
            return;
        if (state.ally_unit_ids.Contains(sourceUnit.unit_id))
        {
            if (!state.ally_unit_ids.Contains(summoned.unit_id))
                state.ally_unit_ids.Add(summoned.unit_id);
            return;
        }
        if (state.enemy_unit_ids.Contains(sourceUnit.unit_id))
        {
            if (!state.enemy_unit_ids.Contains(summoned.unit_id))
                state.enemy_unit_ids.Add(summoned.unit_id);
            return;
        }
        if (sourceUnit.faction_id == "player")
            state.ally_unit_ids.Add(summoned.unit_id);
        else
            state.enemy_unit_ids.Add(summoned.unit_id);
    }

    private static StringName BuildSummonedUnitId(
        BattleState state,
        StringName prefix,
        StringName sourceUnitId,
        StringName bindingId,
        StringName stateKey
    )
    {
        string baseId = string.Join(
            "_",
            SanitizeIdPart(prefix.ToString()),
            SanitizeIdPart(sourceUnitId.ToString()),
            SanitizeIdPart(bindingId.ToString()),
            SanitizeIdPart(stateKey.ToString())
        );
        for (int suffix = 1; suffix < 10000; suffix++)
        {
            StringName candidate = new($"{baseId}_{suffix}");
            if (state?.GetUnit(candidate) == null)
                return candidate;
        }
        return new StringName($"{baseId}_{Guid.NewGuid():N}");
    }

    private static string SanitizeIdPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "summon";
        char[] chars = value.ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            char c = chars[index];
            if (!char.IsLetterOrDigit(c) && c != '_')
                chars[index] = '_';
        }
        return new string(chars);
    }

    internal int CountLivingSummonedUnits(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        StringName bindingId,
        StringName stateKey,
        BattleUnitState radiusSubject = null,
        int radius = -1
    ) => CollectLivingSummonedUnits(
        state,
        sourceUnit,
        source,
        bindingId,
        stateKey,
        radiusSubject,
        radius
    ).Count;

    private List<BattleUnitState> CollectLivingSummonedUnits(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        StringName bindingId,
        StringName stateKey,
        BattleUnitState radiusSubject = null,
        int radius = -1
    )
    {
        var result = new List<BattleUnitState>();
        if (state == null || sourceUnit == null)
            return result;
        StringName sourceEquipmentInstanceId = source?.SourceEquipmentInstanceId ?? "";
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            if (
                !SummonedUnitMatches(
                    unit,
                    sourceUnit.unit_id,
                    sourceEquipmentInstanceId,
                    bindingId,
                    stateKey
                )
            )
            {
                continue;
            }
            if (
                radiusSubject != null
                && radius >= 0
                && DistanceBetweenUnits(unit, radiusSubject) > radius
            )
            {
                continue;
            }
            result.Add(unit);
        }
        return result;
    }

    private bool SummonedUnitMatches(
        BattleUnitState unit,
        StringName sourceUnitId,
        StringName sourceEquipmentInstanceId,
        StringName bindingId,
        StringName stateKey
    )
    {
        BattleAiBlackboard blackboard = unit?.ai_blackboard;
        if (unit == null || !unit.is_alive || blackboard?.summoned != true)
            return false;
        if (sourceUnitId != "" && blackboard.summon_source_unit_id != sourceUnitId)
            return false;
        if (
            sourceEquipmentInstanceId != ""
            && blackboard.summon_source_equipment_instance_id != sourceEquipmentInstanceId
        )
        {
            return false;
        }
        if (bindingId != "" && blackboard.summon_binding_id != bindingId)
            return false;
        if (stateKey != "" && blackboard.summon_state_key != stateKey)
            return false;
        return true;
    }

    private int DistanceBetweenUnits(BattleUnitState first, BattleUnitState second)
    {
        if (first == null || second == null)
            return 999999;
        int best = 999999;
        foreach (Vector2I firstCoord in first.GetOccupiedCoordsTyped())
        {
            foreach (Vector2I secondCoord in second.GetOccupiedCoordsTyped())
                best = Math.Min(best, _runtime._grid_service.GetDistance(firstCoord, secondCoord));
        }
        return best;
    }

    private void SortSummonsForConsumption(
        List<BattleUnitState> summons,
        BattleUnitState targetUnit
    )
    {
        if (summons == null)
            return;
        summons.Sort(
            (left, right) =>
            {
                int leftDistance = targetUnit != null ? DistanceBetweenUnits(left, targetUnit) : 0;
                int rightDistance = targetUnit != null ? DistanceBetweenUnits(right, targetUnit) : 0;
                if (leftDistance != rightDistance)
                    return leftDistance.CompareTo(rightDistance);
                return string.CompareOrdinal(left?.unit_id.ToString(), right?.unit_id.ToString());
            }
        );
    }
}
