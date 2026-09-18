using System.Linq;
using Godot;

public partial class run_npc_quest_offer_dialog_action_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => RunAfterProcessStartup(RunAsync);
    private async void RunAsync()
    {
        NpcQuestOfferDialog dialog = null;
        try
        {
            dialog = GD.Load<PackedScene>("res://scenes/ui/npc_quest_offer_dialog.tscn").Instantiate<NpcQuestOfferDialog>();
            Root.AddChild(dialog);
            await ToSignal(this, SignalName.ProcessFrame);
            var data = new NpcQuestOfferWindowData
            {
                SettlementId = "village", ActionId = "chief", NpcName = "村长", SelectedQuestId = "first",
            };
            data.Entries.Add(new NpcQuestOfferEntryData
            {
                QuestId = "first", DisplayName = "初阵", StateId = "available", IsEnabled = true,
                AcceptDialogueText = "村外有危险，你愿意帮忙吗？", SummaryText = "目标 0/3", CostLabel = "奖励 50 金",
            });
            data.Entries.Add(new NpcQuestOfferEntryData
            {
                QuestId = "locked", DisplayName = "后续任务", StateId = "available", IsEnabled = false,
                DisabledReason = "需要完成初阵", SummaryText = "锁定目标", CostLabel = "锁定奖励",
            });
            int actions = 0;
            string quest = "", settlement = "", action = "", source = "";
            bool confirmed = false;
            dialog.action_requested += (s, a, payload) =>
            {
                actions++; settlement = s; action = a;
                quest = payload["quest_id"].AsString(); source = payload["submission_source"].AsString();
                confirmed = payload["confirm_accept"].AsBool();
            };
            dialog.ShowDialog(data);
            _test.Eq(dialog.title_label.Text, "村长", "对话标题应显示说话者。");
            _test.Eq(dialog.dialogue_label.Text, data.Entries[0].AcceptDialogueText, "NPC 应展示正式对话内容。");
            _test.Eq(dialog.topic_choices.GetChildCount(), 0, "只有一个可谈话题时不展示任务列表，锁定后续任务不成为话题。");
            _test.False(Labels(dialog).Any(t => t.Contains("0/3") || t.Contains("50 金")), "数字目标与奖励应只在日志展示。");
            dialog.accept_button.EmitSignal(BaseButton.SignalName.Pressed);
            _test.Eq(quest, "first", "对话回应必须接取当前任务。");
            _test.Eq(settlement, "village", "保留据点 ID。");
            _test.Eq(action, "chief", "保留 NPC action ID。");
            _test.Eq(source, "npc_quest_offer", "保留正式提交渠道。");
            _test.False(confirmed, "普通回应不能跳过确认。");

            data.Entries.Add(new NpcQuestOfferEntryData
            {
                QuestId = "second", DisplayName = "采药", StateId = "available", IsEnabled = true,
                AcceptDialogueText = "能帮我采药吗？",
            });
            dialog.ShowDialog(data);
            _test.Eq(dialog.topic_choices.GetChildCount(), 2, "多个可谈话题应保留对话选择。");
            dialog.topic_choices.GetChild<Button>(1).EmitSignal(BaseButton.SignalName.Pressed);
            _test.Eq(actions, 1, "选择话题本身不接取任务。");
            _test.Eq(dialog.dialogue_label.Text, "能帮我采药吗？", "话题选择更新对白。");
            dialog.ShowDialog(data);
            dialog.accept_button.EmitSignal(BaseButton.SignalName.Pressed);
            _test.Eq(quest, "second", "刷新后回应仍对应所谈任务。");
            data.SelectedQuestId = "second";
            data.PendingConfirmationQuestId = "second";
            data.PendingConfirmationText = "你确定要去吗？";
            dialog.ShowDialog(data);
            _test.Eq(dialog.dialogue_label.Text, "你确定要去吗？", "确认作为对话显示。");
            dialog.accept_button.EmitSignal(BaseButton.SignalName.Pressed);
            _test.True(confirmed, "确认回应携带 confirm_accept。");
            dialog.topic_choices.GetChild<Button>(0).EmitSignal(BaseButton.SignalName.Pressed);
            dialog.accept_button.EmitSignal(BaseButton.SignalName.Pressed);
            _test.False(confirmed, "另一个话题不能继承确认状态。");

            data.PendingConfirmationQuestId = "";
            foreach (var stage in new[] { "active", "claimable", "completed" })
            {
                dialog.HideDialog();
                data.SelectedQuestId = "first";
                data.Entries[0].StateId = stage;
                data.Entries[0].IsEnabled = stage != "completed";
                dialog.ShowDialog(data);
                _test.Eq(dialog.accept_button.Visible, stage != "completed", "可提交物品/交付时提供回应，完成后只告别。");
                int before = actions;
                dialog.accept_button.EmitSignal(BaseButton.SignalName.Pressed);
                _test.Eq(actions, before + (stage == "completed" ? 0 : 1), "回应可用性保持运行时语义。");
            }
            dialog.HideDialog();
            data.SelectedQuestId = "locked";
            dialog.ShowDialog(data);
            _test.False(dialog.accept_button.Visible, "锁定任务不能通过对话接受。");
        }
        catch (System.Exception e) { _test.Fail(e.ToString()); }
        finally
        {
            if (dialog != null) { dialog.QueueFree(); await ToSignal(this, SignalName.ProcessFrame); }
            RequestTestExit(_test.Finish("NPC conversation action regression"));
        }
    }
    private static System.Collections.Generic.IEnumerable<string> Labels(Node node)
    {
        if (node is Label label && label.IsVisibleInTree()) yield return label.Text;
        foreach (Node child in node.GetChildren()) foreach (string text in Labels(child)) yield return text;
    }
}
