using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

[GlobalClass]
public partial class DisplaySettingsWindow : Control
{
    [Signal]
    public delegate void settings_apply_requestedEventHandler(GDictionary settings);

    [Signal]
    public delegate void cancelledEventHandler();

    private ColorRect _shade;
    private OptionButton _resolutionOptionButton;
    private CheckButton _fullscreenCheckButton;
    private Label _hintLabel;
    private Button _applyButton;
    private Button _cancelButton;
    private Button _headerCloseButton;

    private readonly GDictionaryArray _resolutionOptions = new();

    public override void _Ready()
    {
        _shade = GetNode<ColorRect>("Shade");
        _resolutionOptionButton = GetNode<OptionButton>("%ResolutionOptionButton");
        _fullscreenCheckButton = GetNode<CheckButton>("%FullscreenCheckButton");
        _hintLabel = GetNode<Label>("%HintLabel");
        _applyButton = GetNode<Button>("%ApplyButton");
        _cancelButton = GetNode<Button>("%CancelButton");
        _headerCloseButton = GetNode<Button>("%HeaderCloseButton");

        hide_window();
        _shade.GuiInput += _on_shade_gui_input;
        _fullscreenCheckButton.Toggled += _on_fullscreen_toggled;
        _applyButton.Pressed += _apply;
        _cancelButton.Pressed += _cancel;
        _headerCloseButton.Pressed += _cancel;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey keyEvent)
            return;
        if (!keyEvent.Pressed || keyEvent.Echo)
            return;

        switch (keyEvent.Keycode)
        {
            case Key.Escape:
                GetViewport().SetInputAsHandled();
                _cancel();
                break;
            case Key.Enter:
            case Key.KpEnter:
                GetViewport().SetInputAsHandled();
                _apply();
                break;
        }
    }

    public void configure_options(GDictionaryArray resolution_options)
    {
        _resolutionOptions.Clear();
        if (resolution_options != null)
        {
            foreach (GDictionary entry in resolution_options)
            {
                Vector2I resolution = DictVector2I(entry, "size", Vector2I.Zero);
                if (resolution.X <= 0 || resolution.Y <= 0)
                    continue;
                _resolutionOptions.Add(
                    new GDictionary
                    {
                        ["label"] = DictString(entry, "label", $"{resolution.X} x {resolution.Y}"),
                        ["size"] = resolution,
                    }
                );
            }
        }
        _rebuild_resolution_options();
    }

    public void show_window(GDictionary current_settings)
    {
        Visible = true;
        _rebuild_resolution_options();

        Vector2I selectedResolution = DictVector2I(
            current_settings,
            "resolution",
            new Vector2I(1280, 720)
        );
        int selectedIndex = _find_resolution_index(selectedResolution);
        if (_resolutionOptionButton.GetItemCount() > 0)
            _resolutionOptionButton.Select(selectedIndex);

        _fullscreenCheckButton.ButtonPressed = DictBool(current_settings, "fullscreen", false);
        _update_hint();

        if (_resolutionOptionButton.GetItemCount() > 0)
            _resolutionOptionButton.GrabFocus();
        else
            _cancelButton.GrabFocus();
    }

    public void hide_window()
    {
        Visible = false;
        if (_fullscreenCheckButton != null)
            _fullscreenCheckButton.ButtonPressed = false;
        if (_hintLabel != null)
            _hintLabel.Text = "";
    }

    public GDictionary get_selected_settings()
    {
        return new GDictionary
        {
            ["resolution"] = _get_selected_resolution(),
            ["fullscreen"] = _fullscreenCheckButton.ButtonPressed,
        };
    }

    private void _rebuild_resolution_options()
    {
        if (_resolutionOptionButton == null)
            return;

        _resolutionOptionButton.Clear();
        foreach (GDictionary entry in _resolutionOptions)
            _resolutionOptionButton.AddItem(DictString(entry, "label", ""));

        if (_applyButton != null)
            _applyButton.Disabled = _resolutionOptions.Count == 0;
    }

    private int _find_resolution_index(Vector2I resolution)
    {
        for (int index = 0; index < _resolutionOptions.Count; index++)
        {
            if (DictVector2I(_resolutionOptions[index], "size", Vector2I.Zero) == resolution)
                return index;
        }
        return 0;
    }

    private Vector2I _get_selected_resolution()
    {
        if (_resolutionOptions.Count == 0)
            return new Vector2I(1280, 720);

        int selectedIndex = Mathf.Max(_resolutionOptionButton.GetSelectedId(), 0);
        if (selectedIndex >= _resolutionOptions.Count)
            selectedIndex = 0;
        return DictVector2I(_resolutionOptions[selectedIndex], "size", new Vector2I(1280, 720));
    }

    private void _on_fullscreen_toggled(bool _pressed)
    {
        _update_hint();
    }

    private void _update_hint()
    {
        _hintLabel.Text = _fullscreenCheckButton.ButtonPressed
            ? "全屏模式会优先使用显示器的全屏显示；退出全屏后恢复所选窗口分辨率。"
            : "窗口模式会立即切换到所选的常见分辨率。";
    }

    private void _apply()
    {
        if (!Visible || _applyButton.Disabled)
            return;
        GDictionary settings = get_selected_settings();
        hide_window();
        EmitSignal(SignalName.settings_apply_requested, settings);
    }

    private void _cancel()
    {
        if (!Visible)
            return;
        hide_window();
        EmitSignal(SignalName.cancelled);
    }

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent)
            return;
        if (!mouseEvent.Pressed)
            return;
        if (
            mouseEvent.ButtonIndex != MouseButton.Left
            && mouseEvent.ButtonIndex != MouseButton.Right
        )
            return;
        _cancel();
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsString();
    }

    private static bool DictBool(GDictionary dict, string key, bool defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsBool();
    }

    private static Vector2I DictVector2I(GDictionary dict, string key, Vector2I defaultValue)
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return GdInterop.GetVector2I(dict, key, defaultValue);
    }
}
