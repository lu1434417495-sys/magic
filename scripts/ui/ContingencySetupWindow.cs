using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class ContingencySetupWindow : ModalWindowShell
{
    [Signal]
    public delegate void save_requestedEventHandler(StringName member_id, StringName setup_payload_name);

    [Signal]
    public delegate void charge_requestedEventHandler(StringName member_id, StringName setup_id);

    [Signal]
    public delegate void clear_charge_requestedEventHandler(StringName member_id, StringName setup_id);

    [Signal]
    public delegate void closedEventHandler();

    private IReadOnlyList<ContingencySetupTemplateDefinition> _templates =
        Array.Empty<ContingencySetupTemplateDefinition>();

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
    private StringName _setup_id = "";
    private StringName _selected_payload_name = "";
    private bool _charged;
    private bool _selected_template_saved;
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions = new Dictionary<StringName, SkillDefinition>();
    private IReadOnlyDictionary<StringName, ItemDefinition> _itemDefinitions = new Dictionary<StringName, ItemDefinition>();

    public void SetDisplayDefinitions(
        IReadOnlyDictionary<StringName, SkillDefinition> skills,
        IReadOnlyDictionary<StringName, ItemDefinition> items)
    {
        _skillDefinitions = skills ?? new Dictionary<StringName, SkillDefinition>();
        _itemDefinitions = items ?? new Dictionary<StringName, ItemDefinition>();
    }

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
        base._Ready();
    }

    protected override void _on_modal_close_requested() => CloseWindow();

    public void ShowForMember(
        PartyMemberState member,
        CharacterManagementModule characterManagement,
        IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> templateDefinitions
    )
    {
        SetTemplateDefinitions(templateDefinitions);
        // 静默 HideWindow 会让玩家点开应变设置时"什么都没发生"，既无日志也无提示。
        // 模板为空只可能是内容或装配缺陷，必须报出来。
        ContingencySetupTemplateDefinition defaultTemplate =
            DefaultTemplate
            ?? throw new InvalidOperationException(
                "ContingencySetupWindow was opened without any contingency setup template "
                    + "definitions."
            );
        Visible = true;
        _member_id = member?.member_id ?? new StringName("");
        ContingencyMatrixSetupState setup = ResolveSetup(member);
        // 成员没存过设置时落到默认模板是正常的；但存过、模板却查不到，说明存档与内容
        // 已经漂移。此时套默认模板会渲染出跟 _setup_id 不是一回事的数据，玩家看不出来。
        ContingencySetupTemplateDefinition template =
            setup == null
                ? defaultTemplate
                : ResolveTemplate(setup)
                    ?? throw new InvalidOperationException(
                        $"Stored contingency setup '{setup.SetupId}' has no matching template "
                            + "definition."
                    );
        _setup_id = setup?.SetupId ?? template.TemplateId;
        _selected_payload_name = template.TemplateId;
        _charged = setup?.Charged ?? false;
        _selected_template_saved = setup != null;

        member_status_label.Text = member?.display_name ?? "未选择成员";
        PopulateTriggerOptions(template);
        RenderTemplateState(template, setup, characterManagement, member);
    }

    private void RenderTemplateState(
        ContingencySetupTemplateDefinition template,
        ContingencyMatrixSetupState setup,
        CharacterManagementModule characterManagement,
        PartyMemberState member
    )
    {
        setup_status_label.Text =
            setup != null && setup.SetupId == template.TemplateId
                ? $"{setup.DisplayName} · {(_charged ? "已充能" : "待充能")}"
                : $"{template.DisplayName} · 未保存";

        UiOptionButtonUtils.SetSingle(
            release_mode_selector,
            UiDisplayLabels.ContingencyRelease(setup?.ReleaseMode.ToString() ?? template.ReleaseMode.ToString())
        );
        UiOptionButtonUtils.SetSingle(
            target_resolver_selector,
            UiDisplayLabels.ContingencyTarget(ContingencyContractRules.ToTargetResolverKind(ResolveTargetResolver(setup) ?? ResolveTargetResolver(template)))
        );
        stored_spell_list.Clear();
        if (setup != null && setup.SetupId == template.TemplateId)
            foreach (ContingencyStoredSpellEntryState spell in setup.StoredSpells)
                AddStoredSpell(spell.StoredSkillId, spell.CastLevel, spell.TargetResolver?.ResolverKind ?? ContingencyTargetResolverKind.Unknown);
        else
            foreach (ContingencyStoredSpellTemplateDefinition spell in template.StoredSpells)
                AddStoredSpell(spell.StoredSkillId, spell.MaxCastLevel, spell.TargetResolver?.ResolverKind ?? ContingencyTargetResolverKind.Unknown);

        int matrixLoad = setup?.SetupId == template.TemplateId ? setup.MatrixLoad : template.MatrixLoad;
        int reservedMpMax = setup?.SetupId == template.TemplateId ? setup.ReservedMpMax : 0;
        int effectiveMpMax = Mathf.Max(
            characterManagement?.GetMemberAttributeSnapshot(_member_id)?.GetValue(AttributeService.MP_MAX)
                ?? member?.current_mp
                ?? 0,
            0
        );
        matrix_preview_label.Text =
            $"矩阵负载 {matrixLoad}  ·  预留魔力 {reservedMpMax}  ·  可用魔力上限 {effectiveMpMax}";
        material_preview_label.Text = BuildMaterialPreview(
            template,
            setup?.SetupId == template.TemplateId && setup.Charged
        );
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
        _setup_id = "";
        _selected_payload_name = "";
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

    private ContingencySetupTemplateDefinition ResolveTemplate(ContingencyMatrixSetupState setup)
    {
        if (setup == null)
            return null;
        foreach (ContingencySetupTemplateDefinition definition in _templates)
            if (definition.TemplateId == setup.SetupId)
                return definition;
        return null;
    }

    private ContingencySetupTemplateDefinition ResolveTemplateByPayload(StringName payloadName)
    {
        foreach (ContingencySetupTemplateDefinition definition in _templates)
            if (definition.TemplateId == payloadName)
                return definition;
        // 退回 DefaultTemplate 会让"点了 A 选中 B"变成无声行为。
        throw new InvalidOperationException(
            $"Contingency trigger option '{payloadName}' does not match any loaded template."
        );
    }

    private void PopulateTriggerOptions(ContingencySetupTemplateDefinition selected)
    {
        trigger_selector.Clear();
        int selectedIndex = 0;
        for (int index = 0; index < _templates.Count; index++)
        {
            ContingencySetupTemplateDefinition definition = _templates[index];
            trigger_selector.AddItem(UiDisplayLabels.ContingencyTrigger(definition.Trigger));
            trigger_selector.SetItemMetadata(index, definition.TemplateId.ToString());
            if (definition.TemplateId == selected.TemplateId)
                selectedIndex = index;
        }
        trigger_selector.Selected = selectedIndex;
    }

    private static string ResolveTargetResolver(ContingencySetupTemplateDefinition template)
    {
        return template?.StoredSpells.Count > 0
            ? template.StoredSpells[0].TargetResolver?.Type.ToString() ?? ""
            : "";
    }

    private void AddStoredSpell(StringName skillId, int level, ContingencyTargetResolverKind target)
    {
        string name = _skillDefinitions.TryGetValue(skillId, out var definition) ? definition.DisplayName : skillId.ToString();
        int index = stored_spell_list.AddItem($"{name}  ·  等级 {level}  ·  {UiDisplayLabels.ContingencyTarget(target)}");
        stored_spell_list.SetItemMetadata(index, skillId.ToString());
    }

    private string BuildMaterialPreview(
        ContingencySetupTemplateDefinition template,
        bool charged
    )
    {
        if (template?.ChargeMaterialCosts == null || template.ChargeMaterialCosts.Count == 0)
            return "";
        return string.Join(
            "\n",
            template.ChargeMaterialCosts.Select(cost =>
                $"充能材料：{(_itemDefinitions.TryGetValue(cost.ItemId, out var item) ? item.DisplayName : cost.ItemId.ToString())} × {cost.Quantity}  ·  已投入 {(charged ? cost.Quantity : 0)}"
            )
        );
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
        // PopulateTriggerOptions 只写字符串 metadata；出现别的类型说明选项不是它填的。
        Variant metadata = trigger_selector.GetItemMetadata((int)itemIndex);
        if (
            metadata.VariantType != Variant.Type.String
            && metadata.VariantType != Variant.Type.StringName
        )
        {
            throw new InvalidOperationException(
                $"Contingency trigger option {itemIndex} carries {metadata.VariantType} metadata "
                    + "instead of a template id."
            );
        }
        ContingencySetupTemplateDefinition template = ResolveTemplateByPayload(
            new StringName(metadata.AsString())
        );
        _selected_payload_name = template.TemplateId;
        _setup_id = template.TemplateId;
        _charged = false;
        _selected_template_saved = false;
        RenderTemplateState(template, null, null, null);
    }

    private ContingencySetupTemplateDefinition DefaultTemplate =>
        _templates.Count > 0 ? _templates[0] : null;

    private void SetTemplateDefinitions(
        IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> definitions
    )
    {
        _templates = definitions == null
            ? Array.Empty<ContingencySetupTemplateDefinition>()
            : definitions.Values
                .Where(definition => definition != null)
                .OrderBy(definition => definition.TemplateId.ToString(), StringComparer.Ordinal)
                .ToArray();
    }
}
