using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ShopWindow : ModalWindowShell
{
    // Panel-specific typed submissions. The window never assembles a property bag for the
    // runtime to re-parse: it only reports which stable id the player picked.
    internal event Action<SettlementShopActionRequest> ShopActionRequested;
    internal event Action<SettlementContractBoardActionRequest> ContractActionRequested;
    internal event Action<ForgeActionRequest> ForgeActionRequested;
    internal event Action<SettlementStagecoachActionRequest> StagecoachActionRequested;

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

    private SettlementServiceWindowData _windowData = SettlementServiceWindowData.Empty;
    private int _selectedEntryIndex = -1;
    private StringName _selectedMemberId = "";
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
        UiListTheme.Apply(entry_list);
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
        entry_list.ItemSelected += index => _on_entry_selected((int)index);
        member_selector.ItemSelected += index => _on_member_selected((int)index);
        confirm_button.Pressed += _on_confirm_button_pressed;
        cancel_button.Pressed += _on_cancel_button_pressed;
        close_button.Pressed += _close_window;
        base._Ready();
    }

    protected override void _on_modal_close_requested() => _on_cancel_button_pressed();

    internal void ShowShop(SettlementServiceWindowData window_data)
    {
        if (window_data == null || !window_data.IsValid)
        {
            HideWindow();
            return;
        }

        _windowData = window_data;
        _selectedEntryIndex = -1;
        _selectedMemberId = _resolve_default_member_id();
        Visible = true;
        RefreshView();
    }

    internal void ShowStagecoach(SettlementServiceWindowData window_data)
    {
        if (window_data == null || window_data.PanelKind != SettlementPanelKind.Stagecoach)
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
        _windowData = SettlementServiceWindowData.Empty;
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
        if (_windowData.Confirmation != null)
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
            member_title_label.Visible = false;
            member_selector.Visible = false;
            member_state_label.Visible = false;
            return;
        }

        member_title_label.Visible = true;
        member_selector.Visible = true;
        member_state_label.Visible = true;
        var options = new List<(StringName Id, string Label)>();
        foreach (SettlementMemberOptionData option in _windowData.MemberOptions)
            options.Add((option.MemberId, BuildMemberLabel(option)));
        UiOptionButtonUtils.Populate(member_selector, options, new StringName(""));

        member_selector.Visible = _windowData.MemberOptions.Count > 0;
        member_state_label.Visible = true;

        StringName selectedMemberId = _resolve_default_member_id();
        if (selectedMemberId == (StringName)"" && _windowData.MemberOptions.Count > 0)
            selectedMemberId = _windowData.MemberOptions[0].MemberId;
        _select_member(selectedMemberId);
    }

    private static string BuildMemberLabel(SettlementMemberOptionData option)
    {
        if (string.IsNullOrEmpty(option.DisplayName))
            return "";
        string prefix = option.IsLeader ? "队长 · " : "";
        string roleSuffix = !string.IsNullOrEmpty(option.RosterRole)
            ? $" · {option.RosterRole}"
            : "";
        return $"{prefix}{option.DisplayName}{roleSuffix}  |  HP {option.CurrentHp}  MP {option.CurrentMp}";
    }

    // The runtime already resolved the default member; the window only falls back to the
    // first renderable option when that member is not selectable here.
    private StringName _resolve_default_member_id()
    {
        if (
            _windowData.SelectedMemberId != (StringName)""
            && _windowData.MemberOptionMap.ContainsKey(_windowData.SelectedMemberId)
        )
            return _windowData.SelectedMemberId;
        if (
            _windowData.DefaultMemberId != (StringName)""
            && _windowData.MemberOptionMap.ContainsKey(_windowData.DefaultMemberId)
        )
            return _windowData.DefaultMemberId;
        foreach (SettlementMemberOptionData option in _windowData.MemberOptions)
        {
            if (option.MemberId != (StringName)"")
                return option.MemberId;
        }
        return "";
    }

    private void _select_member(StringName member_id)
    {
        _selectedMemberId =
            member_id != (StringName)"" && _windowData.MemberOptionMap.ContainsKey(member_id)
                ? member_id
                : "";
        UiOptionButtonUtils.SelectById(member_selector, _selectedMemberId);
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
            !_windowData.MemberOptionMap.TryGetValue(
                _selectedMemberId,
                out SettlementMemberOptionData option
            )
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
        foreach (SettlementServiceWindowEntryData entry in _windowData.Entries)
            entry_list.AddItem(_build_entry_label(entry));
    }

    private static string _build_entry_label(SettlementServiceWindowEntryData entry)
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
            state_label.Text = _windowData.Labels.EmptyStateLabel;
            cost_label.Text = _windowData.Labels.EmptyCostLabel;
            details_label.Text = _windowData.Labels.EmptyDetailsText;
            confirm_button.Disabled = true;
            return;
        }

        SettlementServiceWindowEntryData entry = _get_selected_entry();
        state_label.Text = entry.StateLabel;
        cost_label.Text = entry.CostLabel;
        details_label.Text = _build_entry_details(entry);
    }

    private string _build_entry_details(SettlementServiceWindowEntryData entry)
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
        return _windowData.MemberOptionMap.TryGetValue(
            member_id,
            out SettlementMemberOptionData option
        )
            ? option.DisplayName
            : "";
    }

    private void _on_entry_selected(int index)
    {
        _select_entry(index);
        _refresh_details();
        _refresh_controls();
    }

    private void _on_member_selected(int index)
    {
        _selectedMemberId = UiOptionButtonUtils.GetIdAt(member_selector, index);
        _refresh_member_state();
        _refresh_details();
        _refresh_controls();
    }

    private void _show_confirmation_panel()
    {
        _isShowingConfirmation = true;
        confirm_button.Text = "确认";
        cancel_button.Text = "返回";
        details_label.Text = _windowData.Confirmation?.Text ?? "";
    }

    private void _hide_confirmation_panel()
    {
        _isShowingConfirmation = false;
        confirm_button.Text = _windowData.Labels.ConfirmLabel;
        cancel_button.Text = _windowData.Labels.CancelLabel;
        _refresh_details();
    }

    private void _on_confirm_button_pressed()
    {
        if (confirm_button.Disabled)
            return;
        if (_windowData.Confirmation != null && !_isShowingConfirmation)
        {
            _show_confirmation_panel();
            return;
        }
        if (_windowData.Entries.Count == 0)
            return;

        SettlementServiceWindowEntryData entry = _get_selected_entry();
        bool confirmAccept = _windowData.Confirmation != null;
        SettlementActionRequest action = _build_action_request();
        switch (entry.Selection)
        {
            case SettlementShopSelectionData shop:
                HideWindow();
                ShopActionRequested?.Invoke(
                    new SettlementShopActionRequest(
                        action,
                        shop.ActionKind,
                        shop.ItemId,
                        shop.InstanceId,
                        1
                    )
                );
                return;
            case SettlementContractSelectionData contract:
                HideWindow();
                ContractActionRequested?.Invoke(
                    new SettlementContractBoardActionRequest(
                        action,
                        contract.QuestId,
                        confirmAccept
                    )
                );
                return;
            case SettlementForgeSelectionData forge:
                {
                    var request = new ForgeActionRequest(
                        action.SettlementId,
                        action.ServiceId,
                        action.ActionId,
                        action.MemberId,
                        forge.RecipeId
                    );
                    if (!request.IsValid)
                        return;
                    HideWindow();
                    ForgeActionRequested?.Invoke(request);
                    return;
                }
            case SettlementStagecoachSelectionData stagecoach:
                HideWindow();
                StagecoachActionRequested?.Invoke(
                    new SettlementStagecoachActionRequest(
                        action,
                        stagecoach.TargetSettlementId
                    )
                );
                return;
        }
    }

    private SettlementActionRequest _build_action_request() =>
        new(
            _windowData.SettlementId,
            _windowData.ActionId,
            _windowData.ActionId,
            _selectedMemberId,
            0,
            SettlementSubmissionSources.FromPanelKind(_windowData.PanelKind)
        );

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

    private SettlementServiceWindowEntryData _get_selected_entry()
    {
        if (_selectedEntryIndex >= 0 && _selectedEntryIndex < _windowData.Entries.Count)
            return _windowData.Entries[_selectedEntryIndex];
        return _windowData.Entries[0];
    }

    private void _apply_section_titles()
    {
        entry_title_label.Text = _windowData.Labels.EntryTitle;
        summary_title_label.Text = _windowData.Labels.SummaryTitle;
        state_title_label.Text = _windowData.Labels.StateTitle;
        cost_title_label.Text = _windowData.Labels.CostTitle;
        details_title_label.Text = _windowData.Labels.DetailsTitle;
        member_title_label.Text = _windowData.Labels.MemberTitle;
    }
}
