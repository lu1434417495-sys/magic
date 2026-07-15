using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SelectableListWindow : ModalWindowShell
{
    private ItemList _itemList;
    private Label _detailLabel;
    private Label _emptyLabel;
    private Button _confirmButton;
    private Button _cancelButton;
    private Button _footerCancelButton;
    private readonly List<Dictionary<string, object>> _items = new();

    public override void _Ready()
    {
        _itemList = GetNode<ItemList>("%List");
        _detailLabel = GetNode<Label>("%DetailLabel");
        _emptyLabel = GetNode<Label>("%EmptyLabel");
        _confirmButton = GetNode<Button>("%ConfirmButton");
        _cancelButton = GetNode<Button>("%CancelButton");
        _footerCancelButton = GetNode<Button>("%FooterCancelButton");

        UiListTheme.Apply(_itemList);
        HideWindow();
        _itemList.ItemSelected += OnItemSelected;
        _itemList.ItemActivated += OnItemActivated;
        _confirmButton.Pressed += EmitSelected;
        _cancelButton.Pressed += EmitCancel;
        _footerCancelButton.Pressed += EmitCancel;
        base._Ready();
    }

    protected override void _on_modal_close_requested() => EmitCancel();

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);
        if (!Visible || @event is not InputEventKey keyEvent)
            return;
        if (!keyEvent.Pressed || keyEvent.Echo)
            return;
        if (keyEvent.Keycode is Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();
            EmitSelected();
        }
    }

    public void ShowWindow(GArray items)
    {
        ShowWindow(items, "");
    }

    public void ShowWindow(GArray items, StringName default_id)
    {
        var plainItems = new List<Dictionary<string, object>>();
        if (items != null)
        {
            foreach (Variant rawItem in items)
            {
                if (rawItem.VariantType != Variant.Type.Dictionary)
                    continue;
                using GDictionary item = rawItem.AsGodotDictionary();
                plainItems.Add(
                    RuntimePlainPayload.NormalizeDictionary(
                        item,
                        "SelectableListWindow.item"
                    )
                );
            }
        }
        ShowWindow(plainItems, default_id);
    }

    internal void ShowWindow(IReadOnlyList<Dictionary<string, object>> items)
    {
        ShowWindow(items, "");
    }

    internal void ShowWindow(
        IReadOnlyList<Dictionary<string, object>> items,
        StringName default_id
    )
    {
        Visible = true;
        _items.Clear();
        _itemList.Clear();

        if (items != null)
        {
            foreach (Dictionary<string, object> item in items)
            {
                if (item == null)
                    continue;
                Dictionary<string, object> snapshot = RuntimePlainPayload.CloneDictionary(item);
                _items.Add(snapshot);
                _itemList.AddItem(_format_item_label(snapshot));
            }
        }

        bool hasItems = _items.Count > 0;
        _confirmButton.Disabled = !hasItems;
        if (_emptyLabel != null)
            _emptyLabel.Visible = !hasItems;

        if (!hasItems)
        {
            _detailLabel.Text = _format_empty_detail();
            _cancelButton.GrabFocus();
            return;
        }

        int selectedIndex = 0;
        if (default_id != "")
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_get_item_id(_items[i]) == default_id)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        _itemList.Select(selectedIndex);
        _itemList.EnsureCurrentIsVisible();
        RefreshDetail(selectedIndex);
        _itemList.GrabFocus();
    }

    public void HideWindow()
    {
        Visible = false;
        _items.Clear();
        if (_itemList != null)
            _itemList.Clear();
        if (_detailLabel != null)
            _detailLabel.Text = "";
        if (_emptyLabel != null)
            _emptyLabel.Visible = false;
        if (_confirmButton != null)
            _confirmButton.Disabled = false;
    }

    public StringName GetSelectedItemId()
    {
        int[] selected = _itemList.GetSelectedItems();
        if (selected.Length == 0)
            return "";
        int index = selected[0];
        if (index < 0 || index >= _items.Count)
            return "";
        return _get_item_id(_items[index]);
    }

    protected virtual string _format_item_label(IReadOnlyDictionary<string, object> item) => "";

    protected virtual string _format_detail_text(IReadOnlyDictionary<string, object> item) => "";

    protected virtual string _format_empty_detail() => "当前没有可用的条目。";

    protected virtual StringName _get_item_id(IReadOnlyDictionary<string, object> item) => "";

    protected virtual void _emit_confirmed_for_id(StringName item_id) { }

    protected virtual void _emit_cancelled() { }

    private void OnItemSelected(long index)
    {
        RefreshDetail((int)index);
    }

    private void OnItemActivated(long index)
    {
        _itemList.Select((int)index);
        RefreshDetail((int)index);
        EmitSelected();
    }

    private void RefreshDetail(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            _detailLabel.Text = "";
            return;
        }
        _detailLabel.Text = _format_detail_text(_items[index]);
    }

    private void EmitSelected()
    {
        if (!Visible)
            return;
        StringName itemId = GetSelectedItemId();
        if (itemId == "")
            return;
        HideWindow();
        _emit_confirmed_for_id(itemId);
    }

    private void EmitCancel()
    {
        if (!Visible)
            return;
        HideWindow();
        _emit_cancelled();
    }

}
