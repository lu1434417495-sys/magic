using Godot;

public partial class BattleSkillSlotButton : Button
{
    private BattleHudSkillSlotSnapshot _snapshot;
    private Timer _showTimer;
    private Timer _hideTimer;
    private CanvasLayer _tooltipLayer;
    private BattleSkillTooltipPanel _tooltip;
    private Vector2 _pointerPosition;
    private Viewport _viewport;

    internal void SetSnapshot(BattleHudSkillSlotSnapshot snapshot) => _snapshot = snapshot;

    public override void _Ready()
    {
        _showTimer = new Timer { OneShot = true, WaitTime = 0.3 };
        _hideTimer = new Timer { OneShot = true, WaitTime = 0.18 };
        AddChild(_showTimer);
        AddChild(_hideTimer);
        _showTimer.Timeout += ShowTooltip;
        _hideTimer.Timeout += CloseIfPointerOutside;
        MouseEntered += BeginHover;
        MouseExited += EndHover;
        Pressed += CloseTooltip;
        VisibilityChanged += CloseWhenHidden;
        _viewport = GetViewport();
        _viewport.SizeChanged += CloseTooltip;
        SetProcessInput(false);
    }

    public override void _ExitTree()
    {
        if (_viewport != null) _viewport.SizeChanged -= CloseTooltip;
        _viewport = null;
    }

    private void BeginHover()
    {
        _pointerPosition = GetGlobalRect().GetCenter();
        _hideTimer.Stop();
        SetProcessInput(true);
        if (_tooltip == null) _showTimer.Start();
    }

    private void EndHover()
    {
        // MouseExited can fire after child timers have left the tree during a grid rebuild.
        if (_hideTimer?.IsInsideTree() != true) return;
        _showTimer.Stop();
        _hideTimer.Start();
    }

    private void ShowTooltip()
    {
        if (_snapshot == null || _snapshot.IsEmpty || !IsVisibleInTree() || _tooltip != null) return;
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 size = BattleSkillTooltipPanel.ResolveFrameSize(viewportSize);
        Texture2D icon = string.IsNullOrEmpty(_snapshot.IconKey) ? null
            : EngineAssetAccess.ResolveContentAssetBorrowed<Texture2D>(this, _snapshot.IconKey);
        _tooltip = BattleSkillTooltipPanel.Create(_snapshot, icon, size);
        // Below battle modals; this layer is owned by this slot and leaves with it.
        _tooltipLayer = new CanvasLayer { Name = "SkillTooltipLayer", Layer = 19 };
        AddChild(_tooltipLayer);
        _tooltipLayer.AddChild(_tooltip);
        Rect2 source = GetGlobalRect();
        float x = source.End.X + 12;
        if (x + size.X > viewportSize.X - 16) x = source.Position.X - size.X - 12;
        _tooltip.Position = new Vector2(Mathf.Clamp(x, 16, viewportSize.X - size.X - 16),
            Mathf.Clamp(source.Position.Y - size.Y, 16, viewportSize.Y - size.Y - 16));
        _tooltip.MouseEntered += () => _hideTimer.Stop();
        _tooltip.MouseExited += EndHover;
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            _pointerPosition = motion.Position;
            if (PointerInside()) _hideTimer.Stop();
            else if (_hideTimer.IsStopped()) _hideTimer.Start();
        }
        else if (input is InputEventMouseButton { Pressed: true } button)
        {
            _pointerPosition = button.Position;
            if (!PointerInside()) CloseTooltip();
        }
        else if (input is InputEventKey { Pressed: true, Keycode: Key.Escape } && _tooltip != null)
        {
            CloseTooltip();
            GetViewport().SetInputAsHandled();
        }
    }

    private bool PointerInside() => GetGlobalRect().HasPoint(_pointerPosition)
        || (_tooltip != null && _tooltip.GetGlobalRect().HasPoint(_pointerPosition));

    private void CloseIfPointerOutside()
    {
        if (!PointerInside()) CloseTooltip();
    }

    private void CloseWhenHidden()
    {
        if (!IsVisibleInTree()) CloseTooltip();
    }

    private void CloseTooltip()
    {
        _showTimer?.Stop();
        _hideTimer?.Stop();
        _tooltipLayer?.QueueFree();
        _tooltipLayer = null;
        _tooltip = null;
        SetProcessInput(false);
    }
}
