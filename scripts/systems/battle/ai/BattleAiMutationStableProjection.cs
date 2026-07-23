using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Godot;

internal static class BattleAiMutationStableProjection
{
    internal static StableMap StableBattleUnitState(BattleUnitState unit)
    {
        if (unit == null)
        {
            return new StableMap();
        }
        return BattleUnitSnapshot.Capture(unit).ToStableMap();
    }

    internal static StableMap StableAttributeSnapshot(AttributeSnapshot attributeSnapshot)
    {
        StableMap result = new();
        if (attributeSnapshot == null)
        {
            return result;
        }
        foreach (KeyValuePair<StringName, int> entry in attributeSnapshot.GetAllValuesTyped())
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                StableValue.FromInteger(entry.Value)
            );
        }
        return result;
    }

    internal static StableMap StableBattleCell(BattleCellState cell)
    {
        StableMap result = new();
        if (cell == null)
        {
            return result;
        }
        result.Set("coord", StableValue.FromVector2I(cell.coord));
        result.Set("stack_layer", StableValue.FromInteger(cell.stack_layer));
        result.Set("base_terrain", StableNullableStringName(cell.base_terrain));
        result.Set("base_height", StableValue.FromInteger(cell.base_height));
        result.Set("height_offset", StableValue.FromInteger(cell.height_offset));
        result.Set("current_height", StableValue.FromInteger(cell.current_height));
        result.Set("passable", StableValue.FromBool(cell.passable));
        result.Set("move_cost", StableValue.FromInteger(cell.move_cost));
        result.Set("occupant_unit_id", StableNullableStringName(cell.occupant_unit_id));
        result.Set(
            "prop_ids",
            cell.prop_ids == null
                ? StableValue.Nil()
                : StableValue.FromArray(StableStringNameArray(cell.prop_ids))
        );
        result.Set(
            "terrain_effect_ids",
            cell.terrain_effect_ids == null
                ? StableValue.Nil()
                : StableValue.FromArray(StableStringNameArray(cell.terrain_effect_ids))
        );
        result.Set(
            "timed_terrain_effects",
            cell.timed_terrain_effects == null
                ? StableValue.Nil()
                : StableValue.FromArray(StableTerrainEffectArray(cell.timed_terrain_effects))
        );
        result.Set("flow_direction", StableValue.FromVector2I(cell.flow_direction));
        result.Set("edge_feature_east", StableValue.FromMap(StableEdgeFeature(cell.edge_feature_east)));
        result.Set("edge_feature_south", StableValue.FromMap(StableEdgeFeature(cell.edge_feature_south)));
        return result;
    }

    internal static StableMap StableTimeline(BattleTimelineState timeline)
    {
        StableMap result = new();
        if (timeline == null)
        {
            return result;
        }
        result.Set("current_tu", StableValue.FromInteger(timeline.current_tu));
        result.Set("tu_per_tick", StableValue.FromInteger(timeline.tu_per_tick));
        result.Set("frozen", StableValue.FromBool(timeline.frozen));
        result.Set(
            "ready_unit_ids",
            timeline.ready_unit_ids == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    StableStringNameArray(timeline.ready_unit_ids)
                )
        );
        return result;
    }

    internal static StableMap StableStatusEffect(BattleStatusEffectState statusEffect)
    {
        StableMap result = new();
        if (statusEffect == null)
        {
            return result;
        }
        PropertyInfo[] properties = typeof(BattleStatusEffectState).GetProperties(
            BindingFlags.Instance | BindingFlags.Public
        );
        System.Array.Sort(
            properties,
            (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name)
        );
        foreach (PropertyInfo property in properties)
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;
            result.Set(
                property.Name,
                BattleAiMutationSnapshotModel.ReadStableTypedValue(
                    property.GetValue(statusEffect)
                )
            );
        }
        result.Set(
            "params",
            StableValue.FromMap(
                StableMap.FromTypedDictionary(statusEffect.ParamsSnapshotPlain)
            )
        );
        return result;
    }

    internal static StableMap StableTerrainEffect(BattleTerrainEffectState terrainEffect)
    {
        StableMap result = new();
        if (terrainEffect == null)
        {
            return result;
        }
        result.Set("field_instance_id", StableNullableStringName(terrainEffect.field_instance_id));
        result.Set("effect_id", StableNullableStringName(terrainEffect.effect_id));
        result.Set("effect_type", StableNullableStringName(terrainEffect.effect_type));
        result.Set("lifetime_policy", StableNullableStringName(terrainEffect.lifetime_policy));
        result.Set("move_cost_delta", StableValue.FromInteger(terrainEffect.move_cost_delta));
        result.Set("applied_status_id", StableNullableStringName(terrainEffect.applied_status_id));
        result.Set(
            "applied_status_duration_tu",
            StableValue.FromInteger(terrainEffect.applied_status_duration_tu)
        );
        result.Set("render_overlay_id", StableNullableStringName(terrainEffect.render_overlay_id));
        result.Set("overlay_priority", StableValue.FromInteger(terrainEffect.overlay_priority));
        result.Set("display_name", StableNullableText(terrainEffect.display_name));
        result.Set(
            "accuracy_modifier_spec",
            terrainEffect.accuracy_modifier_spec == null
                ? StableValue.Nil()
                : StableValue.FromMap(
                    StableAttackRollModifierSpec(terrainEffect.accuracy_modifier_spec)
                )
        );
        result.Set(
            "does_not_stack_with_status_id",
            StableNullableStringName(terrainEffect.does_not_stack_with_status_id)
        );
        result.Set(
            "does_not_stack_with_status_ids",
            terrainEffect.does_not_stack_with_status_ids == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    StableStringNameList(terrainEffect.does_not_stack_with_status_ids)
                )
        );
        result.Set("contact_status_id", StableNullableStringName(terrainEffect.contact_status_id));
        result.Set(
            "contact_status_duration_tu",
            StableValue.FromInteger(terrainEffect.contact_status_duration_tu)
        );
        result.Set(
            "contact_stack_behavior",
            StableNullableStringName(terrainEffect.contact_stack_behavior)
        );
        result.Set("contact_stack_limit", StableValue.FromInteger(terrainEffect.contact_stack_limit));
        result.Set(
            "contact_status_display_label",
            StableNullableText(terrainEffect.contact_status_display_label)
        );
        result.Set(
            "contact_counts_as_debuff_override",
            StableValue.FromBool(terrainEffect.contact_counts_as_debuff_override)
        );
        result.Set(
            "contact_counts_as_debuff",
            StableValue.FromBool(terrainEffect.contact_counts_as_debuff)
        );
        result.Set("contact_undispellable", StableValue.FromBool(terrainEffect.contact_undispellable));
        result.Set(
            "contact_dispellable_magic",
            StableValue.FromBool(terrainEffect.contact_dispellable_magic)
        );
        result.Set(
            "contact_dispellable_harmful_magic",
            StableValue.FromBool(terrainEffect.contact_dispellable_harmful_magic)
        );
        result.Set(
            "contact_dispellable_beneficial_magic",
            StableValue.FromBool(terrainEffect.contact_dispellable_beneficial_magic)
        );
        result.Set("contact_save_dc", StableValue.FromInteger(terrainEffect.contact_save_dc));
        result.Set(
            "contact_save_ability",
            StableNullableStringName(terrainEffect.contact_save_ability)
        );
        result.Set("contact_save_tag", StableNullableStringName(terrainEffect.contact_save_tag));
        result.Set(
            "contact_apply_on_save_failure",
            StableValue.FromBool(terrainEffect.contact_apply_on_save_failure)
        );
        result.Set(
            "contact_tick_interval_tu",
            StableValue.FromInteger(terrainEffect.contact_tick_interval_tu)
        );
        result.Set(
            "contact_timeline_damage_dice_count",
            StableValue.FromInteger(terrainEffect.contact_timeline_damage_dice_count)
        );
        result.Set(
            "contact_timeline_damage_dice_sides",
            StableValue.FromInteger(terrainEffect.contact_timeline_damage_dice_sides)
        );
        result.Set(
            "contact_timeline_damage_flat_bonus",
            StableValue.FromInteger(terrainEffect.contact_timeline_damage_flat_bonus)
        );
        result.Set(
            "contact_blocked_by_trait_id",
            StableNullableStringName(terrainEffect.contact_blocked_by_trait_id)
        );
        result.Set("source_unit_id", StableNullableStringName(terrainEffect.source_unit_id));
        result.Set("source_skill_id", StableNullableStringName(terrainEffect.source_skill_id));
        result.Set(
            "target_team_filter",
            StableNullableStringName(terrainEffect.target_team_filter)
        );
        result.Set("power", StableValue.FromInteger(terrainEffect.power));
        result.Set("damage_tag", StableNullableStringName(terrainEffect.damage_tag));
        result.Set("remaining_tu", StableValue.FromInteger(terrainEffect.remaining_tu));
        result.Set("tick_interval_tu", StableValue.FromInteger(terrainEffect.tick_interval_tu));
        result.Set("next_tick_at_tu", StableValue.FromInteger(terrainEffect.next_tick_at_tu));
        result.Set("stack_behavior", StableNullableStringName(terrainEffect.stack_behavior));
        result.Set(
            "params",
            StableValue.FromMap(
                StableMap.FromTypedDictionary(terrainEffect.ParamsSnapshotPlain)
            )
        );
        return result;
    }

    internal static StableMap StableAttackRollModifierSpec(
        BattleAttackRollModifierSpec modifier
    )
    {
        StableMap result = new();
        if (modifier == null)
        {
            return result;
        }
        result.Set("source_domain", StableNullableStringName(modifier.source_domain));
        result.Set("source_id", StableNullableStringName(modifier.source_id));
        result.Set("source_instance_id", StableNullableText(modifier.source_instance_id));
        result.Set("label", StableNullableText(modifier.label));
        result.Set("modifier_delta", StableValue.FromInteger(modifier.modifier_delta));
        result.Set("stack_key", StableNullableStringName(modifier.stack_key));
        result.Set("stack_mode", StableNullableStringName(modifier.stack_mode));
        result.Set("roll_kind_filter", StableNullableStringName(modifier.roll_kind_filter));
        result.Set("endpoint_mode", StableNullableStringName(modifier.endpoint_mode));
        result.Set(
            "distance_min_exclusive",
            StableValue.FromInteger(modifier.distance_min_exclusive)
        );
        result.Set(
            "distance_max_inclusive",
            StableValue.FromInteger(modifier.distance_max_inclusive)
        );
        result.Set(
            "target_team_filter",
            StableNullableStringName(modifier.target_team_filter)
        );
        result.Set("footprint_mode", StableNullableStringName(modifier.footprint_mode));
        result.Set("applies_to", StableNullableStringName(modifier.applies_to));
        return result;
    }

    internal static StableMap StableEdgeFeature(BattleEdgeFeatureState feature)
    {
        StableMap result = new();
        if (feature == null)
        {
            return result;
        }
        result.Set("feature_kind", StableNullableStringName(feature.feature_kind));
        result.Set("render_kind", StableNullableStringName(feature.render_kind));
        result.Set("render_layers", StableValue.FromInteger(feature.render_layers));
        result.Set("blocks_move", StableValue.FromBool(feature.blocks_move));
        result.Set("blocks_occupancy", StableValue.FromBool(feature.blocks_occupancy));
        result.Set("blocks_los", StableValue.FromBool(feature.blocks_los));
        result.Set("interaction_kind", StableNullableStringName(feature.interaction_kind));
        result.Set("state_tag", StableNullableStringName(feature.state_tag));
        return result;
    }

    internal static List<StableValue> StableTemporaryEdgeFeatureList(
        IEnumerable<BattleTemporaryEdgeFeatureState> values
    )
    {
        List<StableValue> result = new();
        foreach (
            BattleTemporaryEdgeFeatureState feature in values
                ?? System.Array.Empty<BattleTemporaryEdgeFeatureState>()
        )
        {
            if (feature == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap map = new();
            map.Set("origin_coord", StableValue.FromVector2I(feature.OriginCoord));
            map.Set("direction", StableValue.FromVector2I(feature.Direction));
            map.Set("source_unit_id", StableNullableStringName(feature.SourceUnitId));
            map.Set(
                "source_equipment_instance_id",
                StableNullableStringName(feature.SourceEquipmentInstanceId)
            );
            map.Set("binding_id", StableNullableStringName(feature.BindingId));
            map.Set("action_id", StableNullableStringName(feature.ActionId));
            map.Set("created_at_tu", StableValue.FromInteger(feature.CreatedAtTu));
            map.Set("expires_at_tu", StableValue.FromInteger(feature.ExpiresAtTu));
            map.Set("sequence", StableValue.FromInteger(feature.Sequence));
            map.Set("feature", StableValue.FromMap(StableEdgeFeature(feature.Feature)));
            result.Add(StableValue.FromMap(map));
        }
        return result;
    }

    internal static StableMap StableWarehouse(WarehouseState warehouse)
    {
        StableMap result = new();
        if (warehouse == null)
        {
            return result;
        }
        List<StableValue> stacks = new();
        foreach (WarehouseStackState stack in warehouse.stacks ?? new List<WarehouseStackState>())
        {
            stacks.Add(
                stack == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(StableWarehouseStack(stack))
            );
        }
        List<StableValue> instances = new();
        foreach (
            EquipmentInstanceState instance in warehouse.equipment_instances
                ?? new List<EquipmentInstanceState>()
        )
        {
            instances.Add(
                instance == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(StableEquipmentInstance(instance))
            );
        }
        result.Set(
            "stacks",
            warehouse.stacks == null
                ? StableValue.Nil()
                : StableValue.FromArray(stacks)
        );
        result.Set(
            "equipment_instances",
            warehouse.equipment_instances == null
                ? StableValue.Nil()
                : StableValue.FromArray(instances)
        );
        return result;
    }

    internal static StableMap StableWarehouseStack(WarehouseStackState stack)
    {
        StableMap result = new();
        if (stack == null)
        {
            return result;
        }
        result.Set("item_id", StableNullableStringName(stack.item_id));
        result.Set("quantity", StableValue.FromInteger(stack.quantity));
        return result;
    }

    internal static StableMap StableEquipment(EquipmentState equipment)
    {
        StableMap result = new();
        if (equipment == null)
        {
            return result;
        }
        StableMap slots = new();
        foreach (
            KeyValuePair<StringName, EquipmentEntryState> entry in
            equipment.CaptureEquippedSlotsForMutationSnapshotExact()
        )
        {
            slots.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                entry.Value == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(StableEquipmentEntry(entry.Value))
            );
        }
        result.Set("equipped_slots", StableValue.FromMap(slots));
        StableMap slotToEntrySlot = new();
        foreach (
            KeyValuePair<StringName, StringName> entry in
            equipment.CaptureSlotMappingForMutationSnapshotExact()
        )
        {
            slotToEntrySlot.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                StableNullableStringName(entry.Value)
            );
        }
        result.Set("slot_to_entry_slot", StableValue.FromMap(slotToEntrySlot));
        return result;
    }

    internal static StableMap StableEquipmentEntry(EquipmentEntryState entry)
    {
        StableMap result = new();
        if (entry == null)
        {
            return result;
        }
        result.Set("item_id", StableNullableStringName(entry.item_id));
        result.Set("instance_id", StableNullableStringName(entry.instance_id));
        result.Set(
            "occupied_slot_ids",
            entry.occupied_slot_ids == null
                ? StableValue.Nil()
                : StableValue.FromArray(StableStringNameArray(entry.occupied_slot_ids))
        );
        result.Set(
            "equipment_instance",
            entry.equipment_instance == null
                ? StableValue.Nil()
                : StableValue.FromMap(StableEquipmentInstance(entry.equipment_instance))
        );
        return result;
    }

    internal static StableMap StableEquipmentInstance(EquipmentInstanceState instance)
    {
        StableMap result = new();
        if (instance == null)
        {
            return result;
        }
        result.Set("instance_id", StableNullableStringName(instance.instance_id));
        result.Set("item_id", StableNullableStringName(instance.item_id));
        result.Set("rarity", StableValue.FromInteger(instance.rarity));
        result.Set("current_durability", StableValue.FromInteger(instance.current_durability));
        result.Set(
            "trait_instances",
            instance.trait_instances == null
                ? StableValue.Nil()
                : StableValue.FromArray(StableTraitInstancesExact(instance.trait_instances))
        );
        result.Set(
            "ability_usage_periods",
            instance.ability_usage_periods == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    StableEquipmentAbilityUsagePeriods(instance.ability_usage_periods)
                )
        );
        result.Set(
            "ability_persistent_counters",
            instance.ability_persistent_counters == null
                ? StableValue.Nil()
                : StableValue.FromArray(
                    StableEquipmentAbilityPersistentCounters(
                        instance.ability_persistent_counters
                    )
                )
        );
        return result;
    }

    internal static List<StableValue> StableTraitInstancesExact(
        IEnumerable<TraitInstanceState> values
    )
    {
        List<StableValue> result = new();
        foreach (TraitInstanceState trait in values ?? Array.Empty<TraitInstanceState>())
        {
            if (trait == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set(
                "trait_instance_id",
                StableNullableStringName(trait.trait_instance_id)
            );
            entry.Set("trait_id", StableNullableStringName(trait.trait_id));
            entry.Set("source_type", StableNullableStringName(trait.source_type));
            entry.Set("source_id", StableNullableStringName(trait.source_id));
            entry.Set("rank", StableValue.FromInteger(trait.rank));
            entry.Set("stacks", StableValue.FromInteger(trait.stacks));
            entry.Set(
                "roll_values",
                trait.roll_values == null
                    ? StableValue.Nil()
                    : StableValue.FromArray(StableRollValuesExact(trait.roll_values))
            );
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableEquipmentAbilityUsagePeriods(
        IEnumerable<EquipmentAbilityUsagePeriodState> values
    )
    {
        List<StableValue> result = new();
        foreach (
            EquipmentAbilityUsagePeriodState usage in values
                ?? System.Array.Empty<EquipmentAbilityUsagePeriodState>()
        )
        {
            if (usage == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set("ability_id", StableNullableText(usage.AbilityId));
            entry.Set("period_kind", StableNullableText(usage.PeriodKind));
            entry.Set("period_index", StableValue.FromInteger(usage.PeriodIndex));
            entry.Set("used_count", StableValue.FromInteger(usage.UsedCount));
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableEquipmentAbilityPersistentCounters(
        IEnumerable<EquipmentAbilityPersistentCounterState> values
    )
    {
        List<StableValue> result = new();
        foreach (
            EquipmentAbilityPersistentCounterState counter in values
                ?? System.Array.Empty<EquipmentAbilityPersistentCounterState>()
        )
        {
            if (counter == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set("counter_id", StableNullableText(counter.CounterId));
            entry.Set("value", StableValue.FromInteger(counter.Value));
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableEquipmentAbilitySources(
        IEnumerable<BattleEquipmentAbilitySourceState> values
    )
    {
        List<StableValue> result = new();
        foreach (
            BattleEquipmentAbilitySourceState source in values
                ?? Array.Empty<BattleEquipmentAbilitySourceState>()
        )
        {
            if (source == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set(
                "effective_instance_key",
                StableNullableStringName(source.EffectiveInstanceKey)
            );
            entry.Set(
                "equipment_def_id",
                StableNullableStringName(source.EquipmentDefId)
            );
            entry.Set(
                "source_equipment_instance_id",
                StableNullableStringName(source.SourceEquipmentInstanceId)
            );
            entry.Set(
                "source_kind",
                StableValue.FromInteger((int)source.SourceKind)
            );
            entry.Set(
                "ability_ids",
                source.AbilityIds == null
                    ? StableValue.Nil()
                    : StableValue.FromArray(StableStringNameList(source.AbilityIds))
            );
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableTemporalProgressModifiers(
        IEnumerable<BattleTemporalProgressModifierState> values
    )
    {
        List<StableValue> result = new();
        foreach (
            BattleTemporalProgressModifierState modifier in values
                ?? Array.Empty<BattleTemporalProgressModifierState>()
        )
        {
            if (modifier == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set(
                "modifier_id",
                StableNullableStringName(modifier.ModifierId)
            );
            entry.Set("binding_id", StableNullableStringName(modifier.BindingId));
            entry.Set(
                "source_equipment_instance_id",
                StableNullableStringName(modifier.SourceEquipmentInstanceId)
            );
            entry.Set(
                "applies_to_action_progress",
                StableValue.FromBool(modifier.AppliesToActionProgress)
            );
            entry.Set(
                "applies_to_cast_progress",
                StableValue.FromBool(modifier.AppliesToCastProgress)
            );
            entry.Set("save_dc", StableValue.FromInteger(modifier.SaveDc));
            entry.Set(
                "attribute_modifier_id",
                StableNullableStringName(modifier.AttributeModifierId)
            );
            entry.Set(
                "success_rate_percent",
                StableValue.FromInteger(modifier.SuccessRatePercent)
            );
            entry.Set(
                "failure_rate_percent",
                StableValue.FromInteger(modifier.FailureRatePercent)
            );
            entry.Set(
                "label",
                modifier.Label == null
                    ? StableValue.Nil()
                    : StableValue.FromText(modifier.Label)
            );
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableEquipmentTargetMarks(
        IEnumerable<BattleEquipmentTargetMarkState> values
    )
    {
        List<StableValue> result = new();
        foreach (
            BattleEquipmentTargetMarkState mark in values
                ?? Array.Empty<BattleEquipmentTargetMarkState>()
        )
        {
            if (mark == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set(
                "source_unit_id",
                StableNullableStringName(mark.SourceUnitId)
            );
            entry.Set(
                "target_unit_id",
                StableNullableStringName(mark.TargetUnitId)
            );
            entry.Set(
                "source_equipment_instance_id",
                StableNullableStringName(mark.SourceEquipmentInstanceId)
            );
            entry.Set("binding_id", StableNullableStringName(mark.BindingId));
            entry.Set("state_key", StableNullableStringName(mark.StateKey));
            entry.Set("stacks", StableValue.FromInteger(mark.Stacks));
            entry.Set(
                "remaining_duration_tu",
                StableValue.FromInteger(mark.RemainingDurationTu)
            );
            entry.Set(
                "remove_on_source_missing",
                StableValue.FromBool(mark.RemoveOnSourceMissing)
            );
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static StableMap StableWeaponProjection(WeaponProjection projection)
    {
        StableMap result = new();
        if (projection == null)
        {
            return result;
        }
        result.Set("weapon_profile_kind", StableNullableStringName(projection.weapon_profile_kind));
        result.Set("weapon_item_id", StableNullableStringName(projection.weapon_item_id));
        result.Set("weapon_profile_type_id", StableNullableStringName(projection.weapon_profile_type_id));
        result.Set("weapon_family", StableNullableStringName(projection.weapon_family));
        result.Set("weapon_current_grip", StableNullableStringName(projection.weapon_current_grip));
        result.Set("weapon_attack_range", StableValue.FromInteger(projection.weapon_attack_range));
        result.Set(
            "weapon_one_handed_dice",
            StableValue.FromMap(StableWeaponDice(projection.weapon_one_handed_dice))
        );
        result.Set(
            "weapon_two_handed_dice",
            StableValue.FromMap(StableWeaponDice(projection.weapon_two_handed_dice))
        );
        result.Set("weapon_is_versatile", StableValue.FromBool(projection.weapon_is_versatile));
        result.Set("weapon_uses_two_hands", StableValue.FromBool(projection.weapon_uses_two_hands));
        result.Set(
            "weapon_physical_damage_tag",
            StableNullableStringName(projection.weapon_physical_damage_tag)
        );
        return result;
    }

    internal static StableMap StableWeaponDice(WeaponDice dice)
    {
        StableMap result = new();
        if (dice == null)
        {
            return result;
        }
        result.Set("dice_count", StableValue.FromInteger(dice.dice_count));
        result.Set("dice_sides", StableValue.FromInteger(dice.dice_sides));
        result.Set("flat_bonus", StableValue.FromInteger(dice.flat_bonus));
        return result;
    }

    internal static List<StableValue> StableTerrainEffectArray(
        IEnumerable<BattleTerrainEffectState> values
    )
    {
        List<StableValue> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (BattleTerrainEffectState value in values)
        {
            result.Add(
                value == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(StableTerrainEffect(value))
            );
        }
        return result;
    }

    internal static List<StableValue> StableStringNameArray(IEnumerable<StringName> values)
    {
        List<StableValue> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(StableNullableStringName(value));
        }
        return result;
    }

    internal static StableMap StablePendingCast(BattlePendingCastState pendingCast)
    {
        StableMap result = new();
        if (pendingCast == null)
        {
            return result;
        }
        result.Set("source_unit_id", StableNullableStringName(pendingCast.SourceUnitId));
        result.Set("skill_id", StableNullableStringName(pendingCast.SkillId));
        result.Set("variant_id", StableNullableStringName(pendingCast.VariantId));
        result.Set("target_mode", StableValue.FromInteger((int)pendingCast.TargetMode));
        result.Set(
            "binding_mode",
            StableValue.FromInteger((int)pendingCast.BindingMode)
        );
        result.Set("started_coord", StableValue.FromVector2I(pendingCast.StartedCoord));
        result.Set("started_tu", StableValue.FromInteger(pendingCast.StartedTu));
        result.Set("base_casting_time_tu", StableValue.FromInteger(pendingCast.BaseCastingTimeTu));
        result.Set("remaining_cast_progress", StableValue.FromInteger(pendingCast.RemainingCastProgress));
        result.Set(
            "last_maintenance_checkpoint_hp",
            StableValue.FromInteger(pendingCast.LastMaintenanceCheckpointHp)
        );
        result.Set("cast_sequence", StableValue.FromInteger((long)pendingCast.CastSequence));
        result.Set(
            "target_unit_ids",
            StableValue.FromArray(StableStringNameList(pendingCast.TargetUnitIds))
        );
        result.Set("target_coords", StableValue.FromArray(StableVector2IList(pendingCast.TargetCoords)));
        result.Set(
            "cost_transaction",
            pendingCast.CostTransaction == null
                ? StableValue.Nil()
                : StableValue.FromMap(
                    StableSkillCostTransaction(pendingCast.CostTransaction)
                )
        );
        result.Set(
            "spell_control_metadata",
            pendingCast.SpellControlMetadata == null
                ? StableValue.Nil()
                : StableValue.FromMap(
                    StableSpellControlMetadata(pendingCast.SpellControlMetadata)
                )
        );
        return result;
    }

    internal static StableMap StableSpellControlMetadata(BattleSpellControlMetadata metadata)
    {
        StableMap result = new();
        if (metadata == null)
        {
            return result;
        }
        result.Set("attack_resolution", StableNullableStringName(metadata.AttackResolution));
        result.Set(
            "spell_control_resolution",
            StableNullableStringName(metadata.SpellControlResolution)
        );
        result.Set("attack_success", StableValue.FromBool(metadata.AttackSuccess));
        result.Set("critical_hit", StableValue.FromBool(metadata.CriticalHit));
        result.Set("critical_fail", StableValue.FromBool(metadata.CriticalFail));
        result.Set("ordinary_miss", StableValue.FromBool(metadata.OrdinaryMiss));
        result.Set("is_disadvantage", StableValue.FromBool(metadata.IsDisadvantage));
        result.Set("hidden_luck_at_birth", StableValue.FromInteger(metadata.HiddenLuckAtBirth));
        result.Set("faith_luck_bonus", StableValue.FromInteger(metadata.FaithLuckBonus));
        result.Set("effective_luck", StableValue.FromInteger(metadata.EffectiveLuck));
        result.Set("crit_locked", StableValue.FromBool(metadata.CritLocked));
        result.Set("crit_gate_die", StableValue.FromInteger(metadata.CritGateDie));
        result.Set("crit_gate_roll", StableValue.FromInteger(metadata.CritGateRoll));
        result.Set("hit_roll", StableValue.FromInteger(metadata.HitRoll));
        result.Set("fumble_low_end", StableValue.FromInteger(metadata.FumbleLowEnd));
        result.Set("crit_threshold", StableValue.FromInteger(metadata.CritThreshold));
        result.Set("locked_skill_hit_bonus", StableValue.FromInteger(metadata.LockedSkillHitBonus));
        result.Set("effective_hit_roll", StableValue.FromInteger(metadata.EffectiveHitRoll));
        result.Set("reverse_fate_downgraded", StableValue.FromBool(metadata.ReverseFateDowngraded));
        return result;
    }

    internal static StableMap StableSkillCostTransaction(SkillCostTransaction transaction)
    {
        StableMap result = new();
        if (transaction == null)
        {
            return result;
        }
        result.Set("skill_id", StableNullableStringName(transaction.SkillId));
        result.Set("skill_level", StableValue.FromInteger(transaction.SkillLevel));
        result.Set("ap_cost", StableValue.FromInteger(transaction.ApCost));
        result.Set("mp_cost", StableValue.FromInteger(transaction.MpCost));
        result.Set("stamina_cost", StableValue.FromInteger(transaction.StaminaCost));
        result.Set("aura_cost", StableValue.FromInteger(transaction.AuraCost));
        result.Set("cooldown_turns", StableValue.FromInteger(transaction.CooldownTurns));
        result.Set("precast_damage", StableValue.FromInteger(transaction.PrecastDamage));
        return result;
    }

    internal static StableValue StableObjectiveRuntimeState(
        BattleObjectiveRuntimeState objectiveRuntimeState
    )
    {
        if (objectiveRuntimeState == null)
            return StableValue.Nil();

        StableMap result = new();
        switch (objectiveRuntimeState)
        {
            case BattleEliminationObjectiveRuntimeState:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleEliminationObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)objectiveRuntimeState.Mode)
                );
                return StableValue.FromMap(result);
            case BattleBossObjectiveRuntimeState bossObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleBossObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)bossObjective.Mode)
                );
                result.Set(
                    "target_actor_id",
                    StableNullableStringName(bossObjective.TargetActorId)
                );
                result.Set(
                    "target_unit_id",
                    StableNullableStringName(bossObjective.TargetUnitId)
                );
                result.Set(
                    "required_party_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(bossObjective.RequiredPartyUnitIds)
                    )
                );
                return StableValue.FromMap(result);
            case BattleEscapeObjectiveRuntimeState escapeObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleEscapeObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)escapeObjective.Mode)
                );
                result.Set(
                    "exit_zone_id",
                    StableNullableStringName(escapeObjective.ExitZoneId)
                );
                result.Set(
                    "exit_edge",
                    StableValue.FromInteger((int)escapeObjective.ExitEdge)
                );
                result.Set(
                    "exit_depth",
                    StableValue.FromInteger(escapeObjective.ExitDepth)
                );
                result.Set(
                    "required_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(escapeObjective.RequiredUnitIds)
                    )
                );
                result.Set(
                    "exit_coords",
                    StableValue.FromArray(StableVector2IList(escapeObjective.ExitCoords))
                );
                return StableValue.FromMap(result);
            case BattleRescueObjectiveRuntimeState rescueObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleRescueObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)rescueObjective.Mode)
                );
                result.Set(
                    "target_actor_id",
                    StableNullableStringName(rescueObjective.TargetActorId)
                );
                result.Set(
                    "target_unit_id",
                    StableNullableStringName(rescueObjective.TargetUnitId)
                );
                result.Set(
                    "required_party_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(
                            rescueObjective.RequiredPartyUnitIds
                        )
                    )
                );
                result.Set(
                    "target_secured",
                    StableValue.FromBool(rescueObjective.TargetSecured)
                );
                return StableValue.FromMap(result);
            case BattleEscortObjectiveRuntimeState escortObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleEscortObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)escortObjective.Mode)
                );
                result.Set(
                    "target_actor_id",
                    StableNullableStringName(escortObjective.TargetActorId)
                );
                result.Set(
                    "target_unit_id",
                    StableNullableStringName(escortObjective.TargetUnitId)
                );
                result.Set(
                    "exit_zone_id",
                    StableNullableStringName(escortObjective.ExitZoneId)
                );
                result.Set(
                    "exit_edge",
                    StableValue.FromInteger((int)escortObjective.ExitEdge)
                );
                result.Set(
                    "exit_depth",
                    StableValue.FromInteger(escortObjective.ExitDepth)
                );
                result.Set(
                    "required_party_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(
                            escortObjective.RequiredPartyUnitIds
                        )
                    )
                );
                result.Set(
                    "exit_coords",
                    StableValue.FromArray(
                        StableVector2IList(escortObjective.ExitCoords)
                    )
                );
                return StableValue.FromMap(result);
            case BattleDefenseObjectiveRuntimeState defenseObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleDefenseObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)defenseObjective.Mode)
                );
                result.Set(
                    "target_actor_id",
                    StableNullableStringName(defenseObjective.TargetActorId)
                );
                result.Set(
                    "target_unit_id",
                    StableNullableStringName(defenseObjective.TargetUnitId)
                );
                result.Set(
                    "required_party_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(
                            defenseObjective.RequiredPartyUnitIds
                        )
                    )
                );
                result.Set(
                    "start_tu",
                    StableValue.FromInteger(defenseObjective.StartTu)
                );
                result.Set(
                    "deadline_tu",
                    StableValue.FromInteger(defenseObjective.DeadlineTu)
                );
                return StableValue.FromMap(result);
            case BattleInterceptObjectiveRuntimeState interceptObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(nameof(BattleInterceptObjectiveRuntimeState))
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)interceptObjective.Mode)
                );
                result.Set(
                    "target_actor_id",
                    StableNullableStringName(interceptObjective.TargetActorId)
                );
                result.Set(
                    "target_unit_id",
                    StableNullableStringName(interceptObjective.TargetUnitId)
                );
                result.Set(
                    "exit_zone_id",
                    StableNullableStringName(interceptObjective.ExitZoneId)
                );
                result.Set(
                    "exit_edge",
                    StableValue.FromInteger((int)interceptObjective.ExitEdge)
                );
                result.Set(
                    "exit_depth",
                    StableValue.FromInteger(interceptObjective.ExitDepth)
                );
                result.Set(
                    "required_party_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(
                            interceptObjective.RequiredPartyUnitIds
                        )
                    )
                );
                result.Set(
                    "exit_coords",
                    StableValue.FromArray(
                        StableVector2IList(interceptObjective.ExitCoords)
                    )
                );
                return StableValue.FromMap(result);
            case BattleNodeOperationObjectiveRuntimeState nodeOperationObjective:
                result.Set(
                    "concrete_type",
                    StableValue.FromText(
                        nameof(BattleNodeOperationObjectiveRuntimeState)
                    )
                );
                result.Set(
                    "mode",
                    StableValue.FromInteger((int)nodeOperationObjective.Mode)
                );
                result.Set(
                    "required_party_unit_ids",
                    StableValue.FromArray(
                        StableStringNameList(
                            nodeOperationObjective.RequiredPartyUnitIds
                        )
                    )
                );
                var stableNodes = new List<StableValue>();
                foreach (
                    BattleOperationNodeRuntimeState node in
                    nodeOperationObjective.OperationNodes
                )
                {
                    StableMap stableNode = new();
                    stableNode.Set(
                        "node_id",
                        StableNullableStringName(node.NodeId)
                    );
                    stableNode.Set(
                        "display_name",
                        StableNullableText(node.DisplayName)
                    );
                    stableNode.Set(
                        "zone_id",
                        StableNullableStringName(node.ZoneId)
                    );
                    stableNode.Set(
                        "placement_edge",
                        StableValue.FromInteger((int)node.PlacementEdge)
                    );
                    stableNode.Set(
                        "placement_depth",
                        StableValue.FromInteger(node.PlacementDepth)
                    );
                    stableNode.Set(
                        "coord",
                        StableValue.FromVector2I(node.Coord)
                    );
                    stableNode.Set(
                        "is_completed",
                        StableValue.FromBool(node.IsCompleted)
                    );
                    stableNodes.Add(StableValue.FromMap(stableNode));
                }
                result.Set(
                    "operation_nodes",
                    StableValue.FromArray(stableNodes)
                );
                return StableValue.FromMap(result);
            default:
                throw new InvalidOperationException(
                    $"Unsupported battle objective runtime state '{objectiveRuntimeState.GetType().FullName}'."
                );
        }
    }

    internal static void MaterializeLazyStatusEffects(BattleUnitState unit)
    {
        if (unit == null)
        {
            return;
        }
        unit.GetStatusEffectsTyped();
    }

    internal static BattleTimelineState CloneTimeline(BattleTimelineState timeline)
    {
        if (timeline == null)
        {
            return null;
        }
        return new BattleTimelineState
        {
            current_tu = timeline.current_tu,
            tu_per_tick = timeline.tu_per_tick,
            frozen = timeline.frozen,
            ready_unit_ids = timeline.ready_unit_ids == null
                ? null
                : new StringNameList(timeline.ready_unit_ids),
        };
    }

    internal static BattleStatusEffectState DuplicateStatusEffect(BattleStatusEffectState effect)
    {
        if (effect == null)
            return null;
        return effect.DuplicateState();
    }

    internal static WarehouseState DuplicateWarehouse(WarehouseState warehouse)
    {
        if (warehouse == null)
        {
            return null;
        }
        WarehouseState copy = new();
        foreach (WarehouseStackState stack in warehouse.GetNonEmptyStacksTyped())
        {
            WarehouseStackState stackCopy = DuplicateWarehouseStack(stack);
            if (stackCopy != null && !stackCopy.IsEmpty())
            {
                copy.AddStack(stackCopy);
            }
        }
        foreach (EquipmentInstanceState instance in warehouse.GetNonEmptyEquipmentInstancesTyped())
        {
            EquipmentInstanceState instanceCopy = DuplicateEquipmentInstance(instance);
            if (instanceCopy != null && instanceCopy.instance_id != "" && instanceCopy.item_id != "")
            {
                copy.AddEquipmentInstance(instanceCopy);
            }
        }
        return copy;
    }

    internal static WarehouseState DuplicateWarehouseExact(WarehouseState warehouse) =>
        warehouse?.DuplicateForMutationSnapshotExact();

    internal static WarehouseStackState DuplicateWarehouseStack(WarehouseStackState stack)
    {
        if (stack == null)
        {
            return null;
        }
        return new WarehouseStackState
        {
            item_id = stack.item_id,
            quantity = stack.quantity,
        };
    }

    internal static EquipmentState DuplicateEquipment(EquipmentState equipment)
    {
        if (equipment == null)
        {
            return null;
        }
        EquipmentState copy = new();
        foreach (StringName slotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(slotId);
            if (entry == null || entry.IsEmpty())
            {
                continue;
            }
            copy.SetEquippedEntry(
                slotId,
                entry.item_id,
                DuplicateStringNameList(entry.occupied_slot_ids),
                DuplicateEquipmentInstance(entry.equipment_instance)
            );
        }
        return copy;
    }

    internal static EquipmentEntryState DuplicateEquipmentEntry(EquipmentEntryState entry)
    {
        if (entry == null)
        {
            return null;
        }
        return new EquipmentEntryState
        {
            item_id = entry.item_id,
            occupied_slot_ids = DuplicateStringNameList(entry.occupied_slot_ids),
            instance_id = entry.instance_id,
            equipment_instance = DuplicateEquipmentInstance(entry.equipment_instance),
        };
    }

    internal static EquipmentInstanceState DuplicateEquipmentInstance(EquipmentInstanceState instance)
    {
        return instance?.DuplicateForMutationSnapshotExact();
    }

    internal static EquipmentState DuplicateEquipmentExact(EquipmentState equipment) =>
        equipment?.DuplicateForMutationSnapshotExact();

    internal static BattlePendingCastState DuplicatePendingCastExact(
        BattlePendingCastState pendingCast
    ) => pendingCast?.DuplicateForMutationSnapshotExact();

    internal static List<BattleEffectiveTraitInstanceState>
        DuplicateEffectiveTraitInstancesExact(
            IEnumerable<BattleEffectiveTraitInstanceState> values
        )
    {
        if (values == null)
            return null;
        List<BattleEffectiveTraitInstanceState> result = new();
        foreach (BattleEffectiveTraitInstanceState value in values)
            result.Add(value?.DuplicateForMutationSnapshotExact());
        return result;
    }

    internal static List<StringName> DuplicateStringNameList(IEnumerable<StringName> values)
    {
        List<StringName> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    internal static List<StringName> StringNameArrayToList(IEnumerable<StringName> values)
    {
        List<StringName> result = new();
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
        {
            result.Add(value);
        }
        return result;
    }

    internal static List<Vector2I> Vector2IArrayToList(IEnumerable<Vector2I> values)
    {
        List<Vector2I> result = new();
        foreach (Vector2I value in values ?? System.Array.Empty<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

    internal static List<string> StringArrayToList(IEnumerable<string> values)
    {
        List<string> result = new();
        if (values == null)
            return result;
        foreach (string value in values)
        {
            result.Add(value);
        }
        return result;
    }

    internal static List<PlainPayloadSnapshot> PlainPayloadListToSnapshots(
        IEnumerable<IReadOnlyDictionary<string, object>> values
    )
    {
        List<PlainPayloadSnapshot> result = new();
        foreach (
            IReadOnlyDictionary<string, object> value in
            values ?? Array.Empty<IReadOnlyDictionary<string, object>>()
        )
        {
            result.Add(PlainPayloadSnapshot.Capture(value));
        }
        return result;
    }

    internal static List<StableValue> StableStringNameList(IEnumerable<StringName> values)
    {
        List<StableValue> result = new();
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
        {
            result.Add(StableNullableStringName(value));
        }
        return result;
    }

    internal static List<BattleEquipmentAbilitySourceState> DuplicateEquipmentAbilitySourcesExact(
        IEnumerable<BattleEquipmentAbilitySourceState> values
    )
    {
        if (values == null)
            return null;
        var result = new List<BattleEquipmentAbilitySourceState>();
        foreach (BattleEquipmentAbilitySourceState value in values)
        {
            if (value == null)
            {
                result.Add(null);
                continue;
            }
            result.Add(
                new BattleEquipmentAbilitySourceState
                {
                    EffectiveInstanceKey = value.EffectiveInstanceKey,
                    EquipmentDefId = value.EquipmentDefId,
                    SourceEquipmentInstanceId = value.SourceEquipmentInstanceId,
                    SourceKind = value.SourceKind,
                    AbilityIds = value.AbilityIds == null
                        ? null
                        : new List<StringName>(value.AbilityIds),
                }
            );
        }
        return result;
    }

    internal static List<BattleTemporalProgressModifierState> DuplicateTemporalProgressModifiersExact(
        IEnumerable<BattleTemporalProgressModifierState> values
    )
    {
        if (values == null)
            return null;
        var result = new List<BattleTemporalProgressModifierState>();
        foreach (BattleTemporalProgressModifierState value in values)
            result.Add(value?.DuplicateState());
        return result;
    }

    internal static List<StableValue> StableEffectiveTraitPayload(
        IEnumerable<BattleEffectiveTraitInstanceState> source)
    {
        List<StableValue> result = new();
        foreach (
            BattleEffectiveTraitInstanceState entryState in source
                ?? System.Array.Empty<BattleEffectiveTraitInstanceState>()
        )
        {
            if (entryState == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }

            StableMap entry = new();
            entry.Set("trait_id", StableNullableStringName(entryState.trait_id));
            entry.Set(
                "effective_instance_key",
                StableNullableStringName(entryState.effective_instance_key)
            );
            entry.Set("source_type", StableNullableStringName(entryState.source_type));
            entry.Set("source_id", StableNullableStringName(entryState.source_id));
            entry.Set("effect_type", StableNullableStringName(entryState.effect_type));
            entry.Set("trigger_type", StableNullableStringName(entryState.trigger_type));
            entry.Set(
                "charge_scope",
                StableNullableStringName(entryState.charge_scope)
            );
            entry.Set(
                "charge_reset_timing",
                StableNullableStringName(entryState.charge_reset_timing)
            );
            entry.Set("rank", StableValue.FromInteger(entryState.rank));
            entry.Set("stacks", StableValue.FromInteger(entryState.stacks));
            entry.Set(
                "roll_values",
                entryState.roll_values == null
                    ? StableValue.Nil()
                    : StableValue.FromArray(StableRollValuesExact(entryState.roll_values))
            );
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableRollValuesExact(
        IEnumerable<TraitRollValueState> rollValues
    )
    {
        List<StableValue> result = new();
        foreach (TraitRollValueState rollValue in rollValues ?? Array.Empty<TraitRollValueState>())
        {
            if (rollValue == null)
            {
                result.Add(StableValue.Nil());
                continue;
            }
            StableMap entry = new();
            entry.Set("key", StableNullableStringName(rollValue.key));
            entry.Set("value_type", StableNullableStringName(rollValue.value_type));
            entry.Set("int_value", StableValue.FromInteger(rollValue.int_value));
            entry.Set(
                "string_name_value",
                StableNullableStringName(rollValue.string_name_value)
            );
            entry.Set("bool_value", StableValue.FromBool(rollValue.bool_value));
            result.Add(StableValue.FromMap(entry));
        }
        return result;
    }

    internal static List<StableValue> StableVector2IList(IEnumerable<Vector2I> values)
    {
        List<StableValue> result = new();
        foreach (Vector2I value in values ?? System.Array.Empty<Vector2I>())
        {
            result.Add(StableValue.FromVector2I(value));
        }
        return result;
    }

    internal static List<StableValue> StableTextList(IEnumerable<string> values)
    {
        List<StableValue> result = new();
        foreach (string value in values ?? System.Array.Empty<string>())
        {
            result.Add(StableNullableText(value));
        }
        return result;
    }

    internal static StableValue StableNullableStringName(StringName value) =>
        value == null ? StableValue.Nil() : StableValue.FromText(value.ToString());

    internal static StableValue StableNullableText(string value) =>
        value == null ? StableValue.Nil() : StableValue.FromText(value);

    internal static List<StableValue> StablePlainPayloadSnapshotList(
        IEnumerable<PlainPayloadSnapshot> values
    )
    {
        List<StableValue> result = new();
        foreach (
            PlainPayloadSnapshot value in
            values ?? System.Array.Empty<PlainPayloadSnapshot>()
        )
        {
            result.Add(StableValue.FromMap(value?.ToStableMap() ?? new StableMap()));
        }
        return result;
    }

    internal static void NormalizeAllowedAiBookkeeping(
        StableMap expectedStable,
        StableMap afterStable,
        StringName activeUnitId
    )
    {
        if (
            !expectedStable.TryGetMap("units", out StableMap expectedUnits)
            || !afterStable.TryGetMap("units", out StableMap afterUnits)
        )
        {
            return;
        }

        string activeKey = BattleAiMutationSnapshotModel.StableKey(activeUnitId);
        if (
            !expectedUnits.TryGetMap(activeKey, out StableMap expectedUnit)
            || !afterUnits.TryGetMap(activeKey, out StableMap afterUnit)
        )
        {
            return;
        }
        if (
            !expectedUnit.TryGetMap("fields", out StableMap expectedFields)
            || !afterUnit.TryGetMap("fields", out StableMap afterFields)
        )
        {
            return;
        }

        foreach (string fieldName in BattleAiMutationGuard.AllowedActiveUnitFields)
        {
            if (expectedFields.TryGet(fieldName, out StableValue fieldValue))
            {
                afterFields.Set(fieldName, fieldValue.Clone());
            }
            else
            {
                afterFields.Remove(fieldName);
            }
        }

        if (
            expectedFields.TryGetMap("ai_blackboard", out StableMap expectedBlackboard)
            && afterFields.TryGetMap("ai_blackboard", out StableMap afterBlackboard)
        )
        {
            foreach (string key in BattleAiMutationGuard.AllowedActiveBlackboardKeys)
            {
                if (expectedBlackboard.TryGet(key, out StableValue blackboardValue))
                {
                    afterBlackboard.Set(key, blackboardValue.Clone());
                }
                else
                {
                    afterBlackboard.Remove(key);
                }
            }
            afterFields.Set("ai_blackboard", StableValue.FromMap(afterBlackboard));
        }

        afterUnit.Set("fields", StableValue.FromMap(afterFields));
        afterUnits.Set(activeKey, StableValue.FromMap(afterUnit));
        afterStable.Set("units", StableValue.FromMap(afterUnits));
    }
}
