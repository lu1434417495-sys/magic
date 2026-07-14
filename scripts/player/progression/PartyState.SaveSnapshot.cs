using System;
using System.Collections.Generic;
using Godot;

public partial class PartyState
{
    internal Dictionary<string, object> BuildSaveSnapshotPlain()
    {
        var rewards = new List<object>();
        foreach (PendingCharacterReward reward in pending_character_rewards)
        {
            if (reward != null)
                rewards.Add(BuildPendingCharacterRewardPlain(reward));
        }

        return Map(
            ("version", version),
            ("gold", GetGold()),
            ("leader_member_id", leader_member_id.ToString()),
            ("main_character_member_id", main_character_member_id.ToString()),
            ("fate_run_flags", BuildSortedFlagMap(fate_run_flags)),
            ("meta_flags", BuildSortedFlagMap(meta_flags)),
            ("active_member_ids", BuildStringList(active_member_ids)),
            ("reserve_member_ids", BuildStringList(reserve_member_ids)),
            ("member_states", BuildMemberStatesPlain(member_states)),
            ("pending_character_rewards", rewards),
            ("active_quests", BuildSortedQuestListPlain(active_quests)),
            ("claimable_quests", BuildSortedQuestListPlain(claimable_quests)),
            ("completed_quest_ids", BuildUniqueStringList(completed_quest_ids)),
            (
                "warehouse_state",
                warehouse_state != null ? BuildWarehouseStatePlain(warehouse_state) : EmptyMap()
            )
        );
    }

    private static Dictionary<string, object> BuildMemberStatesPlain(
        PartyMemberStateCollection members
    )
    {
        var result = EmptyMap();
        if (members == null)
            return result;

        foreach (StringName memberId in members.GetSortedIds())
        {
            PartyMemberState member = members.Get(memberId);
            if (member != null)
                result[memberId.ToString()] = BuildPartyMemberStatePlain(member);
        }
        return result;
    }

    private static Dictionary<string, object> BuildPartyMemberStatePlain(
        PartyMemberState member
    )
    {
        return Map(
            ("member_id", member.member_id.ToString()),
            ("display_name", member.display_name),
            ("faction_id", member.faction_id.ToString()),
            ("portrait_id", member.portrait_id.ToString()),
            (
                "progression",
                member.progression != null ? BuildUnitProgressPlain(member.progression) : EmptyMap()
            ),
            (
                "equipment_state",
                member.equipment_state != null
                    ? BuildEquipmentStatePlain(member.equipment_state)
                    : EmptyMap()
            ),
            ("control_mode", member.control_mode.ToString()),
            ("current_hp", member.current_hp),
            ("current_mp", member.current_mp),
            ("current_aura", member.current_aura),
            ("is_dead", member.is_dead),
            ("race_id", member.race_id.ToString()),
            ("subrace_id", member.subrace_id.ToString()),
            ("age_years", member.age_years),
            ("birth_at_world_step", member.birth_at_world_step),
            ("age_profile_id", member.age_profile_id.ToString()),
            ("natural_age_stage_id", member.natural_age_stage_id.ToString()),
            ("effective_age_stage_id", member.effective_age_stage_id.ToString()),
            (
                "effective_age_stage_source_type",
                member.effective_age_stage_source_type.ToString()
            ),
            (
                "effective_age_stage_source_id",
                member.effective_age_stage_source_id.ToString()
            ),
            ("body_size", member.body_size),
            ("body_size_category", member.body_size_category.ToString()),
            ("versatility_pick", member.versatility_pick.ToString()),
            (
                "active_stage_advancement_modifier_ids",
                BuildStringList(member.active_stage_advancement_modifier_ids)
            ),
            ("bloodline_id", member.bloodline_id.ToString()),
            ("bloodline_stage_id", member.bloodline_stage_id.ToString()),
            ("ascension_id", member.ascension_id.ToString()),
            ("ascension_stage_id", member.ascension_stage_id.ToString()),
            ("ascension_started_at_world_step", member.ascension_started_at_world_step),
            (
                "original_race_id_before_ascension",
                member.original_race_id_before_ascension.ToString()
            ),
            ("biological_age_years", member.biological_age_years),
            ("astral_memory_years", member.astral_memory_years),
            ("trait_instances", BuildTraitInstanceListPlain(member.trait_instances)),
            (
                "contingency_matrix_setups",
                BuildContingencySetupListPlain(member.ContingencySetupsForPlainSaveSnapshot)
            )
        );
    }

