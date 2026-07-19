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

    internal GDictionary GetSettlementWindowData(string settlement_id = "")
    {
        if (!_owner._has_runtime())
        {
            return new GDictionary();
        }
        string targetId = !string.IsNullOrEmpty(settlement_id)
            ? settlement_id
            : _owner.ResolveCommandSettlementId();
        using GodotProjectionLease<GDictionary> settlementLease =
            _owner.GetSettlementRecordLease(targetId);
        GDictionary settlement = settlementLease.Value;
        if (settlement.Count == 0)
        {
            return new GDictionary();
        }
        using GodotProjectionLease<GDictionary> settlementStateLease =
            _owner._get_or_create_settlement_state(targetId);
        GDictionary settlementState = settlementStateLease.Value;
        return new GDictionary
        {
            ["settlement_id"] = GameRuntimeSettlementCommandHandler.ReadString(settlement, "settlement_id"),
            ["display_name"] = GameRuntimeSettlementCommandHandler.ReadString(settlement, "display_name"),
            ["tier_name"] = GameRuntimeSettlementCommandHandler.ReadString(settlement, "tier_name"),
            ["footprint_size"] = GameRuntimeSettlementCommandHandler.ReadVariant(settlement, "footprint_size"),
            ["faction_id"] = GameRuntimeSettlementCommandHandler.ReadString(settlement, "faction_id"),
            ["facilities"] = GameRuntimeSettlementCommandHandler.ReadVariant(settlement, "facilities"),
            ["available_services"] = _build_service_entries(settlement, settlementState),
            ["service_npcs"] = GameRuntimeSettlementCommandHandler.ReadVariant(settlement, "service_npcs"),
            ["member_options"] = _build_member_options(),
            ["default_member_id"] = _owner.ResolveDefaultSettlementMemberId().ToString(),
            ["state_summary_text"] = _build_settlement_state_summary(settlementState),
            ["feedback_text"] = _build_settlement_window_feedback_text(),
        };
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
            ["services"] = BuildSettlementServiceIdentityFactsPlain(settlement),
        };
    }

    private GDictArray _build_service_entries(GDictionary settlement, GDictionary settlement_state)
    {
        var entries = new GDictArray();
        foreach (
            GDictionary sourceService in GameRuntimeSettlementCommandHandler.Dictionaries(
                GameRuntimeSettlementCommandHandler.ReadArray(settlement, "available_services")
            )
        )
        {
            GDictionary serviceData = sourceService;
            SettlementServiceMetadata metadata = BuildServiceMetadataTyped(
                settlement,
                serviceData,
                settlement_state
            );
            SettlementServiceMetadataProjection.ApplyToServiceData(serviceData, metadata);
            bool isEnabled = metadata.IsEnabled;
            string disabledReason = metadata.DisabledReason.Trim();
            serviceData["state_label"] = _build_service_state_label(isEnabled, disabledReason);
            serviceData["summary_text"] = _build_service_summary_text(serviceData);
            SettlementPanelKind panelKind = _resolve_service_panel_kind(serviceData);
            string panelKindText = SettlementPanelKinds.ToPayloadValue(panelKind);
            if (!string.IsNullOrEmpty(panelKindText))
            {
                serviceData["panel_kind"] = panelKindText;
            }
            entries.Add(serviceData);
        }
        return entries;
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
        if (GameRuntimeSettlementCommandHandler.SHOP_INTERACTION_IDS.Contains(interactionScriptId))
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
        GDictionary service_data,
        GDictionary settlement_state
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
        if (GameRuntimeSettlementCommandHandler.SHOP_INTERACTION_IDS.Contains(interactionScriptId))
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

    private string _build_settlement_state_summary(GDictionary settlement_state)
    {
        WorldMapSettlementStateData stateData =
            WorldMapSettlementStateData.FromDictionary(settlement_state);
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

    internal GDictArray _build_member_options()
    {
        var options = new GDictArray();
        PartyState partyState = _owner.GetPartyState();
        if (partyState == null)
        {
            return options;
        }
        var seenMemberIds = new GDictionary();
        foreach (StringName memberId in partyState.active_member_ids)
        {
            if (
                memberId == ""
                || seenMemberIds.ContainsKey(memberId)
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            seenMemberIds[memberId] = true;
            options.Add(_build_member_option(partyState, memberId, "上阵"));
        }
        foreach (StringName memberId in partyState.reserve_member_ids)
        {
            if (
                memberId == ""
                || seenMemberIds.ContainsKey(memberId)
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            seenMemberIds[memberId] = true;
            options.Add(_build_member_option(partyState, memberId, "替补"));
        }
        return options;
    }

    private GDictionary _build_member_option(
        PartyState party_state,
        StringName member_id,
        string roster_role
    )
    {
        PartyMemberState memberState = party_state.GetMemberState(member_id);
        if (memberState == null)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["member_id"] = member_id.ToString(),
            ["display_name"] = _owner.GetMemberDisplayName(member_id),
            ["roster_role"] = roster_role,
            ["is_leader"] = party_state.leader_member_id == member_id,
            ["current_hp"] = memberState.current_hp,
            ["current_mp"] = memberState.current_mp,
        };
    }

    private IReadOnlyDictionary<string, object> GetSettlementRecordSnapshotPlain(
        string settlementId
    )
    {
        WorldRuntimeData worldData = _owner.Runtime?.GetActiveWorldRuntimeData();
        if (worldData == null || string.IsNullOrEmpty(settlementId))
            return GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();
        foreach (WorldMapSettlementRecordData settlement in worldData.Settlements)
        {
            if (settlement != null && settlement.SettlementId == settlementId)
                return settlement.BuildSaveSnapshotPlain();
        }
        return GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();
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

    internal IReadOnlyList<object> BuildMemberOptionsSnapshotPlain()
    {
        var options = new List<object>();
        PartyState partyState = _owner.GetPartyState();
        if (partyState == null)
            return options;

        var seenMemberIds = new HashSet<StringName>();
        foreach (StringName memberId in partyState.active_member_ids)
        {
            if (
                memberId == ""
                || !seenMemberIds.Add(memberId)
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            options.Add(BuildMemberOptionSnapshotPlain(partyState, memberId, "上阵"));
        }
        foreach (StringName memberId in partyState.reserve_member_ids)
        {
            if (
                memberId == ""
                || !seenMemberIds.Add(memberId)
                || partyState.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            options.Add(BuildMemberOptionSnapshotPlain(partyState, memberId, "替补"));
        }
        return options;
    }

    private IReadOnlyDictionary<string, object> BuildMemberOptionSnapshotPlain(
        PartyState partyState,
        StringName memberId,
        string rosterRole
    )
    {
        PartyMemberState memberState = partyState.GetMemberState(memberId);
        if (memberState == null)
            return GameRuntimeSettlementCommandHandler.EmptyPlainDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["member_id"] = memberId.ToString(),
            ["display_name"] = _owner.GetMemberDisplayName(memberId),
            ["roster_role"] = rosterRole,
            ["is_leader"] = partyState.leader_member_id == memberId,
            ["current_hp"] = memberState.current_hp,
            ["current_mp"] = memberState.current_mp,
        };
    }

    internal static void AppendWindowEntriesPlain(
        List<object> target,
        IReadOnlyDictionary<string, object> context,
        string key
    )
    {
        foreach (object rawEntry in GameRuntimeSettlementCommandHandler.ReadPlainList(context, key))
        {
            if (rawEntry is not IReadOnlyDictionary<string, object> entry)
                continue;
            Dictionary<string, object> copy = RuntimePlainPayload.CloneDictionary(entry);
            if (!copy.ContainsKey("is_enabled"))
                copy["is_enabled"] = false;
            target.Add(copy);
        }
    }

    internal Dictionary<string, object> CloneActiveShopContextPlain() =>
        _owner._has_runtime()
            ? RuntimePlainPayload.CloneDictionary(_owner.Runtime.GetActiveShopContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    internal Dictionary<string, object> CloneActiveContractBoardContextPlain() =>
        _owner._has_runtime()
            ? RuntimePlainPayload.CloneDictionary(_owner.Runtime.GetActiveContractBoardContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    internal Dictionary<string, object> CloneActiveForgeContextPlain() =>
        _owner._has_runtime()
            ? RuntimePlainPayload.CloneDictionary(_owner.Runtime.GetActiveForgeContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    internal Dictionary<string, object> CloneActiveStagecoachContextPlain() =>
        _owner._has_runtime()
            ? RuntimePlainPayload.CloneDictionary(_owner.Runtime.GetActiveStagecoachContextPlain())
            : new Dictionary<string, object>(StringComparer.Ordinal);

    internal static bool WindowDataMatchesPanelKindPlain(
        IReadOnlyDictionary<string, object> context,
        SettlementPanelKind panelKind
    )
    {
        return GameRuntimeSettlementCommandHandler.ReadPlainString(context, "panel_kind")
            == SettlementPanelKinds.ToPayloadValue(panelKind);
    }
}
