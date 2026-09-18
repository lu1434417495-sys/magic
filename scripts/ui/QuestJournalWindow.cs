using System;
using System.Linq;
using Godot;

[GlobalClass]
public partial class QuestJournalWindow : ModalWindowShell
{
    [Signal] public delegate void closedEventHandler();
    public ItemList quest_list;
    public TabBar filters;
    public Label title_label;
    public Label description_label;
    public Label objectives_label;
    public Label rewards_label;
    public Label next_step_label;
    private Label _state;
    private Label _provider;
    private Label _count;
    private VBoxContainer _details;
    private Label _empty;
    private QuestJournalWindowData _data = QuestJournalWindowData.Empty;
    private QuestJournalEntryData[] _visibleEntries = Array.Empty<QuestJournalEntryData>();
    private StringName _selectedId = "";

    protected override bool DismissOnShade => false;

    public override void _Ready()
    {
        quest_list = GetNode<ItemList>("%QuestList");
        filters = GetNode<TabBar>("%Filters");
        title_label = GetNode<Label>("%QuestTitle");
        description_label = GetNode<Label>("%DescriptionLabel");
        objectives_label = GetNode<Label>("%ObjectivesLabel");
        rewards_label = GetNode<Label>("%RewardsLabel");
        next_step_label = GetNode<Label>("%NextStepLabel");
        _state = GetNode<Label>("%StateLabel");
        _provider = GetNode<Label>("%ProviderLabel");
        _count = GetNode<Label>("%CountLabel");
        _details = GetNode<VBoxContainer>("%Details");
        _empty = GetNode<Label>("%EmptyLabel");
        foreach (string label in new[] { "全部", "进行中", "已完成", "已失败" }) filters.AddTab(label);
        filters.TabChanged += _ => RefreshList();
        quest_list.ItemSelected += index => SelectEntry((int)index);
        GetNode<Button>("%CloseButton").Pressed += RequestClose;
        HideWindow();
        base._Ready();
    }

    protected override void _on_modal_close_requested() => RequestClose();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.J })
        {
            GetViewport().SetInputAsHandled();
            RequestClose();
            return;
        }
        base._UnhandledInput(@event);
    }

    internal void ShowWindow(QuestJournalWindowData data)
    {
        bool opening = !Visible;
        _data = data ?? QuestJournalWindowData.Empty;
        if (opening) { _selectedId = ""; filters.CurrentTab = 0; }
        RefreshList();
        Visible = true;
        if (opening) quest_list.GrabFocus();
    }

    public void HideWindow()
    {
        Visible = false;
        _data = QuestJournalWindowData.Empty;
        _visibleEntries = Array.Empty<QuestJournalEntryData>();
        _selectedId = "";
        quest_list?.Clear();
    }

    private void RefreshList()
    {
        _visibleEntries = _data.Entries.Where(entry => filters.CurrentTab switch
        {
            1 => entry.Stage is QuestJournalStage.Active or QuestJournalStage.Claimable,
            2 => entry.Stage == QuestJournalStage.Completed,
            3 => entry.Stage == QuestJournalStage.Failed,
            _ => true,
        }).ToArray();
        quest_list.Clear();
        foreach (var entry in _visibleEntries) quest_list.AddItem($"{entry.Title}　·　{entry.StateLabel}");
        _count.Text = $"{_data.Entries.Count} 份旅途记录";
        _details.Visible = _visibleEntries.Length > 0;
        _empty.Visible = _visibleEntries.Length == 0;
        _empty.Text = _data.Entries.Count == 0
            ? "旅途尚未留下委托。\n与据点中的人物交谈，接下你的第一份任务。"
            : "这里还没有任务记录。";
        if (_visibleEntries.Length == 0) return;
        int index = Array.FindIndex(_visibleEntries, entry => entry.QuestId == _selectedId);
        index = Math.Max(index, 0);
        quest_list.Select(index);
        SelectEntry(index);
    }

    private void SelectEntry(int index)
    {
        if (index < 0 || index >= _visibleEntries.Length) return;
        var entry = _visibleEntries[index];
        _selectedId = entry.QuestId;
        title_label.Text = entry.Title;
        _state.Text = entry.StateLabel;
        _provider.Text = $"委托来源　{entry.Provider}";
        description_label.Text = entry.Description;
        objectives_label.Text = string.Join("\n\n", entry.Objectives.Select(o =>
            $"{(o.Current >= o.Target ? "✓" : "○")}  {o.Text}　{o.Current} / {o.Target}"));
        rewards_label.Text = string.Join("\n", entry.Rewards);
        next_step_label.Text = entry.NextStep;
        GetNode<ScrollContainer>("%DetailScroll").ScrollVertical = 0;
    }

    private void RequestClose() => EmitSignal(SignalName.closed);
}
