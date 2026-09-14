using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class CharacterInfoWindow : ModalWindowShell
{
    [Signal]
    public delegate void closedEventHandler();

    private const string FateSectionTitle = "命运";

    public ColorRect shade;
    public Label title_label;
    public Label meta_label;
    public ScrollContainer sections_scroll;
    public VBoxContainer sections_container;
    public VBoxContainer status_block;
    public Label status_label;
    public Button close_button;
    private HBoxContainer _section_navigation;
    private Button _attributes_button;
    private Button _traits_button;
    private readonly Dictionary<string, Control> _section_panels = new();

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        title_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/TitleLabel"
        );
        meta_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Header/HeaderText/MetaLabel"
        );
        sections_scroll = GetNode<ScrollContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/SectionsScroll"
        );
        sections_container = GetNode<VBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/SectionsScroll/SectionsContainer"
        );
        status_block = GetNode<VBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Content/Body/StatusBlock"
        );
        status_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Content/Body/StatusBlock/StatusLabel"
        );
        close_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Content/Header/CloseButton"
        );

        _section_navigation = GetNode<HBoxContainer>("%SectionNavigation");
        _attributes_button = GetNode<Button>("%AttributesButton");
        _traits_button = GetNode<Button>("%TraitsButton");
        _attributes_button.Pressed += _show_attributes;
        _traits_button.Pressed += _show_traits;
        HideWindow();
        close_button.Pressed += _close_window;
        Resized += _resize_details_panel;
        _resize_details_panel();
        base._Ready();
    }

    public override void _ExitTree()
    {
        Resized -= _resize_details_panel;
        if (close_button != null)
            close_button.Pressed -= _close_window;
        if (_attributes_button != null)
            _attributes_button.Pressed -= _show_attributes;
        if (_traits_button != null)
            _traits_button.Pressed -= _show_traits;
        _section_panels.Clear();
        base._ExitTree();
    }

    private void _resize_details_panel()
    {
        var panel = GetNode<PanelContainer>("CenterContainer/Panel");
        panel.CustomMinimumSize = new Vector2(
            Mathf.Min(900, Mathf.Max(Size.X - 40, 320)),
            Mathf.Min(760, Mathf.Max(Size.Y - 40, 320))
        );
    }

    protected override void _on_modal_close_requested() => _close_window();

    internal void ShowCharacter(GameRuntimeCharacterInfoContext context)
    {
        string displayName = context == null ? "" : context.DisplayName.StripEdges();
        if (string.IsNullOrEmpty(displayName) || context.Sections.Count == 0)
        {
            HideWindow();
            return;
        }

        Visible = true;
        title_label.Text = displayName;
        meta_label.Text = context.MetaLabel.StripEdges();
        meta_label.Visible = !string.IsNullOrEmpty(meta_label.Text);
        _rebuild_sections(_collect_rendered_sections(context));
        _attributes_button.Visible = _section_panels.ContainsKey("基础属性");
        _traits_button.Visible = _section_panels.ContainsKey("生效特性");
        _section_navigation.Visible = _attributes_button.Visible || _traits_button.Visible;
        status_label.Text = context.StatusLabel;
        status_block.Visible = !string.IsNullOrEmpty(context.StatusLabel);
    }

    public void HideWindow()
    {
        Visible = false;
        title_label.Text = "人物信息";
        meta_label.Text = "";
        meta_label.Visible = false;
        _clear_sections();
        status_label.Text = "";
        status_block.Visible = false;
        _section_navigation.Visible = false;
    }

    private void _show_attributes() => _scroll_to_section("基础属性");

    private void _show_traits() => _scroll_to_section("生效特性");

    private void _scroll_to_section(string title)
    {
        if (_section_panels.TryGetValue(title, out Control section))
            sections_scroll.ScrollVertical = Mathf.RoundToInt(section.Position.Y);
    }

    private void _close_window()
    {
        if (!Visible)
            return;
        HideWindow();
        EmitSignal(SignalName.closed);
    }

    // The fate block is presentation-only: the runtime owns the raw fate values, and the
    // window turns them into the trailing 命运 section without touching the plain snapshot.
    private static List<GameRuntimeCharacterInfoSection> _collect_rendered_sections(
        GameRuntimeCharacterInfoContext context
    )
    {
        var sections = new List<GameRuntimeCharacterInfoSection>(context.Sections);
        if (context.Fate == null || _has_section_title(sections, FateSectionTitle))
            return sections;
        sections.Add(_build_fate_section(context.Fate));
        return sections;
    }

    private static bool _has_section_title(
        IReadOnlyList<GameRuntimeCharacterInfoSection> sections,
        string titleText
    )
    {
        foreach (GameRuntimeCharacterInfoSection section in sections)
        {
            if (section.Title.StripEdges() == titleText)
                return true;
        }
        return false;
    }

    private static GameRuntimeCharacterInfoSection _build_fate_section(
        GameRuntimeCharacterInfoFate fate
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>
        {
            GameRuntimeCharacterInfoEntry.Pair(
                "生来暗运",
                _format_signed_number(fate.HiddenLuckAtBirth)
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "信仰赐运",
                _format_signed_number(fate.FaithLuckBonus)
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "有效运势",
                _format_signed_number(fate.EffectiveLuck)
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "Fortuna 标记",
                _format_fate_mark_value(fate.FortuneMarked, "已获福印", "未获福印")
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "Misfortune 黑兆",
                _format_fate_mark_value(fate.DoomMarked, "已见黑兆", "未见黑兆")
            ),
        };
        if (fate.HasMisfortune)
            entries.Add(GameRuntimeCharacterInfoEntry.Pair("厄权", $"{fate.DoomAuthority} 级"));
        foreach (
            string hintText in _build_fate_hint_texts(fate.HiddenLuckAtBirth, fate.EffectiveLuck)
        )
            entries.Add(GameRuntimeCharacterInfoEntry.TextEntry(hintText));

        return new GameRuntimeCharacterInfoSection(FateSectionTitle, entries);
    }

    private static string _format_signed_number(int value) =>
        value > 0 ? $"+{value}" : value.ToString();

    private static string _format_fate_mark_value(
        int value,
        string markedText,
        string unmarkedText
    ) => $"{value}（{(value > 0 ? markedText : unmarkedText)}）";

    private static List<string> _build_fate_hint_texts(int hiddenLuckAtBirth, int effectiveLuck)
    {
        var hints = new List<string>();
        if (hiddenLuckAtBirth >= UnitBaseAttributes.EffectiveLuckMax)
            hints.Add("生来暗运已处于极端正运档，界面会按原值保留该刻印。");
        else if (hiddenLuckAtBirth <= UnitBaseAttributes.EffectiveLuckMin)
            hints.Add("生来暗运已压到最深坏运档，这类角色更容易撞进命运事件的极端分支。");

        if (effectiveLuck >= UnitBaseAttributes.EffectiveLuckMax)
            hints.Add("有效运势已到 +7 上限：高位大成功威胁区会吃满，但随机掉落仍只按 +5 结算。");
        else if (effectiveLuck <= UnitBaseAttributes.EffectiveLuckMin)
            hints.Add(
                "有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。"
            );
        return hints;
    }

    private void _rebuild_sections(List<GameRuntimeCharacterInfoSection> sections)
    {
        _clear_sections();
        foreach (GameRuntimeCharacterInfoSection section in sections)
        {
            PanelContainer panel = _build_section_panel(section);
            sections_container.AddChild(panel);
            _section_panels[section.Title] = panel;
        }
        if (sections_scroll != null)
            sections_scroll.SetDeferred("scroll_vertical", 0);
    }

    private void _clear_sections()
    {
        _section_panels.Clear();
        if (sections_container == null)
            return;
        foreach (Node child in sections_container.GetChildren())
            child.Free();
    }

    private PanelContainer _build_section_panel(GameRuntimeCharacterInfoSection section)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", _create_section_panel_stylebox());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        if (!string.IsNullOrEmpty(section.Title))
        {
            var sectionTitle = new Label { Text = section.Title };
            sectionTitle.AddThemeColorOverride(
                "font_color",
                new Color(0.972549f, 0.815686f, 0.427451f, 1.0f)
            );
            sectionTitle.AddThemeFontSizeOverride("font_size", 18);
            content.AddChild(sectionTitle);
        }

        Container entriesContainer = content;
        if (section.Layout == GameRuntimeCharacterInfoSectionLayout.AttributeGrid)
        {
            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 28);
            grid.AddThemeConstantOverride("v_separation", 8);
            content.AddChild(grid);
            entriesContainer = grid;
        }
        foreach (GameRuntimeCharacterInfoEntry entry in section.Entries)
        {
            entriesContainer.AddChild(
                entry.Kind == GameRuntimeCharacterInfoEntryKind.Pair
                    ? _build_pair_entry(entry, section.Layout == GameRuntimeCharacterInfoSectionLayout.AttributeGrid)
                    : _build_text_entry(entry.Text)
            );
        }

        return panel;
    }

    private Control _build_pair_entry(GameRuntimeCharacterInfoEntry entry, bool compact)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 12);

        var labelNode = new Label
        {
            CustomMinimumSize = new Vector2(compact ? 135 : 180, 0.0f),
            VerticalAlignment = VerticalAlignment.Top,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = $"{entry.Label}：",
        };
        labelNode.AddThemeColorOverride(
            "font_color",
            new Color(0.69f, 0.68f, 0.62f, 1.0f)
        );
        row.AddChild(labelNode);

        var valueNode = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = entry.Value,
        };
        valueNode.AddThemeColorOverride("font_color", new Color(0.91f, 0.88f, 0.81f, 1.0f));
        row.AddChild(valueNode);

        // Reveal the detail (e.g. equipment trait mechanics) only on hover: the whole row
        // carries the tooltip, and its child labels pass the hover through to the row.
        if (!string.IsNullOrEmpty(entry.Tooltip))
        {
            row.MouseFilter = Control.MouseFilterEnum.Stop;
            row.TooltipText = entry.Tooltip;
            labelNode.MouseFilter = Control.MouseFilterEnum.Ignore;
            valueNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        return row;
    }

    private Label _build_text_entry(string text)
    {
        var textLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = text };
        textLabel.AddThemeColorOverride(
            "font_color",
            new Color(0.91f, 0.88f, 0.81f, 1.0f)
        );
        return textLabel;
    }

    private static StyleBoxFlat _create_section_panel_stylebox()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.031f, 0.038f, 0.28f),
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.56f, 0.46f, 0.31f, 0.3f),
        };
    }
}
