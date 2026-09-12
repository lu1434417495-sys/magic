using Godot;

public partial class CharacterCreationWindow
{
    private Label _creationChapterTitle;
    private Label _creationChapterSubtitle;
    private Control _revealedCreationPhase;
    private Tween _creationRevealTween;

    private Color _creation_color(string name) => Theme.GetColor(name, "Creation");

    private void _configure_creation_appearance()
    {
        var margin = _creationPanel.GetNode<MarginContainer>("Margin");
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        content_root.AddThemeConstantOverride("separation", 14);

        foreach (Node node in content_root.FindChildren("*", "Label", true, false))
        {
            if (node is Label label)
                label.AddThemeColorOverride("font_color", _creation_color("text"));
        }
        var titleRow = content_root.GetNode<HBoxContainer>("TitleRow");
        foreach (Control child in titleRow.GetChildren())
            child.Visible = child.Name == "TitleLabel";
        _creationChapterTitle = titleRow.GetNode<Label>("TitleLabel");
        _creationChapterTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _creationChapterTitle.ThemeTypeVariation = "CreationHeading";
        _creationChapterTitle.RemoveThemeFontSizeOverride("font_size");
        _creationChapterSubtitle = content_root.GetNode<Label>("ChapterSubtitle");
        _creationChapterSubtitle.AddThemeColorOverride("font_color", _creation_color("muted"));
        content_root.GetNode<Label>("ChapterLabel").AddThemeColorOverride("font_color", _creation_color("accent"));

        var status = content_root.GetNode<HBoxContainer>("StatusRow");
        status.Alignment = BoxContainer.AlignmentMode.Begin;
        reroll_count_label.AddThemeFontSizeOverride("font_size", 13);
        reroll_count_label.AddThemeColorOverride("font_color", _creation_color("muted"));
        luck_tier_label.AddThemeFontSizeOverride("font_size", 13);
        name_input.CustomMinimumSize = new Vector2(0, 62);
        name_input.ThemeTypeVariation = "CreationNameInput";
        var nameBody = name_phase.GetNode<VBoxContainer>("BodyScroll/Body");
        var nameIntro = nameBody.GetNode<Label>("NameIntroLabel");
        nameIntro.Text = "最多 24 个字符，留空时使用“主角”。";
        nameIntro.AddThemeColorOverride("font_color", _creation_color("muted"));
        nameBody.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var attributeBody = attribute_phase.GetNode<VBoxContainer>("BodyScroll/Body");
        var hint = attributeBody.GetNode<Label>("AttributeHint");
        hint.Text = "属性范围 4–14。设置预期下限后，可连续重掷至全部达标；点击“停止”随时结束。";
        hint.AddThemeColorOverride("font_color", _creation_color("muted"));
        var attributeHeader = new HBoxContainer { Name = "AttributeHeader" };
        var attributeHeading = _creation_label("基础属性", 14, "muted");
        attributeHeading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        attributeHeader.AddChild(attributeHeading);
        var rollHeading = _creation_label("掷骰", 13, "muted");
        rollHeading.CustomMinimumSize = new Vector2(72, 0);
        attributeHeader.AddChild(rollHeading);
        var thresholdHeading = _creation_label("预期下限", 13, "muted");
        thresholdHeading.CustomMinimumSize = new Vector2(132, 0);
        attributeHeader.AddChild(thresholdHeading);
        attributeBody.AddChild(attributeHeader);
        attributeBody.MoveChild(attributeHeader, attributeBody.GetNode("AttributeList").GetIndex());
        foreach (PanelContainer row in _attributeRowPanels.Values)
        {
            row.CustomMinimumSize = new Vector2(0, 50);
            var badge = row.GetNode<ColorRect>("HBox/Badge");
            badge.CustomMinimumSize = new Vector2(3, 14);
            badge.Color = _creation_color("accent");
        }
        foreach (SpinBox threshold in _attributeThresholdSpinboxes.Values)
            threshold.GetLineEdit().ThemeTypeVariation = "CreationThresholdInput";

        foreach (Control phase in _creation_phases())
        {
            phase.AddThemeConstantOverride("separation", 18);
            var scroll = phase.GetNode<ScrollContainer>("BodyScroll");
            scroll.FollowFocus = true;
            var body = phase.GetNode<VBoxContainer>("BodyScroll/Body");
            body.AddThemeConstantOverride("separation", 16);
            if (phase != name_phase && phase != attribute_phase)
                body.GetChild<Control>(0).Visible = false;
            phase.VisibilityChanged += _refresh_creation_chapter;
        }

        race_card_flow.Resized += () => _size_creation_choices(race_card_flow);
        age_stage_card_flow.Resized += () => _size_creation_choices(age_stage_card_flow);
        _configure_creation_summary();
        VisibilityChanged += _refresh_creation_chapter;
        _refresh_creation_chapter();
    }

