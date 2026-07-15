using Godot;

[GlobalClass]
public partial class ContingencySetupWindow : Control
{
    [Signal]
    public delegate void save_requestedEventHandler(StringName member_id, StringName setup_payload_name);

    [Signal]
    public delegate void charge_requestedEventHandler(StringName member_id, StringName setup_id);

    [Signal]
    public delegate void clear_charge_requestedEventHandler(StringName member_id, StringName setup_id);

    [Signal]
    public delegate void closedEventHandler();

    private sealed class TemplateOption
    {
        public StringName PayloadName { get; init; }
        public StringName SetupId { get; init; }
        public string DisplayName { get; init; } = "";
        public string TriggerType { get; init; } = "";
        public string ReleaseMode { get; init; } = "";
        public string SpellSummary { get; init; } = "";
        public string TargetResolver { get; init; } = "";
        public int MatrixLoad { get; init; }
    }

    private static readonly TemplateOption[] V1Templates =
    {
        new()
        {
            PayloadName = "hp_mirror_self",
            SetupId = "hp_mirror_self",
            DisplayName = "濒死镜影",
            TriggerType = "hp_below_percent",
            ReleaseMode = "burst_release",
            SpellSummary = "mage_mirror_image@2:self",
            TargetResolver = "self",
            MatrixLoad = 3,
        },
        new()
        {
            PayloadName = "owner_turn_mirror_self",
            SetupId = "owner_turn_mirror_self",
            DisplayName = "起手镜影",
            TriggerType = "owner_turn_started",
            ReleaseMode = "burst_release",
            SpellSummary = "mage_mirror_image@2:self",
            TargetResolver = "self",
            MatrixLoad = 3,
        },
    };

    private static TemplateOption DefaultTemplate => V1Templates[0];

    public Label member_status_label;
    public Label setup_status_label;
    public OptionButton trigger_selector;
    public OptionButton release_mode_selector;
    public ItemList stored_spell_list;
    public OptionButton target_resolver_selector;
    public Label matrix_preview_label;
    public Label material_preview_label;
    public Button save_button;
    public Button charge_button;
    public Button clear_charge_button;
    public Label clear_charge_confirmation_label;
    public Button close_button;

    private StringName _member_id = "";
    private StringName _setup_id = DefaultTemplate.SetupId;
    private StringName _selected_payload_name = DefaultTemplate.PayloadName;
    private bool _charged;
    private bool _selected_template_saved;

    public override void _Ready()
    {
        member_status_label = GetNode<Label>("%MemberStatusLabel");
        setup_status_label = GetNode<Label>("%SetupStatusLabel");
        trigger_selector = GetNode<OptionButton>("%TriggerSelector");
        release_mode_selector = GetNode<OptionButton>("%ReleaseModeSelector");
        stored_spell_list = GetNode<ItemList>("%StoredSpellList");
        UiListTheme.Apply(stored_spell_list);
        target_resolver_selector = GetNode<OptionButton>("%TargetResolverSelector");
        matrix_preview_label = GetNode<Label>("%MatrixPreviewLabel");
        material_preview_label = GetNode<Label>("%MaterialPreviewLabel");
        save_button = GetNode<Button>("%SaveButton");
        charge_button = GetNode<Button>("%ChargeButton");
        clear_charge_button = GetNode<Button>("%ClearChargeButton");
        clear_charge_confirmation_label = GetNode<Label>("%ClearChargeConfirmationLabel");
        close_button = GetNode<Button>("%CloseButton");

        save_button.Pressed += OnSavePressed;
        charge_button.Pressed += OnChargePressed;
        clear_charge_button.Pressed += OnClearChargePressed;
        close_button.Pressed += CloseWindow;
        trigger_selector.ItemSelected += OnTriggerSelected;
        HideWindow();
    }

    public void ShowForMember(PartyMemberState member, CharacterManagementModule characterManagement)
    {
        Visible = true;
        _member_id = member?.member_id ?? new StringName("");
        ContingencyMatrixSetupState setup = ResolveSetup(member);
        TemplateOption template = ResolveTemplate(setup) ?? DefaultTemplate;
        _setup_id = setup?.SetupId ?? template.SetupId;
        _selected_payload_name = template.PayloadName;
        _charged = setup?.Charged ?? false;
        _selected_template_saved = setup != null;

        member_status_label.Text =
            member != null
                ? $"{member.display_name} | member_id={member.member_id}"
                : "member_id=";
        PopulateTriggerOptions(template);
        RenderTemplateState(template, setup, characterManagement, member);
    }

