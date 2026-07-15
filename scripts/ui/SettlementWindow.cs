using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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

    private SettlementWindowData _windowData = SettlementWindowData.Empty();
    private string _settlementId = "";
    private StringName _selectedMemberId = "";
    private int _selectedServiceIndex = -1;
    private readonly List<StringName> _memberOptionIds = new();

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

    public void ShowSettlement(GDictionary window_data)
    {
        SettlementWindowData normalized = SettlementWindowData.From(window_data);
        if (normalized == null)
        {
            HideWindow();
            return;
        }

        _windowData = normalized;
        _settlementId = _windowData.SettlementId;
        _selectedMemberId = _windowData.ResolveDefaultMemberId();
        _selectedServiceIndex = -1;
        Visible = true;
        _refresh_view();
    }

    public void HideWindow()
    {
        Visible = false;
        _windowData = SettlementWindowData.Empty();
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

    private string _build_meta_text()
    {
        var lines = new List<string>
        {
            $"{_windowData.TierName}  |  占地 {_windowData.FootprintSize.X}x{_windowData.FootprintSize.Y}  |  阵营 {_windowData.FactionId}",
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
        foreach (FacilityEntry facility in _windowData.Facilities)
        {
            string line = $"- {facility.DisplayName} [{facility.SlotTag}]";
            if (!string.IsNullOrEmpty(facility.InteractionType))
                line += $" · {facility.InteractionType}";
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    private string _build_resident_text()
    {
        if (_windowData.Residents.Count == 0)
            return "驻留 NPC：暂无";

        var lines = new List<string> { "驻留 NPC：" };
        foreach (ResidentEntry resident in _windowData.Residents)
            lines.Add(
                $"- {resident.DisplayName} · {resident.ServiceType} · {resident.FacilityName}"
            );
        return string.Join("\n", lines);
    }

    private void _rebuild_member_selector()
    {
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

        StringName selectedMemberId = _windowData.ResolveDefaultMemberId();
        if (selectedMemberId == (StringName)"" && _windowData.MemberOptions.Count > 0)
            selectedMemberId = _windowData.MemberOptions[0].MemberId;
        _select_member(selectedMemberId);
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
            $"{service.FacilityName} · {service.NpcName} · {service.ServiceType}\n{service.StateLabel}  |  {service.CostLabel}";
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
            $"服务：{service.ServiceType}",
            $"交互：{service.InteractionScriptId}",
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
            service.ActionId,
            service.ActionId,
            _selectedMemberId.ToString(),
            0,
            SettlementSubmissionSources.ToPayloadValue(SettlementSubmissionSource.Settlement)
        );
    }

    private ResolvedService ResolveServiceForSelectedMember(ServiceEntry service)
    {
        ResolvedService resolved = service.ToResolved();
        if (_selectedMemberId == (StringName)"" || service.MemberAvailability.Count == 0)
            return resolved;
        string memberKey = _selectedMemberId.ToString();
        if (!service.MemberAvailability.TryGetValue(memberKey, out MemberAvailability availability))
        {
            resolved.IsEnabled = false;
            resolved.DisabledReason = "当前成员不可用";
            resolved.StateLabel = "状态：当前成员不可用";
            resolved.ApplyToPayload();
            return resolved;
        }

        resolved.IsEnabled = availability.IsEnabled;
        resolved.DisabledReason = availability.DisabledReason;
        resolved.StateLabel = availability.IsEnabled
            ? "状态：可用"
            : $"状态：{(!string.IsNullOrEmpty(availability.DisabledReason) ? availability.DisabledReason : "不可用")}";
        resolved.ApplyToPayload();
        return resolved;
    }

    private void _clear_service_buttons()
    {
        foreach (Node child in services_container.GetChildren())
            child.QueueFree();
    }

    public void _on_member_selected(int index)
    {
        if (index < 0 || index >= _memberOptionIds.Count)
            _selectedMemberId = "";
        else
            _selectedMemberId = _memberOptionIds[index];
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

    private sealed class SettlementWindowData
    {
        public string SettlementId { get; private init; } = "";
        public string DisplayName { get; private init; } = "";
        public string TierName { get; private init; } = "";
        public string FactionId { get; private init; } = "";
        public string FeedbackText { get; private init; } = "";
        public string StateSummaryText { get; private init; } = "";
        public Vector2I FootprintSize { get; private init; } = Vector2I.One;
        public PartyState PartyState { get; private init; }
        public List<MemberOption> MemberOptions { get; private init; } = new();
        public Dictionary<StringName, MemberOption> MemberOptionMap { get; private init; } = new();
        public StringName ExplicitDefaultMemberId { get; private init; } = "";
        public StringName SelectedMemberId { get; private init; } = "";
        public List<FacilityEntry> Facilities { get; private init; } = new();
        public List<ResidentEntry> Residents { get; private init; } = new();
        public List<ServiceEntry> Services { get; private init; } = new();

        public static SettlementWindowData Empty() => new();

        public static SettlementWindowData From(GDictionary data)
        {
            if (data == null)
                return null;
            foreach (
                string fieldName in new[]
                {
                    "settlement_id",
                    "display_name",
                    "tier_name",
                    "faction_id",
                    "feedback_text",
                }
            )
            {
                if (!HasNonEmptyString(data, fieldName))
                    return null;
            }
            if (!HasString(data, "state_summary_text"))
                return null;
            if (
                !HasVector2I(data, "footprint_size")
            )
                return null;
            Vector2I footprintSize = ReadVector2I(data, "footprint_size");
            if (footprintSize.X < 1 || footprintSize.Y < 1)
                return null;
            if (
                !HasArray(data, "available_services")
                || !HasArray(data, "facilities")
                || !HasArray(data, "service_npcs")
            )
                return null;

            List<FacilityEntry> facilities = BuildFacilities(ReadArray(data, "facilities"));
            List<ResidentEntry> residents = BuildResidents(ReadArray(data, "service_npcs"));
            List<ServiceEntry> services = BuildServices(ReadArray(data, "available_services"));
            if (facilities == null || residents == null || services == null)
                return null;

            PartyState partyState = GetPartyState(data);
            List<MemberOption> memberOptions = BuildMemberOptions(data, partyState);
            if (memberOptions == null)
                return null;
            Dictionary<StringName, MemberOption> memberMap = BuildMemberOptionMap(memberOptions);

            return new SettlementWindowData
            {
                SettlementId = data["settlement_id"].AsString().StripEdges(),
                DisplayName = data["display_name"].AsString().StripEdges(),
                TierName = data["tier_name"].AsString().StripEdges(),
                FactionId = data["faction_id"].AsString().StripEdges(),
                FeedbackText = data["feedback_text"].AsString().StripEdges(),
                StateSummaryText = data["state_summary_text"].AsString(),
                FootprintSize = footprintSize,
                PartyState = partyState,
                MemberOptions = memberOptions,
                MemberOptionMap = memberMap,
                ExplicitDefaultMemberId = DictStringName(data, "default_member_id"),
                SelectedMemberId = DictStringName(data, "selected_member_id"),
                Facilities = facilities,
                Residents = residents,
                Services = services,
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
    }

    private readonly record struct FacilityEntry(
        string DisplayName,
        string SlotTag,
        string InteractionType
    );

    private readonly record struct ResidentEntry(
        string DisplayName,
        string ServiceType,
        string FacilityName
    );

    private readonly record struct MemberAvailability(bool IsEnabled, string DisabledReason);

    private sealed class ServiceEntry
    {
        private static readonly string[] ServiceEntryKeys =
        {
            "settlement_id",
            "facility_id",
            "facility_template_id",
            "facility_name",
            "npc_id",
            "npc_template_id",
            "npc_name",
            "service_type",
            "action_id",
            "interaction_script_id",
            "cost_label",
            "state_label",
            "summary_text",
            "is_enabled",
            "disabled_reason",
            "panel_kind",
            "interaction_type",
            "member_availability",
        };

        public string ActionId { get; private init; } = "";
        public string FacilityName { get; private init; } = "";
        public string NpcName { get; private init; } = "";
        public string ServiceType { get; private init; } = "";
        public string InteractionScriptId { get; private init; } = "";
        public string CostLabel { get; private init; } = "";
        public string StateLabel { get; private init; } = "";
        public string SummaryText { get; private init; } = "";
        public bool IsEnabled { get; private init; }
        public string DisabledReason { get; private init; } = "";
        public SettlementPanelKind PanelKind { get; private init; } = SettlementPanelKind.None;
        public Dictionary<string, MemberAvailability> MemberAvailability { get; private init; } =
            new();
        public Dictionary<string, object> Payload { get; private init; } = new();

        public ResolvedService ToResolved()
        {
            return new ResolvedService
            {
                ActionId = ActionId,
                FacilityName = FacilityName,
                NpcName = NpcName,
                ServiceType = ServiceType,
                InteractionScriptId = InteractionScriptId,
                CostLabel = CostLabel,
                StateLabel = StateLabel,
                SummaryText = SummaryText,
                IsEnabled = IsEnabled,
                DisabledReason = DisabledReason,
                PanelKind = PanelKind,
                Payload = new Dictionary<string, object>(Payload, StringComparer.Ordinal),
            };
        }

        public static ServiceEntry From(GDictionary data)
        {
            if (data == null)
                return null;
            if (!HasOnlyKnownKeys(data, ServiceEntryKeys))
                return null;
            foreach (
                string fieldName in new[]
                {
                    "action_id",
                    "facility_name",
                    "npc_name",
                    "service_type",
                    "interaction_script_id",
                    "cost_label",
                    "state_label",
                    "summary_text",
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
            SettlementPanelKind panelKind = SettlementPanelKind.None;
            if (data.ContainsKey("panel_kind"))
            {
                if (!HasNonEmptyString(data, "panel_kind"))
                    return null;
                if (
                    !SettlementPanelKinds.TryParse(
                        data["panel_kind"].AsString().StripEdges(),
                        out panelKind
                    )
                    || panelKind == SettlementPanelKind.None
                )
                    return null;
            }
            if (data.ContainsKey("interaction_type") && !HasString(data, "interaction_type"))
                return null;

            Dictionary<string, object> payload = RuntimePlainPayload.NormalizeDictionary(
                data,
                "SettlementWindow.ServiceEntry"
            );
            payload["is_enabled"] = isEnabled;
            payload["disabled_reason"] = disabledReason;
            string panelKindText = SettlementPanelKinds.ToPayloadValue(panelKind);
            if (!string.IsNullOrEmpty(panelKindText))
                payload["panel_kind"] = panelKindText;

            return new ServiceEntry
            {
                ActionId = data["action_id"].AsString().StripEdges(),
                FacilityName = data["facility_name"].AsString().StripEdges(),
                NpcName = data["npc_name"].AsString().StripEdges(),
                ServiceType = data["service_type"].AsString().StripEdges(),
                InteractionScriptId = data["interaction_script_id"].AsString().StripEdges(),
                CostLabel = data["cost_label"].AsString().StripEdges(),
                StateLabel = data["state_label"].AsString().StripEdges(),
                SummaryText = data["summary_text"].AsString().StripEdges(),
                IsEnabled = isEnabled,
                DisabledReason = disabledReason,
                PanelKind = panelKind,
                MemberAvailability = ParseMemberAvailability(
                    DictDictionary(data, "member_availability")
                ),
                Payload = payload,
            };
        }
    }

    private sealed class ResolvedService
    {
        public string ActionId = "";
        public string FacilityName = "";
        public string NpcName = "";
        public string ServiceType = "";
        public string InteractionScriptId = "";
        public string CostLabel = "";
        public string StateLabel = "";
        public string SummaryText = "";
        public bool IsEnabled;
        public string DisabledReason = "";
        public SettlementPanelKind PanelKind = SettlementPanelKind.None;
        public Dictionary<string, object> Payload = new(StringComparer.Ordinal);

        public void ApplyToPayload()
        {
            Payload["is_enabled"] = IsEnabled;
            Payload["disabled_reason"] = DisabledReason;
            Payload["state_label"] = StateLabel;
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

    private static List<FacilityEntry> BuildFacilities(GArray value)
    {
        var result = new List<FacilityEntry>();
        foreach (var entryValue in value)
        {
            if (!entryValue.TryAsDictionary(out GDictionary entry))
                return null;
            if (
                !HasNonEmptyString(entry, "display_name")
                || !HasNonEmptyString(entry, "slot_tag")
                || !HasString(entry, "interaction_type")
            )
                return null;
            result.Add(
                new FacilityEntry(
                    entry["display_name"].AsString().StripEdges(),
                    entry["slot_tag"].AsString().StripEdges(),
                    entry["interaction_type"].AsString().StripEdges()
                )
            );
        }
        return result;
    }

    private static List<ResidentEntry> BuildResidents(GArray value)
    {
        var result = new List<ResidentEntry>();
        foreach (var entryValue in value)
        {
            if (!entryValue.TryAsDictionary(out GDictionary entry))
                return null;
            if (
                !HasNonEmptyString(entry, "display_name")
                || !HasNonEmptyString(entry, "service_type")
                || !HasNonEmptyString(entry, "facility_name")
            )
                return null;
            result.Add(
                new ResidentEntry(
                    entry["display_name"].AsString().StripEdges(),
                    entry["service_type"].AsString().StripEdges(),
                    entry["facility_name"].AsString().StripEdges()
                )
            );
        }
        return result;
    }

    private static List<ServiceEntry> BuildServices(GArray value)
    {
        var result = new List<ServiceEntry>();
        foreach (var entryValue in value)
        {
            if (!entryValue.TryAsDictionary(out GDictionary entryData))
                return null;
            ServiceEntry entry = ServiceEntry.From(entryData);
            if (entry == null)
                return null;
            result.Add(entry);
        }
        return result;
    }

    private static Dictionary<string, MemberAvailability> ParseMemberAvailability(GDictionary byMember)
    {
        var result = new Dictionary<string, MemberAvailability>();
        if (byMember == null)
            return result;
        foreach (var key in byMember.Keys)
        {
            var availabilityValue = byMember[key];
            if (!availabilityValue.TryAsDictionary(out GDictionary availability))
                continue;
            result[key.AsString()] = new MemberAvailability(
                DictBool(availability, "is_enabled", false),
                DictString(availability, "disabled_reason", "").StripEdges()
            );
        }
        return result;
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

    private static bool HasArray(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Array;
    }

    private static bool HasString(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.String;
    }

    private static bool HasVector2I(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Vector2I;
    }

    private static bool HasBool(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Bool;
    }

    private static bool HasNonEmptyString(GDictionary data, string key)
    {
        return HasString(data, key) && !string.IsNullOrEmpty(data[key].AsString().StripEdges());
    }

    private static bool HasOnlyKnownKeys(GDictionary data, IReadOnlyList<string> expectedKeys)
    {
        if (data == null)
            return false;
        foreach (Variant keyValue in data.Keys)
        {
            string key = keyValue.ToString();
            bool found = false;
            foreach (string expectedKey in expectedKeys)
            {
                if (key == expectedKey)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (!TryRead(data, key, out Variant value))
            return "";
        return value.VariantType switch
        {
            Variant.Type.String => new StringName(value.AsString()),
            _ => "",
        };
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

    private static Vector2I ReadVector2I(GDictionary data, string key)
    {
        return TryRead(data, key, out Variant value) && value.VariantType == Variant.Type.Vector2I
            ? value.AsVector2I()
            : Vector2I.Zero;
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
}