    private Control[] _creation_phases() =>
        new[] { name_phase, attribute_phase, race_phase, age_phase, identity_options_phase };

    private Label _creation_label(string text, int size, string color = "text")
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", _creation_color(color));
        return label;
    }

    private void _refresh_creation_chapter()
    {
        if (_creationChapterTitle == null)
            return;
        Control[] phases = _creation_phases();
        int current = System.Array.FindIndex(phases, phase => phase.Visible);
        if (current < 0)
            return;
        (_creationChapterTitle.Text, _creationChapterSubtitle.Text) = current switch
        {
            0 => ("你的名字", "在书卷上写下旅者的名字。"),
            1 => ("天赋初显", "掷出初始天赋，选择接受这份命运。"),
            2 => ("血脉与出身", "选择你的种族与亚种。"),
            3 => ("岁月之痕", "选择旅程开始时的年龄。"),
            _ => ("启程之前", "查看你的身份与天赋，准备踏入世界。"),
        };
        content_root.GetNode<Control>("StatusRow").Visible = current == 1 || current == 4;
        scan_container.Visible = current == 1;
        if (!IsVisibleInTree())
        {
            _creationRevealTween?.Kill();
            _revealedCreationPhase = null;
            return;
        }
        if (_revealedCreationPhase == phases[current])
            return;
        _creationRevealTween?.Kill();
        if (_revealedCreationPhase != null)
            _revealedCreationPhase.Modulate = Colors.White;
        _revealedCreationPhase = phases[current];
        _revealedCreationPhase.Modulate = new Color(1, 1, 1, 0.5f);
        _creationRevealTween = _revealedCreationPhase.CreateTween();
        _creationRevealTween.TweenProperty(_revealedCreationPhase, "modulate:a", 1.0f, 0.28)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private PanelContainer _build_creation_choice(string title, string summary)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(300, 82),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        card.AddThemeStyleboxOverride("panel", _identityCardStyleNormal);
        var button = new Button
        {
            Name = "Select", ThemeTypeVariation = "CreationChoiceButton",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = $"{title}\n{summary}",
            AccessibilityName = title,
        };
        card.AddChild(button);
        var margin = new MarginContainer { Name = "Margin", MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);
        var details = new VBoxContainer { Name = "Details", MouseFilter = MouseFilterEnum.Ignore };
        details.AddThemeConstantOverride("separation", 6);
        margin.AddChild(details);
        var heading = new HBoxContainer { Name = "Heading", MouseFilter = MouseFilterEnum.Ignore };
        var titleLabel = _creation_label(title, 20);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(titleLabel);
        var mark = _creation_label("○", 16, "muted");
        mark.Name = "SelectionMark";
        heading.AddChild(mark);
        details.AddChild(heading);
        var description = _creation_label(summary, 13, "muted");
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        details.AddChild(description);
        return card;
    }

    private static void _size_creation_choices(HFlowContainer flow)
    {
        if (flow.Size.X < 100)
            return;
        int columns = flow.Size.X >= 810 ? 3 : flow.Size.X >= 510 ? 2 : 1;
        columns = Mathf.Min(columns, Mathf.Max(1, flow.GetChildCount()));
        float width = Mathf.Floor((flow.Size.X - (columns - 1) * 12) / columns);
        foreach (Control child in flow.GetChildren())
            child.CustomMinimumSize = new Vector2(width, 82);
    }

    private void _configure_creation_summary()
    {
        var body = identity_options_phase.GetNode<VBoxContainer>("BodyScroll/Body");
        var columns = new HBoxContainer { Name = "SummaryColumns" };
        columns.AddThemeConstantOverride("separation", 36);
        body.AddChild(columns);
        foreach (Label preview in new[] { identity_options_label, final_attribute_preview_label })
        {
            var section = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            section.AddThemeConstantOverride("separation", 16);
            columns.AddChild(section);
            section.AddChild(new HSeparator());
            preview.Reparent(section, false);
            preview.AddThemeFontSizeOverride("font_size", 16);
            preview.AddThemeConstantOverride("line_spacing", 8);
        }
    }
}
