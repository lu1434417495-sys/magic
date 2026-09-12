using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class GameRuntimeSettlementWindowDataBuilder
{
    private GameRuntimeSettlementCommandHandler _owner;
    private GameRuntimeServiceWindowCommandHandler _serviceWindowHandler;

    internal void Setup(
        GameRuntimeSettlementCommandHandler owner,
        GameRuntimeServiceWindowCommandHandler serviceWindowHandler
    )
    {
        _owner = owner;
        _serviceWindowHandler = serviceWindowHandler;
    }

    internal SettlementOverviewWindowData BuildSettlementOverviewWindowData(
        string settlement_id = ""
    )
    {
        if (!_owner._has_runtime())
        {
            return null;
        }
        string targetId = !string.IsNullOrEmpty(settlement_id)
            ? settlement_id
            : _owner.ResolveCommandSettlementId();
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(targetId);
        GDictionary settlement = settlementLease.Value;
        if (settlement.Count == 0)
        {
            return null;
        }
        WorldMapSettlementStateData settlementState =
            _owner.GetSettlementStateData(targetId);
        if (settlementState == null)
            return null;

        return new SettlementOverviewWindowData(
            GameRuntimeSettlementCommandHandler.ReadString(settlement, "settlement_id").Trim(),
            GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name").Trim(),
            GameRuntimeSettlementCommandHandler.ReadString(settlement, "tier_name").Trim(),
            GameRuntimeSettlementCommandHandler.ReadString(settlement, "faction_id").Trim(),
            GameRuntimeSettlementCommandHandler.ReadString(settlement, "country_id").Trim(),
            _build_settlement_window_feedback_text(),
            _build_settlement_state_summary(settlementState),
            _read_footprint_size(settlement),
            _owner.ResolveDefaultSettlementMemberId(),
            BuildMemberOptionData(),
            _build_facility_entry_data(settlement),
            _build_resident_entry_data(settlement),
            _build_service_entry_data(settlement)
        );
    }

    private static Vector2I _read_footprint_size(GDictionary settlement)
    {
        Variant value = GameRuntimeSettlementCommandHandler.ReadVariant(
            settlement,
            "footprint_size"
        );
        Vector2I footprintSize =
            value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : Vector2I.One;
        return new Vector2I(Mathf.Max(footprintSize.X, 1), Mathf.Max(footprintSize.Y, 1));
    }

    private List<SettlementFacilityEntryData> _build_facility_entry_data(GDictionary settlement)
    {
        var facilities = new List<SettlementFacilityEntryData>();
        foreach (
            GDictionary facility in GameRuntimeSettlementCommandHandler.Dictionaries(
                GameRuntimeSettlementCommandHandler.ReadArray(settlement, "facilities")
            )
        )
        {
            facilities.Add(
                new SettlementFacilityEntryData(
                    GameRuntimeSettlementCommandHandler.ReadString(facility, "facility_id").Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(facility, "display_name").Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(facility, "slot_tag").Trim(),
                    GameRuntimeSettlementCommandHandler
                        .ReadString(facility, "interaction_type")
                        .Trim()
                )
            );
        }
        return facilities;
    }

    private List<SettlementResidentEntryData> _build_resident_entry_data(GDictionary settlement)
    {
        var residents = new List<SettlementResidentEntryData>();
        foreach (
            GDictionary npc in GameRuntimeSettlementCommandHandler.Dictionaries(
                GameRuntimeSettlementCommandHandler.ReadArray(settlement, "service_npcs")
            )
        )
        {
            residents.Add(
                new SettlementResidentEntryData(
                    GameRuntimeSettlementCommandHandler.ReadString(npc, "npc_id").Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(npc, "display_name").Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(npc, "service_type").Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(npc, "facility_name").Trim()
                )
            );
        }
        return residents;
    }

    private List<SettlementServiceEntryData> _build_service_entry_data(GDictionary settlement)
    {
        var entries = new List<SettlementServiceEntryData>();
        foreach (
            GDictionary serviceData in GameRuntimeSettlementCommandHandler.Dictionaries(
                GameRuntimeSettlementCommandHandler.ReadArray(settlement, "available_services")
            )
        )
        {
            SettlementServiceMetadata metadata = BuildServiceMetadataTyped(
                settlement,
                serviceData
            );
            string disabledReason = metadata.DisabledReason.Trim();
            entries.Add(
                new SettlementServiceEntryData(
                    GameRuntimeSettlementCommandHandler.ReadString(serviceData, "action_id").Trim(),
                    GameRuntimeSettlementCommandHandler
                        .ReadString(serviceData, "facility_id")
                        .Trim(),
                    GameRuntimeSettlementCommandHandler
                        .ReadString(serviceData, "facility_name")
                        .Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(serviceData, "npc_id").Trim(),
                    GameRuntimeSettlementCommandHandler.ReadString(serviceData, "npc_name").Trim(),
                    GameRuntimeSettlementCommandHandler
                        .ReadString(serviceData, "service_type")
                        .Trim(),
                    GameRuntimeSettlementCommandHandler
                        .ReadString(serviceData, "interaction_script_id")
                        .Trim(),
                    metadata.CostLabel.Trim(),
                    _build_service_state_label(metadata.IsEnabled, disabledReason),
                    _build_service_summary_text(serviceData),
                    metadata.IsEnabled,
                    disabledReason,
                    _resolve_service_panel_kind(serviceData),
                    _build_member_availability_data(metadata)
                )
            );
        }
        return entries;
    }

    private static List<KeyValuePair<StringName, SettlementMemberAvailabilityData>>
        _build_member_availability_data(SettlementServiceMetadata metadata)
    {
        var result = new List<KeyValuePair<StringName, SettlementMemberAvailabilityData>>();
        foreach (
            SettlementResearchMemberAvailability availability
            in metadata.CloneResearchMemberAvailability()
        )
        {
            if (availability == null || availability.MemberId == "")
                continue;
            result.Add(
                new KeyValuePair<StringName, SettlementMemberAvailabilityData>(
                    availability.MemberId,
                    new SettlementMemberAvailabilityData(
                        availability.IsEnabled,
                        (availability.DisabledReason ?? "").Trim()
                    )
                )
            );
        }
        return result;
    }

    internal List<SettlementMemberOptionData> BuildMemberOptionData()
    {
        var options = new List<SettlementMemberOptionData>();
        PartyState partyState = _owner.GetPartyState();
        if (partyState == null)
            return options;

        var seenMemberIds = new HashSet<StringName>();
        foreach (StringName memberId in partyState.active_member_ids)
            _append_member_option_data(options, seenMemberIds, partyState, memberId, "上阵");
        foreach (StringName memberId in partyState.reserve_member_ids)
            _append_member_option_data(options, seenMemberIds, partyState, memberId, "替补");
        return options;
    }

    private void _append_member_option_data(
        List<SettlementMemberOptionData> options,
        HashSet<StringName> seenMemberIds,
        PartyState partyState,
        StringName memberId,
        string rosterRole
    )
    {
        if (memberId == "" || !seenMemberIds.Add(memberId))
            return;
        PartyMemberState memberState = partyState.GetMemberState(memberId);
        if (memberState == null)
            return;
        options.Add(
            new SettlementMemberOptionData(
                memberId,
                _owner.GetMemberDisplayName(memberId),
                rosterRole,
                partyState.leader_member_id == memberId,
                memberState.current_hp,
                memberState.current_mp
            )
        );
    }

    internal IReadOnlyDictionary<string, object> GetSettlementHeadlessFactsPlain(
        string settlementId
    )
    {
        if (!_owner._has_runtime())
            return GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();

        string targetId = settlementId ?? "";
        if (string.IsNullOrEmpty(targetId))
            return GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();
        IReadOnlyDictionary<string, object> settlement = GetSettlementRecordSnapshotPlain(
            targetId
        );
        if (settlement.Count == 0)
            return GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["settlement_id"] = GameRuntimeSettlementCommandHandler.ReadPlainString(settlement, "settlement_id"),
            ["display_name"] = GameRuntimeSettlementCommandHandler.ReadPlainString(settlement, "display_name"),
            ["tier_name"] = GameRuntimeSettlementCommandHandler.ReadPlainString(settlement, "tier_name"),
            ["faction_id"] = GameRuntimeSettlementCommandHandler.ReadPlainString(settlement, "faction_id"),
            ["country_id"] = GameRuntimeSettlementCommandHandler.ReadPlainString(settlement, "country_id"),
            ["services"] = BuildSettlementServiceIdentityFactsPlain(settlement),
        };
    }

    internal string _build_service_state_label(bool is_enabled, string disabled_reason)
    {
        if (is_enabled)
        {
            return "状态：可用";
        }
        if (!string.IsNullOrEmpty(disabled_reason))
        {
            return $"状态：{disabled_reason}";
        }
        return "状态：不可用";
    }

    internal string _build_service_summary_text(GDictionary service_data)
    {
        return $"{GameRuntimeSettlementCommandHandler.ReadString(service_data, "facility_name").Trim()} · {GameRuntimeSettlementCommandHandler.ReadString(service_data, "npc_name").Trim()} · {GameRuntimeSettlementCommandHandler.ReadString(service_data, "service_type").Trim()}";
    }

    internal SettlementPanelKind _resolve_service_panel_kind(GDictionary service_data)
    {
        string interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(service_data, "interaction_script_id").Trim();
        if (_owner._shop_service.HasShop(interactionScriptId))
        {
            return SettlementPanelKind.Shop;
        }
        if (GameRuntimeSettlementCommandHandler.STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return SettlementPanelKind.Stagecoach;
        }
        if (interactionScriptId == "service_bounty_registry")
        {
            return SettlementPanelKind.BountyBoard;
        }
        if (QuestProviderContentRules.IsSupportedProviderId(interactionScriptId))
        {
            return SettlementPanelKind.ContractBoard;
        }
        if (_serviceWindowHandler._is_forge_interaction(interactionScriptId))
        {
            return SettlementPanelKind.Forge;
        }
        return SettlementPanelKind.None;
    }

    internal SettlementServiceMetadata BuildServiceMetadataTyped(
        GDictionary settlement,
        GDictionary service_data
    )
    {
        string interactionScriptId = GameRuntimeSettlementCommandHandler.ReadString(service_data, "interaction_script_id");
        PartyState typedPartyState = _owner.GetPartyState();
        if (interactionScriptId == "party_warehouse")
        {
            return new SettlementServiceMetadata("免费", true);
        }
        if (interactionScriptId == "service_rest_basic")
        {
            return new SettlementServiceMetadata("免费", true);
        }
        if (interactionScriptId == "service_rest_full")
        {
            bool canAffordRest =
                typedPartyState != null && typedPartyState.CanAfford(GameRuntimeSettlementCommandHandler.REST_FULL_COST);
            return new SettlementServiceMetadata(
                $"{GameRuntimeSettlementCommandHandler.REST_FULL_COST} 金",
                canAffordRest,
                canAffordRest ? "" : "金币不足"
            );
        }
        if (interactionScriptId == "service_village_rumor")
        {
            return new SettlementServiceMetadata("免费", true);
        }
        if (interactionScriptId == "service_intel_network")
        {
            bool canAffordIntel =
                typedPartyState != null && typedPartyState.CanAfford(GameRuntimeSettlementCommandHandler.INTEL_NETWORK_COST);
            return new SettlementServiceMetadata(
                $"{GameRuntimeSettlementCommandHandler.INTEL_NETWORK_COST} 金",
                canAffordIntel,
                canAffordIntel ? "" : "金币不足"
            );
        }
        if (QuestProviderContentRules.IsSupportedProviderId(interactionScriptId))
        {
            return new SettlementServiceMetadata("查看任务", true);
        }
        if (_owner._shop_service.HasShop(interactionScriptId))
        {
            return new SettlementServiceMetadata("按商品计价", true);
        }
        if (GameRuntimeSettlementCommandHandler.STAGECOACH_INTERACTION_IDS.Contains(interactionScriptId))
        {
            List<StagecoachDestinationData> destinations = _serviceWindowHandler.BuildStagecoachDestinationData(
                settlement,
                interactionScriptId
            );
            bool hasDestinations = destinations.Count != 0;
            return new SettlementServiceMetadata(
                $"{GameRuntimeSettlementCommandHandler.STAGECOACH_COST_PER_STEP} 金/格",
                hasDestinations,
                hasDestinations ? "" : "暂无已访问路线"
            );
        }
        if (_serviceWindowHandler._is_forge_interaction(interactionScriptId))
        {
            bool hasRecipe = _owner._forge_service.HasAvailableRecipeTyped(
                settlement,
                service_data,
                _owner._GetItemDefsTyped(),
                _owner.GetRecipeDefsTyped()
            );
            return new SettlementServiceMetadata(
                "按配方材料",
                hasRecipe,
                hasRecipe ? "" : _serviceWindowHandler._build_forge_unavailable_reason(interactionScriptId)
            );
        }
        if (_serviceWindowHandler._is_research_interaction(interactionScriptId))
        {
            return _owner._research_service.BuildServiceMetadataTyped(typedPartyState);
        }
        if (GameRuntimeSettlementCommandHandler.UNIMPLEMENTED_INTERACTION_IDS.Contains(interactionScriptId))
        {
            return new SettlementServiceMetadata("未开放", false, "系统未开放");
        }
        return new SettlementServiceMetadata("", true);
    }

    private string _build_settlement_state_summary(WorldMapSettlementStateData stateData)
    {
        IReadOnlyList<string> conditionStrings = stateData.ActiveConditions;
        return string.Join(
            "\n",
            new[]
            {
                $"访问：{(stateData.Visited ? "是" : "否")}",
                $"声望：{stateData.Reputation}",
                $"活跃条件：{(conditionStrings.Count != 0 ? string.Join("、", conditionStrings) : "无")}",
            }
        );
    }

    private string _build_settlement_window_feedback_text()
    {
        string feedbackText = _owner.GetSettlementFeedbackText().Trim();
        if (!string.IsNullOrEmpty(feedbackText))
        {
            return feedbackText;
        }
        return "点击服务继续，或切换成员后再操作。";
    }

    private IReadOnlyDictionary<string, object> GetSettlementRecordSnapshotPlain(
        string settlementId
    )
    {
        return _owner.GetSettlementRecordSnapshotPlain(settlementId);
    }

    private static IReadOnlyList<object> BuildSettlementServiceIdentityFactsPlain(
        IReadOnlyDictionary<string, object> settlement
    )
    {
        var entries = new List<object>();
        foreach (object rawEntry in GameRuntimeSettlementCommandHandler.ReadPlainList(settlement, "available_services"))
        {
            if (rawEntry is not IReadOnlyDictionary<string, object> entry)
                continue;
            entries.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_id"] = GameRuntimeSettlementCommandHandler.ReadPlainString(entry, "action_id"),
                    ["facility_name"] = GameRuntimeSettlementCommandHandler.ReadPlainString(entry, "facility_name"),
                    ["npc_name"] = GameRuntimeSettlementCommandHandler.ReadPlainString(entry, "npc_name"),
                    ["service_type"] = GameRuntimeSettlementCommandHandler.ReadPlainString(entry, "service_type"),
                    ["interaction_script_id"] = GameRuntimeSettlementCommandHandler.ReadPlainString(
                        entry,
                        "interaction_script_id"
                    ),
                }
            );
        }
        return entries;
    }

}
