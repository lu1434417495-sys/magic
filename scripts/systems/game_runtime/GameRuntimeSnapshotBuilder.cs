using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeSnapshotBuilder : RefCounted
{
    private static readonly string BattleHudAdapterPath = "res://scripts/systems/battle/presentation/battle_hud_adapter.gd";
    private static readonly string GameTextSnapshotRendererPath = "res://scripts/utils/game_text_snapshot_renderer.gd";

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

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public new void Dispose()
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
                ["view"] = _runtime.Call("is_battle_active").AsBool() ? "battle" : "world",
                ["text"] = _runtime.Call("get_status_text").AsString(),
            },
            ["modal"] = new Dictionary
            {
                ["id"] = _runtime.Call("get_active_modal_id").AsString(),
            },
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
        var renderer = GD.Load<GDScript>(GameTextSnapshotRendererPath);
        return renderer.Call("render_world_snapshot", BuildHeadlessSnapshot()).AsString();
    }

    private Dictionary BuildWorldSnapshot()
    {
        var selectedSettlement = _runtime.Call("get_selected_settlement").AsGodotDictionary();
        var selectedNpc = _runtime.Call("get_selected_world_npc").AsGodotDictionary();
        var selectedEncounter = _runtime.Call("get_selected_encounter_anchor").As<EncounterAnchorData>();
        var selectedWorldEvent = _runtime.Call("get_selected_world_event").AsGodotDictionary();
        return new Dictionary
        {
            ["map_id"] = _runtime.Call("get_active_map_id").AsString(),
            ["map_display_name"] = _runtime.Call("get_active_map_display_name").AsString(),
            ["is_submap"] = _runtime.Call("is_submap_active").AsBool(),
            ["world_step"] = _runtime.Call("get_world_step").AsInt32(),
            ["player_coord"] = CoordToDict(_runtime.Call("get_player_coord").AsVector2I()),
            ["player_visible_on_map"] = _runtime.Call("is_player_visible_on_world_map").AsBool(),
            ["selected_coord"] = CoordToDict(_runtime.Call("get_selected_coord").AsVector2I()),
            ["selected_settlement_id"] = DictionaryGet(selectedSettlement, "settlement_id", "").AsString(),
            ["selected_npc_name"] = DictionaryGet(selectedNpc, "display_name", "").AsString(),
            ["selected_world_event_id"] = DictionaryGet(selectedWorldEvent, "event_id", "").AsString(),
            ["selected_world_event_name"] = DictionaryGet(selectedWorldEvent, "display_name", "").AsString(),
            ["selected_encounter_id"] = selectedEncounter != null ? selectedEncounter.entity_id : "",
            ["selected_encounter_name"] = selectedEncounter != null ? selectedEncounter.display_name : "",
            ["selected_encounter_kind"] = selectedEncounter != null ? selectedEncounter.encounter_kind : "",
            ["selected_encounter_growth_stage"] = selectedEncounter != null ? selectedEncounter.growth_stage : 0,
            ["nearby_world_events"] = BuildNearbyWorldEventEntries(),
            ["nearby_encounters"] = BuildNearbyEncounterEntries(),
        };
    }

    private Dictionary BuildSubmapSnapshot()
    {
        var prompt = _runtime.Call("get_pending_submap_prompt").AsGodotDictionary();
        return new Dictionary
        {
            ["active"] = _runtime.Call("is_submap_active").AsBool(),
            ["map_id"] = _runtime.Call("get_active_map_id").AsString(),
            ["map_display_name"] = _runtime.Call("get_active_map_display_name").AsString(),
            ["return_hint_text"] = _runtime.Call("get_submap_return_hint_text").AsString(),
            ["confirm_visible"] = _runtime.Call("get_active_modal_id").AsString() == "submap_confirm",
            ["prompt"] = prompt.Duplicate(true),
        };
    }

    private Dictionary BuildGameOverSnapshot()
    {
        var context = _runtime.Call("get_game_over_context").AsGodotDictionary();
        if (context.Count == 0)
            return new Dictionary();
        return context.Duplicate(true);
    }

    private Dictionary BuildPartySnapshot()
    {
        var members = new Godot.Collections.Array();
        var partyState = _runtime.Call("get_party_state").AsGodotObject();
        if (partyState != null)
        {
            foreach (var memberId in partyState.Get("active_member_ids").AsGodotArray())
                members.Add(BuildPartyMemberSnapshot(memberId.AsStringName(), "active"));
            foreach (var memberId in partyState.Get("reserve_member_ids").AsGodotArray())
                members.Add(BuildPartyMemberSnapshot(memberId.AsStringName(), "reserve"));
        }
        return new Dictionary
        {
            ["gold"] = partyState != null ? partyState.Get("gold").AsInt32() : 0,
            ["leader_member_id"] = partyState != null ? partyState.Get("leader_member_id").AsString() : "",
            ["active_member_ids"] = partyState != null ? StringNameArrayToStringArray(partyState.Get("active_member_ids").AsGodotArray()) : new Godot.Collections.Array(),
            ["reserve_member_ids"] = partyState != null ? StringNameArrayToStringArray(partyState.Get("reserve_member_ids").AsGodotArray()) : new Godot.Collections.Array(),
            ["selected_member_id"] = _runtime.Call("get_party_selected_member_id").AsString(),
            ["pending_reward_count"] = _runtime.Call("get_pending_reward_count").AsInt32(),
            ["members"] = members,
            ["quests"] = BuildQuestSnapshot(partyState),
        };
    }

    private Dictionary BuildQuestSnapshot(GodotObject partyState)
    {
        if (partyState == null)
            return new Dictionary();
        var activeQuestsVariant = GetPartyStateQuestValue(partyState, "active_quests", "get_active_quests");
        var claimableQuestsVariant = GetPartyStateQuestValue(partyState, "claimable_quests", "get_claimable_quests");
        var completedQuestIdsVariant = GetPartyStateQuestValue(partyState, "completed_quest_ids", "get_completed_quest_ids");
        if (activeQuestsVariant.VariantType == Variant.Type.Nil && claimableQuestsVariant.VariantType == Variant.Type.Nil && completedQuestIdsVariant.VariantType == Variant.Type.Nil)
            return new Dictionary();
        var activeQuestEntries = BuildQuestEntries(activeQuestsVariant, "active");
        var claimableQuestEntries = BuildQuestEntries(claimableQuestsVariant, "claimable");
        var activeQuestIds = BuildQuestIds(activeQuestEntries);
        var claimableQuestIds = BuildQuestIds(claimableQuestEntries);
        var completedQuestIds = completedQuestIdsVariant.VariantType == Variant.Type.Array
            ? StringNameArrayToStringArray((Godot.Collections.Array)ProgressionDataUtils.to_string_name_array(completedQuestIdsVariant.AsGodotArray()))
            : new Godot.Collections.Array();
        return new Dictionary
        {
            ["active_quest_ids"] = activeQuestIds,
            ["claimable_quest_ids"] = claimableQuestIds,
            ["completed_quest_ids"] = completedQuestIds,
            ["active_quests"] = activeQuestEntries,
            ["claimable_quests"] = claimableQuestEntries,
        };
    }

    private Variant GetPartyStateQuestValue(GodotObject partyState, string propertyName, string getterName)
    {
        if (partyState == null)
            return default(Variant);
        if (partyState.HasMethod(getterName))
            return partyState.Call(getterName);
        return default(Variant);
    }

    private Godot.Collections.Array BuildQuestEntries(Variant questEntriesVariant, string stageId)
    {
        var entries = new Godot.Collections.Array();
        if (questEntriesVariant.VariantType != Variant.Type.Array)
            return entries;
        foreach (var questVariant in questEntriesVariant.AsGodotArray())
        {
            var questEntry = NormalizeQuestEntry(questVariant, stageId);
            if (questEntry.Count > 0)
                entries.Add(questEntry);
        }
        var sortedList = new System.Collections.Generic.List<Dictionary>();
        foreach (var entry in entries)
            sortedList.Add(entry.AsGodotDictionary());
        sortedList.Sort((a, b) =>
        {
            var aId = DictionaryGet(a, "quest_id", "").AsString();
            var bId = DictionaryGet(b, "quest_id", "").AsString();
            return string.Compare(aId, bId, System.StringComparison.Ordinal);
        });
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
            var questId = DictionaryGet(entry.AsGodotDictionary(), "quest_id", "").AsString();
            if (!string.IsNullOrEmpty(questId))
                questIds.Add(questId);
        }
        return questIds;
    }

    private Dictionary NormalizeQuestEntry(Variant questVariant, string stageId)
    {
        Dictionary questData = null;
        if (questVariant.VariantType == Variant.Type.Dictionary)
            questData = questVariant.AsGodotDictionary().Duplicate(true);
        else if (questVariant.VariantType == Variant.Type.Object && questVariant.AsGodotObject() != null && questVariant.AsGodotObject().HasMethod("to_dict"))
        {
            var dictVariant = questVariant.AsGodotObject().Call("to_dict");
            if (dictVariant.VariantType == Variant.Type.Dictionary)
                questData = dictVariant.AsGodotDictionary().Duplicate(true);
        }
        if (questData == null || questData.Count == 0)
            return new Dictionary();
        if (!HasExactQuestEntryFields(questData))
            return new Dictionary();
        var questId = ReadQuestString(questData["quest_id"]);
        var statusId = ReadQuestString(questData["status_id"]);
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(statusId))
            return new Dictionary();
        if (!IsValidQuestStep(questData["accepted_at_world_step"]) || !IsValidQuestStep(questData["completed_at_world_step"]) || !IsValidQuestStep(questData["reward_claimed_at_world_step"]))
            return new Dictionary();
        var objectiveProgress = NormalizeQuestProgressMap(questData["objective_progress"]);
        if (objectiveProgress == null)
            return new Dictionary();
        var contextVariant = questData["last_progress_context"];
        if (contextVariant.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        questData["quest_id"] = questId;
        questData["stage_id"] = stageId;
        questData["status_id"] = statusId;
        questData["objective_progress"] = objectiveProgress;
        questData["last_progress_context"] = contextVariant.AsGodotDictionary().Duplicate(true);
        return questData;
    }

    private static bool IsValidQuestStep(Variant value)
    {
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

    private static string ReadQuestString(Variant value)
    {
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            return "";
        return value.AsString().StripEdges();
    }

    private static Dictionary NormalizeQuestProgressMap(Variant progressVariant)
    {
        if (progressVariant.VariantType != Variant.Type.Dictionary)
            return null;
        var result = new Dictionary();
        foreach (var objectiveIdVariant in progressVariant.AsGodotDictionary().Keys)
        {
            var objectiveId = ReadQuestString(objectiveIdVariant);
            if (string.IsNullOrEmpty(objectiveId))
                return null;
            var progressValue = progressVariant.AsGodotDictionary()[objectiveIdVariant];
            if (progressValue.VariantType != Variant.Type.Int || progressValue.AsInt32() < 0)
                return null;
            result[objectiveId] = progressValue.AsInt32();
        }
        return result;
    }

    private static bool CompareQuestEntries(Variant a, Variant b)
    {
        var aId = DictionaryGet(a.AsGodotDictionary(), "quest_id", "").AsString();
        var bId = DictionaryGet(b.AsGodotDictionary(), "quest_id", "").AsString();
        return string.Compare(aId, bId, StringComparison.Ordinal) < 0;
    }

    private Dictionary BuildPartyMemberSnapshot(StringName memberId, string rosterRole)
    {
        var partyState = _runtime.Call("get_party_state").AsGodotObject();
        var memberState = partyState != null ? partyState.Call("get_member_state", memberId).AsGodotObject() : null;
        var achievementSummary = _runtime.Call("get_member_achievement_summary", memberId).AsGodotDictionary();
        var attributeSnapshot = _runtime.Call("get_member_attribute_snapshot", memberId).AsGodotObject();
        var equipmentEntries = _runtime.Call("get_member_equipped_entries", memberId).AsGodotArray();
        var progression = memberState != null ? memberState.Get("progression").AsGodotObject() : null;
        return new Dictionary
        {
            ["member_id"] = memberId.ToString(),
            ["display_name"] = _runtime.Call("get_member_display_name", memberId).AsString(),
            ["roster_role"] = rosterRole,
            ["is_leader"] = partyState != null && partyState.Get("leader_member_id").AsStringName() == memberId,
            ["current_hp"] = memberState != null ? memberState.Get("current_hp").AsInt32() : 0,
            ["current_mp"] = memberState != null ? memberState.Get("current_mp").AsInt32() : 0,
            ["current_aura"] = memberState != null ? memberState.Get("current_aura").AsInt32() : 0,
            ["unlocked_combat_resource_ids"] = BuildMemberUnlockedCombatResourceIds(memberState),
            ["learned_skill_ids"] = BuildMemberLearnedSkillIds(memberState),
            ["active_core_skill_ids"] = BuildSortedStringNameArray(progression != null ? progression.Get("active_core_skill_ids").AsGodotArray() : new Godot.Collections.Array()),
            ["active_level_trigger_core_skill_id"] = progression != null ? progression.Get("active_level_trigger_core_skill_id").AsString() : "",
            ["locked_level_trigger_skill_ids"] = BuildSortedStringNameArray(progression != null ? progression.Get("locked_level_trigger_skill_ids").AsGodotArray() : new Godot.Collections.Array()),
            ["blocked_relearn_skill_ids"] = BuildSortedStringNameArray(progression != null ? progression.Get("blocked_relearn_skill_ids").AsGodotArray() : new Godot.Collections.Array()),
            ["skill_entries"] = BuildMemberSkillEntries(memberState),
            ["profession_entries"] = BuildMemberProfessionEntries(memberState),
            ["achievement_summary"] = achievementSummary.Count > 0 ? achievementSummary.Duplicate(true) : new Dictionary(),
            ["attributes"] = attributeSnapshot != null && attributeSnapshot.HasMethod("to_dict") ? attributeSnapshot.Call("to_dict").AsGodotDictionary() : new Dictionary(),
            ["equipment"] = equipmentEntries,
            ["equipment_count"] = equipmentEntries.Count,
        };
    }

    private Godot.Collections.Array BuildMemberLearnedSkillIds(GodotObject memberState)
    {
        var learnedSkillIds = new Godot.Collections.Array();
        if (memberState == null)
            return learnedSkillIds;
        var progression = memberState.Get("progression").AsGodotObject();
        if (progression == null)
            return learnedSkillIds;
        var skills = progression.Get("skills").AsGodotDictionary();
        foreach (var skillKey in skills.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            var skillProgress = progression.Call("get_skill_progress", skillId).AsGodotObject();
            if (skillProgress == null || !skillProgress.Get("is_learned").AsBool())
                continue;
            learnedSkillIds.Add(skillId.ToString());
        }
        learnedSkillIds.Sort();
        return learnedSkillIds;
    }

    private Godot.Collections.Array BuildMemberUnlockedCombatResourceIds(GodotObject memberState)
    {
        var resourceIds = new Godot.Collections.Array();
        if (memberState == null)
            return resourceIds;
        var progression = memberState.Get("progression").AsGodotObject();
        if (progression == null)
            return resourceIds;
        foreach (var resourceId in progression.Get("unlocked_combat_resource_ids").AsGodotArray())
            resourceIds.Add(resourceId.AsString());
        resourceIds.Sort();
        return resourceIds;
    }

    private Godot.Collections.Array BuildMemberSkillEntries(GodotObject memberState)
    {
        var entries = new Godot.Collections.Array();
        if (memberState == null)
            return entries;
        var progression = memberState.Get("progression").AsGodotObject();
        if (progression == null)
            return entries;
        var skills = progression.Get("skills").AsGodotDictionary();
        foreach (var skillKey in ProgressionDataUtils.sorted_string_keys(skills))
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            var skillProgress = progression.Call("get_skill_progress", skillId).AsGodotObject();
            if (skillProgress == null || !skillProgress.Get("is_learned").AsBool())
                continue;
            entries.Add(new Dictionary
            {
                ["skill_id"] = skillId.ToString(),
                ["level"] = skillProgress.Get("skill_level").AsInt32(),
                ["is_core"] = skillProgress.Get("is_core").AsBool(),
                ["assigned_profession_id"] = skillProgress.Get("assigned_profession_id").AsString(),
                ["is_level_trigger_active"] = skillProgress.Get("is_level_trigger_active").AsBool(),
                ["is_level_trigger_locked"] = skillProgress.Get("is_level_trigger_locked").AsBool(),
                ["core_max_growth_claimed"] = skillProgress.Get("core_max_growth_claimed").AsBool(),
                ["granted_source_type"] = skillProgress.Get("granted_source_type").AsString(),
                ["granted_source_id"] = skillProgress.Get("granted_source_id").AsString(),
            });
        }
        return entries;
    }

    private Godot.Collections.Array BuildMemberProfessionEntries(GodotObject memberState)
    {
        var entries = new Godot.Collections.Array();
        if (memberState == null)
            return entries;
        var progression = memberState.Get("progression").AsGodotObject();
        if (progression == null)
            return entries;
        var professions = progression.Get("professions").AsGodotDictionary();
        foreach (var professionKey in ProgressionDataUtils.sorted_string_keys(professions))
        {
            var professionId = ProgressionDataUtils.to_string_name(professionKey);
            var professionProgress = progression.Call("get_profession_progress", professionId).AsGodotObject();
            if (professionProgress == null)
                continue;
            entries.Add(new Dictionary
            {
                ["profession_id"] = professionId.ToString(),
                ["rank"] = professionProgress.Get("rank").AsInt32(),
                ["is_active"] = professionProgress.Get("is_active").AsBool(),
                ["is_hidden"] = professionProgress.Get("is_hidden").AsBool(),
                ["core_skill_ids"] = BuildSortedStringNameArray(professionProgress.Get("core_skill_ids").AsGodotArray()),
                ["granted_skill_ids"] = BuildSortedStringNameArray(professionProgress.Get("granted_skill_ids").AsGodotArray()),
                ["inactive_reason"] = professionProgress.Get("inactive_reason").AsString(),
            });
        }
        return entries;
    }

    private Godot.Collections.Array BuildSortedStringNameArray(Godot.Collections.Array values)
    {
        var result = StringNameArrayToStringArray(values);
        result.Sort();
        return result;
    }

    private Dictionary BuildSettlementSnapshot()
    {
        var settlementId = _runtime.Call("get_resolved_settlement_id").AsString();
        var windowData = !string.IsNullOrEmpty(settlementId) ? _runtime.Call("get_settlement_window_data", settlementId).AsGodotDictionary() : new Dictionary();
        var services = new Godot.Collections.Array();
        foreach (var serviceVariant in DictionaryGet(windowData, "available_services", new Godot.Collections.Array()).AsGodotArray())
        {
            if (serviceVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var serviceData = serviceVariant.AsGodotDictionary();
            services.Add(new Dictionary
            {
                ["action_id"] = DictionaryGet(serviceData, "action_id", "").AsString(),
                ["facility_name"] = DictionaryGet(serviceData, "facility_name", "").AsString(),
                ["npc_name"] = DictionaryGet(serviceData, "npc_name", "").AsString(),
                ["service_type"] = DictionaryGet(serviceData, "service_type", "").AsString(),
                ["interaction_script_id"] = DictionaryGet(serviceData, "interaction_script_id", "").AsString(),
            });
        }
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "settlement",
            ["settlement_id"] = settlementId,
            ["display_name"] = DictionaryGet(windowData, "display_name", "").AsString(),
            ["tier_name"] = DictionaryGet(windowData, "tier_name", "").AsString(),
            ["faction_id"] = DictionaryGet(windowData, "faction_id", "").AsString(),
            ["services"] = services,
            ["feedback_text"] = _runtime.Call("get_settlement_feedback_text").AsString(),
        };
    }

    private Dictionary BuildShopSnapshot()
    {
        var windowData = _runtime.Call("get_shop_window_data").AsGodotDictionary();
        if (WindowDataMatchesPanelKind(windowData, "forge"))
            windowData.Clear();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "shop",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildContractBoardSnapshot()
    {
        var windowData = ResolveContractBoardWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "contract_board",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildForgeSnapshot()
    {
        var windowData = ResolveForgeWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "forge",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildStagecoachSnapshot()
    {
        var windowData = _runtime.Call("get_stagecoach_window_data").AsGodotDictionary();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "stagecoach",
            ["window_data"] = windowData.Duplicate(true),
        };
    }

    private Dictionary BuildCharacterInfoSnapshot()
    {
        var context = _runtime.Call("get_character_info_context").AsGodotDictionary();
        context["visible"] = _runtime.Call("get_active_modal_id").AsString() == "character_info";
        if (context.ContainsKey("coord"))
            context["coord"] = CoordToDict(DictionaryGet(context, "coord", Vector2I.Zero).AsVector2I());
        return context;
    }

    private Dictionary BuildWarehouseSnapshot()
    {
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "warehouse",
            ["entry_label"] = _runtime.Call("get_active_warehouse_entry_label").AsString(),
            ["window_data"] = _runtime.Call("get_party_state").AsGodotObject() != null ? _runtime.Call("get_warehouse_window_data").AsGodotDictionary() : new Dictionary(),
        };
    }

    private Dictionary BuildBattleSnapshot()
    {
        var battleState = _runtime.Call("get_battle_state").AsGodotObject();
        if (battleState == null || (battleState.HasMethod("is_empty") && battleState.Call("is_empty").AsBool()))
            return new Dictionary { ["active"] = false };

        var battleRuntime = _runtime.HasMethod("get_battle_runtime") ? _runtime.Call("get_battle_runtime").AsGodotObject() : null;
        var calamitySnapshot = new Dictionary();
        if (battleRuntime != null && battleRuntime.HasMethod("get_calamity_by_member_id"))
            calamitySnapshot = ProgressionDataUtils.string_name_int_map_to_string_dict(battleRuntime.Call("get_calamity_by_member_id").AsGodotDictionary());

        var adapter = GD.Load<GDScript>(BattleHudAdapterPath).New().AsGodotObject();
        adapter.Call("set_party_member_state_resolver", new Callable(this, nameof(GetPartyMemberStateForSnapshot)));
        adapter.Call("set_content_def_providers",
            _runtime.HasMethod("get_skill_defs") ? new Callable(_runtime, "get_skill_defs") : new Callable(),
            _runtime.HasMethod("get_item_defs") ? new Callable(_runtime, "get_item_defs") : new Callable()
        );
        var hudSnapshot = adapter.Call("build_snapshot",
            battleState,
            _runtime.Call("get_battle_selected_coord"),
            _runtime.Call("get_selected_battle_skill_id"),
            _runtime.Call("get_selected_battle_skill_name"),
            _runtime.Call("get_selected_battle_skill_variant_name"),
            _runtime.Call("get_selected_battle_skill_target_coords"),
            _runtime.Call("get_selected_battle_skill_required_coord_count"),
            _runtime.Call("get_selected_battle_skill_target_unit_ids"),
            _runtime.Call("get_selected_battle_skill_variant_id"),
            _runtime.HasMethod("preview_battle_command") ? new Callable(_runtime, "preview_battle_command") : new Callable(),
            _runtime.Call("get_active_battle_encounter_name")
        ).AsGodotDictionary();

        var units = new Godot.Collections.Array();
        var battleUnits = battleState.Get("units").AsGodotDictionary();
        foreach (var unitIdStr in ProgressionDataUtils.sorted_string_keys(battleUnits))
        {
            var unitId = new StringName(unitIdStr);
            var unitState = battleUnits.ContainsKey(unitId) ? battleUnits[unitId].As<BattleUnitState>() : null;
            if (unitState == null)
                continue;
            var attributeSnapshot = unitState.Get("attribute_snapshot").AsGodotObject();
            units.Add(new Dictionary
            {
                ["unit_id"] = unitState.unit_id.ToString(),
                ["display_name"] = !string.IsNullOrEmpty(unitState.display_name) ? unitState.display_name : unitState.unit_id.ToString(),
                ["coord"] = CoordToDict(unitState.coord),
                ["faction_id"] = unitState.faction_id.ToString(),
                ["control_mode"] = unitState.control_mode.ToString(),
                ["is_alive"] = unitState.is_alive,
                ["current_hp"] = unitState.current_hp,
                ["current_mp"] = unitState.current_mp,
                ["current_stamina"] = unitState.current_stamina,
                ["stamina_max"] = attributeSnapshot != null ? attributeSnapshot.Call("get_value", "stamina_max").AsInt32() : 0,
                ["current_aura"] = unitState.current_aura,
                ["aura_max"] = unitState.Get("aura_max").AsInt32(),
                ["current_shield_hp"] = unitState.current_shield_hp,
                ["shield_max_hp"] = unitState.shield_max_hp,
                ["shield_duration"] = unitState.shield_duration,
                ["shield_family"] = unitState.shield_family.ToString(),
                ["current_ap"] = unitState.current_ap,
                ["current_move_points"] = unitState.current_move_points,
            });
        }
        return new Dictionary
        {
            ["active"] = true,
            ["encounter_id"] = _runtime.Call("get_active_battle_encounter_id").AsString(),
            ["encounter_name"] = _runtime.Call("get_active_battle_encounter_name").AsString(),
            ["phase"] = battleState.Get("phase").AsString(),
            ["active_unit_id"] = battleState.Get("active_unit_id").AsString(),
            ["active_unit_name"] = _runtime.Call("get_battle_active_unit_name").AsString(),
            ["modal_state"] = battleState.Get("modal_state").AsString(),
            ["winner_faction_id"] = battleState.Get("winner_faction_id").AsString(),
            ["selected_coord"] = CoordToDict(_runtime.Call("get_battle_selected_coord").AsVector2I()),
            ["selected_skill_id"] = _runtime.Call("get_selected_battle_skill_id").AsString(),
            ["selected_skill_variant_id"] = _runtime.Call("get_selected_battle_skill_variant_id").AsString(),
            ["selected_target_coords"] = CoordArrayToDictArray(_runtime.Call("get_selected_battle_skill_target_coords").AsGodotArray()),
            ["selected_target_unit_ids"] = StringNameArrayToStringArray(_runtime.Call("get_selected_battle_skill_target_unit_ids").AsGodotArray()),
            ["selected_target_unit_count"] = _runtime.Call("get_selected_battle_skill_target_unit_ids").AsGodotArray().Count,
            ["start_confirm_visible"] = _runtime.Call("get_active_modal_id").AsString() == "battle_start_confirm",
            ["start_prompt"] = _runtime.Call("get_pending_battle_start_prompt").AsGodotDictionary(),
            ["terrain_counts"] = _runtime.Call("get_battle_terrain_counts").AsGodotDictionary(),
            ["calamity_by_member_id"] = calamitySnapshot,
            ["hud"] = hudSnapshot,
            ["report_entry_count"] = battleState.Get("report_entries").AsGodotArray().Count,
            ["report_entries"] = battleState.Get("report_entries").AsGodotArray().Duplicate(true),
            ["units"] = units,
        };
    }

    private GodotObject GetPartyMemberStateForSnapshot(StringName memberId)
    {
        if (_runtime == null || memberId == "")
            return null;
        var partyState = _runtime.HasMethod("get_party_state") ? _runtime.Call("get_party_state").AsGodotObject() : null;
        if (partyState == null || !partyState.HasMethod("get_member_state"))
            return null;
        return partyState.Call("get_member_state", memberId).AsGodotObject();
    }

    private Dictionary BuildRewardSnapshot()
    {
        var reward = _runtime.Call("get_snapshot_reward").AsGodotObject();
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "reward",
            ["remaining_count"] = _runtime.Call("get_pending_reward_count").AsInt32(),
            ["reward"] = reward != null && reward.HasMethod("to_dict") ? reward.Call("to_dict").AsGodotDictionary() : new Dictionary(),
        };
    }

    private Dictionary BuildLootSnapshot()
    {
        if (_runtime == null)
            return new Dictionary();
        var lootSnapshotVariant = _runtime.Call("get_last_battle_loot_snapshot");
        if (lootSnapshotVariant.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        var lootSnapshot = lootSnapshotVariant.AsGodotDictionary().Duplicate(true);
        if (lootSnapshot.Count == 0)
            return new Dictionary();
        if (DictionaryGet(lootSnapshot, "loot_entry_count", 0).AsInt32() <= 0 && DictionaryGet(lootSnapshot, "overflow_entry_count", 0).AsInt32() <= 0)
            return new Dictionary();
        return lootSnapshot;
    }

    private Dictionary BuildPromotionSnapshot()
    {
        var prompt = _runtime.Call("get_current_promotion_prompt").AsGodotDictionary();
        return new Dictionary
        {
            ["visible"] = _runtime.Call("get_active_modal_id").AsString() == "promotion",
            ["prompt"] = prompt.Duplicate(true),
        };
    }

    private Dictionary BuildLogSnapshot(int limit = 30)
    {
        return _runtime != null ? _runtime.Call("get_log_snapshot", limit).AsGodotDictionary() : new Dictionary();
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
        var windowDataVariant = _runtime.Call(methodName);
        return windowDataVariant.VariantType == Variant.Type.Dictionary ? windowDataVariant.AsGodotDictionary().Duplicate(true) : new Dictionary();
    }

    private static bool WindowDataMatchesPanelKind(Dictionary windowData, string panelKind)
    {
        if (windowData.Count == 0)
            return false;
        return DictionaryGet(windowData, "panel_kind", "").AsString() == panelKind;
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

    private static Godot.Collections.Array StringNameArrayToStringArray(Godot.Collections.Array values)
    {
        var result = new Godot.Collections.Array();
        foreach (var value in values)
            result.Add(value.AsString());
        return result;
    }

    private Godot.Collections.Array BuildNearbyEncounterEntries(int limit = 8)
    {
        return _runtime.Call("get_nearby_encounter_entries", limit).AsGodotArray();
    }

    private Godot.Collections.Array BuildNearbyWorldEventEntries(int limit = 8)
    {
        return _runtime.Call("get_nearby_world_event_entries", limit).AsGodotArray();
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }
}