    private static Dictionary<string, object> BuildUnitProgressPlain(UnitProgress progress)
    {
        progress.SyncActiveCoreSkillIds();
        progress.SyncDefaultCombatResourceUnlocks();

        var skills = EmptyMap();
        foreach (StringName skillId in progress.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress skill = progress.GetSkillProgress(skillId);
            if (skill != null)
                skills[skillId.ToString()] = BuildUnitSkillProgressPlain(skill);
        }

        var professions = EmptyMap();
        foreach (StringName professionId in progress.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress profession = progress.GetProfessionProgress(professionId);
            if (profession != null)
                professions[professionId.ToString()] = BuildUnitProfessionProgressPlain(profession);
        }

        var pendingChoices = new List<object>();
        foreach (PendingProfessionChoice choice in progress.PendingProfessionChoicesTyped)
        {
            if (choice != null)
                pendingChoices.Add(BuildPendingProfessionChoicePlain(choice));
        }

        var achievements = EmptyMap();
        foreach (StringName achievementId in SortedKeys(progress.AchievementProgressTyped))
        {
            AchievementProgressState achievement = progress.GetAchievementProgressState(
                achievementId
            );
            if (achievement != null)
                achievements[achievementId.ToString()] = BuildAchievementProgressPlain(
                    achievement
                );
        }

        return Map(
            ("version", progress.version),
            ("unit_id", progress.unit_id.ToString()),
            ("display_name", progress.display_name),
            ("character_level", progress.character_level),
            (
                "unit_base_attributes",
                progress.unit_base_attributes != null
                    ? BuildUnitBaseAttributesPlain(progress.unit_base_attributes)
                    : EmptyMap()
            ),
            (
                "reputation_state",
                progress.reputation_state != null
                    ? BuildUnitReputationStatePlain(progress.reputation_state)
                    : EmptyMap()
            ),
            ("skills", skills),
            ("professions", professions),
            ("known_knowledge_ids", BuildStringList(progress.KnownKnowledgeIdsTyped)),
            ("active_core_skill_ids", BuildStringList(progress.ActiveCoreSkillIdsTyped)),
            (
                "attribute_growth_progress",
                BuildStringNameIntMap(progress.AttributeGrowthProgressTyped, sortKeys: false)
            ),
            ("achievement_progress", achievements),
            ("pending_profession_choices", pendingChoices),
            (
                "blocked_relearn_skill_ids",
                BuildStringList(progress.BlockedRelearnSkillIdsTyped)
            ),
            (
                "merged_skill_source_map",
                BuildStringNameListMap(progress.MergedSkillSourceMapTyped)
            ),
            (
                "unlocked_combat_resource_ids",
                BuildStringList(progress.UnlockedCombatResourceIdsTyped)
            ),
            (
                "active_level_trigger_core_skill_id",
                progress.active_level_trigger_core_skill_id.ToString()
            ),
            (
                "locked_level_trigger_skill_ids",
                BuildStringList(progress.LockedLevelTriggerSkillIdsTyped)
            )
        );
    }

    private static Dictionary<string, object> BuildUnitBaseAttributesPlain(
        UnitBaseAttributes attributes
    )
    {
        return Map(
            ("strength", attributes.strength),
            ("agility", attributes.agility),
            ("constitution", attributes.constitution),
            ("perception", attributes.perception),
            ("intelligence", attributes.intelligence),
            ("willpower", attributes.willpower),
            (
                "custom_stats",
                attributes.custom_stats != null
                    ? BuildStringNameIntMap(attributes.custom_stats.ValuesTyped, sortKeys: true)
                    : EmptyMap()
            )
        );
    }

    private static Dictionary<string, object> BuildUnitReputationStatePlain(
        UnitReputationState reputation
    )
    {
        return Map(
            ("morality", reputation.morality),
            (
                "custom_states",
                reputation.custom_states != null
                    ? BuildStringNameIntMap(reputation.custom_states.ValuesTyped, sortKeys: true)
                    : EmptyMap()
            )
        );
    }

