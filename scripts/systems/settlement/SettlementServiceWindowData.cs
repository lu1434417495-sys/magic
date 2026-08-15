using System;
using System.Collections.Generic;
using Godot;

// Detached window input shared by the shop / contract board / forge / stagecoach panels
// (all rendered by ShopWindow). It never holds PartyState, a runtime owner, a Godot
// collection or a property bag: the runtime builder resolves every displayed fact up front,
// the UI only reads it, and submissions travel back as typed requests carrying stable ids.

internal enum SettlementShopActionKind
{
    Buy,
    Sell,
}

// Only the stable ids a submission needs. Prices, stock, quest state, recipes and travel
// costs are re-resolved by the runtime at submit time and never trusted from this snapshot.
internal abstract record SettlementServiceSelectionData;

internal sealed record SettlementShopSelectionData(
    SettlementShopActionKind ActionKind,
    StringName ItemId,
    StringName InstanceId,
    int MaxQuantity
) : SettlementServiceSelectionData;

internal sealed record SettlementContractSelectionData(StringName QuestId)
    : SettlementServiceSelectionData;

internal sealed record SettlementForgeSelectionData(StringName RecipeId)
    : SettlementServiceSelectionData;

internal sealed record SettlementStagecoachSelectionData(StringName TargetSettlementId)
    : SettlementServiceSelectionData;

// Contract-board-only quest facts. They are not part of the submission contract; they exist
// because the headless text snapshot renders provider kind / listing channels / accept
// dialogue per entry, and that surface must stay stable.
internal sealed record SettlementContractEntryFactsData(
    string StateId,
    string ProviderKind,
    IReadOnlyList<string> ListingChannels,
    string AcceptDialogueText,
    StringName ProviderInteractionId,
    StringName LockReasonId,
    bool IsRepeatable
);

internal sealed record SettlementServiceWindowEntryData(
    StringName EntryId,
    string DisplayName,
    string SummaryText,
    string DetailsText,
    string StateLabel,
    string CostLabel,
    bool IsEnabled,
    string DisabledReason,
    SettlementServiceSelectionData Selection,
    SettlementContractEntryFactsData ContractFacts = null
)
{
    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        var snapshot = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["entry_id"] = Text(EntryId),
            ["display_name"] = DisplayName ?? "",
            ["summary_text"] = SummaryText ?? "",
            ["details_text"] = DetailsText ?? "",
            ["state_label"] = StateLabel ?? "",
            ["cost_label"] = CostLabel ?? "",
            ["is_enabled"] = IsEnabled,
            ["disabled_reason"] = DisabledReason ?? "",
        };
        switch (Selection)
        {
            case SettlementShopSelectionData shop:
                snapshot["shop_action"] = shop.ActionKind == SettlementShopActionKind.Sell
                    ? "sell"
                    : "buy";
                snapshot["item_id"] = Text(shop.ItemId);
                snapshot["instance_id"] = Text(shop.InstanceId);
                snapshot["quantity"] = shop.MaxQuantity;
                break;
            case SettlementContractSelectionData contract:
                snapshot["quest_id"] = Text(contract.QuestId);
                break;
            case SettlementForgeSelectionData forge:
                snapshot["recipe_id"] = Text(forge.RecipeId);
                break;
            case SettlementStagecoachSelectionData stagecoach:
                snapshot["target_settlement_id"] = Text(stagecoach.TargetSettlementId);
                snapshot["settlement_id"] = Text(stagecoach.TargetSettlementId);
                break;
        }
        if (ContractFacts != null)
        {
            snapshot["state_id"] = ContractFacts.StateId ?? "";
            snapshot["provider_kind"] = ContractFacts.ProviderKind ?? "";
            snapshot["listing_channels"] = new List<object>(
                ContractFacts.ListingChannels ?? Array.Empty<string>()
            );
            snapshot["accept_dialogue_text"] = ContractFacts.AcceptDialogueText ?? "";
            snapshot["provider_interaction_id"] = Text(ContractFacts.ProviderInteractionId);
            snapshot["lock_reason_id"] = Text(ContractFacts.LockReasonId);
            snapshot["is_repeatable"] = ContractFacts.IsRepeatable;
        }
        return snapshot;
    }

    private static string Text(StringName value) => value == null ? "" : value.ToString();
}

internal sealed record SettlementServiceWindowLabelsData(
    string ConfirmLabel,
    string CancelLabel,
    string EntryTitle,
    string SummaryTitle,
    string StateTitle,
    string CostTitle,
    string DetailsTitle,
    string MemberTitle,
    string EmptyStateLabel,
    string EmptyCostLabel,
    string EmptyDetailsText
)
{
    internal static SettlementServiceWindowLabelsData Empty { get; } =
        new("", "", "", "", "", "", "", "", "", "", "");
}

