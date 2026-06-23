using System.Collections;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class PartyManagementWindow : Control
{
    [Signal]
    public delegate void leader_change_requestedEventHandler(StringName member_id);

    [Signal]
    public delegate void roster_change_requestedEventHandler(
        GStringNameArray active_member_ids,
        GStringNameArray reserve_member_ids
    );

    [Signal]
    public delegate void warehouse_requestedEventHandler();

    [Signal]
    public delegate void contingency_setup_requestedEventHandler(StringName member_id);

    [Signal]
    public delegate void closedEventHandler();

    private const int MaxActiveMemberCount = 4;
    private const float PanelViewportRatio = 0.5f;
    private static readonly Vector2 MinPanelSize = new(860.0f, 540.0f);
    private static readonly Vector2 ViewportSafeMargin = new(48.0f, 30.0f);
    private const int ContentMargin = 24;
    private const int ContentMarginVertical = 22;

    public ColorRect shade;
    public Control panel;
    public MarginContainer content_margin;
    public Label title_label;
    public Label meta_label;
    public ItemList active_list;
    public ItemList reserve_list;
    public Control lists_column;
    public Button set_leader_button;
    public Control controls_column;
    public Button move_to_active_button;
    public Button move_to_reserve_button;
    public Button warehouse_button;
    public Button contingency_setup_button;
    public RichTextLabel overview_label;
    public RichTextLabel attributes_label;
    public RichTextLabel equipment_label;
    public RichTextLabel skills_label;
    public RichTextLabel professions_label;
    public Label status_label;
    public Button close_button;

    public PartyState _party_state;
    private IReadOnlyDictionary<StringName, AchievementDef> _achievement_defs =
        new Dictionary<StringName, AchievementDef>();
    private IReadOnlyDictionary<StringName, ItemDef> _item_defs =
        new Dictionary<StringName, ItemDef>();
    private IReadOnlyDictionary<StringName, SkillDef> _skill_defs =
        new Dictionary<StringName, SkillDef>();
    private IReadOnlyDictionary<StringName, ProfessionDef> _profession_defs =
        new Dictionary<StringName, ProfessionDef>();
    public CharacterManagementModule _character_management;
    public StringName _leader_member_id = "";
    public StringName _main_character_member_id = "";
    public StringName _selected_member_id = "";

    private readonly Dictionary<StringName, PartyMemberState> _memberStates = new();
    private readonly List<StringName> _activeMemberIds = new();
    private readonly List<StringName> _reserveMemberIds = new();
    private readonly List<StringName> _activeListMemberIds = new();
    private readonly List<StringName> _reserveListMemberIds = new();

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        panel = GetNode<Control>("%Panel");
        content_margin = GetNode<MarginContainer>("%MarginContainer");
        title_label = GetNode<Label>("%TitleLabel");
        meta_label = GetNode<Label>("%MetaLabel");
        active_list = GetNode<ItemList>("%ActiveList");
        reserve_list = GetNode<ItemList>("%ReserveList");
        lists_column = GetNode<Control>("%Lists");
        set_leader_button = GetNode<Button>("%SetLeaderButton");
        controls_column = GetNode<Control>("%Controls");
        move_to_active_button = GetNode<Button>("%MoveToActiveButton");
        move_to_reserve_button = GetNode<Button>("%MoveToReserveButton");
        warehouse_button = GetNode<Button>("%WarehouseButton");
        contingency_setup_button = GetNode<Button>("%ContingencySetupButton");
        overview_label = GetNode<RichTextLabel>("%OverviewLabel");
        attributes_label = GetNode<RichTextLabel>("%AttributesLabel");
        equipment_label = GetNode<RichTextLabel>("%EquipmentLabel");
        skills_label = GetNode<RichTextLabel>("%SkillsLabel");
        professions_label = GetNode<RichTextLabel>("%ProfessionsLabel");
        status_label = GetNode<Label>("%StatusLabel");
        close_button = GetNode<Button>("%CloseButton");

        HideWindow();
        Resized += _update_responsive_layout;
        _update_responsive_layout();
        shade.GuiInput += _on_shade_gui_input;
        close_button.Pressed += _close_window;
        active_list.ItemSelected += index => _on_active_item_selected((int)index);
        reserve_list.ItemSelected += index => _on_reserve_item_selected((int)index);
        set_leader_button.Pressed += _on_set_leader_button_pressed;
        move_to_active_button.Pressed += _on_move_to_active_button_pressed;
        move_to_reserve_button.Pressed += _on_move_to_reserve_button_pressed;
        warehouse_button.Pressed += _on_warehouse_button_pressed;
        contingency_setup_button.Pressed += _on_contingency_setup_button_pressed;
    }

    public void ShowParty(PartyState party_state)
    {
        SetPartyState(party_state);
        Visible = true;
        _update_responsive_layout();
        title_label.Text = "人物管理";
        meta_label.Text =
            $"查看成员属性、装备、技能和职业；上阵人数上限 {MaxActiveMemberCount}，主角必须保持上阵。";
        RefreshView();
    }

    public void SetAchievementDefs(
        IReadOnlyDictionary<StringName, AchievementDef> achievement_defs
    )
    {
        _achievement_defs = achievement_defs ?? new Dictionary<StringName, AchievementDef>();
        if (Visible)
            RefreshView();
    }

    public void SetItemDefs(IReadOnlyDictionary<StringName, ItemDef> item_defs)
    {
        _item_defs = item_defs ?? new Dictionary<StringName, ItemDef>();
        if (Visible)
            RefreshView();
    }

    public void SetSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skill_defs)
    {
        _skill_defs = skill_defs ?? new Dictionary<StringName, SkillDef>();
        if (Visible)
            RefreshView();
    }

    public void SetProfessionDefs(IReadOnlyDictionary<StringName, ProfessionDef> profession_defs)
    {
        _profession_defs = profession_defs ?? new Dictionary<StringName, ProfessionDef>();
        if (Visible)
            RefreshView();
    }

    public void SetCharacterManagement(CharacterManagementModule character_management)
    {
        _character_management = character_management;
        if (Visible)
            RefreshView();
    }

    public void SetPartyState(PartyState party_state)
    {
        _capture_party_state(party_state);
        if (Visible)
            RefreshView();
    }

    public void RefreshView()
    {
        _rebuild_lists();
        _restore_selection();
        _refresh_controls();
        _refresh_details();
    }

    public PartyState GetPartyState() => _party_state;

    public StringName GetSelectedMemberId() => _selected_member_id;

    public bool SelectMember(StringName member_id)
    {
        PartyMemberState memberState = _resolve_member_state(member_id);
        if (memberState == null)
            return false;

        if (_activeMemberIds.Contains(member_id))
            _select_active_member_id(member_id);
        else if (_reserveMemberIds.Contains(member_id))
            _select_reserve_member_id(member_id);
        else
            return false;

        _refresh_controls();
        _refresh_details();
        return true;
    }

    public void HideWindow()
    {
        Visible = false;
        _party_state = null;
        _memberStates.Clear();
        _leader_member_id = "";
        _main_character_member_id = "";
        _activeMemberIds.Clear();
        _reserveMemberIds.Clear();
        _selected_member_id = "";
        active_list?.Clear();
        reserve_list?.Clear();
        _clear_detail_labels();
        if (status_label != null)
            status_label.Text = "";
        if (contingency_setup_button != null)
            contingency_setup_button.Disabled = true;
    }

    public void _update_responsive_layout()
    {
        if (panel == null || content_margin == null)
            return;

        Vector2 viewportSize = Size;
        if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
            viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
            return;

        Vector2 availableSize = new(
            Mathf.Max(viewportSize.X - ViewportSafeMargin.X * 2.0f, 320.0f),
            Mathf.Max(viewportSize.Y - ViewportSafeMargin.Y * 2.0f, 320.0f)
        );
        Vector2 preferredSize = viewportSize * PanelViewportRatio;
        Vector2 panelSize = new(
            Mathf.Clamp(
                preferredSize.X,
                Mathf.Min(MinPanelSize.X, availableSize.X),
                availableSize.X
            ),
            Mathf.Clamp(
                preferredSize.Y,
                Mathf.Min(MinPanelSize.Y, availableSize.Y),
                availableSize.Y
            )
        );
        panel.CustomMinimumSize = panelSize;

        content_margin.AddThemeConstantOverride("margin_left", ContentMargin);
        content_margin.AddThemeConstantOverride("margin_top", ContentMarginVertical);
        content_margin.AddThemeConstantOverride("margin_right", ContentMargin);
        content_margin.AddThemeConstantOverride("margin_bottom", ContentMarginVertical);

        float contentWidth = Mathf.Max(panelSize.X - ContentMargin * 2.0f, 320.0f);
        float listWidth = Mathf.Clamp(contentWidth * 0.26f, 200.0f, 260.0f);
        float controlsWidth = Mathf.Clamp(contentWidth * 0.14f, 112.0f, 136.0f);
        if (contentWidth < 760.0f)
        {
            listWidth = 190.0f;
            controlsWidth = 110.0f;
        }
        lists_column.CustomMinimumSize = new Vector2(listWidth, lists_column.CustomMinimumSize.Y);
        controls_column.CustomMinimumSize = new Vector2(
            controlsWidth,
            controls_column.CustomMinimumSize.Y
        );
        foreach (
            Button button in new[]
            {
                set_leader_button,
                move_to_active_button,
                move_to_reserve_button,
                warehouse_button,
                contingency_setup_button,
            }
        )
        {
            if (button != null)
                button.CustomMinimumSize = new Vector2(controlsWidth, button.CustomMinimumSize.Y);
        }

        float textHeight = Mathf.Max(panelSize.Y - ContentMarginVertical * 2.0f - 136.0f, 300.0f);
        overview_label.CustomMinimumSize = new Vector2(
            overview_label.CustomMinimumSize.X,
            textHeight
        );
        foreach (
            RichTextLabel label in new[]
            {
                attributes_label,
                equipment_label,
                skills_label,
                professions_label,
            }
        )
        {
            if (label != null)
                label.CustomMinimumSize = new Vector2(label.CustomMinimumSize.X, textHeight);
        }
        active_list.CustomMinimumSize = new Vector2(
            active_list.CustomMinimumSize.X,
            Mathf.Max(140.0f, textHeight * 0.48f)
        );
        reserve_list.CustomMinimumSize = new Vector2(
            reserve_list.CustomMinimumSize.X,
            Mathf.Max(122.0f, textHeight * 0.42f)
        );
    }

    private void _capture_party_state(PartyState partyState)
    {
        _party_state = partyState;
        _memberStates.Clear();
        _activeMemberIds.Clear();
        _reserveMemberIds.Clear();
        _selected_member_id = "";

        if (partyState == null)
        {
            _leader_member_id = "";
            _main_character_member_id = "";
            return;
        }

        _leader_member_id = partyState.leader_member_id;
        _main_character_member_id = partyState.GetResolvedMainCharacterMemberId();
        foreach (
            StringName memberId in ProgressionDataUtils.to_string_name_array(
                partyState.active_member_ids
            )
        )
            _activeMemberIds.Add(memberId);
        foreach (
            StringName memberId in ProgressionDataUtils.to_string_name_array(
                partyState.reserve_member_ids
            )
        )
            _reserveMemberIds.Add(memberId);
        foreach (var key in partyState.member_states.Keys)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(key);
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            if (memberId != (StringName)"" && memberState != null)
                _memberStates[memberId] = memberState;
        }
    }

    private void _rebuild_lists()
    {
        active_list.Clear();
        reserve_list.Clear();
        _activeListMemberIds.Clear();
        _reserveListMemberIds.Clear();

        foreach (StringName memberId in _activeMemberIds)
        {
            PartyMemberState memberState = _resolve_member_state(memberId);
            if (memberState == null)
                continue;
            string label =
                $"{(memberId == _leader_member_id ? "队长 · " : "")}{_build_member_list_label(memberState)}";
            active_list.AddItem(label);
            _activeListMemberIds.Add(memberId);
        }

        foreach (StringName memberId in _reserveMemberIds)
        {
            PartyMemberState memberState = _resolve_member_state(memberId);
            if (memberState == null)
                continue;
            reserve_list.AddItem(_build_member_list_label(memberState));
            _reserveListMemberIds.Add(memberId);
        }
    }

    private static string _build_member_list_label(PartyMemberState memberState)
    {
        return $"{memberState.display_name}  |  HP {memberState.current_hp}  MP {memberState.current_mp}";
    }

    private void _restore_selection()
    {
        if (_selected_member_id == (StringName)"")
        {
            if (_activeMemberIds.Count > 0)
                _select_active_member_id(_activeMemberIds[0]);
            else if (_reserveMemberIds.Count > 0)
                _select_reserve_member_id(_reserveMemberIds[0]);
            return;
        }

        if (_activeMemberIds.Contains(_selected_member_id))
            _select_active_member_id(_selected_member_id);
        else if (_reserveMemberIds.Contains(_selected_member_id))
            _select_reserve_member_id(_selected_member_id);
    }

    private void _refresh_controls()
    {
        bool hasSelection = _selected_member_id != (StringName)"";
        bool canSetLeader = hasSelection && _activeMemberIds.Contains(_selected_member_id);
        bool canMoveToActive =
            hasSelection
            && _reserveMemberIds.Contains(_selected_member_id)
            && _activeMemberIds.Count < MaxActiveMemberCount;
        bool canMoveToReserve =
            hasSelection
            && _activeMemberIds.Contains(_selected_member_id)
            && _activeMemberIds.Count > 1
            && _selected_member_id != _main_character_member_id;

        set_leader_button.Disabled = !canSetLeader;
        move_to_active_button.Disabled = !canMoveToActive;
        move_to_reserve_button.Disabled = !canMoveToReserve;
        warehouse_button.Disabled = _party_state == null;
        contingency_setup_button.Disabled = !hasSelection;
    }

    private void _refresh_details()
    {
        if (_memberStates.Count == 0 && _party_state == null)
        {
            overview_label.Text = "当前没有队伍成员数据。";
            _clear_detail_tabs();
            status_label.Text =
                $"上阵 {_activeMemberIds.Count} / {MaxActiveMemberCount}  |  替补 {_reserveMemberIds.Count}";
            return;
        }

        if (_selected_member_id == (StringName)"")
        {
            overview_label.Text = "请选择一名成员查看详情。";
            _clear_detail_tabs();
            status_label.Text = "";
            return;
        }

        PartyMemberState memberState = _resolve_member_state(_selected_member_id);
        if (memberState == null)
        {
            overview_label.Text = "当前成员数据不可用。";
            _clear_detail_tabs();
            status_label.Text = "";
            return;
        }

        AttributeSnapshot snapshot = _build_attribute_snapshot(memberState);
        UnitProgress progression = memberState.progression as UnitProgress;
        overview_label.Text = _build_overview_text(memberState, snapshot);
        attributes_label.Text = _build_attributes_text(memberState, snapshot);
        equipment_label.Text = string.Join("\n", _build_equipment_detail_lines(memberState));
        skills_label.Text = string.Join("\n", _build_skill_detail_lines(progression, snapshot));
        professions_label.Text = string.Join("\n", _build_profession_detail_lines(progression));
        status_label.Text =
            $"当前队长：{_leader_member_id}  |  上阵 {_activeMemberIds.Count} / {MaxActiveMemberCount}  |  替补 {_reserveMemberIds.Count}";
    }

    private AttributeSnapshot _build_attribute_snapshot(PartyMemberState memberState)
    {
        if (memberState == null || _character_management == null)
            return null;
        return _character_management.GetMemberAttributeSnapshotForEquipmentView(
            memberState.member_id,
            memberState.equipment_state
        );
    }

    private PartyMemberState _resolve_member_state(StringName memberId)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == (StringName)"")
            return null;

        if (_memberStates.TryGetValue(normalizedMemberId, out PartyMemberState memberState))
            return memberState;

        PartyMemberState resolvedMemberState = _party_state?.GetMemberState(normalizedMemberId);
        if (resolvedMemberState != null)
            _memberStates[normalizedMemberId] = resolvedMemberState;
        return resolvedMemberState;
    }

    private string _build_overview_text(PartyMemberState memberState, AttributeSnapshot snapshot)
    {
        UnitProgress progression = memberState.progression as UnitProgress;
        var lines = new List<string>
        {
            $"姓名：{memberState.display_name}",
            $"成员 ID：{memberState.member_id}",
            $"编成：{(_activeMemberIds.Contains(memberState.member_id) ? "上阵" : "替补")}",
            $"主角：{(memberState.member_id == _main_character_member_id ? "是" : "否")}",
            $"队长：{(memberState.member_id == _leader_member_id ? "是" : "否")}",
            $"身份：{(memberState.member_id == _main_character_member_id ? "主角 " : "")}{(memberState.member_id == _leader_member_id ? "队长" : "")}",
            $"控制：{memberState.control_mode}",
            $"等级：{(progression != null ? progression.character_level : 0)}",
        };
        lines.AddRange(_build_identity_overview_lines(memberState));
        lines.Add($"当前资源：{_format_current_resource_summary(memberState, snapshot)}");
        lines.Add("");
        lines.Add("核心属性：");
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            lines.Add(
                $"- {_get_attribute_label(attributeId)}：{_get_snapshot_value(snapshot, attributeId)}"
            );
        lines.Add("");
        lines.Add("成就摘要：");
        lines.AddRange(_build_achievement_summary_lines(progression));
        return string.Join("\n", lines);
    }

    private List<string> _build_identity_overview_lines(PartyMemberState memberState)
    {
        var lines = new List<string>();
        GDictionary summary = _get_identity_summary(
            memberState != null ? memberState.member_id : new StringName("")
        );
        if (summary.Count == 0)
        {
            if (memberState != null)
                lines.Add($"体型：{memberState.body_size_category}（{memberState.body_size}）");
            return lines;
        }
        lines.Add($"种族：{DictString(summary, "race_label", "")}");
        string subraceLabel = DictString(summary, "subrace_label", "").StripEdges();
        if (!string.IsNullOrEmpty(subraceLabel))
            lines.Add($"亚种：{subraceLabel}");
        lines.Add(
            $"年龄：{DictInt(summary, "age_years", 0)} 岁  |  自然阶段：{DictString(summary, "natural_age_stage_label", "")}  |  有效阶段：{DictString(summary, "effective_age_stage_label", "")}"
        );
        lines.Add(
            $"体型：{DictString(summary, "body_size_category", "")}（{DictInt(summary, "body_size", 0)}）"
        );
        string bloodlineLabel = DictString(summary, "bloodline_label", "").StripEdges();
        string bloodlineStageLabel = DictString(summary, "bloodline_stage_label", "").StripEdges();
        if (!string.IsNullOrEmpty(bloodlineLabel))
            lines.Add(
                $"血脉：{bloodlineLabel}{(!string.IsNullOrEmpty(bloodlineStageLabel) ? $" · {bloodlineStageLabel}" : "")}"
            );
        string ascensionLabel = DictString(summary, "ascension_label", "").StripEdges();
        string ascensionStageLabel = DictString(summary, "ascension_stage_label", "").StripEdges();
        if (!string.IsNullOrEmpty(ascensionLabel))
            lines.Add(
                $"升华：{ascensionLabel}{(!string.IsNullOrEmpty(ascensionStageLabel) ? $" · {ascensionStageLabel}" : "")}"
            );
        return lines;
    }

    private string _format_current_resource_summary(
        PartyMemberState memberState,
        AttributeSnapshot snapshot
    )
    {
        UnitProgress progression = memberState?.progression as UnitProgress;
        var parts = new List<string>
        {
            $"HP {(memberState != null ? memberState.current_hp : 0)} / {_get_snapshot_value(snapshot, AttributeService.ToStringName(AttributeIdKind.HpMax))}",
        };
        if (
            _is_combat_resource_visible(
                progression,
                CombatResourceIds.ToStringName(CombatResourceIdKind.Mp),
                memberState?.current_mp ?? 0,
                _get_snapshot_value(snapshot, AttributeService.ToStringName(AttributeIdKind.MpMax))
            )
        )
            parts.Add(
                $"MP {(memberState != null ? memberState.current_mp : 0)} / {_get_snapshot_value(snapshot, AttributeService.ToStringName(AttributeIdKind.MpMax))}"
            );
        if (
            _is_combat_resource_visible(
                progression,
                CombatResourceIds.ToStringName(CombatResourceIdKind.Aura),
                memberState?.current_aura ?? 0,
                _get_snapshot_value(snapshot, AttributeService.ToStringName(AttributeIdKind.AuraMax))
            )
        )
            parts.Add(
                $"Aura {(memberState != null ? memberState.current_aura : 0)} / {_get_snapshot_value(snapshot, AttributeService.ToStringName(AttributeIdKind.AuraMax))}"
            );
        return string.Join("  ", parts);
    }

    private static bool _is_combat_resource_visible(
        UnitProgress progression,
        StringName resourceId,
        int currentValue,
        int maxValue
    )
    {
        if (progression != null && progression.HasCombatResourceUnlocked(resourceId))
            return true;
        return currentValue > 0 || maxValue > 0;
    }

    private GDictionary _get_identity_summary(StringName memberId)
    {
        if (memberId == (StringName)"" || _character_management == null)
            return new GDictionary();
        return _character_management.GetIdentitySummaryForMember(memberId) ?? new GDictionary();
    }

    private string _build_attributes_text(PartyMemberState memberState, AttributeSnapshot snapshot)
    {
        if (memberState == null)
            return "当前成员数据不可用。";
        var lines = new List<string> { "基础属性：" };
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            lines.Add(
                $"- {_get_attribute_label(attributeId)}：{_get_snapshot_value(snapshot, attributeId)}"
            );
        lines.Add("");
        lines.Add("资源属性：");
        foreach (StringName attributeId in AttributeService.RESOURCE_ATTRIBUTE_IDS)
            lines.Add(
                $"- {_get_attribute_label(attributeId)}：{_get_snapshot_value(snapshot, attributeId)}"
            );
        lines.Add("");
        lines.Add("战斗属性：");
        foreach (StringName attributeId in AttributeService.COMBAT_ATTRIBUTE_IDS)
            lines.Add(
                $"- {_get_attribute_label(attributeId)}：{_get_snapshot_value(snapshot, attributeId)}"
            );
        lines.Add("");
        lines.Add("命运：");
        lines.Add($"- 出生隐藏幸运：{memberState.GetHiddenLuckAtBirth()}");
        lines.Add($"- 信仰幸运加值：{memberState.GetFaithLuckBonus()}");
        lines.Add($"- 有效幸运：{memberState.GetEffectiveLuck()}");
        lines.Add($"- 战斗幸运：{memberState.GetCombatLuckScore()}");
        lines.Add($"- 掉落幸运：{memberState.GetDropLuck()}");
        return string.Join("\n", lines);
    }

    private List<string> _build_equipment_detail_lines(PartyMemberState memberState)
    {
        var lines = new List<string>();
        if (memberState == null)
        {
            lines.Add("当前成员数据不可用。");
            return lines;
        }

        EquipmentState equipmentState = memberState.equipment_state;
        int filledCount = 0;
        foreach (StringName slotId in EquipmentRules.GetAllSlotIdsTyped())
        {
            StringName entrySlotId = GetEntrySlotForSlot(equipmentState, slotId);
            if (entrySlotId != (StringName)"" && entrySlotId != slotId)
            {
                lines.Add(
                    $"{EquipmentRules.GetSlotLabel(slotId)}：由{EquipmentRules.GetSlotLabel(entrySlotId)}占用"
                );
                continue;
            }
            StringName itemId = GetEquippedItemId(equipmentState, slotId);
            if (itemId == (StringName)"")
            {
                lines.Add($"{EquipmentRules.GetSlotLabel(slotId)}：空");
                continue;
            }
            filledCount += 1;
            ItemDef itemDef = GetTypedObject(_item_defs, itemId);
            lines.Add($"{EquipmentRules.GetSlotLabel(slotId)}：{_get_item_display_name(itemId)}");
            if (itemDef != null)
            {
                string typeLabel = _get_equipment_type_label(
                    itemDef.GetEquipmentTypeIdNormalized()
                );
                if (!string.IsNullOrEmpty(typeLabel))
                    lines.Add($"  类型：{typeLabel}");
                List<string> modifierLines = _build_modifier_lines(itemDef.attribute_modifiers);
                if (modifierLines.Count > 0)
                    lines.Add($"  属性：{string.Join("，", modifierLines)}");
                if (!string.IsNullOrEmpty(itemDef.description))
                    lines.Add($"  说明：{itemDef.description}");
            }
        }
        lines.Insert(0, $"已装备：{filledCount}");
        return lines;
    }

    private List<string> _build_skill_detail_lines(
        UnitProgress progression,
        AttributeSnapshot snapshot = null
    )
    {
        var lines = new List<string>();
        if (progression == null)
        {
            lines.Add("暂无技能数据。");
            return lines;
        }

        int learnedCount = 0;
        foreach (StringName skillId in progression.GetSortedSkillIdsTyped())
        {
            UnitSkillProgress skillProgress = progression.GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            learnedCount += 1;
            SkillDef skillDef = GetTypedObject(_skill_defs, skillId);
            var tags = new List<string>();
            if (skillProgress.is_core)
                tags.Add("核心");
            if (skillProgress.is_level_trigger_active)
                tags.Add("升级触发");
            if (skillProgress.is_level_trigger_locked)
                tags.Add($"锁定：命中/检定/DC +{skillProgress.bonus_to_hit_from_lock}");
            if (skillProgress.profession_granted_by != (StringName)"")
                tags.Add(
                    $"职业授予：{_get_profession_display_name(skillProgress.profession_granted_by)}"
                );
            else if (skillProgress.assigned_profession_id != (StringName)"")
                tags.Add(
                    $"指派：{_get_profession_display_name(skillProgress.assigned_profession_id)}"
                );
            string typeLabel = _get_skill_type_label(
                skillDef != null ? skillDef.skill_type : new StringName("")
            );
            lines.Add(
                $"{_get_skill_display_name(skillId)}  Lv.{skillProgress.skill_level}{(tags.Count > 0 ? $"  |  {string.Join("，", tags)}" : "")}"
            );
            if (!string.IsNullOrEmpty(typeLabel))
                lines.Add($"  类型：{typeLabel}");
            lines.Add(
                $"  熟练度：{skillProgress.current_mastery}  总获得：{skillProgress.total_mastery_earned}"
            );
            if (skillDef != null && !string.IsNullOrEmpty(skillDef.description))
                lines.Add($"  说明：{skillDef.description}");

            int currentLevel = skillProgress.skill_level;
            if (skillDef != null)
            {
                var runtimeContext = new GDictionary();
                if (snapshot != null)
                {
                    runtimeContext["con_mod"] = _get_snapshot_value(
                        snapshot,
                        AttributeService.ToStringName(AttributeIdKind.ConstitutionModifier)
                    );
                    runtimeContext["will_mod"] = _get_snapshot_value(
                        snapshot,
                        AttributeService.ToStringName(AttributeIdKind.WillpowerModifier)
                    );
                }
                runtimeContext["dynamic_max_level"] = SkillEffectiveMaxLevelRules
                    .GetEffectiveMaxLevel(skillDef, skillProgress, progression)
                    .ToString();
                string levelDesc = SkillLevelDescriptionFormatter.BuildLevelDescription(
                    skillDef,
                    currentLevel,
                    runtimeContext
                );
                if (!string.IsNullOrEmpty(levelDesc))
                    lines.Add($"  当前效果：{levelDesc}");
                foreach (
                    string previewLine in _build_level_override_preview(skillDef, currentLevel)
                )
                    lines.Add(previewLine);
            }
            else
            {
                lines.Add("  说明：技能定义缺失。");
            }
            if (skillProgress.merged_from_skill_ids.Count > 0)
                lines.Add($"  来源：{_format_skill_id_list(skillProgress.merged_from_skill_ids)}");
        }
        if (learnedCount <= 0)
            lines.Add("暂无已学技能。");
        else
            lines.Insert(0, $"已学技能：{learnedCount}");
        return lines;
    }

    private static List<string> _build_level_override_preview(SkillDef skillDef, int skillLevel)
    {
        var lines = new List<string>();
        if (skillDef?.combat_profile == null)
            return lines;
        GDictionary overrides = skillDef.combat_profile.level_overrides;
        if (overrides.Count == 0)
            return lines;
        var nextLevels = new List<string>();
        foreach (var levelKey in overrides.Keys)
        {
            int level = levelKey.AsString().ToInt();
            if (level <= skillLevel)
                continue;
            var dataValue = overrides[levelKey];
            if (!dataValue.TryAsDictionary(out GDictionary data))
                continue;
            var parts = new List<string>();
            foreach (
                string costKey in new[]
                {
                    "ap_cost",
                    "mp_cost",
                    "stamina_cost",
                    "aura_cost",
                    "cooldown_tu",
                }
            )
            {
                if (!data.ContainsKey(costKey))
                    continue;
                string label = costKey switch
                {
                    "ap_cost" => "AP",
                    "mp_cost" => "MP",
                    "stamina_cost" => "体力",
                    "aura_cost" => "斗气",
                    "cooldown_tu" => "冷却",
                    _ => "",
                };
                parts.Add($"{label}→{data[costKey].AsInt32()}");
            }
            if (parts.Count > 0)
                nextLevels.Add($"Lv.{level}：{string.Join("，", parts)}");
        }
        if (nextLevels.Count > 0)
            lines.Add($"  升级预览：{string.Join("；", nextLevels)}");
        return lines;
    }

    private List<string> _build_profession_detail_lines(UnitProgress progression)
    {
        var lines = new List<string>();
        if (progression == null)
        {
            lines.Add("暂无职业数据。");
            return lines;
        }

        int professionCount = 0;
        foreach (StringName professionId in progression.GetSortedProfessionIdsTyped())
        {
            UnitProfessionProgress professionProgress = progression.GetProfessionProgress(
                professionId
            );
            if (professionProgress == null || professionProgress.is_hidden)
                continue;
            professionCount += 1;
            ProfessionDef professionDef = GetTypedObject(
                _profession_defs,
                professionId
            );
            lines.Add(
                $"{_get_profession_display_name(professionId)}  Rank {professionProgress.rank}{(professionProgress.is_active ? "  |  激活" : "  |  未激活")}"
            );
            if (professionProgress.inactive_reason != (StringName)"")
                lines.Add($"  原因：{professionProgress.inactive_reason}");
            if (professionDef != null && !string.IsNullOrEmpty(professionDef.description))
                lines.Add($"  说明：{professionDef.description}");
            List<string> modifierLines = _build_modifier_lines(
                professionDef != null ? professionDef.attribute_modifiers : null,
                professionProgress.rank
            );
            if (modifierLines.Count > 0)
                lines.Add($"  属性修正：{string.Join("，", modifierLines)}");
            if (professionProgress.core_skill_ids.Count > 0)
                lines.Add(
                    $"  核心技能：{_format_skill_id_list(professionProgress.core_skill_ids)}"
                );
            if (professionProgress.granted_skill_ids.Count > 0)
                lines.Add(
                    $"  授予技能：{_format_skill_id_list(professionProgress.granted_skill_ids)}"
                );
        }
        if (professionCount <= 0)
            lines.Add("暂无职业。");
        else
            lines.Insert(0, $"职业：{professionCount}");
        return lines;
    }

    private void _clear_detail_labels()
    {
        if (overview_label != null)
            overview_label.Text = "";
        _clear_detail_tabs();
    }

    private void _clear_detail_tabs()
    {
        if (attributes_label != null)
            attributes_label.Text = "";
        if (equipment_label != null)
            equipment_label.Text = "";
        if (skills_label != null)
            skills_label.Text = "";
        if (professions_label != null)
            professions_label.Text = "";
    }

    private static int _get_snapshot_value(AttributeSnapshot snapshot, StringName attributeId)
    {
        return snapshot?.GetValue(attributeId) ?? 0;
    }

    private static StringName GetEquippedItemId(
        EquipmentState equipmentState,
        StringName slotId
    )
    {
        return equipmentState?.GetEquippedItemId(slotId) ?? new StringName("");
    }

    private static StringName GetEntrySlotForSlot(
        EquipmentState equipmentState,
        StringName slotId
    )
    {
        if (equipmentState != null)
            return equipmentState.GetEntrySlotForSlot(slotId);
        return GetEquippedItemId(equipmentState, slotId) != (StringName)""
            ? slotId
            : new StringName("");
    }

    private string _get_item_display_name(StringName itemId)
    {
        ItemDef itemDef = GetTypedObject(_item_defs, itemId);
        return itemDef != null && !string.IsNullOrEmpty(itemDef.display_name)
            ? itemDef.display_name
            : itemId.ToString();
    }

    private string _get_skill_display_name(StringName skillId)
    {
        SkillDef skillDef = GetTypedObject(_skill_defs, skillId);
        return skillDef != null && !string.IsNullOrEmpty(skillDef.display_name)
            ? skillDef.display_name
            : skillId.ToString();
    }

    private string _get_profession_display_name(StringName professionId)
    {
        ProfessionDef professionDef = GetTypedObject(_profession_defs, professionId);
        return professionDef != null && !string.IsNullOrEmpty(professionDef.display_name)
            ? professionDef.display_name
            : professionId.ToString();
    }

    private string _format_skill_id_list(IEnumerable skillIds)
    {
        var labels = new List<string>();
        foreach (object value in skillIds)
        {
            StringName skillId = value is StringName id
                ? id
                : ProgressionDataUtils.to_string_name(value);
            labels.Add(_get_skill_display_name(skillId));
        }
        return string.Join("，", labels);
    }

    private List<string> _build_modifier_lines(IEnumerable modifiers, int rank = 1)
    {
        var lines = new List<string>();
        if (modifiers == null)
            return lines;
        foreach (object modifierValue in modifiers)
        {
            if (modifierValue is not AttributeModifier modifier)
                continue;
            StringName attributeId = modifier.attribute_id;
            int value = modifier.GetValueForRank(rank);
            if (attributeId == (StringName)"" || value == 0)
                continue;
            lines.Add($"{_get_attribute_label(attributeId)} {value:+0;-0;0}");
        }
        return lines;
    }

    private static string _get_equipment_type_label(StringName equipmentTypeId)
    {
        if (equipmentTypeId == (StringName)"weapon")
            return "武器";
        if (equipmentTypeId == (StringName)"armor")
            return "防具";
        if (equipmentTypeId == (StringName)"accessory")
            return "饰品";
        return "";
    }

    private static string _get_skill_type_label(StringName skillType)
    {
        if (skillType == (StringName)"active")
            return "主动";
        if (skillType == (StringName)"passive")
            return "被动";
        if (skillType == (StringName)"combat")
            return "战斗";
        return skillType != (StringName)"" ? skillType.ToString() : "";
    }

    private static string _get_attribute_label(StringName attributeId)
    {
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength))
            return "力量";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            return "敏捷";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution))
            return "体质";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception))
            return "感知";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence))
            return "智力";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower))
            return "意志";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth))
            return "出生隐藏幸运";
        if (attributeId == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.FaithLuckBonus))
            return "信仰幸运加值";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.HpMax))
            return "生命上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus))
            return "人物生命加成%";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.MpMax))
            return "法力上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.StaminaMax))
            return "体力上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.AuraMax))
            return "灵气上限";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ActionPoints))
            return "行动点";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ActionThreshold))
            return "行动阈值 TU";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ArmorClass))
            return "AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ArmorAcBonus))
            return "护甲 AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ShieldAcBonus))
            return "盾牌 AC";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.DodgeBonus))
            return "闪避加值";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.DeflectionBonus))
            return "偏斜加值";
        if (attributeId == AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus))
            return "护甲敏捷上限";
        return attributeId.ToString();
    }

    private List<string> _build_achievement_summary_lines(UnitProgress progression)
    {
        var lines = new List<string>();
        if (progression == null)
        {
            lines.Add("暂无成就数据。");
            return lines;
        }

        int unlockedCount = 0;
        var inProgressEntries = new List<AchievementProgressPreview>();
        string recentUnlockedName = "";
        int recentUnlockedTime = 0;

        foreach (StringName achievementId in GetSortedKeys(_achievement_defs))
        {
            AchievementDef achievementDef = GetTypedObject(_achievement_defs, achievementId);
            if (achievementDef == null)
                continue;

            AchievementProgressState progressState = progression.GetAchievementProgressState(
                achievementId
            );
            if (progressState != null && progressState.is_unlocked)
            {
                unlockedCount += 1;
                int unlockedAt = progressState.unlocked_at_unix_time;
                if (unlockedAt >= recentUnlockedTime)
                {
                    recentUnlockedTime = unlockedAt;
                    recentUnlockedName = achievementDef.display_name;
                }
                continue;
            }

            int currentValue = progressState?.current_value ?? 0;
            if (currentValue <= 0)
                continue;
            int threshold = Mathf.Max(achievementDef.threshold, 1);
            inProgressEntries.Add(
                new AchievementProgressPreview(
                    achievementDef.display_name,
                    currentValue,
                    achievementDef.threshold,
                    currentValue / (float)threshold
                )
            );
        }

        inProgressEntries.Sort(
            (left, right) =>
            {
                int ratioCompare = right.ProgressRatio.CompareTo(left.ProgressRatio);
                return ratioCompare != 0
                    ? ratioCompare
                    : string.CompareOrdinal(left.DisplayName, right.DisplayName);
            }
        );

        lines.Add($"已解锁：{unlockedCount}");
        lines.Add($"进行中：{inProgressEntries.Count}");
        lines.Add(
            $"最近解锁：{(!string.IsNullOrEmpty(recentUnlockedName) ? recentUnlockedName : "暂无")}"
        );
        if (inProgressEntries.Count == 0)
        {
            lines.Add("当前进行中条目：暂无");
            return lines;
        }

        lines.Add("当前进行中条目：");
        int previewCount = Mathf.Min(inProgressEntries.Count, 3);
        for (int index = 0; index < previewCount; index++)
        {
            AchievementProgressPreview entry = inProgressEntries[index];
            lines.Add($"- {entry.DisplayName} {entry.CurrentValue} / {entry.Threshold}");
        }
        return lines;
    }

    private List<string> _build_equipment_summary_lines(PartyMemberState memberState)
    {
        var lines = new List<string>();
        if (memberState == null)
        {
            lines.Add("暂无");
            return lines;
        }

        EquipmentState equipmentState = memberState.equipment_state;
        int filledCount = 0;
        foreach (StringName slotId in EquipmentRules.GetAllSlotIdsTyped())
        {
            StringName entrySlotId = GetEntrySlotForSlot(equipmentState, slotId);
            if (entrySlotId != (StringName)"" && entrySlotId != slotId)
            {
                lines.Add(
                    $"- {EquipmentRules.GetSlotLabel(slotId)}：由{EquipmentRules.GetSlotLabel(entrySlotId)}占用"
                );
                continue;
            }
            StringName itemId = GetEquippedItemId(equipmentState, slotId);
            if (itemId == (StringName)"")
                continue;
            filledCount += 1;
            lines.Add(
                $"- {EquipmentRules.GetSlotLabel(slotId)}：{_get_item_display_name(itemId)}"
            );
        }

        if (filledCount <= 0)
            lines.Add("暂无");
        return lines;
    }

    private void _select_active_member_id(StringName memberId)
    {
        _selected_member_id = memberId;
        reserve_list.DeselectAll();
        for (int index = 0; index < _activeListMemberIds.Count; index++)
        {
            if (_activeListMemberIds[index] == memberId)
            {
                active_list.Select(index);
                break;
            }
        }
    }

    private void _select_reserve_member_id(StringName memberId)
    {
        _selected_member_id = memberId;
        active_list.DeselectAll();
        for (int index = 0; index < _reserveListMemberIds.Count; index++)
        {
            if (_reserveListMemberIds[index] == memberId)
            {
                reserve_list.Select(index);
                break;
            }
        }
    }

    public void _on_active_item_selected(int index)
    {
        if (index < 0 || index >= _activeListMemberIds.Count)
            return;
        StringName memberId = _activeListMemberIds[index];
        if (memberId == (StringName)"")
            return;
        _select_active_member_id(memberId);
        _refresh_controls();
        _refresh_details();
    }

    public void _on_reserve_item_selected(int index)
    {
        if (index < 0 || index >= _reserveListMemberIds.Count)
            return;
        StringName memberId = _reserveListMemberIds[index];
        if (memberId == (StringName)"")
            return;
        _select_reserve_member_id(memberId);
        _refresh_controls();
        _refresh_details();
    }

    public void _on_set_leader_button_pressed()
    {
        if (
            _selected_member_id == (StringName)""
            || !_activeMemberIds.Contains(_selected_member_id)
        )
            return;
        _leader_member_id = _selected_member_id;
        _rebuild_lists();
        _restore_selection();
        _refresh_details();
        EmitSignal(SignalName.leader_change_requested, _leader_member_id);
    }

    public void _on_move_to_active_button_pressed()
    {
        if (
            _selected_member_id == (StringName)""
            || !_reserveMemberIds.Contains(_selected_member_id)
        )
            return;
        if (_activeMemberIds.Count >= MaxActiveMemberCount)
            return;

        _reserveMemberIds.Remove(_selected_member_id);
        _activeMemberIds.Add(_selected_member_id);
        bool leaderChanged = false;
        if (_leader_member_id == (StringName)"")
        {
            _leader_member_id = _selected_member_id;
            leaderChanged = true;
        }
        _rebuild_lists();
        _select_active_member_id(_selected_member_id);
        _refresh_controls();
        _refresh_details();
        _emit_roster_change();
        if (leaderChanged)
            EmitSignal(SignalName.leader_change_requested, _leader_member_id);
    }

    public void _on_move_to_reserve_button_pressed()
    {
        if (
            _selected_member_id == (StringName)""
            || !_activeMemberIds.Contains(_selected_member_id)
        )
            return;
        if (_selected_member_id == _main_character_member_id)
        {
            status_label.Text = "主角必须保持上阵，不能移至替补。";
            return;
        }
        if (_activeMemberIds.Count <= 1)
            return;

        _activeMemberIds.Remove(_selected_member_id);
        _reserveMemberIds.Add(_selected_member_id);
        bool leaderChanged = false;
        if (_leader_member_id == _selected_member_id)
        {
            _leader_member_id = _activeMemberIds[0];
            leaderChanged = true;
        }
        _rebuild_lists();
        _select_reserve_member_id(_selected_member_id);
        _refresh_controls();
        _refresh_details();
        _emit_roster_change();
        if (leaderChanged)
            EmitSignal(SignalName.leader_change_requested, _leader_member_id);
    }

    public GStringNameArray GetActiveMemberIds() => ToStringNameArray(_activeMemberIds);

    public GStringNameArray GetReserveMemberIds() => ToStringNameArray(_reserveMemberIds);

    private void _emit_roster_change()
    {
        EmitSignal(
            SignalName.roster_change_requested,
            ToStringNameArray(_activeMemberIds),
            ToStringNameArray(_reserveMemberIds)
        );
    }

    public void _close_window()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.closed);
    }

    public void _on_warehouse_button_pressed()
    {
        if (_party_state == null)
            return;
        EmitSignal(SignalName.warehouse_requested);
    }

    public void _on_contingency_setup_button_pressed()
    {
        if (_selected_member_id == (StringName)"")
            return;
        EmitSignal(SignalName.contingency_setup_requested, _selected_member_id);
    }

    public void _on_shade_gui_input(InputEvent @event)
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

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private static T GetTypedObject<T>(IReadOnlyDictionary<StringName, T> data, StringName key)
        where T : class
    {
        if (data == null)
            return null;
        return data.TryGetValue(key, out T value) ? value : null;
    }

    private static List<StringName> GetSortedKeys<T>(IReadOnlyDictionary<StringName, T> data)
    {
        var result = new List<StringName>();
        if (data == null)
            return result;
        foreach (StringName key in data.Keys)
            result.Add(key);
        result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return result;
    }

    private static int DictInt(GDictionary data, string key, int defaultValue)
    {
        if (!TryRead(data, key, out Variant value) || value.VariantType != Variant.Type.Int)
            return defaultValue;
        return value.AsInt32();
    }

    private static string DictString(GDictionary data, string key, string defaultValue)
    {
        if (!TryRead(data, key, out Variant value))
            return defaultValue;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => defaultValue,
        };
    }

    private static bool TryRead(GDictionary data, string key, out Variant value)
    {
        value = default;
        if (data == null || !data.ContainsKey(key))
            return false;
        value = data[key];
        return value.VariantType != Variant.Type.Nil;
    }

    private readonly record struct AchievementProgressPreview(
        string DisplayName,
        int CurrentValue,
        int Threshold,
        float ProgressRatio
    );
}
