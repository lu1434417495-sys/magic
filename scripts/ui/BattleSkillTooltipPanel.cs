using System.Collections.Generic;
using Godot;

public partial class BattleSkillTooltipPanel : Control
{
    private const float FrameAspectRatio = 0.9f;
    private const float ViewportAreaRatio = 0.25f;
    private static readonly Color Ink = new("ece6d6");
    private static readonly Color Muted = new("a6afa9");
    private static readonly Color Gold = new("c9ad75");

    internal static Vector2 ResolveFrameSize(Vector2 viewportSize)
    {
        float width = Mathf.Sqrt(viewportSize.X * viewportSize.Y * ViewportAreaRatio * FrameAspectRatio);
        float height = width / FrameAspectRatio;
        float fit = Mathf.Min(1, Mathf.Min((viewportSize.X - 32) / width, (viewportSize.Y - 32) / height));
        return new Vector2(Mathf.Round(width * fit), Mathf.Round(height * fit));
    }

    internal static BattleSkillTooltipPanel Create(BattleHudSkillSlotSnapshot slot, Texture2D icon, Vector2 size)
    {
        var root = new BattleSkillTooltipPanel
        {
            Name = "BattleSkillTooltip", Size = size, CustomMinimumSize = size,
            MouseFilter = MouseFilterEnum.Stop,
            Theme = GD.Load<Theme>("res://scenes/ui/styles/chronicle_theme.tres"),
        };
        var frame = new BattleSkillTooltipFrame { Name = "MetalFrame", MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var padding = new MarginContainer { MouseFilter = MouseFilterEnum.Pass };
        root.AddChild(padding);
        padding.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        padding.AddThemeConstantOverride("margin_left", 28);
        padding.AddThemeConstantOverride("margin_right", 28);
        padding.AddThemeConstantOverride("margin_top", 32);
        padding.AddThemeConstantOverride("margin_bottom", 28);
        var body = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        body.AddThemeConstantOverride("separation", 14);
        padding.AddChild(body);
        body.AddChild(BuildHeader(slot, icon));
        body.AddChild(Rule());

        BattleHudSkillTooltipSnapshot data = slot.Tooltip;
        var metrics = new HBoxContainer();
        metrics.AddThemeConstantOverride("separation", 18);
        metrics.AddChild(Metric(data?.UsesTargetSlotCosts == true ? "单目标消耗" : "消耗",
            data != null ? FormatCosts(data.Costs) : "—", 2));
        metrics.AddChild(Metric("射程", data != null ? $"{data.Range} 格" : "—"));
        metrics.AddChild(Metric("冷却", data?.Costs.CooldownTu > 0 ? $"{data.Costs.CooldownTu} TU" : "无"));
        body.AddChild(metrics);
        body.AddChild(Rule());
        var effectHeading = new HBoxContainer();
        var effectTitle = MakeLabel("当前等级效果", 15, Gold);
        effectTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        effectHeading.AddChild(effectTitle);
        var scrollHint = MakeLabel("滚轮查看", 12, Muted);
        effectHeading.AddChild(scrollHint);
        body.AddChild(effectHeading);
        var scroll = new ScrollContainer
        {
            Name = "LevelEffectScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop,
        };
        var effect = MakeLabel(string.IsNullOrWhiteSpace(data?.LevelEffect) ? "暂无等级说明。" : data.LevelEffect, 18, Ink, true);
        effect.Name = "LevelEffectText";
        effect.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        effect.AddThemeConstantOverride("line_spacing", 5);
        scroll.AddChild(effect);
        body.AddChild(scroll);
        VScrollBar scrollbar = scroll.GetVScrollBar();
        scrollbar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color("1d272c"), ContentMarginLeft = 3, ContentMarginRight = 3 });
        scrollbar.AddThemeStyleboxOverride("grabber", new StyleBoxFlat { BgColor = new Color("8b7954"), ContentMarginLeft = 3, ContentMarginRight = 3 });
        scrollbar.AddThemeStyleboxOverride("grabber_highlight", new StyleBoxFlat { BgColor = Gold });
        scrollbar.Changed += () => scrollHint.Visible = scrollbar.MaxValue > scrollbar.Page + 1;
        body.AddChild(Rule());
        body.AddChild(BuildMastery(data?.Mastery, slot.SkillLevel));
        var footer = new VBoxContainer { CustomMinimumSize = new Vector2(0, 42) };
        footer.AddThemeConstantOverride("separation", 3);
        string availability = slot.IsDisabled ? "暂不可用" : "可施放";
        if (slot.Cooldown > 0) availability += $"  ·  剩余冷却 {slot.Cooldown} TU";
        if (data != null) availability += data.CastingTimeTu > 0 ? $"  ·  吟唱 {data.CastingTimeTu} TU" : "  ·  即时施放";
        footer.AddChild(MakeLabel(availability, 14, slot.IsDisabled ? BattleUiTheme.FATE_WARNING() : new Color("91bda7"), true));
        if (!string.IsNullOrEmpty(slot.DisabledReason))
            footer.AddChild(MakeLabel(slot.DisabledReason, 13, BattleUiTheme.FATE_WARNING(), true));
        body.AddChild(footer);
        IgnoreLayoutMouse(body);
        scroll.MouseFilter = MouseFilterEnum.Stop;
        scrollbar.MouseFilter = MouseFilterEnum.Stop;
        return root;
    }

    private static HBoxContainer BuildHeader(BattleHudSkillSlotSnapshot slot, Texture2D icon)
    {
        var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 72) };
        header.AddThemeConstantOverride("separation", 18);
        var iconFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(68, 68), SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("17252d"), BorderColor = Gold,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 5,
        });
        header.AddChild(iconFrame);
        if (icon != null)
            iconFrame.AddChild(new TextureRect { Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        else
        {
            var glyph = MakeLabel(slot.ShortName, 24, Gold);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            iconFrame.AddChild(glyph);
        }
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        heading.AddThemeConstantOverride("separation", 7);
        header.AddChild(heading);
        heading.AddChild(MakeLabel(slot.DisplayName, 28, Ink, true));
        heading.AddChild(MakeLabel($"主动技能  /  等级 {slot.SkillLevel}" + (slot.IsBattleOnly ? "  /  战斗授予" : ""), 13, Muted));
        if (!string.IsNullOrEmpty(slot.Hotkey))
        {
            var hotkey = MakeLabel($"[{slot.Hotkey}]", 14, Gold);
            hotkey.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            header.AddChild(hotkey);
        }
        return header;
    }

    private static VBoxContainer BuildMastery(BattleHudSkillMasterySnapshot mastery, int displayedLevel)
    {
        var section = new VBoxContainer { CustomMinimumSize = new Vector2(0, 42) };
        section.AddThemeConstantOverride("separation", 9);
        var row = new HBoxContainer();
        var title = MakeLabel(mastery != null && mastery.LearnedLevel != displayedLevel
            ? $"熟练度 · 已学等级 {mastery.LearnedLevel}" : "熟练度", 14, Muted);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(title);
        string value = mastery == null ? "暂无成长进度"
            : mastery.IsAtMaxLevel ? $"已达当前上限 · 等级 {mastery.MaxLevel}"
            : mastery.Required > 0 ? $"{mastery.Current} / {mastery.Required}"
            : $"{mastery.Current} · 暂无升级进度";
        row.AddChild(MakeLabel(value, 14, Gold));
        section.AddChild(row);
        if (mastery != null && !mastery.IsAtMaxLevel && mastery.Required > 0)
        {
            var bar = new ProgressBar { Name = "MasteryProgress", CustomMinimumSize = new Vector2(0, 6),
                MaxValue = mastery.Required, Value = mastery.Current, ShowPercentage = false };
            bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("263034") });
            bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = Gold });
            section.AddChild(bar);
        }
        return section;
    }

    private static VBoxContainer Metric(string title, string value, float ratio = 1)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio };
        column.AddThemeConstantOverride("separation", 6);
        column.AddChild(MakeLabel(title, 13, Muted));
        column.AddChild(MakeLabel(value, 20, Ink, true));
        return column;
    }

    private static string FormatCosts(CombatSkillResourceCosts costs)
    {
        var parts = new List<string>();
        if (costs.ApCost > 0) parts.Add($"{costs.ApCost} AP");
        if (costs.MpCost > 0) parts.Add($"{costs.MpCost} MP");
        if (costs.StaminaCost > 0) parts.Add($"{costs.StaminaCost} ST");
        if (costs.AuraCost > 0) parts.Add($"{costs.AuraCost} 灵气");
        if (parts.Count > 2)
            return string.Join(" · ", parts.GetRange(0, 2)) + "\n" + string.Join(" · ", parts.GetRange(2, parts.Count - 2));
        return parts.Count > 0 ? string.Join(" · ", parts) : "无消耗";
    }

    private static Label MakeLabel(string text, int fontSize, Color color, bool wrap = false)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static ColorRect Rule() => new() { CustomMinimumSize = new Vector2(0, 1),
        Color = new Color(0.65f, 0.55f, 0.35f, 0.3f), MouseFilter = MouseFilterEnum.Ignore };

    private static void IgnoreLayoutMouse(Control node)
    {
        node.MouseFilter = MouseFilterEnum.Ignore;
        foreach (Node child in node.GetChildren())
            if (child is Control control) IgnoreLayoutMouse(control);
    }
}