    private static Dictionary<string, object> BuildUnitSkillProgressPlain(
        UnitSkillProgress skill
    )
    {
        return Map(
            ("skill_id", skill.skill_id.ToString()),
            ("is_learned", skill.is_learned),
            ("skill_level", skill.skill_level),
            ("current_mastery", skill.current_mastery),
            ("total_mastery_earned", skill.total_mastery_earned),
            ("is_core", skill.is_core),
            ("assigned_profession_id", skill.assigned_profession_id.ToString()),
            ("merged_from_skill_ids", BuildStringList(skill.merged_from_skill_ids)),
            ("mastery_from_training", skill.mastery_from_training),
            ("mastery_from_battle", skill.mastery_from_battle),
            ("profession_granted_by", skill.profession_granted_by.ToString()),
            ("granted_source_type", skill.granted_source_type.ToString()),
            ("granted_source_id", skill.granted_source_id.ToString()),
            ("core_max_growth_claimed", skill.core_max_growth_claimed),
            ("is_level_trigger_active", skill.is_level_trigger_active),
            ("is_level_trigger_locked", skill.is_level_trigger_locked),
            ("bonus_to_hit_from_lock", skill.bonus_to_hit_from_lock)
        );
    }

    private static Dictionary<string, object> BuildUnitProfessionProgressPlain(
        UnitProfessionProgress profession
    )
    {
        var promotionHistory = new List<object>();
        foreach (ProfessionPromotionRecord record in profession.promotion_history)
        {
            if (record != null)
                promotionHistory.Add(BuildProfessionPromotionRecordPlain(record));
        }

        return Map(
            ("profession_id", profession.profession_id.ToString()),
            ("rank", profession.rank),
            ("is_active", profession.is_active),
            ("is_hidden", profession.is_hidden),
            ("core_skill_ids", BuildStringList(profession.core_skill_ids)),
            ("granted_skill_ids", BuildStringList(profession.granted_skill_ids)),
            ("promotion_history", promotionHistory),
            ("inactive_reason", profession.inactive_reason.ToString())
        );
    }

    private static Dictionary<string, object> BuildProfessionPromotionRecordPlain(
        ProfessionPromotionRecord record
    )
    {
        return Map(
            ("new_rank", record.new_rank),
            ("consumed_skill_ids", BuildStringList(record.consumed_skill_ids)),
            ("qualifier_skill_ids", BuildStringList(record.qualifier_skill_ids)),
            (
                "snapshot_unit_base_attributes",
                record.snapshot_unit_base_attributes != null
                    ? BuildUnitBaseAttributesPlain(record.snapshot_unit_base_attributes)
                    : EmptyMap()
            ),
            ("timestamp", record.timestamp)
        );
    }

    private static Dictionary<string, object> BuildPendingProfessionChoicePlain(
        PendingProfessionChoice choice
    )
    {
        return Map(
            ("trigger_skill_ids", BuildStringList(choice.TriggerSkillIdsTyped)),
            ("candidate_profession_ids", BuildStringList(choice.CandidateProfessionIdsTyped)),
            (
                "target_rank_map",
                BuildStringNameIntMap(choice.TargetRankMapTyped, sortKeys: false)
            ),
            (
                "qualifier_skill_pool_ids",
                BuildStringList(choice.QualifierSkillPoolIdsTyped)
            ),
            (
                "assignable_skill_candidate_ids",
                BuildStringList(choice.AssignableSkillCandidateIdsTyped)
            ),
            ("required_qualifier_count", choice.required_qualifier_count),
            ("required_assigned_core_count", choice.required_assigned_core_count)
        );
    }

    private static Dictionary<string, object> BuildAchievementProgressPlain(
        AchievementProgressState achievement
    )
    {
        return Map(
            ("achievement_id", achievement.achievement_id.ToString()),
            ("current_value", achievement.current_value),
            ("is_unlocked", achievement.is_unlocked),
            ("unlocked_at_unix_time", achievement.unlocked_at_unix_time)
        );
    }

