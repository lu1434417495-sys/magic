using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using PlainDictionary = System.Collections.Generic.Dictionary<string, object>;
using PlainList = System.Collections.Generic.List<object>;

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
        "failed_at_world_step",
        "failure_reason_id",
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

    internal IReadOnlyDictionary<string, object> BuildHeadlessSnapshotPlain()
    {
        if (_runtime == null)
            return new PlainDictionary(StringComparer.Ordinal);
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["status"] = new PlainDictionary(StringComparer.Ordinal)
            {
                ["view"] = _runtime.IsBattleActive() ? "battle" : "world",
                ["text"] = _runtime.GetStatusText(),
            },
            ["modal"] = new PlainDictionary(StringComparer.Ordinal)
            {
                ["id"] = _runtime.GetActiveModalId(),
            },
            ["logs"] = BuildLogSnapshot(),
            ["world"] = BuildWorldSnapshot(),
            ["submap"] = BuildSubmapSnapshot(),
            ["game_over"] = BuildGameOverSnapshot(),
            ["party"] = BuildPartySnapshot(),
            ["settlement"] = BuildSettlementSnapshot(),
            ["contract_board"] = BuildContractBoardSnapshot(),
            ["bounty_board"] = BuildBountyBoardSnapshot(),
            ["npc_quest_offer"] = BuildNpcQuestOfferSnapshot(),
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

    internal GodotProjectionLease<GDictionary> BuildHeadlessSnapshotLease()
    {
        return RuntimePlainPayload.ProjectDictionaryLease(
            BuildHeadlessSnapshotPlain(),
            "game-runtime-headless-snapshot",
            LifetimeDomain.Request,
            "GameRuntimeSnapshotBuilder.root"
        );
    }

    internal string BuildTextSnapshot()
    {
        return GameTextSnapshotRenderer.RenderWorldSnapshot(BuildHeadlessSnapshotPlain());
    }

    private PlainDictionary BuildWorldSnapshot()
    {
        WorldMapSettlementData selectedSettlement = _runtime.GetSelectedSettlementData();
        WorldMapNpcData selectedNpc = _runtime.GetSelectedWorldNpcData();
        var selectedEncounter = _runtime.GetSelectedEncounterAnchor();
        WorldMapEventData selectedWorldEvent = _runtime.GetSelectedWorldEventData();
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["map_id"] = _runtime.GetActiveMapId(),
            ["map_display_name"] = _runtime.GetActiveMapDisplayName(),
            ["is_submap"] = _runtime.IsSubmapActive(),
            ["world_step"] = _runtime.GetWorldStep(),
            ["player_coord"] = CoordToDict(_runtime.GetPlayerCoord()),
            ["player_visible_on_map"] = _runtime.IsPlayerVisibleOnWorldMap(),
            ["selected_coord"] = CoordToDict(_runtime.GetSelectedCoord()),
            ["selected_settlement_id"] =
                selectedSettlement != null && !selectedSettlement.IsEmpty
                    ? selectedSettlement.SettlementId
                    : "",
            ["selected_npc_name"] =
                selectedNpc != null && !selectedNpc.IsEmpty ? selectedNpc.DisplayName : "",
            ["selected_world_event_id"] =
                selectedWorldEvent != null ? selectedWorldEvent.EventId.ToString() : "",
            ["selected_world_event_name"] =
                selectedWorldEvent != null ? selectedWorldEvent.DisplayName : "",
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

    private PlainDictionary BuildSubmapSnapshot()
    {
        PlainDictionary prompt = RuntimePlainPayload.CloneDictionary(
            _runtime.GetPendingSubmapPromptSnapshotPlain()
        );
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["active"] = _runtime.IsSubmapActive(),
            ["map_id"] = _runtime.GetActiveMapId(),
            ["map_display_name"] = _runtime.GetActiveMapDisplayName(),
            ["return_hint_text"] = _runtime.GetSubmapReturnHintText(),
            ["confirm_visible"] =
                _runtime.GetActiveModalKind() == RuntimeModalKind.SubmapConfirm,
            ["prompt"] = prompt,
        };
    }

    private PlainDictionary BuildGameOverSnapshot()
    {
        PlainDictionary context = RuntimePlainPayload.CloneDictionary(
            _runtime.GetGameOverContextSnapshotPlain()
        );
        if (context.Count == 0)
            return new PlainDictionary(StringComparer.Ordinal);
        return context;
    }

    private PlainDictionary BuildPartySnapshot()
    {
        var members = new PlainList();
        PartyState partyState = _runtime.GetPartyState();
        if (partyState != null)
        {
            foreach (var memberId in partyState.active_member_ids)
                members.Add(BuildPartyMemberSnapshot(memberId, "active"));
            foreach (var memberId in partyState.reserve_member_ids)
                members.Add(BuildPartyMemberSnapshot(memberId, "reserve"));
        }
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["gold"] = partyState != null ? partyState.gold : 0,
            ["leader_member_id"] =
                partyState != null ? partyState.leader_member_id.ToString() : "",
            ["active_member_ids"] =
                partyState != null
                    ? StringNameArrayToStringArray(partyState.active_member_ids)
                    : new PlainList(),
            ["reserve_member_ids"] =
                partyState != null
                    ? StringNameArrayToStringArray(partyState.reserve_member_ids)
                    : new PlainList(),
            ["selected_member_id"] = _runtime.GetPartySelectedMemberId().ToString(),
            ["pending_reward_count"] = _runtime.GetPendingRewardCount(),
            ["contingency_last_result"] = BuildContingencyLastResultSnapshot(),
            ["contingency_status_by_member"] = BuildContingencyStatusByMemberSnapshot(partyState),
            ["members"] = members,
            ["quests"] = BuildQuestSnapshot(partyState),
        };
    }

    private PlainDictionary BuildContingencyLastResultSnapshot()
    {
        ContingencySetupMutationResult result =
            _runtime.GetLastContingencyCommandResultTyped();
        if (result == null)
            return new PlainDictionary(StringComparer.Ordinal);
        return new PlainDictionary(StringComparer.Ordinal)
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

    private PlainDictionary BuildContingencyStatusByMemberSnapshot(PartyState partyState)
    {
        var result = new PlainDictionary(StringComparer.Ordinal);
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

    private static PlainDictionary BuildContingencyMemberStatus(PartyMemberState member)
    {
        var setups = new PlainList();
        foreach (ContingencyMatrixSetupState setup in member.GetContingencySetupsTyped())
        {
            if (setup == null)
                continue;
            setups.Add(BuildContingencySetupSnapshot(setup));
        }
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["member_id"] = member.member_id.ToString(),
            ["setup_count"] = setups.Count,
            ["setups"] = setups,
        };
    }

    private static PlainDictionary BuildContingencySetupSnapshot(
        ContingencyMatrixSetupState setup
    )
    {
        return new PlainDictionary(StringComparer.Ordinal)
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

    private static PlainDictionary BuildContingencyTriggerSnapshot(ContingencyTriggerState trigger)
    {
        PlainDictionary result = new(StringComparer.Ordinal)
        {
            ["type"] = trigger?.Type.ToString() ?? "",
        };
        if (trigger?.TriggerKind == ContingencyTriggerKind.HpBelowPercent)
            result["percent"] = trigger.Percent;
        return result;
    }

    private static PlainList BuildContingencyStoredSpellSnapshots(
        IReadOnlyList<ContingencyStoredSpellEntryState> spells
    )
    {
        var result = new PlainList();
        foreach (ContingencyStoredSpellEntryState spell in spells ?? System.Array.Empty<ContingencyStoredSpellEntryState>())
        {
            if (spell == null)
                continue;
            result.Add(
                new PlainDictionary(StringComparer.Ordinal)
                {
                    ["stored_skill_id"] = spell.StoredSkillId.ToString(),
                    ["cast_level"] = spell.CastLevel,
                    ["order"] = spell.Order,
                    ["target_resolver"] = new PlainDictionary(StringComparer.Ordinal)
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

    private PlainDictionary BuildQuestSnapshot(PartyState partyState)
    {
        if (partyState == null)
            return new PlainDictionary(StringComparer.Ordinal);
        var activeQuestEntries = BuildQuestEntries(partyState.GetActiveQuestsTyped(), "active");
        var claimableQuestEntries = BuildQuestEntries(
            partyState.GetClaimableQuestsTyped(),
            "claimable"
        );
        var failedQuestEntries = BuildQuestEntries(
            partyState.GetFailedQuestsTyped(),
            "failed"
        );
        var completedQuestIds = StringNameArrayToStringArray(
            partyState.GetCompletedQuestIdsTyped()
        );
        if (
            activeQuestEntries.Count == 0
            && claimableQuestEntries.Count == 0
            && failedQuestEntries.Count == 0
            && completedQuestIds.Count == 0
        )
            return new PlainDictionary(StringComparer.Ordinal);
        var activeQuestIds = BuildQuestIds(activeQuestEntries);
        var claimableQuestIds = BuildQuestIds(claimableQuestEntries);
        var failedQuestIds = BuildQuestIds(failedQuestEntries);
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["active_quest_ids"] = activeQuestIds,
            ["claimable_quest_ids"] = claimableQuestIds,
            ["failed_quest_ids"] = failedQuestIds,
            ["completed_quest_ids"] = completedQuestIds,
            ["active_quests"] = activeQuestEntries,
            ["claimable_quests"] = claimableQuestEntries,
            ["failed_quests"] = failedQuestEntries,
        };
    }

    private PlainList BuildQuestEntries(
        IEnumerable<QuestState> questEntries,
        string stageId
    )
    {
        var entries = new List<PlainDictionary>();
        if (questEntries == null)
            return new PlainList();
        foreach (var questState in questEntries)
        {
            var questEntry = NormalizeQuestEntry(questState, stageId);
            if (questEntry.Count > 0)
                entries.Add(questEntry);
        }
        entries.Sort(
            (a, b) =>
            {
                var aId = DictionaryString(a, "quest_id", "");
                var bId = DictionaryString(b, "quest_id", "");
                return string.Compare(aId, bId, System.StringComparison.Ordinal);
            }
        );
        var result = new PlainList();
        foreach (PlainDictionary entry in entries)
            result.Add(entry);
        return result;
    }

    private PlainList BuildQuestIds(IReadOnlyList<object> questEntries)
    {
        var questIds = new PlainList();
        foreach (object entry in questEntries)
        {
            var questId = DictionaryString(entry as IReadOnlyDictionary<string, object>, "quest_id", "");
            if (!string.IsNullOrEmpty(questId))
                questIds.Add(questId);
        }
        return questIds;
    }

    private PlainDictionary NormalizeQuestEntry(QuestState questState, string stageId)
    {
        PlainDictionary questData =
            questState != null
                ? RuntimePlainPayload.CloneDictionary(questState.BuildSnapshotPlain())
                : null;
        if (questData == null || questData.Count == 0)
            return new PlainDictionary(StringComparer.Ordinal);
        if (!HasExactQuestEntryFields(questData))
            return new PlainDictionary(StringComparer.Ordinal);
        var questId = ReadQuestString(questData, "quest_id");
        var statusId = ReadQuestString(questData, "status_id");
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(statusId))
            return new PlainDictionary(StringComparer.Ordinal);
        if (
            !IsValidQuestStep(questData, "accepted_at_world_step")
            || !IsValidQuestStep(questData, "completed_at_world_step")
            || !IsValidQuestStep(questData, "reward_claimed_at_world_step")
            || !IsValidQuestStep(questData, "failed_at_world_step")
        )
            return new PlainDictionary(StringComparer.Ordinal);
        var objectiveProgress = NormalizeQuestProgressMap(questData, "objective_progress");
        if (objectiveProgress == null)
            return new PlainDictionary(StringComparer.Ordinal);
        if (
            questData["last_progress_context"]
            is not IReadOnlyDictionary<string, object> contextValue
        )
            return new PlainDictionary(StringComparer.Ordinal);
        questData["quest_id"] = questId;
        questData["stage_id"] = stageId;
        questData["status_id"] = statusId;
        questData["failure_reason_id"] = ReadQuestString(
            questData,
            "failure_reason_id"
        );
        questData["objective_progress"] = objectiveProgress;
        questData["last_progress_context"] = RuntimePlainPayload.CloneDictionary(contextValue);
        return questData;
    }

    private static bool IsValidQuestStep(
        IReadOnlyDictionary<string, object> questData,
        string fieldName
    )
    {
        return TryReadInteger(questData[fieldName], out long value) && value >= -1;
    }

    private static bool HasExactQuestEntryFields(IReadOnlyDictionary<string, object> questData)
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

    private static string ReadQuestString(
        IReadOnlyDictionary<string, object> questData,
        string fieldName
    )
    {
        return TryReadExactStringValue(questData[fieldName], out string value)
            ? value
            : "";
    }

    private static PlainDictionary NormalizeQuestProgressMap(
        IReadOnlyDictionary<string, object> questData,
        string fieldName
    )
    {
        if (questData[fieldName] is not IReadOnlyDictionary<string, object> progressValue)
            return null;
        var result = new PlainDictionary(StringComparer.Ordinal);
        foreach ((string objectiveId, object objectiveProgressValue) in progressValue)
        {
            if (string.IsNullOrEmpty(objectiveId))
                return null;
            if (
                !TryReadInteger(objectiveProgressValue, out long progress)
                || progress < 0
                || progress > int.MaxValue
            )
                return null;
            result[objectiveId] = (int)progress;
        }
        return result;
    }

    private PlainDictionary BuildPartyMemberSnapshot(StringName memberId, string rosterRole)
    {
        PartyState partyState = _runtime.GetPartyState();
        PartyMemberState memberState =
            partyState != null ? partyState.GetMemberState(memberId) : null;
        PlainDictionary achievementSummary = RuntimePlainPayload.CloneDictionary(
            _runtime.GetMemberAchievementSummarySnapshotPlain(memberId)
        );
        var attributeSnapshot = _runtime.GetMemberAttributeSnapshot(memberId);
        PlainList equipmentEntries = DictionaryFactsToPlainList(
            _runtime.GetMemberEquippedEntriesSnapshotPlain(memberId)
        );
        UnitProgress progression = memberState?.progression;
        return new PlainDictionary(StringComparer.Ordinal)
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
                    ? achievementSummary
                    : new PlainDictionary(StringComparer.Ordinal),
            ["attributes"] =
                attributeSnapshot != null
                    ? BuildAttributeSnapshotPlain(attributeSnapshot)
                    : new PlainDictionary(StringComparer.Ordinal),
            ["equipment"] = equipmentEntries,
            ["equipment_count"] = equipmentEntries.Count,
        };
    }

    private PlainList BuildMemberLearnedSkillIds(PartyMemberState memberState)
    {
        var learnedSkillIds = new PlainList();
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
        SortPlainStrings(learnedSkillIds);
        return learnedSkillIds;
    }

    private PlainList BuildMemberUnlockedCombatResourceIds(
        PartyMemberState memberState
    )
    {
        var resourceIds = new PlainList();
        if (memberState == null)
            return resourceIds;
        UnitProgress progression = memberState.progression;
        if (progression == null)
            return resourceIds;
        foreach (var resourceId in progression.UnlockedCombatResourceIdsTyped)
            resourceIds.Add(resourceId.ToString());
        SortPlainStrings(resourceIds);
        return resourceIds;
    }

    private PlainList BuildMemberSkillEntries(PartyMemberState memberState)
    {
        var entries = new PlainList();
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
                new PlainDictionary(StringComparer.Ordinal)
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

    private PlainList BuildMemberProfessionEntries(PartyMemberState memberState)
    {
        var entries = new PlainList();
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
                new PlainDictionary(StringComparer.Ordinal)
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

    private PlainList BuildSortedStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new PlainList();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        SortPlainStrings(result);
        return result;
    }

    private PlainDictionary BuildSettlementSnapshot()
    {
        var settlementId = _runtime.GetResolvedSettlementId();
        PlainDictionary settlementFacts = !string.IsNullOrEmpty(settlementId)
            ? RuntimePlainPayload.CloneDictionary(
                _runtime.GetSettlementHeadlessFactsPlain(settlementId)
            )
            : new PlainDictionary(StringComparer.Ordinal);
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Settlement,
            ["settlement_id"] = settlementId,
            ["display_name"] = DictionaryString(settlementFacts, "display_name", ""),
            ["tier_name"] = DictionaryString(settlementFacts, "tier_name", ""),
            ["faction_id"] = DictionaryString(settlementFacts, "faction_id", ""),
            ["services"] = new PlainList(
                DictionaryArray(settlementFacts, "services", new PlainList())
            ),
            ["feedback_text"] = _runtime.GetSettlementFeedbackText(),
        };
    }

    private PlainDictionary BuildShopSnapshot()
    {
        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            _runtime.GetShopWindowDataSnapshotPlain()
        );
        if (WindowDataMatchesPanelKind(windowData, SettlementPanelKind.Forge))
            windowData.Clear();
        windowData.Remove("party_state");
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Shop,
            ["window_data"] = windowData,
        };
    }

    private PlainDictionary BuildContractBoardSnapshot()
    {
        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            _runtime.GetContractBoardWindowDataSnapshotPlain()
        );
        windowData.Remove("party_state");
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.ContractBoard,
            ["window_data"] = windowData,
        };
    }

    private PlainDictionary BuildForgeSnapshot()
    {
        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            _runtime.GetForgeWindowDataSnapshotPlain()
        );
        windowData.Remove("party_state");
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Forge,
            ["window_data"] = windowData,
        };
    }

    private PlainDictionary BuildStagecoachSnapshot()
    {
        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            _runtime.GetStagecoachWindowDataSnapshotPlain()
        );
        windowData.Remove("party_state");
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Stagecoach,
            ["window_data"] = windowData,
        };
    }

    private PlainDictionary BuildBountyBoardSnapshot()
    {
        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            _runtime.GetBountyBoardWindowDataSnapshotPlain()
        );
        windowData.Remove("party_state");
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.BountyBoard,
            ["window_data"] = windowData,
        };
    }

    private PlainDictionary BuildNpcQuestOfferSnapshot()
    {
        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            _runtime.GetNpcQuestOfferWindowDataSnapshotPlain()
        );
        windowData.Remove("party_state");
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.NpcQuestOffer,
            ["window_data"] = windowData,
        };
    }

    private PlainDictionary BuildCharacterInfoSnapshot()
    {
        PlainDictionary context = RuntimePlainPayload.CloneDictionary(
            _runtime.GetCharacterInfoContextSnapshotPlain()
        );
        context["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.CharacterInfo;
        if (context.ContainsKey("coord"))
            context["coord"] = CoordToDict(DictionaryVector2I(context, "coord", Vector2I.Zero));
        return context;
    }

    private PlainDictionary BuildWarehouseSnapshot()
    {
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Warehouse,
            ["entry_label"] = _runtime.GetActiveWarehouseEntryLabel(),
            ["window_data"] =
                _runtime.GetPartyState() != null
                    ? RuntimePlainPayload.CloneDictionary(
                        _runtime.GetWarehouseWindowDataSnapshotPlain()
                    )
                    : new PlainDictionary(StringComparer.Ordinal),
        };
    }

    private PlainDictionary BuildBattleSnapshot()
    {
        var battleState = _runtime.GetBattleState();
        if (battleState == null || battleState.IsEmpty())
            return new PlainDictionary(StringComparer.Ordinal) { ["active"] = false };

        var battleRuntime = _runtime.GetBattleRuntime();
        var calamitySnapshot = new PlainDictionary(StringComparer.Ordinal);
        var contingencySnapshot = new PlainDictionary(StringComparer.Ordinal);
        if (battleRuntime != null)
        {
            foreach (
                (StringName memberId, int calamity) in battleRuntime.GetCalamityByMemberIdSnapshot()
            )
            {
                calamitySnapshot[memberId.ToString()] = calamity;
            }
            contingencySnapshot = RuntimePlainPayload.CloneDictionary(
                battleRuntime.GetContingencySystemTyped()?.BuildSnapshotPlain()
            );
        }

        using var adapter = new BattleHudAdapter();
        adapter.SetupRuntimeContext(_runtime as GameRuntimeFacade, _runtime.GetGameSession());
        IReadOnlyList<Vector2I> selectedTargetCoords =
            _runtime.GetSelectedBattleSkillTargetCoordsSnapshotPlain();
        IReadOnlyList<StringName> selectedTargetUnitIds =
            _runtime.GetSelectedBattleSkillTargetUnitIdsSnapshotPlain();
        BattleHudSnapshot hudSnapshot = adapter.BuildSnapshot(
            battleState,
            _runtime.GetBattleSelectedCoord(),
            _runtime.GetSelectedBattleSkillId(),
            _runtime.GetSelectedBattleSkillName(),
            _runtime.GetSelectedBattleSkillVariantName(),
            selectedTargetCoords,
            _runtime.GetSelectedBattleSkillRequiredCoordCount(),
            selectedTargetUnitIds,
            _runtime.GetSelectedBattleSkillVariantId(),
            _runtime.GetActiveBattleEncounterName(),
            _runtime.GetSelectedBattleSkillPreview(),
            _runtime.GetSelectedBattleSkillEntryId()
        );
        PlainDictionary hudPayload = FlattenBattlePresentationDictionary(
            hudSnapshot.CanonicalFacts,
            "GameRuntimeSnapshotBuilder.battle.hud"
        );
        BattleObjectiveProgressSnapshot objectiveProgress =
            new BattleStateReadView(battleState).ObjectiveProgress;

        var units = new PlainList();
        foreach ((StringName _, BattleUnitState unitState) in battleState.UnitEntries(sorted: true))
        {
            if (unitState == null)
                continue;
            var attributeSnapshot = unitState.attribute_snapshot;
            BattleUnitShieldSnapshot shieldState = unitState.GetShieldStateTyped();
            BattleUnitCombatResourceValues combatResources =
                unitState.GetCombatResourcesReadViewTyped().Values;
            units.Add(
                new PlainDictionary(StringComparer.Ordinal)
                {
                    ["unit_id"] = unitState.unit_id.ToString(),
                    ["display_name"] = !string.IsNullOrEmpty(unitState.display_name)
                        ? unitState.display_name
                        : unitState.unit_id.ToString(),
                    ["coord"] = CoordToDict(unitState.GetAnchorCoord()),
                    ["faction_id"] = unitState.faction_id.ToString(),
                    ["control_mode"] = unitState.control_mode.ToString(),
                    ["is_alive"] = combatResources.IsAlive,
                    ["current_hp"] = combatResources.Hp,
                    ["current_mp"] = combatResources.Mp,
                    ["current_stamina"] = combatResources.Stamina,
                    ["stamina_max"] = attributeSnapshot?.GetValue("stamina_max") ?? 0,
                    ["current_aura"] = combatResources.Aura,
                    ["aura_max"] = unitState.GetAuraMax(),
                    ["current_shield_hp"] = shieldState.CurrentHp,
                    ["shield_max_hp"] = shieldState.MaxHp,
                    ["shield_duration"] = shieldState.Duration,
                    ["shield_family"] = shieldState.Family.ToString(),
                    ["current_ap"] = combatResources.Ap,
                    ["current_move_points"] =
                        combatResources.MovePoints,
                    ["has_pending_cast"] = unitState.HasPendingCast(),
                    ["pending_cast"] = BuildPendingCastSnapshot(unitState.pending_cast),
                }
            );
        }
        PlainList reportEntries = new();
        foreach (
            IReadOnlyDictionary<string, object> reportEntry in battleState.ReportEntriesTyped
        )
        {
            reportEntries.Add(RuntimePlainPayload.CloneDictionary(reportEntry));
        }

        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["active"] = true,
            ["encounter_id"] = _runtime.GetActiveBattleEncounterId().ToString(),
            ["encounter_name"] = _runtime.GetActiveBattleEncounterName(),
            ["phase"] = battleState.phase.ToString(),
            ["active_unit_id"] = battleState.active_unit_id.ToString(),
            ["active_unit_name"] = _runtime.GetBattleActiveUnitName(),
            ["modal_state"] = battleState.modal_state.ToString(),
            ["objective_mode"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.ObjectiveRuntimeState?.Mode ?? BattleObjectiveMode.Unknown
            ),
            ["objective"] = BuildBattleObjectiveProgressSnapshot(objectiveProgress),
            ["outcome"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.FinalDecision?.Outcome ?? BattleOutcomeKind.Unknown
            ),
            ["end_reason"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.FinalDecision?.EndReason ?? BattleEndReasonKind.None
            ),
            ["decision_tu"] = battleState.FinalDecision?.DecisionTu ?? -1,
            ["winner_faction_id"] = battleState.winner_faction_id.ToString(),
            ["selected_coord"] = CoordToDict(
                _runtime.GetBattleSelectedCoord()
            ),
            ["selected_skill_entry_id"] = _runtime.GetSelectedBattleSkillEntryId().ToString(),
            ["selected_skill_id"] = _runtime.GetSelectedBattleSkillId().ToString(),
            ["selected_skill_variant_id"] = _runtime
                .GetSelectedBattleSkillVariantId()
                .ToString(),
            ["selected_target_coords"] = CoordEnumerableToDictArray(selectedTargetCoords),
            ["selected_target_unit_ids"] = StringNameArrayToStringArray(
                selectedTargetUnitIds
            ),
            ["selected_target_unit_count"] = selectedTargetUnitIds.Count,
            ["start_confirm_visible"] =
                _runtime.GetActiveModalKind() == RuntimeModalKind.BattleStartConfirm,
            ["start_prompt"] = RuntimePlainPayload.CloneDictionary(
                _runtime.GetPendingBattleStartPromptSnapshotPlain()
            ),
            ["terrain_counts"] = IntDictionaryToPlain(
                _runtime.GetBattleTerrainCountsSnapshotTyped()
            ),
            ["calamity_by_member_id"] = calamitySnapshot,
            ["contingency"] = contingencySnapshot,
            ["hud"] = hudPayload,
            ["report_entry_count"] = battleState.ReportEntryCount,
            ["report_entries"] = reportEntries,
            ["units"] = units,
        };
    }

    private static PlainDictionary BuildBattleObjectiveProgressSnapshot(
        BattleObjectiveProgressSnapshot progress
    )
    {
        progress ??= BattleObjectiveProgressSnapshot.Empty;
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["mode"] = BattleObjectiveRuntimeCodec.ToWireValue(progress.Mode),
            ["target_actor_id"] = progress.TargetActorId.ToString(),
            ["target_unit_id"] = progress.TargetUnitId.ToString(),
            ["target_display_name"] = progress.TargetDisplayName,
            ["target_alive"] = progress.TargetAlive,
            ["target_secured"] = progress.TargetSecured,
            ["target_reached_exit"] = progress.TargetReachedExit,
            ["required_unit_ids"] = StringNameArrayToStringArray(
                progress.RequiredUnitIds
            ),
            ["alive_required_unit_ids"] = StringNameArrayToStringArray(
                progress.AliveRequiredUnitIds
            ),
            ["reached_exit_unit_ids"] = StringNameArrayToStringArray(
                progress.ReachedExitUnitIds
            ),
            ["required_unit_count"] = progress.RequiredUnitCount,
            ["alive_required_unit_count"] = progress.AliveRequiredUnitCount,
            ["reached_exit_unit_count"] = progress.ReachedExitUnitCount,
            ["exit_zone_id"] = progress.ExitZoneId.ToString(),
            ["exit_edge"] = progress.ExitEdgeWireValue,
            ["exit_depth"] = progress.ExitDepth,
            ["exit_coords"] = CoordEnumerableToDictArray(progress.ExitCoords),
            ["current_tu"] = progress.CurrentTu,
            ["start_tu"] = progress.StartTu,
            ["deadline_tu"] = progress.DeadlineTu,
            ["remaining_tu"] = progress.RemainingTu,
            ["enemy_unit_count"] = progress.EnemyUnitCount,
            ["alive_enemy_unit_count"] = progress.AliveEnemyUnitCount,
            ["operation_nodes"] = BuildOperationNodeSnapshots(
                progress.OperationNodes
            ),
            ["operation_node_count"] = progress.OperationNodeCount,
            ["completed_operation_node_count"] =
                progress.CompletedOperationNodeCount,
            ["incomplete_operation_node_count"] =
                progress.IncompleteOperationNodeCount,
            ["control_zones"] = BuildControlZoneSnapshots(
                progress.ControlZones
            ),
            ["control_zone_count"] = progress.ControlZoneCount,
            ["player_control_score"] = progress.PlayerControlScore,
            ["hostile_control_score"] = progress.HostileControlScore,
            ["control_score_target"] = progress.ControlScoreTarget,
        };
    }

    private static PlainList BuildOperationNodeSnapshots(
        IEnumerable<BattleObjectiveNodeProgressSnapshot> nodes
    )
    {
        var result = new PlainList();
        foreach (
            BattleObjectiveNodeProgressSnapshot node in
            nodes ?? Array.Empty<BattleObjectiveNodeProgressSnapshot>()
        )
        {
            result.Add(
                new PlainDictionary(StringComparer.Ordinal)
                {
                    ["node_id"] = node.NodeId.ToString(),
                    ["display_name"] = node.DisplayName,
                    ["zone_id"] = node.ZoneId.ToString(),
                    ["coord"] = CoordToDict(node.Coord),
                    ["is_completed"] = node.IsCompleted,
                }
            );
        }
        return result;
    }

    private static PlainList BuildControlZoneSnapshots(
        IEnumerable<BattleObjectiveControlZoneProgressSnapshot> zones
    )
    {
        var result = new PlainList();
        foreach (
            BattleObjectiveControlZoneProgressSnapshot zone in
            zones ?? Array.Empty<BattleObjectiveControlZoneProgressSnapshot>()
        )
        {
            result.Add(
                new PlainDictionary(StringComparer.Ordinal)
                {
                    ["zone_id"] = zone.ZoneId.ToString(),
                    ["display_name"] = zone.DisplayName,
                    ["placement_edge"] =
                        BattleObjectiveRuntimeCodec.ToWireValue(
                            zone.PlacementEdge
                        ),
                    ["placement_depth"] = zone.PlacementDepth,
                    ["coords"] = CoordEnumerableToDictArray(zone.Coords),
                    ["occupancy"] = zone.OccupancyWireValue,
                }
            );
        }
        return result;
    }

    private PlainDictionary BuildPendingCastSnapshot(BattlePendingCastState pendingCast)
    {
        if (pendingCast == null)
            return new PlainDictionary(StringComparer.Ordinal);
        int remainingProgress = Mathf.Max(pendingCast.RemainingCastProgress, 0);
        int remainingTu = (remainingProgress + 99) / 100;
        return new PlainDictionary(StringComparer.Ordinal)
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

    private PlainDictionary BuildRewardSnapshot()
    {
        var reward = _runtime.GetSnapshotReward();
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Reward,
            ["remaining_count"] = _runtime.GetPendingRewardCount(),
            ["reward"] = reward != null
                ? BuildPendingCharacterRewardSnapshot(reward)
                : new PlainDictionary(StringComparer.Ordinal),
        };
    }

    private PlainDictionary BuildLootSnapshot()
    {
        if (_runtime == null)
            return new PlainDictionary(StringComparer.Ordinal);
        PlainDictionary lootSnapshot = RuntimePlainPayload.CloneDictionary(
            _runtime.GetLastBattleLootSnapshotPlain()
        );
        if (lootSnapshot.Count == 0)
            return new PlainDictionary(StringComparer.Ordinal);
        if (
            DictionaryInt(lootSnapshot, "loot_entry_count", 0) <= 0
            && DictionaryInt(lootSnapshot, "overflow_entry_count", 0) <= 0
        )
            return new PlainDictionary(StringComparer.Ordinal);
        return lootSnapshot;
    }

    private PlainDictionary BuildPromotionSnapshot()
    {
        PlainDictionary prompt = RuntimePlainPayload.CloneDictionary(
            _runtime.GetCurrentPromotionPromptSnapshotPlain()
        );
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.Promotion,
            ["prompt"] = prompt,
        };
    }

    private PlainDictionary BuildLogSnapshot(int limit = 30)
    {
        return _runtime != null
            ? RuntimePlainPayload.CloneDictionary(_runtime.GetLogSnapshotPlain(limit))
            : new PlainDictionary(StringComparer.Ordinal);
    }

    private static bool WindowDataMatchesPanelKind(
        IReadOnlyDictionary<string, object> windowData,
        SettlementPanelKind panelKind
    )
    {
        if (windowData.Count == 0)
            return false;
        return
            DictionaryString(windowData, "panel_kind", "")
            == SettlementPanelKinds.ToPayloadValue(panelKind);
    }

    private static PlainDictionary CoordToDict(Vector2I coord)
    {
        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["x"] = coord.X,
            ["y"] = coord.Y,
        };
    }

    private static PlainList CoordEnumerableToDictArray(IEnumerable<Vector2I> coords)
    {
        var result = new PlainList();
        if (coords == null)
            return result;
        foreach (var coord in coords)
            result.Add(CoordToDict(coord));
        return result;
    }

    private static PlainList StringNameArrayToStringArray(
        IEnumerable<StringName> values
    )
    {
        var result = new PlainList();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value.ToString());
        return result;
    }

    private PlainList BuildNearbyEncounterEntries(int limit = 8)
    {
        return DictionaryFactsToPlainList(
            _runtime.GetNearbyEncounterEntriesSnapshotPlain(limit)
        );
    }

    private PlainList BuildNearbyWorldEventEntries(int limit = 8)
    {
        return DictionaryFactsToPlainList(
            _runtime.GetNearbyWorldEventEntriesSnapshotPlain(limit)
        );
    }

    private static PlainList DictionaryFactsToPlainList(
        IReadOnlyList<IReadOnlyDictionary<string, object>> values
    )
    {
        var result = new PlainList();
        if (values == null)
            return result;
        foreach (IReadOnlyDictionary<string, object> value in values)
            result.Add(RuntimePlainPayload.CloneDictionary(value));
        return result;
    }

    private static PlainDictionary BuildAttributeSnapshotPlain(AttributeSnapshot snapshot)
    {
        var result = new PlainDictionary(StringComparer.Ordinal);
        if (snapshot == null)
            return result;
        List<KeyValuePair<StringName, int>> entries = new(
            snapshot.GetAllValuesTyped()
        );
        entries.Sort(
            (left, right) =>
                string.CompareOrdinal(left.Key.ToString(), right.Key.ToString())
        );
        foreach ((StringName attributeId, int value) in entries)
            result[attributeId.ToString()] = value;
        return result;
    }

    private static PlainDictionary IntDictionaryToPlain(
        IReadOnlyDictionary<string, int> values
    )
    {
        var result = new PlainDictionary(StringComparer.Ordinal);
        if (values == null)
            return result;
        foreach ((string key, int value) in values)
            result[key] = value;
        return result;
    }

    private static int DictionaryInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback
    )
    {
        if (
            dictionary == null
            || !dictionary.TryGetValue(key, out object rawValue)
            || !TryReadInteger(rawValue, out long value)
            || value < int.MinValue
            || value > int.MaxValue
        )
            return fallback;
        return (int)value;
    }

    private static string DictionaryString(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        string fallback
    )
    {
        if (dictionary == null || !dictionary.TryGetValue(key, out object rawValue))
            return fallback;
        return TryReadExactStringValue(rawValue, out string value) ? value : fallback;
    }

    private static bool TryReadExactStringValue(object rawValue, out string value)
    {
        switch (rawValue)
        {
            case string text:
                value = text.StripEdges();
                return true;
            default:
                value = "";
                return false;
        }
    }

    private static IReadOnlyList<object> DictionaryArray(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        IReadOnlyList<object> fallback
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> array
            ? array
            : fallback;
    }

    private static Vector2I DictionaryVector2I(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        Vector2I fallback
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object rawValue)
            && rawValue is Vector2I coord
            ? coord
            : fallback;
    }

    private static bool TryReadInteger(object rawValue, out long value)
    {
        switch (rawValue)
        {
            case byte byteValue:
                value = byteValue;
                return true;
            case short shortValue:
                value = shortValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static void SortPlainStrings(PlainList values)
    {
        values.Sort(
            (left, right) =>
                string.Compare(
                    left as string ?? "",
                    right as string ?? "",
                    StringComparison.Ordinal
                )
        );
    }

    private static PlainDictionary BuildPendingCharacterRewardSnapshot(
        PendingCharacterReward reward
    )
    {
        if (reward == null)
            return new PlainDictionary(StringComparer.Ordinal);

        var entries = new PlainList();
        foreach (PendingCharacterRewardEntry entry in reward.entries ?? new List<PendingCharacterRewardEntry>())
        {
            if (entry == null)
                continue;
            entries.Add(
                new PlainDictionary(StringComparer.Ordinal)
                {
                    ["entry_type"] = entry.entry_type.ToString(),
                    ["target_id"] = entry.target_id.ToString(),
                    ["target_label"] = entry.target_label ?? "",
                    ["amount"] = entry.amount,
                    ["reason_text"] = entry.reason_text ?? "",
                }
            );
        }

        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["reward_id"] = reward.reward_id.ToString(),
            ["member_id"] = reward.member_id.ToString(),
            ["member_name"] = reward.member_name ?? "",
            ["source_type"] = reward.source_type.ToString(),
            ["source_id"] = reward.source_id.ToString(),
            ["source_label"] = reward.source_label ?? "",
            ["summary_text"] = reward.summary_text ?? "",
            ["entries"] = entries,
        };
    }

    private static PlainDictionary FlattenBattlePresentationDictionary(
        IReadOnlyDictionary<string, object> source,
        string path
    )
    {
        var result = new PlainDictionary(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach ((string key, object value) in source)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    $"Battle HUD plain snapshot contains an empty key at {path}."
                );
            }
            result[key] = FlattenBattlePresentationValue(value, $"{path}.{key}");
        }
        return result;
    }

    private static PlainList FlattenBattlePresentationEnumerable(IEnumerable source, string path)
    {
        var result = new PlainList();
        if (source == null)
            return result;
        int index = 0;
        foreach (object value in source)
        {
            result.Add(FlattenBattlePresentationValue(value, $"{path}[{index}]"));
            index++;
        }
        return result;
    }

    private static object FlattenBattlePresentationValue(object value, string path)
    {
        return value switch
        {
            null => null,
            IBattlePresentationSnapshotValue snapshotValue =>
                FlattenBattlePresentationDictionary(snapshotValue.CanonicalFacts, path),
            IReadOnlyDictionary<string, object> dictionaryValue =>
                FlattenBattlePresentationDictionary(dictionaryValue, path),
            string or StringName or bool or byte or short or int or long or float or double
                or Vector2I or Vector2 or Vector3I or Vector3 or Color => value,
            Variant => throw UnsupportedBattlePresentationValue(value, path),
            GodotObject => throw UnsupportedBattlePresentationValue(value, path),
            IDisposable => throw UnsupportedBattlePresentationValue(value, path),
            IEnumerable enumerableValue =>
                FlattenBattlePresentationEnumerable(enumerableValue, path),
            _ => throw UnsupportedBattlePresentationValue(value, path),
        };
    }

    private static InvalidOperationException UnsupportedBattlePresentationValue(
        object value,
        string path
    )
    {
        return new InvalidOperationException(
            $"Battle HUD plain snapshot does not support value type {value?.GetType().FullName ?? "<null>"} at {path}."
        );
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
