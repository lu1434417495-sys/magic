using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class PartyWarehouseWindow : Control
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

    private WarehouseWindowData _windowData = WarehouseWindowData.Empty();
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
        shade.GuiInput += _on_shade_gui_input;
        stack_list.ItemSelected += index => _on_stack_selected((int)index);
        discard_one_button.Pressed += _on_discard_one_button_pressed;
        discard_all_button.Pressed += _on_discard_all_button_pressed;
        target_member_selector.ItemSelected += index => _on_target_member_selected((int)index);
        use_button.Pressed += _on_use_button_pressed;
        close_button.Pressed += _close_window;
    }

    public void ShowWarehouse(GDictionary window_data)
    {
        WarehouseWindowData normalized = WarehouseWindowData.From(window_data);
        if (normalized == null)
        {
            HideWindow();
            return;
        }
        _windowData = normalized;
        Visible = true;
        RefreshView();
    }

    public void SetWindowData(GDictionary window_data)
    {
        WarehouseWindowData normalized = WarehouseWindowData.From(window_data);
        if (normalized == null)
        {
            HideWindow();
            return;
        }
        _windowData = normalized;
        if (Visible)
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
        _windowData = WarehouseWindowData.Empty();
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

    private void _rebuild_stack_list()
    {
        stack_list.Clear();
        foreach (WarehouseEntry entry in _windowData.Entries)
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
        if (_windowData.Entries.Count == 0)
        {
            _selectedItemId = "";
            _selectedInstanceId = "";
            _selectedEntryIndex = -1;
            return;
        }

        int targetIndex = -1;
        if (_selectedItemId != (StringName)"")
        {
            for (int index = 0; index < _windowData.Entries.Count; index++)
            {
                WarehouseEntry entry = _windowData.Entries[index];
                if (
                    entry.ItemId == _selectedItemId
                    && (
                        _selectedInstanceId == (StringName)""
                        || entry.InstanceId == _selectedInstanceId
                    )
                )
                {
                    targetIndex = index;
                    break;
                }
            }
        }
        if (targetIndex < 0)
            targetIndex = Mathf.Clamp(_selectedEntryIndex, 0, _windowData.Entries.Count - 1);

        _selectedEntryIndex = targetIndex;
        WarehouseEntry selectedEntry = _windowData.Entries[targetIndex];
        _selectedItemId = selectedEntry.ItemId;
        _selectedInstanceId = selectedEntry.InstanceId;
        stack_list.Select(targetIndex);
        stack_list.EnsureCurrentIsVisible();
    }

    private void _rebuild_target_member_selector()
    {
        target_member_selector.Clear();
        if (_windowData.TargetMembers.Count == 0)
        {
            _selectedTargetMemberId = "";
            return;
        }

        int selectedIndex = 0;
        for (int index = 0; index < _windowData.TargetMembers.Count; index++)
        {
            TargetMember member = _windowData.TargetMembers[index];
            target_member_selector.AddItem(member.DisplayName);
            if (member.MemberId == _selectedTargetMemberId)
                selectedIndex = index;
        }

        if (
            _selectedTargetMemberId == (StringName)""
            || !_has_target_member(_selectedTargetMemberId)
        )
        {
            _selectedTargetMemberId = _resolve_default_target_member_id();
            for (int index = 0; index < _windowData.TargetMembers.Count; index++)
            {
                if (_windowData.TargetMembers[index].MemberId == _selectedTargetMemberId)
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        target_member_selector.Select(selectedIndex);
    }

    private void _refresh_details()
    {
        if (_windowData.Entries.Count == 0)
        {
            item_icon.Texture = null;
            details_label.Text = "仓库当前为空。";
            return;
        }

        WarehouseEntry entry = _get_selected_entry_data();
        if (!entry.HasValue)
        {
            item_icon.Texture = null;
            details_label.Text = "请选择一个条目查看详情。";
            return;
        }

        item_icon.Texture = _load_icon_texture(entry.IconPath);
        string storageRuleText = entry.IsStackable
            ? $"每堆上限 {entry.StackLimit}"
            : "不可堆叠，按实例独立占格";
        string storageModeText = entry.StorageMode == "stack" ? "堆叠条目" : "装备实例条目";
        var lines = new List<string>
        {
            $"物品：{entry.DisplayName}",
            $"物品 ID：{entry.ItemId}",
            $"当前条目数量：{entry.Quantity}",
            $"同类总数：{entry.TotalQuantity}",
            $"存储方式：{storageModeText}",
            $"堆叠规则：{storageRuleText}",
            $"说明：{entry.Description}",
        };

        if (entry.InstanceId != (StringName)"")
        {
            lines.Add($"装备实例：{entry.InstanceId}");
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
        WarehouseEntry selectedEntry = _get_selected_entry_data();
        bool hasSelection = selectedEntry.HasValue;
        bool isStackEntry = hasSelection && selectedEntry.StorageMode == "stack";
        bool isEquipmentEntry = hasSelection && selectedEntry.StorageMode == "instance";
        bool isSkillBook = _selected_entry_is_skill_book();
        bool canUseSelectedItem = _can_use_selected_item();
        discard_one_button.Text = isEquipmentEntry ? "丢弃此装备" : "丢弃 1 件";
        discard_one_button.Disabled = !hasSelection;
        discard_all_button.Visible = isStackEntry;
        discard_all_button.Disabled = !isStackEntry;
        target_member_label.Visible = isSkillBook;
        target_member_selector.Visible = isSkillBook;
        target_member_selector.Disabled = !isSkillBook || _windowData.TargetMembers.Count == 0;
        use_button.Visible = isSkillBook;
        use_button.Disabled = !canUseSelectedItem;
    }

    private WarehouseEntry _get_selected_entry_data()
    {
        if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _windowData.Entries.Count)
            return WarehouseEntry.Empty();
        return _windowData.Entries[_selectedEntryIndex];
    }

    private StringName _resolve_default_target_member_id()
    {
        if (
            _windowData.DefaultTargetMemberId != (StringName)""
            && _has_target_member(_windowData.DefaultTargetMemberId)
        )
            return _windowData.DefaultTargetMemberId;
        if (_windowData.TargetMembers.Count == 0)
            return "";
        return _windowData.TargetMembers[0].MemberId;
    }

    private bool _has_target_member(StringName member_id)
    {
        foreach (TargetMember member in _windowData.TargetMembers)
        {
            if (member.MemberId == member_id)
                return true;
        }
        return false;
    }

    private string _get_target_member_display_name(StringName member_id)
    {
        foreach (TargetMember member in _windowData.TargetMembers)
        {
            if (member.MemberId == member_id)
                return member.DisplayName;
        }
        return "";
    }

    private bool _selected_entry_is_skill_book()
    {
        return _get_selected_entry_data().IsSkillBook;
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
        WarehouseEntry entry = _get_selected_entry_data();
        _selectedItemId = entry.ItemId;
        _selectedInstanceId = entry.InstanceId;
        _refresh_details();
        _refresh_controls();
    }

    private void _on_target_member_selected(int index)
    {
        if (
            index < 0
            || index >= target_member_selector.GetItemCount()
            || index >= _windowData.TargetMembers.Count
        )
            _selectedTargetMemberId = "";
        else
            _selectedTargetMemberId = _windowData.TargetMembers[index].MemberId;
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
        WarehouseEntry selectedEntry = _get_selected_entry_data();
        if (
            _selectedItemId == (StringName)""
            || !selectedEntry.HasValue
            || selectedEntry.StorageMode != "stack"
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

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
            return;
        if (
            mouseEvent.ButtonIndex != MouseButton.Left
            && mouseEvent.ButtonIndex != MouseButton.Right
        )
            return;
        _close_window();
    }

    private sealed class WarehouseWindowData
    {
        public string Title { get; private init; } = "共享仓库";
        public string Meta { get; private init; } = "共享背包按堆栈占格，不计算重量。";
        public string SummaryText { get; private init; } = "";
        public string StatusText { get; private init; } = "";
        public List<WarehouseEntry> Entries { get; private init; } = new();
        public List<TargetMember> TargetMembers { get; private init; } = new();
        public StringName DefaultTargetMemberId { get; private init; } = "";

        public static WarehouseWindowData Empty() => new();

        public static WarehouseWindowData From(GDictionary data)
        {
            if (data == null)
                return null;
            foreach (string fieldName in RequiredStringFields)
            {
                if (!HasString(data, fieldName))
                    return null;
            }
            if (!HasArray(data, "entries") || !HasArray(data, "target_members"))
                return null;
            List<WarehouseEntry> entries = ParseEntries(ReadArray(data, "entries"));
            if (entries == null)
                return null;
            List<TargetMember> targetMembers = ParseTargetMembers(ReadArray(data, "target_members"));
            if (targetMembers == null)
                return null;
            return new WarehouseWindowData
            {
                Title = ReadString(data, "title", ""),
                Meta = ReadString(data, "meta", ""),
                SummaryText = ReadString(data, "summary_text", ""),
                StatusText = ReadString(data, "status_text", ""),
                Entries = entries,
                TargetMembers = targetMembers,
                DefaultTargetMemberId = ReadStringName(data, "default_target_member_id"),
            };
        }

        private static List<WarehouseEntry> ParseEntries(GArray entriesData)
        {
            var entries = new List<WarehouseEntry>();
            foreach (Variant entryValue in entriesData)
            {
                if (!entryValue.TryAsDictionary(out GDictionary entryData))
                    return null;
                WarehouseEntry entry = WarehouseEntry.From(entryData);
                if (entry == null)
                    return null;
                entries.Add(entry);
            }
            return entries;
        }

        private static List<TargetMember> ParseTargetMembers(GArray membersData)
        {
            var members = new List<TargetMember>();
            foreach (Variant memberValue in membersData)
            {
                if (!memberValue.TryAsDictionary(out GDictionary memberData))
                    return null;
                TargetMember member = TargetMember.From(memberData);
                if (member.MemberId == (StringName)"" || string.IsNullOrEmpty(member.DisplayName))
                    return null;
                members.Add(member);
            }
            return members;
        }

        private static readonly string[] RequiredStringFields =
        {
            "title",
            "meta",
            "summary_text",
            "status_text",
            "default_target_member_id",
        };
    }

    private sealed class WarehouseEntry
    {
        public bool HasValue { get; private init; }
        public StringName ItemId { get; private init; } = "";
        public StringName InstanceId { get; private init; } = "";
        public string DisplayName { get; private init; } = "";
        public string Description { get; private init; } = "暂无说明。";
        public int Quantity { get; private init; }
        public int TotalQuantity { get; private init; }
        public bool IsStackable { get; private init; }
        public int StackLimit { get; private init; } = 1;
        public string StorageMode { get; private init; } = "";
        public string IconPath { get; private init; } = "";
        public int Rarity { get; private init; }
        public int CurrentDurability { get; private init; }
        public bool IsSkillBook { get; private init; }
        public string GrantedSkillName { get; private init; } = "";

        public static WarehouseEntry Empty() => new();

        public static WarehouseEntry From(GDictionary data)
        {
            if (data == null)
                return null;
            foreach (string fieldName in RequiredStringFields)
            {
                if (!HasString(data, fieldName))
                    return null;
            }
            foreach (string fieldName in RequiredIntFields)
            {
                if (!HasInt(data, fieldName))
                    return null;
            }
            foreach (string fieldName in RequiredBoolFields)
            {
                if (!HasBool(data, fieldName))
                    return null;
            }
            foreach (string fieldName in OptionalStringFields)
            {
                if (
                    TryRead(data, fieldName, out Variant optionalStringValue)
                    && optionalStringValue.VariantType != Variant.Type.String
                )
                    return null;
            }
            foreach (string fieldName in OptionalIntFields)
            {
                if (
                    TryRead(data, fieldName, out Variant optionalIntValue)
                    && optionalIntValue.VariantType != Variant.Type.Int
                )
                    return null;
            }
            StringName itemId = ReadStringName(data, "item_id");
            if (itemId == (StringName)"")
                return null;
            return new WarehouseEntry
            {
                HasValue = true,
                ItemId = itemId,
                InstanceId = ReadStringName(data, "instance_id"),
                DisplayName = ReadString(data, "display_name", ""),
                Description = ReadString(data, "description", ""),
                Quantity = ReadInt(data, "quantity", 0),
                TotalQuantity = ReadInt(data, "total_quantity", 0),
                IsStackable = ReadBool(data, "is_stackable", false),
                StackLimit = ReadInt(data, "stack_limit", 1),
                StorageMode = ReadString(data, "storage_mode", ""),
                IconPath = ReadString(data, "icon", ""),
                Rarity = ReadInt(data, "rarity", 0),
                CurrentDurability = ReadInt(data, "current_durability", 0),
                IsSkillBook = ReadBool(data, "is_skill_book", false),
                GrantedSkillName = ReadString(data, "granted_skill_name", ""),
            };
        }

        private static readonly string[] RequiredStringFields =
        {
            "item_id",
            "display_name",
            "description",
            "icon",
            "item_category",
            "granted_skill_id",
            "storage_mode",
            "granted_skill_name",
        };

        private static readonly string[] RequiredIntFields =
        {
            "quantity",
            "total_quantity",
            "stack_limit",
        };

        private static readonly string[] RequiredBoolFields =
        {
            "is_stackable",
            "is_skill_book",
        };

        private static readonly string[] OptionalStringFields =
        {
            "instance_id",
        };

        private static readonly string[] OptionalIntFields =
        {
            "rarity",
            "current_durability",
        };
    }

    private readonly record struct TargetMember(StringName MemberId, string DisplayName)
    {
        public static TargetMember From(GDictionary data)
        {
            if (data == null || !HasString(data, "member_id") || !HasString(data, "display_name"))
                return default;
            return new TargetMember(
                ReadStringName(data, "member_id"),
                ReadString(data, "display_name", "")
            );
        }
    }

    private static GArray ReadArray(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static bool HasArray(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array;
    }

    private static StringName ReadStringName(GDictionary dict, string key)
    {
        if (!TryRead(dict, key, out Variant value))
            return "";
        return value.VariantType == Variant.Type.String ? new StringName(value.AsString()) : "";
    }

    private static string ReadString(GDictionary dict, string key, string defaultValue)
    {
        if (!TryRead(dict, key, out Variant value))
            return defaultValue;
        return value.VariantType == Variant.Type.String ? value.AsString() : defaultValue;
    }

    private static bool HasString(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.String;
    }

    private static bool ReadBool(GDictionary dict, string key, bool defaultValue)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Bool
            ? value.AsBool()
            : defaultValue;
    }

    private static bool HasBool(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Bool;
    }

    private static int ReadInt(GDictionary dict, string key, int defaultValue)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : defaultValue;
    }

    private static bool HasInt(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Int;
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        if (dict == null || string.IsNullOrEmpty(key))
        {
            value = default;
            return false;
        }
        if (dict.ContainsKey(key))
        {
            value = dict[key];
            return true;
        }
        value = default;
        return false;
    }
}
