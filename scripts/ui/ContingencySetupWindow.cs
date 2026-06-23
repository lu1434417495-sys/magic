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

    private static readonly StringName V1PayloadName = "hp_mirror_self";
    private static readonly StringName V1SetupId = "hp_mirror_self";

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
    private StringName _setup_id = V1SetupId;
    private bool _charged;

    public override void _Ready()
    {
        member_status_label = GetNode<Label>("%MemberStatusLabel");
        setup_status_label = GetNode<Label>("%SetupStatusLabel");
        trigger_selector = GetNode<OptionButton>("%TriggerSelector");
        release_mode_selector = GetNode<OptionButton>("%ReleaseModeSelector");
        stored_spell_list = GetNode<ItemList>("%StoredSpellList");
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
        HideWindow();
    }

    public void ShowForMember(PartyMemberState member, CharacterManagementModule characterManagement)
    {
        Visible = true;
        _member_id = member?.member_id ?? new StringName("");
        ContingencyMatrixSetupState setup = ResolveSetup(member);
        _setup_id = setup?.SetupId ?? V1SetupId;
        _charged = setup?.Charged ?? false;

        member_status_label.Text =
            member != null
                ? $"{member.display_name} | member_id={member.member_id}"
                : "member_id=";
        setup_status_label.Text =
            setup != null
                ? $"{setup.SetupId} | {setup.DisplayName} | charged={(_charged ? "yes" : "no")}"
                : "hp_mirror_self | 未保存";

        SetSingleOption(trigger_selector, setup?.Trigger?.ToDictionary()?["type"].AsString() ?? "hp_below_percent");
        SetSingleOption(release_mode_selector, setup?.ReleaseMode.ToString() ?? "burst_release");
        SetSingleOption(target_resolver_selector, ResolveTargetResolver(setup));
        stored_spell_list.Clear();
        if (setup != null)
        {
            foreach (ContingencyStoredSpellEntryState spell in setup.StoredSpells)
            {
                stored_spell_list.AddItem(
                    $"{spell.StoredSkillId}@{spell.CastLevel}:{spell.TargetResolver?.Type}"
                );
            }
        }
        else
        {
            stored_spell_list.AddItem("mage_mirror_image@2:self");
        }

        int matrixLoad = setup?.MatrixLoad ?? 3;
        int reservedMpMax = setup?.ReservedMpMax ?? 0;
        matrix_preview_label.Text = $"matrix_load={matrixLoad} | reserved_mp_max={reservedMpMax}";
        material_preview_label.Text =
            $"special_contingency_gem:{GetMaterialQuantity(setup)}";
        save_button.Disabled = _member_id == "" || _charged;
        charge_button.Disabled = _member_id == "" || _charged;
        clear_charge_button.Disabled = _member_id == "" || !_charged;
        clear_charge_confirmation_label.Visible = _charged;
        clear_charge_confirmation_label.Text =
            _charged ? "清除充能后材料不返还，当前 MP 不恢复。" : "";
    }

    public void HideWindow()
    {
        Visible = false;
        _member_id = "";
        _setup_id = V1SetupId;
        _charged = false;
        if (clear_charge_confirmation_label != null)
            clear_charge_confirmation_label.Text = "";
    }

    private static ContingencyMatrixSetupState ResolveSetup(PartyMemberState member)
    {
        if (member == null)
            return null;
        if (member.TryGetContingencySetupTyped(V1SetupId, out ContingencyMatrixSetupState setup))
            return setup;
        return null;
    }

    private static string ResolveTargetResolver(ContingencyMatrixSetupState setup)
    {
        if (setup == null || setup.StoredSpells.Count == 0)
            return "self";
        return setup.StoredSpells[0].TargetResolver?.Type.ToString() ?? "self";
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
        EmitSignal(SignalName.save_requested, _member_id, V1PayloadName);
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
}
