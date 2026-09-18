using System;
using System.Linq;
using Godot;

public partial class run_quest_journal_window_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => RunAfterProcessStartup(RunAsync);
    private async void RunAsync()
    {
        QuestJournalWindow window = null;
        try
        {
            GameContentCatalog catalog = Root.GetNode<GameSession>("GameSession").GetContentCatalogTyped();
            var generation = catalog.GetWorldGenerations().Values.First();
            var party = new PartyState();
            _test.Eq(GameRuntimeQuestJournalBuilder.Build(party, catalog, generation).Entries.Count, 0,
                "日志不能泄露未接取任务和锁定后续任务。");
            var definition = catalog.GetQuestDefTyped("tutorial_first_blood");
            var objective = definition.Objectives[0];
            var state = new QuestState { quest_id = definition.QuestId };
            state.MarkAccepted(0);
            state.RecordObjectiveProgress(objective.ObjectiveId, 1, objective.TargetValue);
            party.SetActiveQuestState(state);
            var data = GameRuntimeQuestJournalBuilder.Build(party, catalog, generation);
            _test.Eq(data.Entries.Count, 1, "只展示已接的初阵。");
            _test.Eq(data.Entries[0].Objectives[0].Current, 1, "进度必须来自正式任务日志。");
            _test.True(data.Entries[0].Rewards.Any(r => r.Contains("50")), "详情保留正式奖励。");
            state.RecordObjectiveProgress(objective.ObjectiveId, 1, objective.TargetValue);
            _test.Eq(data.Entries[0].Objectives[0].Current, 1, "展示 DTO 应与可变任务状态隔离。");

            window = GD.Load<PackedScene>("res://scenes/ui/quest_journal_window.tscn").Instantiate<QuestJournalWindow>();
            Root.AddChild(window);
            await ToSignal(this, SignalName.ProcessFrame);
            foreach (Vector2I resolution in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                Root.Size = resolution;
                window.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                window.ShowWindow(data);
                await ToSignal(this, SignalName.ProcessFrame);
                await ToSignal(this, SignalName.ProcessFrame);
                _test.True(window.GetNode<Control>("FullPanel").GetGlobalRect().IsEqualApprox(window.GetGlobalRect()),
                    "任务界面必须占满视口，而非居中小弹窗。");
                _test.Eq(window.title_label.Text, definition.DisplayName, "选择任务显示正确详情。");
                _test.True(window.objectives_label.Text.Contains("1 / 3"), "目标进度在独立任务界面可见。");
                _test.True(window.GetGlobalRect().Encloses(window.GetNode<Button>("%CloseButton").GetGlobalRect()),
                    "全屏窗口关闭按钮应始终可见。");
            }
            window.filters.CurrentTab = 2;
            _test.Eq(window.quest_list.ItemCount, 0, "已完成筛选不能混入进行中任务。");
            _test.True(window.GetNode<Label>("%EmptyLabel").Visible, "空分类有明确空态。");
            window.filters.CurrentTab = 0;
            _test.Eq(window.quest_list.ItemCount, 1, "切回全部恢复记录。");
            int closed = 0;
            window.closed += () => closed++;
            window.GetNode<Button>("%CloseButton").EmitSignal(BaseButton.SignalName.Pressed);
            _test.Eq(closed, 1, "关闭请求经信号返回世界 owner。");
            _test.Eq(party.GetActiveQuestState(definition.QuestId).GetObjectiveProgress(objective.ObjectiveId), 1,
                "浏览与筛选不能改变任务进度。");

            state.MarkCompleted(2);
            party.SetQuestState(definition.QuestId, state);
            var claimable = GameRuntimeQuestJournalBuilder.Build(party, catalog, generation);
            _test.Eq(claimable.Entries[0].Stage, QuestJournalStage.Claimable, "达成目标与已领奖必须区分。");
            _test.True(claimable.Entries[0].NextStep.Contains("交付"), "待交付任务说明下一步。");
            window.HideWindow();
            window.ShowWindow(QuestJournalWindowData.Empty);
            _test.True(window.GetNode<Label>("%EmptyLabel").Visible, "新档展示空日志。");
        }
        catch (Exception e) { _test.Fail(e.ToString()); }
        finally
        {
            if (window != null) { window.QueueFree(); await ToSignal(this, SignalName.ProcessFrame); }
            RequestTestExit(_test.Finish("Quest journal window regression"));
        }
    }
}
