using System;
using Godot;
using Godot.Collections;

public sealed class GameRuntimeSnapshotBuilder
{
    private static readonly string[] QuestEntryRequiredFields =
    {
        "quest_id",
        "status_id",
        "objective_progress",
        "accepted_at_world_step",
        "completed_at_world_step",
        "reward_claimed_at_world_step",
        "last_progress_context",
    };

    private const int HiddenLuckAtBirthMax = 2;
    private const int HiddenLuckAtBirthMin = -6;
    private const int InitialHpBase = 14;
    private static readonly StringName DefaultSourceId = "birth_roll";
    private const int MaximumRerollTierMinimum = 10_000_000;
    private const string CreationOptionBakeRerollLuck = "bake_reroll_luck";

    private WeakReference<IGameRuntimeSnapshotSource> _runtimeRef;

    private IGameRuntimeSnapshotSource _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<IGameRuntimeSnapshotSource>(value) : null;
    }

    public void Setup(IGameRuntimeSnapshotSource runtime)
    {
        _runtime = runtime;
    }

    public void Dispose()
    {
        _runtime = null;
    }

    public Dictionary BuildHeadlessSnapshot()
    {
        if (_runtime == null)
            return new Dictionary();
        return new Dictionary
        {
            ["status"] = new Dictionary
            {
                ["view"] = _runtime.is_battle_active() ? "battle" : "world",
                ["text"] = _runtime.get_status_text(),
            },
            ["modal"] = new Dictionary { ["id"] = _runtime.get_active_modal_id() },
            ["logs"] = BuildLogSnapshot(),
            ["world"] = BuildWorldSnapshot(),
            ["submap"] = BuildSubmapSnapshot(),
            ["game_over"] = BuildGameOverSnapshot(),
            ["party"] = BuildPartySnapshot(),
            ["settlement"] = BuildSettlementSnapshot(),
            ["contract_board"] = BuildContractBoardSnapshot(),
            ["shop"] = BuildShopSnapshot(),
            ["forge"] = BuildForgeSnapshot(),
            ["stagecoach"] = BuildStagecoachSnapshot(),
            ["character_info"] = BuildCharacterInfoSnapshot(),
            ["warehouse"] = BuildWarehouseSnapshot(),
            ["battle"] = BuildBattleSnapshot(),
            ["loot"] = BuildLootSnapshot(),
            ["reward"] = BuildRewardSnapshot(),
            ["promotion"] = BuildPromotionSnapshot(),
        };
    }

    public string BuildTextSnapshot()
    {
        return GameTextSnapshotRenderer.render_world_snapshot(BuildHeadlessSnapshot());
    }

    private Dictionary BuildWorldSnapshot()
    {
        var selectedSettlement = _runtime.get_selected_settlement();
        var selectedNpc = _runtime.get_selected_world_npc();
        var selectedEncounter = _runtime.get_selected_encounter_anchor();
        var selectedWorldEvent = _runtime.get_selected_world_event();
        return new Dictionary
        {
            ["map_id"] = _runtime.get_active_map_id(),
            ["map_display_name"] = _runtime.get_active_map_display_name(),
            ["is_submap"] = _runtime.is_submap_active(),
            ["world_step"] = _runtime.get_world_step(),
            ["player_coord"] = CoordToDict(_runtime.get_player_coord()),
            ["player_visible_on_map"] = _runtime.is_player_visible_on_world_map(),
            ["selected_coord"] = CoordToDict(_runtime.get_selected_coord()),
            ["selected_settlement_id"] = DictionaryString(selectedSettlement, "settlement_id", ""),
            ["selected_npc_name"] = DictionaryString(selectedNpc, "display_name", ""),
            ["selected_world_event_id"] = DictionaryString(selectedWorldEvent, "event_id", ""),
            ["selected_world_event_name"] = DictionaryString(
                selectedWorldEvent,
                "display_name",
                ""
            ),
            ["selected_encounter_id"] =
                selectedEncounter != null ? selectedEncounter.entity_id : "",
            ["selected_encounter_name"] =
                selectedEncounter != null ? selectedEncounter.display_name : "",
            ["selected_encounter_kind"] =
                selectedEncounter != null ? selectedEncounter.encounter_kind : "",
            ["selected_encounter_growth_stage"] =
                selectedEncounter != null ? selectedEncounter.growth_stage : 0,
            ["nearby_world_events"] = BuildNearbyWorldEventEntries(),
            ["nearby_encounters"] = BuildNearbyEncounterEntries(),
        };
    }

    private Dictionary BuildSubmapSnapshot()
    {
        var prompt = _runtime.get_pending_submap_prompt();
        return new Dictionary
        {
            ["active"] = _runtime.is_submap_active(),
            ["map_id"] = _runtime.get_active_map_id(),
            ["map_display_name"] = _runtime.get_active_map_display_name(),
            ["return_hint_text"] = _runtime.get_submap_return_hint_text(),
            ["confirm_visible"] =
                _runtime.get_active_modal_id() == "submap_confirm",
            ["prompt"] = prompt.Duplicate(true),
        };
    }

    private Dictionary BuildGameOverSnapshot()
    {
        var context = _runtime.get_game_over_context();
        if (context.Count == 0)
            return new Dictionary();
        return context.Duplicate(true);
    }

    private Dictionary BuildPartySnapshot()
    {
        var members = new Godot.Collections.Array();
        PartyState partyState = _runtime.get_party_state();
        if (partyState != null)
        {
            foreach (var memberId in partyState.active_member_ids)
                members.Add(BuildPartyMemberSnapshot(memberId, "active"));
            foreach (var memberId in partyState.reserve_member_ids)
                members.Add(BuildPartyMemberSnapshot(memberId, "reserve"));
        }
        return new Dictionary
        {
            ["gold"] = partyState != null ? partyState.gold : 0,
            ["leader_member_id"] =
                partyState != null ? partyState.leader_member_id.ToString() : "",
            ["active_member_ids"] =
                partyState != null
                    ? StringNameArrayToStringArray(partyState.active_member_ids)
                    : new Godot.Collections.Array(),
            ["reserve_member_ids"] =
                partyState != null
                    ? StringNameArrayToStringArray(partyState.reserve_member_ids)
                    : new Godot.Collections.Array(),
            ["selected_member_id"] = _runtime.get_party_selected_member_id().ToString(),
            ["pending_reward_count"] = _runtime.get_pending_reward_count(),
            ["members"] = members,
            ["quests"] = BuildQuestSnapshot(partyState),
        };
    }

    private Dictionary BuildQuestSnapshot(PartyState partyState)
    {
        if (partyState == null)
            return new Dictionary();
        var activeQuestEntries = BuildQuestEntries(partyState.get_active_quests(), "active");
        var claimableQuestEntries = BuildQuestEntries(
            partyState.get_claimable_quests(),
            "claimable"
        );
        var completedQuestIds = StringNameArrayToStringArray(partyState.get_completed_quest_ids());
        if (
            activeQuestEntries.Count == 0
            && claimableQuestEntries.Count == 0
            && completedQuestIds.Count == 0
        )
            return new Dictionary();
        var activeQuestIds = BuildQuestIds(activeQuestEntries);
        var claimableQuestIds = BuildQuestIds(claimableQuestEntries);
        return new Dictionary
        {
            ["active_quest_ids"] = activeQuestIds,
            ["claimable_quest_ids"] = claimableQuestIds,
            ["completed_quest_ids"] = completedQuestIds,
            ["active_quests"] = activeQuestEntries,
            ["claimable_quests"] = claimableQuestEntries,
        };
    }

    private Godot.Collections.Array BuildQuestEntries(
        Godot.Collections.Array<QuestState> questEntries,
        string stageId
    )
    {
        var entries = new Godot.Collections.Array();
        if (questEntries == null)
            return entries;
        foreach (var questState in questEntries)
        {
            var questEntry = NormalizeQuestEntry(questState, stageId);
            if (questEntry.Count > 0)
                entries.Add(questEntry);
        }
        var sortedList = new System.Collections.Generic.List<Dictionary>();
        foreach (var entry in entries)
            sortedList.Add(entry.AsGodotDictionary());
        sortedList.Sort(
            (a, b) =>
            {
                var aId = DictionaryString(a, "quest_id", "");
                var bId = DictionaryString(b, "quest_id", "");
                return string.Compare(aId, bId, System.StringComparison.Ordinal);
            }
        );
        var result = new Godot.Collections.Array();
        foreach (var entry in sortedList)
            result.Add(entry);
        return result;
    }

    private Godot.Collections.Array BuildQuestIds(Godot.Collections.Array questEntries)
    {
        var questIds = new Godot.Collections.Array();
        foreach (var entry in questEntries)
        {
            var questId = DictionaryString(entry.AsGodotDictionary(), "quest_id", "");
            if (!string.IsNullOrEmpty(questId))
                questIds.Add(questId);
        }
        return questIds;
    }

    private Dictionary NormalizeQuestEntry(QuestState questState, string stageId)
    {
        Dictionary questData = questState != null ? questState.to_dict().Duplicate(true) : null;
        if (questData == null || questData.Count == 0)
            return new Dictionary();
        if (!HasExactQuestEntryFields(questData))
            return new Dictionary();
        var questId = ReadQuestString(questData, "quest_id");
        var statusId = ReadQuestString(questData, "status_id");
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(statusId))
            return new Dictionary();
        if (
            !IsValidQuestStep(questData, "accepted_at_world_step")
            || !IsValidQuestStep(questData, "completed_at_world_step")
            || !IsValidQuestStep(questData, "reward_claimed_at_world_step")
        )
            return new Dictionary();
        var objectiveProgress = NormalizeQuestProgressMap(questData, "objective_progress");
        if (objectiveProgress == null)
            return new Dictionary();
        var contextValue = questData["last_progress_context"];
        if (contextValue.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        questData["quest_id"] = questId;
        questData["stage_id"] = stageId;
        questData["status_id"] = statusId;
        questData["objective_progress"] = objectiveProgress;
        questData["last_progress_context"] = contextValue.AsGodotDictionary().Duplicate(true);
        return questData;
    }

    private static bool IsValidQuestStep(Dictionary questData, string fieldName)
    {
        var value = questData[fieldName];
        return value.VariantType == Variant.Type.Int && value.AsInt32() >= -1;
    }

    private static bool HasExactQuestEntryFields(Dictionary questData)
    {
        if (questData.Count != QuestEntryRequiredFields.Length)
            return false;
        foreach (var fieldName in QuestEntryRequiredFields)
        {
            if (!questData.ContainsKey(fieldName))
                return false;
        }
        return true;
    }

    private static string ReadQuestString(Dictionary questData, string fieldName)
    {
        var value = questData[fieldName];
        if (
            value.VariantType != Variant.Type.String
            && value.VariantType != Variant.Type.StringName
        )
            return "";
        return value.AsString().StripEdges();
    }

    private static Dictionary NormalizeQuestProgressMap(Dictionary questData, string fieldName)
    {
        var progressValue = questData[fieldName];
        if (progressValue.VariantType != Variant.Type.Dictionary)
            return null;
        var result = new Dictionary();
        foreach (var objectiveIdValue in progressValue.AsGodotDictionary().Keys)
        {
            var objectiveId = "";
            if (
                objectiveIdValue.VariantType == Variant.Type.String
                || objectiveIdValue.VariantType == Variant.Type.StringName
            )
                objectiveId = objectiveIdValue.AsString().StripEdges();
            if (string.IsNullOrEmpty(objectiveId))
                return null;
            var objectiveProgressValue = progressValue.AsGodotDictionary()[objectiveIdValue];
            if (
                objectiveProgressValue.VariantType != Variant.Type.Int
                || objectiveProgressValue.AsInt32() < 0
            )
                return null;
            result[objectiveId] = objectiveProgressValue.AsInt32();
        }
        return result;
    }

    private Dictionary BuildPartyMemberSnapshot(StringName memberId, string rosterRole)
    {
        PartyState partyState = _runtime.get_party_state();
        PartyMemberState memberState =
            partyState != null ? partyState.get_member_state(memberId) : null;
        var achievementSummary = _runtime
            .get_member_achievement_summary(memberId);
        var attributeSnapshot = _runtime.get_member_attribute_snapshot(memberId);
        var equipmentEntries = _runtime.get_member_equipped_entries(memberId);
        UnitProgress progression = memberState?.progression;
        return new Dictionary
        {
            ["member_id"] = memberId.ToString(),
            ["display_name"] = _runtime.get_member_display_name(memberId),
            ["roster_role"] = rosterRole,
            ["is_leader"] =
                partyState != null && partyState.leader_member_id == memberId,
            ["current_hp"] = memberState != null ? memberState.current_hp : 0,
            ["current_mp"] = memberState != null ? memberState.current_mp : 0,
            ["current_aura"] = memberState != null ? memberState.current_aura : 0,
            ["unlocked_combat_resource_ids"] = BuildMemberUnlockedCombatResourceIds(memberState),
            ["learned_skill_ids"] = BuildMemberLearnedSkillIds(memberState),
            ["active_core_skill_ids"] = BuildSortedStringNameArray(
                progression != null
                    ? progression.active_core_skill_ids
                    : new Godot.Collections.Array<StringName>()
            ),
            ["active_level_trigger_core_skill_id"] =
                progression != null ? progression.active_level_trigger_core_skill_id.ToString() : "",
            ["locked_level_trigger_skill_ids"] = BuildSortedStringNameArray(
                progression != null
                    ? progression.locked_level_trigger_skill_ids
                    : new Godot.Collections.Array<StringName>()
            ),
            ["blocked_relearn_skill_ids"] = BuildSortedStringNameArray(
                progression != null
                    ? progression.blocked_relearn_skill_ids
                    : new Godot.Collections.Array<StringName>()
            ),
            ["skill_entries"] = BuildMemberSkillEntries(memberState),
            ["profession_entries"] = BuildMemberProfessionEntries(memberState),
            ["achievement_summary"] =
                achievementSummary.Count > 0
                    ? achievementSummary.Duplicate(true)
                    : new Dictionary(),
            ["attributes"] =
                attributeSnapshot != null ? attributeSnapshot.to_dict() : new Dictionary(),
            ["equipment"] = equipmentEntries,
            ["equipment_count"] = equipmentEntries.Count,
        };
    }

    private Godot.Collections.Array BuildMemberLearnedSkillIds(PartyMemberState memberState)
    {
        var learnedSkillIds = new Godot.Collections.Array();
        if (memberState == null)
            return learnedSkillIds;
        UnitProgress progression = memberState.progression;
        if (progression == null)
            return learnedSkillIds;
        foreach (var skillKey in progression.skills.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            var skillProgress = progression.get_skill_progress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            learnedSkillIds.Add(skillId.ToString());
        }
        learnedSkillIds.Sort();
        return learnedSkillIds;
    }

    private Godot.Collections.Array BuildMemberUnlockedCombatResourceIds(
        PartyMemberState memberState
    )
    {
        var resourceIds = new Godot.Collections.Array();
        if (memberState == null)
            return resourceIds;
        UnitProgress progression = memberState.progression;
        if (progression == null)
            return resourceIds;
        foreach (var resourceId in progression.unlocked_combat_resource_ids)
            resourceIds.Add(resourceId.ToString());
        resourceIds.Sort();
        return resourceIds;
    }

    private Godot.Collections.Array BuildMemberSkillEntries(PartyMemberState memberState)
    {
        var entries = new Godot.Collections.Array();
        if (memberState == null)
            return entries;
        UnitProgress progression = memberState.progression;
        if (progression == null)
            return entries;
        foreach (var skillKey in ProgressionDataUtils.sorted_string_keys(progression.skills))
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            var skillProgress = progression.get_skill_progress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            entries.Add(
                new Dictionary
                {
                    ["skill_id"] = skillId.ToString(),
                    ["level"] = skillProgress.skill_level,
                    ["is_core"] = skillProgress.is_core,
                    ["assigned_profession_id"] = skillProgress.assigned_profession_id.ToString(),
                    ["is_level_trigger_active"] = skillProgress.is_level_trigger_active,
                    ["is_level_trigger_locked"] = skillProgress.is_level_trigger_locked,
                    ["core_max_growth_claimed"] = skillProgress.core_max_growth_claimed,
                    ["granted_source_type"] = skillProgress.granted_source_type.ToString(),
                    ["granted_source_id"] = skillProgress.granted_source_id.ToString(),
                }
            );
        }
        return entries;
    }

    private Godot.Collections.Array BuildMemberProfessionEntries(PartyMemberState memberState)
    {
        var entries = new Godot.Collections.Array();
        if (memberState == null)
            return entries;
        UnitProgress progression = memberState.progression;
        if (progression == null)
            return entries;
        foreach (var professionKey in ProgressionDataUtils.sorted_string_keys(progression.professions))
        {
            var professionId = ProgressionDataUtils.to_string_name(professionKey);
            var professionProgress = progression.get_profession_progress(professionId);
            if (professionProgress == null)
                continue;
            entries.Add(
                new Dictionary
                {
                    ["profession_id"] = professionId.ToString(),
                    ["rank"] = professionProgress.rank,
                    ["is_active"] = professionProgress.is_active,
                    ["is_hidden"] = professionProgress.is_hidden,
                    ["core_skill_ids"] = BuildSortedStringNameArray(
                        professionProgress.core_skill_ids
                    ),
                    ["granted_skill_ids"] = BuildSortedStringNameArray(
                        professionProgress.granted_skill_ids
                    ),
                    ["inactive_reason"] = professionProgress.inactive_reason.ToString(),
                }
            );
        }
        return entries;
    }

    private Godot.Collections.Array BuildSortedStringNameArray(Godot.Collections.Array values)
    {
        var result = StringNameArrayToStringArray(values);
        result.Sort();
        return result;
    }

    private Godot.Collections.Array BuildSortedStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = StringNameArrayToStringArray(values);
        result.Sort();
        return result;
    }

    private Dictionary BuildSettlementSnapshot()
    {
        var settlementId = _runtime.get_resolved_settlement_id();
        var windowData = !string.IsNullOrEmpty(settlementId)
            ? _runtime.get_settlement_window_data(settlementId)
            : new Dictionary();
        var services = new Godot.Collections.Array();
        foreach (
            var serviceValue in DictionaryArray(
                windowData,
                "available_services",
                new Godot.Collections.Array()
            )
        )
        {
            if (serviceValue.VariantType != Variant.Type.Dictionary)
                continue;
            var serviceData = serviceValue.AsGodotDictionary();
            services.Add(
                new Dictionary
                {
                    ["action_id"] = DictionaryString(serviceData, "action_id", ""),
                    ["facility_name"] = DictionaryString(serviceData, "facility_name", ""),
                    ["npc_name"] = DictionaryString(serviceData, "npc_name", ""),
                    ["service_type"] = DictionaryString(serviceData, "service_type", ""),
                    ["interaction_script_id"] = DictionaryString(
                        serviceData,
                        "interaction_script_id",
                        ""
                    ),
                }
            );
        }
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "settlement",
            ["settlement_id"] = settlementId,
            ["display_name"] = DictionaryString(windowData, "display_name", ""),
            ["tier_name"] = DictionaryString(windowData, "tier_name", ""),
            ["faction_id"] = DictionaryString(windowData, "faction_id", ""),
            ["services"] = services,
            ["feedback_text"] = _runtime.get_settlement_feedback_text(),
        };
    }

    private Dictionary BuildShopSnapshot()
    {
        var windowData = _runtime.get_shop_window_data();
        if (WindowDataMatchesPanelKind(windowData, "forge"))
            windowData.Clear();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "shop",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildContractBoardSnapshot()
    {
        var windowData = ResolveContractBoardWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "contract_board",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildForgeSnapshot()
    {
        var windowData = ResolveForgeWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "forge",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildStagecoachSnapshot()
    {
        var windowData = _runtime.get_stagecoach_window_data();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "stagecoach",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildCharacterInfoSnapshot()
    {
        var context = _runtime.get_character_info_context();
        context["visible"] = _runtime.get_active_modal_id() == "character_info";
        if (context.ContainsKey("coord"))
            context["coord"] = CoordToDict(DictionaryVector2I(context, "coord", Vector2I.Zero));
        return context;
    }

    private Dictionary BuildWarehouseSnapshot()
    {
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "warehouse",
            ["entry_label"] = _runtime.get_active_warehouse_entry_label(),
            ["window_data"] =
                _runtime.get_party_state() != null
                    ? _runtime.get_warehouse_window_data()
                    : new Dictionary(),
        };
    }

    private Dictionary BuildBattleSnapshot()
    {
        var battleState = _runtime.get_battle_state();
        if (battleState == null || battleState.is_empty())
            return new Dictionary { ["active"] = false };

        var battleRuntime = _runtime.get_battle_runtime();
        var calamitySnapshot = new Dictionary();
        if (battleRuntime != null)
            calamitySnapshot = ProgressionDataUtils.string_name_int_map_to_string_dict(
                battleRuntime.get_calamity_by_member_id()
            );

        var adapter = new BattleHudAdapter();
        adapter.setup_runtime_context(_runtime as GameRuntimeFacade, _runtime.get_game_session());
        var hudSnapshot = adapter.build_snapshot(
            battleState,
            _runtime.get_battle_selected_coord(),
            _runtime.get_selected_battle_skill_id(),
            _runtime.get_selected_battle_skill_name(),
            _runtime.get_selected_battle_skill_variant_name(),
            _runtime.get_selected_battle_skill_target_coords(),
            _runtime.get_selected_battle_skill_required_coord_count(),
            _runtime.get_selected_battle_skill_target_unit_ids(),
            _runtime.get_selected_battle_skill_variant_id(),
            _runtime.get_active_battle_encounter_name()
        );

        var units = new Godot.Collections.Array();
        var battleUnits = battleState.units;
        foreach (var unitIdStr in ProgressionDataUtils.sorted_string_keys(battleUnits))
        {
            var unitId = new StringName(unitIdStr);
            var unitState = battleUnits.ContainsKey(unitId)
                ? battleUnits[unitId].As<BattleUnitState>()
                : null;
            if (unitState == null)
                continue;
            var attributeSnapshot = unitState.attribute_snapshot;
            units.Add(
                new Dictionary
                {
                    ["unit_id"] = unitState.unit_id.ToString(),
                    ["display_name"] = !string.IsNullOrEmpty(unitState.display_name)
                        ? unitState.display_name
                        : unitState.unit_id.ToString(),
                    ["coord"] = CoordToDict(unitState.coord),
                    ["faction_id"] = unitState.faction_id.ToString(),
                    ["control_mode"] = unitState.control_mode.ToString(),
                    ["is_alive"] = unitState.is_alive,
                    ["current_hp"] = unitState.current_hp,
                    ["current_mp"] = unitState.current_mp,
                    ["current_stamina"] = unitState.current_stamina,
                    ["stamina_max"] = attributeSnapshot?.get_value("stamina_max") ?? 0,
                    ["current_aura"] = unitState.current_aura,
                    ["aura_max"] = unitState.get_aura_max(),
                    ["current_shield_hp"] = unitState.current_shield_hp,
                    ["shield_max_hp"] = unitState.shield_max_hp,
                    ["shield_duration"] = unitState.shield_duration,
                    ["shield_family"] = unitState.shield_family.ToString(),
                    ["current_ap"] = unitState.current_ap,
                    ["current_move_points"] = unitState.current_move_points,
                }
            );
        }
        return new Dictionary
        {
            ["active"] = true,
            ["encounter_id"] = _runtime.get_active_battle_encounter_id().ToString(),
            ["encounter_name"] = _runtime.get_active_battle_encounter_name(),
            ["phase"] = battleState.phase.ToString(),
            ["active_unit_id"] = battleState.active_unit_id.ToString(),
            ["active_unit_name"] = _runtime.get_battle_active_unit_name(),
            ["modal_state"] = battleState.modal_state.ToString(),
            ["winner_faction_id"] = battleState.winner_faction_id.ToString(),
            ["selected_coord"] = CoordToDict(
                _runtime.get_battle_selected_coord()
            ),
            ["selected_skill_id"] = _runtime.get_selected_battle_skill_id().ToString(),
            ["selected_skill_variant_id"] = _runtime
                .get_selected_battle_skill_variant_id()
                .ToString(),
            ["selected_target_coords"] = CoordArrayToDictArray(
                _runtime.get_selected_battle_skill_target_coords()
            ),
            ["selected_target_unit_ids"] = StringNameArrayToStringArray(
                _runtime.get_selected_battle_skill_target_unit_ids()
            ),
            ["selected_target_unit_count"] = _runtime
                .get_selected_battle_skill_target_unit_ids()
                .Count,
            ["start_confirm_visible"] =
                _runtime.get_active_modal_id() == "battle_start_confirm",
            ["start_prompt"] = _runtime.get_pending_battle_start_prompt(),
            ["terrain_counts"] = _runtime.get_battle_terrain_counts(),
            ["calamity_by_member_id"] = calamitySnapshot,
            ["hud"] = hudSnapshot,
            ["report_entry_count"] = battleState.report_entries.Count,
            ["report_entries"] = battleState.report_entries.Duplicate(true),
            ["units"] = units,
        };
    }

    private Dictionary BuildRewardSnapshot()
    {
        var reward = _runtime.get_snapshot_reward();
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "reward",
            ["remaining_count"] = _runtime.get_pending_reward_count(),
            ["reward"] = reward != null ? reward.to_dict() : new Dictionary(),
        };
    }

    private Dictionary BuildLootSnapshot()
    {
        if (_runtime == null)
            return new Dictionary();
        var lootSnapshot = _runtime.get_last_battle_loot_snapshot().Duplicate(true);
        if (lootSnapshot.Count == 0)
            return new Dictionary();
        if (
            DictionaryInt(lootSnapshot, "loot_entry_count", 0) <= 0
            && DictionaryInt(lootSnapshot, "overflow_entry_count", 0) <= 0
        )
            return new Dictionary();
        return lootSnapshot;
    }

    private Dictionary BuildPromotionSnapshot()
    {
        var prompt = _runtime.get_current_promotion_prompt();
        return new Dictionary
        {
            ["visible"] = _runtime.get_active_modal_id() == "promotion",
            ["prompt"] = prompt.Duplicate(true),
        };
    }

    private Dictionary BuildLogSnapshot(int limit = 30)
    {
        return _runtime != null
            ? _runtime.get_log_snapshot(limit)
            : new Dictionary();
    }

    private Dictionary ResolveContractBoardWindowData()
    {
        var windowData = GetWindowDataFromRuntime("get_contract_board_window_data");
        if (windowData.Count > 0)
            return windowData;
        return GetWindowDataFromRuntime("get_active_contract_board_context");
    }

    private Dictionary ResolveForgeWindowData()
    {
        var windowData = GetWindowDataFromRuntime("get_forge_window_data");
        if (windowData.Count > 0)
            return windowData;
        var activeShopContext = GetWindowDataFromRuntime("get_active_shop_context");
        if (WindowDataMatchesPanelKind(activeShopContext, "forge"))
            return activeShopContext;
        var shopWindowData = GetWindowDataFromRuntime("get_shop_window_data");
        if (WindowDataMatchesPanelKind(shopWindowData, "forge"))
            return shopWindowData;
        return new Dictionary();
    }

    private Dictionary GetWindowDataFromRuntime(string methodName)
    {
        if (_runtime == null)
            return new Dictionary();
        return methodName switch
        {
            "get_contract_board_window_data" => _runtime
                .get_contract_board_window_data()
                .Duplicate(true),
            "get_active_contract_board_context" => _runtime
                .get_active_contract_board_context()
                .Duplicate(true),
            "get_forge_window_data" => _runtime.get_forge_window_data().Duplicate(true),
            "get_active_shop_context" => _runtime.get_active_shop_context().Duplicate(true),
            "get_shop_window_data" => _runtime.get_shop_window_data().Duplicate(true),
            _ => new Dictionary(),
        };
    }

    private static bool WindowDataMatchesPanelKind(Dictionary windowData, string panelKind)
    {
        if (windowData.Count == 0)
            return false;
        return DictionaryString(windowData, "panel_kind", "") == panelKind;
    }

    private static Dictionary CoordToDict(Vector2I coord)
    {
        return new Dictionary { ["x"] = coord.X, ["y"] = coord.Y };
    }

    private static Godot.Collections.Array CoordArrayToDictArray(Godot.Collections.Array coords)
    {
        var result = new Godot.Collections.Array();
        foreach (var coord in coords)
            result.Add(CoordToDict(coord.AsVector2I()));
        return result;
    }

    private static Godot.Collections.Array CoordArrayToDictArray(
        Godot.Collections.Array<Vector2I> coords
    )
    {
        var result = new Godot.Collections.Array();
        foreach (var coord in coords)
            result.Add(CoordToDict(coord));
        return result;
    }

    private static Godot.Collections.Array StringNameArrayToStringArray(
        Godot.Collections.Array values
    )
    {
        var result = new Godot.Collections.Array();
        foreach (var value in values)
            result.Add(value.AsString());
        return result;
    }

    private static Godot.Collections.Array StringNameArrayToStringArray(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new Godot.Collections.Array();
        foreach (var value in values)
            result.Add(value.ToString());
        return result;
    }

    private Godot.Collections.Array BuildNearbyEncounterEntries(int limit = 8)
    {
        return _runtime.get_nearby_encounter_entries(limit);
    }

    private Godot.Collections.Array BuildNearbyWorldEventEntries(int limit = 8)
    {
        return _runtime.get_nearby_world_event_entries(limit);
    }

    private static int DictionaryInt(Dictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsString();
    }

    private static Godot.Collections.Array DictionaryArray(
        Dictionary dictionary,
        string key,
        Godot.Collections.Array fallback
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsGodotArray();
    }

    private static Vector2I DictionaryVector2I(Dictionary dictionary, string key, Vector2I fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsVector2I();
    }

    private static IGameRuntimeSnapshotSource ResolveWeakRef(
        WeakReference<IGameRuntimeSnapshotSource> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimeSnapshotSource target)
            || (target is GodotObject godotTarget && !GodotObject.IsInstanceValid(godotTarget))
        )
            return null;
        return target;
    }
}
