using Godot;

// 弹窗模态外壳基类：统一"遮罩点击关闭 + Esc 关闭"两条通道。
//
// 约定：
// - 场景根下有名为 "Shade" 的 ColorRect 遮罩（全部弹窗场景已是此结构）。
// - 子类在自己的 _Ready 里完成节点接线后调用 base._Ready()。
// - 子类实现 _on_modal_close_requested() 决定"关闭"的语义（隐藏 + 发哪个信号）；
//   基类只负责判定"什么时候算请求关闭"。
// - 不允许随手关掉的窗（如必须确认的奖励结算）覆写 DismissOnShade /
//   DismissOnEscape 返回 false；遮罩点击仍会被吞掉，不会漏到底层世界。
[GlobalClass]
public partial class ModalWindowShell : Control
{
    private ColorRect _modal_shade;

    protected virtual bool DismissOnShade => true;

    protected virtual bool DismissOnEscape => true;

    public override void _Ready()
    {
        _modal_shade = GetNodeOrNull<ColorRect>("Shade") ?? GetNodeOrNull<ColorRect>("%Shade");
        if (_modal_shade != null)
            _modal_shade.GuiInput += _on_modal_shade_gui_input;
    }

    public override void _ExitTree()
    {
        if (_modal_shade != null)
            _modal_shade.GuiInput -= _on_modal_shade_gui_input;
        _modal_shade = null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
            return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
            return;
        if (keyEvent.Keycode != Key.Escape)
            return;
        GetViewport().SetInputAsHandled();
        if (DismissOnEscape)
            _on_modal_close_requested();
    }

    protected virtual void _on_modal_close_requested() { }

    private void _on_modal_shade_gui_input(InputEvent @event)
    {
        if (!Visible)
            return;
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
            return;
        if (
            mouseEvent.ButtonIndex != MouseButton.Left
            && mouseEvent.ButtonIndex != MouseButton.Right
        )
            return;
        AcceptEvent();
        if (DismissOnShade)
            _on_modal_close_requested();
    }
}
