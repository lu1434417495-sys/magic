using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleUnitStatePlainSnapshot
{
    internal static Dictionary<string, object> Build(BattleUnitState state) =>
        state?.BuildPlainSnapshotDetached()
        ?? new Dictionary<string, object>(StringComparer.Ordinal);

    internal static GDictionary WriteOwned<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleUnitState state,
        string reason
    )
        where TLeaseRoot : class, IDisposable =>
        TraceDictionaryProjection.WriteDictionary(lease, Build(state), reason);
}

public partial class BattleUnitState
{
    internal Dictionary<string, object> BuildPlainSnapshotDetached()
    {
        EnsureBodySizeProjectionInvariant();
        SyncDefaultCombatResourceUnlocks();
        BattleUnitTurnSnapshot turnState = GetTurnStateTyped();
        BattleUnitShieldSnapshot shieldState = CaptureShieldStateCanonical();
        BattleUnitCooldownSnapshot cooldownState = GetCooldownStateTyped();
        BattleUnitActionClockSnapshot actionClockState =
            GetActionClockStateTyped();
        BattleUnitKnownSkillReadView knownSkillState =
            GetKnownSkillsReadViewTyped();
        BattleUnitCombatResourceUnlockReadView combatResourceUnlockState =
            GetCombatResourceUnlocksReadViewTyped();
        BattleUnitCombatResourceValues combatResources =
            GetCombatResourcesReadViewTyped().Values;
        BattleUnitGeometryReadView geometry =
            GetGeometryReadViewTyped();
        BattleUnitSaveModifierReadView saveModifiers =
            GetSaveModifiersReadViewTyped();
        BattleUnitDamageResistanceReadView damageResistances =
            GetDamageResistancesReadViewTyped();
        BattleUnitEffectiveTraitReadView effectiveTraits =
            GetEffectiveTraitsReadViewTyped();
        BattleUnitEquipmentAbilityProjectionReadView
            equipmentAbilityProjection =
                GetEquipmentAbilityProjectionReadViewTyped();
        NormalizeWeaponProjection();
        BattleWeaponProjectionValues weaponProjection =
            GetWeaponProjectionReadViewTyped().Values;

        return Map(
            ("unit_id", unit_id.ToString()),
            ("source_member_id", source_member_id.ToString()),
            ("enemy_template_id", enemy_template_id.ToString()),
            ("encounter_actor_id", encounter_actor_id.ToString()),
            ("display_name", display_name ?? ""),
            ("battle_sprite_texture_path", battle_sprite_texture_path ?? ""),
            ("faction_id", faction_id.ToString()),
            ("control_mode", control_mode.ToString()),
            ("ai_brain_id", ai_brain_id.ToString()),
            ("ai_state_id", ai_state_id.ToString()),
            (
                "cognition_kind",
                BattleCognitionContentRules
                    .ToStringName(GetBaseCognitionKindTyped())
                    .ToString()
            ),
            ("coord", geometry.AnchorCoord),
            ("body_size", geometry.BodySize),
            (
                "body_size_category",
                geometry.BodySizeCategory.ToString()
            ),
            ("footprint_size", geometry.FootprintSize),
            (
                "occupied_coords",
                BuildVectorList(geometry.OccupiedCoords)
            ),
            ("is_alive", combatResources.IsAlive),
            ("attribute_snapshot", BuildAttributeSnapshotPlain(attribute_snapshot)),
            ("equipment_view", BuildEquipmentStatePlain(GetEquipmentView())),
            ("current_hp", combatResources.Hp),
            ("current_mp", combatResources.Mp),
            ("current_stamina", combatResources.Stamina),
            ("current_aura", combatResources.Aura),
            ("aura_max", GetAuraMax()),
            ("current_ap", combatResources.Ap),
            ("current_move_points", combatResources.MovePoints),
            (
                "unlocked_combat_resource_ids",
                BuildCombatResourceUnlockList(
                    combatResourceUnlockState.ResourceIds
                )
            ),
            (
                "stamina_recovery_progress",
                combatResources.StaminaRecoveryProgress
            ),
            ("is_resting", IsRestingTyped()),
            ("has_taken_action_this_turn", turnState.HasTakenActionThisTurn),
            (
                "can_use_locked_move_points_this_turn",
                turnState.CanUseLockedMovePointsThisTurn
            ),
            ("current_shield_hp", shieldState.CurrentHp),
            ("shield_max_hp", shieldState.MaxHp),
            ("shield_duration", shieldState.Duration),
            ("shield_family", shieldState.Family.ToString()),
            ("shield_source_unit_id", shieldState.SourceUnitId.ToString()),
            ("shield_source_skill_id", shieldState.SourceSkillId.ToString()),
            ("action_progress", actionClockState.ActionProgress),
            ("action_threshold", actionClockState.ActionThreshold),
            (
                "known_active_skill_ids",
                BuildKnownActiveSkillList(knownSkillState.ActiveSkills)
            ),
            (
                "known_skill_level_map",
                BuildKnownSkillLevelMap(knownSkillState.SkillLevels)
            ),
            (
                "known_skill_lock_hit_bonus_map",
                BuildKnownSkillLevelMap(knownSkillState.LockHitBonuses)
            ),
            (
                "movement_tags",
                BuildStringList(
                    GetMovementTagsReadViewTyped().Tags
                )
            ),
            (
                "vision_tags",
                BuildStringList(
                    GetVisionProficiencyReadViewTyped().VisionTags
                )
            ),
            (
                "proficiency_tags",
                BuildStringList(
                    GetVisionProficiencyReadViewTyped().ProficiencyTags
                )
            ),
            (
                "save_advantage_tags",
                BuildStringList(saveModifiers.AdvantageTags)
            ),
            (
                "save_disadvantage_tags",
                BuildStringList(saveModifiers.DisadvantageTags)
            ),
            (
                "save_immunity_tags",
                BuildStringList(saveModifiers.ImmunityTags)
            ),
            (
                "damage_resistances",
                BuildDamageResistanceMap(
                    damageResistances.Resistances
                )
            ),
            (
                "save_bonus_by_ability",
                BuildSaveAbilityBonusMap(saveModifiers.BonusByAbility)
            ),
            (
                "effective_trait_instances",
                BuildEffectiveTraitInstancesPlain(
                    effectiveTraits.Instances
                )
            ),
            (
                "effective_trait_ids",
                BuildStringList(
                    GetCanonicalEffectiveTraitIdsReadViewTyped()
                )
            ),
            (
                "equipment_ability_sources",
                BuildEquipmentAbilitySourcesPlain(
                    equipmentAbilityProjection.Sources
                )
            ),
            (
                "creature_type_tags",
                BuildStringList(
                    GetCreatureTypeTagsReadViewTyped().Tags
                )
            ),
            ("versatility_pick", versatility_pick.ToString()),
            ("weapon_profile_kind", weaponProjection.ProfileKind.ToString()),
            ("weapon_item_id", weaponProjection.ItemId.ToString()),
            ("weapon_profile_type_id", weaponProjection.ProfileTypeId.ToString()),
            ("weapon_range_type", weaponProjection.RangeType.ToString()),
            ("weapon_family", weaponProjection.Family.ToString()),
            ("weapon_current_grip", weaponProjection.CurrentGrip.ToString()),
            ("weapon_attack_range", weaponProjection.AttackRange),
            (
                "weapon_one_handed_dice",
                BuildWeaponDicePlain(weaponProjection.OneHandedDice)
            ),
            (
                "weapon_two_handed_dice",
                BuildWeaponDicePlain(weaponProjection.TwoHandedDice)
            ),
            ("weapon_is_versatile", weaponProjection.IsVersatile),
            ("weapon_uses_two_hands", weaponProjection.UsesTwoHands),
            (
                "weapon_physical_damage_tag",
                weaponProjection.PhysicalDamageTag.ToString()
            ),
            ("cooldowns", BuildStringNameIntMap(cooldownState.Cooldowns)),
            ("last_turn_tu", cooldownState.LastTurnTu),
            ("status_effects", BuildStatusEffectsPlain())
        );
    }

