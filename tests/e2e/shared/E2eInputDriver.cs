using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

internal sealed class E2eInputDriver
{
    private readonly record struct HeldKey(Key Keycode, Key PhysicalKeycode, uint Unicode);

    private sealed record HeldMouseButton(Viewport Viewport, Vector2 Position);

    private readonly E2eWait _wait;
    private readonly HashSet<StringName> _heldActions = new();
    private readonly HashSet<HeldKey> _heldKeys = new();
    private readonly Dictionary<MouseButton, HeldMouseButton> _heldMouseButtons = new();

    internal E2eInputDriver(SceneTree tree, E2eWait wait)
    {
        ArgumentNullException.ThrowIfNull(tree);
        _wait = wait ?? throw new ArgumentNullException(nameof(wait));
    }

    internal async Task TapActionAsync(StringName action, int heldFrames = 1)
    {
        if (action.IsEmpty)
            throw new ArgumentException("Input action is required.", nameof(action));
        ValidateHeldFrames(heldFrames);

        PressAction(action);
        try
        {
            await _wait.FramesAsync(heldFrames);
        }
        finally
        {
            ReleaseAction(action);
        }
        await _wait.NextFrameAsync();
    }

    internal async Task TapKeyAsync(
        Key keycode,
        uint unicode = 0,
        Key physicalKeycode = Key.None,
        int heldFrames = 1
    )
    {
        if (keycode == Key.None && physicalKeycode == Key.None && unicode == 0)
            throw new ArgumentException("A keycode, physical keycode, or Unicode value is required.");
        ValidateHeldFrames(heldFrames);

        var heldKey = new HeldKey(keycode, physicalKeycode, unicode);
        PressKey(heldKey);
        try
        {
            await _wait.FramesAsync(heldFrames);
        }
        finally
        {
            ReleaseKey(heldKey);
        }
        await _wait.NextFrameAsync();
    }

