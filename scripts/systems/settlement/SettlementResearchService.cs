using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SettlementResearchService : RefCounted
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

    private static readonly string[] RequiredResearchCandidateStringFields =
    {
        "research_id",
        "entry_type",
        "target_id",
        "target_label",
        "reason_text",
    };

    private static readonly GArray ResearchRewardCatalog = new()
    {
        new GDictionary
        {
            ["research_id"] = "research_field_manual",
            ["entry_type"] = "knowledge_unlock",
            ["target_id"] = "field_manual",
            ["target_label"] = "野外手册",
            ["reason_text"] = "研究员整理出一份可长期翻阅的野外手册抄本。",
        },
        new GDictionary
        {
            ["research_id"] = "research_guard_break",
            ["entry_type"] = "skill_unlock",
            ["target_id"] = "warrior_guard_break",
            ["target_label"] = "裂甲斩",
            ["reason_text"] = "研究记录补全了裂甲斩的动作拆解。",
        },
    };

    private sealed class ResearchMemberAvailability
    {
        public readonly StringName MemberId;
        public readonly bool HasAvailableResearch;
        public readonly bool IsEnabled;
        public readonly string DisabledReason;

        public ResearchMemberAvailability(
            StringName memberId,
            bool hasAvailableResearch,
            bool isEnabled,
            string disabledReason
        )
        {
            MemberId = ProgressionDataUtils.to_string_name(memberId);
            HasAvailableResearch = hasAvailableResearch;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason ?? "";
        }

        internal GDictionary ToDictionary() =>
            new()
            {
                ["member_id"] = MemberId.ToString(),
                ["has_available_research"] = HasAvailableResearch,
                ["is_enabled"] = IsEnabled,
                ["disabled_reason"] = DisabledReason,
            };
    }

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
        string catalogSchemaError = _validate_research_catalog_schema();
        List<ResearchMemberAvailability> memberAvailabilityEntries = _build_member_research_availability_entries(party_state, canAffordResearch, catalogSchemaError);
        GDictionary memberAvailability = BuildMemberResearchAvailabilityDictionary(memberAvailabilityEntries);
        StringName requestedMemberId = ReadStringName(payload, "member_id");

        bool hasAvailableResearch = false;
        string memberDisabledReason = "";
        if (requestedMemberId != "")
        {
            ResearchMemberAvailability selectedAvailability = FindMemberAvailability(
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
            new GDictionary { ["member_availability"] = memberAvailability }
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

        string catalogSchemaError = _validate_research_catalog_schema();
        if (!string.IsNullOrEmpty(catalogSchemaError))
        {
            return _build_result(false, catalogSchemaError, quest_progress_events);
        }

        PartyMemberState memberState = _resolve_tarGetMemberState(party_state, payload);
        if (memberState == null || memberState.progression == null)
        {
            return _build_result(false, "当前没有可承接研究的成员。", quest_progress_events);
        }

        GDictionary researchCandidate = _select_research_candidate(party_state, memberState);
        if (researchCandidate.Count == 0)
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
        return DuplicateDictionaryArrayUntyped(ResearchRewardCatalog);
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

    private string _validate_research_catalog_schema()
    {
        int index = 0;
        foreach (var candidateValue in GetResearchRewardCatalogCore())
        {
            if (candidateValue.VariantType != Variant.Type.Dictionary)
            {
                return $"研究候选配置无效：catalog[{index}] 必须是 Dictionary。";
            }
            string candidateError = _validate_research_candidate_schema(candidateValue.AsGodotDictionary(), index);
            if (!string.IsNullOrEmpty(candidateError))
            {
                return candidateError;
            }
            index++;
        }
        return "";
    }

    private string _validate_research_candidate_schema(GDictionary researchCandidate, int index = -1)
    {
        string schemaLabel = index >= 0 ? $"research candidate[{index}]" : "research candidate";
        return _validate_required_string_fields(researchCandidate, RequiredResearchCandidateStringFields, schemaLabel);
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

    private List<ResearchMemberAvailability> _build_member_research_availability_entries(
        PartyState partyState,
        bool canAffordResearch,
        string catalogSchemaError
    )
    {
        var entries = new List<ResearchMemberAvailability>();
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
                && _select_research_candidate(partyState, memberState).Count > 0;
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
                new ResearchMemberAvailability(
                    memberId,
                    hasCandidate,
                    canAffordResearch && hasCandidate,
                    disabledReason
                )
            );
        }
        return entries;
    }

    private static GDictionary BuildMemberResearchAvailabilityDictionary(
        List<ResearchMemberAvailability> entries
    )
    {
        var result = new GDictionary();
        if (entries == null)
            return result;
        foreach (var entry in entries)
            result[entry.MemberId.ToString()] = entry.ToDictionary();
        return result;
    }

    private static ResearchMemberAvailability FindMemberAvailability(
        List<ResearchMemberAvailability> entries,
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

    private static Godot.Collections.Array<StringName> _collect_rostered_member_ids(PartyState partyState)
    {
        var memberIds = new Godot.Collections.Array<StringName>();
        if (partyState == null)
        {
            return memberIds;
        }
        AppendRosterMemberIds(memberIds, partyState.active_member_ids);
        AppendRosterMemberIds(memberIds, partyState.reserve_member_ids);
        return memberIds;
    }

    private static void AppendRosterMemberIds(Godot.Collections.Array<StringName> memberIds, Godot.Collections.Array<StringName> rawIds)
    {
        foreach (var memberId in rawIds)
        {
            if (memberId != "" && !memberIds.Contains(memberId))
            {
                memberIds.Add(memberId);
            }
        }
    }

    private GDictionary _select_research_candidate(PartyState partyState, PartyMemberState memberState)
    {
        if (memberState == null || memberState.progression == null)
        {
            return new GDictionary();
        }
        StringName memberId = memberState.member_id;
        GDictionary reservedTargets = _collect_pending_reward_targets(partyState, memberId);
        UnitProgress progression = memberState.progression;
        foreach (var candidateValue in GetResearchRewardCatalogCore())
        {
            if (candidateValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary candidate = (GDictionary)candidateValue.AsGodotDictionary().Duplicate(true);
            StringName entryType = new StringName(ReadString(candidate, "entry_type").StripEdges());
            StringName targetId = new StringName(ReadString(candidate, "target_id").StripEdges());
            if (targetId == "")
            {
                continue;
            }
            if (reservedTargets.ContainsKey(_build_reward_target_key(entryType, targetId)))
            {
                continue;
            }
            if (entryType == PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.KnowledgeUnlock))
            {
                if (!progression.HasKnowledge(targetId))
                {
                    return candidate;
                }
            }
            else if (entryType == PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillUnlock))
            {
                UnitSkillProgress skillProgress = progression.GetSkillProgress(targetId);
                if (skillProgress == null || !skillProgress.is_learned)
                {
                    return candidate;
                }
            }
        }
        return new GDictionary();
    }

    private static GDictionary _collect_pending_reward_targets(PartyState partyState, StringName memberId)
    {
        var targets = new GDictionary();
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
                targets[_build_reward_target_key(
                    entry.entry_type,
                    entry.target_id)] = true;
            }
        }
        return targets;
    }

    private static StringName _build_reward_target_key(StringName entryType, StringName targetId)
    {
        return new StringName($"{entryType}|{targetId}");
    }

    private PendingCharacterReward _build_pending_research_reward(
        PartyMemberState memberState,
        GDictionary researchCandidate,
        string facilityName,
        string npcName,
        string serviceType)
    {
        if (memberState == null || memberState.progression == null || researchCandidate == null || researchCandidate.Count == 0)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(_validate_research_candidate_schema(researchCandidate)))
        {
            return null;
        }
        StringName targetId = new(ReadString(researchCandidate, "target_id").StripEdges());
        string targetLabel = ReadString(researchCandidate, "target_label").StripEdges();
        StringName researchId = new(ReadString(researchCandidate, "research_id").StripEdges());
        StringName entryType = new(ReadString(researchCandidate, "entry_type").StripEdges());
        string reasonText = ReadString(researchCandidate, "reason_text").StripEdges();
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

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GArray DuplicateDictionaryArrayUntyped(GArray value)
    {
        var result = new GArray();
        foreach (GDictionary entryData in DuplicateDictionaryEntries(value))
        {
            result.Add(entryData);
        }
        return result;
    }

    private static List<GDictionary> DuplicateDictionaryEntries(GArray value)
    {
        var result = new List<GDictionary>();
        if (value == null)
        {
            return result;
        }
        foreach (var entryValue in value)
        {
            if (entryValue.VariantType == Variant.Type.Dictionary)
            {
                result.Add((GDictionary)entryValue.AsGodotDictionary().Duplicate(true));
            }
        }
        return result;
    }
}