    private Dictionary<string, object> BuildStatusEffectsPlain()
    {
        var result = EmptyMap();
        foreach (StringName statusId in _statusEffects.GetSortedStatusEffectIds())
        {
            BattleStatusEffectState effect = _statusEffects.Get(statusId);
            if (effect != null && !effect.IsEmpty())
                result[statusId.ToString()] = BuildStatusEffectPlain(effect);
        }
        return result;
    }

    private static Dictionary<string, object> BuildStatusEffectPlain(
        BattleStatusEffectState effect
    )
    {
        var result = Map(
            ("status_id", effect.status_id.ToString()),
            ("source_unit_id", effect.source_unit_id.ToString()),
            ("power", effect.power),
            ("params", effect.GetParamsTyped()),
            ("stacks", effect.stacks)
        );
        if (effect.HasDuration())
            result["duration"] = effect.duration;
        if (!string.IsNullOrWhiteSpace(effect.display_label))
            result["display_label"] = effect.display_label;
        if (effect.tick_interval_tu > 0)
            result["tick_interval_tu"] = effect.tick_interval_tu;
        if (effect.next_tick_at_tu > 0)
            result["next_tick_at_tu"] = effect.next_tick_at_tu;
        if (effect.timeline_damage_dice_count > 0 || effect.timeline_damage_dice_sides > 0)
        {
            result["timeline_damage_dice_count"] = effect.timeline_damage_dice_count;
            result["timeline_damage_dice_sides"] = effect.timeline_damage_dice_sides;
        }
        if (effect.timeline_damage_flat_bonus > 0)
            result["timeline_damage_flat_bonus"] = effect.timeline_damage_flat_bonus;
        if (effect.skip_next_turn_end_decay)
            result["skip_next_turn_end_decay"] = true;
        if (effect.counts_as_debuff_override)
        {
            result["counts_as_debuff_override"] = true;
            result["counts_as_debuff"] = effect.counts_as_debuff;
        }
        if (effect.lock_counterattack)
            result["lock_counterattack"] = true;
        if (effect.lock_guard)
            result["lock_guard"] = true;
        if (effect.lock_dodge_bonus)
            result["lock_dodge_bonus"] = true;
        if (effect.lock_crit)
            result["lock_crit"] = true;
        if (effect.main_skill_lock_other_debuff_count > 0)
        {
            result["main_skill_lock_other_debuff_count"] =
                effect.main_skill_lock_other_debuff_count;
        }
        return result;
    }