internal sealed record SettlementServiceConfirmationData(
    StringName QuestId,
    string Text,
    SettlementSubmissionSource Source
);

internal sealed class SettlementServiceWindowData
{
    internal static SettlementServiceWindowData Empty { get; } =
        new(
            "",
            "",
            SettlementPanelKind.None,
            "",
            "",
            "",
            "",
            SettlementServiceWindowLabelsData.Empty,
            false,
            "",
            "",
            "",
            "",
            "",
            "",
            null,
            null,
            "",
            "",
            null
        );

    internal SettlementServiceWindowData(
        StringName settlementId,
        StringName actionId,
        SettlementPanelKind panelKind,
        string title,
        string meta,
        string summaryText,
        string stateSummaryText,
        SettlementServiceWindowLabelsData labels,
        bool showMemberSelector,
        StringName interactionScriptId,
        StringName facilityId,
        string facilityName,
        StringName npcId,
        string npcName,
        string serviceType,
        IEnumerable<SettlementServiceWindowEntryData> entries,
        IEnumerable<SettlementMemberOptionData> memberOptions,
        StringName defaultMemberId,
        StringName selectedMemberId,
        SettlementServiceConfirmationData confirmation
    )
    {
        SettlementId = settlementId ?? "";
        ActionId = actionId ?? "";
        PanelKind = panelKind;
        Title = title ?? "";
        Meta = meta ?? "";
        SummaryText = summaryText ?? "";
        StateSummaryText = stateSummaryText ?? "";
        Labels = labels ?? SettlementServiceWindowLabelsData.Empty;
        ShowMemberSelector = showMemberSelector;
        InteractionScriptId = interactionScriptId ?? "";
        FacilityId = facilityId ?? "";
        FacilityName = facilityName ?? "";
        NpcId = npcId ?? "";
        NpcName = npcName ?? "";
        ServiceType = serviceType ?? "";
        Entries = CopyList(entries);
        MemberOptions = CopyList(memberOptions);
        DefaultMemberId = defaultMemberId ?? "";
        SelectedMemberId = selectedMemberId ?? "";
        Confirmation = confirmation;

        var memberOptionMap = new Dictionary<StringName, SettlementMemberOptionData>();
        foreach (SettlementMemberOptionData option in MemberOptions)
        {
            if (option.MemberId != (StringName)"" && !string.IsNullOrEmpty(option.DisplayName))
                memberOptionMap[option.MemberId] = option;
        }
        MemberOptionMap = memberOptionMap;
    }

    internal StringName SettlementId { get; }

    internal StringName ActionId { get; }

    internal SettlementPanelKind PanelKind { get; }

    internal string Title { get; }

    internal string Meta { get; }

    internal string SummaryText { get; }

    internal string StateSummaryText { get; }

    internal SettlementServiceWindowLabelsData Labels { get; }

    internal bool ShowMemberSelector { get; }

    internal StringName InteractionScriptId { get; }

    internal StringName FacilityId { get; }

    internal string FacilityName { get; }

    internal StringName NpcId { get; }

    internal string NpcName { get; }

    internal string ServiceType { get; }

    internal IReadOnlyList<SettlementServiceWindowEntryData> Entries { get; }

    internal IReadOnlyList<SettlementMemberOptionData> MemberOptions { get; }

    internal IReadOnlyDictionary<StringName, SettlementMemberOptionData> MemberOptionMap { get; }

    internal StringName DefaultMemberId { get; }

    internal StringName SelectedMemberId { get; }

    internal SettlementServiceConfirmationData Confirmation { get; }

    internal bool IsValid =>
        PanelKind != SettlementPanelKind.None
        && SettlementId != (StringName)""
        && ActionId != (StringName)""
        && !string.IsNullOrEmpty(Title);

    internal bool IsEmpty => !IsValid && Entries.Count == 0;

    // Active modal context updates are replace-whole: the runtime never mutates a published
    // DTO, so rollback snapshots can safely keep the previous immutable reference.
    internal SettlementServiceWindowData WithMemberOptions(
        IEnumerable<SettlementMemberOptionData> memberOptions,
        StringName defaultMemberId,
        StringName selectedMemberId
    ) =>
        new(
            SettlementId,
            ActionId,
            PanelKind,
            Title,
            Meta,
            SummaryText,
            StateSummaryText,
            Labels,
            ShowMemberSelector,
            InteractionScriptId,
            FacilityId,
            FacilityName,
            NpcId,
            NpcName,
            ServiceType,
            Entries,
            memberOptions,
            defaultMemberId,
            selectedMemberId,
            Confirmation
        );

