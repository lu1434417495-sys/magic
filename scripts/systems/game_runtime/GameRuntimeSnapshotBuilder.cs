using System;
using System.Collections.Generic;
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

    internal void Dispose()
    {
        _runtime = null;
    }

    internal Dictionary BuildHeadlessSnapshot()
    {
        if (_runtime == null)
            return new Dictionary();
        return new Dictionary
        {
            ["status"] = new Dictionary
            {
                ["view"] = _runtime.IsBattleActive() ? "battle" : "world",
                ["text"] = _runtime.GetStatusText(),
            },
            ["modal"] = new Dictionary { ["id"] = _runtime.GetActiveModalId() },
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

    internal string BuildTextSnapshot()
    {
        return GameTextSnapshotRenderer.RenderWorldSnapshot(BuildHeadlessSnapshot());
    }

    private Dictionary BuildWorldSnapshot()
    {
        var selectedSettlement = _runtime.GetSelectedSettlement();
        var selectedNpc = _runtime.GetSelectedWorldNpc();
        var selectedEncounter = _runtime.GetSelectedEncounterAnchor();
        var selectedWorldEvent = _runtime.GetSelectedWorldEvent();
        return new Dictionary
        {
            ["map_id"] = _runtime.GetActiveMapId(),
            ["map_display_name"] = _runtime.GetActiveMapDisplayName(),
            ["is_submap"] = _runtime.IsSubmapActive(),
            ["world_step"] = _runtime.GetWorldStep(),
            ["player_coord"] = CoordToDict(_runtime.GetPlayerCoord()),
            ["player_visible_on_map"] = _runtime.IsPlayerVisibleOnWorldMap(),
            ["selected_coord"] = CoordToDict(_runtime.GetSelectedCoord()),
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
        var prompt = _runtime.GetPendingSubmapPrompt();
        return new Dictionary
        {
            ["active"] = _runtime.IsSubmapActive(),
            ["map_id"] = _runtime.GetActiveMapId(),
            ["map_display_name"] = _runtime.GetActiveMapDisplayName(),
            ["return_hint_text"] = _runtime.GetSubmapReturnHintText(),
            ["confirm_visible"] =
                _runtime.GetActiveModalKind() == RuntimeModalKind.SubmapConfirm,
            ["prompt"] = RuntimePayloadCopy.Dictionary(
                prompt,
                "GameRuntimeSnapshotBuilder.BuildSubmapSnapshot.prompt"
            ),
        };
    }

    private Dictionary BuildGameOverSnapshot()
    {
        var context = _runtime.GetGameOverContext();
        if (context.Count == 0)
            return new Dictionary();
        return RuntimePayloadCopy.Dictionary(
            context,
            "GameRuntimeSnapshotBuilder.BuildGameOverSnapshot"
        );
    }

    private Dictionary BuildPartySnapshot()
    {
        var members = new Godot.Collections.Array();
        PartyState partyState = _runtime.GetPartyState();
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
            ["selected_member_id"] = _runtime.GetPartySelectedMemberId().ToString(),
            ["pending_reward_count"] = _runtime.GetPendingRewardCount(),
            ["contingency_last_result"] = BuildContingencyLastResultSnapshot(),
            ["contingency_status_by_member"] = BuildContingencyStatusByMemberSnapshot(partyState),
            ["members"] = members,
            ["quests"] = BuildQuestSnapshot(partyState),
        };
    }

    private Dictionary BuildContingencyLastResultSnapshot()
    {
        ContingencySetupMutationResult result =
            _runtime.GetLastContingencyCommandResultTyped();
        if (result == null)
            return new Dictionary();
        return new Dictionary
        {
            ["ok"] = result.Ok,
            ["reason_id"] = result.Ok ? "ok" : result.ErrorCode,
            ["member_id"] = result.MemberId.ToString(),
            ["setup_id"] = result.SetupId.ToString(),
            ["charged"] = result.Charged,
            ["reserved_mp_max"] = result.ReservedMpMax,
            ["effective_mp_max"] = result.EffectiveMpMax,
            ["material_item_id"] = "special_contingency_gem",
            ["material_quantity"] = GetContingencyMaterialQuantity(result.MaterialCosts),
        };
    }

    private Dictionary BuildContingencyStatusByMemberSnapshot(PartyState partyState)
    {
        var result = new Dictionary();
        if (partyState == null)
            return result;
        foreach (PartyMemberState member in partyState.GetMemberStates())
        {
            if (member == null || member.member_id == "")
                continue;
            result[member.member_id.ToString()] = BuildContingencyMemberStatus(member);
        }
        return result;
    }

    private static Dictionary BuildContingencyMemberStatus(PartyMemberState member)
    {
        var setups = new Godot.Collections.Array();
        foreach (ContingencyMatrixSetupState setup in member.GetContingencySetupsTyped())
        {
            if (setup == null)
                continue;
            setups.Add(BuildContingencySetupSnapshot(setup));
        }
        return new Dictionary
        {
            ["member_id"] = member.member_id.ToString(),
            ["setup_count"] = setups.Count,
            ["setups"] = setups,
        };
    }

    private static Dictionary BuildContingencySetupSnapshot(ContingencyMatrixSetupState setup)
    {
        return new Dictionary
        {
            ["setup_id"] = setup.SetupId.ToString(),
            ["display_name"] = setup.DisplayName,
            ["charged"] = setup.Charged,
            ["reserved_mp_max"] = setup.ReservedMpMax,
            ["material_quantity"] = GetContingencyMaterialQuantity(setup.MaterialCosts),
            ["trigger"] = BuildContingencyTriggerSnapshot(setup.Trigger),
            ["release_mode"] = setup.ReleaseMode.ToString(),
            ["stored_spells"] = BuildContingencyStoredSpellSnapshots(setup.StoredSpells),
        };
    }

    private static Dictionary BuildContingencyTriggerSnapshot(ContingencyTriggerState trigger)
    {
        Dictionary payload = trigger?.ToDictionary() ?? new Dictionary();
        Dictionary result = new()
        {
            ["type"] = DictionaryString(payload, "type", ""),
        };
        if (payload.ContainsKey("percent"))
            result["percent"] = DictionaryInt(payload, "percent", 0);
        return result;
    }

    private static Godot.Collections.Array BuildContingencyStoredSpellSnapshots(
        IReadOnlyList<ContingencyStoredSpellEntryState> spells
    )
    {
        var result = new Godot.Collections.Array();
        foreach (ContingencyStoredSpellEntryState spell in spells ?? System.Array.Empty<ContingencyStoredSpellEntryState>())
        {
            if (spell == null)
                continue;
            result.Add(
                new Dictionary
                {
                    ["stored_skill_id"] = spell.StoredSkillId.ToString(),
                    ["cast_level"] = spell.CastLevel,
                    ["order"] = spell.Order,
                    ["target_resolver"] = new Dictionary
                    {
                        ["type"] = spell.TargetResolver?.Type.ToString() ?? "",
                    },
                }
            );
        }
        return result;
    }

    private static int GetContingencyMaterialQuantity(
        IReadOnlyList<ContingencyMaterialCostState> costs
    )
    {
        int total = 0;
        foreach (ContingencyMaterialCostState cost in costs ?? System.Array.Empty<ContingencyMaterialCostState>())
        {
            if (cost != null && cost.ItemId == "special_contingency_gem")
                total += cost.Quantity;
        }
        return total;
    }

    private Dictionary BuildQuestSnapshot(PartyState partyState)
    {
        if (partyState == null)
            return new Dictionary();
        var activeQuestEntries = BuildQuestEntries(partyState.GetActiveQuestsTyped(), "active");
        var claimableQuestEntries = BuildQuestEntries(
            partyState.GetClaimableQuestsTyped(),
            "claimable"
        );
        var completedQuestIds = StringNameArrayToStringArray(
            partyState.GetCompletedQuestIdsTyped()
        );
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
        IEnumerable<QuestState> questEntries,
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
        Dictionary questData =
            questState != null
                ? RuntimePayloadCopy.Dictionary(
                    questState.ToDictionary(),
                    "GameRuntimeSnapshotBuilder.NormalizeQuestEntry"
                )
                : null;
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
        questData["last_progress_context"] = RuntimePayloadCopy.Dictionary(
            contextValue.AsGodotDictionary(),
            "GameRuntimeSnapshotBuilder.NormalizeQuestEntry.last_progress_context"
        );
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
        return TryReadExactStringValue(questData[fieldName], out string value)
            ? value
            : "";
    }

    private static Dictionary NormalizeQuestProgressMap(Dictionary questData, string fieldName)
    {
        var progressValue = questData[fieldName];
        if (progressValue.VariantType != Variant.Type.Dictionary)
            return null;
        var result = new Dictionary();
        foreach (var objectiveIdValue in progressValue.AsGodotDictionary().Keys)
        {
            var objectiveId = TryReadExactStringValue(objectiveIdValue, out string objectiveKey)
                ? objectiveKey
                : "";
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
        PartyState partyState = _runtime.GetPartyState();
        PartyMemberState memberState =
            partyState != null ? partyState.GetMemberState(memberId) : null;
        var achievementSummary = _runtime
            .GetMemberAchievementSummary(memberId);
        var attributeSnapshot = _runtime.GetMemberAttributeSnapshot(memberId);
        var equipmentEntries = _runtime.GetMemberEquippedEntries(memberId);
        UnitProgress progression = memberState?.progression;
        return new Dictionary
        {
            ["member_id"] = memberId.ToString(),
            ["display_name"] = _runtime.GetMemberDisplayName(memberId),
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
                    ? progression.ActiveCoreSkillIdsTyped
                    : System.Array.Empty<StringName>()
            ),
            ["active_level_trigger_core_skill_id"] =
                progression != null ? progression.active_level_trigger_core_skill_id.ToString() : "",
            ["locked_level_trigger_skill_ids"] = BuildSortedStringNameArray(
                progression != null
                    ? progression.LockedLevelTriggerSkillIdsTyped
                    : System.Array.Empty<StringName>()
            ),
            ["blocked_relearn_skill_ids"] = BuildSortedStringNameArray(
                progression != null
                    ? progression.BlockedRelearnSkillIdsTyped
                    : System.Array.Empty<StringName>()
            ),
            ["skill_entries"] = BuildMemberSkillEntries(memberState),
            ["profession_entries"] = BuildMemberProfessionEntries(memberState),
            ["achievement_summary"] =
                achievementSummary.Count > 0
                    ? RuntimePayloadCopy.Dictionary(
                        achievementSummary,
                        "GameRuntimeSnapshotBuilder.BuildPartyMemberSnapshot.achievement_summary"
                    )
                    : new Dictionary(),
            ["attributes"] =
                attributeSnapshot != null ? attributeSnapshot.ToDictionary() : new Dictionary(),
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
        foreach (var skillId in progression.GetSortedSkillIdsTyped())
        {
            var skillProgress = progression.GetSkillProgress(skillId);
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
        foreach (var resourceId in progression.UnlockedCombatResourceIdsTyped)
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
        foreach (var skillId in progression.GetSortedSkillIdsTyped())
        {
            var skillProgress = progression.GetSkillProgress(skillId);
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
        foreach (var professionId in progression.GetSortedProfessionIdsTyped())
        {
            var professionProgress = progression.GetProfessionProgress(professionId);
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

    private Godot.Collections.Array BuildSortedStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        result.Sort();
        return result;
    }

    private Dictionary BuildSettlementSnapshot()
    {
        var settlementId = _runtime.GetResolvedSettlementId();
        var windowData = !string.IsNullOrEmpty(settlementId)
            ? _runtime.GetSettlementWindowData(settlementId)
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
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Settlement,
            ["settlement_id"] = settlementId,
            ["display_name"] = DictionaryString(windowData, "display_name", ""),
            ["tier_name"] = DictionaryString(windowData, "tier_name", ""),
            ["faction_id"] = DictionaryString(windowData, "faction_id", ""),
            ["services"] = services,
            ["feedback_text"] = _runtime.GetSettlementFeedbackText(),
        };
    }

    private Dictionary BuildShopSnapshot()
    {
        var windowData = _runtime.GetShopWindowData();
        if (WindowDataMatchesPanelKind(windowData, SettlementPanelKind.Forge))
            windowData.Clear();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Shop,
            ["window_data"] = RuntimePayloadCopy.Dictionary(
                windowData,
                "GameRuntimeSnapshotBuilder.BuildShopSnapshot.window_data"
            ),
        };
    }

    private Dictionary BuildContractBoardSnapshot()
    {
        var windowData = ResolveContractBoardWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.ContractBoard,
            ["window_data"] = RuntimePayloadCopy.Dictionary(
                windowData,
                "GameRuntimeSnapshotBuilder.BuildContractBoardSnapshot.window_data"
            ),
        };
    }

    private Dictionary BuildForgeSnapshot()
    {
        var windowData = ResolveForgeWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Forge,
            ["window_data"] = RuntimePayloadCopy.Dictionary(
                windowData,
                "GameRuntimeSnapshotBuilder.BuildForgeSnapshot.window_data"
            ),
        };
    }

    private Dictionary BuildStagecoachSnapshot()
    {
        var windowData = _runtime.GetStagecoachWindowData();
        windowData.Remove("party_state");
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Stagecoach,
            ["window_data"] = RuntimePayloadCopy.Dictionary(
                windowData,
                "GameRuntimeSnapshotBuilder.BuildStagecoachSnapshot.window_data"
            ),
        };
    }

    private Dictionary BuildCharacterInfoSnapshot()
    {
        var context = _runtime.GetCharacterInfoContext();
        context["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.CharacterInfo;
        if (context.ContainsKey("coord"))
            context["coord"] = CoordToDict(DictionaryVector2I(context, "coord", Vector2I.Zero));
        return context;
    }

    private Dictionary BuildWarehouseSnapshot()
    {
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Warehouse,
            ["entry_label"] = _runtime.GetActiveWarehouseEntryLabel(),
            ["window_data"] =
                _runtime.GetPartyState() != null
                    ? _runtime.GetWarehouseWindowData()
                    : new Dictionary(),
        };
    }

    private Dictionary BuildBattleSnapshot()
    {
        var battleState = _runtime.GetBattleState();
        if (battleState == null || battleState.IsEmpty())
            return new Dictionary { ["active"] = false };

        var battleRuntime = _runtime.GetBattleRuntime();
        var calamitySnapshot = new Dictionary();
        var contingencySnapshot = new Dictionary();
        if (battleRuntime != null)
        {
            calamitySnapshot = ProgressionDataUtils.string_name_int_map_to_string_dict(
                new System.Collections.Generic.Dictionary<StringName, int>(
                    battleRuntime.GetCalamityByMemberIdSnapshot()
                )
            );
            contingencySnapshot = battleRuntime.GetContingencySystemTyped()?.BuildSnapshot()
                ?? new Dictionary();
        }

        var adapter = new BattleHudAdapter();
        adapter.SetupRuntimeContext(_runtime as GameRuntimeFacade, _runtime.GetGameSession());
        var hudSnapshot = adapter.BuildSnapshot(
            battleState,
            _runtime.GetBattleSelectedCoord(),
            _runtime.GetSelectedBattleSkillId(),
            _runtime.GetSelectedBattleSkillName(),
            _runtime.GetSelectedBattleSkillVariantName(),
            _runtime.GetSelectedBattleSkillTargetCoords(),
            _runtime.GetSelectedBattleSkillRequiredCoordCount(),
            _runtime.GetSelectedBattleSkillTargetUnitIds(),
            _runtime.GetSelectedBattleSkillVariantId(),
            _runtime.GetActiveBattleEncounterName(),
            _runtime.GetSelectedBattleSkillPreview()
        );

        var units = new Godot.Collections.Array();
        foreach ((StringName _, BattleUnitState unitState) in battleState.UnitEntries(sorted: true))
        {
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
                    ["stamina_max"] = attributeSnapshot?.GetValue("stamina_max") ?? 0,
                    ["current_aura"] = unitState.current_aura,
                    ["aura_max"] = unitState.GetAuraMax(),
                    ["current_shield_hp"] = unitState.current_shield_hp,
                    ["shield_max_hp"] = unitState.shield_max_hp,
                    ["shield_duration"] = unitState.shield_duration,
                    ["shield_family"] = unitState.shield_family.ToString(),
                    ["current_ap"] = unitState.current_ap,
                    ["current_move_points"] = unitState.current_move_points,
                    ["has_pending_cast"] = unitState.HasPendingCast(),
                    ["pending_cast"] = BuildPendingCastSnapshot(unitState.pending_cast),
                }
            );
        }
        return new Dictionary
        {
            ["active"] = true,
            ["encounter_id"] = _runtime.GetActiveBattleEncounterId().ToString(),
            ["encounter_name"] = _runtime.GetActiveBattleEncounterName(),
            ["phase"] = battleState.phase.ToString(),
            ["active_unit_id"] = battleState.active_unit_id.ToString(),
            ["active_unit_name"] = _runtime.GetBattleActiveUnitName(),
            ["modal_state"] = battleState.modal_state.ToString(),
            ["winner_faction_id"] = battleState.winner_faction_id.ToString(),
            ["selected_coord"] = CoordToDict(
                _runtime.GetBattleSelectedCoord()
            ),
            ["selected_skill_id"] = _runtime.GetSelectedBattleSkillId().ToString(),
            ["selected_skill_variant_id"] = _runtime
                .GetSelectedBattleSkillVariantId()
                .ToString(),
            ["selected_target_coords"] = CoordArrayToDictArray(
                _runtime.GetSelectedBattleSkillTargetCoords()
            ),
            ["selected_target_unit_ids"] = StringNameArrayToStringArray(
                _runtime.GetSelectedBattleSkillTargetUnitIds()
            ),
            ["selected_target_unit_count"] = _runtime
                .GetSelectedBattleSkillTargetUnitIds()
                .Count,
            ["start_confirm_visible"] =
                _runtime.GetActiveModalKind() == RuntimeModalKind.BattleStartConfirm,
            ["start_prompt"] = _runtime.GetPendingBattleStartPrompt(),
            ["terrain_counts"] = _runtime.GetBattleTerrainCounts(),
            ["calamity_by_member_id"] = calamitySnapshot,
            ["contingency"] = contingencySnapshot,
            ["hud"] = hudSnapshot,
            ["report_entry_count"] = battleState.ReportEntryCount,
            ["report_entries"] = battleState.ProjectReportEntries(),
            ["units"] = units,
        };
    }

    private Dictionary BuildPendingCastSnapshot(BattlePendingCastState pendingCast)
    {
        if (pendingCast == null)
            return new Dictionary();
        int remainingProgress = Mathf.Max(pendingCast.RemainingCastProgress, 0);
        int remainingTu = (remainingProgress + 99) / 100;
        return new Dictionary
        {
            ["source_unit_id"] = pendingCast.SourceUnitId.ToString(),
            ["skill_id"] = pendingCast.SkillId.ToString(),
            ["variant_id"] = pendingCast.VariantId.ToString(),
            ["target_mode"] = BattleTypedNames.ToStringName(pendingCast.TargetMode).ToString(),
            ["binding_mode"] = BattleTypedNames.ToStringName(pendingCast.BindingMode).ToString(),
            ["started_coord"] = CoordToDict(pendingCast.StartedCoord),
            ["started_tu"] = pendingCast.StartedTu,
            ["base_casting_time_tu"] = pendingCast.BaseCastingTimeTu,
            ["remaining_cast_progress"] = remainingProgress,
            ["remaining_cast_tu"] = remainingTu,
            ["target_unit_ids"] = StringNameArrayToStringArray(pendingCast.TargetUnitIds),
            ["target_coords"] = CoordEnumerableToDictArray(pendingCast.TargetCoords),
        };
    }

    private Dictionary BuildRewardSnapshot()
    {
        var reward = _runtime.GetSnapshotReward();
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Reward,
            ["remaining_count"] = _runtime.GetPendingRewardCount(),
            ["reward"] = reward != null
                ? PendingCharacterRewardPayload.Project(reward)
                : new Dictionary(),
        };
    }

    private Dictionary BuildLootSnapshot()
    {
        if (_runtime == null)
            return new Dictionary();
        var lootSnapshot = RuntimePayloadCopy.Dictionary(
            _runtime.GetLastBattleLootSnapshot(),
            "GameRuntimeSnapshotBuilder.BuildLootSnapshot"
        );
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
        var prompt = _runtime.GetCurrentPromotionPrompt();
        return new Dictionary
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Promotion,
            ["prompt"] = RuntimePayloadCopy.Dictionary(
                prompt,
                "GameRuntimeSnapshotBuilder.BuildPromotionSnapshot.prompt"
            ),
        };
    }

    private Dictionary BuildLogSnapshot(int limit = 30)
    {
        return _runtime != null
            ? _runtime.GetLogSnapshot(limit)
            : new Dictionary();
    }

    private Dictionary ResolveContractBoardWindowData()
    {
        var windowData = GetWindowDataFromRuntime("GetContractBoardWindowData");
        if (windowData.Count > 0)
            return windowData;
        return GetWindowDataFromRuntime("GetActiveContractBoardContext");
    }

    private Dictionary ResolveForgeWindowData()
    {
        var windowData = GetWindowDataFromRuntime("GetForgeWindowData");
        if (windowData.Count > 0)
            return windowData;
        var activeShopContext = GetWindowDataFromRuntime("GetActiveShopContext");
        if (WindowDataMatchesPanelKind(activeShopContext, SettlementPanelKind.Forge))
            return activeShopContext;
        var shopWindowData = GetWindowDataFromRuntime("GetShopWindowData");
        if (WindowDataMatchesPanelKind(shopWindowData, SettlementPanelKind.Forge))
            return shopWindowData;
        return new Dictionary();
    }

    private Dictionary GetWindowDataFromRuntime(string methodName)
    {
        if (_runtime == null)
            return new Dictionary();
        return methodName switch
        {
            "GetContractBoardWindowData" => RuntimePayloadCopy.Dictionary(
                _runtime.GetContractBoardWindowData(),
                "GameRuntimeSnapshotBuilder.GetContractBoardWindowData"
            ),
            "GetActiveContractBoardContext" => RuntimePayloadCopy.Dictionary(
                _runtime.GetActiveContractBoardContext(),
                "GameRuntimeSnapshotBuilder.GetActiveContractBoardContext"
            ),
            "GetForgeWindowData" => RuntimePayloadCopy.Dictionary(
                _runtime.GetForgeWindowData(),
                "GameRuntimeSnapshotBuilder.GetForgeWindowData"
            ),
            "GetActiveShopContext" => RuntimePayloadCopy.Dictionary(
                _runtime.GetActiveShopContext(),
                "GameRuntimeSnapshotBuilder.GetActiveShopContext"
            ),
            "GetShopWindowData" => RuntimePayloadCopy.Dictionary(
                _runtime.GetShopWindowData(),
                "GameRuntimeSnapshotBuilder.GetShopWindowData"
            ),
            _ => new Dictionary(),
        };
    }

    private static bool WindowDataMatchesPanelKind(
        Dictionary windowData,
        SettlementPanelKind panelKind
    )
    {
        if (windowData.Count == 0)
            return false;
        return
            DictionaryString(windowData, "panel_kind", "")
            == SettlementPanelKinds.ToPayloadValue(panelKind);
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

    private static Godot.Collections.Array CoordEnumerableToDictArray(IEnumerable<Vector2I> coords)
    {
        var result = new Godot.Collections.Array();
        if (coords == null)
            return result;
        foreach (var coord in coords)
            result.Add(CoordToDict(coord));
        return result;
    }

    private static Godot.Collections.Array StringNameArrayToStringArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value.ToString());
        return result;
    }

    private Godot.Collections.Array BuildNearbyEncounterEntries(int limit = 8)
    {
        return _runtime.GetNearbyEncounterEntries(limit);
    }

    private Godot.Collections.Array BuildNearbyWorldEventEntries(int limit = 8)
    {
        return _runtime.GetNearbyWorldEventEntries(limit);
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
        return TryReadExactStringValue(dictionary[key], out string value) ? value : fallback;
    }

    private static bool TryReadExactStringValue(object rawValue, out string value)
    {
        switch (rawValue)
        {
            case string text:
                value = text.StripEdges();
                return true;
            case Variant variant when variant.VariantType == Variant.Type.String:
                value = variant.AsString().StripEdges();
                return true;
            default:
                value = "";
                return false;
        }
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