    private static Dictionary<string, object> BuildAttributeSnapshotPlain(
        AttributeSnapshot snapshot
    )
    {
        var result = EmptyMap();
        if (snapshot == null)
            return result;
        foreach ((StringName key, int value) in snapshot.GetAllValuesTyped())
            result[key.ToString()] = value;
        return result;
    }

    private static Dictionary<string, object> BuildEquipmentStatePlain(EquipmentState equipment)
    {
        var slots = EmptyMap();
        if (equipment != null)
        {
            foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
            {
                EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
                if (entry != null)
                    slots[entrySlotId.ToString()] = BuildEquipmentEntryPlain(entry);
            }
        }
        return Map(("equipped_slots", slots));
    }

    private static Dictionary<string, object> BuildEquipmentEntryPlain(
        EquipmentEntryState entry
    ) =>
        Map(
            ("occupied_slot_ids", BuildStringList(entry?.occupied_slot_ids)),
            (
                "equipment_instance",
                entry?.equipment_instance != null
                    ? BuildEquipmentInstancePlain(entry.equipment_instance)
                    : EmptyMap()
            )
        );

    private static Dictionary<string, object> BuildEquipmentInstancePlain(
        EquipmentInstanceState instance
    )
    {
        var usagePeriods = new List<object>();
        foreach (
            EquipmentAbilityUsagePeriodState usage
            in instance?.ability_usage_periods
                ?? new List<EquipmentAbilityUsagePeriodState>()
        )
        {
            if (usage == null)
                continue;
            usagePeriods.Add(
                Map(
                    ("ability_id", usage.AbilityId ?? ""),
                    ("period_kind", usage.PeriodKind ?? ""),
                    ("period_index", usage.PeriodIndex),
                    ("used_count", usage.UsedCount)
                )
            );
        }

        var counters = new List<object>();
        foreach (
            EquipmentAbilityPersistentCounterState counter
            in instance?.ability_persistent_counters
                ?? new List<EquipmentAbilityPersistentCounterState>()
        )
        {
            if (counter != null)
                counters.Add(Map(("counter_id", counter.CounterId ?? ""), ("value", counter.Value)));
        }

        return Map(
            ("instance_id", instance?.instance_id.ToString() ?? ""),
            ("item_id", instance?.item_id.ToString() ?? ""),
            ("rarity", instance?.rarity ?? 0),
            ("current_durability", instance?.current_durability ?? 0),
            ("trait_instances", BuildTraitInstancesPlain(instance?.trait_instances)),
            ("ability_usage_periods", usagePeriods),
            ("ability_persistent_counters", counters)
        );
    }