    internal async Task TypeTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Rune rune in text.EnumerateRunes())
        {
            uint unicode = (uint)rune.Value;
            await TapKeyAsync((Key)unicode, unicode);
        }
    }

    internal async Task ClickAsync(
        Control control,
        MouseButton button = MouseButton.Left,
        int heldFrames = 1
    )
    {
        ArgumentNullException.ThrowIfNull(control);
        await ClickAtAsync(control, control.Size * 0.5f, button, heldFrames);
    }

    internal async Task ClickAtAsync(
        Control control,
        Vector2 localPosition,
        MouseButton button = MouseButton.Left,
        int heldFrames = 1
    )
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree())
            throw new InvalidOperationException("Cannot click a control outside the live scene tree.");
        if (!control.IsVisibleInTree())
            throw new InvalidOperationException($"Cannot click hidden control {control.GetPath()}.");
        if (control is BaseButton { Disabled: true })
            throw new InvalidOperationException($"Cannot click disabled control {control.GetPath()}.");
        if (button == MouseButton.None)
            throw new ArgumentOutOfRangeException(nameof(button), button, "Mouse button is required.");
        ValidateHeldFrames(heldFrames);

        Viewport viewport = control.GetViewport();
        if (viewport == null || !GodotObject.IsInstanceValid(viewport))
            throw new InvalidOperationException($"Control {control.GetPath()} has no live viewport.");

        Rect2 globalRect = control.GetGlobalRect();
        if (globalRect.Size.X <= 0.0f || globalRect.Size.Y <= 0.0f)
            throw new InvalidOperationException($"Cannot click zero-sized control {control.GetPath()}.");
        if (
            localPosition.X < 0.0f
            || localPosition.Y < 0.0f
            || localPosition.X > control.Size.X
            || localPosition.Y > control.Size.Y
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(localPosition),
                localPosition,
                $"Click position must be inside control {control.GetPath()} with size {control.Size}."
            );
        }

        Vector2 position = control.GetGlobalTransform() * localPosition;
        PushMouseMotion(viewport, position);
        await _wait.NextFrameAsync();

        PressMouseButton(viewport, position, button);
        try
        {
            await _wait.FramesAsync(heldFrames);
        }
        finally
        {
            ReleaseMouseButton(button);
        }
        await _wait.NextFrameAsync();
    }

    internal async Task ReleaseAllAsync()
    {
        var failures = new List<Exception>();
        bool releasedAny = _heldActions.Count > 0 || _heldKeys.Count > 0 || _heldMouseButtons.Count > 0;

        foreach (StringName action in _heldActions.ToArray())
            TryRelease(() => ReleaseAction(action), failures);
        foreach (HeldKey heldKey in _heldKeys.ToArray())
            TryRelease(() => ReleaseKey(heldKey), failures);
        foreach (MouseButton button in _heldMouseButtons.Keys.ToArray())
            TryRelease(() => ReleaseMouseButton(button), failures);

        if (releasedAny)
            await _wait.NextFrameAsync();
        if (failures.Count > 0)
            throw new AggregateException("Failed to release one or more synthetic inputs.", failures);
    }

    private void PressAction(StringName action)
    {
        if (!_heldActions.Add(action))
            throw new InvalidOperationException($"Input action {action} is already held.");

        using var inputEvent = new InputEventAction
        {
            Action = action,
            Pressed = true,
            Strength = 1.0f,
        };
        Godot.Input.ParseInputEvent(inputEvent);
    }

    private void ReleaseAction(StringName action)
    {
        if (!_heldActions.Contains(action))
            return;

        using var inputEvent = new InputEventAction
        {
            Action = action,
            Pressed = false,
            Strength = 0.0f,
        };
        Godot.Input.ParseInputEvent(inputEvent);
        _heldActions.Remove(action);
    }

    private void PressKey(HeldKey heldKey)
    {
        if (!_heldKeys.Add(heldKey))
            throw new InvalidOperationException($"Input key {heldKey.Keycode} is already held.");

        using var inputEvent = BuildKeyEvent(heldKey, pressed: true);
        Godot.Input.ParseInputEvent(inputEvent);
    }

    private void ReleaseKey(HeldKey heldKey)
    {
        if (!_heldKeys.Contains(heldKey))
            return;

        using var inputEvent = BuildKeyEvent(heldKey, pressed: false);
        Godot.Input.ParseInputEvent(inputEvent);
        _heldKeys.Remove(heldKey);
    }

    private static InputEventKey BuildKeyEvent(HeldKey heldKey, bool pressed) =>
        new()
        {
            Keycode = heldKey.Keycode,
            PhysicalKeycode = heldKey.PhysicalKeycode,
            Unicode = pressed ? heldKey.Unicode : 0,
            Pressed = pressed,
            Echo = false,
        };

    private static void PushMouseMotion(Viewport viewport, Vector2 position)
    {
        using var inputEvent = new InputEventMouseMotion
        {
            Position = position,
            GlobalPosition = position,
            ButtonMask = (MouseButtonMask)0,
        };
        viewport.PushInput(inputEvent, inLocalCoords: true);
    }

    private void PressMouseButton(Viewport viewport, Vector2 position, MouseButton button)
    {
        if (_heldMouseButtons.ContainsKey(button))
            throw new InvalidOperationException($"Mouse button {button} is already held.");

        _heldMouseButtons.Add(button, new HeldMouseButton(viewport, position));
        using var inputEvent = BuildMouseButtonEvent(position, button, pressed: true);
        viewport.PushInput(inputEvent, inLocalCoords: true);
    }

    private void ReleaseMouseButton(MouseButton button)
    {
        if (!_heldMouseButtons.TryGetValue(button, out HeldMouseButton heldButton))
            return;

        if (GodotObject.IsInstanceValid(heldButton.Viewport))
        {
            using var inputEvent = BuildMouseButtonEvent(
                heldButton.Position,
                button,
                pressed: false
            );
            heldButton.Viewport.PushInput(inputEvent, inLocalCoords: true);
        }
        _heldMouseButtons.Remove(button);
    }

    private static InputEventMouseButton BuildMouseButtonEvent(
        Vector2 position,
        MouseButton button,
        bool pressed
    ) =>
        new()
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = button,
            ButtonMask = pressed ? MaskFor(button) : (MouseButtonMask)0,
            Pressed = pressed,
            DoubleClick = false,
            Factor = 1.0f,
        };

    private static MouseButtonMask MaskFor(MouseButton button) =>
        button switch
        {
            MouseButton.Left => MouseButtonMask.Left,
            MouseButton.Right => MouseButtonMask.Right,
            MouseButton.Middle => MouseButtonMask.Middle,
            MouseButton.Xbutton1 => MouseButtonMask.MbXbutton1,
            MouseButton.Xbutton2 => MouseButtonMask.MbXbutton2,
            _ => (MouseButtonMask)0,
        };

    private static void TryRelease(Action release, List<Exception> failures)
    {
        try
        {
            release();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void ValidateHeldFrames(int heldFrames)
    {
        if (heldFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heldFrames),
                heldFrames,
                "Press and release must be separated by at least one process frame."
            );
        }
    }
}