    private void RenderTemplateState(
        TemplateOption template,
        ContingencyMatrixSetupState setup,
        CharacterManagementModule characterManagement,
        PartyMemberState member
    )
    {
        setup_status_label.Text =
            setup != null && setup.SetupId == template.SetupId
                ? $"{setup.SetupId} | {setup.DisplayName} | charged={(_charged ? "yes" : "no")}"
                : $"{template.SetupId} | {template.DisplayName} | 未保存";

        SetSingleOption(release_mode_selector, setup?.ReleaseMode.ToString() ?? template.ReleaseMode);
        SetSingleOption(target_resolver_selector, ResolveTargetResolver(setup) ?? template.TargetResolver);
        stored_spell_list.Clear();
        if (setup != null && setup.SetupId == template.SetupId)
            foreach (ContingencyStoredSpellEntryState spell in setup.StoredSpells)
                stored_spell_list.AddItem($"{spell.StoredSkillId}@{spell.CastLevel}:{spell.TargetResolver?.Type}");
        else
            stored_spell_list.AddItem(template.SpellSummary);

        int matrixLoad = setup?.SetupId == template.SetupId ? setup.MatrixLoad : template.MatrixLoad;
        int reservedMpMax = setup?.SetupId == template.SetupId ? setup.ReservedMpMax : 0;
        int effectiveMpMax = Mathf.Max(
            characterManagement?.GetMemberAttributeSnapshot(_member_id)?.GetValue(AttributeService.MP_MAX)
                ?? member?.current_mp
                ?? 0,
            0
        );
        matrix_preview_label.Text =
            $"matrix_load={matrixLoad} | reserved_mp_max={reservedMpMax} | effective_mp_max={effectiveMpMax}";
        material_preview_label.Text =
            $"special_contingency_gem:{(setup?.SetupId == template.SetupId ? GetMaterialQuantity(setup) : 0)}";
        save_button.Disabled = _member_id == "" || _charged;
        charge_button.Disabled = _member_id == "" || _charged || !_selected_template_saved;
        clear_charge_button.Disabled = _member_id == "" || !_charged;
        clear_charge_confirmation_label.Visible = _charged;
        clear_charge_confirmation_label.Text =
            _charged ? "清除充能后材料不返还，当前 MP 不恢复。" : "";
    }

    public void HideWindow()
    {
        Visible = false;
        _member_id = "";
        _setup_id = DefaultTemplate.SetupId;
        _selected_payload_name = DefaultTemplate.PayloadName;
        _charged = false;
        _selected_template_saved = false;
        if (clear_charge_confirmation_label != null)
            clear_charge_confirmation_label.Text = "";
    }

    private static ContingencyMatrixSetupState ResolveSetup(PartyMemberState member)
    {
        if (member == null)
            return null;
        ContingencyMatrixSetupState first = null;
        foreach (ContingencyMatrixSetupState setup in member.GetContingencySetupsTyped())
        {
            if (setup == null)
                continue;
            first ??= setup;
            if (setup.Charged)
                return setup;
        }
        return first;
    }

    private static string ResolveTargetResolver(ContingencyMatrixSetupState setup)
    {
        if (setup == null || setup.StoredSpells.Count == 0)
            return null;
        return setup.StoredSpells[0].TargetResolver?.Type.ToString() ?? "self";
    }

    private static TemplateOption ResolveTemplate(ContingencyMatrixSetupState setup)
    {
        if (setup == null)
            return null;
        foreach (TemplateOption option in V1Templates)
            if (option.SetupId == setup.SetupId)
                return option;
        return null;
    }

    private static TemplateOption ResolveTemplateByPayload(StringName payloadName)
    {
        foreach (TemplateOption option in V1Templates)
            if (option.PayloadName == payloadName)
                return option;
        return DefaultTemplate;
    }

    private void PopulateTriggerOptions(TemplateOption selected)
    {
        trigger_selector.Clear();
        int selectedIndex = 0;
        for (int index = 0; index < V1Templates.Length; index++)
        {
            TemplateOption option = V1Templates[index];
            trigger_selector.AddItem(option.TriggerType);
            trigger_selector.SetItemMetadata(index, option.PayloadName.ToString());
            if (option.PayloadName == selected.PayloadName)
                selectedIndex = index;
        }
        trigger_selector.Selected = selectedIndex;
    }

    private static int GetMaterialQuantity(ContingencyMatrixSetupState setup)
    {
        int total = 0;
        foreach (ContingencyMaterialCostState cost in setup?.MaterialCosts ?? System.Array.Empty<ContingencyMaterialCostState>())
            if (cost != null && cost.ItemId == "special_contingency_gem")
                total += cost.Quantity;
        return total;
    }

    private static void SetSingleOption(OptionButton selector, string value)
    {
        selector.Clear();
        selector.AddItem(value ?? "");
        selector.Selected = 0;
    }

    private void OnSavePressed()
    {
        if (_member_id == "" || _charged)
            return;
        EmitSignal(SignalName.save_requested, _member_id, _selected_payload_name);
    }

    private void OnChargePressed()
    {
        if (_member_id == "" || _charged)
            return;
        EmitSignal(SignalName.charge_requested, _member_id, _setup_id);
    }

    private void OnClearChargePressed()
    {
        if (_member_id == "" || !_charged)
            return;
        EmitSignal(SignalName.clear_charge_requested, _member_id, _setup_id);
    }

    public void CloseWindow()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.closed);
    }

    private void OnTriggerSelected(long itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= trigger_selector.ItemCount)
            return;
        Variant metadata = trigger_selector.GetItemMetadata((int)itemIndex);
        StringName payloadName =
            metadata.VariantType == Variant.Type.String || metadata.VariantType == Variant.Type.StringName
                ? new StringName(metadata.AsString())
                : DefaultTemplate.PayloadName;
        TemplateOption template = ResolveTemplateByPayload(payloadName);
        _selected_payload_name = template.PayloadName;
        _setup_id = template.SetupId;
        _charged = false;
        _selected_template_saved = false;
        RenderTemplateState(template, null, null, null);
    }
}