public partial class BattleSkillTooltipFrame : Control
{
    public override void _Ready() => Resized += QueueRedraw;

    public override void _Draw()
    {
        var gold = new Color("a68b58");
        var shine = new Color("e0c58b");
        Vector2[] outline = Bevel(3, 13);
        DrawColoredPolygon(outline, new Color("0e171d"));
        DrawPolyline(outline, new Color("05090c"), 6, true);
        DrawPolyline(outline, gold, 2, true);
        DrawPolyline(Bevel(8, 10), new Color("455047"), 1, true);
        DrawPolyline(Bevel(11, 8), new Color(0.65f, 0.55f, 0.35f, 0.2f), 1, true);
        DrawRect(new Rect2(17, 18, Size.X - 34, 101), new Color(0.13f, 0.19f, 0.22f, 0.38f));
        foreach (Vector2 corner in new[] { new Vector2(9, 9), new Vector2(Size.X - 9, 9),
            new Vector2(9, Size.Y - 9), Size - new Vector2(9, 9) })
        {
            float dx = corner.X < Size.X / 2 ? 1 : -1;
            float dy = corner.Y < Size.Y / 2 ? 1 : -1;
            Vector2 P(float x, float y) => corner + new Vector2(dx * x, dy * y);
            DrawPolyline(new[] { P(0, 38), P(0, 10), P(10, 0), P(38, 0) }, shine, 2, true);
            DrawPolyline(new[] { P(5, 27), P(5, 13), P(13, 5), P(27, 5) }, gold, 1, true);
            DrawColoredPolygon(new[] { P(8, 17), P(17, 8), P(13, 17), P(8, 22) }, gold);
        }
        foreach (float y in new[] { 9f, Size.Y - 9 })
        {
            float x = Size.X / 2;
            DrawLine(new Vector2(x - 90, y), new Vector2(x - 12, y), gold, 1, true);
            DrawLine(new Vector2(x + 12, y), new Vector2(x + 90, y), gold, 1, true);
            var diamond = new[] { new Vector2(x, y - 5), new Vector2(x + 7, y),
                new Vector2(x, y + 5), new Vector2(x - 7, y), new Vector2(x, y - 5) };
            DrawColoredPolygon(diamond, new Color("293d42"));
            DrawPolyline(diamond, shine, 1, true);
        }
    }

    private Vector2[] Bevel(float inset, float cut) => new[]
    {
        new Vector2(inset + cut, inset), new Vector2(Size.X - inset - cut, inset),
        new Vector2(Size.X - inset, inset + cut), new Vector2(Size.X - inset, Size.Y - inset - cut),
        new Vector2(Size.X - inset - cut, Size.Y - inset), new Vector2(inset + cut, Size.Y - inset),
        new Vector2(inset, Size.Y - inset - cut), new Vector2(inset, inset + cut), new Vector2(inset + cut, inset),
    };
}
