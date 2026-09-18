using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Conversation only; detailed quest information belongs to QuestJournalWindow.
[GlobalClass]
public partial class NpcQuestOfferDialog : ModalWindowShell
{
    [Signal]
    public delegate void action_requestedEventHandler(string settlement_id, string action_id, GDictionary payload);
    [Signal]
    public delegate void closedEventHandler();

    public Label title_label;
    public Label dialogue_label;
    public VBoxContainer topic_choices;
    public Button accept_button;
    public Button return_button;
    public Button close_button;
    private NpcQuestOfferWindowData _data;
    private string _selectedQuestId = "";

    protected override bool AnimateEntrance => false;

    public override void _Ready()
    {
        title_label = GetNode<Label>("%TitleLabel");
        dialogue_label = GetNode<Label>("%DialogueLabel");
        topic_choices = GetNode<VBoxContainer>("%TopicChoices");
        accept_button = GetNode<Button>("%AcceptButton");
        return_button = GetNode<Button>("%ReturnButton");
        close_button = GetNode<Button>("%CloseButton");
        accept_button.Pressed += OnReply;
        return_button.Pressed += OnLeave;
        close_button.Pressed += OnLeave;
        HideDialog();
        base._Ready();
    }

    protected override void _on_modal_close_requested() => OnLeave();

    internal void ShowDialog(NpcQuestOfferWindowData data)
    {
        if (data == null || data.Entries.Count == 0) { HideDialog(); return; }
        bool keep = Visible && _data?.SettlementId == data.SettlementId && _data.ActionId == data.ActionId
            && data.Entries.Any(entry => entry.QuestId == _selectedQuestId);
        _data = data;
        if (!keep) _selectedQuestId = data.SelectedQuestId;
        ClearTopics();
        var topics = data.Entries.Where(entry => entry.IsEnabled || entry.StateId == "active").ToArray();
        if (topics.Length > 1)
        {
            foreach (var entry in topics)
            {
                var button = new Button
                {
                    Text = $"关于「{entry.DisplayName}」……", ThemeTypeVariation = "ChronicleQuiet",
                    Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 40),
                };
                button.Pressed += () => { _selectedQuestId = entry.QuestId; RefreshConversation(); };
                topic_choices.AddChild(button);
            }
        }
        RefreshConversation();
        Visible = true;
    }

    private void RefreshConversation()
    {
        var entry = _data.Entries.FirstOrDefault(e => e.QuestId == _selectedQuestId) ?? _data.Entries[0];
        _selectedQuestId = entry.QuestId;
        bool confirming = !string.IsNullOrEmpty(_data.PendingConfirmationQuestId)
            && _data.PendingConfirmationQuestId == _selectedQuestId;
        title_label.Text = string.IsNullOrEmpty(_data.NpcName) ? "交谈" : _data.NpcName;
        dialogue_label.Text = confirming ? _data.PendingConfirmationText
            : _selectedQuestId == _data.SelectedQuestId && !string.IsNullOrEmpty(_data.FeedbackText)
                ? _data.FeedbackText : entry.StateId switch
                {
                    "active" => entry.IsEnabled ? "东西带来了吗？交给我就好。" : "这件事还没有办完。准备好了再来找我吧。",
                    "claimable" => "你把事情办妥了。这是说好的谢礼，收下吧。",
                    "completed" => "多谢你的帮助。愿你一路平安。",
                    _ => !entry.IsEnabled ? "眼下没有需要你帮忙的事。路上小心。"
                        : string.IsNullOrEmpty(entry.AcceptDialogueText) ? entry.Description : entry.AcceptDialogueText,
                };
        accept_button.Text = confirming ? "我确定。" : entry.StateId switch
        {
            "active" => "东西带来了。", "claimable" => "谢谢。",
            "restartable_failed" => "让我再试一次。", _ => "交给我吧。",
        };
        accept_button.Visible = entry.IsEnabled;
        accept_button.Disabled = !entry.IsEnabled;
    }

    public void HideDialog()
    {
        Visible = false;
        _data = null;
        _selectedQuestId = "";
        if (title_label != null) title_label.Text = "";
        if (dialogue_label != null) dialogue_label.Text = "";
        if (accept_button != null) { accept_button.Disabled = true; accept_button.Visible = false; }
        ClearTopics();
    }

    private void ClearTopics()
    {
        if (topic_choices == null) return;
        foreach (Node child in topic_choices.GetChildren())
        {
            topic_choices.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnReply()
    {
        if (_data == null || accept_button.Disabled) return;
        using var payload = new GDictionary
        {
            ["submission_source"] = "npc_quest_offer", ["quest_id"] = _selectedQuestId,
            ["confirm_accept"] = _data.PendingConfirmationQuestId == _selectedQuestId,
        };
        EmitSignal(SignalName.action_requested, _data.SettlementId, _data.ActionId, payload);
    }

    private void OnLeave() => EmitSignal(SignalName.closed);
}
