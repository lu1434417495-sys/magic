using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class ShopWindow : Control
{
    [Signal]
    public delegate void action_requestedEventHandler(
        string settlement_id,
        string action_id,
        GDictionary payload
    );

    [Signal]
    public delegate void closedEventHandler();

    public ColorRect shade;
    public Label title_label;
    public Label meta_label;
    public Label entry_title_label;
    public Label summary_label;
    public Label summary_title_label;
    public ItemList entry_list;
    public RichTextLabel details_label;
    public Label state_label;
    public Label state_title_label;
    public Label cost_label;
    public Label cost_title_label;
    public Label details_title_label;
    public OptionButton member_selector;
    public Label member_title_label;
    public Label member_state_label;
    public Button confirm_button;
    public Button cancel_button;
    public Button close_button;

    private ShopWindowData _windowData = ShopWindowData.Empty();
    private string _settlementId = "";
    private string _actionId = "";
    private int _selectedEntryIndex = -1;
    private StringName _selectedMemberId = "";
    private readonly List<StringName> _memberOptionIds = new();
    private bool _isShowingConfirmation = false;

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        meta_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        entry_title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/EntryColumn/EntryTitle"
        );
        summary_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/SummaryLabel"
        );
        summary_title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/SummaryTitle"
        );
        entry_list = GetNode<ItemList>(
            "CenterContainer/Panel/MarginContainer/Content/Body/EntryColumn/EntryList"
        );
        details_label = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/DetailsLabel"
        );
        state_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StateLabel"
        );
        state_title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StateTitle"
        );
        cost_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/CostLabel"
        );
        cost_title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/CostTitle"
        );
        details_title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/DetailsTitle"
        );
        member_selector = GetNode<OptionButton>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/MemberSelector"
        );
        member_title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/MemberTitle"
        );
        member_state_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/MemberStateLabel"
        );
        confirm_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Footer/ConfirmButton"
        );
        cancel_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Footer/CancelButton"
        );
        close_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Header/CloseButton"
        );

        HideWindow();
        shade.GuiInput += _on_shade_gui_input;
        entry_list.ItemSelected += index => _on_entry_selected((int)index);
        member_selector.ItemSelected += index => _on_member_selected((int)index);
        confirm_button.Pressed += _on_confirm_button_pressed;
        cancel_button.Pressed += _on_cancel_button_pressed;
        close_button.Pressed += _close_window;
    }

    public void ShowShop(GDictionary window_data)
    {
        ShopWindowData normalized = ShopWindowData.From(window_data);
        if (normalized == null)
        {
            HideWindow();
            return;
        }

        _windowData = normalized;
        _settlementId = _windowData.SettlementId;
        _actionId = _windowData.ActionId;
        _selectedEntryIndex = -1;
        _selectedMemberId = _resolve_default_member_id();
        Visible = true;
        RefreshView();
    }

    public void ShowStagecoach(GDictionary window_data)
    {
        if (window_data == null || !HasString(window_data, "panel_kind"))
        {
            HideWindow();
            return;
        }
        string panelKindText = window_data["panel_kind"].AsString().StripEdges();
        if (
            !SettlementPanelKinds.TryParse(panelKindText, out SettlementPanelKind panelKind)
            || panelKind != SettlementPanelKind.Stagecoach
        )
        {
            HideWindow();
            return;
        }
        ShowShop(window_data);
    }

    public void HideWindow()
    {
        Visible = false;
        _isShowingConfirmation = false;
        _windowData = ShopWindowData.Empty();
        _settlementId = "";
        _actionId = "";
        _selectedEntryIndex = -1;
        _selectedMemberId = "";

        if (title_label != null)
            title_label.Text = "";
        if (meta_label != null)
            meta_label.Text = "";
        if (entry_title_label != null)
            entry_title_label.Text = "";
        if (summary_title_label != null)
            summary_title_label.Text = "";
        if (state_title_label != null)
            state_title_label.Text = "";
        if (cost_title_label != null)
            cost_title_label.Text = "";
        if (details_title_label != null)
            details_title_label.Text = "";
        if (member_title_label != null)
            member_title_label.Text = "";
        entry_list?.Clear();
        member_selector?.Clear();
        if (summary_label != null)
            summary_label.Text = "";
        if (details_label != null)
            details_label.Text = "";
        if (state_label != null)
            state_label.Text = "";
        if (cost_label != null)
            cost_label.Text = "";
        if (member_state_label != null)
            member_state_label.Text = "";
        if (confirm_button != null)
        {
            confirm_button.Text = "";
            confirm_button.Disabled = true;
        }
        if (cancel_button != null)
            cancel_button.Text = "";
    }

    public void RefreshView()
    {
        title_label.Text = _windowData.Title;
        meta_label.Text = _build_meta_text();
        summary_label.Text = _windowData.SummaryText;
        _apply_section_titles();
        _rebuild_entry_list();
        _build_member_selector();
        _select_entry(_selectedEntryIndex >= 0 ? _selectedEntryIndex : 0);
        _refresh_member_state();
        _refresh_details();
        _refresh_controls();
        if (_windowData.PendingConfirmationQuestId != (StringName)"")
            _show_confirmation_panel();
        else
            _hide_confirmation_panel();
    }

    private string _build_meta_text()
    {
        if (!string.IsNullOrEmpty(_windowData.StateSummaryText))
            return $"{_windowData.Meta}\n{_windowData.StateSummaryText}";
        return _windowData.Meta;
    }

    private void _build_member_selector()
    {
        if (!_windowData.ShowMemberSelector)
        {
            member_selector.Clear();
            _memberOptionIds.Clear();
            member_title_label.Visible = false;
            member_selector.Visible = false;
            member_state_label.Visible = false;
            return;
        }

        member_title_label.Visible = true;
        member_selector.Visible = true;
        member_state_label.Visible = true;
        member_selector.Clear();
        _memberOptionIds.Clear();
        for (int index = 0; index < _windowData.MemberOptions.Count; index++)
        {
            MemberOption option = _windowData.MemberOptions[index];
            member_selector.AddItem(option.BuildLabel());
            _memberOptionIds.Add(option.MemberId);
        }

        member_selector.Visible = _windowData.MemberOptions.Count > 0;
        member_state_label.Visible = true;

        StringName selectedMemberId = _resolve_default_member_id();
        if (selectedMemberId == (StringName)"" && _windowData.MemberOptions.Count > 0)
            selectedMemberId = _windowData.MemberOptions[0].MemberId;
        _select_member(selectedMemberId);
    }

    private StringName _resolve_default_member_id()
    {
        return _windowData.ResolveDefaultMemberId();
    }

    private void _select_member(StringName member_id)
    {
        _selectedMemberId =
            member_id != (StringName)"" && _windowData.MemberOptionMap.ContainsKey(member_id)
                ? member_id
                : "";

        for (int index = 0; index < member_selector.GetItemCount(); index++)
        {
            if (index < _memberOptionIds.Count && _memberOptionIds[index] == _selectedMemberId)
            {
                member_selector.Select(index);
                break;
            }
        }
    }

    private void _refresh_member_state()
    {
        if (!_windowData.ShowMemberSelector)
        {
            member_state_label.Text = "";
            member_state_label.Visible = false;
            return;
        }
        if (_windowData.MemberOptions.Count == 0)
        {
            member_state_label.Text = "成员：暂无可用成员。";
            return;
        }
        if (_selectedMemberId == (StringName)"")
        {
            member_state_label.Text = "成员：请选择一名成员。";
            return;
        }
        if (
            !_windowData.MemberOptionMap.TryGetValue(_selectedMemberId, out MemberOption option)
            || string.IsNullOrEmpty(option.DisplayName)
        )
        {
            member_state_label.Text = "成员：当前选择不可用。";
            return;
        }

        var lines = new List<string>
        {
            $"成员：{option.DisplayName}",
            $"编组：{option.RosterRole}",
            $"HP {option.CurrentHp} / MP {option.CurrentMp}",
        };
        if (option.IsLeader)
            lines.Add("状态：当前队长");
        if (!string.IsNullOrEmpty(_windowData.StateSummaryText))
            lines.Add(_windowData.StateSummaryText);
        member_state_label.Text = string.Join("\n", lines);
    }

    private void _rebuild_entry_list()
    {
        entry_list.Clear();
        for (int index = 0; index < _windowData.Entries.Count; index++)
        {
            ShopEntry entry = _windowData.Entries[index];
            entry_list.AddItem(_build_entry_label(entry));
        }
    }

    private static string _build_entry_label(ShopEntry entry)
    {
        string label = $"{entry.DisplayName}\n{entry.StateLabel}  |  {entry.CostLabel}";
        if (!entry.IsEnabled && !string.IsNullOrEmpty(entry.DisabledReason))
            label += $"\n{entry.DisabledReason}";
        return label;
    }

    private void _select_entry(int index)
    {
        if (_windowData.Entries.Count == 0)
        {
            _selectedEntryIndex = -1;
            return;
        }
        if (index < 0 || index >= _windowData.Entries.Count)
            index = 0;
        _selectedEntryIndex = index;
        entry_list.DeselectAll();
        entry_list.Select(index);
    }

    private void _refresh_details()
    {
        if (_windowData.Entries.Count == 0)
        {
            state_label.Text = _windowData.EmptyStateLabel;
            cost_label.Text = _windowData.EmptyCostLabel;
            details_label.Text = _windowData.EmptyDetailsText;
            confirm_button.Disabled = true;
            return;
        }

        ShopEntry entry = _get_selected_entry();
        state_label.Text = entry.StateLabel;
        cost_label.Text = entry.CostLabel;
        details_label.Text = _build_entry_details(entry);
    }

    private string _build_entry_details(ShopEntry entry)
    {
        var lines = new List<string>
        {
            $"条目：{entry.DisplayName}",
            $"摘要：{entry.SummaryText}",
            $"说明：{entry.DetailsText}",
            $"状态：{entry.StateLabel}",
            $"费用：{entry.CostLabel}",
        };
        if (!string.IsNullOrEmpty(entry.DisabledReason))
            lines.Add($"不可用原因：{entry.DisabledReason}");
        if (_windowData.ShowMemberSelector)
        {
            string selectedMemberDisplayName =
                _selectedMemberId != (StringName)""
                    ? _get_selected_member_display_name(_selectedMemberId)
                    : "";
            lines.Add(
                $"当前成员：{(!string.IsNullOrEmpty(selectedMemberDisplayName) ? selectedMemberDisplayName : "未选择")}"
            );
        }
        return string.Join("\n", lines);
    }

    private void _refresh_controls()
    {
        bool hasMember =
            _selectedMemberId != (StringName)""
            && _windowData.MemberOptionMap.ContainsKey(_selectedMemberId);
        bool hasEntry = _windowData.Entries.Count > 0;
        bool entryEnabled = hasEntry && _get_selected_entry().IsEnabled;
        confirm_button.Disabled = (_windowData.ShowMemberSelector && !hasMember) || !entryEnabled;
        member_selector.Disabled = _windowData.MemberOptions.Count == 0;
    }

    private string _get_selected_member_display_name(StringName member_id)
    {
        return _windowData.MemberOptionMap.TryGetValue(member_id, out MemberOption option)
            ? option.DisplayName
            : "";
    }

    private GDictionary _build_confirm_payload()
    {
        ShopEntry entry = _get_selected_entry();
        GDictionary payload = RuntimePlainPayload.ProjectDictionary(
            entry.Payload,
            "ShopWindow.confirm_payload"
        );
        string panelKind = SettlementPanelKinds.ToPayloadValue(_windowData.PanelKind);
        string submissionSource = SettlementSubmissionSources.ToPayloadValue(
            SettlementSubmissionSources.FromPanelKind(_windowData.PanelKind)
        );
        payload["settlement_id"] = _settlementId;
        payload["action_id"] = _actionId;
        payload["interaction_script_id"] = FirstNonEmpty(
            _windowData.InteractionScriptId,
            DictString(payload, "interaction_script_id", "")
        );
        payload["facility_id"] = FirstNonEmpty(
            _windowData.FacilityId,
            DictString(payload, "facility_id", "")
        );
        payload["facility_name"] = FirstNonEmpty(
            _windowData.FacilityName,
            DictString(payload, "facility_name", "")
        );
        payload["npc_id"] = FirstNonEmpty(_windowData.NpcId, DictString(payload, "npc_id", ""));
        payload["npc_name"] = FirstNonEmpty(
            _windowData.NpcName,
            DictString(payload, "npc_name", "")
        );
        payload["service_type"] = FirstNonEmpty(
            _windowData.ServiceType,
            DictString(payload, "service_type", "")
        );
        payload["member_id"] = _selectedMemberId.ToString();
        payload["default_member_id"] = _selectedMemberId.ToString();
        payload["submission_source"] = submissionSource;
        payload["panel_kind"] = panelKind;
        payload["state_summary_text"] = _windowData.StateSummaryText;
        return payload;
    }

    private void _on_entry_selected(int index)
    {
        _select_entry(index);
        _refresh_details();
        _refresh_controls();
    }

    private void _on_member_selected(int index)
    {
        if (index < 0 || index >= _memberOptionIds.Count)
            _selectedMemberId = "";
        else
            _selectedMemberId = _memberOptionIds[index];
        _refresh_member_state();
        _refresh_details();
        _refresh_controls();
    }

    private void _show_confirmation_panel()
    {
        _isShowingConfirmation = true;
        confirm_button.Text = "确认";
        cancel_button.Text = "返回";
        details_label.Text = _windowData.PendingConfirmationText;
    }

    private void _hide_confirmation_panel()
    {
        _isShowingConfirmation = false;
        confirm_button.Text = _windowData.ConfirmLabel;
        cancel_button.Text = _windowData.CancelLabel;
        _refresh_details();
    }

    private void _on_confirm_button_pressed()
    {
        if (confirm_button.Disabled)
            return;
        if (
            _windowData.PendingConfirmationQuestId != (StringName)""
            && !_isShowingConfirmation
        )
        {
            _show_confirmation_panel();
            return;
        }

        GDictionary payload = _build_confirm_payload();
        if (_windowData.PendingConfirmationQuestId != (StringName)"")
            payload["confirm_accept"] = true;

        string settlementId = _settlementId;
        string actionId = _actionId;
        HideWindow();
        EmitSignal(SignalName.action_requested, settlementId, actionId, payload);
    }

    private void _on_cancel_button_pressed()
    {
        if (!Visible)
            return;
        if (_isShowingConfirmation)
        {
            _hide_confirmation_panel();
            return;
        }
        HideWindow();
        EmitSignal(SignalName.closed);
    }

    private void _close_window()
    {
        _on_cancel_button_pressed();
    }

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
            return;
        if (mouseEvent.ButtonIndex != MouseButton.Left)
            return;
        _on_cancel_button_pressed();
    }

    private ShopEntry _get_selected_entry()
    {
        if (_selectedEntryIndex >= 0 && _selectedEntryIndex < _windowData.Entries.Count)
            return _windowData.Entries[_selectedEntryIndex];
        return _windowData.Entries[0];
    }

    private void _apply_section_titles()
    {
        entry_title_label.Text = _windowData.EntryTitle;
        summary_title_label.Text = _windowData.SummaryTitle;
        state_title_label.Text = _windowData.StateTitle;
        cost_title_label.Text = _windowData.CostTitle;
        details_title_label.Text = _windowData.DetailsTitle;
        member_title_label.Text = _windowData.MemberTitle;
    }

    private sealed class ShopWindowData
    {
        public string SettlementId { get; private init; } = "";
        public string ActionId { get; private init; } = "";
        public SettlementPanelKind PanelKind { get; private init; } = SettlementPanelKind.None;
        public string Title { get; private init; } = "";
        public string Meta { get; private init; } = "";
        public string SummaryText { get; private init; } = "";
        public string ConfirmLabel { get; private init; } = "";
        public string CancelLabel { get; private init; } = "";
        public string EntryTitle { get; private init; } = "";
        public string SummaryTitle { get; private init; } = "";
        public string StateTitle { get; private init; } = "";
        public string CostTitle { get; private init; } = "";
        public string DetailsTitle { get; private init; } = "";
        public string MemberTitle { get; private init; } = "";
        public string EmptyStateLabel { get; private init; } = "";
        public string EmptyCostLabel { get; private init; } = "";
        public string EmptyDetailsText { get; private init; } = "";
        public string StateSummaryText { get; private init; } = "";
        public bool ShowMemberSelector { get; private init; }
        public string InteractionScriptId { get; private init; } = "";
        public string FacilityId { get; private init; } = "";
        public string FacilityName { get; private init; } = "";
        public string NpcId { get; private init; } = "";
        public string NpcName { get; private init; } = "";
        public string ServiceType { get; private init; } = "";
        public PartyState PartyState { get; private init; }
        public List<ShopEntry> Entries { get; private init; } = new();
        public List<MemberOption> MemberOptions { get; private init; } = new();
        public Dictionary<StringName, MemberOption> MemberOptionMap { get; private init; } = new();
        public StringName ExplicitDefaultMemberId { get; private init; } = "";
        public StringName SelectedMemberId { get; private init; } = "";
        public StringName PendingConfirmationQuestId { get; private init; } = "";
        public string PendingConfirmationText { get; private init; } = "";
        public string PendingConfirmationSource { get; private init; } = "";

        public static ShopWindowData Empty() => new();

        public static ShopWindowData From(GDictionary data)
        {
            if (data == null)
                return null;
            foreach (string fieldName in RequiredStringFields)
            {
                if (!HasNonEmptyString(data, fieldName))
                    return null;
            }
            if (!HasString(data, "state_summary_text"))
                return null;
            if (
                !HasBool(data, "show_member_selector")
            )
                return null;

            string panelKindText = data["panel_kind"].AsString().StripEdges();
            if (
                !SettlementPanelKinds.TryParse(panelKindText, out SettlementPanelKind panelKind)
                || panelKind == SettlementPanelKind.None
            )
                return null;
            List<ShopEntry> entries = BuildEntries(data);
            if (entries == null)
                return null;

            PartyState partyState = GetPartyState(data);
            List<MemberOption> memberOptions = BuildMemberOptions(data, partyState);
            if (memberOptions == null)
                return null;
            Dictionary<StringName, MemberOption> memberMap = BuildMemberOptionMap(memberOptions);

            return new ShopWindowData
            {
                SettlementId = data["settlement_id"].AsString().StripEdges(),
                ActionId = data["action_id"].AsString().StripEdges(),
                PanelKind = panelKind,
                Title = data["title"].AsString().StripEdges(),
                Meta = data["meta"].AsString().StripEdges(),
                SummaryText = data["summary_text"].AsString().StripEdges(),
                ConfirmLabel = data["confirm_label"].AsString().StripEdges(),
                CancelLabel = data["cancel_label"].AsString().StripEdges(),
                EntryTitle = data["entry_title"].AsString().StripEdges(),
                SummaryTitle = data["summary_title"].AsString().StripEdges(),
                StateTitle = data["state_title"].AsString().StripEdges(),
                CostTitle = data["cost_title"].AsString().StripEdges(),
                DetailsTitle = data["details_title"].AsString().StripEdges(),
                MemberTitle = data["member_title"].AsString().StripEdges(),
                EmptyStateLabel = data["empty_state_label"].AsString().StripEdges(),
                EmptyCostLabel = data["empty_cost_label"].AsString().StripEdges(),
                EmptyDetailsText = data["empty_details_text"].AsString().StripEdges(),
                StateSummaryText = data["state_summary_text"].AsString(),
                ShowMemberSelector = DictBool(data, "show_member_selector", false),
                InteractionScriptId = OptionalString(data, "interaction_script_id"),
                FacilityId = OptionalString(data, "facility_id"),
                FacilityName = OptionalString(data, "facility_name"),
                NpcId = OptionalString(data, "npc_id"),
                NpcName = OptionalString(data, "npc_name"),
                ServiceType = OptionalString(data, "service_type"),
                PartyState = partyState,
                Entries = entries,
                MemberOptions = memberOptions,
                MemberOptionMap = memberMap,
                ExplicitDefaultMemberId = DictStringName(data, "default_member_id"),
                SelectedMemberId = DictStringName(data, "selected_member_id"),
                PendingConfirmationQuestId = DictStringName(data, "pending_confirmation_quest_id"),
                PendingConfirmationText = DictString(data, "pending_confirmation_text", ""),
                PendingConfirmationSource = DictString(data, "pending_confirmation_source", ""),
            };
        }

        public StringName ResolveDefaultMemberId()
        {
            if (
                ExplicitDefaultMemberId != (StringName)""
                && MemberOptionMap.ContainsKey(ExplicitDefaultMemberId)
            )
                return ExplicitDefaultMemberId;
            if (SelectedMemberId != (StringName)"" && MemberOptionMap.ContainsKey(SelectedMemberId))
                return SelectedMemberId;
            if (PartyState != null)
            {
                if (
                    PartyState.leader_member_id != (StringName)""
                    && MemberOptionMap.ContainsKey(PartyState.leader_member_id)
                )
                    return PartyState.leader_member_id;
                foreach (StringName memberId in PartyState.active_member_ids)
                {
                    StringName normalized = ProgressionDataUtils.to_string_name(memberId);
                    if (normalized != (StringName)"" && MemberOptionMap.ContainsKey(normalized))
                        return normalized;
                }
                foreach (StringName memberId in PartyState.reserve_member_ids)
                {
                    StringName normalized = ProgressionDataUtils.to_string_name(memberId);
                    if (normalized != (StringName)"" && MemberOptionMap.ContainsKey(normalized))
                        return normalized;
                }
            }
            foreach (MemberOption option in MemberOptions)
            {
                if (option.MemberId != (StringName)"")
                    return option.MemberId;
            }
            return "";
        }

        private static readonly string[] RequiredStringFields =
        {
            "settlement_id",
            "action_id",
            "panel_kind",
            "title",
            "meta",
            "summary_text",
            "confirm_label",
            "cancel_label",
            "entry_title",
            "summary_title",
            "state_title",
            "cost_title",
            "details_title",
            "member_title",
            "empty_state_label",
            "empty_cost_label",
            "empty_details_text",
        };
    }

    private sealed class ShopEntry
    {
        public string EntryId { get; private init; } = "";
        public string DisplayName { get; private init; } = "";
        public string SummaryText { get; private init; } = "";
        public string DetailsText { get; private init; } = "";
        public string StateLabel { get; private init; } = "";
        public string CostLabel { get; private init; } = "";
        public bool IsEnabled { get; private init; }
        public string DisabledReason { get; private init; } = "";
        public Dictionary<string, object> Payload { get; private init; } = new();

        public static ShopEntry From(GDictionary data)
        {
            if (data == null)
                return null;
            foreach (
                string fieldName in new[]
                {
                    "entry_id",
                    "display_name",
                    "summary_text",
                    "details_text",
                    "state_label",
                    "cost_label",
                }
            )
            {
                if (!HasNonEmptyString(data, fieldName))
                    return null;
            }
            if (
                !HasBool(data, "is_enabled")
            )
                return null;
            if (!HasString(data, "disabled_reason"))
                return null;

            bool isEnabled = DictBool(data, "is_enabled", false);
            string disabledReason = StrictString(data, "disabled_reason").StripEdges();
            if (!isEnabled && string.IsNullOrEmpty(disabledReason))
                return null;

            Dictionary<string, object> payload = RuntimePlainPayload.NormalizeDictionary(
                data,
                "ShopWindow.ShopEntry"
            );
            string entryId = data["entry_id"].AsString().StripEdges();
            string displayName = data["display_name"].AsString().StripEdges();
            string summaryText = data["summary_text"].AsString().StripEdges();
            string detailsText = data["details_text"].AsString().StripEdges();
            string stateLabel = data["state_label"].AsString().StripEdges();
            string costLabel = data["cost_label"].AsString().StripEdges();
            payload["entry_id"] = entryId;
            payload["display_name"] = displayName;
            payload["summary_text"] = summaryText;
            payload["details_text"] = detailsText;
            payload["state_label"] = stateLabel;
            payload["cost_label"] = costLabel;
            payload["is_enabled"] = isEnabled;
            payload["disabled_reason"] = disabledReason;

            return new ShopEntry
            {
                EntryId = entryId,
                DisplayName = displayName,
                SummaryText = summaryText,
                DetailsText = detailsText,
                StateLabel = stateLabel,
                CostLabel = costLabel,
                IsEnabled = isEnabled,
                DisabledReason = disabledReason,
                Payload = payload,
            };
        }
    }

    private sealed class MemberOption
    {
        public StringName MemberId { get; private init; } = "";
        public string DisplayName { get; private init; } = "";
        public string RosterRole { get; private init; } = "";
        public bool IsLeader { get; private init; }
        public int CurrentHp { get; private init; }
        public int CurrentMp { get; private init; }

        public string BuildLabel()
        {
            if (string.IsNullOrEmpty(DisplayName))
                return "";
            string prefix = IsLeader ? "队长 · " : "";
            string roleSuffix = !string.IsNullOrEmpty(RosterRole) ? $" · {RosterRole}" : "";
            return $"{prefix}{DisplayName}{roleSuffix}  |  HP {CurrentHp}  MP {CurrentMp}";
        }

        public static MemberOption From(GDictionary data)
        {
            if (data == null)
                return null;
            StringName memberId = DictStringName(data, "member_id");
            if (memberId == (StringName)"")
                return null;
            string displayName = StrictString(data, "display_name").StripEdges();
            if (string.IsNullOrEmpty(displayName))
                return null;
            return new MemberOption
            {
                MemberId = memberId,
                DisplayName = displayName,
                RosterRole = DictString(data, "roster_role", ""),
                IsLeader = DictBool(data, "is_leader", false),
                CurrentHp = DictInt(data, "current_hp", 0),
                CurrentMp = DictInt(data, "current_mp", 0),
            };
        }

        public static MemberOption FromParty(
            PartyState partyState,
            StringName memberId,
            string defaultRole
        )
        {
            if (partyState == null || memberId == (StringName)"")
                return null;
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            if (memberState == null)
                return null;
            string displayName = memberState.display_name.StripEdges();
            if (string.IsNullOrEmpty(displayName))
                return null;
            return new MemberOption
            {
                MemberId = memberId,
                DisplayName = displayName,
                RosterRole = defaultRole,
                IsLeader = partyState.leader_member_id == memberId,
                CurrentHp = memberState.current_hp,
                CurrentMp = memberState.current_mp,
            };
        }
    }

    private static List<ShopEntry> BuildEntries(GDictionary data)
    {
        if (!HasArray(data, "entries"))
            return null;
        var entries = new List<ShopEntry>();
        foreach (Variant entryValue in ReadArray(data, "entries"))
        {
            if (!entryValue.TryAsDictionary(out GDictionary entryData))
                return null;
            ShopEntry entry = ShopEntry.From(entryData);
            if (entry == null)
                return null;
            entries.Add(entry);
        }
        return entries;
    }

    private static List<MemberOption> BuildMemberOptions(GDictionary data, PartyState partyState)
    {
        var options = new List<MemberOption>();
        if (data.ContainsKey("member_options"))
        {
            if (!HasArray(data, "member_options"))
                return null;
            foreach (Variant optionValue in ReadArray(data, "member_options"))
            {
                if (!optionValue.TryAsDictionary(out GDictionary optionData))
                    return null;
                MemberOption option = MemberOption.From(optionData);
                if (option == null)
                    return null;
                options.Add(option);
            }
            return options;
        }

        if (partyState == null)
            return options;
        var seenIds = new HashSet<string>();
        foreach (StringName memberId in partyState.active_member_ids)
            AppendMemberOption(
                options,
                seenIds,
                partyState,
                ProgressionDataUtils.to_string_name(memberId),
                "上阵"
            );
        foreach (StringName memberId in partyState.reserve_member_ids)
            AppendMemberOption(
                options,
                seenIds,
                partyState,
                ProgressionDataUtils.to_string_name(memberId),
                "替补"
            );
        return options;
    }

    private static void AppendMemberOption(
        List<MemberOption> options,
        HashSet<string> seenIds,
        PartyState partyState,
        StringName memberId,
        string role
    )
    {
        string key = memberId.ToString();
        if (string.IsNullOrEmpty(key) || seenIds.Contains(key))
            return;
        MemberOption option = MemberOption.FromParty(partyState, memberId, role);
        if (option == null)
            return;
        seenIds.Add(key);
        options.Add(option);
    }

    private static Dictionary<StringName, MemberOption> BuildMemberOptionMap(
        List<MemberOption> options
    )
    {
        var result = new Dictionary<StringName, MemberOption>();
        foreach (MemberOption option in options)
        {
            if (option.MemberId != (StringName)"" && !string.IsNullOrEmpty(option.DisplayName))
                result[option.MemberId] = option;
        }
        return result;
    }

    private static PartyState GetPartyState(GDictionary data)
    {
        if (!TryRead(data, "party_state", out Variant value))
            return null;
        return PartyState.TryReadPartyPayload(value, out PartyState partyState)
            ? partyState
            : null;
    }

    private static bool HasString(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.String;
    }

    private static bool HasArray(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Array;
    }

    private static bool HasBool(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Bool;
    }

    private static bool HasNonEmptyString(GDictionary data, string key)
    {
        return HasString(data, key) && !string.IsNullOrEmpty(data[key].AsString().StripEdges());
    }

    private static string DictString(GDictionary data, string key, string defaultValue)
    {
        if (!TryRead(data, key, out Variant value))
            return defaultValue;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => defaultValue,
        };
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (!TryRead(data, key, out Variant value))
            return "";
        return value.VariantType == Variant.Type.String ? new StringName(value.AsString()) : "";
    }

    private static string StrictString(GDictionary data, string key)
    {
        return DictString(data, key, "");
    }

    private static bool DictBool(GDictionary data, string key, bool defaultValue)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Bool
            ? value.AsBool()
            : defaultValue;
    }

    private static int DictInt(GDictionary data, string key, int defaultValue)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : defaultValue;
    }

    private static string OptionalString(GDictionary data, string key)
    {
        return DictString(data, key, "");
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static bool TryRead(GDictionary data, string key, out Variant value)
    {
        if (data == null || string.IsNullOrEmpty(key))
        {
            value = default;
            return false;
        }
        if (data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = default;
        return false;
    }

    private static string FirstNonEmpty(string preferred, string fallback)
    {
        return !string.IsNullOrEmpty(preferred) ? preferred : fallback;
    }
}
