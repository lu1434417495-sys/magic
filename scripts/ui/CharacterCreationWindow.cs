using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class CharacterCreationWindow : Control
{
    [Signal]
    public delegate void character_confirmedEventHandler(GDictionary payload);

    [Signal]
    public delegate void cancelledEventHandler();

    private static readonly StringName[] AttributeOrder =
    {
        "strength",
        "agility",
        "constitution",
        "perception",
        "intelligence",
        "willpower",
    };

    private static readonly Dictionary<StringName, string> AttributeDisplayNames = new()
    {
        ["strength"] = "力量",
        ["agility"] = "敏捷",
        ["constitution"] = "体质",
        ["perception"] = "感知",
        ["intelligence"] = "智力",
        ["willpower"] = "意志",
    };

    private const int DiceCount = 5;
    private const int DiceSides = 3;
    private const int DiceOffset = -1;
    private const int DiceValueFloor = 4;
    private const int DiceMinTotal = DiceValueFloor;
    private const int DiceMaxTotal = DiceCount * DiceSides + DiceOffset;
    private const string DefaultName = "主角";
    private static readonly StringName DefaultCreationRaceId = "human";
    private static readonly StringName DefaultCreationAgeStageId = "adult";
    private static readonly StringName HumanVersatilityTraitId = "human_versatility";

    private const ulong LabelRefreshIntervalMs = 60;
    private const int TumbleSteps = 6;
    private const double TumbleIntervalSec = 0.05;
    private const double RowFlashDurationSec = 0.6;
    private const double ScanLightPeriodSec = 0.7;

    public ColorRect shade;
    public Control name_phase;
    public Control attribute_phase;
    public LineEdit name_input;
    public Button name_confirm_button;
    public Button name_cancel_button;
    public VBoxContainer content_root;
    public Label reroll_count_label;
    public Label luck_tier_label;
    public Label name_preview_label;
    public Button reroll_button;
    public Button stop_button;
    public Button confirm_button;
    public Button attribute_cancel_button;
    public Control scan_container;
    public ColorRect scan_light;

    public bool _rerolling;
    public bool _stop_requested;
    public int _reroll_count;
    public readonly Dictionary<StringName, int> _rolled_attributes = new();
    public string _player_name = "";
    public StringName _selected_race_id = "";
    public StringName _selected_subrace_id = "";
    public StringName _selected_age_stage_id = "";
    public int _selected_age_years;
    public StringName _selected_versatility_pick = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength);
    private readonly List<StringName> _raceOptionIds = new();
    private readonly List<StringName> _subraceOptionIds = new();
    private readonly List<StringName> _ageStageOptionIds = new();
    private readonly List<StringName> _versatilityOptionIds = new();

    public VBoxContainer race_phase;
    public VBoxContainer age_phase;
    public VBoxContainer identity_options_phase;
    public OptionButton race_variant_button;
    public OptionButton subrace_variant_button;
    public OptionButton age_stage_variant_button;
    public OptionButton versatility_variant_button;
    public Label race_preview_label;
    public Label age_preview_label;
    public Label identity_options_label;
    public Label final_attribute_preview_label;
    public Button race_back_button;
    public Button race_next_button;
    public Button age_back_button;
    public Button age_next_button;
    public Button options_back_button;
    public Button final_confirm_button;

    private readonly RuntimeRandom _rng = new(TrueRandomSeedService.GenerateSeed());
    private readonly Dictionary<StringName, Label> _attributeValueLabels = new();
    private readonly Dictionary<StringName, SpinBox> _attributeThresholdSpinboxes = new();
    private readonly Dictionary<StringName, PanelContainer> _attributeRowPanels = new();
    private readonly Dictionary<StringName, bool> _rowPreviousMet = new();
    private readonly Dictionary<StringName, Tween> _rowValueTweens = new();
    private Tween _scanTween;
    private ulong _lastLabelRefreshMsec;
    private StyleBoxFlat _rowStyleNormal;
    private StyleBoxFlat _rowStyleMet;
    private ProgressionContentRegistry _progressionContentRegistry = new ProgressionContentRegistry();
    private List<StringName> _raceIds = new();
    private List<StringName> _subraceIds = new();
    private GStringNameArray _ageStageIds = new();

    public override void _Ready()
    {
        _rng.Reseed(TrueRandomSeedService.GenerateSeed());
        _build_row_styles();
        _cache_attribute_rows();
        _build_identity_phase_nodes();
        _apply_button_palettes();

        name_confirm_button.Pressed += _on_name_confirmed;
        name_cancel_button.Pressed += _cancel;
        name_input.TextSubmitted += _on_name_text_submitted;
        reroll_button.Pressed += _on_reroll_pressed;
        stop_button.Pressed += _on_stop_pressed;
        confirm_button.Pressed += _on_attribute_next_pressed;
        attribute_cancel_button.Pressed += _cancel;
        scan_container.Resized += _on_scan_container_resized;

        HideWindow();
    }

    public void SetProgressionContentRegistry(ProgressionContentRegistry registry)
    {
        if (registry == null)
            return;
        _progressionContentRegistry = registry;
        _rebuild_creation_identity_options();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
            return;
        if (@event is not InputEventKey keyEvent)
            return;
        if (!keyEvent.Pressed || keyEvent.Echo)
            return;
        if (keyEvent.Keycode != Key.Escape)
            return;

        GetViewport()?.SetInputAsHandled();
        _cancel();
    }

    public void ShowWindow()
    {
        Visible = true;
        _rerolling = false;
        _stop_requested = false;
        _reroll_count = 0;
        _rolled_attributes.Clear();
        _rowPreviousMet.Clear();
        _player_name = "";
        _rebuild_creation_identity_options();
        name_input.Text = "";
        foreach (StringName attributeId in AttributeOrder)
        {
            if (_attributeThresholdSpinboxes.TryGetValue(attributeId, out SpinBox spinbox))
                spinbox.Value = DiceMinTotal;
            if (_attributeValueLabels.TryGetValue(attributeId, out Label label))
            {
                label.Text = "—";
                label.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
            }
            if (_attributeRowPanels.TryGetValue(attributeId, out PanelContainer panel))
                panel.Modulate = Colors.White;
            _apply_row_state(attributeId, false);
        }
        name_phase.Visible = true;
        attribute_phase.Visible = false;
        _hide_identity_phases();
        _refresh_reroll_count_label();
        _refresh_luck_tier_indicator();
        _set_rerolling_visuals(false);
        _update_button_states();
        name_input.GrabFocus();
    }

    public void HideWindow()
    {
        Visible = false;
        _rerolling = false;
        _stop_requested = false;
        _reroll_count = 0;
        _rolled_attributes.Clear();
        _player_name = "";
        _hide_identity_phases();
        _set_rerolling_visuals(false);
        _kill_all_value_tweens();
        foreach (StringName attributeId in AttributeOrder)
        {
            if (_attributeRowPanels.TryGetValue(attributeId, out PanelContainer panel))
                panel.Modulate = Colors.White;
        }
    }

    private void _cache_attribute_rows()
    {
        shade = GetNode<ColorRect>("Shade");
        name_phase = GetNode<Control>("%NamePhase");
        attribute_phase = GetNode<Control>("%AttributePhase");
        name_input = GetNode<LineEdit>("%NameInput");
        name_confirm_button = GetNode<Button>("%NameConfirmButton");
        name_cancel_button = GetNode<Button>("%NameCancelButton");
        content_root = GetNode<VBoxContainer>("CenterContainer/Panel/Margin/Content");
        reroll_count_label = GetNode<Label>("%RerollCountLabel");
        luck_tier_label = GetNode<Label>("%LuckTierLabel");
        name_preview_label = GetNode<Label>("%NamePreviewLabel");
        reroll_button = GetNode<Button>("%RerollButton");
        stop_button = GetNode<Button>("%StopButton");
        confirm_button = GetNode<Button>("%ConfirmButton");
        attribute_cancel_button = GetNode<Button>("%AttributeCancelButton");
        scan_container = GetNode<Control>("%ScanContainer");
        scan_light = GetNode<ColorRect>("%ScanLight");

        _attributeValueLabels.Clear();
        _attributeValueLabels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength)] = GetNode<Label>(
            "%StrengthValueLabel"
        );
        _attributeValueLabels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)] = GetNode<Label>("%AgilityValueLabel");
        _attributeValueLabels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)] = GetNode<Label>(
            "%ConstitutionValueLabel"
        );
        _attributeValueLabels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception)] = GetNode<Label>(
            "%PerceptionValueLabel"
        );
        _attributeValueLabels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)] = GetNode<Label>(
            "%IntelligenceValueLabel"
        );
        _attributeValueLabels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)] = GetNode<Label>(
            "%WillpowerValueLabel"
        );

        _attributeThresholdSpinboxes.Clear();
        _attributeThresholdSpinboxes[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength)] = GetNode<SpinBox>(
            "%StrengthThresholdSpinBox"
        );
        _attributeThresholdSpinboxes[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)] = GetNode<SpinBox>(
            "%AgilityThresholdSpinBox"
        );
        _attributeThresholdSpinboxes[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)] = GetNode<SpinBox>(
            "%ConstitutionThresholdSpinBox"
        );
        _attributeThresholdSpinboxes[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception)] = GetNode<SpinBox>(
            "%PerceptionThresholdSpinBox"
        );
        _attributeThresholdSpinboxes[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)] = GetNode<SpinBox>(
            "%IntelligenceThresholdSpinBox"
        );
        _attributeThresholdSpinboxes[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)] = GetNode<SpinBox>(
            "%WillpowerThresholdSpinBox"
        );

        _attributeRowPanels.Clear();
        _attributeRowPanels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength)] = GetNode<PanelContainer>(
            "%StrengthRow"
        );
        _attributeRowPanels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)] = GetNode<PanelContainer>("%AgilityRow");
        _attributeRowPanels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)] = GetNode<PanelContainer>(
            "%ConstitutionRow"
        );
        _attributeRowPanels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception)] = GetNode<PanelContainer>(
            "%PerceptionRow"
        );
        _attributeRowPanels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)] = GetNode<PanelContainer>(
            "%IntelligenceRow"
        );
        _attributeRowPanels[UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)] = GetNode<PanelContainer>(
            "%WillpowerRow"
        );

        foreach (StringName attributeId in AttributeOrder)
        {
            if (_attributeThresholdSpinboxes.TryGetValue(attributeId, out SpinBox spinbox))
            {
                spinbox.MinValue = DiceMinTotal;
                spinbox.MaxValue = DiceMaxTotal;
                spinbox.Step = 1;
                spinbox.Rounded = true;
                spinbox.AllowGreater = false;
                spinbox.AllowLesser = false;
                spinbox.Value = DiceMinTotal;
                SpinBox capturedSpinbox = spinbox;
                spinbox.ValueChanged += value =>
                    _on_threshold_value_changed(value, capturedSpinbox);
            }
            _apply_row_state(attributeId, false);
        }
    }

    private void _build_row_styles()
    {
        _rowStyleNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.13f, 0.20f, 0.65f),
            BorderColor = new Color(0.32f, 0.4f, 0.55f, 0.45f),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        _rowStyleNormal.SetBorderWidthAll(1);
        _rowStyleNormal.SetCornerRadiusAll(6);

        _rowStyleMet = new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.14f, 0.07f, 0.92f),
            BorderColor = new Color(1.0f, 0.78f, 0.32f),
            ShadowColor = new Color(1.0f, 0.7f, 0.2f, 0.4f),
            ShadowSize = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        _rowStyleMet.SetBorderWidthAll(2);
        _rowStyleMet.SetCornerRadiusAll(6);
    }

    private void _apply_button_palettes()
    {
        Color subdued = new(0.22f, 0.24f, 0.32f);
        Color primary = new(0.27f, 0.32f, 0.5f);
        Color danger = new(0.55f, 0.18f, 0.18f);
        Color emphasis = new(0.65f, 0.45f, 0.18f);
        _apply_button_palette(name_cancel_button, subdued, new Color(0.85f, 0.88f, 0.95f));
        _apply_button_palette(attribute_cancel_button, subdued, new Color(0.85f, 0.88f, 0.95f));
        _apply_button_palette(reroll_button, primary, new Color(0.95f, 0.95f, 1.0f));
        _apply_button_palette(stop_button, danger, new Color(1.0f, 0.92f, 0.85f));
        _apply_button_palette(confirm_button, emphasis, new Color(1.0f, 0.96f, 0.82f));
        _apply_button_palette(name_confirm_button, emphasis, new Color(1.0f, 0.96f, 0.82f));

        foreach (Button button in new[] { race_back_button, age_back_button, options_back_button })
            _apply_button_palette(button, subdued, new Color(0.85f, 0.88f, 0.95f));
        foreach (Button button in new[] { race_next_button, age_next_button, final_confirm_button })
            _apply_button_palette(button, emphasis, new Color(1.0f, 0.96f, 0.82f));
    }

    private void _apply_button_palette(Button button, Color baseColor, Color textColor)
    {
        if (button == null)
            return;
        StyleBoxFlat normal = _make_button_stylebox(baseColor);
        normal.BorderColor = baseColor.Lightened(0.18f);
        StyleBoxFlat hover = _make_button_stylebox(baseColor.Lightened(0.12f));
        hover.BorderColor = baseColor.Lightened(0.4f);
        StyleBoxFlat pressed = _make_button_stylebox(baseColor.Darkened(0.18f));
        pressed.BorderColor = baseColor.Darkened(0.05f);
        StyleBoxFlat focus = _make_button_stylebox(new Color(0, 0, 0, 0));
        focus.BorderColor = new Color(1.0f, 0.85f, 0.4f);
        focus.SetBorderWidthAll(2);
        StyleBoxFlat disabled = _make_button_stylebox(new Color(0.18f, 0.18f, 0.22f, 0.85f));
        disabled.BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.5f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeStyleboxOverride("disabled", disabled);
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", textColor);
        button.AddThemeColorOverride("font_pressed_color", textColor);
        button.AddThemeColorOverride("font_focus_color", textColor);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.55f, 0.6f));
    }

    private static StyleBoxFlat _make_button_stylebox(Color bg)
    {
        var styleBox = new StyleBoxFlat { BgColor = bg };
        styleBox.SetCornerRadiusAll(8);
        styleBox.SetContentMarginAll(10);
        styleBox.SetBorderWidthAll(1);
        return styleBox;
    }

    public void _on_name_text_submitted(string _text)
    {
        _on_name_confirmed();
    }

    public void _on_name_confirmed()
    {
        if (_rerolling || !name_phase.Visible)
            return;
        _player_name = _resolve_name_input();
        _enter_attribute_phase();
    }

    private string _resolve_name_input()
    {
        string text = name_input.Text.StripEdges();
        return string.IsNullOrEmpty(text) ? DefaultName : text;
    }

    private void _enter_attribute_phase()
    {
        name_phase.Visible = false;
        attribute_phase.Visible = true;
        _hide_identity_phases();
        name_preview_label.Text = $"角色名：{_player_name}";
        confirm_button.Text = "下一步";
        _reroll_count = 0;
        _rowPreviousMet.Clear();
        _roll_once_silent();
        _refresh_reroll_count_label();
        _refresh_luck_tier_indicator();
        _animate_all_value_tumbles();
        _update_button_states();
        reroll_button.GrabFocus();
    }

    private void _roll_once_silent()
    {
        foreach (StringName attributeId in AttributeOrder)
            _rolled_attributes[attributeId] = _roll_attribute_value();
    }

    private int _roll_attribute_value()
    {
        int total = DiceOffset;
        for (int i = 0; i < DiceCount; i++)
            total += _rng.RandiRange(1, DiceSides);
        return Mathf.Max(DiceValueFloor, total);
    }

    private int RolledAttributeValue(StringName attributeId)
    {
        return _rolled_attributes.TryGetValue(attributeId, out int value) ? value : 0;
    }

    private void _refresh_attribute_value_labels()
    {
        foreach (StringName attributeId in AttributeOrder)
        {
            int value = RolledAttributeValue(attributeId);
            _set_value_label(attributeId, value, true);
            bool met = _row_meets_threshold(attributeId);
            bool wasMet = _rowPreviousMet.TryGetValue(attributeId, out bool previous) && previous;
            _apply_row_state(attributeId, met);
            if (met && !wasMet && !_rerolling)
                _flash_row(attributeId);
            _rowPreviousMet[attributeId] = met;
        }
    }

    private void _set_value_label(StringName attributeId, int value, bool finalValue)
    {
        if (!_attributeValueLabels.TryGetValue(attributeId, out Label label))
            return;
        label.Text = value.ToString();
        label.AddThemeColorOverride(
            "font_color",
            finalValue ? _value_tier_color(value) : new Color(0.78f, 0.82f, 0.92f)
        );
    }

    private static Color _value_tier_color(int value)
    {
        if (value <= 6)
            return new Color(0.6f, 0.62f, 0.66f);
        if (value <= 9)
            return new Color(0.92f, 0.94f, 0.97f);
        if (value <= 11)
            return new Color(0.95f, 0.85f, 0.55f);
        if (value <= 13)
            return new Color(1.0f, 0.8f, 0.3f);
        return new Color(1.0f, 0.66f, 0.2f);
    }

    private bool _row_meets_threshold(StringName attributeId)
    {
        if (!_attributeThresholdSpinboxes.TryGetValue(attributeId, out SpinBox spinbox))
            return false;
        int threshold = (int)spinbox.Value;
        if (threshold <= DiceMinTotal)
            return false;
        return RolledAttributeValue(attributeId) >= threshold;
    }

    private void _apply_row_state(StringName attributeId, bool met)
    {
        if (_attributeRowPanels.TryGetValue(attributeId, out PanelContainer panel))
            panel.AddThemeStyleboxOverride("panel", met ? _rowStyleMet : _rowStyleNormal);
    }

    private void _flash_row(StringName attributeId)
    {
        if (!_attributeRowPanels.TryGetValue(attributeId, out PanelContainer panel))
            return;
        panel.Modulate = new Color(1.55f, 1.4f, 1.05f);
        Tween tween = CreateTween();
        tween
            .TweenProperty(panel, "modulate", Colors.White, RowFlashDurationSec)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    private void _animate_all_value_tumbles()
    {
        foreach (StringName attributeId in AttributeOrder)
            _animate_value_tumble(attributeId, RolledAttributeValue(attributeId));
    }

    private void _kill_all_value_tweens()
    {
        foreach (StringName attributeId in AttributeOrder)
        {
            if (
                _rowValueTweens.TryGetValue(attributeId, out Tween existing)
                && existing != null
                && existing.IsValid()
            )
                existing.Kill();
            _rowValueTweens[attributeId] = null;
        }
    }

    private void _animate_value_tumble(StringName attributeId, int targetValue)
    {
        if (!_attributeValueLabels.ContainsKey(attributeId))
            return;
        if (
            _rowValueTweens.TryGetValue(attributeId, out Tween existing)
            && existing != null
            && existing.IsValid()
        )
            existing.Kill();
        Tween tween = CreateTween();
        _rowValueTweens[attributeId] = tween;
        for (int i = 0; i < TumbleSteps; i++)
        {
            int sample = _rng.RandiRange(DiceValueFloor, DiceMaxTotal);
            tween.TweenCallback(Callable.From(() => _set_value_label(attributeId, sample, false)));
            tween.TweenInterval(TumbleIntervalSec);
        }
        tween.TweenCallback(Callable.From(() => _set_value_label(attributeId, targetValue, true)));
        tween.TweenCallback(Callable.From(() => _finalize_row_state(attributeId)));
    }

    private void _finalize_row_state(StringName attributeId)
    {
        bool met = _row_meets_threshold(attributeId);
        bool wasMet = _rowPreviousMet.TryGetValue(attributeId, out bool previous) && previous;
        _apply_row_state(attributeId, met);
        if (met && !wasMet)
            _flash_row(attributeId);
        _rowPreviousMet[attributeId] = met;
    }

    private void _refresh_reroll_count_label()
    {
        reroll_count_label.Text = $"Reroll 次数：{_reroll_count}";
    }

    private void _refresh_luck_tier_indicator()
    {
        int tier = CharacterCreationService.MapRerollCountToHiddenLuckAtBirth(_reroll_count);
        luck_tier_label.Text = $"出生运势 {tier:+0;-0;0}  {_luck_tier_glyphs(tier)}";
        luck_tier_label.AddThemeColorOverride("font_color", _luck_tier_color(tier));
    }

    private static string _luck_tier_glyphs(int tier)
    {
        if (tier >= 2)
            return "✦✦✦✦✦";
        if (tier == 1)
            return "✦✦✦✦·";
        if (tier == 0)
            return "✦✦✦··";
        if (tier == -1)
            return "✦✦···";
        if (tier == -2)
            return "✦····";
        return "·····";
    }

    private static Color _luck_tier_color(int tier)
    {
        if (tier >= 2)
            return new Color(1.0f, 0.85f, 0.4f);
        if (tier == 1)
            return new Color(0.95f, 0.85f, 0.55f);
        if (tier == 0)
            return new Color(0.92f, 0.93f, 0.95f);
        if (tier == -1)
            return new Color(0.7f, 0.7f, 0.7f);
        if (tier == -2)
            return new Color(0.6f, 0.55f, 0.5f);
        return new Color(0.7f, 0.4f, 0.35f);
    }

    private void _update_button_states()
    {
        reroll_button.Disabled = _rerolling;
        stop_button.Disabled = !_rerolling;
        confirm_button.Disabled = _rerolling;
        attribute_cancel_button.Disabled = _rerolling;
        name_confirm_button.Disabled = _rerolling;
        name_cancel_button.Disabled = _rerolling;
        foreach (StringName attributeId in AttributeOrder)
        {
            if (_attributeThresholdSpinboxes.TryGetValue(attributeId, out SpinBox spinbox))
                spinbox.Editable = !_rerolling;
        }

        bool identityValid = _build_selected_identity_payload().Count > 0;
        if (race_next_button != null)
            race_next_button.Disabled = !identityValid;
        if (age_next_button != null)
            age_next_button.Disabled = !identityValid;
        if (final_confirm_button != null)
            final_confirm_button.Disabled = !identityValid;
    }

    private bool _has_any_threshold()
    {
        foreach (StringName attributeId in AttributeOrder)
        {
            if (
                _attributeThresholdSpinboxes.TryGetValue(attributeId, out SpinBox spinbox)
                && (int)spinbox.Value > DiceMinTotal
            )
                return true;
        }
        return false;
    }

    private bool _meets_thresholds()
    {
        foreach (StringName attributeId in AttributeOrder)
        {
            if (!_attributeThresholdSpinboxes.TryGetValue(attributeId, out SpinBox spinbox))
                continue;
            int threshold = (int)spinbox.Value;
            if (threshold <= DiceMinTotal)
                continue;
            if (RolledAttributeValue(attributeId) < threshold)
                return false;
        }
        return true;
    }

    public void _on_threshold_value_changed(double value, SpinBox spinbox)
    {
        if (spinbox == null)
            return;
        int clampedValue = Mathf.Clamp(Mathf.RoundToInt(value), DiceMinTotal, DiceMaxTotal);
        if ((int)spinbox.Value == clampedValue)
            return;
        spinbox.SetValueNoSignal(clampedValue);
    }

    public async void _on_reroll_pressed()
    {
        if (_rerolling)
            return;
        if (_has_any_threshold())
        {
            await _run_continuous_reroll();
            return;
        }
        _reroll_count += 1;
        _roll_once_silent();
        _refresh_reroll_count_label();
        _refresh_luck_tier_indicator();
        _animate_all_value_tumbles();
    }

    public void _on_stop_pressed()
    {
        if (_rerolling)
            _stop_requested = true;
    }

    private async Task _run_continuous_reroll()
    {
        _rerolling = true;
        _stop_requested = false;
        _lastLabelRefreshMsec = 0;
        _kill_all_value_tweens();
        _set_rerolling_visuals(true);
        _update_button_states();
        while (true)
        {
            _reroll_count += 1;
            _roll_once_silent();
            if (_meets_thresholds() || _stop_requested)
                break;
            ulong now = Time.GetTicksMsec();
            if (now - _lastLabelRefreshMsec >= LabelRefreshIntervalMs)
            {
                _refresh_attribute_value_labels();
                _refresh_reroll_count_label();
                _refresh_luck_tier_indicator();
                _lastLabelRefreshMsec = now;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _rerolling = false;
        _stop_requested = false;
        _set_rerolling_visuals(false);
        _refresh_attribute_value_labels();
        _refresh_reroll_count_label();
        _refresh_luck_tier_indicator();
        _update_button_states();
    }

    private void _set_rerolling_visuals(bool active)
    {
        if (scan_light == null || scan_container == null)
            return;
        if (_scanTween != null && _scanTween.IsValid())
        {
            _scanTween.Kill();
            _scanTween = null;
        }
        scan_light.Visible = active;
        if (active)
            _start_scan_tween();
    }

    public void _on_scan_container_resized()
    {
        if (!_rerolling)
            return;
        if (_scanTween != null && _scanTween.IsValid())
        {
            _scanTween.Kill();
            _scanTween = null;
        }
        _start_scan_tween();
    }

    private void _start_scan_tween()
    {
        if (scan_light == null || scan_container == null)
            return;
        float maxX = Mathf.Max(scan_container.Size.X - scan_light.Size.X, 0.0f);
        scan_light.Position = new Vector2(0.0f, scan_light.Position.Y);
        _scanTween = CreateTween().SetLoops();
        _scanTween
            .TweenProperty(scan_light, "position:x", maxX, ScanLightPeriodSec)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        _scanTween
            .TweenProperty(scan_light, "position:x", 0.0f, ScanLightPeriodSec)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private void _build_identity_phase_nodes()
    {
        race_phase = _make_phase_container("RaceAndSubracePhase");
        race_phase.AddChild(_make_phase_label("种族与亚种", 20));
        race_variant_button = _make_variant_button();
        subrace_variant_button = _make_variant_button();
        race_phase.AddChild(_make_labeled_variant_row("种族", race_variant_button));
        race_phase.AddChild(_make_labeled_variant_row("亚种", subrace_variant_button));
        race_preview_label = _make_phase_label("", 14);
        race_phase.AddChild(race_preview_label);
        HBoxContainer raceButtons = _make_button_row();
        race_back_button = _make_phase_button("上一步");
        race_next_button = _make_phase_button("下一步");
        raceButtons.AddChild(race_back_button);
        raceButtons.AddChild(race_next_button);
        race_phase.AddChild(raceButtons);
        content_root.AddChild(race_phase);

        age_phase = _make_phase_container("AgePhase");
        age_phase.AddChild(_make_phase_label("年龄阶段", 20));
        age_stage_variant_button = _make_variant_button();
        age_phase.AddChild(_make_labeled_variant_row("阶段", age_stage_variant_button));
        age_preview_label = _make_phase_label("", 14);
        age_phase.AddChild(age_preview_label);
        HBoxContainer ageButtons = _make_button_row();
        age_back_button = _make_phase_button("上一步");
        age_next_button = _make_phase_button("下一步");
        ageButtons.AddChild(age_back_button);
        ageButtons.AddChild(age_next_button);
        age_phase.AddChild(ageButtons);
        content_root.AddChild(age_phase);

        identity_options_phase = _make_phase_container("IdentityOptionsPhase");
        identity_options_phase.AddChild(_make_phase_label("身份选项", 20));
        versatility_variant_button = _make_variant_button();
        identity_options_phase.AddChild(
            _make_labeled_variant_row("适应属性", versatility_variant_button)
        );
        identity_options_label = _make_phase_label("", 14);
        final_attribute_preview_label = _make_phase_label("", 14);
        identity_options_phase.AddChild(identity_options_label);
        identity_options_phase.AddChild(final_attribute_preview_label);
        HBoxContainer optionsButtons = _make_button_row();
        options_back_button = _make_phase_button("上一步");
        final_confirm_button = _make_phase_button("确认");
        optionsButtons.AddChild(options_back_button);
        optionsButtons.AddChild(final_confirm_button);
        identity_options_phase.AddChild(optionsButtons);
        content_root.AddChild(identity_options_phase);

        race_variant_button.ItemSelected += index => _on_race_variant_selected((int)index);
        subrace_variant_button.ItemSelected += index => _on_subrace_variant_selected((int)index);
        age_stage_variant_button.ItemSelected += index => _on_age_stage_variant_selected((int)index);
        versatility_variant_button.ItemSelected += index =>
            _on_versatility_variant_selected((int)index);
        race_back_button.Pressed += _enter_attribute_phase_from_back;
        race_next_button.Pressed += _enter_age_phase;
        age_back_button.Pressed += _enter_race_phase;
        age_next_button.Pressed += _enter_identity_options_phase;
        options_back_button.Pressed += _enter_age_phase;
        final_confirm_button.Pressed += _on_confirm_pressed;
    }

    private static VBoxContainer _make_phase_container(string nodeName)
    {
        return new VBoxContainer { Name = nodeName, Visible = false }.WithSeparation(12);
    }

    private static Label _make_phase_label(string text, int fontSize)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.92f, 1.0f));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private static OptionButton _make_variant_button()
    {
        return new OptionButton
        {
            CustomMinimumSize = new Vector2(280, 36),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
    }

    private static HBoxContainer _make_labeled_variant_row(
        string labelText,
        OptionButton optionButton
    )
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = new Label
        {
            CustomMinimumSize = new Vector2(96, 0),
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
        label.AddThemeFontSizeOverride("font_size", 16);
        row.AddChild(label);
        row.AddChild(optionButton);
        return row;
    }

    private static HBoxContainer _make_button_row()
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 12);
        return row;
    }

    private static Button _make_phase_button(string text)
    {
        return new Button { CustomMinimumSize = new Vector2(120, 40), Text = text };
    }

    private void _hide_identity_phases()
    {
        if (race_phase != null)
            race_phase.Visible = false;
        if (age_phase != null)
            age_phase.Visible = false;
        if (identity_options_phase != null)
            identity_options_phase.Visible = false;
    }

    public void _rebuild_creation_identity_options()
    {
        if (_progressionContentRegistry == null)
        {
            _raceIds.Clear();
            _subraceIds.Clear();
            _ageStageIds.Clear();
            _selected_race_id = "";
            _selected_subrace_id = "";
            _selected_age_stage_id = "";
            _selected_age_years = 0;
            _refresh_identity_variant_controls();
            return;
        }

        _raceIds = new List<StringName>(
            CharacterCreationIdentityOptionService.CollectCreationRaceIds(
                _progressionContentRegistry
            )
        );
        _selected_race_id = _choose_race_id(_selected_race_id);
        _selected_subrace_id = _choose_subrace_id(_selected_subrace_id);
        _refresh_age_stage_selection();
        _refresh_versatility_selection();
        _refresh_identity_variant_controls();
    }

    public void _refresh_identity_variant_controls()
    {
        _refresh_race_options();
        _refresh_subrace_options();
        _refresh_age_stage_options();
        _refresh_versatility_options();
        _refresh_identity_previews();
        _update_button_states();
    }

    private StringName _choose_race_id(StringName currentId)
    {
        return CharacterCreationIdentityOptionService.ChooseRaceId(
            _progressionContentRegistry,
            currentId,
            DefaultCreationRaceId
        );
    }

    private StringName _choose_subrace_id(StringName currentId)
    {
        _subraceIds = _collect_subrace_ids_for_race(_selected_race_id);
        return CharacterCreationIdentityOptionService.ChooseSubraceId(
            _progressionContentRegistry,
            _selected_race_id,
            currentId
        );
    }

    private List<StringName> _collect_subrace_ids_for_race(StringName raceId)
    {
        return new List<StringName>(
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(
                _progressionContentRegistry,
                raceId
            )
        );
    }

    private void _refresh_age_stage_selection()
    {
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        _ageStageIds = _collect_creation_age_stage_ids(ageProfile);
        if (_ageStageIds.Contains(_selected_age_stage_id))
        {
            _selected_age_years = _resolve_default_age_for_stage(
                ageProfile,
                _selected_age_stage_id
            );
            return;
        }
        if (_ageStageIds.Contains(DefaultCreationAgeStageId))
            _selected_age_stage_id = DefaultCreationAgeStageId;
        else if (_ageStageIds.Count > 0)
            _selected_age_stage_id = _ageStageIds[0];
        else
            _selected_age_stage_id = "";
        _selected_age_years = _resolve_default_age_for_stage(ageProfile, _selected_age_stage_id);
    }

    private GStringNameArray _collect_creation_age_stage_ids(
        AgeProfileDefinition ageProfile
    )
    {
        var ids = new GStringNameArray();
        if (ageProfile == null)
            return ids;
        foreach (StringName stageId in ageProfile.CreationStageIds)
        {
            AgeStageRuleDefinition stageRule = _get_age_stage_rule(ageProfile, stageId);
            if (stageRule != null && stageRule.SelectableInCreation && !ids.Contains(stageId))
                ids.Add(stageId);
        }
        if (ids.Count == 0)
        {
            foreach (AgeStageRuleDefinition rule in ageProfile.StageRules)
            {
                if (rule != null && rule.SelectableInCreation && !ids.Contains(rule.StageId))
                    ids.Add(rule.StageId);
            }
        }
        return ids;
    }

    private void _refresh_versatility_selection()
    {
        if (!_selected_identity_has_human_versatility())
        {
            _selected_versatility_pick = "";
            return;
        }
        if (!AttributeContains(_selected_versatility_pick))
            _selected_versatility_pick = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength);
    }

    private void _refresh_race_options()
    {
        if (race_variant_button == null)
            return;
        race_variant_button.Clear();
        _raceOptionIds.Clear();
        foreach (StringName raceId in _raceIds)
        {
            RaceDefinition raceDef = _get_race_def(raceId);
            race_variant_button.AddItem(_identity_label(raceDef, raceId));
            _raceOptionIds.Add(raceId);
        }
        _select_variant_by_metadata(race_variant_button, _selected_race_id);
        race_variant_button.Disabled = race_variant_button.GetItemCount() <= 1;
    }

    private void _refresh_subrace_options()
    {
        if (subrace_variant_button == null)
            return;
        subrace_variant_button.Clear();
        _subraceOptionIds.Clear();
        foreach (StringName subraceId in _subraceIds)
        {
            SubraceDefinition subraceDef = _get_subrace_def(subraceId);
            subrace_variant_button.AddItem(_identity_label(subraceDef, subraceId));
            _subraceOptionIds.Add(subraceId);
        }
        _select_variant_by_metadata(subrace_variant_button, _selected_subrace_id);
        subrace_variant_button.Disabled = subrace_variant_button.GetItemCount() <= 1;
    }

    private void _refresh_age_stage_options()
    {
        if (age_stage_variant_button == null)
            return;
        age_stage_variant_button.Clear();
        _ageStageOptionIds.Clear();
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        foreach (StringName stageId in _ageStageIds)
        {
            AgeStageRuleDefinition stageRule = _get_age_stage_rule(ageProfile, stageId);
            age_stage_variant_button.AddItem(_identity_label(stageRule, stageId));
            _ageStageOptionIds.Add(stageId);
        }
        _select_variant_by_metadata(age_stage_variant_button, _selected_age_stage_id);
        age_stage_variant_button.Disabled = age_stage_variant_button.GetItemCount() <= 1;
    }

    private void _refresh_versatility_options()
    {
        if (versatility_variant_button == null)
            return;
        versatility_variant_button.Clear();
        _versatilityOptionIds.Clear();
        if (!_selected_identity_has_human_versatility())
        {
            versatility_variant_button.AddItem("无");
            _versatilityOptionIds.Add("");
            versatility_variant_button.Select(0);
            versatility_variant_button.Disabled = true;
            return;
        }
        foreach (StringName attributeId in AttributeOrder)
        {
            versatility_variant_button.AddItem(_attribute_display_name(attributeId));
            _versatilityOptionIds.Add(attributeId);
        }
        _select_variant_by_metadata(versatility_variant_button, _selected_versatility_pick);
        versatility_variant_button.Disabled = false;
    }

    public void _select_variant_by_metadata(OptionButton option_button, StringName target_id)
    {
        int index = _find_variant_index_by_metadata(option_button, target_id);
        if (index >= 0)
            option_button.Select(index);
    }

    public int _find_variant_index_by_metadata(OptionButton option_button, StringName target_id)
    {
        if (option_button == null)
            return -1;
        List<StringName> ids = _get_variant_ids(option_button);
        for (int index = 0; index < ids.Count; index++)
        {
            if (ids[index] == target_id)
                return index;
        }
        return -1;
    }

    private StringName _get_variant_metadata(OptionButton optionButton, int index)
    {
        List<StringName> ids = _get_variant_ids(optionButton);
        if (index < 0 || index >= ids.Count)
            return "";
        return ids[index];
    }

    private List<StringName> _get_variant_ids(OptionButton optionButton)
    {
        if (optionButton == race_variant_button)
            return _raceOptionIds;
        if (optionButton == subrace_variant_button)
            return _subraceOptionIds;
        if (optionButton == age_stage_variant_button)
            return _ageStageOptionIds;
        if (optionButton == versatility_variant_button)
            return _versatilityOptionIds;
        return new List<StringName>();
    }

    public void _on_race_variant_selected(int index)
    {
        StringName raceId = _get_variant_metadata(race_variant_button, index);
        if (raceId == (StringName)"")
            return;
        _selected_race_id = raceId;
        _selected_subrace_id = _choose_subrace_id("");
        _selected_age_stage_id = "";
        _refresh_age_stage_selection();
        _refresh_versatility_selection();
        _refresh_identity_variant_controls();
    }

    public void _on_subrace_variant_selected(int index)
    {
        StringName subraceId = _get_variant_metadata(subrace_variant_button, index);
        if (subraceId == (StringName)"")
            return;
        _selected_subrace_id = subraceId;
        _refresh_versatility_selection();
        _refresh_identity_variant_controls();
    }

    public void _on_age_stage_variant_selected(int index)
    {
        StringName stageId = _get_variant_metadata(age_stage_variant_button, index);
        if (stageId == (StringName)"")
            return;
        _selected_age_stage_id = stageId;
        _selected_age_years = _resolve_default_age_for_stage(_get_selected_age_profile(), stageId);
        _refresh_identity_previews();
        _update_button_states();
    }

    public void _on_versatility_variant_selected(int index)
    {
        _selected_versatility_pick = _get_variant_metadata(versatility_variant_button, index);
        _refresh_identity_previews();
    }

    public void _on_attribute_next_pressed()
    {
        if (_rerolling || _rolled_attributes.Count == 0)
            return;
        _enter_race_phase();
    }

    public void _enter_attribute_phase_from_back()
    {
        _hide_identity_phases();
        name_phase.Visible = false;
        attribute_phase.Visible = true;
        confirm_button.Text = "下一步";
        _update_button_states();
        reroll_button.GrabFocus();
    }

    public void _enter_race_phase()
    {
        if (_build_selected_identity_payload().Count == 0)
            _rebuild_creation_identity_options();
        name_phase.Visible = false;
        attribute_phase.Visible = false;
        _hide_identity_phases();
        race_phase.Visible = true;
        _refresh_identity_variant_controls();
        race_next_button.GrabFocus();
    }

    public void _enter_age_phase()
    {
        name_phase.Visible = false;
        attribute_phase.Visible = false;
        _hide_identity_phases();
        age_phase.Visible = true;
        _refresh_identity_variant_controls();
        age_next_button.GrabFocus();
    }

    public void _enter_identity_options_phase()
    {
        name_phase.Visible = false;
        attribute_phase.Visible = false;
        _hide_identity_phases();
        identity_options_phase.Visible = true;
        _refresh_identity_variant_controls();
        final_confirm_button.GrabFocus();
    }

    private void _refresh_identity_previews()
    {
        GDictionary identityPayload = _build_selected_identity_payload();
        if (race_preview_label != null)
            race_preview_label.Text = _build_race_preview_text(identityPayload);
        if (age_preview_label != null)
            age_preview_label.Text = _build_age_preview_text(identityPayload);
        if (identity_options_label != null)
            identity_options_label.Text = _build_identity_options_text(identityPayload);
        if (final_attribute_preview_label != null)
            final_attribute_preview_label.Text = _build_attribute_preview_text();
    }

    public GDictionary _build_selected_identity_payload()
    {
        if (
            !CharacterCreationIdentityOptionService.IsValidCreationRaceSubracePair(
                _progressionContentRegistry,
                _selected_race_id,
                _selected_subrace_id
            )
        )
        {
            return new GDictionary();
        }

        RaceDefinition raceDef = _get_selected_race_def();
        SubraceDefinition subraceDef = _get_selected_subrace_def();
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        AgeStageRuleDefinition ageStageRule = _get_age_stage_rule(
            ageProfile,
            _selected_age_stage_id
        );
        if (raceDef == null || subraceDef == null || ageProfile == null || ageStageRule == null)
            return new GDictionary();

        StringName bodySizeCategory = _resolve_body_size_category_for_selection(
            raceDef,
            subraceDef
        );
        int bodySize = BodySizeContentRules.GetBodySizeForCategory(bodySizeCategory);
        if (bodySizeCategory == (StringName)"" || bodySize <= 0)
            return new GDictionary();

        int ageYears = _selected_age_years;
        if (ageYears <= 0)
            ageYears = _resolve_default_age_for_stage(ageProfile, ageStageRule.StageId);
        StringName versatilityPick = _selected_identity_has_human_versatility()
            ? _selected_versatility_pick
            : new StringName("");

        return new GDictionary
        {
            ["race_id"] = raceDef.RaceId,
            ["subrace_id"] = subraceDef.SubraceId,
            ["age_years"] = ageYears,
            ["birth_at_world_step"] = 0,
            ["age_profile_id"] = ageProfile.ProfileId,
            ["natural_age_stage_id"] = ageStageRule.StageId,
            ["effective_age_stage_id"] = ageStageRule.StageId,
            ["effective_age_stage_source_type"] = new StringName(""),
            ["effective_age_stage_source_id"] = new StringName(""),
            ["body_size"] = bodySize,
            ["body_size_category"] = bodySizeCategory,
            ["versatility_pick"] = versatilityPick,
            ["active_stage_advancement_modifier_ids"] = new GArray(),
            ["bloodline_id"] = new StringName(""),
            ["bloodline_stage_id"] = new StringName(""),
            ["ascension_id"] = new StringName(""),
            ["ascension_stage_id"] = new StringName(""),
            ["ascension_started_at_world_step"] = -1,
            ["original_race_id_before_ascension"] = new StringName(""),
            ["biological_age_years"] = ageYears,
            ["astral_memory_years"] = 0,
        };
    }

    public string _build_race_preview_text(GDictionary identity_payload)
    {
        if (identity_payload == null || identity_payload.Count == 0)
            return "身份内容无效，无法继续建卡。";
        RaceDefinition raceDef = _get_selected_race_def();
        SubraceDefinition subraceDef = _get_selected_subrace_def();
        var lines = new List<string>
        {
            $"种族：{_identity_label(raceDef, _selected_race_id)}",
            $"亚种：{_identity_label(subraceDef, _selected_subrace_id)}",
            $"体型：{DictString(identity_payload, "body_size_category", "")}（{DictInt(identity_payload, "body_size", 0)}）",
        };
        List<string> traitLines = _collect_trait_summary_lines(false);
        if (traitLines.Count > 0)
            lines.Add($"特性：{string.Join("；", traitLines)}");
        return string.Join("\n", lines);
    }

    public string _build_age_preview_text(GDictionary identity_payload)
    {
        if (identity_payload == null || identity_payload.Count == 0)
            return "年龄内容无效，无法继续建卡。";
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        AgeStageRuleDefinition ageStageRule = _get_age_stage_rule(
            ageProfile,
            _selected_age_stage_id
        );
        var lines = new List<string>
        {
            $"年龄：{DictInt(identity_payload, "age_years", 0)}",
            $"自然阶段：{_identity_label(ageStageRule, _selected_age_stage_id)}",
            $"有效阶段：{_identity_label(ageStageRule, _selected_age_stage_id)}",
        };
        List<string> ageTraits = _collect_age_trait_summary_lines();
        if (ageTraits.Count > 0)
            lines.Add($"阶段特性：{string.Join("；", ageTraits)}");
        return string.Join("\n", lines);
    }

    public string _build_identity_options_text(GDictionary identity_payload)
    {
        if (identity_payload == null || identity_payload.Count == 0)
            return "身份内容无效，无法继续建卡。";
        RaceDefinition raceDef = _get_selected_race_def();
        SubraceDefinition subraceDef = _get_selected_subrace_def();
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        AgeStageRuleDefinition ageStageRule = _get_age_stage_rule(
            ageProfile,
            _selected_age_stage_id
        );
        var lines = new List<string>
        {
            $"姓名：{_player_name}",
            $"身份：{_identity_label(raceDef, _selected_race_id)} / {_identity_label(subraceDef, _selected_subrace_id)} / {_identity_label(ageStageRule, _selected_age_stage_id)}",
            $"体型：{DictString(identity_payload, "body_size_category", "")}（{DictInt(identity_payload, "body_size", 0)}）",
        };
        if (_selected_identity_has_human_versatility())
            lines.Add(
                $"适应属性：{_attribute_display_name(DictStringName(identity_payload, "versatility_pick"))} +1"
            );
        List<string> traitLines = _collect_trait_summary_lines(true);
        if (traitLines.Count > 0)
            lines.Add($"特性：{string.Join("；", traitLines)}");
        return string.Join("\n", lines);
    }

    public string _build_attribute_preview_text()
    {
        if (_rolled_attributes.Count == 0)
            return "属性：尚未掷骰";
        var lines = new List<string> { "属性预览：" };
        foreach (StringName attributeId in AttributeOrder)
        {
            int baseValue = RolledAttributeValue(attributeId);
            int finalValue = _resolve_preview_attribute_value(attributeId, baseValue);
            if (finalValue == baseValue)
                lines.Add($"{_attribute_display_name(attributeId)}：{baseValue}");
            else
                lines.Add(
                    $"{_attribute_display_name(attributeId)}：{baseValue} -> {finalValue} ({finalValue - baseValue:+0;-0;0})"
                );
        }
        return string.Join("\n", lines);
    }

    private int _resolve_preview_attribute_value(StringName attributeId, int baseValue)
    {
        var totals = new AttributeModifierTotals();
        RaceDefinition raceDef = _get_selected_race_def();
        SubraceDefinition subraceDef = _get_selected_subrace_def();
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        AgeStageRuleDefinition ageStageRule = _get_age_stage_rule(
            ageProfile,
            _selected_age_stage_id
        );
        if (raceDef != null)
            _accumulate_attribute_modifiers(totals, attributeId, raceDef.AttributeModifiers);
        if (subraceDef != null)
            _accumulate_attribute_modifiers(totals, attributeId, subraceDef.AttributeModifiers);
        if (ageStageRule != null)
            _accumulate_attribute_modifiers(
                totals,
                attributeId,
                ageStageRule.AttributeModifiers
            );
        if (_selected_identity_has_human_versatility() && _selected_versatility_pick == attributeId)
            totals.Flat += 1;
        int result = baseValue + totals.Flat;
        if (totals.Percent != 0)
            result = Mathf.FloorToInt(result * (100 + totals.Percent) / 100.0f);
        return result;
    }

    private static void _accumulate_attribute_modifiers(
        AttributeModifierTotals totals,
        StringName attributeId,
        IReadOnlyList<AttributeModifierDefinition> modifiers
    )
    {
        foreach (AttributeModifierDefinition modifier in modifiers)
        {
            if (modifier == null || modifier.AttributeId != attributeId)
                continue;
            int value = modifier.GetValueForRank(1);
            if (modifier.IsPercent())
                totals.Percent += value;
            else
                totals.Flat += value;
        }
    }

    private List<string> _collect_trait_summary_lines(bool includeOptions)
    {
        var lines = new List<string>();
        RaceDefinition raceDef = _get_selected_race_def();
        SubraceDefinition subraceDef = _get_selected_subrace_def();
        if (raceDef != null)
        {
            foreach (string line in raceDef.RacialTraitSummary)
            {
                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }
        }
        if (subraceDef != null)
        {
            foreach (string line in subraceDef.RacialTraitSummary)
            {
                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }
        }
        lines.AddRange(_collect_age_trait_summary_lines());
        if (includeOptions && _selected_identity_has_human_versatility())
            lines.Add($"人类多才：{_attribute_display_name(_selected_versatility_pick)} +1");
        return lines;
    }

    private List<string> _collect_age_trait_summary_lines()
    {
        var lines = new List<string>();
        AgeProfileDefinition ageProfile = _get_selected_age_profile();
        AgeStageRuleDefinition ageStageRule = _get_age_stage_rule(
            ageProfile,
            _selected_age_stage_id
        );
        if (ageStageRule == null)
            return lines;
        foreach (string line in ageStageRule.TraitSummary)
        {
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }
        return lines;
    }

    private bool _selected_identity_has_human_versatility()
    {
        var traitIds = new GStringNameArray();
        RaceDefinition raceDef = _get_selected_race_def();
        SubraceDefinition subraceDef = _get_selected_subrace_def();
        if (raceDef != null)
            AppendStringNameArray(traitIds, raceDef.TraitIds);
        if (subraceDef != null)
            AppendStringNameArray(traitIds, subraceDef.TraitIds);

        foreach (StringName traitId in traitIds)
        {
            if (traitId == HumanVersatilityTraitId)
                return true;
            TraitDefinition traitDef = _get_trait_def(traitId);
            if (traitDef != null && traitDef.EffectType == HumanVersatilityTraitId)
                return true;
        }
        return false;
    }

    private static StringName _resolve_body_size_category_for_selection(
        RaceDefinition raceDef,
        SubraceDefinition subraceDef
    )
    {
        if (
            subraceDef != null
            && subraceDef.BodySizeCategoryOverride != (StringName)""
            && BodySizeContentRules.IsValidBodySizeCategory(
                subraceDef.BodySizeCategoryOverride
            )
        )
        {
            return subraceDef.BodySizeCategoryOverride;
        }
        if (
            raceDef != null
            && BodySizeContentRules.IsValidBodySizeCategory(raceDef.BodySizeCategory)
        )
            return raceDef.BodySizeCategory;
        return "";
    }

    private RaceDefinition _get_selected_race_def() => _get_race_def(_selected_race_id);

    private SubraceDefinition _get_selected_subrace_def() =>
        _get_subrace_def(_selected_subrace_id);

    private AgeProfileDefinition _get_selected_age_profile()
    {
        RaceDefinition raceDef = _get_selected_race_def();
        if (raceDef == null)
            return null;
        if (_progressionContentRegistry == null)
            return null;
        IReadOnlyDictionary<StringName, AgeProfileDefinition> ageProfileDefs =
            _progressionContentRegistry.GetAgeProfileDefsTyped();
        return ageProfileDefs.TryGetValue(
            raceDef.AgeProfileId,
            out AgeProfileDefinition ageProfile
        )
            ? ageProfile
            : null;
    }

    private RaceDefinition _get_race_def(StringName raceId)
    {
        if (raceId == (StringName)"" || _progressionContentRegistry == null)
            return null;
        IReadOnlyDictionary<StringName, RaceDefinition> raceDefs =
            _progressionContentRegistry.GetRaceDefsTyped();
        return raceDefs.TryGetValue(raceId, out RaceDefinition raceDef) ? raceDef : null;
    }

    private SubraceDefinition _get_subrace_def(StringName subraceId)
    {
        if (subraceId == (StringName)"" || _progressionContentRegistry == null)
            return null;
        IReadOnlyDictionary<StringName, SubraceDefinition> subraceDefs =
            _progressionContentRegistry.GetSubraceDefsTyped();
        return subraceDefs.TryGetValue(subraceId, out SubraceDefinition subraceDef)
            ? subraceDef
            : null;
    }

    private TraitDefinition _get_trait_def(StringName traitId)
    {
        if (traitId == (StringName)"" || _progressionContentRegistry == null)
            return null;
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs =
            _progressionContentRegistry.GetTraitDefsTyped();
        return traitDefs.TryGetValue(traitId, out TraitDefinition traitDef) ? traitDef : null;
    }

    private static AgeStageRuleDefinition _get_age_stage_rule(
        AgeProfileDefinition ageProfile,
        StringName stageId
    )
    {
        if (ageProfile == null || stageId == (StringName)"")
            return null;
        foreach (AgeStageRuleDefinition rule in ageProfile.StageRules)
        {
            if (rule != null && rule.StageId == stageId)
                return rule;
        }
        return null;
    }

    private static int _resolve_default_age_for_stage(
        AgeProfileDefinition ageProfile,
        StringName stageId
    )
    {
        if (ageProfile == null || stageId == (StringName)"")
            return 0;
        if (ageProfile.DefaultAgeByStage.TryGetValue(stageId, out int defaultAge))
            return defaultAge;
        if (stageId == (StringName)"child")
            return ageProfile.ChildAge;
        if (stageId == (StringName)"teen")
            return ageProfile.TeenAge;
        if (stageId == (StringName)"young_adult")
            return ageProfile.YoungAdultAge;
        if (stageId == (StringName)"adult")
            return ageProfile.AdultAge;
        if (stageId == (StringName)"middle_age")
            return ageProfile.MiddleAge;
        if (stageId == (StringName)"old")
            return ageProfile.OldAge;
        if (stageId == (StringName)"venerable")
            return ageProfile.VenerableAge;
        return ageProfile.AdultAge;
    }

    private static string _identity_label(object definition, StringName fallbackId)
    {
        if (definition is RaceDefinition raceDef && !string.IsNullOrEmpty(raceDef.DisplayName))
            return raceDef.DisplayName;
        if (
            definition is SubraceDefinition subraceDef
            && !string.IsNullOrEmpty(subraceDef.DisplayName)
        )
            return subraceDef.DisplayName;
        if (
            definition is AgeStageRuleDefinition ageStageRule
            && !string.IsNullOrEmpty(ageStageRule.DisplayName)
        )
            return ageStageRule.DisplayName;
        return fallbackId.ToString();
    }

    private static string _attribute_display_name(StringName attributeId)
    {
        return AttributeDisplayNames.TryGetValue(attributeId, out string displayName)
            ? displayName
            : attributeId.ToString();
    }

    public void _on_confirm_pressed()
    {
        if (_rerolling || _rolled_attributes.Count == 0)
            return;
        GDictionary identityPayload = _build_selected_identity_payload();
        if (identityPayload.Count == 0)
        {
            GameLog.Warning(
                "CharacterCreationWindow: identity payload is invalid; confirmation blocked.",
                "ui.char_create.invalid_identity",
                "ui"
            );
            return;
        }

        var payload = new GDictionary
        {
            ["display_name"] = _player_name,
            ["reroll_count"] = _reroll_count,
            ["strength"] = RolledAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength)),
            ["agility"] = RolledAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)),
            ["constitution"] = RolledAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)),
            ["perception"] = RolledAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception)),
            ["intelligence"] = RolledAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence)),
            ["willpower"] = RolledAttributeValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)),
        };
        foreach (var key in identityPayload.Keys)
            payload[key] = identityPayload[key];
        HideWindow();
        EmitSignal(SignalName.character_confirmed, payload);
    }

    public void _cancel()
    {
        if (_rerolling)
        {
            _stop_requested = true;
            return;
        }
        HideWindow();
        EmitSignal(SignalName.cancelled);
    }

    private static void AppendStringNameArray(
        GStringNameArray target,
        IEnumerable<StringName> source
    )
    {
        foreach (StringName value in source)
        {
            if (value != (StringName)"")
                target.Add(value);
        }
    }

    private static bool AttributeContains(StringName attributeId)
    {
        foreach (StringName knownAttributeId in AttributeOrder)
        {
            if (knownAttributeId == attributeId)
                return true;
        }
        return false;
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(data[key]);
    }

    private static int DictInt(GDictionary data, string key, int defaultValue)
    {
        if (data == null || !data.ContainsKey(key))
            return defaultValue;
        return data[key].AsInt32();
    }

    private static int DictInt(GDictionary data, StringName key, int defaultValue)
    {
        if (data == null || !data.ContainsKey(key))
            return defaultValue;
        return data[key].AsInt32();
    }

    private static string DictString(GDictionary data, string key, string defaultValue)
    {
        if (data == null || !data.ContainsKey(key))
            return defaultValue;
        return data[key].AsString();
    }

    private sealed class AttributeModifierTotals
    {
        public int Flat;
        public int Percent;
    }
}

internal static class CharacterCreationWindowNodeExtensions
{
    public static VBoxContainer WithSeparation(this VBoxContainer container, int separation)
    {
        container.AddThemeConstantOverride("separation", separation);
        return container;
    }
}