    private static Dictionary<string, object> BuildEquipmentStatePlain(EquipmentState equipment)
    {
        var slots = EmptyMap();
        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            if (entry != null)
                slots[entrySlotId.ToString()] = BuildEquipmentEntryStatePlain(entry);
        }
        return Map(("equipped_slots", slots));
    }

    private static Dictionary<string, object> BuildEquipmentEntryStatePlain(
        EquipmentEntryState entry
    )
    {
        return Map(
            ("occupied_slot_ids", BuildStringList(entry.occupied_slot_ids)),
            (
                "equipment_instance",
                entry.equipment_instance != null
                    ? BuildEquipmentInstanceStatePlain(entry.equipment_instance)
                    : EmptyMap()
            )
        );
    }

    private static Dictionary<string, object> BuildEquipmentInstanceStatePlain(
        EquipmentInstanceState instance
    )
    {
        var usagePeriods = new List<object>();
        if (instance.ability_usage_periods != null)
        {
            foreach (EquipmentAbilityUsagePeriodState usage in instance.ability_usage_periods)
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
        }

        var counters = new List<object>();
        if (instance.ability_persistent_counters != null)
        {
            foreach (
                EquipmentAbilityPersistentCounterState counter in instance.ability_persistent_counters
            )
            {
                if (counter == null)
                    continue;
                counters.Add(
                    Map(("counter_id", counter.CounterId ?? ""), ("value", counter.Value))
                );
            }
        }

        return Map(
            ("instance_id", instance.instance_id.ToString()),
            ("item_id", instance.item_id.ToString()),
            ("rarity", instance.rarity),
            ("current_durability", instance.current_durability),
            ("trait_instances", BuildTraitInstanceListPlain(instance.trait_instances)),
            ("ability_usage_periods", usagePeriods),
            ("ability_persistent_counters", counters)
        );
    }

    private static List<object> BuildTraitInstanceListPlain(
        IEnumerable<TraitInstanceState> instances
    )
    {
        var result = new List<object>();
        if (instances == null)
            return result;
        foreach (TraitInstanceState instance in instances)
        {
            if (instance != null)
                result.Add(BuildTraitInstanceStatePlain(instance));
        }
        return result;
    }

    private static Dictionary<string, object> BuildTraitInstanceStatePlain(
        TraitInstanceState instance
    )
    {
        var rollValues = EmptyMap();
        foreach (
            TraitRollValueState value in TraitInstanceState.NormalizeRollValues(
                instance.roll_values
            )
        )
        {
            object plainValue = value.ValueTypeKind switch
            {
                TraitRollValueType.Int => value.int_value,
                TraitRollValueType.StringName => value.string_name_value.ToString(),
                TraitRollValueType.Bool => value.bool_value,
                _ => throw new InvalidOperationException(
                    $"Unsupported trait roll value type for {value.key}."
                ),
            };
            rollValues[value.key.ToString()] = plainValue;
        }

        return Map(
            ("trait_instance_id", instance.trait_instance_id.ToString()),
            ("trait_id", instance.trait_id.ToString()),
            ("source_type", instance.source_type.ToString()),
            ("source_id", instance.source_id.ToString()),
            ("rank", instance.rank),
            ("stacks", instance.stacks),
            ("roll_values", rollValues)
        );
    }

    private static Dictionary<string, object> BuildWarehouseStatePlain(WarehouseState warehouse)
    {
        var stacks = new List<object>();
        foreach (WarehouseStackState stack in warehouse.GetNonEmptyStacksTyped())
            stacks.Add(BuildWarehouseStackStatePlain(stack));

        var instances = new List<object>();
        foreach (
            EquipmentInstanceState instance in warehouse.GetNonEmptyEquipmentInstancesTyped()
        )
        {
            instances.Add(BuildEquipmentInstanceStatePlain(instance));
        }

        return Map(("stacks", stacks), ("equipment_instances", instances));
    }

    private static Dictionary<string, object> BuildWarehouseStackStatePlain(
        WarehouseStackState stack
    )
    {
        return Map(
            ("item_id", stack.item_id.ToString()),
            ("quantity", Math.Max(stack.quantity, 0))
        );
    }

    private static List<object> BuildContingencySetupListPlain(
        IReadOnlyList<ContingencyMatrixSetupState> setups
    )
    {
        var result = new List<object>();
        if (setups == null)
            return result;
        foreach (ContingencyMatrixSetupState setup in setups)
        {
            if (setup != null)
                result.Add(BuildContingencySetupPlain(setup));
        }
        return result;
    }

    private static Dictionary<string, object> BuildContingencySetupPlain(
        ContingencyMatrixSetupState setup
    )
    {
        var costs = new List<object>();
        foreach (ContingencyMaterialCostState cost in setup.MaterialCosts)
        {
            costs.Add(
                cost != null
                    ? Map(("item_id", cost.ItemId.ToString()), ("quantity", cost.Quantity))
                    : EmptyMap()
            );
        }

        var spells = new List<object>();
        foreach (ContingencyStoredSpellEntryState spell in setup.StoredSpells)
        {
            spells.Add(spell != null ? BuildContingencyStoredSpellPlain(spell) : EmptyMap());
        }

        return Map(
            ("setup_id", setup.SetupId.ToString()),
            ("display_name", setup.DisplayName),
            ("enabled", setup.Enabled),
            ("charged", setup.Charged),
            ("source_skill_id", setup.SourceSkillId.ToString()),
            ("source_skill_level", setup.SourceSkillLevel),
            ("matrix_load", setup.MatrixLoad),
            ("reserved_mp_max", setup.ReservedMpMax),
            ("material_costs", costs),
            (
                "trigger",
                setup.Trigger != null
                    ? BuildPlainPayloadMap(setup.Trigger.PayloadForPlainSaveSnapshot)
                    : EmptyMap()
            ),
            ("release_mode", setup.ReleaseMode.ToString()),
            ("stored_spells", spells)
        );
    }

    private static Dictionary<string, object> BuildContingencyStoredSpellPlain(
        ContingencyStoredSpellEntryState spell
    )
    {
        return Map(
            ("stored_skill_id", spell.StoredSkillId.ToString()),
            ("cast_level", spell.CastLevel),
            ("order", spell.Order),
            (
                "target_resolver",
                spell.TargetResolver != null
                    ? BuildContingencyTargetResolverPlain(spell.TargetResolver)
                    : EmptyMap()
            ),
            (
                "parameter_bindings",
                BuildPlainPayloadMap(spell.ParameterBindingsForPlainSaveSnapshot)
            ),
            ("fallback_policy", spell.FallbackPolicy.ToString())
        );
    }

    private static Dictionary<string, object> BuildContingencyTargetResolverPlain(
        ContingencyTargetResolverState resolver
    )
    {
        var result = Map(("type", resolver.Type.ToString()));
        if (resolver.ResolverKind == ContingencyTargetResolverKind.EmptyCellNearOwner)
        {
            result["preference"] = resolver.Preference.ToString();
            result["max_distance"] = resolver.MaxDistance;
        }
        return result;
    }

    private static Dictionary<string, object> BuildPendingCharacterRewardPlain(
        PendingCharacterReward reward
    )
    {
        var entries = new List<object>();
        foreach (PendingCharacterRewardEntry entry in reward.entries)
        {
            if (entry == null)
                continue;
            entries.Add(
                Map(
                    ("entry_type", entry.entry_type.ToString()),
                    ("target_id", entry.target_id.ToString()),
                    ("target_label", entry.target_label),
                    ("amount", entry.amount),
                    ("reason_text", entry.reason_text)
                )
            );
        }

        return Map(
            ("reward_id", reward.reward_id.ToString()),
            ("member_id", reward.member_id.ToString()),
            ("member_name", reward.member_name),
            ("source_type", reward.source_type.ToString()),
            ("source_id", reward.source_id.ToString()),
            ("source_label", reward.source_label),
            ("summary_text", reward.summary_text),
            ("entries", entries)
        );
    }

    private static List<object> BuildSortedQuestListPlain(IEnumerable<QuestState> quests)
    {
        var sorted = new List<(string Id, QuestState State)>();
        if (quests != null)
        {
            foreach (QuestState quest in quests)
            {
                if (quest != null && quest.quest_id != "")
                    sorted.Add((quest.quest_id.ToString(), quest));
            }
        }
        sorted.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

        var result = new List<object>();
        foreach ((string _, QuestState quest) in sorted)
            result.Add(BuildQuestStatePlain(quest));
        return result;
    }

    private static Dictionary<string, object> BuildQuestStatePlain(QuestState quest)
    {
        return Map(
            ("quest_id", quest.quest_id.ToString()),
            ("status_id", quest.status_id.ToString()),
            (
                "objective_progress",
                quest.objective_progress != null
                    ? BuildStringNameIntMap(quest.objective_progress.ValuesTyped, sortKeys: true)
                    : EmptyMap()
            ),
            ("accepted_at_world_step", quest.accepted_at_world_step),
            ("completed_at_world_step", quest.completed_at_world_step),
            ("reward_claimed_at_world_step", quest.reward_claimed_at_world_step),
            (
                "last_progress_context",
                quest.last_progress_context != null
                    ? BuildQuestProgressContextPlain(quest.last_progress_context)
                    : EmptyMap()
            )
        );
    }

    private static Dictionary<string, object> BuildQuestProgressContextPlain(
        QuestProgressContext context
    )
    {
        var result = EmptyMap();
        if (context.MemberId != "")
            result["member_id"] = context.MemberId.ToString();
        if (!string.IsNullOrEmpty(context.ActionId))
            result["action_id"] = context.ActionId;
        if (context.EnemyTemplateId != "")
            result["enemy_template_id"] = context.EnemyTemplateId.ToString();
        if (!string.IsNullOrEmpty(context.SettlementId))
            result["settlement_id"] = context.SettlementId;
        if (context.SourceType != "")
            result["source_type"] = context.SourceType.ToString();
        if (context.SourceId != "")
            result["source_id"] = context.SourceId.ToString();
        if (context.ItemId != "")
            result["item_id"] = context.ItemId.ToString();
        if (context.SubmittedQuantity > 0)
            result["submitted_quantity"] = context.SubmittedQuantity;
        return result;
    }

    private static Dictionary<string, object> BuildSortedFlagMap(
        IReadOnlyDictionary<StringName, bool> values
    )
    {
        var result = EmptyMap();
        if (values == null)
            return result;
        foreach (StringName key in SortedKeys(values))
        {
            if (key != "")
                result[key.ToString()] = values[key];
        }
        return result;
    }

    private static Dictionary<string, object> BuildStringNameIntMap(
        IReadOnlyDictionary<StringName, int> values,
        bool sortKeys
    )
    {
        var result = EmptyMap();
        if (values == null)
            return result;

        if (sortKeys)
        {
            foreach (StringName key in SortedKeys(values))
                result[key.ToString()] = values[key];
            return result;
        }

        foreach (KeyValuePair<StringName, int> entry in values)
            result[entry.Key.ToString()] = entry.Value;
        return result;
    }

    private static Dictionary<string, object> BuildStringNameListMap(
        IReadOnlyDictionary<StringName, List<StringName>> values
    )
    {
        var result = EmptyMap();
        if (values == null)
            return result;
        foreach (KeyValuePair<StringName, List<StringName>> entry in values)
        {
            if (entry.Key != "")
                result[entry.Key.ToString()] = BuildStringList(entry.Value);
        }
        return result;
    }

    private static List<object> BuildStringList(IEnumerable<StringName> values)
    {
        var result = new List<object>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static List<object> BuildUniqueStringList(IEnumerable<StringName> values)
    {
        var result = new List<object>();
        var seen = new HashSet<StringName>();
        if (values == null)
            return result;
        foreach (StringName value in values)
        {
            if (value != "" && seen.Add(value))
                result.Add(value.ToString());
        }
        return result;
    }

    private static List<StringName> SortedKeys<T>(IReadOnlyDictionary<StringName, T> values)
    {
        var result = new List<StringName>(values.Keys);
        result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return result;
    }

    private static Dictionary<string, object> BuildPlainPayloadMap(
        IReadOnlyDictionary<string, object> values
    )
    {
        var result = EmptyMap();
        if (values == null)
            return result;
        foreach (KeyValuePair<string, object> entry in values)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = ConvertPlainPayloadValue(entry.Value);
        }
        return result;
    }

    private static object ConvertPlainPayloadValue(object value)
    {
        switch (value)
        {
            case null:
                return null;
            case bool or byte or short or int or long or float or double or string:
                return value;
            case StringName stringName:
                return stringName.ToString();
            case IReadOnlyDictionary<string, object> dictionary:
                return BuildPlainPayloadMap(dictionary);
            case IReadOnlyList<object> list:
            {
                var result = new List<object>(list.Count);
                for (int index = 0; index < list.Count; index++)
                    result.Add(ConvertPlainPayloadValue(list[index]));
                return result;
            }
            default:
                throw new InvalidOperationException(
                    $"Unsupported plain save payload value type {value.GetType().FullName}."
                );
        }
    }

    private static Dictionary<string, object> Map(
        params (string Key, object Value)[] fields
    )
    {
        var result = EmptyMap();
        foreach ((string key, object value) in fields)
            result[key] = value;
        return result;
    }

    private static Dictionary<string, object> EmptyMap() =>
        new(StringComparer.Ordinal);
}

public partial class PartyMemberState
{
    internal IReadOnlyList<ContingencyMatrixSetupState> ContingencySetupsForPlainSaveSnapshot =>
        _contingencyMatrixSetups;
}

public partial class ContingencyStoredSpellEntryState
{
    internal IReadOnlyDictionary<string, object> ParameterBindingsForPlainSaveSnapshot =>
        _parameterBindings;
}

public partial class ContingencyTriggerState
{
    internal IReadOnlyDictionary<string, object> PayloadForPlainSaveSnapshot => _payload;
}
