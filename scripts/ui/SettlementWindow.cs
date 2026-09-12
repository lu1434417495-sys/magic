using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class SettlementWindow : ModalWindowShell
{
    [Signal]
    public delegate void action_requestedEventHandler(
        string settlement_id,
        string service_id,
        string action_id,
        string member_id,
        int quantity,
        string submission_source
    );

    [Signal]
    public delegate void closedEventHandler();

    public ColorRect shade;
    public Label title_label;
    public Label meta_label;
    public RichTextLabel facilities_label;
    public RichTextLabel resident_label;
    public OptionButton member_selector;
    public Label member_state_label;
    public VBoxContainer services_container;
    public Label service_state_label;
    public Label service_cost_label;
    public RichTextLabel service_details_label;
    public Label feedback_label;
    public Button close_button;

    private SettlementOverviewWindowData _windowData = SettlementOverviewWindowData.Empty;
    private string _settlementId = "";
    private StringName _selectedMemberId = "";
    private int _selectedServiceIndex = -1;

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        meta_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        facilities_label = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/FacilitiesLabel"
        );
        resident_label = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/ResidentLabel"
        );
        member_selector = GetNode<OptionButton>(
            "CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/MemberSelector"
        );
        member_state_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/LeftColumn/MemberStateLabel"
        );
        services_container = GetNode<VBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServicesScroll/ServicesContainer"
        );
        service_state_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServiceStateLabel"
        );
        service_cost_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServiceCostLabel"
        );
        service_details_label = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/ServiceDetailsLabel"
        );
        feedback_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/RightColumn/FeedbackLabel"
        );
        close_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Header/CloseButton"
        );

        HideWindow();
        close_button.Pressed += _close_from_button;
        member_selector.ItemSelected += index => _on_member_selected((int)index);
        base._Ready();
    }

    protected override void _on_modal_close_requested() => _close_from_button();

    internal void ShowSettlement(SettlementOverviewWindowData window_data)
    {
        if (window_data == null || !window_data.IsValid)
        {
            HideWindow();
            return;
        }

        _windowData = window_data;
        _settlementId = _windowData.SettlementId.ToString();
        _selectedMemberId = _resolve_default_member_id();
        _selectedServiceIndex = -1;
        Visible = true;
        _refresh_view();
    }

    public void HideWindow()
    {
        Visible = false;
        _windowData = SettlementOverviewWindowData.Empty;
        _settlementId = "";
        _selectedMemberId = "";
        _selectedServiceIndex = -1;

        if (title_label != null)
            title_label.Text = "";
        if (meta_label != null)
            meta_label.Text = "";
        if (facilities_label != null)
            facilities_label.Text = "";
        if (resident_label != null)
            resident_label.Text = "";
        member_selector?.Clear();
        if (member_state_label != null)
            member_state_label.Text = "";
        if (services_container != null)
            _clear_service_buttons();
        if (service_state_label != null)
            service_state_label.Text = "";
        if (service_cost_label != null)
            service_cost_label.Text = "";
        if (service_details_label != null)
            service_details_label.Text = "";
        if (feedback_label != null)
            feedback_label.Text = "";
    }

    public void SetFeedback(string message)
    {
        if (feedback_label != null)
            feedback_label.Text = message;
    }

    private void _refresh_view()
    {
        title_label.Text = _windowData.DisplayName;
        meta_label.Text = _build_meta_text();
        facilities_label.Text = _build_facility_text();
        resident_label.Text = _build_resident_text();
        _rebuild_member_selector();
        _rebuild_service_buttons();
        _refresh_member_state();
        _refresh_service_details();
        if (feedback_label != null && string.IsNullOrEmpty(feedback_label.Text))
            feedback_label.Text = _windowData.FeedbackText;
    }

    // The runtime already resolved the default against leader / active / reserve order; the
    // window only falls back to the first renderable option.
    private StringName _resolve_default_member_id()
    {
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

    private static string _build_member_option_label(SettlementMemberOptionData option)
    {
        string prefix = option.IsLeader ? "队长 · " : "";
        string roleSuffix = !string.IsNullOrEmpty(option.RosterRole)
            ? $" · {option.RosterRole}"
            : "";
        return $"{prefix}{option.DisplayName}{roleSuffix}  |  HP {option.CurrentHp}  MP {option.CurrentMp}";
    }

    private string _build_meta_text()
    {
        string identityText =
            $"{_windowData.TierName}  |  占地 {_windowData.FootprintSize.X}x{_windowData.FootprintSize.Y}  |  阵营 {UiDisplayLabels.Faction(_windowData.FactionId)}";
        if (!string.IsNullOrEmpty(_windowData.CountryId))
            identityText += $"  |  国家 {_windowData.CountryId}";
        var lines = new List<string>
        {
            identityText,
        };
        if (!string.IsNullOrEmpty(_windowData.StateSummaryText))
            lines.Add(_windowData.StateSummaryText);
        return string.Join("\n", lines);
    }

    private string _build_facility_text()
    {
        if (_windowData.Facilities.Count == 0)
            return "设施：暂无";

        var lines = new List<string> { "设施：" };
        foreach (SettlementFacilityEntryData facility in _windowData.Facilities)
        {
            string line = $"- {facility.DisplayName}";
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    private string _build_resident_text()
    {
        if (_windowData.Residents.Count == 0)
            return "驻留 NPC：暂无";

        var lines = new List<string> { "驻留 NPC：" };
        foreach (SettlementResidentEntryData resident in _windowData.Residents)
            lines.Add(
                $"- {resident.DisplayName} · {resident.FacilityName}"
            );
        return string.Join("\n", lines);
    }

    private void _rebuild_member_selector()
    {
        var options = new List<(StringName Id, string Label)>();
        foreach (SettlementMemberOptionData option in _windowData.MemberOptions)
            options.Add((option.MemberId, _build_member_option_label(option)));
        UiOptionButtonUtils.Populate(member_selector, options, new StringName(""));

        member_selector.Visible = _windowData.MemberOptions.Count > 0;
        member_state_label.Visible = true;

        _select_member(_resolve_default_member_id());
    }

    private void _select_member(StringName member_id)
    {
        _selectedMemberId =
            member_id != (StringName)"" && _windowData.MemberOptionMap.ContainsKey(member_id)
                ? member_id
                : "";
        UiOptionButtonUtils.SelectById(member_selector, _selectedMemberId);
        _refresh_member_state();
    }

    private void _refresh_member_state()
    {
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

    private void _rebuild_service_buttons()
    {
        _clear_service_buttons();
        for (int index = 0; index < _windowData.Services.Count; index++)
        {
            ResolvedService service = ResolveServiceForSelectedMember(_windowData.Services[index]);
            var button = new Button
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 58),
                Text = _build_service_button_text(service),
                Disabled = !service.IsEnabled,
            };
            if (button.Disabled)
                button.TooltipText = service.DisabledReason;
            int capturedIndex = index;
            button.Pressed += () => _on_service_button_pressed(capturedIndex);
            services_container.AddChild(button);
        }

        if (services_container.GetChildCount() == 0)
        {
            var placeholder = new Label
            {
                Text = "当前据点没有可用服务。",
                Modulate = new Color(0.77f, 0.83f, 0.91f, 0.85f),
            };
            services_container.AddChild(placeholder);
        }
    }

    private static string _build_service_button_text(ResolvedService service)
    {
        string text =
            $"{service.FacilityName} · {service.NpcName}\n{service.StateLabel}  |  {service.CostLabel}";
        if (!service.IsEnabled && !string.IsNullOrEmpty(service.DisabledReason))
            text += $"\n{service.DisabledReason}";
        return text;
    }

    private void _refresh_service_details()
    {
        if (_windowData.Services.Count == 0)
        {
            service_state_label.Text = "状态：暂无服务";
            service_cost_label.Text = "费用：暂无服务";
            service_details_label.Text = "当前据点没有可用服务。";
            return;
        }

        if (_selectedServiceIndex < 0 || _selectedServiceIndex >= _windowData.Services.Count)
            _selectedServiceIndex = 0;
        ResolvedService service = ResolveServiceForSelectedMember(
            _windowData.Services[_selectedServiceIndex]
        );
        service_state_label.Text = service.StateLabel;
        service_cost_label.Text = service.CostLabel;
        service_details_label.Text = _build_service_detail_text(service);
    }

    private static string _build_service_detail_text(ResolvedService service)
    {
        var lines = new List<string>
        {
            $"设施：{service.FacilityName}",
            $"NPC：{service.NpcName}",
            $"服务：{UiDisplayLabels.SettlementService(service.ServiceType)}",
            $"状态：{service.StateLabel}",
            $"费用：{service.CostLabel}",
        };
        if (!string.IsNullOrEmpty(service.DisabledReason))
            lines.Add($"说明：{service.DisabledReason}");
        return string.Join("\n", lines);
    }

    private void _on_service_button_pressed(int index)
    {
        if (index < 0 || index >= _windowData.Services.Count)
            return;
        ResolvedService service = ResolveServiceForSelectedMember(_windowData.Services[index]);
        if (!service.IsEnabled)
        {
            SetFeedback(service.DisabledReason);
            return;
        }

        _selectedServiceIndex = index;
        _refresh_service_details();
        EmitSignal(
            SignalName.action_requested,
            _settlementId,
            service.ActionId.ToString(),
            service.ActionId.ToString(),
            _selectedMemberId.ToString(),
            0,
            SettlementSubmissionSources.ToPayloadValue(SettlementSubmissionSource.Settlement)
        );
    }

    private ResolvedService ResolveServiceForSelectedMember(SettlementServiceEntryData service)
    {
        var resolved = new ResolvedService(
            service.ActionId,
            service.FacilityName,
            service.NpcName,
            service.ServiceType,
            service.InteractionScriptId,
            service.CostLabel,
            service.StateLabel,
            service.IsEnabled,
            service.DisabledReason
        );
        if (_selectedMemberId == (StringName)"" || service.MemberAvailability.Count == 0)
            return resolved;
        if (
            !service.MemberAvailability.TryGetValue(
                _selectedMemberId,
                out SettlementMemberAvailabilityData availability
            )
        )
        {
            return resolved with
            {
                IsEnabled = false,
                DisabledReason = "当前成员不可用",
                StateLabel = "状态：当前成员不可用",
            };
        }

        return resolved with
        {
            IsEnabled = availability.IsEnabled,
            DisabledReason = availability.DisabledReason,
            StateLabel = availability.IsEnabled
                ? "状态：可用"
                : $"状态：{(!string.IsNullOrEmpty(availability.DisabledReason) ? availability.DisabledReason : "不可用")}",
        };
    }

    // UI-local view of one service row after the selected member is applied. It carries no
    // property bag: submissions go out as stable ids on the existing signal.
    private readonly record struct ResolvedService(
        StringName ActionId,
        string FacilityName,
        string NpcName,
        string ServiceType,
        StringName InteractionScriptId,
        string CostLabel,
        string StateLabel,
        bool IsEnabled,
        string DisabledReason
    );

    private void _clear_service_buttons()
    {
        foreach (Node child in services_container.GetChildren())
            child.QueueFree();
    }

    public void _on_member_selected(int index)
    {
        _selectedMemberId = UiOptionButtonUtils.GetIdAt(member_selector, index);
        _refresh_member_state();
        _rebuild_service_buttons();
        _refresh_service_details();
    }

    private void _close_from_button()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.closed);
    }
}
