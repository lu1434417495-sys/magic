using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleMapPanel
{
    private const string BATTLE_EQUIPMENT_EMPTY_TEXT = "战中队伍共享背包暂无可装备实例。";
    private const string BATTLE_EQUIPMENT_SOURCE_HINT =
        "来源：战斗局部队伍共享背包（不是据点共享仓库）。";
    private const string BATTLE_EQUIPMENT_COMMAND_UNAVAILABLE_TEXT = "战斗换装入口尚未连接运行时。";

    private void _ensure_battle_equipment_ui()
    {
        if (_battle_equipment_overlay != null)
            return;

        if (_battle_equipment_button == null && equipment_button_slot != null)
            _battle_equipment_button = equipment_button_slot.GetNodeOrNull<Button>(
                "BattleEquipmentButton"
            );
        if (_battle_equipment_button != null)
        {
            _battle_equipment_button.TooltipText =
                "打开队伍共享背包（战斗局部）；战中不访问据点共享仓库。";
            _battle_equipment_button.MouseDefaultCursorShape = CursorShape.PointingHand;
            _apply_button_skin(_battle_equipment_button, true, true);
            _battle_equipment_button.Pressed += _open_battle_equipment_panel;
        }

        var hudRoot = GetNodeOrNull<Control>("HudRoot") ?? this;
        _battle_equipment_overlay = new Control
        {
            Name = "BattleEquipmentOverlay",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 1024,
        };
        _set_control_full_rect(_battle_equipment_overlay);
        hudRoot.AddChild(_battle_equipment_overlay);

        var shade = new ColorRect
        {
            Name = "BattleEquipmentShade",
            Color = new Color(0.03f, 0.015f, 0.01f, 0.76f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _set_control_full_rect(shade);
        _battle_equipment_overlay.AddChild(shade);

        var center = new CenterContainer
        {
            Name = "BattleEquipmentCenter",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _set_control_full_rect(center);
        _battle_equipment_overlay.AddChild(center);

        var panel = new PanelContainer
        {
            Name = "BattleEquipmentPanel",
            CustomMinimumSize = new Vector2(820, 520),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                BattleUiTheme.PANEL_BG_ALT(),
                BattleUiTheme.PANEL_EDGE(),
                18,
                2,
                new Color(0.0f, 0.0f, 0.0f, 0.48f),
                14
            )
        );
        center.AddChild(panel);

        var content = new VBoxContainer { Name = "BattleEquipmentContent" };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        var header = new HBoxContainer { Name = "BattleEquipmentHeader" };
        header.AddThemeConstantOverride("separation", 12);
        content.AddChild(header);

        var titleStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleStack.AddThemeConstantOverride("separation", 3);
        header.AddChild(titleStack);

        _battle_equipment_title_label = new Label { Name = "BattleEquipmentTitleLabel" };
        _style_header_label(_battle_equipment_title_label, 22, BattleUiTheme.TEXT_PRIMARY());
        titleStack.AddChild(_battle_equipment_title_label);

        _battle_equipment_meta_label = new Label
        {
            Name = "BattleEquipmentMetaLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _style_header_label(_battle_equipment_meta_label, 12, BattleUiTheme.TEXT_SECONDARY());
        titleStack.AddChild(_battle_equipment_meta_label);

        _battle_equipment_close_button = new Button
        {
            Name = "BattleEquipmentCloseButton",
            Text = "关闭",
            CustomMinimumSize = new Vector2(82, 30),
        };
        _apply_button_skin(_battle_equipment_close_button, true);
        _battle_equipment_close_button.Pressed += _close_battle_equipment_panel;
        header.AddChild(_battle_equipment_close_button);

        _battle_equipment_summary_label = new Label
        {
            Name = "BattleEquipmentSummaryLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _style_header_label(_battle_equipment_summary_label, 13, BattleUiTheme.TEXT_SECONDARY());
        content.AddChild(_battle_equipment_summary_label);

        var body = new HBoxContainer
        {
            Name = "BattleEquipmentBody",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        content.AddChild(body);

        var slotsPanel = _create_equipment_section_panel("BattleEquipmentSlotsPanel");
        slotsPanel.CustomMinimumSize = new Vector2(305, 0);
        body.AddChild(slotsPanel);
        VBoxContainer slotsLayout = _create_equipment_section_layout(slotsPanel);
        slotsLayout.AddChild(_create_equipment_section_title("当前行动单位装备"));
        var slotsScroll = new ScrollContainer
        {
            Name = "BattleEquipmentSlotsScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        slotsLayout.AddChild(slotsScroll);
        _battle_equipment_slot_list = new VBoxContainer
        {
            Name = "BattleEquipmentSlotList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _battle_equipment_slot_list.AddThemeConstantOverride("separation", 6);
        slotsScroll.AddChild(_battle_equipment_slot_list);

        var backpackPanel = _create_equipment_section_panel("BattleEquipmentBackpackPanel");
        backpackPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(backpackPanel);
        VBoxContainer backpackLayout = _create_equipment_section_layout(backpackPanel);
        backpackLayout.AddChild(_create_equipment_section_title("队伍共享背包（战斗局部）"));

        _battle_equipment_backpack_list = new ItemList
        {
            Name = "BattleEquipmentBackpackList",
            CustomMinimumSize = new Vector2(420, 180),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AllowReselect = true,
            FixedIconSize = Vector2I.Zero,
        };
        UiListTheme.Apply(_battle_equipment_backpack_list);
        _battle_equipment_backpack_list.ItemSelected += index =>
            _on_battle_equipment_backpack_selected((int)index);
        backpackLayout.AddChild(_battle_equipment_backpack_list);

        _battle_equipment_details_label = new Label
        {
            Name = "BattleEquipmentDetailsLabel",
            CustomMinimumSize = new Vector2(0, 86),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _battle_equipment_details_label.AddThemeFontSizeOverride("font_size", 12);
        _battle_equipment_details_label.AddThemeColorOverride(
            "font_color",
            BattleUiTheme.TEXT_SECONDARY()
        );
        backpackLayout.AddChild(_battle_equipment_details_label);

        var commandRow = new HBoxContainer { Name = "BattleEquipmentCommandRow" };
        commandRow.AddThemeConstantOverride("separation", 8);
        backpackLayout.AddChild(commandRow);

        _battle_equipment_slot_selector = new OptionButton
        {
            Name = "BattleEquipmentSlotSelector",
            CustomMinimumSize = new Vector2(150, 30),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _battle_equipment_slot_selector.ItemSelected += index =>
            _on_battle_equipment_slot_selected((int)index);
        commandRow.AddChild(_battle_equipment_slot_selector);

        _battle_equipment_equip_button = new Button
        {
            Name = "BattleEquipmentEquipButton",
            Text = "装备",
            CustomMinimumSize = new Vector2(92, 30),
        };
        _apply_button_skin(_battle_equipment_equip_button, true, true);
        _battle_equipment_equip_button.Pressed += _on_battle_equipment_equip_pressed;
        commandRow.AddChild(_battle_equipment_equip_button);

        _battle_equipment_status_label = new Label
        {
            Name = "BattleEquipmentStatusLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _style_header_label(_battle_equipment_status_label, 12, BattleUiTheme.TEXT_SECONDARY());
        content.AddChild(_battle_equipment_status_label);
    }

    private static void _set_control_full_rect(Control control)
    {
        control.LayoutMode = 1;
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
        control.GrowHorizontal = GrowDirection.Both;
        control.GrowVertical = GrowDirection.Both;
    }

    private PanelContainer _create_equipment_section_panel(string section_name)
    {
        var panel = new PanelContainer
        {
            Name = section_name,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                new Color(0.1f, 0.04f, 0.025f, 0.9f),
                BattleUiTheme.PANEL_EDGE_SOFT(),
                10,
                1,
                new Color(0.0f, 0.0f, 0.0f, 0.22f),
                10
            )
        );
        return panel;
    }

    private VBoxContainer _create_equipment_section_layout(PanelContainer panel)
    {
        var layout = new VBoxContainer
        {
            Name = $"{panel.Name}Layout",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        layout.AddThemeConstantOverride("separation", 7);
        panel.AddChild(layout);
        return layout;
    }

    private Label _create_equipment_section_title(string title)
    {
        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        return label;
    }

    public void _open_battle_equipment_panel()
    {
        if (_battle_equipment_overlay == null)
            return;
        if (_battleEquipmentSnapshot == null)
            return;
        _battle_equipment_feedback_text = "";
        _battle_equipment_overlay.Visible = true;
        _refresh_battle_equipment_ui();
    }

    public void _close_battle_equipment_panel()
    {
        if (_battle_equipment_overlay == null)
            return;
        _battle_equipment_overlay.Visible = false;
    }

    private void _refresh_battle_equipment_ui()
    {
        if (_battle_equipment_button != null)
        {
            bool hasSnapshot = _battleEquipmentSnapshot != null;
            _battle_equipment_button.Disabled = !hasSnapshot;
            _battle_equipment_button.TooltipText = hasSnapshot
                ? "打开队伍共享背包（战斗局部）；战中不访问据点共享仓库。"
                : "等待战斗数据。";
        }
        if (_battle_equipment_overlay == null || !_battle_equipment_overlay.Visible)
            return;

        BattleHudEquipmentPanelSnapshot battleEquipmentSnapshot = _battleEquipmentSnapshot;
        string disabledReason = _get_battle_equipment_panel_disabled_reason();
        _battle_equipment_title_label.Text =
            !string.IsNullOrEmpty(battleEquipmentSnapshot?.Title)
                ? battleEquipmentSnapshot.Title
                : "队伍共享背包（战斗局部）";
        _battle_equipment_meta_label.Text =
            !string.IsNullOrEmpty(battleEquipmentSnapshot?.Meta)
                ? battleEquipmentSnapshot.Meta
                : "战中不展示或访问据点共享仓库入口。";
        _battle_equipment_summary_label.Text = battleEquipmentSnapshot?.SummaryText ?? "";
        if (!string.IsNullOrEmpty(_battle_equipment_feedback_text))
            _battle_equipment_status_label.Text = _battle_equipment_feedback_text;
        else if (!string.IsNullOrEmpty(disabledReason))
            _battle_equipment_status_label.Text = disabledReason;
        else
            _battle_equipment_status_label.Text =
                "选择队伍共享背包中的装备实例，装备到当前行动单位。";
        _rebuild_battle_equipment_slot_rows();
        _rebuild_battle_equipment_backpack_list();
        _refresh_battle_equipment_backpack_details();
    }

    private string _get_battle_equipment_panel_disabled_reason()
    {
        if (_battleEquipmentSnapshot == null)
            return "战斗装备数据尚未就绪。";
        if (_battleEquipmentSnapshot.CanChangeEquipment)
            return "";
        return !string.IsNullOrEmpty(_battleEquipmentSnapshot.DisabledReason)
            ? _battleEquipmentSnapshot.DisabledReason
            : "当前不能换装。";
    }

    private void _rebuild_battle_equipment_slot_rows()
    {
        if (_battle_equipment_slot_list == null)
            return;
        _clear_container(_battle_equipment_slot_list);
        IReadOnlyList<BattleHudEquipmentSlotSnapshot> slots =
            _battleEquipmentSnapshot?.Slots ?? Array.Empty<BattleHudEquipmentSlotSnapshot>();
        if (slots.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "当前行动单位暂无 battle-local 装备视图。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            emptyLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_MUTED());
            _battle_equipment_slot_list.AddChild(emptyLabel);
            return;
        }
        foreach (BattleHudEquipmentSlotSnapshot slot in slots)
        {
            _battle_equipment_slot_list.AddChild(_create_battle_equipment_slot_row(slot));
        }
    }

    private Control _create_battle_equipment_slot_row(BattleHudEquipmentSlotSnapshot slot)
    {
        var row = new PanelContainer
        {
            Name = "BattleEquipmentSlotRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeStyleboxOverride(
            "panel",
            _build_panel_style(
                new Color(0.14f, 0.06f, 0.035f, 0.92f),
                new Color(0.34f, 0.22f, 0.13f, 0.82f),
                8,
                1
            )
        );

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        row.AddChild(margin);

        var layout = new HBoxContainer();
        layout.AddThemeConstantOverride("separation", 8);
        margin.AddChild(layout);

        var textStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        textStack.AddThemeConstantOverride("separation", 2);
        layout.AddChild(textStack);

        var title = new Label
        {
            Text =
                $"{(!string.IsNullOrEmpty(slot?.SlotLabel) ? slot.SlotLabel : "槽位")}：{(!string.IsNullOrEmpty(slot?.ItemDisplayName) ? slot.ItemDisplayName : "空")}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        textStack.AddChild(title);

        var detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var detailLines = new List<string>();
        string instanceId = slot?.InstanceId ?? "";
        if (string.IsNullOrEmpty(instanceId))
            detailLines.Add("未装备");
        else
            detailLines.Add($"实例 {instanceId}");
        IReadOnlyList<string> occupiedLabels =
            slot?.OccupiedSlotLabels ?? Array.Empty<string>();
        if (occupiedLabels.Count > 0)
            detailLines.Add($"占用 {string.Join("、", occupiedLabels)}");
        string disabledReason = slot?.DisabledReason ?? "";
        if (!string.IsNullOrEmpty(disabledReason))
            detailLines.Add(disabledReason);
        detail.Text = string.Join("  |  ", detailLines);
        detail.AddThemeFontSizeOverride("font_size", 11);
        detail.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_MUTED());
        textStack.AddChild(detail);

        var button = new Button { Text = "卸下", CustomMinimumSize = new Vector2(62, 28) };
        _apply_button_skin(button, true);
        string panelDisabledReason = _get_battle_equipment_panel_disabled_reason();
        bool canUnequip =
            string.IsNullOrEmpty(panelDisabledReason)
            && slot?.CanUnequip == true
            && !string.IsNullOrEmpty(instanceId);
        button.Disabled = !canUnequip;
        button.TooltipText = canUnequip
            ? ""
            : _resolve_unequip_disabled_reason(slot, panelDisabledReason);
        StringName entrySlotId = new(slot?.EntrySlotId ?? "");
        StringName slotInstanceId = new(instanceId);
        button.Pressed += () => _on_battle_equipment_unequip_pressed(entrySlotId, slotInstanceId);
        layout.AddChild(button);
        return row;
    }

    private string _resolve_unequip_disabled_reason(
        BattleHudEquipmentSlotSnapshot slot,
        string panel_disabled_reason
    )
    {
        if (!string.IsNullOrEmpty(panel_disabled_reason))
            return panel_disabled_reason;
        string disabledReason = slot?.DisabledReason ?? "";
        if (!string.IsNullOrEmpty(disabledReason))
            return disabledReason;
        if (string.IsNullOrEmpty(slot?.InstanceId))
            return "该槽位没有可卸下的装备。";
        return "只能从装备入口槽卸下。";
    }

    private void _rebuild_battle_equipment_backpack_list()
    {
        if (_battle_equipment_backpack_list == null)
            return;
        StringName previousSelection = _selected_backpack_instance_id;
        _battle_equipment_backpack_list.Clear();
        _battleEquipmentBackpackEntriesByIndex.Clear();
        IReadOnlyList<BattleHudBackpackEntrySnapshot> entries =
            _battleEquipmentSnapshot?.BackpackEntries
            ?? Array.Empty<BattleHudBackpackEntrySnapshot>();
        if (entries.Count == 0)
        {
            _battle_equipment_backpack_list.AddItem(BATTLE_EQUIPMENT_EMPTY_TEXT);
            _battle_equipment_backpack_list.SetItemDisabled(0, true);
            _selected_backpack_instance_id = "";
            _selected_backpack_slot_id = "";
            return;
        }
        int selectedIndex = -1;
        int firstIndex = -1;
        foreach (BattleHudBackpackEntrySnapshot entry in entries)
        {
            string itemStatus = entry.CanEquip ? "可装备" : "不可用";
            int itemIndex = _battle_equipment_backpack_list.AddItem(
                $"{entry.DisplayName}  ·  {itemStatus}"
            );
            _battleEquipmentBackpackEntriesByIndex.Add(entry);
            _battle_equipment_backpack_list.SetItemTooltipEnabled(itemIndex, true);
            _battle_equipment_backpack_list.SetItemTooltip(
                itemIndex,
                _build_backpack_entry_tooltip(entry)
            );
            if (firstIndex < 0)
                firstIndex = itemIndex;
            if (new StringName(entry.InstanceId) == previousSelection)
                selectedIndex = itemIndex;
        }
        if (selectedIndex < 0)
            selectedIndex = firstIndex;
        if (selectedIndex >= 0)
        {
            _battle_equipment_backpack_list.Select(selectedIndex);
            BattleHudBackpackEntrySnapshot selectedEntry =
                _get_backpack_entry_at_index(selectedIndex);
            _selected_backpack_instance_id = new StringName(selectedEntry?.InstanceId ?? "");
            _sync_selected_backpack_slot(selectedEntry);
        }
    }

    private string _build_backpack_entry_tooltip(BattleHudBackpackEntrySnapshot entry)
    {
        var lines = new List<string>
        {
            entry?.DisplayName ?? "",
            $"实例：{entry?.InstanceId ?? ""}",
            BATTLE_EQUIPMENT_SOURCE_HINT,
        };
        IReadOnlyList<string> allowedLabels =
            entry?.AllowedSlotLabels ?? Array.Empty<string>();
        if (allowedLabels.Count > 0)
            lines.Add($"可装备槽位：{string.Join("、", allowedLabels)}");
        string disabledReason = entry?.DisabledReason ?? "";
        if (!string.IsNullOrEmpty(disabledReason))
            lines.Add($"不可用：{disabledReason}");
        return string.Join("\n", lines);
    }

    public void _on_battle_equipment_backpack_selected(int index)
    {
        BattleHudBackpackEntrySnapshot entry = _get_backpack_entry_at_index(index);
        if (entry == null)
        {
            _selected_backpack_instance_id = "";
            _selected_backpack_slot_id = "";
        }
        else
        {
            _selected_backpack_instance_id = new StringName(entry.InstanceId);
            _sync_selected_backpack_slot(entry);
        }
        _refresh_battle_equipment_backpack_details();
    }

    public void _on_battle_equipment_slot_selected(int index)
    {
        if (
            _battle_equipment_slot_selector == null
            || index < 0
            || index >= _battle_equipment_slot_ids_by_index.Count
        )
            return;
        _selected_backpack_slot_id = _battle_equipment_slot_ids_by_index[index];
        _refresh_battle_equipment_backpack_details();
    }

    private void _refresh_battle_equipment_backpack_details()
    {
        if (
            _battle_equipment_details_label == null
            || _battle_equipment_slot_selector == null
            || _battle_equipment_equip_button == null
        )
            return;
        BattleHudBackpackEntrySnapshot entry = _get_selected_backpack_entry();
        _battle_equipment_slot_selector.Clear();
        _battle_equipment_slot_ids_by_index.Clear();
        if (entry == null)
        {
            _battle_equipment_details_label.Text =
                BATTLE_EQUIPMENT_EMPTY_TEXT + "\n" + BATTLE_EQUIPMENT_SOURCE_HINT;
            _battle_equipment_slot_selector.Disabled = true;
            _battle_equipment_equip_button.Disabled = true;
            _battle_equipment_equip_button.TooltipText = "请选择战斗局部队伍共享背包中的装备实例。";
            return;
        }

        _sync_selected_backpack_slot(entry);
        IReadOnlyList<string> allowedSlotIds = entry.AllowedSlotIds;
        IReadOnlyList<string> allowedSlotLabels = entry.AllowedSlotLabels;
        int selectedIndex = -1;
        for (int index = 0; index < allowedSlotIds.Count; index++)
        {
            string slotId = allowedSlotIds[index];
            string slotLabel = index < allowedSlotLabels.Count ? allowedSlotLabels[index] : slotId;
            _battle_equipment_slot_selector.AddItem(slotLabel);
            _battle_equipment_slot_ids_by_index.Add(new StringName(slotId));
            if (new StringName(slotId) == _selected_backpack_slot_id)
                selectedIndex = index;
        }
        if (selectedIndex >= 0)
            _battle_equipment_slot_selector.Select(selectedIndex);
        _battle_equipment_slot_selector.Disabled = allowedSlotIds.Count == 0;

        var detailLines = new List<string>
        {
            $"{entry.DisplayName}  |  物品 {entry.ItemId}  |  实例 {entry.InstanceId}",
            $"可装备槽位：{(allowedSlotLabels.Count > 0 ? string.Join("、", allowedSlotLabels) : "无")}",
            !string.IsNullOrEmpty(entry.Description) ? entry.Description : "暂无说明。",
            BATTLE_EQUIPMENT_SOURCE_HINT,
        };
        string disabledReason = _get_equip_disabled_reason(entry);
        if (!string.IsNullOrEmpty(disabledReason))
            detailLines.Add($"不可用：{disabledReason}");
        _battle_equipment_details_label.Text = string.Join("\n", detailLines);
        _battle_equipment_equip_button.Disabled = !string.IsNullOrEmpty(disabledReason);
        _battle_equipment_equip_button.TooltipText = disabledReason;
    }

    private void _sync_selected_backpack_slot(BattleHudBackpackEntrySnapshot entry)
    {
        IReadOnlyList<string> allowedSlotIds =
            entry?.AllowedSlotIds ?? Array.Empty<string>();
        if (allowedSlotIds.Count == 0)
        {
            _selected_backpack_slot_id = "";
            return;
        }
        if (
            !StringNameIsEmpty(_selected_backpack_slot_id)
            && allowedSlotIds.Contains(_selected_backpack_slot_id.ToString())
        )
            return;
        string defaultSlot = entry?.DefaultSlotId ?? "";
        _selected_backpack_slot_id = new StringName(
            allowedSlotIds.Contains(defaultSlot) ? defaultSlot : allowedSlotIds[0]
        );
    }

    private string _get_equip_disabled_reason(BattleHudBackpackEntrySnapshot entry)
    {
        string panelDisabledReason = _get_battle_equipment_panel_disabled_reason();
        if (!string.IsNullOrEmpty(panelDisabledReason))
            return panelDisabledReason;
        string entryDisabledReason = entry?.DisabledReason ?? "";
        if (!string.IsNullOrEmpty(entryDisabledReason))
            return entryDisabledReason;
        if (entry?.CanEquip != true)
            return "该实例当前不能装备。";
        if (StringNameIsEmpty(_selected_backpack_slot_id))
            return "请选择装备槽位。";
        return "";
    }

    private BattleHudBackpackEntrySnapshot _get_selected_backpack_entry()
    {
        if (_battle_equipment_backpack_list == null)
            return null;
        int[] selectedItems = _battle_equipment_backpack_list.GetSelectedItems();
        if (selectedItems.Length == 0)
            return null;
        return _get_backpack_entry_at_index(selectedItems[0]);
    }

    private BattleHudBackpackEntrySnapshot _get_backpack_entry_at_index(int index)
    {
        if (
            _battle_equipment_backpack_list == null
            || index < 0
            || index >= _battle_equipment_backpack_list.ItemCount
            || index >= _battleEquipmentBackpackEntriesByIndex.Count
        )
            return null;
        return _battleEquipmentBackpackEntriesByIndex[index];
    }

    public void _on_battle_equipment_equip_pressed()
    {
        BattleHudBackpackEntrySnapshot entry = _get_selected_backpack_entry();
        if (entry == null)
        {
            _set_battle_equipment_feedback("请选择战斗局部队伍共享背包中的装备实例。");
            return;
        }
        string disabledReason = _get_equip_disabled_reason(entry);
        if (!string.IsNullOrEmpty(disabledReason))
        {
            _set_battle_equipment_feedback(disabledReason);
            return;
        }
        StringName activeUnitId = new(_battleEquipmentSnapshot?.ActiveUnitId ?? "");
        if (StringNameIsEmpty(activeUnitId))
        {
            _set_battle_equipment_feedback("当前没有可换装单位。");
            return;
        }
        StringName itemId = new(entry.ItemId);
        StringName instanceId = new(entry.InstanceId);
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.ChangeEquipment,
            unit_id = activeUnitId,
            target_unit_id = activeUnitId,
            EquipmentOperationKind = BattleEquipmentOperationKind.Equip,
            equipment_slot_id = _selected_backpack_slot_id,
            equipment_item_id = itemId,
            equipment_instance_id = instanceId,
        };
        EmitSignal(
            SignalName.battle_equipment_equip_requested,
            _selected_backpack_slot_id,
            itemId,
            instanceId
        );
        _submit_battle_equipment_command(command);
    }

    public void _on_battle_equipment_unequip_pressed(StringName slot_id, StringName instance_id)
    {
        string panelDisabledReason = _get_battle_equipment_panel_disabled_reason();
        if (!string.IsNullOrEmpty(panelDisabledReason))
        {
            _set_battle_equipment_feedback(panelDisabledReason);
            return;
        }
        StringName activeUnitId = new(_battleEquipmentSnapshot?.ActiveUnitId ?? "");
        if (StringNameIsEmpty(activeUnitId))
        {
            _set_battle_equipment_feedback("当前没有可换装单位。");
            return;
        }
        if (StringNameIsEmpty(slot_id) || StringNameIsEmpty(instance_id))
        {
            _set_battle_equipment_feedback("该槽位没有可卸下的装备。");
            return;
        }
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.ChangeEquipment,
            unit_id = activeUnitId,
            target_unit_id = activeUnitId,
            EquipmentOperationKind = BattleEquipmentOperationKind.Unequip,
            equipment_slot_id = NormalizeStringName(slot_id),
            equipment_instance_id = NormalizeStringName(instance_id),
        };
        EmitSignal(SignalName.battle_equipment_unequip_requested, slot_id, instance_id);
        _submit_battle_equipment_command(command);
    }

    private void _submit_battle_equipment_command(BattleCommand command)
    {
        if (_runtime_proxy == null)
        {
            _set_battle_equipment_feedback(BATTLE_EQUIPMENT_COMMAND_UNAVAILABLE_TEXT);
            return;
        }
        RuntimeCommandResult result = _runtime_proxy.IssueBattleCommand(command);
        string statusText = result.Message ?? "";
        if (string.IsNullOrEmpty(statusText))
            statusText = result.Ok
                ? "换装命令已提交。"
                : BATTLE_EQUIPMENT_COMMAND_UNAVAILABLE_TEXT;
        _battle_equipment_feedback_text = statusText;
        _refresh_battle_equipment_ui();
    }

    private string _resolve_encounter_display_name() =>
        _runtime_proxy?.GetActiveBattleEncounterName() ?? "";

    private void _set_battle_equipment_feedback(string message)
    {
        _battle_equipment_feedback_text = message ?? "";
        _refresh_battle_equipment_ui();
    }
}