    private static List<object> BuildTraitInstancesPlain(
        IEnumerable<TraitInstanceState> instances
    )
    {
        var result = new List<object>();
        foreach (TraitInstanceState instance in instances ?? Array.Empty<TraitInstanceState>())
        {
            if (instance == null)
                continue;
            result.Add(
                Map(
                    ("trait_instance_id", instance.trait_instance_id.ToString()),
                    ("trait_id", instance.trait_id.ToString()),
                    ("source_type", instance.source_type.ToString()),
                    ("source_id", instance.source_id.ToString()),
                    ("rank", instance.rank),
                    ("stacks", instance.stacks),
                    ("roll_values", BuildRollValuesPlain(instance.roll_values))
                )
            );
        }
        return result;
    }

    private static List<object> BuildEffectiveTraitInstancesPlain(
        IEnumerable<BattleEffectiveTraitInstanceReadView> instances
    )
    {
        var result = new List<object>();
        foreach (
            BattleEffectiveTraitInstanceReadView instance
            in instances
            ?? Array.Empty<BattleEffectiveTraitInstanceReadView>()
        )
        {
            if (!instance.IsPresent)
                continue;
            result.Add(
                Map(
                    ("trait_id", instance.TraitId.ToString()),
                    (
                        "effective_instance_key",
                        instance.EffectiveInstanceKey.ToString()
                    ),
                    ("source_type", instance.SourceType.ToString()),
                    ("source_id", instance.SourceId.ToString()),
                    ("effect_type", instance.EffectType.ToString()),
                    ("trigger_type", instance.TriggerType.ToString()),
                    ("charge_scope", instance.ChargeScope.ToString()),
                    (
                        "charge_reset_timing",
                        instance.ChargeResetTiming.ToString()
                    ),
                    ("rank", Math.Max(instance.Rank, 1)),
                    ("stacks", Math.Max(instance.Stacks, 1)),
                    (
                        "roll_values",
                        BuildRollValuesPlain(
                            instance.RollValues.CopyNormalized()
                        )
                    )
                )
            );
        }
        return result;
    }

    private static Dictionary<StringName, object> BuildRollValuesPlain(
        IEnumerable<TraitRollValueState> values
    )
    {
        var result = new Dictionary<StringName, object>();
        foreach (TraitRollValueState value in TraitInstanceState.NormalizeRollValues(values))
        {
            object plainValue = value.ValueTypeKind switch
            {
                TraitRollValueType.Int => value.int_value,
                TraitRollValueType.StringName => value.string_name_value,
                TraitRollValueType.Bool => value.bool_value,
                _ => throw new InvalidOperationException(
                    $"Unsupported trait roll value type for {value.key}."
                ),
            };
            result[value.key] = plainValue;
        }
        return result;
    }

