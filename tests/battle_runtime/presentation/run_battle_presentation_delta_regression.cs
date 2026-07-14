using System;
using Godot;

public partial class run_battle_presentation_delta_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestLogOnlyDeltaDoesNotDirtyBoard();
            TestLogOnlyRefreshTextUsesCurrentBattleTail();
            TestChangedCoordsStayConservative();
            TestPlacementWithoutUnitIdsStaysConservative();
            TestTimelineRefreshesAllUnitsWithoutFullBoard();
            TestUnitDeltaSkipsFullRuntimeLogScan();
            TestMergeFromPreservesCombinedFacts();
            TestCommandDeltaCaptureResetsBetweenCommands();
        }
        catch (Exception exception)
        {
            _test.Fail(exception.ToString());
        }

        RequestTestExit(_test.Finish("Battle presentation delta regression"));
    }

    private void TestLogOnlyDeltaDoesNotDirtyBoard()
    {
        var batch = new BattleEventBatch();
        batch.AddLogLine("presentation-only log");

        BattlePresentationDelta delta = BattlePresentationDeltaFactory.Create(batch);

        _test.True(delta.IsLogOnly, "纯日志 batch 应投影成 log-only delta。");
        _test.False(delta.RequiresPanelRefresh, "纯日志 delta 不应要求 BattleMapPanel 刷新。");
        _test.False(delta.RequiresFullBoardRefresh, "纯日志 delta 不应触发棋盘全量刷新。");
        _test.Eq(
            delta.ToLegacyRefreshMode(),
            BattleRefreshMode.Overlay,
            "旧 projection 仍应把 log-only 表示为非 full refresh。"
        );
    }

    private void TestLogOnlyRefreshTextUsesCurrentBattleTail()
    {
        var state = new BattleState();
        state.log_entries.Add("old");
        state.log_entries.Add("first");
        state.log_entries.Add("second");
        state.log_entries.Add("latest");

        _test.Eq(
            BattleMapPanel.BuildRecentLogText(state),
            "first\nsecond\nlatest",
            "log-only 轻量刷新应更新面板内最近三条日志且不依赖完整 HUD snapshot。"
        );
    }

    private void TestChangedCoordsStayConservative()
    {
        var batch = new BattleEventBatch();
        batch.AddChangedUnitId("unit_a");
        batch.AddChangedCoord(new Vector2I(3, 4));

        BattlePresentationDelta delta = BattlePresentationDeltaFactory.Create(batch);

        _test.True(delta.RequiresFullBoardRefresh, "未分类 changed coord 在 Phase 1 应保守全刷。");
        _test.Eq(delta.ChangedUnitIds.Count, 1, "delta 应携带 changed unit id。");
        _test.Eq(delta.ChangedCoords.Count, 1, "delta 应携带 changed coord。");
    }

    private void TestPlacementWithoutUnitIdsStaysConservative()
    {
        var batch = new BattleEventBatch();
        batch.MarkUnitPlacementChanged();

        BattlePresentationDelta delta = BattlePresentationDeltaFactory.Create(batch);

        _test.True(
            delta.RequiresFullBoardRefresh,
            "未携带 changed unit/coord 的 placement fact 不得误走空增量刷新。"
        );
    }

    private void TestTimelineRefreshesAllUnitsWithoutFullBoard()
    {
        var batch = new BattleEventBatch();
        batch.MarkTimelineChanged();

        BattlePresentationDelta delta = BattlePresentationDeltaFactory.Create(batch);

        _test.True(delta.RequiresPanelRefresh, "timeline fact 应刷新 HUD 与单位 active styling。");
        _test.False(delta.RequiresFullBoardRefresh, "timeline fact 不应重铺 TileMap。");
        _test.Eq(
            delta.ChangedUnitIds.Count,
            0,
            "空 changed ids 是 refresh-all-units contract，不应伪造单位 id。"
        );
    }

    private void TestMergeFromPreservesCombinedFacts()
    {
        var target = new BattleEventBatch();
        target.AddChangedUnitId("unit_a");
        var source = new BattleEventBatch();
        source.AddChangedUnitId("unit_a");
        source.AddChangedUnitId("unit_b");
        source.AddLogLine("merged log");
        source.phase_changed = true;

        target.MergeFrom(source);

        _test.Eq(target.ChangedUnitIdsTyped.Count, 2, "MergeFrom 应去重合并 changed units。");
        _test.Eq(target.LogLinesTyped.Count, 1, "MergeFrom 应保留日志事实。");
        _test.True(target.phase_changed, "MergeFrom 应保留 phase_changed。");
        _test.True(
            (target.ChangeFlags & BattleChangeFlags.Log) != 0
                && (target.ChangeFlags & BattleChangeFlags.Phase) != 0
                && (target.ChangeFlags & BattleChangeFlags.UnitState) != 0,
            "MergeFrom 应合并全部 domain flags。"
        );
    }

    private void TestUnitDeltaSkipsFullRuntimeLogScan()
    {
        var unitBatch = new BattleEventBatch();
        unitBatch.AddChangedUnitId("unit_delta");
        BattlePresentationDelta unitDelta = BattlePresentationDeltaFactory.Create(unitBatch);
        _test.False(
            WorldMapSystem.ShouldRefreshBattleLogDock(true, unitDelta),
            "已显示棋盘的 unit-only delta 不应扫描完整 runtime battle log。"
        );

        var logBatch = new BattleEventBatch();
        logBatch.AddLogLine("new log");
        _test.True(
            WorldMapSystem.ShouldRefreshBattleLogDock(
                true,
                BattlePresentationDeltaFactory.Create(logBatch)
            ),
            "log delta 必须刷新 runtime battle log。"
        );
        _test.True(
            WorldMapSystem.ShouldRefreshBattleLogDock(false, unitDelta),
            "首次显示棋盘时即使是 unit delta 也必须初始化 runtime battle log。"
        );
        _test.True(
            WorldMapSystem.ShouldRefreshBattleLogDock(true, null),
            "legacy render path 没有 typed delta 时应保守刷新 runtime battle log。"
        );
    }

    private void TestCommandDeltaCaptureResetsBetweenCommands()
    {
        using var runtime = new GameRuntimeFacade();
        var batch = new BattleEventBatch();
        batch.AddChangedUnitId("command_unit");

        runtime.CaptureLastCommandBattlePresentationDelta(batch);
        BattlePresentationDelta captured = runtime.GetLastCommandBattlePresentationDelta();
        _test.True(captured.HasChanges, "命令 batch 应保存本次 typed presentation delta。");
        _test.Eq(captured.ChangedUnitIds.Count, 1, "命令 delta 应保留 changed unit id。");

        runtime.ResetLastCommandBattlePresentationDelta();
        _test.False(
            runtime.GetLastCommandBattlePresentationDelta().HasChanges,
            "下一条命令开始前必须清空旧 delta，避免 selection-only 命令复用脏结果。"
        );

        var timelineOnly = new BattleEventBatch();
        timelineOnly.MarkTimelineChanged();
        _test.True(
            runtime.BatchHasUpdates(timelineOnly),
            "facade update gate 必须接受 flag-only batch，不能在 factory 前吞掉 timeline fact。"
        );
    }
}
