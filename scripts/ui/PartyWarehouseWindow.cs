using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class PartyWarehouseWindow : ModalWindowShell
{
    [Signal]
    public delegate void discard_one_requestedEventHandler(
        StringName item_id,
        StringName instance_id
    );

    [Signal]
    public delegate void discard_all_requestedEventHandler(StringName item_id);

    [Signal]
    public delegate void use_requestedEventHandler(StringName item_id, StringName member_id);

    [Signal]
    public delegate void closedEventHandler();

    public ColorRect shade;
    public Label title_label;
    public Label meta_label;
    public ItemList stack_list;
    public Label summary_label;
    public TextureRect item_icon;
    public RichTextLabel details_label;
    public Label status_label;
    public Button discard_one_button;
    public Button discard_all_button;
    public Label target_member_label;
    public OptionButton target_member_selector;
    public Button use_button;
    public Button close_button;

    private WarehouseWindowData _windowData = WarehouseWindowData.Empty;
    private StringName _selectedItemId = "";
    private StringName _selectedInstanceId = "";
    private int _selectedEntryIndex = -1;
    private StringName _selectedTargetMemberId = "";

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        meta_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        stack_list = GetNode<ItemList>(
            "CenterContainer/Panel/MarginContainer/Content/Body/ListColumn/StackList"
        );
        UiListTheme.Apply(stack_list);
        summary_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/SummaryLabel"
        );
        item_icon = GetNode<TextureRect>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/ItemRow/IconFrame/ItemIcon"
        );
        details_label = GetNode<RichTextLabel>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/ItemRow/DetailsLabel"
        );
        details_label.BbcodeEnabled = false;
        status_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/DetailsColumn/StatusLabel"
        );
        discard_one_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Body/Controls/DiscardOneButton"
        );
        discard_all_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Body/Controls/DiscardAllButton"
        );
        target_member_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/Controls/TargetMemberLabel"
        );
        target_member_selector = GetNode<OptionButton>(
            "CenterContainer/Panel/MarginContainer/Content/Body/Controls/TargetMemberSelector"
        );
        use_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Body/Controls/UseButton"
        );
        close_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Header/CloseButton"
        );

        HideWindow();
        stack_list.ItemSelected += index => _on_stack_selected((int)index);
        discard_one_button.Pressed += _on_discard_one_button_pressed;
        discard_all_button.Pressed += _on_discard_all_button_pressed;
        target_member_selector.ItemSelected += index => _on_target_member_selected((int)index);
        use_button.Pressed += _on_use_button_pressed;
        close_button.Pressed += _close_window;
        base._Ready();
    }

    protected override void _on_modal_close_requested() => _close_window();

    internal void ShowWarehouse(WarehouseWindowData window_data)
    {
        if (window_data?.Snapshot == null || !window_data.Snapshot.Available)
        {
            HideWindow();
            return;
        }
        _windowData = window_data;
        Visible = true;
        RefreshView();
    }

    public void RefreshView()
    {
        title_label.Text = _windowData.Title;
        meta_label.Text = _windowData.Meta;
        summary_label.Text = _windowData.SummaryText;
        status_label.Text = _windowData.StatusText;
        _rebuild_stack_list();
        _rebuild_target_member_selector();
        _restore_selection();
        _refresh_details();
        _refresh_controls();
    }

    public void HideWindow()
    {
        Visible = false;
        _windowData = WarehouseWindowData.Empty;
        _selectedItemId = "";
        _selectedInstanceId = "";
        _selectedEntryIndex = -1;
        _selectedTargetMemberId = "";
        stack_list?.Clear();
        target_member_selector?.Clear();
        if (summary_label != null)
            summary_label.Text = "";
        if (item_icon != null)
            item_icon.Texture = null;
        if (details_label != null)
            details_label.Text = "";
        if (status_label != null)
            status_label.Text = "";
    }

    private IReadOnlyList<WarehouseInventoryEntrySnapshot> _entries => _windowData.Snapshot.Entries;

    private IReadOnlyList<WarehouseTargetMemberSnapshot> _target_members =>
        _windowData.Snapshot.TargetMembers;

    // Only equipment entries carry an instance identity; stack entries submit an empty
    // instance id so the runtime keeps resolving them by item id alone.
    private static StringName _entry_instance_id(WarehouseInventoryEntrySnapshot entry) =>
        entry != null && entry.HasEquipmentInstance ? entry.InstanceId ?? "" : "";

    private static StringName _entry_item_id(WarehouseInventoryEntrySnapshot entry) =>
        entry?.ItemId ?? "";

    private void _rebuild_stack_list()
    {
        stack_list.Clear();
        foreach (WarehouseInventoryEntrySnapshot entry in _entries)
        {
            string label = $"{entry.DisplayName}  x{entry.Quantity}";
            if (entry.IsStackable)
                label += $"  |  堆栈 {entry.Quantity}/{entry.StackLimit}";
            else
                label += "  |  按实例占格";
            stack_list.AddItem(label);
        }
    }

    private void _restore_selection()
    {
        if (_entries.Count == 0)
        {
            _selectedItemId = "";
            _selectedInstanceId = "";
            _selectedEntryIndex = -1;
            return;
        }

        int targetIndex = -1;
        if (_selectedItemId != (StringName)"")
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                WarehouseInventoryEntrySnapshot entry = _entries[index];
                if (
                    _entry_item_id(entry) == _selectedItemId
                    && (
                        _selectedInstanceId == (StringName)""
                        || _entry_instance_id(entry) == _selectedInstanceId
                    )
                )
                {
                    targetIndex = index;
                    break;
                }
            }
        }
        if (targetIndex < 0)
            targetIndex = Mathf.Clamp(_selectedEntryIndex, 0, _entries.Count - 1);

        _selectedEntryIndex = targetIndex;
        WarehouseInventoryEntrySnapshot selectedEntry = _entries[targetIndex];
        _selectedItemId = _entry_item_id(selectedEntry);
        _selectedInstanceId = _entry_instance_id(selectedEntry);
        stack_list.Select(targetIndex);
        stack_list.EnsureCurrentIsVisible();
    }

    private void _rebuild_target_member_selector()
    {
        if (_target_members.Count == 0)
        {
            target_member_selector.Clear();
            _selectedTargetMemberId = "";
            return;
        }

        if (
            _selectedTargetMemberId == (StringName)""
            || !_has_target_member(_selectedTargetMemberId)
        )
            _selectedTargetMemberId = _resolve_default_target_member_id();

        var options = new List<(StringName Id, string Label)>();
        foreach (WarehouseTargetMemberSnapshot member in _target_members)
            options.Add((member.MemberId, member.DisplayName));
        UiOptionButtonUtils.Populate(target_member_selector, options, _selectedTargetMemberId);
    }

    private void _refresh_details()
    {
        if (_entries.Count == 0)
        {
            item_icon.Texture = null;
            details_label.Text = "仓库当前为空。";
            return;
        }

        WarehouseInventoryEntrySnapshot entry = _get_selected_entry_data();
        if (entry == null)
        {
            item_icon.Texture = null;
            details_label.Text = "请选择一个条目查看详情。";
            return;
        }

        item_icon.Texture = _load_icon_texture(entry.Icon);
        string storageRuleText = entry.IsStackable
            ? $"每堆上限 {entry.StackLimit}"
            : "不可堆叠，按实例独立占格";
        string storageModeText = entry.StorageMode == (StringName)"stack" ? "堆叠条目" : "装备实例条目";
        var lines = new List<string>
        {
            $"物品：{entry.DisplayName}",
            $"物品 ID：{_entry_item_id(entry)}",
            $"当前条目数量：{entry.Quantity}",
            $"同类总数：{entry.TotalQuantity}",
            $"存储方式：{storageModeText}",
            $"堆叠规则：{storageRuleText}",
            $"说明：{entry.Description}",
        };

        if (entry.HasEquipmentInstance)
        {
            lines.Add($"装备实例：{_entry_instance_id(entry)}");
            lines.Add($"品质：{entry.Rarity}");
            lines.Add($"耐久：{entry.CurrentDurability}");
        }
        if (entry.IsSkillBook)
        {
            lines.Add($"技能书效果：使目标角色学会 {entry.GrantedSkillName}。");
            if (_selectedTargetMemberId != (StringName)"")
                lines.Add($"当前目标：{_get_target_member_display_name(_selectedTargetMemberId)}");
        }

        details_label.Text = string.Join("\n", lines);
    }

    private void _refresh_controls()
    {
        WarehouseInventoryEntrySnapshot selectedEntry = _get_selected_entry_data();
        bool hasSelection = selectedEntry != null;
        bool isStackEntry = hasSelection && selectedEntry.StorageMode == (StringName)"stack";
        bool isEquipmentEntry = hasSelection && selectedEntry.StorageMode == (StringName)"instance";
        bool isSkillBook = _selected_entry_is_skill_book();
        bool canUseSelectedItem = _can_use_selected_item();
        discard_one_button.Text = isEquipmentEntry ? "丢弃此装备" : "丢弃 1 件";
        discard_one_button.Disabled = !hasSelection;
        discard_all_button.Visible = isStackEntry;
        discard_all_button.Disabled = !isStackEntry;
        target_member_label.Visible = isSkillBook;
        target_member_selector.Visible = isSkillBook;
        target_member_selector.Disabled = !isSkillBook || _target_members.Count == 0;
        use_button.Visible = isSkillBook;
        use_button.Disabled = !canUseSelectedItem;
    }

    private WarehouseInventoryEntrySnapshot _get_selected_entry_data()
    {
        if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _entries.Count)
            return null;
        return _entries[_selectedEntryIndex];
    }

    private StringName _resolve_default_target_member_id()
    {
        StringName defaultTargetMemberId = _windowData.Snapshot.DefaultTargetMemberId ?? "";
        if (
            defaultTargetMemberId != (StringName)""
            && _has_target_member(defaultTargetMemberId)
        )
            return defaultTargetMemberId;
        if (_target_members.Count == 0)
            return "";
        return _target_members[0].MemberId;
    }

    private bool _has_target_member(StringName member_id)
    {
        foreach (WarehouseTargetMemberSnapshot member in _target_members)
        {
            if (member.MemberId == member_id)
                return true;
        }
        return false;
    }

    private string _get_target_member_display_name(StringName member_id)
    {
        foreach (WarehouseTargetMemberSnapshot member in _target_members)
        {
            if (member.MemberId == member_id)
                return member.DisplayName;
        }
        return "";
    }

    private bool _selected_entry_is_skill_book()
    {
        return _get_selected_entry_data()?.IsSkillBook ?? false;
    }

    private bool _can_use_selected_item()
    {
        return _selected_entry_is_skill_book()
            && _selectedTargetMemberId != (StringName)""
            && _has_target_member(_selectedTargetMemberId);
    }

    private Texture2D _load_icon_texture(string icon_path)
    {
        if (
            string.IsNullOrEmpty(icon_path)
            || !ResourceLoader.Exists(icon_path, "Texture2D")
        )
            return null;
        return EngineAssetAccess.ResolveBorrowed<Texture2D>(this, icon_path);
    }

    private void _on_stack_selected(int index)
    {
        _selectedEntryIndex = index;
        WarehouseInventoryEntrySnapshot entry = _get_selected_entry_data();
        _selectedItemId = _entry_item_id(entry);
        _selectedInstanceId = _entry_instance_id(entry);
        _refresh_details();
        _refresh_controls();
    }

    private void _on_target_member_selected(int index)
    {
        _selectedTargetMemberId = UiOptionButtonUtils.GetIdAt(target_member_selector, index);
        _refresh_details();
        _refresh_controls();
    }

    private void _on_discard_one_button_pressed()
    {
        if (_selectedItemId == (StringName)"")
            return;
        EmitSignal(SignalName.discard_one_requested, _selectedItemId, _selectedInstanceId);
    }

    private void _on_discard_all_button_pressed()
    {
        WarehouseInventoryEntrySnapshot selectedEntry = _get_selected_entry_data();
        if (
            _selectedItemId == (StringName)""
            || selectedEntry == null
            || selectedEntry.StorageMode != (StringName)"stack"
        )
            return;
        EmitSignal(SignalName.discard_all_requested, _selectedItemId);
    }

    private void _on_use_button_pressed()
    {
        if (!_can_use_selected_item())
            return;
        EmitSignal(SignalName.use_requested, _selectedItemId, _selectedTargetMemberId);
    }

    private void _close_window()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.closed);
    }
}