    private static List<object> BuildEquipmentAbilitySourcesPlain(
        IEnumerable<BattleEquipmentAbilitySourceReadView> sources
    )
    {
        var result = new List<object>();
        foreach (
            BattleEquipmentAbilitySourceReadView source
            in sources
                ?? Array.Empty<
                    BattleEquipmentAbilitySourceReadView
                >()
        )
        {
            if (source == null)
                continue;
            result.Add(
                Map(
                    ("effective_instance_key", source.EffectiveInstanceKey.ToString()),
                    ("equipment_def_id", source.EquipmentDefId.ToString()),
                    (
                        "source_equipment_instance_id",
                        source.SourceEquipmentInstanceId.ToString()
                    ),
                    (
                        "source_kind",
                        BattleEquipmentAbilitySourceState.ToStringName(source.SourceKind).ToString()
                    ),
                    ("ability_ids", BuildStringList(source.AbilityIds))
                )
            );
        }
        return result;
    }

    private static Dictionary<string, object> BuildWeaponDicePlain(
        BattleWeaponDiceValues dice
    )
    {
        if (!dice.HasUsableDice)
            return EmptyMap();
        return Map(
            ("dice_count", dice.DiceCount),
            ("dice_sides", dice.DiceSides),
            ("flat_bonus", dice.FlatBonus)
        );
    }

    private static Dictionary<string, object> BuildStringIntMap(BattleStringNameIntMap values)
    {
        var result = EmptyMap();
        if (values == null)
            return result;
        foreach ((StringName key, int value) in values)
            result[key.ToString()] = value;
        return result;
    }

    private static Dictionary<string, object> BuildKnownSkillLevelMap(
        BattleKnownSkillLevelReadView values
    )
    {
        var result = EmptyMap();
        foreach ((StringName key, int value) in values)
            result[key.ToString()] = value;
        return result;
    }

    private static Dictionary<string, object> BuildSaveAbilityBonusMap(
        BattleSaveAbilityBonusReadView values
    )
    {
        var result = EmptyMap();
        foreach ((StringName key, int value) in values)
            result[key.ToString()] = value;
        return result;
    }

    private static Dictionary<StringName, int> BuildStringNameIntMap(
        BattleStringNameIntMap values
    )
    {
        var result = new Dictionary<StringName, int>();
        if (values == null)
            return result;
        foreach ((StringName key, int value) in values)
            result[key] = value;
        return result;
    }

    private static Dictionary<string, object>
        BuildDamageResistanceMap(
        BattleDamageResistanceReadView values
    )
    {
        var result = EmptyMap();
        foreach ((StringName key, StringName value) in values)
            result[key.ToString()] = value.ToString();
        return result;
    }

    private static List<object> BuildStringList(IEnumerable<StringName> values)
    {
        var result = new List<object>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
            result.Add(value.ToString());
        return result;
    }

    private static List<object> BuildCombatResourceUnlockList(
        BattleCombatResourceUnlockReadView values
    )
    {
        var result = new List<object>();
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static List<object> BuildKnownActiveSkillList(
        BattleKnownActiveSkillReadView values
    )
    {
        var result = new List<object>();
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static List<object> BuildVectorList(IEnumerable<Vector2I> values)
    {
        var result = new List<object>();
        foreach (Vector2I value in values ?? Array.Empty<Vector2I>())
            result.Add(value);
        return result;
    }

    private static Dictionary<string, object> EmptyMap() =>
        new(StringComparer.Ordinal);

    private static Dictionary<string, object> Map(
        params (string Key, object Value)[] entries
    )
    {
        var result = EmptyMap();
        foreach ((string key, object value) in entries)
        {
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("Battle unit snapshot keys must not be empty.");
            result.Add(key, value);
        }
        return result;
    }
}
