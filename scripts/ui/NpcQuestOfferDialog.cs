using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>
/// Player-facing modal for NPC quest offers. Consumes the typed <see cref="NpcQuestOfferWindowData"/>
/// built by <see cref="GameRuntimeSettlementCommandHandler"/> and emits accept/close signals that
/// <see cref="WorldMapSystem"/> forwards to the runtime command handler.
///
/// Accept button logic:
/// - If the selected quest is already in pending confirmation state, the payload carries
///   <c>confirm_accept=true</c>.
/// - Otherwise it carries <c>confirm_accept=false</c>; the runtime may set pending confirmation
///   and return a re-prompt when <c>accept_confirmation_text</c> is configured.
///
/// Emitted payload keys: <c>submission_source="npc_quest_offer"</c>, <c>quest_id</c>,
/// <c>confirm_accept</c>.
/// </summary>
[GlobalClass]
public partial class NpcQuestOfferDialog : Control
{
    [Signal]
    public delegate void action_requestedEventHandler(
        string settlement_id,
        string action_id,
        GDictionary payload
    );

    [Signal]
    public delegate void closedEventHandler();

    public ColorRect shade;
    public Label title_label;
    public Label dialogue_label;
    public Label summary_label;
    public Label reward_label;
    public Label feedback_label;
    public Button accept_button;
    public Button return_button;
    public Button close_button;

    private string _settlementId = "";
    private string _actionId = "";
    private string _selectedQuestId = "";
    private string _pendingConfirmationQuestId = "";

    public override void _Ready()
    {
        shade = GetNodeOrNull<ColorRect>("%Shade");
        title_label = GetNodeOrNull<Label>("%TitleLabel");
        dialogue_label = GetNodeOrNull<Label>("%DialogueLabel");
        summary_label = GetNodeOrNull<Label>("%SummaryLabel");
        reward_label = GetNodeOrNull<Label>("%RewardLabel");
        feedback_label = GetNodeOrNull<Label>("%FeedbackLabel");
        accept_button = GetNodeOrNull<Button>("%AcceptButton");
        return_button = GetNodeOrNull<Button>("%ReturnButton");
        close_button = GetNodeOrNull<Button>("%CloseButton");

        HideDialog();

        if (shade != null)
            shade.GuiInput += _on_shade_gui_input;
        if (accept_button != null)
            accept_button.Pressed += _on_accept_pressed;
        if (return_button != null)
            return_button.Pressed += _on_return_pressed;
        if (close_button != null)
            close_button.Pressed += _on_return_pressed;
    }

    internal void ShowDialog(NpcQuestOfferWindowData windowData)
    {
        if (windowData == null || windowData.Entries.Count == 0)
        {
            HideDialog();
            return;
        }

        _settlementId = windowData.SettlementId ?? "";
        _actionId = windowData.ActionId ?? "";
        _selectedQuestId = windowData.SelectedQuestId ?? "";
        _pendingConfirmationQuestId = windowData.PendingConfirmationQuestId ?? "";

        NpcQuestOfferEntryData selectedEntry = _resolve_selected_entry(windowData);
        bool isEnabled = selectedEntry?.IsEnabled ?? false;
        string npcName = windowData.NpcName ?? "";
        string dialogueText = selectedEntry?.AcceptDialogueText ?? "";
        if (string.IsNullOrEmpty(dialogueText))
            dialogueText = selectedEntry?.Description ?? "";

        if (title_label != null)
            title_label.Text = string.IsNullOrEmpty(npcName) ? "NPC 委托" : $"{npcName} 的委托";
        if (dialogue_label != null)
            dialogue_label.Text = dialogueText;
        if (summary_label != null)
            summary_label.Text = selectedEntry?.SummaryText ?? "";
        if (reward_label != null)
            reward_label.Text = selectedEntry?.CostLabel ?? "";
        if (feedback_label != null)
            feedback_label.Text = windowData.FeedbackText ?? "";

        if (accept_button != null)
        {
            bool isConfirming = _pendingConfirmationQuestId == _selectedQuestId
                && !string.IsNullOrEmpty(_pendingConfirmationQuestId);
            accept_button.Text = isConfirming ? "确认接受" : "接受委托";
            accept_button.Disabled = !isEnabled;
        }

        if (return_button != null)
            return_button.Text = "返回";
        if (close_button != null)
            close_button.Text = "关闭";

        Visible = true;
    }

    public void HideDialog()
    {
        Visible = false;
        _settlementId = "";
        _actionId = "";
        _selectedQuestId = "";
        _pendingConfirmationQuestId = "";

        if (title_label != null)
            title_label.Text = "";
        if (dialogue_label != null)
            dialogue_label.Text = "";
        if (summary_label != null)
            summary_label.Text = "";
        if (reward_label != null)
            reward_label.Text = "";
        if (feedback_label != null)
            feedback_label.Text = "";
        if (accept_button != null)
        {
            accept_button.Text = "接受委托";
            accept_button.Disabled = true;
        }
        if (return_button != null)
            return_button.Text = "返回";
        if (close_button != null)
            close_button.Text = "关闭";
    }

    private void _on_accept_pressed()
    {
        if (string.IsNullOrEmpty(_selectedQuestId))
            return;

        bool isConfirming = _pendingConfirmationQuestId == _selectedQuestId
            && !string.IsNullOrEmpty(_pendingConfirmationQuestId);

        var payload = new GDictionary
        {
            ["submission_source"] = "npc_quest_offer",
            ["quest_id"] = _selectedQuestId,
            ["confirm_accept"] = isConfirming,
        };
        EmitSignal(SignalName.action_requested, _settlementId, _actionId, payload);
    }

    private void _on_return_pressed()
    {
        EmitSignal(SignalName.closed);
    }

    private void _on_shade_gui_input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            _on_return_pressed();
        }
    }

    private NpcQuestOfferEntryData _resolve_selected_entry(NpcQuestOfferWindowData windowData)
    {
        if (windowData?.Entries == null || windowData.Entries.Count == 0)
            return null;

        NpcQuestOfferEntryData selected = windowData.Entries.FirstOrDefault(
            e => e.QuestId == _selectedQuestId
        );
        return selected ?? windowData.Entries[0];
    }
}
