using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SubmapEntryWindow : Control
{
    [Signal]
    public delegate void confirmedEventHandler();

    [Signal]
    public delegate void cancelledEventHandler();

    public ColorRect shade;
    public Label title_label;
    public Label description_label;
    public Button confirm_button;
    public Button cancel_button;
    public PanelContainer panel;
    public MarginContainer margin_container;
    public VBoxContainer layout;
    public HBoxContainer button_row;

    private bool _dismiss_on_shade = true;
    private bool _cancel_visible = true;
    private bool _accept_input_enabled;

    private Vector2 _default_panel_min_size = Vector2.Zero;
    private Vector2 _default_confirm_button_min_size = Vector2.Zero;
    private Vector2 _default_cancel_button_min_size = Vector2.Zero;
    private int _default_title_font_size;
    private int _default_description_font_size;
    private int _default_confirm_button_font_size;
    private int _default_cancel_button_font_size;
    private int _default_margin_left;
    private int _default_margin_top;
    private int _default_margin_right;
    private int _default_margin_bottom;
    private int _default_layout_separation;
    private int _default_button_row_separation;

    public override void _Ready()
    {
        shade = GetNode<ColorRect>("Shade");
        title_label = GetNode<Label>("CenterContainer/Panel/MarginContainer/Layout/TitleLabel");
        description_label = GetNode<Label>(
            "CenterContainer/Panel/MarginContainer/Layout/DescriptionLabel"
        );
        confirm_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Layout/ButtonRow/ConfirmButton"
        );
        cancel_button = GetNode<Button>(
            "CenterContainer/Panel/MarginContainer/Layout/ButtonRow/CancelButton"
        );
        panel = GetNode<PanelContainer>("CenterContainer/Panel");
        margin_container = GetNode<MarginContainer>("CenterContainer/Panel/MarginContainer");
        layout = GetNode<VBoxContainer>("CenterContainer/Panel/MarginContainer/Layout");
        button_row = GetNode<HBoxContainer>(
            "CenterContainer/Panel/MarginContainer/Layout/ButtonRow"
        );

        _cache_default_metrics();
        HideWindow();
        shade.GuiInput += _on_shade_gui_input;
        confirm_button.Pressed += _on_confirm_button_pressed;
        cancel_button.Pressed += _on_cancel_button_pressed;
        confirm_button.FocusMode = FocusModeEnum.All;
        cancel_button.FocusMode = FocusModeEnum.All;
    }

    public void ShowPrompt(GDictionary prompt)
    {
        prompt ??= new GDictionary();
        _apply_prompt_metrics(prompt);
        Visible = true;
        title_label.Text = DictString(prompt, "title", "进入子地图");
        description_label.Text = DictString(prompt, "description", "确认后将进入新的区域。");
        confirm_button.Text = DictString(prompt, "confirm_text", "确认进入");
        cancel_button.Text = DictString(prompt, "cancel_text", "取消");
        _cancel_visible = DictBool(prompt, "cancel_visible", true);
        cancel_button.Visible = _cancel_visible;
        cancel_button.Disabled = !_cancel_visible;
        _dismiss_on_shade = DictBool(prompt, "dismiss_on_shade", true);
        _accept_input_enabled = DictBool(prompt, "accept_input_enabled", false);
        if (_accept_input_enabled)
            confirm_button.GrabFocus();
    }

    public void HideWindow()
    {
        _restore_default_metrics();
        Visible = false;
        title_label.Text = "";
        description_label.Text = "";
        confirm_button.Text = "确认进入";
        cancel_button.Text = "取消";
        cancel_button.Visible = true;
        cancel_button.Disabled = false;
        _dismiss_on_shade = true;
        _cancel_visible = true;
        _accept_input_enabled = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !_accept_input_enabled || @event == null)
            return;
        if (@event.IsActionPressed("ui_accept"))
        {
            GetViewport().SetInputAsHandled();
            _on_confirm_button_pressed();
        }
    }

    private void _on_confirm_button_pressed()
    {
        HideWindow();
        EmitSignal(SignalName.confirmed);
    }

    private void _on_cancel_button_pressed()
    {
        if (!_cancel_visible)
            return;
        HideWindow();
        EmitSignal(SignalName.cancelled);
    }

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (!Visible || !_dismiss_on_shade)
            return;
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            HideWindow();
            EmitSignal(SignalName.cancelled);
        }
    }

    private void _cache_default_metrics()
    {
        _default_panel_min_size = panel.CustomMinimumSize;
        _default_confirm_button_min_size = confirm_button.CustomMinimumSize;
        _default_cancel_button_min_size = cancel_button.CustomMinimumSize;
        _default_title_font_size = _read_int_property(
            title_label,
            "theme_override_font_sizes/font_size"
        );
        _default_description_font_size = _read_int_property(
            description_label,
            "theme_override_font_sizes/font_size"
        );
        _default_confirm_button_font_size = _read_int_property(
            confirm_button,
            "theme_override_font_sizes/font_size"
        );
        _default_cancel_button_font_size = _read_int_property(
            cancel_button,
            "theme_override_font_sizes/font_size"
        );
        _default_margin_left = _read_int_property(
            margin_container,
            "theme_override_constants/margin_left"
        );
        _default_margin_top = _read_int_property(
            margin_container,
            "theme_override_constants/margin_top"
        );
        _default_margin_right = _read_int_property(
            margin_container,
            "theme_override_constants/margin_right"
        );
        _default_margin_bottom = _read_int_property(
            margin_container,
            "theme_override_constants/margin_bottom"
        );
        _default_layout_separation = _read_int_property(
            layout,
            "theme_override_constants/separation"
        );
        _default_button_row_separation = _read_int_property(
            button_row,
            "theme_override_constants/separation"
        );
    }

    private void _apply_prompt_metrics(GDictionary prompt)
    {
        panel.CustomMinimumSize = DictVector2(prompt, "panel_min_size", _default_panel_min_size);
        confirm_button.CustomMinimumSize = DictVector2(
            prompt,
            "confirm_button_min_size",
            _default_confirm_button_min_size
        );
        cancel_button.CustomMinimumSize = DictVector2(
            prompt,
            "cancel_button_min_size",
            _default_cancel_button_min_size
        );
        _set_font_override(
            title_label,
            DictInt(prompt, "title_font_size", _default_title_font_size)
        );
        _set_font_override(
            description_label,
            DictInt(prompt, "description_font_size", _default_description_font_size)
        );
        _set_font_override(
            confirm_button,
            DictInt(prompt, "confirm_button_font_size", _default_confirm_button_font_size)
        );
        _set_font_override(
            cancel_button,
            DictInt(prompt, "cancel_button_font_size", _default_cancel_button_font_size)
        );
        margin_container.AddThemeConstantOverride(
            "margin_left",
            DictInt(prompt, "margin_left", _default_margin_left)
        );
        margin_container.AddThemeConstantOverride(
            "margin_top",
            DictInt(prompt, "margin_top", _default_margin_top)
        );
        margin_container.AddThemeConstantOverride(
            "margin_right",
            DictInt(prompt, "margin_right", _default_margin_right)
        );
        margin_container.AddThemeConstantOverride(
            "margin_bottom",
            DictInt(prompt, "margin_bottom", _default_margin_bottom)
        );
        layout.AddThemeConstantOverride(
            "separation",
            DictInt(prompt, "layout_separation", _default_layout_separation)
        );
        button_row.AddThemeConstantOverride(
            "separation",
            DictInt(prompt, "button_row_separation", _default_button_row_separation)
        );
    }

    private void _restore_default_metrics()
    {
        panel.CustomMinimumSize = _default_panel_min_size;
        confirm_button.CustomMinimumSize = _default_confirm_button_min_size;
        cancel_button.CustomMinimumSize = _default_cancel_button_min_size;
        _set_font_override(title_label, _default_title_font_size);
        _set_font_override(description_label, _default_description_font_size);
        _set_font_override(confirm_button, _default_confirm_button_font_size);
        _set_font_override(cancel_button, _default_cancel_button_font_size);
        margin_container.AddThemeConstantOverride("margin_left", _default_margin_left);
        margin_container.AddThemeConstantOverride("margin_top", _default_margin_top);
        margin_container.AddThemeConstantOverride("margin_right", _default_margin_right);
        margin_container.AddThemeConstantOverride("margin_bottom", _default_margin_bottom);
        layout.AddThemeConstantOverride("separation", _default_layout_separation);
        button_row.AddThemeConstantOverride("separation", _default_button_row_separation);
    }

    private static void _set_font_override(Control control, int fontSize)
    {
        if (fontSize > 0)
        {
            control.AddThemeFontSizeOverride("font_size", fontSize);
            return;
        }
        control.RemoveThemeFontSizeOverride("font_size");
    }

    private static int _read_int_property(GodotObject target, string propertyName, int fallback = 0)
    {
        if (target == null)
            return fallback;
        var value = target.Get(propertyName);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (!TryRead(dict, key, out Variant value))
            return defaultValue;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => defaultValue,
        };
    }

    private static bool DictBool(GDictionary dict, string key, bool defaultValue)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Bool)
            return defaultValue;
        return value.AsBool();
    }

    private static int DictInt(GDictionary dict, string key, int defaultValue)
    {
        if (!TryRead(dict, key, out Variant value) || value.VariantType != Variant.Type.Int)
            return defaultValue;
        return value.AsInt32();
    }

    private static Vector2 DictVector2(GDictionary dict, string key, Vector2 defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        var value = dict[key];
        return value.VariantType == Variant.Type.Vector2 ? value.AsVector2() : defaultValue;
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        value = default;
        if (dict == null || !dict.ContainsKey(key))
            return false;
        value = dict[key];
        return value.VariantType != Variant.Type.Nil;
    }
}
