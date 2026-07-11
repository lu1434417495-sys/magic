using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public class SettlementResearchService
{
    private const string ResearchInteractionId = "service_research";
    private const int ResearchGoldCost = 200;
    private static readonly StringName ResearchSourceType = "npc_teach";

    private static readonly string[] RequiredServicePayloadStringFields =
    {
        "facility_name",
        "npc_name",
        "service_type",
    };

    private static readonly SettlementResearchRewardEntry[] ResearchRewardCatalog =
    {
        SettlementResearchRewardEntry.Create(
            "research_field_manual",
            SettlementResearchRewardKind.KnowledgeUnlock,
            "field_manual",
            "野外手册",
            "研究员整理出一份可长期翻阅的野外手册抄本。"
        ),
        SettlementResearchRewardEntry.Create(
            "research_guard_break",
            SettlementResearchRewardKind.SkillUnlock,
            "warrior_guard_break",
            "裂甲斩",
            "研究记录补全了裂甲斩的动作拆解。"
        ),
    };

    public bool IsSupportedInteraction(string interaction_script_id)
    {
        return (interaction_script_id ?? "").StripEdges() == ResearchInteractionId;
    }

    internal SettlementServiceMetadata BuildServiceMetadataTyped(
        PartyState party_state,
        GDictionary payload = null
    )
    {
        payload ??= new GDictionary();
        bool canAffordResearch = party_state != null && party_state.CanAfford(ResearchGoldCost);
        List<SettlementResearchRewardEntry> researchCatalog =
            BuildResearchCatalogTyped(out string catalogSchemaError);
        List<SettlementResearchMemberAvailability> memberAvailabilityEntries =
            _build_member_research_availability_entries(
                party_state,
                canAffordResearch,
                catalogSchemaError,
                researchCatalog
            );
        StringName requestedMemberId = ReadStringName(payload, "member_id");

        bool hasAvailableResearch = false;
        string memberDisabledReason = "";
        if (requestedMemberId != "")
        {
            SettlementResearchMemberAvailability selectedAvailability = FindMemberAvailability(
                memberAvailabilityEntries,
                requestedMemberId
            );
            hasAvailableResearch = selectedAvailability?.HasAvailableResearch ?? false;
            memberDisabledReason = selectedAvailability?.DisabledReason ?? "暂无可研究内容";
        }
        else
        {
            foreach (var availability in memberAvailabilityEntries)
            {
                if (availability.HasAvailableResearch)
                {
                    hasAvailableResearch = true;
                    break;
                }
            }
        }

        bool isEnabled = canAffordResearch && hasAvailableResearch;
        string disabledReason = "";
        if (!string.IsNullOrEmpty(catalogSchemaError))
        {
            disabledReason = "研究配置无效";
        }
        else if (!canAffordResearch)
        {
            disabledReason = "金币不足";
        }
        else if (!hasAvailableResearch)
        {
            disabledReason = !string.IsNullOrEmpty(memberDisabledReason) ? memberDisabledReason : "暂无可研究内容";
        }

        return new SettlementServiceMetadata(
            $"{ResearchGoldCost} 金",
            isEnabled,
            disabledReason,
            memberAvailabilityEntries
        );
    }

    internal SettlementServiceResult ExecuteTyped(
        GDictionary settlement,
        GDictionary payload,
        PartyState party_state,
        IEnumerable<QuestProgressService.QuestProgressEventData> quest_progress_events = null)
    {
        settlement ??= new GDictionary();
        payload ??= new GDictionary();

        if (party_state == null)
        {
            return _build_result(false, "当前不存在队伍数据。", quest_progress_events);
        }

        string schemaError = _validate_execution_schema(settlement, payload);
        if (!string.IsNullOrEmpty(schemaError))
        {
            return _build_result(false, schemaError, quest_progress_events);
        }

        List<SettlementResearchRewardEntry> researchCatalog =
            BuildResearchCatalogTyped(out string catalogSchemaError);
        if (!string.IsNullOrEmpty(catalogSchemaError))
        {
            return _build_result(false, catalogSchemaError, quest_progress_events);
        }

        PartyMemberState memberState = _resolve_tarGetMemberState(party_state, payload);
        if (memberState == null || memberState.progression == null)
        {
            return _build_result(false, "当前没有可承接研究的成员。", quest_progress_events);
        }

        SettlementResearchRewardEntry researchCandidate = _select_research_candidate(
            party_state,
            memberState,
            researchCatalog
        );
        if (researchCandidate == null)
        {
            return _build_result(false, $"{_resolve_member_name(memberState)} 当前暂无可研究的新内容。", quest_progress_events);
        }

        string facilityName = ReadString(payload, "facility_name").StripEdges();
        string npcName = ReadString(payload, "npc_name").StripEdges();
        string serviceType = ReadString(payload, "service_type").StripEdges();
        PendingCharacterReward pendingReward = _build_pending_research_reward(
            memberState,
            researchCandidate,
            facilityName,
            npcName,
            serviceType
        );
        if (pendingReward == null || pendingReward.IsEmpty())
        {
            return _build_result(false, "当前研究成果构造失败。", quest_progress_events);
        }

        string settlementName = ReadString(settlement, "display_name").StripEdges();
        PendingCharacterRewardEntry rewardEntry = _get_first_reward_entry(pendingReward);
        string rewardLabel = rewardEntry?.target_label?.StripEdges() ?? "";
        if (string.IsNullOrEmpty(rewardLabel))
        {
            return _build_result(false, "当前研究成果构造失败。", quest_progress_events);
        }

        if (!party_state.SpendGold(ResearchGoldCost))
        {
            return _build_result(false, "金币不足，无法委托研究。", quest_progress_events);
        }

        string message = $"{settlementName} 的 {facilityName} 已收下 {ResearchGoldCost} 金研究经费，由 {npcName} 启动本次{serviceType}委托。";
        message += $"已整理出新成果：{rewardLabel}。";

        return _build_result(
            true,
            message,
            quest_progress_events,
            true,
            -ResearchGoldCost,
            new[] { pendingReward },
            new GDictionary
            {
                ["research_interaction_id"] = ResearchInteractionId,
                ["gold_spent"] = ResearchGoldCost,
                ["facility_name"] = facilityName,
                ["research_source_id"] = pendingReward.source_id.ToString(),
                ["research_entry_type"] = rewardEntry.entry_type.ToString(),
                ["research_target_id"] = rewardEntry.target_id.ToString(),
            });
    }

    protected virtual GArray GetResearchRewardCatalogCore()
    {
        var result = new GArray();
        foreach (SettlementResearchRewardEntry entry in ResearchRewardCatalog)
        {
            result.Add(entry?.ToDictionary() ?? new GDictionary());
        }
        return result;
    }

    private SettlementServiceResult _build_result(
        bool success,
        string message,
        IEnumerable<QuestProgressService.QuestProgressEventData> questProgressEvents,
        bool persistPartyState = false,
        int goldDelta = 0,
        IEnumerable<PendingCharacterReward> pendingCharacterRewards = null,
        GDictionary serviceSideEffects = null)
    {
        var result = new SettlementServiceResult
        {
            Success = success,
            Message = message,
            PersistPartyState = persistPartyState,
            GoldDelta = goldDelta,
        };
        result.SetPendingCharacterRewardsTyped(pendingCharacterRewards);
        result.SetQuestProgressEventsTyped(questProgressEvents);
        result.SetServiceSideEffects(serviceSideEffects);
        return result;
    }

    private string _validate_execution_schema(GDictionary settlement, GDictionary payload)
    {
        string payloadError = _validate_required_string_fields(payload, RequiredServicePayloadStringFields, "research payload");
        if (!string.IsNullOrEmpty(payloadError))
        {
            return payloadError;
        }
        return _validate_required_string_fields(settlement, new[] { "display_name" }, "settlement");
    }

    private List<SettlementResearchRewardEntry> BuildResearchCatalogTyped(out string error)
    {
        error = "";
        var result = new List<SettlementResearchRewardEntry>();
        int index = 0;
        foreach (Variant candidateValue in GetResearchRewardCatalogCore())
        {
            string schemaLabel = $"research candidate[{index}]";
            if (candidateValue.VariantType != Variant.Type.Dictionary)
            {
                error = $"研究候选配置无效：catalog[{index}] 必须是 Dictionary。";
                return new List<SettlementResearchRewardEntry>();
            }
            SettlementResearchRewardEntry entry = SettlementResearchRewardEntry.FromDictionary(
                candidateValue.AsGodotDictionary(),
                schemaLabel,
                out string entryError
            );
            if (!string.IsNullOrEmpty(entryError))
            {
                error = entryError;
                return new List<SettlementResearchRewardEntry>();
            }
            result.Add(entry);
            index++;
        }
        return result;
    }

    private string _validate_research_catalog_schema()
    {
        BuildResearchCatalogTyped(out string catalogError);
        return catalogError;
    }

    private string _validate_research_candidate_schema(GDictionary researchCandidate, int index = -1)
    {
        string schemaLabel = index >= 0 ? $"research candidate[{index}]" : "research candidate";
        SettlementResearchRewardEntry.FromDictionary(
            researchCandidate,
            schemaLabel,
            out string error
        );
        return error;
    }

    private static string _validate_required_string_fields(GDictionary data, string[] fieldNames, string schemaLabel)
    {
        foreach (string fieldName in fieldNames)
        {
            if (data == null || !data.ContainsKey(fieldName))
            {
                return $"{schemaLabel}.{fieldName} 必须显式提供非空 String。";
            }
            var value = data[fieldName];
            if (value.VariantType != Variant.Type.String || value.AsString().StripEdges().Length == 0)
            {
                return $"{schemaLabel}.{fieldName} 必须显式提供非空 String。";
            }
        }
        return "";
    }

    private static PartyMemberState _resolve_tarGetMemberState(PartyState partyState, GDictionary payload)
    {
        if (partyState == null)
        {
            return null;
        }
        StringName requestedMemberId = ReadStringName(payload, "member_id");
        if (requestedMemberId != "")
        {
            return partyState.GetMemberState(requestedMemberId);
        }
        StringName defaultMemberId = _resolve_default_member_id(partyState);
        return defaultMemberId != ""
            ? partyState.GetMemberState(defaultMemberId)
            : null;
    }

    private static StringName _resolve_default_member_id(PartyState partyState)
    {
        if (partyState == null)
        {
            return "";
        }
        StringName leaderMemberId = partyState.leader_member_id;
        if (leaderMemberId != "" && partyState.GetMemberState(leaderMemberId) != null)
        {
            return leaderMemberId;
        }
        foreach (var memberId in partyState.active_member_ids)
        {
            if (memberId != "" && partyState.GetMemberState(memberId) != null)
            {
                return memberId;
            }
        }
        return "";
    }

    private List<SettlementResearchMemberAvailability> _build_member_research_availability_entries(
        PartyState partyState,
        bool canAffordResearch,
        string catalogSchemaError,
        IReadOnlyList<SettlementResearchRewardEntry> researchCatalog
    )
    {
        var entries = new List<SettlementResearchMemberAvailability>();
        if (partyState == null)
        {
            return entries;
        }
        foreach (StringName memberId in _collect_rostered_member_ids(partyState))
        {
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            bool hasCandidate = string.IsNullOrEmpty(catalogSchemaError)
                && memberState != null
                && memberState.progression != null
                && _select_research_candidate(partyState, memberState, researchCatalog) != null;
            string disabledReason = "";
            if (!string.IsNullOrEmpty(catalogSchemaError))
            {
                disabledReason = "研究配置无效";
            }
            else if (!canAffordResearch)
            {
                disabledReason = "金币不足";
            }
            else if (!hasCandidate)
            {
                disabledReason = "暂无可研究内容";
            }
            entries.Add(
                new SettlementResearchMemberAvailability(
                    memberId,
                    hasCandidate,
                    canAffordResearch && hasCandidate,
                    disabledReason
                )
            );
        }
        return entries;
    }

    private static SettlementResearchMemberAvailability FindMemberAvailability(
        List<SettlementResearchMemberAvailability> entries,
        StringName memberId
    )
    {
        if (entries == null || memberId == "")
            return null;
        foreach (var entry in entries)
        {
            if (entry.MemberId == memberId)
                return entry;
        }
        return null;
    }

    private static List<StringName> _collect_rostered_member_ids(PartyState partyState)
    {
        var memberIds = new List<StringName>();
        if (partyState == null)
        {
            return memberIds;
        }
        AppendRosterMemberIds(memberIds, partyState.active_member_ids);
        AppendRosterMemberIds(memberIds, partyState.reserve_member_ids);
        return memberIds;
    }

    private static void AppendRosterMemberIds(
        List<StringName> memberIds,
        IEnumerable<StringName> rawIds
    )
    {
        foreach (StringName memberId in rawIds ?? System.Array.Empty<StringName>())
        {
            if (memberId != "" && !memberIds.Contains(memberId))
            {
                memberIds.Add(memberId);
            }
        }
    }

    private SettlementResearchRewardEntry _select_research_candidate(
        PartyState partyState,
        PartyMemberState memberState,
        IReadOnlyList<SettlementResearchRewardEntry> researchCatalog
    )
    {
        if (memberState == null || memberState.progression == null)
        {
            return null;
        }
        StringName memberId = memberState.member_id;
        HashSet<string> reservedTargets = _collect_pending_reward_targets(partyState, memberId);
        UnitProgress progression = memberState.progression;
        foreach (SettlementResearchRewardEntry candidate in researchCatalog ?? new List<SettlementResearchRewardEntry>())
        {
            if (candidate == null || candidate.TargetId == "")
            {
                continue;
            }
            if (reservedTargets.Contains(_build_reward_target_key(candidate.EntryType, candidate.TargetId)))
            {
                continue;
            }
            if (candidate.Kind == SettlementResearchRewardKind.KnowledgeUnlock)
            {
                if (!progression.HasKnowledge(candidate.TargetId))
                {
                    return candidate;
                }
            }
            else if (candidate.Kind == SettlementResearchRewardKind.SkillUnlock)
            {
                UnitSkillProgress skillProgress = progression.GetSkillProgress(candidate.TargetId);
                if (skillProgress == null || !skillProgress.is_learned)
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    private static HashSet<string> _collect_pending_reward_targets(PartyState partyState, StringName memberId)
    {
        var targets = new HashSet<string>(System.StringComparer.Ordinal);
        if (partyState == null || memberId == "")
        {
            return targets;
        }
        foreach (var reward in partyState.pending_character_rewards)
        {
            if (reward == null || reward.member_id != memberId)
            {
                continue;
            }
            foreach (var entry in reward.entries)
            {
                if (entry == null || entry.IsEmpty())
                {
                    continue;
                }
                targets.Add(_build_reward_target_key(entry.entry_type, entry.target_id));
            }
        }
        return targets;
    }

    private static string _build_reward_target_key(StringName entryType, StringName targetId)
    {
        return $"{entryType}|{targetId}";
    }

    private PendingCharacterReward _build_pending_research_reward(
        PartyMemberState memberState,
        SettlementResearchRewardEntry researchCandidate,
        string facilityName,
        string npcName,
        string serviceType)
    {
        if (memberState == null || memberState.progression == null || researchCandidate == null)
        {
            return null;
        }
        StringName targetId = researchCandidate.TargetId;
        string targetLabel = researchCandidate.TargetLabel;
        StringName researchId = researchCandidate.ResearchId;
        StringName entryType = researchCandidate.EntryType;
        string reasonText = researchCandidate.ReasonText;
        string sourceLabel = _build_reward_source_label(facilityName, npcName, serviceType);
        string memberName = _resolve_member_name(memberState);
        StringName memberId = memberState.member_id;
        string summaryText = $"{npcName} 为 {memberName} 整理出新的研究成果：{targetLabel}。";
        var reward = new PendingCharacterReward
        {
            reward_id = new StringName($"{memberId}_{researchId}_reward"),
            member_id = memberId,
            member_name = memberName,
            source_type = ResearchSourceType,
            source_id = researchId,
            source_label = sourceLabel,
            summary_text = summaryText,
        };
        reward.entries.Add(
            new PendingCharacterRewardEntry
            {
                entry_type = entryType,
                target_id = targetId,
                target_label = targetLabel,
                amount = 1,
                reason_text = reasonText,
            }
        );
        return reward;
    }

    private static string _build_reward_source_label(string _facilityName, string npcName, string serviceType)
    {
        return $"{npcName}·{serviceType}";
    }

    private static string _resolve_member_name(PartyMemberState memberState)
    {
        if (memberState == null)
        {
            return "成员";
        }
        string displayName = memberState.display_name;
        return !string.IsNullOrEmpty(displayName) ? displayName : memberState.member_id.ToString();
    }

    private static PendingCharacterRewardEntry _get_first_reward_entry(PendingCharacterReward rewardData)
    {
        if (rewardData == null || rewardData.entries.Count == 0)
            return null;
        return rewardData.entries[0]?.DuplicateState();
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        if (value.VariantType == Variant.Type.String)
        {
            return value.AsString();
        }
        return fallback;
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        string value = ReadString(data, key);
        return !string.IsNullOrEmpty(value) ? new StringName(value) : "";
    }

}
