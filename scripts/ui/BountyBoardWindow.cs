using Godot;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>
/// Player-facing modal for the bounty registry board. Consumes the typed
/// <see cref="BountyBoardWindowData"/> built by <see cref="GameRuntimeSettlementCommandHandler"/>
/// and emits action/close signals that <see cref="WorldMapSystem"/> forwards to the runtime.
///
/// Interaction contract (V1, single accept):
/// - Clicking a list entry only focuses it and shows details; it never accepts.
/// - The explicit action button submits the focused quest.
/// - Bounty entries never carry per-item confirmation text.
///
/// Emitted payload keys: <c>submission_source="bounty_board"</c>, <c>quest_id</c>,
/// <c>provider_interaction_id</c>.
/// </summary>
[GlobalClass]
public partial class BountyBoardWindow : ModalWindowShell
{
    [Signal]
    public delegate void action_requestedEventHandler(
        string settlement_id,
        string action_id,
        GDictionary payload
    );

    [Signal]
    public delegate void closedEventHandler();

    public Label title_label;
    public Label meta_label;
    public ItemList entry_list;
    public Label danger_label;
    public Label state_label;
    public Label summary_label;
    public Label reward_label;
    public Label details_label;
    public Label feedback_label;
    public Button action_button;
    public Button return_button;
    public Button close_button;

    private BountyBoardWindowData _windowData;
    private string _selectedQuestId = "";

    public override void _Ready()
    {
        title_label = GetNodeOrNull<Label>("%TitleLabel");
        meta_label = GetNodeOrNull<Label>("%MetaLabel");
        entry_list = GetNodeOrNull<ItemList>("%EntryList");
        danger_label = GetNodeOrNull<Label>("%DangerLabel");
        state_label = GetNodeOrNull<Label>("%StateLabel");
        summary_label = GetNodeOrNull<Label>("%SummaryLabel");
        reward_label = GetNodeOrNull<Label>("%RewardLabel");
        details_label = GetNodeOrNull<Label>("%DetailsLabel");
        feedback_label = GetNodeOrNull<Label>("%FeedbackLabel");
        action_button = GetNodeOrNull<Button>("%ActionButton");
        return_button = GetNodeOrNull<Button>("%ReturnButton");
        close_button = GetNodeOrNull<Button>("%CloseButton");

        HideWindow();

        if (entry_list != null)
            entry_list.ItemSelected += _on_entry_selected;
        if (action_button != null)
            action_button.Pressed += _on_action_pressed;
        if (return_button != null)
            return_button.Pressed += _on_return_pressed;
        if (close_button != null)
            close_button.Pressed += _on_return_pressed;
        base._Ready();
    }

    protected override void _on_modal_close_requested() => _on_return_pressed();

    internal void ShowBoard(BountyBoardWindowData windowData)
    {
        if (windowData == null)
        {
            HideWindow();
            return;
        }
        _windowData = windowData;
        _selectedQuestId = windowData.SelectedQuestId ?? "";

        if (title_label != null)
            title_label.Text = string.IsNullOrEmpty(windowData.Title)
                ? "悬赏板"
                : windowData.Title;
        if (meta_label != null)
            meta_label.Text = windowData.Meta ?? "";
        if (feedback_label != null)
            feedback_label.Text = windowData.FeedbackText ?? "";

        _rebuild_entry_list();
        _render_selected_entry();
        Visible = true;
    }

    public void HideWindow()
    {
        Visible = false;
    }

    private void _rebuild_entry_list()
    {
        if (entry_list == null || _windowData == null)
            return;
        entry_list.Clear();
        int selectedIndex = -1;
        for (int index = 0; index < _windowData.Entries.Count; index++)
        {
            BountyBoardEntryData entry = _windowData.Entries[index];
            string stars = entry.DangerStars > 0
                ? new string('★', entry.DangerStars)
                : "未评级";
            entry_list.AddItem($"{entry.DisplayName}  [{stars}]  {entry.StateLabel}");
            if (entry.QuestId == _selectedQuestId)
                selectedIndex = index;
        }
        if (selectedIndex < 0 && _windowData.Entries.Count > 0)
        {
            selectedIndex = 0;
            _selectedQuestId = _windowData.Entries[0].QuestId;
        }
        if (selectedIndex >= 0)
            entry_list.Select(selectedIndex);
    }

    private BountyBoardEntryData _resolve_selected_entry()
    {
        if (_windowData == null)
            return null;
        foreach (BountyBoardEntryData entry in _windowData.Entries)
        {
            if (entry.QuestId == _selectedQuestId)
                return entry;
        }
        return _windowData.Entries.Count > 0 ? _windowData.Entries[0] : null;
    }

    private void _render_selected_entry()
    {
        BountyBoardEntryData entry = _resolve_selected_entry();
        bool hasEntry = entry != null;
        if (danger_label != null)
            danger_label.Text = hasEntry ? entry.DangerLabel : "";
        if (state_label != null)
            state_label.Text = hasEntry ? entry.StateLabel : "状态：暂无悬赏";
        if (summary_label != null)
            summary_label.Text = hasEntry ? entry.ObjectiveSummary : "当前没有可查看的悬赏。";
        if (reward_label != null)
            reward_label.Text = hasEntry ? entry.RewardLabel : "奖励：无";
        if (details_label != null)
            details_label.Text = hasEntry ? entry.DetailsText : "";
        if (action_button != null)
        {
            action_button.Text = hasEntry && !string.IsNullOrEmpty(entry.ActionLabel)
                ? entry.ActionLabel
                : "接取悬赏";
            action_button.Disabled = !hasEntry || !entry.IsEnabled;
            action_button.TooltipText = hasEntry && !entry.IsEnabled
                ? entry.DisabledReason
                : "";
        }
    }

    private void _on_entry_selected(long index)
    {
        if (_windowData == null || index < 0 || index >= _windowData.Entries.Count)
            return;
        _selectedQuestId = _windowData.Entries[(int)index].QuestId;
        _render_selected_entry();
    }

    private void _on_action_pressed()
    {
        if (_windowData == null)
            return;
        BountyBoardEntryData entry = _resolve_selected_entry();
        if (entry == null || !entry.IsEnabled)
            return;
        var payload = new GDictionary
        {
            ["submission_source"] = SettlementSubmissionSources.ToPayloadValue(
                SettlementSubmissionSource.BountyBoard
            ),
            ["quest_id"] = entry.QuestId,
            ["provider_interaction_id"] = _windowData.ProviderInteractionId,
        };
        EmitSignal(
            SignalName.action_requested,
            _windowData.SettlementId,
            _windowData.ActionId,
            payload
        );
    }

    private void _on_return_pressed()
    {
        HideWindow();
        EmitSignal(SignalName.closed);
    }
}
