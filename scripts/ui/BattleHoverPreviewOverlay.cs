using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleHoverPreviewOverlay : PanelContainer
{
    private const int HitStageSegmentWidth = 50;
    private const int HitStageSegmentHeight = 16;
    private const int HitStageSegmentSeparation = 6;
    private const int HpBarHeight = 18;
    private const int HpBarMinWidth = 280;

    // 本地放大字号(仅此 hover 预览浮层,不动全局 BattleUiTheme),目标是一眼看清
    private const int PreviewFontLabel = 20;
    private const int PreviewFontCaption = 16;

    private VBoxContainer _layout;
    private HBoxContainer _targetHeader;
    private Label _targetNameLabel;
    private Label _targetFactionLabel;
    private Control _targetHpStack;
    private ProgressBar _targetHpLossBar;
    private ProgressBar _targetHpBar;
    private Label _targetHpLabel;
    private HBoxContainer _hitStageRow;
    private Label _hitSummaryLabel;
    private HFlowContainer _fateBadgeRow;
    private Label _damageLabel;
    private Label _invalidLabel;

    public override void _Ready()
    {
        if (_layout != null)
            return;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        CustomMinimumSize = new Vector2(340, 0);
        AddThemeStyleboxOverride("panel", _build_panel_style());
        _build_layout();
    }

    public void Clear()
    {
        Visible = false;
    }

    internal void ApplyPreview(BattleHoverSnapshot preview)
    {
        if (preview == null)
        {
            Visible = false;
            return;
        }

        BattleHoverTargetUnitSnapshot targetUnit = preview.TargetUnit;
        bool hasTargetUnit = targetUnit != null;
        bool hasSkill = preview.HasSelectedSkill;
        bool isValidTarget = preview.HoverIsValidTarget;

        if (!hasTargetUnit && !hasSkill)
        {
            Visible = false;
            return;
        }

        _refresh_target_unit(
            targetUnit,
            preview.DamageMin,
            preview.DamageMax,
            isValidTarget && preview.DamageMax > 0
        );
        _refresh_hit_stages(preview.HitStageRates);
        _refresh_fate_badges(preview.FateBadges);
        _refresh_damage_label(
            preview.DamageMin,
            preview.DamageMax,
            preview.DamageText
        );
        string saveBranchText = preview.SaveBranchPreviewText;
        _refresh_hit_summary(
            !string.IsNullOrEmpty(saveBranchText)
                ? saveBranchText
                : preview.HitBadgeText
        );
        _refresh_invalid_label(hasSkill && !isValidTarget);

        Visible = true;
    }

    private void _build_layout()
    {
        _layout = new VBoxContainer { Name = "HoverLayout" };
        _layout.AddThemeConstantOverride("separation", 10);
        AddChild(_layout);

        _targetHeader = new HBoxContainer { Name = "TargetHeader" };
        _targetHeader.AddThemeConstantOverride("separation", 12);
        _layout.AddChild(_targetHeader);

        _targetNameLabel = new Label
        {
            Name = "TargetNameLabel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _targetNameLabel.AddThemeFontSizeOverride("font_size", PreviewFontLabel);
        _targetNameLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        _targetHeader.AddChild(_targetNameLabel);

        _targetFactionLabel = new Label { Name = "TargetFactionLabel" };
        _targetFactionLabel.AddThemeFontSizeOverride("font_size", PreviewFontCaption);
        _targetFactionLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        _targetHeader.AddChild(_targetFactionLabel);

        // HP 条为双层叠放：下层按当前 HP 填充预扣色，上层按"受击后剩余 HP"填充常规
        // HP 色；两层之间露出的色带即本次技能的预计伤害段（C3 伤害预扣段）。
        _targetHpStack = new Control
        {
            Name = "TargetHpStack",
            CustomMinimumSize = new Vector2(HpBarMinWidth, HpBarHeight),
        };
        _layout.AddChild(_targetHpStack);

        _targetHpLossBar = new ProgressBar
        {
            Name = "TargetHpLossBar",
            ShowPercentage = false,
        };
        _targetHpLossBar.SetAnchorsPreset(LayoutPreset.FullRect);
        _targetHpLossBar.AddThemeStyleboxOverride(
            "background",
            _build_progress_background_style()
        );
        _targetHpLossBar.AddThemeStyleboxOverride(
            "fill",
            _build_progress_fill_style(BattleUiTheme.FATE_WARNING())
        );
        _targetHpStack.AddChild(_targetHpLossBar);

        _targetHpBar = new ProgressBar
        {
            Name = "TargetHpBar",
            ShowPercentage = false,
        };
        _targetHpBar.SetAnchorsPreset(LayoutPreset.FullRect);
        _targetHpBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
        _targetHpBar.AddThemeStyleboxOverride(
            "fill",
            _build_progress_fill_style(BattleUiTheme.RESOURCE_HP())
        );
        _targetHpStack.AddChild(_targetHpBar);

        _targetHpLabel = new Label { Name = "TargetHpLabel" };
        _targetHpLabel.AddThemeFontSizeOverride("font_size", PreviewFontCaption);
        _targetHpLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_SECONDARY());
        _layout.AddChild(_targetHpLabel);

        _hitStageRow = new HBoxContainer { Name = "HitStageRow" };
        _hitStageRow.AddThemeConstantOverride("separation", HitStageSegmentSeparation);
        _layout.AddChild(_hitStageRow);

        _hitSummaryLabel = new Label { Name = "HitSummaryLabel" };
        _hitSummaryLabel.AddThemeFontSizeOverride("font_size", PreviewFontLabel);
        _hitSummaryLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        _layout.AddChild(_hitSummaryLabel);

        _fateBadgeRow = new HFlowContainer { Name = "FateBadgeRow" };
        _fateBadgeRow.AddThemeConstantOverride("h_separation", 6);
        _fateBadgeRow.AddThemeConstantOverride("v_separation", 4);
        _layout.AddChild(_fateBadgeRow);

        _damageLabel = new Label { Name = "DamageRangeLabel" };
        _damageLabel.AddThemeFontSizeOverride("font_size", PreviewFontLabel);
        _damageLabel.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        _layout.AddChild(_damageLabel);

        _invalidLabel = new Label
        {
            Name = "InvalidTargetLabel",
            Text = "不可达",
            Visible = false,
        };
        _invalidLabel.AddThemeFontSizeOverride("font_size", PreviewFontCaption);
        _invalidLabel.AddThemeColorOverride("font_color", BattleUiTheme.FATE_DANGER());
        _layout.AddChild(_invalidLabel);
    }

    private void _refresh_target_unit(
        BattleHoverTargetUnitSnapshot targetUnit,
        int damageMin,
        int damageMax,
        bool showDamagePredict
    )
    {
        if (targetUnit == null)
        {
            _targetHeader.Visible = false;
            _targetHpStack.Visible = false;
            _targetHpLabel.Visible = false;
            return;
        }

        _targetHeader.Visible = true;
        _targetHpStack.Visible = true;
        _targetHpLabel.Visible = true;
        _targetNameLabel.Text = string.IsNullOrEmpty(targetUnit.Name) ? "单位" : targetUnit.Name;
        _targetFactionLabel.Text =
            targetUnit.IsSelf ? "本单位"
            : targetUnit.IsEnemy ? "敌方"
            : "我方";

        int hpMax = Mathf.Max(targetUnit.HpMax, 1);
        int hpCurrent = Mathf.Clamp(targetUnit.HpCurrent, 0, hpMax);
        int remainingWorst = Mathf.Clamp(hpCurrent - Mathf.Max(damageMax, 0), 0, hpMax);
        int remainingBest = Mathf.Clamp(hpCurrent - Mathf.Max(damageMin, 0), 0, hpMax);
        _targetHpLossBar.MinValue = 0;
        _targetHpLossBar.MaxValue = hpMax;
        _targetHpLossBar.Value = hpCurrent;
        _targetHpBar.MinValue = 0;
        _targetHpBar.MaxValue = hpMax;
        _targetHpBar.Value = showDamagePredict ? remainingWorst : hpCurrent;
        if (!showDamagePredict)
        {
            _targetHpLabel.Text = $"HP {hpCurrent}/{hpMax}";
            return;
        }
        string remainingText = remainingWorst == remainingBest
            ? remainingWorst.ToString()
            : $"{remainingWorst}~{remainingBest}";
        _targetHpLabel.Text = $"HP {hpCurrent}/{hpMax} → 受击后 {remainingText}";
    }

    private void _refresh_hit_stages(IReadOnlyList<int> stageRates)
    {
        ClearChildren(_hitStageRow);
        if (stageRates.Count == 0)
        {
            _hitStageRow.Visible = false;
            return;
        }

        _hitStageRow.Visible = true;
        foreach (int rateValue in stageRates)
            _hitStageRow.AddChild(_build_hit_stage_segment(rateValue));
    }

    private void _refresh_hit_summary(string summaryText)
    {
        if (string.IsNullOrEmpty(summaryText))
        {
            _hitSummaryLabel.Visible = false;
            _hitSummaryLabel.Text = "";
            return;
        }
        _hitSummaryLabel.Visible = true;
        _hitSummaryLabel.Text = summaryText;
    }

    private void _refresh_fate_badges(IReadOnlyList<BattleHudFateBadgeSnapshot> badges)
    {
        ClearChildren(_fateBadgeRow);
        if (badges.Count == 0)
        {
            _fateBadgeRow.Visible = false;
            return;
        }

        _fateBadgeRow.Visible = true;
        foreach (BattleHudFateBadgeSnapshot badge in badges)
        {
            if (badge == null)
                continue;
            _fateBadgeRow.AddChild(_build_fate_badge(badge));
        }
    }

    private void _refresh_damage_label(int damageMin, int damageMax, string damageText)
    {
        if (damageMax <= 0 && string.IsNullOrEmpty(damageText))
        {
            _damageLabel.Visible = false;
            _damageLabel.Text = "";
            return;
        }

        _damageLabel.Visible = true;
        if (damageMax > 0)
            _damageLabel.Text =
                damageMin == damageMax ? $"伤害 {damageMax}" : $"伤害 {damageMin}-{damageMax}";
        else
            _damageLabel.Text = damageText;
    }

    private void _refresh_invalid_label(bool shouldShow)
    {
        _invalidLabel.Visible = shouldShow;
    }

    private static Control _build_hit_stage_segment(int ratePercent)
    {
        int clamped = Mathf.Clamp(ratePercent, 0, 100);
        return new ColorRect
        {
            CustomMinimumSize = new Vector2(HitStageSegmentWidth, HitStageSegmentHeight),
            Color = _hit_stage_color(clamped),
            TooltipText = $"命中 {clamped}%",
        };
    }

    private static Control _build_fate_badge(BattleHudFateBadgeSnapshot badge)
    {
        var container = new PanelContainer();
        container.AddThemeStyleboxOverride(
            "panel",
            _build_fate_badge_style(badge.Tone ?? new StringName("calm"))
        );

        var label = new Label { Text = badge.Text ?? "" };
        label.AddThemeFontSizeOverride("font_size", PreviewFontCaption);
        label.AddThemeColorOverride("font_color", BattleUiTheme.TEXT_PRIMARY());
        container.AddChild(label);

        string tooltipText = badge.TooltipText ?? "";
        if (!string.IsNullOrEmpty(tooltipText))
            container.TooltipText = tooltipText;
        return container;
    }

    private static Color _hit_stage_color(int ratePercent)
    {
        int clamped = Mathf.Clamp(ratePercent, 0, 100);
        if (clamped >= 65)
            return BattleUiTheme.FATE_CALM();
        if (clamped >= 35)
            return BattleUiTheme.FATE_WARNING();
        return BattleUiTheme.FATE_DANGER();
    }

    private static StyleBoxFlat _build_panel_style()
    {
        return new StyleBoxFlat
        {
            BgColor = BattleUiTheme.PANEL_BG_DEEP(),
            BorderColor = BattleUiTheme.PANEL_EDGE(),
            BorderWidthLeft = BattleUiTheme.PANEL_BORDER(),
            BorderWidthRight = BattleUiTheme.PANEL_BORDER(),
            BorderWidthTop = BattleUiTheme.PANEL_BORDER(),
            BorderWidthBottom = BattleUiTheme.PANEL_BORDER(),
            CornerRadiusTopLeft = BattleUiTheme.PANEL_RADIUS_SMALL(),
            CornerRadiusTopRight = BattleUiTheme.PANEL_RADIUS_SMALL(),
            CornerRadiusBottomLeft = BattleUiTheme.PANEL_RADIUS_SMALL(),
            CornerRadiusBottomRight = BattleUiTheme.PANEL_RADIUS_SMALL(),
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
    }

    private static StyleBoxFlat _build_progress_background_style()
    {
        return new StyleBoxFlat
        {
            BgColor = BattleUiTheme.PANEL_BG_ALT(),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    private static StyleBoxFlat _build_progress_fill_style(Color fillColor)
    {
        return new StyleBoxFlat
        {
            BgColor = fillColor,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    private static StyleBoxFlat _build_fate_badge_style(StringName tone)
    {
        return new StyleBoxFlat
        {
            BgColor = BattleUiTheme.PANEL_BG(),
            BorderColor = BattleUiTheme.FateColor(tone),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = BattleUiTheme.PANEL_RADIUS_TINY(),
            CornerRadiusTopRight = BattleUiTheme.PANEL_RADIUS_TINY(),
            CornerRadiusBottomLeft = BattleUiTheme.PANEL_RADIUS_TINY(),
            CornerRadiusBottomRight = BattleUiTheme.PANEL_RADIUS_TINY(),
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
    }

    private static void ClearChildren(Container container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

}
