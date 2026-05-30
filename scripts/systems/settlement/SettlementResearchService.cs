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

    public bool is_supported_interaction(string interaction_script_id)
    {
        return (interaction_script_id ?? "").StripEdges() == ResearchInteractionId;
    }

    public GDictionary build_service_metadata(PartyState party_state, GDictionary payload = null)
    {
        payload ??= new GDictionary();
        bool canAffordResearch = party_state != null && party_state.can_afford(ResearchGoldCost);
        string catalogSchemaError = _validate_research_catalog_schema();
        GDictionary memberAvailability = _build_member_research_availability(party_state, canAffordResearch, catalogSchemaError);
        StringName requestedMemberId = GdInterop.GetStringName(payload, "member_id");

        bool hasAvailableResearch = false;
        string memberDisabledReason = "";
        if (!GdInterop.IsEmpty(requestedMemberId))
        {
            GDictionary selectedAvailability = GdInterop.GetDictionary(memberAvailability, requestedMemberId.ToString());
            hasAvailableResearch = GdInterop.GetBool(selectedAvailability, "has_available_research", false);
            memberDisabledReason = GdInterop.GetString(selectedAvailability, "disabled_reason", "暂无可研究内容");
        }
        else
        {
            foreach (var availabilityValue in memberAvailability.Values)
            {
                if (availabilityValue.VariantType != Variant.Type.Dictionary)
                {
                    continue;
                }
                if (GdInterop.GetBool(availabilityValue.AsGodotDictionary(), "has_available_research", false))
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

        return new GDictionary
        {
            ["cost_label"] = $"{ResearchGoldCost} 金",
            ["is_enabled"] = isEnabled,
            ["disabled_reason"] = disabledReason,
            ["member_availability"] = memberAvailability,
        };
    }

    public GDictionary execute(GDictionary settlement, GDictionary payload, PartyState party_state, GArray quest_progress_events = null)
    {
        settlement ??= new GDictionary();
        payload ??= new GDictionary();
        quest_progress_events ??= new GArray();

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

        PartyMemberState memberState = _resolve_target_member_state(party_state, payload);
        if (memberState == null || memberState.progression == null)
        {
            return _build_result(false, "当前没有可承接研究的成员。", quest_progress_events);
        }

        GDictionary researchCandidate = _select_research_candidate(party_state, memberState);
        if (researchCandidate.Count == 0)
        {
            return _build_result(false, $"{_resolve_member_name(memberState)} 当前暂无可研究的新内容。", quest_progress_events);
        }

        string facilityName = GdInterop.GetString(payload, "facility_name").StripEdges();
        string npcName = GdInterop.GetString(payload, "npc_name").StripEdges();
        string serviceType = GdInterop.GetString(payload, "service_type").StripEdges();
        GDictionary pendingReward = _build_pending_research_reward(memberState, researchCandidate, facilityName, npcName, serviceType);
        if (pendingReward.Count == 0)
        {
            return _build_result(false, "当前研究成果构造失败。", quest_progress_events);
        }

        string settlementName = GdInterop.GetString(settlement, "display_name").StripEdges();
        GDictionary rewardEntry = _get_first_reward_entry(pendingReward);
        string rewardLabel = GdInterop.GetString(rewardEntry, "target_label").StripEdges();
        if (string.IsNullOrEmpty(rewardLabel))
        {
            return _build_result(false, "当前研究成果构造失败。", quest_progress_events);
        }

        if (!party_state.spend_gold(ResearchGoldCost))
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
            new GArray { pendingReward },
            new GDictionary
            {
                ["research_interaction_id"] = ResearchInteractionId,
                ["gold_spent"] = ResearchGoldCost,
                ["facility_name"] = facilityName,
                ["research_source_id"] = GdInterop.GetString(pendingReward, "source_id"),
                ["research_entry_type"] = GdInterop.GetString(rewardEntry, "entry_type"),
                ["research_target_id"] = GdInterop.GetString(rewardEntry, "target_id"),
            });
    }

    public GArray _get_research_reward_catalog()
    {
        return GetResearchRewardCatalogCore();
    }

    protected virtual GArray GetResearchRewardCatalogCore()
    {
        return DuplicateDictionaryArrayUntyped(ResearchRewardCatalog);
    }

    private GDictionary _build_result(
        bool success,
        string message,
        GArray questProgressEvents,
        bool persistPartyState = false,
        int goldDelta = 0,
        GArray pendingCharacterRewards = null,
        GDictionary serviceSideEffects = null)
    {
        var result = new SettlementServiceResult
        {
            success = success,
            message = message,
            persist_party_state = persistPartyState,
            gold_delta = goldDelta,
            pending_character_rewards = DuplicateDictionaryArrayUntyped(pendingCharacterRewards ?? new GArray()),
            quest_progress_events = DuplicateDictionaryArrayUntyped(questProgressEvents ?? new GArray()),
            service_side_effects = serviceSideEffects != null ? (GDictionary)serviceSideEffects.Duplicate(true) : new GDictionary(),
        };
        return result.to_dictionary();
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
        foreach (var candidateValue in GetResearchRewardCatalog())
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

    private GArray GetResearchRewardCatalog()
    {
        return GetResearchRewardCatalogCore();
    }

    private static PartyMemberState _resolve_target_member_state(PartyState partyState, GDictionary payload)
    {
        if (partyState == null)
        {
            return null;
        }
        StringName requestedMemberId = GdInterop.GetStringName(payload, "member_id");
        if (!GdInterop.IsEmpty(requestedMemberId))
        {
            return partyState.get_member_state(requestedMemberId);
        }
        StringName defaultMemberId = _resolve_default_member_id(partyState);
        return !GdInterop.IsEmpty(defaultMemberId)
            ? partyState.get_member_state(defaultMemberId)
            : null;
    }

    private static StringName _resolve_default_member_id(PartyState partyState)
    {
        if (partyState == null)
        {
            return "";
        }
        StringName leaderMemberId = partyState.leader_member_id;
        if (!GdInterop.IsEmpty(leaderMemberId) && partyState.get_member_state(leaderMemberId) != null)
        {
            return leaderMemberId;
        }
        foreach (var memberId in partyState.active_member_ids)
        {
            if (!GdInterop.IsEmpty(memberId) && partyState.get_member_state(memberId) != null)
            {
                return memberId;
            }
        }
        return "";
    }

    private GDictionary _build_member_research_availability(PartyState partyState, bool canAffordResearch, string catalogSchemaError)
    {
        var availabilityByMember = new GDictionary();
        if (partyState == null)
        {
            return availabilityByMember;
        }
        foreach (StringName memberId in _collect_rostered_member_ids(partyState))
        {
            PartyMemberState memberState = partyState.get_member_state(memberId);
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
            availabilityByMember[memberId.ToString()] = new GDictionary
            {
                ["member_id"] = memberId.ToString(),
                ["has_available_research"] = hasCandidate,
                ["is_enabled"] = canAffordResearch && hasCandidate,
                ["disabled_reason"] = disabledReason,
            };
        }
        return availabilityByMember;
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
            if (!GdInterop.IsEmpty(memberId) && !memberIds.Contains(memberId))
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
        foreach (var candidateValue in GetResearchRewardCatalog())
        {
            if (candidateValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary candidate = (GDictionary)candidateValue.AsGodotDictionary().Duplicate(true);
            StringName entryType = new StringName(GdInterop.GetString(candidate, "entry_type").StripEdges());
            StringName targetId = new StringName(GdInterop.GetString(candidate, "target_id").StripEdges());
            if (GdInterop.IsEmpty(targetId))
            {
                continue;
            }
            if (reservedTargets.ContainsKey(_build_reward_target_key(entryType, targetId)))
            {
                continue;
            }
            if (entryType == PendingCharacterRewardContentRules.ENTRY_KNOWLEDGE_UNLOCK)
            {
                if (!progression.has_knowledge(targetId))
                {
                    return candidate;
                }
            }
            else if (entryType == PendingCharacterRewardContentRules.ENTRY_SKILL_UNLOCK)
            {
                UnitSkillProgress skillProgress = progression.get_skill_progress(targetId);
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
        if (partyState == null || GdInterop.IsEmpty(memberId))
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
                if (entry == null || entry.is_empty())
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

    private GDictionary _build_pending_research_reward(
        PartyMemberState memberState,
        GDictionary researchCandidate,
        string facilityName,
        string npcName,
        string serviceType)
    {
        if (memberState == null || memberState.progression == null || researchCandidate == null || researchCandidate.Count == 0)
        {
            return new GDictionary();
        }
        if (!string.IsNullOrEmpty(_validate_research_candidate_schema(researchCandidate)))
        {
            return new GDictionary();
        }
        string targetId = GdInterop.GetString(researchCandidate, "target_id").StripEdges();
        string targetLabel = GdInterop.GetString(researchCandidate, "target_label").StripEdges();
        string researchId = GdInterop.GetString(researchCandidate, "research_id").StripEdges();
        string entryType = GdInterop.GetString(researchCandidate, "entry_type").StripEdges();
        string reasonText = GdInterop.GetString(researchCandidate, "reason_text").StripEdges();
        string sourceLabel = _build_reward_source_label(facilityName, npcName, serviceType);
        string memberName = _resolve_member_name(memberState);
        string memberId = memberState.member_id.ToString();
        string summaryText = $"{npcName} 为 {memberName} 整理出新的研究成果：{targetLabel}。";
        return new GDictionary
        {
            ["reward_id"] = $"{memberId}_{researchId}_reward",
            ["member_id"] = memberId,
            ["member_name"] = memberName,
            ["source_type"] = ResearchSourceType.ToString(),
            ["source_id"] = researchId,
            ["source_label"] = sourceLabel,
            ["summary_text"] = summaryText,
            ["entries"] = new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = entryType,
                    ["target_id"] = targetId,
                    ["target_label"] = targetLabel,
                    ["amount"] = 1,
                    ["reason_text"] = reasonText,
                },
            },
        };
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

    private static GDictionary _get_first_reward_entry(GDictionary rewardData)
    {
        GArray entries = GdInterop.GetArray(rewardData, "entries");
        if (entries.Count == 0 || entries[0].VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        return (GDictionary)entries[0].AsGodotDictionary().Duplicate(true);
    }

    private static GArray DuplicateDictionaryArrayUntyped(GArray value)
    {
        var result = new GArray();
        if (value == null)
        {
            return result;
        }
        foreach (var entryValue in value)
        {
            if (entryValue.VariantType == Variant.Type.Dictionary)
            {
                result.Add(entryValue.AsGodotDictionary().Duplicate(true));
            }
        }
        return result;
    }
}