    internal SettlementServiceWindowData WithConfirmation(
        SettlementServiceConfirmationData confirmation
    ) =>
        new(
            SettlementId,
            ActionId,
            PanelKind,
            Title,
            Meta,
            SummaryText,
            StateSummaryText,
            Labels,
            ShowMemberSelector,
            InteractionScriptId,
            FacilityId,
            FacilityName,
            NpcId,
            NpcName,
            ServiceType,
            Entries,
            MemberOptions,
            DefaultMemberId,
            SelectedMemberId,
            confirmation
        );

    internal SettlementServiceWindowData WithIdentity(
        StringName settlementId,
        StringName interactionScriptId
    ) =>
        new(
            settlementId,
            ActionId,
            PanelKind,
            Title,
            Meta,
            SummaryText,
            StateSummaryText,
            Labels,
            ShowMemberSelector,
            interactionScriptId,
            FacilityId,
            FacilityName,
            NpcId,
            NpcName,
            ServiceType,
            Entries,
            MemberOptions,
            DefaultMemberId,
            SelectedMemberId,
            Confirmation
        );

    // One-way projection for the headless snapshot surface. It is never read back as a
    // runtime or window owner.
    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        if (IsEmpty)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        var entries = new List<object>();
        foreach (SettlementServiceWindowEntryData entry in Entries)
            entries.Add(entry.BuildSnapshotPlain());

        var memberOptions = new List<object>();
        foreach (SettlementMemberOptionData option in MemberOptions)
        {
            memberOptions.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["member_id"] = Text(option.MemberId),
                    ["display_name"] = option.DisplayName ?? "",
                    ["roster_role"] = option.RosterRole ?? "",
                    ["is_leader"] = option.IsLeader,
                    ["current_hp"] = option.CurrentHp,
                    ["current_mp"] = option.CurrentMp,
                }
            );
        }

        var snapshot = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["settlement_id"] = Text(SettlementId),
            ["action_id"] = Text(ActionId),
            ["panel_kind"] = SettlementPanelKinds.ToPayloadValue(PanelKind),
            ["title"] = Title,
            ["meta"] = Meta,
            ["summary_text"] = SummaryText,
            ["state_summary_text"] = StateSummaryText,
            ["confirm_label"] = Labels.ConfirmLabel ?? "",
            ["cancel_label"] = Labels.CancelLabel ?? "",
            ["entry_title"] = Labels.EntryTitle ?? "",
            ["summary_title"] = Labels.SummaryTitle ?? "",
            ["state_title"] = Labels.StateTitle ?? "",
            ["cost_title"] = Labels.CostTitle ?? "",
            ["details_title"] = Labels.DetailsTitle ?? "",
            ["member_title"] = Labels.MemberTitle ?? "",
            ["empty_state_label"] = Labels.EmptyStateLabel ?? "",
            ["empty_cost_label"] = Labels.EmptyCostLabel ?? "",
            ["empty_details_text"] = Labels.EmptyDetailsText ?? "",
            ["show_member_selector"] = ShowMemberSelector,
            ["interaction_script_id"] = Text(InteractionScriptId),
            ["facility_id"] = Text(FacilityId),
            ["facility_name"] = FacilityName,
            ["npc_id"] = Text(NpcId),
            ["npc_name"] = NpcName,
            ["service_type"] = ServiceType,
            ["default_member_id"] = Text(DefaultMemberId),
            ["selected_member_id"] = Text(SelectedMemberId),
            ["member_options"] = memberOptions,
            ["entries"] = entries,
        };
        if (PanelKind == SettlementPanelKind.ContractBoard)
            snapshot["provider_interaction_id"] = Text(InteractionScriptId);
        if (Confirmation != null)
        {
            snapshot["pending_confirmation_quest_id"] = Text(Confirmation.QuestId);
            snapshot["pending_confirmation_text"] = Confirmation.Text ?? "";
            snapshot["pending_confirmation_source"] = SettlementSubmissionSources.ToPayloadValue(
                Confirmation.Source
            );
        }
        return snapshot;
    }

    private static string Text(StringName value) => value == null ? "" : value.ToString();

    private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> values)
        where T : class
    {
        var copy = new List<T>();
        if (values != null)
        {
            foreach (T value in values)
            {
                if (value != null)
                    copy.Add(value);
            }
        }
        return copy.AsReadOnly();
    }
}
