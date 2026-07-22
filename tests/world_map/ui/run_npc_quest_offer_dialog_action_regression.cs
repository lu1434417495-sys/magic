using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_npc_quest_offer_dialog_action_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene DialogScene = GD.Load<PackedScene>(
        "res://scenes/ui/npc_quest_offer_dialog.tscn"
    );
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunAsync);
    }

    private async void RunAsync()
    {
        NpcQuestOfferDialog dialog = DialogScene.Instantiate<NpcQuestOfferDialog>();
        Root.AddChild(dialog);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        try
        {
            TestActionLabelsFollowRuntimeState(dialog);
            await TestActionSignalKeepsRuntimePayload(dialog);
        }
        finally
        {
            dialog.QueueFree();
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        }

        RequestTestExit(_test.Finish("NPC quest offer dialog action regression"));
    }

    private void TestActionLabelsFollowRuntimeState(NpcQuestOfferDialog dialog)
    {
        dialog.ShowDialog(BuildWindowData("active", "提交物品", true));
        _test.True(dialog.Visible, "NPC 委托面板应显示运行时提供的 active 状态。");
        _test.Eq(dialog.accept_button.Text, "提交物品", "active 采集任务应显示提交物品。");
        _test.False(dialog.accept_button.Disabled, "可提交任务的动作按钮应启用。");

        dialog.ShowDialog(BuildWindowData("claimable", "领取奖励", true));
        _test.Eq(dialog.accept_button.Text, "领取奖励", "claimable 任务应显示领取奖励。");
        _test.False(dialog.accept_button.Disabled, "待领奖任务的动作按钮应启用。");

        dialog.ShowDialog(BuildWindowData("completed", "已完成", false));
        _test.Eq(dialog.accept_button.Text, "已完成", "completed 任务应显示已完成。");
        _test.True(dialog.accept_button.Disabled, "已完成任务不应允许重复提交。");
    }

    private async Task TestActionSignalKeepsRuntimePayload(NpcQuestOfferDialog dialog)
    {
        string capturedSettlementId = "";
        string capturedActionId = "";
        GDictionary capturedPayload = null;
        dialog.action_requested += (settlementId, actionId, payload) =>
        {
            capturedSettlementId = settlementId;
            capturedActionId = actionId;
            capturedPayload = payload;
        };

        dialog.ShowDialog(BuildWindowData("claimable", "领取奖励", true));
        dialog.accept_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.Eq(capturedSettlementId, "spring_village_01", "动作信号应保留 settlement_id。");
        _test.Eq(capturedActionId, "npc_village_healer", "动作信号应保留 NPC action_id。");
        _test.True(capturedPayload != null, "动作按钮应发射 payload。");
        if (capturedPayload == null)
            return;
        _test.Eq(capturedPayload["submission_source"].AsString(), "npc_quest_offer", "动作 payload 应保留正式 submission_source。");
        _test.Eq(capturedPayload["quest_id"].AsString(), "tutorial_gather_herbs", "动作 payload 应保留 quest_id。");
        _test.False(capturedPayload["confirm_accept"].AsBool(), "非接取确认动作不应伪造 confirm_accept。");
    }

    private static NpcQuestOfferWindowData BuildWindowData(
        string stateId,
        string actionLabel,
        bool isEnabled
    ) =>
        new()
        {
            SettlementId = "spring_village_01",
            ActionId = "npc_village_healer",
            NpcInteractionId = "npc_village_healer",
            NpcName = "村医",
            SelectedQuestId = "tutorial_gather_herbs",
            Entries = new System.Collections.Generic.List<NpcQuestOfferEntryData>
            {
                new()
                {
                    QuestId = "tutorial_gather_herbs",
                    DisplayName = "采集药材",
                    Description = "提交三份药草。",
                    SummaryText = "目标：提交物资 治疗药草 0/3",
                    CostLabel = "奖励：30 金",
                    StateId = stateId,
                    ActionLabel = actionLabel,
                    IsEnabled = isEnabled,
                },
            },
        };
}
