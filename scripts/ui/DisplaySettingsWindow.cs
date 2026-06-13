using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class DisplaySettingsWindow : Control
{
    [Signal]
    public delegate void settings_apply_requestedEventHandler(Vector2I resolution, bool fullscreen);

    [Signal]
    public delegate void cancelledEventHandler();

    private ColorRect _shade;
    private OptionButton _resolutionOptionButton;
    private CheckButton _fullscreenCheckButton;
    private Label _hintLabel;
    private Button _applyButton;
    private Button _cancelButton;
    private Button _headerCloseButton;

    private readonly List<DisplaySettingsService.ResolutionOption> _resolutionOptions = new();

    public override void _Ready()
    {
        _shade = GetNode<ColorRect>("Shade");
        _resolutionOptionButton = GetNode<OptionButton>("%ResolutionOptionButton");
        _fullscreenCheckButton = GetNode<CheckButton>("%FullscreenCheckButton");
        _hintLabel = GetNode<Label>("%HintLabel");
        _applyButton = GetNode<Button>("%ApplyButton");
        _cancelButton = GetNode<Button>("%CancelButton");
        _headerCloseButton = GetNode<Button>("%HeaderCloseButton");

        HideWindow();
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

    public void ConfigureOptions(IReadOnlyList<DisplaySettingsService.ResolutionOption> resolution_options)
    {
        _resolutionOptions.Clear();
        if (resolution_options != null)
        {
            foreach (DisplaySettingsService.ResolutionOption entry in resolution_options)
            {
                Vector2I resolution = entry.Size;
                if (resolution.X <= 0 || resolution.Y <= 0)
                    continue;
                string label = string.IsNullOrEmpty(entry.Label)
                    ? $"{resolution.X} x {resolution.Y}"
                    : entry.Label;
                _resolutionOptions.Add(new DisplaySettingsService.ResolutionOption(label, resolution));
            }
        }
        _rebuild_resolution_options();
    }

    public void ShowWindow(DisplaySettingsService.DisplaySettings current_settings)
    {
        Visible = true;
        _rebuild_resolution_options();

        Vector2I selectedResolution = current_settings.Resolution;
        int selectedIndex = _find_resolution_index(selectedResolution);
        if (_resolutionOptionButton.GetItemCount() > 0)
            _resolutionOptionButton.Select(selectedIndex);

        _fullscreenCheckButton.ButtonPressed = current_settings.Fullscreen;
        _update_hint();

        if (_resolutionOptionButton.GetItemCount() > 0)
            _resolutionOptionButton.GrabFocus();
        else
            _cancelButton.GrabFocus();
    }

    public void HideWindow()
    {
        Visible = false;
        if (_fullscreenCheckButton != null)
            _fullscreenCheckButton.ButtonPressed = false;
        if (_hintLabel != null)
            _hintLabel.Text = "";
    }

    public DisplaySettingsService.DisplaySettings GetSelectedSettings()
    {
        return new DisplaySettingsService.DisplaySettings(
            _get_selected_resolution(),
            _fullscreenCheckButton.ButtonPressed
        );
    }

    private void _rebuild_resolution_options()
    {
        if (_resolutionOptionButton == null)
            return;

        _resolutionOptionButton.Clear();
        foreach (DisplaySettingsService.ResolutionOption entry in _resolutionOptions)
            _resolutionOptionButton.AddItem(entry.Label);

        if (_applyButton != null)
            _applyButton.Disabled = _resolutionOptions.Count == 0;
    }

    private int _find_resolution_index(Vector2I resolution)
    {
        for (int index = 0; index < _resolutionOptions.Count; index++)
        {
            if (_resolutionOptions[index].Size == resolution)
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
        return _resolutionOptions[selectedIndex].Size;
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
        DisplaySettingsService.DisplaySettings settings = GetSelectedSettings();
        HideWindow();
        EmitSignal(SignalName.settings_apply_requested, settings.Resolution, settings.Fullscreen);
    }

    private void _cancel()
    {
        if (!Visible)
            return;
        HideWindow();
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

}
